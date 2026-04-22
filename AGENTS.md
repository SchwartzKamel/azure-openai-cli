# Agent Archetypes 🤖

This project uses [GitHub Copilot custom agents](https://gh.io/customagents/config) -- specialized AI personas defined in `.github/agents/` that guide Copilot toward domain-specific behavior during development.

## What Are Agent Archetypes?

Agent archetypes are markdown files that configure Copilot with a **name**, **description**, and **system instructions** scoped to a particular role. When invoked, each agent applies its expertise to the task -- a product manager thinks about UX and roadmaps, an engineer thinks about correctness and tests.

This project uses a **fleet dispatch pattern**: instead of one catch-all agent, specialized agents collaborate across their domains. The **main cast** of 5 drives the core build-ship loop; a bench of 20 **supporting players** -- NBC-producer style -- fills in the executive, release, marketing, QA, legal, ethics, a11y, i18n, competitive, perf, chaos, style, and advocacy beats that keep the show on the air. **25 agents total.**

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

The supporting players are the executive suite and service bench behind the main cast -- the producers, lawyers, publicists, and testers who ship the show. They coordinate scope, releases, messaging, quality, and legal coverage so the main cast can focus on product and code.

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
| **Frank Costanza** | SRE / Observability / Incident Response | SLOs, opt-in telemetry, reliability signals, incident runbooks | [`frank.agent.md`](.github/agents/frank.agent.md) |
| **The Maestro** | Prompt Engineering / LLM Research | Prompt library, model A/B, eval harness, temperature cookbook | [`maestro.agent.md`](.github/agents/maestro.agent.md) |
| **Kenny Bania** | Performance Benchmarking | Pre-merge perf benchmarks, regression detection, throughput/latency baselines | [`bania.agent.md`](.github/agents/bania.agent.md) |
| **Mickey Abbott** | Accessibility & CLI Ergonomics | a11y review, screen-reader output, keyboard ergonomics, terminal UX | [`mickey.agent.md`](.github/agents/mickey.agent.md) |
| **Sue Ellen Mischke** | Competitive Analysis & Market Positioning | Competitor tracking, differentiators, positioning briefs | [`sue-ellen.agent.md`](.github/agents/sue-ellen.agent.md) |
| **Keith Hernandez** | DevRel & Conference Speaking | Talk pitches, demo scripts, CFP submissions, stage presence | [`keith.agent.md`](.github/agents/keith.agent.md) |
| **Rabbi Kirschbaum** | AI Ethics & Responsible Use | Ethical guardrails, responsible-AI review, bias and misuse checks | [`rabbi.agent.md`](.github/agents/rabbi.agent.md) |
| **Babu Bhatt** | i18n / Localization | Translations, locale handling, Unicode correctness, RTL support | [`babu.agent.md`](.github/agents/babu.agent.md) |
| **Russell Dalrymple** | UX / Presentation Standards | Visual polish, output formatting, presentation consistency | [`russell.agent.md`](.github/agents/russell.agent.md) |
| **Mr. Wilhelm** | Process & Change Management | Change control, process adherence, merge protocol, handoffs | [`wilhelm.agent.md`](.github/agents/wilhelm.agent.md) |
| **The Soup Nazi** | Code Style & Merge Gatekeeping | Formatting, style enforcement, strict merge gates -- no soup for you | [`soup-nazi.agent.md`](.github/agents/soup-nazi.agent.md) |
| **FDR (Franklin Delano Romanowski)** | Adversarial Red Team / Chaos Engineering | Red-team exercises, fault injection, chaos scenarios, attack paths | [`fdr.agent.md`](.github/agents/fdr.agent.md) |

## How They're Used

The fleet runs as a multi-phase pipeline. Not every phase fires for every change -- small bugfixes skip most of it -- but at release boundaries and for anything user-visible, the full cast shows up.

```text
                              Feature Idea
                                   │
                                   ▼
┌─────────────────────────────── PLANNING ───────────────────────────────┐
│  Mr. Pitt ──→ Costanza ──→ Sue Ellen (competitive) ──→ Rabbi (ethics)  │
└────────────────────────────────┬───────────────────────────────────────┘
                                 ▼
┌──────────────────────────────── DESIGN ────────────────────────────────┐
│  Maestro (prompt) ──→ Russell (UX) ──→ Mickey (a11y) ──→ Babu (i18n)   │
└────────────────────────────────┬───────────────────────────────────────┘
                                 ▼
┌───────────────────────── IMPLEMENTATION ───────────────────────────────┐
│              Kramer  ⇄  Puddy  ⇄  Morty (cost-audit)                   │
└────────────────────────────────┬───────────────────────────────────────┘
                                 ▼
┌──────────────────────────────── TESTING ───────────────────────────────┐
│       FDR (red team / chaos)  ⇄  Bania (perf benchmarks)               │
└────────────────────────────────┬───────────────────────────────────────┘
                                 ▼
┌─────────────────────────────── HARDENING ──────────────────────────────┐
│       Newman (security)  ⇄  Frank (reliability / SLOs)                 │
└────────────────────────────────┬───────────────────────────────────────┘
                                 ▼
┌────────────────────────────── MERGE GATES ─────────────────────────────┐
│    Soup Nazi (style)  ⇄  Wilhelm (process)  ⇄  Jackie (licensing)      │
└────────────────────────────────┬───────────────────────────────────────┘
                                 ▼
┌─────────────────────────── RELEASE & LAUNCH ───────────────────────────┐
│        Jerry (CI)  ──→  Mr. Lippman (release)  ──→  🚢 Ship            │
└────────────────────────────────┬───────────────────────────────────────┘
                                 ▼
┌──────────────────────── COMMUNITY & ADVOCACY ──────────────────────────┐
│  Peterman (copy)  ⇄  Keith (speaking)  ⇄  Uncle Leo (community)        │
│              ⇄  Bob (packaging)  ⇄  Elaine (docs)                      │
└────────────────────────────────┬───────────────────────────────────────┘
                                 ▼
┌───────────────────────────── OPERATIONS ───────────────────────────────┐
│       Frank (SLOs / incidents)  ⇄  Morty (cost watch)                  │
└────────────────────────────────────────────────────────────────────────┘
```

Each agent is stateless -- invoke any of them at any time via the Copilot CLI or GitHub Copilot Chat. They can be used individually for focused tasks or composed as a pipeline for larger features. The supporting players are optional for small changes but become essential at release boundaries and for anything user-visible.

## Adding a New Agent

1. Create a new file in [`.github/agents/`](.github/agents/) following the naming convention `name.agent.md`
2. Add the YAML frontmatter with `name` and `description`
3. Write system instructions in the markdown body
4. Merge to the default branch to make the agent available

See the [GitHub custom agents documentation](https://gh.io/customagents/config) for format details.

## Skills -- the verbs every agent follows

Agents are *nouns* (who). Skills are *verbs* (how). Every cast member -- human or AI -- follows the same skill procedures so we don't relearn the same lesson twice. Skills live in [`.github/skills/`](.github/skills/).

| Skill | Purpose | File |
|-------|---------|------|
| **preflight** | Format + build + test + integration before every code commit | [`preflight.md`](.github/skills/preflight.md) |
| **commit** | Conventional Commits, Copilot trailer, push rules | [`commit.md`](.github/skills/commit.md) |
| **ci-triage** | Diagnose and fix-forward a red CI run | [`ci-triage.md`](.github/skills/ci-triage.md) |

**Enforcement:** The Soup Nazi blocks merges that skipped **preflight**. Mr. Wilhelm blocks commits that skipped **commit**. Jerry + Frank own **ci-triage** escalation. None of it is optional.

The existence of these skills is a debt we paid in real incidents -- commit `180d64f` shipped without `dotnet format` and left `main` red for five consecutive runs before `ec03a37` cleaned it up. Every skill file is a ward against that class of mistake.
