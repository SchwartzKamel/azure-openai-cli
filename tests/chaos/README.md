# tests/chaos — FDR adversarial drill against v2

Reproducible attack scripts used by the v2 chaos drill (`docs/chaos-drill-v2.md`).

```bash
# Build + snapshot the AOT binary, then run everything.
dotnet publish azureopenai-cli-v2 -c Release -r linux-x64 -p:PublishAot=true
cp azureopenai-cli-v2/bin/Release/net10.0/linux-x64/publish/az-ai-v2 tests/chaos/artifacts/
bash tests/chaos/run_all.sh
```

Every script writes to `tests/chaos/artifacts/`:
- `<id>.out` — first 1 KB of stdout per attack
- `<id>.err` — first 1 KB of stderr per attack
- `results.tsv` — id / label / rc / stdout / stderr, one row per attack

Do not commit `tests/chaos/artifacts/` — it contains test fixtures (including
100 MB files) and drill output.

---

## Scenario catalog

| Script | Attack surface | Owner | Drill section |
|---|---|---|---|
| `01_argv_injection.sh` | argv fuzzing — long flags, embedded NULs, shell metachars, UTF-8 boundary bytes, billion-dash flag floods. | FDR | `docs/chaos-drill-v2.md` §01 |
| `02_stdin_evil.sh` | stdin — closed/EOF, 100 MB line, mixed CRLF, invalid UTF-8, embedded ANSI escape codes. | FDR | §02 |
| `03_env_chaos.sh` | environment vars — oversized values, encoding attacks, variable-injection via `AZUREOPENAIMODEL`, `NO_COLOR` boundaries. | FDR | §03 |
| `04_config_chaos.sh` | `.azureopenai-cli.json` — 50 MB file (F4), malformed JSON, deep nesting, type confusion, permission-bit games. | FDR / Kramer | §04 |
| `05_squad_chaos.sh` | `.squad.json` — persona-name edge cases (pre-F3 hardening), oversized persona lists, self-referential squads. | FDR / Kramer | §05 |
| `06_persona_memory.sh` | persona history I/O — file perms, concurrent writes, symlink games (pre-F1/F2 hardening). | FDR / Kramer | §06 |
| `07_tool_chaos.sh` | built-in tools — SSRF, file-path traversal, symlink escape, oversized outputs, non-zero exit propagation. | FDR / Newman | §07 |
| `08_signal_chaos.sh` | SIGINT / SIGTERM / SIGHUP / SIGPIPE timing; Ctrl-C during tool call, during model streaming, during cache write. | FDR / Frank | §08 |
| `09_network_chaos.sh` | requires `mock_server.py` — partial streams, mid-stream disconnects, malformed SSE, 429 Retry-After, slow-loris. | FDR / Frank | §09 |
| `10_ralph_depth.sh` | Ralph loop depth bounds, infinite-tool-call detection, watchdog behaviour at depth limits. | FDR / Kramer | §10 |
| `11_persona_live.sh` | **live reproducers** for F1 (100 MB RSS amplification), F2 (`/dev/urandom` symlink hang), F3 (persona-name traversal). Kept green by `PersonaMemoryHardeningTests.cs`. | FDR / Kramer | §F1–F3 |

Every script writes to the shared `artifacts/` directory via the helpers in
`_lib.sh`. `run_all.sh` orchestrates all 11 and produces `results.tsv`.

## Current status

🟢 **READY** at v2.0.4 / v2.0.5 — see
[`../../docs/testing/chaos-drill-status.md`](../../docs/testing/chaos-drill-status.md)
for the verdict table, what "ready" means, and what doesn't.

The three original ship-blockers (F1/F2/F3) have xUnit regression coverage in
`tests/AzureOpenAI_CLI.V2.Tests/PersonaMemoryHardeningTests.cs`. If any of those
scripts regresses, the xUnit suite catches it on every PR.

## Cadence

Run the full harness (`bash tests/chaos/run_all.sh`):

- **Pre-release gate** for any minor-or-greater version bump (v2.1, v3.0).
- **On-demand** after any PR that touches `azureopenai-cli-v2/Squad/`,
  argv parsing in `Program.cs`, or `UserConfig.cs`.
- **Not** on every PR — the harness is slow, needs the mock server for
  script 09, and fights for the same ports under parallel CI.

Patch releases (v2.0.x) may skip the harness unless the patch touches
process, signal, network, config, or persona code.

## Mock server (`mock_server.py`)

Only `09_network_chaos.sh` requires it. Start manually before running 09,
or `run_all.sh` will skip that script and mark it SKIPPED in `results.tsv`
— not FAILED.

## Adding a new chaos scenario

1. Pick the next free `NN_<short_name>.sh` prefix.
2. Source `_lib.sh` for the standard helpers (`write_out`, `write_err`,
   `record_result`).
3. Writes **only** under `artifacts/`. Never mutate the user's `$HOME` or
   the repo tree.
4. Add a row to the scenario catalog above and a matching section in
   `docs/chaos-drill-v2.md` (or the next drill doc).
5. If the scenario surfaces a 🔴 finding, pair it with an xUnit regression
   test — the bash reproducer alone does not pin the promise.

## Cross-references

- [`../../docs/chaos-drill-v2.md`](../../docs/chaos-drill-v2.md) — original
  FDR report and per-attack detail.
- [`../../docs/testing/chaos-drill-status.md`](../../docs/testing/chaos-drill-status.md)
  — live status, roadmap, and re-drill instructions.
- [`../../docs/testing/coverage-matrix.md`](../../docs/testing/coverage-matrix.md)
  — which features have chaos coverage vs. don't.
- [`../AzureOpenAI_CLI.V2.Tests/PersonaMemoryHardeningTests.cs`](../AzureOpenAI_CLI.V2.Tests/PersonaMemoryHardeningTests.cs)
  — xUnit regressions pinning F1/F2/F3.

*Either the harness runs green or it doesn't. The findings it catches are
reproducible or they're not. No vibes here. High-five.*
