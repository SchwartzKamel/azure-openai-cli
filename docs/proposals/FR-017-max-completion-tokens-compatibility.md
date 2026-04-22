# FR-017 -- v1.9.1 Hotfix: Send `max_completion_tokens` for new-generation models

**Status**: ✅ Fixed in v1.9.1 (2026-04-20)
**Severity**: High
**Filed**: 2026-04-20 (surfaced during Phase 0 Agent Framework spike)
**Assignee**: Kramer
**Target release**: v1.9.1

## Summary

The handrolled path sends `max_tokens` in the Azure OpenAI chat completion request. New-generation Azure deployments reject this:

```text
[ERROR] ClientResultException: HTTP 400 (invalid_request_error: unsupported_parameter)
Parameter: max_tokens
Unsupported parameter: 'max_tokens' is not supported with this model. Use 'max_completion_tokens' instead.
```

Reproduced against `gpt-5.4-nano` on `https://sierrahackingco.cognitiveservices.azure.com/` with `api-version=2025-04-01-preview`.

Affected models include at least: `gpt-5.x`, `o1`, and likely the whole Responses-API family.

## Fix

Detect the deployment name / API shape and route token-cap to the correct field. In `Azure.AI.OpenAI` 2.1.0:

- `ChatCompletionOptions.MaxOutputTokens` maps to `max_completion_tokens` on newer models (auto-switched by the SDK in recent versions)
- Older SDK releases hardwired `max_tokens`

Likely one-liner: upgrade `Azure.AI.OpenAI` to a version that maps `MaxOutputTokens` → `max_completion_tokens` by default, or switch our call site to the newer property. Verify with an AOT + JIT run against the test endpoint.

## Resolution (2026-04-20)

The SDK upgrade alone was **not** sufficient. `Azure.AI.OpenAI` 2.9.0-beta.1 still
serializes `ChatCompletionOptions.MaxOutputTokenCount` as `max_tokens` on the
Chat Completions wire path for back-compat. The SDK ships an opt-in extension
method for the new wire property:

```csharp
#pragma warning disable AOAI001  // experimental API
requestOptions.SetNewMaxCompletionTokensPropertyEnabled(true);
#pragma warning restore AOAI001
```

Applied at both Chat Completions call sites in `Program.cs` (standard/agent
path at line ~614, ralph iteration at line ~1573). Safe to always enable --
older models (`gpt-4o`, `gpt-4o-mini`) also accept `max_completion_tokens`.

**Verified**: JIT + AOT both return clean responses from `gpt-5.4-nano` on
`api-version=2025-04-01-preview`. `1001/1001` tests still pass.

## Acceptance criteria

- [x] Non-trivial prompt to `gpt-5.4-nano` returns a streamed response without HTTP 400
- [x] `1001/1001` tests still pass (no `gpt-4o` regression on existing mocks)
- [x] CHANGELOG note: "v1.9.1 fixes gpt-5.x / o1 compatibility"
- [ ] Live `gpt-4o` regression guard (deferred -- no gpt-4o deployment on current endpoint)

## Related

- FR-016 (AOT reflection regression; separate but bundled in same hotfix)
- `docs/adr/ADR-004-agent-framework-adoption.md`
