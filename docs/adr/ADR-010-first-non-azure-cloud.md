# ADR-010 -- First Non-Azure Cloud Provider

- **Status**: Accepted -- 2026-05-06
- **Deciders**: Costanza (PM, lead), Sue Ellen Mischke (competitive review), Larry David (showrunner sign-off), Kramer (eng), Newman (sec), Morty (FinOps), Maestro (prompts)
- **Related**:
  - [ADR-007 -- Third-Party HTTP Provider Security](./ADR-007-third-party-http-provider-security.md) -- the six guardrails every non-Azure provider inherits
  - [ADR-009 -- Default Model Resolution](./ADR-009-default-model-resolution.md) -- the resolution chain S03E06 generalised to four layers; ADR-010 sits alongside it
  - [FR-014 -- Local Preferences and Multi-Provider](../proposals/FR-014-local-preferences-and-multi-provider.md) -- the umbrella FR this episode realises
  - [`docs/exec-reports/s03-blueprint.md`](../exec-reports/s03-blueprint.md) -- canonical Arc 2 slate (E08-E13)
  - [`docs/exec-reports/s03e06-the-schema.md`](../exec-reports/s03e06-the-schema.md) -- the schema (drawer)
  - [`docs/exec-reports/s03e07-the-redactor.md`](../exec-reports/s03e07-the-redactor.md) -- the redactor (lock on the drawer)

## Context

The schema landed (E06). The redactor landed (E07). The Provider Abstraction Seam now has a place to put providers and a guarantee that no provider's auth header escapes a stack trace. The drawer exists. The lock works. The next question -- the only question Arc 2 actually has to answer -- is *which provider walks through the door first*.

Until E06, az-ai was Azure-only because the credential helper, the model resolver, and the dispatcher were all hard-wired to `AZUREOPENAIENDPOINT` + `AZUREOPENAIAPI` + `AZUREOPENAIMODEL`. There was no abstraction to pick *across*. The decision was implicit and the decision was Azure. After E06's `preferences.json` v1 schema and E07's centralised `SecretRedactor`, the decision is no longer implicit. We can now register a second provider in `preferences.providers{}`, route through it via an active profile, and emit any error it throws without leaking the bearer. The technical floor is in.

What is *not* in is the adapter that implements a non-Azure provider's wire protocol. That work is E09 and beyond. E08's job is the architectural pick: of the realistic candidates -- Anthropic Claude, OpenAI direct, AWS Bedrock, Google Vertex / Gemini, and a long tail of OpenAI-compatible aggregators (Cloudflare Workers AI, Together, Groq) -- which one does the project commit to as the *first* non-Azure cloud, and what shape does that commitment lock in for everything downstream?

The blueprint draft made a recommendation. It said: "OpenAI direct, because it slots straight into the generic OpenAI-compat adapter with zero bridge code." This ADR is where that recommendation becomes a decision. Sue Ellen filed a competitive case for Anthropic in the writers' room -- Claude is what users are asking for, *right now*, by name -- and that case is documented honestly in §Alternatives. The decision still goes to OpenAI. The reasoning is below.

The decision matters because it sets the *abstraction shape* of the entire arc, not just the first adapter. Picking OpenAI commits the project to "OpenAI-compat first" as the seam. Every future provider either (a) speaks OpenAI-compat and rides the same adapter as a profile preset, or (b) needs its own adapter implementation. The blast radius of that choice extends through Arc 3 (local providers, E14-E19), Arc 4 (switch ergonomics, E20-E23), and into S04. Pick wrong here and we are still paying for it in S05.

## Decision

**OpenAI direct is the first non-Azure cloud provider.** It is implemented via the generic `OpenAiCompatAdapter` shipped in S03E09 *The Compat*. The adapter is built once and serves every OpenAI-compatible endpoint -- OpenAI's own API, Cloudflare Workers AI, Together, Groq, and (via Arc 3) Ollama, llama.cpp, NIM, and vLLM -- as configuration presets, not as separate adapters.

The first profile to land in `preferences.profiles{}` against the new adapter is `openai`, pointing at `https://api.openai.com/v1`, authed by a bearer token resolved from `OPENAI_API_KEY` (env) or the per-OS keychain (S03E10).

## Rationale

Six reasons, in the order they were argued in the writers' room. The blueprint's headline -- "zero bridge code" -- is reason #1; the rest are why that headline survives scrutiny.

### 1. Wire-protocol compatibility -- zero bridge code

Azure OpenAI's chat-completions request and response shapes are a near-superset of OpenAI's public API. The two surfaces share message structure, tool/function-call format, streaming chunk shape, finish-reason vocabulary, usage accounting fields, and content-part envelope. The deltas that exist (Azure's `api-version` query parameter, Azure's `api-key` header vs OpenAI's `Authorization: Bearer`, Azure's deployment-name path segment vs OpenAI's model-id field) are *transport-layer* deltas, not *model-layer* deltas. They live in the adapter's request builder and never leak into the rest of the codebase.

Anthropic does not share this property. Anthropic's Messages API uses a different request envelope (`messages` array with a separate `system` field rather than a system-role message), a different streaming protocol (`event:` SSE frames with a custom event vocabulary -- `message_start`, `content_block_delta`, `message_delta`, `message_stop`), a different tool-call shape (tool_use blocks within content arrays rather than a `tool_calls` array on the message), and a different stop-reason vocabulary. Adapting Anthropic is a real translation, not a header swap. That work is justified -- Claude is excellent and users want it -- but it is not "zero bridge code." It is a separate adapter on its own schedule.

### 2. Streaming and tool-call parity

The two highest-risk surfaces of any new provider are streaming and tool-calling. Streaming because the chat-loop UX depends on first-token latency feeling near-instant; a parity bug here is felt by every user on every call. Tool-calling because agent mode (`--agent`), Ralph mode (`--ralph`), and the squad personas all depend on a tool-call round-trip that survives the provider boundary.

OpenAI's streaming protocol is the protocol az-ai already speaks against Azure -- same chunk shape, same delta semantics, same `finish_reason` vocabulary. Tool calling is the same `tool_calls` array on the assistant message, same `tool_call_id` correlation, same `role: "tool"` reply shape. The capability matrix that S03E13 *The Stream* will freeze for v3.0 has *Azure* and *OpenAI* in the same column with no asterisks. Anthropic's tool_use blocks would land in a different column with at least three asterisks (different shape, different streaming envelope, different content-array contract).

Picking the surface we already know is tested unlocks E13's parity-test work as a *verification* episode rather than a *bring-up* episode. That is the difference between a one-week and a three-week E13.

### 3. Auth surface -- already covered by the v2.1.1 redactor

OpenAI direct uses a single bearer token: `Authorization: Bearer sk-...`. The S03E07 redactor's pattern #1 (the headline P1 case) is *exactly* this shape. The api-key/x-api-key pattern (#2) and the AZUREOPENAIAPI env-export pattern (#3) cover the Azure side. No new redactor pattern is required for OpenAI direct -- the existing six already scrub it on every error path, and the `P1_BearerTokenNeverAppearsInRedactedOutput` contract test guards the regression.

Anthropic uses an `x-api-key` header. The redactor's pattern #2 already catches that shape. So Anthropic is also covered for redaction. Auth-surface coverage is not what differentiates the two; it is even.

### 4. Cost story -- no surprise regression for current users

Per Morty's spot-check (full receipt episode is S03E12), OpenAI's published per-token rates for the comparable SKUs (`gpt-4o-mini`, `gpt-4o`) are within 5-15% of Azure's published rates for the same models. A user who points az-ai at OpenAI direct instead of Azure does not experience a step-change in their per-call cost. The fallback model (`gpt-4o-mini`, per ADR-009) exists on both sides and is priced comparably on both sides.

Anthropic's cost surface is not directly comparable -- different models, different pricing tiers, different effective per-task spend. Comparing Claude pricing against `gpt-4o-mini` is an apples-and-walnuts exercise, and the receipt episode (S03E12) would have to render that asymmetry honestly. With OpenAI direct, the receipt is "Azure: $0.0021 / OpenAI: $0.0018" -- a number a user can act on. With Anthropic, the receipt is a paragraph.

This is a Morty point. Morty signs off on it in the receipt episode; this ADR records the directional argument.

### 5. Local-provider compatibility -- the multiplier

This is the reason the decision is not close.

Picking OpenAI's wire protocol does not just unlock OpenAI direct. It unlocks every local OpenAI-compatible runtime in the ecosystem -- Ollama, llama.cpp `llama-server`, NVIDIA NIM, vLLM, and the long tail of community endpoints (Cloudflare Workers AI, Together, Groq, Fireworks, Perplexity API) that all expose `/v1/chat/completions` against an OpenAI-shaped envelope. *Every one of these becomes a configuration preset against the same adapter*, not a separate adapter implementation.

Arc 3 of the S03 blueprint (E14-E19, *First Local Provider*) is entirely premised on this multiplier. S03E14 *The Daemon* -- Ollama via the OpenAI-compat adapter -- is scoped as "zero new code, only docs + a `providers.ollama` example profile." That scope only holds if Arc 2 lands the OpenAI-compat adapter as the seam. If Arc 2 picks Anthropic instead, Arc 3 has to bring up its own local-provider adapter from scratch and the whole Arc 3 schedule slips by weeks.

Anthropic does not have this multiplier. There is no "Anthropic-compatible" local runtime. Picking Anthropic first commits the project to one provider for the same adapter cost that OpenAI commits us to roughly *eight to twelve* providers (one cloud + every local runtime + every OpenAI-compat aggregator). One adapter, many providers, vs one adapter, one provider. The multiplier is the decision.

### 6. Maintenance burden -- one adapter, many providers

Every adapter the project ships is a maintenance surface forever. Streaming protocol drift, tool-call shape drift, error-envelope drift, retry-after semantics, model-list endpoint drift -- all of it has to be regression-tested against the live upstream forever. The cheapest adapter is the one that serves the most providers. OpenAI-compat is that adapter.

This is a Wilhelm-flavoured argument and Wilhelm would phrase it as a process risk: the project does not have the bench depth to maintain two adapter codepaths in parallel through S04 and S05 without one of them rotting. Picking the multiplier-adapter first means the project has at most one adapter to keep healthy through the rest of S03 and into S04. The Anthropic adapter, when it lands (see below), will be the *second* adapter and will be staffed and scheduled accordingly.

## Alternatives considered

Five alternatives were on the table. Each got a fair hearing. None won.

### Anthropic Claude

The serious challenger. Sue Ellen's competitive brief flags Anthropic as the "table stakes" provider users will ask for next -- by name, in issue trackers, in DevRel conversations, in conference Q&A. Claude is widely regarded as best-in-class for code-adjacent tasks (large-context reasoning, careful refusals, instruction following) and the brand recognition rivals OpenAI's in the developer segment. If the decision were "which provider do users want most?", Anthropic would win.

The decision is not that. The decision is "which provider unlocks the most arc work for the least adapter cost?" Anthropic loses that decision on three independent axes:

1. **Adapter cost.** Bespoke request/response models (Messages API, not chat-completions). Bespoke streaming protocol (named SSE events, not OpenAI-shape deltas). Bespoke tool-call shape (tool_use blocks within content arrays). None of this is hard work; all of it is *new* work, scheduled against a single provider rather than a family.
2. **No multiplier.** No local runtime speaks Anthropic-compat. Picking Anthropic does not unlock Arc 3.
3. **Sequencing.** Anthropic done *second* is cheap (the seam is in, the redactor patterns are in, the keychain namespace is in, the wizard knows how to ask "which provider?"). Anthropic done *first* would require all of those to be either Anthropic-shaped or generic-from-day-one, and the latter is over-engineering on speculation.

**Disposition.** Anthropic is deferred to **S03 Arc 4 or S04 Arc 1**. A follow-up FR is reserved -- placeholder **FR-024 -- Anthropic Claude Adapter** -- to be written by Costanza after S03E13 freezes the v3.0 capability matrix. The FR will scope: (a) the bespoke adapter, (b) the streaming-protocol adapter, (c) the tool_use translation layer, (d) the capability-matrix asterisks the matrix will need. Sue Ellen owns the competitive update that lands alongside the FR.

This is the deferral that will generate the most user complaints. We are documenting it explicitly so when Sue Ellen brings the complaint volume to a writers' room meeting in S03E20-ish, the decision and the deferral plan are both already on the record.

### AWS Bedrock

Bedrock is structurally interesting because it is a *gateway* to multiple model families (Anthropic, Mistral, Meta, Amazon Titan, Cohere) behind a single API surface. One adapter, many models, sounds like the multiplier pitch from §5 -- but it is not the same multiplier. Bedrock adds an AWS-specific dependency surface that the rest of az-ai does not carry: SigV4 request signing, STS token refresh, region-scoped endpoints, IAM-role-vs-access-key auth, and an AWS SDK dependency that is heavyweight in AOT terms (large reflection surface, bigger binary, slower cold start). Per ADR-001, AOT-friendliness is not negotiable.

Bedrock is also second-tier on brand recognition relative to OpenAI direct and Anthropic direct -- users who want Claude tend to want it via Anthropic's own API, not via Bedrock. The Bedrock case is real for a specific class of enterprise user, but it is not the right *first* non-Azure cloud.

**Disposition.** Deferred indefinitely. Revisit if and when a multi-cloud enterprise FR materialises with a sponsor and a budget.

### Google Vertex AI / Gemini

Vertex carries an OAuth flow plus a GCP-project scoping requirement plus a per-region endpoint convention plus a service-account JSON-key auth pattern. Every one of those is a UX heavy lift and at least one of them (OAuth refresh on a single-binary CLI with no persistent daemon) is genuinely hard. The Gemini API surface itself is cleaner, but the *enrolment* surface is the issue.

**Disposition.** Deferred. Revisit after Arc 4 ships and the wizard is mature enough to handle multi-step OAuth onboarding without making the first-run experience worse.

### Cloudflare Workers AI / Together / Groq / Fireworks / Perplexity

All five expose OpenAI-compatible `/v1/chat/completions` endpoints. They are not separate adapters under this decision -- they are *profile presets* against the same `OpenAiCompatAdapter` that S03E09 ships. Picking OpenAI direct as the first cloud is what makes these come along for free.

**Disposition.** Documented as profile presets in the S03E11 wizard work and the post-E13 docs sweep. No additional ADRs required; they ride ADR-010.

### Status quo (Azure-only forever)

Not a serious alternative; included for completeness. The S03 mission statement and FR-014 both commit the project to multi-provider as a first-order roadmap priority, second only to MCP. Doing nothing here would invalidate Arc 2, Arc 3, and Arc 4, and would leave FR-014 / FR-018 / FR-019 / FR-020 stranded on a dependency that never materialised.

## Consequences

### Positive

- **Arc 3 unlocks for free.** Ollama, llama.cpp, NIM, and vLLM all ride the OpenAI-compat adapter as profile presets. S03E14 *The Daemon* stays scoped at "zero new code, only docs."
- **S03E11 wizard simplifies.** First-run prompts the user for "azure" or "openai", writes a `default` profile against either, and stops. No per-provider branching code path. Adding a third provider later is a `presets[]` entry, not a wizard rewrite.
- **S03E12 receipts can A/B Azure vs OpenAI directly.** Same model class, same per-token rate-card structure, same `--verbose` cost line. Morty gets a clean comparison surface.
- **S03E13 stream parity tests are verification, not bring-up.** The capability matrix freezes against two providers in the same column. Asterisks are reserved for future non-OpenAI-compat providers.
- **The redactor's six patterns are sufficient.** No new pattern required for OpenAI direct. The existing P1 contract test guards the regression.
- **FR-018 / FR-019 / FR-020 lose their FR-014 dependency hold.** All three were blocked on "preferences.json + adapter shape." E08 unblocks the adapter-shape half.

### Negative

- **Anthropic users wait.** The most user-visible deferral. Sue Ellen will receive complaints. We have planned the deferral (FR-024 placeholder, post-E13 scheduling) and named the owner (Costanza writes the FR; Sue Ellen owns the competitive comms). This is the cost we accept for the multiplier.
- **The "OpenAI-compat first" commitment narrows future provider work.** Any future non-OpenAI-compat provider (Anthropic, Bedrock, Vertex) requires its own adapter, not an extension of the existing one. This is a real constraint, not a free lunch. Documented here so future contributors do not assume the adapter is generic.
- **One competitive note we own.** Some user segments (Anthropic-curious developers, large-context-reasoning workloads) will see "OpenAI direct shipped first" as a signal about az-ai's affiliation. It is not -- the project is provider-agnostic by design -- but the optics will require a Peterman comms note when E13 lands.

### Neutral

- **ADR-010 commits the project to "OpenAI-compat first" as the abstraction shape.** Future non-OpenAI-compat providers (Anthropic, Bedrock, Vertex AI) require separate adapter work, not extension of `OpenAiCompatAdapter`. This is the architectural lock-in this ADR creates. It is judged worth the multiplier.
- **The legacy Azure environment variables stay supported forever.** `AZUREOPENAIENDPOINT` / `AZUREOPENAIAPI` / `AZUREOPENAIMODEL` continue to resolve to the `azure` provider preset via the resolution chain. No migration is forced on existing users by this decision.
- **The bearer-token auth pattern becomes the default mental model.** New providers will be assumed bearer-authed unless documented otherwise. Azure's `api-key` header is now the special case, not the default.

## Implementation plan

Five episodes, in narrative order. Each named episode is a separate exec report; this ADR is the spine.

- **S03E09 -- *The Compat*.** Implement `azureopenai-cli/Providers/OpenAiCompatAdapter.cs` -- HTTP client, bearer auth, base-URL resolution, optional `OpenAI-Organization` header, request/response shape against `chat.completions`. Wire behind `--provider openai` and `preferences.providers["openai"]`. **Lead: Kramer.** Acceptance: a passing integration test that hits a recorded OpenAI fixture and a dry-run path for CI.
- **S03E10 -- *The Keychain*.** Extend the per-OS credential store from S02E04 *The Locksmith* to namespace by provider: `az-ai/openai/api_key` distinct from `az-ai/azure/api_key`. **Lead: Newman.** Acceptance: keychain round-trip tests on each platform shim, ADR-007 §2 compliance preserved per provider.
- **S03E11 -- *The Wizard, Reprise*.** First-run wizard learns to ask "which provider?" and writes a `default` profile pointing at the chosen provider. Empty-input fallback selects `azure` to preserve current behaviour. **Lead: Jerry.** Acceptance: first-run smoke against a clean home directory writes a valid `preferences.json` for either choice.
- **S03E12 -- *The Receipt*.** Per-provider rate-card stubs in the cost path -- enough to render `Azure: $0.0021 / OpenAI: $0.0018` in `--verbose`, no smart picking yet. **Lead: Morty Seinfeld.** Acceptance: cost line renders both rates when the active profile is OpenAI; renders only Azure when not.
- **S03E13 -- *The Stream*.** Verify streaming + tool-call parity on the new adapter; freeze the v3.0 capability matrix. **Lead: Puddy.** Acceptance: parity test suite green against both Azure and OpenAI for streaming chunk shape, tool-call round trip, finish-reason vocabulary, and usage accounting.

Sequencing rule: E09 must land before E10/E11/E12; E10/E11 may run in parallel; E12 depends on E10 (rate cards keyed by provider); E13 depends on E09 + at least one of {E10, E11}.

## Compliance checklist for downstream work

Anything that lands in Arc 2, Arc 3, or Arc 4 that touches provider dispatch, credential resolution, error formatting, or capability advertisement must satisfy the following before review:

- [ ] If the change adds a provider, the provider is registered as a *profile preset* against `OpenAiCompatAdapter` unless a separate ADR justifies a bespoke adapter (currently: only Azure has one; Anthropic, when shipped, will land its own ADR).
- [ ] If the change adds a credential, the credential is namespaced by provider in the keychain (`az-ai/<provider>/<key-name>`) per S03E10. No global, un-namespaced credential keys are added after this ADR.
- [ ] If the change emits an error message that could carry an upstream payload, the message routes through `SecretRedactor.Redact` (or `RedactException`). No new `Console.Error.WriteLine` call sites bypass the redactor.
- [ ] If the change advertises a capability (`streaming`, `tool_calls`, `vision`, `json_mode`, etc.), the capability is recorded in the v3.0 capability matrix S03E13 freezes. No silent capability drift between providers.
- [ ] If the change touches the cost path, it carries a per-provider rate-card row keyed by the provider name in `preferences.providers{}`. Morty's S03E12 stub is the schema; do not reinvent.
- [ ] If the change is "we should also support Anthropic / Bedrock / Vertex," the change is scoped as its own FR with its own ADR. Do not extend `OpenAiCompatAdapter` with non-OpenAI-compat shapes.

The gate is not yet mechanical. It will be added to `make exec-report-check` after the v3.0 capability matrix lands in S03E13.

## How this changes day-to-day work

For users, after the full Arc 2 ships:

```bash
# Pick OpenAI for one call:
az-ai --provider openai "summarise this diff" < diff.patch

# Pick OpenAI as the active profile for the session:
export AZ_PROVIDER=openai

# Pick OpenAI as the persistent default (writes to preferences.json):
az-ai --config set defaultProvider=openai

# Show what's resolved and where it came from:
az-ai --config show
```

The `--config show` output paints the resolved provider, endpoint, model, and active profile, each with its source label per the four-layer chain ADR-009 generalised. No part of this surface is OpenAI-specific; the same commands work against Azure, against any future OpenAI-compat profile preset (Together, Groq, Cloudflare Workers AI), and against the Arc 3 local providers.

For maintainers, after this ADR:

- The default mental model for "a new provider" is: *write a profile preset, not an adapter*. Adapter work requires an ADR.
- The redactor pattern set is *closed* for the OpenAI-compat family. Adding a pattern is a security-architecture decision and gets its own review.
- The capability matrix is the source of truth for "does this work on provider X?". `--agent` against a provider that does not advertise `tool_calls` refuses cleanly per S03E18 *The Capability Gate*.

## Cross-references

- [ADR-007 -- Third-Party HTTP Provider Security](./ADR-007-third-party-http-provider-security.md) -- six guardrails inherited per provider
- [ADR-009 -- Default Model Resolution](./ADR-009-default-model-resolution.md) -- the four-layer resolution chain extended in S03E06
- [FR-014 -- Local Preferences and Multi-Provider](../proposals/FR-014-local-preferences-and-multi-provider.md) -- the umbrella FR
- [`docs/exec-reports/s03-blueprint.md`](../exec-reports/s03-blueprint.md) §"Arc 2 -- First Non-Azure Cloud (E08-E13)"
- [`docs/exec-reports/s03e06-the-schema.md`](../exec-reports/s03e06-the-schema.md) -- preferences.json v1
- [`docs/exec-reports/s03e07-the-redactor.md`](../exec-reports/s03e07-the-redactor.md) -- centralised secret scrubber
- [`docs/exec-reports/s03e08-the-pick.md`](../exec-reports/s03e08-the-pick.md) -- the episode this ADR shipped in
- **FR-024 (placeholder)** -- Anthropic Claude Adapter, to be authored by Costanza after S03E13

## Sign-off

- **Costanza (PM, lead author).** The multiplier is the decision. OpenAI-compat first is not a preference, it is the only choice that makes Arc 3 cheap. Anthropic is a real user ask and we will ship it -- second, not first, on a schedule we control. *It is not a lie if you believe it. And I believe this is the cheapest correct answer on the table.*
- **Sue Ellen Mischke (competitive review).** Filed the Anthropic case for the record. Acknowledges the deferral risk. Owns the competitive update that lands with FR-024. Will absorb complaint volume in the interim. *Users will ask for Claude. They are asking. Right now.*
- **Larry David (showrunner sign-off).** Approved as written. Episode S03E08 *The Pick* dispatched. S03E09 *The Compat* on the schedule for the next slot. The compat starts tomorrow.

*That's the decision. Move on.*
