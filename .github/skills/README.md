# Skills 🛠️

Reusable, **agent-agnostic** procedures that every contributor -- human or AI -- follows. Skills are the *verbs* of this repo; agents in [`../agents/`](../agents/) are the *nouns*. Any cast member may invoke any skill.

Skills exist because we've paid the tax of *not* having them. The canonical example: commit `180d64f` (Foundry routing) shipped without running `dotnet format`. Five CI runs went red on `main` before The Soup Nazi cleaned it up in `ec03a37`. Every skill file below encodes a lesson we learned the hard way.

## Catalog

| Skill | When to run | File |
|-------|-------------|------|
| **preflight** | Before every `git commit` that touches `.cs`, `.csproj`, `.sln`, or CI config | [`preflight.md`](preflight.md) |
| **commit** | Every commit (message format, trailer, signing) | [`commit.md`](commit.md) |
| **ci-triage** | When CI goes red on `main` or a PR | [`ci-triage.md`](ci-triage.md) |

## How agents use skills

Each skill file is a short checklist + the exact commands. Agents (see [`../../AGENTS.md`](../../AGENTS.md)) are expected to:

1. Read the relevant skill file before acting
2. Execute every step -- no skipping "because it's a small change"
3. If a skill step fails, STOP and report. Do not push past a red signal.

The Soup Nazi will reject PRs that skipped **preflight**. Mr. Wilhelm will reject commits that skipped **commit**. There is no negotiation.

## Adding a skill

Open a PR that adds `<verb>.md` to this directory, linked from the catalog above. A skill earns its place by being:

- **Invokable by any agent** (no persona-specific voice)
- **Runnable as-is** (exact commands, no prose paraphrase)
- **Small** (one page or less -- if it's bigger, it's probably two skills)
