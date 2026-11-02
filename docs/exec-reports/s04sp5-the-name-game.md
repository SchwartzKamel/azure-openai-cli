# S04SP5 -- *The Name Game*

> *Six rounds of argument about a name, one reversed verdict, and three gates nobody is allowed to skip.*

**Commit:** `(this commit)` -- docs and one read-only script; nothing is renamed
**Branch:** `docs/rebrand-review` (PR, off `main`)
**Runtime:** ~2 hours wall-clock, interleaved with S04E10 in a separate jj workspace
**Director:** Larry David (showrunner seat, filled by a single orchestrator)
**Lead:** Jackie Chiles (legal lens, not counsel)
**Cast:** 0 sub-agents, 6 inline review rounds by one orchestrator. Jackie Chiles (LEAD), Costanza, Sue Ellen Mischke, Newman, Peterman, Bob Sacamano, Jerry, Mr. Lippman, Mr. Pitt, FDR (devil's advocate)

## The pitch

The maintainer asked for a rebrand despite an earlier "do not rename" verdict, and
asked that it be reviewed in many rounds of multi-persona conferral with heavy
devil's advocate, not in a single pass. This is the off-roster special for that
review. Off-roster specials are where the cast-balance skill says to backfill
bench players with no appearances, and the bench here was thin: Jackie and Bob
had zero S04 appearances before this, Peterman one (an E03 prep brief), and Jerry
none in the arc.

The result is a **reversed verdict** and a plan with three open gates. It is
deliberately not a rename. A name is an owner's decision, a legal question is
counsel's, and neither can be made by the person running the review.

## Scene-by-scene

### Act I -- What changed since "do not rename"

The earlier verdict assumed external users with muscle memory, and that the name
was merely inconvenient. Both premises failed against evidence gathered in the
first twenty minutes:

- The repository has **0 stars, 0 forks and 0 watchers**.
- The project's own `docs/legal/trademark-policy.md` section 3 says third-party
  marks are used "only nominatively ... only so much of the mark as is
  necessary", while the project is named **"Azure OpenAI CLI"**. Two third-party
  marks are the product's identity.
- The product routes seven providers, speaks MCP, runs a queue and an eval
  harness. The name describes the first of those.

The reversal is recorded as a reversal, with its reasons, in FR-024. It was not
reversed because it was asked for twice.

### Act II -- Fleet dispatch

| Wave | Agents (parallel) | Outcome |
|------|-------------------|---------|
| **1** | orchestrator, workspace `rebrand` | Evidence: blast radius, published surfaces, trademark policy, 24-name collision sweep (background) |
| **1** | orchestrator, workspace `eval` | S04E10 built concurrently; the sweep ran while its tests were written |
| **2** | orchestrator, workspace `rebrand` | Exact-name re-check of 8 finalists; the six review rounds; FR-024 and ADR-016 |
| **3** | orchestrator, workspace `rebrand` | Claim audit of the draft; index repairs; findings; this report |

The six rounds, in one line each (the full tables, with every devil's-advocate
challenge and its resolution, are in FR-024):

1. **Rename at all?** Yes, reversed. FDR argued zero users means zero harm, so it
   is vanity. Answer: the harm is to the mark owners' view, not audience size, and
   zero users also means near-zero cost, so now is the cheap moment.
2. **What scope?** Display name and repo first, binary second with `az-ai` kept as
   an alias. A new repo and "do nothing plus a disclaimer" were rejected. Bob,
   Jerry and Lippman each named a surface that could break (formulas, the image
   path, the version bump).
3. **What makes a good name?** Six criteria. The key finding: **every short word is
   taken on npm and crates.io, and 23 of 24 on PyPI**, so "free everywhere" is
   unsatisfiable and irrelevant, because the tool is not published there. What
   matters is the channels it ships through and not shadowing a real command.
4. **Attack the finalists.** `pith` (recommended), `usher` (runner-up, polluted by
   a well-known bioinformatics tool of the same name), `herald` (does not describe
   the product). Naming is taste; the data only excludes bad choices.
5. **Stress the migration.** Espanso and AHK scripts, the image path, formulas,
   completions, deep links, env vars (unchanged), and history (never rewritten).
6. **What would change the recommendation?** A real user base, counsel clearing the
   current name, or counsel flagging the chosen one.

Three things went sideways, and all three are the useful part:

- **My own data overstated the problem.** The first GitHub column was a substring
  search, so "pith" surfaced "pithos" and looked like a collision. The three
  finalists were re-checked by exact name, and the numbers dropped sharply (97 stars
  for the largest exact-name `pith`). A second pass was warranted by the first
  pass's flaw.
- **One result was inconclusive and says so.** The exact-name search for `ask`
  returned nothing, but only because the API returns the top 30 substring hits.
  FR-024 records it as inconclusive, not clean.
- **A claim I would have made, and could not support.** The draft said the
  container registry does not redirect after a repo rename, and that GitHub's
  redirects had been tested. Neither was verified. A claim audit of the draft
  rewrote both as "documented, not tested here" and "none was found or tested".
  The same audit also removed a "known AI product" label for one name, which came
  from background knowledge and not from anything checked, and fixed a star range
  that was wrong.

### Act III -- What is deliberately not done

No file outside documentation changed, apart from one read-only script. The three
gates are written into both FR-024 and ADR-016:

- **G1 the name.** The maintainer chooses. `pith`, runner-up `usher`.
- **G2 clearance.** Counsel, or at minimum a trademark and common-law search, on
  the chosen name and on whether the current name needs to change. **Not done:**
  no trademark database, domain or social-handle check was performed here.
- **G3 go.** Approval to start Stage 1.

Until all three clear, nothing irreversible runs. The review lenses are not legal
advice, and the Jackie lens says so in the document, in the finding it opened, and
here.

**Gates.** Docs-only change plus a read-only script: dotnet preflight skipped per
`docs-only-commit`. `shellcheck` clean, `bash -n` clean, `ascii-check` clean,
`markdownlint` clean across the repo in a residue-free workspace,
`exec-report-check` clean.

## What shipped

**Production code**

- n/a. No `.cs` or `.csproj` change. `scripts/brand-inventory.sh` is a read-only
  shell script that counts every form of the product name in tracked files, with
  history excluded. It reproduces the blast-radius numbers exactly and offers
  `--json`, so the rename is measurable before and after.

**Tests**

- n/a for a docs-only episode. The evidence is reproducible instead: the FR ends
  with the commands that regenerate each figure.

**Docs**

- `docs/proposals/FR-024-rebrand-away-from-third-party-marks.md` (the six rounds,
  the evidence table, the criteria, the finalists, the staged plan, the gates).
- `docs/adr/ADR-016-rebrand-away-from-third-party-marks.md` (status Proposed).
- `docs/adr/README.md`: the index stopped at ADR-008; rows for ADR-009 through
  ADR-016 were derived from the files themselves and added.
- `docs/proposals/README.md`: FR-024 row.
- `docs/findings-backlog.md`: 3 rows (1 resolved, 2 open, including the trademark
  question that needs counsel).
- `.gitignore`: one whitelist line for the script. A first attempt would have left
  the script out of the PR entirely, because `scripts/*` is ignored by default;
  `jj status` showed it missing before anything was pushed.
- This report and the exec-report index row.

**Not shipped**

- **Any rename.** Gated; see above.
- **A name-clearance check.** Needs counsel or a proper search (G2).
- **Verification of container-registry and repo-rename redirect behavior.** Marked
  untested in FR-024; verify before Stage 1, not after.
- **Index rows for FR-021, FR-022 and FR-023** (open, Wilhelm).
- **Neutral environment-variable aliases.** A later, additive stage.

## Lessons from this episode

1. **A reversed verdict should say why it reversed.** Premises failed and a fact
   surfaced. Being asked twice is not a reason.
2. **Unsatisfiable criteria are noise.** "Free on every registry" excluded
   nothing and everything. The useful criterion was the set of channels the tool
   ships through.
3. **Your own first pass can overstate the problem.** A substring search made a
   clean name look taken. Re-check finalists with the exact question.
4. **Mark inconclusive results as inconclusive.** "None found" and "none exist"
   are different sentences.
5. **Audit the draft for claims you did not check.** Four statements in the first
   draft went further than the evidence. Each was weakened or removed.
6. **A review lens is not an authority.** The legal lens identifies a question and
   hands it to counsel. Saying so in three places is not redundancy.
7. **Separate user-visible from internal.** 275 files of C# namespace look like
   the cost of a rename and are not; the surfaces a user touches are few.
8. **Indexes drift in the same way every time.** The ADR index was missing seven
   entries. Derive rows from the files, not from memory.

## Metrics

- **Diff:** 9 files changed, 560 insertions, 0 deletions (this report and its index
  row included). No `.cs` or `.csproj`. One `.gitignore` line whitelists the script,
  because the repo ignores `scripts/*` by default.
- **Test delta:** n/a (docs-only).
- **Preflight:** docs-only path per `docs-only-commit`; `shellcheck`, `bash -n`,
  `ascii-check`, `markdownlint` (clean workspace) and `exec-report-check` green.
- **CI status at push time:** pending at commit time; link in the PR.

## Credits

Jackie Chiles led, and her lens produced the one question that matters and the
discipline of not answering it. FDR's objections are why the first round has an
answer to "this is vanity". Peterman supplied the only line of copy
("get to the pith"). Bob, Jerry and Lippman each found a real surface that a
rename touches. Cast-balance ledger after this episode: Jackie and Bob have their first
S04 appearance (Rule 3 debt reduced) and Jerry, Lippman and Peterman another, and
the E06 slate is unchanged:
E11 *The Corpus* (Newman lead; Puddy, Maestro and Rabbi guest) is next. Commits
carry `Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>`.
