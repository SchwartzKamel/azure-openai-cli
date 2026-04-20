# Skill: commit

**Run every commit through this procedure.** Enforced by Mr. Wilhelm (change management) and The Soup Nazi (style).

## Prerequisites

- [**preflight**](preflight.md) has passed on every code change
- You know which agent(s) are co-authoring this change — the cast matters for the commit body

## Commit message format

Conventional Commits, lowercase type, optional scope:

```
<type>(<scope>): <imperative subject, ≤72 chars>

<body — wrap at 72 cols. Explain the why, not the what.>

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>
```

**Types** used in this repo: `feat`, `fix`, `docs`, `style`, `refactor`, `perf`, `test`, `build`, `ci`, `chore`, `bench`, `security`.

**Scopes** commonly used: `cost`, `foundry`, `espanso`, `squad`, `agents`, `ralph`, `tools`, `ci`, `docker`, a filename stub, or empty.

## Required trailer

Every commit authored with Copilot assistance **must** carry:

```
Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>
```

No exceptions. This is how we give credit and how we trace provenance.

## Signing

Local repo signs commits by default. Agents use `-c commit.gpgsign=false` because they cannot sign:

```bash
git -c commit.gpgsign=false commit -m "feat(scope): subject

body

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

Human contributors should sign normally.

## Pushing to `main`

- Direct pushes to `main` are permitted for maintainers (this is a solo-led repo).
- Before pushing, **preflight** and commit checks must be green.
- After pushing, watch CI: `gh run list --branch main --limit 1 --json conclusion,status,displayTitle`. If red, fix forward within the hour.

## Pushing to a branch / PR

- Branch naming: `<agent-or-initial>/<kebab-topic>` (e.g., `kramer/foundry-routing`, `ch/espanso-wsl`).
- Open the PR immediately so CI runs — don't hoard local commits.
- PR title mirrors the commit subject; PR body can quote the commit body.

## What a good commit looks like

```
feat(foundry): route Foundry models to services.ai.azure.com (ADR-005)

Adds a 30-line FoundryAuthPolicy that swaps Authorization: Bearer for
api-key and appends api-version=2024-05-01-preview. Gated on
AZURE_FOUNDRY_ENDPOINT + an allowlist in AZUREOPENAIMODEL. Default path
(Azure OpenAI) is untouched. 23 new tests, AOT size unchanged at 8.9 MB.

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>
```

## What a bad commit looks like

```
updates
```

If your commit message would embarrass you in a blame output three months from now, rewrite it.
