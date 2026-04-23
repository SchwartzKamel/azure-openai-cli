# Exec Reports — *Seinfeld Coding Cast*

> *"What's the deal with ... shipping software?"*

A post-facto log of every fleet-mode coding session on `azure-openai-cli`.
Each episode captures what the cast built, who showed up, what went
sideways, and what we learned -- structured like a proper TV show
because the [cast itself is](../../AGENTS.md).

## Seasons

| Season | Theme                          | Era                         |
|--------|--------------------------------|-----------------------------|
| **S01** | The Pilot Years                | Initial commit through v1   |
| **S02** | Production & Polish (v2 era)   | Post-v2, current            |

New seasons get declared when a major version ships, a theme shifts, or
the cast would tell you it's time for a retool.

## Episode index

### Season 2 -- Production & Polish

| #        | Title              | Episode file                                        | Commit    | What happened                                           |
|----------|--------------------|-----------------------------------------------------|-----------|---------------------------------------------------------|
| S02E01   | *The Wizard*       | [s02e01-the-wizard.md](s02e01-the-wizard.md)        | `f57032f` | Interactive first-run wizard + per-OS LOLBin storage     |
| S02E02   | *The Cleanup*      | [s02e02-the-cleanup.md](s02e02-the-cleanup.md)      | `f65adbd` | docs-lint green + whitespace-key guard across all stores |

### Season 3 -- *(unaired)*

[Blueprint](s03-blueprint.md) -- pre-season treatment, three candidate
themes awaiting showrunner greenlight.

### Season 1 -- The Pilot Years

Retroactive coverage pending. See `git log` and `CHANGELOG.md` for the raw
timeline until an on-screen cast member back-fills the pilot episodes.

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
