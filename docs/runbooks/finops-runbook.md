# FinOps Runbook — Monthly Review

> *"It's the ones you don't know about that buy the boat."* — Morty
>
> This is how you know about them.

**Audience:** whoever is on FinOps rotation this month (default: Morty).
Budget: ~45 minutes of focused work on the 1st business day of each
month. If it's taking two hours, the tooling is broken — file an issue,
don't power through.

**Outcome:** a short review note posted to the repo (issue or wiki)
with three numbers (last-month spend, MoM delta, top three meters), a
one-line verdict (`green` / `yellow` / `red`), and any ADR drafts
triggered by the findings.

**Related docs:**
- [`docs/cost-optimization.md`](../cost-optimization.md) — the playbook
- [`docs/cost/pricing-sourcing.md`](../cost/pricing-sourcing.md) —
  where the per-model rates come from
- [`docs/ops/v2-sre-runbook.md`](../ops/v2-sre-runbook.md) — the
  `+15% over 48h` cost-regression alert lives here

---

## 0. Inputs

| Item | Example | Where |
|---|---|---|
| Month under review | `2026-03` | previous calendar month |
| Subscription ID | `00000000-0000-…` | `az account show` |
| Cognitive Services resource | `az-ai-prod-eastus` | Azure Portal |
| Resource group | `rg-az-ai-prod` | Azure Portal |
| Baseline spend (prior 3-mo avg) | `$14.20/mo` | last review note |
| Current default model | `gpt-5.4-nano` | `cost-optimization.md §3.7` |

Set these as shell variables before running commands below:

```bash
export MONTH='2026-03'
export AZ_SUB='00000000-0000-0000-0000-000000000000'
export AZ_RESOURCE='az-ai-prod-eastus'
export AZ_RG='rg-az-ai-prod'
```

---

## 1. Pre-flight

```bash
az account set --subscription "$AZ_SUB"
az account show --query '{name:name, id:id}' -o table
az cognitiveservices account show \
  --name "$AZ_RESOURCE" --resource-group "$AZ_RG" \
  --query '{name:name, kind:kind, sku:sku.name, location:location}' -o table
```

Stop if the account or resource isn't what you expected.

---

## 2. Pull usage from Azure Cost Analysis

Two sources of truth. You want **both** — they should agree to within
rounding. If they don't, that's a finding.

### 2a. Azure Cost Management (the bill)

Portal path: *Cost Management + Billing → Cost analysis → Scope = your
subscription → Group by: Meter subcategory → Filter: Service name =
Cognitive Services → Time range = last month*.

Export the CSV. Keep it under `docs/cost/reviews/YYYY-MM-<reviewer>.csv`
(gitignored if sensitive) or attach to the review issue.

CLI equivalent (JSON):

```bash
# ISO month boundaries
FROM="${MONTH}-01"
TO=$(date -d "${FROM} +1 month -1 day" +%F)

az costmanagement query \
  --type ActualCost \
  --timeframe Custom \
  --time-period from="${FROM}" to="${TO}" \
  --scope "/subscriptions/${AZ_SUB}" \
  --dataset '{
    "granularity": "None",
    "grouping": [
      {"type":"Dimension","name":"MeterSubcategory"},
      {"type":"Dimension","name":"MeterName"}
    ],
    "filter": {
      "dimensions": {
        "name":"ServiceName",
        "operator":"In",
        "values":["Cognitive Services"]
      }
    }
  }' \
  -o json \
  | jq -r '.properties.rows[] | @tsv' \
  | sort -k1 -n -r
```

Record the total, the top-3 meters, and the number of distinct
meters. "Too many distinct meters" is itself a yellow flag — see §5.

### 2b. Stderr token-count summation (ground truth for the CLI)

Every `az-ai` invocation emits a `[tokens: X→Y, Z total]` line on
stderr (see [`cost-optimization.md §2`](../cost-optimization.md)).
If your team pipes stderr to a log sink — and they should — sum it:

```bash
# Adjust path to wherever stderr lands. Example: per-user log files.
zcat ~/.local/share/az-ai/logs/${MONTH}-*.log.gz 2>/dev/null \
  | grep -oE '\[tokens: [0-9]+→[0-9]+, [0-9]+ total\]' \
  | awk -F'[:→, ]+' '{in_t+=$3; out_t+=$4; tot+=$5; n+=1}
       END {printf "calls=%d input=%d output=%d total=%d\n", n, in_t, out_t, tot}'
```

If no log sink is configured, that's a finding — open an issue tagged
`finops/observability-gap`. The monthly review cannot be done well
without one.

### 2c. Reconcile

Multiply the per-model token totals from 2b by the per-model rates in
[`docs/cost/pricing-sourcing.md`](../cost/pricing-sourcing.md).
Compare to 2a's total.

| Reconciliation case | Interpretation | Action |
|---|---|---|
| Within ±5% | Normal — rounding, rate rotation mid-month, Batch API skew | Note and proceed |
| 2b < 2a by > 10% | Spend the CLI can't see: portal experiments, other tools on the same resource, or a forgotten deployment | Investigate the extra meters; add to next review |
| 2b > 2a by > 10% | Stale rates in `pricing-sourcing.md` — we *think* we're paying more than we are | Run a pricing drift check (see `pricing-sourcing.md §2`) |
| Within 5% **but** total up > 15% MoM | Real usage growth or an anomaly — see §3 | Investigate |

---

## 3. Flag anomalies

Walk the top-3 meters from §2a. For each:

1. **MoM delta.** Compare last-month to 3-month trailing average.
   - `< ±10%`: green, no action
   - `±10%–±25%`: yellow — note in the review; compare to headcount
     or traffic growth to see if it's proportional
   - `> ±25%`: red — investigate before closing the review. Common
     causes and triage below.
2. **Per-call spend.** Divide meter total by the call count from 2b.
   If per-call cost jumped and call count didn't, the *prompt* got
   heavier — see `cost-optimization.md §4`. If both jumped, it's
   traffic.
3. **Ralph / agent fan-out.** Ralph mode iterates up to
   `--max-iterations` (default 10, hard cap 50). A Ralph outage
   stuck at the cap for a week is a real line item. Cross-check
   against the ralph-iteration telemetry if you have it.

### Common anomalies and where they come from

| Symptom | Likely cause | First place to look |
|---|---|---|
| Output-token spike on `gpt-5.4-nano` (or whichever default) | `--max-tokens` removed from an Espanso trigger, or a doc-audit prompt that never terminates | `cost-optimization.md §4 "Cap output aggressively"`; `espanso-ahk-integration.md` |
| Input-token spike, call count flat | Persona memory bloat (32 KB cap rarely hit; more often a 20 KB tail that grew 5 KB MoM) or longer stable system prompt | `cost-optimization.md §4 "Shave the system prompt"`; `§4 "Respect the 32 KB persona memory cap"` |
| Call count spike | New workflow shipped, new user onboarded, Espanso macro firing on a hot path, or a Ralph loop runaway | Check CHANGELOG for new features touching `--espanso` or agent mode |
| Premium-model meter (`gpt-4.1` / `gpt-4o`) appears where it shouldn't | `AZURE_DEPLOYMENT` env leaked from a dev's `.env` into a shared workflow, or an explicit `--model` override in a script | Grep CI configs and shared scripts for `gpt-4.1` / `gpt-4o` |
| Cached Input ratio drops | Unstable prefix — someone moved a changing field to the front of the system prompt and broke prefix match | `cost-optimization.md §5.1 "what qualifies"` |
| Batch API meter absent where it should be present (Ralph-validator, nightly doc-audit) | `--batch` not wired to async workloads | `cost-optimization.md §5.2` |
| Ralph cost multiplier out of expected range | See `cost-optimization.md §6.7` and compare `N × single + prompt_growth × N²` |

---

## 4. When to cut a model-switch ADR

A **model-switch ADR** is written (not just an issue, not just a PR
comment) when any of these is true:

1. The operational default model is changing.
2. A new model is being adopted as the recommended tier for a
   workflow class (e.g. switching Espanso from mini to nano and back,
   switching Ralph validator from reasoning to instruct).
3. A model is being retired (deprecation path, migration window).
4. A pricing modifier is being adopted org-wide (e.g. "we're turning
   Batch API on for every async workload").

ADR contents (copy the skeleton from the most recent ADR under
`docs/adr/`):

- **Context.** What changed in the market, our workload, or our
  measurements that prompts the switch.
- **Decision.** Exactly which model, for which scope (default, per
  persona, per flag, per workflow).
- **Measured before/after.** Token totals and $/call on a
  representative workload. No hand-waving. If you don't have numbers,
  you're not ready to write the ADR.
- **Rollback plan.** One command or one config change.
- **Cost delta.** Per-seat per-month at the baseline workload in
  `cost-optimization.md §6.5`. This row gets copied into
  `cost-optimization.md §3.7` default-model change log.
- **Sign-off.** Morty (FinOps), Mr. Lippman (release), Kramer
  (engineering owner of the affected flow).

**Hard rule:** no default flip without an ADR. The
documented-vs-operational drift that triggered this whole audit
(C-1 in `docs/audits/docs-audit-2026-04-22-morty.md`) is exactly
what an ADR prevents. Don't ship another one.

---

## 5. Publish the review

Post to the repo. One issue per month, label `finops/monthly-review`.
Template:

```markdown
## FinOps review — YYYY-MM

**Verdict:** green | yellow | red
**Total spend:** $X.XX  (MoM: +/-Y%, vs 3-mo avg: +/-Z%)
**Calls (from stderr):** N   **Tokens in / out:** A / B
**Reconciliation (cost-mgmt vs stderr*rates):** within K%

**Top 3 meters**
1. <meter> — $X.XX (+/-%)
2. ...
3. ...

**Findings**
- [ ] <finding 1, linked to issue or ADR draft>
- [ ] ...

**Drift check performed?** yes/no (next quarterly: YYYY-MM-DD)
**ADRs drafted this cycle:** <links or "none">
**Carry-overs from last month:** <links or "none">

_Signed, <reviewer>._
```

Close last month's review issue once this month's is posted.

---

## 6. End-of-year wrap

In December's review, additionally:

- Total annual spend and per-seat equivalent.
- List every default-model change that shipped (pull from
  `cost-optimization.md §3.7`).
- Total $ saved by corrective actions (Batch API adoption, cache
  wins, default reversions). This is the "look what we didn't pay"
  number; it justifies the rotation's existence.
- Identify the single largest anomaly of the year and whether its
  root cause was addressed.

---

## 7. Escalations

| Condition | Who to notify | How |
|---|---|---|
| Spend > 2× baseline in a single month | Mr. Lippman + Kramer | Slack + issue `finops/spike-major` |
| Pricing drift > 15% on the default model meter | Morty (rotation) + release manager | Issue `finops/drift-major`; see `pricing-sourcing.md §2` |
| Runaway Ralph loop suspected (meter still climbing hour-over-hour) | Frank Costanza (SRE) | Page; see `docs/ops/v2-sre-runbook.md` |
| Observability gap (stderr not being captured) | Engineering | Issue `finops/observability-gap`, block next release |

---

*"Get the documentation truthful, get the caching turned on, get the
Batch API attached to the async jobs. Then when somebody asks
'where'd the money go?' you'll have an answer that isn't 'I think
Kramer did something.'"* — Morty
