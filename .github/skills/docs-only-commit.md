# Skill: docs-only-commit

**Run when your diff is markdown / docs only.** Tells you what to skip, what to still run, and how to keep a stray `.cs` file from sneaking through the gate.

## What counts as docs-only

A diff that touches **only** the following paths:

- `*.md` anywhere in the tree
- `docs/**`
- `.github/skills/**`
- `.github/agents/**`
- `.github/ISSUE_TEMPLATE/**`
- `CHANGELOG.md`
- New non-build asset files referenced from docs (e.g., `docs/assets/*.png`, `docs/assets/*.svg`)

A diff that touches **any** of the following is **not** docs-only -- run [`preflight`](preflight.md):

- `*.cs`, `*.csproj`, `*.sln`, `.editorconfig`
- `.github/workflows/*.yml`
- `Dockerfile`, `Makefile`
- `tests/integration_tests.sh` or anything under `tests/`

## What you SKIP

- `make preflight` -- docs do not compile, do not link, do not run
- `dotnet format` / `dotnet build` / `dotnet test` -- same reason
- `bash tests/integration_tests.sh` -- same reason
- `markdownlint-cli2` locally -- it OOMs on this tree; the upstream `docs-lint` workflow runs it for you. Do **not** try to run it locally.

## What you STILL run

1. **[`ascii-validation`](ascii-validation.md)** -- the smart-quote grep on every new or modified `.md` outside the upstream exclusion list. This is the one that hard-fails CI.
2. **`git status` and `git diff --name-only HEAD`** -- confirm the diff really is docs-only. See "The trap" below.
3. **Local link sanity** for any new relative links you added (`ls path/from/the/link/target`). The workflow's link-check will catch you if you skip; you would rather catch yourself.

## What you STILL do

- **Conventional Commits** subject and body, per [`commit`](commit.md). Type is almost always `docs`; scope is the area touched (`docs(skills)`, `docs(agents)`, `docs(ethics)`, etc.).
- **Copilot co-author trailer** on every Copilot-assisted commit, per [`commit`](commit.md). No exceptions.
- **Push to `main`** when green, or to a branch + PR. Direct push to `main` is permitted for docs-only changes by the same maintainer policy as code changes.
- **CHANGELOG decision** -- most docs changes do **not** earn a CHANGELOG bullet. See [`changelog-append`](changelog-append.md) for the call.

## The trap

A `.cs` file sneaking into your "docs-only" diff bypasses preflight in your head -- not in CI. Format errors, build breaks, test regressions land on `main`.

The discipline:

```bash
git status                              # before stage
git diff --name-only --diff-filter=AM HEAD   # before commit
```

If you see anything outside the docs-only path list above, **stop and run preflight**. The cost of one `make preflight` invocation is cheap; the cost of a red `main` is a fix-forward commit, a CI re-run, and an [`ci-triage`](ci-triage.md) entry.

## Edge case: docs change that adds a new asset

A markdown file that adds `docs/assets/screenshot.png` is still docs-only **iff** the asset is referenced only from docs and is not consumed by build (Dockerfile `COPY`, csproj `<EmbeddedResource>`, test fixtures). If the asset is wired into build inputs, treat the change as code -- run preflight.

## Edge case: workflow file change

`.github/workflows/*.yml` is **not** docs even though it is YAML. Workflow logic affects every future PR. Run preflight (the YAML lint inside it counts) and prefer a branch + PR over direct push so CI tests the workflow against itself.

## Cross-refs

- [`preflight`](preflight.md) -- the gate this skill lets you skip, conditionally
- [`ascii-validation`](ascii-validation.md) -- the gate this skill does **not** let you skip
- [`commit`](commit.md) -- the message format every commit (docs or code) follows
- [`changelog-append`](changelog-append.md) -- when a docs change earns a CHANGELOG bullet (mostly: it does not)
