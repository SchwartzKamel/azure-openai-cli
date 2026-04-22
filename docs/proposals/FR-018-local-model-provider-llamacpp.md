# FR-018 -- Local-Model Provider (llama.cpp / Ollama) for az-ai

**Status:** Draft (Costanza + Kramer + Newman + Morty, 2026-04-22)
**Related:** FR-014 (local preferences + multi-provider profiles -- **hard dependency**), FR-010 (model aliases), FR-013 (MCP client), FR-003 (local user preferences), [v2-migration.md](../v2-migration.md)
**Owner:** Kramer (implementation), Costanza (product shape), Newman (SSRF + key-material review), Morty (zero-API-cost telemetry hooks), Jerry (AOT + HTTP pipeline reuse)
**Target release:** v2.1 (post v2.0 cutover; depends on FR-014 preferences file landing first)

---

## 1. Problem Statement

az-ai is Azure-native by design -- every code path assumes an `AzureOpenAIClient` pointed at an `*.openai.azure.com` endpoint with a Foundry-issued key. That positioning is our moat for regulated-enterprise users, and we are **not** walking it back. But a material fraction of the CLI's day-to-day users (developers iterating on Espanso snippets, AHK macro authors debugging locally, users on metered airplane wifi, users at companies that cannot send prompts off-prem without a DLP review) want the same binary to target a **local** OpenAI-compatible server -- either [`llama.cpp`](https://github.com/ggml-org/llama.cpp)'s `llama-server` on `http://localhost:8080/v1` or [Ollama](https://ollama.com)'s server on `http://localhost:11434/v1`. Today they have to switch tools (fall back to `aichat`, `llm`, `ollama run`), which means we lose the session entirely -- cold-start advantage, persona memory, tools, pattern library, `--raw` contract, everything.

This proposal adds a **local provider adapter** that slots into FR-014's provider-profile model, keeps Azure OpenAI the default, and treats local as an explicitly opted-in named profile. Reference local model: **Gemma 3** (and forward-compatible with "Gemma 4" should Google ship it -- we pin no version numbers in the adapter itself; model names are resolved through FR-010 aliases). The adapter speaks the OpenAI chat-completions wire format that both servers already expose, so we do not build a second protocol; we reuse the same transport with a different base URL, a different key-material policy, and a capability gate for tool calling.

## 2. Competitive Gap / Motivation

| Driver | Today | With FR-018 |
|---|---|---|
| **Privacy / DLP** | Prompts leave the machine; blocked in some orgs. | `--provider local` keeps every byte on-device; auditable via `curl localhost`. |
| **Offline use** | az-ai is unusable on airplane wifi, air-gapped labs, customer sites. | Fully usable against a running `ollama serve` or `llama-server`. |
| **Iteration cost** | Every dev-loop prompt burns Azure tokens. | Zero API dollars while iterating on prompt templates, personas, and tool flows. |
| **Latency floor** | TTFT dominated by network round-trip to Azure region. | Sub-10 ms TTFT on warm local model (hardware-dependent). |
| **Competitive optics** | aichat, `llm`, fabric, `sgpt`, `crush` all ship local-model support. az-ai is the only actively-maintained AOT CLI without it. | Closes the "wait, you can't even hit Ollama?" objection in head-to-head comparisons. |
| **Testing / CI** | Integration tests that exercise tool loops need live Azure keys. | CI can spin up a tiny local model and exercise the tool-call loop end-to-end without secrets. |

This is **not** a pivot away from Azure. Azure remains the default, the shipping recommendation, the thing the README leads with, and the only provider we offer SLAs against. Local is a safety valve for power users, a dev-loop accelerant for contributors, and a DLP release valve for the 20 % of prospects who currently bounce at the word "cloud."

## 3. Rough Effort

**Medium (3/5).** ~1.5 engineer-weeks once FR-014 has landed.

Breakdown:

- Provider-abstraction interface in v2 (`IProviderAdapter` or similar): ~1 day -- the `IChatClient` seam from `Microsoft.Extensions.AI` already exists (see `azureopenai-cli-v2/Program.cs:197-198`), so most of the work is routing, not adaptation.
- Local adapter (shared for llama.cpp + Ollama, since they speak the same wire format): ~2 days.
- Preferences schema additions, flag parsing, precedence chain integration with FR-014: ~1 day.
- Capability negotiation (probe `/v1/models` or rely on declared capability flags in preferences) + tool-call gating: ~2 days.
- SSRF hardening, allowlist logic, tests: ~1.5 days.
- Fallback policy (Azure → local on 429/5xx): ~1 day.
- Docs, example Modelfile, recommended local-model matrix: ~1 day.

Cost estimate is predicated on FR-014 having already shipped the preferences file and provider-profile scaffolding. If FR-014 slips, this proposal slips.

## 4. Proposed Solution

### 4.1 High-level architecture

v2 already builds on `Microsoft.Extensions.AI.IChatClient` via `AsIChatClient()` (Program.cs:198). That is the seam. We do **not** invent a new abstraction -- we substitute the backing implementation:

```
┌─────────────────────────────────────────────────────────────┐
│ CLI entry (Program.cs)                                      │
│  flags → CliOptions → ProviderSelector                      │
└────────────────────────────┬────────────────────────────────┘
                             ▼
              ┌──────────────────────────┐
              │  ProviderSelector        │  resolves profile by
              │  (new, ~150 LOC)         │  precedence chain:
              │                          │  flag → env → proj
              │                          │  → user → default
              └────────────┬─────────────┘
                           ▼
        ┌──────────────────┼──────────────────┐
        ▼                                     ▼
┌──────────────────┐                 ┌────────────────────┐
│ Azure adapter    │                 │ Local OAI adapter  │
│ (existing)       │                 │ (new, FR-018)      │
│ AzureOpenAI-     │                 │ OpenAIClient with  │
│ Client.GetChat-  │                 │ custom BaseAddress │
│ Client()         │                 │ + ApiKeyCredential │
│  .AsIChatClient()│                 │  .AsIChatClient()  │
└────────┬─────────┘                 └─────────┬──────────┘
         │                                     │
         └──────────┬──────────────────────────┘
                    ▼
            IChatClient (MAF)
                    │
                    ▼
         Existing agent/ralph/persona
         pipelines unchanged
```

The critical observation: `OpenAIClient` (from the `OpenAI` NuGet package we already depend on transitively via `Azure.AI.OpenAI 2.9.0-beta.1`) accepts an `OpenAIClientOptions.Endpoint` override. Pointing it at `http://localhost:8080/v1` or `http://localhost:11434/v1` produces a working chat-completions client that plugs into the same `AsIChatClient()` extension -- **zero new NuGet dependencies**.

### 4.2 Preferences schema additions

Extends FR-014's `~/.config/az-ai/preferences.toml`:

```toml
# Global default; can be overridden by --provider flag or $AZ_AI_PROVIDER.
default_provider = "azure"

[providers.azure]
kind = "azure-openai"
endpoint = "https://my-tenant.openai.azure.com/"
# Key material still lives in env / Windows Credential Manager / keyring --
# never in this file. FR-014 defines the lookup chain.
api_key_env = "AZURE_OPENAI_API_KEY"
default_model = "gpt-4o"

[providers.local]
kind = "openai-compatible"
endpoint = "http://localhost:11434/v1"   # Ollama default; use :8080/v1 for llama-server
# Local servers usually accept any non-empty string; this is fine to hardcode.
# We refuse to read key material from TOML in principle (Newman), but local
# dummy keys are the one exception -- they carry no secret.
api_key = "ollama"
default_model = "gemma3:27b"

# Declared capabilities. The adapter will refuse --agent mode if tool_calling=false
# rather than silently producing gibberish. Probed on first use and cached.
[providers.local.capabilities]
tool_calling = true      # Gemma 3-IT + Ollama ≥0.4 support it; llama.cpp supports grammars.
streaming = true
vision = false
json_mode = true

# Optional: per-provider model aliases (FR-010 extension).
[providers.local.aliases]
g3    = "gemma3:27b"
g3s   = "gemma3:4b"
qwen  = "qwen2.5-coder:32b"
# Forward-compatible slot for a hypothetical future Gemma release.
# If/when "gemma4" lands, users edit this line; no code change required.
latest = "gemma3:27b"

# Optional fallback policy (default off). When enabled and the primary
# provider returns HTTP 429 / 5xx, we retry against the named fallback.
[fallback]
enabled = false
from    = "azure"
to      = "local"
on_status = [429, 500, 502, 503, 504]
max_retries = 1
# Never fall back in --agent or --ralph mode by default -- tool-call shapes
# may differ across providers and a mid-loop swap is dangerous.
skip_in_agent_mode = true
```

AOT constraint: all of this is parsed via a source-generated TOML reader (FR-014 commits to that choice). No reflection, no `JsonSerializer` overloads that require it. The capability block is a plain record registered in `AppJsonContext` (or its TOML equivalent).

### 4.3 CLI surface

New/extended flags (all additive; no breaking changes):

| Flag | Purpose | Example |
|---|---|---|
| `--provider <name>` | Select a named provider profile for this invocation only. | `az-ai --provider local "what's 2+2?"` |
| `--endpoint <url>` | One-off endpoint override. Validated against the SSRF allowlist (§6). | `az-ai --provider local --endpoint http://localhost:9999/v1 …` |
| `--model <name-or-alias>` | Already in FR-010. Now resolves **per provider** -- `--model g3` under `local`, `--model 4o` under `azure`. | `az-ai --provider local --model g3 …` |
| `--list-providers` | Prints configured providers and their resolved capabilities. | `az-ai --list-providers` |
| `--check <provider>` | Dry run: pings `/v1/models`, reports reachability, streaming support, tool-call support. No prompt sent. | `az-ai --check local` |
| (env) `AZ_AI_PROVIDER` | Shell-scoped default (sits between CLI flag and preferences file in the precedence chain). | `AZ_AI_PROVIDER=local az-ai …` |

Precedence chain (matches FR-014, extended):

```
--provider flag
  > $AZ_AI_PROVIDER
    > project-local ./.az-ai.toml [default_provider]
      > ~/.config/az-ai/preferences.toml [default_provider]
        > hardcoded "azure"
```

Alias resolution is **scoped to the resolved provider's `[providers.<name>.aliases]` table** before falling through to any global alias table. This prevents `--model mini` from accidentally meaning `gpt-4.1-mini` when the active provider is `local` and has no such model.

### 4.4 Tool-calling negotiation

Tool calling is the load-bearing capability for `--agent` and `--ralph` modes. Local models vary wildly: Gemma 3-IT supports OpenAI-style function calling in Ollama ≥0.4; llama.cpp exposes function calling via GBNF grammars or its newer OpenAI-compat `tools` parameter; older quantizations may silently drop the `tools` field and hallucinate. **Silent degradation is the worst outcome** -- it looks like the agent is working and then produces nonsense.

Algorithm:

1. On first use of a non-Azure provider in the session, call `GET {endpoint}/models` with a 2 s timeout. Record the response.
2. Cross-reference the declared `[providers.<name>.capabilities]` block from preferences.
3. If the user invoked `--agent` or `--ralph`:
   - If `tool_calling = false` (declared) → **fail fast** with a clear error:
     ```
     [ERROR] Provider 'local' has tool_calling=false in preferences.
             --agent requires tool-calling support.
             Edit ~/.config/az-ai/preferences.toml or switch providers with --provider azure.
     ```
   - If `tool_calling = true` (declared) → attempt. If the first assistant turn returns a non-empty `tool_calls[]`, proceed. If it returns plain text when a tool call was required by the prompt harness, log a one-time warning to stderr (suppressed under `--raw`) and continue in best-effort mode.
4. Cache the probe result for the lifetime of the process. Do not re-probe on every call.

We do **not** attempt to auto-convert between Azure's tool schema and any proprietary local format -- both servers already accept the OpenAI `tools`/`tool_calls` shape, and that is the only shape we emit.

### 4.5 Streaming path + `--raw` contract

Both llama.cpp and Ollama stream via `text/event-stream` compatible SSE at the `/v1/chat/completions` endpoint when `stream: true`. The `IChatClient.GetStreamingResponseAsync` path (already used at Program.cs:241) works unmodified once the client is pointed at the local endpoint.

The `--raw` contract (clean stdout, no spinner, no stderr decoration) is preserved verbatim. Specifically:

- Spinner is suppressed when `--raw` **or** when stderr is not a TTY -- unchanged.
- The token-usage line (`[tokens: …]`) is suppressed under `--raw`. Local servers report usage inconsistently; when absent, we emit nothing (we do **not** fabricate zeroes).
- Errors from the local server are routed through `ErrorAndExit()` with the existing `[ERROR]` prefix; no new error-surface format.

## 5. Security Review Hooks (Newman)

Local endpoints bypass Azure's TLS + Entra ID + managed-identity authz model. Four concrete risks, four concrete mitigations:

### 5.1 SSRF via `--endpoint`

A malicious Espanso snippet or a poisoned project-local `.az-ai.toml` could set `endpoint = "http://169.254.169.254/…"` (cloud metadata service) or point at a LAN admin panel. **Default allowlist:**

- `http://localhost[:port]/…`
- `http://127.0.0.1[:port]/…`
- `http://[::1][:port]/…`
- `https://…` to any host (assumed TLS-authenticated).

**Default denylist** (refused even with explicit `--endpoint`):

- RFC 1918 ranges: `10.0.0.0/8`, `172.16.0.0/12`, `192.168.0.0/16`.
- Link-local: `169.254.0.0/16`, `fe80::/10`.
- Cloud metadata: `169.254.169.254`, `fd00:ec2::254`, `metadata.google.internal`.
- Anything that resolves (post-DNS) into the above ranges -- matches the existing `WebFetchTool` resolver hook; we reuse that logic verbatim rather than re-implementing it.

**Opt-in LAN flag:** `[providers.<name>] allow_lan = true` in preferences (not a CLI flag -- requires deliberate, persistent action). Even with `allow_lan = true`, cloud-metadata endpoints remain blocked unconditionally.

### 5.2 Key material

Key-like strings in `preferences.toml` are refused at parse time **except** for the known-dummy values (`"ollama"`, `""`, `"not-needed"`, `"sk-no-key-required"`) that local servers accept. Real keys must come from environment variables or the existing keyring integration (FR-003/014). This is a parse-time check, not runtime.

### 5.3 Prompt exfiltration on fallback

Fallback from Azure → local is safe (prompt stays local). Fallback from local → Azure is **disabled** in the default policy -- a user who chose local did so for a reason, and silently shipping their prompt to Azure on a local-server crash would be a privacy incident. `from = "local"` in `[fallback]` emits a startup warning.

### 5.4 Tool-call hijacking

Local models are easier for an attacker to swap (no signed manifest). Defense-in-depth: the existing `ToolHardeningTests` (shell blocklist, file-read denylist, SSRF guard on `web_fetch`) apply at the tool layer, *after* the model has emitted a tool call. The provider swap changes who *generates* the call; it does not change who *executes* it. No new attack surface in the tool layer.

Newman sign-off required before merge -- specifically on the LAN-allow flag and the metadata-endpoint denylist completeness.

## 6. Cost / Telemetry (Morty + Frank)

Local runs cost **zero API dollars**, but they are not free -- they burn watt-hours, GPU VRAM, and human wall-clock. The telemetry story:

- **Token counting is still performed** when the server reports usage (Ollama does; llama.cpp does when `usage=true` is set on the request). This is useful for benchmarking prompts, comparing model-quality-per-token across providers, and for FR-015's cost-estimator to give "Azure would have cost $X for this prompt."
- **Dollar line is suppressed** when `kind = "openai-compatible"` and the provider has no rate-card entry. We do not emit `$0.0000` -- that encodes "free" into the UX and trains users to ignore the cost line.
- **Wall-clock + TTFT are recorded** (Frank's OTel pipeline from v2 Phase 5, already in flight). These are the meaningful signals for local runs.
- **Cumulative savings banner** (opt-in, off by default): `az-ai budget` can display "This week: 12,431 tokens handled locally, est. Azure-equivalent cost $0.18." Morty's call on whether to ship this -- risks trivializing Azure cost or over-encouraging local use for regulated data. Flag it open for PM review.

No new network telemetry destinations. Opt-in local-only logging to `~/.local/share/az-ai/usage.jsonl`, rotated at 10 MB, gitignored by default.

## 7. AOT Implications (Jerry + Kramer)

Hard constraint: **zero new NuGet packages**, **zero new trim warnings**, **zero cold-start regression beyond the v2 speed gates (±10 %).**

- `OpenAIClient` is already on the graph (transitive from `Azure.AI.OpenAI 2.9.0-beta.1`). Constructing it with a custom `OpenAIClientOptions.Endpoint` does not pull new code paths at AOT-trim time -- we validated this informally in the v2 spike; a follow-up bench during Phase B will confirm.
- HTTP pipeline: reuse the existing `SocketsHttpHandler` that FR-007 pre-warms. Pass the same handler into `OpenAIClientOptions.Transport`. This gives local runs the same connection-pooling behavior, and more importantly, does not instantiate a second handler on hot path.
- TOML parsing is FR-014's problem, not ours. If FR-014 ships a source-generated TOML reader, we register our schema types against it. If FR-014 ships a reflective TOML reader, this FR is **blocked** at the AOT review gate and we negotiate with Jerry before proceeding.
- New types: `ProviderProfile` record, `ProviderCapabilities` record, `FallbackPolicy` record, `LocalProviderAdapter` class. All `internal sealed`. All registered in `AppJsonContext` for any JSON interop (e.g., the `--list-providers --json` path).

No new `IlcGenerateStackTraceData` concerns. No reflection. No dynamic proxy.

## 8. Phased Rollout

### Phase A -- Provider abstraction in v2 (2-3 days)

**Prerequisite: FR-014 has landed** with a working `preferences.toml` loader and the `ProviderProfile` shape defined.

- Introduce `IProviderAdapter` (or reuse `IChatClient` directly and add a thin `ProviderSelector` helper -- implementation choice; the former is more honest about what we're doing, the latter is fewer LOC).
- Rewire `Program.cs` (v2) to route through the selector. Azure remains the only registered adapter at end of phase.
- Tests: existing v2 tests pass unchanged. Adds ~20 unit tests for the selector (precedence chain, alias scoping, malformed preferences).
- **Exit criterion**: `az-ai --provider azure "hello"` produces byte-identical output to `az-ai "hello"` on the same git SHA. No behavior change for any existing user.

### Phase B -- Local adapter (3-4 days)

- Implement `LocalOpenAICompatibleAdapter` -- single adapter class serving both llama.cpp and Ollama. The only differences are default ports and idiomatic model-name formats (`gemma3:27b` vs `gemma-3-27b-instruct.Q4_K_M.gguf`), both handled via the alias table.
- Capability probe (§4.4).
- SSRF allowlist (§5.1).
- Integration tests: spin up a tiny `ollama serve` + `llama-cpp-python[server]` in CI (Docker-sidecar pattern, opt-in job, not blocking for PRs). Smoke `az-ai --provider local "2+2"` end-to-end.
- **Exit criterion**: Golden-path commands work against both reference servers; `--agent` mode works against Gemma 3-IT on Ollama with the built-in tools.

### Phase C -- Fallback policy + preference knobs (1-2 days)

- Implement `[fallback]` block.
- Default off. Emit a startup warning when enabled and when `from = "local"`.
- Fallback suppressed in `--agent` and `--ralph` modes by default (§4.2).
- **Exit criterion**: Simulated 429 from Azure routes to local; metrics confirm exactly one retry; `--raw` output stays clean.

### Phase D -- Docs + example Modelfiles + recommended local-model matrix (1 day)

- `docs/local-models.md` -- setup guide for `ollama pull gemma3:27b`, `llama-server` invocation, hardware recommendations (VRAM / RAM floor), known-good model list.
- `docs/recipes/` -- three end-to-end demos: summarization with `g3s` (4B), tool-using agent with `g3` (27B), privacy-sensitive git-commit-message generation fully local.
- CHANGELOG entry (Mr. Lippman).
- README "Providers" section, lead paragraph still Azure-first.
- **Exit criterion**: Elaine + Russell sign off. Peterman drafts the launch announcement. Keith gets a 60-second demo script.

## 9. Open Questions

1. **Tool-call schema drift.** Ollama's tool-call shape is OpenAI-compatible *today*, but Ollama has shipped breaking changes to its `/v1` surface before (0.1 → 0.3). Do we pin a minimum Ollama version in the probe, or lazily detect and degrade? **Recommend**: declared minimum in docs (Ollama ≥0.4, llama.cpp ≥b4000), probe-based detection at runtime, clear error on version mismatch.
2. **Context-window handling.** Azure deployments declare `max_context_length` server-side; local models vary by model + quantization + user config. How do we surface "you blew the context window" errors when the server returns a 400 with a provider-specific message? **Recommend**: pattern-match the common error shapes, rewrite to a unified `ContextWindowExceededException`, include the model name and observed limit in the message.
3. **Alias cross-provider bleed.** If the user has `--model g3` aliased under `local` and runs `az-ai --provider azure --model g3 …`, should we (a) error out, (b) fall through to the Azure alias table, or (c) try to resolve `g3` as a literal Azure deployment name? **Recommend**: (a) fail fast with "alias 'g3' is not defined for provider 'azure'" -- explicit > magical.
4. **`--persona` interaction when the local model is weaker.** Our 25 personas were tuned on GPT-4 class. Gemma 3-4B cannot faithfully role-play `FDR` (the red-team persona needs adversarial reasoning). Do we (a) gate personas on declared model capability, (b) warn once and let the user proceed, (c) ship a "lite" persona variant for small models? **Recommend**: (b) for v1, revisit with Maestro in a follow-up FR.
5. **Gemma 4 forward-compat.** The user asked about "gemma 4." At the time of writing, Gemma 3 is the latest published Google open-weight model (April 2026). This FR does **not** pin to a specific version -- the adapter is version-agnostic; users edit the alias table when new models ship. Flag open so PM can decide whether to seed a "latest" alias (risk: user confusion when it silently points to something different next quarter) or hard-code (risk: stale on day one).
6. **Embeddings endpoint.** Explicitly out of scope (§12), but should we reserve the `kind = "openai-compatible"` slot for a future embeddings adapter so we don't have to rename? **Recommend**: yes, schema-wise; no, implementation-wise -- FR-018 ships chat only.
7. **Windows / macOS parity.** Ollama on macOS runs natively; Ollama on Windows is beta; `llama-server` works on both. Do we need platform-specific CI coverage, or is Linux-only integration test sufficient for v1? **Recommend**: Linux-only for integration tests; document known-good setups for macOS and Windows in `docs/local-models.md`; revisit if bug reports cluster by OS.
8. **Key-file discovery.** Some users run `llama-server` behind nginx with HTTP basic auth or a Bearer token. Do we support the existing keyring/env lookup, or add a `[providers.<name>] auth_header` escape hatch? **Recommend**: env + keyring only for v1; escape hatch is a separate FR if demand emerges.
9. **Logging sensitivity.** Local runs handle data the user explicitly did **not** want to send off-machine. Our existing error logs may include snippets of the prompt on failure. Do we need a separate `log_redaction_level` per provider? **Recommend**: yes -- default `full_redaction` for non-Azure providers, `normal_redaction` for Azure. Flag for Rabbi + Newman.
10. **Fallback identity leak.** When fallback fires Azure → local, the *system prompt* (which may contain Azure-specific deployment naming, tenant slugs, or internal project codenames embedded by an ops team) gets sent to the local model. Low risk (local stays on-box) but worth an explicit note in the fallback section of the docs. Flag for Elaine.

## 10. Success Criteria

Measurable, testable, all required for Phase D exit:

- [ ] `az-ai --provider local "what is 2+2"` returns a non-empty completion against a stock `ollama serve` with `gemma3:27b` pulled, in ≤ 2 s TTFT on reference hardware (RTX 4090, 64 GB RAM).
- [ ] `az-ai --provider local --agent "fetch https://example.com and summarize"` completes a full tool-call round-trip against Gemma 3-IT + Ollama.
- [ ] `az-ai --check local` prints reachability, capabilities, and default model without sending a prompt, in ≤ 150 ms.
- [ ] Cold-start for `az-ai --provider azure …` is within ±10 % of the v2.0 baseline (no regression from the selector indirection).
- [ ] `az-ai --provider local --endpoint http://169.254.169.254/` exits with code 1 and `[ERROR] endpoint blocked by SSRF policy`.
- [ ] `az-ai --provider local --agent "…"` against a model declared `tool_calling = false` exits with code 1 and the clear error message from §4.4.
- [ ] No new NuGet dependencies in `azureopenai-cli-v2.csproj`.
- [ ] No new AOT trim warnings (`make publish-aot` produces a clean build).
- [ ] Existing `1001+` unit tests pass unchanged.
- [ ] ≥ 30 new unit tests covering the selector, precedence chain, SSRF allowlist, capability probe, and fallback policy.
- [ ] Opt-in integration test job (`local-models.yml`) passes against Ollama + llama.cpp sidecars in CI.

## 11. Out of Scope

Explicitly **not** in FR-018. Each is potentially a future FR; none are load-bearing for v1:

- **Shipping a bundled model.** az-ai does not download, unpack, or host weights. Users run their own `ollama serve` / `llama-server`. Distribution of a 4-27 B model file is outside our charter, our license posture (Jackie), and our binary-size budget (Jerry).
- **GPU / VRAM management.** We do not probe available VRAM, select quantizations, or advise on offload layers. That is the local runtime's job.
- **Embeddings endpoint.** `/v1/embeddings` for RAG-style use cases is a separate FR. The provider abstraction is designed to extend cleanly, but we ship chat only.
- **Fine-tuning / LoRA workflows.** Out of scope forever. Use `ollama create` or llama.cpp's own tooling.
- **Non-OpenAI-compatible local servers.** vLLM, Text Generation Inference, LM Studio -- many speak OpenAI-compat mode and will work incidentally. If they don't, that's not an FR-018 bug; it's an FR-019.
- **Running az-ai *as* a local server.** That is FR-013 (MCP server role).
- **Automatic model download / `ollama pull` on demand.** No implicit network fetches from the CLI. If the model isn't present, the server errors and we surface that error cleanly.
- **Streaming token usage when the server doesn't report it.** We report what the server reports; we do not estimate.
- **Provider-aware prompt rewriting.** The same system prompt goes to whichever provider is selected. Capability-aware prompt rewriting (e.g., "this model is small, shorten the system prompt") is a Maestro follow-up, not this FR.

---

**Review queue:** Costanza (product), Kramer (implementation), Newman (SSRF + keys), Morty (cost telemetry), Jerry (AOT + HTTP pipeline), Frank (observability), Elaine (docs), Mr. Pitt (scope + scheduling against FR-014). Soup Nazi to gate merge on format/style compliance. Wilhelm to gate on change-management process (ADR-005 required for the provider-abstraction decision).
