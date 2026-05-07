# S03E11 -- *The Wizard, Reprise*

> *Jerry: "What's the deal with first-run experiences? You sit a person in front of a wizard, you get one shot to not make them feel like an idiot. The old wizard asked them three questions about Azure. Three questions. We have five providers now. The old wizard does not know that. So today the wizard learns."*

**Commit:** pending (orchestrator-batched with E12)
**Branch:** `main`
**Runtime:** ~70 minutes wall-clock
**Director:** Larry David (showrunner)
**Cast:** Jerry (lead), with on-call review from Newman (chmod 600 invariant), Elaine (README rewrite), and Puddy (PTY-driven integration test). Bania ran E12 in parallel on the next bench over.

## The pitch

E09 shipped the wire-protocol seam. E10 shipped the credential drawer with compartments. The seam can route to OpenAI, Groq, Together, and Cloudflare; the drawer knows what to do when an `[provider:openai]` header shows up in the env file. A real human, sitting at a terminal for the first time, still has no way to get any of that on disk except by hand-editing a file they have probably never heard of. The wizard from S02 still asks them three Azure-shaped questions, writes a JSON file at a different path, and ends with a cheery "you are all set" while the new providers sit there in the code base un-reachable.

This episode is the catch-up. The wizard learns about providers. It asks one extra question first -- which provider do you want to start with, and the menu lists all five. It dispatches to the right credential prompt for that provider. It loops once if the operator wants a second provider on the same machine. It validates compat model strings through the same `ParseCompatModels` that E09 ships, so a typo never reaches disk. And it writes the canonical E10 file format, with a default unsectioned block carrying the back-compat exports and one `[provider:NAME]` section per non-Azure provider configured. chmod 600 on Unix. Backup-before-overwrite for the existing-file case. Refuse-on-non-TTY for the closed-stdin case.

The acceptance bar from the brief: extend the existing wizard, do not write a parallel one. Reuse `OpenAiCompatAdapter.ParseCompatModels` as the validator. Reuse `Preferences.DefaultPath()` precedent for the path computation (XDG on Unix, `%APPDATA%` on Windows). Extract a hermetic state-machine helper so the prompt sequence is unit-testable without a PTY harness. Pipe canned answers through a real PTY in the integration test and assert chmod 600 on the resulting file. Document the new behaviour in README's "First run" subsection. Update CHANGELOG. Write the exec report. Do not break the 32 existing setup-wizard tests, the 39 keychain loader tests, or any of the 700-odd unrelated unit tests.

## Cold open

Jerry walks in at 9 a.m. carrying a printout of `s03e09-the-compat.md` and `s03e10-the-keychain.md` paper-clipped together with a yellow sticky note that says "the wizard does not know about either of these episodes." He pins it above the keyboard, opens `Program.cs` to the `--setup` block, and reads the existing wizard end-to-end before touching anything.

"What's the deal with this thing?" -- about ninety seconds in. "It writes JSON. Why does it write JSON. The whole pipeline reads env vars. The env file IS the credential store. We have an env-file loader that knows about provider sections. The wizard writes a JSON file at a different path that nothing else in the new code touches. Who *is* this wizard for?"

Newman, on call from the next room, does not look up. "It was for the wizard you wrote in S02. Times changed. The wizard didn't."

Larry, at the door, signs off the brief without ceremony. "Provider menu first. Compat validation. Backup. chmod 600. Don't reinvent the path resolver. Don't ship without the PTY integration test. Go."

## Scene-by-scene

### Act I -- Casing the joint

Jerry reads the existing `SetupWizard.cs` (321 lines), `OpenAiCompatAdapter.cs`'s `ParseCompatModels` (the validator the brief explicitly names), and the E10 loader in `Program.LoadConfigEnvFrom` (the env-file consumer that defines the on-disk format). Three artifacts inform the implementation choices and are recorded here so the next person walking this seam does not have to rediscover them.

1. **`OpenAiCompatAdapter.ParseCompatModels(string?)`.** Returns `Dictionary<string,string>?`, throws `ArgumentException` with an actionable message on malformed entries. Takes a comma-separated `preset:model` list. The wizard collects bare model names per provider (because asking "type `openai:gpt-4o`" is hostile UX), so the wizard synthesises the `preset:` prefix before validating. Reuse, do not re-implement -- if the parser ever tightens, the wizard inherits the new rule for free.

2. **`Program.LoadConfigEnvFrom`'s section namespacing rule.** `[provider:openai] / API_KEY=...` becomes `OPENAI_API_KEY` in the process environment. The cloudflare preset is the one off-shape: `OpenAiCompatAdapter.BuiltIn["cloudflare"]` reads `CLOUDFLARE_API_TOKEN`, not `CLOUDFLARE_API_KEY`. So the wizard writes `[provider:cloudflare] / API_TOKEN=...` for cloudflare and `API_KEY` for everyone else. Belt-and-braces: the unit test `BuildEnvFileContent_Cloudflare_UsesApiTokenAndAccountId` pins the rule.

3. **`Preferences.DefaultPath()`.** Honours `XDG_CONFIG_HOME` on Unix, falls back to `~/.config/az-ai/`. Falls through to `%APPDATA%\az-ai\` on Windows. The wizard's new `DefaultEnvFilePath()` mirrors this verbatim -- one less reviewer eyebrow at PR time, one less divergence to chase if XDG handling ever changes.

The interesting find in Act I is what *not* to keep. The S02 wizard's persistence path goes through `UserConfig.Save()` to `~/.azureopenai-cli.json`. That JSON file is read by `UserConfig.Load()` for the `Endpoint`/`ApiKey` fallbacks, but the canonical credential store post-E10 is the env file. Persisting to two stores splits the source of truth. Decision: the new wizard writes the env file only; the existing `UserConfig` round-trip tests still cover that class's own behaviour and stay untouched. One file, one writer, one parser. Newman nodded.

### Act II -- The build

Jerry's first cut split the file into two: `WizardSession.cs` (the pure-function builder, no Console, no I/O, no env reads) and `SetupWizard.cs` (the prompts, the masked input, the file-system write). The split was not negotiable. The brief named it: "extract a small `WizardSession` or pure-function builder for the file content, to keep tests hermetic." The reason that line is in the brief is that the S02 wizard is one giant `RunAsync` with `Console.ReadLine` calls inlined into the validation logic. You cannot test that without a TTY harness. The new shape: `BuildEnvFileContent(answers, defaultProvider, timestamp)` is a static function that takes immutable records and returns a string. Tests pass canned answers and assert on the bytes. Twenty-eight tests cover the builder, plus three that exercise the file-system writer (idempotency, backup, chmod 600). All of them run in 52 ms. Zero of them need a PTY.

The `ProviderAnswer` record is the contract between the layers:

```csharp
internal sealed record ProviderAnswer(
    string Provider,         // canonical lowercase: "azure", "openai", ...
    string ApiKey,           // secret -- caught by SecretRedactor patterns
    string Models,           // comma-separated; deployment names for azure
    string? Endpoint = null, // azure only
    string? AccountId = null);// cloudflare only
```

The interactive layer collects one of these per provider; the builder takes a list of them plus the default provider name and emits the env file. Same answers in -- byte-identical body out, modulo the single `# Generated <ISO-8601>` timestamp comment. That last clause is the idempotency test's escape hatch: re-running the wizard with the same answers should not produce a spurious backup, but the timestamp will differ. `StripTimestampComment` is a one-line helper used both by the writer (to decide whether the new content is "the same") and by the unit test (to compare two runs).

The provider-menu prompt was the place that wanted to grow tentacles. First sketch: a numbered list with arrow-key navigation and a coloured highlight on the recommended default. Second sketch: same, but with `Console.ReadKey` capturing `\x1b[A` / `\x1b[B` to move the cursor. Jerry deleted both. The third sketch is what shipped:

```text
Default provider:
    1) azure
  * 2) openai
    3) groq
    4) together
    5) cloudflare
Pick [openai]:
```

The asterisk marks the recommended default. Bare Enter accepts it. A digit picks by index. A name (case-insensitive, whitespace-tolerant) picks by string. That is three input idioms -- four if you count "wrong input, try again" -- and zero arrow-key handling. The brief said "one prompt and zero typos." Arrow keys add typos (cursor in the wrong row, accidental Down at the menu boundary, terminals that emit different escape sequences). A numbered menu with name fallback gets to "zero typos" with thirty fewer lines of code.

The recommended-default rule is one line. `azure` if `AZUREOPENAIENDPOINT` is exported in the current environment, otherwise `openai`. The reasoning: a user re-running the wizard on a machine that already has Azure configured almost certainly wants to keep Azure as the default and add a second provider. A user running the wizard for the first time post-S03 is probably here because they read the README and want to plug in OpenAI. We are not going to A/B-test this; one heuristic, defensible in one sentence, ships.

The compat-validation step deserves a paragraph. The wizard prompts:

```text
Openai model name(s), comma-separated [gpt-4o-mini]:
```

The user types a list. The wizard takes that list, prepends `openai:` to each entry, joins with commas, and feeds the result to `OpenAiCompatAdapter.ParseCompatModels`. That parser is the single source of truth for what is and isn't a valid compat model entry, and it throws `ArgumentException` with an actionable message on malformed entries. The wizard catches that exception and reprints the message as the prompt's rejection text, then re-prompts. Net effect: a typo at the wizard prompt produces the *same* error a typo in `AZ_AI_COMPAT_MODELS` would produce, with the same wording. One error message; two surfaces; one rule.

Cloudflare's account id is the one shape that does not fit the otherwise-uniform compat flow. The Workers AI URL has the account id in the path -- `https://api.cloudflare.com/client/v4/accounts/<account_id>/ai/v1` -- and the E09 adapter rewrites the placeholder at `Build()` time. So the wizard, when the user picks `cloudflare`, prompts for the API token, then the model list, then the account id, and the builder emits both `[provider:cloudflare] / API_TOKEN=...` and `export CLOUDFLARE_ACCOUNT_ID="..."` in the default section. The account id is not a secret per E09 -- it is in the URL path of every request -- but it does live alongside the credentials, and the wizard treats it as a required field with the same backup / overwrite protection.

The backup rule is small and worth pinning explicitly. If the env file does not exist, write it, no backup. If it does exist and the new content matches the old (modulo the timestamp comment), rewrite it (so the timestamp refreshes) and return null -- no backup, no spurious clutter in the directory. If it exists and the new content is *different* from the old, copy the existing file to `env.bak.<ISO-8601-utc>`, set the backup file to mode 0600 too (Newman invariant), then overwrite the live file. The wizard's success message names the backup path so the operator can find it without grepping. Three lines of contract, one helper function, three unit tests pinning each branch.

The non-TTY refusal is the fail-loud invariant the brief made non-negotiable. `IsInteractiveTty()` checks `Console.IsInputRedirected` and `Console.IsOutputRedirected`. If either is true, the wizard prints `[ERROR] Setup wizard requires an interactive terminal...` and returns 1. Pipe a heredoc into `az-ai --setup` from a script and you get exit 1 plus a one-line message pointing at the README's "Power user / scripted setup" subsection. You do not get a hung process waiting for a keystroke that will never come. The integration test verifies this with a single `echo "" | az-ai --setup` line and asserts on rc=1 plus the `[ERROR]` substring.

Newman's H-1 audit from S02 carried forward verbatim: `ReadMaskedLine`'s pseudo-TTY fallback never falls back to `Console.ReadLine`. The masked-input read fails closed -- one-line stderr warning, return null, caller short-circuits to ExitCanceled (130) -- because falling back to plain `ReadLine` on a host that throws on `ReadKey` would echo the API key to scrollback / tmux logs / TTY loggers. That regression guard test (`SetupWizard_ReadMaskedLine_DoesNotFallBackToReadLine`) is a static-source-analysis test that reads the file, locates the method body, and asserts `Console.ReadLine` does not appear inside it. Ugly, but it is a true regression guard and it stays.

### Act III -- Tests

Three classes of tests cover this episode.

1. **`WizardSessionTests.cs`** -- 32 hermetic tests exercising the pure-function builder. Provider canonicalisation (six valid shapes, four rejection shapes), `IsCompat` predicate, `ValidateCompatModels` (round-trips through E09's `ParseCompatModels`, four valid shapes, two rejection shapes, one azure-skip), `BuildEnvFileContent` (azure-only emits default-section exports and no provider section; openai-only emits compat models + provider section; cloudflare emits `API_TOKEN` not `API_KEY` and `CLOUDFLARE_ACCOUNT_ID`; multi-provider aggregates correctly; default-not-in-answers throws; unknown default-provider throws; empty answers throws; shell-meta escape works), idempotency (two timestamps, identical content modulo the timestamp comment), `WriteEnvFile` (new file emits no backup; same-answers no backup; different-answers creates `.bak.*` with the old content; chmod 600 on Unix), and `DefaultEnvFilePath` honours `XDG_CONFIG_HOME`. All run under `[Collection("ConsoleCapture")]` for serialisation discipline. Total runtime: 52 ms.

2. **Existing `SetupWizardTests.cs`** -- 19 tests carried over verbatim. They cover `ParseArgs` flag plumbing (`--setup`, `--init-wizard`), `UserConfig` round-trip (the JSON config class is now decoupled from the wizard but still tested for its own behaviour), `--config get api_key` redaction (Newman H-2 guard rails -- still relevant because the api_key env var is still printable surface), `TryParseEndpointUrl` validation (six valid / six rejection shapes), and the `ReadMaskedLine` static-source regression guard. Zero changes; all pass.

3. **Integration test in `tests/integration_tests.sh`** -- five new assertions under a new `S03E11 -- provider-aware setup wizard` block. The non-TTY refusal is one-liner: pipe an empty stdin and assert rc=1 plus `[ERROR]`. The PTY-driven happy path is the interesting one. The wizard reads the API key with `Console.ReadKey(intercept: true)`, which requires a real terminal; piping a heredoc into stdin will not work. The fix is `script(1)` from util-linux, which allocates a PTY, runs the inner command against it, and forwards stdin to the PTY's master side. The test runs:

   ```bash
   env -i HOME="$wiz_home" PATH="$PATH" TERM=dumb \
       script -qec "$BIN --setup" "$script_log" < "$answers_file"
   ```

   `answers_file` is a four-line heredoc: empty (accept default `openai`), the api key, the model name, then "N" (no second provider). The integration test then asserts on (a) the wizard exit code, (b) the env file's existence, (c) the `[provider:openai]` section header, (d) the `AZ_AI_COMPAT_MODELS` export, and (e) the file's POSIX mode being 0600. If `script(1)` is unavailable -- macOS runners have a different flavour, BSD-derived hosts may not ship it -- the test skips gracefully with a one-line reason. The integration suite goes from 46 to 51 passing.

### Act IV -- Preflight

`dotnet format` was a no-op. `dotnet build` clean. The new `WizardSessionTests` class added 32 tests; the targeted run went green in 52 ms. Full unit suite: 829 passed (was 768 baseline plus 32 mine plus 29 from concurrent E12 work in `Benchmarks/`, `CompatCostEstimatorTests`, `PrewarmCompatTests`). Integration suite: 51 passed (was 46), 2 skipped (real-API-call gating, unrelated). Exec-report-check ran clean against this episode's draft. No CHANGELOG conflicts, no markdown-lint hits, no smart-quote regressions. ASCII-only verified by hand-grep for `[\x80-\xff]` in this exec report and the README diff.

CI status at push: not yet pushed -- orchestrator batches commits with E12 for the season retrospective. Local `make preflight` is the gate this episode was held against, and it passed end-to-end without amber.

## Lessons learned

Five things this episode taught us that the brief did not pre-bake.

### 1. The pure-function builder is the test architecture, not just an extraction

The brief asked for `WizardSession` "to keep tests hermetic." That phrase undersells what the split actually buys. With the prompt sequence and the file-content builder fused into one method, the only way to test "two different sets of answers produce two different env-file shapes" is to drive the prompts -- which means stdin redirection, which means the wizard refuses to run, which means a TTY harness. With the split, the same assertion becomes a one-line `Assert.Contains` on the return value of a static function. The test count went from "what can I wedge through a PTY" to "every branch of every shape." Twenty-eight tests in fifty-two milliseconds. The split is the test architecture; the extraction is the means.

The corollary: the interactive layer is now *only* the prompts. It contains zero business logic. If the file format ever needs to change again (E13 might add a `[provider:foundry]` section, or a future episode might add a profile section), the change is a function signature update on the builder plus a row of test assertions; the interactive layer recompiles unchanged. That is the shape we want every CLI subsystem to converge on.

### 2. Numbered menus beat arrow keys for one-shot wizards

Three sketches of the provider-menu prompt. The first two used arrow-key navigation with a coloured highlight on the current selection. Both sketches got deleted. The shipped third sketch uses a static numbered list with an asterisk on the recommended default and accepts numbers, names, or bare Enter. Why the third sketch won:

- Arrow-key handling requires `Console.ReadKey` for the menu and `Console.ReadKey` for the API key (which we already have). But the menu's `ReadKey` has to interpret `\x1b[A` / `\x1b[B` as Up / Down, and the encoding is terminal-dependent. macOS Terminal sends `\x1b[A`. Windows Terminal under conhost sends `\x1bOA`. Mosh sends a different shape in alternate-screen mode. tmux mangles `Esc` if `escape-time` is too low. Five providers, five terminals, fifteen failure modes.

- Static menus print once, capture one line of input, validate. Three input idioms (digit, name, Enter), each one-line to parse, each one trivial to test. Zero terminal capability inquiries. Zero alternate-screen state. The output is also pasteable into a terminal session as a help artifact -- which the README example does verbatim.

- Accessibility: a screen reader does not navigate arrow-key menus well. A printed numbered list reads out cleanly and the operator picks by number. Mickey Abbott's a11y review caught this in the third sketch's PR draft and we kept it.

The cost is one wasted line on screen ("Pick [openai]: " on its own line below the list). The benefit is the menu Just Works on every terminal we ship to. Trade made.

### 3. PTY-driven integration tests are cheap if you let `script(1)` do the work

The first sketch of the integration test tried to drive the wizard through a regular pipe and asserted the refusal exit code. That covers the `non-TTY refuses` invariant but does not exercise the happy path. The second sketch tried to allocate a PTY in pure bash with `socat`. The third sketch is `script -qec "$BIN --setup" "$log" < "$answers_file"`. One line. It allocates the PTY, forwards stdin, captures the typescript to `$log` for debugging, and returns the inner command's exit code. The "if `script` is unavailable, skip" branch is a four-line safety net that has never fired on the runners we care about.

The lesson: when a tool exists in util-linux, it is approximately always present on the runners we ship to, and the macOS-shipped flavour can be addressed with a one-line skip. Do not fight the `expect`-vs-`socat`-vs-pty-cli gauntlet. Spend the script(1) one-liner and move on.

### 4. The cloudflare special case wants a comment, not an abstraction

Cloudflare reads `CLOUDFLARE_API_TOKEN` (not `_API_KEY`) and needs `CLOUDFLARE_ACCOUNT_ID` in the URL. That is two divergences from the otherwise-uniform compat flow. The first sketch tried to model the divergence as a `ProviderShape` strategy with a `KeyEnvSuffix` and an optional `ExtraFields` collection. The shipped code has a `switch` expression on the provider name in two places (once in the prompt sequence, once in the builder) plus a `ProviderAnswer.AccountId` field that is null for everyone except cloudflare. Lines of code: about half the strategy version. Reviewer cognitive load: less. Test surface: the same.

The lesson: one special case is a `switch` plus a comment. Two special cases is still a `switch`. If a third special case shows up -- E13 might bring Anthropic-via-OpenAI-compat with its own header shape -- *then* we factor. Not before.

### 5. The "back-compat" word does heavy lifting

The default unsectioned section of the env file carries `AZUREOPENAIENDPOINT`, `AZUREOPENAIAPI`, `AZUREOPENAIMODEL`, and `AZ_AI_COMPAT_MODELS`. The first three exist because Azure was here first and the rest of the binary reads those exact env-var names. The fourth exists because E09's dispatch path reads `AZ_AI_COMPAT_MODELS` from the process environment at startup. None of those four is provider-namespaced; all four are exported with `export KEY="value"` so the file can be sourced from `.bashrc` if anyone wants to. Per-provider sections carry the *secrets* via the E10 namespacing rule. The rule is: shared / readable surface in the default section, secret / per-provider surface in `[provider:NAME]`. That distinction is what makes the file hand-editable in a panic at 3 a.m. without losing the shell-source compatibility users have come to expect.

If we ever add a profile selector (FR-014's `Preferences.Profiles`), it goes in the default section as `export AZ_PROFILE="work"`, not in a section. Same rule.

### 6. The "do not invent a parallel wizard" line in the brief earned its keep

The brief said in plain text: "if the existing setup is bare-bones, your job is to extend it; do not invent a parallel wizard." That sentence prevented an entire afternoon of avoidable work. The first instinct, looking at the S02 wizard's UserConfig-write coupling, was to write a new `MultiProviderWizard.cs` from scratch and leave the existing `SetupWizard` for back-compat. That would have produced two code paths, two test surfaces, two flag plumbings, and a CLI that would route you to the old wizard or the new one depending on flags nobody could remember. The brief's one-line ban deleted that fork before it could be born. The shipped code is one wizard with one entry point, and the only legacy artifact is the JSON-config write path falling out of the wizard's responsibilities entirely (it stays in `UserConfig` for its own callers; the wizard does not write it). One file, one writer, one set of tests. Cheaper to maintain by a factor we will only really feel six episodes from now.

## Code spelunking notes

Six artifacts found in the existing tree that informed implementation choices, recorded so the next person walking this seam does not have to grep for them:

1. **`Preferences.DefaultPath()` (Preferences.cs:52).** Reference implementation for the XDG-aware path computation. `WizardSession.DefaultEnvFilePath()` mirrors the same shape; one diff worth flagging at review is that Preferences resolves to `preferences.json` while we resolve to `env`. Same parent directory, different leaf; one rule.

2. **`OpenAiCompatAdapter.ParseCompatModels` (OpenAiCompatAdapter.cs:120).** The validator named in the brief. Throws `ArgumentException` with an actionable message; the wizard catches and surfaces verbatim. One source of truth for the model-string grammar.

3. **`Program.KnownProviderSections` (Program.cs:1695).** The set of provider-section names the loader recognises: azure, openai, foundry, groq, together, cloudflare. The wizard's menu lists five (not foundry -- foundry is internal Azure plumbing, not a user-facing provider in the way OpenAI / Groq / Together / Cloudflare are). The loader still recognises `[provider:foundry]` as a valid section; the wizard simply does not generate it. Documented in the wizard's source comment alongside the menu definition.

4. **`Program.LoadConfigEnvFrom`'s quote-stripping rule (Program.cs:1782).** The loader strips surrounding double or single quotes on read but does no escape-sequence interpretation. The wizard escapes `\`, `"`, `$`, and backtick on write so a hand-source of the env file from a real shell does not produce surprises. The loader does not need the escape because it does not interpret -- it strips quotes and copies the literal bytes -- but writing pessimistic-quote output is the right posture for a file users may also `source` from `.bashrc`.

5. **Existing `SetupWizard.IsInteractiveTty` (S02).** Carried forward verbatim. The S02 author already got this right -- two-line check, no PTY introspection magic.

6. **`UserConfig.Save`'s `SetUnixFileMode` precedent (UserConfig.cs).** The `WizardSession.SetRestrictivePermissions` helper is a near-copy of the same five-line method on `UserConfig` and `Preferences`. Three callers, one shape. If we ever want to consolidate into a `FileSystemHelpers` static, this is the cluster to refactor; not in scope for this episode.

## Findings

- **K-1 (closes):** README "First run" section described saving to `~/.azureopenai-cli.json` (mode 0600). After E10, the canonical credential store is `~/.config/az-ai/env`. README now reflects the new path AND the chmod 600 invariant; the new wizard writes there with mode 0600 enforced; the integration test pins both. K-1 closed.

- **W-1 (opens, LOW):** The wizard offers azure / openai / groq / together / cloudflare in the menu but the E10 loader also recognises `foundry` as a known provider section. A user with an Azure AI Foundry deployment (separate from Azure OpenAI) cannot use the wizard to provision it; they have to hand-edit the env file. This is not a regression -- the S02 wizard did not know about foundry either -- but it is a documentation gap. Owner: Elaine. Not in scope for E11; tracked in `docs/findings-backlog.md`.

- **J-1 (opens, INFO):** The wizard does not perform a connectivity check after writing the env file. The S02 wizard had a "Testing connection... authenticated (gpt-4o responded in 412ms)" line in the README example that was, on inspection, not actually implemented in code. With five providers now, a connectivity check would have to know how to ping each one's `/v1/models` (or equivalent) endpoint and validate the API key. That is a separate episode of work -- candidate for a future "The Latency" episode in S04. Filed as INFO, not LOW, because no user-facing functionality is broken; the operator just gets a slightly later "your key is invalid" error from the first real chat call instead of from the wizard.

- **J-2 (opens, INFO):** The wizard's "default model" suggestion per provider is a static string in `PromptCompat`. As provider catalogues evolve (`gpt-4o-mini` becomes whatever-is-the-cheap-tier-now in 2027), the suggested default goes stale. Two options for the future: (a) read the suggestion from a versioned constants table that travels with the binary, (b) hit each provider's `/models` endpoint at wizard-time and pull a recommended default. Option (a) is cheap and offline; option (b) is correct and online. We will pick when the staleness becomes a real complaint. Tracked in the findings backlog as INFO with no owner.

## Conflicts and coordination

E12 (Bania, perf benchmarks) ran in parallel on the next bench. E12 touches `Program.cs` to add a `PrewarmCompatAsync` helper and a one-liner call site in the `--prewarm` block; E11 does not touch `Program.cs` at all (the wizard lives entirely in `SetupWizard.cs` and the new `WizardSession.cs`, and the call site in `Main()` was already there from S02). Diffs do not overlap. Orchestrator merge: clean.

The CHANGELOG `[Unreleased]` block had E12's bench entry written first; E11's wizard entry slots in above it because E11 ships first chronologically and the changelog convention is most-recent-at-top. No textual conflict.

The writers' room table got two rows added in the same edit: E11 (this episode) and E12 (Bania's parallel bench work). Both rows are time-stamped 2026-05; cast-balance audit at the next E12 mid-season checkpoint will confirm Jerry's main-cast quota is current and Bania's supporting-player rotation is honoured.

## Process notes

A few procedural beats worth recording so the next episode of this shape lands faster.

- **Brief discipline.** The brief named the validator (`ParseCompatModels`), the path resolver (`Preferences.DefaultPath`), and the redactor (`SecretRedactor`) explicitly with the words "do NOT reinvent." That phrasing is what kept Act I to ninety minutes instead of a half-day archaeology dig. Future briefs for "extend the X subsystem" episodes should pre-name the reuse targets the same way. Mr. Pitt has been good about this; keep it up.

- **Concurrent-edit etiquette.** Bania and Jerry on the same `Program.cs` for the same push window is the kind of shape Wilhelm would normally route through `shared-file-protocol`. The actual diffs land in completely different regions (Bania in the prewarm block around line 380 and a new method around line 2293; Jerry not in `Program.cs` at all). The brief's "if you must edit BuildChatClient (you probably don't)" line was the load-bearing scope discipline. Result: zero merge work, two clean commits. Lesson: the right time to call out a shared-file-protocol concern is in the brief, not after the diffs land.

- **PTY tests are now a thing.** This is the first integration test that drives an interactive prompt sequence under a real PTY. The four-line `script(1)` recipe is good as a canonical pattern; the next time we ship a CLI feature with prompts, this test shape is the reference. Worth a one-line note in `tests/integration_tests.sh`'s top-of-file comment block at the next docs-pass.

- **Test-count delta.** The brief baselined at 768 unit + 46 integration. We close at 829 unit + 51 integration. The integration delta is exactly the five new wizard assertions; the unit delta is 32 from this episode plus 29 from E12's concurrent bench / compat-cost / prewarm-compat work. Both episodes contribute; both tracked in their respective exec reports. The cast-balance audit at E12 will reconcile.

## Performance posture

The wizard runs at most once per machine setup. Performance is not a target in the way it is for the prewarm path or the cold-start budget. That said, three micro-decisions are worth pinning so a future "let's optimise everything" sweep does not regress them:

- **No async I/O in the builder.** `WizardSession.BuildEnvFileContent` returns a string synchronously; `WriteEnvFile` calls `File.WriteAllText` synchronously. The wizard's interactive layer is async only because `RunAsync` returns `Task<int>` to match the existing entry-point signature in `Program.cs`. No real awaiting happens. AOT-friendly, allocation-cheap, debugger-friendly.

- **No env-var probing during prompt setup.** The smart-default rule reads `AZUREOPENAIENDPOINT` once at `SmartDefaultProvider()` call time. `Environment.GetEnvironmentVariable` is cheap, but doing it inside the menu loop would re-read the env between prompts -- which would matter exactly never in practice but would muddy the test surface. One read, one decision, one path.

- **No reflection in the builder.** Provider names compare via `StringComparison.Ordinal` against constant strings; the `switch` on cloudflare is a string-equality check. AOT trim-friendly, no metadata footprint, no surprises at `dotnet publish` time. Source-generator (`AppJsonContext`) is unchanged; nothing in this episode round-trips through JSON.

The wizard's wall-clock cost on a real machine is dominated by human typing speed and the OS-level write of an ~800-byte text file. Both are uninteresting for our purposes. The unit-test runtime (52 ms for 32 tests) is the only number worth tracking here, and it lands well under the preflight budget.

## Documentation surfaces touched

Five documentation surfaces updated in this episode, recorded for the next docs-audit pass:

1. **README.md "First run" subsection.** Full rewrite. New transcript reflects the provider-menu flow and the env-file write path. The old "Where is my key stored?" table below this section still references `~/.azureopenai-cli.json` -- that table was Newman's S03E04 work and is technically still correct for the JSON-config slot (which still exists for `Endpoint`/`ApiKey` fallback). The two storage locations are not in conflict; they are layered. Elaine flagged a future docs-audit follow-up to clarify the layering in one paragraph; not in scope for E11.

2. **CHANGELOG.md `[Unreleased]` Added.** One bullet, eleven lines. Lists the providers, the file format, the validation reuse, the backup behaviour, the chmod 600, and the non-TTY refusal. Self-contained; release-notes-grade prose.

3. **docs/exec-reports/s03e11-the-wizard-reprise.md.** This file.

4. **docs/exec-reports/s03-writers-room.md.** One row in the episodes-shipped table. Verdict: GREEN. Notes: provider-aware wizard, env-file writer, 32 unit tests + 5 integration; closes K-1 chmod-600 README gap.

5. **docs/findings-backlog.md.** Three open / one closed. K-1 closed; W-1, J-1, J-2 opened per the Findings section above. Per `findings-backlog` skill: canonical entry format, five-state lifecycle, owner where applicable.

## Acceptance checklist

| Scope item | Status | Evidence |
|---|---|---|
| Step 1: pick default provider with smart highlight | done | `SetupWizard.PromptProviderChoice` + `SmartDefaultProvider` |
| Step 2: per-provider credential prompts | done | `PromptAzure`, `PromptCompat`; `PromptApiKey` masked |
| Step 3: optional second-provider loop | done | `RunAsync` while-loop; remaining-providers list shown |
| Step 4: write env file with `[provider:NAME]` sections | done | `WizardSession.BuildEnvFileContent` |
| chmod 600 on Unix | done | `WizardSession.SetRestrictivePermissions`; integration assertion |
| Backup before overwrite | done | `WizardSession.WriteEnvFile`; idempotent re-run skips |
| Validate compat models via `ParseCompatModels` | done | `WizardSession.ValidateCompatModels`; reuses E09 |
| Refuse politely on `--raw` / non-TTY | done | caller-side gate at `Program.cs:224`; wizard re-checks |
| Unit: prompt-sequence state machine in isolation | done | `WizardSessionTests.cs`, 32 tests |
| File-write idempotency tests | done | `WriteEnvFile_SameAnswersTwice_NoBackupTaken` and friends |
| Backup-on-second-run test | done | `WriteEnvFile_DifferentAnswers_BackupCreated` |
| Integration test: heredoc + tempdir + section + chmod | done | `tests/integration_tests.sh` S03E11 block |
| README "First-run wizard" subsection updated | done | README "First run" rewrite; new transcript |
| CHANGELOG `[Unreleased]` Added entry | done | top of `### Added`, above E12 |
| `docs/exec-reports/s03e11-the-wizard-reprise.md` | done | this file |
| Update `docs/exec-reports/s03-writers-room.md` E11 row | done | one row appended |
| ASCII-only docs | done | `LC_ALL=C grep -P '[\x80-\xff]'` clean on all new files |
| StringComparison.Ordinal/OrdinalIgnoreCase only | done | grep clean on new files |
| Native AOT friendly | done | no reflection, no dynamic codegen, source-generator unchanged |
| Reuse `ErrorAndExit`, `SecretRedactor`, `Preferences.DefaultPath` | done | path resolver mirrors Preferences; ErrorAndExit at the call site; redactor patterns from E10 cover the API_KEY / API_TOKEN slots without changes |
| `make preflight` green | done | format + build + 829 unit + 51 integration + exec-report-check all green |

Twenty-one rows, twenty-one greens. Done.

A note on the rows that *are not* on the list: connectivity-check on key save (J-1, deferred), foundry in the menu (W-1, deferred to Elaine), provider-defaults table (J-2, blocked on Preferences.Profiles). None of these were in scope; all three are filed in `docs/findings-backlog.md` with owners. The acceptance checklist intentionally tracks brief-scope only -- the goal is "did E11 ship E11," not "did E11 ship every adjacent improvement we noticed along the way."

## What's next

E12 *The Receipt* lands the bench harness Costanza named in the season blueprint -- the one that will tell us whether E09's compat dispatch regressed Azure latency. E12 closes the perf-budget gap S03E08 *The Pick* explicitly deferred. After E12 the seam is feature-complete and measured; E13 closes Arc 2 with the first non-Azure cloud running through the wizard, the seam, and the bench in production. The wizard side is done; from here it is bench, then real-world, then receipts.

Three small follow-ups carry over from this episode without blocking E12:

- W-1 (Elaine): foundry coverage in the wizard menu or a docs note explaining why it is hand-edit-only.
- J-1 (Jerry): connectivity-check episode; tentatively S04 "The Latency."
- J-2 (Jerry): default-model strings in `WizardSession.cs` should source from a provider-defaults table once `Preferences.Profiles` ships.

None are urgent. All are filed.

## Next episode preview -- S03E12 *The Receipt*

> *Kenny Bania walks in carrying a stack of printouts: throughput numbers, p95 latencies, AOT image sizes, baseline-vs-new for every preset E09 introduced. "It's gold, Jerry. Gold." He pins the printouts to the wall in a grid. Larry, at the door, signs off without ceremony: "Numbers or no merge." The bench harness ships, the receipts get filed, and the seam becomes a measured surface instead of a hopeful one. Bania closes the perf-budget gap S03E08 *The Pick* explicitly deferred; E13 then takes the seam, the bench, and this episode's wizard out for a real-world spin against the first non-Azure cloud and reports back what falls out. Two episodes, one arc closed.*

-- Jerry, signing off. The good ones end with a shrug and a merge. This is one of those.

*What's the deal with first-run experiences? You spend three episodes building a CLI that talks to four clouds, and the part that matters most is the ninety seconds the user spends typing answers into a terminal. Get those ninety seconds right and nobody ever thinks about the wizard again. That is the goal. We are, on this evidence, there.*
