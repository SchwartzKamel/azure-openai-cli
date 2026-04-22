# Chaos Drill Status

> **As of v2.0.5 (main):** 🟢 **READY.** The v2.0.0-era ship blockers
> (F1/F2/F3) are resolved with xUnit regression coverage. The drill itself
> hasn't been re-run end-to-end against v2.0.5 -- that's the remaining gap.

## Scope

This file answers the question the `docs/chaos-drill-v2.md` verdict line
(🔴 NOT READY for v2.0.0) left open: *is it ready now?* Short answer yes,
long answer below.

## Verdict table

| Item | v2.0.0 drill (FDR) | v2.0.5 (today) | Evidence |
|---|---|---|---|
| F1 -- `PersonaMemory.ReadHistory` RSS amplification | 🔴 open | 🟢 closed | `tests/AzureOpenAI_CLI.V2.Tests/PersonaMemoryHardeningTests.cs` -- `F1_ReadHistory_100MbFile_ReturnsAtMost32Kb`, `F1_ReadHistory_TailWindow_LandsOnUtf8Boundary`, `F1_ReadHistory_SmallFile_ReadsEntireFileNoTruncationMarker` |
| F2 -- `ReadHistory` hangs on device / unbounded files | 🔴 open | 🟢 closed | same file -- `F2_ReadHistory_DevUrandomSymlink_RefusesWithinTimeout`, `F2_ReadSeekableTail_CancellationTokenFires_OnSlowStream`, `F2_ReadHistory_RegularSmallFile_StillReadsCorrectly` |
| F3 -- Persona-name path traversal | 🔴 open | 🟢 closed | same file -- `[Theory]` with `../../canary`, `..\\..\\canary`, absolute paths; also `SquadConfig.Load` name-validation gate |
| F4 -- 50 MB config parse amplification | 🟡 accepted | 🟡 accepted | No xUnit guard. Config size-cap not implemented. Documented risk. |
| F5-F8 -- 🟡 noisy/accepted findings | 🟡 accepted | 🟡 accepted | Unchanged. Not ship blockers. |
| Drill re-run on v2.0.5 AOT binary | -- | ⏳ not run | `tests/chaos/run_all.sh` has not been executed against the v2.0.5 AOT snapshot. Last known green: v2.0.0 baseline. |

## What "READY" means here

- No known 🔴 chaos finding is open.
- Every F1/F2/F3 reproducer from `tests/chaos/11_persona_live.sh` has a
  corresponding xUnit regression test that runs on every CI green.
- `dotnet test azure-openai-cli.sln` is green at 1,510 tests (1,025 v1 +
  485 v2).

## What READY does **not** mean

- It does **not** mean the bash chaos harness has been re-run against
  v2.0.5. xUnit regressions pin the specific reproducers; they do not
  replay the entire 11-scenario drill. A regression could land in a
  non-PersonaMemory code path (argv parsing, signal handling, mock-server
  network flakes) and the xUnit suite would miss it.
- It does **not** mean the 🟡 findings are fixed. They are accepted,
  documented, and non-blocking -- not resolved.

## Remaining work (roadmap)

| Task | Owner | Blocking? | When |
|---|---|---|---|
| Re-run `tests/chaos/run_all.sh` on the v2.0.5 AOT binary and archive the `artifacts/results.tsv` under `docs/chaos-drill-v2.5.md` | FDR | No (not gating release) | Pre-v2.1 cut |
| Close F4 (config size-cap) or explicitly downgrade to ℹ️ informational | Kramer + Newman | No | Opportunistic |
| Wire `bash tests/chaos/run_all.sh` into `make chaos-test` so anyone can run the drill locally without memorising the invocation | FDR / Jerry | No | Opportunistic |
| Decide: does `make preflight` ever call chaos? (Current answer: no -- too slow, needs mock server.) Document the decision either way. | Puddy | No | Next audit pass |

## Cadence

Run the chaos harness:

- **Pre-release gate** for any minor-or-greater version (v2.1, v2.2, v3.0).
  Patch releases (v2.0.x) skip unless the patch touches process, signal,
  network, config, or persona code.
- **On-demand** after any PR that touches `azureopenai-cli-v2/Squad/`,
  `azureopenai-cli-v2/Program.cs` argv parsing, or
  `azureopenai-cli-v2/UserConfig.cs`.
- **Never** on every PR -- too slow, needs the mock server, too noisy for
  merge gating.

## How to re-drill (the short version)

```bash
# 1. Snapshot the AOT binary.
dotnet publish azureopenai-cli-v2 -c Release -r linux-x64 -p:PublishAot=true
mkdir -p tests/chaos/artifacts
cp azureopenai-cli-v2/bin/Release/net10.0/linux-x64/publish/az-ai-v2 \
   tests/chaos/artifacts/

# 2. Run everything. Every script writes to tests/chaos/artifacts/.
bash tests/chaos/run_all.sh

# 3. Read the tape. results.tsv is the shippable artefact.
cat tests/chaos/artifacts/results.tsv
```

Any new 🔴 finding → open an issue, file a reproducer under
`tests/chaos/NN_<name>.sh`, add the xUnit regression, annotate this file.

## Cross-references

- [`../chaos-drill-v2.md`](../chaos-drill-v2.md) -- the original FDR report.
- [`../../tests/chaos/README.md`](../../tests/chaos/README.md) -- harness
  runbook.
- [`../audits/docs-audit-2026-04-22-puddy.md`](../audits/docs-audit-2026-04-22-puddy.md)
  -- finding H6, which this file closes.

*Either the drill runs green or it doesn't. F1/F2/F3 run green. The bash
harness re-run is the last box to tick. High-five.*
