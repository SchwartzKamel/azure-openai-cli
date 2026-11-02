# S04E15 -- *The Denylist*

> *A guard that checks the first word of a command is a guard against typing the command correctly.*

**Commit:** `(this commit)` -- one code commit plus the episode report
**Branch:** `fix/s04e15-the-denylist` (PR, off `main`)
**Runtime:** ~3 hours wall-clock, in a separate jj workspace
**Director:** Larry David (showrunner seat, filled by a single orchestrator)
**Lead:** Newman (security inspector)
**Cast:** 0 sub-agents, inline passes by one orchestrator. Newman (LEAD), FDR (co-lead: filed the finding), Kramer (the scanner), Puddy (the test lists), Rabbi Kirschbaum (guest: what the guard promises, and to whom)

## The pitch

`fdr-2026-11-F-1` said the agent's `shell_exec` tool could be bypassed with `bash -c '...'`:
`sh` and `bash` are not on the blocklist, and the blocklist only checks the first word of each
segment. Verified by probe, filed MEDIUM, open since S04E13.

The finding was a single example of a general problem, and nobody had measured how general.

## Scene-by-scene

### Act I -- Measure before fixing

A throwaway test ran 32 variations of "delete this temp file" through the *current* guard, each
against a file it created, and recorded whether the file survived. A deleted file is a bypass.

**30 of 32 worked.** Only the literal `rm file` was stopped. The list included `\rm`, `"rm"`,
`r'm'`, `env rm`, `(rm x)`, `then rm x`, `timeout 5 rm`, `echo x | xargs rm`, `find x -delete`,
`rm${IFS}x`, `a=rm; $a x`, `true && exec rm x` and `echo 'rm x' | sh`. The two that did not
delete anything only failed because `/bin/sh` on this machine is dash, which lacks `<<<` and
`$'..'`. Under bash they run. The finding had named one of thirty.

### Act II -- Why a patch will not do

The guard's model is "the command is the first word". Every bypass is a way to make the command
not the first word, or to make the first word not look like its name, and there are more of those
than anyone will list. So the fix changes the model: **read the line the way the shell reads it,
then ask what runs.**

`ShellCommandScanner` is a small tokenizer, not a regex. It removes quotes and backslashes as the
shell does (so `\rm`, `r'm'` and `"rm"` are `rm`), finds every command position across
`; | & ( )` and newlines, skips reserved words, looks through wrappers (`env`, `nohup`, `nice`,
`timeout`, `xargs`, `command`, `time`, `busybox`, `flock`, ...), recurses into the payload of
`sh -c`, treats an interpreter reading stdin as the hidden command it is, checks `find -exec`,
refuses `find -delete`, and refuses a command name it cannot see through. The old checks stay in
front of it: they over-block on purpose (a tab or a quoted `;` counts as a separator), and none
of their error messages changed.

### Act III -- The bypass I built

The same probe, rerun against the new code, left **one** bypass: `rm${IFS}/tmp/x`.

My tokenizer reset a word's "dynamic" flag at a `/`, so that `$HOME/bin/tool` would be allowed:
only the directory is a variable. But in `rm${IFS}/tmp/x` the slash comes *after* the expansion,
so the reset made the word look harmless while the shell splits it into `rm /tmp/x`. In fact no
variant of that shortcut is safe: any unquoted expansion can word-split (`X='rm '; $X/tmp/y`).

Removing it also fixed a second case the tests found (`{rm,/tmp/x}`, a brace list with a slash
inside), which came from the same assumption. The price is that `$HOME/bin/tool` is now refused as
a command. That is stated in the code, in a test, and in `SECURITY.md`.

### Act IV -- FDR and Rabbi on what this is

- *"A denylist is never complete."* Agreed, and said. Whatever is not seen through is refused,
  but a script file the agent ran, a general-purpose interpreter (`python -c`, `perl -e`,
  `node -e`, `awk 'BEGIN{system()}'`) and a git alias are not inspected. Blocking those would
  break legitimate work, and no denylist closes them. `SECURITY.md` now says the control that
  does is a container or a restricted user.
- *"You will block good commands."* That is what the 46-command allowed list is for: `bash -c
  'ls | head'`, `timeout 5 git status`, `env | sort`, `find . -name x -exec grep ...`, `awk
  '{print $1}'`. A mutation that makes the scanner block everything turns 54 tests red.
- Rabbi's question: *who is protected?* The collaborator whose history or files an agent could
  destroy after reading an instruction planted in a repository. The promise is "the agent will not
  casually run these commands", not "the agent cannot harm the machine".

## What shipped

**Production code**

- `Tools/ShellCommandScanner.cs` (new): the tokenizer and the scan.
- `Tools/ShellExecTool.cs`: one shared `IsBlockedName`; the scan runs after the legacy checks;
  the blocklist gains `doas`, `pkexec`, `shred`, `unlink`, `wipefs`, `mkswap` and the `mkfs.*` family.

**Tests**

- `ShellCommandScannerTests.cs`: 129 tests: 58 blocked forms, 46 ordinary commands that must run,
  the fail-closed limits (variable command names, unbalanced quotes, deep nesting), tokenizer
  behaviour, and end-to-end cases where the victim file must survive. **Mutation-checked:** 10
  mutations, each turned tests red, and the revert was clean.
- No existing test changed. Unit 2,017 to 2,146. Integration 163, unchanged.

**Docs**

- `SECURITY.md`: the structural scan, the new names, and the limit of a denylist.
- CHANGELOG (Security), the findings backlog.

**Not shipped**

- **Containment.** A sandbox for the shell tool is the real answer and is out of scope here.
- **The bare-token redaction work** (`newman-2026-11-N-3`, `-N-7`) is still open.

## Lessons from this episode

1. **Measure the finding before fixing it.** One reported bypass was thirty. The severity and the
   design both depended on the number.
2. **When every bypass is "make it not the first word", change the model, not the list.**
3. **Rerun the same probe on the fix.** It found the bypass I had just introduced.
4. **A shortcut for the common case is a bypass for the uncommon one.** The slash reset existed
   to allow `$HOME/bin/tool`. Say what you give up instead.
5. **Keep an allowed list as long as the blocked one.** Without it, "block everything" passes.
6. **State what a denylist cannot do,** in the code, in the tests and in `SECURITY.md`.

## Metrics

- **Diff:** 2 production files (one new), 1 new test file, `SECURITY.md`, the report, the index
  row, CHANGELOG, findings.
- **Bypasses against the measured list:** 30 of 32 before, 0 after.
- **Test delta:** unit 2,017 to 2,146 (+129); integration 163 passed, 2 skipped.
- **Preflight:** format (after one whitespace fix), 0 warnings on the touched project, unit,
  integration, `ascii-check`, `markdownlint`, `exec-report-check`.
- **CI status at push time:** pending at commit time; link in the PR.

## Credits

Newman led because this is a hardening fix. FDR filed the finding, and the measured list is the
one FDR would have written. Kramer's lens is why the tokenizer is a tokenizer and not a bigger
regex. Puddy's is the allowed list. Rabbi's is the sentence about who is protected. Commits carry
`Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>`.
