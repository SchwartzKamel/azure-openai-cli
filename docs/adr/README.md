# Architecture Decision Records (ADRs)

This directory captures significant architectural and technical decisions made
on `azure-openai-cli`, following [Michael Nygard's ADR format][nygard].

## What is an ADR?

An ADR is a short, immutable document describing **one decision**, the
**context** that forced it, and the **consequences** (good and bad) the team
accepts by making it. ADRs are not design docs or tutorials — they are the
"why" behind non-obvious choices, written at the moment the decision is made.

## When to write one

Write an ADR when a change:

- Locks in a technology, dependency, or runtime target (e.g. Native AOT, .NET
  version, container base image).
- Establishes a cross-cutting convention that future contributors must follow
  (e.g. JSON source-generator usage, error-handling strategy).
- Reverses or supersedes a prior ADR.
- Has trade-offs significant enough that someone will ask "why did we do it
  this way?" six months later.

Skip an ADR for routine refactors, bug fixes, or reversible choices.

## Conventions

- **Filename**: `ADR-NNN-short-kebab-title.md`, zero-padded three-digit index.
- **Status**: one of `Proposed`, `Accepted`, `Deprecated`, `Superseded by
  ADR-XXX`. Include the acceptance date (`YYYY-MM-DD`).
- **Immutability**: once `Accepted`, do not edit the Context/Decision sections.
  Supersede with a new ADR and mark the old one `Superseded`.
- **Length**: target 100–200 lines. If it's longer, it's probably a design
  doc, not an ADR.
- **Template sections** (Nygard): Title, Status, Context, Decision,
  Consequences, Alternatives Considered, References.

## Index

| ID       | Title                                                        | Status                 |
| -------- | ------------------------------------------------------------ | ---------------------- |
| ADR-001  | [Native AOT as the Recommended Publish Mode](./ADR-001-native-aot-recommended.md) | Accepted — 2026-04-19 |
| ADR-002  | [Squad Persona + Memory Architecture](./ADR-002-squad-persona-memory.md) | Accepted — 2026-04-09 |
| ADR-003  | [Behavior-Driven Development in xUnit](./ADR-003-behavior-driven-development.md) | Accepted — 2026-04-20 |
| ADR-004  | [Speed-gated hybrid adoption of Microsoft Agent Framework](./ADR-004-agent-framework-adoption.md) | Accepted — 2026-04-20 |
| ADR-005  | [Azure AI Foundry endpoint routing](./ADR-005-foundry-routing.md) | Proposed — 2026-04-20 |
| ADR-006  | [NVIDIA NIM / NVFP4 Provider Integration](./ADR-006-nvfp4-nim-integration.md) | Proposed — 2026-04-23 |
| ADR-007  | [Security Guardrails for Third-Party HTTP Inference Providers](./ADR-007-third-party-http-provider-security.md) | Proposed — 2026-04-23 |
| ADR-008  | [Benchmarking Policy for GPU / Non-CI-Gated Providers](./ADR-008-gpu-provider-bench-policy.md) | Proposed — 2026-04-23 |

## Who writes them?

Anyone proposing a significant change opens an ADR as part of the same PR.
Reviewers discuss the ADR before the implementation is merged. Once merged,
the ADR is the record of record — do not rewrite history in later PRs.

[nygard]: https://cognitect.com/blog/2011/11/15/documenting-architecture-decisions
