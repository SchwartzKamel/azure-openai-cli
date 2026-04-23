# Season 4 -- Blueprint -- *Model Intelligence*

> *"Multi-provider was the orchestra. This season is the conductor.
> A score with no one waving the baton is just a hundred people
> tuning."* -- The Maestro, S04 writers' room kickoff

**Status:** pre-season treatment, awaiting showrunner greenlight.
**Showrunner:** Larry David.
**Lead voice:** The Maestro (prompt engineering, eval, A/B).
**Co-lead:** George Costanza (smart-defaults UX).
**Successor to:** S03 -- *Local & Multi-Provider* (the `IModelProvider`
seam, llama.cpp / gemma.cpp / NIM adapters).
**Predecessor to:** S05 -- *Protocols & Plugins* (MCP client/server,
plugin registry).

---

## Showrunner note

S03 broke the Azure-only assumption. By the time S03 wraps, `az-ai`
will speak to at least three provider families through one
`IModelProvider` seam, and the user can pick a backend by env-var or
config. That is necessary -- and not sufficient. The instant a user
has more than one model available, the next question lands on their
desk: *which one for this prompt, right now?* S04 answers that
question on the user's behalf, conservatively, and gives the power
user the seams to override and prove it. The core promise: the
default behavior gets smarter without the surface area getting
weirder.

## Theme statement

"Smart" in a CLI is not a marketing word and not a magic word. It
means three small, boring things, in this order: **(1)** the default
model for a given prompt is *defensibly* the right one, with the
defense written down in a model card; **(2)** the user can override
the default with a one-flag escape hatch and never gets locked in;
**(3)** there is a deterministic, scriptable way to *prove* a prompt
got better or worse when something changed -- model, temperature,
system prompt, or routing rule. No agent framework, no orchestrator,
no LLM-judging-an-LLM ceremony. Just defaults that don't suck and an
eval seam that a power user can run from their own shell.

The second paragraph is about discipline. Every feature in S04 has to
clear two bars: it must improve the *default* behavior of the tool
(no flag required) **and** it must produce machine-readable output
that the eval harness can consume. If a feature can't be evaluated,
it can't ship -- because we cannot promise users it will not regress
in S05 or S06. Maestro has the marker on this. Costanza has the marker
on whether the default behavior actually feels smart or just
*aggressive* (the difference between a router and a back-seat driver).

## Why this season, why now

Three forces converged in 2026 that make this the right season to
ship:

1. **Routing matured.** LiteLLM, OpenRouter, Portkey, NotDiamond, and
   Martian all consolidated around the same primitives -- per-request
   strategy hints (`cost`, `latency`, `quality`), cascading fallback
   chains, and provider-side observability. The patterns are stable
   enough to *port*, not invent.
2. **Eval went CLI-shaped.** Inspect (UK AISI), Promptfoo, and DeepEval
   all ship with a `tool eval <suite> --model <x>` invocation that
   looks suspiciously like ours could. The "eval is a Python notebook"
   era is ending; the "eval is a regression suite" era is here.
3. **Model count exploded.** With S03 landing local + commercial
   non-Azure, a fresh-install user can plausibly have 6-12 models
   visible. Without smart defaults, the UX collapses into "pick from
   the list and hope." Without an eval seam, the team cannot tell
   whether a default change helped or hurt.

If we don't ship this season, we ship *more* models in S05 (via MCP
servers exposing remote models) on top of a defaults engine that
hasn't been written. That ordering is wrong.

## Landscape snapshot (2026)

Compact comparison of the routing layers we surveyed against the
axes that matter for *this* CLI (single-binary, AOT, .NET 10, no
mandatory daemon).

| Layer       | Strategy axes                | Eval support      | .NET SDK         | Single-binary fit | Det. cache | Semantic cache |
|-------------|------------------------------|-------------------|------------------|-------------------|------------|----------------|
| LiteLLM     | cost / latency / fallback    | external (logs)   | OpenAI-compat    | proxy-shaped      | yes (Redis)| via plugin     |
| OpenRouter  | quality / cost / availability| dashboard only    | OpenAI-compat    | SaaS only         | server-side| no             |
| NotDiamond  | quality (learned router)     | first-class       | OpenAI-compat    | SaaS only         | no         | no             |
| Martian     | cost / SLA (enterprise)      | enterprise        | OpenAI-compat    | proxy-shaped      | yes        | optional       |
| Helicone    | observability (not routing)  | logs + replays    | OpenAI-compat    | proxy-shaped      | yes        | yes (managed)  |
| Portkey     | cost / latency / fallback    | dashboard         | OpenAI-compat    | proxy-shaped      | yes        | yes            |
| **az-ai S04** | **cost / latency / quality, local rules** | **baked-in CLI** | **native** | **single AOT binary** | **on disk (FR-008 lineage)** | **opt-in, embeddings provider-pluggable** |

The takeaway: every credible router is a separate process you call
*through*. We are the opposite shape -- a single binary the user
calls *to*. So we do not adopt any of these wholesale; we adopt their
**vocabulary** (`prefer-cost`, `prefer-latency`, `prefer-quality`,
fallback chains, model cards) and implement it locally. Model cards
follow the Hugging Face structured-front-matter pattern (machine-
readable YAML / JSON header, prose body) so that anyone who already
publishes cards has a path to give us metadata for free.

## 24-episode candidate slate

> Maestro anchors the eval and prompt-shaped episodes (4 leads).
> Costanza anchors the smart-defaults UX arc (4 leads). Kramer takes
> the heavier engineering lifts (4 leads). Elaine owns the
> docs-shaped episodes (3 leads). Jerry covers eval-as-CI (2 leads).
> Newman covers eval-corpus privacy and prompt redaction (2 leads).
> Lloyd Braun gets the learner-facing on-ramp episode (1 lead). The
> remaining slots distribute across Morty, Frank, Bania, Mickey,
> Russell, Wilhelm, Soup Nazi, FDR, and Maestro encore.

### Act I -- Model registry + cards (E01-E04)

- **S04E01 -- *The Card.*** Define the model-card schema (YAML front
  matter, prose body) and ship the first 3 cards (`gpt-4o-mini`,
  `gpt-5.4-nano`, llama-local). *Lead: Maestro.*
- **S04E02 -- *The Inventory.*** Build the in-binary model registry
  loader; cards become embedded resources. *Lead: Kramer.*
- **S04E03 -- *The Capabilities.*** Per-model capability flags
  (tool-calling, JSON mode, vision-in / out, max context, reasoning
  family) and a `--capabilities` query subcommand. *Lead: Costanza.*
- **S04E04 -- *The Reading.*** Docs episode: `docs/models/` becomes
  the rendered card index, with `docs-lint` rules. *Lead: Elaine.*

### Act II -- Smart-defaults engine (E05-E08)

- **S04E05 -- *The Picker.*** `ResolveSmartDefault()` (hinted at in
  ADR-009) becomes a real function: prompt heuristics (length, has
  tools, persona requested) -> default candidate. *Lead: Costanza.*
- **S04E06 -- *The Fallback.*** Cascading fallback when the chosen
  model is unavailable (rate-limit, 5xx, missing deployment).
  *Lead: Kramer.*
- **S04E07 -- *The Onboarding.*** First-run wizard (FR-023 hook)
  surfaces the picker's reasoning ("I picked `gpt-4o-mini` because
  your prompt has tools and is < 4k tokens"). *Lead: Lloyd Braun.*
- **S04E08 -- *The Override.*** `--prefer cost|latency|quality` and
  the audit trail (`--why-this-model`). *Lead: Costanza.*

### Act III -- Eval harness baked in (E09-E12)

- **S04E09 -- *The Score.*** `az-ai eval <suite>` subcommand;
  fixture format from `docs/prompts/eval-harness.md` becomes the
  shipping schema. *Lead: Maestro.*
- **S04E10 -- *The Corpus.*** Ship 3 deterministic test corpora
  (smoke, persona-voice, refusal). Bound their on-disk size.
  *Lead: Maestro.*
- **S04E11 -- *The Gate.*** CI integration: `dotnet test` includes
  `az-ai eval` against pinned model + temperature for goldens.
  *Lead: Jerry.*
- **S04E12 -- *The Redaction.*** Eval corpora must not leak user
  prompts into the binary or telemetry; redaction policy +
  pre-commit check. *Lead: Newman.*

### Act IV -- Caching: deterministic + semantic + correctness-aware (E13-E16)

- **S04E13 -- *The Hash.*** Promote the FR-008 deterministic cache
  to a first-class layer keyed on `(prompt, model, temperature,
  system_prompt, tools_schema)`. *Lead: Kramer.*
- **S04E14 -- *The Echo.*** Optional semantic cache via a pluggable
  embeddings provider (no embedding model bundled by default; user
  brings one). *Lead: Maestro.*
- **S04E15 -- *The Receipt.*** Cache-hit tagging in stderr (`[cache:
  hit/miss/semantic@0.93]`) and a `--no-cache` override. *Lead:
  Russell Dalrymple.*
- **S04E16 -- *The Recall.*** Correctness-aware invalidation: model
  version change, system-prompt change, and tool-schema change all
  bust the cache automatically. *Lead: Maestro.*

### Act V -- Cost estimator + budget guardrails (E17-E20)

- **S04E17 -- *The Estimate.*** `--estimate` (FR-015 hook): tokenize
  locally, multiply by per-model rate card, print before sending.
  *Lead: Kramer.*
- **S04E18 -- *The Card Update.*** Rate-card refresh policy +
  `docs/cost/pricing-sourcing.md` becomes the source of truth that
  feeds the estimator. *Lead: Morty Seinfeld.*
- **S04E19 -- *The Budget.*** `az-ai budget` subcommand; soft cap
  warns, hard cap refuses. *Lead: Costanza.*
- **S04E20 -- *The Receipt II.*** Per-day / per-week / per-model
  rollups in `~/.azureopenai-cli/spend.jsonl`, opt-in. *Lead:
  Frank Costanza.*

### Act VI -- Routing rules engine (E21-E23)

- **S04E21 -- *The Rule.*** Routing rules DSL (the smallest viable
  one): a JSON file mapping `(persona, prompt-class, --prefer)`
  triples to a model + fallback chain. *Lead: Maestro.*
- **S04E22 -- *The Latency Budget.*** Per-rule latency target +
  fallback-on-timeout; Bania benchmarks every rule against goldens.
  *Lead: Kenny Bania.*
- **S04E23 -- *The Quality Gate.*** A rule can declare an eval
  suite as a precondition; if the suite drops below threshold on
  the chosen model, the rule degrades to its fallback automatically.
  *Lead: Maestro.*

### Finale (E24)

- **S04E24 -- *The Conductor.*** Two end-to-end demos: (a) "cheapest
  correct" -- routing rule prefers `gpt-5.4-nano`, falls back to
  `gpt-4o-mini` if eval suite drops; (b) "fastest acceptable" --
  routing rule prefers local llama for < 1s budget, falls back to
  Azure if local quality eval fails. Mr. Lippman cuts the v3.0
  release; Peterman writes the launch copy. *Lead: Maestro and
  Costanza, dual-anchor.*

## Cross-references to FR-NNN proposals

| FR     | Title                                      | S04 episodes         | Status going in    |
|--------|--------------------------------------------|----------------------|--------------------|
| FR-008 | Prompt-response cache                      | E13, E15, E16        | Shipped v2.0.0; promote to layer |
| FR-010 | Model aliases + smart defaults             | E03, E05, E08        | Superseded by FR-014; revisit alias surface |
| FR-014 | Local preferences + multi-provider         | E03, E05             | Phase 1 shipped; Phase 2 hooks here |
| FR-015 | Pattern library + cost estimator           | E17, E18, E19        | Stub; estimator half scoped here, pattern library deferred |
| FR-017 | `max_completion_tokens` compatibility      | E03 (capabilities)   | Shipped v1.9.1; capability flag formalizes the family check |
| FR-021 | Persona `ArgumentException` UX wrap        | E08 (override audit) | Accepted for 2.0.1; routing override path inherits the same wrap |
| FR-023 | First-run wizard                           | E07                  | Accepted; "why this model" panel is the S04 hook |

ADR-009 (default model resolution) is the *canonical* upstream for
the picker; S04E05 implements its `ResolveSmartDefault()` in earnest.

## Risks and known unknowns

1. **Eval as default-on is non-trivial to ship.** A regression suite
   that hits the network on every CI run is fragile and expensive.
   Mitigation: deterministic goldens (fixture-based, model output
   stubbed) for CI; live-eval is opt-in (`--live`) and runs nightly,
   not per-PR.
2. **Semantic cache without an embedding model is a chicken/egg.**
   We will not bundle an embedding model in the AOT binary -- it
   would dwarf the rest of the binary and constrain platforms. We
   ship the seam (`IEmbeddingsProvider`) and let the user bring one
   (Azure OpenAI embeddings, local sentence-transformers via
   llama.cpp, OpenRouter). Default: semantic cache off.
3. **Cost estimator drifts as providers reprice silently.** Azure
   OpenAI pricing has changed mid-quarter at least twice in the
   past year. Mitigation: rate card is a JSON resource with a
   `last_verified` date; `--estimate` warns when the card is
   > 90 days stale; Morty owns the refresh cadence.
4. **Smart defaults cause vendor lock-in over time.** If the
   picker's heuristics happen to favor Azure deployments (because
   they have the most cards), users drift back into a
   single-vendor footprint we just escaped in S03. Mitigation:
   capability-based selection only, never vendor-name-based;
   capability data lives in cards, not in code.
5. **Eval corpora bloat the binary.** A naive embed of three
   corpora pushes the AOT binary past comfortable distribution
   sizes. Mitigation: corpora are a separate `eval-corpus.zip`
   downloaded on first `az-ai eval` and cached in
   `~/.azureopenai-cli/corpora/`; binary stays lean.
6. **Routing rule DSL is its own UX problem.** Every config DSL
   we have ever shipped (squad routing keywords, pattern blocklist)
   has needed a v2 within a season. Mitigation: keep the DSL flat
   (no nesting, no expressions, no templating) for S04; the
   "rules engine" is a lookup table with a fallback list, full
   stop. If we need expressions, that is an S05 conversation.
7. **Eval-corpus prompts may contain PII or proprietary phrasing
   contributed by users.** Newman's E12 mandate is hard: no user
   prompt enters a shipped corpus without explicit consent and a
   redaction pass. Anything else is a privacy incident.
8. **The "why this model" panel can become a wall of text.**
   Russell + Mickey will have to gate the verbosity; a smart
   default that explains itself for 14 lines on every invocation
   is a worse UX than a dumb one that ships in silence.

## What S04 does NOT cover (boundary)

- **NOT** new providers themselves -- that is S03's mandate
  (*Local & Multi-Provider*: llama.cpp, gemma.cpp, NIM, the
  `IModelProvider` seam). S04 *consumes* whatever S03 ships and
  does not add backend adapters.
- **NOT** MCP / external plugin tools -- that is S05
  (*Protocols & Plugins*). The routing engine here is internal;
  exposing it over MCP for external consumers is an S05 question.
- **NOT** multimodal eval. Text-only this season. Image, audio,
  and tool-trace eval are a separate season candidate (likely
  paired with whatever vision work follows S05).
- **NOT** fine-tuning or custom-model-training workflows. S04
  treats the model as a black box you can pick, evaluate, and
  cache around -- not one you train.
- **NOT** an LLM-as-judge eval mode. Goldens and trait assertions
  only this season; judge-model eval is a 2027 conversation.
- **NOT** a managed cloud routing dashboard. We are a CLI. Logs
  go to disk; users who want a dashboard plug Helicone in via the
  observability seam Frank Costanza already owns.

## Open questions for showrunner greenlight

1. **Default-on or opt-in for the smart-defaults engine?** Costanza
   wants on; Maestro wants opt-in for one minor version, then on.
   Both have a point. Larry calls it.
2. **Do we ship a bundled rate card, or fetch one on first run?**
   Bundled is simpler and offline-friendly; fetched is fresher.
   Morty leans bundled-with-staleness-warning; Costanza leans
   fetched-with-cache. Pick one before E17 starts filming.
3. **Is the eval corpus public?** A public corpus invites
   contribution and benchmarking by others; a private one keeps
   our refusal-fixture set out of model training data. Newman
   strongly prefers private; Maestro mildly prefers public-with-
   adversarial-set-private. Showrunner call.
4. **Does the routing rules DSL ship in the binary or as a
   conventionally-located file users edit?** In-binary is faster
   and AOT-friendly; on-disk is more honest about being a
   configuration surface. Probably on-disk; please confirm.
5. **Is "fastest acceptable" allowed to fall back to a
   *cheaper-but-slower* model?** Or must "fastest" only fall back
   to "fastest-available"? This is a one-sentence policy with
   large UX consequences. Costanza wants to write it down before
   E22.

---

*-- The Maestro (lead, prompt and eval discipline), with notes
from George Costanza (smart-defaults UX), Mr. Pitt (program
management), and a quietly enthusiastic Morty Seinfeld
(estimator + budget guardrails are finally on the slate).*
