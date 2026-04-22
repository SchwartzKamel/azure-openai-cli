# Coverage Matrix

> **Purpose:** catalogue, not cure. This file answers "what flavour of test
> coverage exists for each feature?" It does **not** propose fixes for
> gaps -- fixing gaps is a separate decision owned by Kramer / Puddy /
> FDR / Newman per feature.
>
> Coverage numbers are not the point; coverage of the **risky** paths is.
> See [`README.md §8`](./README.md).

## Legend

| Symbol | Meaning |
|---|---|
| ✅ Yes | Coverage exists and is non-trivial. |
| 🟡 Partial | Thin coverage (one happy-path case, or only asserts a weak invariant). |
| ❌ No | Coverage is absent. |
| -- | Not applicable. (A pure-stdout feature has no sensible "chaos" column.) |
| ⏳ | Pending -- planned but not yet implemented. |

"Contract" here means an explicit contract test under the
[`contract-tests.md`](./contract-tests.md) definition.

## Matrix -- v2 surface

| Feature / area | Unit | Integration | Contract | Chaos | Notes |
|---|:---:|:---:|:---:|:---:|---|
| `--version` / `--version --short` | ✅ Yes | ✅ Yes | ✅ Yes | -- | `VersionContractTests`, `integration_tests.sh` smoke. |
| `--raw` stdout/stderr discipline | ✅ Yes | ✅ Yes | ✅ Yes | -- | `RawModeTests`. |
| Ralph mode -- exit codes | ✅ Yes | ✅ Yes | ✅ Yes | -- | `RalphExitCodeTests`, `RalphWorkflowTests`. |
| Ralph validator -- JSON tool-call shape | 🟡 Partial | ❌ No | ❌ No | ❌ No | Mode entry + exit codes covered; the validator's JSON output shape is not asserted. Audit gap §4. |
| Ralph depth / loop bounds | ✅ Yes | ✅ Yes | -- | ✅ Yes | `RalphModeTests`, `tests/chaos/10_ralph_depth.sh`. |
| Persona memory -- read tail | ✅ Yes | ❌ No | -- | ✅ Yes | `PersonaMemoryHardeningTests` F1-F3; `tests/chaos/06_persona_memory.sh`, `11_persona_live.sh`. |
| Persona name sanitisation (F3 traversal) | ✅ Yes | ❌ No | -- | ✅ Yes | `PersonaMemoryHardeningTests` F3 `[Theory]`; `SquadConfig.Load` validator. |
| Squad config parsing (`.squad.json`) | ✅ Yes | 🟡 Partial | -- | ✅ Yes | `SquadTests`, `SquadInitializerTests`, `tests/chaos/05_squad_chaos.sh`. |
| User config (`~/.azureopenai-cli.json`) | ✅ Yes | ❌ No | -- | ✅ Yes | `UserConfigTests`, `UserConfigQuietTests`, `tests/chaos/04_config_chaos.sh`. 🟡 HOME isolation -- see audit C1 in test-sanity-audit. |
| argv parsing | ✅ Yes | ✅ Yes | -- | ✅ Yes | `CliParserTests`, `CliParserPropertyTests`, `tests/chaos/01_argv_injection.sh`. |
| stdin handling | 🟡 Partial | ✅ Yes | -- | ✅ Yes | `tests/chaos/02_stdin_evil.sh`; xUnit coverage is thin (most code paths are process-level). |
| env var ingestion | ✅ Yes | ✅ Yes | -- | ✅ Yes | `UserConfigTests`, `V2FlagParityTests`, `tests/chaos/03_env_chaos.sh`. |
| Tool registry / built-in tools | ✅ Yes | ✅ Yes | ✅ Yes | ✅ Yes | `ToolTests`, `ToolHardeningTests`, `tests/chaos/07_tool_chaos.sh`. |
| `ShellExec` tool -- adversarial | 🟡 Partial | ❌ No | ❌ No | ❌ No | `tests/README.md` claims "edge cases"; reality is echo-hello happy path. Audit §4 / test-sanity M8 still open. |
| SSRF / symlink traversal | ✅ Yes | ❌ No | -- | ✅ Yes | `SecurityToolTests`, `ToolHardeningTests`, `tests/chaos/07_tool_chaos.sh`. |
| Retry / backoff | ✅ Yes | ❌ No | -- | ✅ Yes | `RetryTests` (incl. wall-clock backoff). |
| 429 rate-limit `Retry-After` parsing | ❌ No | ❌ No | -- | 🟡 Partial | Not asserted in unit tests; chaos defers (drill §F/9). Audit §4 gap. |
| Cancellation / SIGINT | ✅ Yes | ✅ Yes | -- | ✅ Yes | `CancellationTests`, `tests/chaos/08_signal_chaos.sh`. |
| Parallel tool execution | ✅ Yes | -- | -- | -- | `ParallelToolExecutionTests`. No wall-clock assertion (intentional -- see audit c861c2e). |
| Network failure handling | 🟡 Partial | ❌ No | -- | ✅ Yes | `tests/chaos/09_network_chaos.sh` (mock_server). xUnit coverage thin. |
| Cost estimation | ✅ Yes | ❌ No | -- | -- | `CostEstimatorTests`. |
| Prewarm / cache | ✅ Yes | ❌ No | -- | -- | `PrewarmTests`, `PromptCacheTests`. |
| Foundry routing | ✅ Yes | ❌ No | -- | -- | `FoundryRoutingTests`. |
| JSON source-generator contract | ✅ Yes | -- | -- | -- | `JsonSourceGeneratorTests`. |
| Publish target / AOT packaging | ✅ Yes | ✅ Yes | -- | -- | `PublishTargetTests`, release workflows. |
| Telemetry / observability | ✅ Yes | ❌ No | ✅ Yes | -- | `ObservabilityTests`, plus `VersionContractTests` pinning `ServiceVersion`. |
| `SECURITY.md` doc / code agreement | -- | -- | ✅ Yes | -- | `SecurityDocValidationTests`. |
| Console capture sequencing under parallel xUnit | ❌ No | -- | -- | -- | `[Collection("ConsoleCapture")]` convention exists; no test asserts capture ordering is deterministic. Audit §4 / test-sanity C2 open. |
| Windows path / SkippableFact infra | ❌ No | ❌ No | -- | -- | CI dropped `windows-latest` (test-sanity H4). 26+ POSIX paths hardcoded. |

## Matrix -- v1 surface (maintenance mode)

v1 is maintenance-only per CONTRIBUTING. No new coverage is added; existing
coverage listed above under the shared feature rows stays green.

## What this catalogue is **not**

- Not a TODO list. A ❌ cell is a *known* gap, not an auto-filed ticket.
  Filing requires a judgement call: is the gap worth the test cost?
  Puddy + feature owner decide per row.
- Not a measure of quality. `✅` means "coverage exists" -- not "coverage
  is sufficient." A thinly-asserted test is still `✅` here; upgrading
  thin-to-thick is a separate exercise.
- Not auto-generated. This file drifts -- refresh quarterly or after any
  cross-cutting testing PR. Last refreshed: 2026-04-22, baseline v2.0.5.

## How to read a row as a reviewer

- **All four columns empty (`❌ ❌ ❌ ❌`) on a user-facing feature:**
  smell. Ask if the feature is actually needed, or if the coverage is
  missing.
- **Contract ❌ but external consumer exists:** that's the
  [`contract-tests.md`](./contract-tests.md) "add one when" trigger.
- **Chaos ✅ but unit ❌ / 🟡:** the bash script is the only proof. Consider
  an xUnit regression to pin the specific reproducer (F1/F2/F3 did this).
- **Unit ✅ but integration ❌:** fine if the code path is pure. Not fine if
  the path involves process exit codes, signal handling, or argv parsing --
  those need real-binary coverage.

## Cross-references

- [`README.md`](./README.md) -- testing playbook.
- [`contract-tests.md`](./contract-tests.md) -- what earns the contract
  column.
- [`chaos-drill-status.md`](./chaos-drill-status.md) -- what the chaos
  column's ✅ cells currently prove.
- [`flaky-triage.md`](./flaky-triage.md) -- what the matrix cannot catch
  (test quality under repeated runs).
- [`../audits/docs-audit-2026-04-22-puddy.md`](../audits/docs-audit-2026-04-22-puddy.md)
  -- §4 coverage-gaps table the matrix extends and makes live.

*Either the cell is green or it isn't. This file is a map, not a promise.
High-five.*
