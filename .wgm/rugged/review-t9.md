# Review Ruggedness Record -- T9
date: 2026-05-17
phase: Review (T9)
scope: Built-in Commander / Work Queue Mode (az-ai queue / commander: init, add, list, status, plan, run, clear) & MCP Queue Tools
produced_by: inline rubric (companion /rugged protocol applied inline)

1. Actual operator & environment:
   Developers, CI pipelines, and parent MCP agents orchestrating multi-step DAG workflows (`az-ai queue plan` / `az-ai queue run`) across cloud and local providers (`azure`, `openai`, `openrouter`, `ollama`, `llamacpp`).
2. Bottleneck decomposition:
   - Intrinsic design constraint: Native AOT (`TrimMode=full`) requires `WorkQueueFile`, `QueueTaskItem`, and `QueueStatusSummary` to be source-generated in `AppJsonContext`.
   - User capacity: Operators need a single binary (`az-ai queue ...` / `az-ai commander ...`) rather than deploying and syncing a separate sibling orchestration repository.
   - Operational stress (dominant): Dependency cycles (`A -> B -> C -> A`), mid-run crashes leaving `queue.json` corrupted or tasks stuck in `running`, or failed upstream tasks allowing broken downstream tasks to execute.
3. Simplify:
   - DFS cycle + missing-dependency validation (`WorkQueue.ValidateDag`) before every `add`, `plan`, and `run`.
   - Atomic write-to-temp + rename (`WorkQueue.SaveAtomic`) on every task transition.
   - Automatic downstream `blocked` propagation when a prerequisite fails, plus `--recover-stale` to reset interrupted `running` tasks.
4. Exact check:
   `dotnet test tests/AzureOpenAI_CLI.Tests/AzureOpenAI_CLI.Tests.csproj -c Release --filter "FullyQualifiedName~CommanderQueueTests" --nologo` (6/6 passed).
5. Verdict:
   RUGGED
