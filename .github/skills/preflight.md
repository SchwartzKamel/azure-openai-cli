# Skill: preflight

**Run before every `git commit` that touches C# code, project files, or CI config.** Non-negotiable. This is the skill whose absence caused the `180d64f` incident (five consecutive red CI runs on `main`).

## When to run

- Any change to `*.cs`, `*.csproj`, `*.sln`, `*.editorconfig`
- Any change to `.github/workflows/*.yml`
- Any change to `Dockerfile` or integration test scripts

Docs-only changes (`*.md`) can skip preflight, but running it is free and catches nothing.

## The five checks

All five must be green. Exit code 0 or the soup is closed.

```bash
cd "$(git rev-parse --show-toplevel)"
export PATH="$HOME/.dotnet:$PATH"

# 1. Format gate -- the one Kramer skipped
dotnet format azure-openai-cli.sln --verify-no-changes

# 2. Build gate
dotnet build azureopenai-cli/AzureOpenAI_CLI.csproj -c Release --nologo

# 3. Test gate
dotnet test tests/AzureOpenAI_CLI.Tests/AzureOpenAI_CLI.Tests.csproj --verbosity minimal --nologo

# 4. Integration gate (only if you changed CLI surface or Program.cs)
bash tests/integration_tests.sh

# 5. Exec-report gate -- enforces docs/exec-reports/sNNeMM-*.md per push
bash scripts/exec-report-check.sh
```

## If format check fails

```bash
dotnet format azure-openai-cli.sln   # apply fixes
git diff --stat                       # confirm whitespace only
dotnet format azure-openai-cli.sln --verify-no-changes   # confirm clean
```

If `dotnet format` wants to change anything that isn't whitespace or trivial ordering, **STOP**. Investigate. Do not squash-commit semantic changes into a "format" commit.

## If tests fail

Do not push. Fix the test or fix the code. If the test is flaky, file the flake under the `puddy` agent's triage flow -- do not retry-and-hope.

## Shortcut

`make preflight` runs all five steps (see `Makefile`). If the Makefile target is missing or stale, add it in the same PR as whatever you're shipping.

## Why exec-report-check is here

The skill text alone wasn't catching the miss -- agents (and humans) would push code, CHANGELOG, and CHANGELOG-only docs without an episode write-up, then a reviewer would have to flag it after the fact. As of S02E37 the gate is mechanical: `scripts/exec-report-check.sh` fails preflight and the pre-push hook (install via `make install-hooks`) when the push range touches anything outside `docs/exec-reports/` and adds no new `sNNeMM-*.md`. Opt out per-commit by adding a `Skip-Exec-Report: <reason>` trailer (start-of-line, like `Co-authored-by:`) to the commit body for genuinely trivial changes (typo fixes, dependency bumps, hotfix rollbacks). See [`.github/skills/exec-report-format.md`](exec-report-format.md) for the report shape.

## Escape hatches

There are none. If you genuinely cannot run .NET locally (you're on a constrained shell, you're debugging CI from an SSH jumpbox), push to a branch and let CI run preflight for you -- but do **not** push directly to `main`.
