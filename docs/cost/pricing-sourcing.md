# Pricing Sourcing & Provenance

> *"You quoted a price without a source? In MY documentation? Get out."*
> -- Morty

Every per-token dollar figure that appears anywhere in this repo's
documentation **must** correspond to a row in the table below. The row
must name the model, the rate, the source URL, the date the rate was
last verified, and a reproducible verification command (or portal
breadcrumb) a reader can run themselves.

**Rule:** if a price is quoted in any `docs/**` file, `README.md`,
CHANGELOG entry, ADR, FR proposal, or benchmark report, it has a row
here. No exceptions. If you add a price, you add a row. If the row
goes stale, you either refresh it or you remove the quote.

**Authoritative hardcoded table:**
[`azureopenai-cli-v2/Observability/CostHook.cs`](../../azureopenai-cli-v2/Observability/CostHook.cs)
(the `PriceTable` dictionary, roughly lines 20-30). Override at
runtime via `AZAI_PRICE_TABLE=/path/to/prices.json` (schema in
[`docs/observability.md`](../observability.md)). This doc tracks the
provenance of each row in that table.

---

## 1. Provenance table

Dates are ISO-8601. "Stale" = verified > 90 days ago (one quarter,
Morty's rule). Amounts are USD per 1 M tokens unless noted.

| Model | Input $/M | Cached $/M | Output $/M | Source | Verified | Method / verification breadcrumb |
|---|---:|---:|---:|---|---|---|
| `gpt-4o-mini` | 0.15 | 0.075 | 0.60 | [Azure OpenAI pricing][az] | *pending re-check* | Azure Portal → *Cognitive Services → Azure OpenAI → Pricing blade* → select model `gpt-4o-mini`, region `East US`, tier `Global Standard`. Confirm `Input` / `Cached Input` / `Output` columns. |
| `gpt-5.4-nano` | 0.20 | 0.10 | 1.25 | [Azure OpenAI pricing][az] | *pending re-check (prior stamp 2025-08-07, stale)* | Same as above; model `gpt-5.4-nano`. **Historical note:** the `5.4` SKU was not visible on the public pricing page as of 2026-04-22; confirm against the Foundry endpoint your deployment actually calls (`az cognitiveservices account deployment list`). |
| `gpt-4o` | 2.50 | 1.25 | 10.00 | [Azure OpenAI pricing][az] | *pending re-check* | Azure Portal pricing blade → `gpt-4o` → Global Standard. |
| `gpt-4.1` | 3.00 | 1.50 | 12.00 | [Azure OpenAI pricing][az] | *pending re-check* | Azure Portal pricing blade → `gpt-4.1` → Global Standard. `CostHook.cs:26` comment marks this row "estimated"; replace with a verified figure on next refresh. |
| `o1-mini` | 3.00 | 1.50 | 12.00 | [Azure OpenAI pricing][az] | *pending re-check* | Azure Portal pricing blade → `o1-mini`. Note: reasoning tokens bill as **output**. |
| `DeepSeek-V3.2` | 0.58 | n/a | 1.68 | [cloudprice.net][cp] + [MS Community Hub announcement][msch] | 2026-04-22 | Third-party aggregator + first-party announcement. Replace with Foundry Console screenshot on next refresh. `curl -s https://api.cloudprice.net/... \| jq '...'` when their public API stabilises. |
| `Phi-4-mini-instruct` | 0.075 | n/a | 0.30 | [MS Community Hub -- Phi-4 on Foundry][msch-phi] | 2026-04-22 | First-party announcement post. Replace with Foundry Console screenshot (portal → Foundry → Model Catalog → `Phi-4-mini-instruct` → Pricing tab). |
| `Phi-4-mini-reasoning` | 0.08 | n/a | 0.32 | [MS Community Hub -- Phi-4 reasoning][msch-phi] | 2026-04-22 | Same as above. |
| `Phi-3.5-mini-instruct` | 0.13 | n/a | 0.52 | *no first-party source on file* | **never** | 🔴 Remove quote from docs or attach a source on next refresh. Currently referenced in `cost-optimization.md §3.6` "strictly dominated" argument; argument stands only if the number is real. |
| **GPT-5 (flagship, peer-comparison)** | 1.25 | 0.125 | 10.00 | [Azure OpenAI pricing][az] | *pending re-check (prior stamp 2026-04 footnote in competitive-analysis.md)* | Used in `competitive-analysis.md §4` for like-for-like comparison against Claude/Gemini flagship tiers. **Not** the az-ai default; see `cost-optimization.md §3` for the default's actual pricing basis. |
| **Claude 3.5 Sonnet** | 3.00 | n/a | 15.00 | [Anthropic API pricing][anth] | 2026-04-22 | `curl -s https://docs.anthropic.com/en/docs/about-claude/pricing \| grep -A2 'claude-3-5-sonnet'` (page structure may shift; prefer portal screenshot). Competitor; peer-comparison only. |
| **Claude Opus 4.6** | 5.00 | n/a | 25.00 | [Anthropic API pricing][anth] | 2026-04-22 | Same method. Competitor; peer-comparison only. |
| **Gemini 2.5 Pro** | 1.25 | n/a | 10.00 | [Google AI pricing][gai] | 2026-04-22 | `curl -s https://ai.google.dev/pricing \| grep -A2 'Gemini 2.5 Pro'`. Competitor; peer-comparison only. |
| **Azure Batch API discount** | −50% | -- | −50% | [Azure OpenAI pricing][az] overview section | 2026-04-22 | "Batch API returns completions within 24 hours for a 50% discount on Global Standard Pricing." Applies to all models on the Batch endpoint; no per-model row needed -- it's a pricing *modifier*. |

### Verification commands (copy-paste)

```bash
# Fetch the public Azure OpenAI pricing page (HTML; numeric cells are
# client-side rendered so grep won't always yield numbers, but the
# page structure -- model rows, Cached Input column, Batch footer --
# is visible).
curl -sSL 'https://azure.microsoft.com/en-us/pricing/details/cognitive-services/openai-service/' \
  | grep -Eo '(gpt-[0-9]+[^<]*|Cached Input|Batch API)' | sort -u | head -40

# Authenticated Foundry deployment inventory -- confirms *what your
# deployment actually runs*, which is the number that matters for
# your bill. Requires `az login` and the Cognitive Services resource
# name/RG.
az cognitiveservices account deployment list \
  --name "$AZURE_OPENAI_RESOURCE" \
  --resource-group "$AZURE_RG" \
  --query '[].{name:name, model:properties.model.name, version:properties.model.version, sku:sku.name}' \
  -o table

# Walk the hardcoded table in code (source of truth at runtime):
grep -n -A 20 'PriceTable' azureopenai-cli-v2/Observability/CostHook.cs
```

### Portal-only checks (no curl breadcrumb possible)

Some rates render client-side and only exist behind an authenticated
session. For those, "verification" means a screenshot or a breadcrumb:

1. Azure Portal → *Cost Management + Billing* → *Cost analysis* →
   filter `Service name = Cognitive Services`, `Meter subcategory`
   contains the model name. The per-meter rate on a recent invoice
   line is ground truth for **what you actually paid**.
2. Azure AI Foundry (https://ai.azure.com) → *Model catalog* →
   select the model → *Pricing* tab. This is the rate the endpoint
   charges today.

When these disagree with the hardcoded table, **update the table and
the sourcing row** in the same PR. Morty signs off.

[az]: https://azure.microsoft.com/en-us/pricing/details/cognitive-services/openai-service/
[cp]: https://cloudprice.net/
[msch]: https://techcommunity.microsoft.com/category/azureaiservices
[msch-phi]: https://techcommunity.microsoft.com/category/azureaiservices
[anth]: https://docs.anthropic.com/en/docs/about-claude/pricing
[gai]: https://ai.google.dev/pricing

---

## 2. Provider drift watchdog

Providers move their prices. Quietly. Around holidays. On pages that
render JavaScript so your `curl` cron doesn't see the change. This is
the policy that keeps us from finding out six months late.

### Ownership

- **Primary:** the active FinOps reviewer (currently Morty).
- **Backup:** the release manager (Mr. Lippman) checks drift as part of
  the release pre-flight when a release touches model defaults or the
  price table -- see
  [`docs/runbooks/release-runbook.md`](../runbooks/release-runbook.md).
- **Escalation:** any engineer who notices a bill anomaly in the monthly
  FinOps review (see [`finops-runbook.md`](../runbooks/finops-runbook.md))
  files an issue tagged `finops/drift` and pings the primary.

### Cadence

**Quarterly, on the first business day of the quarter.** If the
quarter boundary lands in a release window, drift-check first, cut
the release second. Skipping a quarter is a finding, not a footnote.

- **Q1 check:** first business day on/after **January 2**
- **Q2 check:** first business day on/after **April 1**
- **Q3 check:** first business day on/after **July 1**
- **Q4 check:** first business day on/after **October 1**

Additionally, perform a **spot-check** any time:
- a new model is added to `CostHook.cs` `PriceTable`
- the operational default model changes
  (see `cost-optimization.md §3.7` default-model change log)
- the monthly FinOps review flags a > 15% spend delta on any meter
- a provider announces a pricing change via blog / release notes

### Calendar

Track the cadence in whatever calendar the maintainers actually use.
Options that survive turnover (ordered by preference):

1. **GitHub repository issue** with label `finops/drift-watch` and
   milestone per quarter (e.g. `finops-2026-Q3`). Close the issue
   when the quarterly check is complete; open next quarter's on
   close. This leaves an audit trail in-repo.
2. **Calendar reminder** on the shared maintainer calendar titled
   `FinOps: quarterly pricing drift check` with this file linked in
   the description.
3. **CHANGELOG trailer.** Each release that touches the price table
   or defaults must include a `Pricing-verified: YYYY-MM-DD` trailer
   so the provenance is legible in git history.

**Preferred:** option 1. A closed issue per quarter is the cheapest
possible audit trail and does not depend on any individual's inbox.

### Procedure (the quarterly check itself)

1. Walk §1's provenance table top to bottom. For each row:
   - Run the verification command (or open the portal breadcrumb).
   - Compare the rendered number to the row's quoted value.
   - If the numbers match, update `Verified` to today's date in the
     same PR. No other change.
   - If they differ, see "drift response" below.
2. Re-run the `az cognitiveservices account deployment list` command
   to confirm no phantom deployments (a deployment that exists in
   Foundry but has no row in §1 is a finding).
3. Check the [Azure pricing page][az] *overview* section for new
   pricing modifiers (Batch API, Provisioned Throughput Units,
   Cached Input changes). New modifiers are new rows or new
   §1 mentions -- file the PR.
4. Sanity-check the hardcoded `PriceTable` in
   `azureopenai-cli-v2/Observability/CostHook.cs` against §1. If they
   disagree, the code is canonical *for what the CLI reports* but §1
   is canonical *for what docs may quote*. Reconcile in the same PR.
5. Mark the tracking issue (#1 above) closed. Open next quarter's.

### Drift response

When a row disagrees with the live source:

| Drift magnitude (per-token rate change) | Response |
|---|---|
| < 5% | Update the number + `Verified` date. Note in PR body. No ADR needed. |
| 5% - 15% | Update as above **and** note in the next monthly FinOps review. If the drifted model is the operational default, recompute the baseline-cost examples in `cost-optimization.md §6.5`. |
| > 15% | Stop. File an issue tagged `finops/drift-major`. The operational default or any budget-gating meter at > 15% drift gets a CHANGELOG entry and, if the default changes as a result, an ADR (see `cost-optimization.md §3.7`). Do **not** silently update `PriceTable`. |
| Row disappears from the live source (model deprecated) | Mark the row `DEPRECATED YYYY-MM-DD` in §1, add a migration note to `cost-optimization.md §3.7`, and file an issue for any docs still quoting the dead rate. |
| New row appears (new model, new modifier) | Add it to §1 with a source, a verified date, and a verification command. If it's a default candidate, the decision goes through `cost-optimization.md §3.7`. |

### What a good drift check looks like on paper

A merged PR, around the first of a quarter, whose diff is mostly:

```diff
- | `gpt-4o-mini` | 0.15 | 0.075 | 0.60 | ... | *pending re-check* | ...
+ | `gpt-4o-mini` | 0.15 | 0.075 | 0.60 | ... | 2026-07-01 | ...
```

Boring. Quarterly. Signed off by Morty. That's the goal.

---

## 3. Adding a new price to the docs -- checklist

Use this as a PR self-review before asking for FinOps sign-off.

- [ ] The price is in `§1` of this file.
- [ ] The row has a source URL *or* a portal breadcrumb (no blanks).
- [ ] The row has a `Verified` date within the last 90 days.
- [ ] The row has a verification command or a named portal tab.
- [ ] If the price is referenced from multiple docs, all references
      point at the same row (no copy-paste-and-drift).
- [ ] If the price is for a new operational default, the change has
      an entry in `cost-optimization.md §3.7` and an ADR link.
- [ ] The hardcoded `PriceTable` in `CostHook.cs` is updated in the
      same PR if this is a price the CLI reports.
- [ ] `Pricing-verified: YYYY-MM-DD` trailer added to the commit
      message if the PR ships in a release.

---

*"No price without a source. No source without a date. No date older
than a quarter. Paid HOW much for unsourced pricing docs? Not on my
watch."* -- Morty
