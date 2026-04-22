# FR-020 -- NVIDIA NIM Provider with Per-Trigger Routing (2B-first)

**Status:** Draft (Costanza, 2026-04-23)
**Related:** [FR-014](FR-014-local-preferences-and-multi-provider.md) (preferences schema home), [FR-018](FR-018-local-model-provider-llamacpp.md) (provider abstraction -- **hard dependency**), [FR-019](FR-019-gemma-cpp-direct-adapter.md) (sibling local-model FR), [FR-013](FR-013-mcp-client-and-server-support.md) (complexity reference)
**Implements:** [ADR-006 -- NVFP4 / NIM integration](../adr/ADR-006-nvfp4-nim-integration.md)
**Owner:** Costanza (PM / flagship framing) · Kramer (impl) · Newman (security) · Jackie (licensing) · Morty (cost telemetry) · Frank (reliability) · Bania (perf gates) · Mickey (failure UX)
**Target release:** v2.1 (post v2.0.0 cutover; ships release N+1 after FR-018 lands in v2.1-pre)

---

## 1. Problem Statement

Text expansion is a sub-500 ms game. Mickey's ergonomic bands are unforgiving:
*≤500 ms is instant, >1500 ms the user starts typing around you, >3000 ms they
give up.* Azure OpenAI, even on the best WAN day, pays an 800-1200 ms round-trip
floor before a single token arrives. A warm 2B NVFP4 model resident on a
Blackwell GPU returns first token in **15-40 ms** (Bania's numbers, §A).
For Espanso/AHK triggers that's the difference between "native snippet" and
"broken tool."

But "always local" is the wrong answer. A 2B model is *overqualified* for
`:aifix` (grammar correction is a near-deterministic transform) and
*outclassed* on `:aic` (code review is a reasoning task). Maestro's quality
matrix (§A) is uncompromising: `:aic` stays on Azure, period. A single-provider
policy -- local-for-everything OR cloud-for-everything -- gives up latency on
the trivial triggers or gives up quality on the hard ones. **The answer is
per-trigger routing: the right model for the right movement of the score.**

This FR is the concrete implementation of [ADR-006](../adr/ADR-006-nvfp4-nim-integration.md).
It adds an NVIDIA NIM provider (OpenAI-compat HTTP, served by the NIM
container), layers a per-trigger routing table on top of FR-018's provider
registry, and ships the 2B NVFP4 Gemma-4 path first -- with 9B as the flagship
demo target once the larger NVFP4 image is available.

## 2. Motivation / Competitive Gap

| Dimension | Today | With FR-020 |
|---|---|---|
| **Text-expansion TTFT** | 800-1200 ms (Azure WAN). | 15-40 ms warm (local 2B NVFP4). |
| **Per-trigger provider choice** | One provider per process. | Routing table: `:aifix` → local, `:aic` → Azure, length-gated `:aitldr`. |
| **Cost of iteration** | Every `:aifix` keystroke burns tokens. | Zero per-call cost on warm local; Azure only where quality demands it. |
| **Competitive differentiator** | fabric/aichat/llm/crush all ship local-model support; none ship per-trigger routing. | **No existing CLI does this.** This is the wedge. |
| **Privacy posture** | Grammar-fix prompts leave the machine. | Trivial transformations stay on-device; heavyweight reasoning goes cloud -- an auditable, configurable boundary. |

The differentiator is not "we have a local provider." That is table stakes as
of FR-018. The differentiator is **we ship the routing policy** so Espanso/AHK
users don't have to build it themselves. Bania's data justifies local-first on
short-rewrite. Maestro's quality gate justifies cloud-only on review. Morty's
cost model prefers local where quality permits. All three align on the same
routing table; this FR codifies it.

## 3. Rough Effort

**Small (S) -- ≈1.5 engineer-days on top of FR-018 landing.**

The C# surface is narrow: `NimProvider` is a thin subclass of FR-018's
OpenAI-compat provider (new base URL, bearer token, health probe). The
routing layer is a dictionary lookup plus a word-count heuristic.

Breakdown (Kramer):

- `NimProvider : IChatProvider` + provider-registry wiring: ~2 h.
- Preferences schema extension (`[providers.nim]`, `[routing]`, `[fallback]`):
  ~2 h.
- `--provider` / `--trigger` / `--fallback` / `--list-providers` flag
  parsing + precedence: ~2 h.
- Per-trigger routing resolver + length-gating heuristic: ~2 h.
- Health probe (150 ms timeout, 2 s in-proc cache): ~1 h.
- Newman-spec hardening (SSRF allow-list, bearer redaction, digest pin, ack
  file): ~3 h.
- ToolHardeningTests additions: ~2 h.
- `make install-nim-gemma-2b` one-shot + `az-ai-nim.service` systemd-user
  unit + docs: ~1 day.
- Espanso/AHK examples: ~2 h.

**Total: ~12 h C# + ~1 day infra/docs ≈ 1.5 dev-days.** Numbers assume FR-018
has shipped the `IChatProvider` contract, the `providers.*` preferences
block, and the `--provider` CLI flag. **If FR-018 slips, this FR slips.**

## 4. Proposed Solution

### 4.1 Architecture

```text
┌──────────────────────────────────────────────────────────────┐
│ CLI entry (Program.cs / v2)                                  │
│   flags → CliOptions → TriggerRouter                         │
└───────────────────────────┬──────────────────────────────────┘
                            ▼
               ┌───────────────────────────┐
               │  TriggerRouter (new)      │  resolves
               │                           │  (trigger, input_len)
               │   routing[trigger]        │  → provider_name
               │   ┤ length-gate (nim:N)   │
               │   ┤ default_provider      │
               └───────────┬───────────────┘
                           ▼
                  ProviderRegistry  ◄── FR-018
                           │
          ┌────────────────┼────────────────┬──────────────────┐
          ▼                ▼                ▼                  ▼
   AzureOpenAIProvider  NimProvider   OllamaProvider   …future providers
   (existing)           (NEW, FR-020) (FR-018)
```

The routing layer sits **above** the provider registry. Providers themselves
know nothing about triggers; they just speak OpenAI-compat chat-completions.
`TriggerRouter` is ~80 LOC of pure logic -- no HTTP, no I/O -- and is trivially
unit-testable with a mock `IChatProvider`.

`NimProvider` is a **subclass** of FR-018's generic OpenAI-compat provider.
It adds:

- A required `auth_token_file` (bearer token, never in env, never in
  preferences file, never in logs).
- A health probe against `/v1/health/ready` with a 150 ms timeout and 2 s
  in-proc cache.
- A required `digest` field (sha256 of the pinned NIM image; advisory in the
  provider, load-bearing in the install script).
- SSRF allow-list defaulting to loopback only.
- Gemma ToU ack-file enforcement (§6).

### 4.2 Preferences schema (extension of FR-014)

```toml
[providers.nim]
endpoint          = "http://localhost:8000/v1"
model             = "nvidia/gemma-4-2b-it-nvfp4"   # 9B on bigger rigs
auth_token_file   = "~/.config/az-ai/nim.token"
health_endpoint   = "/v1/health/ready"
health_timeout_ms = 150
health_cache_ms   = 2000
digest            = "sha256:<pinned>"              # see nim-install docs

[routing]
# Per-trigger overrides. Missing triggers fall through to default_provider.
grammar   = "nim"         # :aifix  -- 🟢 per Maestro
rewrite   = "nim"         # :airw   -- 🟢 per Maestro
summarize = "nim:400"     # :aitldr -- 🟡 length-gated: nim if input ≤400 tok
explain   = "azure"       # :aiexp  -- 🟡 default Azure
review    = "azure"       # :aic    -- 🔴 NON-NEGOTIABLE per Maestro
freeform  = "azure"       # :ai     -- 🟡 default Azure
default_provider = "azure"

[fallback]
on_nim_down = "fail"      # "fail" (default) | "azure" | "prompt"
```

The `nim:N` syntax on `summarize` is a length gate: route to NIM when the
estimated input length is ≤ *N* tokens, otherwise fall through to
`default_provider`. The only syntax; no clever DSL.

**Precedence chain** (inherits from FR-014):
`--provider` flag → `--trigger` + `[routing]` table → `AZ_AI_PROVIDER` env
→ `default_provider` preference → Azure hard default.

**Hard override, defense-in-depth:** if `routing.review = "nim"` is
misconfigured, the router still forces `:aic` to `default_provider` and
logs a warning. `:aic` is load-bearing; we do not let a typo route code
review to a 2B model.

### 4.3 CLI surface additions

| Flag | Purpose |
|---|---|
| `--provider <name>` | Hard override of routing for this call. Beats `--trigger`. |
| `--trigger <name>` | Logical trigger name (grammar, rewrite, summarize, …). Engages the routing table. Intended for Espanso/AHK wrappers. |
| `--fallback azure` | Opt-in silent fallback to Azure when NIM is unreachable. **Off by default** (Mickey's rule). |
| `--list-providers` | Print configured providers + health status (running / warming / unreachable). |
| `--dry-run` | Print the resolved `(trigger, provider, model)` decision and exit without making the call. (Proposed; see §13 Q4.) |
| `--stream-mode {atomic,token}` | `atomic` buffers then injects once (screen-reader safe; default for `--trigger`). `token` streams (default for terminal). |

**Install path (`make` vs subcommand):** ship a **`make install-nim-gemma-2b`**
target, not an `az-ai nim-install` subcommand. Rationale: installing NIM
requires `docker run`, `nvidia-ctk`, `systemctl --user`, and `loginctl
enable-linger` -- a shell script is the native idiom, and bundling 200 LOC of
shell inside a C# CLI binary is wrong. `az-ai --list-providers` and
`az-ai --health nim` stay in-binary for runtime; install is a Make target
with a README pointer. This keeps the AOT binary focused and the install
logic auditable. (If we later want a single-binary onboarding experience, we
can ship an `az-ai nim-install` that simply `exec`s the installed script.)

### 4.4 Per-trigger length gating (AOT-safe)

For `summarize = "nim:400"`: count input tokens before the call, route to
NIM if ≤400 else fall through.

We do **not** ship a real tokenizer. Tokenizer packages are AOT-hostile
(reflection, static data tables, ICU) and we've fought that battle already
(FR-006). Instead: **word-count × 1.3**. This is a well-known heuristic;
over-counts on CJK and code-heavy input (they tokenize denser than English),
which is exactly the direction we want the error to go -- erring toward
"fall through to Azure" on ambiguous input is safer than erring toward
"overload the small model." Document the failure mode (§13 Q3).

```csharp
// Rough approximation; intentionally conservative on the high side.
static int EstimateTokens(string input)
    => (int)Math.Ceiling(input.Split(WhitespaceChars).Length * 1.3);
```

No new packages, no new trim warnings, no AOT regressions.

### 4.5 Warm-state lifecycle (Kramer's spec)

NIM's cold start is 10-20 s (weight load + CUDA graph compile + first kernel
JIT). That is unshippable on a keystroke. **NIM must run as a persistent
service**, not container-on-demand. The install target provisions:

- A `systemd --user` unit `az-ai-nim.service` that runs
  `docker run --gpus=all --rm -p 8000:8000 -e NIM_MODEL_NAME=… <digest-pinned-image>`.
- `loginctl enable-linger $USER` so the service survives SSH disconnects and
  keeps the GPU warm across WSL session boundaries.
- A documented health probe (`curl -fsS http://localhost:8000/v1/health/ready`)
  that the install script uses to wait for readiness.
- An uninstall target (`make uninstall-nim-gemma-2b`) that stops the unit,
  removes the lingering, and `docker rmi`s the image.

Runtime behavior: every `az-ai` invocation probes `/v1/health/ready` with a
150 ms timeout, cached for 2 s in-process. First call after boot pays the
10-20 s warm-up; subsequent calls land in Bania's 15-40 ms band.

### 4.6 Streaming path

OpenAI-compat SSE. Reuse FR-018's SSE parser verbatim -- the wire format is
identical. Byte-clean into the `--raw` sink; `NO_COLOR=1` and `--raw` MUST
produce byte-identical output regardless of provider (Mickey's invariant;
CI test: diff NIM output against Azure output on a fixed deterministic
prompt, zero diff).

**Screen-reader constraint (Mickey, non-negotiable):**

- `--stream-mode atomic` (default when `--trigger` is set): **buffer the
  entire response, inject once.** Token-streaming into an active text field
  is a screen-reader announce-storm.
- `--stream-mode token` (default for interactive terminal): stream tokens
  as they arrive.

The default when `--trigger` is present is `atomic`. The default for bare
terminal use is `token`. Explicit `--stream-mode` overrides both.

### 4.7 Failure UX (Mickey's spec)

User pressed `:aifix` in Slack. They cannot see stderr. They see their
trigger string sitting there. Therefore **stdout sentinels, exit codes,
and nothing else**:

| Condition | Exit code | stdout |
|---|---|---|
| NIM unreachable | 42 | `[az-ai: NIM unreachable -- systemctl --user start az-ai-nim]` |
| NIM warming up (health returns 503) | 43 | `[az-ai: NIM warming up -- retry in 15s or use --fallback azure]` |
| Auth token missing / invalid | 44 | `[az-ai: NIM auth failed -- check ~/.config/az-ai/nim.token]` |
| Model not loaded | 45 | `[az-ai: model not loaded -- check NIM logs]` |

Rules (inherited from Mickey): ≤72 chars, ASCII only, no ANSI, no tabs,
always actionable. Exit codes are a **public contract** -- documented in
`docs/man/az-ai.1` and stable across minor releases, so Espanso/AHK macros
can branch on them.

Silent fallback to Azure is **off by default**. It fires only when the user
passes `--fallback azure` or sets `fallback.on_nim_down = "azure"`. Silent
cloud calls on a local-requested prompt violate privacy, cost, and offline
intent simultaneously; we do not do this.

### 4.8 Cloud-only mode -- zero-hardware onboarding (Uncle Leo / Costanza)

**Not every user has a Blackwell laptop.** A contributor on a ThinkPad, a
reviewer on an Intel MacBook, or a CI runner in a plain container must have
a first-class onboarding path that is strictly simpler than the NIM path,
not a degraded variant of it.

**Guarantee:** If `[providers.nim]` is absent from preferences, the router
degrades to a **no-op**. Every trigger resolves to `default_provider`
(Azure). No health probes, no warm-up, no Docker, no systemd, no NGC key,
no Gemma ToU gate. The binary behaves exactly as v1.x did -- no regression
in setup cost for users who never asked for local inference.

**Three-step cloud-only setup:**

```bash
# 1. Install the AOT binary (or download a release artifact)
make publish-aot && sudo install -m 0755 dist/aot/AzureOpenAI_CLI /usr/local/bin/az-ai

# 2. Export Azure creds (same as v1)
export AZUREOPENAIENDPOINT="https://YOUR.openai.azure.com"
export AZUREOPENAIAPI="YOUR-KEY"
export AZUREOPENAIMODEL="gpt-4o-mini"

# 3. Drop in the Espanso/AHK kit -- it works unchanged
cp examples/espanso-ahk-wsl/espanso/ai.yml ~/.config/espanso/match/
```

That's it. `:aifix`, `:airw`, `:aitldr`, `:aiexp`, `:aic`, `:ai` all route
to Azure. Latency is whatever Azure gives you (~2-3 s typical); no local
footprint at all.

**No separate binary, no separate build flag.** The same AOT binary ships
for both audiences. This is load-bearing: we will not fork the release into
`az-ai-cloud` and `az-ai-local` SKUs. Complexity stays in configuration,
not in packaging.

**Opt-in upgrade path:** when a cloud-only user later gets a Blackwell
box, they run `make install-nim-gemma-2b`, which **only writes to
`~/.config/az-ai/preferences.toml`** -- it never touches the binary. The
installer adds `[providers.nim]` + a `[routing]` table with Maestro's
defaults. Existing Azure-only users upgrading a release see **zero**
behaviour change until they explicitly opt in.

**Success criterion for cloud-only:** the `examples/espanso-ahk-wsl` kit's
Option A (Espanso-in-WSL) and Option B (Espanso-on-Windows-to-WSL) must
both work end-to-end on a machine with **no GPU, no Docker, no systemd**,
using only Azure creds. That's the hardest test: the kit cannot assume
local inference exists. (See §10 criterion 9.)

**Documentation ordering rule (Elaine/Uncle Leo):** every onboarding doc
(`README.md`, `examples/espanso-ahk-wsl/README.md`, `docs/nim-setup.md`)
must present **cloud-only first, NIM second**. The common case leads. The
niche case -- local NVFP4 on Blackwell -- is a clearly-labelled upgrade
section, not the default narrative. This is a welcome-mat commitment, not
a style preference.

## 5. Security (Newman -- non-negotiables)

1. **Digest-pinned images only.** The install script pulls by sha256 digest.
   Tag-based pulls (`latest`, `1.0`) are rejected with a clear error. The
   pinned digest is checked into the repo under
   `docs/providers/nim/pinned-digests.md` and updated via PR. Any rotation
   is a visible, reviewable change.
2. **Bearer token auth.** `auth_token_file` at `~/.config/az-ai/nim.token`,
   permissions enforced at **0600**; first-use check refuses to read a
   world-readable token file. Token is **never logged**, never appears in
   `--verbose`, never in `--dry-run` output, never in error messages.
   `ToolHardeningTests` adds an assertion that token bytes do not appear
   anywhere in captured stderr/stdout across the full happy and sad paths.
3. **SSRF allow-list.** Default allow-list for NIM endpoints:
   `127.0.0.1`, `::1`, `localhost`. `--allow-lan` opt-in extends to
   RFC1918 / ULA ranges. Cloud metadata endpoints are **permanently
   blocked** regardless of opt-in: `169.254.169.254` (IMDS), `169.254.170.2`
   (ECS), `[fe80::]/10` (link-local v6). Tests cover each.
4. **Gemma Terms-of-Use ack file.** First-run creates
   `~/.config/az-ai/providers/nvidia.ack.json` with `{"gemma_tou": true,
   "accepted_at": "…", "version": "2024-…"}` after an interactive prompt.
   `--accept-gemma-tou` is the non-interactive opt-in for CI / Espanso.
   The provider refuses to run if the ack file is absent. (Mirrors
   FR-019's gemma.cpp gating.)
5. **No env-var token exfil.** Tokens live only in the 0600 file.
   `AZ_AI_NIM_TOKEN=…` env vars are **rejected with a security error**,
   not silently consumed. Env is too leaky (ps, /proc/*/environ, shell
   history).

`ToolHardeningTests` additions (mandatory before merge):

- Bearer token redaction across every log path.
- SSRF allow-list: each blocked range rejected; metadata endpoints blocked
  under `--allow-lan`.
- Ack-file enforcement: missing file → clear error, no network call made.
- Digest pinning: tag-based config → rejected at load time.

## 6. Licensing (Jackie -- non-negotiables)

- **No bundling.** az-ai does not redistribute NIM images or Gemma weights,
  ever. The install script `docker pull`s from nvcr.io at the user's
  direction; we are not a mirror.
- **HTTP-only invocation.** We do not link NVIDIA code, we do not vendor
  gemma.cpp (that's FR-019), we do not embed weights. Everything is behind
  an OpenAI-compat HTTP boundary.
- **NOTICE update:** adds NVIDIA + Google attribution lines. Jackie owns the
  final wording.
- **NVIDIA AI Enterprise disclosure.** Docs must state clearly: *personal
  use of NIM is likely free under NVIDIA's Developer Program; production
  deployment may require an NVIDIA AI Enterprise subscription. Check with
  NVIDIA.* We are not providing legal advice; we are pointing at the right
  terms page.
- **Gemma ToU.** Same gating as FR-019 (§6 above). One shared ack registry
  at `~/.config/az-ai/providers/`.

## 7. Cost / Telemetry (Morty + Frank)

Morty's per-call schema (matches FR-019):

```json
{
  "provider":      "nim",
  "model":         "nvidia/gemma-4-2b-it-nvfp4",
  "trigger":       "grammar",
  "tokens_in":     118,
  "tokens_out":    94,
  "wall_ms":       612,
  "ttft_ms":       28,
  "cost_usd":      0.00,
  "notes":         "local-inference; warm"
}
```

Frank's opt-in telemetry flag (`--telemetry`) exports these per the Phase 5
schema. Default off. No per-call phone-home. Local-inference cost is always
logged as `0.00`; Morty's spend warnings suppress for `provider=nim`.

**Tokenizer drift:** NIM's Gemma tokenizer is not Azure's tokenizer. For
mixed-provider sessions, tokens are counted per-provider and labelled.
Known-disagree warning fires once per session if counts diverge >20 %.
Same policy as FR-019 §9.

## 8. AOT implications

**Zero.** Every addition is:

- `System.Net.Http` (already in use).
- TOML preferences parsing via FR-014's source-generated path.
- New request/response DTOs registered in `AppJsonContext`.
- `System.IO.File.Exists` + `FileInfo.UnixFileMode` for 0600 checks.

No new NuGet packages. No reflection. No dynamic code. `make publish-aot`
should produce a binary within ±1 % of pre-FR-020 size. CI asserts this.

## 9. Phased Rollout

- **Phase A (prereq):** FR-018 provider abstraction lands. Blocks everything
  below.
- **Phase B:** `NimProvider` + preferences extension + `--provider` /
  `--trigger` flags + digest pinning + bearer token auth + ack-file gate.
  **Cloud-only remains the default out-of-the-box state** -- shipping Phase B
  must not change the behaviour of a user who never edits preferences.
- **Phase C:** Per-trigger routing layer + length-gating heuristic +
  `--fallback` opt-in. `:aifix` / `:airw` ship here as local-by-default
  **only when `[providers.nim]` is configured**; the no-op degrade path
  (§4.8) is a gating test, not an afterthought.
- **Phase D:** `make install-nim-gemma-2b` one-shot + `az-ai-nim.service`
  systemd-user unit + `loginctl enable-linger` + install/uninstall docs.
  WSL2 Ubuntu 24.04 is the reference target.
- **Phase E:** Buffer-and-inject streaming mode (`--stream-mode atomic`
  default for `--trigger`) + published Espanso/AHK example bundles.
  This is the "demo-able in 15 seconds" phase.
- **Phase F:** OpenTelemetry / cost hook integration (depends on the
  Phase 5 telemetry schema landing project-wide).

9B is the flagship demo model and ships the moment NVFP4 Gemma-4-9B-it is
published by NVIDIA. 2B is the launch target because (a) it exists today
and (b) it fits every Blackwell laptop including the reference 12 GB
RTX PRO 3000. See §13 Q2 on sequencing risk.

## 10. Success Criteria (measurable)

Phase C is "done" when all hold on a **Blackwell RTX PRO 3000 (12 GB) /
Ultra 7 265H / WSL2 Ubuntu 24.04** reference box:

1. `az-ai --trigger grammar --raw "Their going to the store"` returns
   corrected text with **p50 ≤ 800 ms, p95 ≤ 1200 ms** end-to-end
   (including WSL spawn, per Bania's §A budget).
2. Warm-up cost is paid **once** at `systemctl --user start az-ai-nim`,
   never on a user keystroke.
3. All Newman hardening tests pass (token redaction, SSRF allow-list,
   metadata block, digest pin, ack-file gate).
4. **`:aic` routes to Azure even when `routing.review = "nim"` is
   misconfigured** -- defense-in-depth assertion in tests.
5. `make install-nim-gemma-2b` succeeds on a fresh WSL2 Ubuntu 24.04
   in under **10 minutes** (network permitting), docker-install included.
6. Zero new AOT trim warnings. `make publish-aot` binary size within ±1 %.
7. `NO_COLOR=1 --raw` output is byte-identical between NIM and Azure on
   a fixed deterministic prompt (Mickey's diff test).
8. Mickey-approved exit codes (42-45) and sentinel strings stable and
   documented in `docs/man/az-ai.1`.
9. **Cloud-only parity (§4.8):** on a machine with **no GPU, no Docker,
   no systemd, no `[providers.nim]` config**, every `--trigger` flag
   routes to Azure with no warnings, no health probes, and no startup
   cost beyond v1.x baseline. The full `examples/espanso-ahk-wsl` kit
   (Options A and B) works end-to-end on such a machine.

## 11. Routing Map Appendix (Maestro, verbatim quality matrix)

| Trigger | Local 2B rating | Routing decision | Reasoning |
|---|---|---|---|
| `:aifix` (grammar) | 🟢 Green | **Local 2B** | Near-deterministic transform. 2B matches cloud on 1-3 sentence inputs. Latency wins, quality parity. Overqualified for cloud. |
| `:airw` (rewrite) | 🟡 Yellow | **Local 2B** (strict system prompt) | Competent; tonally uneven. Acceptable with negative-constraint prompting ("do NOT add pleasantries"). Watch for corporate over-formality. |
| `:aitldr` (summarize) | 🟡 Yellow | **Local 2B if ≤400 input tokens, else Azure** | Fine on short input; collapses to first paragraph on long multi-topic input. Length gate is the fix. |
| `:aiexp` (explain) | 🟡 Yellow | **Azure default; local 2B opt-in** | Explanations are read carefully. Code explanation misses framework-specific nuance, occasionally confabulates APIs. Prose passable. |
| `:aic` (code review) | 🔴 Red | **Azure only -- NON-NEGOTIABLE** | Code review is a reasoning task, not a text task. 2B produces surface nits, misses logic bugs, invents issues. Stays cloud until ≥14 B reasoning-distilled local model proves out. |
| `:ai` (freeform) | 🟡 Yellow | **Azure default** | Unbounded scope demands the stronger orchestra. Users may opt into local via `--provider nim`. |

**Prompt-engineering invariants for small models** (Maestro §2-4):

- System prompts are cages, not suggestions. Explicit `Output ONLY …` for
  every transformation trigger.
- `max_tokens = ceil(input_tokens × 1.3)` for `:aifix` / `:airw`.
  Transformations cannot balloon; truncation self-corrects drift.
- Temperature: 0.2 `:aifix`, 0.3 `:airw`, 0.0 `:aitldr`.
- Output sanity check: if output > 2× input length, discard and retry once.
- Prefix strip: outputs beginning with "Sure,", "Here's", "I think" are
  monologue; strip or retry.

**Forward-compat graduation ladder** (when NVFP4 Gemma-4 larger variants
land):

- **4B:** `:aiexp` graduates to local; `:airw` loses its Azure fallback.
- **9B:** `:aitldr` goes fully local, length gate relaxes to ~1200 tokens;
  `:ai` becomes local-default with Azure opt-in.
- **14B+ reasoning-distilled:** `:aic` is re-evaluated against the eval
  harness. Until then it stays cloud. Maestro "will not be moved."

## 12. Open Questions

1. **NIM personal-use license clarity.** Personal use under NVIDIA's
   Developer Program is widely believed free; production is AI Enterprise.
   Who owns getting that confirmed in writing before we publish install
   docs? Jackie + Bob Sacamano likely. Blocker on Phase D public launch.
2. **9B NVFP4 availability timing.** ADR-006 names 9B as the flagship
   demo target. If NVIDIA has not published Gemma-4-9B-IT-NVFP4 by our
   v2.1 target, we ship 2B-only and re-announce when 9B drops. That's
   acceptable but requires a clear marketing line ("2B now, 9B the day
   NVIDIA ships it"). Sequencing risk is on NVIDIA's roadmap, not ours.
3. **Word-count × 1.3 heuristic failure modes.** Over-counts are
   conservative (fall through to Azure). Under-counts are rare but possible
   on code-heavy or CJK input where a word is many tokens. Document the
   failure; consider a `--force-local` / `--force-cloud` override for
   advanced users. Not worth a real tokenizer until v2.2.
4. **`--dry-run` scope.** Proposed: prints `(trigger, provider, model,
   estimated_tokens)` and exits 0 without making the call. Useful for
   debugging Espanso configs. Cheap to build. Do we ship it in Phase B
   or defer? Default recommendation: ship in B.
5. **Inline routing override for Espanso/AHK users.** Some users will want
   per-snippet overrides. Options: (a) `--provider` flag (already in),
   (b) trigger-name suffix like `:aifix@azure`, (c) environment variable
   pinning. Recommend (a) only for v1 -- keep the surface tight.
6. **Sentinel collision.** Do `[az-ai: …]` sentinels collide with content
   any user might actually type? Unlikely -- the combination of square
   brackets, the literal prefix `az-ai:`, and the trailing actionable
   sentence is distinctive -- but we should search a public Espanso corpus
   before finalizing.
7. **Rules-based vs ML-driven routing.** Rules-based for v2.1. An ML
   quality classifier ("is this input 2B-suitable?") is intellectually
   attractive but adds a model before the model, and the whole point of
   local is latency. Revisit post-v2.1 only if routing miss-rate data
   justifies it.
8. **Docker + NVIDIA Container Toolkit bootstrap in WSL2.** The reference
   machine does **not** have Docker or `nvidia-ctk` installed in WSL2
   yet. Does `make install-nim-gemma-2b` install them, or does it just
   check-and-fail? Recommend check-and-fail with a one-command fix in
   the error message. Auto-installing Docker into a user's WSL distro is
   too intrusive.
9. **Hot-reload of preferences.** Phase B does not hot-reload; a
   preferences change requires restarting the NIM provider (and thus the
   next `az-ai` invocation). This is intentionally out of scope (§13).

## 13. Out of Scope (explicit, v1 of this FR)

This FR does **not** cover, and will reject scope-creep on:

- **TensorRT-LLM direct integration.** ADR-006 rejected this. NIM is the
  only integration path.
- **Native-lib linking (P/Invoke into inference runtime).** AOT-hostile.
  See FR-019 §3 Option C rejection for the full reasoning.
- **Bundling or redistributing NIM images or Gemma weights.** Ever.
- **Automatic / ML-driven provider election.** Rules-based only for v2.1.
- **Fine-tuning / LoRA support.** NIM can serve LoRA adapters; we do not
  configure them.
- **Embeddings API.** Separate future FR.
- **GPU sharing across multiple local providers.** If a user runs both
  NIM and gemma.cpp (FR-019), coordinating VRAM is their problem.
- **Hot-reload of preferences.** Preferences are read at process start.
  Changing them requires re-invoking `az-ai`. Non-issue given sub-10 ms
  AOT cold start.
- **Windows-native (non-WSL) NIM.** NIM's Windows story is nascent.
  WSL2 Ubuntu is our reference; native Windows is a future FR if demand
  materializes.
- **Cluster / multi-GPU deployments.** Single-GPU single-host only.

---

## Appendix A -- Performance Budget (Bania)

End-to-end budget for `:aifix` on a 100-token selection → 80-token rewrite,
RTX PRO 3000 Blackwell Mobile (12 GB VRAM), warm NIM:

| Stage | Budget | Notes |
|---|---|---|
| Espanso trigger → `wsl.exe` launch | 80-200 ms | **Dominant non-model term.** WSL process spawn. |
| `az-ai` AOT cold start | 5-10 ms | Per FR-006. |
| HTTP localhost → NIM | 1-3 ms | |
| Prefill (100 tok) + TTFT | 20-40 ms | |
| Decode 80 tokens @ 150 tok/s | ~530 ms | |
| Response → clipboard (WSL→Win) | 30-80 ms | `clip.exe` or OSC52. |
| **Total** | **~700-900 ms** | p50 target ≤ 800 ms, p95 ≤ 1200 ms. |

vs Azure gpt-4o-mini end-to-end on same task: ~1.6 s p50 with a fatter p99
tail from WAN variability. **Local warm wins; local cold loses; the whole
game is keeping it warm.** That is why §4.5 is non-negotiable.

CI gates (Bania §7):

- `az-ai` cold start p95 ≤ 10 ms -- **non-negotiable AOT budget.**
- HTTP client request overhead p95 ≤ 5 ms.
- End-to-end mock round-trip p95 ≤ 25 ms.

Nightly on reference box (live NIM):

- Warm TTFT p95 ≤ 60 ms.
- Decode tok/s p50 ≥ 120.
- End-to-end `:aifix` wall-clock p95 ≤ 1.2 s.
- 24 h VRAM residency drift: **zero leak gate.**

---

## Appendix B -- Why Not Just Ollama-with-Gemma?

Same question as FR-019 §Appendix, different answer. Ollama's Gemma bundle
gives you 3B-class tok/s on CPU (good) or a naive CUDA path on GPU
(acceptable, not NVFP4). NIM's value is **NVFP4 tensor-core utilization on
Blackwell** -- which is the only reason the 15-40 ms TTFT number is real.
On a non-Blackwell machine, Ollama is the right answer and FR-018 already
covers it. FR-020 is specifically the Blackwell-NVFP4 path. Docs must say
that clearly so non-Blackwell users don't install NIM expecting magic.
