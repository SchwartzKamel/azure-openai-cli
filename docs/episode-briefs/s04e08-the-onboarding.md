**Status:** DRAFT (Lloyd Braun, brief author -- awaiting Larry David greenlight)

**Date:** 2026-06-11 (target film date; Lloyd leads when GREENLIT, Costanza co-leads)
**Lead:** Lloyd Braun -- Junior Developer / Onboarding lens; owns the learner experience and the "first 60 seconds" contract
**Co-lead:** Costanza -- Product Manager; owns UX flow, latency budget on the happy path, and final say on prompt copy
**Support:** Russell Dalrymple (UX polish), Babu Bhatt (i18n + cross-platform TTY/ConPTY), Mickey Abbott (a11y of the prompt loop), David Puddy (test harness), Newman (0600 perms + key validation review), Kramer (AOT-safe input plumbing)
**Dependencies:** S04E04 *Reading Room* on `main` (the `--doctor` style smoke surface and the `models` listing the wizard validates against); E05 picker NOT required (wizard predates resolver invocation -- it writes the config the resolver later reads)

# S04E08 -- The Onboarding

> Log line: I typed `az-ai "hello"` for the first time and it threw a
> stack trace at me. I am the user. I should not have to grep the repo
> to find the env vars. Tonight the tool asks me instead.

---

You are filming **S04E08 *The Onboarding*** for `azure-openai-cli`.
Working directory: `/home/tweber/tools/azure-openai-cli`. Branch:
`main`.

---

## The story

Picture it: a developer named Sam reads the README on the train.
`brew install az-ai` (or `scoop install az-ai`, or `dotnet tool install`,
or `make install` from source -- the package doesn't matter). They get
to their desk, open a terminal, and type the first thing the README told
them to type:

```text
az-ai "hello"
```

Today this prints a stack trace, or at best an `[ERROR] AZUREOPENAIENDPOINT not set` and exits non-zero. Sam closes the terminal, opens the README, scrolls to the env-var table, opens `.env.example`, copies three values, edits them, saves, runs `source .env`, retries, fails again because they didn't `export`, asks in Slack, gives up, switches to `litellm` or `ollama`. We lost a user in the first 60 seconds because we asked them to be a shell programmer before we let them be our user.

The promise of `az-ai` is "type a prompt, get text." E08 makes the
promise come true on contact. When `az-ai` detects a TTY and no
credentials it asks Sam three questions, validates the answers, writes
`~/.config/az-ai/env` with `0600`, runs the doctor, and prints the
greeting Sam typed. Total time to first reply: under 90 seconds. No
README required. No grep required. No "I forgot to export" required.

The wizard never runs in scripted contexts. Espanso, AHK, cron, CI, and
anything piping into stdin or out of stdout still sees the headless
contract byte-for-byte. The wizard is a learner ramp, not a feature
flag.

---

## The pitch (engineering substance)

When `az-ai` starts up today, `LoadConfigEnvFrom()` reads
`~/.config/az-ai/env` if it exists, then we read process env, then we
validate. If `AZUREOPENAIENDPOINT` / `AZUREOPENAIAPI` /
`AZUREOPENAIMODEL` are missing we call `ErrorAndExit`. E08 inserts one
branch in front of `ErrorAndExit`: if (a) stdin and stdout are both
TTYs, (b) we are not in `--raw`/`--json`/headless-mode, (c)
`AZ_AI_SKIP_WIZARD` is not set, and (d) the user did not pass
`--no-wizard`, we hand control to `OnboardingWizard.RunAsync()`.

The wizard is a hand-rolled readline-style prompt loop. No
Spectre.Console (AOT-hostile and 200+ KB of UI surface). No Avalonia. No
fancy TUI. We use `Console.ReadLine()`, `Console.IsInputRedirected`,
`Console.IsOutputRedirected`, and `Environment.UserInteractive`. We
wrap key entry in `Console.ReadKey(intercept: true)` so the API key is
masked. That is the whole console surface. It compiles AOT and adds an
estimated 30 to 80 KB to the binary -- Bania bench at greenlight.

The wizard collects three values, validates each inline, and writes
them to `${XDG_CONFIG_HOME:-$HOME/.config}/az-ai/env` with `0600`
permissions using `File.WriteAllText` followed by an explicit
`chmod 0600` via `File.SetUnixFileMode` (no-op on Windows; on Windows
we fall back to an ACL that denies Everyone read -- Newman's call). It
then invokes the same code path `--doctor` uses (E04 surface) and
prints `[OK]` or a list of `[FAIL]` lines. If the doctor fails, the
wizard offers to re-prompt for the failing field; it does not write
partial state.

Re-running the wizard on an existing config (i.e., the user typed
`az-ai --wizard` after the file exists) detects each populated field
and offers `[keep] / [edit]` per field. We never blow away a key the
user did not edit -- including unrelated keys we do not own (e.g.,
`AZURE_FOUNDRY_KEY`, `AZURE_IMAGE_MODEL`).

---

## Scope (in)

- `azureopenai-cli/Cli/OnboardingWizard.cs` (NEW). Hand-rolled prompt
  loop; no third-party TUI deps; AOT-safe.
- One insertion in `azureopenai-cli/Program.cs` immediately before the
  "missing required env var" `ErrorAndExit` branch.
- `--wizard` flag (explicit invoke, runs even when config is complete --
  triggers idempotent edit mode).
- `--no-wizard` flag (force-skip; turns "missing config" back into
  the today-behaviour `ErrorAndExit`).
- `AZ_AI_SKIP_WIZARD=1` env-var opt-out (precedence equal to
  `--no-wizard`; either disables; this is the Espanso / AHK / CI
 escape hatch).
- Auto-detection of headless context: `Console.IsInputRedirected ||
  Console.IsOutputRedirected` => wizard skipped silently, today's
  `ErrorAndExit` runs.
- Inline validation:
  - Endpoint must parse as `Uri`, must be `https`, must end with `/`
    (or we append it).
  - API key length sanity check (>= 32 chars, base64-ish; reject
    obvious junk like `your-key-here` and the literal placeholder
    strings from `.env.example`).
  - Model allowlist: comma-separated, each entry passes the
    shell-hostile gate from S04E04 (no shell metacharacters, no
    whitespace, no path separators).
- Config write at `${XDG_CONFIG_HOME:-$HOME/.config}/az-ai/env` with
  `0600` (Unix) or restrictive ACL (Windows).
- Idempotent re-run: per-field keep/edit; never overwrite unrelated
  keys; preserve comments and ordering when possible.
- Final smoke test reuses the `--doctor` code path (E04); rc=0 on
  green; on red the wizard offers to re-edit the failing field.
- Localised prompt strings via Babu's resource surface (defer
  actual translations; English only at ship time; the string table
  is in place).

## Scope (out)

- GUI wizard. No Avalonia, no Terminal.Gui, no Spectre.Console. AOT
  and binary size kill all three.
- Auto-detection of `az login` / Azure CLI credentials and offering
  to mint a resource key. Backlog; FR-023 already non-goals it.
- Auto-creation of Azure OpenAI resources. The wizard never spends
  money. The wizard never calls Azure ARM. The wizard never reads or
  writes outside `~/.config/az-ai/`.
- Multi-profile (`--profile work` / `--profile personal`). Backlog.
- OS keychain integration (`libsecret`, macOS Keychain, Windows
  Credential Manager). FR-023 design accommodates it later via
  `ICredentialStore`; E08 ships file-only.
- Auto-discovering models by hitting the endpoint and listing
  deployments. The doctor smoke-test uses the model the user typed;
  it does not enumerate.
- Foundry as a setup path. The wizard collects the Azure OpenAI
  triple. Foundry env vars (`AZURE_FOUNDRY_*`) are documented in the
  "next steps" footer and left to manual config (open question).
- Localised prompt translations (the string table lands; the .resx
  files are out of scope -- Babu greenlights for a later episode).

---

## Acceptance criteria

1. `make preflight` exits 0.
2. With no config file and a TTY, `az-ai "hello"` enters the wizard
   instead of `ErrorAndExit`-ing.
3. With no config file and stdin OR stdout redirected, `az-ai "hello"`
   behaves exactly as today -- `ErrorAndExit` with the missing-env-var
   message. No prompt is ever written to a pipe.
4. `AZ_AI_SKIP_WIZARD=1 az-ai "hello"` with missing config exits
   non-zero with today's error message. The wizard never runs. (Espanso
   / AHK / cron contract preserved.)
5. `az-ai --no-wizard "hello"` with missing config behaves identically
   to AC #4.
6. The written config file lives at
   `${XDG_CONFIG_HOME:-$HOME/.config}/az-ai/env` and has mode `0600`
   on Unix. Verified by `stat -c %a` in the integration test. On
   Windows the file has an ACL that denies Everyone read; verified by
   a PowerShell ACL probe.
7. Endpoint validation rejects: non-URI strings, `http://` URIs,
   URIs without a host, and strings containing whitespace. Friendly
   inline message per case; the prompt re-asks; no stack trace.
8. API key validation rejects: any of the placeholder literals from
   `.env.example` (`your-key-here`, `REPLACE_ME`, empty string,
   obvious-junk strings under 32 chars). Friendly inline message;
   the prompt re-asks.
9. Model allowlist validation rejects: shell metacharacters
   (`;`, `|`, `&`, `` ` ``, `$`, `(`, `)`, `<`, `>`), whitespace,
   path separators (`/`, `\`). Same shell-hostile rules as the E04
   `models` surface. Friendly inline message; the prompt re-asks.
10. Re-running the wizard on an existing config (via `--wizard`)
    detects each populated field and offers `[k]eep` / `[e]dit`.
    Choosing `k` for every field is a no-op write (file mtime
    unchanged; checked by integration test). Choosing `e` for one
    field updates only that line and leaves all other lines --
    including unrelated keys like `AZURE_FOUNDRY_KEY` -- byte-for-byte
    untouched.
11. Final smoke test invokes the same code path as `--doctor` and
    reports `[OK]` on success or a list of `[FAIL]` lines on failure.
    On failure the wizard offers to re-edit the failing field; it does
    not exit silently and it does not write a half-validated config.
12. The wizard's stderr output is ASCII-only, `NO_COLOR`-honouring,
    screen-reader friendly (Mickey signs off). No emoji, no ANSI
    boxes, no Unicode arrows.
13. API key entry uses `Console.ReadKey(intercept: true)` and never
    echoes the key to stdout/stderr. The key never appears in any
    log or trace line, even when `--verbose` is set.
14. AOT binary size delta: the wizard adds <= 100 KB to
    `dist/aot/az-ai`. Bania benches pre- and post-merge.
15. xUnit suite (Puddy) covers: TTY-gated trigger, headless skip,
    env-var skip, `--no-wizard` skip, each validator's reject path,
    idempotent re-run preserves unrelated keys, doctor-fail loops the
    prompt, doctor-pass writes 0600 and exits 0, API key is never
    logged. At least one test per AC #2-#13.

---

## Dispatch plan (sub-agents and files)

| Wave | Agent | Files (file-disjoint) | Scope |
|------|-------|-----------------------|-------|
| 1 | **Lloyd Braun (lead)** | `azureopenai-cli/Cli/OnboardingWizard.cs` (NEW); the prompt loop, validators, idempotent re-run, doctor invocation, AOT-safe input plumbing | Owns the learner-grade prompt copy, the validator messages, the idempotent merge, and the doctor handoff. The brief author films their own brief. |
| 1 | **Costanza (co-lead)** | One insertion in `azureopenai-cli/Program.cs` before the missing-env `ErrorAndExit`; `--wizard` / `--no-wizard` flag parsing | Owns the trigger logic, flag parsing, and the precedence rules. Final say on copy at the prompt boundary. |
| 2 | **Newman** | Security appendix to FR-023 (or a new ADR if Larry calls it) | `0600` enforcement on Unix; Windows ACL design; key-never-logged invariant; placeholder-rejection list (the `your-key-here` blocklist). |
| 2 | **Babu Bhatt** | `azureopenai-cli/Cli/OnboardingStrings.cs` (NEW; resource keys + English defaults, no .resx yet); cross-platform TTY/ConPTY review | The string table for every prompt and validator message; TTY detection on Windows ConPTY, macOS Terminal, Linux tty, WSL. Documents the locale-aware-formatting deferred to a later episode. |
| 2 | **Russell Dalrymple** | Review of the prompt visuals; may file findings | Output formatting consistency with the rest of the CLI (E04 doctor lines, error prefixes). No fancy formatting. Plain text, predictable wrap. |
| 2 | **Mickey Abbott** | a11y review of the prompt loop; may file findings | Screen-reader compatibility, `NO_COLOR` honoured, key-press echo behaviour, no ANSI escapes, keyboard-only flow (no arrow-key requirements). |
| 3 | **David Puddy** | `tests/AzureOpenAI_CLI.Tests/OnboardingWizardTests.cs` (NEW); `tests/AzureOpenAI_CLI.Tests/Fixtures/FakeConsole.cs` (NEW or extension of an existing fixture) | Deterministic console-double that scripts user input, captures output, and asserts file state after the wizard runs. Covers every AC #2-#13. |
| 3 | **Kramer** | Review of `OnboardingWizard.cs` for AOT pitfalls; no production edits in this wave unless Lloyd asks | Spot-checks reflection paths, JSON serialisation against `AppJsonContext`, and the file-write encoding (UTF-8 with no BOM, LF line endings). |
| 3 | **Kenny Bania** | Bench the AOT delta pre- and post-merge | Enforce AC #14 (<= 100 KB delta). |

Shared-file note: only Costanza touches `Program.cs` in Wave 1, and
only for the single insertion before the missing-env `ErrorAndExit`.
The wizard implementation lives in its own file (`OnboardingWizard.cs`)
so the hot path stays auditable and reviewers can read the wizard
without rereading the program.

---

## Risks and mitigations

- **AOT-hostile console libraries.** The temptation to reach for
  Spectre.Console for a prompt loop is enormous. *Mitigation:* this
  brief forbids it in Scope (out). The prompt loop is
  `Console.ReadLine` and `Console.ReadKey(intercept: true)`. Kramer
  reviews for accidental reflection.
- **TTY detection on Windows ConPTY and WSL.** `Console.IsInputRedirected`
  is reliable in modern .NET but ConPTY edge cases exist (Windows
  Terminal vs legacy `cmd.exe` vs PowerShell-in-VSCode). *Mitigation:*
  Babu reviews cross-platform detection and writes the test matrix.
  If detection is ambiguous, default to "skipped" -- false negatives
  (no wizard when we could have shown one) are far less bad than
  false positives (wizard prompt written into an AHK pipe).
- **Overwriting an existing config.** The wizard is idempotent by
  design but a buggy merge could clobber `AZURE_FOUNDRY_KEY` or any
  key the wizard does not own. *Mitigation:* AC #10 requires
  byte-for-byte preservation of unrelated keys with an integration
  test that diffs the file. Puddy owns that test.
- **Headless callers tripping the wizard.** Espanso and AHK invoke
  `az-ai` through pipes; the wizard MUST NOT block on input there.
  *Mitigation:* the trigger requires `!IsInputRedirected &&
  !IsOutputRedirected` AND no `AZ_AI_SKIP_WIZARD` AND no
  `--no-wizard`. Three independent gates; any one of them off and
  the wizard does not run. AC #3 / #4 / #5 cover this.
- **API key leakage into logs.** If anyone adds verbose logging
  later, the captured key could leak. *Mitigation:* the key field
  is stored in a local string that never enters `Console.WriteLine`,
  never enters a structured log, and is wiped from the local after
  the file write. AC #13 is a regression test.
- **Smoke test cost.** The doctor smoke-test calls the model. That
  costs tokens. *Mitigation:* the doctor uses the cheapest model in
  the allowlist and the shortest possible prompt (E04 already does
  this). Morty signs off on the cost at greenlight if Larry adds him.
- **Localised prompts shipping without translations.** The string
  table without .resx files is English-only. *Mitigation:* documented
  in Scope (out); Babu greenlights the structure, translations are a
  later episode.

---

## AOT delta budget

**Target: <= 100 KB.** The wizard adds one new class
(`OnboardingWizard`), one string-table class (`OnboardingStrings`),
and one insertion call in `Program.cs`. No new NuGet deps; the
validators are plain regex (already AOT-safe via the source
generator) and `Uri.TryCreate`. The file write uses
`File.WriteAllText` + `File.SetUnixFileMode`, both AOT-friendly. If
the delta exceeds 100 KB, look for an accidental
`System.Text.RegularExpressions` non-source-generated pattern -- swap
to `[GeneratedRegex]`. Bania baselines pre- and post-merge.

---

## Prompt copy (Lloyd's draft -- Costanza has final say)

The wizard prints, in order, to stderr (stdout is reserved for the
eventual model reply once setup is done):

```text
[az-ai] No configuration found at ~/.config/az-ai/env.
[az-ai] Let's set up Azure OpenAI access. Three questions, about a minute.
[az-ai] (Skip with --no-wizard or AZ_AI_SKIP_WIZARD=1.)

Endpoint URL (e.g. https://my-resource.cognitiveservices.azure.com/):
> _

API key (input hidden):
> ********

Model deployment name(s), comma-separated (e.g. gpt-5.4-mini,gpt-4o-mini):
> _

[az-ai] Writing ~/.config/az-ai/env (mode 0600)... done.
[az-ai] Running doctor smoke test... [OK]
[az-ai] You're set. Re-running your original command.
```

On failure of any validator the next line is a single `[!]` prefix,
one sentence, then the prompt repeats. No stack traces. No links to
README. If the user really cannot get past it, they can quit with
Ctrl+C and we exit with rc=2 and a one-line pointer to the README.

---

## Open questions (for Larry David at greenlight)

These are the questions a junior would ask and that nobody has
written down. The brief deliberately picks defaults so the answer
"agreed, ship it" is a one-word greenlight. Larry overrides on any
of them.

1. **Does the wizard also run on first `--doctor` invocation if
   config is missing?** Lloyd's default: no. `--doctor` is a
   diagnostic, not a setup tool. It reports missing config with
   rc=2 and points at the wizard. Costanza may overrule.
2. **Does the wizard require interactive confirmation before
   writing the file?** Lloyd's default: yes, a single `[y/N]` at
   the end ("Write ~/.config/az-ai/env now?") with `y` required.
   Russell may argue this is one prompt too many.
3. **Does the wizard offer Azure AI Foundry as a setup path?**
   Lloyd's default: no for E08. The wizard collects the Azure
   OpenAI triple and prints a one-line pointer to Foundry env
   vars in the "you're set" footer. Costanza may want both paths.
4. **Does the wizard support `--profile <name>` writing to
   `~/.config/az-ai/env.<name>`?** Lloyd's default: no, deferred
   to a later FR. FR-023 already non-goals multi-profile.
5. **Does the smoke test call the real Azure endpoint, or do we
   stub it for the wizard?** Lloyd's default: real call, shortest
   possible prompt, cheapest model. Morty owns the cost number.
6. **On Windows, what is the "0600 equivalent" we enforce?**
   Lloyd's default: ACL that denies Everyone read, grants the
   current user read/write. Newman owns the exact rule.
7. **Does the wizard log a single anonymised telemetry event on
   first-run completion** (so Frank Costanza can see adoption)?
   Lloyd's default: no, opt-in only, deferred to a separate FR.
   Frank may push.
8. **What happens if `${XDG_CONFIG_HOME}` is set but the directory
   does not exist?** Lloyd's default: create it with mode `0700`,
   then write `env` with mode `0600` inside. Newman confirms.

---

## References

- `docs/proposals/FR-023-first-run-wizard.md` -- the proposal this
  episode films; Lloyd's brief is the film version of FR-023, scoped
  to v2.4.0
- `docs/exec-reports/s02e01-the-wizard.md` -- S02 prior art; the
  `ICredentialStore` interface and detection helpers landed there
- ADR-009 -- default model resolution (the model allowlist surface
  the wizard validates against)
- ADR-014 -- output formatting standard (the wizard's stderr lines
  must match)
- `docs/episode-briefs/s04e04-reading-room.md` -- the `models`
  surface and the `--doctor` smoke-test code path the wizard reuses
- `docs/audits/s04-midseason-cast-balance.md` (commit `ec7707e`) --
  Pitt's audit, Rule 4 / Rule 5 pairing Lloyd with Costanza
- `.github/agents/lloyd-braun.agent.md` -- Lloyd's doctrine; learner
  lens, undocumented assumptions, glossary stewardship
- `.github/agents/costanza.agent.md` -- Costanza co-lead; UX, latency
  budget, prompt copy final say
- `.github/skills/episode-brief.md` -- canonical brief format
  (this file follows it)

---

## Validation

```bash
grep -nP '[\x{2018}\x{2019}\x{201C}\x{201D}\x{2013}\x{2014}]' \
  docs/episode-briefs/s04e08-the-onboarding.md  # 0 matches required
NODE_OPTIONS="--max-old-space-size=4096" \
  npx markdownlint-cli2 docs/episode-briefs/s04e08-the-onboarding.md
```

---

## On completion

```sql
UPDATE todos SET status = 'done' WHERE id = 's04e08-brief-lloyd';
```

Return: commit SHA, line count, opening hook, and the open questions
Larry needs to resolve at greenlight.
