# v2.1 Post-Prompts Security Audit

**Auditor:** Newman (Security Inspector)
**Date:** post-`905515e` (`feat(prompts): Add five canonical task templates with Espanso/AHK integration`), HEAD = `main`
**Scope:** second-pass audit covering everything that has shipped since the v1.8 release-pipeline RED report (`docs/audits/security-v1.8-post-release.md`). Specifically: Foundry routing, image-generation clipboard surface, S03E01 bash-hardening verification on the Windows-to-WSL config, the new `ai-prompts.yml` Linux/bash template surface, tool hardening regression, supply-chain pipeline, and the `~/.config/az-ai/env` auto-load.

---

## Executive Summary

**Verdict: 🔴 RED -- one CRITICAL and one HIGH command-injection class in `examples/espanso-ahk-wsl/espanso/ai-prompts.yml` (commit `905515e`). The v1.8 release-pipeline failure is fixed; the rest of the v2.x posture is solid.**

The pipeline-side problems from v1.8 are gone: CycloneDX is now pinned via a committed `.config/dotnet-tools.json` manifest (`cyclonedx 6.1.1`), the SBOM step in `release.yml` uses `dotnet tool restore` + `dotnet dotnet-CycloneDX`, Trivy is pinned to "the only safe version", OpenSSF Scorecards still runs on `main`, and every action in `ci.yml`/`release.yml`/`scorecards.yml` is SHA-pinned. Tool hardening (`ShellExecTool`, `ReadFileTool`, `WebFetchTool`, `DelegateTaskTool`) holds up under regression: process-substitution, eval/exec, curl write-side, NFKC + sensitive-path block, post-redirect SSRF, and the AsyncLocal depth cap of 3 are all backed by passing tests in `ToolHardeningTests.cs` / `DelegateTaskToolTests.cs`. The `FoundryAuthPolicy` does not log keys, requires HTTPS for non-loopback endpoints, and the model-allowlist gate (`ParseModelEnv`) is enforced before client construction.

What blew up in this audit was the new file shipped in `905515e`: `examples/espanso-ahk-wsl/espanso/ai-prompts.yml`. All five trigger templates (`:aiquestion`, `:aiarch`, `:aicode`, `:aidata`, `:aicost`) skipped the S03E01 unified pattern entirely. Form-field values land directly inside single- and double-quoted bash strings inside the Espanso `cmd:` block. Espanso substitutes `{{form1.X}}` as raw text *before* bash parses, so any apostrophe (single-quote case) or any `$(...)` / backtick / `$VAR` (double-quote case) in the form input is interpreted as bash. **`:aicode` is worse**: the `system_prompt` itself is constructed by interpolating two form fields (`{{request.language}}`, `{{request.runtime}}`) into a single-quoted bash literal, which directly violates the "system prompts hard-coded, not user-controlled" invariant the prompt-template rollout was supposed to honour.

The Windows-to-WSL counterpart (`examples/espanso-ahk-wsl/espanso/ai-windows-to-wsl.yml`) passes `scripts/lint-espanso-yml.sh` cleanly across all 22 triggers and follows the documented S03E01 unified pattern (PowerShell here-string + WSLENV + env-var). The lint script does not check the Linux-only bash file, which is how `905515e` slipped through.

**Bottom line:** the binary's tool surface is in good shape. The Espanso example shipped in `905515e` is an injection liability and should not be installed by users until the templates are rewritten on the WSLENV+stdin pattern. Recommend treating S03E04 (`s03e04-write`) as **partially blocked**: the prose can proceed, but any "secured prompts library" claim must wait on the F-1/F-2 fixes below.

| Severity | Count |
|----------|-------|
| CRITICAL | 1 |
| HIGH     | 1 |
| MEDIUM   | 3 |
| LOW      | 4 |
| INFO     | 3 |

Top three: **F-1** (CRITICAL: `:aicode` system-prompt + `task_desc` shell injection in `ai-prompts.yml`), **F-2** (HIGH: apostrophe-driven shell breakout across the other four prompt triggers), **F-3** (MEDIUM: `docs-lint.yml` regressed to tag-pinned actions).

---

## Scope Checklist

| # | Scope item | Status | Evidence |
|---|------------|--------|----------|
| 1 | Foundry routing surface (env vars, `FoundryAuthPolicy`, `BuildChatClient`) | ✅ | `Program.cs:1750-1803`, `2045-2085`. HTTPS-only with explicit loopback HTTP allowance; api-key set in header, never logged; allowlist intersection enforced before dispatch. |
| 2 | Image generation clipboard shellouts | ⚠️ | `ClipboardImageWriter.cs` -- 3 LOW-severity argument-quoting concerns on `osascript` / `powershell.exe` / `xclip`. Trust path is local, but the patterns are fragile. See F-5/F-6/F-7. |
| 3 | S03E01 bash-hardening verification on `ai-windows-to-wsl.yml` | ✅ | `bash scripts/lint-espanso-yml.sh examples/espanso-ahk-wsl/espanso/ai-windows-to-wsl.yml` -> `ok (22 triggers checked)`. Documented residual surface (multi-line `'@` boundary) acknowledged in header comment lines 102-123. |
| 4 | `ai-prompts.yml` env-var / form-field surface | ❌ | All 5 triggers in `905515e` violate "form fields go via stdin not args" and `:aicode` violates "system prompts hard-coded". `AZURE_IMAGE_MODEL` IS in the scrub list (`ShellExecTool.cs:42`). See F-1, F-2. |
| 5 | Tool hardening regression coverage | ✅ | `ToolHardeningTests.cs` covers `$()`, backticks, `<()`, `>()`, eval, exec, curl write-side, sensitive env-var scrub, NFKC blocklist, post-redirect SSRF; `DelegateTaskToolTests.cs:172-197` covers MaxDepth=3 + reset. Two coverage gaps noted as LOW (F-9, F-10). |
| 6 | Supply-chain / release pipeline | ⚠️ | v1.8 SBOM bug fixed via `.config/dotnet-tools.json` (`cyclonedx 6.1.1`). One regression: `docs-lint.yml` uses tag-pinned actions (F-3). Otherwise SHA pins, least-priv `permissions:`, Trivy + Scorecards intact. |
| 7 | Secrets-in-config surface (`~/.config/az-ai/env`) | ⚠️ | `setup-secrets.sh` chmod 600 by construction; `LoadConfigEnvFrom` does not enforce/check mode (F-8); error path swallows silently (good); no `--debug` env-dump path exists (good). |

Legend: ✅ pass · ⚠️ pass-with-findings · ❌ broken.

---

## Findings

Findings are ordered by severity, then by file path. Format follows
`docs/audits/security-v1.8-post-release.md` and the `findings-backlog`
skill (`.github/skills/findings-backlog.md`).

---

### F-1 -- CRITICAL: `:aicode` template injects user form fields into the bash system prompt **and** into a double-quoted bash argument

**Evidence:**

- `examples/espanso-ahk-wsl/espanso/ai-prompts.yml:130` --
  ```bash
  system_prompt='You are az-ai. Generate a minimal, reproducible example in {{request.language}}, runtime {{request.runtime}}. ...'
  ```
  Espanso substitutes `{{request.language}}` and `{{request.runtime}}` as
  raw text *before* bash parses. The surrounding string is single-quoted,
  so any apostrophe in the form value (`O'Reilly`, `it's`, `'; rm -rf ~; '`)
  closes the literal and the rest is bash code.

- `examples/espanso-ahk-wsl/espanso/ai-prompts.yml:132` --
  ```bash
  response=$(printf '%s' "{{request.task_desc}}" \
    | az-ai --raw --max-tokens 1200 --temperature 0.2 \
        --system "$system_prompt" \
        2>/dev/null)
  ```
  `task_desc` is substituted into a *double-quoted* bash string, so
  `$()`, backticks, `${VAR}`, and `\"`-followed-by-arbitrary-text all
  get evaluated by bash. A user pasting an attacker-crafted task
  description (e.g. from a chat reply, a PR comment, a Slack message)
  triggers code execution in the user's shell context.

**Impact:**

- Code execution as the local user during a routine `:aicode` trigger.
- Violates the file's own header invariant (line 21: *"User text via STDIN, never shell-interpolated args"*).
- Violates the rollout claim that **system prompts are hard-coded and not user-controlled** -- here the system prompt is half user input.
- `AZUREOPENAIAPI` and `AZURE_FOUNDRY_KEY` are reachable from the injected shell because this code runs *outside* `ShellExecTool` (`ShellExecTool`'s env-scrub does not apply to the user's interactive shell -- which is exactly what Espanso `shell: bash` is).

**Remediation (sketch -- to be designed by Kramer/Maestro, not patched here):**

- Adopt the Windows-to-WSL pattern: capture the multi-line free-form field via stdin only; capture single-line free-form via an env-var passed through `WSLENV` (or directly via `env VAR=$value bash -c '... "$VAR" ...'` on Linux).
- Rebuild `system_prompt` so it is a constant string -- map `{{request.language}}` and `{{request.runtime}}` to a fixed allowlist via a bash `case` (mirrors the `switch` choice-mapping used for `:aitone`/`:aitr` in `ai-windows-to-wsl.yml`). Free-form prompts must NEVER be interpolated into the system prompt.
- Extend `scripts/lint-espanso-yml.sh` to also lint `shell: bash` files. Today it only enforces the PowerShell-pattern assertions; against `ai-prompts.yml` it reports 25 unrelated structural failures (missing `$trigger`, `$ph`, `try`/`finally`) but never says anything about `'{{form1.X}}'` or `"{{form1.X}}"` in bash positions. This is the lint blind-spot that let `905515e` ship.

**Status:** open. **Owner:** Kramer (impl) + Maestro (prompt review) + Newman (sign-off).

---

### F-2 -- HIGH: Four other prompt triggers shell-interpolate form fields into single-/double-quoted bash strings

**Evidence:** `examples/espanso-ahk-wsl/espanso/ai-prompts.yml`

- `:aiquestion` line 50:
  ```bash
  response=$(printf '%s' '{{question.query}}' \
    | az-ai --raw ... --system "$system_prompt" 2>/dev/null)
  ```
  Single-quoted; any apostrophe in the question breaks out.
- `:aiarch` lines 89-91:
  ```bash
  user_input="Goal: {{goal.goal_text}}"
  [ -n "{{goal.constraints}}" ] && user_input="$user_input"$'\n\nConstraints: {{goal.constraints}}'
  [ -n "{{goal.resources}}" ] && user_input="$user_input"$'\n\nExisting resources: {{goal.resources}}'
  ```
  Mixed single/double-quoted; `goal_text` substituted into a
  double-quoted string allows `$()`, `` ` ``, `$VAR`. The `$'\n...'`
  ANSI-C-quoted concatenations break on `'`.
- `:aidata` line 174:
  ```bash
  [ -n "{{workflow.compliance}}" ] && user_input="$user_input"$'\n'"Governance: {{workflow.compliance}}"
  ```
  Double-quoted -- bash interpolation of substituted text.
- `:aicost` line 218 -- same pattern as `:aidata`.

**Impact:**

- Same class as F-1 but on user-pasted text (not just configuration form fields). Likelihood of accidental breakage is high (apostrophes in English are ubiquitous), likelihood of malicious exploitation is lower than F-1 because the user is the one typing the form, but a user pasting a cost spreadsheet, an architecture brief, or a translated requirements blob from a third party can trigger code execution.
- Once injection lands, the same env exfil and arbitrary-write surface as F-1 applies.

**Remediation:** Same pattern as F-1. Pipe the *only* free-form field via `printf '%s' "$VAR"` on stdin where `$VAR` was assigned from a heredoc that contains the Espanso substitution and **nothing else**, then `tr -d` any structural characters before downstream use. Better: rewrite all five triggers on the WSLENV-style pattern even on Linux, since `env AZ_AI_PROMPT="$prompt" bash -c '... "$AZ_AI_PROMPT" ...'` is portable across Linux and macOS.

**Status:** open. **Owner:** Kramer.

---

### F-3 -- MEDIUM: `docs-lint.yml` regressed to tag-pinned actions

**Evidence:** `.github/workflows/docs-lint.yml:39, 42`

```yaml
- name: Checkout
  uses: actions/checkout@v4
- name: Setup Node
  uses: actions/setup-node@v4
```

Every other workflow (`ci.yml`, `release.yml`, `scorecards.yml`) pins to a 40-char SHA with a `# vX.Y.Z` comment. The v1.8 audit specifically called this out as ✅ ("All third-party Actions pinned by commit SHA"); `docs-lint.yml` was added later and skipped the rule.

**Impact:** Tag pins can be re-pointed by an attacker who compromises the action's repository or maintainer account; SHA pins cannot. This is exactly the supply-chain class Scorecards' `Pinned-Dependencies` check warns on.

**Remediation:** Repin to the same SHAs used in `ci.yml`:
- `actions/checkout@11bd71901bbe5b1630ceea73d27597364c9af683 # v4.2.2`
- `actions/setup-node@<latest-SHA> # v4.x.y`

**Status:** open. **Owner:** Jerry.

---

### F-4 -- MEDIUM: `BuildImageClient` re-implements the same Foundry path as `BuildChatClient` -- two copies, one drift hazard

**Evidence:** `azureopenai-cli/Program.cs:1750-1803` (chat) and `1977-2042` (image). Both:

- read `AZURE_FOUNDRY_ENDPOINT` / `AZURE_FOUNDRY_KEY`,
- check the same allowlist via `ParseFoundryModels()`,
- gate HTTPS / loopback-HTTP,
- attach `FoundryAuthPolicy` with the same `2024-05-01-preview` API version.

Two near-identical implementations of the same security-critical
gate. Today they agree, but a future patch to one (e.g. tightening
the loopback rule, adding a header sanitiser, swapping the API
version) is unlikely to be mirrored.

**Impact:** No live exposure; this is structural risk. A divergence
on the chat path that is not mirrored on the image path would
silently re-introduce, e.g., HTTP-to-non-loopback, on `--image`.

**Remediation:** Extract a single `BuildFoundryOptions(model, jsonMode)` helper that returns either `(OpenAIClientOptions, effectiveKey)` or null+error, and use it from both call sites. Newman is content for the v2.x line to live with the duplication if it is regression-tested; recommend a `ProviderDispatchTests` parity test that asserts both paths reject HTTP-to-non-loopback identically.

**Status:** open. **Owner:** Kramer.

---

### F-5 -- MEDIUM: `ClipboardImageWriter.RunWSL` interpolates `wslpath` output into a single-quoted PowerShell string

**Evidence:** `azureopenai-cli/Tools/ClipboardImageWriter.cs:117-118`

```csharp
var psCommand = $"Add-Type -AssemblyName System.Windows.Forms; [System.Windows.Forms.Clipboard]::SetImage([System.Drawing.Image]::FromFile('{winPath}'))";
return RunProcess("powershell.exe", $"-NoProfile -Command \"{psCommand}\"", redirectInput: null);
```

`winPath` is the stdout of `wslpath -w "$linuxPath"` (line 226).
`linuxPath` is `outputPath`, which comes from the user-controlled
`--output <path>` CLI flag (`Program.cs:1172, 1923-1927`). If
`outputPath` contains `'` (legal on Linux/WSL filesystems), the
single-quoted PowerShell literal closes early and the remainder is
parsed as PowerShell. The same shape exists for native Windows in
`RunWindows` at line 126-127.

Trust posture: the user passes `--output` themselves, so the
exploitability is "self-inflicted local". **But** this is also the
landing surface if a future Espanso template ever wires `--image`
to a form field, and the `--image` README explicitly recommends
piping the prompt to az-ai. Defence-in-depth fix is cheap.

**Impact:** Local PowerShell execution if `--output` contains `'`.
No remote vector today.

**Remediation:** Don't construct the PowerShell command via string
interpolation. Either (a) read the PNG into stdin and use
`[Windows.Forms.Clipboard]::SetImage([System.Drawing.Image]::FromStream($input))`,
or (b) write the path to a temp env-var (`AZ_AI_IMG`) and reference
it as `$env:AZ_AI_IMG` inside a single-quoted PS string. Same
pattern Kramer used for `:aireply.intent` in S03E01 round-2.

**Status:** open. **Owner:** Kramer.

---

### F-6 -- LOW: `ClipboardImageWriter.RunMacOS` builds AppleScript by string concat with the file path

**Evidence:** `ClipboardImageWriter.cs:73-74`

```csharp
var script = $"set the clipboard to (read (POSIX file \"{filePath}\") as «class PNGf»)";
return RunProcess("osascript", $"-e '{script}'", redirectInput: null);
```

A `"` in `filePath` closes the AppleScript string; a `'` in
`filePath` closes the `-e '<script>'` single-quoted argument. .NET's
`Process.Start` parses `Arguments` itself (no shell), so this is
specifically AppleScript injection / argv corruption, not classic
shell injection. Same `--output`-controlled vector as F-5.

**Impact:** AppleScript injection on macOS if the user passes a
crafted `--output`. Local, self-inflicted.

**Remediation:** Use `osascript`'s `-` (read script from stdin) or
its argument form `osascript -e 'on run argv ... end run' -- "$path"`
where the path is a real argv element rather than embedded in the
script.

**Status:** open. **Owner:** Kramer.

---

### F-7 -- LOW: `ClipboardImageWriter.RunX11` quotes `filePath` into the `Arguments` string

**Evidence:** `ClipboardImageWriter.cs:88`

```csharp
return RunProcess("xclip", $"-selection clipboard -t image/png -i \"{filePath}\"", redirectInput: null);
```

.NET's Process.Start (Linux) parses `Arguments` with a quote-aware
splitter, not via `/bin/sh`, so this isn't classical shell
injection. But a path containing `"` corrupts argv and could either
cause xclip to misbehave or cause `Process` to silently truncate
the path.

**Impact:** Functional bug class, very low security impact.

**Remediation:** Use `ProcessStartInfo.ArgumentList.Add(filePath)`
(no quoting needed -- same pattern as `ShellExecTool.cs:111-112`).
Mirror in `GetWindowsPath` (line 226) and `FindCommand` (line 260)
for consistency.

**Status:** open. **Owner:** Kramer.

---

### F-8 -- LOW: `LoadConfigEnvFrom` does not verify file mode

**Evidence:** `azureopenai-cli/Program.cs:1657-1699`. The function
opens `~/.config/az-ai/env` with `File.ReadAllLines` and parses it
unconditionally. Nothing checks that the file is mode 0600 (only
the writer side, `scripts/setup-secrets.sh:118`, sets it).

A user who copies their `env` file onto a multi-user box with the
default umask gets a world-readable secret. The CLI doesn't warn.

**Impact:** Out-of-tree configuration drift. Won't leak via the CLI
itself, but will leak to any other process running as another
local user.

**Remediation:** On Unix, stat the file and emit a one-line stderr
warning (NOT a hard fail -- that breaks Espanso/AHK paths) when
the file has any group/other read or write bits set, e.g.
`(mode & 0o077) != 0`. Skip on Windows (NTFS ACLs are the
mechanism there). Document the check in `SECURITY.md:726`.

**Status:** open. **Owner:** Kramer + Newman.

---

### F-9 -- LOW: `SensitiveEnvVars` parameterised test does not cover all 12 entries

**Evidence:**
- `azureopenai-cli/Tools/ShellExecTool.cs:33-47` lists 12 vars: `AZURE_OPENAI_API_KEY`, `AZUREOPENAIAPI`, `AZUREOPENAIENDPOINT`, `AZUREOPENAIMODEL`, `AZURE_FOUNDRY_ENDPOINT`, `AZURE_FOUNDRY_KEY`, `AZURE_FOUNDRY_MODELS`, `AZURE_IMAGE_MODEL`, `GITHUB_TOKEN`, `GH_TOKEN`, `OPENAI_API_KEY`, `ANTHROPIC_API_KEY`.
- `tests/AzureOpenAI_CLI.Tests/ToolHardeningTests.cs:427-432` parameterises only 4 (`AZURE_OPENAI_API_KEY`, `GITHUB_TOKEN`, `OPENAI_API_KEY`, `ANTHROPIC_API_KEY`).
- `AZUREOPENAIAPI` is covered separately at line 406 (`ShellExec_ScrubsSensitiveEnvVars`).
- The remaining 7, including `AZURE_IMAGE_MODEL` (which the project README explicitly calls out as scrub-required), have no direct assertion.

**Impact:** A future refactor that drops one of the entries from
`SensitiveEnvVars` (typo, merge conflict, "do we still need this?"
cleanup) will not be caught by tests.

**Remediation:** Replace the hand-listed `[InlineData]` set with a
data source that enumerates `ShellExecTool.SensitiveEnvVars` (make
it `internal` if it isn't already) so the test is auto-extended
on additions.

**Status:** open. **Owner:** Puddy.

---

### F-10 -- LOW: No direct positive test that `ReadFileTool` rejects an NFKC homoglyph for a blocked path

**Evidence:**

- `ReadFileTool.cs:135` calls `rawPath.Normalize(NormalizationForm.FormKC)` before `IsBlockedPath`.
- `tests/AzureOpenAI_CLI.Tests/ToolHardeningTests.cs:312-366` has positive coverage for the literal blocklist (`/var/run/secrets`, `~/.aws`, `~/.azure`, dotenv) but no test that asserts e.g. `~/\uFF0Essh/id_rsa` (fullwidth `.`) is rejected.

**Impact:** The defence is implemented; the regression net for it is
not. Easy to silently regress in a future "simplify path
normalisation" cleanup.

**Remediation:** Add a `[Theory]` against `ReadFileTool.Validate(...)`
with NFKC inputs: `~/\uFF0Essh/id_rsa`, `~/\uFF0E\u0073\u0073\u0068/id_rsa`,
fullwidth `.aws`. Assert all return an `Error: ...` string.

**Status:** open. **Owner:** Puddy.

---

### F-11 -- LOW: `WebFetchTool.IsPrivateAddress` does not block 100.64.0.0/10 (CGNAT) or IPv4 0.0.0.0/8

**Evidence:** `azureopenai-cli/Tools/WebFetchTool.cs:130-171`. Blocks
RFC 1918 (10/8, 172.16/12, 192.168/16), 127/8, 169.254/16, IPv6
loopback, fd00::/8, fe80::/10. Does **not** block:

- 100.64.0.0/10 (RFC 6598 CGNAT) -- carrier-grade NAT inside ISPs and some cloud VPCs.
- 0.0.0.0/8 (RFC 1122 "this network").
- 198.18.0.0/15 (RFC 2544 benchmark).
- 192.0.0.0/24, 192.0.2.0/24, 198.51.100.0/24, 203.0.113.0/24 (RFC 5737 documentation).

**Impact:** SSRF surface narrows but isn't fully closed. CGNAT is
the most realistic vector -- a target deployed inside a CGNAT'd
network can be reached from a cloud worker that itself sits in
CGNAT.

**Remediation:** Add the missing ranges. Cheap. Add tests in the
same `Theory` shape as the existing private-IP redirect tests.

**Status:** open. **Owner:** Newman + Kramer.

---

### F-12 -- INFO: `WebFetchTool` performs a TOCTOU DNS check

**Evidence:** `WebFetchTool.cs:38-54` resolves `uri.Host` and
inspects the addresses, then `HttpClient.GetAsync` re-resolves
the same hostname before connecting. A DNS-rebinding adversary can
serve a public IP for the first lookup and a private IP for the
second.

**Impact:** Real but narrow. The attacker needs to control the
DNS for a hostname the LLM agrees to fetch. Mitigations exist
elsewhere (post-redirect check; Azure egress controls in CI).

**Remediation:** Two options, neither cheap: (a) resolve once,
then connect to the IP literal with a `Host:` header set;
(b) use a custom `SocketsHttpHandler.ConnectCallback` that
re-checks the resolved IP. Tracked as INFO; acceptable for v2.x.

**Status:** open (informational). **Owner:** Newman.

---

### F-13 -- INFO: `~/.config/az-ai/env` is loaded with no mode check at startup, and the README does not state the chmod 600 expectation in the auto-load section

**Evidence:** `README.md:151` documents the auto-load but not the
mode expectation; `SECURITY.md:726` mentions the directory but
not in the same paragraph as the chmod requirement;
`scripts/setup-secrets.sh:118` is the only place chmod 600 is set.
Companion to F-8 -- the doc gap is what makes the mode check
in F-8 worth doing.

**Impact:** Documentation drift; user expectation depends on running
`make setup-secrets` rather than rolling their own.

**Remediation:** One-line addition to `README.md:151` and to the
`Environment Variables` table footnote in `.github/copilot-instructions.md`:
"`~/.config/az-ai/env` should be chmod 600. `make setup-secrets`
sets this for you."

**Status:** open (informational). **Owner:** Elaine.

---

### F-14 -- INFO: `--debug` is not a flag and no env-dump path exists, but the absence is not asserted by tests

**Evidence:** `grep -nE '\-\-debug|DumpEnv|printenv|GetEnvironmentVariables' azureopenai-cli/Program.cs` returns no hits. Good. There is, however, no negative test that asserts `az-ai --debug` exits with an "unknown flag" error rather than silently dumping state.

**Impact:** None today. Listed for the same reason F-9/F-10 are: hardening invariants without regression tests rot.

**Remediation:** A two-line `CliParserTests` assertion that `--debug` returns a non-zero exit and an "unknown" error.

**Status:** open (informational). **Owner:** Puddy.

---

## Remediation Backlog

Findings consolidated for the writers' room. Owners are suggestions; final assignment is Larry David's call.

| ID  | Severity | Title                                                                                            | Suggested Owner             | Suggested Episode |
|-----|----------|--------------------------------------------------------------------------------------------------|------------------------------|-------------------|
| F-1 | CRITICAL | Rewrite `:aicode` to remove form-field interpolation from `system_prompt` and `task_desc`         | Kramer + Maestro             | s03e04 or hotfix  |
| F-2 | HIGH     | Migrate `:aiquestion` / `:aiarch` / `:aidata` / `:aicost` to env-var + stdin pattern             | Kramer                       | s03e04 or hotfix  |
| F-3 | MEDIUM   | SHA-pin actions in `docs-lint.yml`                                                               | Jerry                        | s03e04 (cheap)    |
| F-4 | MEDIUM   | Extract shared `BuildFoundryOptions` helper from chat + image dispatch                            | Kramer                       | s03e0X            |
| F-5 | MEDIUM   | Replace single-quoted PowerShell interpolation in `RunWSL` / `RunWindows` with env-var pattern    | Kramer                       | s03e04            |
| F-6 | LOW      | Replace AppleScript string concat in `RunMacOS` with stdin or argv form                          | Kramer                       | s03e0X            |
| F-7 | LOW      | Switch `ClipboardImageWriter` shellouts to `ArgumentList`                                        | Kramer                       | s03e0X            |
| F-8 | LOW      | Mode check + warning on `~/.config/az-ai/env`                                                    | Kramer + Newman              | s03e0X            |
| F-9 | LOW      | Auto-derive `ShellExec_ScrubsAllSensitiveEnvVars` Theory from `SensitiveEnvVars`                 | Puddy                        | s03e0X            |
| F-10| LOW      | NFKC homoglyph regression test for `ReadFileTool.Validate`                                       | Puddy                        | s03e0X            |
| F-11| LOW      | Extend `WebFetchTool.IsPrivateAddress` to cover CGNAT + RFC 5737 + RFC 2544                       | Newman + Kramer              | s03e0X            |
| F-12| INFO     | DNS-rebinding hardening for `WebFetchTool` (custom `ConnectCallback`)                            | Newman                       | parking lot       |
| F-13| INFO     | Document chmod 600 expectation for `~/.config/az-ai/env` in README                                | Elaine                       | s03e04 (docs)     |
| F-14| INFO     | Negative test that `--debug` is rejected as unknown flag                                         | Puddy                        | s03e0X            |

---

## Final Word

The v2.x binary tool surface is in good shape -- the hardening regression net (`ToolHardeningTests`, `DelegateTaskToolTests`, `ProviderDispatchTests`) holds, the v1.8 SBOM-pin bug is fixed via a committed dotnet-tools manifest, and the workflow pinning + permissions hygiene survived contact with three releases.

What didn't survive contact was the `ai-prompts.yml` ship in `905515e`. The author followed the public Espanso "shell: bash" examples, not the S03E01 unified pattern that lives in the sibling `ai-windows-to-wsl.yml`. The lint script (`scripts/lint-espanso-yml.sh`) was specific to the PowerShell pattern and produced 25 unrelated structural failures against the bash file -- noise that was easy to dismiss as "wrong target". The lesson, in the same key as the original `180d64f` regression: *one pattern beats two, and lint must track every pattern you ship.*

Recommend: hold S03E04 launch claims about a "hardened prompts library" pending F-1 and F-2 fixes; the rest of the report can ship.
