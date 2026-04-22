# ADR-005 -- Azure AI Foundry endpoint routing

**Status**: 🟡 Proposed  
**Date**: 2026-04-20  
**Deciders**: Costanza (product), Kramer (engineering), Morty (FinOps), The Maestro (prompts)  
**Supersedes**: none  
**Related**: ADR-004 (MAF adoption); `docs/cost-optimization.md` §3.6; `.env` (staged Foundry creds)

## Context

Morty's analysis of Phi-4-mini-instruct (cost-optimization.md §3.6) identifies it as a **serious cost challenger**: ~2× cheaper than gpt-4o-mini on input/output, Microsoft-native, same compliance posture, built for Espanso use cases. The blocker: **hand-rolled `Program.cs` cannot reach Azure AI Foundry endpoints today.** Current auth flow routes `AZUREOPENAIENDPOINT` + key → `AzureOpenAIClient` → Azure OpenAI SDK. Foundry uses the same OpenAI-compatible chat-completions wire protocol but a different hostname (`services.ai.azure.com/models`) and deployment-name convention. This ADR routes that traffic without overengineering.

The spike's Foundry path (`spike/agent-framework/Program.cs` §148) is a `NotImplementedException` stub. This is about shipping the hand-rolled path first--lean, contained, no MAF dependency.

## Decision

**1. SDK strategy: Reuse `OpenAI.Chat.ChatClient` with custom base URL.**

Foundry exposes OpenAI-compatible chat-completions at `{endpoint}/openai/deployments/{model-id}/chat/completions?api-version=2024-05-01-preview`. The existing `Azure.AI.OpenAI` SDK already speaks this dialect when handed a custom base URL. We inject the Foundry URL + api-version query param at client construction, zero branching at call sites. **Tradeoff winner**: zero new dependencies, API parity with Azure OpenAI path, smallest AOT footprint. Raw `HttpClient` is leaner but loses streaming support; `Azure.AI.Inference` is first-party but introduces a second SDK to maintain.

**2. Env-var precedence: `AZURE_FOUNDRY_ENDPOINT` wins if set AND model is Foundry-routable.**

When *both* `AZUREOPENAIENDPOINT` and `AZURE_FOUNDRY_ENDPOINT` are present, check: does the model name (`AZUREOPENAIMODEL`) appear in a hard-coded list of known Foundry models (`Phi-4-mini-instruct`, `Phi-4-mini-reasoning`, `DeepSeek-V3.2`)? If yes, use Foundry. If no, fall back to Azure OpenAI. This keeps the v1 contract stable: existing `AZUREOPENAIMODEL=gpt-5.4-nano` users see zero change. Phi users opt in by setting both env vars (or a future `--endpoint foundry` flag if we choose to add one, but **not for v1**).

**3. API-version threading: Bake `2024-05-01-preview` into the Foundry endpoint URL at client construction.**

Foundry requires the api-version in the query string; Azure OpenAI SDK manages it internally. We construct the Foundry base URL with the version already embedded (`{endpoint}?api-version=2024-05-01-preview`), so the SDK's standard chat-completions path still works unchanged. One-time cost at auth time, zero branch at call sites.

**4. Flag surface: Env vars only in v1. Document explicit flag for v2.**

Do not add `--endpoint` flag to the v1 CLI. Users who want Foundry set `AZURE_FOUNDRY_ENDPOINT` + model env vars. Future v2 (under MAF) can gate on `--endpoint foundry` if UX demands it. This keeps v1 CLI surface stable and reserves the flag for unified MAF auth dispatch later.

**5. System prompt: No twist for Phi in v1.**

Morty notes Phi-4-mini's instruction-following is shakier than OpenAI models (cost-optimization.md §3.6 callout: "UsernameConfirmed data point"). Do NOT tweak the default system prompt when routing to Foundry. Let Bania's bench quantify whether a prompt change helps. If it does, file a separate FR. Ship v1 with standard prompt on all models.

**6. Backwards-compat matrix: Zero change unless opted in.**

Existing `gpt-5.4-nano` / `gpt-4o-mini` users see zero diff: they don't set `AZURE_FOUNDRY_ENDPOINT`, so `ValidateConfiguration()` uses the Azure OpenAI path as today. Phi users set both env vars. Tests for both paths--existing Azure OpenAI path must pass without modification.

## Consequences

### Positive
- **Lean scope**: ~50-80 LOC in `Program.cs` (`ValidateConfiguration`, client construction, URL binding). No new dependencies.
- **Phi-4-mini unblocked**: Morty can run cost benchmarks against live Foundry endpoint; Bania's harness feeds into v1.11.0 roadmap decision.
- **Reversible**: if Foundry routing breaks, users fall back to Azure OpenAI by unsetting `AZURE_FOUNDRY_ENDPOINT`.
- **MAF-clean**: this hand-rolled path doesn't conflict with MAF adoption in v2.0; the spike's Foundry `NotImplementedException` is independent.

### Negative
- **Hard-coded model list**: updating the Foundry-routable models requires a code change. Mitigated by documenting the list in code comments + adding to CONTRIBUTING.md.
- **Query-string URL binding**: if Foundry changes the api-version or URL structure, we re-bind at constructor. Small maintenance burden.
- **No AAD support for Foundry in v1**: only API-key auth. AAD + Foundry ships under MAF in v2. Document as a known limitation.

## Implementation plan

1. **Extend `ValidateConfiguration()`**: add `AZURE_FOUNDRY_ENDPOINT` + model-list check. Return a flag indicating which path to take (Azure OpenAI vs. Foundry).
2. **Client construction dispatch**: if Foundry path, build base URL with embedded api-version query param; pass to `ChatClient` constructor.
3. **Test matrix**: 
   - Existing Azure OpenAI path (gpt-5.4-nano) still passes all tests.
   - New Foundry path (mock endpoint or skip if creds unavailable).
4. **Document**: Add a note to `CONTRIBUTING.md` listing known Foundry models and the env-var precedence logic.
5. **Staged `.env`**: verify the file's `AZURE_FOUNDRY_*` vars are present and match the live endpoint (already there; no action needed).

## Acceptance criteria

- ✅ All existing tests pass without modification.
- ✅ New test covers `AZURE_FOUNDRY_ENDPOINT` precedence (env var set → Foundry path chosen for known models).
- ✅ New test covers fallback (env var set but model not in list → Azure OpenAI path chosen).
- ✅ `ValidateConfiguration()` returns both endpoint and routing choice.
- ✅ Binary size (AOT) does not increase.
- ✅ Morty can run Phi-4-mini-instruct cost bench against live Foundry endpoint post-merge.

## Open questions (for Bania's benchmark sprint)

These belong in `docs/spikes/phi-bench.md`, not in this ADR decision:

1. Strict JSON Schema mode on Phi-4-mini? (Does Foundry's endpoint respect `response_format: json_schema`?)
2. Function-calling reliability at 3.8B scale? (Benchmark Phi against our 6 built-in tools.)
3. Regional availability? (Is Phi-4-mini available in the user's region yet?)
4. Cold-start latency on Foundry serverless? (Measure warm vs. cold TTFT for Espanso baseline.)
5. Prompt-tuning ROI? (Does a tweaked system prompt improve Phi instruction-following on benchmark set?)

---

**Morty's sign-off**: "Phi-4-mini-instruct is the first serious Phi contender. Until Foundry routing lands in Program.cs, the gpt-4o-mini default stands by default-of-default. Ship this. Then we measure."
