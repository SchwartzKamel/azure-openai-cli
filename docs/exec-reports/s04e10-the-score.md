# S04E10 -- *The Score*

> *Prompts get a scoreboard. A suite that grades nothing fails, and a safety fixture nobody can verify fails louder.*

**Commit:** `(this commit)` -- one code commit plus the episode report
**Branch:** `feat/s04e10-the-score` (PR, off `main`)
**Runtime:** ~3 hours wall-clock, with two other pieces of work running in parallel workspaces
**Director:** Larry David (showrunner seat, filled by a single orchestrator)
**Cast:** 0 sub-agents, 3 inline passes by one orchestrator across 3 jj workspaces. The Maestro (LEAD, prompt engineering), Costanza (guest, product), Uncle Leo (guest, contributor lens)

## The pitch

`docs/prompts/eval-harness.md` has described a prompt eval harness since S02. Its
status line read "design-only ... The runner does not yet exist." The design is
good and it names its own hard problem: the **trait judge**. Freeform traits such
as "sounds like the reviewer persona" need a human or an LLM to grade, and an LLM
judge introduces variance of its own and needs its own fixtures to validate.

E10 ships the half that does not need a judge: the schema, the runner and the
scoring, over **deterministic assertions** (`contains`, `regex`, `max_chars`,
`json_valid`, and four more). `az-ai eval <suite.json>` runs each fixture's prompt
the way the CLI would, scores it, prints a report, and exits non-zero on failure
so it can gate CI (that gate is E12). Freeform traits are reported as *ungraded*.
The design doc's own advice was "ship (1) first", which is human review; the
shipped runner leaves that tier to humans and says so instead of faking a judge.

The interesting part of the episode is not the assertions. It is what a scoreboard
must refuse to do.

## Scene-by-scene

### Act I -- Decide what the scoreboard must refuse

A scorer can mislead in quiet ways. Four policies were fixed before any code, each
earned by a specific failure mode:

1. **No vacuous pass.** "0 of 0 passed" must never look green. A suite that grades
   nothing fails and says why. This matters immediately. The design doc's own
   `coder.json` has no assertions, and a real run shows what the policies do with
   it: two fixtures are reported ungraded (six traits each waiting on a judge), and
   the third, a safety fixture with no assertion to verify it, errors as "cannot be
   verified", so the suite fails closed. That is correct, and it is the honest first
   message to anyone who points the tool at the existing fixtures.
2. **Errors count against the score.** A fixture that cannot run (missing persona,
   provider error, timeout) is graded and failed, never skipped. Skipping errors
   would let a provider outage score 100%.
3. **Safety fails closed.** A safety fixture that fails, errors, or has no
   assertion to verify it with blocks the run regardless of `--fail-under`. The
   design doc says "no override, no waiver"; the code now agrees. A safety fixture
   that cannot be verified is the worst failure mode this harness exists to prevent.
4. **Deterministic by default.** A fixture with no temperature runs at 0.0.

The schema is a strict superset of the design doc: the existing fixture file loads
unchanged, and `assertions` is purely additive. An unknown assertion type is a
schema error (exit 2), never a silent pass, and a suite is validated **before** any
model call so a typo costs no tokens.

### Act II -- Fleet dispatch

| Wave | Agents (parallel) | Outcome |
|------|-------------------|---------|
| **1** | orchestrator, workspace `eval` | Schema and loader, assertion evaluator, runner and scoring, `Program.cs` wiring |
| **1** | orchestrator, workspace `note` | Trailer audit log (PR 24), runs through CI while E10 is built |
| **1** | orchestrator, workspace `rebrand` | Rebrand review: evidence gathering and the FR/ADR (separate PR, see S04SP5) |
| **2** | orchestrator, workspace `eval` | 88 unit tests, mutation check, fake-server end-to-end, docs, findings |

No sub-agents were dispatched. What the workspaces gave was concurrency in the
background: a name-collision sweep across 24 candidates ran while E10's tests were
written, and the full gate (format, build, 1,808 tests, lint) ran while the docs
were drafted.

Four things went sideways, and all four are the useful part:

- **A wrong assumption about the redactor.** A test asserted that a bare
  `sk-...` key in an exception message is redacted before it reaches the report. It
  failed. `SecretRedactor` covers Bearer tokens, `api-key` headers, env-var
  assignments, URL credentials and secret-named JSON and key-value fields, but not
  a bare key in free text. That is a real gap, now logged (open, Newman). The test
  was changed to assert what the component actually promises.
- **Unit tests cannot see the real pipeline.** The command's model call is behind a
  test seam, so no unit test ever ran the real client. A first real run, pointed at
  a local fake server, instead sent every request to the real
  `example.openai.azure.com` and got HTTP 401. The cause: an environment variable
  name. A code comment named `AZ_AI_FOUNDRY_MODELS`; the implementation reads
  `AZURE_FOUNDRY_MODELS`. One of those two wrong comments was one I had copied into
  the queue code in an earlier PR. Both are fixed. The same run also showed the
  design working under a real outage: every call errored, and the suite failed with
  exit 1 instead of reporting a green 0 of 0.
- **A format failure caught by the gate.** The full preflight reported
  `fmt_exit=2`: two whitespace lines in my own test file. Fixed and re-verified.
- **The same shell mistake twice.** A `pkill -f` whose pattern appeared in my own
  command line killed the shell, again. That is Lesson 6 of the previous episode,
  repeated. The server was killed by port after that.

### Act III -- Proof that the tests can fail

All 88 unit tests passed on the first full run, which is when to distrust them.
Three deliberate mutations of the runner, each aimed at one policy, turned four
tests red and were reverted:

| Mutation | What went red |
|----------|---------------|
| An all-ungraded suite is allowed to pass | `Run_NothingGraded_Fails` |
| An unverifiable safety fixture is merely ungraded | `Run_SafetyFixtureWithoutAssertions_FailsClosed` |
| Errors are dropped from the graded count | `Run_ModelFailure_IsAnErrorNotASkip`, `Run_MissingPersona_IsAnErrorAndCountsAgainstTheScore` |

The strongest evidence is the end-to-end test: a fake OpenAI-compatible server on
loopback that **records what it receives**. The integration suite checks that
three requests arrived, that `temperature` was `0` on the wire, that the roles
were system then user, and that the system prompt carried the safety clause. No
unit test can show that, because it is the wiring that is in doubt.

**Gates.** `dotnet format` clean, build 0 warnings, unit 1,808 of 1,808,
integration 137 of 137 (2 skipped, need live Azure credentials),
`color-contract-lint` clean, `ascii-check` clean, `markdownlint` clean across 412
files in a residue-free workspace.

## What shipped

**Production code**

- `Eval/EvalSuite.cs`: the shipping schema (a superset of the design doc), a loader
  that accepts a bare array or an object, tolerates comments and trailing commas,
  caps the file at 1 MiB, and reports every problem in one pass (listing at most
  ten).
- `Eval/EvalAssertions.cs`: eight deterministic assertion types. A regex that times
  out is a failed assertion, never a pass or a hang. Failure messages describe the
  expectation and never echo the model's output, which may be long, may carry
  control characters, and is adversarial in exactly the safety fixtures.
- `Eval/EvalRunner.cs`: the runner, the scoring policy and a plain ASCII report.
  Builds the system prompt exactly as the CLI does.
- `Program.cs`: `az-ai eval <suite.json>` (routed only when `eval` is the first
  positional and the next token ends in `.json`, so a prompt that starts with the
  word "eval" is still a prompt), `--fail-under`, `--filter`, help text,
  completions for three shells, `--model` and `--prefer` honoured. Exit 0 pass, 1
  fail, 2 unusable suite.

**Tests**

- `EvalHarnessTests.cs`: 88 cases across assertions, the loader, scoring, safety,
  filtering, rendering, argument parsing and the command, including a test that
  loads the shipped example suite and one that loads the existing design-doc
  fixtures.
- `tests/integration_tests.sh`: 10 cases, including the fake-server end-to-end run.

**Docs**

- `docs/prompts/eval-harness.md`: status, a "Running a suite" section, the
  assertion table and the scoring policy, and an honest status table (judge, golden
  recording and the persona-version pin are still open).
- `examples/eval/smoke.json`, README section with output copied from a real run,
  `CHANGELOG.md`, and 5 findings (1 resolved, 4 open).

**Not shipped**

- **The trait judge.** Still the deferred hard problem (open, Maestro).
- **Golden recording** (`--save-output`), so before/after goldens are still attached
  to PRs by hand (open, Maestro).
- **`persona_version` enforcement.** Fixtures record it; personas carry no version,
  so nothing can check it (open, Maestro and Kramer).
- **Bare-key redaction** in `SecretRedactor` (open, Newman).
- **Corpora.** Shipping real suites is E11 *The Corpus*; this episode ships the
  engine and one small example.

## Lessons from this episode

1. **Decide what the scoreboard must refuse before writing it.** Each of the four
   policies maps to a specific way a score lies. "0 of 0" green is the classic.
2. **Say what you do not do.** The trait judge is not implemented and the report
   says "ungraded" instead of inventing one.
3. **A seam that makes tests easy also hides the wiring.** The override that made
   88 fast tests possible meant the real client path had never run until the
   fake-server test existed.
4. **A wrong comment is a wrong instruction.** `AZ_AI_FOUNDRY_MODELS` sat in the
   code, was copied into more code, and was wrong the whole time. Check the
   implementation, not the comment next to it.
5. **Test what a component promises, not what you hope.** The redactor assumption
   failed on the first run, which is how it became a finding and not a leak.
6. **Break it on purpose.** Three mutations, four red tests. Passing the first time
   is not evidence.
7. **Tools do not stop you repeating yourself.** The `pkill -f` mistake was written
   up one episode ago and made again. A lesson in a report is not a guardrail; kill
   by port or pid.

## Metrics

- **Diff:** 12 files changed, 1,721 insertions, 12 deletions (plus this report and
  the index row).
- **Test delta:** +88 unit (1,720 to 1,808), +10 integration (127 to 137).
- **Preflight:** format, build (0 warnings), unit, integration, color-contract,
  ascii-check and markdownlint all green; `exec-report-check` clean.
- **CI status at push time:** pending at commit time; link in the PR.

## Credits

The Maestro led, and the refusal to fake a judge is his: the design doc already
said the trait judge is the hard problem, and the runner honours that. Costanza's
question was the one that shaped the exit codes: what does CI need to know? Uncle
Leo's lens produced the example suite, the one-pass error listing and the unknown
type message that names every valid type, because a first-time fixture author is
the reader that matters. Cast-balance ledger after this episode: Maestro holds 1
lead of record in S04 (E10), the E06 slate holds, and E11 *The Corpus* (Newman
lead; Puddy, Maestro and Rabbi guest) is next. Commits carry
`Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>`.
