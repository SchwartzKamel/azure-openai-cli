# Agent Archetypes 🤖

This project uses [GitHub Copilot custom agents](https://gh.io/customagents/config) — specialized AI personas defined in `.github/agents/` that guide Copilot toward domain-specific behavior during development.

## What Are Agent Archetypes?

Agent archetypes are markdown files that configure Copilot with a **name**, **description**, and **system instructions** scoped to a particular role. When invoked, each agent applies its expertise to the task — a product manager thinks about UX and roadmaps, an engineer thinks about correctness and tests.

This project uses a **fleet dispatch pattern**: instead of one catch-all agent, specialized agents collaborate across their domains. A proposal starts with Costanza (product), gets documented by Elaine (docs), implemented by Kramer (code), secured by Newman (security), and modernized by Jerry (DevOps).

## Agent Fleet

| Agent | Role | Specialty | File |
|-------|------|-----------|------|
| **Costanza** | Product Manager | Architecture, UX, latency optimization, feature proposals | [`costanza.agent.md`](.github/agents/costanza.agent.md) |
| **Kramer** | Engineer | C#, Docker, Azure OpenAI, test implementation | [`kramer.agent.md`](.github/agents/kramer.agent.md) |
| **Elaine** | Technical Writer | Documentation, ADRs, guides, clarity | [`elaine.agent.md`](.github/agents/elaine.agent.md) |
| **Jerry** | DevOps Specialist | CI/CD, Dockerfile optimization, dependency management | [`jerry.agent.md`](.github/agents/jerry.agent.md) |
| **Newman** | Security Inspector | Container hardening, secrets, OWASP, supply chain | [`newman.agent.md`](.github/agents/newman.agent.md) |

## How They're Used

```
Feature Idea
    │
    ▼
Costanza ──→ Writes proposal (.md in docs/proposals/)
    │
    ▼
Kramer ───→ Implements code + writes tests
    │
    ▼
Newman ───→ Reviews for security vulnerabilities
    │
    ▼
Elaine ───→ Updates documentation
    │
    ▼
Jerry ────→ Modernizes build/CI, manages dependencies
```

Each agent is stateless — invoke any of them at any time via the Copilot CLI or GitHub Copilot Chat. They can be used individually for focused tasks or composed as a pipeline for larger features.

## Adding a New Agent

1. Create a new file in [`.github/agents/`](.github/agents/) following the naming convention `name.agent.md`
2. Add the YAML frontmatter with `name` and `description`
3. Write system instructions in the markdown body
4. Merge to the default branch to make the agent available

See the [GitHub custom agents documentation](https://gh.io/customagents/config) for format details.
