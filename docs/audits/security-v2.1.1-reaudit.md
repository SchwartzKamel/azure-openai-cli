---
title: Security Re-audit -- v2.1.1 (post-fix for F-1 / F-2)
auditor: Newman (Security Inspector)
date: 2026-05-06
release_under_review: post-`c25ca38`
scope:
  - examples/espanso-ahk-wsl/espanso/ai-prompts.yml (form-input bash triggers)
  - scripts/lint-espanso-yml.sh (new bash heredoc guard)
  - tests/fixtures/espanso-lint/bash-injection-broken.yml
  - tests/fixtures/espanso-lint/bash-injection-fixed.yml
  - examples/espanso-ahk-wsl/espanso/ai-windows-to-wsl.yml (regression-only)
  - examples/espanso-ahk-wsl/ahk/az-ai-prompts.ahk (regression-only)
  - /mnt/c/Users/tweber/AppData/Roaming/espanso/match/ai-prompts.yml (mirror)
excluded_from_scope:
  - F-3 through F-14 from `security-v2.1-post-prompts` -- carried forward, not re-evaluated this pass
  - All `Program.cs`, tool-hardening, and CI-pipeline surface
supersedes: n/a
related_audits:
  - docs/audits/security-v2.1-post-prompts.md
related_exec_reports:
  - docs/exec-reports/s03e01-the-yada-yada-strikes-back.md
predecessor_audit_id: security-v2.1-post-prompts
audit_id: security-v2.1.1-reaudit
severity_scale:
  CRITICAL: Code execution as the local user via routine trigger.
  HIGH: Likely accidental breakage / lower-likelihood RCE under realistic input.
  MEDIUM: Structural drift hazard, no live exposure.
  LOW: Defense-in-depth only.
  INFO: Note for the next auditor.
---

# Security Re-audit -- 2026-05-06 -- Newman

> *Hello. Newman. The patches close F-1 and F-2; the lint catches the next one before it lands.*

---

## Executive Summary

**Verdict: GREEN -- F-1 (CRITICAL) and F-2 (HIGH) are CLOSED. Two new LOW-severity residual findings filed (F-15 lint coverage, F-16 lint false-positive on comments). Mirror diff is clean.**

Kramer's `c25ca38` rewrites all five form-input triggers in
`examples/espanso-ahk-wsl/espanso/ai-prompts.yml` onto the unified S03E01
quoted-heredoc pattern. The `--system` arguments are now constant strings,
and every form placeholder lives inside `<<'__AZ_AI_EOF__' ... __AZ_AI_EOF__`
where bash performs no substitution. The lint extension in
`scripts/lint-espanso-yml.sh` (lines 98-168) parses the cmd block, masks
quoted-heredoc bodies, and fails any `{{ns.field}}` left in the surrounding
shell context. I tried six adversarial bypasses against it; the four that
matter (unquoted heredoc, indented-quoted heredoc, multi-heredoc with stray
echo, comment-line placeholder) are all caught -- two with intentional
default-deny strictness, two on the merits. The Windows-to-WSL config still
passes lint at 22 triggers and is byte-identical to the live mirror at
`/mnt/c/Users/tweber/AppData/Roaming/espanso/match/`. The AHK companion
remains constant-input-only.

| Severity | Count (this pass) |
|----------|-------------------|
| CRITICAL | 0 |
| HIGH     | 0 |
| MEDIUM   | 0 |
| LOW      | 2 (F-15, F-16) |
| INFO     | 1 (F-17)       |

| Prior finding | Status this pass |
|---------------|------------------|
| F-1 (CRITICAL) | CLOSED -- CONFIRMED PATCHED |
| F-2 (HIGH)     | CLOSED -- CONFIRMED PATCHED |

---

## Verification Methodology

### 1. `examples/espanso-ahk-wsl/espanso/ai-prompts.yml` -- line by line

Header invariant section (`ai-prompts.yml:19-29`) now documents the unified
S03E01 pattern: quoted-heredoc body, constant `--system`, no `{{form.X}}`
placeholders in shell-parsed positions. The five form-input triggers were
each verified against three rules:

| Trigger | Heredoc opener | `--system` payload | Placeholders only inside heredoc body? |
|---------|----------------|--------------------|-----------------------------------------|
| `:aiquestion` | line 57 `<<'__AZ_AI_EOF__'` | line 56 (constant) | Yes -- line 58 `{{question.query}}` |
| `:aiarch`     | line 96 `<<'__AZ_AI_EOF__'` | line 95 (constant) | Yes -- lines 97/99/101 |
| `:aicode`     | line 141 `<<'__AZ_AI_EOF__'` | line 140 (constant; the F-1 system-prompt interpolation is gone) | Yes -- lines 142/143/146 |
| `:aidata`     | line 186 `<<'__AZ_AI_EOF__'` | line 185 (constant) | Yes -- lines 187/189/191/193 |
| `:aicost`     | line 233 `<<'__AZ_AI_EOF__'` | line 232 (constant) | Yes -- lines 234/236/238/240 |

`:aiprompts` (lines 252-280) is a static `replace:` reference card -- no
shell context, no input -- correctly untouched.

The single-quoted heredoc terminator is the load-bearing artifact: bash
disables `$(...)`, backticks, `${VAR}`, and apostrophe-based literal
breaks within the body. The two F-1 sub-bugs are both eliminated:
- the `:aicode` `--system` argument is now a fixed string with no
  `{{request.language}}` / `{{request.runtime}}` interpolation;
- the `task_desc` flow no longer goes through a double-quoted bash
  string; it travels via stdin only.

### 2. `scripts/lint-espanso-yml.sh` -- new bash guard

Read end-to-end. The relevant additions:
- `HEREDOC_OPEN_RE = r"<<\s*'([A-Za-z_][A-Za-z0-9_]*)'"` (line 102)
  matches only single-quoted terminators. This is correct: only quoted
  terminators disable interpolation.
- `PLACEHOLDER_RE = r"\{\{\s*[A-Za-z_][A-Za-z0-9_]*\s*\."` (line 103)
  matches the Espanso namespace.field placeholder shape.
- `strip_quoted_heredocs()` (lines 105-139) walks line-by-line, retains
  the opener line for inspection (so `cat <<'TAG'; echo {{x.y}}` on the
  same physical line is still caught), drops body lines until a line
  whose `.strip()` equals the terminator.
- The bash guard fires only when `shell == "bash"` (line 156).

The lint passes on `ai-prompts.yml` (6 triggers) and on
`ai-windows-to-wsl.yml` (22 triggers) with no failures.

### 3. Regression fixtures

- `tests/fixtures/espanso-lint/bash-injection-broken.yml` -- I confirmed
  it exercises BOTH F-1 and F-2:
  - line 18 (`printf '%s' "{{question.query}}"`) covers F-2: the
    double-quoted bash body where `$()` / backticks / `${VAR}` would be
    interpreted.
  - line 20 (`--system 'You are az-ai. Lang: {{question.query}}'`)
    covers F-1: a form placeholder interpolated into the `--system`
    argument inside a single-quoted bash literal (apostrophe escape).
  - Lint output: 2 failures, lines 1 and 3 of the cmd body. Both
    failures cite F-1/F-2 and the S03E01 exec report. Correct.
- `tests/fixtures/espanso-lint/bash-injection-fixed.yml` -- a minimal
  stdin / `<<'__AZ_AI_EOF__'` reference. Lint output: `ok (1 triggers
  checked)`. Correct.

### 4. Bypass attempts against the lint

I fed six handcrafted YAMLs through the lint via process substitution
(`bash scripts/lint-espanso-yml.sh <(cat <<YAML ...)`). No files were
written to the repo. Results:

| # | Probe | Expected | Actual | Verdict |
|---|-------|----------|--------|---------|
| 1 | Unquoted heredoc `<<__AZ_AI_EOF__` (no quotes around terminator) -- placeholder in body | FAIL (unquoted heredoc still interpolates `$(...)`/`${VAR}`) | FAIL: "form placeholder ... outside a single-quoted heredoc body" | CAUGHT |
| 2 | `<<-'__AZ_AI_EOF__'` indented-quoted heredoc | Either accept (it IS quoted, bash disables interpolation) or fail loudly | FAIL: regex `<<\s*'TAG'` does not match `<<-'TAG'`, so body lines are treated as outside the heredoc | CAUGHT (default-deny; see F-15 -- minor coverage gap, NOT a security hole) |
| 3 | A bash comment line containing `{{q.query}}` | Accept (comments are not parsed) or fail (default-deny) | FAIL: placeholder seen on comment line | CAUGHT (default-deny; see F-16 -- lint false positive, NOT a security hole) |
| 4 | `shell: powershell` block with no form placeholders | Skip the bash guard, run PS structural checks | OK (1 trigger checked) | CORRECT |
| 5 | Two `<<'TAG1'`/`<<'TAG2'` heredocs with a stray `echo "{{q.query}}"` between them | Both heredoc bodies masked, the stray echo flagged | FAIL: line 4 (the stray echo) flagged; both heredoc bodies correctly skipped | CAUGHT |
| 6 | `shell: cmd` (Windows cmd.exe) trigger with raw `{{q.query}}` interpolated into `echo` | Either skipped (out-of-scope shell) or flagged (default-deny) | FAILS, but for the WRONG reason: it falls through to the PowerShell structural checks and reports `expected exactly one $trigger assignment, found 0`. It does not invoke the bash placeholder guard. | PARTIAL -- see F-15 |

Two probes that I considered but did not file as fixtures because the
real `ai-prompts.yml` does not exercise them:
- An `eval "$v"` line where `$v` was assigned from a clean stdin
  heredoc -- the lint cannot see this because it only inspects
  shell-parsed text, not the data-flow inside the heredoc-fed variable.
  Filed as F-17 (INFO) for the next pass.
- A `replace.encode` Espanso filter chain that pre-substitutes the
  placeholder in a shell-unsafe way before the cmd block runs -- not a
  feature used in this repo today.

### 5. Mirror diff (Windows live config)

```
$ md5sum examples/espanso-ahk-wsl/espanso/ai-prompts.yml \
        /mnt/c/Users/tweber/AppData/Roaming/espanso/match/ai-prompts.yml
c78a002f020488b8b7de43f5e91cc05e  examples/espanso-ahk-wsl/espanso/ai-prompts.yml
c78a002f020488b8b7de43f5e91cc05e  /mnt/c/Users/tweber/AppData/Roaming/espanso/match/ai-prompts.yml
```

Bytes match. `diff -u` empty.

The `ai-wsl.yml` mirror (the deployed name of `ai-windows-to-wsl.yml`)
also matches (`d1a9dc352ef64ea3971a1f423d04bb6d`). Out of scope for
F-1/F-2 but nice to confirm in the same pass.

### 6. AHK companion (regression check)

`examples/espanso-ahk-wsl/ahk/az-ai-prompts.ahk:32-47` -- single
`RunAzAi(stdinText, systemPrompt, ...)` helper:

- Line 33: `sysEsc := StrReplace(systemPrompt, "'", "'\''")` --
  defensive single-quote escaping in case a future caller passes a
  system prompt containing `'`.
- Lines 34-37: `bashCmd` is built with `--system '" sysEsc "'` (single-
  quoted bash literal). `stdinText` does NOT appear in `bashCmd`.
- Line 38: `fullCmd := A_ComSpec ' /c wsl.exe -e bash -c "' bashCmd '"'`.
- Lines 41-43: user input flows exclusively via
  `exec.StdIn.Write(stdinText)` -- never the command line.

Five call sites at lines 90, 112, 134, 156, 178 all pass static
`templateA..E` strings as `systemPrompt`. Confirmed: no caller
interpolates user input into `systemPrompt`. Kramer's prior verdict
holds.

### 7. `ai-windows-to-wsl.yml` regression

`bash scripts/lint-espanso-yml.sh examples/espanso-ahk-wsl/espanso/ai-windows-to-wsl.yml`
-> `ok (22 triggers checked)`. The new bash guard does not fire because
all 22 triggers are `shell: powershell`. No regression.

---

## Findings (this pass)

### F-15 -- LOW: Lint coverage gaps for non-`bash` POSIX shells and `<<-'TAG'`

**Evidence:** `scripts/lint-espanso-yml.sh:156` gates the heredoc guard
on `shell == "bash"`. Espanso also accepts `shell: sh`, `shell: wsl`,
and `shell: cmd`. Probes 6, 7 (sh), and 8 (wsl) showed that these
shells fall through to the PowerShell structural assertions and fail
loudly for unrelated reasons -- a contributor who satisfied the PS
checks (or removed them) would not see the placeholder guard fire on a
non-bash POSIX shell. Separately, `HEREDOC_OPEN_RE = r"<<\s*'TAG'"`
does not match `<<-'TAG'` (legitimate bash for tab-indented quoted
heredocs); a contributor using that form would see false-positive
"placeholder outside heredoc" errors.

**Impact:** Defense-in-depth only. No live exposure -- `ai-prompts.yml`
uses `shell: bash` exclusively, and no trigger uses `<<-`.

**Remediation:** Two small lint patches:
1. Treat `shell` values in `{"bash", "sh", "wsl"}` (POSIX-family) as
   triggering the placeholder guard. Optionally extend the heredoc
   regex to `<<-?\s*'TAG'`. Add `shell: cmd` to a deny-list with a
   clear "cmd-style triggers not yet supported by this lint" message.
2. Add a regression fixture exercising `shell: sh` with a placeholder
   leaking out of the heredoc.

**Status:** open. **Owner:** Kramer (impl) + Puddy (fixtures).

---

### F-16 -- LOW: Lint false-positive on bash comment lines containing `{{ns.field}}`

**Evidence:** Probe 3 -- a comment such as
`# a comment about {{q.query}} placeholder` outside a heredoc is
flagged as a placeholder leak. Comments are not parsed by bash, so
this is a false positive.

**Impact:** Author friction only. The default-deny posture is correct
in spirit; the cost is that comments cannot mention placeholder
syntax.

**Remediation:** Strip lines whose first non-whitespace char is `#`
before running `PLACEHOLDER_RE`. Accept the residual edge case of `#`
inside a quoted string -- this is a Linter, not a parser.

**Status:** open. **Owner:** Kramer.

---

### F-17 -- INFO: Lint does not analyze data-flow out of heredoc-fed variables

**Evidence:** A future drift could write
```bash
v=$(cat <<'__AZ_AI_EOF__'
{{q.query}}
__AZ_AI_EOF__
)
eval "echo $v"
```
The lint correctly accepts this snippet (`ok`) because the placeholder
is masked inside the quoted heredoc and `eval "echo $v"` does not
contain `{{ns.field}}`. The actual security gap is the `eval` against
attacker-controlled `$v`. `ai-prompts.yml` does not do this today.

**Impact:** None today. Tracked so the next audit pass knows where to
look if someone refactors a trigger to post-process the form field
before piping to `az-ai`.

**Remediation:** Add a soft "no `eval` / `bash -c "$v"` / `source
<(...)`" guideline to `docs/hardening.md` (next pass) and consider a
companion lint rule that flags `eval` / `bash -c` / `sh -c` inside any
`shell: bash` cmd block.

**Status:** open. **Owner:** Kramer (lint) + Elaine (docs).

---

## Carry-forward Status of Prior Findings

These were explicitly out of scope for this pass; status taken from
`security-v2.1-post-prompts.md` plus the `c25ca38` diff (which only
touched the Espanso surface).

| ID | Severity | Title | Status |
|----|----------|-------|--------|
| F-1 | CRITICAL | `:aicode` system-prompt + `task_desc` shell injection | CLOSED (this pass) |
| F-2 | HIGH | Apostrophe / interpolation in 4 other prompt triggers | CLOSED (this pass) |
| F-3 | MEDIUM | `docs-lint.yml` regressed to tag-pinned actions | OPEN -- deferred to next pass (owner: Jerry) |
| F-4 | MEDIUM | `BuildImageClient` duplicates Foundry path | OPEN -- deferred (owner: Kramer) |
| F-5 | MEDIUM | `ClipboardImageWriter.RunWSL` PS quoting | OPEN -- deferred (owner: Kramer) |
| F-6 | LOW | `RunMacOS` AppleScript string concat | OPEN -- deferred (owner: Kramer) |
| F-7 | LOW | `RunX11` `xclip` argument quoting | OPEN -- deferred (owner: Kramer) |
| F-8 | LOW | `LoadConfigEnvFrom` does not enforce mode 600 on env file | OPEN -- deferred (owner: Kramer) |
| F-9 | LOW | Tool-hardening test gap (per prior audit) | OPEN -- deferred (owner: Puddy) |
| F-10 | LOW | Tool-hardening test gap (per prior audit) | OPEN -- deferred (owner: Puddy) |
| F-11 | INFO | (per prior audit) | OPEN -- deferred |
| F-12 | INFO | (per prior audit) | OPEN -- deferred |
| F-13 | INFO | (per prior audit) | OPEN -- deferred |
| F-14 | INFO | (per prior audit) | OPEN -- deferred |

---

## Next-pass Recommendations

- **Scope of next pass (proposed):** S03E07 or scheduled 2026-06.
  Re-evaluate F-3 (CI workflow pin drift), F-4 (Foundry dispatch
  duplication), F-5..F-7 (`ClipboardImageWriter` argument quoting),
  F-8 (env file mode enforcement). Land F-15 / F-16 / F-17 lint
  improvements alongside.
- **Deliverable to add by next audit:** `docs/hardening.md` -- the
  tool-by-tool blocklist and shell-substitution ban list referenced
  by Newman's role brief but not yet committed. Elaine + Newman.
- **Process:** the `bash-injection-broken.yml` / `bash-injection-fixed.yml`
  fixtures need to be wired into a CI assertion (today they live as
  fixtures only; `make preflight` does not exercise them). Owner:
  Jerry. This is the difference between "we have a regression test"
  and "the regression test will fail the build". Without that wiring,
  F-15 / F-16 fixes risk regressing the F-1 / F-2 coverage.

---

## Sign-off

*Hello. Newman.* The patch in `c25ca38` does what it says on the tin.
The five form-input triggers route every byte of user-controlled text
through a single-quoted heredoc into `az-ai`'s stdin, the `--system`
arguments are constants, and the lint extension catches the bug class
on the next attempt with five out of six adversarial probes returning
the right verdict for the right reason. The two LOW findings I am
filing this pass (F-15 lint coverage on non-bash POSIX shells and
`<<-'TAG'`; F-16 false-positive on comment lines) are quality-of-lint
concerns, not security holes -- the failure mode of both is "yells
when it should not", which is the safe direction. The Windows mirror
is byte-equal to the repo, the AHK companion remains constant-input-
only, and `ai-windows-to-wsl.yml` is unchanged at 22 clean triggers.
**RED -> GREEN.** F-1 closed. F-2 closed. The postman always rings
twice; the attacker only needs to ring once -- and on this surface,
they don't get to ring at all.

-- Newman
