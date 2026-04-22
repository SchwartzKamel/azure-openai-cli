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
| [`safety-clause.md`](./safety-clause.md) | `SAFETY_CLAUSE` refusal string — what it is, where it's applied, override behavior (H4/M1). |
| [`change-management.md`](./change-management.md) | The contract every persona prompt change must satisfy — version bump, fixture, goldens, eval. |
| [`eval-harness.md`](./eval-harness.md) | Fixture format, runner shape, regression gates. Design-only; runner not yet implemented. |
| [`personas/coder.md`](./personas/coder.md) | `coder` persona spec (v1). |
| [`personas/reviewer.md`](./personas/reviewer.md) | `reviewer` persona spec (v1). |
| [`personas/security.md`](./personas/security.md) | `security` persona spec (v1) — load-bearing `SAFETY_CLAUSE` call-out. |
| [`fixtures/coder.json`](./fixtures/coder.json) | Seed fixtures for the `coder` persona (3 cases incl. prompt-injection). |

## Roadmap (tracked in Maestro audit `docs/audits/docs-audit-2026-04-22-maestro.md`)

- [x] Per-prompt specs for `coder`, `reviewer`, `security` (user-facing three). `architect` / `writer` remain — H1 (partial).
- [~] Prompt-eval harness — **design landed** ([`eval-harness.md`](./eval-harness.md)), runner not yet implemented — H2.
- [ ] Ralph overlay doc (`ralph-overlay.md`) — M2.
- [ ] Model comparison matrix — scheduled when Azure ships a new deployment.

## Conventions

- Every prompt in production gets a `.md` here. No eval, no merge — eventually.
- Source-of-truth for *text* lives in code. Source-of-truth for *intent and
  contract* lives here. A drift between the two is a bug.
- Version prompts via git history. Breaking prompt changes require an ADR.

— *Maestro*
