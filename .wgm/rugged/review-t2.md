# Review Ruggedness Record -- T2
date: 2026-05-17
phase: Review (T2)
scope: Startup & Runtime Optimization (Shared SocketsHttpHandler Prewarm Pool & Agent --max-rounds Cap Enforcement)
produced_by: inline rubric (companion /rugged protocol applied inline)

1. Actual operator & environment:
   Developers and scripts running `az-ai --prewarm` for low-latency text expansion or running `az-ai --agent --max-rounds N` against models that may loop on tool calls.
2. Bottleneck decomposition:
   - Intrinsic design constraint: `AzureOpenAIClient` uses `System.ClientModel.Primitives.HttpClientPipelineTransport` to wrap a custom `HttpClient`.
   - User capacity: Operators setting `--max-rounds` expect a hard ceiling on tool executions per agent invocation.
   - Operational stress (dominant): Unbounded tool loops burning tokens/time or `PrewarmAsync` hanging if `HttpClient.Timeout` is mutated concurrently across threads.
3. Simplify:
   Use a single `SharedSocketsHandler` + `SharedHttpClient` with per-call `CancellationTokenSource(TimeSpan.FromSeconds(3))` in `PrewarmAsync`, and wrap `AIFunction` instances in a thread-safe `MaxRoundsAIFunction` (`Interlocked.Increment`) when `maxRounds` is specified.
4. Exact check:
   `dotnet test tests/AzureOpenAI_CLI.Tests/AzureOpenAI_CLI.Tests.csproj -c Release --filter "FullyQualifiedName~StartupAndMaxRoundsTests|FullyQualifiedName~PrewarmTests" --nologo` (13/13 passed).
5. Verdict:
   RUGGED
