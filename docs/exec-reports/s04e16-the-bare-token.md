# S04E16 -- *The Bare Token*

> *The redactor knew a secret by where it sat. A provider error does not care where it sits.*

**Commit:** `(this commit)` -- one code commit plus the episode report
**Branch:** `fix/s04e16-the-bare-token` (PR, off `main`)
**Runtime:** ~2 hours wall-clock, in a separate jj workspace
**Director:** Larry David (showrunner seat, filled by a single orchestrator)
**Lead:** Frank Costanza (SRE; error paths and what reaches a log)
**Cast:** 0 sub-agents, inline passes by one orchestrator. Frank (LEAD), Newman (co-lead: filed `N-3` and `N-7`), Puddy (the negative list), Babu Bhatt (guest: does a "token" look the same in every language)

## The pitch

Two findings from S04E10 and S04E13 described one gap from two sides.

- `newman-2026-11-N-3`: `SecretRedactor` recognises a secret by *position* (a Bearer header, an
  `api-key:` header, an env assignment, URL credentials, a secret-named JSON field). A bare
  `sk-...` in a sentence matches none of them, so a provider error that echoes the key passes through.
- `newman-2026-11-N-7`: `EvalLint` carries its *own* copy of the bare-token regexes (`sk-`, AWS, GitHub,
  Slack, JWT, private-key header), because the redactor lacked them.

Two definitions of "what a secret looks like" in one codebase is how they drift apart.

## Scene-by-scene

### Act I -- Check that the redactor is on the path

Better patterns help only where the redactor runs. `ErrorAndExit`, the central error helper, calls
`SecretRedactor.Redact` before anything reaches stderr, in text and JSON mode, and the catch-all
at the top of `Program` does too. So the user-facing error path is covered, and extending the
patterns extends the protection. (Seven warnings elsewhere print `ex.Message` directly; see the
findings.)

### Act II -- One list, two readers

`SecretRedactor.BareTokenRules` is the single list: OpenAI-style keys (including `sk-proj-`), AWS
access key ids, GitHub tokens (classic and fine-grained `github_pat_`), Slack tokens, JWTs and PEM
private keys. Each rule has a mask label for the redactor and a sentence for the lint, and
`EvalLint` now builds its token checks from that list. A test runs every rule through both and
asserts they agree.

Three details that mattered:

- **Case-sensitive on purpose.** The prefixes are fixed-case; `IgnoreCase` would only redact a part
  number that happens to read `SK-...`. A test pins that.
- **A whole private key, not its header.** The pattern matches from the header to the footer, or to
  the end of the text when the footer was cut off (a truncated log line), so the key body is masked
  and not just the first line.
- **No double report.** `EvalLint` already asked the redactor "would you change this text?" and
  reported a generic finding if so. With bare tokens inside `Redact`, a leaked key would have been
  reported twice. The lint now asks a structured-only entry point, `RedactStructured`, and a test
  asserts exactly one finding per rule.

### Act III -- Two things the mutation run taught me

I mutated the code eight ways. Two results needed reading, not just counting.

- **A mutation that did not apply looked like a mutation that survived.** My search string for
  "drop the fine-grained GitHub form" was missing a `(?:`, the script's assertion failed, and the
  harness carried on and reported the *unmutated* code as passing. I only noticed because the
  traceback printed. Redone properly, it was caught.
- **One mutation survived, and it was my comment that was wrong.** "Run the bare rules before the
  structured ones" changed nothing, because the structured rules also match the mask text
  (`api_key=[REDACTED:openai-key]` still matches `api_key=`) and re-label it. I had written a code
  comment saying the order mattered. It does not, so the comment now says what is true, and a test
  pins the output regardless of order.

## What shipped

**Production code**

- `SecretRedactor.cs`: `BareTokenRules`, applied inside `Redact`; `RedactStructured` for callers
  that report bare tokens themselves.
- `Eval/EvalLint.cs`: reads the shared rules; its generic check uses `RedactStructured`.

**Tests**

- `BareTokenRedactionTests.cs`: 37 tests: the provider-error sentence, each shape with its own label,
  a whole and a truncated private key, several tokens at once, 13 ordinary strings that must be left
  alone (hyphenated words, short prefixes, an already-masked key, upper- and lower-case lookalikes),
  idempotence, composition with the structured rules, both `ErrorAndExit` modes, and the
  redactor-and-lint agreement for every rule. **Mutation-checked:** 8 mutations, of which 7 were caught and
  1 was equivalent (order), plus the harness slip above.
- No existing test changed. Unit 2,146 to 2,183.

**Docs**

- `SECURITY.md`: what the redactor recognises now, and its honest limit.

**Not shipped**

- A key in a format the list does not know. The list is deliberately narrow: a broad entropy rule
  would redact git hashes and base64 payloads in ordinary errors.

## Lessons from this episode

1. **Two copies of a definition are a bug waiting for a date.** Make one the source and have the
   other read it, then test that they agree.
2. **Better patterns only help where the code runs.** Trace the path before extending the list.
3. **A mutation that did not apply looks exactly like a mutation that survived.** Make the harness
   stop when the edit does not take.
4. **When a mutation survives, ask whether the test is weak or the claim is wrong.** Here the claim
   was a comment, and it was mine.
5. **Reuse changes who else reports.** Moving a rule into a shared function can make a second
   caller report the same finding twice.
6. **Pin what must not match** at least as hard as what must.

## Metrics

- **Diff:** 2 production files, 1 new test file, `SECURITY.md`, the report, the index row,
  CHANGELOG and the findings backlog.
- **Test delta:** unit 2,146 to 2,183 (+37). Integration unchanged (no CLI surface changed).
- **Preflight:** format, 0 warnings, unit, `ascii-check`, `markdownlint`, `exec-report-check`.
- **CI status at push time:** pending at commit time; link in the PR.

## Credits

Frank led because the question was "what reaches a log". Newman filed both findings and wanted
them closed as one. Puddy's lens is the 13-line negative list, which is what lets the redactor stay
narrow without being timid. Babu's question, whether `token` and `key` read the same in other
languages, is why the rules key on fixed prefixes and not on English words. Commits carry
`Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>`.
