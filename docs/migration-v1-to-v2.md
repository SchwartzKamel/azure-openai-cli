# Migrating from v1 to v2.0.0

> Target audience: existing `az-ai` users on v1.9.x upgrading to `az-ai-v2` /
> v2.0.0. For the internal phase plan that produced v2, see
> [`v2-migration.md`](v2-migration.md). For the cutover gate checklist, see
> [`v2-cutover-checklist.md`](v2-cutover-checklist.md).

**TL;DR** -- if you only consume `az-ai` from the command line, Espanso, or
AutoHotkey, your existing scripts keep working. Env vars, flags, exit codes,
and `--raw` output are unchanged. What's new is additive: nine new flags, an
observability layer, a cost estimator, and persona routing that actually runs.

---

## 1. What's new

v2 replaces ~2200 lines of hand-rolled chat/tool/workflow code with Microsoft
Agent Framework (MAF) primitives. The user-visible payoff:

- **MAF underpinning** -- `ChatClientAgent`, `AgentThread`, and MAF's function-tool
  interface replace the v1 bespoke loop. Streaming, tool calls, and Ralph
  retries all sit on top of MAF now. See [ADR-004](adr/ADR-004-agent-framework-adoption.md).
- **Observability (opt-in)** -- `--telemetry` (or `AZ_TELEMETRY=1`) emits
  OpenTelemetry spans and per-call cost events to stderr. Off by default.
  `--otel` / `--metrics` narrow the export to traces or meters only.
- **Cost estimator** -- `--estimate` / `--dry-run-cost` prints predicted USD for
  a prompt without making an API call. `--estimate-with-output <n>` adds a
  worst-case output cost for `n` completion tokens.
- **Persona routing is now wired** -- `--persona <name>` and `--persona auto`
  actually overlay the persona's system prompt, tools, and memory in v2. In
  early v2 previews the flag was parsed and silently ignored. See
  [`persona-guide.md`](persona-guide.md).
- **Nine new flags** -- machine-readable errors, schema capture, round cap,
  alternate config files, config CRUD, shell completions, version short form,
  and model-alias CRUD. Full list in section 3 of
  [`use-cases.md`](use-cases.md#whats-new-in-v2) and documented inline in
  `az-ai-v2 --help`.
- **FR-017 runtime fix** -- `gpt-5.x`, `o1`, and `o3` deployments no longer
  crash on the `max_completion_tokens` wire property. This was a latent bug
  in late v1.9.x; v2 ships with the fix baked in.

## 2. What is NOT breaking

v2 is a major version for **dependency** reasons (MAF is a public transitive
dep), not behavioral ones. The following surfaces are byte-identical to v1.9.1:

- **Env vars** -- `AZUREOPENAIENDPOINT`, `AZUREOPENAIAPI`, `AZUREOPENAIMODEL`,
  `SYSTEMPROMPT`, `AZURE_MAX_TOKENS`, `AZURE_TEMPERATURE`, `AZURE_TIMEOUT` all
  read the same way. The `KEY` vs `API` suffix trap is unchanged -- see
  [`prerequisites.md`](prerequisites.md).
- **Every v1 flag still works** -- `--raw`, `--agent`, `--ralph`, `--persona`,
  `--validate`, `--task-file`, `--max-iterations`, `--tools`, `--system`,
  `--temperature`, `--max-tokens`, `--timeout`, `--model`, `--squad-init`,
  `--personas`, `--help`, `--version`, `--config show`. Same spelling, same
  semantics, same short aliases (`-s`, `-t`, `-m`, `-v`, `-h`).
- **Config file path** -- `~/.azureopenai-cli.json` (user) and
  `./.azureopenai-cli.json` (project-local) are read unchanged. v2 adds
  `--config <path>` as an explicit override; it does not replace the defaults.
- **Exit codes** -- `0` success, `1` generic error, `2` config/validation error,
  `3` network/auth error, `99` internal error. `--json` preserves the same
  codes but routes the message to stdout as structured JSON.
- **Stdin handling** -- reading from a redirected stdin still has the 1 MB
  bounded cap. Error message on overflow is unchanged.
- **`--raw` semantics** -- stdout is model text only. No spinner, no token
  stats, no 🎭 persona banners, no telemetry. Safe to pipe into Espanso or
  AHK without any changes.
- **`.squad/` on-disk format** -- `.squad.json`, `.squad/history/<name>.md`,
  and `.squad/decisions.md` are read/written byte-identically. Your
  accumulated persona memory transfers across the upgrade.
- **Docker image name** -- `ghcr.io/schwartzkamel/azure-openai-cli:latest`
  continues to point at the v2 image post-cutover.

## 3. Behavioral deltas

Subtle things that changed internally. None of these affect correctness for
existing users; they are called out so you are not surprised.

| Area | v1.9.x | v2.0.0 |
|------|--------|--------|
| Ralph retry prompt | Re-injected the original task on every iteration | Shorter -- task carried by `AgentThread`; only the accumulated error context is re-sent |
| Telemetry | N/A | Emits **only** when `--telemetry` or `AZ_TELEMETRY=1` is set. Zero overhead and zero output when off. |
| Estimator path | N/A | `--estimate` short-circuits **before** credential and endpoint resolution. It will not read `AZUREOPENAIAPI`, will not hit the network, and works offline. |
| `--persona` routing | Parsed but not applied in early v2 | Fully wired: overlays system prompt + tools, forces agent mode, persists memory |
| FR-017 | Runtime crash on gpt-5.x/o1/o3 in late v1.9.x | Fixed; reasoning-model deployments work end-to-end |

Nothing in this table alters CLI output format or exit codes.

## 4. Cleaning up v1 leftovers

If you never touched `make install` or your shell rc, you have nothing to
clean - skip this section.

Otherwise: two targets help you find and remove stale v1 `az-ai` bits that
can silently shadow the v2 binary (the classic case is a docker-run alias
in `~/.bashrc` from 2024 that wins the PATH race against the new AOT
binary in `~/.local/bin/az-ai`).

- `make migrate-check` - read-only scan. Lists stale shell aliases, stale
  binaries on PATH, and stale v1 Docker images. Safe to run any time.
- `make migrate-clean` - dry-run by default (prints what it would do and
  exits 0). Re-run with `FORCE=1 make migrate-clean` to actually apply.
  Rc files are backed up to `<path>.bak-azai-<timestamp>` before edits.
  Docker images are never auto-removed - the command line is printed for
  you to run manually (cross-tag risk is too high to automate).

Your `~/.azureopenai-cli.json` config is left alone - v2 reads it as-is.

## 5. Rollback

v2 ships as `az-ai-v2` during the dual-tree window. After cutover, the v2
binary is installed as `az-ai`; the v1 binary remains available as a pinned
version for users who need it.

**During the dual-tree window** (pre-cutover):

```bash
# v1 (stable):
az-ai --version --short      # → 1.9.1

# v2 (parallel install):
az-ai-v2 --version --short   # → 2.0.0
```

Run both side by side until you are satisfied with v2. No config changes
needed -- both read the same env vars and config files.

**Post-cutover** (v2 becomes `az-ai`):

```bash
# Homebrew users -- pin v1:
brew uninstall azure-openai-cli
brew install schwartzkamel/tap/azure-openai-cli@1.9.1

# Scoop users:
scoop uninstall azure-openai-cli
scoop install azure-openai-cli@1.9.1

# Manual install -- download the 1.9.1 release asset:
#   https://github.com/SchwartzKamel/azure-openai-cli/releases/tag/v1.9.1
```

The v1.x branch continues to receive critical security fixes. No new features
will land there.

## 6. Performance notes

Detailed benchmarks land in `docs/perf-baseline-v2.md` (forward link --
document published alongside the 2.0.0 release). Summary:

- **Cold start** -- +7.6% measured at Phase 0 on `linux-x64` AOT (5.4 ms → 5.8 ms
  median). Below the ≤10% gate.
- **TTFT (time to first token)** -- within 5 ms of v1.9.1 baseline.
- **Streaming throughput** -- identical; MAF streams through the same
  `RunStreamingAsync` primitive we already validated.
- **Tool round-trip** -- +2.1 ms per tool call vs v1's handrolled loop.

If your use case is latency-critical (Espanso triggers, AHK hotkeys), the
hot path is safe. If you are orchestrating thousands of calls per second,
benchmark against `perf-baseline-v2.md` when it publishes.

## 7. Cost notes

- **Binary size** -- MAF adds to cold-start working set. Phase 0 measured the
  published AOT binary at ~19 MB after strip (up from ~9 MB in v1.9.x). Trim
  analysis is tracked as a follow-up.
- **`--telemetry` overhead when off** -- negligible. The OTel wiring is
  gated behind the flag and env var; zero allocations on the hot path when
  disabled.
- **`--estimate` path** -- zero-network. Reads the hardcoded price table (see
  [`cost-optimization.md`](cost-optimization.md)) or `AZAI_PRICE_TABLE` if set,
  formats the prediction, and exits. Safe to call from CI for budget gates.
- **Opt-in telemetry** -- cost events go to stderr as JSON; `--raw` suppresses
  them entirely.

## 8. Known limitations at 2.0.0

Tracked in the issue tracker; not blockers for cutover.

- **`--schema <json>` wire enforcement is deferred.** v2 parses and captures
  the schema argument but does not yet send it as a `response_format` strict
  schema. Use `--json` + post-validation in the meantime. Enforcement lands
  in 2.1.x.
- **Windows CI is not yet enabled.** Linux (glibc, musl, arm64) and macOS
  (x64, arm64) are tested in CI per release. Windows binaries are produced
  but validated manually pre-release. Tracked under `docs-build-ci`.
- **arm64 builds are not yet in the release matrix.** The v1.9.x release
  matrix (linux-arm64, osx-arm64, win-arm64) rolls forward to v2.0.1. If
  arm64 is your primary platform, pin v1.9.1 until 2.0.1 ships.
- **Multi-provider (FR-014) is a spike, not a feature.** v2 remains
  Azure-OpenAI-only. See `docs/spikes/fr-014-multi-provider.md`.

---

## Related reading

- [`v2-migration.md`](v2-migration.md) -- internal phase plan and status
- [`v2-cutover-checklist.md`](v2-cutover-checklist.md) -- Wilhelm's gate list
- [`persona-guide.md`](persona-guide.md) -- how persona routing works in v2
- [`use-cases.md`](use-cases.md) -- updated mode-by-mode walkthroughs
- [ADR-004](adr/ADR-004-agent-framework-adoption.md) -- why MAF

**Maintained by**: Elaine (docs), Wilhelm (change mgmt).
**Questions?** File an issue, or see [`CONTRIBUTING.md`](../CONTRIBUTING.md)
for the triage flow.
