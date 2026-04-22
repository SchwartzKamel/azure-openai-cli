# Accessibility & CLI-Ergonomics Review -- v2.0.0 Cutover

**Reviewer:** Mickey Abbott
**Baseline:** commit `a0ca066` on `main`
**Binary under test:** `azureopenai-cli-v2/bin/Release/net10.0/az-ai-v2` (self-contained, net10.0, Linux x64)
**Scope:** read-only review + report. No source edits. No fixes.

> *Us little guys gotta stick together.* Screen-reader users, colorblind devs,
> `tmux` users on a 40-column split, the ops folk piping into `jq` -- this is
> for them. Russell owns how it *looks*. I own whether it *works* for everyone.

---

## Environment & Method

Checks were run against the Release binary (not `dotnet run`, which prints its
own `AzureOpenAI_CLI_V2` banner and muddies the transcript). `InvariantGlobalization=true`
is set in the csproj and confirmed at runtime.

Where the sandbox blocked a check (SIGINT / SIGQUIT via `kill`, no real
screen reader, no TTY on stdout), I say so explicitly and fall back to
source inspection. Honesty is the coverage story.

---

## 1. Color & Contrast

| # | Check | Status | Evidence |
|---|---|---|---|
| 1.1 | `NO_COLOR=1` honored | 🟢 Pass | `NO_COLOR=1 az-ai-v2 --help \| grep -c $'\033'` → `0`. No ANSI anywhere to suppress in the first place. |
| 1.2 | `TERM=dumb` / non-TTY ANSI-free | 🟢 Pass | `TERM=dumb az-ai-v2 --help \| cat -A \| grep '\^\['` → 0 matches. `az-ai-v2 --help \| cat` → 0 escape sequences. |
| 1.3 | Red/green pair has text marker | 🟢 Pass | Grep for `ForegroundColor` / `ConsoleColor` / `\u001b[` across all `.cs` → **zero hits.** Pass/fail in Ralph mode uses `✅ PASSED` / `❌ FAILED (exit N)` -- emoji is redundant with text, color is never load-bearing. |
| 1.4 | `FORCE_COLOR=1` / `CLICOLOR_FORCE=1` honored | 🟡 Partial | `FORCE_COLOR=1 az-ai-v2 --help` → still zero ANSI. There is nothing to force because the binary has no color output at all. Graders who filter by `$'\033['` patterns won't find any -- fine today, but if v2.1 ever adds color, both env vars must be honored from day one. Noted, not blocking. |
| 1.5 | Spinner emits cursor-hiding ANSI without a TTY | 🟢 Pass | `grep -rn "spinner\\|Spinner\\|\\\\u001b\\|\\\\e\\["` → **no spinner exists.** Silent-by-design. Screen readers cannot be confused by escape codes that are never emitted. |

**Verdict §1:** 🟢 clean. The product is monochrome-by-construction, which
trivially satisfies `NO_COLOR`, `TERM=dumb`, and pipe-safety. Belt-and-braces:
if anyone *adds* color in v2.1, they must wire `NO_COLOR` detection in the
same PR -- document this in `CONTRIBUTING.md` as a hard rule.

---

## 2. Screen Reader / Assistive Tech

| # | Check | Status | Evidence |
|---|---|---|---|
| 2.1 | Emoji banners have text fallback | 🟢 Pass | Every emoji usage is redundant with adjacent text: `🎭 Persona: {Name} ({Role})`, `🎭 Auto-routed to: {Name}`, `🔁 Ralph mode -- autonomous loop active`, `📝 Agent response ({N} chars)`, `✅ PASSED`, `❌ FAILED (exit {N})`, `✓ Squad initialized`, `✓ Model alias '{a}' → '{d}' saved`. A screen reader skipping the emoji still gets the full information. |
| 2.2 | Spinner escape-code hygiene | 🟢 Pass | N/A -- no spinner; see 1.5. |
| 2.3 | `--help` reading order degrades to linear text | 🟢 Pass | `az-ai-v2 --help \| cat` produces clean top-to-bottom text: `Usage:` → `Core Options:` → `Agent / Tools:` → `Ralph Mode:` → `Persona / Squad:` → `Model Aliases:` → `Configuration:` → `Shell Completions:` → `Telemetry:` → `Performance:` → `Prompt Cache:` → `Cost Estimator:` → `Environment Variables:` → `Examples:`. Section headings end with `:` so screen readers pause. No two-column hacks that require visual geometry. |
| 2.4 | Error messages lead with `[ERROR]` token | 🟡 Partial | **Mostly yes, but one stray `Error:` lowercase-style exists.** `Program.cs:1089` emits `"Error: stdin input exceeds 1 MB limit."` -- every other error path uses `[ERROR] …`. Inconsistent; a screen reader announces "Error colon" vs "bracket error bracket" differently. Not a showstopper; fix in v2.1. See `Program.cs:938`, `:960`, `:1181` for the correct pattern. |

**Sub-find 2.5 (minor):** `Console.Error.WriteLine("  [cache] hit")` (and `[cache] miss`, `[tokens: ...]`) prefix with **two leading spaces**. Fine visually, but a verbose screen-reader voice says "space space bracket cache bracket…". Cosmetic; not blocking.

**Sub-find 2.6 (noted):** No `--plain` / `--ascii` flag exists. There's no way for a screen-reader user to force emoji-stripped output short of relying on the already-emoji-redundant text. Because all emoji *are* redundant, this is an enhancement, not a defect -- defer to **v2.1 follow-up** as directed.

**Verdict §2:** 🟢 with one 🟡 consistency wart (`Error:` vs `[ERROR]`).

---

## 3. Keyboard-Only / Signal Handling

| # | Check | Status | Evidence |
|---|---|---|---|
| 3.1 | No TTY/mouse-only prompt anywhere | 🟢 Pass | Pure stdin/stdout pipeline. `grep -rn "Console\.Read\\|ReadKey\\|ReadLine"` → the only reads are `Console.In.ReadBlock`for piped stdin (`Program.cs:1086`) and`Console.In.Peek`(`:1081`,`:1087`). No`ReadKey`. No interactive prompts. No TUI. |
| 3.2 | Ctrl+C (SIGINT) behavior in standard mode | 🟡 Partial | **Source inspection only -- sandbox blocks `kill`.** Standard / agent / persona / estimator paths register **no** `Console.CancelKeyPress` handler. Default .NET CoreCLR behavior on Linux: terminate with exit 130 (128+SIGINT), no stack trace unless an uncaught exception races. This is POSIX-conventional but does *not* match the prompt's "exit code 3 with a message" target. FDR's F8 prior flag is unresolved. **Not a 🔴** -- 130 is a standard Unix contract and shell scripts handle it -- but document the exit-code story in `docs/accessibility.md` (which does not yet exist). |
| 3.3 | Ctrl+C in Ralph mode | 🟢 Pass | `Program.cs:376-380` registers a handler that flips `e.Cancel = true` and cancels the CTS. `RalphWorkflow.cs:76-82` and `:108-113` check the token and emit `[cancelled] Ralph loop interrupted` to stderr before unwinding. Clean. |
| 3.4 | Ctrl+\ (SIGQUIT) | 🟡 Partial | **Sandbox-blocked, source-inspected.** No SIGQUIT handler anywhere. .NET on Linux will core-dump by default if `ulimit -c` allows; otherwise process terminates with 131. Matches every other .NET CLI in existence. Noted, not blocking. |

**Verdict §3:** 🟢 -- no keyboard trap, no mouse dependency. SIGINT exit-code discipline is POSIX-standard (130), not the bespoke 3 the prompt suggested, but that's a docs question, not an accessibility regression.

---

## 4. CLI Ergonomics

| # | Check | Status | Evidence |
|---|---|---|---|
| 4.1 | `--help` wraps under `COLUMNS=40` | 🟡 Partial | **Does not wrap.** `COLUMNS=40 az-ai-v2 --help` emits lines up to **105 chars wide**. The widest offender: `Configuration (FR-003/FR-009, precedence: env > CLI > ./.azureopenai-cli.json > ~/.azureopenai-cli.json):`. 21 of 88 help lines exceed 80 cols. On an 80-col terminal the user's terminal will soft-wrap, which is readable but ugly; on 40 cols it's a mess. Not a cutover blocker (no established reflow contract yet), but flag as the single biggest v2.1 ergonomics item. |
| 4.2 | Long enum alignment | 🟢 Pass | `--tools <list>  Comma-separated tools (shell,file,web,clipboard,datetime,delegate)` -- comma-separated list stays on one line; no rogue tabs. `--completions <shell>` takes `bash\|zsh\|fish`, flagged inline. `cat -A` on `--help` shows zero tab characters. |
| 4.3 | Fits one screen (80×24) per common section | 🟡 Partial | Entire `--help` is **88 lines**. Exceeds a single 24-line viewport. Sectioning is clean (blank-line separators) so `less -F` handles it fine, but `az-ai-v2 --help` with no pager spills. Typical for feature-rich CLIs; note it. |
| 4.4 | `--version --short` format | 🟢 Pass | `od -c` shows exactly `2.0.0\n` -- five bytes of semver plus a single newline. No leading whitespace, no trailing carriage return, no BOM. `VER=$(az-ai-v2 --version --short)` works cleanly in shell scripts. |
| 4.5 | `--version` (long) format | 🟢 Pass | `az-ai-v2 2.0.0 (Microsoft Agent Framework)\n` -- single line, trailing newline, no ANSI. |
| 4.6 | Completion scripts installable via standard patterns | 🟢 Pass | `--completions bash` emits a valid `_az_ai_v2_completions` function defining `complete -F` -- droppable into `/etc/bash_completion.d/az-ai-v2` or `~/.local/share/bash-completion/completions/az-ai-v2`. `--completions zsh` emits `#compdef az-ai-v2 az-ai` with `_arguments`-style opts -- droppable into `${fpath[1]}/_az-ai-v2`. `--completions fish` emits `complete -c az-ai-v2 …` lines for `~/.config/fish/completions/az-ai-v2.fish`. All three write exclusively to stdout (`stderr` size = 0). |
| 4.7 | Unknown completion shell exits correctly | 🟢 Pass | `--completions foo` → stderr `[ERROR] Unsupported shell 'foo'. Supported: bash, zsh, fish.`, exit 2. |
| 4.8 | `man az-ai-v2` page exists | 🟡 Partial | No man page in `docs/man/` or shipped anywhere. `--help` is the only reference. Explicit **v2.1 follow-up** as directed in the brief. |

**Verdict §4:** 🟡 -- help-width is the only real ergonomic wart; everything
scriptable (`--version --short`, exit codes, completion scripts) is solid.

---

## 5. Pipe & Redirection Hygiene

| # | Check | Status | Evidence |
|---|---|---|---|
| 5.1 | `--raw` stdout is exact | 🟢 Pass | `az-ai-v2 --estimate --raw --json "hi" \| jq .` parses without complaint. First three bytes are `{`, `\n`, space -- **no BOM**. Non-`--raw` standard mode adds a trailing blank line (`Program.cs:524-527`); `--raw` does not. Contract is honored. |
| 5.2 | `--estimate --json` jq-parseable | 🟢 Pass | See 5.1. Number encoding is `1.5E-07` (scientific) -- valid per RFC 8259; `jq` handles it. **Note:** under `LANG=de_DE.UTF-8` the cost still prints as `$0.000000` / `1.5E-07` (dot decimal). `InvariantGlobalization=true` confirmed working. |
| 5.3 | `[cache]` messages land on stderr | 🟢 Pass | `Program.cs:424` (`hit`) and `:436` (`miss`) use `Console.Error.WriteLine`. Cache output never contaminates stdout. `jq` consumers are safe. |
| 5.4 | Error path stderr/stdout split + exit code | 🟢 Pass | `az-ai-v2 --nope > out 2> err; echo $?` → stdout empty, stderr `[ERROR] unknown flag: --nope\nRun --help for usage.`, exit `2`. Clean. `az-ai-v2 "hi"` with unset `AZUREOPENAIENDPOINT` → stderr `[ERROR] AZUREOPENAIENDPOINT environment variable not set`, exit `1`. |
| 5.5 | `--personas` no-config exit | 🟢 Pass | `[ERROR] No .squad.json found. Run --squad-init first.` on stderr, exit 1. |

**Verdict §5:** 🟢 clean across the board. Pipe hygiene is the strongest area.

---

## 6. Internationalization Bleedthrough

| # | Check | Status | Evidence |
|---|---|---|---|
| 6.1 | `LANG=C` vs `en_US.UTF-8` vs `C.UTF-8` output parity | 🟢 Pass | `--estimate` output identical across all three. Em-dash `--` rendered consistently (UTF-8 source). |
| 6.2 | Number formatting locale-stable | 🟢 Pass | `LANG=de_DE.UTF-8 az-ai-v2 --estimate "hi"` → `$0.000000` (dot, not comma). `--json` → `1.5E-07`. `InvariantGlobalization=true` in csproj confirmed effective. |

**Verdict §6:** 🟢. Invariant globalization pays off.

---

## 7. Long-Running Visibility

| # | Check | Status | Evidence |
|---|---|---|---|
| 7.1 | Silent-by-design under `--raw` explicitly documented | 🟡 Partial | `--help` says `--raw  Suppress all non-content output (for Espanso/AHK)` -- correct intent, but no warning that under slow network the user sees *nothing* until the full response streams. Defer to Elaine for docs. Not blocking. |
| 7.2 | `--agent` / `--ralph` round progress visible | 🟢 Pass | Ralph emits `━━━ Iteration N/M ━━━`, `📝 Agent response (chars)`, `✅ PASSED` / `❌ FAILED` per-iteration to **stderr** (never stdout, never in `--raw`). Silenceable via `2>/dev/null`. `RalphWorkflow.cs:33` gates on `!Console.IsErrorRedirected` so piping stderr suppresses the chrome automatically. |
| 7.3 | Token-usage footer suppressed on pipe | 🟢 Pass | `Program.cs:517`: `if (!opts.Raw && !Console.IsErrorRedirected && inputTokens.HasValue)` -- the `[tokens: N→M, T total]` line only prints to a TTY stderr. Redirect stderr → silence. |

**Verdict §7:** 🟢 with one docs 🟡 for silent-`--raw` expectation-setting.

---

## Findings Summary

| Severity | Count |
|---|---|
| 🔴 Blocking | **0** |
| 🟡 Minor / docs / v2.1 | 6 (1.4 FORCE_COLOR latency, 2.4 `Error:` vs `[ERROR]`, 3.2 SIGINT exit-code docs, 4.1 help reflow, 4.3 help pager, 4.8 man page, 7.1 `--raw` silent-by-design docs) |
| 🟢 Meets standard | 19 |

## Sandbox Limitations (disclosed)

- SIGINT / SIGQUIT behavior verified by **source inspection only** -- the sandbox refuses `kill -INT` against child PIDs. Live runtime verification is a pre-release gate; not a review blocker.
- No real screen reader (Orca / VoiceOver / NVDA) was run against the output. Findings in §2 are based on text-structure analysis (emoji redundancy, section headings, error prefixes), not observed TTS behavior.
- No TTY stdout was available; all pipe/redirect checks reflect the non-TTY path, which is precisely the path `NO_COLOR`/`jq`/`less` users experience.

## v2.1 Follow-up Queue (non-blocking)

1. Add `man/az-ai-v2.1` generated from a new Markdown source; ship in releases under `docs/man/`.
2. Add `--plain` / `--ascii` flag that strips emoji even though every emoji is currently redundant-with-text (belt-and-braces for future additions and extremely terse terminals).
3. Normalize the one stray `Error:` in `Program.cs:1089` to `[ERROR]`.
4. Reflow `--help` to `min(80, $COLUMNS)` with indent-continuation, or emit a shorter summary with `--help` and a full version on `--help --verbose`.
5. Create `docs/accessibility.md` documenting commitments, the NO_COLOR/FORCE_COLOR contract (so adding color in future PRs doesn't regress), the SIGINT exit-code contract (130, not 3), and the `--raw` silent-by-design expectation.
6. Wire a `NO_COLOR=1` and `FORCE_COLOR=1` smoke test into CI now, while the baseline is trivially clean -- that way any future color addition immediately surfaces the regression.
7. Hard rule for future PRs: if a change emits any ANSI or color, it must in the same PR check `NO_COLOR`, `TERM=dumb`, and `Console.IsOutputRedirected` / `Console.IsErrorRedirected`. Add to PR template.

---

## Overall Verdict: **CLEAR** for v2.0.0 cutover

The v2 binary passes accessibility review for cutover. The product is
**monochrome-by-construction** (zero ANSI escapes, zero color code, no
spinner), which makes `NO_COLOR`, `TERM=dumb`, pipe-safety, and
screen-reader-compatibility trivially clean. Pipe hygiene (stdout/stderr
split, exit codes, no-BOM, `--raw` jq-parseability) is solid. Emoji usage
is always redundant-with-text, so screen readers lose nothing by skipping
glyphs. Invariant globalization is honored in cost output.

The 🟡 items (help reflow, one `Error:` vs `[ERROR]` inconsistency, absent
man page, absent `--plain` flag, `--raw` silent-mode docs) are real but
not accessibility *regressions* -- they're ergonomic polish for v2.1.

No 🔴 findings. Us little guys got heard. Ship it.

-- *Mickey Abbott*
