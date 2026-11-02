# S04E13 -- *The Redaction*

> *A lint that refuses to commit what it finds, and refuses to print it either. FDR broke it once before it shipped.*

**Commit:** `(this commit)` -- one code commit plus the episode report
**Branch:** `feat/s04e13-the-redaction` (PR, off `main`)
**Runtime:** ~3 hours wall-clock
**Director:** Larry David (showrunner seat, filled by a single orchestrator)
**Lead:** FDR (adversarial red team)
**Cast:** 0 sub-agents, 3 inline passes by one orchestrator across 1 jj workspace. FDR (LEAD of record), Newman (co-lead, policy), Jackie Chiles (guest, legal lens, not counsel)

## The pitch

The blueprint line for this episode is "Eval corpora must not leak user prompts into the
binary or telemetry; redaction policy plus pre-commit check." Two things about the work
that came before made it urgent. The three corpora from E11 are compiled into a
distributed binary, and the baselines from E12 are meant to be committed to a public
repository. Anything real in either (a pasted prompt, a key, an address, a home
directory) is published to everyone. And the only guard so far was a handful of tests
over the three shipped files: **it did not protect a contributor's future corpus, or a
baseline, or a suite someone wrote from a real session.**

FDR leads because the right way to build this is as an attack. A lint that has never
been attacked is a lint whose bypasses are unknown.

## Scene-by-scene

### Act I -- The policy, and the rules that make it hard to defeat

`az-ai --lint-suite <file>` (repeatable) checks an eval file for secrets, real emails and
hosts, non-reserved IP addresses, home-directory paths, PII shapes, high-entropy tokens
and non-ASCII characters. It is built into the binary, so a user can lint their own suite
before committing it, not only the repository's CI. Each design rule maps to a way a leak
or a bypass gets through:

1. **Context-aware.** A regex assertion's `value` is a pattern, so it may look like a
   secret: the refusal corpus detects `sk-...` in model output and would be unshippable
   if that were flagged. A literal assertion (`contains`, `starts_with`) is text and is
   scanned like a prompt. The exemption is by assertion type, not by how a string looks.
2. **Decode before scanning.** A JSON `\u0073` is an `s`.
3. **Scan everywhere text can hide.** String values, property **names** (a baseline keeps
   fixture ids as names), ids, traits, the safety assertion, and comments, which the
   loader ignores and the repository publishes.
4. **Never print what was found.** A finding is a rule, a location and a masked preview
   (two characters and a length). A lint log that printed the secret would be a second
   leak, into CI logs.
5. **An unparseable file is a finding.** It still gets a raw-text scan; it is never
   silently skipped.
6. **No opt-out.** The pre-push hook gets no skip trailer for this gate. A check that
   guards against publishing secrets should not have a waiver; `--no-verify` remains the
   only bypass, and CI still enforces it.

Newman's contribution is that the check is wired where the repo's other gates already
are: `make preflight`, CI (through the integration suite, which every pull request runs),
and the pre-push hook when an eval file is in the push range. Jackie's lens is the
`provenance` rule: a corpus that ships in a binary must declare `Origin: synthetic` in
the file itself, so the claim travels with the artifact.

### Act II -- Fleet dispatch

| Wave | Agents (parallel) | Outcome |
|------|-------------------|---------|
| **1** | orchestrator, workspace `redact` | The linter, CLI wiring, 69 unit tests |
| **2** | orchestrator, workspace `redact` | Six mutations, the script, Makefile, hook and CI wiring, 6 integration cases |
| **3** | orchestrator, workspace `redact` | Docs, findings, full gate |

No sub-agents were dispatched.

**FDR broke it before it shipped.** The first version scanned the raw file text for
non-ASCII characters. FDR's test serialized a prompt containing a Cyrillic look-alike
letter with a default JSON encoder, which writes it as `\u0430`: the **file is pure
ASCII and the decoded string is not.** The raw-text rule never saw it. Four tests failed,
correctly. The fix runs the character rule on every decoded string and property name as
well. That is the bypass the episode exists to find, and it was found by a test written
to find it, not by a user.

**The first real run found a real violation.** Linting the repository's existing eval
files, the lint flagged `docs/prompts/fixtures/coder.json`, a design-doc fixture from
before this work, for a non-ASCII arrow in a trait description. A true positive against
the rule this episode now enforces. It was fixed (`->`), and the matching snippet in the
design doc was kept consistent.

**Proof the guards bite.** All 69 tests passed after the fix, so six deliberate
mutations were applied, each removing one guard, with everything restored afterwards:

| Mutation | What went red |
|----------|---------------|
| The decoded-string character rule removed (the bypass just fixed) | all four non-ASCII bypass cases |
| Every assertion value treated as a pattern | all three literal-assertion cases |
| Property **names** not scanned | the property-name bypass |
| The mask printing the full match | every secret in the output-hygiene test |
| Comments not scanned | the comment and block-comment bypasses |
| An unparseable file skipped silently | the unparseable-file test |

**A control for the telemetry check.** The slate also asks that an eval run not leak
prompts into telemetry. A test that asserts "no prompt text appears" passes trivially if
the harness cannot see telemetry at all, or if nothing is ever emitted. So it has a
control: a normal prompt run with telemetry on is shown to emit events (a resolver
decision, a fallback hop, a dispatch record), and even those carry no prompt text. The
eval run with the same settings emitted **no telemetry events at all** when checked by hand,
and the integration test asserts the invariant that matters: no prompt, output or key text on
stdout or stderr. It deliberately does not assert "no events", so a future telemetry addition
to eval is not blocked as long as it stays clean.

### Act III -- What is and is not established

**Established:** the lint's rules and context handling (69 unit tests, each guard
mutation-checked); that every eval file in this repository, and the corpora compiled
into the binary, pass the policy; the script, the hook and the Makefile wiring against
the real binary; that a leaky file exits 1 and its output contains neither the key nor the
address; that a JSON escape does not hide a look-alike; that a missing binary exits 2
instead of passing; and that an eval run emits no prompt, output or key text with
telemetry on.

**Not established, stated as plainly:**

- **The lint finds shapes, not meaning.** A name or address written in prose, or a
  sentence that happens to describe something private, is not caught. Human review of
  any new corpus is still required, and the docs say so. Logged for Rabbi.
- **The detectors are duplicated.** The lint carries bare-token shapes (`sk-`, `AKIA`,
  `ghp_`, `xox`, JWT, private-key header) that `SecretRedactor` does not. They should move
  into the redactor, which would also close the E10 finding about runtime error messages.
  Logged for Newman.
- **Eval spend is invisible to cost telemetry** precisely because eval emits none. Safe
  for privacy, a gap for FinOps. Logged for Morty.
- **The pre-push hook** step was syntax- and shellcheck-checked but not exercised by a
  real push, which needs a configured upstream and a built binary.

**Gates.** `dotnet format` clean (first try), build 0 warnings, unit 2,006 of 2,006,
integration 155 of 155 (2 skipped, need live Azure credentials), `color-contract-lint`
clean, `ascii-check` clean, `markdownlint` clean across 422 files in a residue-free
workspace, `shellcheck` clean on the new script and the edited hook.

## What shipped

**Production code**

- `Eval/EvalLint.cs` (new). The policy: context-aware string classification, decoded
  scanning, property-name and comment scanning, masked output, fail-closed handling of
  unparseable files and rule timeouts.
- `Program.cs`: `--lint-suite <file>` (repeatable), `--require-provenance`, exit 0 clean
  and 1 findings, help text and completions for three shells. It needs no credentials,
  model or network.
- `scripts/eval-lint.sh`, a `make eval-lint` target added to `make preflight`, and a
  pre-push gate with no opt-out trailer.
- `docs/prompts/fixtures/coder.json`: a non-ASCII arrow replaced, found by the lint's
  first real run.

**Tests**

- `EvalLintTests.cs`: 69 cases. Every credential shape in a prompt; each rule with
  reserved-value negatives; high-entropy versus long plain words; detector-in-regex
  allowed versus the same shape in a literal assertion flagged; the bypass attempts
  (unicode escapes, comments, block comments, ids, property names, traits, safety text,
  homoglyphs, zero-width and bidi characters, unparseable files, pathological input);
  output hygiene (no 8-character window of any secret in text or JSON output); provenance;
  missing files; every eval file in the repository and every embedded corpus; and the
  command's exit codes.
- `tests/integration_tests.sh`: 6 cases against the real binary, including the telemetry
  control.

**Docs**

- `docs/prompts/eval-harness.md` (the redaction policy and rule table), `README.md`,
  `eval/baselines/README.md`, `CONTRIBUTING.md`, `.github/skills/preflight.md` (the gate
  chains now list the new step), `CHANGELOG.md`, and 5 findings (2 resolved, 3 open).

**Not shipped**

- **Moving the detectors into `SecretRedactor`** (open, Newman).
- **Meaning-level review**, which no lint can do (open, Rabbi).
- **Eval cost telemetry** (open, Morty).
- **A lint for user-written prose files** beyond eval JSON. The policy covers eval files
  only.

## Lessons from this episode

1. **A guard that has never been attacked has unknown holes.** The first version had one,
   and the attack that found it took one test.
2. **Check decoded content, not just bytes.** An escape sequence is ASCII on disk and
   anything at all in memory.
3. **A lint's output is part of the attack surface.** A tool that prints what it found
   leaks it a second time. Mask, and test the mask with a sliding window.
4. **Exempt by role, not by appearance.** A detector pattern and a secret can look the
   same; where the string sits is what tells them apart.
5. **An unreadable input is a finding.** A typo in a path or a file that will not parse
   must never become a green "0 files checked".
6. **A negative test needs a control.** "No prompt text in telemetry" proves nothing
   until a normal run shows telemetry is visible.
7. **Run the new rule on the old files.** The first real run found a violation that had
   been in the repository all along.
8. **Do not let a secrets guard have a waiver.** The docs gates have opt-out trailers for
   good reasons; this one deliberately does not.

## Metrics

- **Diff:** 17 files changed, 1,077 insertions, 13 deletions (plus this report and the
  index row).
- **Test delta:** +69 unit (1,937 to 2,006), +6 integration (149 to 155).
- **Preflight:** format, build (0 warnings), unit, integration, eval-lint,
  color-contract, ascii-check, markdownlint, shellcheck green; `exec-report-check` clean.
- **CI status at push time:** pending at commit time; link in the PR. The new lint runs in
  CI through the integration suite.

## Credits

FDR led, and the escape-sequence attack is his. Newman's policy is the wiring: the lint
runs where every other gate already runs and has no waiver. Jackie's provenance rule is
why the claim "synthetic" is written into each shipped corpus. Cast-balance ledger after
this episode: FDR holds his first lead of record in S04, Newman and Jackie each have another
appearance, and the E06 slate is complete through E13. The remaining S04 arc (E14 onward,
caching) is outside that slate. Commits carry
`Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>`.
