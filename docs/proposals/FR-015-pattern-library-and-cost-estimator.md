# FR-015 -- Curated Pattern Library + Pre-Flight Cost Estimator

**Status:** Stub (Costanza + Morty, 2026-04-20)
**Related:** FR-008 (response cache), `docs/cost-optimization.md`

## Problem Statement

Two user-facing wins, shippable as one thematic release:

1. **Pattern library.** fabric's hundreds of curated prompts are the single biggest "why would I switch?" moment users cite when comparing CLIs. az-ai ships zero. A small, Azure-tuned, opinionated set (`summarize`, `fix-grammar`, `commit-message`, `explain-code`, `shell-oneliner`, ~15 patterns) invoked via `az-ai pattern run <name>` would close the "out-of-the-box usefulness" gap without requiring the user to compose a prompt -- which is perceived latency we can eliminate entirely.
2. **Pre-flight cost estimator.** `--estimate` / `--dry-run-cost` prints predicted tokens × current rate card before the request fires, and a running daily/monthly total lives at `az-ai budget`. This turns our FinOps annex from a README claim into a UX feature, directly defending Sue Ellen's "cheapest in class" pitch and giving Morty telemetry-free budget enforcement.

## Competitive Gap Justification

- **Pattern library:** fabric ships the most complete library; aichat has "roles"; llm has prompt templates via plugins. az-ai is the only major actively-maintained CLI shipping zero prompt primitives.
- **Cost estimator:** No competitor ships a first-class pre-flight estimator in the CLI itself. Claude Code has usage dashboards (post-hoc, web). gh copilot has quota bars (post-hoc). A synchronous pre-flight estimator is a clean whitespace win and maps 1:1 onto the Morty/Azure-PAYG narrative.

## Rough Effort

**Small-to-medium (2/5).** Patterns are static embedded resources + a tiny runner that composes `--system` + `--persona` behind the scenes. Cost estimator reuses existing tokenizer hooks + a small rate-card JSON (Azure OpenAI 2026 pricing, updated quarterly). ~1 engineer-week for a shippable v1.
