# S04E08 -- *The Onboarding*

> *The wizard finally says why it picked your model, and a newcomer's first command stops failing.*

**Commit:** `(this commit)` -- one code commit plus docs, single push
**Branch:** `lafiamafia/s04e08-the-onboarding` (PR, off `main`)
**Runtime:** ~2 hours wall-clock
**Director:** Larry David (showrunner seat, filled by a single orchestrator)
**Cast:** 0 sub-agents, 3 inline passes by one orchestrator. Lloyd Braun (LEAD, the junior lens), Costanza (guest, product), Mickey Abbott (guest, a11y), Newman (finding owner, security claim)

## The pitch

FR-023 asked for a first-run wizard so that "type a prompt, get text"
survives first contact. The wizard has existed since S03E11. What it did
not do was explain itself. After setup it printed the default *provider*
and said nothing about the default *model*, which is the choice a new user
will actually notice on the first real prompt.

S04E05 gave the CLI a picker with four locked reason codes, so the reasoning
now exists in code. E08 connects it to the first-run screen: after setup,
say which model is your default and why, in plain words, and say how to
change it. That is the Rule 4 episode from the E06 audit: the junior reader
gets a lead, and the 27 items parked in `REVIEW-onboarding.md` get a
reason to be read.

One correction to the brief came first. The blueprint's example reads "I
picked `gpt-4o-mini` because your prompt has tools and is under 4k tokens."
The shipped picker never sees the prompt. It is pure and allowlist-based:
an explicit model, an optional axis, and the `AZUREOPENAIMODEL` list in the
order the user typed it. An explanation that claimed more than the picker
decides would be false on day one, so the panel explains what the picker
really does.

## Scene-by-scene

### Act I -- Read the code, not the brief

The design constraints came out of three facts found by reading, not
assuming:

1. **The picker is pure and does not see prompts.** The panel therefore
   wraps `ResolveSmartDefault.Pick` and branches only on the reason code. It
   adds no second decision-maker, so it cannot disagree with the runtime,
   and it adds no fifth reason code.
2. **"Not in the registry" means something different for Azure.** A
   deployment name is chosen by the user (`my-gpt4`), and the capability
   gate deliberately passes unregistered models through (S04E03). The
   picker's raw FALLBACK text ("fix AZUREOPENAIMODEL") would read as an
   error to a first-time user, so the panel says it is normal for a custom
   deployment name and says what it costs: capability checks are skipped.
3. **Terminals vary.** Mickey's constraints applied: ASCII only, no color,
   no glyphs, every line at most 78 columns, and wrapping that never splits
   a model name.

### Act II -- Fleet dispatch

| Wave | Agents (parallel) | Outcome |
|------|-------------------|---------|
| **1** | orchestrator (Lloyd lens, inline) | `WizardModelExplainer` + 21 unit tests |
| **2** | orchestrator (Costanza + Mickey lens, inline) | Wired into `SetupWizard`, 5 PTY integration cases |
| **3** | orchestrator (Newman lens, inline) | Docs corrected, 5 findings logged |

No sub-agents were dispatched, so the solo-background-dispatch rule is not
engaged. The lenses are review passes the one orchestrator ran in order, and
this report does not claim a fleet.

Three things went sideways, and all three are the useful part:

- **A vacuous-pass risk, caught by a control case.** The first PTY run
  showed the unregistered-name note for `gpt-4o`. That was truthful (the
  seed registry ships only three models), but it raised the real question:
  is the registry even loaded on the wizard path? If not, *every* name would
  look unregistered and the unregistered-name test would pass for the wrong
  reason. The control case is a known model (`gpt-4o-mini`) that must get no
  note. It does, so the registry is loaded, and that assertion now lives in
  the integration suite. No unit test could have seen this, because the
  wiring is what was in doubt.
- **A failing test that was the test's fault.** One unit test failed because
  the wrapper had split "normal for a custom / deployment name" across two
  lines and the test matched across a newline. The product was right. The
  fix was to reflow lines before prose assertions, so tests no longer depend
  on where a line breaks.
- **A compile error from guessing a type's shape.** `ModelRegistryEntry` is
  a positional record, not an object-initializer type. Read the definition,
  then write the test.

### Act III -- What the docs promised

Writing the README sample from *real* wizard output turned up two onboarding
defects that had nothing to do with the picker:

- **`az-ai --init` does not exist.** The README's key-rotation step and
  `docs/onboarding.md` both send a newcomer to it. Running it prints
  "unknown flag". Nobody had run the doc's own commands.
- **The key-storage table promised encryption that is not implemented.**
  It named Windows DPAPI, the macOS Keychain and Linux libsecret. A search of
  the code finds none of them; S03E10 *The Keychain* shipped per-provider
  env-var namespaces, and FR-023, which proposed OS keystores, is still
  `Proposed`. What the code really does: the wizard writes a plaintext env
  file at `~/.config/az-ai/env` (mode `0600` on Linux and macOS), and
  `--config set api_key=` writes `~/.azureopenai-cli.json` the same way. On
  Windows no permission change is applied. A reader trusting the old table
  would believe their API key was encrypted at rest.

Both are fixed in the README and onboarding doc. The Windows permission gap
is logged for Newman as an open finding, and the README now states the
behavior.

**Gates.** `dotnet format` clean, build 0 warnings, unit 1,632 of 1,632,
integration 116 of 116 (2 skipped, need live Azure credentials),
`color-contract-lint` clean, `markdownlint` clean on the touched docs.

## What shipped

**Production code**

- `azureopenai-cli/Resolution/WizardModelExplainer.cs` (new, 125 lines). A
  pure function: registry plus the typed model list in, panel lines out.
  Calls `ResolveSmartDefault.Pick` for the decision and branches only on the
  reason code. Scrubs echoed names, wraps at 78 columns.
- `azureopenai-cli/SetupWizard.cs`. A two-line hint at the model prompt
  (what a deployment name is; the first name becomes the default) and the
  panel at the end of setup, printed when an Azure answer exists.
- `azureopenai-cli/Cli/CapabilityRejection.cs`. `Scrub` changed from
  `private` to `internal` so the panel reuses the existing terminal-injection
  guard instead of adding a third copy.

**Tests**

- `WizardModelExplainerTests.cs`: 21 cases. The panel's default always equals
  the picker's; known head has no note; unregistered head is "normal" and
  free of alarming words; single versus multiple models; empty input;
  78-column and printable-ASCII checks including the wrap path; no mid-name
  splits; control characters scrubbed; null registry throws.
- `tests/integration_tests.sh`: 5 new PTY cases. Panel names the default,
  states why, explains an unregistered name as normal, states the rule before
  the choice, and the control case that proves the registry is loaded.

**Docs**

- `README.md`: wizard sample now shows real output; key-storage table and
  paragraph corrected; `--init` replaced with `--setup`; one bullet on the
  panel.
- `docs/onboarding.md`: Path A rewritten to match the shipped wizard.
- `docs/findings-backlog.md`: 5 rows (2 resolved, 3 open, with owners).
- `CHANGELOG.md`: one `[Unreleased]` entry.

**Not shipped**

- **Seed registry expansion** (open, Kramer + Costanza). Three seed models
  means `gpt-4o` reads as "custom". Choosing which models to seed is a
  product call with a cost-tier decision attached, not a drive-by.
- **Windows permission hardening** (open, Newman). Needs an ACL design.
- **Stale v1 flag descriptions** in `docs/user-stories.md` and
  `docs/accessibility.md` (open, Elaine). Historical prose, not instructions.
- **`--why-this-model` and `--prefer`.** Those are E09 *The Override*.
- **A "run every command in the docs" check.** The `--init` defect shows the
  need; it is a CI task for Jerry, not part of this episode.

## Lessons from this episode

1. **A blueprint can describe a feature the code does not have.** The brief
   promised prompt-aware reasoning; the picker is allowlist-based. Read the
   code before honoring the brief, and shrink the promise to the truth.
2. **The same fact is a different message for different readers.** "Not in
   registry" is a diagnostic for an engineer and routine for a user naming
   their own deployment. Do not reuse an engineer-facing string on the first
   screen a newcomer sees.
3. **A passing test can be vacuous.** If the registry had not been loaded,
   the unregistered-name test would have passed for the wrong reason. A
   control case with the opposite expectation is what makes it mean
   something.
4. **Grep the code for the claim before trusting a security table.** The
   README promised encryption that no line of code implements. A docs claim
   about a security property deserves the same evidence as a code claim.
5. **Run the commands your onboarding doc tells people to run.** A newcomer's
   first copy-paste failed. This is the junior lens doing its one job.
6. **Do not let prose tests depend on line breaks.** The failing test was
   mine; reflowing before matching made it robust to the wrapper.
7. **Read the type before writing the test.** A positional record is not an
   object initializer.

## Metrics

- **Diff:** 8 files changed, 421 insertions, 16 deletions (plus this report,
  the index row and the changelog line).
- **Test delta:** +21 unit (1,611 to 1,632), +5 integration (111 to 116).
- **Preflight:** format, build (0 warnings), unit, integration and
  color-contract all green. `exec-report-check` clean.
- **CI status at push time:** pending at commit time; link in the PR.

## Credits

Lloyd Braun led, and is the reason the first screen says what a deployment
name is. Costanza supplied the product question (what does a new user need
to know right now?), Mickey the output constraints, and Newman owns the
keystore finding. Cast-balance ledger after this episode: Lloyd now holds a
lead of record (Rule 4 satisfied), and the E06 slate holds with E09
*The Override* (Russell lead; Sue Ellen and Morty guests) next. Commits carry
`Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>`.
