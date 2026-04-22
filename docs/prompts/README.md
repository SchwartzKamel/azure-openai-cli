# `docs/prompts/` — Prompt Library

> *It's Maestro. With an M. This is the score the orchestra plays from.*

Canonical, versioned documentation for every prompt this CLI puts in front of
a model. Source prompts live in code (AOT-safe); the `.md` files here are the
spec — intent, inputs, expected output shape, known failure modes — and the
code is the implementation of that spec.

## Current contents

| Doc | Subject |
|---|---|
| [`temperature-cookbook.md`](./temperature-cookbook.md) | Recommended `--temperature` per task category (H3). |
| [`safety-clause.md`](./safety-clause.md) | `SAFETY_CLAUSE` refusal string — what it is, where it's applied, override behavior (H4). |

## Roadmap (tracked in Maestro audit `docs/audits/docs-audit-2026-04-22-maestro.md`)

- [ ] Per-prompt READMEs for the five Squad defaults (coder, reviewer, architect, writer, security) — H1.
- [ ] Prompt-eval harness (`tests/prompts/`, golden snapshots) — H2.
- [ ] Ralph overlay doc (`ralph-overlay.md`) — M2.
- [ ] Model comparison matrix — scheduled when Azure ships a new deployment.

## Conventions

- Every prompt in production gets a `.md` here. No eval, no merge — eventually.
- Source-of-truth for *text* lives in code. Source-of-truth for *intent and
  contract* lives here. A drift between the two is a bug.
- Version prompts via git history. Breaking prompt changes require an ADR.

— *Maestro*
