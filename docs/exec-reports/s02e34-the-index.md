# S02E34 -- *The Index*

> *Elaine drew the map in E25; three orphaned docs never made it onto it. Lloyd walks the hallway with a clipboard.*

**Commit:** `<pending -- set after push>`
**Branch:** `main` (direct push)
**Runtime:** ~20 min
**Director:** Larry David (showrunner)
**Cast:** 1 sub-agent (Lloyd Braun, junior-dev / onboarding lens) with Elaine as a read-only advisor on docs shape. Single wave, no parallel dispatch within the episode.

## The pitch

Elaine's `docs/README.md` landed in S02E25 *The Story Editor* as the single
map into everything under `docs/`. Good map, but three mechanical findings
came out of that pass and stayed open: 17 top-level `docs/*.md` files had
no inbound link from the map (so they existed but could not be found by
browsing), the `docs/launch/` directory -- 18 files, not the 10 the brief
estimated -- had no `index.md` / `README.md` of its own (so a release
manager inheriting a launch would have to `ls` the directory and guess),
and the top-level `README.md`'s Documentation section is a flat list that
asks a newcomer to pick the right doc out of 30-plus filenames.

This episode closes the first two. They are newcomer-shaped problems and
call for a newcomer-shaped reviewer. The third finding is out of scope
for a sub-agent: `README.md` is orchestrator-owned per
[`shared-file-protocol`](../../.github/skills/shared-file-protocol.md).
Logged for Larry to handle in a micro-orchestrator commit; not a failure,
just a different lane.

The guiding question for every edit: *if a person joined this project
today and clicked `docs/README.md`, could they reach this doc?* If no,
wire it up. If yes, leave it alone.

## Scene-by-scene

### Act I -- Planning

Walked `docs/*.md` (one level deep) and `docs/<subdir>/{index,README}.md`
and cross-referenced each against the link set in `docs/README.md`. The
orphan inventory:

**Top-level orphans (17):** `CHANGELOG-style-guide.md`,
`accessibility-review-v2.md`, `chaos-drill-v2.md`,
`dogfooding-baseline-2026-04.md`, `i18n-audit.md`,
`incident-runbooks.md`, `nim-setup.md`, `observability.md`,
`perf-baseline-v2.md`, `release-notes-v2.0.0.md`,
`security-review-v2.md`, `telemetry.md`, `user-stories.md`,
`v2-cutover-checklist.md`, `v2-cutover-decision.md`,
`v2-dogfood-plan.md`, `why-az-ai.md`.

**Subdirectory index notes:** `announce/README.md`, `talks/README.md`,
and `devrel/README.md` are *directory-linked* from the map
(`[announce/](announce/)`, etc.) rather than README-linked. GitHub
renders the README when you click the directory, so these are navigable
today -- not counted as orphans. Noted in case a future pass wants to
tighten the linking convention.

**`docs/launch/` (18 files):** no index at all. Top priority.

Decisions locked in Act I, one-line rationale each:

1. **Additive edits only to `docs/README.md`** -- brief constraint, plus not breaking existing anchor links is a Mickey-Abbott-style a11y consideration for anyone who has bookmarked one.
2. **Three new H2 sections added:** "Recent additions", "Observability and telemetry", "Quality audits and reviews". Each is cohesive enough that a 4th junior pass would not have to re-classify.
3. **`why-az-ai.md` goes in Getting started, not in a new Product section** -- product pitch is the first thing a newcomer should see, not a separate silo.
4. **`Recent additions` scoped to E09 + E33 + E32 only.** E26 (file-read blocklist) is still filming; referencing an unlanded change from this commit would make the exec report lie if E26 slips. Orchestrator can add the bullet in the same batch commit that lands E26.
5. **`nim-setup.md` into Operating the CLI, not Specialist trees** -- it is a how-to, not a spike or audit, and a contributor running `:aifix` locally is exactly the audience of that section.
6. **v2-cutover-* docs grouped as a sub-paragraph of Release/ops** rather than a new section, because they are frozen-as-shipped and not a living topic.

### Act II -- Fleet dispatch

| Wave | Agents (parallel) | Outcome |
|------|-------------------|---------|
| **1** | lloyd-braun (lead) + elaine (read-only advisor) | Orphan audit, `docs/README.md` edits, new `docs/launch/README.md`, exec report. |

Single wave by design -- the episode is a focused docs pass and parallel
dispatch would only have created a collision surface with the E26
security episode filming concurrently.

### Act III -- Ship

- ASCII-punctuation grep on both touched files: clean (no smart
  quotes, en/em dashes).
- `test -f` roll-call over every path referenced in the new bullets
  and the new launch index: 25/25 resolve OK (see Verification).
- Link count in `docs/README.md` grew from 61 to 82 markdown links
  (`grep -c '\[.*\](.*\.md)'`).
- No preflight needed -- no `.cs`, `.csproj`, `.sln`, workflow, or
  `Dockerfile` touches. CI will gate markdownlint on push.
- Commit staged with explicit paths (no `git add -A`), signed off
  with the Copilot trailer and `-c commit.gpgsign=false` per the
  [`commit`](../../.github/skills/commit.md) skill.

## What shipped

**Production code** -- n/a (docs-only episode).

**Tests** -- n/a.

**Docs**

- `docs/README.md` -- edited. Added:
  - Two follow-on bullets in Getting started (`why-az-ai.md`, `user-stories.md`).
  - New **Recent additions** H2 citing `--show-cost` (S02E09), `make migrate-check` / `make migrate-clean` (S02E33), and structural shell-blocklist hardening (S02E32).
  - `nim-setup.md` in Operating the CLI.
  - `CHANGELOG-style-guide.md` in Process and governance.
  - `security-review-v2.md` in Security.
  - New **Observability and telemetry** H2 (`observability.md`, `telemetry.md`, `incident-runbooks.md`).
  - `perf-baseline-v2.md` in Performance and benchmarks.
  - `launch/README.md` link + a v2-cutover sub-paragraph (`v2-cutover-decision.md`, `v2-cutover-checklist.md`, `v2-dogfood-plan.md`, `release-notes-v2.0.0.md`) in Release, ops, and migration.
  - New **Quality audits and reviews** H2 (`accessibility-review-v2.md`, `chaos-drill-v2.md`, `i18n-audit.md`, `dogfooding-baseline-2026-04.md`).
- `docs/launch/README.md` -- new. 23-line index with one-line "when you want this" descriptions for all 18 files in the directory, in roughly the order a release manager would touch them. Cross-links out to `docs/runbooks/release-runbook.md` for the canonical, evergreen release procedure.

**Not shipped (intentional follow-ups for the orchestrator)**

- **`e25-readme-documentation-section-flat`** -- the top-level `README.md` Documentation section is still a flat list. `README.md` is orchestrator-owned per `shared-file-protocol`; Lloyd does not edit it. Suggested shape for Larry's next micro-orchestrator commit: group the list into the same four buckets already established in `docs/README.md` (Getting started / Operating the CLI / Process and governance / Specialist and audits). One commit, one file, `docs(readme): categorize documentation section`.
- **E26 recent-additions bullet.** The expanded file-read blocklist (S02E26 *The Locked Drawer*) is filming in parallel. Left out of the Recent additions section to avoid referencing an unlanded feature. Suggested follow-up for Larry's orchestrator batch after E26 lands: add a fourth bullet under Recent additions naming the expanded `ReadFileTool.cs` blocklist and linking `SECURITY.md`.
- **Subdirectory link convention.** `announce/`, `talks/`, `devrel/` are directory-linked (GitHub renders their READMEs on click) rather than README-linked. Works today; a future consistency pass could standardize on `[foo/README.md](foo/README.md)` everywhere. Out of scope here -- would break existing anchor conventions across the map.

## Verification

```text
$ grep -c '\[.*\](.*\.md)' docs/README.md   # before
61
$ grep -c '\[.*\](.*\.md)' docs/README.md   # after
82
```

All 25 paths referenced in the new bullets + launch index resolve:

```text
OK docs/why-az-ai.md
OK docs/user-stories.md
OK docs/nim-setup.md
OK docs/CHANGELOG-style-guide.md
OK docs/security-review-v2.md
OK docs/observability.md
OK docs/telemetry.md
OK docs/incident-runbooks.md
OK docs/perf-baseline-v2.md
OK docs/v2-cutover-decision.md
OK docs/v2-cutover-checklist.md
OK docs/v2-dogfood-plan.md
OK docs/release-notes-v2.0.0.md
OK docs/launch/README.md
OK docs/launch/v2.0.6-release-notes.md
OK docs/accessibility-review-v2.md
OK docs/chaos-drill-v2.md
OK docs/i18n-audit.md
OK docs/dogfooding-baseline-2026-04.md
OK azureopenai-cli/Observability/CostEstimator.cs
OK Makefile
OK docs/migration-v1-to-v2.md
OK SECURITY.md
OK CHANGELOG.md
OK docs/runbooks/release-runbook.md
```

`docs/launch/README.md` exists and opens with the expected sentence:

```text
$ ls docs/launch/README.md && head -5 docs/launch/README.md
docs/launch/README.md
# `docs/launch/` -- the launch locker

This directory holds everything you reach for when you are cutting a release
or landing one: playbooks, announcement copy, social drafts, conference
```

ASCII-punctuation grep on both touched files: clean.

```text
$ grep -nP '[\x{2018}\x{2019}\x{201C}\x{201D}\x{2013}\x{2014}]' docs/README.md docs/launch/README.md
# (no output)
```

Markdownlint not run locally (environment OOMs per prior sessions); CI
will gate it.

## Lessons from this episode

1. **The brief's "10 files" was 18.** Lloyd counted the directory before
   writing the index -- good. Reminder: a brief's numeric estimates are
   a prompt to count, not a contract. If the sub-agent had trusted the
   "10" and stopped after ten bullets, eight files would have stayed
   orphaned *inside their own index*. Classic junior-trap; avoided by
   running `ls docs/launch/` first.
2. **"Orphan" is link-shape-dependent.** `announce/`, `talks/`, and
   `devrel/` looked like orphans to a file-name grep (`README.md` does
   not appear in the docs map) but are reachable via their directory
   links. The junior lens almost filed them as findings; pausing to
   click through saved three spurious edits. Worth codifying as a note
   in a future orphan-audit skill.
3. **Parallel-dispatch discipline held.** E26 (Newman + Kramer) was
   filming on `ReadFileTool.cs` + `tests/.../Adversary/` + `SECURITY.md`
   while this episode ran on a disjoint docs surface. Zero working-tree
   collision. The `shared-file-protocol` denylist (no `README.md`, no
   `AGENTS.md`, no `CHANGELOG.md`, no `SECURITY.md`) was the reason --
   every single one of those files would have collided if touched.
4. **"Additive only" is a real a11y primitive.** Not restructuring the
   existing H2s in `docs/README.md` means every pre-existing anchor link
   in exec reports, PR descriptions, and bookmarks still resolves. Three
   new H2s were inserted between existing H2s; no existing H2 was
   renamed, renumbered, or split. This should be the default for any
   sub-agent editing `docs/README.md`.
5. **The flat-list finding is the right one to punt.** It is the
   highest-visibility of the three (top-level `README.md`), which is
   exactly why a junior does not unilaterally re-categorize it. Tone and
   shape of the project front door is Larry + Peterman's call.

## Metrics

- Diff size: 3 files changed (`docs/README.md` edited, `docs/launch/README.md` and `docs/exec-reports/s02e34-the-index.md` new). Approximate insertions `~170`, deletions `~3` (pending final `git diff --stat` at push time).
- Link count in `docs/README.md`: **61 -> 82** (+21 new markdown links).
- Top-level orphan count in `docs/*.md`: **17 -> 0**.
- Subdirectories without an index: **1 -> 0** (`docs/launch/`).
- Test delta: n/a (docs-only).
- Preflight: skipped-with-reason (no `.cs` / `.csproj` / `.sln` / workflow / `Dockerfile` touches).
- CI state at push time: pending (watch `gh run list --branch main --limit 1`).

## Credits

- **Lloyd Braun** (junior-dev / onboarding lens) -- lead. Orphan audit, both doc edits, exec report. Primary author.
- **Elaine** (docs architect) -- read-only advisor. Consulted on section placement for the three new H2s so they fit the shape of her E25 map.
- **Larry David** (showrunner) -- cast the episode, held the line on
  "do not touch `README.md`" so the flat-list finding stays under
  orchestrator control.
- Concurrent episode: **S02E26 *The Locked Drawer*** (Newman + Kramer, security code) filmed in parallel on a disjoint surface. No collisions.

Co-authored-by trailer: `Co-authored-by: Copilot
<223556219+Copilot@users.noreply.github.com>` confirmed on the commit.
