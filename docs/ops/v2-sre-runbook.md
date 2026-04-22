# v2 SRE Runbook -- `az-ai-v2` 2.0.0

**Owner:** Frank Costanza (SRE)
**Baseline:** commit `a0ca066` on `main` (v2.0.0 release window).
**Audience:** on-call / release-duty engineer at 3am. Grep-friendly. No prose padding.
**Scope:** operations, SLOs, telemetry decoding, incident response, rollback.
**Not in scope:** security posture (see [`docs/security-review-v2.md`](../security-review-v2.md), Newman), cost tuning (see [`docs/cost-optimization.md`](../cost-optimization.md), Morty), perf-gate policy (see [`docs/perf-baseline-v2.md`](../perf-baseline-v2.md), Bania).

> SERENITY NOW! -- then, a clipboard.

---

## Table of contents

1. [Service summary](#1-service-summary)
2. [SLOs / SLIs](#2-slos--slis)
3. [Telemetry decoder](#3-telemetry-decoder)
4. [Incident response playbook](#4-incident-response-playbook)
5. [Rollback procedure](#5-rollback-procedure)
6. [Known limitations at 2.0.0](#6-known-limitations-at-200)
7. [On-call cheat sheet](#7-on-call-cheat-sheet)

---

## 1. Service summary

`az-ai-v2` is a single-binary command-line client for Azure OpenAI Chat Completions. It is invoked per-process by end users, Espanso, AutoHotkey, and CI pipelines. There is no long-running server; "up" is defined by **exit-code semantics**, not HTTP availability:

- `0` success · `1` generic error · `2` config/validation · `3` network/auth · `99` internal · `130` SIGINT (ref [`docs/migration-v1-to-v2.md`](../migration-v1-to-v2.md) §2).
- The AOT binary runs offline for `--help`, `--version`, `--estimate` (no credential or endpoint resolution). Real-call paths require `AZUREOPENAIENDPOINT` + `AZUREOPENAIAPI`.
- On-call owns: the shipping AOT binary's startup latency, telemetry correctness when opted-in, rollback mechanics. On-call does **not** own Azure OpenAI platform availability -- that's Microsoft's SLA.

---

## 2. SLOs / SLIs

All numbers are grounded in `docs/perf-baseline-v2.md` §3.3 (AOT, linux-x64, reference hardware `AB-DM4MFJ4`). Ratios are v2/v1 where relevant; absolute ceilings are on reference hardware -- CI runners will re-baseline. Error budgets assume a rolling **30-day window**.

### SLO-1 -- Cold-start p95 (`--help`, AOT)
- **SLI:** wall-clock ms from fork/exec to exit, 50-run rolling window from `scripts/bench.sh --aot` on CI reference runner. *(⚠️ **Planned.** Today's `scripts/bench.sh` times `dotnet <dll>`, not the AOT binary, and has no `--aot` flag -- it is scheduled under [`bania-v2-03`]. Current AOT SLI is captured via `python3 scripts/bench.py dist/aot/<bin>`. See [`docs/audits/docs-audit-2026-04-22-bania.md`](../audits/docs-audit-2026-04-22-bania.md) H2.)*
- **SLO target:** p95 ≤ **25 ms** absolute, and ≤ **1.25× v1** (baseline 15.30 ms → ceiling ≈ 19.1 ms; 25 ms is the hardware-independent ceiling). Observed at release: 18.78 ms.
- **Alert threshold:** page when 3 consecutive CI runs exceed 22 ms p95, or any single run exceeds 28 ms.
- **Error budget:** 5% of CI runs may exceed target ⇒ ≈ 1.5 runs/day on the hourly cadence. Burn >50% in a week → freeze perf-affecting merges until Bania signs.

### SLO-2 -- `--version --short` p95 (Gate-2 command)
- **SLI:** same harness, `--version --short` scenario.
- **SLO target:** p95 ≤ **20 ms** absolute, and ≤ **1.25× v1** (v1 p95 16.34 ms; observed v2 18.25 ms, ratio 1.12×).
- **Alert threshold:** page on 2 consecutive CI runs > 22 ms p95.
- **Error budget:** 2% of runs. This is the tightest gate -- Espanso's hot path.

### SLO-3 -- `--estimate` p95 (offline path)
- **SLI:** `--estimate "hello world"` under AOT, 50-run window.
- **SLO target:** p95 ≤ **50 ms** absolute. Observed 15.48 ms at release (§3.3). <!-- TODO: Bania to ratify 50 ms or tighten to 25 ms once 2.0.1 data lands -->
- **Alert threshold:** p95 > 40 ms for 2 consecutive runs.
- **Error budget:** 5% of runs. Covers CI budget-gate consumers.

### SLO-4 -- Real-call success rate (opt-in telemetry)
- **SLI:** among users who enabled `--telemetry` / `AZ_TELEMETRY=1`, the ratio of `az.chat.request` / `az.agent.request` spans with `status=Ok` to total spans. Classify out of budget: user/auth errors (exit `2`, `3` due to 401/403), client-side aborts (SIGINT/130).
- **SLO target:** ≥ **99.5%** weekly, excluding user-classified failures.
- **Alert threshold:** < 99.0% over any 24h rolling window.
- **Error budget:** 0.5% ≈ 3.5 h of unavailability / 30 d. <!-- TODO: Bania/Costanza to backfill the first 30-day baseline once telemetry has fleet data; the 99.5% target is provisional, based on Azure OpenAI's own 99.9% regional SLA minus client-path overhead -->

### SLO-5 -- Cost regression (per-100k-completion-token spend)
- **SLI:** rolling 7-day sum of `usd` field in stderr cost events (per `model`, per `mode`). Compared week-over-week.
- **SLO target:** delta ≤ **+10%** vs prior week at fixed model/mode/traffic mix.
- **Alert threshold:** >+15% over 48h on any model.
- **Error budget:** two breaches per quarter. Third breach → Morty pauses feature merges that touch prompting/caching/model-default code until root-caused.

### SLO-6 -- Crash rate
- **SLI:** exit code `99` (internal) or `139/134/137` (SIGSEGV/SIGABRT/SIGKILL) as a fraction of all invocations reported via telemetry. Exit `1` is not counted here -- it's generic/user-classified.
- **SLO target:** ≤ **0.1%** weekly.
- **Alert threshold:** > 0.3% over 24h, or any single SIGSEGV in CI.
- **Error budget:** 0.1% ≈ ~7 minutes' worth of 1-rps traffic per 30d. A single repro'd crash in a week consumes the full budget -- crashes block releases.

---

## 3. Telemetry decoder

Authoritative source: [`docs/observability.md`](../observability.md). This section is the operator-side decoder ring.

### 3.1 Opt-in matrix

| Trigger | Spans | Meters | Cost events (stderr) | Default |
|---|:---:|:---:|:---:|:---:|
| (none) | -- | -- | -- | **off** |
| `--telemetry` | ✅ | ✅ | ✅ | off |
| `AZ_TELEMETRY=1` / `true` / `yes` | ✅ | ✅ | ✅ | off |
| `--otel` | ✅ | -- | -- | off |
| `--metrics` | -- | ✅ | ✅ | off |
| `--raw` (any combo) | suppressed on stdout | suppressed on stdout | **still on stderr** | -- |

`AZ_TELEMETRY` parsing is case-insensitive; any other value keeps telemetry off. See [`azureopenai-cli-v2/Observability/Telemetry.cs:80`](../../azureopenai-cli-v2/Observability/Telemetry.cs) for the `IsEnabled` contract.

### 3.2 Span catalogue

| Span | Kind | Tags | Emitter |
|---|---|---|---|
| `az.chat.request` | Client | `az.model`, `az.mode=standard`, `az.raw` | standard path |
| `az.agent.request` | Client | `az.model`, `az.mode=agent`, `az.raw` | `--agent` / persona path |
| `az.ralph.iteration` | Internal | `ralph.iteration`, `ralph.max_iterations` | Ralph mode |

OTLP target: `OTEL_EXPORTER_OTLP_ENDPOINT`, default `http://localhost:4317`.

### 3.3 Meter catalogue

| Instrument | Type | Unit | Tags |
|---|---|---|---|
| `azai.chat.duration` | Histogram | s | `model` |
| `azai.tokens.input` | Counter | tokens | `model`, `mode` |
| `azai.tokens.output` | Counter | tokens | `model`, `mode` |
| `azai.cost.usd` | Histogram | USD | `model`, `mode` |
| `azai.ralph.iterations` | Histogram | iterations | -- |
| `azai.tool.invocations` | Counter | invocations | -- |

### 3.4 Cost-event schema (stderr, one JSON line per completed LLM request)

Source of truth: [`azureopenai-cli-v2/Observability/CostEvent.cs`](../../azureopenai-cli-v2/Observability/CostEvent.cs), priced by [`azureopenai-cli-v2/Observability/CostHook.cs`](../../azureopenai-cli-v2/Observability/CostHook.cs).

```json
{"ts":"2026-04-20T12:34:56.789Z","kind":"cost","model":"gpt-4o-mini","input_tokens":1200,"output_tokens":340,"usd":0.000384,"mode":"standard"}
```

| Field | Type | Notes |
|---|---|---|
| `ts` | ISO-8601 UTC (`"O"` round-trip) | deterministic format -- safe to sort lexicographically |
| `kind` | string -- always `"cost"` | reserved namespace; other `kind` values may ship later |
| `model` | string | deployment name; matches `AZUREOPENAIMODEL` / `--model` |
| `input_tokens` | int | prompt tokens |
| `output_tokens` | int | completion tokens |
| `usd` | number \| **null** | `null` ⇒ model not in price table. **Never faked.** See `CostHook.CalculateCost` ([`CostHook.cs:39`](../../azureopenai-cli-v2/Observability/CostHook.cs)). |
| `mode` | `"standard"` \| `"agent"` \| `"ralph"` | mode selector |

**Known-priced models** (default table, [`CostHook.cs:20-30`](../../azureopenai-cli-v2/Observability/CostHook.cs)): `gpt-4o-mini`, `gpt-5.4-nano`, `gpt-4o`, `gpt-4.1`, `Phi-4-mini-instruct`, `Phi-4-mini-reasoning`, `DeepSeek-V3.2`, `o1-mini`. Override via `AZAI_PRICE_TABLE=/path/to/prices.json`.

### 3.5 Data handling / PII posture

**What is NEVER emitted** (do not relax without an ADR):

- API keys (`AZUREOPENAIAPI`) -- never logged, never in spans, never in cost events.
- Endpoint URLs -- `AZUREOPENAIENDPOINT` may contain tenant identifiers in the host; **keep out of telemetry**. If future spans add endpoint tags, redact host per ADR.
- User prompt text or completion text -- not emitted on any path. Token **counts** only.
- File paths from `--persona` / `~/.squad/history/*` -- surface errors by `safeName`, not raw path (see [`PersonaMemory.cs:99,105,111,141`](../../azureopenai-cli-v2/Squad/PersonaMemory.cs)).
- `.squad.json` content, `~/.azureopenai-cli.json` content, stdin content.

**What is emitted** (opt-in only): span names + tags listed §3.2, meter values §3.3, cost-event fields §3.4. Everything fits on one stderr line per request.

### 3.6 Tailing events

**Local audit** (proves wire-up, writes one cost line to stderr, no OTLP collector required):

```bash
AZ_TELEMETRY=1 az-ai-v2 --metrics --raw "hi" 2> >(grep '"kind":"cost"')
```

**Local tail** (continuous stderr filter):

```bash
AZ_TELEMETRY=1 az-ai-v2 --metrics "$@" 2>&1 >/dev/null | tee -a /var/log/az-ai-cost.jsonl
```

**Via OTLP** (spans + meters, collector on localhost):

```bash
export OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:4317
AZ_TELEMETRY=1 az-ai-v2 --agent "plan a migration"
# spans land in the collector; cost events still on stderr
```

---

## 4. Incident response playbook

Every playbook: **Detect → Diagnose → Mitigate → Follow-up.** Keep it short.

### 4.1 High cost spike (SLO-5 breach, > +15% in 48h)

- **Detect:** `azai.cost.usd` week-over-week delta alert, or Morty's weekly report.
- **Diagnose (in order):**
  1. Same model? Check `model` tag -- sudden shift to `gpt-4o` / `gpt-4.1` from a cheaper default is most-common root cause.
  2. Prompt length inflation? Compare `azai.tokens.input` histogram mean; if ≥ 2× last week, someone grew a system prompt or persona memory.
  3. Cache disabled? Check downstream provider-side cache -- v2 does not cache locally.
  4. Ralph divergence? If `mode=ralph` share jumped, look at `azai.ralph.iterations` p95.
- **Mitigate:** revert the prompt/persona PR; pin `AZUREOPENAIMODEL` back to the prior default; if Ralph, cap `--max-iterations` fleet-wide via env var guidance; if a specific user, recommend `--estimate` + `AZAI_PRICE_TABLE`.
- **Follow-up:** file issue, attach 7-day cost-event sample (PII-clean -- only `model`, `mode`, tokens, usd).

### 4.2 Auth failures across the fleet (exit code `3` spike)

- **Detect:** span `status=Error` with auth signature, or user-reported 401/403.
- **Diagnose:**
  1. Is `AZUREOPENAIAPI` rotated? Check Azure portal. `KEY` vs `API` suffix trap -- see [`docs/prerequisites.md`](../prerequisites.md).
  2. Is `AZUREOPENAIENDPOINT` still valid? Tenant move / region rename changes the host.
  3. Regional Azure OpenAI outage? Check `https://status.azure.com` for Cognitive Services.
  4. api-version mismatch -- see FR-017 history (CHANGELOG v1.9.1).
- **Mitigate:** rotate key; update env via the recovery steps in [`docs/prerequisites.md`](../prerequisites.md); if regional outage, route users to a secondary deployment name.
- **Follow-up:** if rotation drills are overdue, schedule one (Frank Costanza / Newman).

### 4.3 Tool-call storm (agent loop runaway)

- **Detect:** `azai.ralph.iterations` p95 climbing, or user reports "it's been running for 20 minutes." Iteration cap is 50 (see [`Program.cs:785`](../../azureopenai-cli-v2/Program.cs)), tool-round cap 20 (`DEFAULT_MAX_AGENT_ROUNDS`, [`Program.cs:625`](../../azureopenai-cli-v2/Program.cs)).
- **Diagnose:**
  1. Bad system prompt / persona that instructs "keep retrying"? Dump the persona: `az-ai-v2 --persona <name> --estimate ""` will resolve it without network.
  2. Broken tool? Check `azai.tool.invocations` -- which tool is looping.
  3. Model regression producing malformed tool calls?
- **Mitigate:** tell user to `Ctrl-C` (honored, exit 130); reduce `--max-rounds` (e.g. `--max-rounds 3`); downgrade the model; disable the offending persona in `.squad.json`.
- **Follow-up:** adversarial repro in `tests/chaos/10_ralph_depth.sh`. Ralph-mode safety checklist applies to any PR touching the loop.

### 4.4 Crash / unhandled exception (exit `99`, `139`, `134`, `137`)

- **Detect:** any non-zero exit outside the user-classified set (see §1). `dmesg` on Linux for SIG* exits.
- **Diagnose:**
  1. Collect repro: exact `argv`, stdin size (`wc -c`), relevant env (`AZUREOPENAIMODEL`, `AZ_TELEMETRY`, `AZAI_PRICE_TABLE`). **Do not collect `AZUREOPENAIAPI`.**
  2. Check if already in FDR's F-series ([`docs/chaos-drill-v2.md`](../chaos-drill-v2.md)). F1/F2/F3 are fixed as of `a0ca066`; F4-F8 are 🟡.
  3. AOT-only? Reproduce against framework-dependent `dotnet az-ai-v2.dll` to isolate trim/ILC regressions.
- **Mitigate:** if the user can, fall back to v1 per §5; if the crash is persona-triggered, run with `--persona off` or delete `.squad/history/<name>.md`.
- **Follow-up:** file issue using the chaos-drill template; add a repro to `tests/chaos/` if new; check SLO-6 burn.

### 4.5 Cache poisoning / disk bloat

- **Detect:** user reports `~/.cache/azureopenai-cli/` > 50 MB, or slow `--help`. v2 does minimal on-disk caching -- any growth > 50 MB is anomalous.
- **Diagnose:** `du -sh ~/.cache/azureopenai-cli/ ~/.local/share/azureopenai-cli/ ~/.squad/` -- find the offender. Most commonly a runaway `.squad/history/<persona>.md` (covered by PersonaMemory tail-only read as of F1 fix, but the file itself can still grow on the *write* side).
- **Mitigate:**
  ```bash
  rm -rf ~/.cache/azureopenai-cli/
  # per-persona:
  : > .squad/history/<persona>.md
  ```
- **Follow-up:** file issue; if eviction logic is missing, cross-link to the FR tracker. <!-- TODO: Kramer to confirm whether v2 writes anything under ~/.cache/ at all; if not, this runbook entry is defensive only -->

### 4.6 `PersonaMemory` refusing on user's history file

User sees one of these on stderr (all are **non-fatal**, the CLI continues without history):

| Message (substring) | Cause | Source |
|---|---|---|
| `is not readable (...) -- skipping` | permission / IO error | [`PersonaMemory.cs:99,147,152`](../../azureopenai-cli-v2/Squad/PersonaMemory.cs) |
| `is not a regular file -- skipping` | directory, device, or non-seekable file | [`PersonaMemory.cs:105,122,130`](../../azureopenai-cli-v2/Squad/PersonaMemory.cs) |
| `resolves outside history dir (...) -- skipping` | symlink escape -- F2/F3 defense | [`PersonaMemory.cs:111`](../../azureopenai-cli-v2/Squad/PersonaMemory.cs) |
| `read timed out after Ns -- skipping` | device or blocking file (e.g. `/dev/urandom`) | [`PersonaMemory.cs:141`](../../azureopenai-cli-v2/Squad/PersonaMemory.cs) |

- **Diagnose:**
  1. `ls -la .squad/history/<name>.md` -- regular file? Size? Owner? Readable?
  2. `readlink -f .squad/history/<name>.md` -- does the target live under `<cwd>/.squad/history/`?
  3. If persona name contains `/`, `\`, or `..`, it will be rejected earlier (F3 defense). Rename the persona to `^[A-Za-z0-9_-]{1,64}$`.
- **Repro:** `tests/chaos/06_persona_memory.sh` (`06a` = 100 MB file; `11_persona_live.sh` `11a/b/c` for traversal / device / size).
- **Mitigate:** `: > .squad/history/<name>.md` to reset; or remove the symlink; or rename the persona.

---

## 5. Rollback procedure

**Rule of thumb:** `az-ai-v2` ships alongside v1 during the dual-tree window. After cutover, v2 is installed as `az-ai`; v1 remains pinnable. No data migration -- env vars, config files, `.squad/` are byte-compatible ([`docs/migration-v1-to-v2.md`](../migration-v1-to-v2.md) §2).

### 5.1 Decision tree -- "should I roll back now?"

Work top-down; stop at first match.

1. **SIGSEGV / corruption / data loss affecting ≥ 1 user?** → roll back immediately.
2. **Crash rate > 1%** (10× SLO-6 target) across telemetry-enabled fleet for > 1h? → roll back.
3. **Fleet-wide auth failure caused by v2 wire change?** → roll back. (v2 preserves the v1 wire; if this fires, it's a regression.)
4. **Cost regression > +50% week-over-week** with a known v2-introduced cause (prompt change, new default)? → roll back the offending commit, not the release, if feasible; otherwise roll back the release.
5. **Perf SLO-1/SLO-2 breach > 2× target** reproducible on Espanso/AHK hosts? → roll back.
6. **Single-user issue, workaround exists (`--persona off`, `--max-rounds 1`, env var tweak)?** → **do not** roll back; hotfix or doc workaround.
7. **`--schema`, arm64, or Windows-CI complaint?** → do not roll back; these are documented known limitations (§6).

### 5.2 Mechanics (post-cutover)

Homebrew:
```bash
brew uninstall azure-openai-cli
brew install schwartzkamel/tap/azure-openai-cli@1.9.1
brew pin azure-openai-cli@1.9.1
```

Scoop (Windows):
```powershell
scoop uninstall azure-openai-cli
scoop install azure-openai-cli@1.9.1
scoop hold azure-openai-cli
```

Nix:
```bash
# pin via flake ref -- adjust <rev> to the v1.9.1 commit hash
nix profile install github:SchwartzKamel/azure-openai-cli/v1.9.1
```
<!-- TODO: Bob Sacamano to confirm the actual flake ref / tap name is published at v1.9.1; the versioned Homebrew/Scoop manifests and Nix flake are the 2.0.1 packaging sweep per release-notes-v2.0.0.md §Known limitations. Until then, the "universal" rollback is the GitHub Release tarball: https://github.com/SchwartzKamel/azure-openai-cli/releases/tag/v1.9.1 -->

Manual / CI (GitHub Release tarball -- always works, zero dependencies):
```bash
curl -LO https://github.com/SchwartzKamel/azure-openai-cli/releases/download/v1.9.1/az-ai-linux-x64.tar.gz
tar xf az-ai-linux-x64.tar.gz
install -m 0755 az-ai /usr/local/bin/az-ai
```

Container:
```bash
docker pull ghcr.io/schwartzkamel/azure-openai-cli:1.9.1
# re-tag as `latest` locally if needed
```

### 5.3 Validating v1 is serving

```bash
az-ai --version --short     # → 1.9.1 (must NOT print 2.x.x)
az-ai --estimate "hi"       # → v1 has no --estimate; expect exit 2 "unknown flag". This PROVES v1 is live.
# For v2: az-ai-v2 --version --short → 2.0.0 (dual-tree confirmation)
```

If `az-ai --version --short` prints `2.x.x`, the rollback did not take -- check `which az-ai`, shell hash (`hash -r`), package-manager symlinks.

### 5.4 Comms template (release notes / GitHub Discussions)

```
Title: [Rolled back] az-ai 2.0.0 → 1.9.1

We're rolling back v2.0.0 to v1.9.1 due to <one-line impact>, observed
<date/time UTC>. Impact: <who, how many, what broke>. Mitigation in place:
v1.9.1 is now the latest release on Homebrew / Scoop / GHCR. No config or
env changes are required -- v1.9.1 reads the same AZUREOPENAIENDPOINT /
AZUREOPENAIAPI / AZUREOPENAIMODEL / ~/.azureopenai-cli.json / .squad/ as v2.

Pin command:   brew install schwartzkamel/tap/azure-openai-cli@1.9.1
Tracking:      <issue link>
Post-mortem:   will be posted within 72h (Festivus rules apply).

-- on-call
```

---

## 6. Known limitations at 2.0.0

Do not file these as incidents. Pointers only -- authoritative copies live upstream.

- **`--schema <json>` wire enforcement deferred to 2.1.x.** Flag parses, does not yet emit `response_format`. See [`docs/release-notes-v2.0.0.md` §Known limitations](../release-notes-v2.0.0.md).
- **Windows CI not enabled.** Windows binaries manually validated. Ref release notes.
- **arm64 release matrix rolls to 2.0.1.** Pin `v1.9.1` for arm64 until then.
- **Multi-provider (FR-014) is a spike.** Azure-OpenAI-only in 2.0.0.
- **AOT binary size 1.456× v1** (12.91 MB vs 8.86 MB v1.9.1) -- passes the 1.5× size gate without a waiver. Initial v2 build measured 1.625×; the AOT trim in commit `056920f` (`OptimizationPreference=Size` + `StackTraceSupport=false`) brought it to shipped. This is the figure to quote on calls. See `docs/aot-trim-investigation.md`.
- **Homebrew / Scoop / Nix manifests lag one release.** `packaging/` still pins v1.8.1; v2.0.0 manifests land in 2.0.1 (Bob Sacamano). Affects rollback mechanics §5.2 -- prefer the GitHub Release tarball until then.
- **Framework-dependent (`dotnet <dll>`) startup is ~1.5-1.7× v1.** Not the shipping form; not a gate. Users who ship the DLL rather than the AOT binary: use AOT.
- **FDR 🟡 findings F4-F8** (`docs/chaos-drill-v2.md`) are documented accepted risks for 2.0.0 -- not runtime incidents.

---

## 7. On-call cheat sheet

Copy/paste. Everything here is safe on a box with zero env config.

```bash
# --- smoke tests (no network, no credentials) ---
az-ai-v2 --version --short                 # → 2.0.0
az-ai-v2 --help | head -5                  # proves binary is intact
az-ai-v2 --estimate "test"                 # proves binary + offline path + price table

# --- telemetry wire-up (writes one JSON line to stderr) ---
AZ_TELEMETRY=1 az-ai-v2 --metrics --raw "hi" 2> >(grep '"kind":"cost"' >&2)

# --- real call (requires env) ---
AZUREOPENAIENDPOINT=... AZUREOPENAIAPI=... AZUREOPENAIMODEL=gpt-4o-mini \
  az-ai-v2 --raw "ping"

# --- log tail & event grep ---
tail -F /var/log/az-ai-cost.jsonl | jq -c 'select(.kind=="cost")'
grep -E '"mode":"ralph".*"output_tokens":[0-9]{5,}' /var/log/az-ai-cost.jsonl  # find expensive ralph runs
grep -E '\[persona\] history file .* skipping' ~/.cache/az-ai/stderr.log       # persona refusals

# --- which binary is live? ---
which az-ai az-ai-v2 ; az-ai --version --short ; az-ai-v2 --version --short

# --- rollback one-liners (see §5.2 for pins/holds) ---
brew uninstall azure-openai-cli && brew install schwartzkamel/tap/azure-openai-cli@1.9.1
scoop uninstall azure-openai-cli && scoop install azure-openai-cli@1.9.1
docker pull ghcr.io/schwartzkamel/azure-openai-cli:1.9.1
curl -LO https://github.com/SchwartzKamel/azure-openai-cli/releases/download/v1.9.1/az-ai-linux-x64.tar.gz

# --- kill a stuck ralph loop ---
pgrep -fa 'az-ai-v2.*--ralph' ; kill -INT <pid>     # 130 exit; Ctrl-C-equivalent
# if unresponsive (F2-class symptom), escalate to SIGTERM:
kill -TERM <pid>

# --- persona first aid ---
: > .squad/history/<name>.md                        # reset history
ls -la .squad/history/ ; readlink -f .squad/history/*.md  # check for symlink escapes
```

---

**Maintainer:** Frank Costanza · **Review cadence:** monthly, and after every incident.
**Festivus post-mortem:** annual -- Airing of Grievances covers every incident of the year, what broke, what we learned, what's still broken. Feats of Strength optional.

> *I got a lotta problems with you people -- and now you're gonna hear about 'em.* Incident log starts fresh each January 1st.
