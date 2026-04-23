# `docs/` -- the documentation map

> *If you landed here from `README.md`, you are in the right place. This
> file is the single map for everything under `docs/`. Every other index
> in this tree is more specialized; start here, then descend.*

This map is curated by **Elaine** (technical writing) on the principle that
a new contributor should reach the right doc in two clicks. Each section
lists the canonical entry point for a topic and the next 1-4 files worth
opening. If a doc is missing here, it is either intentionally specialist
(launch playbooks, raw benchmark JSON, audit transcripts) or it is a
finding -- log it via [`findings-backlog`](../.github/skills/findings-backlog.md).

For the user-facing project front door, see the top-level
[`README.md`](../README.md). For the cast and dispatch model, see
[`AGENTS.md`](../AGENTS.md). For the Copilot CLI's view of the same,
see [`.github/copilot-instructions.md`](../.github/copilot-instructions.md).

## Getting started (new contributor, in order)

1. [`../README.md`](../README.md) -- what `az-ai` is, install, first run.
2. [`../CONTRIBUTING.md`](../CONTRIBUTING.md) -- dev workflow, preflight, PR expectations.
3. [`../AGENTS.md`](../AGENTS.md) -- the cast roster and how fleet dispatch works.
4. [`onboarding.md`](onboarding.md) -- contributor onboarding deep-dive.
5. [`prerequisites.md`](prerequisites.md) -- env vars (single source of truth).
6. [`glossary.md`](glossary.md) -- Ralph mode, Squad, persona, Wiggum loop, etc.

Also worth a read early on, even before you write code:

- [`why-az-ai.md`](why-az-ai.md) -- one-page pitch for why this CLI exists and who it beats on what axis. Good context before you open `ARCHITECTURE.md`.
- [`user-stories.md`](user-stories.md) -- one paragraph per shipped feature in plain English. If you are looking at a flag and wondering "who asked for this?", this is the file.

## Recent additions (latest user-visible features)

If you are returning to the project after a break, these are the user-visible
landings you most likely missed. Each bullet names the feature, the episode
that shipped it, and where to read more.

- **`--show-cost` opt-in cost summary** (S02E09 *The Receipt*). Prints a per-invocation breakdown of tokens and dollars after each run. See the `[Unreleased]` / v2 entries in [`../CHANGELOG.md`](../CHANGELOG.md) and the source under [`../azureopenai-cli/Observability/`](../azureopenai-cli/Observability/) (`CostEstimator.cs`, `CostEvent.cs`, `CostHook.cs`).
- **`make migrate-check` and `make migrate-clean`** (S02E33 *The Uninstaller*). Contributor-facing targets that help you move a workstation from v1 to v2 and tidy up the v1 leftovers. Full procedure in [`migration-v1-to-v2.md`](migration-v1-to-v2.md); the targets are defined in [`../Makefile`](../Makefile).
- **Structural shell-blocklist hardening** (S02E32 *The Bypass*). The run-shell tool now rejects a wider family of command-chaining and redirection bypass attempts before they reach a subprocess. The rationale and threat model live in [`../SECURITY.md`](../SECURITY.md).
- **Expanded file-read sensitive-path blocklist** (S02E26 *The Locked Drawer*). `ReadFileTool` now hard-blocks seven home-directory credential paths (`~/.ssh`, `~/.kube`, `~/.gnupg`, `~/.netrc`, `~/.docker/config.json`, `~/.git-credentials` + XDG variant, `~/.npmrc`/`~/.pypirc`) with a structural validate pipeline matching E32's shell-side shape. See [`../SECURITY.md`](../SECURITY.md).

## Architecture and decisions

- [`../ARCHITECTURE.md`](../ARCHITECTURE.md) -- system design, tool registry, Squad internals.
- [`adr/`](adr/) -- Architecture Decision Records. Start at [`adr/README.md`](adr/README.md) for the index and conventions.
- [`archive/ARCHITECTURE-v1.md`](archive/ARCHITECTURE-v1.md) -- the v1 architecture, retained for context only.
- [`aot-trim-investigation.md`](aot-trim-investigation.md) -- Native AOT and trimming notes feeding ADR-001.

## Operating the CLI (user-facing)

- [`use-cases.md`](use-cases.md) -- index of end-to-end recipes; per-mode guides:
  [`use-cases-standard.md`](use-cases-standard.md),
  [`use-cases-agent.md`](use-cases-agent.md),
  [`use-cases-ralph-squad.md`](use-cases-ralph-squad.md),
  [`use-cases-config-integration.md`](use-cases-config-integration.md).
- [`config-reference.md`](config-reference.md) -- every flag and env var.
- [`persona-guide.md`](persona-guide.md) -- `--persona`, `.squad.json`, persona memory.
- [`espanso-ahk-integration.md`](espanso-ahk-integration.md) -- text-expander setup.
- [`cost-optimization.md`](cost-optimization.md) -- model selection and budgeting.
- [`accessibility.md`](accessibility.md) -- `NO_COLOR`, `--raw`, keyboard workflows. Deeper notes in [`accessibility/`](accessibility/).
- [`i18n.md`](i18n.md) -- invariant globalization contract; deeper notes in [`i18n/`](i18n/).
- [`nim-setup.md`](nim-setup.md) -- getting `az-ai` talking to a local NVIDIA NIM (Gemma-4-2B-NVFP4) on WSL2 + Blackwell in under 10 minutes. Read this if you want to run the `:aifix` flow without calling Azure.

## Process and governance

The four-doc stack lives in [`process/`](process/) (start at [`process/README.md`](process/README.md)):

- [`process/change-management.md`](process/change-management.md) -- change classes and the stage-gate sequence.
- [`process/adr-stewardship.md`](process/adr-stewardship.md) -- when a decision earns an ADR.
- [`process/cab-lite.md`](process/cab-lite.md) -- lightweight cross-cast review.
- [`process/retrospective-cadence.md`](process/retrospective-cadence.md) -- season-finale and post-incident retros.
- [`CHANGELOG-style-guide.md`](CHANGELOG-style-guide.md) -- Mr. Lippman's rulebook for writing `CHANGELOG.md` entries. Read before you append a bullet to the release log.

Skills (the verbs) live in [`../.github/skills/`](../.github/skills/) and agents (the cast)
live in [`../.github/agents/`](../.github/agents/). Process governs both.

## Season exec reports

The episode log -- one file per shipped fleet-mode session.

- [`exec-reports/README.md`](exec-reports/README.md) -- the TV guide. Indexed by season and episode.
- [`exec-reports/_template.md`](exec-reports/_template.md) -- starter scaffold for new reports (spec lives in [`../.github/skills/exec-report-format.md`](../.github/skills/exec-report-format.md)).
- [`exec-reports/s02-writers-room.md`](exec-reports/s02-writers-room.md) -- current-season arc plan and findings backlog.
- Future-season blueprints: [`s03-blueprint.md`](exec-reports/s03-blueprint.md), [`s04-blueprint.md`](exec-reports/s04-blueprint.md), [`s05-blueprint.md`](exec-reports/s05-blueprint.md), [`s06-blueprint.md`](exec-reports/s06-blueprint.md), and the long-horizon [`seasons-roadmap.md`](exec-reports/seasons-roadmap.md).

## Proposals (FR-NNN)

- [`proposals/README.md`](proposals/README.md) -- the priority matrix and ship status (single source of truth for FR state).
- [`proposals/SECURITY-AUDIT-001.md`](proposals/SECURITY-AUDIT-001.md) -- standing security-audit proposal.

Individual `FR-NNN-*.md` files are linked from the priority matrix; do not
edit FR status outside the matrix.

## Security

- [`../SECURITY.md`](../SECURITY.md) -- threat model, reporting policy.
- [`security/index.md`](security/index.md) -- security-doc landing page.
- [`security/hardening-checklist.md`](security/hardening-checklist.md) -- pre-release hardening gate.
- [`security/cve-log.md`](security/cve-log.md), [`security/sbom.md`](security/sbom.md), [`security/scanners.md`](security/scanners.md), [`security/supply-chain.md`](security/supply-chain.md) -- supply-chain posture.
- [`runbooks/threat-model-v2.md`](runbooks/threat-model-v2.md) -- v2 threat model.
- [`security-review-v2.md`](security-review-v2.md) -- Newman's cutover security review of the v2 tree, surface by surface, with evidence by line number.
- [`verifying-releases.md`](verifying-releases.md) -- cosign / attestation verification.

## Observability and telemetry

Opt-in, off by default. Read these before you enable telemetry locally or
before you argue about what signals the CLI should emit.

- [`observability.md`](observability.md) -- Phase 5 observability posture: OTel spans, cost events, what is on/off by default.
- [`telemetry.md`](telemetry.md) -- the short policy version: zero default egress, opt-in flags, what a user can and cannot turn on.
- [`incident-runbooks.md`](incident-runbooks.md) -- Frank Costanza's triage guide for end users hitting a failure mid-pipeline. The three runbooks (key, quota, network) cover most "the CLI is broken" tickets.

## Performance and benchmarks

- [`perf/index.md`](perf/index.md) -- performance landing page.
- [`perf/bench-workflow.md`](perf/bench-workflow.md) -- which `make bench*` to run when.
- [`perf/v2.0.5-baseline.md`](perf/v2.0.5-baseline.md) -- current cold-start / size baseline.
- [`perf/reference-hardware.md`](perf/reference-hardware.md) -- the reference rig.
- [`perf-baseline-v2.md`](perf-baseline-v2.md) -- Kenny Bania's v1-vs-v2 perf baseline and the AOT-size gate verdict for the v2.0.0 cutover.
- [`benchmarks/`](benchmarks/) -- dated benchmark transcripts. Raw artifacts under [`benchmarks/raw/`](benchmarks/raw/).

## Release, ops, and migration

- [`release/pre-release-checklist.md`](release/pre-release-checklist.md), [`release/semver-policy.md`](release/semver-policy.md), [`release/artifact-inventory.md`](release/artifact-inventory.md), [`release/ghcr-tag-lifecycle.md`](release/ghcr-tag-lifecycle.md).
- [`runbooks/release-runbook.md`](runbooks/release-runbook.md) and the rest of [`runbooks/`](runbooks/) -- finops, packaging-publish, macOS-runner triage.
- [`ops/slos-v2.md`](ops/slos-v2.md), [`ops/v2-sre-runbook.md`](ops/v2-sre-runbook.md), [`ops/telemetry-schema-v2.0.0.md`](ops/telemetry-schema-v2.0.0.md), [`ops/ghcr-tag-policy.md`](ops/ghcr-tag-policy.md).
- [`migration-v1-to-v2.md`](migration-v1-to-v2.md) -- user-facing v1 -> v2 upgrade.
- [`v2-migration.md`](v2-migration.md) -- internal MAF-adoption phase plan (different audience).
- [`launch/README.md`](launch/README.md) -- per-release launch playbooks, announcements, social drafts, CFPs, and post-tag diagnostics. Start here when you inherit a launch. The raw directory is at [`launch/`](launch/).
- [`announce/`](announce/) -- announcement archive.

The v2.0.0 cutover generated its own stack of decision and checklist docs.
They are frozen as shipped; read them to understand how the v2 ship decision
was made, not to re-open it.

- [`v2-cutover-decision.md`](v2-cutover-decision.md) -- Costanza's go/no-go ruling for v2.0.0.
- [`v2-cutover-checklist.md`](v2-cutover-checklist.md) -- the Phase 6 cutover gate, one checkbox per precondition.
- [`v2-dogfood-plan.md`](v2-dogfood-plan.md) -- the Phase 7 dogfood + chaos plan that fed into the decision.
- [`release-notes-v2.0.0.md`](release-notes-v2.0.0.md) -- v2.0.0 release notes (tag immutable; published line is v2.0.6 -- see [`launch/v2.0.6-release-notes.md`](launch/v2.0.6-release-notes.md) for what actually shipped).

## Specialist trees (browse, do not read top-to-bottom)

- [`prompts/`](prompts/) -- prompt library, eval harness, persona prompts. Start at [`prompts/README.md`](prompts/README.md).
- [`testing/`](testing/) -- BDD guide, contract tests, coverage, flaky triage. Start at [`testing/README.md`](testing/README.md).
- [`distribution/`](distribution/) -- Homebrew, Scoop, Nix, Docker hardening. Start at [`distribution/README.md`](distribution/README.md).
- [`demos/`](demos/) -- hero-GIF and demo scripts. Start at [`demos/README.md`](demos/README.md).
- [`devrel/`](devrel/), [`talks/`](talks/), [`speaker-bureau.md`](speaker-bureau.md) -- developer relations and conference material.
- [`ethics/`](ethics/) -- responsible-use and disclosure stance.
- [`legal/`](legal/), [`licensing-audit.md`](licensing-audit.md) -- license posture and trademark policy.
- [`audits/`](audits/) -- dated audit transcripts (immutable; one file per auditor per audit).
- [`spikes/`](spikes/), [`diary/`](diary/), [`opportunity-analysis.md`](opportunity-analysis.md), [`competitive-analysis.md`](competitive-analysis.md), [`competitive-landscape.md`](competitive-landscape.md) -- exploratory and comparative work.

## Quality audits and reviews

Dated, signed, read-only review reports. When something breaks a regression,
these are the receipts.

- [`accessibility-review-v2.md`](accessibility-review-v2.md) -- Mickey Abbott's a11y + CLI-ergonomics review of the v2 cutover build.
- [`chaos-drill-v2.md`](chaos-drill-v2.md) -- FDR's red-team chaos drill against the AOT v2 binary (10 attack categories + a live persona-memory exercise).
- [`i18n-audit.md`](i18n-audit.md) -- Babu Bhatt's i18n-readiness audit of every user-facing string in the v1 CLI binary.
- [`dogfooding-baseline-2026-04.md`](dogfooding-baseline-2026-04.md) -- snapshot of where we actually use `az-ai` today vs where we still reach for other tools. Feeds the S06 Dogfooding arc.

## Conventions

- All docs are plain markdown. There is no static-site generator; this map *is* the index.
- ASCII punctuation only outside the upstream exclusion list -- see [`../.github/skills/ascii-validation.md`](../.github/skills/ascii-validation.md).
- New top-level `docs/*.md` files must earn a bullet here when they land. Orphaned docs are findings.
- Episode exec reports are immutable post-air; do not retro-edit.
- Orchestrator-owned files (this map does not edit them) are listed in [`../.github/skills/shared-file-protocol.md`](../.github/skills/shared-file-protocol.md).

## Provenance

Stood up in **S02E25 *The Story Editor*** by Elaine, with friction-pass
notes from Lloyd Braun (junior-dev lens) and an a11y pass from Mickey
Abbott. See [`exec-reports/s02e25-the-story-editor.md`](exec-reports/s02e25-the-story-editor.md).
