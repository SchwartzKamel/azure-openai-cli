# S04SP8 -- *The Name Game, Part IV*

> *"Stop worrying about trademarks. It's open source. Compare us to the competition."*

**Commit:** `(this commit)` -- docs plus a tooling change to `scripts/name-screen.py`; nothing is renamed
**Branch:** `docs/name-landscape` (PR, off `main`)
**Runtime:** ~1 hour wall-clock, in a separate jj workspace
**Director:** Larry David (showrunner seat, filled by a single orchestrator)
**Lead:** Sue Ellen Mischke (competitive analysis; the owner asked for a comparison with the field)
**Cast:** 0 sub-agents, inline rounds by one orchestrator. Sue Ellen (LEAD), Costanza, J. Peterman, Mickey Abbott, Frank Costanza, FDR (devil's advocate)

## The pitch

The owner reopened the name and gave two instructions: ignore trademarks, since this is a
command-line tool with no legal entity, and compare against competitors on GitHub.

The first instruction removes the stage that decided the last two episodes. The second is
the better one: every earlier round generated names in a vacuum and asked only whether
they were free. Nobody had asked what the neighbours are called, or what the neighbours'
names fail to say.

## Scene-by-scene

### Act I -- Drop the trademark stage, keep what an open-source user hits

A name matters to an open-source user in four places: the command they type, the package
they install, the search they run, and the other tool they land on by mistake. The US
register matters to none of them. The screen kept the checks for those four and added a
mode for them (`--oss`).

### Act II -- Read the field

A 314-repository keyword sweep was too noisy (coding agents dominate it), so 26 comparable
tools were pulled by exact repository. Two patterns came out of the table. The tools in
the 1k to 13k tier are 12 of 14 literal "ai / gpt / shell / cli" names, and none of the ten
tools above 28k stars has a literal AI word in its name. That is confounded by vendor
backing, and the report says so. The more useful result is what nobody claims: speed, Unix
pipes, provider-agnosticism, and text injection.

### Act III -- Fleet dispatch

| Wave | Agents (parallel) | Outcome |
|------|-------------------|---------|
| **1** | orchestrator, workspace `names3` | The landscape sweep and the 26-tool table |
| **2** | orchestrator, two screening workers | 128 names written with sense lines and screened with `--oss` |
| **3** | orchestrator, two screening workers | 47 more (team, fallback, and the earlier mark-only rejections) |
| **4** | orchestrator, one worker | Re-check of 13 suspicious zero results, then 12 more in the hop family |

### Act IV -- The screen was wrong twice, and it is fixed

- **`type` passed.** The local check looked for executables, and `type` is a shell builtin
  in bash, zsh and fish. The check now asks the shell, and carries a short list of zsh and
  fish builtins that bash cannot answer for.
- **A failed GitHub lookup was reported as "no repositories".** `tempo` showed no flags
  while `grafana/tempo` has 5,552 stars, which is how it was caught. A failed lookup is an
  error now. Of the 13 names that had shown zero repositories, 7 are genuinely zero, 5 had
  repositories, and 1 was a builtin.

The second one is the third time this series has found an errored lookup treated as a clean
result. It is the same defect as `newman-2026-11-N-6`, in a different place.

### Act V -- Decision and the devil's advocate

`hopper`. It passes the one-sentence sense test twice (it hops between providers, and stdin
goes into the hopper), it has the shape of the top-tier names (a friendly word, with `-er`
like `aider`), and its meaning is already in the code: the project's telemetry calls a
provider switch a `fallback_hop`. `anymux` stays as the descriptive runner-up and `tell`
as a minimalist third. `summon` was the author's early favourite and is out, because
`cyberark/summon` is a command-line tool with that name.

FDR's objection to `hopper` was that "a hopper is a bin, which has nothing to do with
models". It was answered, not dismissed: that is the second meaning, and it fits stdin.

## What shipped

**Production code**

- n/a. No `.cs` or `.csproj` change.

**Tooling**

- `scripts/name-screen.py`: an `--oss` mode; a shell-builtin check; a GitHub lookup failure
  is now an error instead of "none". The default (trademark) mode is unchanged and was
  re-run to confirm.

**Tests**

- n/a. The script has no test suite. It was checked by re-running known cases: `aider` is
  out on Homebrew, `type` is out as a builtin, `anymux` passes, and `pith` still fails the
  default mode on a live mark.

**Docs**

- `docs/proposals/FR-024-...md`: a third addendum (rounds 16 to 19) with the competitor
  table, the naming-strategy analysis, the funnel, the defects found, the finalists, the
  devil's advocate and the decision.
- `docs/adr/ADR-016-...md`: the recommended name updated.
- `docs/findings-backlog.md` and the index row.

**Not shipped**

- **Any rename.** Gate G1 (the owner confirms the name) and G3 (go) are open.
- **A trademark or legal opinion.** Not part of this round, by instruction.

## Lessons from this episode

1. **Look at the neighbours before naming yourself.** The competitor table says more about
   what to avoid (another `ai-something`) than 186 candidates did.
2. **Name the claim you are making.** "Zero results" from a top-100 search means "nothing
   notable", and the proposal says so.
3. **A suspiciously clean result is a prompt to re-check.** `tempo` having no flags was
   the only sign that the tool was failing quietly.
4. **Your own early favourite can be the collision.** `summon` felt right and is a
   762-star command-line tool.
5. **Dropping a constraint changes the old verdicts.** Names rejected only on marks were
   re-screened, not quietly reinstated.

## Metrics

- **Diff:** 6 files changed (the report, the index row, FR-024, ADR-016, the backlog, the
  script).
- **Names:** 186 screened, 126 survived distribution, 62 had no AI-space collision, 10
  compared closely, 1 recommended.
- **Test delta:** n/a.
- **Preflight:** `ascii-check`, `markdownlint`, `exec-report-check` and a syntax check of the
  script. The dotnet gates are unaffected, since no `.cs` changed.
- **CI status at push time:** pending at commit time; link in the PR.

## Credits

Sue Ellen led because the owner's request was a competitor comparison. Costanza's sense
test from S04SP7 is why the finalists are explained in a sentence. Peterman wrote the
usage lines. Frank pressed on the silent-failure path that exposed `tempo`. FDR argued the
case against `hopper`. Cast-balance ledger after this episode: Sue Ellen holds a lead of
record outside the arc, and the E06 slate is unchanged. Commits carry
`Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>`.
