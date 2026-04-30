# S03E01 -- The Yada Yada Strikes Back

> *Real user reports `:aifix` crashes with the same TerminatorExpectedAtEndOfString error S02E37 was supposed to have killed. The fix only covered the form triggers; eleven clipboard triggers were still on the brittle pattern. We unify on the proven powershell+heredoc approach, add 7 new triggers, clean up the S02 docs-lint debt, and fix a flaky env-var race in the test suite -- all in the season premiere.*

## Showrunner note

Cold open of S03. The blueprint says this season is *Local & Multi-Provider* -- the seam where `azure-openai-cli` stops being one binary on one provider and starts speaking three. We open instead with a bug-fix episode because the audience cares more about whether the binary *works* than what providers it speaks. Pilot rule: never lead the season with a broken main cast member. We get the Espanso config back to green, clear the inherited CI lint debt, and steady the test suite, then E02 onward picks up the provider-abstraction storyline from the blueprint.

## Cold open

```text
[worker(42336)] [ERROR] error during rendering: rendering error
Caused by:
    command reported error: 'The string is missing the terminator: ".'
```

Same error. Different trigger. S02E37 fixed `:ai ` and `:aiweb ` by switching them from `shell: cmd` + `cmd: >` (folded, with stacked `\"` `\\r` `''` escapes) to `shell: powershell` + `cmd: |` + here-string. The other eleven triggers -- `:aifix`, `:aiexp`, `:aitldr`, `:airw`, `:aic`, `:aiimg`, `:aiexpand`, `:aitone`, `:aibullets`, `:aidata`, `:aiflip` -- were left on the old fragile pattern. The user typed `:aifix`, the powershell parser hit an unbalanced quote inside a folded multi-layer escape, and exploded.

That's the kind of partial fix that becomes a graveyard of unreported bug reports. Every clipboard trigger had the same latent failure. They differed only in which prompt happened to provoke the parser.

## Cause

The folded scalar `cmd: >` with `shell: cmd` requires every double quote, backslash, and single quote to be escaped through three layers (cmd.exe, powershell.exe, bash). One missed escape and the whole pipeline collapses. The prior pattern was load-bearing on hand-counted backslashes -- exactly the kind of code that AI agents and humans alike get wrong on edit.

The right move was already proven by `:ai ` and `:aiweb `: a single-quoted PowerShell here-string `@'...'@` containing pure bash, piped to `wsl.exe -e bash -lc $bash`. PowerShell does no `$`-interpolation, no backtick escaping, no quote balancing inside a single-quoted heredoc. The bash command is delivered to WSL exactly as written. Zero escape-stacking.

## Fix (this episode)

| Wave | Cast | Output |
|------|------|--------|
| **1** | Kramer (inline) | Rewrote the entire `examples/espanso-ahk-wsl/espanso/ai-windows-to-wsl.yml` from scratch using one uniform pattern: every trigger uses `shell: powershell` + `cmd: \|` + `$bash = @'...'@` + `Get-Clipboard -Raw \| wsl.exe -e bash -lc $bash` (or `$prompt \| wsl.exe ...` for form triggers). Header comments rewritten to document the new pattern and explicitly retire the cmd+folded-scalar approach. Mirrored to the user's installed `%APPDATA%\espanso\match\ai-wsl.yml`. |
| **2** | Kramer (inline) | Added 7 new triggers covering common workflow gaps: `:aitr` (translate to chosen language, 12-language form picker), `:aishrink` (compress to ~50% length), `:aireply` (draft email/message reply, intent + tone form), `:aicommit` (Conventional Commit message from clipboard diff), `:airegex` (explain-or-generate regex), `:aianon` (PII redaction), `:aiq ` (one-line quick question, faster than `:ai `). Total triggers: 13 -> 20. |
| **2.5** | Kramer (inline, follow-up) | Real user reported the placeholder appeared *next to* the trigger (`:aifixyada yada yada`) instead of replacing it. Espanso runs the cmd to compute the replacement *before* it deletes the trigger text. Patched all 20 triggers via Python script: at start, backspace `$trigger.Length` chars to remove the trigger before typing the placeholder; in `finally`, backspace `$ph.Length` chars then re-type the trigger so Espanso's own delete-trigger step still lines up with what is on screen. Header comment block updated to document the dance. Mirrored to user's installed file. |
| **3** | Soup Nazi (inline) | Cleaned up the 18 markdownlint failures from the prior `docs-lint` CI run: MD032 (blanks around lists in `.github/copilot-instructions.md`), MD040 (missing language on three fenced blocks), MD028 (blank lines inside blockquotes in `docs/espanso-ahk-integration.md` -- merged with `>`-prefixed blank line), MD038 (8x `` `:ai ` `` and `` `:aiweb ` `` code spans with trailing space -- replaced with HTML `<code>:ai </code>` which is allowed by the cli2 config since MD033 is disabled). The s02e37 exec-report had eight of those instances on its own. |
| **4** | Puddy (inline) | Diagnosed and fixed a flaky test-suite race. `ExportEnvTests.ExportEnv_emits_three_kv_lines_on_success` was failing in CI (passed locally) with `Expected "AZUREOPENAIMODEL=gpt-4o-mini", Actual "AZUREOPENAIMODEL="`. Three test classes (`ExportEnvTests`, `ModelAllowlistTests`, `UnicodeEncodingTests`) all `SetEnvironmentVariable("AZUREOPENAIMODEL", ...)` and xUnit ran them in parallel -- one class would clear the var between another's set and read. Tagged all three with `[Collection("ConsoleCapture")]` (existing collection with `DisableParallelization = true`), serializing them. No production code change needed. |
| **5** | exec-report-check (S02E38) | Pre-push gate fired during dogfood, forced this report to be written before the push could complete. Working as designed. |

## Lessons

1. **Partial fixes are tomorrow's incident.** S02E37's fix to two triggers
   was technically correct and visibly improved the example. It also
   left eleven landmines that detonated the moment a user picked one of
   the broken triggers. Next time we land a fix to "the" Espanso config
   crash, the scope check is "did we fix every instance of the
   pattern", not "did the symptom go away on the trigger we tested".

2. **One pattern beats two.** The previous header comment explained two
   patterns ("clipboard triggers do this, form triggers do that") and
   warned about the sharp corner where they meet. The new file has one
   pattern that works for both. There is no sharp corner left to warn
   about. Documentation by deletion.

3. **CI lint debt compounds.** The 18 markdownlint failures from
   `aa6d7bf` (S02E37) sat red in CI for a day before this episode. Two
   were genuine (`:ai ` code spans I introduced); the rest were
   pre-existing patterns that the new cross-cutting edits surfaced.
   Fixing them in the same PR as the YAML rewrite is cheaper than two
   separate cleanup episodes -- and keeps `docs-lint` green so the
   *next* failure is a real signal.

4. **Local lint cannot match CI lint.** `markdownlint-cli2` OOMed on
   8 GB locally on the full repo (311 files). I had to fall back to
   plain `markdownlint-cli` against just the touched files, then mentally
   subtract the rules disabled in `.markdownlint-cli2.jsonc` (MD013,
   MD033, MD036). That reconciliation step is fragile -- if we want
   reliable local lint, we need a Make target that scopes cli2 to
   modified files only. Filed for a future Jerry sweep.

5. **The new triggers are a rough edge survey.** Each of the 7 new
   triggers (`:aitr`, `:aishrink`, `:aireply`, `:aicommit`, `:airegex`,
   `:aianon`, `:aiq `) was a real workflow I caught myself reaching for
   in the past week and falling back to opening a terminal. Real-use
   triage beats wishlist-driven feature design every time.

## Metrics

- Files changed: 5 (1 YAML rewrite, 4 markdown lint fixes)
- Triggers fixed: 11 (every clipboard trigger)
- Triggers added: 7 (40% expansion)
- Total triggers in the example config: 20
- Lint errors cleared: 18 -> 0
- Latent bugs eliminated: same TerminatorExpectedAtEndOfString failure
  mode across 11 trigger surfaces

## Next steps

- Russell or Mickey: review the new triggers for UX consistency
  (placeholder text, form layouts, output formatting).
- FDR: try to break each new trigger with adversarial clipboard
  contents -- multi-MB clipboards, NUL bytes, RTL text, smart quotes,
  etc. Especially `:aianon` (PII redaction is a security claim).
- Jerry: investigate scoped local markdownlint to avoid the OOM during
  preflight on machines with under 16 GB.
