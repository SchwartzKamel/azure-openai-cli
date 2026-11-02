# S04SP7 -- *The Name Game, Part III*

> *"Those names don't make sense." They were right. Names have to say what the tool does.*

**Commit:** `(this commit)` -- docs only; nothing is renamed
**Branch:** `docs/name-anymux` (PR, off `main`)
**Runtime:** ~2 hours wall-clock, interleaved with S04E12 in a separate jj workspace
**Director:** Larry David (showrunner seat, filled by a single orchestrator)
**Lead:** Costanza (product; a name is the first feature)
**Cast:** 0 sub-agents, 3 inline rounds by one orchestrator. Costanza (LEAD), J. Peterman, Mickey Abbott, Babu Bhatt, Frank Costanza, FDR (devil's advocate)

## The pitch

S04SP6 ended with `dunlin`, a small wader that flies in flocks, and `curlew` as runner-up.
The owner's reply was four words: those names don't make sense.

That is not a taste complaint to be argued with. It is a correct description of a ranking
mistake. Last time "clears the trademark screen" decided the order and meaning was a
tiebreaker, so two arbitrary birds with clean records beat everything that explained
itself. This special reverses the order: a name has to pass a one-sentence sense test
("it is called X because it does Y") before it is screened, not after.

## Scene-by-scene

### Act I -- Reverse the ranking rule

The earlier method was sound at ruling names out and poor at choosing among the ones left.
Automation can clear a name; it cannot tell you whether a person would understand it. So
the generation step changed to start from **what the tool does** and write the sense
sentence first.

### Act II -- Fleet dispatch

| Wave | Agents (parallel) | Outcome |
|------|-------------------|---------|
| **1** | orchestrator, workspace `name2` | 84 meaning-first names written with their sense lines, screened in the background |
| **1** | orchestrator, workspace `gate` | S04E12 built concurrently |
| **2** | orchestrator, workspace `name2` | A second batch of 31 (blends and adverb voices), re-run serially after failures |
| **3** | orchestrator, workspace `name2` | A crowding check, the taste round, the decision, the write-up |

Three findings drove the result:

- **Almost every obvious word is already a live software mark.** `gateway`, `courier`,
  `dispatch`, `crossbar`, `intercom`, `mux`, `oracle`, `switchboard`, `junction`,
  `nutshell`, `distill`, and every adverb voice except `plainly` were out. The words that
  best describe a router are the words everyone has already registered.
- **A trademark screen cannot see a same-named tool doing the same job.** `llmsh` cleared
  the register and is already a PyPI command-line tool for using LLMs in your shell.
  `pipellm` is already an npm CLI for LLM pipelines. Reading what the existing projects
  *are* removed both. This is the crowding check, and it is now part of the method.
- **Parallel lookups failed and were not findings.** Three concurrent USPTO queries ran
  beside a full build and test gate, and most failed. Errored lookups were re-run one at a
  time, the same lesson as the previous special.

The names that survived both screens and the sense test:

| Name | "It is called X because..." |
|------|------------------------------|
| **anymux** | it multiplexes any provider behind one command, as `tmux` does for terminals |
| **plainly** | it answers plainly: the default prompt is "factual, no fluff" |
| **askany** | you ask any model |

### Act III -- The decision, and the devil's advocate

**`anymux`**, with `plainly` and `askany` as runner-ups. It is the only one that names the
product's real differentiator (many providers behind one command), and it is the cleanest
on every check: no exact or whole-word live US mark, no GitHub repository, PyPI package or
npm package of that name, no command it can be mistyped for, and `.dev` is free.

FDR's objection was the best of the round: *`mux` is jargon, and "any" over-promises.* The
reply is in the proposal: the audience already uses a command-line tool with provider
flags, for whom `tmux` and `mux` are ordinary words, and the tagline keeps "any" honest
(*anymux: plain answers from any model*). Peterman's point, that `plainly` is the better
voice, is why it is the runner-up and why the tagline borrows its word.

The owner asked not to spend more on clearance, so none is pursued here. The limits stay
stated in the proposal as one line each, because dropping the caveat would be a different
claim than not pursuing the work.

**Gates.** Docs only: dotnet preflight skipped per `docs-only-commit`. `ascii-check`
clean, `markdownlint` clean across the repo in a residue-free workspace,
`exec-report-check` clean.

## What shipped

**Production code**

- n/a. No `.cs`, `.csproj`, script or workflow change.

**Tests**

- n/a for a docs-only episode. The evidence is reproducible: `scripts/name-screen.py
  anymux` reproduces the screen, and the crowding check is three queries listed in the
  proposal's table.

**Docs**

- `docs/proposals/FR-024-...md`: a second addendum (rounds 13 to 15) with the objection,
  the meaning-first families, what was eliminated and why, the crowding table, the
  shortlist on the sense test, the taste round and the decision. The first addendum is
  kept as the record of the `dunlin` round, not deleted.
- `docs/adr/ADR-016-...md`: the recommended name updated to `anymux`.
- `docs/findings-backlog.md`: the copy finding reworded to the chosen name.
- This report and the index row.

**Not shipped**

- **Any rename.** The three gates in FR-024 still apply.
- **Brand copy.** The tagline and the "formerly Azure OpenAI CLI" line are Stage 1 work.

## Lessons from this episode

1. **When the owner says a name does not make sense, believe it.** It describes a ranking
   error, not a preference.
2. **Put the sense test before the screen.** Meaning decides what is worth screening;
   the screen only rules names out.
3. **The best words for a thing are already taken.** The common vocabulary of routing and
   conversation is almost entirely registered in the software classes.
4. **Read what the existing projects are.** A clean register says nothing about a tool
   that already does the same job under the same name.
5. **Errored lookups are not eliminations.** Said last episode, true again.
6. **Withdraw by reason, not by embarrassment.** `dunlin` and `curlew` were withdrawn on
   taste, and the proposal says that, not that the evidence changed.

## Metrics

- **Diff:** 5 files changed (the report, the index row, FR-024, ADR-016, the backlog). No code.
- **Names:** 109 unique meaning-first candidates, 4 compared closely, 1 recommended.
- **Test delta:** n/a (docs-only).
- **Preflight:** docs-only path; `ascii-check`, `markdownlint` (clean workspace) and
  `exec-report-check` green.
- **CI status at push time:** pending at commit time; link in the PR.

## Credits

Costanza led, and the sense test is his: a name is the first thing the product says. FDR's
objection to `mux` is why the decision carries its own counter-argument. Peterman's
voice point is why `plainly` is the runner-up. Mickey, Babu and Frank each narrowed the
field for a stated reason. Cast-balance ledger after this episode: Costanza holds a lead
of record outside the arc, and the E06 slate is unchanged: E13 *The Redaction* (FDR lead of
record, Newman co-lead, Jackie guest) is next. Commits carry
`Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>`.
