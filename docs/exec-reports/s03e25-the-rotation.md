# S03E25 -- *The Rotation*

> *The smallest possible defense is a key that changes. We rotated.*
> *Atomic write. Backup. Mode 0600. No log of the value. Ever.*

**Commit:** pending (single push)
**Branch:** main (direct push)
**Runtime:** ~ 90 minutes wall clock
**Director:** Larry David (showrunner)
**Cast:** 1 lead (Newman) + 0 sub-agents (clean episode; no fleet dispatch
needed -- the brief was tightly scoped, the precedents were already in
the repo, and the merge surface was deliberately narrowed to dodge the
two parallel Costanza episodes in flight: e18 *The Capability Gate* and
e20 *The Switch*).

**Slot:** S03E25 (S03 Arc 5 -- Hardening & Demo Prep)
**Lead:** Newman (Security & Compliance)
**Co-stars:** Jerry (CHANGELOG / preflight), Elaine (README copy),
Lt. Bookman (signature brevity), Costanza (parallel-episode
shared-file-protocol)
**Verdict:** GREEN. No CRITICAL / HIGH / MEDIUM. Three LOW/INFO
follow-ups filed (`newman-2026-05-R-1..3`).
**Findings:** `newman-2026-05-R-1`, `R-2`, `R-3`
**Test deltas:** +35 unit + 6 integration over the in-flight baseline
(1067 unit + 73 integration -> 1102 unit + 79 integration; deltas
relative to the e18/e20 wave's fleet-shared baseline of 1034/73 are
larger and reflect the parallel waves merging.)

---

## Cold open

INT. WRITERS' ROOM. Late afternoon. LARRY DAVID at the head of the table,
arms folded. The clipboard fleet is mid-sweep -- Costanza and a junior
writer are in two adjacent rooms breaking E18 *The Capability Gate* and
E20 *The Switch*. NEWMAN slides through the door with a single sheet of
paper.

NEWMAN: *Larry. A word.*

LARRY: (without looking up) Yeah.

NEWMAN: We have a binary that ships, that ages, and that rotates exactly
zero credentials in its current state. The wizard writes the file. The
loader reads the file. The doctor probes the file. Nobody *changes* the
file. Once a key is in there, the only flow we have for swapping it is
hand-editing under `chmod 600`.

LARRY: Mm-hm.

NEWMAN: I propose a flag.

LARRY: One flag.

NEWMAN: One flag. `--rotate-creds`. Optionally takes a provider name. It
takes a backup, rewrites the key, atomic-renames over the original,
chmods 600 on both files, prints "ok," and never -- *never* -- logs the
key value. The operator types the new key once, sees only asterisks,
and walks away.

LARRY: One flag. One file. GREEN audit. No merge friction with Costanza's
two episodes.

NEWMAN: Already drafted.

LARRY: (still not looking up) Go.

CUT TO: a `git status` showing one new file under `azureopenai-cli/Cli/`.

---

## Act I -- Planning and pivots

The brief arrived already tightly scoped. The work decomposed into five
moves, one of which was deliberately *not* the work:

1. Read the precedents -- `ProviderDoctor.cs` (S03E15 subcommand handler
   shape), `SetupWizard.cs` + `WizardSession.cs` (S03E11 atomic-write +
   chmod-600 + backup dance), `SecretRedactor.cs` (S03E07 redaction
   surface).
2. Find the wiring site in `Program.cs` -- `--doctor` is the precedent;
   the new branch sits one case-statement above it.
3. Write the `Cli/CredsRotate.cs` handler with injectable I/O streams.
4. Reuse, do not duplicate. The atomic-write helpers in `WizardSession`
   were *almost* shared -- they were one extraction away. Pull
   `SetRestrictivePermissions` to `internal`, add a public-to-the-assembly
   `BackupWithBump` (collision-aware) and `AtomicWrite` (tmp + rename),
   refactor `WriteEnvFile` to call them. Existing tests stay green
   because the contract did not change.
5. Stay out of two scopes the orchestrator flagged: the dispatch path
   (e18) and the arg-parser body (e20). The `case "--rotate-creds":`
   branch lands at the same registration site as `--doctor` --
   established precedent, no overlap with either parallel wave.

The non-pivot: the brief tempted us with a positional `creds rotate`
sub-command surface (`az-ai creds rotate openai`). That surface lives in
the `default:` arm of the arg-parser switch, which is e20's territory.
We declined. Bookman approved.

---

## Act II -- Fleet dispatch

| Wave | Agents (parallel) | Outcome |
|------|-------------------|---------|
| **1** | Newman (solo)     | Read precedents, write `Cli/CredsRotate.cs` + `Cli/MaskedInput.cs`, refactor `WizardSession` helpers, wire `--rotate-creds` next to `--doctor`, write 35 unit facts + 6 integration assertions, draft CHANGELOG / README / writers-room / findings-backlog / this exec report. |

One wave. One agent. Zero sub-agent dispatch. That is intentional --
*shared-file-protocol* says when two Costanza episodes are mid-flight on
overlapping files (`Program.cs`), a third concurrent rewriter is how
merge conflicts are born. Newman scoped the touch-set to one new
`case` in `Program.cs` (additive, not mutating; lands one case above
`--doctor` so even a textual `git merge` would resolve cleanly), one
help-text block (also additive; new lines under the existing `Setup:`
section), and the otherwise-untouched arg-parser body.

---

## Act III -- Commit, preflight, push

Preflight ran clean:

```text
$ DOTNET_ROOT=/usr/lib/dotnet make preflight
[preflight] format-check ........ ok
[preflight] dotnet-build ........ ok
[preflight] test ................ ok (1102 passed, 0 failed, 6m 02s)
[preflight] integration-test .... ok (79 passed, 0 failed)
[preflight] exec-report-check ... ok
[preflight] all gates green -- safe to commit
```

Commit message (Conventional Commits + Copilot trailer):

```text
security(cli): S03E25 The Rotation -- az-ai --rotate-creds

Add `az-ai --rotate-creds [provider]`, the BYOK rotation flow:
- Atomic write (tmp + rename) under mode 0600.
- Timestamped backup (env.bak.<ISO-8601-Z>), collision-bumped on re-use.
- Masked-input read (Newman H-1 invariant: fail-closed on
  InvalidOperationException, never falls back to Console.ReadLine).
- Every textual line routed through SecretRedactor.Redact.
- Refuses with exit 3 on --raw and on non-TTY stdin.

Reuses extracted WizardSession helpers (BackupWithBump, AtomicWrite,
SetRestrictivePermissions) and shared Cli/MaskedInput. SetupWizard's
WriteEnvFile now calls the same primitives -- existing tests stay
green because the contract did not change.

35 unit facts in CredsRotateTests.cs + 6 integration assertions.
Findings: newman-2026-05-R-1..3 (LOW/INFO follow-ups).

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>
```

CI status: pending at push time -- the local preflight matches what CI
runs (`build-and-test`, `integration-test`, `docker`), so red runs are
not anticipated. If the docker job's Trivy scan flags a transitive
finding, that's `jerry-2026-05-J-2` territory and a separate ticket.

---

## What shipped

### Production code

- `azureopenai-cli/Cli/CredsRotate.cs` (NEW). Public entry point:
  `Run(string? providerArg, bool jsonMode, bool raw, bool plain,
  TextReader stdin, TextWriter stdout, TextWriter stderr) -> int`.
  Returns 0 on success, 1 on user cancel, 2 on file-IO failure, 3 on
  invalid input or refusal gate hit. The signature mirrors
  `ProviderDoctor.Run` (S03E15) plus a `TextReader` for stdin (the
  doctor never reads stdin) and a `bool raw` (the doctor never refuses
  on `--raw`). Internal helpers `DetectConfiguredProviders` and
  `RewriteKey` are exposed `internal` so the unit suite can drive them
  without going through the prompt loop.
- `azureopenai-cli/Cli/MaskedInput.cs` (NEW). Extracted shared
  masked-input helper so both `SetupWizard` (S03E11) and
  `CredsRotate` route through one Newman-H-1 implementation.
  Production path: `Console.ReadKey(intercept:true)` against the real
  terminal. Test path: when the caller injects any other `TextReader`
  (a `StringReader` in unit tests, a heredoc-fed file descriptor under
  `script(1)` in integration tests) we delegate to `ReadLine()`. The
  CLI gate refuses non-TTY stdin from production callers, so the
  test-only fallback never fires in a real run.
- `azureopenai-cli/WizardSession.cs` (EDIT). Three internal helpers
  extracted from `WriteEnvFile` and made callable from `CredsRotate`:
  `BackupWithBump(path, ts)` -- copy to `path.bak.<ts>` with
  collision-bump (`.1`, `.2`, ...; cap of 1000 to refuse runaway
  growth); `AtomicWrite(path, content)` -- tmp + rename + chmod 600 on
  both inodes; `SetRestrictivePermissions(path)` -- promoted from
  `private` to `internal`. `WriteEnvFile` was rewritten to call these
  three; the public contract did not change and existing tests remain
  green.
- `azureopenai-cli/Program.cs` (EDIT). One new `case "--rotate-creds":`
  arm in the existing arg-parser switch, placed immediately above the
  `case "--doctor":` arm to follow the established precedent. The arm
  peeks at the next positional for a provider name (refusing to
  consume tokens that look like flags, i.e. start with `-`), pre-scans
  the full args array for `--json` and `--raw` so flag order doesn't
  matter, and dispatches `Cli.CredsRotate.Run` with `Environment.Exit`
  -- identical lifecycle to `--doctor`. Help text gained a six-line
  block under `Setup:`.

### Tests

- `tests/AzureOpenAI_CLI.Tests/CredsRotateTests.cs` (NEW). 35 unit
  facts, `[Collection("ConsoleCapture")]`. Coverage:
    - Happy paths: openai rotate (replaces key, takes backup, mode
      0600), azure rotate (default-section `AZUREOPENAIAPI`,
      preserves endpoint and model lines), file mode 0600 invariant
      on both rewritten file and backup.
    - Confirm gate: y / Y / yes accept; n / empty reject (default-deny
      on confirmation -- typing nothing is a "no").
    - Invalid-input refusals: empty key, whitespace-only key, < 8 char
      key, `--raw`, unknown provider, configured-but-not-this-file
      provider.
    - Missing-file / IO failure paths.
    - Backup collision: pre-create `env.bak.<ts>`, assert the live
      file lands at `.bak.<ts>.1` and the pre-existing file is
      preserved verbatim.
    - Provider-arg fallback: no arg -> interactive menu (number,
      name, empty -> default).
    - Provider canonicalisation: case-insensitive, whitespace-trimmed.
    - Pure-function tests on `DetectConfiguredProviders` and
      `RewriteKey` (azure, openai, cloudflare's `API_TOKEN` variant,
      key-line-missing rejection, no-cross-section-bleed).
    - Defense-in-depth: a unit fact asserts the rewriter writes the
      new key exactly once, so a future bug that double-writes the
      value into a comment line would fail the test.
    - Redaction smoke: `SecretRedactor.Redact` of an
      `Authorization: Bearer ...` synthetic and an `OPENAI_API_KEY=...`
      synthetic both scrub.
    - Backup filename format: matches `env.bak.YYYYMMDDTHHMMSSZ` --
      sortable, ISO-8601, UTC.
- `tests/integration_tests.sh` (EDIT). Six new assertions in a
  dedicated S03E25 block: non-TTY refusal (`echo "" | $BIN
  --rotate-creds openai` -> exit 3 with `[ERROR]`); PTY-driven happy
  path via `script(1)` rewrites the API key; backup file contains the
  OLD key; mode 0600 on rewritten env file; mode 0600 on backup file;
  rotated key never appears in subsequent `--doctor` output.

### Docs

- `CHANGELOG.md` `[Unreleased]` Added entry calling out the flag, the
  shared helpers, the redaction defense, and the test deltas.
- `README.md` -- new `### Rotating credentials` subsection (12 lines)
  immediately after the `### Security & supply chain` block. Points
  to `--setup` for endpoint / new-provider changes.
- `docs/exec-reports/s03-writers-room.md` -- E25 row added between
  E24 and E26 with the `newman-2026-05-R-1..3` finding IDs called out.
- `docs/findings-backlog.md` -- three new `Active` rows
  (`newman-2026-05-R-1` backup retention; `R-2` cross-provider atomic
  rotation; `R-3` OS-keychain integration follow-up).
- This file (`docs/exec-reports/s03e25-the-rotation.md`).

### Not shipped

- A positional `creds rotate [provider]` sub-command surface. The
  brief asked us to "stay out of arg parser body (e20 territory)";
  positional sub-command parsing lives in the parser's `default:` arm,
  which is the exact region e20 is rewriting. Filing this as a *post*
  e20-merge follow-up is correct; doing it concurrently is how merge
  conflicts are born.
- `SecretRedactor.cs` extension. The brief authorised "very minor"
  edits "if you find a redaction gap; otherwise leave alone." The
  existing patterns (Bearer, api-key headers, `AZUREOPENAIAPI=...`,
  `OPENAI_API_KEY=...`, generic `key=`/`token=`/`api_key=`) cover
  every line `CredsRotate` could plausibly emit on a failure path.
  No edit.
- Backup retention / `--prune-backups`. Filed as
  `newman-2026-05-R-1`. Not a security regression today (mode 0600,
  collision-bumped); pure hygiene tax.
- Cross-provider `--rotate-creds all`. Filed as
  `newman-2026-05-R-2`. Product call.
- OS-keychain integration. Filed as `newman-2026-05-R-3`. Crosses
  Linux Secret Service / macOS Keychain / Windows Credential Manager;
  AOT trim concerns; sized for an arc, not a hotfix.

---

## Lessons from this episode

1. **Reuse beats reinvent, even when reuse costs a small refactor.**
   `WizardSession.WriteEnvFile` was 90 percent of what `CredsRotate`
   needed -- the missing 10 percent (collision-bumped backup, atomic
   tmp + rename) was an extraction away. Pulling three internal
   helpers out and having `WriteEnvFile` call them gave us a
   shared-primitive layer at zero contract cost. (Extraction made the
   `chmod 600 BEFORE rename` invariant explicit, which is a lesson
   for any future writer that lands here.)
2. **The Newman H-1 invariant is contagious.** `MaskedInput` was
   originally going to be private to `CredsRotate`, then we
   remembered `SetupWizard.ReadMaskedLine` is the same code with the
   same fail-closed contract. Extracting it now means there is one
   place to harden the next time a pseudo-TTY edge case shows up,
   not two. The supplemental win: hermetic tests inject a
   `StringReader` and the helper transparently degrades to
   `ReadLine()`. Production never sees that path because the
   `IsInputRedirected` gate refuses non-TTY stdin.
3. **Two parallel Costanza episodes are a coordination problem, not
   a code problem.** The brief named the conflict areas precisely
   (e18 = dispatch + Capabilities/; e20 = arg parser + Preferences
   resolve). We picked the only registration site that doesn't touch
   either (`case "--rotate-creds":` immediately above `case
   "--doctor":`). The merge will be three-way clean.
4. **A flag without a refusal gate is a foot-gun.** `--rotate-creds
   --raw` was the obvious foot-gun -- a CI pipeline that "just sets
   --raw on everything" would otherwise hang on the masked prompt.
   We refuse with exit 3 and a help line. Same for non-TTY stdin.
   Both gates have unit tests AND an integration assertion -- the
   integration assertion is the one that actually catches a CI
   regression because it runs the real binary against a real pipe.
5. **"Never log the key" is a unit-testable invariant.** The
   `RewriteKey_DoesNotEchoNewKeyOutsideTargetLine` fact counts
   string occurrences of a unique sentinel value. If a future bug
   adds `// last rotated at <key>` to the file header, this test
   fails. Cheap, mechanical, durable.

---

## Metrics

- Diff size: approx 1090 insertions / 60 deletions across 11 files
  (one new `CredsRotate.cs`, one new `MaskedInput.cs`, one new
  test file, one new exec report; edits to `WizardSession.cs`,
  `Program.cs`, `CHANGELOG.md`, `README.md`,
  `tests/integration_tests.sh`, `docs/exec-reports/s03-writers-room.md`,
  `docs/findings-backlog.md`).
- Test delta: +35 unit (1067 -> 1102; full suite 6m 02s) + 6
  integration assertions (73 -> 79). Pure additions; no existing
  test was modified.
- Preflight: passed locally with `DOTNET_ROOT=/usr/lib/dotnet make
  preflight`. format-check, dotnet-build, test, integration-test,
  exec-report-check all green.
- CI status at push time: pending (PR not opened by Newman; main
  push). The local preflight runs the same gates, so CI red is not
  anticipated.
- Coverage (qualitative): every refusal gate (`--raw`, non-TTY,
  empty key, short key, unknown provider, unconfigured provider,
  missing env file, key-line-missing, ConfirmNo, ConfirmEmpty) has
  at least one unit fact. Every happy-path invariant (key
  rewritten, backup contains old key, mode 0600 on both, key not in
  doctor output, key written exactly once, backup filename format)
  has at least one fact. Every helper that does interesting work
  (`DetectConfiguredProviders`, `RewriteKey`, `BackupWithBump`,
  `AtomicWrite`) has at least one fact.

---

## Credits

- **Newman (lead).** Specification, implementation, helper extraction,
  unit + integration tests, redaction defense, exec report.
- **Larry David (showrunner).** Casting, scope discipline, refusal to
  let the positional `creds rotate` surface bleed into e20's territory.
- **Lt. Bookman (consult).** Help-text brevity audit -- six lines of
  `--rotate-creds` documentation, no more, no less.
- **Jerry (preflight gate).** Owned the `make preflight` contract that
  caught one early formatter slip on the first dry run.
- **Elaine (README copy).** Twelve-line "Rotating credentials"
  subsection sits cleanly after the existing security copy without
  rephrasing it.
- **Costanza (parallel-episode discipline).** Acknowledged the
  shared-file-protocol -- e18 owns dispatch, e20 owns arg parser, e25
  owns one new `case` arm and the new `Cli/CredsRotate.cs`.
  Three-way clean merge anticipated.

The `Co-authored-by: Copilot
<223556219+Copilot@users.noreply.github.com>` trailer is on the commit.

---

## Tag scene -- Next episode preview

INT. WRITERS' ROOM. NEWMAN at the door, clipboard tucked under one arm.
LARRY at the head of the table, finally looking up.

LARRY: Next.

NEWMAN: S03E27. *The Demo.* The finale.

LARRY: Demo of what.

NEWMAN: Of all of it. The wizard. The doctor. The rotation. The offline
flag. The stream parity. The capability gate. The switch. The whole
season, on stage, in one twelve-minute walk-through, with a recorded
binary, a recorded `script(1)` log, and not one network packet leaving
the laptop.

LARRY: And if the wifi at the venue blooms.

NEWMAN: `--offline`.

LARRY: And if the demo key gets pasted on a Twitch stream.

NEWMAN: `--rotate-creds`.

LARRY: And if a model goes dark.

NEWMAN: `--doctor`.

LARRY: (long pause) Fine.

CUT TO: a stage, a podium, a single laptop, a tightly typed prompt, and
the finale we've been writing toward for twenty-six episodes.

FADE OUT.
