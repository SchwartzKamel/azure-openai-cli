# FR-016 — v1.9.1 Hotfix: AOT Reflection Regression

**Status**: 🔴 Open (production-blocking for AOT deployments against modern Azure models)
**Severity**: High
**Filed**: 2026-04-20 (surfaced during Phase 0 Agent Framework spike)
**Assignee**: Kramer + Newman
**Target release**: v1.9.1 (hotfix on v1.9 line)

## Summary

The v1.9.0-alpha.1 Native AOT binary throws at runtime when processing a response from a modern Azure OpenAI Responses-API endpoint (e.g. `gpt-5.4-nano`):

```
[ERROR] InvalidOperationException: Reflection-based serialization has been disabled for this application.
Either use the source generator APIs or explicitly configure the 'JsonSerializerOptions.TypeInfoResolver' property.
```

This means **the AOT binary is non-functional for these endpoints in production**. The JIT build does not hit this path because reflection-based `System.Text.Json` is not disabled in JIT.

## Repro

```bash
export AZUREOPENAIENDPOINT=https://<your-res>.cognitiveservices.azure.com/
export AZUREOPENAIAPI=<key>
export AZUREOPENAIMODEL=gpt-5.4-nano   # or any gpt-5.x / o1 Responses-API model
echo "hi" | dist/aot/AzureOpenAI_CLI --raw
# → [ERROR] Reflection-based serialization has been disabled
# → exit 99
```

## Root cause hypothesis

Despite the `AppJsonContext` source-generator covering all types *we* serialize, some code path in the streaming-response handling (likely inside `Azure.AI.OpenAI` 2.1.0 or `OpenAI` 2.1.0 pass-through) falls back to reflection-based `JsonSerializer.Deserialize(..., options)` with a default `JsonSerializerOptions`. Under Native AOT, `JsonSerializer.IsReflectionEnabledByDefault` is `false`, so that path throws.

## Fix options (in order of preference)

1. **Surgical**: locate the specific deserialization call and pass a source-gen `JsonTypeInfo<T>` explicitly. Requires finding it via stack trace in a debug AOT build.
2. **Compatibility shim**: set `AppContext.SetSwitch("System.Text.Json.Serialization.EnableSourceGenReflectionFallback", true)` or the env-var equivalent at startup. Loses some trim aggressiveness but unblocks ship.
3. **Upgrade**: bump `Azure.AI.OpenAI` to the latest beta; newer versions have improved AOT/source-gen support.

Preference: option 1 if we can find it in a timeboxed pass, otherwise option 3 before option 2.

## Acceptance criteria

- [ ] AOT binary runs `echo hi | AzureOpenAI_CLI --raw` end-to-end against `gpt-5.4-nano` without the reflection error
- [ ] No new IL2026/IL3050 warnings beyond the current baseline
- [ ] Existing AOT cold-start benchmark unchanged (5.4-7 ms)
- [ ] Test case added that runs the AOT binary in CI against a mock Azure Responses endpoint

## References

- `docs/spikes/af-benchmarks.md` (run 2026-04-20)
- `docs/adr/ADR-004-agent-framework-adoption.md`
- `azureopenai-cli/JsonGenerationContext.cs`
