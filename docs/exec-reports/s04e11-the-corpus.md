# S04E11 -- *The Corpus*

> *Three suites ship in the binary, each proven satisfiable and violable, none proven against a real model.*

**Commit:** `(this commit)` -- one code commit plus the episode report
**Branch:** `feat/s04e11-the-corpus` (PR, off `main`)
**Runtime:** ~3 hours wall-clock, with a 435-name search running in a parallel workspace
**Director:** Larry David (showrunner seat, filled by a single orchestrator)
**Cast:** 0 sub-agents, 3 inline passes by one orchestrator across 2 jj workspaces. Newman (LEAD, security), David Puddy (W2 lead, regression), The Maestro (guest, prompts), Rabbi Kirschbaum (guest, responsible use)

## The pitch

S04E10 shipped the eval engine and one small example. E11 ships the content that
makes the engine useful on day one: **three corpora compiled into the binary**,
run with `az-ai eval --corpus <name>` and listed with `--list-corpora`. A Homebrew
install has no repo and no fixtures directory, so a file path is the wrong
interface for something every user should be able to run.

- `smoke` (7 fixtures): does this deployment work at all?
- `persona-voice` (10): does each built-in persona keep the promises its own
  system prompt makes?
- `refusal` (8): does the safety clause fire on adversarial prompts?

The slate puts Newman in the lead, and the reason is the framing. A corpus that
is compiled into a distributed binary is a **published artifact**. Anything real
in it, such as a pasted prompt, an address or a key, ships to everyone. So most of
the episode's rules are constraints on what a corpus may contain, enforced by tests
and not by good intentions.

## Scene-by-scene

### Act I -- A corpus is a published artifact

Newman's rules, each earned by a specific way a shipped file goes wrong:

1. **Bounded.** 16 KiB per corpus and 48 KiB in total, enforced by a test. The
   corpora total about 13 KiB today; the cap stops them quietly bloating the AOT
   image.
2. **Synthetic only.** No user prompts, no real credentials, hosts or addresses.
   Hosts use the reserved `.invalid` TLD and addresses only TEST-NET-3
   (`203.0.113.0/24`), which cannot route to anyone. A test scans every prompt
   for emails, IPs and home-directory paths, and a second test requires that the
   project's own `SecretRedactor` leaves each prompt unchanged.
3. **ASCII only.** Homoglyphs, bidi controls and zero-width characters are how a
   reviewed file differs from what runs. A test checks every byte.
4. **Declared provenance.** Each file opens with an `Origin: synthetic` header, so
   the claim travels with the file.
5. **Same validation as a user suite.** Built-ins load through the shared
   `EvalSuiteLoader.TryParse`, so a corpus cannot pass rules a user's file would
   fail.

The Maestro's contribution is `persona-voice`: every assertion comes from a promise
in that persona's own prompt (the reviewer "cites line numbers", security
"classifies findings by severity" and gives remediation, the architect documents
"trade-offs and alternatives"). Voice is checked structurally; freeform quality is
still not scored, because the trait judge is still not built.

Rabbi Kirschbaum's lens on `refusal`: the prompts are generic and widely known
attack shapes with no actionable harmful detail, and the assertion measures that a
refusal *marker* is present and nothing secret-shaped is, not whether the refusal
was good or kind. That limit is stated in the corpus header and the docs.

### Act II -- Fleet dispatch

| Wave | Agents (parallel) | Outcome |
|------|-------------------|---------|
| **1** | orchestrator, workspace `corpus` | Three corpora, `Corpora.cs`, `--corpus` and `--list-corpora`, persona fallback with source reporting |
| **1** | orchestrator, workspace `name` | A 435-candidate name search ran in the background through all of this (see S04SP6) |
| **2** | orchestrator, workspace `corpus` | 83 unit tests, four mutations, 6 integration cases, docs, full gate in the background |

No sub-agents were dispatched. The concurrency was real: a multi-minute network
sweep and the full format, build, test and lint gate both ran in the background
while code and docs were written.

The center of the episode is a test design, and it is Puddy's. The corpora are
"deterministic" only in how they are *scored*; a model's output is not bit-stable
even at temperature 0, and no live credentials were used. So the strongest
deterministic check available is: **every fixture has a sample good answer that
must pass all its assertions and a sample bad answer that must fail at least one.**
That proves each fixture is *satisfiable* (it is not one nothing can pass) and
*discriminating* (it is not one everything passes). A fixture added without both
exemplars fails the build.

**Proof the guards bite.** All 83 tests passed on the first run, so four deliberate
mutations of the corpus files were applied, each aimed at one guard, and the files
were restored byte-identical afterwards:

| Mutation | What went red |
|----------|---------------|
| An assertion no good answer can satisfy (arithmetic expects 52) | `GoodExemplar_PassesEveryAssertion`, the whole-corpus pass test, the command test |
| An assertion everything passes (capital only checks the word "France") | `BadExemplar_FailsAtLeastOneAssertion`, the whole-corpus failure test |
| A fixture added with no exemplars | `Exemplars_CoverEveryFixtureExactly` and four more |
| A real-looking email in a prompt | `Corpora_UseOnlyReservedHostsAndAddresses` |

### Act III -- What is and is not established

End to end, the real binary lists the corpora with no credentials, rejects an
unknown name with the valid ones, runs the embedded `smoke` corpus against a
loopback fake model server (seven requests arrive and it passes), passes `refusal`
against a model that refuses, and **fails** `persona-voice` against a model with no
persona voice while reporting `Personas: built-in defaults`.

What it does **not** establish, stated as plainly as the passes:

- **No real model has been run against these corpora.** The exemplars prove the
  assertions can be met and violated, nothing about any provider. Real pass rates
  and flake rates are unknown and are logged for S04E12, which must pin a model and
  temperature and measure.
- **Refusal markers are English.** A correct refusal in another language fails the
  marker check. Logged for Babu.
- **The embedding was exercised in a normal build, not a Native AOT publish.** It
  mirrors the registry pattern that ships AOT. Logged for Jerry.

**Gates.** `dotnet format` clean, build 0 warnings, unit 1,891 of 1,891, integration
143 of 143 (2 skipped, need live Azure credentials), `color-contract-lint` clean,
`ascii-check` clean, `markdownlint` clean across 417 files in a residue-free
workspace.

## What shipped

**Production code**

- `Eval/Corpora/{smoke,persona-voice,refusal}.json`: 25 fixtures, about 13 KiB,
  embedded resources.
- `Eval/Corpora.cs`: the registry, the size caps, and loading through the same
  validation as a user suite.
- `Eval/EvalSuite.cs`: the loader's parsing split out as `TryParse`, shared by file
  and embedded suites.
- `Eval/EvalRunner.cs`, `Eval/CorpusInfo.cs`: the report gains `persona_source`;
  `--list-corpora --json` has a source-generated DTO.
- `Program.cs`: `--corpus` (validated, normalized, mutually exclusive with a file),
  `--list-corpora`, persona resolution that prefers `./.squad.json` and falls back to
  the built-in defaults, help text, completions for three shells.

**Tests**

- `CorpusTests.cs`: 83 cases. Load and validate, size budget, ASCII, provenance,
  reserved hosts and addresses, no secret shapes, graded and uniquely prefixed ids,
  refusal fixtures all safety with two required checks, persona coverage, the
  exemplar table (coverage, good passes, bad fails), whole-corpus runs, flag
  parsing, the commands, and persona-source reporting.
- `tests/integration_tests.sh`: 6 cases against the real binary.

**Docs**

- `README.md`, `docs/prompts/eval-harness.md` (the rules every corpus must meet and
  how to add a fixture), `CHANGELOG.md`, and 3 findings (all open).

**Not shipped**

- **Real-model pass rates** (open, Maestro and Jerry): S04E12.
- **Non-English refusal markers** (open, Babu).
- **An AOT-publish check** (open, Jerry).
- **The redaction pre-commit check** that stops a user prompt entering a corpus:
  S04E13 *The Redaction*. Today the tests guard the three shipped files; they do
  not guard a contributor's future corpus from a different source.

## Lessons from this episode

1. **A shipped fixture file is a published artifact.** Size, provenance and
   "nothing real in it" are constraints, and constraints belong in tests.
2. **Prove a check is satisfiable and violable.** Two sample answers per fixture
   catch both the assertion nothing can pass and the one everything passes.
3. **Say what the deterministic check cannot show.** Exemplars say nothing about
   any real model. The report, the docs and the findings all say so.
4. **Make the built-in obey the user's rules.** Sharing one parse-and-validate path
   means the corpus cannot be quietly exempt.
5. **Report where a result came from.** `Personas: built-in defaults` versus
   `./.squad.json` is the difference between a hermetic CI run and a project's own.
6. **Mutate every guard once.** Four mutations, each aimed at a different guard,
   each caught by the right test.
7. **Parallel work only helps if the slow things are in the background.** A 13-minute
   network sweep and a full gate cost nothing here because nothing waited on them.

## Metrics

- **Diff:** 16 files changed, 1,118 insertions, 15 deletions (plus this report and
  the index row).
- **Test delta:** +83 unit (1,808 to 1,891), +6 integration (137 to 143).
- **Preflight:** format, build (0 warnings), unit, integration, color-contract,
  ascii-check and markdownlint all green; `exec-report-check` clean.
- **CI status at push time:** pending at commit time; link in the PR.

## Credits

Newman led, and the corpus-as-published-artifact framing is his. Puddy's exemplar
table is the reason the corpora are more than plausible-looking JSON. The Maestro
tied every voice assertion to a line in the persona's own prompt. Rabbi's lens kept
the refusal corpus to generic, non-actionable prompts and kept its claims modest.
Cast-balance ledger after this episode: Newman holds a lead of record (Rule 2: not
zero past the E12 line), Puddy a second-wave lead, and the E06 slate holds with E12
*The Gate* (Jerry lead; Soup Nazi and Frank guest) next. Commits carry
`Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>`.
