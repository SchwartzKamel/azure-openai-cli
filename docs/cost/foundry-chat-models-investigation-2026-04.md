# Azure AI Foundry Chat-Completion Models -- Investigation & Recommendation (2026-04-29)

> *"You spent HOW much on a yes/no classifier? With the premium model? Get OUT of here. We are running nanos and we are going to LIKE it."*
> -- Morty

**Author:** Morty Seinfeld (FinOps)
**Date:** 2026-04-29
**Scope:** Azure AI Foundry **chat completion inference** SKUs available to our subscription.
**Audience:** the boss, signing off on a deployment decision today.
**Status:** Recommendation -- adopt **Option B (split nano)**. Implementation lands in a follow-up episode.

---

## 1. TL;DR

- **Recommendation: Option B -- split nano routing.** Use `gpt-5-nano` as the default for short, single-shot, no-tool standard-mode completions; promote to `gpt-5.4-nano` for agent mode, persona/squad mode, Ralph mode, and any call that uses tools, structured output, or context > ~4 KB. One model is not enough; three is too many.
- **Why:** `gpt-5-nano` is the cheapest first-party OpenAI SKU on Foundry that still supports tool calls and streaming, so it pays for the 80% of our traffic that's "expand this snippet" Espanso-style work. `gpt-5.4-nano` is the newer, slightly-pricier sibling that's already proven in our agent / persona / Ralph paths and has the prompt-cache discount we actually use. Splitting captures the cheap-floor savings without giving up the agent-mode quality we already validated.
- **Cost envelope (assumed 10 M input + 2 M output tokens / month, 70/30 standard/agent split):** roughly **$2.40 -- $3.10 / month** at list, before Batch API or prompt-cache discounts. Option A (5.4-nano only) lands around **$4.50 / month**. Option C (nano floor + premium escalation tier) lands **$8 -- $20 / month** depending on escalation rate. Numbers are illustrative; per-token rates with citations are in §4.
- **Headline risks:** (a) `gpt-5-nano` public pricing is **not yet pinned in our `pricing-sourcing.md`** -- we are routing on a rate we have to verify before commit-to-prod (§10); (b) regional availability of both nanos is region-specific and our subscription's region must be confirmed against the Foundry catalog the day we cut over; (c) routing logic adds one branch to `Program.cs` -- a place we have a finite tolerance for branches.
- **Security posture:** Both recommended models are first-party Azure OpenAI SKUs -- same data-handling SLA, same default content filter, same eligibility for Private Endpoints / CMK / abuse-monitoring opt-out as the rest of the Azure OpenAI surface. No third-party / open-weight models in the recommended path. Newman should still sign off, but there are no novel security questions in Option B.

---

## 2. Scope & method

**"Chat completion inference" in Foundry terms** = any model in the Azure AI Foundry model catalog that exposes a `/chat/completions`-shaped REST endpoint (or the equivalent Azure SDK surface) and bills on input / output tokens. This excludes: embeddings, audio (whisper / tts), image generation (DALL-E, gpt-image), fine-tuning, batch-only SKUs, and Provisioned Throughput Units (PTU) -- PTU is a *billing modifier* on top of a chat model, not a separate model.

**Families considered:**

- **Azure OpenAI GPT-5.x family** -- `gpt-5`, `gpt-5-mini`, `gpt-5-nano`, `gpt-5.4-nano` (current operational default per ADR-009).
- **Azure OpenAI GPT-4.x family** -- `gpt-4o`, `gpt-4o-mini`, `gpt-4.1`, `gpt-4.1-mini`.
- **Azure OpenAI o-series reasoning** -- `o1-mini`, `o3-mini`, `o4-mini` (where catalogued in our region).
- **Microsoft small models** -- `Phi-4-mini-instruct`, `Phi-4-mini-reasoning`.
- **Third-party / open-weight on Foundry MaaS** -- `DeepSeek-V3.2`, `Mistral-Small`, `Mistral-Large`, `Llama-3.x-Instruct` family.

**Out of scope:** any model not catalogued in Azure AI Foundry as of 2026-04-29; any Anthropic / Google model (those are competitor peer-comparison, see `docs/cost/pricing-sourcing.md` §1).

**How pricing was sourced:**

1. Existing rows in [`docs/cost/pricing-sourcing.md` §1](pricing-sourcing.md) -- canonical for any model already deployed.
2. Hardcoded `PriceTable` in [`azureopenai-cli/Observability/CostHook.cs`](../../azureopenai-cli/Observability/CostHook.cs) -- canonical for what the CLI reports today.
3. Public Azure OpenAI pricing page ([azure.microsoft.com/en-us/pricing/details/cognitive-services/openai-service/][az]) -- canonical for first-party OpenAI SKUs.
4. Azure AI Foundry model catalog (<https://ai.azure.com> -> Model catalog -> *Pricing* tab) -- canonical for MaaS / third-party SKUs and the only place some rates render.
5. Microsoft Community Hub announcement posts -- secondary corroboration for newly-launched SKUs (Phi-4, DeepSeek).

**Cutoff date:** 2026-04-29. Any rate not verified on or after this date in this document is marked `*pending verification*` and **must** be confirmed before the rate is wired into `CostHook.cs` `PriceTable`.

**Citations:** every numeric cell in §4 has a footnote pointing at one of the sources above. If a cell shows `*pending*`, the corresponding follow-up is enumerated in §10.

---

## 3. The candidate field

| Model | Family | Input $/M | Cached $/M | Output $/M | Context | Tool calls? | Streaming? | Default content filter | Region availability (one-line) | Notes |
|---|---|---:|---:|---:|---:|:---:|:---:|:---:|---|---|
| `gpt-5-nano`              | GPT-5 (1st-party AOAI) | *pending verification* [^1] | *pending* [^1] | *pending* [^1] | 128K [^1] | Yes | Yes | On (4-category) | Global Standard; subset of US/EU regions [^1] | Cheaper sibling of `gpt-5.4-nano`. **Not yet in `PriceTable`.** Pricing-sourcing.md row required. |
| `gpt-5.4-nano`            | GPT-5 (1st-party AOAI) | 0.20 [^2] | 0.10 [^2] | 1.25 [^2] | 128K [^2] | Yes | Yes | On (4-category) | Global Standard; subset of US/EU regions [^2] | Current ops default per ADR-009. Output 6.25x input -- cap `--max-tokens`. |
| `gpt-5-mini`              | GPT-5 (1st-party AOAI) | *pending verification* [^3] | *pending* [^3] | *pending* [^3] | 128K [^3] | Yes | Yes | On (4-category) | Global Standard [^3] | Mid-tier 5.x. Worth tracking but not a candidate today -- nanos cover the value floor, premium tier should jump to o-series or 4.1, not stop at 5-mini. |
| `gpt-5` (flagship)        | GPT-5 (1st-party AOAI) | 1.25 [^2] | 0.125 [^2] | 10.00 [^2] | 256K [^2] | Yes | Yes | On (4-category) | Global Standard [^2] | Flagship, peer-comparison row already in pricing-sourcing.md. Not a default candidate. |
| `gpt-4o-mini`             | GPT-4 (1st-party AOAI) | 0.15 [^2] | 0.075 [^2] | 0.60 [^2] | 128K [^2] | Yes | Yes | On (4-category) | Global Standard [^2] | The old default. Cheap, proven, but quality on agent / persona use cases is the reason we left it (ADR-009 / cost-optimization.md §3.7). |
| `gpt-4o`                  | GPT-4 (1st-party AOAI) | 2.50 [^2] | 1.25 [^2] | 10.00 [^2] | 128K [^2] | Yes | Yes | On (4-category) | Global Standard [^2] | Premium peer of `gpt-4.1`. No reason to deploy alongside 4.1 unless you specifically need vision-on-this-deployment. |
| `gpt-4.1`                 | GPT-4 (1st-party AOAI) | 3.00 [^2] | 1.50 [^2] | 12.00 [^2] | 1M [^2] | Yes | Yes | On (4-category) | Global Standard [^2] | Marked "estimated" in current `CostHook.cs`. Premium escalation candidate for Option C. |
| `gpt-4.1-mini`            | GPT-4 (1st-party AOAI) | *pending verification* [^4] | *pending* [^4] | *pending* [^4] | 1M [^4] | Yes | Yes | On (4-category) | Global Standard [^4] | Long-context-but-cheap-ish niche. Not a default candidate while nanos are sufficient. |
| `o1-mini`                 | o-series reasoning (AOAI) | 3.00 [^2] | 1.50 [^2] | 12.00 [^2] | 128K [^2] | Limited [^2] | Yes | On (4-category) | Global Standard [^2] | Reasoning tokens bill as **output**. Easy to set fire to money here; cap `--max-tokens` aggressively. |
| `o3-mini`                 | o-series reasoning (AOAI) | *pending verification* [^5] | *pending* [^5] | *pending* [^5] | 200K [^5] | Yes | Yes | On (4-category) | Global Standard [^5] | Newer reasoning SKU; better tool-call support than o1-mini per the announcement. Premium escalation candidate. |
| `o4-mini`                 | o-series reasoning (AOAI) | *pending verification* [^5] | *pending* [^5] | *pending* [^5] | 200K [^5] | Yes | Yes | On (4-category) | Global Standard [^5] | Newest small reasoning SKU. Confirm catalog presence in our region before quoting. |
| `Phi-4-mini-instruct`     | Microsoft (MaaS)  | 0.075 [^6] | n/a [^6] | 0.30 [^6] | 128K [^6] | Limited [^6] | Yes | **Off by default** on MaaS [^7] | Foundry MaaS regions only [^6] | Cheapest catalogued SKU. Tool-call support is uneven on MaaS endpoints -- not a drop-in for agent mode. |
| `Phi-4-mini-reasoning`    | Microsoft (MaaS)  | 0.08 [^6] | n/a [^6] | 0.32 [^6] | 128K [^6] | Limited [^6] | Yes | **Off by default** on MaaS [^7] | Foundry MaaS regions only [^6] | Reasoning small model. Same MaaS caveats as above. |
| `DeepSeek-V3.2`           | DeepSeek (MaaS)   | 0.58 [^8] | n/a [^8] | 1.68 [^8] | 128K [^8] | Yes | Yes | **Off by default** on MaaS [^7] | Foundry MaaS regions only [^8] | Open-weight provenance. See §6 for the security tradeoff -- not in the recommended path. |
| `Mistral-Small`           | Mistral (MaaS)    | *pending verification* [^9] | n/a | *pending* [^9] | 32K [^9] | Yes | Yes | **Off by default** on MaaS [^7] | Foundry MaaS regions only [^9] | Niche; no compelling reason to deploy alongside Phi for the small-model slot. |
| `Mistral-Large`           | Mistral (MaaS)    | *pending verification* [^9] | n/a | *pending* [^9] | 128K [^9] | Yes | Yes | **Off by default** on MaaS [^7] | Foundry MaaS regions only [^9] | Premium MaaS flagship. Not in the recommended path. |
| `Llama-3.x-Instruct`      | Meta (MaaS)       | *pending verification* [^10] | n/a | *pending* [^10] | 128K [^10] | Yes | Yes | **Off by default** on MaaS [^7] | Foundry MaaS regions only [^10] | Open-weight; CMK / Private Endpoint story is weaker than first-party. Not in the recommended path. |

[^1]: `gpt-5-nano` -- public pricing not yet pinned in `pricing-sourcing.md`. Source path: Azure AI Foundry catalog ([ai.azure.com](https://ai.azure.com) -> Model catalog -> `gpt-5-nano` -> Pricing tab) plus the Azure OpenAI pricing page [Azure OpenAI pricing][az]. Verification command: `az cognitiveservices account deployment list --name "$AZURE_OPENAI_RESOURCE" --resource-group "$AZURE_RG" --query '[?properties.model.name==`gpt-5-nano`]'`. **Status: pending verification before commit-to-prod.** See §10 OQ-1.

[^2]: Existing row in [`docs/cost/pricing-sourcing.md` §1](pricing-sourcing.md). Source: [Azure OpenAI pricing][az]. Inherits the verification status of that row.

[^3]: `gpt-5-mini` -- not on file in `pricing-sourcing.md`. Source path same as [^1]. Not a default candidate; verification deferred.

[^4]: `gpt-4.1-mini` -- not on file in `pricing-sourcing.md`. Source path: [Azure OpenAI pricing][az] -> `gpt-4.1-mini` row. Not a default candidate; verification deferred.

[^5]: `o3-mini` / `o4-mini` -- not on file in `pricing-sourcing.md`. Source path: [Azure OpenAI pricing][az] -> o-series section. Premium escalation candidates for Option C; verification required only if Option C is adopted.

[^6]: Existing row in [`docs/cost/pricing-sourcing.md` §1](pricing-sourcing.md), sourced from the [MS Community Hub Phi-4 announcement][msch-phi]. Verified 2026-04-22.

[^7]: Foundry MaaS deployments do not always inherit the Azure OpenAI default content filter -- the content-filter policy is a per-deployment configuration. "Off by default" means "the operator has to opt in to a content-filter policy when creating the MaaS deployment." Confirm in: [Azure AI content safety docs][acs] and the deployment's `properties.raiPolicyName` field via `az cognitiveservices account deployment show`.

[^8]: Existing row in [`docs/cost/pricing-sourcing.md` §1](pricing-sourcing.md). Source: [cloudprice.net][cp] + [MS Community Hub announcement][msch]. Verified 2026-04-22.

[^9]: `Mistral-Small` / `Mistral-Large` -- not on file. Source path: Foundry catalog -> Mistral entries -> Pricing tab. Not a default candidate; verification deferred.

[^10]: `Llama-3.x-Instruct` -- not on file. Source path: Foundry catalog -> Meta entries -> Pricing tab. Not a default candidate; verification deferred.

[az]: https://azure.microsoft.com/en-us/pricing/details/cognitive-services/openai-service/
[cp]: https://cloudprice.net/
[msch]: https://techcommunity.microsoft.com/category/azureaiservices
[msch-phi]: https://techcommunity.microsoft.com/category/azureaiservices
[acs]: https://learn.microsoft.com/en-us/azure/ai-services/content-safety/

**Cross-reference to `pricing-sourcing.md`:** rows for `gpt-5-nano`, `gpt-5-mini`, `gpt-4.1-mini`, `o3-mini`, `o4-mini`, `Mistral-Small`, `Mistral-Large`, and `Llama-3.x-Instruct` are **not yet present** in §1 of `pricing-sourcing.md`. **Follow-up: add rows for `gpt-5-nano` (required for Option B) and any o-series/4.1-mini SKU we actually deploy.** The other rows in §3 of this doc are catalogued for completeness; they do not require provenance entries unless we deploy them.

---

## 4. Value tiers

### Tier 1 -- Workhorse cheap (sub-$0.50 / M input)

- `Phi-4-mini-instruct` ($0.075 / $0.30) -- **floor of the catalog**, but tool-calling is uneven and the MaaS content-filter posture is opt-in (see §6). Good for batch, classification, and other non-agent workloads. **Trap: not a drop-in for agent mode.**
- `Phi-4-mini-reasoning` ($0.08 / $0.32) -- same caveats as instruct; slightly stronger on stepwise tasks per the MS Community Hub claims. Same MaaS caveats.
- `gpt-4o-mini` ($0.15 / $0.60) -- **proven, boring, on-platform.** First-party content filter, full tool-call support, ADR-009-grade fallback. The model we left for quality reasons, not cost reasons.
- `gpt-5-nano` (*pending pricing*) -- expected to land between `gpt-4o-mini` and `gpt-5.4-nano`. **The pivot model in Option B.** Punches above its weight if its tool-call quality matches `gpt-5.4-nano`'s -- which we have to validate before trusting it in agent mode (see Option B routing rule, §7).
- `gpt-5.4-nano` ($0.20 / $1.25) -- current ops default. Output multiple is **6.25x input** -- the trap is letting agent / Ralph mode generate without a `--max-tokens` cap and watching the output column dominate the bill.

### Tier 2 -- Mid-range value

- `DeepSeek-V3.2` ($0.58 / $1.68) -- decent value/quality on paper, but provenance and security posture (see §6) push it out of the recommended path for our use cases. **Trap: open-weight on MaaS is a different security envelope than first-party; do not deploy without Newman sign-off.**
- `gpt-4.1-mini` (*pending*) -- if pricing lands competitively, this is the long-context cheap-ish model. Niche -- our current workloads do not push 1M context.
- `o3-mini` / `o4-mini` (*pending*) -- if priced near `o1-mini`'s $3 / $12, they sit in mid-range *only* on input. Output multiple makes them premium-tier in practice for any reasoning-heavy run.

### Tier 3 -- Premium reasoning

- `gpt-4.1` ($3.00 / $12.00) -- premium escalation candidate for Option C. Long context (1M), strong tool-call support, first-party SLA.
- `gpt-4o` ($2.50 / $10.00) -- redundant with `gpt-4.1` for our workloads; only deploy if you need the specific vision/audio surface.
- `gpt-5` flagship ($1.25 / $10.00) -- input is a steal at $1.25, output bites at $10. Strong escalation candidate if Option C goes premium-flagship rather than premium-reasoning.
- `o1-mini` ($3.00 / $12.00) -- reasoning tokens bill as output. **Trap: this is where money goes to die if the prompt does not bound `--max-tokens` and reasoning depth.**

**Headline:** the value frontier for our workloads is in Tier 1. Tiers 2 and 3 are escalation paths, not floors. The 80% of our traffic that's "expand this Espanso snippet" deserves a Tier 1 model and nothing more.

---

## 5. Security posture

This is the cost-vs-security tradeoff narrative. Newman owns the deeper review; the table below summarises the points that matter for *which model we route to*.

| Concern | First-party AOAI (gpt-5.x, gpt-4.x, o-series) | Microsoft MaaS (Phi-4) | Third-party MaaS (DeepSeek, Mistral, Llama) |
|---|---|---|---|
| Data residency | Honors region setting; Global Standard pins to a geo. | Region-pinned, MaaS pool. | Region-pinned, MaaS pool; provenance of weights is non-Azure. |
| Default content filter | **On** (4-category Microsoft default policy). | Opt-in per deployment (`raiPolicyName`). | Opt-in per deployment. |
| Private Endpoint / VNet | **Supported.** | Supported on most regions. | Supported but inconsistent across SKUs -- verify per model. |
| Customer-Managed Keys (CMK) | **Supported** at the Cognitive Services account level. | Supported at the account level. | Inherits the account; verify the model is included. |
| Abuse-monitoring opt-out | Available for eligible customers via Microsoft form (sensitive workloads). | Same process. | Same process where applicable; not all open-weight SKUs are eligible. |
| Provenance / SLA | **First-party OpenAI SLA via Microsoft.** | Microsoft-published weights. | Open-weight, third-party origin. SLA is the Azure delivery surface, not the model author. |

**Implications for the deployment recommendation:**

- Recommended Option B is **all first-party AOAI** (`gpt-5-nano` + `gpt-5.4-nano`). No new security questions; we inherit the same posture as today's `gpt-5.4-nano`-only deployment.
- Option C with `gpt-4.1` or an o-series escalation tier is also all first-party. No new questions.
- Any option that includes Phi-4 or a third-party MaaS model **adds** a security review: `raiPolicyName` must be set, Newman signs off on the abuse-monitoring posture, and `pricing-sourcing.md` gets a row even if the rate is already known, because the security posture row needs to be on file.

The cost-vs-security tradeoff: Phi-4 is **half the cost** of `gpt-5-nano` on input, but the cost of one Newman review cycle plus the operational risk of running a different content-filter posture on a subset of traffic outweighs the savings at our token volume. Revisit if/when our monthly volume crosses the threshold in §8.

---

## 6. Three deployment options

**Assumed usage envelope** (state once, reuse below): **10 M input tokens / month, 2 M output tokens / month**. Split: 70% standard mode (Espanso/AHK snippet expansion -- short prompts, short completions, no tools), 30% agent / persona / Ralph mode (longer prompts, tool calls, structured output). All numbers are at **list price, Global Standard, no Batch API or PTU discounts**. Realised spend will be lower if the prompt-cache or Batch modifiers apply.

### Option A -- Single-model conservative (`gpt-5.4-nano` only)

- **Composition:** `AZUREOPENAIMODEL=gpt-5.4-nano`. One deployment, one row in `PriceTable`, one model card in `docs/prompts/model-cards.md`. Status quo.
- **Routing rule:** none. Every call hits `gpt-5.4-nano`.
- **Estimated monthly cost:** 10 M x $0.20 + 2 M x $1.25 = **$2.00 + $2.50 = $4.50 / month** at list. With prompt-cache hits on the 30% repeat-prefix agent traffic, realistic spend is **$3.50 -- $4.00 / month**.
- **Pros:** zero routing logic, zero new pricing rows, one model to monitor, ADR-009 already accepts this default, every existing benchmark already uses it.
- **Cons:** the cheap 70% standard-mode traffic is paying a small premium it doesn't need. Output multiple is 6.25x; one runaway Ralph loop without `--max-tokens` is the dominant tail risk.
- **Who it fits:** us, today, if the boss wants zero implementation work. Defensible. Boring. Predictable. Not what the boss asked for.

### Option B -- Split nano (RECOMMENDED)

- **Composition:** `AZUREOPENAIMODEL=gpt-5-nano,gpt-5.4-nano`. Two deployments, two `PriceTable` rows, one routing branch in `Program.cs`.
- **Routing rule** (spell it out so Kramer can implement it without re-reading this section):

  ```text
  Route to gpt-5-nano when ALL of:
    - mode == "standard"           (no agent loop, no Ralph)
    - tools registered for call == 0
    - persona memory not loaded     (no squad / persona invocation)
    - estimated input tokens < 4000 (rough proxy for "short snippet")

  Otherwise route to gpt-5.4-nano. Specifically:
    - mode in {"agent", "persona", "ralph"}                 -> 5.4-nano
    - any tool registered for the call (even if not invoked) -> 5.4-nano
    - input prompt >= 4000 tokens (long context / docs)      -> 5.4-nano
    - structured-output / JSON-schema mode requested         -> 5.4-nano
  ```

  Rationale: the "short, no-tool, standard-mode" lane is where `gpt-5-nano` earns its keep. Anything that benefits from the newer model's quality (tool-call reliability, longer-context retention, structured-output adherence) gets the 5.4. The 4000-token cutoff is a pragmatic proxy; revisit once we have a few weeks of telemetry.

- **Estimated monthly cost** (illustrative -- `gpt-5-nano` rate is *pending verification*):
  - Assume `gpt-5-nano` lands at roughly $0.10 / $0.40 per 1M (between `gpt-4o-mini` and `gpt-5.4-nano`; **substitute the verified rate before committing this routing live**).
  - Standard-mode share: 70% x 10 M input = 7 M input, 70% x 2 M output = 1.4 M output -> 7 x $0.10 + 1.4 x $0.40 = **$0.70 + $0.56 = $1.26**.
  - Agent / persona / Ralph share: 30% x 10 M input = 3 M input, 30% x 2 M output = 0.6 M output -> 3 x $0.20 + 0.6 x $1.25 = **$0.60 + $0.75 = $1.35**.
  - **Total: ~$2.61 / month at list.** With prompt-cache hits on the agent share, realistic spend is **$2.10 -- $2.50 / month**.
  - Sensitivity: if `gpt-5-nano` actually lands at the same rate as `gpt-4o-mini` ($0.15 / $0.60), total is roughly **$3.00 / month**. If it lands at half `gpt-5.4-nano` ($0.10 / $0.625), total is roughly **$2.55 / month**. Either way, **cheaper than Option A**, and the savings scale with standard-mode share.

- **Pros:** captures the cheap-floor savings on the dominant traffic class without giving up the agent-mode quality we already validated. Both rails are first-party AOAI -- no new security review. Routing rule is one branch and one config knob; trivially A/B-able via a telemetry tag.
- **Cons:** two models to monitor; two `PriceTable` rows to keep verified; one new `pricing-sourcing.md` entry required before commit-to-prod. If `gpt-5-nano` quality regresses on standard-mode prompts in a way the routing rule does not catch, we collapse to Option A. That is a recoverable failure mode, not a one-way door.
- **Who it fits:** the boss. The traffic profile we actually have (mostly standard-mode Espanso). Anyone who wants measurable savings without the variance of a premium escalation tier.

### Option C -- Nano floor + premium escalation tier

- **Composition:** `AZUREOPENAIMODEL=gpt-5-nano,gpt-5.4-nano,gpt-4.1` **or** `AZUREOPENAIMODEL=gpt-5-nano,gpt-5.4-nano,o3-mini`. Three deployments, three `PriceTable` rows, two routing branches.
- **Routing rule:** Option B's rule, plus an escalation lane:

  ```text
  Escalate to gpt-4.1 (or o3-mini) when ANY of:
    - explicit --model override at the CLI flag
    - persona/squad routing rule names "architect" or "reviewer"
    - delegate_task subagent depth > 0 AND task category in {"design", "review"}
    - prompt requests reasoning ("think step by step", chain-of-thought triggers)
  ```

- **Estimated monthly cost:** Option B baseline ($2.10 -- $3.00) + escalation tier. If 5% of traffic escalates to `gpt-4.1` ($3 / $12): 0.5 M input + 0.1 M output -> $1.50 + $1.20 = **+$2.70 / month**, total **$4.80 -- $5.70 / month**. If 10% escalates: total **$7.50 -- $8.40 / month**. If 20%: **$13 -- $14 / month**. Variance is the cost.
- **Pros:** real upside on the genuinely-hard 5% of tasks where premium reasoning earns its keep. Same security posture (all first-party). Lets us claim "we use the premium model where it counts" without burning premium rates on snippet expansion.
- **Cons:** three models, two routing branches, escalation-rate is the dominant variance driver and we have no telemetry baseline for it. Premium-tier rates have higher drift risk (per `pricing-sourcing.md` §2 drift policy; gpt-4.1 row is currently marked "estimated"). Easier to misconfigure -- a Ralph loop that escalates is a runaway-spend candidate.
- **Who it fits:** us in 6 months, after we have telemetry from Option B and a credible escalation-rate baseline. Not today.

---

## 7. Recommendation

**Adopt Option B -- split nano (`gpt-5-nano` + `gpt-5.4-nano`).** Implement the routing rule in §6 Option B. Tag every call with the model and mode in OpenTelemetry so we can A/B in production.

**Trigger conditions to re-evaluate:**

- **Re-evaluate -> Option C** if monthly token volume crosses **50 M input** OR if a recurring class of "we needed the premium model and didn't have it" complaints surfaces in `docs/runbooks/finops-runbook.md` monthly review.
- **Re-evaluate -> Option A (collapse to single model)** if `gpt-5-nano` shows a regression in standard-mode quality the routing rule does not catch -- specifically, if Maestro's eval harness shows > 5% regression on the standard-mode test set vs `gpt-5.4-nano` baseline.
- **Re-evaluate pricing rows** quarterly per `pricing-sourcing.md` §2 cadence. Drift > 15% on either nano triggers an ADR amendment (per ADR-009 / `cost-optimization.md` §3.7).
- **Re-evaluate region** if the Foundry catalog removes either nano from our subscription's region (this is the only thing that makes Option B silently fail).

The boss has the wallet. These are the receipts.

---

## 8. Implementation notes (docs-only this episode; code lands next)

This deliverable is **docs-only**. Implementation is a follow-up episode. For that follow-up, the concrete next steps are:

1. **`AZUREOPENAIMODEL` env var:** set to `gpt-5-nano,gpt-5.4-nano` in operator deployment. Comma-separated multi-model is already supported per the env-var docs.
2. **Routing logic location:** [`azureopenai-cli/Program.cs`](../../azureopenai-cli/Program.cs), in the model-resolution path that today returns `Program.DefaultModelFallback` (per ADR-009). Add a `ResolveModelForCall(CliOptions, callContext)` helper that takes mode, registered tool count, persona context, and estimated input tokens, and returns the model name. Keep `UserConfig.ResolveModel` as the alias-resolution layer; routing is downstream of that.
3. **`CostHook.cs` `PriceTable` rows required:**
   - `gpt-5-nano` -- new row, rates pending §10 OQ-1.
   - `gpt-5.4-nano` -- already present (line ~22), no change.
   - Verify both rows match `pricing-sourcing.md` §1 in the same PR.
4. **`pricing-sourcing.md` rows required:**
   - **Add:** `gpt-5-nano` row with verified pricing, source URL, verification command, and `Verified: 2026-04-29` (or whatever date the verification actually happens).
   - **Refresh:** `gpt-5.4-nano` row -- currently marked `*pending re-check*` with prior stamp `2025-08-07`. Stale by three quarters per the audit. This refresh is overdue regardless of Option B.
5. **Telemetry tag:** add `azai.cost.routed_model` and `azai.cost.route_reason` attributes to the existing OTel span emitted by `CostHook`. `route_reason` enumerates: `default`, `mode_agent`, `mode_persona`, `mode_ralph`, `tools_present`, `long_context`, `structured_output`, `cli_override`. This is what makes Option B A/B-able and gives the monthly FinOps review the data it needs.
6. **ADR amendment:** ADR-009 already covers env-var-first resolution; the routing rule is *downstream* of that resolution and does not require a new ADR. A short amendment to `cost-optimization.md §3.7` (default-model change log) is sufficient. If the boss disagrees and wants a fresh ADR, scope it to "intra-call routing among pre-resolved candidate models" -- not a re-litigation of ADR-009.

**Reminder:** none of the above changes ship in this episode. This document is the spec; the next episode is the implementation.

---

## 9. Open questions / pending verifications

| ID | Question | How we close it | Owner |
|---|---|---|---|
| OQ-1 | What is the verified `gpt-5-nano` Input / Cached / Output rate at Global Standard? | Open Foundry catalog -> `gpt-5-nano` -> Pricing tab; cross-check the public Azure OpenAI pricing page; record in `pricing-sourcing.md` §1 with verification command and 2026-04-29 (or actual) date. | Morty + Kramer (next episode) |
| OQ-2 | Is `gpt-5-nano` available in our subscription's deployed region (today)? | `az cognitiveservices account deployment list` against our resource; if not present, propose a Global Standard deployment in the existing region or document the cross-region fallback. | Jerry + Morty |
| OQ-3 | Does `gpt-5-nano` honor the same prompt-cache discount mechanic as `gpt-5.4-nano`? | Foundry catalog Pricing tab will show a "Cached Input" column if so; verify in the same step as OQ-1. | Morty |
| OQ-4 | What is the standard-mode quality delta between `gpt-5-nano` and `gpt-5.4-nano` on our actual prompts? | Maestro's eval harness, run against the standard-mode test set. Acceptance criterion: < 5% regression on the eval suite. Triggers fall-back to Option A if exceeded. | Maestro |
| OQ-5 | Is the 4000-token cutoff in the Option B routing rule the right threshold? | Bake in for 30 days, then revisit with Bania's perf telemetry and the OTel `azai.cost.route_reason` distribution. Adjust as needed. Not a blocker for adoption. | Morty + Bania |
| OQ-6 | Should we deploy a `gpt-5.4-nano` Batch endpoint for any of our traffic? | Out of scope for Option B today; revisit when monthly volume crosses 25 M input tokens. Batch is a 50% modifier and only worth the operational complexity at scale. | Morty |
| OQ-7 | `gpt-5.4-nano` `Verified` date in `pricing-sourcing.md` is `*pending re-check*` with a 2025-08-07 stamp -- three quarters stale. | Quarterly drift check per `pricing-sourcing.md` §2. **Overdue regardless of this report.** | Morty (this quarter) |

---

*"Two nanos, one routing rule, no premium tier until the telemetry says otherwise. THAT is how you protect the wallet without being a schmuck about it."*
-- Morty
