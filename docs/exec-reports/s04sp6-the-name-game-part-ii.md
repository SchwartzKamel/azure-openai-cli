# S04SP6 -- *The Name Game, Part II*

> *The first shortlist died at the trademark office. 719 names later, a small shorebird that flies in a flock.*

**Commit:** `(this commit)` -- docs and one read-only script; nothing is renamed
**Branch:** `docs/name-selection` (PR, off `main`)
**Runtime:** ~4 hours wall-clock, running in the background beside S04E11
**Director:** Larry David (showrunner seat, filled by a single orchestrator)
**Lead:** J. Peterman (storyteller; a name is copy)
**Cast:** 0 sub-agents, 6 inline screening rounds by one orchestrator. J. Peterman (LEAD), Costanza, Sue Ellen Mischke, Mickey Abbott, Babu Bhatt, Frank Costanza, Bob Sacamano, Jackie Chiles and Newman (legal lens, not counsel), FDR (devil's advocate)

## The pitch

The maintainer asked for a good name found by multi-persona, multi-round review, and
for me to carry it out and proceed. S04SP5 reversed the "do not rename" verdict and
recommended `pith`, with `usher` as runner-up, on the strength of package registries
and GitHub. This special is what happened when that recommendation met the US
trademark register.

It did not survive. A programmatic query of the USPTO's public search found a live
application for PITH in the software classes, two live software-class marks for
USHER, and a live class 9 mark for HERALD. All three earlier finalists were out. The
lesson is written into the proposal: a package registry and a GitHub search are not a
trademark screen, and the previous review's evidence table had a gap exactly there.

The rest of the episode is how to find a name once you know that, honestly, including
the places the method itself went wrong.

## Scene-by-scene

### Act I -- The shortlist dies

The first step was to find out whether a trademark screen was possible at all. The
`tmsearch.uspto.gov` page returned 200, but that proved nothing; a POST to its search
backend returned real records (18 hits for "pith"). That made a preliminary screen of
US federal marks available, which was the thing the first review lacked. Running it on
the three finalists took seconds and ended the shortlist.

### Act II -- Fleet dispatch

| Wave | Agents (parallel) | Outcome |
|------|-------------------|---------|
| **1** | orchestrator, workspace `name` | Screen built; 435-name first pass in the background |
| **1** | orchestrator, workspace `corpus` | S04E11 built concurrently while the sweep ran |
| **2** | orchestrator, workspace `name` | Re-screen of errored lookups; GitHub and near-miss stages; four more rounds |
| **3** | orchestrator, workspace `name` | Persona round, decision, tracked script, write-up |

No sub-agents were dispatched. The parallelism was real: a sweep taking over thirteen
minutes ran while S04E11's code, tests and gate were being done.

Six screening rounds covered **719 unique names**. Each round learned from the last:

1. **Words and random coinages (435).** Dictionary words mostly died on live marks.
   Random coinages passed everything and were **unusable**: `kofpu`, `rudmu`, `sluiex`.
   Automation can clear a name; it cannot give it taste.
2. **Curated by lens (83).** Streams, nautical guidance, messengers, concision, word
   roots, tools, obscure birds. Real words, hand-chosen, and a workable survivor list.
3. **Constructive coinages (142).** Roots tied to what the tool does, plus short words
   for "word" and "ask" from other languages. Cleared easily, still ugly.
4. **Hand-designed (27), then a taste-curated finalist set (28)** re-run so every name
   had identical data. This is where `pipit` looked like the winner and was not.
5. **Waders, textiles, stone, weather (40).** Added `curlew`, the runner-up.

### Where the method was wrong, and what each error cost

- **187 lookups errored and were first counted as eliminations.** Eight parallel
  threads were rejected by the USPTO endpoint, and "uspto: error" rows were being
  dropped as if they were findings. They were re-screened serially with retries before
  any conclusion was drawn. Two never resolved and are logged.
- **The first GitHub stage was silently rate-limited.** An unauthenticated client
  allows about ten searches a minute; many lookups came back empty. It was redone with
  the authenticated client.
- **Wikipedia presence was a bad filter.** 163 of 222 short candidates had a page, so
  it filtered nothing. It was demoted to information.
- **Edit-distance-1 counts hid severity.** `pipit` was a real bird, five letters,
  sounding like "pipe it", with one unrelated clothing mark. It had 11 near-miss
  marks. Reading them showed **PIPPIT**, a live software mark one letter away, listed
  to Lemon Inc. I believe the product is an AI video tool; that part is background
  knowledge and unverified. The live mark alone ruled it out. A count would have
  shipped it; reading the names caught it.

### Act III -- Five finalists, a taste round, a decision

`dunlin`, `curlew`, `uncial`, `ouzel` and `adage` were compared on identical data:
live US marks, near-miss marks, exact-name GitHub repositories, and whether any AI
project already bore the name (none did).

The persona round gave each lens a real objection. Peterman's pitch for `dunlin` is a
flock that turns as one, which is multi-provider routing. Mickey ruled out `uncial`
and `ouzel` on ambiguous pronunciation, and noted `curlew` is spelled unlike it
sounds. Frank flagged `curlew` as a tab-completion neighbour of `curl` and `adage` as
a search collision with a well-known advertising publication (which I believe is Ad
Age; unverified). Sue Ellen noted a common word gets lost in search while arbitrary
names are the norm among peers. Babu found no offensive meaning for `dunlin` in what
he could reason about, with the English verb "to dun" the only echo. FDR's objection
was the best of the round: "It is a boring bird, it sounds like 'dull', 'dun' means
debt collector, and you will spend a year explaining what a dunlin is."

That was answered in the proposal and not dismissed, and it is the kind of objection
gate G1 exists to let the owner overrule.

**Decision: `dunlin`, runner-up `curlew`.** `dunlin` has two live US marks in
unrelated goods (a plastic bottle holder, dental supplies), no live software marks
within two edits, and exact-name repositories that are a crypto-notes app, a
clinical-trial library and a panorama editor. It is spoken and typed the way it reads.

**What is not established**, stated as plainly as the decision: this is a preliminary
screen of US federal marks as of 2026-11-02, not clearance and not legal advice. It
does not cover state or common-law marks, unregistered use, the EU, UK or WIPO
registers (the EUIPO site was unreachable; the WIPO database needs a browser),
translations, or sound-alikes beyond two edits. Specific questions are logged for
counsel, including DUNLOP, a tyre brand I know from background knowledge, which is two
edits from `dunlin` and shares its first syllable.

**Gates.** Docs and a read-only script: dotnet preflight skipped per
`docs-only-commit`. `ascii-check` clean, `markdownlint` clean across the repo in a
residue-free workspace, `exec-report-check` clean, and the script was run against
`pith`, `dunlin` and `curlew` and reproduced the earlier results.

## What shipped

**Production code**

- n/a. No `.cs` or `.csproj`. `scripts/name-screen.py` is a read-only, self-contained
  screen (local commands, package channels, US trademark exact and near-miss by class,
  GitHub, domains, command typo-neighbours). It states its own limits in its output
  and prints every live mark it relied on, so a human can check the reasoning. It
  replaces a pile of throwaway scripts so the screen can be re-run on any name.

**Tests**

- n/a for a docs-only episode. The script was checked against known results: it
  rejects `pith` for the Pith Games mark and passes `dunlin` and `curlew` with notes.

**Docs**

- `docs/proposals/FR-024-...md`: an addendum (rounds 7 to 12) with the funnel table,
  the method's errors, the finalists table, the persona round with devil's advocate,
  the decision, and what is and is not established. The superseded round is marked,
  not deleted.
- `docs/adr/ADR-016-...md`: the recommended name updated to `dunlin`, with the
  withdrawn shortlist explained.
- `docs/findings-backlog.md`: 3 rows (all open).
- `.gitignore`: a whitelist line for the script (the repo ignores `scripts/*`).
- This report and the index row.

**Not shipped**

- **Any rename.** G1 (owner confirms), G2 (clearance) and G3 (go) are all still open.
- **Clearance.** Counsel, or at minimum a full common-law and international search.
- **Copy.** `dunlin`'s story, the README description and the "formerly Azure OpenAI
  CLI" line are Stage 1 work, after the gates.
- **Resolving two persistent lookup errors** from round one (logged).

## Lessons from this episode

1. **A package registry is not a trademark screen.** Three finalists that looked clean
   on registries and GitHub were all out at the trademark office.
2. **Automation clears, taste chooses.** The screen is for ruling names out cheaply.
   The names worth considering were chosen by hand and then screened.
3. **Read the near-misses; do not count them.** Eleven neighbours meant nothing until
   one of them was a live software mark one letter away.
4. **An error is not an elimination.** 187 failed lookups were briefly treated as
   findings. Resolve the errors before drawing conclusions.
5. **Rate limits shape the method.** Unauthenticated GitHub and eight parallel
   threads each quietly produced empty or failed results.
6. **Label background knowledge.** Lemon Inc.'s product, Ad Age and DUNLOP each rest
   on what I know, not on anything checked, and each is marked so.
7. **Withdraw a recommendation openly.** The earlier review recommended `pith`. The
   method had a gap and the proposal now says so.
8. **Delegating a decision does not delegate its gates.** The name is chosen;
   clearance and the go-ahead are still not mine to give.
9. **Track the tool, not just the result.** A reproducible script means counsel or the
   maintainer can screen the next name without me.

## Metrics

- **Diff:** the script, three docs, findings and index (see the PR). No code.
- **Test delta:** n/a (docs-only).
- **Names screened:** 719 unique, across 6 rounds. Final survivors compared: 5.
- **Preflight:** docs-only path; `ascii-check`, `markdownlint` (clean workspace) and
  `exec-report-check` green; the script verified against known results.
- **CI status at push time:** pending at commit time; link in the PR.

## Credits

J. Peterman led, and the flock story is his. FDR's objection is the reason the
decision carries its own counter-argument. Jackie and Newman kept the legal lens
honest by refusing to answer the question they raised. Mickey, Babu, Frank, Bob, Sue
Ellen and Costanza each ruled something in or out for a stated reason. Cast-balance
ledger after this episode: Peterman has a lead of record, Babu, Mickey, Frank and Bob
each have another appearance, and the E06 slate is unchanged: E12 *The Gate* (Jerry
lead; Soup Nazi and Frank guest) is next. Commits carry
`Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>`.
