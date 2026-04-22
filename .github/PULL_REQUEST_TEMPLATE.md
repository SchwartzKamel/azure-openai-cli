<!--
  Thanks for sending a PR. Fill out what applies; delete the rest.
  Keep the diff focused -- one concern per PR is easier to review and revert.
  Scope creep is the leading cause of stalled reviews. When in doubt, split.
-->

## Summary

<!-- 1-3 sentences. What does this change, and why? -->

## Related issue

<!-- Link the issue this resolves, e.g. "Fixes #123" or "Refs #123". -->

Fixes #

## Type of change

- [ ] 🐛 Bug fix
- [ ] ✨ Feature
- [ ] 📝 Docs
- [ ] ♻️ Refactor (no behavior change)
- [ ] ✅ Test
- [ ] 🔧 Chore / tooling / CI

## Tree touched

<!-- Check all that apply. PRs that touch both trees are unusual -- if you must, note why. -->

- [ ] `azureopenai-cli-v2/` -- v2, default for new work
- [ ] `azureopenai-cli/` -- v1 maintenance only (security / P0 / cutover)
- [ ] `tests/` only
- [ ] Docs / CI / tooling only (no compiled code)

## Testing

<!--
  What did you run to verify this works? Paste commands and the outcome.
  Example (v2):
    $ make preflight
    ... format clean / build OK
    $ dotnet test tests/AzureOpenAI_CLI.Tests/AzureOpenAI_CLI.Tests.csproj --nologo
    ... Passed!  - Failed:     0, Passed:   538
    $ az-ai-v2 chat "hi" --verbose
    ... expected output
-->

**Test count before → after:** <!-- e.g. 538 → 542. If you added behavior, the count should move. -->

## Preflight

<!--
  Required on any change touching .cs, .csproj, .sln, .editorconfig,
  .github/workflows/*.yml, Dockerfile, or integration test scripts.
  Skip only for docs-only PRs. See .github/skills/preflight.md.
-->

- [ ] `make preflight` passes locally (format + build + test + integration)
- [ ] Or: this is a docs-only PR (`*.md` + no code) and preflight does not apply

## Checklist

- [ ] Tests added or updated for new behavior -- positive **and** negative paths (or N/A)
- [ ] `CHANGELOG.md` updated under `[Unreleased]` (or N/A for docs-only)
- [ ] Docs updated where behavior changed: `README.md`, `ARCHITECTURE.md`, `CONFIGURATION.md`, `--help` text, man pages, `docs/…` -- whichever applies
- [ ] Diff is scoped to one concern (no drive-by refactors, no unrelated renames)
- [ ] No secrets, endpoints, or tokens committed
- [ ] Commit subject is a [Conventional Commit](skills/commit.md) (lowercase type, imperative, ≤ 72 chars)
- [ ] **If AI-assisted**, the commit message includes the provenance trailer:
      `Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>`

## Screenshots / logs

<!-- Paste terminal output, asciinema links, or screenshots if this is a UX-visible change. Redact secrets. -->

