# Color & terminal output contract

> *Us little guys gotta stick together.* This file is the **source of truth**
> for how `az-ai-v2` must treat color, ANSI escapes, and terminal decoration.
> `CONTRIBUTING.md` links here. Any PR that adds color output **must** conform
> to every rule below; reviewers will block on this file.
>
> **Owner:** Mickey Abbott (accessibility / CLI ergonomics)
> **Status:** locked-in for v2.1 (pre-emptive — v2.0.0 ships monochrome).

## Why this exists

v2.0.0 is **monochrome-by-construction** — zero ANSI SGR escapes, zero
`ConsoleColor` calls, no spinner. That is a feature, not an accident. It
means `NO_COLOR`, `TERM=dumb`, pipe-safety, and screen-reader compatibility
are all trivially satisfied today.

The moment any PR adds the first `\e[31m` or `Console.ForegroundColor = ...`,
the trivially-clean invariant is gone and we need an enforceable contract
**in the same PR**. That contract is this file.

## The rules

### Rule 1 — `NO_COLOR` always wins

If the `NO_COLOR` environment variable is **set** and **non-empty**, the
binary **MUST NOT** emit any ANSI SGR escape sequences. Ever. No exceptions.
This rule beats every other rule in this document, including
`FORCE_COLOR=1` and `CLICOLOR_FORCE=1`.

Reference: <https://no-color.org>.

Implementation note: check `Environment.GetEnvironmentVariable("NO_COLOR")`
for a non-null, non-empty string. Presence alone is not sufficient — an
empty value (`NO_COLOR=`) does **not** disable color, per the spec.

### Rule 2 — `FORCE_COLOR` / `CLICOLOR_FORCE` force color on (subject to Rule 1)

If `FORCE_COLOR=1` **or** `CLICOLOR_FORCE=1` is set, the binary **MUST**
emit ANSI SGR escapes even when stdout is redirected (pipe, file). This
supports CI log collectors, `script(1)`, `unbuffer`, and test harnesses
that want the color bytes preserved.

`NO_COLOR` still wins. If both `NO_COLOR=1` and `FORCE_COLOR=1` are set
simultaneously, color is **off**.

### Rule 3 — Auto-detect: color off unless stdout is a TTY

With no env-var overrides, the default is:

- Color **ON** if `Console.IsOutputRedirected == false`
  (equivalent to `isatty(STDOUT_FILENO) == 1`).
- Color **OFF** in every other case: pipes, files, subprocess capture,
  `dotnet test` harnesses, CI without `FORCE_COLOR`.

Never assume a TTY. Never emit color unconditionally.

### Rule 4 — `TERM=dumb` disables color

If `TERM=dumb`, no ANSI. This covers Emacs `M-x shell`, some CI runners,
and historic `tramp` sessions. It is a weaker signal than `NO_COLOR` but
a stronger signal than auto-detect.

### Rule 5 — Precedence table (the ordering is the contract)

Evaluate top-to-bottom; first match wins:

| Priority | Signal | Result |
|---|---|---|
| 1 | `NO_COLOR` set and non-empty | **Color OFF** |
| 2 | `TERM=dumb` | **Color OFF** |
| 3 | `CLICOLOR=0` | **Color OFF** |
| 4 | `FORCE_COLOR=1` or `CLICOLOR_FORCE=1` | **Color ON** |
| 5 | Auto-detect: stdout is a TTY | **Color ON** |
| 6 | (default fallthrough) | **Color OFF** |

The `--raw` flag is **not** in this table — it is handled by Rule 6 and
short-circuits everything.

### Rule 6 — `--raw` is silent-by-design

The `--raw` flag disables **all** decoration:

- No ANSI SGR escapes.
- No spinner, no progress bars, no cursor manipulation.
- No banner, no `🎭` / `✅` / `❌` emoji chrome.
- No trailing blank lines beyond the final content newline.
- No `[tokens: ...]` footer on stderr.
- No `[cache] hit` / `[cache] miss` on stderr (already suppressed today).

`--raw` is a **stable machine-readable output contract** for Espanso,
AutoHotkey, `jq` pipelines, and shell scripts. It is documented as such
in `docs/release-notes-v2.0.0.md` §7.1. Breaking `--raw` output is a
breaking change under SemVer.

When `--raw` is present, all color/TTY logic in Rule 5 is **bypassed**
and color is unconditionally **OFF**.

### Rule 7 — `[ERROR]` prefix is mandatory on stderr

Every error line written to `stderr` **MUST** start with the literal
token `[ERROR]` followed by a space. Not `Error:`, not `ERR:`, not
`error -`, not unprefixed. Screen readers (Orca, NVDA, VoiceOver) key
off the literal `[ERROR]` token — `"bracket error bracket"` is parsed
into a recognizable announcement, `"Error colon"` is not.

Correct:
```
[ERROR] unknown flag: --nope
[ERROR] AZUREOPENAIENDPOINT environment variable not set
```

Incorrect:
```
Error: stdin input exceeds 1 MB limit.
ERR: bad config
  [error] validation failed
```

Reference: v2.0.0 accessibility review §2.4 tracks one stray
`Error:` at `Program.cs:1089` — that line is the v2.1 normalization
work; every new error path landed after this contract goes live must
be `[ERROR]` from day one.

## Helper contract: `Theme.UseColor()`

All color output **MUST** be gated by a single boolean helper:

```csharp
// One place, one decision, one grep target.
public static class Theme
{
    public static bool UseColor() { /* implements Rules 1–5 */ }
}
```

And every caller looks like:

```csharp
if (Theme.UseColor())
    Console.Write("\u001b[31m" + msg + "\u001b[0m");
else
    Console.Write(msg);
```

**PRs that add `Console.ForegroundColor = ...` or a raw ANSI string
literal *without* going through `Theme.UseColor()` will be blocked.**
This is enforceable via a trivial grep — see the follow-up lint rule
proposal in the [v2.1 accessibility queue](../../docs/accessibility-review-v2.md).

Rationale: color decisions are policy, not a per-call concern. One
choke point means one place to audit `NO_COLOR` / `FORCE_COLOR` /
`--raw` / TTY detection. Scatter the decision across 40 call sites
and the first env-var regression is inevitable.

## Test checklist (required for every colorized output path)

Every PR that adds a new colorized output must ship these tests in the
same PR. No exceptions.

- [ ] **`NO_COLOR=1` test:** invoke the path with `NO_COLOR=1` set;
      assert the captured stdout contains **zero** `\u001b` bytes.
- [ ] **`FORCE_COLOR=1` test:** invoke the path with `FORCE_COLOR=1`
      and stdout redirected (non-TTY); assert stdout contains **at
      least one** `\u001b[` SGR sequence.
- [ ] **`TERM=dumb` test:** invoke with `TERM=dumb`; assert zero ANSI.
- [ ] **`--raw` test:** invoke with `--raw`; assert zero ANSI **and**
      zero banner/spinner/decoration bytes (byte-exact comparison).
- [ ] **Auto-detect test:** invoke with stdout redirected to a
      `StringBuilder` / file (non-TTY, no overrides); assert zero ANSI.
- [ ] **Precedence test:** invoke with **both** `NO_COLOR=1` **and**
      `FORCE_COLOR=1`; assert zero ANSI (Rule 1 beats Rule 2).

## Anti-patterns (do NOT do)

1. **Hard-coded ANSI escapes in string literals** — `"\u001b[31m"`,
   `"\e[1;33m"`, `"\x1b[0m"` scattered through call sites. Every
   escape must come from a helper that already consulted
   `Theme.UseColor()`.
2. **Bypassing `Theme.UseColor()`** — `Console.ForegroundColor = ...`
   or direct ANSI writes without checking the helper.
3. **Assuming `isatty`** — emitting color without checking
   `Console.IsOutputRedirected`. Users pipe into `less -R` *and* into
   `jq`; both must work.
4. **Emitting color to stderr by default** — stderr is for diagnostics;
   colorizing it without explicit opt-in breaks tools that grep stderr
   for the literal `[ERROR]` token.
5. **Using color as the only signal for state** — a red word and a
   green word must still be distinguishable in monochrome. Use
   `✅ PASSED` / `❌ FAILED` / `[ERROR]` / `[WARN]` text tokens,
   and let color be *garnish* on top.
6. **Spinner that writes raw ANSI cursor codes to stderr without
   checking `Console.IsErrorRedirected`** — it spams screen-reader
   assistive tech with escape soup.
7. **Tab characters inside an 80-column help line.** Don't do it. Mickey
   will find it and Mickey will not be nice about it.

## Enforcement

- **Review:** reviewers check this file before approving any PR that
  touches output formatting.
- **Grep gate:** a CI job (proposed, tracked for v2.1) will fail on
  any `.cs` file outside `Theme.cs` containing `\u001b[` or
  `ConsoleColor\.` — see the lint-rule proposal in the v2.0.0
  accessibility review follow-up queue.
- **Release notes:** every release that changes color behavior must
  call out the change in `docs/release-notes-v*.md`.

## References

Canonical artifacts implementing this contract (keep in sync — any
divergence between spec and code is a bug against this document):

- **`scripts/check-color-contract.sh`** — automated lint gate. Greps
  `azureopenai-cli-v2/**/*.cs` for `ConsoleColor.`,
  `Console.ForegroundColor` / `Console.BackgroundColor`, and raw ANSI
  escape literals (`\u001b[`, `\x1b[`, `\e[`, `\033[`). Wired into
  `make lint` and `make preflight`. Exit 1 on any violation, with a
  pointer to `Theme.cs`. Lines deliberately carrying raw ANSI (e.g. an
  intentional stderr spinner) must carry the trailing marker comment
  `// color-contract: approved-spinner` and be recorded below.
- **`azureopenai-cli-v2/Theme.cs`** — the canonical helper. New call
  sites **must** route through `Theme.WriteColored(...)` /
  `Theme.WriteLineColored(...)`; these consult `Theme.UseColor()`
  (Rules 1–5) and honor `Theme.RawMode` (Rule 6 defensive layer). The
  Rule 7 prefixes `[ERROR]` / `[warn]` are exposed as
  `Theme.ErrorPrefix` / `Theme.WarnPrefix` so call sites don't
  hand-roll them. AOT-clean, BCL-only, no reflection.

### Approved carve-outs (raw ANSI allowed)

None today. The v2.0.0 tree is monochrome-by-construction, so the lint
runs clean against zero call sites. Entries below are added only when
a specific line is granted an exception and tagged with the
`// color-contract: approved-spinner` marker.

<!-- carve-out entries go here, in the form:
     - `path/to/file.cs:LINE` — rationale (reviewer, date, issue link) -->

### Forward-looking: v2.1 migration scope

Existing `azureopenai-cli/` (v1) `ConsoleColor` call sites are
deliberately **out of scope** for the lint gate — it targets only
`azureopenai-cli-v2/`. v1 is frozen on its current color story; the
v2.1 migration wave will port the remaining legitimate colorized paths
(error highlighting, banner chrome) onto `Theme.WriteColored(...)` so
the single chokepoint owns every decision. Any new ANSI added to
`azureopenai-cli-v2/` between now and v2.1 must land through
`Theme.cs` on day one — the lint will block otherwise.

---

*Color is garnish, never the entrée. Information must survive
monochrome. — Mickey*
