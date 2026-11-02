# S04E09 -- *The Override*

> *A flag that says which model, and a second one that admits when it had nothing to decide with.*

**Commit:** `(this commit)` -- one code commit plus the episode report
**Branch:** `feat/s04e09-the-override` (PR, off `main`)
**Runtime:** ~2 hours wall-clock, with two other PRs landed in parallel
**Director:** Larry David (showrunner seat, filled by a single orchestrator)
**Cast:** 0 sub-agents, 3 inline passes by one orchestrator across 3 jj workspaces. Russell Dalrymple (LEAD, UX and presentation), Sue Ellen Mischke (guest, competitive), Morty Seinfeld (guest, FinOps)

## The pitch

S04E05 gave the picker an axis parameter and left a comment in `Program.cs`:
"E09 wires `--prefer`." The picker could already rank `AZUREOPENAIMODEL` by
cost, latency or quality; nothing let a user ask it to. E09 is that wire, plus
the audit trail that makes it trustworthy: `--why-this-model` prints which
model would be used and why, with no credentials, no network and no tokens.

The design problem was not the flag. It was a fact found by reading the
shipped registry: **it carries `costTier` only.** No model has a `latencyTier`
or a `qualityTier`. So on a stock install `--prefer latency` has nothing to
rank. The picker would still answer "chosen for axis 'latency' (tier unknown)",
which reads like a decision and is just the list order. A flag that looks like
it routed when it could not is the dishonest version of this feature, so the
episode is built around saying so out loud.

## Scene-by-scene

### Act I -- Read the data before designing the flag

Three facts shaped everything:

1. **The shipped registry has cost tiers only.** Latency and quality tiers
   exist as fields (pinned in ADR-014: `fast|medium|slow`, `premium|standard|basic`)
   but no seed model sets them. `--why-this-model` therefore carries a `Note:`
   line when a preference had no data, naming the exact field to add.
2. **A user registry replaces the seed, with no merge.** The loader says so. A
   note that told people to "add a tier" without saying the file replaces the
   built-in list would have sent them to delete the models they have.
3. **`--model` already beats the picker**, so `--prefer` only matters when no
   `--model` is given. Precedence needed no new machinery; it needed to be
   *stated*, so an ignored `--prefer` says "ignored" and not nothing.

The decision logic stays in one place. `ModelDecisionExplainer` calls
`ResolveSmartDefault.Pick` and adds nothing to it: same model, same reason
code, same four codes. It only adds the note and the last-resort chain the
runtime already used when the picker is empty.

### Act II -- Fleet dispatch

| Wave | Agents (parallel) | Outcome |
|------|-------------------|---------|
| **1** | orchestrator, workspace `e09` | `ModelDecisionExplainer`, flags, completions, 50 unit tests |
| **1** | orchestrator, workspace `cancel` | `fix-cancellation` rebased and tested (1,632 of 1,632), PR 21 |
| **1** | orchestrator, workspace `sanitizer` | Smith WIP reviewed; shell git guard rebuilt and tested (1,670 of 1,670), PR 22 |
| **2** | orchestrator, workspace `e09` | 11 integration cases against the real binary, docs, findings |

The three workspaces were jj workspaces sharing one object store, created in a
separate clone so the working checkout (with its live smith daemon and
untracked residue) was never touched. Nothing here is a claimed speedup in
seconds; it was not measured. What changed is concrete: a build and test in
one workspace ran while code was read and written in another, and CI for two
PRs ran on GitHub's side while the third was written. See Lesson 6 for what
went wrong along the way.

Two things went sideways in the product work, and both are the useful part:

- **A silent attribute theft.** Inserting a helper directly above
  `ShellExecTool.ExecuteAsync` moved its `[Description("Execute a shell
  command...")]` attribute onto the new field. Nothing failed to compile. The
  model's tool schema would simply have lost its description. A reflection test
  now pins it (this was in the PR 22 work, found by reading the diff's context
  lines, not by a test).
- **Tests that passed the first time.** All 50 passed on the first run, which
  is when to distrust them. A deliberate mutation (the axis ignored) turned 9
  red, across the reorder cases, the picker-agreement cases and rendering, then
  was reverted. They can fail.

### Act III -- What the PRs landed, and what was not landed

Two PRs merged in parallel with E09's own:

- **PR 21**, `refactor(squad)`: the old `fix-cancellation` branch, rebased.
  Behavior-neutral and labeled as a refactor, because it is one.
- **PR 22**, `fix(security)`: refuse destructive remote git operations in the
  shell tool. Salvaged from an unfinished smith worktree and corrected: that
  version missed `git push origin +main` and `bash -c "git push --force"`.
- **Not landed: the tool-output "prompt-injection sanitizer"** from the same
  worktree. Probed against ordinary content, it damaged 5 of 8 benign inputs
  (`var apiKey = config.ApiKey;` became `var [credential redacted]`; a comment
  containing "act as" was deleted outright) and missed 3 of 3 bypasses
  (leetspeak, a line split). Redacting `read_file` output corrupts what the
  agent is about to edit. It is logged as deferred with that evidence.
- **Found, not fixed: the shell blocklist is bypassed by any wrapper.**
  `bash -c 'rm --version'` and `sh -c "sudo --version"` both ran. Logged open.

**Gates for E09.** `dotnet format` clean, build 0 warnings, unit tests
1,720 of 1,720, integration 127 of 127 (2 skipped, need live Azure credentials),
`color-contract-lint` clean, `markdownlint` clean on the touched docs.

## What shipped

**Production code**

- `Resolution/ModelDecision.cs` (new). `ModelDecisionExplainer.Decide` is a pure
  function over the registry, the explicit model, the axis and the allowlist.
  It reports the picker's model and reason code unchanged, and adds a `Note`
  for three cases: `--prefer` ignored because `--model` was given, nothing to
  choose between, and no tier data. `Render` prints labeled ASCII lines and
  scrubs echoed names.
- `Resolution/ResolveSmartDefault.cs`. `HasUsableTier`, using the ranker's own
  rule, so "had data" can never disagree with "was ranked".
- `Program.cs`. `--prefer` (normalized, validated, exit non-zero with the three
  valid values on a typo), `--why-this-model` (no credentials), the real pick
  now honors `--prefer`, help text, and completions for bash, zsh and fish.
- `JsonGenerationContext.cs`. The AOT source-generated registration for the
  `--json` envelope.

**Tests**

- `PreferAndWhyThisModelTests.cs`: 50 cases. Each axis reorders a list built
  so no model wins every axis; independence from list order; `--model` beats
  `--prefer` and says so; no-data, unrecognised-tier and too-few-models notes
  (with singular and plural); the fallback chain; `Decide` always equals
  `Pick`; axis parsing; rendering and scrubbing; flag parsing; the command's
  text and JSON shape.
- `tests/integration_tests.sh`: 11 cases against the real binary with no
  credentials and a hermetic HOME: each axis picks the expected model, the
  explicit-wins and JSON cases, typo rejection, the stock-install honesty
  case, and all three completion scripts.

**Docs**

- `README.md`: two flag-table rows and a new section with real captured output,
  the tier vocabularies, and the "replaces the built-in list" warning.
- `CHANGELOG.md`, `docs/findings-backlog.md` (1 resolved, 3 open).

**Not shipped**

- **Latency and quality tiers on the seed models** (open, Maestro + Kramer).
  This is what would make `--prefer latency` useful out of the box. Assigning
  tiers is a measurement and product call, not a drive-by.
- **A default `--prefer`** by env var or config (open, Costanza).
- **A competitive comparison** (open, Sue Ellen). The repo's competitive doc
  does not cover preference flags, and no claim was invented to fill the seat.
- **Fixing the shell wrapper bypass** (open, Newman + FDR). A design choice
  about how to inspect `sh -c` payloads.

## Lessons from this episode

1. **Read the data before designing the flag.** Ten minutes with `registry.json`
   turned "add a flag" into "add a flag that is honest about having no data."
2. **A control that says "I had nothing to go on" is a feature.** The `Note:`
   line is the difference between a router and a list that pretends to be one.
3. **State the precedence out loud.** `--model` beating `--prefer` needed no
   code, but an ignored flag with no message is a support ticket.
4. **Passing on the first run is not evidence.** Break the feature on purpose
   and watch the right tests fail.
5. **Review the context lines of an insertion, not just the added lines.** The
   stolen attribute was in the lines around the diff, not in it.
6. **jj workspaces, honestly.** What worked: a separate clone, three workspaces
   in seconds, builds running concurrently, and auto-rebase of a workspace onto
   the new `main` after merges. What bit: commits imported from another remote
   are immutable, so `jj rebase` refused and my first `jj new` stacked on the
   *old* base (251 files "removed" was the tell); `jj duplicate -d main` was
   the fix. And a `pkill -f` whose pattern matched my own shell command line
   killed the shell. Neither was caught by a tool, only by reading the output.
7. **Evidence beats a plausible WIP.** The sanitizer looked reasonable and
   had tests that passed. Five lines of probing showed it damaging code.
8. **Check trailers before the merge button, not after.** PR 19 landed five
   commits without the Copilot trailer because the audit ran afterwards. Every
   PR since was audited first.

## Metrics

- **Diff:** 9 files changed, 803 insertions, 7 deletions (plus this report,
  the index row).
- **Test delta:** +50 unit, +11 integration (116 to 127).
- **Preflight:** format, build (0 warnings), unit, integration and
  color-contract all green; `exec-report-check` clean.
- **CI status at push time:** pending at commit time; link in the PR.

## Credits

Russell led, and the shape of the output (labeled lines, a `Note:` instead of a
silent no-op) is his. Sue Ellen's seat produced a recorded gap and no invented
comparison. Morty's point is in the README: a cost tier is a coarse label, not
a price, and `--estimate` is the tool for dollars. Cast-balance ledger after
this episode: Russell holds 2 leads of record (E02, E09), the E06 slate holds,
and E10 *The Score* (Maestro lead; Costanza and Uncle Leo guest) is next.
Commits carry `Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>`.
