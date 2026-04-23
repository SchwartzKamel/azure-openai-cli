# S02E32 -- *The Bypass*

> *Newman closes the IFS hole: substring blocklist out, NFKC-tokenize-and-match in.*

**Commit:** `<filled at push>`
**Branch:** `main` (direct push)
**Runtime:** ~25 minutes (with concurrent-WIP stash maneuvers)
**Director:** Larry David (showrunner)
**Cast:** 2 sub-agents in 1 dispatch wave (Newman lead, Kramer guest)

## The pitch

S02E23 *The Adversary* surfaced 22 findings against the tool surface; the
headline was `e23-shell-ifs-tokenization`: `ShellExecTool`'s blocklist
matched substrings against the raw command string, which `/bin/sh`
re-tokenizes via `${IFS}`, tab, newline, quoted/escaped names, env-var
indirection, and fullwidth Unicode lookalikes. Eight test cases under
`tests/AzureOpenAI_CLI.Tests/Adversary/ShellExecBypassTests.cs` were
left `[Fact(Skip = "Live bypass: e23-shell-...")]` as the acceptance
bar for a future hardening episode.

This episode is that hardening. The substring primitive is replaced
with a defense-in-depth pipeline: reject shell metacharacters at parse
time (so `${IFS}`, `$(...)`, backticks, `<(...)`, `>(...)`, tab,
newline, and `<`/`>` redirection never reach the tokenizer);
NFKC-normalize the surviving input (so fullwidth `\uFF52\uFF4D`
collapses to `rm` *before* matching); split on shell-statement
separators (`;`, `|`, `&` -- which also handles `&&` and `||`); for
each segment extract the command head, strip surrounding quotes and
leading backslashes (`"rm"`, `\rm` -> `rm`), basename
(`/usr/bin/rm` -> `rm`), lowercase, and exact-match against the
existing `BlockedCommands` set + the `eval`/`exec` sentinels. The
8 Skipped tests are now `[Fact]` and pass.

`ProcessStartInfo.ArgumentList` was already the convention -- no change
needed there. Sensitive-env-var scrubbing is unchanged. Curl write-form
detection runs after the new pipeline against the NFKC-normalized
command (no behavior change for existing tests).

## Scene-by-scene

### Act I -- Reconnaissance

Read `azureopenai-cli/Tools/ShellExecTool.cs` and the 8 Skipped cases.
Each Skipped case carries the bypass payload and a comment explaining
which primitive failed. Confirmed `ProcessStartInfo.ArgumentList` is
already used, not concatenated `Arguments`. Mapped the existing
passing tests in `SecurityToolTests`, `ToolHardeningTests`, and the
non-Skipped half of `ShellExecBypassTests` to constrain the rewrite:
pipe-chains must still work, `curl` GETs must still pass, `printenv`
of scrubbed env vars must still return MISSING, and the existing
"Error: ... blocked / substitution / process substitution / eval"
assertion strings must keep matching.

### Act II -- Fleet dispatch

| Wave | Agents (parallel) | Outcome |
|------|-------------------|---------|
| **1** | newman (lead) + kramer (engineer) | `Validate()` pipeline + `ExtractCommandHead()` helper land in `ShellExecTool.cs`; 8 `Skip` attributes removed from `ShellExecBypassTests.cs`; preflight green. |

### Act III -- Stash, preflight, push

Concurrent sub-agent activity on `azureopenai-cli/Program.cs` and a new
`azureopenai-cli/CostAccounting.cs` (S02E09 cost-receipt feature)
broke the build during preflight five separate times. Per
[`shared-file-protocol`](../../.github/skills/shared-file-protocol.md),
each wave was stashed by explicit path (`git stash push -m
concurrent-cost-wave-N -- azureopenai-cli/Program.cs ...`) and the
untracked `CostAccounting.cs` / `CostAccountingTests.cs` were
relocated outside the working tree before re-running preflight.
After isolation, `make preflight` passed: 0 format diff, 0 build
errors, 1199/1199 unit tests, 150/150 integration tests (3 skipped --
require `AZUREOPENAIAPI`).

## What shipped

### Production code

- `azureopenai-cli/Tools/ShellExecTool.cs` -- structural rewrite of the
  validation path. New `Validate(string command)` static method
  replaces the four ad-hoc substring checks. New
  `ExtractCommandHead(string segment)` helper centralises basename +
  quote/backslash stripping + lowercase. `using System.Text;` added
  for `StringBuilder` / `NormalizationForm`. `ContainsHttpWriteForms`
  unchanged but now called against the NFKC-normalized command.

### Tests

- `tests/AzureOpenAI_CLI.Tests/Adversary/ShellExecBypassTests.cs` --
  removed `Skip = "Live bypass: e23-shell-..."` from all 8 cases:
  - `Bypass_EvalAfter_AndAnd_ShouldBeRejected`
  - `Bypass_IfsExpansionTokenization_ShouldBeRejected`
  - `Bypass_TabAsCommandSeparator_ShouldBeRejected`
  - `Bypass_NewlineCommandSeparator_ShouldBeRejected`
  - `Bypass_QuotedCommandName_ShouldBeRejected`
  - `Bypass_BackslashEscapedCommandName_ShouldBeRejected`
  - `Bypass_EnvVarCommandIndirection_ShouldBeRejected`
  - `Bypass_FullwidthUnicodeLookalike_ShouldBeRejected`

  All 18 cases in the file now pass; 0 Skipped. Existing
  `SecurityToolTests` and `ToolHardeningTests` shell-exec coverage
  (~30 cases) still passes unchanged -- no test edits needed.

- No new `ShellExecToolTests.cs` regression file added; the existing
  hardening surface in `ToolHardeningTests` and `SecurityToolTests`
  already covers the shapes the new pipeline reasons about, and
  reactivating the 8 adversary tests is the targeted regression.

### Docs

- `CHANGELOG.md` -- one bullet under `[Unreleased]` -> `### Security`,
  per [`changelog-append`](../../.github/skills/changelog-append.md).
- `docs/exec-reports/s02e32-the-bypass.md` (this file).

### Not shipped

- **No new tool / flag / config surface** (per scope discipline).
- **`ReadFileTool` (S02E26 *The Locked Drawer*)** -- different
  episode.
- **`WebFetchTool` SSRF gaps** -- future S03 episode.
- **The other 21 E23 findings** -- triaged to subsequent episodes;
  this episode closes only the IFS / tokenization headline.
- **`AGENTS.md` / `docs/exec-reports/README.md` / writers' room**
  updates -- orchestrator-owned per
  [`shared-file-protocol`](../../.github/skills/shared-file-protocol.md).
  Larry harvests and lands those after this episode airs.

### Findings to log (orchestrator follow-up)

None new from this episode. All 8 Skipped tests resolved cleanly under
the structural rewrite; no additional bypass vectors surfaced during
development.

## Before / after of the blocklist primitive

**Before** (substring-on-raw-input, ~25 lines):

```csharp
if (command.Contains("$(") || command.Contains("`")) return ...;
if (command.Contains("<(") || command.Contains(">(") ||
    command.TrimStart().StartsWith("eval ") || ...) return ...;
var firstToken = command.TrimStart().Split(' ', 2)[0]
                        .Split('/', ...).LastOrDefault();
if (BlockedCommands.Contains(firstToken)) return ...;
foreach (var segment in command.Split('|', ';', '&')) {
    var token = segment.Trim().Split(' ', 2)[0]
                              .Split('/').LastOrDefault();
    if (BlockedCommands.Contains(token)) return ...;
}
```

Bypass surface: any shell expansion that delays command resolution
(`${IFS}`, `$RM`), any non-space whitespace separator (`\t`, `\n`),
any quoting/escaping that survives `/bin/sh` (`"rm"`, `\rm`), any
Unicode lookalike that is normalised away at exec time.

**After** (defense-in-depth, ~70 lines including XML doc):

```csharp
var validation = Validate(command);
if (validation != null) return validation;
// ... ProcessStartInfo + ArgumentList unchanged ...

internal static string? Validate(string command)
{
    // 1. Substitution / parameter expansion
    if (command.Contains("$(") || command.Contains("`")) return ...;
    if (command.Contains("<(") || command.Contains(">(")) return ...;
    if (command.Contains("${")) return ...;

    // 2. Whitespace metachars
    if (command.Contains('\n') || command.Contains('\r')) return ...;
    if (command.Contains('\t')) return ...;

    // 3. I/O redirection
    if (command.Contains('<') || command.Contains('>')) return ...;

    // 4. NFKC-normalize
    var normalized = command.Normalize(NormalizationForm.FormKC);

    // 5/6. Per-segment exact-match
    foreach (var segment in normalized.Split(
                 new[] { ';', '|', '&' },
                 StringSplitOptions.RemoveEmptyEntries))
    {
        var head = ExtractCommandHead(segment);
        if (head is null) continue;
        if (head == "eval" || head == "exec") return ...;
        if (BlockedCommands.Contains(head)) return ...;
    }

    // 7. curl/wget write-form check (unchanged)
    if (ContainsHttpWriteForms(normalized, out var off)) return ...;
    return null;
}
```

The key invariant: **everything that reaches `BlockedCommands.Contains`
has already been NFKC-normalised, segment-split, quote-/backslash-
stripped, basenamed, and lowercased.** No more substring matching.

## Lessons from this episode

1. **Substring matching against shell input is structurally wrong.**
   It matches the lexical surface, but `/bin/sh` reasons about the
   *post-expansion* token stream. The two never line up. The fix is
   not "more substrings to block" -- it's "reject anything that lets
   the shell re-interpret a name", then exact-match the residue.
2. **`<(` / `>(` must be checked *before* the generic `<` / `>`
   redirection block** so the more specific error message wins.
   Otherwise legitimate process-substitution detection regresses to a
   bare "I/O redirection blocked" string and downstream tests that
   assert on the substring `"process substitution"` fail.
3. **Concurrent sub-agent WIP is the dominant cost on this repo
   right now.** Five separate stash maneuvers across one episode --
   another agent is shipping S02E09 *The Receipt* (cost accounting)
   in parallel and Program.cs is touched on every wave. The
   shared-file protocol's stash-isolate-restore discipline held;
   the alternative (touching Program.cs to "fix" it) would have
   been the actual incident.
4. **`StringSplitOptions.RemoveEmptyEntries` collapses `&&` to a
   single segment-break correctly.** `"true && eval echo"` splits
   on `&` to `["true ", "", " eval echo"]`; the empty middle drops
   away; the `eval` head is reached. Verified in the
   `Bypass_EvalAfter_AndAnd_ShouldBeRejected` case.
5. **NFKC normalisation is cheap and deflects an entire class of
   cosmetic-Unicode attacks.** Doing it once, before tokenization,
   means the rest of the pipeline never has to reason about Unicode
   lookalikes again. Pin this with the `Bypass_FullwidthUnicode...`
   regression test so a future "performance" optimisation cannot
   silently strip the `Normalize` call.

## Metrics

- Diff: `+114 / -32` across 3 files
  - `azureopenai-cli/Tools/ShellExecTool.cs` (~ +106 / -28)
  - `tests/AzureOpenAI_CLI.Tests/Adversary/ShellExecBypassTests.cs`
    (8 Skip-attribute lines edited)
  - `CHANGELOG.md` (+11 lines, 1 bullet)
  - `docs/exec-reports/s02e32-the-bypass.md` (NEW, this file)
- Test delta: **+8 active** (8 previously-Skipped tests now run as
  `[Fact]`); no new test files; total Adversary `ShellExecBypassTests`
  count unchanged at 18, Skipped count goes 8 -> 0.
- Preflight: **PASSED** -- format clean, color-contract clean,
  Release build clean, 1199/1199 unit tests, 150/150 integration
  tests (3 skipped: require `AZUREOPENAIAPI`).
- CI status at push time: not yet observed; will follow with
  `gh run list --branch main --limit 1` post-push.
- Staged paths (verified via `git diff --cached --name-only` before
  commit, per `shared-file-protocol` "Shared working tree"):
  - `CHANGELOG.md`
  - `azureopenai-cli/Tools/ShellExecTool.cs`
  - `docs/exec-reports/s02e32-the-bypass.md`
  - `tests/AzureOpenAI_CLI.Tests/Adversary/ShellExecBypassTests.cs`

## Credits

- **Newman** (lead) -- threat-model framing, validation-pipeline
  design, blocklist-rationale comments, exec-report authorship.
- **Kramer** (engineer guest) -- `Validate()` / `ExtractCommandHead()`
  implementation, NFKC-normalisation placement, Skip-attribute
  cleanup, preflight stash maneuvers.

`Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>`
trailer present on the commit.
