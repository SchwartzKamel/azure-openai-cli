# Review Ruggedness Record -- T5

date: 2026-05-17
phase: Review (T5)
scope: NDJSON Event Streaming (--jsonl) & Machine-Readable Personas (--personas --json, --models --json)
produced_by: inline rubric (companion /rugged protocol applied inline)

1. Actual operator & environment:
   Parent AI agents, IDE extensions, and streaming scripts consuming `az-ai --jsonl` line-by-line or querying `--personas --json` / `--models --json`.
2. Bottleneck decomposition:
   - Intrinsic design constraint: NDJSON requires `WriteIndented = false` so embedded newlines in `text` are escaped as `\n` and each event is strictly one line.
   - User capacity: Callers expect immediate flush per chunk (`Console.Out.Flush()`) and zero stderr token footers in `--jsonl` mode.
   - Operational stress: Multi-line markdown chunks breaking naive line-oriented stream consumers if serialized with indentation.
3. Simplify:
   Serialize `JsonlStreamEvent` through `McpJsonContext` (`WriteIndented = false`) and reuse `AppJsonContext.Default.ListPersonaConfig` / `DictionaryStringString` for `--personas --json` and `--models --json`.
4. Exact check:
   `dotnet test tests/AzureOpenAI_CLI.Tests/AzureOpenAI_CLI.Tests.csproj -c Release --filter "FullyQualifiedName~JsonlAndMachineDiscoveryTests" --nologo` (4/4 passed).
5. Verdict:
   RUGGED
