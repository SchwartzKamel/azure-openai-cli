# S02E37 -- *The Yada Yada Yada*

> *Real user reports the Espanso WSL example crashes on <code>:ai </code>. Three bugs and a missing feature later, the loading placeholder finally arrives.*

**Commit:** `026a7ec` (fix), `f660bb2` (feat)
**Branch:** `main` (direct push)
**Runtime:** ~95 min (live triage, two waves)
**Director:** Larry David (showrunner)
**Cast:** Field-debug episode -- no sub-agents dispatched; main cast ran point inline (Kramer on the YAML, Jerry on `make espanso-install` parity, Elaine on the header comments and CHANGELOG).

## The pitch

A real user installed the hardened WSL Espanso config, typed <code>:ai </code>, and
got back the worst possible Espanso failure mode: the form pops up, the
user types a prompt, and then a popup says *"An error occurred during
rendering, please examine the logs."* No replacement. No clue what broke.
For 99% of users, that's the end of the journey -- they uninstall and
move on. This user dug into the log, pasted the trail, and we discovered
that the example config our own docs point at as the *production-hardened
reference* had been quietly broken since the Microsoft Agent Framework
migration paved over `az-ai-wrap`.

The same investigation also surfaced the docs-vs-reality gap that's been
lurking since the *Loading Placeholder* doc section landed: the doc
**explicitly claims** the example configs ship with a "yada yada yada"
placeholder pattern, but the example file never actually had one. Users
who copied the file got a 2-3 second silent void during every Azure
round-trip. Two episodes' worth of work compressed into a live triage --
fix the crash, then ship the feature the doc has been promising for a
season.

## Scene-by-scene

### Act I -- Triage

User report: <code>:ai </code> produces "error during rendering". Not a render error
in the Espanso sense -- a shell-extension non-zero exit. We tailed
`%APPDATA%\espanso\logs\espanso.log` (via WSL's `/mnt/c` mount) and read
the smoking gun: PowerShell `ParserError: TerminatorExpectedAtEndOfString`.

Three bugs surfaced once we pulled the thread:

1. **Multiline `cmd: |` under `shell: cmd`.** The <code>:ai </code> and <code>:aiweb </code>
   blocks used the literal-block YAML scalar (`|`, preserves newlines)
   under `shell: cmd`. `cmd.exe` is a single-line shell -- it consumed
   only the first line of the command, leaving an unterminated `"` that
   PowerShell choked on. The other triggers used `cmd: >` (folded --
   newlines become spaces) and worked.
2. **`az-ai-wrap` referenced but not on PATH.** The wrapper became
   unnecessary in v2.1.1 once `az-ai` started auto-loading
   `~/.azureopenai-cli.json` and `~/.config/az-ai/env` at startup, but
   the example was never updated. Users running `make install` get
   `~/.local/bin/az-ai`, no wrapper.
3. **`wsl.exe -e bash -c` is non-login.** Even with `az-ai-wrap` →
   `az-ai`, a non-login non-interactive shell doesn't source `.bashrc`,
   so `~/.local/bin` isn't on PATH. `command not found`.

### Act II -- Fleet dispatch

| Wave | Agents (parallel) | Outcome |
|------|-------------------|---------|
| **1** | Kramer (inline) | Diagnosed the three bugs from the espanso log. Rewrote <code>:ai </code> and <code>:aiweb </code> to `shell: powershell` + `@'...'@` here-string (multiline-safe + metachar-immune). Globally `sed`'d `az-ai-wrap` → `az-ai` and `bash -c` → `bash -lc`. Smoke-tested through the actual pipeline. Shipped as `026a7ec`. |
| **2** | Kramer + Russell (inline) | User came back: "it works, but the yada yada yada doesn't appear." Investigation: the docs have advertised a loading placeholder pattern in `examples/` since the *Loading Placeholder* section landed, but the actual yml never had one. Wrote a Python pass to inject `[System.Windows.Forms.SendKeys]` + `try { <pipeline> } finally { backspace }` into every trigger. Form triggers handled in their `cmd: \|` blocks; clipboard triggers in their `cmd: >` blocks; `:aiimg` got a hand-fix because its closing brace shape is unique. Shipped as `f660bb2`. |

### Act III -- Push, preflight, push again

`make preflight` ran format-check, color-contract, build, and 657-test
suite. Format clean, color clean, build clean. One pre-existing flaky
test (`ListModelsCommand_ShowsEnvAllowlist` -- passes in isolation,
fails in full-suite ordering) reproduced on `main` without the change,
so flagged as unrelated. Integration test runner exited 131 -- also
environmental, also reproduces on clean `main`. Both are episodes for
another day.

CHANGELOG `Fixed` and `Added` sections updated. Conventional Commits
with Copilot trailer on both pushes.

## What shipped

- **Production code:** n/a -- examples and docs only.
- **Examples:** `examples/espanso-ahk-wsl/espanso/ai-windows-to-wsl.yml`
  -- three correctness fixes (`shell: powershell` + here-string for form
  triggers, `az-ai`-direct for all triggers, `bash -lc` for login PATH)
  and the long-promised "yada yada yada" placeholder via SendKeys around
  every trigger's pipeline. Header comments rewritten to document the
  *folded vs literal* / *cmd vs powershell* rule so the trap stays
  clearly marked.
- **Tests:** n/a -- no behavior change in the binary. The fix is
  user-facing config; the right test is the user's next <code>:ai </code> trigger.
- **Docs:** CHANGELOG `[Unreleased] / Fixed` got the three-bug breakdown,
  `[Unreleased] / Added` got the placeholder feature note. The
  *Loading Placeholder* doc section is now finally truthful.
- **Not shipped:** A regression test that catches "example yml that
  parses but is semantically broken under cmd.exe" -- there's no good
  test harness for that on Linux CI without Wine, and even Wine doesn't
  reproduce Espanso's exact spawn pattern. Filed as a future follow-up.
  Also not shipped: a fix to the pre-existing `ListModelsCommand_ShowsEnvAllowlist`
  flake (out of scope; ticketed mentally for a future episode).

## Lessons from this episode

1. **Documentation lies undetected when the docs don't run.** The
   *Loading Placeholder* section had been claiming the example shipped
   the pattern for months. Nobody noticed until a user actually copied
   the file and asked where the yada was. We need a "doc vs example"
   coherence check -- at minimum a grep that flags claims like "the
   example includes X" without an `examples/` reference that can be
   verified mechanically.
2. **`shell: cmd` + `cmd: |` is a footgun trap.** YAML's two block
   scalar styles (`|` literal, `>` folded) interact differently with
   `cmd.exe` (single-line) vs `powershell` (multi-line tolerant). The
   header comment now spells out the rule, but the real fix would be a
   schema validator that flags the combination.
3. **Wrapper-removal episodes need example-file sweeps.** When v2.1.1
   made `az-ai-wrap` redundant, the binary, install scripts, and CHANGELOG
   all got updated -- but the WSL espanso example kept calling
   `az-ai-wrap`. Lesson: the deprecation checklist needs an explicit
   `git grep` step across `examples/`, not just `azureopenai-cli/` and
   `tests/`.
4. **Field-debug episodes can compress two PRs into one wave.** The
   first commit was the safety fix, the second was the feature backlog
   item. Both touched the same file, both were triggered by the same
   user complaint, and shipping them as separate commits kept the
   diffs reviewable while letting the user retest after each.
5. **The `make preflight` flake on `ListModelsCommand_ShowsEnvAllowlist`
   has been red on main for at least one prior episode.** It's a test-
   isolation issue (passes alone, fails in suite), not a regression we
   caused. But "preflight ran clean modulo a known flake" is exactly
   the kind of language the Soup Nazi shouldn't have to keep accepting.
   Filing for a real fix.

## Metrics

- **Diff:** +92 / -26 lines across 2 files (`examples/espanso-ahk-wsl/espanso/ai-windows-to-wsl.yml`, `CHANGELOG.md`).
- **Tests:** n/a (config-only).
- **Commits:** 2 (`026a7ec` fix, `f660bb2` feat).
- **Wall clock:** ~95 min from the user's first "no working" through the
  second push.
- **CI state at episode close:** preflight green modulo the pre-existing
  flake; pushed to `main`; awaiting next CI run.
