# S02E10 -- *The Press Kit*

> Lippman and Costanza curate thirty-plus episodes into one tagged, release-note-ready version.

**Commit:** `<filled-at-push>`
**Branch:** `main` (direct push)
**Runtime:** ~35 minutes
**Director:** Larry David (showrunner)
**Cast:** 5 voices (Lippman lead, Costanza co-lead, Peterman / Elaine / Jerry guests), 0 dispatch waves -- off-roster curation special

## The pitch

Thirty-plus episodes of user-visible work accumulated in
`CHANGELOG.md [Unreleased]`, but only seventeen were cited. Ten aired
episodes -- E22, E23, E25, E26, E27, E28, E29, E31, E33, E34 -- had
never landed a changelog line. The csproj still read `2.0.6` with no
matching release heading. The press kit was overdue.

This episode curates the backlog into a single dated release, cuts the
SemVer decision, drafts release notes in two distinct voices (Lippman's
release mechanics, Costanza's customer story), and leaves the tag
itself for showrunner discretion. Scope is disciplined: no code
changes, no workflow edits, one version bump, one new release-notes
doc, ten new CHANGELOG entries, one exec report.

E36 *The Attribution* (Jackie Chiles + Bob Sacamano, licensing audit)
filmed in parallel on `THIRD_PARTY_NOTICES.md` / `NOTICE` / `LICENSE`.
Zero file overlap; no rebase needed.

## Scene-by-scene

### Act I -- Planning and the SemVer call

Read the full `[Unreleased]` surface end-to-end, the ten missing
exec reports, and the v2.0.x heading history. Three things stood out:

1. The existing `[Unreleased]` section had already absorbed the
   v2.0.6 fix-forward content (integration-test version-assertion fix,
   cancelled-v2.0.5 banner) without ever being cut to a `## [2.0.6]`
   heading. The csproj was at `2.0.6` but the CHANGELOG had no
   matching heading. The whole block was ours to rename.
2. None of the ten missing episodes introduced a breaking change. All
   additive -- new Makefile targets, new tool blocklist paths, new
   adversarial tests, new docs, new skills.
3. The accumulated work already in `[Unreleased]` (`--show-cost`,
   cast personas, OTLP lazy-init, shell_exec tokenization hardening,
   Docker hardening, first-run wizard, persona memory) is similarly
   additive with one minor behavioral delta (Ralph `--validate`
   temperature default), all documented, none breaking.

**Decision: minor bump, 2.0.6 → 2.1.0.** Per SemVer 2.0.0 §7: *"Minor
version Y (x.Y.z | x > 0) MUST be incremented if new, backwards
compatible functionality is introduced."* No §8 (MAJOR) triggers
apply -- no public-API removals, no behavioral changes that break v2.0.x
callers. Patch bump (§6) would not be honest: the surface is too
large and the new flags / tool contracts are user-visible additions,
not bug-fixes.

### Act II -- Curation, version bump, release notes

No fleet dispatch. Single-hand curation by design -- this episode
exists specifically so one hand owns the CHANGELOG instead of 35
sub-agents elbowing the file.

| Pass | Surface | Outcome |
|------|---------|---------|
| **1** | `azureopenai-cli/AzureOpenAI_CLI.csproj` | `<Version>2.0.6</Version>` → `<Version>2.1.0</Version>`. Single line. |
| **2** | `CHANGELOG.md` `[Unreleased]` → `[2.1.0]` cutover | Fresh empty `[Unreleased]` block at top with all six standard sub-headings; renamed the old `[Unreleased]` heading to `## [2.1.0] — 2026-04-23`. |
| **3** | `CHANGELOG.md` new entries | Ten new entries inserted at the top of the right sub-sections (2× Added, 7× Changed, 1× Security). All ten cited SHAs confirmed to resolve. |
| **4** | `docs/release-notes-v2.1.0.md` | New longer-form narrative. Peterman opener, Lippman mechanics section, distinct Costanza-voice customer story, thin migration section, 27-line cast acknowledgments. |
| **5** | Jerry's CI check | `.github/workflows/release.yml` 4-leg matrix intact (linux-x64, linux-musl-x64, osx-arm64, win-x64). Dockerfile has no hard-coded `<Version>` label -- only OCI `source` / `title` / `description` -- so no bump required. No workflow edits. |

### Act III -- Preflight, commit, push

`make preflight` -- green:

```
═══════════════════════════════════════════
 All 34 tests passed! (2 skipped)
═══════════════════════════════════════════
[preflight] all gates green — safe to commit
```

Single commit, explicit paths, Copilot trailer, no tag.

## What shipped

### Production code

- `azureopenai-cli/AzureOpenAI_CLI.csproj` -- `<Version>` bumped
  `2.0.6` → `2.1.0`. One-line diff.

No `.cs` / test / workflow changes. Dockerfile untouched (no version
label out of sync).

### Tests

n/a -- no code changes. Preflight re-run confirms existing suites stay
green under the bump.

### Docs

- `CHANGELOG.md` -- ten new entries added; `[Unreleased]` block
  cut to `## [2.1.0] — 2026-04-23`; fresh empty `[Unreleased]` left
  at the top.
- `docs/release-notes-v2.1.0.md` -- new, ~11 KB narrative release
  notes modelled on `docs/release-notes-v2.0.0.md`. Structure: cold
  open → Headline → Why you'll care → Theme-grouped what's-new →
  **Costanza's customer-story cut** → Breaking changes (none) →
  Migration / upgrade → Known limitations → Upgrading / rolling back
  → Acknowledgments.
- `docs/exec-reports/s02e10-the-press-kit.md` -- this file.

### Not shipped (intentional follow-ups)

- **No git tag.** Human-gated per the brief -- showrunner discretion.
  Commands pre-filled below.
- **Homebrew / Scoop / Nix manifests unchanged.** Per packaging
  policy, formula / manifest hash-syncs happen post-publish, not
  pre-tag. They are called out as DRAFT in the release notes.
- **README version badge.** Orchestrator-owned; Larry updates at
  sign-off.
- **No CI workflow edits.** Jerry's sanity check found nothing stale
  worth a drive-by fix; anything else gets its own episode, not
  smuggled into the release cut.

## The ten new CHANGELOG entries

| Episode | Title | SHA | Section | One-line |
|---------|-------|-----|---------|----------|
| E22 | The Process | `480e877` | Changed | Change-management contract: ADR stewardship, CAB-lite, retrospective cadence. |
| E23 | The Adversary | `3f845fc` | Changed | Chaos drill against read_file / shell_exec / web_fetch tool surface. |
| E25 | The Story Editor | `7d57a01` | Added | `docs/README.md` entry-point map + 8 cross-link footers. |
| E26 | The Locked Drawer | `04be3ee` | **Security** | ReadFileTool blocklist extends to 7 home-dir credential families + 53 adversary facts. |
| E27 | The Bible | `1a1e12e` | Changed | Writers' bible: `episode-brief` / `fleet-dispatch` / `shared-file-protocol` skills. |
| E28 | The Style Guide | `f3046e1` | Changed | Three hygiene skills: `ascii-validation` / `docs-only-commit` / `changelog-append`. |
| E29 | The Casting Call | `4a4b894` | Changed | Cohesion skills: `writers-room-cast-balance` + `findings-backlog`. |
| E31 | The Audition | `dae6145` | Changed | Adversarial coverage of 5 generic personas; 9 findings, 1 routing-scorer bug fixed. |
| E33 | The Uninstaller | `350e67a` | Added | `make migrate-check` / `make migrate-clean` for v1 `az-ai` leftovers. |
| E34 | The Index | `54b9c19` | Changed | Orphan-doc cleanup + new `docs/launch/README.md`. |

## Jerry's CI check (clean bill of health)

- **`.github/workflows/release.yml`** -- 4-leg matrix (`linux-x64`,
  `linux-musl-x64`, `osx-arm64`, `win-x64`) intact since v2.0.4. No
  stale refs. No edits.
- **`Dockerfile`** -- OCI labels are `source` / `title` /
  `description` / `documentation` / `licenses`. No hard-coded
  `version=` label. No edit required under the one-line allowance.
- **Markdownlint** -- skipped locally (per session history, `npx
  markdownlint-cli2` OOMs at 8 GB on this box). CI gate will catch
  drift.
- **Preflight** -- `make preflight` green end-to-end (format, build,
  unit tests, integration tests; 34 passed / 2 skipped where
  Azure creds are absent).

No follow-up findings for Jerry's queue.

## Verification (paste-ready)

```text
$ grep -c '^- ' CHANGELOG.md         # whole file
# (pre)                               ... (post)
```

Entries inside the new `[2.1.0]` section:

```bash
$ awk '/^## \[2\.1\.0\]/{f=1} /^## \[2\.0\.5\]/{f=0} f' CHANGELOG.md | grep -c '^- '
46
```

New version string:

```bash
$ grep -E '<Version>' azureopenai-cli/AzureOpenAI_CLI.csproj
    <Version>2.1.0</Version>
```

Cited SHAs all resolve:

```bash
$ for s in 480e877 3f845fc 7d57a01 04be3ee 1a1e12e f3046e1 4a4b894 dae6145 350e67a 54b9c19; do
    git log --oneline | grep -q "^$s " && echo "$s ok" || echo "$s MISSING"
  done
# 10× ok, 0× MISSING
```

Preflight: **green** (34 passed, 2 skipped on absent Azure creds).

## Tag-prep commands (NOT executed -- showrunner discretion)

```bash
# After this commit lands on origin/main and E36 signs off:
git tag -s v2.1.0 -m "Azure OpenAI CLI v2.1.0

Minor release. Thirty-plus episodes of accumulated v2 work: --show-cost
receipts, 12-persona cast, make migrate-check / migrate-clean, expanded
ReadFileTool credential blocklist, hardened shell_exec tokenization,
Alpine Docker hardening, NO_COLOR / FORCE_COLOR gates, OTLP lazy-init.

No breaking changes for v2.0.x users. See docs/release-notes-v2.1.0.md
and CHANGELOG.md#210--2026-04-23 for the full surface.
"

git push origin v2.1.0
```

The tag is signed; the message body is pre-filled. Larry runs the
commands after Jackie / Bob sign off on E36 and after the release PR
(if one is opened) is merged.

## Lessons from this episode

1. **Never let `[Unreleased]` accumulate ten uncited episodes.** The
   moment an aired episode ships user-visible work, the CHANGELOG
   append happens in the same PR, not "at release time." E10 exists
   as the designated exception; it should not be needed for ten
   entries at a time.
2. **Cut the heading when the version bumps.** The csproj went to
   `2.0.6` but the CHANGELOG never got a `## [2.0.6]` heading --
   the block stayed as `[Unreleased]`. This is the exact C-1 / C-2
   drift pattern that cancelled v2.0.5. The `changelog-append.md`
   skill (new in this release) now makes the cutover explicit.
3. **Separate the mechanics voice from the customer voice.** The
   release notes have two distinct sections -- Lippman's grouped
   theme rundown and Costanza's customer story -- and they read
   differently on purpose. A user who wants the contract reads
   Lippman. A user who wants to know "why should I upgrade" reads
   Costanza. Don't blend them.
4. **Tag-prep is not tagging.** Pre-fill the command with a message
   body so the showrunner can paste-and-sign, but leave the actual
   `git tag` + `git push` for human-gated discretion. Orchestrator
   ownership preserved.

## Metrics

- **Diff size (pre-push):** 3 files changed, ~260 insertions, ~1 deletion.
  - `CHANGELOG.md`: +70 / -1
  - `azureopenai-cli/AzureOpenAI_CLI.csproj`: +1 / -1 (version bump)
  - `docs/release-notes-v2.1.0.md`: +~225 (new)
  - `docs/exec-reports/s02e10-the-press-kit.md`: +~220 (new, this file)
- **Test delta:** n/a (no code changes).
- **Preflight:** passed -- 34 / 2-skipped.
- **CI status at push time:** linked on push. Expected green (docs +
  version-bump surface only; `VersionContractTests.cs` re-reads the
  csproj at runtime and self-adjusts).

## Credits

- **Mr. Lippman** (lead) -- SemVer call, CHANGELOG curation, release
  notes mechanics, tag-prep commands. "We're going to press."
- **George Costanza** (co-lead) -- customer-story section of the
  release notes. Distinct voice by contract; Costanza earns his
  S02 floor corrective by writing the "who is this release for"
  cut in his own cadence. Sandwich-sized receipts. Archaeological
  digs. Idempotent.
- **J. Peterman** (guest) -- opening two sentences of the release
  notes. Launch tone, never oversold.
- **Elaine Benes** (guest) -- docs sanity pass on the release notes
  and CHANGELOG entry prose. No commit-dump bullets; every entry reads
  like something a user can parse.
- **Jerry** (guest) -- CI / release plumbing sanity. Release workflow
  matrix, Dockerfile labels, markdownlint. Clean bill of health; no
  drive-by edits.
- **Larry David** (showrunner) -- cast assignment, sign-off after the
  push. Runs the tag when E36 signs off.

`Co-authored-by: Copilot` trailer confirmed on the single commit that
closes this episode.
