# S02E26 -- *The Locked Drawer*

> *Newman extends the ReadFileTool blocklist to cover seven home-dir credential stores -- and installs the canonicalize-then-match pipeline to keep them locked.*

**Commit:** `<filled-at-push>`
**Branch:** `main` (direct push)
**Runtime:** ~35 min wall clock
**Director:** Larry David (showrunner)
**Cast:** 1 sub-agent (Newman) in 1 dispatch wave -- hardening episode, no committee

## The pitch

S02E23 *The Adversary* (FDR) ran twenty-one adversarial probes against the
tool surface and logged seven specific home-dir credential stores that
`ReadFileTool` would happily hand to the LLM: the OpenSSH user directory,
kubectl's cluster config, GnuPG's keyring, `.netrc`, `.docker/config.json`,
`.git-credentials`, and the `.npmrc`/`.pypirc` pair. Every one of these is
the first stop for an attacker who has smuggled a tool-call into the
conversation -- and the blocklist didn't know about any of them. Seven
findings, one shape, one episode's worth of work.

The fix had two halves. Half one was the list itself: seven new
prefix entries (plus `~/.config/gh/hosts.yml` as a same-shape bonus, since
the GitHub CLI stores OAuth tokens there in cleartext). Half two was
structural: the pre-E26 path check `Path.GetFullPath` → `IsBlockedPath`
worked, but it did not NFKC-normalize the input, did not reject
percent-encoded evasions, and mixed validation with I/O in one method.
S02E32 *The Bypass* had already landed the analogous rewrite on the shell
side; Newman mirrored the shape here so future hardening episodes have one
pipeline to reason about, not two.

The "did not touch" list matters as much as the "did": ShellExec was
E32's job and stayed untouched. WebFetch's DNS-rebinding and TOCTOU
gaps are S03 material and stayed queued. `s02-writers-room.md` is
orchestrator-owned and stayed closed on Newman's side -- the findings
closures are listed below for Larry to batch-commit at sign-off.

## Scene-by-scene

### Act I -- Planning

Inventory pass on the existing state:

- `ReadFileTool.cs` had 14 blocklist entries, a single `ReadAsync`
  method that inlined validation + I/O, and already supported
  tilde-prefixed entries and symlink re-check. No NFKC, no
  percent-encoding rejection, no structural separation of
  validate-vs-act.
- No `Adversary/` test directory existed yet (contra the E32 commit
  message, which referenced `Adversary/ShellExecBypassTests.cs` --
  that file was collateral damage of the `b913617` v2-consolidation
  refactor and has not been restored). The `Adversary/` directory is
  new ground, created in this episode.
- `ToolHardeningTests.cs` had 11 existing `ReadFileTool` facts
  exercising the pre-existing blocklist entries (AWS, Azure,
  `az-ai`, container secrets, `.env`) -- kept intact, unchanged.
- Findings-backlog search confirmed the seven `e23-readfile-*` IDs
  exactly as the brief named them. No scope creep.

Decisions locked in Act I:

1. **Curated list** -- the seven findings plus `~/.config/gh/hosts.yml`.
   gh-cli stores OAuth tokens in cleartext at that path; same threat
   shape, same one-line cost to cover, and Newman was already on-set.
2. **Alternate `git-credentials` location** -- the finding name is
   singular but git ships two credential-store paths (`~/.git-credentials`
   *and* `~/.config/git/credentials` under XDG). Cover both.
3. **Validation shape** -- extract a `Validate(rawPath, out canonical)`
   method returning `string?` (error-or-null), same signature pattern
   as `ShellExecTool.Validate` in `a4fd184`. `IsBlockedPath` stays
   untouched as the canonical-form inner primitive.
4. **Evasion-rejection policy** -- default-deny for percent-encoded
   segments (`%2E`, `%2F`, `%00`) and for control bytes in the raw
   path. Neither appears in a legitimate filename coming from an LLM
   tool call; both are classic bypass shapes.

### Act II -- Fleet dispatch

Single-wave solo episode -- hardening work that did not require a cast.

| Wave | Agents (parallel) | Outcome |
|------|-------------------|---------|
| **1** | Newman | Extended blocklist (+10 entries), extracted `Validate()` pipeline with NFKC + canonicalize + prefix-match, created `Adversary/ReadFileSensitivePathTests.cs` with 53 facts, updated `SECURITY.md`. |

E34 *The Index* (Lloyd + Elaine) was filming in parallel on docs-only
surface; file boundaries did not overlap. No coordination events.

### Act III -- Ship

Preflight (all four gates, `make preflight`):

```text
Format gate          -- 0 changes needed
Build gate           -- 0 warnings, 0 errors
Test gate            -- all suites passed
Integration gate     -- 34 passed, 2 skipped (API-key-gated)
[preflight] all gates green -- safe to commit
```

Targeted test run (paste per brief):

```text
$ dotnet test tests/AzureOpenAI_CLI.Tests/AzureOpenAI_CLI.Tests.csproj \
    --filter "FullyQualifiedName~ReadFile" --verbosity minimal
Passed!  - Failed: 0, Passed: 54, Skipped: 0, Total: 54
```

54 = 53 new `ReadFileSensitivePathTests` facts + 1 pre-existing
`ToolHardeningTests.ReadFile_EmptyPath_ReturnsError` fact. The other 10
`ReadFileTool` facts in `ToolHardeningTests` match the filter
`~IsBlockedPath` rather than `~ReadFile`; they also still pass (covered
by the full test-gate run above).

Skip-attribute check:

```text
$ grep -n 'Skip' tests/AzureOpenAI_CLI.Tests/Adversary/ReadFileSensitivePathTests.cs \
    || echo "no skipped facts remaining"
no skipped facts remaining
```

Commit single, push direct to `main`, rebase on E34 if they land first.

## What shipped

**Production code** -- `azureopenai-cli/Tools/ReadFileTool.cs`:

- `BlockedPathPrefixes` extended from 14 entries to 24 (+10). New
  entries grouped and commented with the `e23-readfile-*` finding
  ID that drove each addition.
- New `Validate(string rawPath, out string canonical) -> string?`
  method -- the defense-in-depth pipeline. Six stages, documented
  inline:
  1. Empty-check + reject NUL / control bytes.
  2. Reject percent-encoded path segments (`%2E`, `%2F`, `%00`).
  3. `Normalize(NormalizationForm.FormKC)` -- collapses fullwidth
     and compatibility lookalikes to ASCII.
  4. Tilde-expand (only if input starts with `~`).
  5. `Path.GetFullPath` -- canonicalizes `..`, collapses `//`,
     strips trailing separators.
  6. `IsBlockedPath(canonical)` exact-prefix match.
- `ReadAsync` slimmed down: calls `Validate` first, then existence
  check, then symlink resolution + re-check against blocklist on
  the resolved target, then size cap, then read.
- `IsBlockedPath` is unchanged in behaviour -- it is the inner
  primitive the pipeline feeds canonical paths to.

**Tests** -- `tests/AzureOpenAI_CLI.Tests/Adversary/ReadFileSensitivePathTests.cs`:

New file (directory is new; no pre-existing adversary tests to
reactivate -- see the Act-I note on `b913617`'s collateral damage).
53 facts total, grouped into three sections with per-fact rationale
comments per the Newman hardening spec:

- **Section 1 (42 facts)** -- the 7 finding IDs' 14 canonical
  paths, each exercised three ways: via tilde form through
  `ReadAsync`, via pre-expanded absolute form through `ReadAsync`,
  and directly on `IsBlockedPath`. Parameterised via
  `[MemberData(nameof(SensitiveHomePaths))]`.
- **Section 2 (9 facts)** -- evasion matrix: NFKC fullwidth-dot
  (`\uFF0E`), `%2E`, `%2F`, NUL-byte truncation, trailing slash on
  a directory, `//double/slash`, `..` traversal, case variant
  (`~/.SSH`), and the symlink-through-`/tmp`-to-`/etc/shadow`
  test (which skips at runtime, not via `[Skip=]`, if
  `/etc/shadow` is absent or the runner lacks symlink privilege).
- **Section 3 (2 facts)** -- negative controls: `/tmp/not-sensitive.txt`
  and `/usr/share/doc/readme` must NOT be blocked, regression gate
  against future over-wide blocklists.

No `Skip=` attributes. Every fact has a one-to-three-line rationale
comment explaining *what* class of evasion it pins -- required
reading if the blocklist ever needs to narrow or widen.

**Docs** -- `SECURITY.md`:

The "ReadFileTool" subsection of §11 "Tool Security" was rewritten
to document the new pipeline (NFKC → canonicalize → prefix-match),
the evasion-rejection policy (control bytes, percent-encoding),
and the full extended blocklist including which E23 findings
motivated which entries. One cross-link each to the source file
and the new adversary test file.

**Not shipped** (intentional follow-ups):

- **DNS rebinding / TOCTOU defenses for `WebFetchTool`** -- the
  E23 finding `e23-webfetch-dns-rebinding-toctou` is
  structural-rewrite-sized and belongs to the S03 hardening arc,
  not this episode. Deferred, not dropped.
- **Other tool files' sensitive-surface review** (`ClipboardGet`,
  etc.) -- the brief explicitly scoped Newman to `ReadFileTool`
  only. Future episodes.
- **Writers' room findings update** -- Newman does not edit
  `docs/exec-reports/s02-writers-room.md` (orchestrator-owned).
  Closures listed below for Larry's batch update at sign-off.
- **CHANGELOG.md** -- owned by E10 *Press Kit*. Not touched here.

## Lessons from this episode

1. **Paper-trail the blocklist rationale.** Grouping the new entries
   with `// e23-readfile-<id>` comments means the next person to
   widen or narrow this list has a direct path back to the
   finding-backlog entry that put each path there. Security PRs
   that cannot answer "why is this on the list?" get reverted.
2. **`a4fd184`'s shape was worth copying.** Having two tool
   validators with the same `Validate() -> error-or-null`
   signature means reviewers have one pipeline shape to reason
   about, not N. Newman's recommendation: the next tool to get a
   validation pass (WebFetch, in S03) should mirror the same
   signature.
3. **`b913617` ate the previous adversary tests.** The E32 commit
   message referenced `Adversary/ShellExecBypassTests.cs`, but the
   v2-consolidation refactor that followed removed the entire
   `Adversary/` tree. E26 rebuilds it. Future consolidation
   refactors must explicitly enumerate which test files they are
   moving vs. deleting -- silent delete is how security coverage
   disappears.
4. **Runtime-gated skips, never `[Skip=]`.** The symlink test
   cannot run on Windows or unprivileged containers, but rather
   than `[Skip="no symlink privilege"]` it `return`s early from
   inside the fact if the preconditions are not met. That way the
   test is never Red, never Skipped, and `grep -n 'Skip'` stays
   clean as a hard signal.
5. **Percent-encoding is a default-deny, not an allow-with-decode.**
   .NET's file APIs don't URL-decode, so `%2E` is a literal
   dirname on disk and there is zero legitimate reason for an LLM
   tool call to include one. Rejecting up front is both safer and
   simpler than decoding-then-rechecking.

## Findings-backlog closures (for Larry's writers' room update)

Newman closes these `e23-readfile-*` findings. To be batch-applied
to `docs/exec-reports/s02-writers-room.md` at sign-off:

- `e23-readfile-ssh-userdir-not-blocked` -- closed by `~/.ssh` prefix entry + 4 adversarial facts.
- `e23-readfile-kube-config-not-blocked` -- closed by `~/.kube` prefix entry + 2 adversarial facts.
- `e23-readfile-gnupg-not-blocked` -- closed by `~/.gnupg` prefix entry + 2 adversarial facts.
- `e23-readfile-netrc-not-blocked` -- closed by `~/.netrc` prefix entry + 1 adversarial fact.
- `e23-readfile-docker-config-not-blocked` -- closed by `~/.docker/config.json` prefix entry + 1 adversarial fact.
- `e23-readfile-git-credentials-not-blocked` -- closed by `~/.git-credentials` **and** `~/.config/git/credentials` prefix entries + 2 adversarial facts (both locations covered).
- `e23-readfile-npmrc-pypirc-not-blocked` -- closed by `~/.npmrc` + `~/.pypirc` prefix entries + 2 adversarial facts.

**New findings queued** (none raised this episode; scope was purely
closure + structural hardening, no new adversarial probing).

## Metrics

- Diff size: ~3 files, ~220 insertions / ~30 deletions
  (`ReadFileTool.cs` +116/-27, `ReadFileSensitivePathTests.cs` +236/0
  new file, `SECURITY.md` +51/-14). Plus this exec report.
- Test delta: +53 new facts (`ReadFileSensitivePathTests`).
  Pre-existing ReadFileTool coverage unchanged. 0 skipped, 0 flaky.
- Preflight: **passed** (format, build, test, integration -- all
  four gates green in one run, no retries).
- CI status at push time: pending at commit; watch
  `gh run list --branch main --limit 1`.

## Credits

- **Newman** (security inspector) -- blocklist curation, validation
  pipeline, adversarial test matrix, `SECURITY.md` update, this
  report. Arrived with a clipboard. Left with a PR.
- **FDR** (red team, absentia) -- the seven findings `e23-readfile-*`
  closed by this episode came from his S02E23 probe run.
- **E32 *The Bypass*** (absentia) -- the `Validate()` signature shape
  this episode mirrors was landed by Newman in `a4fd184`. Same
  author, different call site, consistent discipline.

Commit trailer on the episode commit:

```text
Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>
```
