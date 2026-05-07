# S03E30 -- *The Audit Trilogy*

> *Four parallel audits, eight fixes, one reproducible release pipeline -- the Special goes Special-er.*

**Commit:** see push range (multi-commit)
**Branch:** `main` (direct push)
**Runtime:** ~45 min wall-clock end-to-end
**Director:** Larry David (showrunner)
**Cast:** 12 sub-agents across 3 dispatch waves (4 audits + 7 Wave A fixes + 1 Wave B fix)

## The pitch

S03E29 *The Season Special* turned `main` green again, but green CI is
table stakes -- it doesn't make a release reproducible, doesn't catch
SAST regressions, and doesn't guarantee the next `git tag v2.2.0`
produces an artifact set anyone can actually verify. The user asked
for an audit trilogy: CI, security, and app -- plus a docs/release
readiness pass -- explicitly framed as "we need solid reproducible
CI builds that get new releases and updated docs."

So we ran four read-only audits in parallel, triaged the findings into
a critical/high/medium ladder, and shipped eight fixes in two waves.
The headline outcomes: a 7-leg release matrix (was 4), CHANGELOG-sourced
release notes (no more handwritten drift), published SHA256 digests,
deterministic AOT builds, a CodeQL SAST baseline, SHA-pinned actions on
`docs-lint`, hardened scorecards, a documented release runbook,
`make release-precheck` to gate the cut, and a CLI-correctness fix that
makes `--fallback bogus` exit 2 *before* asking for credentials.

This isn't a feature episode. It's a "the product is real now" episode.

## Scene-by-scene

### Act I -- Planning

Four read-only explore agents (haiku-tier, gpt-5.4-mini) dispatched in
parallel against four scopes: CI, security, app, docs+release-readiness.
All four returned prioritized findings docs (CRITICAL/HIGH/MEDIUM/LOW
with summary tables). Triage produced 8 actionable fix todos. Decisions
locked:

- **Release matrix completeness is CRITICAL**, not nice-to-have. Linux
  ARM64, macOS Intel, and Windows ARM64 were all missing -- shipping a
  v2.2.0 without them would orphan three real user populations.
- **Release notes must come from CHANGELOG**, not `generate_release_notes`.
  GitHub's auto-notes leak commit subjects; we curate `[Unreleased]`
  exactly so the release body is the curated cut.
- **Fallback parser must precede the creds gate.** S03E29 worked around
  this by injecting `fb_dummy_env` into 3 integration assertions; the
  workaround is a smell. Move the parser, drop the workaround.
- **CodeQL** belongs in this episode. It's the missing SAST baseline
  the security audit flagged.
- **Node-20 era action major bumps** get their own future episode --
  too much surface for a release-readiness pass.

### Act II -- Fleet dispatch

| Wave | Agents (parallel) | Outcome |
|------|-------------------|---------|
| **0 -- audits** | audit-ci, audit-security, audit-app, audit-docs-release-readiness | 4 prioritized findings docs; 8 fix todos seeded |
| **A -- file-independent fixes** | fix-release-workflow, fix-release-runbook, fix-aot-determinism, fix-add-codeql, fix-pin-docs-lint, fix-harden-scorecards, fix-doc-drift | 7 fixes shipped in parallel; no two agents touched the same file; no agent ran `dotnet build` (orchestrator validates at end) |
| **B -- isolated build/test fix** | fix-fallback-before-creds | Program.cs reorder + 4 new unit tests + integration test workaround reverted; full local build+test pass green |

Wave A's safety pattern: 7 agents in parallel works *only* when scopes
are file-disjoint. Verified post-hoc with `git status --short` -- zero
conflicts. Wave B ran alone because it had to build and test, and
parallel `dotnet build` invocations race on the AOT output dir.

### Act III -- Ship

- Local preflight: format-check + build + 600+ unit tests + 111
  integration tests + exec-report-check -- all green.
- markdownlint-cli2 with `NODE_OPTIONS="--max-old-space-size=4096"`: clean.
- `make release-precheck` correctly fails on the dirty tree (expected --
  it's a release-cut gate, not a commit gate).
- Push protocol: this exec report (`s03e30-the-audit-trilogy.md`)
  satisfies the `pre-push` hook for the push range.

## What shipped

**Production code:**

- `azureopenai-cli/Program.cs` -- `FallbackPolicy.Resolve` lifted to
  fire alongside `--help`/`--version`/`--doctor`, before the
  `AZUREOPENAIENDPOINT` creds gate. Approach (a) from the audit:
  `Resolve` is purely syntactic, no `PreValidate` shim needed.
- `azureopenai-cli/AzureOpenAI_CLI.csproj` -- `<Deterministic>true</>`
  plus `<ContinuousIntegrationBuild>` (gated on CI/GITHUB_ACTIONS
  env) for reproducible AOT output.

**CI / release infrastructure:**

- `.github/workflows/release.yml` -- 7-leg matrix (linux-x64, linux-arm64,
  osx-x64, osx-arm64, win-x64, win-arm64); `linux-arm64` uses native
  `ubuntu-24.04-arm`; `osx-x64` pinned to `macos-13` (last Intel);
  `win-arm64` cross-publishes from `windows-latest`. Release notes now
  awk-extracted from `CHANGELOG.md` `[Unreleased]` section into
  `release-body.md`. Per-asset SHA256 digests aggregated into
  `digests.txt`, attached to release, and embedded in body.
- `.github/workflows/codeql.yml` (NEW) -- csharp SAST. Manual `dotnet
  build` (no autobuild for AOT/trim project). Push + PR + weekly
  schedule. SHA-pinned actions. `security-events: write` permission.
  Concurrency + 30-min timeout.
- `.github/workflows/docs-lint.yml` -- SHA-pinned `actions/checkout` and
  `actions/setup-node` to v4.2.2 / v4.4.0 commit shas.
- `.github/workflows/scorecards.yml` -- `concurrency:` block + 15-min
  timeout (was unbounded).

**Tests:**

- `tests/AzureOpenAI_CLI.Tests/FallbackChainTests.cs` -- 4 new
  `FallbackParser_*` tests asserting `HasError` behavior with no creds
  in env (unknown preset, depth>3, duplicate, valid chain parses).
- `tests/integration_tests.sh` -- removed `fb_dummy_env()` helper added
  in S03E29; reverted tests 2/3/4 in the S03E22 fallback block to plain
  `$BIN` invocations. They now pass without dummy creds, proving the
  fix end-to-end. 111/111 integration tests green.

**Docs:**

- `docs/process/release.md` (NEW) -- 6-section release runbook
  (precheck, promote `[Unreleased]`, tag+push, CI takeover,
  post-release verification, open next `[Unreleased]`).
- `docs/process/README.md` -- index updated.
- `Makefile` -- `release-precheck` target (5 gates: clean tree,
  `[Unreleased]` non-empty, csproj > latest tag, README version
  reference, no open CRITICAL/HIGH findings) and
  `release-notes-preview` target. Both wired into `.PHONY` and
  `make help`.
- `README.md` -- agent count drift `25` -> `28`
  (1 showrunner + 5 main + 22 supporting).
- `SECURITY.md` -- supported versions table corrected: 2.2.x active,
  2.1.x maintenance, 2.0.x EOL, <2.0 unsupported (was claiming 2.0.x
  active when csproj is 2.2.0).

**Not shipped** (intentional follow-ups):

- Node-20 era action major bumps (`actions/upload-artifact@v3` +
  friends) -- own future episode; too broad for this pass.
- `--doctor` coverage expansion -- audit flagged gaps in tool/preset
  coverage; backlog item.
- Mode parser refactor -- audit flagged Program.cs cyclomatic
  complexity around mode dispatch; backlog item.
- Program.cs error-handling consistency sweep -- some paths still
  inline `Console.Error.WriteLine` + `Environment.Exit` instead of
  `ErrorAndExit`; backlog item.

## Lessons from this episode

1. **Audits are cheap when parallelized.** Four read-only explore
   agents at haiku tier returned in ~3 minutes total wall-clock and
   produced findings I would have spent an hour scrolling through
   solo. The cost is in triage discipline, not the audit itself.
2. **File-independence is the parallelism contract.** Wave A's 7
   parallel fixes worked because the scopes were genuinely disjoint.
   Verified `git status --short` post-hoc; zero conflicts. The next
   time someone wants to dispatch 7 agents in parallel, the
   pre-flight question is "do any two of them write to the same
   file?" -- not "are they logically related?"
3. **Workarounds in tests are smells.** S03E29 added `fb_dummy_env` to
   make the fallback parser tests pass without changing Program.cs.
   The real fix was 28 lines in Program.cs. The workaround survived
   one episode and got caught the next.
4. **Release notes from CHANGELOG > generate_release_notes.** Caught
   by the audit, not by us. GitHub's auto-notes leak commit subjects
   that don't belong in user-facing release bodies. We curate
   `[Unreleased]` exactly so the release body is the curated cut --
   the workflow had been throwing that work away.
5. **`make release-precheck` failing on a dirty tree is correct.**
   First instinct was to "fix" the test failure. Second look: the
   target is a release-cut gate, not a commit gate. It should pass
   *only* when the tree is clean and csproj has been bumped past the
   latest tag. Working as intended.
6. **`SECURITY.md` and `README.md` drift silently.** Agent count was
   off by 3, supported-versions table was off by two minor versions.
   Neither was caught by any lint or test. The `release-precheck`
   `README` version-reference check is a partial mitigation; consider
   a future skill or test for `SECURITY.md` table consistency.

## Metrics

- Diff size: ~254 insertions / ~75 deletions across 13 files
  (11 modified, 2 new: `codeql.yml`, `release.md`).
- Test delta: +4 unit tests (FallbackChainTests), +1 integration
  block reverted-to-direct (3 fewer lines of test plumbing). 111/111
  integration green; full unit suite green.
- Preflight: passed (format + build + unit + integration +
  exec-report-check).
- CI status at push time: pending. Watch CI / docs-lint / SBOM /
  Scorecard / **CodeQL** (new) / Automatic Dependency Submission.

## Credits

- **Audit wave (4 agents):** audit-ci, audit-security, audit-app,
  audit-docs-release-readiness -- all read-only, all parallel,
  haiku-tier (gpt-5.4-mini).
- **Wave A (7 agents, parallel, sonnet-4.6 + haiku-4.5):**
  fix-release-workflow (release.yml 7-leg matrix + CHANGELOG notes
  and digests), fix-release-runbook (docs/process/release.md and
  Makefile precheck), fix-aot-determinism (csproj),
  fix-add-codeql (codeql.yml), fix-pin-docs-lint (docs-lint.yml
  SHA pins), fix-harden-scorecards (scorecards.yml concurrency
  plus timeout), fix-doc-drift (README + SECURITY).
- **Wave B (1 agent, sonnet-4.6):** fix-fallback-before-creds
  (Program.cs reorder + integration-test revert + 4 unit tests +
  full local build/test pass).
- **Director:** Larry David (showrunner) -- episode conception,
  triage, dispatch, sign-off.

`Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>`
trailer on every commit in the push range.
