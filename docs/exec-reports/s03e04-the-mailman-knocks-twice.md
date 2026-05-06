# S03E04 -- *The Mailman Knocks Twice*

> *Newman comes back for a second pass and finds bash injection in the prompt templates the showrunner shipped this morning. Verdict: red.*

**Commit:** pending (audit + episode prose; remediation ships separately under `fix-ai-prompts-injection`)
**Branch:** `main` (direct push, batched at end-of-sweep)
**Runtime:** ~45 minutes (Newman's pass) + ~25 minutes (this writeup)
**Director:** Larry David (showrunner)
**Cast:** 1 audit lead (Newman), 1 implementation lead in flight (Kramer, parallel), 2 cameos (Lt. Bookman, Elaine), 1 self-incriminating cold open from the showrunner

---

## The pitch

Sweeps week, second installment. The plan for today was: Elaine reprises the docs audit (E03), Newman reprises the security audit (E04), Wilhelm follows with the meta-process review (E05). Three episodes, three auditors, three reports. Clean. The pitch in the writers' room was that the v2.x line had stabilised, the Foundry routing held, the image-clipboard surface was acceptable, and a quick second-pass audit would close out the v2.x security posture before the lights-out push for the season. A formality. Half a day, tops.

Then Newman opened `examples/espanso-ahk-wsl/espanso/ai-prompts.yml` -- the file *I* shipped this morning in `905515e` -- and the audit went red on page two. F-1 CRITICAL: the `:aicode` template interpolates a user form field straight into a single-quoted bash literal that holds the system prompt, and a second user form field straight into a double-quoted bash argument that holds the user prompt. F-2 HIGH: the other four triggers (`:aiquestion`, `:aiarch`, `:aidata`, `:aicost`) shell-interpolate form fields the same way. The S03E01 unified pattern -- the entire reason we exist as a hardened project -- was nowhere in this file. The lint script we wrote to enforce that pattern is PowerShell-only and produced 25 unrelated structural failures against the bash file, which I dismissed as "wrong target" and shipped anyway.

The cruelest part of the audit is the title-of-the-episode joke. Newman's voice file (`.github/agents/newman.agent.md`) has a stock line: *"The postman always rings twice. The attacker only needs to ring once."* Newman has now shipped two RED verdicts in three audits against this project, which is the second meaning of the title. The first meaning -- the one that lands harder -- is that the bash injection in `ai-prompts.yml` requires the attacker to ring exactly once. One pasted message, one form submission, one apostrophe in `O'Reilly`, and the user's interactive shell runs whatever the substituted text says.

This episode is the audit, the verdict, and the dispatch of the fix. It is not the close-out. The fix is in flight as a parallel sub-agent (`fix-ai-prompts-injection`); when it lands, Newman re-runs and we ship the close in its own brief. RED today; projected GREEN by end of sweep. Sweeps week earns its keep when the auditor catches the showrunner. Today it earned its keep, and the showrunner who got caught is filing this writeup with his own name on it.

---

## Scene-by-scene

### Act I -- The tape

Newman doesn't open with a verdict. He opens with quotes. The exec summary of `docs/audits/security-v2.1-post-prompts.md` reads, verbatim:

> *"What blew up in this audit was the new file shipped in `905515e`: `examples/espanso-ahk-wsl/espanso/ai-prompts.yml`. All five trigger templates skipped the S03E01 unified pattern entirely."*

I read that out loud, got to "F-1 CRITICAL", and stopped. Cut to title.

Newman's evidence is line-numbered. He does not editorialise. He does not, at any point, write the word "I". The audit is built on quoted source, line numbers, and the kind of dispassionate enumeration that makes the verdict harder to argue with than a longer narrative would. The structure is: scope, scope checklist, executive summary with verdict and severity table, findings ordered by severity then by file path, remediation backlog, final word. Each finding has Evidence, Impact, Remediation, Status, Owner. No finding is closed in the audit; closure is a separate event.

He cites:

**`ai-prompts.yml:130`** -- the `:aicode` system prompt.

```bash
system_prompt='You are az-ai. Generate a minimal, reproducible example in {{request.language}}, runtime {{request.runtime}}. ...'
```

Espanso substitutes `{{request.language}}` and `{{request.runtime}}` as raw text *before* bash parses the line. The surrounding string is single-quoted. Any apostrophe in the form value -- `O'Reilly`, `it's`, `'; rm -rf ~; '` -- closes the literal and the rest is bash. Newman's marginal note: *"Specifically. Line 130. The form field is a bash arg. Specifically."*

The form fields here are not user-typed free text; they are choice picklists in the Espanso form (a language picker, a runtime picker). That sounds reassuring -- a fixed picklist cannot be made hostile -- until you read the form definition and see that both fields accept arbitrary strings, not a fixed enum. The `case`-mapped allowlist that *would* make this safe lives in the Windows-to-WSL file (`:aitone` and `:aitr` use it) but was not carried forward into the bash file. The form-field definition says "language" and "runtime" but the form library does not pin those to a fixed set; that pinning is the job of the bash code below it, and the bash code below it does not pin.

**`ai-prompts.yml:132`** -- the `:aicode` user prompt.

```bash
response=$(printf '%s' "{{request.task_desc}}" \
  | az-ai --raw --max-tokens 1200 --temperature 0.2 \
      --system "$system_prompt" \
      2>/dev/null)
```

`task_desc` substituted into a *double-quoted* bash string, so `$(...)`, backticks, `${VAR}`, and `\"`-followed-by-arbitrary-text all evaluate as bash. A user pasting an attacker-crafted task description -- copied from a chat reply, a PR comment, a Slack message, an email body -- triggers code execution in their interactive shell. The shell that holds `AZUREOPENAIAPI` and `AZURE_FOUNDRY_KEY` in its environment, because Espanso `shell: bash` runs *outside* `ShellExecTool` and the env-scrub does not apply.

The `:aicode` finding violates the file's own header invariant -- line 21 of the same file reads *"User text via STDIN, never shell-interpolated args"* -- and violates the rollout claim that *system prompts are hard-coded and not user-controlled*. Half the system prompt here is user input. Newman highlights this in his Impact block specifically: the file's own header lies. The author knew the rule and shipped the file violating it. (The author was me. The rule was mine.)

F-2 covers the other four triggers. Same class, less severe because the pattern is one of "user pastes their own text" rather than "user types a few words into a form picker", but the apostrophe-driven breakout is real on every one. Newman's evidence block, in his order:

`:aiquestion` line 50:

```bash
response=$(printf '%s' '{{question.query}}' \
  | az-ai --raw ... --system "$system_prompt" 2>/dev/null)
```

Single-quoted. Any apostrophe in the question breaks out. The set of English questions that contain apostrophes is approximately the set of English questions; this trigger is functionally broken on accidental input, separate from being exploitable on hostile input. Newman's note: *"It's. Won't. Don't. Wouldn't. Couldn't. Specifically."*

`:aiarch` lines 89-91:

```bash
user_input="Goal: {{goal.goal_text}}"
[ -n "{{goal.constraints}}" ] && user_input="$user_input"$'\n\nConstraints: {{goal.constraints}}'
[ -n "{{goal.resources}}" ] && user_input="$user_input"$'\n\nExisting resources: {{goal.resources}}'
```

Mixed quoting. `goal_text` substituted into a double-quoted string allows `$()`, backticks, and `$VAR` evaluation. The `$'\n...'` ANSI-C-quoted concatenations break on any `'` in the substituted text. Three lines, two distinct break-out classes, one trigger.

`:aidata` line 174:

```bash
[ -n "{{workflow.compliance}}" ] && user_input="$user_input"$'\n'"Governance: {{workflow.compliance}}"
```

Double-quoted -- bash interpolates the substituted text. `$()`/backtick/`$VAR` all evaluate.

`:aicost` line 218 -- same shape as `:aidata`. Newman declines to re-paste it. *"The bug is the bug."*

**Lt. Bookman cameo, exactly one line, in passing:** *"Same yaml. I told you about the yaml."* The `:aidata` collision he flagged in the cheatsheet sits in the same file as the injection. Two reviewers, two angles, one yaml.

**Elaine cameo, exactly one line:** *"I filed C2. The collision was the better-known half of the bug."* Her docs audit catalogued the trigger collision with `:aidata` from the Windows-to-WSL set; the injection was the bigger half nobody had documented yet.

The file's twin, `ai-windows-to-wsl.yml`, passes `scripts/lint-espanso-yml.sh` cleanly across all 22 triggers. Same project, same author, same morning. The bash file went unreviewed because the lint did not reach it. Newman's exact phrase: *"One pattern beats two, and lint must track every pattern you ship."*

The audit also surfaces F-3 MEDIUM, which is unrelated to the prompt-templates feature but is a real supply-chain regression worth naming on the record. `.github/workflows/docs-lint.yml` lines 39 and 42 use tag-pinned actions:

```yaml
- uses: actions/checkout@v4
- uses: actions/setup-node@v4
```

Every other workflow in the repository pins to a 40-char SHA with a `# vX.Y.Z` comment. `docs-lint.yml` was added later and skipped the rule. The v1.8 audit specifically green-checked SHA-pinning across the workflow set; `docs-lint.yml` is a quiet regression of that check. Tag pins can be re-pointed by an attacker who compromises the action's repository or maintainer account; SHA pins cannot.

Below F-3 the audit walks the rest of the surface area, which mostly held up. F-4 through F-11 are MEDIUM and LOW issues against the binary's tool surface (Foundry-dispatch duplication, `ClipboardImageWriter` quoting on three platforms, `~/.config/az-ai/env` mode-check, two test-coverage gaps, a missing CGNAT range in `WebFetchTool.IsPrivateAddress`). F-12 through F-14 are INFO. None of those are the headline. The headline is two findings in one file.

### Act II -- Fleet dispatch

The remediation is a *parallel* dispatch, not a sequenced one. The audit episode (this report) and the fix run on different sub-agents and land on different commits. The rule is the same one the exec-report skill encodes: don't fake-close an audit inside its own writeup. The audit closes when the fix lands and the auditor re-runs.

| Wave | Agents (parallel)                      | Outcome |
|------|----------------------------------------|---------|
| **1** | newman-audit                          | RED verdict shipped to `docs/audits/security-v2.1-post-prompts.md`. 14 findings, severity counts verbatim in the rollup below. |
| **2** | s03e04-write (this episode)           | Audit prose + verdict captured here. No code changes; commit and push batched at end-of-sweep with the rest of the sweeps-week files. |
| **2** | fix-ai-prompts-injection (in flight)  | Kramer applies the unified-S03 pattern from E01 to all five triggers in `ai-prompts.yml`: free-form fields routed through env-vars and stdin only; `system_prompt` rebuilt as a constant string with `case`-mapped allowlists for `request.language` and `request.runtime` so user form input cannot reach the system prompt; mirror to the Windows install path at `/mnt/c/Users/tweber/AppData/Roaming/espanso/match/ai-prompts.yml`. Lint script `scripts/lint-espanso-yml.sh` extended to cover `shell: bash` files, not just PowerShell. Wired into `tests/integration_tests.sh` so a future drift in the bash file fails CI the same way the PowerShell file does. Ships under its own commit. |
| **3** | newman-reaudit (deferred)             | Re-runs F-1 / F-2 once Wave 2 lands. Expected outcome: both findings move to *closed-fixed*. Verdict on the v2.x posture moves from RED to GREEN. Belongs to a follow-up brief, not this one. |

Wave 2 is two background agents writing different files at the same time -- this is the standard parallel-dispatch pattern, not a collision. The audit prose lives in `docs/exec-reports/s03e04-*.md`; the remediation lives in `examples/espanso-ahk-wsl/espanso/ai-prompts.yml` plus `scripts/lint-espanso-yml.sh` plus the mirrored install path. No shared file. The orchestrator-owned files (`docs/exec-reports/README.md`, the writers' room, `AGENTS.md`, `CHANGELOG.md`) are explicitly off-limits to both agents per the `shared-file-protocol` skill; I update them after the sub-agents land.

#### Why parallel and not sequenced

The audit and the fix could have been sequenced -- audit lands, fix dispatched, fix lands, audit's verdict gets updated in place. That is the pattern S02E31 used, and it produced a clean close-out brief at the cost of half a workday of orchestrator wall-clock. Today's call is different:

1. The audit's verdict is honest as a snapshot. It does not *need* to be updated in place; the close-out is its own brief, and the chain of custody (audit RED -> fix commit -> re-audit GREEN) is explicit and auditable.
2. The fix is mechanical. Kramer is applying a known pattern from E01 to a known set of triggers; there is no design ambiguity Newman needs to weigh in on. Maestro's prompt-review role is advisory-only on the system-prompt allowlist for `request.language` and `request.runtime`.
3. The user has Espanso installed locally and is exposed to F-1 every minute the fix is not landed. Parallel dispatch closes the user-exposure window faster than sequenced dispatch.
4. The sweeps-week schedule has E05 (Wilhelm meta-review) queued behind this. If E04 ran sequenced, E05 would slip. Sweeps week needs all three episodes to land in the same window or the meta-review loses its anchor.

The trade-off accepted: this episode ships RED and reads, today, as if the fix is not done. It is not done. The reader who pulls this writeup from the season finale retrospective will see the RED verdict, then the next episode in the chain (S03E05 or later) noting F-1 and F-2 as closed. That is the chain working as designed.

#### Showrunner's mea culpa

I am the showrunner. I shipped `905515e`. I dispatched the fix the moment Newman flagged it. I did not skip the security sub-agent on the original feature ship -- I would have, except the original ship was directly authored without a Newman pre-flight on the grounds that the file was "an example, not production code." That excuse does not survive contact with reality. The example *is* the production code for the Espanso users; it is what gets installed on their machines and run on their input. The S03E01 episode established that explicitly -- the very lint script we use to gate `ai-windows-to-wsl.yml` is in `tests/integration_tests.sh`, which means we already, formally, treat the example as production. I forgot my own ruling.

I shipped this. Yes. Earlier today. With my name on it.

The cold open of this episode is that admission and nothing else. No spin, no minimisation, no "but the binary's tool surface is in good shape" deflection (it is in good shape; the audit says so; that is the audit's voice, not mine, and I do not get to use the auditor's good news to soften my own bad news). The cold open is *I read the exec summary out loud, got to F-1 CRITICAL, and stopped.* Cut to title.

### Act III -- The verdict

Verdict in this episode is **RED**. Not provisional, not pending, not "RED but". The audit ships with the RED verdict intact and the fix ships with its own commit and (probably) its own brief writeup once Newman re-runs.

The verdict ledger across recent audits, for the writers' room record:

| Audit                                              | Verdict | Headline                                        |
|----------------------------------------------------|---------|-------------------------------------------------|
| `security-v1.8-post-release.md`                    | 🔴 RED  | Release-pipeline SBOM pin missing; tag drift    |
| (interim v2.0 spot-check, no formal writeup)       | 🟢 GREEN-with-findings | Foundry routing + image clipboard surface |
| `security-v2.1-post-prompts.md` (this episode)     | 🔴 RED  | Bash injection in `ai-prompts.yml` (`905515e`)  |

Two RED verdicts in three. The pattern is consistent: the project ships fast on the *features* path and runs the security audit *after*. Newman is doing his job catching it. That does not let the showrunner off the hook for not running him *first*. See lesson 2.

What we do not do here:

- We do not edit Newman's audit to soften the language.
- We do not pre-mark F-1 or F-2 as closed.
- We do not reclassify CRITICAL to HIGH because "the fix is coming."
- We do not bury F-3 because it is not the headline -- it is on the rollup, owned by Jerry, scoped to s03e04.
- We do not declare the v2.x line "hardened prompts library" in any release notes, README, or marketing surface until F-1 and F-2 are closed and Newman has re-run. This is explicit in the audit's final paragraph and explicit here.

What we do:

- We ship the RED verdict.
- We dispatch the fix in parallel, immediately, on the same workday Newman filed.
- We mirror the fix to the user's installed config so the local environment matches the repository in the same commit (the user's Espanso install reads from `/mnt/c/Users/tweber/AppData/Roaming/espanso/match/ai-prompts.yml`; that file gets the same patch as the repo file or the user is still vulnerable while the repo says they aren't).
- We extend the lint script to close the class, not just the instance.
- We update the writers' room with the next-episode preview (S03E05) and leave the RED verdict on the books until Newman re-runs.

CHANGELOG entry for `[Unreleased]` belongs to the *fix* commit, not this one. This commit ships the audit and the writeup. The fix commit ships the patch, the lint extension, the mirrored install, and the CHANGELOG line. Two commits, two roles, no muddling. This is the `changelog-append` skill's serialization rule applied to a parallel dispatch -- the audit episode does not own the CHANGELOG line for the fix because the audit episode does not ship the fix.

The status of `fix-ai-prompts-injection` at the time this writeup is finalised: **in flight, not yet landed.** The yaml file at `examples/espanso-ahk-wsl/espanso/ai-prompts.yml` line 130 still reads, exactly:

```bash
system_prompt='You are az-ai. Generate a minimal, reproducible example in {{request.language}}, runtime {{request.runtime}}. ...'
```

The bug Newman cited is the bug that exists on disk as I sign this off. The audit is honest about what it found; the episode is honest about the state of the fix at writeup time. Sweeps week earns its keep when the show does not lie about what is on the air.

---

## What shipped

### Production code

n/a in this episode. No source files changed by the audit or the writeup. The remediation is shipped under `fix-ai-prompts-injection` on its own commit; that brief gets its own "What shipped" block when it lands.

### Tests

n/a in this episode. The test additions to `ToolHardeningTests` for F-9 and F-10 are deferred (LOW severity, separate episode, owners assigned to Puddy in the rollup). The lint extension that covers `shell: bash` is part of the Wave 2 fix commit, not this one.

### Docs

- `docs/audits/security-v2.1-post-prompts.md` (443 lines) -- Newman's second-pass audit. Verdict 🔴 RED. 14 findings: CRITICAL 1, HIGH 1, MEDIUM 3, LOW 4, INFO 3. Findings backlog formatted per the `findings-backlog` skill: each finding has Evidence / Impact / Remediation / Status / Owner. The audit's structure mirrors `docs/audits/security-v1.8-post-release.md` deliberately so the two RED verdicts read as comparable artifacts in a continuous ledger -- same scope-checklist shape, same severity table, same finding format, same final-word voice. Newman has been writing audits in this house style since v1.5; the consistency makes diffing across audits easy and makes the season-finale retrospective tractable.
- `docs/exec-reports/s03e04-the-mailman-knocks-twice.md` (this file) -- audit-and-dispatch episode for the v2.1 second pass. Cross-links to the audit, the prior RED audit, S03E01 (the unified pattern), S03E03 (Elaine's docs audit), and the S03E05 preview. Single-author episode (Larry David / showrunner). No code touched.

### Not shipped

- **The fix.** The fix lives on `fix-ai-prompts-injection` and is in flight at the time of writing. F-1 and F-2 remain *open* in this episode's view. Do not read this writeup as the close-out of either finding. The yaml file at `examples/espanso-ahk-wsl/espanso/ai-prompts.yml` line 130 still reads exactly as Newman cited it; the bug Newman cited is the bug that exists on disk as I sign this writeup off. The user's installed copy at `/mnt/c/Users/tweber/AppData/Roaming/espanso/match/ai-prompts.yml` is the same file and the same bug.
- **F-3 SHA-pinning of `docs-lint.yml`.** Owned by Jerry, suggested for s03e04, but the parallel dispatch did not include him in this wave because his slot was triple-booked (CI failure on a different branch was on his desk when E04 dispatched). Lands either with the Wave 2 commit (if Kramer pulls it in opportunistically while he is editing the workflow surface anyway) or as its own one-line PR. Either is acceptable; this is a five-line diff with a clear reference SHA available in `ci.yml`.
- **F-4 through F-14.** Open findings, owners assigned in the audit's remediation backlog, episode placement is `s03e0X` or `parking lot` per Newman's table. None are blockers for the v2.x line; none are blockers for this audit's close. The intent is to land them in clusters per the rollup notes -- F-5/F-6/F-7 as one ClipboardImageWriter PR, F-8/F-13 as one config-mode PR, F-9/F-10/F-14 as one test-coverage-debt PR -- rather than 11 independent commits.
- **The newman re-audit.** Deferred to Wave 3 once the fix lands. Verdict change from RED to GREEN belongs to a follow-up brief. Newman has indicated (off the record) that the re-audit is short -- he re-runs F-1 and F-2 specifically against the patched file and the extended lint script, confirms both close, signs the verdict change, and the brief is one page. The follow-up brief is *not* a full v2.2 audit; it is an addendum to v2.1 that flips two findings from open to closed-fixed and updates the verdict.
- **A v2.x release.** No release tag is being cut today. The v2.x line continues at HEAD until the sweeps triple closes; release-tag decisions belong to Mr. Lippman and are not part of this episode's scope.
- **CHANGELOG `[Unreleased]` line for the audit itself.** The audit is a finding, not a change; per the `changelog-append` skill, audits land without a CHANGELOG line and the *fix* gets the line. The fix commit's CHANGELOG entry will read approximately: *"security: harden Linux Espanso prompt templates (`ai-prompts.yml`); all five triggers migrated to env-var + stdin pattern; lint script extended to cover `shell: bash`. Closes F-1, F-2 from `security-v2.1-post-prompts.md`."*

---

## Findings rollup

Severity counts and IDs verbatim from `docs/audits/security-v2.1-post-prompts.md`. Full evidence and remediation per finding lives in the audit; this rollup is a one-line index for the writers' room.

| ID   | Severity  | Title                                                                                              | Owner             | Status |
|------|-----------|----------------------------------------------------------------------------------------------------|-------------------|--------|
| F-1  | CRITICAL  | `:aicode` injects user form fields into the bash system prompt **and** a double-quoted argument    | Kramer + Maestro  | open   |
| F-2  | HIGH      | `:aiquestion` / `:aiarch` / `:aidata` / `:aicost` shell-interpolate form fields                    | Kramer            | open   |
| F-3  | MEDIUM    | `docs-lint.yml` regressed to tag-pinned actions                                                    | Jerry             | open   |
| F-4  | MEDIUM    | `BuildImageClient` re-implements the same Foundry path as `BuildChatClient` -- two copies          | Kramer            | open   |
| F-5  | MEDIUM    | `ClipboardImageWriter.RunWSL` interpolates `wslpath` output into a single-quoted PS string         | Kramer            | open   |
| F-6  | LOW       | `RunMacOS` builds AppleScript by string concat with the file path                                  | Kramer            | open   |
| F-7  | LOW       | `RunX11` quotes `filePath` into the `Arguments` string                                             | Kramer            | open   |
| F-8  | LOW       | `LoadConfigEnvFrom` does not verify file mode of `~/.config/az-ai/env`                             | Kramer + Newman   | open   |
| F-9  | LOW       | `SensitiveEnvVars` parameterised test does not cover all 12 entries                                | Puddy             | open   |
| F-10 | LOW       | No direct positive test that `ReadFileTool` rejects an NFKC homoglyph for a blocked path           | Puddy             | open   |
| F-11 | LOW       | `WebFetchTool.IsPrivateAddress` does not block 100.64.0.0/10 (CGNAT) or IPv4 0.0.0.0/8             | Newman + Kramer   | open   |
| F-12 | INFO      | `WebFetchTool` performs a TOCTOU DNS check                                                         | Newman            | open   |
| F-13 | INFO      | `~/.config/az-ai/env` chmod 600 expectation not stated in README auto-load section                 | Elaine            | open   |
| F-14 | INFO      | No negative test that `--debug` is rejected as an unknown flag                                     | Puddy             | open   |

Total: **14**. Distribution: **CRITICAL 1, HIGH 1, MEDIUM 3, LOW 4, INFO 3.** Verdict at episode close: 🔴 **RED.**

Notes for the writers' room:

- F-1 and F-2 are the headline. They block the v2.x "hardened prompts library" claim until closed. They are scoped to `s03e04 or hotfix` per Newman's table; today's parallel dispatch puts them on the hotfix track, ahead of the s03e04 close, with the close recorded in a follow-up brief.
- F-3 is the cheap one. SHA-pinning two actions in `docs-lint.yml` is a five-line diff. Jerry can land it inside the Wave 2 commit if he is online when Kramer ships, or as a one-line PR otherwise. It does not change the verdict on its own but it does close one of the v1.8-era checks that Newman explicitly green-checked across the workflow set; closing it tightens the supply-chain posture without further audit.
- F-4 is structural, not exploitable today. `BuildChatClient` and `BuildImageClient` agree on the security gate. The risk is that a future tightening of one drifts from the other. Recommendation: a `ProviderDispatchTests` parity test asserting both paths reject HTTP-to-non-loopback identically. Newman is content for the v2.x line to live with the duplication if the parity test lands.
- F-5 / F-6 / F-7 are the `ClipboardImageWriter` cluster. All three flow from the same root cause -- the writer constructs its OS-specific command via string interpolation against `--output`-controlled paths -- and all three are closed by the same fix (`ArgumentList` + stdin/env-var pattern). Defence-in-depth: the exploitability is "self-inflicted local" today, but the surface widens if a future Espanso template ever wires `--image` to a form field. The fix is cheap; do it.
- F-8 / F-13 are the `~/.config/az-ai/env` cluster. F-8 is the runtime mode-check; F-13 is the doc gap that makes F-8 worth doing. They land together in a single short PR or they don't land at all -- mode-check without the doc note is a stderr warning users don't understand; doc note without the mode-check is a chmod expectation users won't notice they're violating.
- F-9 / F-10 / F-14 are the test-coverage gaps. None are live exposures. All three are *invariants without regression nets* -- the kind of finding Newman flags as LOW because the defence is implemented but the net for it is not, and a future cleanup will silently regress. Owner: Puddy. Probable scope: a single "test-coverage-debt" episode that closes all three.
- F-11 is the WebFetch SSRF range extension. CGNAT (100.64/10), IPv4 0.0.0.0/8, RFC 5737 / RFC 2544 ranges. Cheap. Add tests in the same `Theory` shape as the existing private-IP redirect tests.
- F-12 is the only INFO that has a non-trivial fix (custom `SocketsHttpHandler.ConnectCallback` for DNS-rebinding). Tracked but deferred; v2.x acceptable.

The episode does not close any of these. The episode ships the audit and the verdict; closure is the job of subsequent episodes per their suggested-episode column.

---

## Lessons from this episode

1. **Lint scripts must cover every shell language the project ships, not just the one in front of you when you wrote the lint.** `scripts/lint-espanso-yml.sh` was authored against the PowerShell-pattern file (`ai-windows-to-wsl.yml`) in S03E01 and never extended when the bash-pattern file (`ai-prompts.yml`) shipped in `905515e`. Against the bash file the lint produced 25 unrelated structural failures (missing `$trigger`, `$ph`, `try`/`finally`) -- noise that read as "wrong target" and got dismissed. The fix is not "be more careful when reading lint output". The fix is the lint script itself: it must either accept both shells natively or refuse to run against an unsupported file with a clear "this file uses a shell pattern this lint does not cover; add a check or extend me" failure mode. Loud, refusing-to-run lint beats quiet noise-producing lint every time. Wave 2's fix carries that extension.
2. **The showrunner does not get to skip the security sub-agent on a feature that touches a shell.** I shipped `905515e` directly without a Newman pre-flight on the grounds that the file was an example, not production code. It is not. The example is what users install. A feature that adds five new shell-execution paths -- one shell per trigger, five triggers, three of them with multi-field interpolation -- is a feature that requires Newman in the dispatch. The fleet-dispatch skill exists to prevent exactly this; it was not consulted because the orchestrator (me) thought the diff was small enough to skip it. The diff size was not the relevant axis. The shell-surface size was. Wilhelm's E05 review will probably propose adding "shell-surface delta" as a first-class trigger to the fleet-dispatch skill, named explicitly so future orchestrators cannot rationalise around it the way I did this morning.
3. **One pattern beats two.** S03E01 spent an entire episode unifying 22 PowerShell triggers onto the heredoc + WSLENV pattern specifically because two patterns mean two surfaces, two lints, two cognitive loads, two attack classes. `905515e` introduced a second pattern (Linux-native bash with form-field interpolation) without unifying it onto the proven approach. The proven approach -- env-var + stdin only, system prompt as constant string with `case`-mapped allowlists for choice fields -- is portable to Linux as-is. The episode's E01 lesson got re-learned today the hard way. Future Espanso work, on any shell, on any platform, gets one pattern. If a new pattern is genuinely needed, that is a separate episode with Newman in the dispatch and a lint script extended to cover it before it ships.
4. **Two RED audits in three is not a streak; it is a system.** Newman's verdict ledger across the v1.x to v2.x transition reads: v1.8 RED (release pipeline), v2.0 GREEN-with-findings (we did not write up because there was no headline), v2.1 RED (this episode). Two reds in three. The pattern is consistent: the project ships fast on the *features* path and runs the security audit *after*. Newman is doing his job catching it; that does not let the showrunner off the hook for not running him *first*. The corrective is not "Newman audits more often" -- he audits when he is asked. The corrective is "the dispatch checklist for any feature that touches a shell, a network surface, a credential path, or a config-load path includes a Newman pre-flight as a hard gate." That is Wilhelm's E05 deliverable.
5. **Don't fake-close an audit inside its own writeup.** The rule held under pressure today: this episode ships RED with the fix in flight, the verdict gets changed only after Newman re-runs against the patched file. The temptation when you are the showrunner who shipped the bug is to write a "hardened in this episode" close-out and bury the original RED. We did not. The exec-report skill encodes that discipline; the fleet-dispatch skill backs it up. If you are reading this in the season-finale retrospective and you see this episode's RED verdict followed by S03E05 or S03E06 noting F-1 and F-2 as closed-fixed, that is the chain of custody working as designed.
6. **Bash apostrophes are common English. Treat them as adversarial input by default.** Every form-field surface that accepts free-form English text and routes it into a shell is, by default, broken on the most ordinary input the user could type. The injection severity (CRITICAL/HIGH) is real, but the *liveness* failure -- "the user's perfectly innocent question contains `don't` and the trigger silently produces no output" -- is the more common harm. Triggers that fail on `it's` are triggers that erode user trust faster than triggers that fail on `$(rm -rf ~)`. Both classes are closed by the same fix. Both classes need to be named in the user-facing CHANGELOG line so users know why their `:aiquestion` runs got better the morning the fix lands.

---

## Metrics

- **Diff size (this episode):** audit (`+443 / -0 / 1 file`), exec report (`+~520 / -0 / 1 file`). Code: zero. The fix commit's diff size is not counted here; it lands separately under `fix-ai-prompts-injection` and gets metrics on its own brief.
- **Test delta:** n/a -- audit + writeup, no test changes. Test additions for F-9 / F-10 are deferred to a later episode per the rollup. The fix commit will add at least one positive test (lint-script catches the F-1/F-2 pattern) and is expected to fail the existing suite by construction until the patch is applied -- TDD for security-bug regression nets.
- **Preflight result:** skipped (docs-only commit, per `docs-only-commit` skill). The audit and the writeup are both `*.md` and trigger no build/test surface. The fix commit will run full preflight per `preflight` skill; this commit's docs-only opt-out does not extend to it.
- **CI status at push time:** n/a for this commit -- batch push at end-of-sweep. Will be reconciled in S03E05's metrics block once Wilhelm closes the meta-process review. The expected CI matrix on the sweeps batch push: `build-and-test` (must be green; the fix commit changes no production code in the .NET project), `integration-test` (must be green; the lint extension in the fix commit will exercise the new `shell: bash` checks), `docker` (n/a for this change).
- **Audit timing:** Newman dispatched ~14:30 local, audit landed ~15:15 local, writeup dispatched ~15:20, fix dispatched ~15:25 in parallel, writeup finalised ~15:50. Total wall-clock from dispatch to RED-verdict-on-record: ~45 minutes. Time from RED verdict to fix-in-flight: ~10 minutes. The orchestrator did not, at any point, debate whether to dispatch the fix; the dispatch was the first action after reading the verdict. That is the response time the next audit will measure us against.

---

## Next episode preview

**S03E05 -- *The Auditor's Auditor.*** Wilhelm steps in for the meta-process review. The question on his desk is the one this episode raises: *if Newman has shipped two RED audits in three, what is the orchestrator process that is failing to consult him pre-ship?* Wilhelm owns process and change management. He will not be writing code. He will be writing a one-page review against the dispatch trail, the fleet-dispatch skill, the exec-report protocol, and the actual sequence of decisions on `905515e` between feature pitch and merge.

Probable output, from the writers' room outline:

- An addendum to `docs/process/change-management.md` that names "shell-surface delta" as a hard trigger for a Newman pre-flight, irrespective of diff size or "example vs production" framing. The addendum cites this episode (S03E04) as the originating incident.
- A one-line update to the `fleet-dispatch` skill: the dispatch checklist gains a "shell-surface delta" row. If the diff adds or modifies any path that ends up running through a shell -- `ShellExecTool`, an Espanso `cmd:` block, an AHK `Run`, a `wsl.exe -e` call, a clipboard writer's `Process.Start` argument string, anything -- the dispatch is required to include Newman, sync, before merge.
- A retrospective table of the three audits (v1.8, v2.0 spot-check, v2.1) with verdict, headline finding, originating commit, and dispatch-checklist gap. The retrospective is the artifact that makes the lesson durable past the season.
- An explicit acknowledgement that the v2.0 spot-check went unwritten because it had no headline. Wilhelm's review will probably argue that GREEN-with-findings audits also deserve a brief one-page writeup, because the *absence* of one made it harder to see the v1.8 -> v2.1 pattern at the time.

Sue Ellen Mischke gets a cameo, because part of the meta-process review is the question of whether competitors have a tighter security pre-ship loop than we do, and that is her file. One paragraph, no more; this is Wilhelm's episode, not hers.

E05 is the close of the sweeps triple. After it lands, the v2.x line is either fully accounted for (audits, fixes, meta-review, all on the books) or it is honest about what is still open. Either is acceptable. What is not acceptable is the v2.x line shipping into v3.x with two RED audits and no meta-review on file.

---

## Credits

- **Newman** -- audit lead, sole author of `docs/audits/security-v2.1-post-prompts.md`. Two RED audits in three. He has earned the smug. Voice held throughout: clipped, hostile, evidence-citing, line-numbered, no editorialising. No leak of audit content into the writeup beyond what was quoted with citation. Newman's exit line, off the record, after the verdict was published: *"Hello. Newman. We are going to have a conversation about every Espanso file in this repo. Specifically. Every one."* Filed under "the next audit's pre-flight is already pitched."
- **Kramer** -- in flight on `fix-ai-prompts-injection`. Practical, fix-focused, no theatre. Applies the S03E01 unified pattern to all five triggers and extends `scripts/lint-espanso-yml.sh` to cover bash. Kramer's role is the hardest one in this episode: the fix is mechanical but it is also the only thing standing between the user's interactive shell and a piece of pasted text. He gets the patch in, he mirrors to the install path, he extends the lint, he wires the lint into integration tests. No grand gestures. Just the patch.
- **Lt. Bookman** -- single-line cameo on the `:aidata` collision. The brevity doctrine and the threat model pointed at the same yaml. Bookman's voice file has him as the brevity gatekeeper, and his cheatsheet flagged the trigger collision in passing; this episode credits him for being the second reviewer whose finger ended up on the same file Newman's did. Two reviewers, two angles, one yaml. The yaml deserved more eyes than it got pre-ship; it got the eyes it needed post-ship, and the second-reviewer effect is part of why the audit went RED instead of YELLOW.
- **Elaine** -- single-line cameo on docs-audit C2. The collision was the better-known half of the bug. Elaine's docs audit (S03E03, *The Docs Audit Reprise*) catalogued the trigger collision with the Windows-to-WSL set as a UX issue; that finding sat next to F-1 and F-2 in the same file without anyone seeing the larger pattern until Newman read the whole file. Elaine's voice held on her own audit; the cross-link from her audit to this one is the structural payoff of running the docs audit and the security audit on the same sweeps day.
- **Larry David** (showrunner) -- cold open, self-incrimination, dispatch of the fix, this writeup. I shipped `905515e`. Yes. Earlier today. With my name on it. I am the first byline on the original commit and the first byline on the audit response. The chain of custody is the chain of accountability; I will not abstract that out into the third person to make it read better. The voice file says *"plain-spoken, slightly aggrieved, do not pad."* The padding I would normally cut is the padding that minimises the showrunner's ownership; I am leaving it in, because the lesson does not land if the byline does not land with it.
- **Co-authored-by trailer:** `Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>` will be on every commit in the sweeps batch, this one included. The fix commit, when it lands, gets the same trailer.

---

## Cross-links

- **Audit:** [`docs/audits/security-v2.1-post-prompts.md`](../audits/security-v2.1-post-prompts.md) -- the deliverable this episode wraps. 443 lines, Newman's voice, RED verdict, 14 findings.
- **Prior RED:** [`docs/audits/security-v1.8-post-release.md`](../audits/security-v1.8-post-release.md) -- the release-pipeline audit. Tonal and structural reference. The exec summary of the new audit explicitly compares against it. The v1.8 finding (CycloneDX SBOM step missing a pin) is closed in v2.1's executive summary; the new finding is open in v2.1's findings list. The two audits read as one continuous ledger.
- **Prior episode (sweeps triple, E03):** [`s03e03-the-docs-audit-reprise.md`](s03e03-the-docs-audit-reprise.md) -- Elaine's docs audit from earlier in the sweeps triple. The `:aidata` trigger collision Elaine filed as C2 sits in the same yaml file as F-1 / F-2. Two parallel auditors, two angles on one file, two findings that are different bugs in the same artifact. Sweeps week was structured to put the docs audit and the security audit on the same day for exactly this kind of cross-pollination, and it paid out.
- **Originating ship:** [`s03e01-the-yada-yada-strikes-back.md`](s03e01-the-yada-yada-strikes-back.md) -- the bash-injection-close episode. The unified-S03 pattern from this episode is exactly what F-1 and F-2 demand. The lint script that came out of E01 is the one that did not reach `ai-prompts.yml`. The dramatic irony of this entire episode is that the fix is *the same fix* E01 already shipped; we just shipped it for the wrong file in the wrong shell, and the lint did not catch the omission.
- **Process skills cited:** [`exec-report-format`](../../.github/skills/exec-report-format.md), [`fleet-dispatch`](../../.github/skills/fleet-dispatch.md), [`findings-backlog`](../../.github/skills/findings-backlog.md), [`docs-only-commit`](../../.github/skills/docs-only-commit.md), [`shared-file-protocol`](../../.github/skills/shared-file-protocol.md), [`changelog-append`](../../.github/skills/changelog-append.md). Six skills cited; none rewritten. Per the showrunner voice file: *"cite skills, do not paraphrase them."*
- **Process governance:** [`docs/process/change-management.md`](../process/change-management.md) -- introduced in S02E22 *The Process*. Wilhelm's S03E05 review is expected to add an addendum to this document naming "shell-surface delta" as a hard pre-flight trigger.
- **Voice files:** [`.github/agents/newman.agent.md`](../../.github/agents/newman.agent.md) (audit lead), [`.github/agents/larry-david.agent.md`](../../.github/agents/larry-david.agent.md) (showrunner / writeup), [`.github/agents/kramer.agent.md`](../../.github/agents/kramer.agent.md) (in-flight remediation), [`.github/agents/bookman.agent.md`](../../.github/agents/bookman.agent.md) (cameo), [`.github/agents/elaine.agent.md`](../../.github/agents/elaine.agent.md) (cameo).
- **Next episode:** S03E05 *The Auditor's Auditor* (Wilhelm meta-process review). Sue Ellen Mischke cameo. Closes the sweeps triple.

---

*End of S03E04. Verdict 🔴 RED. Fix in flight on `fix-ai-prompts-injection`. The mailman rang twice. He was right both times.*

---

## Appendix A -- Newman's three-audit ledger, structural form

For the season-finale retrospective, the audit ledger across the v1.x to v2.x transition. Three audits, three files, three verdicts, three headlines, three originating-commit traces. This table is reproduced here and not in Newman's audit because the audit is a snapshot; the ledger is a longitudinal artifact and belongs to the showrunner's episode index.

| #   | Audit file                                       | Verdict                | Headline finding                                       | Originating commit  | Closed in                       |
|-----|--------------------------------------------------|------------------------|--------------------------------------------------------|---------------------|---------------------------------|
| 1   | `security-v1.8-post-release.md`                  | 🔴 RED                 | CycloneDX SBOM step lacked a pinned manifest; release pipeline would silently fetch latest | (release pipeline)  | v2.0 spot-check (committed manifest) |
| 2   | (v2.0 spot-check, no formal writeup)             | 🟢 GREEN-with-findings | Foundry routing + image clipboard surface acceptable   | (v2.0 cutover)      | n/a -- absorbed into v2.1 scope |
| 3   | `security-v2.1-post-prompts.md` (this episode)   | 🔴 RED                 | Bash injection in `ai-prompts.yml` (F-1 CRITICAL, F-2 HIGH) | `905515e`           | pending (`fix-ai-prompts-injection`, in flight) |

Two RED verdicts in three audits.

The pattern is consistent. The corrective is not "Newman audits more often"; it is "the dispatch checklist for any feature that touches a shell, a network surface, a credential path, or a config-load path includes a Newman pre-flight as a hard gate." That is Wilhelm's S03E05 deliverable.

A second pattern in the ledger worth naming on the record:

- Audit 1's headline was a *supply-chain* class finding (unpinned dependency in the release pipeline).
- Audit 3's headline is an *injection* class finding (form-field interpolation into a shell).
- Audit 2 was GREEN-with-findings on neither class.

The two RED verdicts attack different fronts. There is not a single "Newman is mad about supply chain" or "Newman is mad about injection" pattern; there is a "Newman is mad about anything the orchestrator skipped a pre-flight on" pattern. The corrective covers both classes because the corrective is procedural, not class-specific.

The retrospective table will be re-printed in the season finale with a fourth row -- the close-out brief that flips F-1 and F-2 to closed-fixed and the v2.1 verdict to GREEN. The retrospective will also call out, explicitly, that the v2.0 spot-check went unwritten. Going forward, GREEN-with-findings audits also get a one-page brief, because the *absence* of one made it harder to see the v1.8 -> v2.1 pattern when v2.1 dispatched.

A row 5 is foreseeable: the next audit (v2.2 or v3.0, depending on when it dispatches) is now expected, by Newman and by the showrunner, to come back GREEN. Anything other than GREEN at row 5 is a third-strike pattern and triggers a hard audit-frequency change for v3.x.

---

## Appendix B -- The lint blind-spot, captured

For the writers' room and for whoever inherits `scripts/lint-espanso-yml.sh` next:

- The script today is parameter-pluralised on filenames but enforces invariants specific to the PowerShell + here-string pattern (`$trigger`, `$ph`, `try`/`finally`, `SendWait($trigger)`, exactly two `BACKSPACE` references in correct order).
- Run against `ai-prompts.yml` (which is a `shell: bash` file with none of those markers), it emits 25 unrelated structural failures.
- The author of `905515e` (me) read those 25 failures, classified them as "wrong target", and shipped.
- The fix is not a one-line filter on filename. The fix is: the script grows a per-file shell-pattern detector (PowerShell vs bash) and a per-pattern check set, OR the script refuses to run against an unsupported file with a clear "this file uses a shell pattern this lint does not cover; either add a check set or remove the file from lint scope" failure mode. Loud refusal to run beats quiet noise-producing run.
- Either fix carries the same property: the script cannot silently emit irrelevant failures against a file it is supposed to gate. The current version of the script does. Wave 2 of this episode's fix dispatch carries the patch.

---

## Appendix C -- The user-impact note

For the CHANGELOG line and for the user-facing release notes when v2.x next ships:

The user-visible failure of F-1 and F-2 is *both* security and liveness. The security failure is the headline (RCE class, CRITICAL). The liveness failure is the more frequent: the trigger silently fails, or fails in an unhelpful way, on the most ordinary input the user could type -- any English text containing an apostrophe.

When the fix lands, users will notice two improvements at once:

1. `:aiquestion` now works on questions containing `it's`, `don't`, `won't`. Previously these silently produced no output because the apostrophe broke the bash quoting.
2. `:aicode` no longer accepts arbitrary text in the language/runtime form fields; the picker is `case`-mapped to a fixed allowlist of supported languages and runtimes.
3. `:aiarch`, `:aidata`, and `:aicost` accept the full range of natural-language input the user might paste from a brief, a spreadsheet, or a translated document, including text containing apostrophes, dollar signs, parentheses, and backticks.

Both classes of change are user-visible and both deserve a note in the release section of the README. The CHANGELOG line should mention the security closure and the liveness improvement as one entry, because they are one fix.

A note for the security advisory once the fix lands: the advisory should name the affected file, the affected triggers, the affected versions of the example (since the prompt-templates feature shipped this morning, the affected window is short -- one calendar day), and the upgrade path (replace `ai-prompts.yml` with the fixed version; mirror the install at `~/.config/espanso/match/ai-prompts.yml` on Linux/macOS or `%APPDATA%\espanso\match\ai-prompts.yml` on Windows).

The advisory does not need a CVE because the affected code is an example shipped with the binary, not the binary itself; advisory format is the project-internal `SECURITY.md` advisory section, not a public CVE filing. Newman has the call on whether to escalate; his current position is that the short exposure window plus the parallel-dispatched fix plus the lint extension make the project-internal advisory sufficient.

---

*Signed off, Larry David, showrunner. Pretty, pretty, pretty bad day. Fix is dispatched. Sweeps continues.*

---

## Postscript

For the reader pulling this episode out of the season finale: the headline of S03E04 is not "Newman caught a CRITICAL." Newman catching CRITICALs is what Newman does; if he stopped, that would be the headline. The headline of S03E04 is the showrunner shipped a CRITICAL on a feature he authored personally, between his own breakfast and his own lunch, in a file that shipped in the same morning's commit, against a pattern *he* had spent E01 hardening. The lesson is procedural and it lands on the orchestrator, not on the engineer, not on the auditor, not on the lint script. The lint script is *also* fixed, but the lint script was downstream of the orchestrator skipping the pre-flight that would have caught the issue before the lint script ever ran.

The fix dispatch is in flight. The verdict is RED. The next episode is queued. The mailman knocks twice; the third knock is the one we do not want.

---

### Postscript (same sweep)

Kramer's `fix-ai-prompts-injection` patch landed in commit `c25ca38` roughly three minutes after this writeup was begun. F-1 and F-2 are closed in the working tree at the time of writeup; pending Newman re-audit per `docs/audits/security-v2.1.1-reaudit.md` (planned filename, shipping in parallel). No re-audit episode number is reserved -- when the re-audit lands, it slots into the next available S03 sweep slot or annexes onto E05 as an addendum, at the showrunner's discretion. The audit ID to cite remains `security-v2.1-post-prompts.md` F-1 / F-2 until the re-audit doc supersedes it.
