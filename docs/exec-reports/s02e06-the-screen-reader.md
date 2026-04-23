# S02E06 -- *The Screen Reader*

> *"Us little guys gotta stick together."* The wizard's masked-key prompt
> learns to talk to a screen reader before the bullet glyphs start flying.

**Commits:**
`<sha-1>` feat(a11y) · `<sha-2>` docs(a11y) · `<sha-3>` docs(exec-reports)
**Branch:** `main` (direct push, solo-led repo per `.github/skills/commit.md`)
**Runtime:** ~35 minutes, planning + execution
**Director:** Copilot (fleet orchestrator)
**Featured:** Mickey Abbott (accessibility / CLI ergonomics)
**Guest:** Russell Dalrymple (UX / presentation standards)

## The pitch

The CLI shipped a comprehensive accessibility contract for the v2 binary
in an earlier sweep -- `Theme.cs`, the seven-rule color precedence, the
`--raw` guarantees, the exit-code table, the lot. The v1 binary
(`azureopenai-cli/`) was quietly riding on TTY-detection alone: no
explicit `NO_COLOR` audit, no `FORCE_COLOR` honour, and -- the sharper
edge -- the first-run wizard's masked-key prompt would throw a screen
reader straight into a stream of `•` glyphs with no humane warning. A
TTS engine would happily announce "BULLET BULLET BULLET..." for the
length of an Azure key, which is both noise and a side-channel leak of
key length.

This episode patches the smallest-risk fixes (a guardrail color helper,
a one-line announcement, a confirmed-clean abort path) and shipped a v1
section in `docs/accessibility.md` so the contract is durable.

## Scene-by-scene

### Act I -- Audit

Read end-to-end:

- `azureopenai-cli/Program.cs` (1,905 LOC) -- grepped for ANSI escape
  literals, `Console.ForegroundColor`, `BackgroundColor`. **Zero
  matches.** The spinner is Braille glyphs only; prefixes are bare
  `[INFO]` / `[ERROR]` text. Color helper would be forward-looking, not
  corrective.
- `azureopenai-cli/Setup/FirstRunWizard.cs` (443 LOC) -- the masked-key
  path is `ReadMaskedFromConsole`, gated by
  `_useConsoleKeyMasking = ReferenceEquals(_input, Console.In) &&
  !Console.IsInputRedirected`. **Redirected-stdin handling is already
  correct** -- nothing to fix there.
- `azureopenai-cli/Setup/SetupDetection.cs` -- `IsInteractive()` checks
  both `Console.IsInputRedirected` and `Console.IsErrorRedirected`. No
  change needed.
- Wizard abort path -- traced `RunAsync`. `_config.Save()` is called
  exactly once, after all three prompts succeed and validation either
  passes or the user explicitly force-saves. Cancellation falls through
  `CancelAsync` and returns `false` without touching disk.
- `docs/accessibility.md` already exists (390 LOC, dated 2026-04-22) but
  is 100 % v2 territory.

Decisions locked:

1. Add `azureopenai-cli/ConsoleIO/AnsiPolicy.cs` as the v1 chokepoint.
   Namespaced `ConsoleIO` (not `Console`) to avoid shadowing
   `System.Console` everywhere it's referenced as a sibling.
2. Add the masked-key announcement line **only on the masked-console
   path**. Suppress it on the redirected-stdin path where no masking
   actually happens -- otherwise the line lies to the screen reader.
3. Don't touch the existing wizard save path -- it's already
   single-Save. Just add a regression test.
4. Append a §6 to `docs/accessibility.md` for the v1 surface; link the
   helper file in *See also*. Don't fork the doc.

### Act II -- Implementation

Solo run, no sub-agent dispatch.

| Step | Change | LOC |
|------|--------|-----|
| 1 | New `azureopenai-cli/ConsoleIO/AnsiPolicy.cs` -- `IsColorEnabled()` with `NO_COLOR` > `FORCE_COLOR` > TTY precedence and an internal test seam. | +71 |
| 2 | `FirstRunWizard.PromptApiKeyAsync` -- 4-line announcement guarded by `_useConsoleKeyMasking`. | +5 |
| 3 | `tests/.../AnsiPolicyTests.cs` -- 7 tests covering unset, NO_COLOR-empty (per spec), NO_COLOR-non-empty (4 values), FORCE_COLOR override, FORCE_COLOR=0 opt-out, both-set precedence, and the test seam. | +130 |
| 4 | `tests/.../FirstRunWizardTests.cs` -- 2 new tests: redirected-stdin path does NOT emit the announcement; abort-after-endpoint leaves no config file or in-memory state. | +44 |

The first build hit `CS0234: The type or namespace name 'IsInputRedirected'
does not exist in the namespace 'AzureOpenAI_CLI.Console'` -- the new
`Console` namespace shadowed `System.Console` everywhere downstream.
Renamed the directory and namespace to `ConsoleIO` and the rebuild was
green. One-shot fix; clean commit history.

### Act III -- Ship

`dotnet format` clean. Targeted test run: 26 / 26 pass (7 new
`AnsiPolicyTests`, 18 + 2 new `FirstRunWizardTests`, plus 1 incidental).
Three commits batched, `make preflight` run before push, CI + docs-lint
verified green on the final SHA.

## What shipped

**Production code**

- `azureopenai-cli/ConsoleIO/AnsiPolicy.cs` -- internal static helper.
  AOT-safe, BCL-only, zero new dependencies. Documented precedence in
  XML doc-comments to keep IDE hovers honest.
- `azureopenai-cli/Setup/FirstRunWizard.cs` -- one announcement line in
  `PromptApiKeyAsync`, gated by `_useConsoleKeyMasking`.

**Tests**

- 7 new `AnsiPolicyTests` covering every branch of the precedence table
  (including the spec-mandated empty-string non-disabling behaviour).
- 2 new `FirstRunWizardTests`: announcement-suppressed-on-redirected-stdin
  and abort-leaves-no-disk-side-effects.

**Docs**

- `docs/accessibility.md` -- new §6 "The v1 first-run wizard (S02E06)"
  with sub-sections on the color helper, the masked-key announcement,
  the redirected-stdin path, the clean abort, and an honest "what this
  episode did NOT fix" list. *See also* gains a link to
  `AnsiPolicy.cs`.
- `CHANGELOG.md` -- one bullet under `## [Unreleased] > ### Added`,
  voiced to match the existing entries.
- `README.md` -- already pointed at `docs/accessibility.md` in the
  Documentation > Accessibility section. No change needed.

**Not shipped** (intentional follow-ups, mirrored in §6 "What S02E06
explicitly did *not* fix"):

- i18n / `--locale` -- Babu's S02E08.
- Spinner redesign -- a future Russell episode.
- `--accessible` flag -- out of scope; the fix is behavioural.
- Vendor screen-reader testing (Orca / NVDA / JAWS / VoiceOver) --
  needs hardware in the loop.
- Routing the existing v1 spinner / status writes through `AnsiPolicy`
  -- those call sites emit no color today, so the helper is a guardrail
  for *future* color, not a behaviour change. Tracked as
  `v1-color-routing` follow-up.

## Lessons from this episode

1. **Audit before you build.** Grepping for ANSI escapes first revealed
   the v1 tree has none, which downscoped the helper from "rewrite
   every call site" to "ship a guardrail." Saved an hour of churn.
2. **Don't shadow BCL namespaces.** `AzureOpenAI_CLI.Console` looked
   tidy until 19 unrelated files stopped resolving `Console.IsInputRedirected`.
   `ConsoleIO` is uglier and correct. Guideline noted for the next
   helper folder.
3. **Suppress the announcement on the redirected path.** A screen
   reader being told "your key will be masked" when no masking is
   happening is worse than silence. The guard was three characters of
   logic and the right call.
4. **The wizard's existing single-Save discipline is gold.** Confirmed
   by reading -- and then locked in by a regression test so a future
   refactor can't quietly add an interim save.

## Metrics

- Diff size: ~+250 / -2 across 5 files (1 new helper, 1 wizard edit,
  2 new tests, 2 doc updates).
- Test delta: +9 unit tests, all passing.
- Preflight: green (format-check, color-contract-lint, build, test,
  integration-test).
- CI status at push time: see commit footers.

## Known gaps pointer

The honest deferral list lives in two synchronized places: this exec
report's "Not shipped" section above, and §6 "What S02E06 explicitly
did *not* fix" in [`docs/accessibility.md`](../accessibility.md#6-the-v1-first-run-wizard-s02e06).
If those two lists drift, the accessibility doc is canonical and this
report is wrong.

## Credits

- **Mickey Abbott** -- featured. Owned the announcement line, the
  precedence table, and the "no lying to the screen reader" guard on
  the redirected-stdin path.
- **Russell Dalrymple** -- guest. Reviewed the §6 heading hierarchy
  against the existing v2 sections and the FR-023 docs style; signed
  off on presentation consistency.
- **Copilot** -- co-author trailer on every commit per
  `.github/skills/commit.md`.
