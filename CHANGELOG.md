# Changelog

All notable changes to Azure OpenAI CLI will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- **docs(security):** `docs/security/v2-audit.md` -- end-to-end
  audit of the v2 surface (credential stores, shell_exec,
  read_file, web_fetch SSRF, dependency vulns, subagent depth
  cap). Each protection paired with the attack it stops; verdict
  5 PASS / 1 NEEDS-FOLLOW-UP / 0 GAP. ([s02e13-the-inspector])
- **docs(legal):** `THIRD_PARTY_NOTICES.md` extended with a v1.x dependency manifest covering all 15 packages (3 direct, 12 transitive) in the production CLI closure, all MIT-licensed. ([s02e15-the-lawyer])
- **docs(legal):** `docs/legal/license-audit.md` is the v1 OSS license audit -- per-package classification, license-obligation posture, Lloyd callouts on MIT vs Apache 2.0 and GPL contagion, and the exact `dotnet` commands to refresh attribution before each release. ([s02e15-the-lawyer])
- **docs(market):** `docs/competitive-landscape.md` -- Sue Ellen
  Mischke's tight landscape brief against five credible CLI / TUI
  alternatives, naming three differentiators we lean into (per-OS
  keystore, AOT single binary, Azure-specific first-run wizard) and
  three gaps we accept (Azure-only, no TUI, no multi-model routing /
  prompt library). Includes three Peterman draft positioning
  paragraphs marked "not yet adopted." ([s02e19-the-competition])
- **docs(i18n):** `docs/i18n-audit.md` inventories every user-facing
  string in the v1 CLI binary and classifies each as locale-agnostic,
  translation-ready, or needing refactor before l10n can begin. No
  strings translated; no `--locale` flag added. ([s02e08-the-translation])
- **docs(glossary):** `docs/glossary.md` is the new project glossary --
  AOT, DPAPI, libsecret, i18n, l10n, RTL, CJK, MCP, TPM, RPM, SSRF.
  Single source of truth; future episodes append. ([s02e08-the-translation])
- **docs(observability):** New `docs/telemetry.md` -- one-page honesty
  pass on the project's zero-default-telemetry posture, with the grep
  commands a contributor can run to audit the tree themselves.
  ([s02e07-the-observability])
- **docs(observability):** New `docs/incident-runbooks.md` -- three
  short runbooks (401 auth, 429 rate limit, DNS/TLS) for the
  user-facing failures we see most often, plus a plain-English Lloyd
  callout on what an SLO is. ([s02e07-the-observability])
- **docs(product):** `docs/user-stories.md` -- one-paragraph user
  stories translating every shipped S02 feature (and a handful of
  pre-S02 catch-up entries) out of engineering jargon, grouped by
  user role. ([s02e11-the-spec])
- **feat(a11y):** `NO_COLOR` and `FORCE_COLOR` env-var gates land in v1
  via the new `AzureOpenAI_CLI.ConsoleIO.AnsiPolicy` helper — the
  forward-looking chokepoint for any future color in the v1 binary.
  Precedence is `NO_COLOR` (off, any non-empty) > `FORCE_COLOR` (on,
  any non-empty other than `"0"`, even when stdout is redirected) >
  TTY auto-detect. Kept in lockstep with v2's `Theme.UseColor()` so
  users get one set of rules across both binaries. The first-run
  wizard now prints a one-line announcement
  (`Your key will be masked as you type. Press Enter when done.`)
  before the masked key prompt — Mickey's gift to screen-reader
  users, since a stream of bullet glyphs with no warning is hostile.
  The announcement is suppressed on the redirected-stdin path where
  no masking actually happens. The `docs/accessibility.md` contract
  picks up a v1 section documenting all of the above plus the clean
  abort path. ([s02e06-the-screen-reader])
- **feat(bench):** `make bench-quick` target — a 5–10 s directional
  cold-start smoke (N=50, no warm-up, stdout only) for the pre-commit /
  dev loop. Sits below `make bench` (mid-PR, N=100) and `make bench-full`
  (pre-merge, N=500, `--flag-matrix`). See
  [`docs/perf/bench-workflow.md`](docs/perf/bench-workflow.md).
- **ci(bench-canary):** New `bench-canary` job in `.github/workflows/ci.yml`
  runs `make bench-quick` on every push / PR (`needs: build-and-test`) and
  posts the table to the GitHub Actions step summary under
  `## bench-canary (directional only)`. Explicitly **not** a regression
  gate — shared-runner jitter (±30 %) makes it useless for precise
  comparisons; pinned-rig numbers (`make bench-full` per
  `docs/perf/reference-hardware.md`) remain authoritative. The bench step
  uses `continue-on-error: true` so noisy numbers cannot redden CI.
- **feat(credentials):** Opportunistic libsecret credential store on
  Linux. When `/usr/bin/secret-tool` is present and a DBus session
  bus is available, `az-ai` now stores credentials via libsecret
  (GNOME Keyring / KDE Wallet) instead of plaintext. Falls back to
  plaintext on systems without libsecret or without a session bus
  (headless containers, minimal installs). Zero new NuGet deps.
- **docs(perf):** Reference-hardware pinning doc (`docs/perf/reference-hardware.md`)
  captures the canonical bench rig (`malachor`, i7-10710U, linux-x64), the
  pre-merge protocol (governor=performance, AC power, N=500, warm-up=5,
  `--flag-matrix`), and the tolerance bands (±5 % noise, 10 – 20 %
  regression flag, > 20 % regression block). ([bania-v2-03])
- **docs(perf):** v2 cold-start p99 investigation
  (`docs/perf/v2-cold-start-p99-investigation.md`) — closes the baseline's
  p99 watchlist item as *rig noise, not a code defect*, with N=500 evidence
  across flag matrix and a runtime-knob sweep. The 20.9 % `--help` p50
  drift item stays open but is re-scoped to the pinned-rig re-run.
  ([bania-v2-02])
- **scripts(bench):** `scripts/bench.py` promoted to a first-class
  pre-merge perf harness with `--n`, `--warmup`, `--flag-matrix`, and
  `--json` options, plus an env fingerprint (CPU model, governor, kernel,
  binary size) on every run. `make bench-full` wires the canonical
  `N=500 --flag-matrix` sweep and writes dated JSON + text bundles to
  `docs/perf/runs/`. ([bania-v2-03])
- **feat(setup):** First-run setup wizard — when credentials are
  unresolvable on an interactive terminal, `az-ai` now prompts for
  endpoint, API key (masked input), and model, then validates via a
  test ping before persisting. Re-runnable any time via `--init`
  (aliases `--configure`, `--login`).
- **feat(setup):** Per-OS native credential storage ("living off the
  land" — zero new NuGet dependencies, AOT-safe):
  - **Windows:** DPAPI via `crypt32.dll` P/Invoke, user-scoped.
  - **macOS:** Apple Keychain via `/usr/bin/security` (service
    `az-ai`).
  - **Linux / containers:** plaintext at `~/.azureopenai-cli.json`
    with mode `0600` (matches AWS CLI / GitHub CLI / Azure CLI
    baseline; rotate keys per documented guidance).
- **feat(config):** `ApiKeyFingerprint` (`sha256(key)[0..12]`) shown
  in `--config show` for safe display and tamper detection; the
  actual key is never printed.
- **docs(proposals):** `docs/proposals/FR-NNN-first-run-wizard.md`
  (number assigned by the docs-fr task) captures motivation, per-OS
  LOLBin design, rejected alternatives, and the Newman + Rabbi
  signoff for the Linux plaintext trade-off.
- **Backward compatibility:** environment variables retain full
  precedence. CI, Docker, Espanso, AHK, and any scripted workflow
  exporting `AZUREOPENAIENDPOINT` / `AZUREOPENAIAPI` /
  `AZUREOPENAIMODEL` is unaffected. The wizard runs only when
  credentials are unresolvable **and** the terminal is interactive
  **and** neither `--raw` nor `--json` is set **and** we are not
  in a container.
### Changed
- **feat(ralph):** Ralph `--validate <cmd>` validation loop now defaults to a
  low sampling temperature (0.15) when the operator has not explicitly pinned
  one via `--temperature` or `AZURE_TEMPERATURE`. High creative temperature
  made the pass/fail verdict oscillate across iterations; 0.15 keeps the loop
  deterministic. Precedence: CLI flag > env var > validate default (0.15) >
  general default (0.55). Non-validate runs still get the 0.55 creative
  default. `Program.RALPH_VALIDATE_TEMPERATURE`, 5 tests in
  `ValidationTemperatureTests.cs`.
- **perf(telemetry):** Lazy-init OTLP exporters — the OpenTelemetry SDK
  pipeline (TracerProvider / MeterProvider + `AddOtlpExporter`) is now only
  constructed when `OTEL_EXPORTER_OTLP_ENDPOINT` is set. With no collector
  configured, `--otel` / `--metrics` / `--telemetry` previously paid
  ~2.7 ms / ~4.2 ms of cold-start tax building a pipeline that exported to
  nothing. Measured drops on the reference rig (`malachor`, AOT linux-x64,
  N=50 per variant): `--otel` −2.74 ms mean (−20.7 %), `--metrics` −4.58 ms
  (−31.1 %), `--otel --metrics` −5.28 ms (−34.5 %). The stderr FinOps
  cost-event channel is independent and continues to fire whenever
  `--metrics`/`--telemetry` is set. When an endpoint IS configured, eager
  construction is preserved so first-span export latency stays predictable.
  See `docs/perf/v2.0.5-baseline.md` §4.3 for the full before/after table.
  ([bania-v2-01])
- **docs(community):** Contributor onboarding refreshed (CONTRIBUTING.md,
  issue/PR templates). ([s02e17-the-newsletter])
### Deprecated
### Removed
### Fixed
### Security

## [2.0.6] — 2026-04-22

> **Fix-forward from cancelled v2.0.5.** The v2.0.5 release-workflow run
> failed at CI integration-test because `tests/integration_tests.sh`
> Gate 2 hardcoded `"2.0.2"` as the expected output of
> `az-ai-v2 --version --short` — the same stale-literal bug class
> (C-1 / C-2) that v2.0.5 existed to eliminate. All release legs
> (`linux-x64`, `linux-musl-x64`, `osx-arm64`, `win-x64`,
> `docker-publish-v2`) were skipped, so no v2.0.5 binaries, tarballs,
> or GHCR tags were published. The `v2.0.5` git tag remains on
> `origin` (per tag-immutability policy in
> `docs/release/ghcr-tag-lifecycle.md`) but is a no-op marker — treat
> it as cancelled, same status as v2.0.3. v2.0.6 carries everything
> v2.0.5 intended to ship, plus the integration-test fix.
>
> **No user impact.** There was nothing to downgrade from — v2.0.5
> never produced artifacts. Users on v2.0.4 go straight to v2.0.6.

### Fixed
- **Integration-test version assertion (fix-forward from v2.0.5).**
  `tests/integration_tests.sh` Gate 2 now reads the expected version
  string dynamically from `azureopenai-cli-v2/AzureOpenAI_CLI_V2.csproj`
  `<Version>` instead of carrying a hardcoded `"2.0.2"` literal. The
  test was itself an instance of the C-1 / C-2 drift pattern — a
  stale constant that nobody rolled forward. Now it self-adjusts on
  every bump and hard-fails if the shipped binary disagrees with the
  csproj, matching the contract already enforced by
  `VersionContractTests.cs`.

### Notes
- **v2.0.5 status:** cancelled (tag pushed, workflow failed at CI
  gate, zero artifacts published). The `v2.0.5` tag is preserved on
  `origin` for audit-trail continuity; it is not a released version.
  Documented precedent: v2.0.3 was cancelled on the same lifecycle
  pattern (see v2.0.4 banner).

## [2.0.5] — 2026-04-22 (cancelled)

> **Cancelled.** This tag was pushed on 2026-04-22 but the release
> workflow failed at CI integration-test before any build or publish
> step executed. No binaries, tarballs, Docker images, or GHCR tags
> were produced. All substantive content from this entry shipped in
> v2.0.6. Entry retained for audit continuity only.

> **Version-string fix + 50+ marathon findings closed.** The headline
> defect: v2.0.3 and v2.0.4 binaries shipped with `--version --short`
> reporting `2.0.2` because `Program.VersionSemver` and
> `Telemetry.ServiceVersion` were hardcoded string literals that nobody
> rolled forward. v2.0.5 single-sources the version from the csproj
> `<Version>` element (AOT-safe via `Assembly.GetName().Version`), adds
> a xUnit contract pin so it can never drift again, and teaches
> `packaging/tarball/stage.sh` to read the same source of truth.
> No behavior change beyond the version string itself. The rest of
> this release is a documentation marathon: release policy, SemVer
> contract, pre-release checklist, CHANGELOG style guide, runbooks,
> perf baseline, and 45+ audit/dogfood findings closed across the
> wave 1/2/3 agent sweeps.

### Changed
- **Default-model canonicalization (ADR-009).** The "default model" is now
  formally a resolution chain — CLI flag → `AZUREOPENAIMODEL` env →
  `UserConfig.default_model` / smart-default → hardcoded fallback
  (`Program.DefaultModelFallback = "gpt-4o-mini"`). The two duplicated
  `"gpt-4o-mini"` literals in `Program.cs` have been replaced by the
  named constant; fallback behavior is unchanged. `cost-optimization.md`
  §3.5 rewritten to describe the override contract. Operators who prefer
  `gpt-5.4-nano` continue to set it via env/config. See
  `docs/adr/ADR-009-default-model-resolution.md`.

### Fixed
- **Shipped-version-string drift (audit findings C-1 / C-2).**
  `Program.VersionSemver`, `Program.VersionFull`, and
  `Telemetry.ServiceVersion` were hardcoded to `"2.0.2"` and never
  rolled past v2.0.2 — the v2.0.3 and v2.0.4 binaries reported
  `az-ai-v2 --version --short` → `2.0.2`, which would have failed
  `brew test az-ai-v2` on install and broken the Scoop / Nix install
  smoke tests. Version is now single-sourced from the csproj
  `<Version>` element via `typeof(Program).Assembly.GetName().Version`
  (AOT-safe, no reflection-on-metadata path). `packaging/tarball/stage.sh`
  now parses the csproj for tarball filenames (with `STAGE_VERSION`
  env-var override for exceptional re-stages) instead of carrying a
  stale hardcoded `VERSION="2.0.2"`.

### Added
- `tests/AzureOpenAI_CLI.V2.Tests/VersionContractTests.cs` — xUnit
  contract pin that runs on every PR and hard-fails if (a)
  `Program.VersionSemver` regresses to `"2.0.2"` or any other
  hardcoded literal, (b) `Telemetry.ServiceVersion` drifts from
  `Program.VersionSemver`, or (c) either drifts from the csproj
  `<Version>`. Gate row 4 of `docs/release/pre-release-checklist.md`.
- Perf baseline for v2.0.5 on bare-metal Linux (`malachor`):
  [`docs/perf/v2.0.5-baseline.md`](docs/perf/v2.0.5-baseline.md).
  Supersedes the v2.0.0 WSL2 baseline and the v2.0.2 dogfood bench.
  No regression vs. v2.0.2 on cold-start, warm-start, or binary size.

### Docs
- **Release discipline.** New `docs/release/` tree:
  [`semver-policy.md`](docs/release/semver-policy.md) (how to pick the
  bump), [`pre-release-checklist.md`](docs/release/pre-release-checklist.md)
  (20-row gate table, sign-offs, no-go triggers),
  [`ghcr-tag-lifecycle.md`](docs/release/ghcr-tag-lifecycle.md)
  (container tag immutability policy),
  [`artifact-inventory.md`](docs/release/artifact-inventory.md)
  (the asset list every release must match), and
  [`docs/CHANGELOG-style-guide.md`](docs/CHANGELOG-style-guide.md)
  (house style for this file).
- **Runbooks.** `docs/runbooks/release-runbook.md`,
  `docs/runbooks/packaging-publish.md`,
  `docs/runbooks/macos-runner-triage.md`,
  `docs/runbooks/finops-runbook.md`, and
  `docs/runbooks/threat-model-v2.md`.
- **Wave 1/2/3 marathon.** 45+ findings closed across DevRel (speaker
  bureau, swag brief, livestream checklist, announce template cleanup),
  legal (LICENSE year refresh, third-party notices sync, demo attribution
  audit), i18n, accessibility, ethics, QA test-matrix hygiene, and
  docs polish. Full receipts in `docs/audits/` and per-agent sweep
  commit range `9a6d54e..0d26566`.

### Packaging
- Homebrew / Scoop / Nix hash-sync deferred to a T+2h follow-up PR
  (per [`pre-release-checklist.md`](docs/release/pre-release-checklist.md)
  row 20 and [`ghcr-tag-lifecycle.md`](docs/release/ghcr-tag-lifecycle.md)) —
  sha256 digests are computed from the published GitHub Release
  artifacts, not guessed pre-tag. Install via Homebrew / Scoop will
  resolve once that PR merges; until then, direct-download from the
  GitHub Release works.

### Notes
- This release ships from `main` directly; no release branch. CI
  green on the tagged commit was the gate.

## [2.0.4] — 2026-04-22

> **Drop macOS Intel (`osx-x64`) from the release matrix + ship FDR dogfood
> fixes.** The v2.0.3 release pipeline was blocked for another ~9h on
> GitHub Actions' `macos-13` runner pool — the same backlog that blocked
> v2.0.2. macOS Intel is a diminishing platform (Apple's last Intel Macs
> were 2020; Rosetta 2 gives Intel-Mac users a stable Apple Silicon
> binary path), and letting infra flakiness on one leg gate every release
> is not a tenable ship discipline. v2.0.4 cuts macOS Intel from the
> official artifact matrix so every release can publish against the
> reliable legs (`linux-x64`, `linux-musl-x64`, `osx-arm64`, `win-x64`).
>
> **Intel-Mac paths that still work out-of-the-box:**
> - **Rosetta 2** — install `az-ai-v2-<version>-osx-arm64.tar.gz` on
>   macOS 11+; Rosetta 2 handles the translation transparently.
> - **Docker** — `ghcr.io/schwartzkamel/azure-openai-cli/az-ai-v2:2.0.4`
>   runs under `linux/amd64` emulation on Docker Desktop for Intel Mac.
> - **Build from source** — `dotnet publish -r osx-x64 -c Release
>   --self-contained -p:PublishAot=true` still produces a working Intel
>   binary; the csproj supports the RID, we just stop *shipping* it.

### Fixed
- **FDR v2.0.2 dogfood High-severity findings** (report:
  [`docs/audits/fdr-v2-dogfood-2026-04-22.md`](docs/audits/fdr-v2-dogfood-2026-04-22.md),
  commit `4842b6a`). Three items, all shipped to v2.0.2/v2.0.3 and now
  resolved:
  1. **`fdr-v2-err-unwrap`** — global catch now handles
     `Azure.RequestFailedException` before generic `Exception`, unwraps
     up to 5 levels of `InnerException`, and redacts `AZUREOPENAIAPI` +
     endpoint hostname from every error surface. Users see actionable
     status + errorCode instead of "A type initializer threw an
     exception" noise.
  2. **`fdr-v2-raw-config-warning`** — `UserConfig.Load(bool quiet)`
     gained a `quiet` gate; `Program.cs` passes `quiet: opts.Raw` so the
     `--raw` contract (nothing on stderr, ever) holds even when
     `~/.azureopenai-cli.json` fails to parse.
  3. **`fdr-v2-ralph-exit-code`** — `RalphWorkflow` now returns exit 1
     when `--max-iterations` is exhausted without validation passing and
     when every iteration errored. SIGINT-130 preserved.
- 16 new tests (`ExceptionUnwrapTests`, `UserConfigQuietTests`,
  `RalphExitCodeTests`). Full suite: v1 1025/1025 + v2 485/485 = 1510
  green.

### Changed
- **Release matrix** (`.github/workflows/release.yml`): removed `osx-x64`
  from v1 and v2 `build-binaries` matrices + release-body artifact
  tables. No more `macos-13` jobs.
- **Packaging manifests:**
  - `packaging/homebrew/Formula/az-ai.rb` — dropped `on_intel` macOS
    block; notes Rosetta 2 / Docker / source-build fallbacks.
  - `packaging/nix/flake.nix` — dropped `x86_64-darwin` from
    `sourcesFor`; `latestHashes` no longer tracks `osx-x64`. Frozen
    `pinnedHashes` for 2.0.0/2.0.1/2.0.2/2.0.3 retain `osx-x64` keys as
    unreferenced historical markers; they are now ignored by
    `sourcesFor` and can be dropped in a future cleanup.
- Versioned formulas/manifests for older releases (`@2.0.0`, `@2.0.1`,
  `@2.0.2`) keep their `osx-x64` URLs as historical records — those
  releases either never published artifacts at all or never published
  `osx-x64` specifically.

### Notes
- **v2.0.3** was tagged and got as far as publishing the Docker image
  (`ghcr.io/schwartzkamel/azure-openai-cli/az-ai-v2:2.0.3`) before the
  same `macos-13` runner pool wedged the binary matrix. The v2.0.3 run
  was cancelled at cutover; no GitHub Release exists for v2.0.3.
  v2.0.4 supersedes it.

### Packaging (post-publish, hash-sync)
- Homebrew, Nix, and Scoop manifests hash-synced against the v2.0.4
  GitHub Release (run `24789065975`, published 2026-04-22). Digests:
  - `linux-x64.tar.gz` — `sha256:9592a962…8e6` (SRI
    `sha256-lZKpYgsN3jdF2wtXFwja0i1qYABobnwPB2E6lq6nmOY=`)
  - `linux-musl-x64.tar.gz` — `sha256:48b0a81a…ceb` (Nix-only; Homebrew
    does not model musl)
  - `osx-arm64.tar.gz` — `sha256:6c3051a4…874` (SRI
    `sha256-bDBRpKV0wJ9R95WbYZ4YeszjeykY2t2HmnnmfOfrmHQ=`)
  - `win-x64.zip` — `sha256:2d3f8c67…943`
- New frozen pin siblings: `packaging/homebrew/Formula/az-ai-v2@2.0.4.rb`,
  `packaging/scoop/versions/az-ai-v2@2.0.4.json`, and a `"2.0.4"` entry
  in `packaging/nix/flake.nix` `pinnedHashes`. Unversioned manifests
  (`az-ai.rb`, `az-ai.json`, flake `latestHashes`) rolled to 2.0.4.
- **Known issue — tarball-filename drift (audit finding C-1).** The
  v2.0.4 tarballs were uploaded with `2.0.2` embedded in their
  filenames because `packaging/tarball/stage.sh` `VERSION` and the
  `Program.cs` / `Observability/Telemetry.cs` version constants were
  not rolled past `2.0.2` in the v2.0.3/v2.0.4 commits. Manifest URLs
  therefore hardcode the literal `az-ai-v2-2.0.2-<rid>.tar.gz`
  filenames at the `v2.0.4` tag, and the shipped binary still reports
  `--version --short` → `2.0.2`. `brew test az-ai-v2` will fail
  against v2.0.4 until v2.0.5 rolls the version strings in lock-step.
  Full diagnosis in `docs/audits/docs-audit-2026-04-22-lippman.md`
  (Critical findings C-1 / C-2).
- Tap / bucket publish is a separate workflow and is NOT done in this
  commit (owner: Bob Sacamano).

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

---

## Cancelled-release policy

A **cancelled release** is a git tag in the `vX.Y.Z` sequence for which the
release pipeline started but did **not** complete its full publish contract.
The tag is immutable — it is never deleted, retagged, or rewritten — but the
artifacts behind it are partial or absent. This policy defines how the
project handles those cases so users can tell at a glance whether a tag
corresponds to a shipped release.

### Taxonomy

| State | Git tag | GitHub Release | Tarballs | GHCR image | Homebrew / Scoop / Nix | User action |
|---|---|---|---|---|---|---|
| **Shipped** | ✅ exists | ✅ published | ✅ uploaded | ✅ pushed | ✅ hash-synced | Use it. |
| **Cancelled — Docker-only** | ✅ exists | ❌ none | ❌ none | ✅ pushed | ❌ not synced | Docker users may pull the image; everyone else skips to the next shipped version. |
| **Cancelled — nothing published** | ✅ exists | ❌ none | ❌ none | ❌ none | ❌ not synced | Skip the tag entirely; use the next shipped version. |
| **Attempted** | ✅ exists | ❌ none | ⚠️ partial | ⚠️ partial | ❌ not synced | Skip the tag; treat as historical marker only. |

### Rules

1. **Tags are immutable.** Once pushed, a `vX.Y.Z` tag is never deleted or
   moved, even if the release pipeline fails. This preserves the git
   history for post-mortems and keeps third-party references (SBOMs,
   lockfiles, blog posts) valid.
2. **Version numbers are not reused.** The next release takes the next
   SemVer number (`X.Y.(Z+1)`), regardless of how much of the cancelled
   release actually published. v2.0.3 cancelled → v2.0.4 is next.
3. **Partial artifacts are not retroactively completed.** If Docker
   published but binaries did not, the project does **not** go back and
   upload tarballs under the cancelled tag. The supersede-forward path
   ([`v2.0.4`](#204--2026-04-22) in the v2.0.3 case) carries the fix.
4. **Packaging manifests skip cancelled tags.** `az-ai.rb` /
   `az-ai.json` / flake `latestHashes` roll directly to the next
   shipped version. Frozen per-version pins
   (`az-ai-v2@2.0.3.rb`, etc.) are **not** created for cancelled
   releases.
5. **Cancelled entries stay in the changelog.** They are labelled
   `— *Cancelled release*` in the heading, with a callout paragraph
   explaining what shipped, what did not, and which tag supersedes.

### Known cancelled releases

| Tag | State | Notes |
|---|---|---|
| [`v2.0.1`](#201--2026-04-21) | Attempted | Tagged on `039e6bd`; `docker-publish-v2` failed on AOT asset-graph mismatch. Superseded by v2.0.2. |
| [`v2.0.3`](#203--2026-04-22--cancelled-release) | Cancelled — Docker-only | GHCR `2.0.3` is live; no GitHub Release. Superseded by v2.0.4. |
