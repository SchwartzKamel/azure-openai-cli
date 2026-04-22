# v2 Dogfood Plan (Phase 7)

**Owners:** FDR (chaos / adversarial) + Kenny Bania (perf / regression gating) + all agents (usage validation)
**Status:** Pending -- runs after Phase 5 (Observability) and before Phase 6 (Cutover)
**Companion docs:** [`v2-migration.md`](v2-migration.md) · [`v2-cutover-checklist.md`](v2-cutover-checklist.md) · [`adr/ADR-004-agent-framework-adoption.md`](adr/ADR-004-agent-framework-adoption.md)

> I fuzzed the CLI flag parser for six hours. I found a crash on `--max-tokens=1e999`. Your move. -- FDR
>
> It's gold, Jerry! *Gold!* Unless the p99 moved. Did the p99 move? *Did you check the p99?* -- Bania

This plan is the speed-gate between "v2 passes its own tests" and "v2 replaces v1 on `main`." It is adversarial by design and measurement-obsessed by policy. A green dogfood window is a **precondition** for the cutover checklist ([`v2-cutover-checklist.md`](v2-cutover-checklist.md) §1).

---

## 1. Objectives

1. **Zero-regression on the hot path.** Cold start, TTFT, streaming throughput, and AOT binary size must stay within budget vs 1.9.1. A user of v1.9.1 cannot feel v2.0.0 -- except where we intentionally added opt-in features.
2. **Exhaustive persona coverage.** All 25 personas render and round-trip correctly through MAF's `AgentSession` + `AIContextProvider`. `.squad/history/<name>.md` files remain byte-identical in format.
3. **Edge-case surfacing.** FDR's evil-input catalogs and chaos scenarios must run against the real v2 build, not a mock.
4. **Real-endpoint validation.** Run against actual Azure OpenAI and Azure AI Foundry deployments, not just recorded fixtures. WSL is a first-class target because that is the primary user environment.
5. **Cost parity.** Morty confirms v2 does not emit additional completion calls, stealth retries, or hidden system prompts.

A dogfood window is not "a week of people using it." It is a scripted, measured, adversarial campaign with named owners, checklists, and artifacts.

---

## 2. Test matrix

Each cell must be exercised at least once. The cell owner files the result in the findings log (§7).

### 2.1 Modes × Models

Every mode must run against at least one model from each family.

| Mode | gpt-4o-mini | gpt-4o | gpt-5.4-nano | gpt-5.4 |
|------|-------------|--------|--------------|---------|
| `standard` (single response) | ✅ | ✅ | ✅ | ✅ |
| `--agent` (tool-calling loop) | ✅ | ✅ | ✅ | ✅ |
| `--ralph` (autonomous) | ✅ | -- | ✅ | ✅ |
| `--persona <name>` | ✅ | -- | ✅ | -- |

`--ralph` uses `RALPH_DEPTH=2` and a seeded validator script. `--persona` runs all 25 personas against at least one model (§2.2).

### 2.2 Persona coverage (all 25, at least once)

Main cast (5): Costanza, Kramer, Elaine, Jerry, Newman.
Supporting (20): Mr. Pitt, Mr. Lippman, J. Peterman, Puddy, Jackie Chiles, Morty Seinfeld, Bob Sacamano, Uncle Leo, Frank Costanza, The Maestro, Kenny Bania, Mickey Abbott, Sue Ellen Mischke, Keith Hernandez, Rabbi Kirschbaum, Babu Bhatt, Russell Dalrymple, Mr. Wilhelm, The Soup Nazi, FDR.

Per persona, validate:

- System prompt loads from `.squad/config.json` unchanged.
- First run creates `.squad/history/<name>.md` in the v1-compatible markdown format (byte-diff vs a v1.9.1 snapshot).
- Multi-turn session appends correctly; memory cap (32 KB) triggers truncation at the right boundary.
- `.squad/decisions.md` is appended to, not overwritten.
- Routing via `SquadCoordinator` keyword scoring returns the same persona on the same input as v1.9.1 (differential test).

### 2.3 Authentication paths

| Auth mode | Coverage |
|-----------|----------|
| `AZUREOPENAIAPI` env (API key) | Required -- primary user path |
| `DefaultAzureCredential` (AAD) | Required -- Costanza's directive post-MAF adoption |
| Azure AI Foundry endpoint (`PersistentAgentsClient`) | Required -- open question Q1 in [`v2-migration.md`](v2-migration.md) §Open questions |
| Invalid/expired creds | Required -- must fail cleanly with no secret leak (Newman reviews error output) |

### 2.4 Platforms

| Platform | Build | Priority | Notes |
|----------|-------|----------|-------|
| **Linux AOT** (`make publish-aot`) | x64 musl + glibc | P0 | Published artifact; Bania's primary bench target |
| **Linux JIT** (`dotnet run`) | -- | P0 | Developer loop |
| **Docker** (Alpine `linux-musl-x64`) | -- | P0 | Published container |
| **WSL** (Ubuntu 22.04 on Windows 11) | linux-x64 glibc | **P0 -- critical** | Maintainer's daily-driver environment; surfaces CRLF, path, and clock-skew bugs that pure Linux CI misses |
| macOS AOT | arm64 + x64 | P1 | Release artifact, lower user volume |
| Windows AOT | x64 | P1 | Pre-existing POSIX-path test gaps (see CHANGELOG 1.9.1); do not regress |

A finding that reproduces on WSL but not on clean Linux CI is **automatically P1 minimum** because it means our CI matrix is lying to us.

---

## 3. Performance gates (Bania)

All deltas measured against a locked 1.9.1 baseline captured at commit of the `v1.9.1` tag. Methodology per [`docs/benchmarks.md`](benchmarks.md): ≥ 30 warm-up iterations, ≥ 100 measured iterations, outlier rejection, pinned reference hardware.

> ⚠️ **Planned, not shipped.** The `scripts/bench.py` flags cited in the
> Measurement column below (`--cold`, `--iterations`, `--ttft`,
> `--endpoint`, `--stream`, `--duration`) are the target CLI surface
> once [`bania-v2-03`] promotes the current harness. Today's
> `scripts/bench.py` is a positional cold-start timer
> (`bench.py <binary> [-n RUNS] [-w WARMUP] [--args ...]`) -- no TTFT,
> no streaming, no budget mode. Track:
> [`docs/audits/docs-audit-2026-04-22-bania.md`](audits/docs-audit-2026-04-22-bania.md) C2.

| Metric | Budget vs 1.9.1 | Measurement (planned -- see note above) |
|--------|-----------------|-------------|
| Cold start (AOT, p95) | Δ ≤ **+10%** | `scripts/bench.py --cold --iterations 100` |
| TTFT (time-to-first-token, p95) | Δ ≤ **+5 ms** (absolute, not percent) | `scripts/bench.py --ttft --endpoint $AZUREOPENAIENDPOINT` |
| Streaming throughput (tokens/sec, p50) | Δ ≤ **−5%** | `scripts/bench.py --stream --duration 30s` |
| AOT binary size | Absolute ≤ **15 MB** (per-RID) | `ls -l dist/aot/AzureOpenAI_CLI` |
| Memory residency (peak RSS, standard mode) | Δ ≤ **+15%** | `/usr/bin/time -v` |
| Agent-mode tool round-trip (p95) | Δ ≤ **+5 ms** | Phase 0 pt 2 harness, re-run |

**Regression policy:** any metric over budget blocks cutover. A waiver requires Costanza + Bania + Lippman triple-sign and a named remediation ticket with a date.

Every PR into `azureopenai-cli-v2/` during the dogfood window must produce a PR-diff bench comment -- no silent perf changes.

---

## 4. Chaos scenarios (FDR)

All scenarios are scripted and seeded. Harnesses live in `tests/adversarial/` and run via `bash tests/adversarial/run-chaos-drill.sh --baseline 1.9.1`.

### 4.1 Streaming chaos

| Scenario | Trigger | Expected behavior |
|----------|---------|-------------------|
| Partial SSE frame | Mock server sends 512 bytes then EOF | Clean error to stderr; exit 1; no orphan tokens on stdout |
| Mid-stream disconnect | TCP RST after N tokens | Error surfaces; no panic; `--raw` still produces only the tokens received |
| Malformed JSON in `delta.content` | Inject `"content": "\uD83D"` (lone surrogate) | Graceful decode fallback; no crash; one `[WARN]` on stderr |
| 2 GB frame with missing delimiter | FDR's classic | Bounded-buffer rejection, no OOM, specific error |
| Slow-loris (1 byte / 10s) | Rate-limited byte stream | Client timeout fires before hanging forever |

### 4.2 Tool-call chaos

| Scenario | Trigger | Expected behavior |
|----------|---------|-------------------|
| Recursive tool loop | `delegate_task` calls itself at depth 4 | `MaxDepth` cap (3) holds; clean error; no unbounded recursion |
| Malformed tool output | Tool returns `"\0\0\0"` | Newman's output sanitizer strips NULs; result passed back safely |
| Tool returns 50 MB | `read_file` on huge file | Size cap triggers; truncation is explicit in output |
| Conflicting tool schemas | Two tools named `read_file` registered | `ToolRegistry.Create()` rejects at startup with a specific error |
| Shell injection retries | `shell_exec` input with `$(whoami)`, backticks, `<()`, `eval`, `exec` | All rejected per Newman's blocklist; tests in `ToolHardeningTests` stay green |

### 4.3 API chaos

| Scenario | Trigger | Expected behavior |
|----------|---------|-------------------|
| 429 storm | Mock endpoint returns 429 on 50% of calls | Backoff/retry (if any) bounded; eventual clean failure |
| 503 burst | 3 consecutive 503s | Same as above |
| TLS reset mid-request | Injection via proxy | Clean error; no secret in logs (Newman reviews) |
| DNS failure | `AZUREOPENAIENDPOINT` → unroutable host | Fast-fail with DNS-specific error |
| Response Unicode trap | Bidi override, NUL byte, zero-width chars | Rendered safely in terminal; no ANSI escape injection |

### 4.4 Persona / squad chaos

| Scenario | Trigger | Expected behavior |
|----------|---------|-------------------|
| Corrupt `.squad/config.json` | Trailing comma, NUL byte, truncated file | Clean error pointing at file and line; no `NullReferenceException` |
| Corrupt `.squad/history/<name>.md` | Truncated mid-UTF-8 codepoint | File detected as corrupt; new session starts; old file preserved as `.corrupt.bak` |
| Persona name collision | Two personas with same name in config | Startup error, no ambiguous-resolution silent pick |
| Oversized memory file | 10 MB history | 32 KB cap enforced; truncation is byte-safe (no split codepoints) |
| Race on `.squad/decisions.md` | Two agents append simultaneously | Lock / atomic append holds; no interleaved corruption |

### 4.5 SSRF / supply-chain (Newman lane, run by FDR)

| Scenario | Trigger | Expected behavior |
|----------|---------|-------------------|
| `web_fetch` → `http://169.254.169.254/` | AWS metadata endpoint | Rejected by private-IP filter |
| `web_fetch` → redirect to `127.0.0.1` | 302 to loopback | Post-redirect filter rejects |
| `web_fetch` → `file:///etc/passwd` | File scheme | Scheme allowlist rejects |
| Evil URL with embedded creds | `http://user:pass@example.com` | Creds stripped from logs |

**Triage rule:** every chaos finding gets severity-scored and routed -- Newman (security), Frank (reliability), Bania (perf), Kramer (implementation). FDR writes the failing test; FDR does **not** patch.

---

## 5. Cost gates (Morty)

Per-mode token budget measured by the Phase-5 OTel cost hook. Budgets are equality-with-tolerance against 1.9.1 on a fixed prompt corpus (`tests/fixtures/cost-prompts/`).

| Mode | Token budget vs 1.9.1 | Notes |
|------|-----------------------|-------|
| `standard` (one completion) | Δ ≤ **±2%** | MAF must not add stealth system prompts |
| `--agent` (tool round-trip) | Δ ≤ **±5%** | Slight variance acceptable if AF function-tool formatting differs marginally |
| `--ralph` (multi-iteration) | Δ ≤ **±5%** | `CheckpointManager` must not emit extra resume prompts |
| `--persona` | Δ ≤ **±2%** | `AIContextProvider` must not add framing tokens |

**Headline invariant:** MAF adoption must not emit extra completion calls. If v1 made N calls for prompt P, v2 makes N calls for prompt P. Any increase is a defect.

Morty publishes the cost-parity table as a release artifact. Any budget miss blocks cutover.

---

## 6. Success criteria

Dogfood exits green only when **all** are true:

- **Duration:** minimum **7 consecutive calendar days** of daily use, with at least one weekend-day session (different latency, different network posture).
- **Core contributors:** at least **3 distinct core contributors** have completed a full test-matrix pass (§2) on their own environment. At least one must be on WSL.
- **Regressions:** **zero P1** regressions outstanding. P2s may be outstanding if each has a named owner and a fix target.
- **Perf gates (§3):** all green against 1.9.1 baseline.
- **Chaos gates (§4):** all scenarios have run; zero crashes; every new failure mode has a regression test in `tests/adversarial/`.
- **Cost gates (§5):** all modes within budget; Morty's parity table published.
- **Persona sweep (§2.2):** all 25 personas exercised; `.squad/history/*.md` byte-format parity confirmed.
- **Real-endpoint validation:** integration tests have run against at least one live Azure OpenAI deployment **and** at least one Azure AI Foundry deployment.

---

## 7. Findings log

All findings land in a single GitHub project board: **"v2 Dogfood -- Findings"**. Each card has:

- **Title:** one-line reproduction.
- **Severity:** P0 / P1 / P2 / P3 per the table below.
- **Lane:** Newman (security), Frank (reliability), Bania (perf), Kramer (impl), Morty (cost), Elaine (docs).
- **Reproduction:** seed, command, environment (OS, build, model, auth mode).
- **Baseline behavior:** what v1.9.1 does on the same input.
- **Owner:** named, not `@team`.
- **Target:** fix before cutover / fix before 2.0.1 / waived.

### 7.1 Severity definitions

| Severity | Definition | Blocks cutover? |
|----------|------------|-----------------|
| **P0** | Data loss, credential leak, security regression, crash on `--raw` hot path | Yes -- immediate rollback of the offending change |
| **P1** | Regression on any §3 perf budget, `.squad/` corruption, any mode broken vs v1.9.1 | **Yes** -- must be fixed before tag |
| **P2** | Non-hot-path defect, cosmetic regression, missing error clarity | No -- fix in 2.0.1 with owner + date |
| **P3** | Quality-of-life, doc gap, nice-to-have | Tracked, not blocking |

### 7.2 Rollback trigger thresholds

During the dogfood window itself (pre-tag), these trigger a Phase-5 hold and force a Phase re-entry, not a rollback:

- Any **P0** finding.
- **≥ 2 P1** findings with no plausible fix in the remaining window.
- Any **cost parity miss > 10%** on the `standard` mode -- that is an architectural regression, not a tuning exercise.
- Any **perf metric over budget by > 2×** (e.g., cold start Δ > 20%) -- indicates wrong-shape change, not tunable noise.

Post-tag rollback triggers are defined in [`v2-cutover-checklist.md`](v2-cutover-checklist.md) §5.1.

---

## 8. Entry + exit criteria

### 8.1 Entry (can dogfood start?)

- [ ] Phase 5 (Observability) merged -- OTel + cost hook available for §5 measurements.
- [ ] v2 project builds green across the platform matrix (§2.4).
- [ ] 1.9.1 baseline snapshots captured and locked:
  - [ ] Bench artifacts (`docs/benchmarks/1.9.1/`)
  - [ ] Cost corpus output (`tests/fixtures/cost-prompts/1.9.1.json`)
  - [ ] `.squad/history/*.md` reference format snapshot
- [ ] Chaos harness in `tests/adversarial/` is runnable and seeded.
- [ ] Findings board created; severity/lane labels configured.
- [ ] At least 3 core contributors have v2 built locally and creds staged.

### 8.2 Exit (can cutover proceed?)

- [ ] All §6 success criteria green.
- [ ] Bania publishes final "Dogfood bench report" with 1.9.1 vs 2.0.0 tables.
- [ ] FDR publishes "Dogfood chaos report" -- scenarios run, outcomes, regression tests landed.
- [ ] Morty publishes "Dogfood cost parity report" -- per-mode deltas.
- [ ] Newman sign-off on security findings lane.
- [ ] Frank sign-off on reliability / SLO posture.
- [ ] Costanza final go/no-go for cutover.

On exit-green, Phase 6 ([`v2-cutover-checklist.md`](v2-cutover-checklist.md)) §1 preconditions are satisfied and the release PR may be opened.

---

**Maintained by:** FDR (chaos) + Bania (perf) + Frank Costanza (reliability) + Morty (cost)
**Review cadence:** Daily standup during the active window; final sign-off meeting at exit
**Successor:** [`v2-cutover-checklist.md`](v2-cutover-checklist.md)
