# Changelog

All notable changes to Azure OpenAI CLI will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [2.0.3] — 2026-04-22

> **Re-tag to recover from infra-stuck v2.0.2 release.** v2.0.2 (`fd4ddc7`)
> shipped GHCR image cleanly, but `release-v2` publish was blocked by a
> ~13-hour GitHub Actions `macos-13` runner backlog. The
> `workflow_dispatch` recovery lever in `d2dc627` only applies to
> workflows on `main`; at the v2.0.2 tag, `release.yml` predates that
> commit and rejects `gh workflow run --ref v2.0.2` with HTTP 422. An
> intermediate `gh run rerun --failed` re-queued the cancelled `osx-x64`
> leg but hit the same pool saturation. Re-tagging at HEAD of `main`
> puts `workflow_dispatch` in-tag and gives the release pipeline a fresh
> queue slot. Post-mortem: [`docs/launch/v2.0.2-publish-handoff.md`](docs/launch/v2.0.2-publish-handoff.md).
>
> **No runtime changes from v2.0.2.** Same AOT binaries, same tool
> surface, same security posture. GHCR images `2.0.2` and `2.0.3` are
> bit-identical apart from version-embedded metadata.

### Changed
- `AzureOpenAI_CLI_V2.csproj` Version bump `2.0.2` → `2.0.3`.
- Tag `v2.0.3` picks up `d2dc627` (workflow_dispatch on `release.yml`),
  `641918d` (handoff doc), `ddb76ff` (corrected recovery recipe),
  `315726b` (FDR dogfood report), `f7a83fb` (Bania dogfood report) —
  all docs, no runtime delta.

## [2.0.2] — 2026-04-21

> **Fix-forward from v2.0.1.** v2.0.1 was tagged on `039e6bd` but did not
> publish: `docker-publish-v2` failed at
> `COPY --from=build /app/az-ai-v2 ...: not found` with the same error
> signature as v2.0.0 attempt #1. v2.0.1's Debian→Alpine SDK swap did
> **not** address the real root cause — the failure was libc-independent.
> Post-mortem:
> [`docs/launch/v2.0.1-release-attempt-diagnostic.md`](docs/launch/v2.0.1-release-attempt-diagnostic.md).
> The `v2.0.1` tag is retained as an "attempted release" marker alongside
> `v2.0.0`; v2.0.2 is now the first publicly published v2.x release.

### Fixed
- **`Dockerfile.v2` AOT asset-graph mismatch (real root cause)** — the
  v2.0.0/v2.0.1 pattern split build into `dotnet restore -r linux-musl-x64
  /p:PublishReadyToRun=true` followed by `dotnet publish --no-restore
  -p:PublishAot=true`. R2R and AOT resolve different RID-specific NuGet
  asset graphs; `--no-restore` then forbade publish from pulling the AOT
  assets restore had never fetched. The .NET 10 SDK, rather than
  hard-erroring, **silently fell back to a framework-dependent managed
  publish** — emitting `/app/az-ai-v2.dll` (plus runtimeconfig/deps) and
  no ELF at `/app/az-ai-v2`. Publish exited 0 in ~21s (no ILC/link phase)
  and the runtime-stage COPY then failed because the expected native
  binary never existed. Fix in this release: drop the separate restore
  step and the `--no-restore` flag; a single `dotnet publish` invocation
  resolves the AOT asset graph in one shot. Libc was never the issue —
  the Alpine SDK base is retained for cost/size, not correctness. Cites
  Lippman's Round-2 diagnostic above.
- **Dockerfile.v2 AOT-output verification gates** — three new `RUN`
  lines immediately after the publish step (`test -f /app/az-ai-v2`,
  `file /app/az-ai-v2 | grep -q ELF`, `/app/az-ai-v2 --version`) turn a
  silent managed-fallback regression into a red build at the publish
  layer instead of a cryptic COPY miss ~30s later in the runtime stage.
  Non-negotiable per Lippman's Round-2 playbook update
  (`docs/launch/release-v2-playbook.md` §Troubleshooting). `apk add file`
  added to the build stage so the `file(1)` check works on Alpine.
- **Version strings bumped** — csproj `<Version>`, `Program.cs`
  `VersionSemver`/`VersionFull`, `Observability/Telemetry.cs`
  `ServiceVersion`, `packaging/tarball/stage.sh` `VERSION`,
  `tests/integration_tests.sh` Gate 2 assertion — all rolled
  `2.0.1 → 2.0.2` in lock-step.

### Packaging
- **Versioned-pin manifests for 2.0.2** — new frozen siblings:
  `packaging/homebrew/Formula/az-ai-v2@2.0.2.rb`,
  `packaging/scoop/versions/az-ai-v2@2.0.2.json`, plus a `"2.0.2"` entry
  in `packaging/nix/flake.nix` `pinnedHashes`. SHA256 / SRI slots carry
  `TODO_FILL_AT_RELEASE_TIME` / `lib.fakeHash` sentinels per the tag-time
  ritual in `packaging/README.md`. Tracking manifests (`az-ai.rb`,
  `az-ai.json`, flake `version`) rolled to `2.0.2` with sentinels intact
  — they never got hash-synced for v2.0.1 (release didn't publish), so
  no prior hash state to reset.
- **`@2.0.0.rb` + `@2.0.0.json` + flake `"2.0.0"` pinnedHash entry**
  retained unchanged as historical markers.
- **`@2.0.1.rb` + `@2.0.1.json` + flake `"2.0.1"` pinnedHash entry**
  retained unchanged as historical markers — v2.0.1 joins v2.0.0 as a
  "tagged but never published" marker. Two dead-end markers now.

### Note
- v2.0.2 supersedes v2.0.1, which was tagged on `039e6bd` but did not
  publish (same publish-gate failure mode as v2.0.0 attempt #1 — the
  Alpine SDK swap in v2.0.1 did not address root cause). The immutable
  `v2.0.1` tag remains in the repo for audit trail; no artifacts were
  ever uploaded under that tag.

## [2.0.1] — 2026-04-21

> **Fix-forward from v2.0.0.** The `v2.0.0` tag on `b1fd2cd` is immutable
> and remains in the repo as an "attempted release" marker, but no
> artifacts published — `release.yml` run
> [24736776551](https://github.com/SchwartzKamel/azure-openai-cli/actions/runs/24736776551)
> failed in `build-binaries-v2 / win-x64` and `docker-publish-v2` before
> `release-v2` could run. v2.0.1 is the first publicly published v2.x
> release and supersedes the v2.0.0 tag on every channel (tarballs, GHCR,
> Homebrew, Scoop, Nix). Post-mortem:
> [`docs/launch/v2-release-attempt-1-diagnostic.md`](docs/launch/v2-release-attempt-1-diagnostic.md).

### Fixed
- **`stage.sh` win-x64 packaging** — `packaging/tarball/stage.sh` no
  longer shells out to Info-ZIP `zip` on `win-*` RIDs. `windows-latest`
  runners don't ship `zip`, and neither does the bundled Git-for-Windows
  MSYS bash. The Windows branch now uses PowerShell's always-present
  `Compress-Archive` (via `powershell.exe -NoProfile`), with `cygpath`
  translation so the MSYS paths resolve on the Windows side. Archive
  layout preserved (`az-ai-v2-<ver>-<rid>/` top-level directory) — brew /
  scoop manifests unchanged.
- **`Dockerfile.v2` NativeAOT cross-libc mismatch** — build stage
  switched from Debian-glibc `dotnet/sdk:10.0` to musl-native
  `dotnet/sdk:10.0-alpine`. Publishing for `linux-musl-x64` from a glibc
  host was silently emitting no ELF at `/app/az-ai-v2` (ILC cross-link
  fell through without a diagnostic; `docker-publish-v2` failed at
  `COPY --from=build /app/az-ai-v2 ...: not found`). With host libc and
  target RID both musl, no cross-link is required. Build-stage packages
  updated from `apt-get install clang zlib1g-dev` to `apk add clang
  build-base zlib-dev`. Runtime-deps stage (`runtime-deps:10.0-alpine`)
  unchanged. Image is structurally unchanged from the consumer side.
- **Telemetry `ServiceVersion` drift** — `Observability/Telemetry.cs`
  still reported `"2.0.0-alpha.1"` on OTel spans and meters even after
  the 2.0.0 tag. Corrected to `"2.0.1"` on this release. Flagged by
  Frank in [`docs/ops/telemetry-schema-v2.0.0.md`](docs/ops/telemetry-schema-v2.0.0.md).
- **AOT size figures reconciled (documentation-only).** Prior
  `[2.0.0]` entry reported 15.10 MB / 1.62× / shipped-with-waiver. The
  real shipped AOT size is **12.91 MB / 1.456× / no waiver required**,
  matching `docs/release-notes-v2.0.0.md`, `docs/perf-baseline-v2.md`,
  `docs/aot-trim-investigation.md`, and the `OptimizationPreference=Size`
  + `StackTraceSupport=false` levers landed on the csproj in `056920f`.
  The 15.10 MB figure was the pre-trim measurement and should never
  have stayed in the CHANGELOG. Corrected in this release. Ground truth:
  `docs/aot-trim-investigation.md` §Levers (row 7+8 = 13,533,472 bytes).

### Packaging
- **Versioned-pin manifests for 2.0.1** — new frozen siblings:
  `packaging/homebrew/Formula/az-ai-v2@2.0.1.rb`,
  `packaging/scoop/versions/az-ai-v2@2.0.1.json`, plus a `"2.0.1"` entry
  in `packaging/nix/flake.nix` `pinnedHashes`. SHA256 / SRI slots carry
  the `TODO_FILL_AT_RELEASE_TIME` / `lib.fakeHash` sentinels per the
  tag-time ritual in `packaging/README.md`. Tracking manifests
  (`az-ai.rb`, `az-ai.json`) already at `version "2.0.1"` from the prior
  G6 sweep.

### Docs
- **v2.0.0 release-attempt #1 post-mortem committed** —
  [`docs/launch/v2-release-attempt-1-diagnostic.md`](docs/launch/v2-release-attempt-1-diagnostic.md)
  (Lippman). Captures the job-matrix outcome, the two failure roots, and
  the recommended fix-forward path — what landed in this release.
- **v2 release playbook §Troubleshooting extended** —
  [`docs/launch/release-v2-playbook.md`](docs/launch/release-v2-playbook.md)
  gains the two observed failure-mode recipes so the next red run has a
  recipe-level fix already on-page.
- **Release notes carry a v2.0.1 banner** —
  `docs/release-notes-v2.0.0.md` gets an in-place note that v2.0.0 was
  tagged but never published; v2.0.1 supersedes it. Filename is
  preserved to avoid orphaning ~13 inbound CHANGELOG / contract / launch
  doc references.

## [2.0.0] — 2026-04-20

> Release window opens 2026-04-20 (commit-cutoff date for the 2.0.0 line). See
> [`docs/release-notes-v2.0.0.md`](docs/release-notes-v2.0.0.md) for the
> user-facing narrative and [`docs/migration-v1-to-v2.md`](docs/migration-v1-to-v2.md)
> for upgrade guidance.

v2.0.0 replaces ~2,200 lines of hand-rolled chat / tool / workflow code with
Microsoft Agent Framework (MAF) primitives and ships alongside v1 as a
separate binary (`az-ai-v2`) during the dual-tree window. For end users
invoking `az-ai` from the command line, Espanso, or AutoHotkey, no flag,
env var, config file, or exit code changes behavior. The major-version bump
is driven by the public transitive dependency surface — MAF, OpenTelemetry,
and an Azure SDK bump — not by CLI contract changes.

### Added
- **Microsoft Agent Framework runtime** ([ADR-004](docs/adr/ADR-004-agent-framework-adoption.md),
  commit `0b2e655`) — `ChatClientAgent`, `AgentThread`, and MAF function-tool
  primitives back the chat, agent, and Ralph code paths. The v1 bespoke loop
  is gone.
- **Persona routing wired end-to-end** (commits `0b2e655`, `cbcc49b`) —
  `--persona <name>` and `--persona auto` now overlay the persona's system
  prompt, tool allow-list, and `.squad/history/<name>.md` memory, force
  agent mode on, and update memory on session exit. In earlier v2 previews
  the flag was parsed and silently ignored. Full reference in
  [`docs/persona-guide.md`](docs/persona-guide.md).
- **Cost estimator** — [FR-015](docs/proposals/FR-015-pattern-library-and-cost-estimator.md),
  commit `0b2e655`. `--estimate` (alias `--dry-run-cost`) prints predicted
  USD for a prompt without making an API call;
  `--estimate-with-output <n>` adds a worst-case output cost for `n`
  completion tokens. The estimator short-circuits before credential or
  endpoint resolution — safe to call from CI budget gates offline.
- **Opt-in OpenTelemetry observability** (commit `0b2e655`) — `--telemetry`
  (or `AZ_TELEMETRY=1`) emits spans and per-call cost events to stderr.
  `--otel` / `--metrics` narrow the export to traces or meters only.
  Suppressed entirely by `--raw`. Zero allocation on the hot path when
  disabled.
- **Connection prewarming** — [FR-007](docs/proposals/FR-007-parallel-startup-and-connection-prewarming.md),
  commit `8e53851`. `--prewarm` opens the Azure OpenAI connection in
  parallel with prompt assembly to cut latency on the first interactive
  token.
- **Shell + streaming agent parity with v1** — [FR-005](docs/proposals/FR-005-shell-integration-and-output-intelligence.md)
  and [FR-011](docs/proposals/FR-011-agent-streaming-output.md), verified
  in commit `8e53851`.
- **Nine new flags reaching v1 parity plus v2 additions** (commit `0b2e655`):
  `--estimate`, `--estimate-with-output`, `--telemetry`, `--otel`,
  `--metrics`, `--prewarm`, `--config <path>`, `--schema <json>` (captured
  but not yet enforced on the wire — see _Deprecated / deferred_),
  `--version --short`.
- **`.azureopenai-cli.json` config reference** (commit `a309154`) — new
  `docs/configuration-reference.md` with sample file and WSL path gotchas.
- **v2 user documentation** (commit `cbcc49b`) —
  [`docs/persona-guide.md`](docs/persona-guide.md) (recreated for v2),
  [`docs/migration-v1-to-v2.md`](docs/migration-v1-to-v2.md), and a slimmer
  `docs/use-cases.md`. README refreshed for v2.
- **Perf baseline harness** (commit `3de364a`) — `scripts/bench.sh` and
  [`docs/perf-baseline-v2.md`](docs/perf-baseline-v2.md) provide the first
  v1↔v2 comparison (50 runs, warmup 2, linux-x64 AOT + framework-dependent).
- **OSS license artifacts** (commit `81a1e3a`) — `NOTICE` and
  `THIRD_PARTY_NOTICES.md` at the repo root, with an accompanying
  [`docs/licensing-audit.md`](docs/licensing-audit.md). Every 39-package
  dependency is MIT, Apache-2.0, or BSD-3-Clause. No copyleft.
- **ADR-006 split** (commit `cf7901b`) — original roundtable ADR decomposed
  into three focused ADRs (`ADR-006-nvfp4-nim-integration.md`,
  `ADR-007-third-party-http-provider-security.md`,
  `ADR-008-gpu-provider-bench-policy.md`) with verbatim appendix preserved.
- **v2 integration suite** (commit `488aebd`) — `tests/integration_tests.sh`
  now exercises v1 and v2 in parallel (29 assertions across 14 cases).

### Changed
- **Packaged as `az-ai-v2` during the dual-tree window.** The v1 binary
  continues to install as `az-ai`. Post-cutover, v2 becomes `az-ai`; v1
  remains available as a pinned version (`azure-openai-cli@1.9.1` on
  Homebrew / Scoop / manual download). See
  [`docs/migration-v1-to-v2.md`](docs/migration-v1-to-v2.md) §4.
- **`--max-rounds` semantics** (migration guide §3) — unchanged as a cap,
  but the loop termination is now driven by MAF's tool-call accounting.
  No behavior change is visible at the CLI boundary; logged round counts
  may differ by ±1 on edge cases.
- **Ralph retry prompt shape** (migration guide §3) — the task is now
  carried by `AgentThread`; only the accumulated error context is
  re-injected on each iteration. v1 re-sent the full original task every
  round. Output and exit codes are unchanged.
- **Streaming ordering guarantees** (migration guide §3) — streamed through
  MAF's `RunStreamingAsync` primitive. Byte-for-byte identical to v1 on
  `--raw`; banner ordering on interactive stderr may differ by a few
  microseconds.
- **Estimator short-circuits before credential resolution** (migration
  guide §3) — `--estimate` does not read `AZUREOPENAIAPI`, does not touch
  the network, and works offline. v1 had no estimator.
- **`OpenAI` transitive bumped 2.1.0 → 2.9.1** (licensing audit, commit
  `0b2e655`) via the MAF stack. Still MIT.
- **NOTICE truthfulness** (commit `81a1e3a`) — v1's NOTICE claimed "all
  dependencies are MIT." v2 introduces Apache-2.0 (OpenTelemetry, 4
  packages) and BSD-3-Clause (Google.Protobuf, 1 package) via
  observability and MAF transitive closure. `NOTICE` is updated to
  reflect that truthfully.

### Deprecated
- **`--schema <json>` wire enforcement is deferred to 2.1.x.** The flag
  is parsed and captured in v2.0.0 but is not yet sent as a
  `response_format` strict schema. Use `--json` + post-validation in the
  meantime.

### Removed
- **Handrolled chat / tool / Ralph orchestration** — the ~2,200-line v1
  loop is replaced by MAF primitives. Not user-visible; called out because
  extension points that reached into those internals (there are none in
  the public API, but downstream forks may have relied on them) no longer
  exist.
- **`.smith/` scratch directory** (commits `781741f`, `cd5fdc6`, `ce377f6`,
  `b654d97`) — internal scratch state; never shipped as an API.

### Fixed
- **[FR-017](docs/proposals/FR-017-max-completion-tokens-compatibility.md)
  baked in** — `gpt-5.x`, `o1`, and `o3` deployments no longer crash on
  the `max_completion_tokens` wire property. Originally shipped as a v1.9.1
  hotfix; v2 incorporates the fix from day one.

### Security
- **v1 hardening preserved byte-for-byte.** Every `ShellExecTool`,
  `WebFetchTool`, `ReadFileTool`, `GetClipboardTool`, and `ToolRegistry`
  defense landed in v1.0.x–v1.9.x carries forward unchanged in v2:
  command blocklist and metacharacter rejection, `ArgumentList`-based
  process spawning, HTTPS-only web fetch, private-IP DNS rebinding block,
  redirect-final-URL validation, symlink-traversal blocking, exact-alias
  tool matching, CTRL+C signal handling with exit code 130.
- **License audit: clear** (commit `81a1e3a`,
  [`docs/licensing-audit.md`](docs/licensing-audit.md)). 39 packages
  reviewed. 34 MIT, 4 Apache-2.0, 1 BSD-3-Clause. Zero copyleft.
  Attribution obligations discharged via `NOTICE` +
  `THIRD_PARTY_NOTICES.md`.

### Performance
- **Shipping-form (AOT, linux-x64) startup gates pass**
  ([`docs/perf-baseline-v2.md`](docs/perf-baseline-v2.md)). `--version
  --short` p95 is 1.12× v1 (12.58 ms mean). `--help` p95 is 1.23× v1.
  `parse-heavy` is _faster_ than v1 (0.93× mean) — the ParseArgs rework
  vindicated. Memory RSS is at or below v1 for every scenario.
- **AOT binary size: 8.86 MB → 12.91 MB (1.456×, +4.05 MB).** Inside
  the proposed 1.5× ratio gate — **passes without a waiver.** Cause:
  MAF host assemblies (whole-subgraph DI), OpenTelemetry API +
  exporters, and `Azure.AI.OpenAI 2.1.0` trim warnings that prevent
  full elision. `OptimizationPreference=Size` and
  `StackTraceSupport=false` (commit `056920f`) trimmed ~1.5 MB off an
  initial 14.41 MB / 1.625× build. A further residual-reflection trim
  pass is tracked for 2.1.x but is not blocking. Full lever analysis in
  [`docs/aot-trim-investigation.md`](docs/aot-trim-investigation.md).
- **Framework-dependent (`dotnet <dll>`) startup is ~1.5–1.7× v1.** This
  is not the shipping form — it exercises the JIT path, where MAF / DI /
  OTel types pay a one-time compile cost. Documented for completeness;
  not a gate.

### Verified
- `488 / 488` v2 unit tests passing; v1 suite continues green in parallel.
- 29 integration assertions across 14 cases against both binaries.
- AOT publish clean on `linux-x64`; IL2104 / IL3053 warnings confined to
  third-party assemblies, tracked for the 2.0.1 trim pass.

## [1.9.1] - 2026-04-20

### Fixed
- **gpt-5.x / o1 / o3 compatibility** ([FR-017](docs/proposals/FR-017-max-completion-tokens-compatibility.md)) — Chat
  Completions now send `max_completion_tokens` on the wire instead of the
  legacy `max_tokens`, unblocking modern Azure OpenAI Responses-API
  deployments that reject the old parameter with HTTP 400. Applied via the
  `SetNewMaxCompletionTokensPropertyEnabled()` opt-in at both the standard
  and Ralph iteration call sites. Safe for older models too — `gpt-4o`,
  `gpt-4o-mini`, and earlier accept both field names.
- **AOT reflection regression** ([FR-016](docs/proposals/FR-016-aot-reflection-regression-hotfix.md)) — Native AOT
  binary no longer throws `InvalidOperationException: Reflection-based
  serialization has been disabled` when streaming from modern endpoints.
  Fixed incidentally by the SDK upgrade below. AOT binary is now 8.9 MB
  (down from 9.1 MB), zero new trim/AOT warnings.

### Changed
- Upgraded `Azure.AI.OpenAI` `2.1.0` → `2.9.0-beta.1` (pulls `OpenAI` `2.1.0` →
  `2.9.1`). Required for `max_completion_tokens` support and AOT cleanliness.

### Verified
- End-to-end against `gpt-5.4-nano` on
  `api-version=2025-04-01-preview` (both JIT and AOT).
- `1001/1001` unit tests passing.

## [Unreleased - pre-1.9.1 notes]

### Added
- **`az-ai --completions <shell>`** — Emit shell-completion scripts for
  `bash`, `zsh`, and `fish`. Pipe into your rc file (e.g.
  `az-ai --completions bash >> ~/.bash_completion`) for flag / subcommand
  tab-completion.
- **Packaging scaffolds** — Homebrew formula, Scoop manifest, and Nix flake
  under [`packaging/`](packaging/) for third-party distribution channels.
  Not yet submitted to upstream taps/buckets; see `packaging/README.md`.
- **Verifying Releases guide** — [`docs/verifying-releases.md`](docs/verifying-releases.md)
  walks users through cosign / GitHub attestation verification for binaries,
  container images, and SBOMs.
- **Cost Optimization guide** — [`docs/cost-optimization.md`](docs/cost-optimization.md)
  covers model selection, token budgeting, caching, and per-persona cost
  profiles.
- **Trademark Policy** — [`docs/legal/trademark-policy.md`](docs/legal/trademark-policy.md)
  clarifies permitted use of the "Azure OpenAI CLI" / `az-ai` names.
- **ADR-002: Squad persona + memory architecture** —
  [`docs/adr/ADR-002-squad-persona-memory.md`](docs/adr/ADR-002-squad-persona-memory.md)
  records the persona-config + `.squad/` memory design.
- **FR-012 proposal: Plugin / tool registry** —
  [`docs/proposals/FR-012-plugin-tool-registry.md`](docs/proposals/FR-012-plugin-tool-registry.md)
  outlines a future extensibility surface for third-party tools.
- **Asciinema demo scripts** — [`docs/demos/`](docs/demos/) ships rehearsed
  terminal demos for standard, raw, and agent modes, ready for recording.
- **Contributor onboarding** — New `CONTRIBUTORS.md`, structured issue forms
  under `.github/ISSUE_TEMPLATE/`, and a PR template at
  [`.github/PULL_REQUEST_TEMPLATE.md`](.github/PULL_REQUEST_TEMPLATE.md).
- **Property-based tests for `CliParser`** — Boundary and invariant coverage
  using FsCheck-style generators. Total test count **541 → 925**.

### Changed
- **CI matrix expanded to `{ubuntu, macos}`** — `build-and-test` now runs
  on Ubuntu **and** macOS in parallel (fail-fast off), catching
  platform-specific regressions before release. `windows-latest` was in
  scope for this Golden Run but surfaced 26 pre-existing POSIX-path
  assumptions (`/bin/sh`, `/etc/passwd`, `/root/.ssh/*`) in
  `ToolHardeningTests` / `SecurityToolTests` that predate this release.
  Tracked as a v1.9 follow-up; the cross-OS *publish* matrix in
  `release.yml` continues to build + smoke-test `win-x64` / `win-arm64`
  binaries, so shipped Windows artifacts remain covered.

## [1.8.1] — 2026-04-19

### Fixed
- **Release workflow: CycloneDX SBOM tool pin** — The v1.8.0 release workflow
  failed in all five `build-binaries` matrix jobs because
  `dotnet tool install --global CycloneDX --version 4.0.2` referenced a version
  that does not exist on NuGet (latest is 6.x). As a result, no binary artifacts
  or binary-provenance attestations shipped for v1.8.0 (the container image and
  its attestation shipped cleanly and remain the anchor for `v1.8.0`). The tool
  is now pinned via a `.config/dotnet-tools.json` local manifest (CycloneDX
  6.1.1), restored with `dotnet tool restore`, and invoked as
  `dotnet dotnet-CycloneDX` with the current `--output-format Json` flag.
  Dependabot will surface future CycloneDX bumps as reviewable PRs.
- **Release runbook** — New `docs/runbooks/release-runbook.md` codifies the
  pre-flight checklist, tag-and-push sequence, post-release verification, and
  rollback / hotfix procedure so the next release does not re-discover the
  lessons learned from v1.8.0.

## [1.8.0] — 2025-11-20

### Changed
- **Native AOT promoted from experimental to recommended** — `make publish-aot`
  produces a **~9 MB single-file binary with ~5.4 ms cold start** on Linux x64,
  compared to ~54 ms for ReadyToRun and ~400+ ms for the Docker container path.
  That is roughly **10× faster than R2R and ~75× faster than Docker**, which is
  significant for Espanso/AutoHotKey text-injection workflows where every key
  sequence spawns a fresh process. `make publish` is now an alias for
  `publish-aot`; `publish-fast` (ReadyToRun) is retained for compatibility.
- **`Azure.AI.OpenAI` downgraded to 2.1.0 (stable GA)** — The project previously
  tracked the `2.9.0-beta.1` pre-release for tool-calling coverage. Tool calling
  works correctly on the stable `2.1.0` release, so the dependency has been
  moved back to a supported GA build. This removes pre-release transitive
  packages from the supply chain.
- **Remaining AOT warnings fixed** — Migrated `SquadConfig.Load` / `Save` and
  `SquadInitializer.Initialize` off reflection-based `JsonSerializer` overloads
  onto source-generated `AppJsonContext.Default.SquadConfig`. Added
  `ReadCommentHandling`, `AllowTrailingCommas`, and `PropertyNameCaseInsensitive`
  to `AppJsonContext`'s shared options so Squad config parsing stays forgiving.
- **Anonymous type eliminated in `OutputJsonError`** — Replaced with the new
  `ErrorJsonResponse` record registered in `AppJsonContext`.
- **`DelegateTaskTool` single-file safety** — Replaced
  `Assembly.GetExecutingAssembly().Location` (empty in single-file/AOT builds)
  with `Environment.ProcessPath` + `AppContext.BaseDirectory`. Child agents can
  now be spawned correctly from the AOT-published binary.
- **Retry/backoff logic consolidated** — Shared backoff helper reused across
  streaming and non-streaming code paths (~60 lines of duplication removed)
  without behavior changes.
- **`ParseCliFlags` scoped to `internal`** — Argument parser exposed to the test
  assembly via `InternalsVisibleTo` rather than being part of the public API
  surface.

### Added
- **Graceful cancellation on CTRL+C (SIGINT)** — A top-level signal handler
  cancels the in-flight operation, flushes the Ralph log / persona memory, and
  exits with code **130** (128 + SIGINT) per POSIX convention. Previously a
  CTRL+C could leave `.ralph-log` partially written.
- **Cross-platform publish targets** — New Makefile targets for all 7 supported
  Runtime Identifiers: `publish-linux-x64`, `publish-linux-musl-x64`,
  `publish-linux-arm64`, `publish-osx-x64`, `publish-osx-arm64`,
  `publish-win-x64`, `publish-win-arm64`, plus an aggregate `publish-all`.
- **`make install` / `make uninstall`** — Installs the AOT binary as `az-ai`
  on the user's `PATH` (`~/.local/bin` on Linux/macOS) and removes it.
- **`make bench`** — Invokes `scripts/bench.py` to measure cold-start latency
  of the locally-built AOT binary against a configurable number of runs.
- **`scripts/bench.py`** — Portable Python startup benchmark that captures
  wall-clock invocation time with statistical summaries (min/median/p95/max).
- **`CliParser` test coverage** — 71 new unit tests covering flag parsing,
  precedence, validation, and error paths.

### Tests
- Suite now passes **538 tests** (up from 454) — primarily from new
  `CliParser` coverage and cancellation tests.

The only remaining AOT publish warnings come from third-party assemblies
(`Azure.AI.OpenAI`, `OpenAI`) and do not affect runtime behavior.

## [1.7.0] — 2025-07-21

### Added
- **Token usage tracking** — Displays `[tokens: X→Y, Z total]` on stderr after every API call. Included in `--json` output as `input_tokens` and `output_tokens` fields
- **`--raw` flag** — Suppresses all formatting (no spinner, no newline, no stderr output). Designed for Espanso/AHK text expansion integration
- **TTY-aware output** — Spinners auto-suppress when stdout is piped (`Console.IsOutputRedirected`). Works even without `--raw`
- **Espanso/AHK integration guide** — New `docs/espanso-ahk-integration.md` with working configs for Espanso (Linux/macOS/Windows) and AutoHotKey v2
- **24 new tests** (454 total unit tests)

### Changed
- **AOT anonymous type elimination** — Replaced 2 anonymous types with source-generated `ChatJsonResponse` and `AgentJsonResponse` records in `AppJsonContext`. Removes last Native AOT blocker
- **ErrorAndExit DRY helper** — Extracted shared error handler replacing 8 duplicated error patterns (~40 lines saved). Consistent `[ERROR]` prefix across all error paths
- **DotEnv resilience** — `.env` file loading now wrapped in try-catch (missing/malformed `.env` no longer crashes)

### Security
- **Shell injection hardening** — `ShellExecTool` now blocks `$()`, backticks, process substitution (`<()`, `>()`), `eval`, and `exec`. Switched from string-interpolated `Arguments` to `ArgumentList` for proper OS-level escaping

## [1.6.0] — 2025-07-20

### Added
- **JSON Source Generators (AOT)** — `JsonGenerationContext.cs` with `AppJsonContext` providing source-generated serialization for `UserConfig`, `SquadConfig`, `PersonaConfig`, and all Squad types. Unblocks Native AOT compilation
- **CLI Validation** — Temperature validated to 0.0–2.0 range, max-tokens validated to 1–128000 range in `ParseCliFlags`
- **Rate-limit aware backoff** — Streaming retry now respects `Retry-After` header from Azure API (capped at 60s)
- **60+ new tests** — `JsonSourceGeneratorTests` (16), `ToolHardeningTests` (33), `ProgramTests` validation (11), integration tests (8 new)

### Changed
- **Dockerfile Optimization** — Added `PublishReadyToRun=true` for ~50% startup improvement; improved layer caching by copying `.csproj` first, restoring, then copying source
- **Makefile** — Fixed stale .NET 9.0 references → .NET 10.0

### Security
- **WebFetchTool SSRF redirect protection** — Validates final URL after HTTP redirects (HTTPS-only, no private IPs)
- **Tool parameter hardening** — All tools replaced `GetProperty()` with `TryGetProperty()` for graceful error handling on missing parameters

### Fixed
- **Console.Out race condition** — `RunRalphLoop` now guarantees `Console.Out` restoration via try-finally

## [1.5.0] — 2026-04-09

### Added
- **Persona System** inspired by [bradygaster/squad](https://github.com/bradygaster/squad) — AI team members with persistent memory
- `--persona <name>` flag: select named persona (coder, reviewer, architect, writer, security)
- `--persona auto`: auto-route to best persona via keyword-based routing
- `--personas`: list available personas from `.squad.json`
- `--squad-init`: scaffold `.squad.json` and `.squad/` directory with default team
- Persistent persona memory in `.squad/history/` — knowledge compounds across sessions
- Shared decision log in `.squad/decisions.md`
- `SquadCoordinator` for intelligent task routing with keyword scoring
- `SquadConfig` for JSON-based team configuration (`.squad.json`)
- `PersonaMemory` for per-persona history management with 32 KB cap and tail truncation
- `SquadInitializer` for scaffolding default squad with 5 personas and routing rules
- 5 default personas with specialized system prompts and tool selections
- ~46 new unit tests for Squad system
- Zero new dependencies — built entirely with `System.Text.Json`

## [1.4.0] — 2025-07-13

### Added
- **Ralph Mode** (`--ralph`): Autonomous Wiggum loop for self-correcting agent workflows
- `--validate <cmd>`: External validation command for Ralph loop iterations
- `--task-file <path>`: Read task prompt from file
- `--max-iterations <n>`: Control Ralph loop iteration limit (default: 10, max: 50)
- **DelegateTaskTool**: New built-in tool for subagent calling (`delegate_task`)
- Subagent recursion depth control via `RALPH_DEPTH` env var
- `.ralph-log` iteration history file
- 44 new tests (28 Ralph mode unit tests + 16 delegate tool tests)
- 20 new integration tests for Ralph mode flags

## [1.3.0] — 2025-04-09

### Security
- **ReadFileTool**: Fixed symlink traversal vulnerability; added prefix-based path blocking
- **ShellExecTool**: Expanded blocked commands (sudo, su, crontab, vi, vim, nano, nc, ncat, netcat, wget); close stdin on child process; HasExited guard before Kill
- **WebFetchTool**: Added DNS rebinding protection (private IP blocklist); limited redirects to 3; dynamic User-Agent from assembly version
- **GetClipboardTool**: Enforced clipboard size cap with truncation warning; PATH-based command detection
- **ToolRegistry**: Replaced substring matching with exact alias dictionary

### Added
- Parallel tool call execution via `Task.WhenAll` for concurrent agent tool rounds
- Accurate tool call counting in JSON output (`tools_called` field)
- CI: code formatting check (`dotnet format --verify-no-changes`)
- CI: NuGet vulnerability audit (`dotnet list package --vulnerable`)
- CI: Trivy container image scanning (CRITICAL/HIGH severity)
- CI: integration test job
- Makefile: `format`, `format-check`, `audit`, `all-tests` targets
- 104 new security unit tests (138 total)
- 11 parallel execution unit tests

### Changed
- Agent loop now executes multiple tool calls concurrently instead of sequentially
- Tool name matching uses explicit alias dictionary instead of broad substring search

## [1.2.0] — 2026-04-08

### Added
- **Agentic mode** (`--agent`): model can call built-in tools before responding
- 5 built-in tools: `shell_exec`, `read_file`, `web_fetch`, `get_clipboard`, `get_datetime`
- `--tools <list>` flag to restrict which tools are available (comma-separated)
- `--max-rounds N` flag to limit tool-calling iterations (default: 5)
- Agent-aware system prompt injection with available tool names
- JSON output includes agent metadata (rounds, tools_called) when `--agent --json` combined
- Tool safety: shell command blocklist, HTTPS-only web fetch, file size caps, path blocking
- Unit tests for tool registry and built-in tools
- Integration tests for agent mode CLI flags

### Changed
- Upgraded Azure.AI.OpenAI from 2.1.0 to 2.9.0-beta.1 (required for tool calling)
- Upgraded Azure.Core from 1.47.2 to 1.51.1
- `--json` flag now detected anywhere in args (previously required first position)
- Removed experimental `SetNewMaxCompletionTokensPropertyEnabled` call (incompatible across SDK versions)

### Security
- Shell tool blocks dangerous commands (rm, kill, mkfs, dd, etc.) and pipe chains containing them
- Shell command timeout (10s) and output size cap (64KB)
- File read tool blocks sensitive paths (/etc/shadow, /etc/passwd, etc.)
- Web fetch enforces HTTPS-only with timeout and response size cap

## [1.1.0] — 2026-04-08

### Added
- Stdin pipe support: `echo "question" | az-ai`, `cat file | az-ai "summarize"` — combines piped content with prompt arguments
- `--json` output mode for scripting and automation
- `--version` / `-v` flag to display current version
- Progress spinner (braille animation) on stderr while waiting for first token
- Input validation with 32K character prompt limit
- Azure-specific exception handling for HTTP 401, 403, 404, and 429 responses
- Configurable streaming timeout via `AZURE_TIMEOUT` environment variable
- Configurable `AZURE_MAX_TOKENS` and `AZURE_TEMPERATURE` environment variables
- Restrictive file permissions (chmod 600) on config file at creation
- API key validation before client creation
- HTTPS endpoint validation
- xUnit test project with 16 unit tests
- GitHub Actions CI/CD pipeline
- SECURITY.md — comprehensive security documentation
- ARCHITECTURE.md — system design and component documentation
- CONTRIBUTING.md — developer onboarding guide
- CODE_OF_CONDUCT.md — Contributor Covenant v2.1
- GitHub issue templates (bug report, feature request)
- Pull request template
- Copilot agent archetypes: Costanza (PM), Kramer (engineer), Newman, Elaine, Jerry
- 5 feature proposals (FR-001 through FR-005) with priority matrix and shipping timeline

### Changed
- Upgraded from .NET 9.0-preview to .NET 10.0 stable
- Upgraded Dockerfile base images from preview tags to stable
- Optimized Dockerfile layer ordering for faster rebuilds
- Removed redundant COPY instruction in Dockerfile
- Credentials now injected via `--env-file` at runtime instead of baked into image
- Makefile: added `help`, `test`, and `smoke-test` targets
- README: added badges, configuration reference table, and exit code documentation

### Fixed
- Removed `.env` credential bundling from Docker image (security vulnerability)
- Generic exception handling replaced with Azure-specific error handlers
- Stream null check for content delta updates

### Security
- Container credentials are no longer baked into Docker images
- API key is validated before Azure OpenAI client creation
- HTTPS endpoint validation prevents insecure connections
- Config file restricted to owner-only access (chmod 600 on Unix)

## [1.0.1] — 2025-12-04

### Added
- Multi-model selection support: `--models`, `--set-model`, `--current-model` flags
- Feature proposals README with priority matrix
- Copilot agent definitions (Costanza, Kramer)

### Fixed
- Improved exception handling based on code review feedback (modern C# range syntax, named constants)

## [1.0.0] — 2025-08-17

### Added
- Initial release
- Azure OpenAI chat completion via Docker container
- Streaming responses (token-by-token output)
- System prompt configuration via `SYSTEM_PROMPT` environment variable
- Docker-first architecture with Alpine Linux base image
- Non-root container execution (`appuser`)
- `.dockerignore` for minimal build context

### Security
- Switched to Alpine Linux for reduced attack surface (OWASP/Snyk compliance)
- Fixed 2 critical and several high-severity container vulnerabilities
- Vulnerability scanning integrated into workflow
