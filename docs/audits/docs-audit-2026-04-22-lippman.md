# Docs Audit — Release Management (Lippman)

**Date:** 2026-04-22
**Auditor:** Mr. Lippman (release management)
**Scope:** `CHANGELOG.md`, `docs/launch/`, `docs/runbooks/release-runbook.md`,
`packaging/*` manifests, `.github/ISSUE_TEMPLATE/`, release-adjacent
procedure docs.
**Context:** v2.0.4 release run `24789065975` SUCCEEDED and is live
(GitHub Release + GHCR `2.0.4`) as of 2026-04-22 16:16 UTC. This audit
runs concurrently with the hash-sync side quest in the same release
window.

---

## TL;DR

v2.0.4 is **shipped and functionally usable**, but three serious release-
discipline failures got through the cutover: the tarballs carry the
wrong version in their filenames, the binary reports the wrong version
at `--version`, and the canonical release runbook is ~2 major versions
stale. The fact that v2.0.2 and v2.0.4 both slipped through with the
same stage-script drift (`VERSION=2.0.2` never rolled) means this is
a **process gap, not a typo** — no pre-flight gate catches it.

**Release-process maturity score: 6.5 / 10.** Up from ~4 at v2.0.0
(three failed attempts in a row), but well short of the v1.8.x discipline
we had. Tag hygiene is intact (no force-pushes, no retagging), the
CHANGELOG is real prose, and attempted-release diagnostics are preserved
for posterity. The gaps are all in *pre-flight enforcement* and *post-
release closure*.

---

## Findings

### Severity legend

| Severity      | Meaning                                                         |
|---------------|-----------------------------------------------------------------|
| Critical      | Breaks a shipped artifact or a user-visible contract. Block ship. |
| High          | Silent wrong behavior, stale canonical doc, missing gate.       |
| Medium        | Procedural drift, confusing-but-recoverable.                    |
| Low           | Stylistic, housekeeping, non-load-bearing staleness.            |
| Informational | Worth knowing; not required to act on.                          |

---

### CRITICAL

#### C-1. v2.0.4 tarballs embed the wrong version in their filenames

- **Files:** `packaging/tarball/stage.sh:30` (`VERSION="2.0.2"`)
- **Observed:** the v2.0.4 GitHub Release assets are named
  `az-ai-v2-2.0.2-linux-x64.tar.gz`, `az-ai-v2-2.0.2-osx-arm64.tar.gz`,
  `az-ai-v2-2.0.2-linux-musl-x64.tar.gz`, `az-ai-v2-2.0.2-win-x64.zip`.
  The *tag* is `v2.0.4` but the *filename* is `2.0.2`.
- **Impact:** every downstream consumer that derived a download URL from
  the tag (Homebrew `az-ai.rb` URL template `az-ai-v2-#{version}-*`,
  Nix `sourcesFor`, Scoop `autoupdate`) would 404. Today's hash-sync
  had to hardcode the drifted filenames at the v2.0.4 tag and thread a
  `tarballVersionFor` override through the flake. **Fragile** —
  future consumers will trip over this.
- **Root cause:** the v2.0.2 CHANGELOG entry explicitly lists
  `packaging/tarball/stage.sh` VERSION as bumped `2.0.1→2.0.2`. The
  v2.0.3 commit (`ec92bcc`) and v2.0.4 commit (`afa95fd`) rolled
  `AzureOpenAI_CLI_V2.csproj` `<Version>` only; they did not touch
  `stage.sh`. No pre-flight gate caught it.
- **Fix:** (a) v2.0.5 hotfix that rolls `stage.sh:30`, `Program.cs:1550-1551`,
  `Observability/Telemetry.cs:31`, and `tests/integration_tests.sh`
  Gate 2 in lock-step; (b) add a shell step to the release workflow
  that asserts `grep -q "VERSION=\"${GITHUB_REF_NAME#v}\"" packaging/tarball/stage.sh`
  and fails the run before `build-binaries` if it drifts; (c) add a
  Release Runbook §1 pre-flight row for "all version strings match tag".
- **Severity:** Critical.

#### C-2. v2.0.4 binary reports `--version --short` → `2.0.2`

- **Files:** `azureopenai-cli-v2/Program.cs:1550-1551`
  (`VersionSemver = "2.0.2"`, `VersionFull = "az-ai-v2 2.0.2 …"`),
  `azureopenai-cli-v2/Observability/Telemetry.cs:31`
  (`ServiceVersion = "2.0.2"`).
- **Observed:** The v2.0.4-tagged binary, when run, tells the user it
  is `2.0.2`. Telemetry emits `service.version=2.0.2`.
- **Impact:** (a) user-visible contract break — users on v2.0.4 who
  file bugs with `az-ai-v2 --version` will misreport as 2.0.2, sending
  maintainers to the wrong fix-window; (b) `brew test az-ai-v2` with
  `assert_equal "2.0.4"` (per Homebrew convention) will FAIL; (c)
  observability dashboards group the v2.0.4 fleet under the 2.0.2
  label, making the FDR-fix rollout invisible.
- **Fix:** same v2.0.5 hotfix as C-1. Until then, the shipped Homebrew
  test block is known-broken; audit committee has signed off on this
  explicitly in the @2.0.4 formula header.
- **Severity:** Critical.

#### C-3. `docs/runbooks/release-runbook.md` is v1-era and contradicts reality

- **File:** `docs/runbooks/release-runbook.md` (entire file, 1-159)
- **Observed:**
  - §0 inputs reference `azureopenai-cli/AzureOpenAI_CLI.csproj` (v1
    project), not `azureopenai-cli-v2/AzureOpenAI_CLI_V2.csproj`.
  - §1 pre-flight claims baseline "541 passing" tests; current
    baseline per CHANGELOG [2.0.4] is 1510 (v1 1025 + v2 485).
  - §3 describes a **5-way** binary matrix including `osx-x64`; v2.0.4
    explicitly drops `osx-x64` and is 4-way.
  - §3 names the Docker job `docker-publish`; the v2 pipeline is
    `docker-publish-v2`.
  - §4 verifies against `azure-openai-cli-linux-x64.tar.gz`; the v2
    artifact name pattern is `az-ai-v2-<version>-<rid>.tar.gz`.
  - §5 rollback references `ghcr.io/schwartzkamel/azure-openai-cli`
    not `…/az-ai-v2`.
  - No mention of hash-sync, tap/bucket handoff, or the
    `gh run rerun --failed` recovery lever that actually saved v2.0.2.
- **Impact:** anyone following the runbook verbatim will misread the
  actual pipeline and miss the v2-specific gates. New maintainers
  (the runbook's target audience) will be misled.
- **Fix:** rewrite end-to-end for v2, fold in the v2-specific playbook
  content from `docs/launch/release-v2-playbook.md`, and demote the
  playbook to a historical reference. Target: v2.0.5 window.
- **Severity:** Critical (canonical doc actively misleading).

---

### HIGH

#### H-1. Pre-flight checklist does not guard against version-string drift

- **Files:** `docs/runbooks/release-runbook.md:31-45`,
  `docs/launch/release-v2-playbook.md:12-37`.
- **Observed:** both checklists verify `csproj <Version>` matches the
  tag, but neither requires `stage.sh:VERSION`, `Program.cs`
  `VersionSemver`/`VersionFull`, `Telemetry.cs:ServiceVersion`, and
  `tests/integration_tests.sh` Gate 2 to match. This is exactly the
  drift that produced C-1 and C-2.
- **Fix:** add a single pre-flight row:
  ```
  [ ] Run `packaging/scripts/verify-version-strings.sh $TAG` — exits
      non-zero if any of {csproj, stage.sh, Program.cs, Telemetry.cs,
      integration_tests.sh} disagree with $TAG.
  ```
  And create the verification script (one-shot grep, ~15 lines).
- **Severity:** High.

#### H-2. No post-release hash-sync SLA or owner documented

- **Files:** `docs/launch/release-v2-playbook.md:196-236` (hash-sync
  section), `CHANGELOG.md` entries for [2.0.2].
- **Observed:** the v2 playbook §4 describes the hash-sync mechanics
  but does not specify (a) a time SLA ("hash-sync MUST land within
  N hours of publish"), (b) an owner (Lippman? Bob? release PM?),
  (c) what blocks: does an un-hash-synced release block the announce-
  ment or the tap/bucket publish? v2.0.2 was published 2026-04-22
  but never hash-synced in-repo (see flake `pinnedHashes."2.0.2"`,
  still `fakeHash`). This audit is the first time v2.0.x has been
  hash-synced, and it's happening on v2.0.4, skipping 2.0.2.
- **Fix:** add an SLA to the playbook ("T+2h hash-sync PR, T+4h
  merged, T+6h tap/bucket opened"); document owner as "Lippman cuts
  the PR, Bob merges after tap verification". Back-fill `pinnedHashes."2.0.2"`
  or document explicitly that 2.0.2 will never be back-filled.
- **Severity:** High.

#### H-3. No rollback runbook for a published-then-broken release

- **Files:** `docs/runbooks/release-runbook.md:114-134` (existing
  stub is v1-era container-only), `docs/launch/release-v2-playbook.md:257-270`.
- **Observed:** the v2 playbook §6 Rollback says "do NOT delete the
  tag; cut a new versioned pin." Correct direction, but no actionable
  steps for: revoking a GHCR tag, yanking from the Homebrew tap,
  un-listing from the Scoop bucket, publishing an advisory, coordinating
  with Newman on security impact. No procedure for a "partial publish"
  (e.g. GHCR live but GitHub Release not — which is exactly what
  happened to v2.0.2 pre-rerun).
- **Fix:** new `docs/runbooks/release-rollback.md` with three scenarios:
  (a) binary regression post-publish, (b) partial-publish recovery,
  (c) security advisory retraction. Each scenario: preconditions →
  steps → comms template → sign-offs.
- **Severity:** High.

#### H-4. No tag-protection policy documented

- **Files:** (absent — no file in repo)
- **Observed:** the playbook and runbook both say "never rewrite a
  pushed tag" as prose, but there is no documented GitHub branch/tag
  protection rule, no `CODEOWNERS` entry gating the `v*` tag
  namespace, and no written policy for who can cut tags. v1.8.x, all
  v2.0.x attempts, and v2.0.4 have been clean — but that is organiza-
  tional discipline, not enforced policy.
- **Fix:** (a) author `docs/policies/tag-protection.md` covering the
  "never rewrite, never force-push, annotated+signed only, versioned-
  pin for retags" rules already informally followed; (b) configure
  the GitHub tag protection rule and record the configuration in
  `docs/policies/`; (c) reference the policy from the release runbook.
- **Severity:** High.

---

### MEDIUM

#### M-1. "Cancelled release" policy not codified

- **Files:** `docs/launch/v2-release-attempt-1-diagnostic.md`,
  `docs/launch/v2.0.1-release-attempt-diagnostic.md`,
  `docs/launch/v2.0.2-release-attempt-diagnostic.md`,
  `docs/launch/v2.0.2-publish-handoff.md`, plus the cancelled v2.0.3
  (which has **no dedicated diagnostic**).
- **Observed:** the first three attempted-release markers get their
  own diagnostic doc. v2.0.3, the most recent cancellation, gets only
  a short "Notes" paragraph in `CHANGELOG [2.0.4]`. There is no written
  rule: *when do we author a diagnostic? when does a short CHANGELOG
  note suffice?*
- **Fix:** add a `docs/launch/README.md` (or extend
  `docs/runbooks/release-runbook.md`) with: "Every cancelled or failed
  tagged release MUST produce a diagnostic note with (a) run ID,
  (b) root cause, (c) which tag supersedes. File in `docs/launch/`
  as `<tag>-release-attempt-diagnostic.md` if >1 paragraph; inline in
  CHANGELOG otherwise." Back-fill a 1-paragraph v2.0.3 entry to satisfy
  the new rule.
- **Severity:** Medium.

#### M-2. Handoff doc for v2.0.2 still reads as "in progress"

- **Files:** `docs/launch/v2.0.2-publish-handoff.md:1-65`.
- **Observed:** opens with `Status as of 2026-04-21 20:25 UTC`, present-
  tense "release run … is blocked". v2.0.2 was eventually published
  and then superseded by v2.0.4. The doc is now a **diagnostic**, not
  an active handoff, but a naive reader would think the release is
  still in limbo.
- **Fix:** prepend a short "RESOLVED 2026-04-22 — v2.0.2 published via
  `gh run rerun --failed` recovery; superseded by v2.0.4. Retained
  below as the post-mortem for future infra-backlog recoveries." Or
  rename to `v2.0.2-publish-postmortem.md` and drop the "handoff"
  framing entirely.
- **Severity:** Medium.

#### M-3. `[Unreleased]` section is empty but has no skeleton

- **Files:** `CHANGELOG.md:8-9`.
- **Observed:** `## [Unreleased]` is followed immediately by
  `## [2.0.4]`. Keep-a-Changelog recommends either (a) a non-empty
  `[Unreleased]` with in-flight items or (b) a clean skeleton with
  `### Added / Changed / Fixed / Removed / Security` placeholder
  subheaders so contributors know where to drop entries. Runbook §7
  says "open a fresh `[Unreleased]` skeleton"; it was not opened.
- **Fix:** add the placeholder skeleton immediately after cutting
  each release, as part of the release commit itself. Not a blocker.
- **Severity:** Medium.

#### M-4. Pre-release checklist does not mention SBOM or image-digest capture

- **Files:** `docs/launch/release-v2-playbook.md:12-37` (§0 Preconditions),
  `docs/runbooks/release-runbook.md:86-110` (§4 Post-release verification).
- **Observed:** v2.0.2 CHANGELOG entry cites SBOM generation + Sigstore
  attestation (`Rekor logIndex 1352893652`) but neither checklist lists
  "SBOM generated + attached" or "image digest recorded in release body"
  as a hard gate. The release body is auto-generated, which is why this
  worked in practice — but the moment the auto-gen breaks, we will ship
  without noticing.
- **Fix:** add explicit rows to the post-release checklist: `[ ] SBOM
  JSON present for each RID leg in the release assets`, `[ ] GHCR image
  digest recorded in release body`, `[ ] attestation verify passes for
  at least one binary + the OCI image`.
- **Severity:** Medium.

#### M-5. No `.github/ISSUE_TEMPLATE/release*.yml` template

- **Files:** `.github/ISSUE_TEMPLATE/` (existing: `bug_report.yml`,
  `config.yml`, `feature_request.yml`, `question.yml`,
  `v2_bug_report.yml`; missing: release checklist).
- **Observed:** each release currently threads through commit messages
  and `docs/launch/*.md`. A GitHub Issue with a release-checklist
  template would let Lippman, Puddy (QA), Newman (security), Jackie
  (licensing), and Elaine (docs) each tick their sign-off in one
  place.
- **Fix:** author `.github/ISSUE_TEMPLATE/release_checklist.yml` with
  pre-flight, sign-off, and post-release sections. Nice-to-have, not
  blocking.
- **Severity:** Medium.

---

### LOW

#### L-1. Release runbook SemVer rules don't cover the persisted-config case we've already hit

- **File:** `docs/runbooks/release-runbook.md:21-23`.
- **Observed:** says "major for breaking changes to CLI flags, config
  schema, or the persisted `~/.azureopenai-cli.json` contract." Good
  as far as it goes, but doesn't address: telemetry schema changes,
  OCI image-tag scheme changes, persona/squad YAML schema, tool-surface
  breaking changes. v2.0.0 cut a new binary namespace specifically to
  avoid exactly these questions; the rule should be captured.
- **Fix:** fold into the runbook rewrite (part of C-3).
- **Severity:** Low.

#### L-2. v2.0.0 release-body doc still in active/authoring voice

- **Files:** `docs/launch/v2.0.0-release-body.md`,
  `docs/launch/v2.0.0-announcement.md`, `v2.0.0-blog-draft.md`, etc.
- **Observed:** these read as "to be published" drafts even though
  v2.0.0 was **never published** (the tag exists but no GitHub Release).
  A reader landing here thinks they missed a launch.
- **Fix:** add a 3-line banner at the top of each v2.0.0 launch doc:
  "HISTORICAL DRAFT — v2.0.0 was tagged but never published; see
  `v2-release-attempt-1-diagnostic.md`. v2.0.2 is the first published
  v2.x release. Retained for the record."
- **Severity:** Low.

#### L-3. `docs/audits/` has no index or naming convention

- **Files:** `docs/audits/fdr-v2-dogfood-2026-04-22.md`,
  `docs/audits/security-v1.8-post-release.md`, (this file).
- **Observed:** three audits, three different naming schemes
  (`<persona>-<scope>-<date>.md`, `<team>-<version>-<phase>.md`,
  `<kind>-<date>-<persona>.md`). No `README.md` in `docs/audits/`.
- **Fix:** land a `docs/audits/README.md` with the canonical pattern
  (`<kind>-<YYYY-MM-DD>-<persona>.md`) and a one-line index. Low
  priority — directory is small.
- **Severity:** Low.

#### L-4. `packaging/scoop/az-ai.json` `autoupdate.hash.url` still points at the SBOM

- **File:** `packaging/scoop/az-ai.json` (`autoupdate.hash.url = "$url.sbom.json"`).
- **Observed:** the intent is "Scoop should auto-update by fetching the
  hash from a sidecar file". Pointing at the SBOM is incorrect —
  Scoop's `hash.url` expects a `.sha256` or similar digest file, not a
  CycloneDX JSON document. This will silently fail the first time
  checkver runs unattended.
- **Fix:** either publish a `.sha256` sidecar per RID in the release
  workflow and point `autoupdate.hash.url` at that, or remove the
  `autoupdate.hash` block and require a manual hash-sync PR per
  release (matches current Lippman-driven practice anyway).
- **Severity:** Low.

---

### INFORMATIONAL

#### I-1. Release Maturity Scorecard

| Dimension                                         | Score | Notes                                                       |
|---------------------------------------------------|:-----:|-------------------------------------------------------------|
| Tag hygiene (no force-push, annotated, signed)    |  9/10 | Strong; no violations through 4 attempts + 4 cuts.          |
| Pre-flight enforcement                            |  4/10 | Allowed version-string drift through twice. H-1.             |
| CHANGELOG discipline (Keep-a-Changelog, prose)    |  9/10 | Best-in-class prose; [Unreleased] skeleton missing (M-3).    |
| Runbook accuracy                                  |  3/10 | v1-era; actively misleading. C-3.                            |
| Post-release hash-sync cadence                    |  5/10 | First v2 hash-sync is happening in this audit. H-2.          |
| Rollback readiness                                |  4/10 | Stub exists; no real procedure. H-3.                         |
| Cancelled-release documentation                   |  7/10 | Three full diagnostics; v2.0.3 missing one. M-1.             |
| Tag-protection policy                             |  5/10 | Followed in practice, undocumented. H-4.                     |
| Sign-off coordination (QA/Sec/Licensing/Docs)     |  6/10 | Happens in PR review; not checkpointed. M-5.                 |
| Artifact naming consistency                       |  2/10 | C-1: filename drift two releases running.                    |
| **Weighted mean**                                 | **6.5 / 10** | Competent, not yet disciplined.                      |

#### I-2. Prior-audit cross-reference

- `fdr-v2-dogfood-2026-04-22.md` — the three High findings it flagged
  are now shipped in v2.0.4 `### Fixed`. Clean.
- `security-v1.8-post-release.md` — no release-management findings in
  scope of this audit.

---

## Recommended action plan (ordered by blast radius)

1. **v2.0.5 hotfix** to close C-1 + C-2: roll `stage.sh`, `Program.cs`,
   `Telemetry.cs`, `integration_tests.sh` version strings to the new
   tag. Add the pre-flight guard (H-1) in the same PR so this can't
   recur. Bob-ready hash-sync for 2.0.5 lands in the same window.
2. **Runbook rewrite** (C-3) — target the v2.0.5 window so the next
   cut is against a correct runbook.
3. **Rollback runbook** (H-3) + **tag-protection policy** (H-4) — one
   PR each, independent of release cadence.
4. **Hash-sync SLA + cancelled-release policy** (H-2, M-1) — light-
   weight doc updates; pair them.
5. **Handoff-doc archival**, **CHANGELOG `[Unreleased]` skeleton**,
   **SBOM/digest checklist rows**, **release-checklist issue template**
   (M-2 through M-5) — housekeeping, batch when convenient.
6. **Low items** (L-1 through L-4) — fold into the runbook rewrite or
   the next packaging PR.

---

## Go / no-go for v2.0.4

**GO — but with a caveat.** v2.0.4 is published, the binaries are
downloadable, the GHCR image is signed, and the formulas are now hash-
synced. The shipped artifact contract is intact at the *byte* level.
The *metadata* contract is broken (C-1, C-2) — users will see a 2.0.2
version string from a v2.0.4 tag. That's a user-visible bug, not a
shipped-artifact corruption, and v2.0.5 can close it inside a day.

Lippman's press call: **we went to press. v2.0.5 is already in the
queue.** No retag of v2.0.4, no force-push, no mutation of the GitHub
Release.  We fix forward.

— Mr. Lippman, release management
