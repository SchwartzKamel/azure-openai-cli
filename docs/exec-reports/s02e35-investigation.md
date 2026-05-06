# S02E35 Numbering-Gap Investigation

Date: 2026-04-29
Lead: read-only forensic pass (no writes, no renames)

## Summary

`docs/exec-reports/` jumps from `s02e34-the-index.md` to
`s02e36-the-attribution.md` with no `s02e35-*.md` in between. Git history,
the index (`docs/exec-reports/README.md`), and the writers' room
(`docs/exec-reports/s02-writers-room.md`) all confirm: **no E35 file ever
existed and no E35 episode was ever planned, drafted, or aired.** E25
through E34 plus E36 were all "off-roster" numbers assigned ad-hoc by the
orchestrator beyond the original 24-episode S02 arc. The slot 35 was
simply skipped during off-roster numbering -- a gap, not a deletion and
not a missing report.

## Evidence

### 1. No E35 file in any commit, on any branch

```text
$ git log --oneline --all -- 'docs/exec-reports/s02e35*'
(empty)
```

E35 was never created and then deleted. It never existed.

### 2. The S02 index lists 35 rows, but no row 35

`docs/exec-reports/README.md` contains `| S02E01` ... `| S02E34` and then
`| S02E36`. Total rows: 35. Highest number: 36. The skipped slot is the
number, not an episode.

```text
S02E33 The Uninstaller   350e67a
S02E34 The Index         54b9c19
S02E36 The Attribution   6e26608   <-- jumps from 34 directly to 36
```

### 3. Off-roster numbering is documented

`docs/exec-reports/s02-writers-room.md` (lines 55-66) tags E25, E27, E28,
E29, E30, E31, E32, E33, E34, E26 (out-of-order), and E36 all explicitly
as `aired (off-roster, ...)`. Off-roster episodes are reactive --
security hotfixes (E26, E32), Jerry-floor correctives (E33), docs orphan
sweeps (E34), pre-release license audits (E36). Numbers are picked when
the episode films, not pre-allocated. E35 was never picked because no
off-roster work landed in that slot between E34 (`54b9c19`, 2026-04-23
12:09:51) and E36 (`6e26608`, same day, 12:37:02) -- a 28-minute window.

### 4. Wave 7 sign-off goes 32 -> 34, not 32 -> 35

Commit `06507a6` ("Wave 7 sign-off -- E10 + E36 aired"):

> docs/exec-reports/s02-writers-room.md: aired count 32 -> 34; ...
> main-arc remaining narrowed to E24.

Two new airings (E10 + E36) raised the aired count from 32 to 34. If E35
had ever been counted as aired-or-planned, the math would not work. The
orchestrator went directly from E34 to E36 with no intermediate slot.

### 5. Season-close sign-off confirms 35 episodes shipped (count, not max)

Commit `d00beee` ("S02 season-close sign-off -- E24 aired, 35 episodes
total"): the headline `35 episodes total` is the **count of aired
exec-reports** (E01-E34 + E36 = 35 files), not the highest episode
number. The numbering ceiling is E36; E35 is the unassigned hole.

### 6. Zero references to E35 anywhere

```text
$ grep -rn 'E35\|e35' docs/
(no exec-report or planning hits; only an unrelated SHA fragment in
 docs/release/artifact-inventory.md)
```

Neither `s02e34-the-index.md` nor `s02e36-the-attribution.md` mentions
each other or an intermediate E35. `seasons-roadmap.md` and
`s03-blueprint.md` carry no E35 reference. No findings, no TODOs, no
"backfill E35" notes.

## Verdict

**Intentional skip** -- or more precisely, **unassigned slot in
ad-hoc off-roster numbering**. The off-roster scheme (E25+) hands out
numbers as episodes air. E36 was assigned to *The Attribution* without
E35 first being claimed by any in-flight work. There is no missing
episode, no deleted report, no shipped-without-exec-report process bug.
The aired-episode count (35) is intact; only the number 35 is unused.

## Recommendation

**Document the skip; do not rename.** Concrete options, ranked:

1. **(Recommended) Add a one-line footnote to `docs/exec-reports/README.md`**
   under the S02 table along the lines of:

   > Note: E35 is intentionally unassigned. Off-roster S02 episodes
   > (E25+) drew numbers as they filmed; the slot between E34 *The
   > Index* and E36 *The Attribution* was never claimed. No episode
   > is missing.

   Cost: one line, no renames, no churn in S03+ planning.

2. **Leave this investigation report (`s02e35-investigation.md`) in
   place** as the canonical answer for anyone who notices the gap
   later and re-asks the same question.

3. **Do NOT rename `s02e36-the-attribution.md` -> `s02e35-*`.** S02 is
   sealed (`d00beee`). The exec-report SHA, the writers' room, the
   index, the release-notes for v2.1.0, and Wave 7 sign-off all
   reference E36 by name. Renaming creates more drift than the gap it
   would close.

Owner of the follow-up: orchestrator (Larry David) or Elaine (index
steward). This report is informational; the action is theirs.
