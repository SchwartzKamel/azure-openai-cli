# Exec Reports — *Seinfeld Coding Cast*

> *"What's the deal with ... shipping software?"*

A post-facto log of every fleet-mode coding session on `azure-openai-cli`.
Each episode captures what the cast built, who showed up, what went
sideways, and what we learned -- structured like a proper TV show
because the [cast itself is](../../AGENTS.md).

## Seasons

| Season  | Theme                                    | Status                          | Blueprint |
|---------|------------------------------------------|---------------------------------|-----------|
| **S01** | The Pilot Years                          | Aired (back-fill pending)        | --        |
| **S02** | Production & Polish (v2 era)             | In production (24-arc + specials) | --        |
| **S03** | Local & Multi-Provider                   | Pre-production                  | [s03-blueprint.md](s03-blueprint.md) |
| **S04** | Model Intelligence                       | Pre-production                  | [s04-blueprint.md](s04-blueprint.md) |
| **S05** | Protocols & Plugins (incl. MCP)          | Pre-production                  | [s05-blueprint.md](s05-blueprint.md) |
| **S06** | Dogfooding (`az-ai` is the main tool)    | Pre-production (showrunner override) | [s06-blueprint.md](s06-blueprint.md) |
| **S07+** | Roadmap pad (Enterprise, Multimodal, ...) | Slate                            | [seasons-roadmap.md](seasons-roadmap.md) |

S06 baseline (the "before" picture before dogfooding starts):
[`dogfooding-baseline-2026-04.md`](../dogfooding-baseline-2026-04.md).

New seasons get declared when a major version ships, a theme shifts, or
the cast would tell you it's time for a retool.

## Episode index

### Season 2 -- Production & Polish

| #        | Title              | Episode file                                            | Commit    | What happened                                                |
|----------|--------------------|---------------------------------------------------------|-----------|--------------------------------------------------------------|
| S02E01   | *The Wizard*       | [s02e01-the-wizard.md](s02e01-the-wizard.md)            | `f57032f` | Interactive first-run wizard + per-OS LOLBin storage          |
| S02E02   | *The Cleanup*      | [s02e02-the-cleanup.md](s02e02-the-cleanup.md)          | `f65adbd` | docs-lint green + whitespace-key guard across all stores      |
| S02E03   | *The Warn-Only Lie* | [s02e03-the-warn-only-lie.md](s02e03-the-warn-only-lie.md) | `b36ec19` | docs-lint Summary step honesty -- label matches behaviour |
| S02E04   | *The Locksmith*    | [s02e04-the-locksmith.md](s02e04-the-locksmith.md)      | `ef8756c` | Opportunistic libsecret credential store on Linux             |
| S02E05   | *The Marathon*     | [s02e05-the-marathon.md](s02e05-the-marathon.md)        | `42b14bb` | `make bench-quick` + CI bench-canary (directional only)       |
| S02E06   | *The Screen Reader* | [s02e06-the-screen-reader.md](s02e06-the-screen-reader.md) | `6b22a7a` | NO_COLOR / FORCE_COLOR gates + wizard a11y hardening      |
| S02E07   | *The Observability* | [s02e07-the-observability.md](s02e07-the-observability.md) | `87d190d` | Telemetry posture doc + incident runbooks (no code yet)   |
| S02E08   | *The Translation*  | [s02e08-the-translation.md](s02e08-the-translation.md)  | `0c3a6ae` | i18n readiness audit + project glossary                       |
| S02E11   | *The Spec*         | [s02e11-the-spec.md](s02e11-the-spec.md)                | `8854588` | User-stories doc translating S02 features for non-engineers   |
| S02E12   | *The Apprentice*   | [s02e12-the-apprentice.md](s02e12-the-apprentice.md)    | `b6be974` | Lloyd Braun debut: friction log, glossary appends, starter PRs |
| S02E13   | *The Inspector*    | [s02e13-the-inspector.md](s02e13-the-inspector.md)      | `9bee782` | v2 surface security audit (5 PASS / 1 follow-up named)        |
| S02E14   | *The Container*    | [s02e14-the-container.md](s02e14-the-container.md)      | `09fa6d2` | Dockerfile hardening: numeric UID/GID, layer trim, CIS 4.1    |
| S02E15   | *The Lawyer*       | [s02e15-the-lawyer.md](s02e15-the-lawyer.md)            | `8d3e17f` | License audit -- 100% MIT, no GPL contagion, NOTICE bundling  |
| S02E16   | *The Catalog*      | [s02e16-the-catalog.md](s02e16-the-catalog.md)          | `ba96f6d` | Homebrew + Scoop + Nix packaging drafts (Bob's S02 debut)     |
| S02E17   | *The Newsletter*   | [s02e17-the-newsletter.md](s02e17-the-newsletter.md)    | `63c37db` | CONTRIBUTING refresh + named contributor wall                 |
| S02E18   | *The Maestro*      | [s02e18-the-maestro.md](s02e18-the-maestro.md)          | `5b502bd` | Prompt library inventory + ralph-mode temperature finding     |
| S02E19   | *The Competition*  | [s02e19-the-competition.md](s02e19-the-competition.md)  | `fc58fb4` | Competitive landscape brief + draft positioning copy          |
| S02E20   | *The Conference*   | [s02e20-the-conference.md](s02e20-the-conference.md)    | `3c5a319` | LOLBin credentials talk: 20 slides, 27 min, demo script       |
| S02E21   | *The Conscience*   | [s02e21-the-conscience.md](s02e21-the-conscience.md)    | `c047ea5` | Responsible-use ethics matrix (8 rows, 5 ENFORCED)            |
| S02E22   | *The Process*      | [s02e22-the-process.md](s02e22-the-process.md)          | `480e877` | Change-mgmt + ADR + CAB-lite + retrospective cadence docs     |
| S02E23   | *The Adversary*    | [s02e23-the-adversary.md](s02e23-the-adversary.md)      | `3f845fc` | Chaos drill: 21 findings, 9 CVE-shaped (FDR S02 debut)        |
| S02E27   | *The Bible*        | [s02e27-the-bible.md](s02e27-the-bible.md)              | `f3046e1` | 4 process skills (episode-brief / exec-report-format / ...)   |
| S02E28   | *The Style Guide*  | [s02e28-the-style-guide.md](s02e28-the-style-guide.md)  | `f3046e1` | 3 hygiene skills (ascii-validation / docs-only-commit / ...)  |
| S02E29   | *The Casting Call* | [s02e29-the-casting-call.md](s02e29-the-casting-call.md) | `4a4b894` | 2 cohesion skills (writers-room-cast-balance / findings-backlog) |
| S02E30   | *The Cast*         | [s02e30-the-cast.md](s02e30-the-cast.md)                | `93dfac7` | 12 cast personas baked as runtime defaults -- the show lives on |
| S02E31   | *The Audition*     | [s02e31-the-audition.md](s02e31-the-audition.md)        | `dae6145` | Adversarial audit of 5 generic personas (9 findings, 1 bug)   |
| S02E25   | *The Story Editor* | [s02e25-the-story-editor.md](s02e25-the-story-editor.md) | `7d57a01` | Doc-tree consolidation: new `docs/README.md` map + 8 cross-link footers |
| S02E09   | *The Receipt*      | [s02e09-the-receipt.md](s02e09-the-receipt.md)          | `8897880` | Opt-in `--show-cost` receipts (Morty's S02 debut, 11 priced models) |
| S02E32   | *The Bypass*       | [s02e32-the-bypass.md](s02e32-the-bypass.md)            | `a4fd184` | Structural shell-blocklist rewrite -- 8 reactivated bypass tests pass |

**Rest of the season:** see [`s02-writers-room.md`](s02-writers-room.md)
for the remaining arc -- E10 *Press Kit* (Lippman -- after all
bullet-emitting episodes), E26 *Locked Drawer* (Newman + Kramer --
ReadFileTool blocklist extension), E24 *Finale* (Pitt + ensemble --
absolute last).

### Season 3 -- *(unaired)*

[Blueprint](s03-blueprint.md) -- pre-season treatment, three candidate
themes awaiting showrunner greenlight.

### Season 1 -- The Pilot Years

Retroactive coverage pending. See `git log` and `CHANGELOG.md` for the raw
timeline until an on-screen cast member back-fills the pilot episodes.

## Where to find non-episode docs

The exec-report log covers *episodes* (fleet-mode coding sessions). The
broader `docs/` tree has 170+ files across security audits, ADRs,
runbooks, proposals, perf baselines, launch playbooks, and prompt
engineering. Top-level entry points:

- [`docs/proposals/`](../proposals/) -- feature proposals (FR-NNN).
- [`docs/adr/`](../adr/) -- architecture decision records.
- [`docs/security/`](../security/), [`docs/legal/`](../legal/) -- audit trails.
- [`docs/runbooks/`](../runbooks/), [`docs/ops/`](../ops/) -- operational playbooks.
- [`docs/perf/`](../perf/), [`docs/benchmarks/`](../benchmarks/) -- performance.
- [`docs/launch/`](../launch/) -- release playbooks and announcements.
- [`docs/prompts/`](../prompts/) -- prompt library and personas.

A consolidation pass (S02E25 *The Story Editor*) is queued to thin
overlap between e.g. `competitive-analysis.md` + `competitive-landscape.md`,
`i18n-audit.md` + `i18n.md` + `i18n/`, and `licensing-audit.md` +
`legal/license-audit.md`.

## Naming conventions

- **Episodes** follow the Seinfeld convention: `The <Noun>`. Short,
  iconic, no cute subtitles. Think *The Contest*, *The Soup Nazi*,
  *The Marine Biologist* -- not *How We Shipped Feature X v3*.
- **Filenames:** `sNNeMM-kebab-case-title.md` (lowercase, hyphenated).
- **Season numbering** follows the major-version / era convention
  above, not calendar time.
- **Episode numbering** is strict: S02E01 ships before S02E02. No
  retroactive numbering within a season once published -- if an episode
  gets back-filled, it gets the next sequential number regardless of
  when the work happened.

## What goes in an episode

Every episode follows the [`_template.md`](_template.md) structure:

1. **Front matter** -- commit, branch, runtime, director, cast.
2. **The pitch** -- one-paragraph log line (TV Guide-style).
3. **Scene-by-scene** -- wave-by-wave or phase-by-phase breakdown.
4. **What shipped** -- production code, tests, docs, intentional non-goals.
5. **Lessons from this episode** -- blind spots, process misses, aha
   moments. Gold, Jerry! Gold! (Bania is watching.)
6. **Metrics** -- diff size, test delta, preflight state, CI at push time.
7. **Credits** -- which agents contributed and to what.

## Why this matters

Three months from now, `git blame` will give you the *what*. This log
gives you the *why* and the *who*, with enough personality to actually
re-read. That's how institutional memory survives contributor churn --
and how the Seinfeld cast metaphor earns its keep instead of being a
gag in `AGENTS.md` that nobody references.

*-- Elaine (docs), with notes from Mr. Pitt (program management) and a
reluctant nod from The Soup Nazi (who demands strict structure).*
