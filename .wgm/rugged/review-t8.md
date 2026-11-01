# Review Ruggedness Record -- T8
date: 2026-05-17
phase: Review (T8)
scope: Multi-Provider Router (--provider, AZ_PROVIDER, --providers [--json], --endpoint, --api-key) with Loopback Security Guard
produced_by: inline rubric (companion /rugged protocol applied inline)

1. Actual operator & environment:
   Developers and scripts switching between Azure OpenAI (`azure`), OpenAI (`openai`), OpenRouter (`openrouter`), local Ollama (`ollama`), local llama.cpp (`llamacpp` / `local`), and custom OpenAI-compatible endpoints (`custom`) via flags or `AZ_PROVIDER`.
2. Bottleneck decomposition:
   - Intrinsic design constraint: Native AOT + zero new NuGet dependencies; solved by routing non-Azure providers through `OpenAIClient` (already present transitively) with `HttpClientPipelineTransport(Program.SharedHttpClient)`.
   - User capacity: Operators running local Ollama/llama.cpp expect zero API-key ceremony and `http://localhost` to work out of the box.
   - Operational stress (dominant): Accidentally sending cloud API keys or prompts over plaintext `http://` to remote or LAN hosts (`192.168.x.x`).
3. Simplify:
   Strict `ProviderSelector.IsLoopbackHost` check allowing `http://` ONLY for `localhost`, `127.0.0.1`, and `::1` on local-capable providers (`ollama`, `llamacpp`, `custom`), while rejecting `http://` everywhere else.
4. Exact check:
   `dotnet test tests/AzureOpenAI_CLI.Tests/AzureOpenAI_CLI.Tests.csproj -c Release --filter "FullyQualifiedName~MultiProviderTests" --nologo` (10/10 passed).
5. Verdict:
   RUGGED
