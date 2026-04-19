# Agent Archetypes 🤖

This project uses [GitHub Copilot custom agents](https://gh.io/customagents/config) — specialized AI personas defined in `.github/agents/` that guide Copilot toward domain-specific behavior during development.

## What Are Agent Archetypes?

Agent archetypes are markdown files that configure Copilot with a **name**, **description**, and **system instructions** scoped to a particular role. When invoked, each agent applies its expertise to the task — a product manager thinks about UX and roadmaps, an engineer thinks about correctness and tests.

This project uses a **fleet dispatch pattern**: instead of one catch-all agent, specialized agents collaborate across their domains. The **main cast** drives the core build-ship loop; a bench of **supporting players** — NBC-producer style — fills in the executive, release, marketing, QA, and legal beats that keep the show on the air.

## Agent Fleet

### Main Cast

| Agent | Role | Specialty | File |
|-------|------|-----------|------|
| **Costanza** | Product Manager | Architecture, UX, latency optimization, feature proposals | [`costanza.agent.md`](.github/agents/costanza.agent.md) |
| **Kramer** | Engineer | C#, Docker, Azure OpenAI, test implementation | [`kramer.agent.md`](.github/agents/kramer.agent.md) |
| **Elaine** | Technical Writer | Documentation, ADRs, guides, clarity | [`elaine.agent.md`](.github/agents/elaine.agent.md) |
| **Jerry** | DevOps Specialist | CI/CD, Dockerfile optimization, dependency management | [`jerry.agent.md`](.github/agents/jerry.agent.md) |
| **Newman** | Security Inspector | Container hardening, secrets, OWASP, supply chain | [`newman.agent.md`](.github/agents/newman.agent.md) |

### Supporting Players

The supporting players are the executive suite and service bench behind the main cast — the producers, lawyers, publicists, and testers who ship the show. They coordinate scope, releases, messaging, quality, and legal coverage so the main cast can focus on product and code.

| Agent | Role | Specialty | File |
|-------|------|-----------|------|
| **Mr. Pitt** | Executive / Program Manager | Roadmap, OKRs, cross-agent coordination, scoping | [`mr-pitt.agent.md`](.github/agents/mr-pitt.agent.md) |
| **Mr. Lippman** | Release Manager | SemVer decisions, CHANGELOG curation, release notes | [`mr-lippman.agent.md`](.github/agents/mr-lippman.agent.md) |
| **J. Peterman** | Storyteller / Marketing | Hero copy, demo scripts, launch announcements | [`peterman.agent.md`](.github/agents/peterman.agent.md) |
| **David Puddy** | QA / Test Engineer | Regression suites, flakiness triage, adversarial tests | [`puddy.agent.md`](.github/agents/puddy.agent.md) |
| **Jackie Chiles** | Legal / OSS Licensing | License compliance, third-party attribution, legal review | [`jackie.agent.md`](.github/agents/jackie.agent.md) |
| **Morty Seinfeld** | FinOps / Cost Watchdog | Token budgets, model economics, spend analysis | [`morty.agent.md`](.github/agents/morty.agent.md) |
| **Bob Sacamano** | Integrations / Partnerships | Homebrew/Scoop/Nix, VS Code extension, ecosystem packaging | [`bob.agent.md`](.github/agents/bob.agent.md) |
| **Uncle Leo** | DevRel / Community | Contributor onboarding, issue triage, tone stewardship | [`uncle-leo.agent.md`](.github/agents/uncle-leo.agent.md) |

## How They're Used

```
Feature Idea
    │
    ▼
Mr. Pitt (scopes) ──→ Costanza (product proposal) ──→ docs/proposals/
    │
    ▼
Kramer (implements) ⇄ Puddy (tests adversarially) ⇄ Morty (cost-audits)
    │
    ▼
Newman (security) ⇄ Jackie (license/legal)
    │
    ▼
Elaine (technical docs) ⇄ Peterman (marketing copy) ⇄ Bob (packaging/integrations)
    │
    ▼
Jerry (DevOps polish) ──→ Mr. Lippman (release) ──→ 🚢 Ship
                                                    │
                                                    ▼
                                              Uncle Leo (community)
                                              ──→ 📣 Welcome new users
                                              ──→ 🛠  Triage issues
                                              ──→ 👋 Onboard contributors
```

Each agent is stateless — invoke any of them at any time via the Copilot CLI or GitHub Copilot Chat. They can be used individually for focused tasks or composed as a pipeline for larger features. The supporting players are optional for small changes but become essential at release boundaries and for anything user-visible.

## Adding a New Agent

1. Create a new file in [`.github/agents/`](.github/agents/) following the naming convention `name.agent.md`
2. Add the YAML frontmatter with `name` and `description`
3. Write system instructions in the markdown body
4. Merge to the default branch to make the agent available

See the [GitHub custom agents documentation](https://gh.io/customagents/config) for format details.
