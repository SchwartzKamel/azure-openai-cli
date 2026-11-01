# Review Ruggedness Record -- T1

date: 2026-05-17
phase: Review (T1)
scope: Machine I/O Contract (--json happy-path envelope, --schema wire enforcement, --code extraction) & Combined Stdin+Args Piping
produced_by: inline rubric (companion /rugged protocol applied inline)

1. Actual operator & environment:
   Shell pipelines, git hooks, CI scripts, and parent agents invoking `az-ai --json`, `az-ai --code`, `az-ai --schema`, or `cat file | az-ai "prompt"` in non-interactive environments where stdin may be an active pipe or an idle test-runner stream.
2. Bottleneck decomposition:
   - Intrinsic design constraint: Native AOT requires `ChatJsonResponse` to be source-generated in `AppJsonContext`.
   - User capacity: Callers piping stdout to `jq` or a file require zero human status noise on stdout/stderr.
   - Operational stress (dominant): Reading `Console.In` unconditionally when a positional prompt is already present can hang if an outer harness attaches an open idle pipe; solved via `HasRedirectedStdinDataAvailable()` (15 ms bounded peek) before `TryReadBoundedStdin` (1 MB cap).
3. Simplify:
   Single `FormatChatJsonResponse` helper and `ExtractCodeBlocks` parser shared across standard chat, agent, cache-hit, and Ralph modes.
4. Exact check:
   `dotnet test tests/AzureOpenAI_CLI.Tests/AzureOpenAI_CLI.Tests.csproj -c Release --filter "FullyQualifiedName~MachineIoAndCombinedStdinTests" --nologo` (11/11 passed; full suite 664/664 passed in 10 s).
5. Verdict:
   RUGGED
