# Skill: ci-triage

**Run when CI is red on `main` or on a PR.** Goal: diagnose in under 5 minutes, fix forward within the hour.

## Step 1 -- find the failing run

```bash
gh run list --branch main --limit 5 --json databaseId,conclusion,displayTitle,workflowName
```

Or from an MCP session: `github-mcp-server-actions_list` with `workflow_runs_filter.status=completed`.

## Step 2 -- pull failed-job logs

```bash
gh run view <run-id> --log-failed | tail -200
```

Or: `github-mcp-server-get_job_logs` with `failed_only=true`, `return_content=true`, `tail_lines=200`.

## Step 3 -- classify the failure

| Signature | Class | Owner |
|-----------|-------|-------|
| `error WHITESPACE: Fix whitespace formatting` | Style -- **preflight** was skipped | The Soup Nazi |
| `error CS\d+:` (compile error) | Build break | Kramer |
| `Test ... failed` | Test regression | Kramer + Puddy |
| Flaky test (passes on rerun) | Flake | Puddy (triage), not retry |
| `Trivy` / `OSV-Scanner` vulnerability | Security | Newman |
| `docker build` failed | Container / Dockerfile | Jerry |
| Timeout / network | Infra flake | Retry once, then Frank |

## Step 4 -- fix forward

- **Style**: run `dotnet format azure-openai-cli.sln`, commit as `style: dotnet format cleanup`, push. See [`preflight.md`](preflight.md).
- **Build/test**: reproduce locally first. Never commit a "maybe fix" blind.
- **Flake**: do NOT add a retry loop. Quarantine the test with an `[Trait("Flaky", "true")]` and file a follow-up with repro steps. Consult Puddy.
- **Security**: bump the dep; if no upgrade path exists, open an issue and tag Newman.

## Step 5 -- verify the fix

```bash
# After push:
gh run list --branch main --limit 1 --json conclusion,status,displayTitle
```

Wait for `conclusion: success` before claiming victory. "It built locally" is not a green CI run.

## Anti-patterns -- do not do these

- **Disabling the failing check** to unblock a push. If the check is wrong, fix the check; don't mute it.
- **Rerunning the job hoping it passes.** That's flake denial.
- **Pushing "fix ci" with no reproduction.** If you don't know why it failed, you don't know if your change fixes it.
- **Letting `main` stay red overnight.** Everyone's downstream work is blocked. Fix forward or revert.

## Reverting

If fix-forward isn't quick, revert the offending commit:

```bash
git revert <bad-sha>
git push origin main
```

Then land the real fix on a branch with CI green before re-applying.
