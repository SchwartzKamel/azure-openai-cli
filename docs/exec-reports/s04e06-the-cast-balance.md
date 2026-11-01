# S04E06 -- *The Cast Balance*

> *Mr. Pitt counts the room. Two rules are on the clock, one is already broken in the plan, and the index forgot a season.*

**Commit:** `(this commit)` -- docs-only; no production code touched
**Branch:** `lafiamafia/s04e06-cast-balance` (PR, off `origin/main`)
**Runtime:** ~1 hour wall-clock
**Director:** Larry David (showrunner seat, filled by a single orchestrator)
**Cast:** 0 sub-agents, 1 dispatch wave. Mr. Pitt (LEAD, audit voice), Lloyd Braun (cited as the missing junior lens), Larry David (ratifies the slate)

## The pitch

The `writers-room-cast-balance` skill makes the E06 slot a mandatory
mid-season checkpoint. S04 aired E05 and then jumped to E07, so the
checkpoint was never run. The slot sat empty while the blueprint
([`s04-blueprint.md`](s04-blueprint.md)) kept slating leads for E08
through E13 without anyone measuring the drift those leads would have to
correct.

This is an audit-only episode, as the blueprint says: no feature, no
code. The deliverable is a measurement and a decision. The measurement
comes from the aired reports themselves, not from memory. The decision is
a corrected slate for E08-E13, ratified so the next episode does not
start by arguing about who is in it.

Two things turned up that the audit was not looking for. The blueprint's
own running order contains a Rule 1 violation (back-to-back Maestro
leads). And the exec-report index in `README.md` still calls S03 "unaired"
and has no S04 section at all, so the one document readers use to find
episodes was missing about forty of them.

## Scene-by-scene

### Act I -- Method, and why the first count was wrong

Appearances were pulled mechanically. For each aired arc episode
(S04E01-E05, E07) the cast section and front matter were matched against
the 27 roster names. That gives a reproducible tally, but a keyword match
cannot tell a cast entry from a citation.

So every low count was spot-checked against the source line. Four held up
as real cast entries (Pitt E02 prep, Peterman E03 prep, Babu E04 i18n
wave, Puddy E05 W2 lead). One did not: **Lloyd Braun's E02 hit is the
*subject* of Elaine's fix-forward ("Lloyd Braun top-3 onboarding"), not a
cast appearance.** The first pass said Lloyd had 2 appearances. The
correct number is 1 (E01), which is also what the blueprint's own ledger
says. An audit that overcounts the junior lens is exactly the failure
Rule 4 exists to prevent, so the corrected figure is the one used below.

### Act II -- Fleet dispatch

| Wave | Agents (parallel) | Outcome |
|------|-------------------|---------|
| **1** | orchestrator (inline, Pitt voice) | Tally, spot-check, five-rule verdicts, corrected slate, index repair |

No sub-agents were dispatched. The fleet-dispatch skill forbids a solo
*background* dispatch; this ran inline in the orchestrator's own turn, so
the rule is not engaged. That is the same shape as S03E17, S03E20 and
S03E27.

#### The tally (S04 arc episodes aired: E01-E05, E07)

Leads of record, by airing order:

| Ep | Lead | Co-lead / notable |
|----|------|-------------------|
| E01 | Kramer | Elaine (docs), Babu (off-roster i18n special) |
| E02 | Russell | Mickey (a11y), Kramer (reader), Newman (hotfix) |
| E03 | Bookman | Maestro (co-lead), Kramer |
| E04 | Elaine | Mickey (co-lead), Babu (i18n wave) |
| E05 | Costanza | Maestro (co-lead), Puddy (W2 lead) |
| E07 | Frank Costanza | Newman (co-lead), Puddy (W2 lead) |

Appearances (lead or guest), after the Lloyd correction:

| Appearances | Cast |
|-------------|------|
| 6 | Puddy |
| 4 | Kramer, Elaine, Mickey, FDR |
| 2 | Costanza, Newman, Frank, Maestro, Babu, Bookman |
| 1 | Mr. Pitt, Peterman, Morty, Bania, Russell, Lloyd |
| **0** | **Jerry, Jackie, Bob, Uncle Leo, Sue Ellen, Keith, Rabbi, Soup Nazi** |

Mr. Lippman (SP2) and Mr. Wilhelm (SP3) have zero arc appearances but each
led an off-roster special, so they clear Rule 3. SP1 has an empty cast
block and is not counted either way.

#### Rule-by-rule verdict

| Rule | Verdict | Evidence |
|------|---------|----------|
| **1. No back-to-back leads** | **PASS (aired), FAIL (planned)** | Aired leads never repeat. But the blueprint slates *The Score* and *The Corpus* both to Maestro, which in the revised order lands as E10 then E11. |
| **2. Main-cast multi-lead floor** | **AT RISK** | Kramer, Elaine and Costanza hold 1 lead each. Newman and **Jerry hold 0**. The failure line is "zero leads after E12"; Newman's only slated lead (*The Redaction*) is E13, which is *after* the line. |
| **3. Supporting one-appearance floor** | **ON TRACK, WITH DEBT** | Eight players at zero. The failure line is E18, so this is a scheduling debt, not a violation yet. |
| **4. Lloyd is the junior lens** | **WARNING** | E04 *Reading Room* is the skill's own "bad" example: docs-heavy, Elaine leading, no Lloyd. 27 of his E01 review items are still parked in `docs/model-cards/REVIEW-onboarding.md`. |
| **5. Pair complementary guests** | **WARNING** | Pairings are firing (Kramer+Elaine, FDR+Puddy, Frank+Newman), but FDR+Puddy has fired in E01, E02, E03 and E07 with no rotation. |

Bench utilization is also lopsided: Puddy is in 6 of 6 arc episodes while
8 players have not appeared once. That is the skill's "5+ versus 1"
warning, and it is the cheapest one to fix.

A note on the main-cast floor itself. "Multiple leads per season" was
written for a 24-episode order with five main cast. S04 has 24 slots, but
about half are pre-assigned by arc theme, so two leads for each of five
people is arithmetically tight. This audit treats "one lead each, second
lead owed by Act IV" as the S04 reading and records it as a decision, not
a loophole.

#### The corrected slate (recommendation, ratified below)

| Slot | Title | Lead | Guests | What it fixes |
|------|-------|------|--------|---------------|
| E08 | *The Onboarding* | Lloyd Braun | Costanza (Rule 5 pairing), Mickey | Rule 4; pays down the 27 parked review items |
| E09 | *The Override* | Russell | Sue Ellen, Morty | Rule 3 (Sue Ellen); competitive read on how other routers expose `--prefer`, plus cost audit |
| E10 | *The Score* | Maestro | Costanza, Uncle Leo | Rule 3 (Uncle Leo); contributor-facing fixture format |
| E11 | *The Corpus* | **Newman** (was Maestro) | Puddy (W2 lead), Maestro, Rabbi | **Rule 1** (no back-to-back Maestro); **Rule 2** (Newman gets a lead before the E12 line); Rule 3 (Rabbi, for the refusal corpus) |
| E12 | *The Gate* | Jerry | Soup Nazi, Frank | **Rule 2** (Jerry's first S04 lead); Rule 3 (Soup Nazi) |
| E13 | *The Redaction* | FDR (was Newman) | Newman (co-lead, policy owner), Jackie | Rule 3 (Jackie); FDR gets a lead of record; red-team-led redaction |

Still owed by E18 with no slot yet: **Bob** (packaging) and **Keith**
(talk material). Act IV (caching) is the natural home; the E12 checkpoint
decides it. Second leads owed to Kramer, Elaine, Costanza and Newman are
carried forward, not scheduled here.

Why Newman on *The Corpus* and not an off-roster special: bounding corpus
size and proving provenance is half of what the E13 redaction policy
needs anyway, so the lead is real work and not a favor. If the showrunner
prefers an off-roster special, Rule 2 is satisfied either way; what is not
acceptable is leaving Newman at zero past E12.

### Act III -- Index repair, ratification, and what was not run

**Ratified.** The slate above is adopted as the S04 running order from
E08. This is the showrunner seat's call; any later override should be
recorded in the E12 checkpoint and not made silently in a brief.

**Index repair.** `docs/exec-reports/README.md` now lists S03 as aired and
carries a Season 4 section with every aired S04 arc episode and special,
each row's commit field and log line taken from the report itself. S03
gets a pointer, not a full back-fill: thirty rows of S03 history is its
own task and is listed under *Not shipped*.

**Gates.** Docs-only change, so the dotnet preflight is skipped per
`docs-only-commit`. `ascii-check` and `markdownlint` are run on the
touched files only; `exec-report-check` runs repo-wide.

## What shipped

**Production code**

- n/a. Audit-only, as the blueprint specifies. No `.cs`, no `.csproj`.

**Tests**

- n/a for a docs-only episode. The check is reproducible instead: the tally
  comes from the aired reports, and the spot-check lines are cited in Act I.

**Docs**

- `docs/exec-reports/s04e06-the-cast-balance.md` (this report).
- `docs/exec-reports/README.md`: S03 and S04 status rows corrected, a
  Season 4 index section added.
- `CHANGELOG.md`: one `[Unreleased]` line.

**Not shipped**

- **S03 index back-fill** (30 episodes). Real work, wrong episode; needs
  its own pass so the SHAs are checked, not guessed.
- **`s04-blueprint.md` edits.** The blueprint is canonical history. The
  corrected slate lives here and the E12 checkpoint reconciles it, so the
  original is not retro-edited.
- **A mechanical roster/appearance script.** The keyword tally needed a
  human spot-check to be trusted. A script that reads a structured
  `Lead:` / `Cast:` front matter would remove that, but the S04 reports do
  not yet share one (see Lesson 2).
- **SP1 attribution.** Its cast block is empty; recovering the cast needs
  the original dispatch brief, not a guess.

## Lessons from this episode

1. **A mandatory checkpoint can be skipped silently.** The E06 slot was
   not rejected, it was simply stepped over. Nothing in the pipeline fails
   when an episode number is missing. A gap in the airing sequence is now
   a signal worth checking at the start of each episode.
2. **The "lead" column means three different things.** The E01 ledger
   counts wave leads, the blueprint counts leads of record, and later
   reports use `LEAD` and `CO-LEAD` inconsistently. This audit uses
   *lead of record* and says so. A shared `Lead:` front-matter field would
   make the next audit a one-liner.
3. **A keyword tally overcounts.** Lloyd read as 2 and was 1, because a
   cast section can name someone it is not casting. Spot-check every low
   number before it becomes a casting decision.
4. **The plan can break a rule the aired record never did.** Rule 1 held
   for every aired episode and failed in the slate. Audit the plan, not
   only the history.
5. **Index drift is invisible until someone reads it as a newcomer.** A
   reader looking for S04 found "unaired" S03 and nothing else. That is
   Lloyd's lens applied to the repo's front door.
6. **One agent playing the whole cast is allowed, and should say so.**
   `Cast: 0 sub-agents` is the honest front matter here. The Pitt voice is
   a framing device for an audit one orchestrator ran, not a claim of a
   fleet.

## Metrics

- **Diff:** 3 files (1 new report, README index, CHANGELOG). No code.
- **Test delta:** n/a (docs-only).
- **Preflight:** dotnet gates skipped per `docs-only-commit`; `ascii-check`
  and `markdownlint` run on touched files; `exec-report-check` green.
- **CI status at push time:** pending at commit time; link in the PR.

## Credits

Mr. Pitt ran the audit. Lloyd Braun is the reason Rule 4 is checked and
the reason the Lloyd count was corrected. Larry David ratifies the slate.
Commits carry `Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>`.
