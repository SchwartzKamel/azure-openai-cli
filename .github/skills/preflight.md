# Skill: preflight

**Run before every `git commit` that touches C# code, project files, CI config, or markdown outside `docs/exec-reports/`.** Non-negotiable. This is the skill whose absence caused the `180d64f` incident (five consecutive red CI runs on `main`) and the S04E01..S04SP1 docs-lint outage (ten consecutive red `docs-lint` runs because the gate was server-only).

## When to run

- Any change to `*.cs`, `*.csproj`, `*.sln`, `*.editorconfig`
- Any change to `.github/workflows/*.yml`
- Any change to `Dockerfile` or integration test scripts
- Any change to `*.md` outside `docs/exec-reports/` (since S04SP3)

## The seven checks

All seven must be green. Exit code 0 or the soup is closed.

```bash
cd "$(git rev-parse --show-toplevel)"
export PATH="$HOME/.dotnet:$PATH"

# 1. Format gate -- the one Kramer skipped
dotnet format azure-openai-cli.sln --verify-no-changes

# 2. Docs-lint gate -- markdownlint-cli2, mirrors docs-lint.yml (S04SP3)
NODE_OPTIONS=--max-old-space-size=4096 npx --yes markdownlint-cli2

# 3. Ascii-check gate -- bans U+2018/U+2019/U+201C/U+201D/U+2013/U+2014 (S04SP3)
make ascii-check

# 4. Build gate
dotnet build azureopenai-cli/AzureOpenAI_CLI.csproj -c Release --nologo

# 5. Test gate
dotnet test tests/AzureOpenAI_CLI.Tests/AzureOpenAI_CLI.Tests.csproj --verbosity minimal --nologo

# 6. Integration gate (only if you changed CLI surface or Program.cs)
bash tests/integration_tests.sh

# 7. Exec-report gate -- enforces docs/exec-reports/sNNeMM-*.md per push
bash scripts/exec-report-check.sh
```

## If format check fails

```bash
dotnet format azure-openai-cli.sln   # apply fixes
git diff --stat                       # confirm whitespace only
dotnet format azure-openai-cli.sln --verify-no-changes   # confirm clean
```

If `dotnet format` wants to change anything that isn't whitespace or trivial ordering, **STOP**. Investigate. Do not squash-commit semantic changes into a "format" commit.

## If docs-lint or ascii-check fails

`make docs-lint` runs markdownlint-cli2 against the full tree with the exact args CI uses (config in `.markdownlint-cli2.jsonc`). Fix the reported errors, re-run, and iterate. `make ascii-check` greps `*.md` for the six banned characters; replace per [`ascii-validation.md`](ascii-validation.md).

Pre-push hook (`make install-hooks`) re-runs both gates against the push range. Per-commit opt-out: add `Skip-Docs-Lint: <reason>` (start-of-line, like `Co-authored-by:`) to a commit body in the range. Reserve for bulk renames where lint will be cleaned in a follow-up.

## If tests fail

Do not push. Fix the test or fix the code. If the test is flaky, file the flake under the `puddy` agent's triage flow -- do not retry-and-hope.

## Shortcut

`make preflight` runs all seven steps (see `Makefile`). If the Makefile target is missing or stale, add it in the same PR as whatever you're shipping.

## Why exec-report-check is here

The skill text alone wasn't catching the miss -- agents (and humans) would push code, CHANGELOG, and CHANGELOG-only docs without an episode write-up, then a reviewer would have to flag it after the fact. As of S02E37 the gate is mechanical: `scripts/exec-report-check.sh` fails preflight and the pre-push hook (install via `make install-hooks`) when the push range touches anything outside `docs/exec-reports/` and adds no new `sNNeMM-*.md`. Opt out per-commit by adding a `Skip-Exec-Report: <reason>` trailer (start-of-line, like `Co-authored-by:`) to the commit body for genuinely trivial changes (typo fixes, dependency bumps, hotfix rollbacks). See [`.github/skills/exec-report-format.md`](exec-report-format.md) for the report shape.

## Why docs-lint + ascii-check are here

The S04E01..S04SP1 outage was identical in shape to the `180d64f` lesson: a gate existed in CI but not locally, so agents committed bad markdown without warning and the failure only surfaced server-side. Ten consecutive red `docs-lint` runs on `main` before SP1 caught it. S04SP3 *The Pre-Push* (Wilhelm) wired both gates into `make preflight` and into `scripts/pre-push.sh`. The gate is the gate. The gate is *there* for a reason.

## Escape hatches

There are none. If you genuinely cannot run .NET locally (you're on a constrained shell, you're debugging CI from an SSH jumpbox), push to a branch and let CI run preflight for you -- but do **not** push directly to `main`.
