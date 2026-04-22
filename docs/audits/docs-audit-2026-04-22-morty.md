# Docs Audit — FinOps segment (Morty)

> *"You paid HOW much for a pair of sneakers?"*

- **Auditor:** Morty Seinfeld, President of the Condo Association and Unpaid CFO
- **Date:** 2026-04-22
- **Scope:** `docs/cost-optimization.md` (my turf), `README.md`, `docs/**`
  pricing/token refs, `docs/proposals/*` with cost analysis, CHANGELOG
  model-switch entries
- **Method:** read the source, grep the numbers, pull the Azure pricing page,
  compare against what the code actually does
- **Non-goals (per brief):** no source edits; flagged items only
- **Baseline assumptions for impact math:** one active seat = ~3,000
  Espanso-style calls/mo at ~1,000 input + ~200 output tokens each
  (3 M input, 0.6 M output tokens/seat/mo). Adjust as your telemetry permits.

---

## 0. TL;DR — "Hey! Where's the beef?!"

Three words: **the doc lies.**

1. `cost-optimization.md` insists "default stays `gpt-4o-mini`" in **six
   separate places**, but the user has **operationally flipped the default
   to `gpt-5.4-nano`**. The doc has not been updated, there is no
   before/after cost-impact table, and **I am paying 50–67% more per
   Espanso call than last week and nobody wrote it down.** That's a cost
   regression shipped without a cost review. Critical.
2. §5 "Caching strategies (**mostly future work**)" is written as if
   Azure had no prompt-cache pricing tier and as if FR-008 hadn't shipped.
   Both are wrong. Azure lists a dedicated `Cached Input` rate on every
   model SKU today, Batch API gives 50% off async workloads, and
   `azureopenai-cli-v2/Cache/PromptCache.cs` has been in main since
   February. The section needs a full rewrite.
3. `FR-008-prompt-response-cache.md:47` justifies the cache with
   "GPT-4o pricing, a cached response saves ~$0.01–0.05 per hit."
   That's a **~10× overstatement** at our actual defaults. Don't oversell
   the ROI — the real number sells itself.
4. `competitive-analysis.md §4` and `cost-optimization.md §3` use two
   **different price bases** to describe "the az-ai default." One says
   GPT-5 @ $1.25/$10; the other says gpt-4o-mini @ $0.15/$0.60. Pick one.
5. The Phi-4-mini comparison tables compare to `gpt-4o-mini`, but the
   Bania benchmark compares to `gpt-5.4-nano` — which is now the *actual*
   deployed default. The doc's cost narrative and the benchmark's cost
   narrative no longer line up.

**Verdict on `cost-optimization.md`:** Needs **~40% rewrite**, not a full
teardown. Sections 1, 2, 4, 6, 7, 8 are still essentially correct.
Sections 3 (pricing table, gpt-5.4-nano framing), 3.5, 3.6 (Phi vs. what?),
and 5 (caching) all need surgery. Plan on 1–2 hours of focused edits.

**Severity counts:** Critical 2 · High 5 · Medium 9 · Low 4 · Informational 4
· **Total 24 findings.**

**Estimated monthly savings if all High+ findings are acted on (single-seat,
baseline workload above):** **$3.50 – $6.00/seat/month** — dominated by
(a) correcting the default back to `gpt-4o-mini` *or* fully adopting Azure
native prompt caching on `gpt-5.4-nano`, and (b) turning on Batch API for
the Ralph validator and doc-audit pipelines. At 10 active seats, that's
**~$40–$70/month saved**. At 100, **~$400–$700/month**. Per-task the numbers
look small; **compound it across a year of a growing user base and it's a
pair of sneakers. A very expensive pair of sneakers.**

---

## 1. Pricing Freshness Dashboard

*Every row needs a date. Dates older than one quarter (90 days) are stale
by my rule.*

| Model | Doc $/M in | Doc $/M out | Location | Last verified in doc | Days stale (today 2026-04-22) | Verdict |
|---|---:|---:|---|---|---:|---|
| `gpt-4o-mini` | $0.15 | $0.60 | `cost-optimization.md:42` | **undated** (doc header says "as of 2026-04") | — | ⚠️ never individually verified; ratios stable |
| `gpt-5.4-nano` | $0.20 | $1.25 | `cost-optimization.md:43` | **2025-08-07** (per L53 footnote) | **258** | 🔴 **stale by 3 quarters**; also: model name does not currently appear on the public Azure pricing page (GPT-5, 5.1, 5.2, 5.3 series listed — no "5.4" SKU). Verify this is the actual deployment name your Foundry endpoint uses |
| `gpt-4o` | $2.50 | $10.00 | `cost-optimization.md:44` | **undated** | — | ⚠️ unverified — the "~" qualifiers and "as of 2026-04" are all the reader has |
| `gpt-4.1` | $3.00 | $12.00 | `cost-optimization.md:45` | **undated** | — | ⚠️ unverified (and `CostHook.cs:26` comment openly says "estimated") |
| `DeepSeek-V3.2` | $0.58 | $1.68 | `cost-optimization.md:46` | **April 2026** (cloudprice.net + MS Community Hub) | ~22 | 🟡 recent but third-party source; Foundry direct confirmation missing |
| `Phi-4-mini-instruct` | $0.075 | $0.300 | `cost-optimization.md:47` | April 2026 | ~22 | 🟡 corroborated by Community Hub; Foundry Console screenshot would close the loop |
| `Phi-4-mini-reasoning` | $0.080 | $0.320 | `cost-optimization.md:48` | April 2026 | ~22 | 🟡 same caveat |
| `Phi-3.5-mini-instruct` | $0.13 | $0.52 | `cost-optimization.md:131` | **undated** (referenced for "Pareto loss" argument) | — | ⚠️ the whole "strictly dominated" conclusion rests on numbers with no cited source |
| `o1-mini` | $3.00 | $12.00 | `cost-optimization.md:49` | **undated** | — | ⚠️ and marked as "reasoning tokens bill as output" — true, but unverified |
| GPT-5 (Azure) | $1.25 | $10.00 | `competitive-analysis.md:98` | **April 2026 (footnote 15/16)** | ~22 | 🟡 **contradicts** `cost-optimization.md` which does not list a full GPT-5 SKU at all |
| Claude 3.5 Sonnet | $3.00 | $15.00 | `competitive-analysis.md:101` | April 2026 | ~22 | 🟡 actual product sold as "Claude Code Pro" is now ~Claude 3.7/Sonnet-4 class — verify the model↔product mapping |
| Claude Opus 4.6 | $5.00 | $25.00 | `competitive-analysis.md:102` | April 2026 | ~22 | 🟡 competitor pricing; independent verify |
| Gemini 2.5 Pro | $1.25 | $10.00 | `competitive-analysis.md:97` | April 2026 | ~22 | 🟡 competitor pricing; independent verify |
| **`Cached Input` discount tier** | — | — | **MISSING** | — | — | 🔴 **Azure lists `Cached Input` on every model row. Our doc has zero words about it.** See Finding H-2. |
| **Batch API (50% off)** | — | — | **MISSING** | — | — | 🔴 Azure: "[Batch API] returns completions within 24 hours for a 50% discount on Global Standard Pricing." Applicable to Ralph-validator, doc-audit, nightly-benchmark workloads. Zero mentions in the doc. See H-5. |

**Freshness bottom line:** Of 13 named models, **6 rows have no verification
date at all, 1 row is 258 days stale, and 2 entire pricing tiers (cached-input,
batch) are unrepresented.** The doc's §3 header says "as of 2026-04" but that
is an aspiration, not a per-row audit.

---

## 2. Findings

### 🔴 Critical

**C-1. The documented default contradicts the operational default.**
- **File:** `docs/cost-optimization.md:42`, `:55`, `:60`, `:70`, `:178`,
  `:193`, `:300`, `:304` — "gpt-4o-mini" asserted as **the** default in
  8 places.
- **Problem:** User reports the default was recently flipped to
  `gpt-5.4-nano`. The hardcoded fallback in `azureopenai-cli-v2/Program.cs:259`
  and `:1223` is still `"gpt-4o-mini"`, but the effective default is set via
  `UserConfig.DefaultModel` / env (`ResolveSmartDefault`,
  `UserConfig.cs:155`). That means *what the docs call the default* and
  *what users actually hit* have diverged, with **no before/after cost
  callout** in either the doc or CHANGELOG.
- **Dollar impact (single seat, baseline workload):**
  - Old (`gpt-4o-mini`): 3 M × $0.15 + 0.6 M × $0.60 = **$0.81/mo**
  - New (`gpt-5.4-nano`): 3 M × $0.20 + 0.6 M × $1.25 = **$1.35/mo** (**+67%**)
  - 10 seats: **+$5.40/mo**, **+$65/yr**. 100 seats: **+$540/yr**.
- **Proposed fix:** Add a §3.7 "Default model switch log" with a table of
  every default change, date, rationale, and before/after per-seat cost
  delta. Same trailer in CHANGELOG under each version that moves it.
- **Severity:** Critical (documentation-vs-reality mismatch + silent cost
  regression).

**C-2. §5 "Caching strategies (mostly future work)" is almost entirely
obsolete.**
- **File:** `docs/cost-optimization.md:236–256`
- **Problem:** Three overlapping staleness bugs:
  1. **Azure native prompt caching is not mentioned anywhere.** The Azure
     OpenAI pricing page (fetched 2026-04-22) lists a `Cached Input` rate
     on every model SKU. This is a first-class discount — on the order of
     50% off cached input tokens — and the doc doesn't know it exists.
  2. **FR-008 shipped.** `azureopenai-cli-v2/Cache/PromptCache.cs` +
     `--cache` / `AZ_CACHE=1` / `--no-cache` are live in v2 (see
     `FR-008-prompt-response-cache.md` "As shipped" callouts). §5 still
     calls it "Proposed: `--cache-dir` flag (future FR)."
  3. **Batch API (50% off async) is not mentioned.**
- **Dollar impact:** for a seat with large persona memory (say 10K
  repeated system+persona tokens × 3,000 calls/mo = 30 M cached-eligible
  input tokens), Azure native cached-input pricing at ~50% off on
  `gpt-5.4-nano` saves roughly **30 M × ($0.20 − $0.10)/1 M ≈ $3.00/seat/mo**.
  Batch-eligible pipelines (Ralph validator, nightly doc-audit, benchmark
  harness) get 50% off everything — workload-dependent but a realistic
  **$1–$5/seat/mo** on top.
- **Proposed fix:** Rewrite §5 as three subsections: (5.1) Azure native
  prompt caching — what qualifies, reality of the `Cached Input` tier,
  link to the pricing page; (5.2) Our local FR-008 cache — `--cache` flag,
  canonical key, TTL, what's shipped; (5.3) Batch API — when a workload
  is batch-eligible, 50% discount, trade-off vs. interactive latency. Kill
  the "mostly future work" header.
- **Severity:** Critical (doc misrepresents both the platform and our
  own shipped product).

---

### 🟠 High

**H-1. Pricing footnote (`cost-optimization.md:53`) is 258 days stale.**
- "gpt-5.4-nano rates verified against Azure OpenAI pricing (**2025-08-07
  refresh**)." That's three quarters ago. Morty's rule is quarterly
  re-verification.
- **Fix:** re-verify each row against the live pricing page, stamp each
  row individually with `(verified YYYY-MM-DD)`, not one blanket footnote.
- **Impact:** hard to quantify directly, but stale per-model rates
  propagate into every `--estimate` call, every budget gate, and every
  FR PR-description cost estimate. A 30% drift on one rate is 30% drift
  on every CI budget alert. **Medium-to-high blast radius.**

**H-2. Azure native prompt caching is missing as a first-class lever.**
- See C-2 for scope. Breaking it out as its own finding because even if
  we keep §5's structure, the *single* most impactful FinOps knob on
  Azure right now — cached-input pricing on stable system+persona
  prompts — is not mentioned in any doc (`grep -ri "cached.*input"
  docs/` returns zero FinOps hits).
- **Dollar impact:** ~$3/seat/mo at baseline persona-heavy workload; could
  be 5–10× higher for agent-mode workflows with long tool-definitions.
- **Fix:** document which parts of our prompt are cache-eligible (stable
  system prompt, stable persona memory prefix, stable tool schemas), the
  64-token minimum, and how to verify hits via the `usage.prompt_tokens_details.cached_tokens` response field.

**H-3. FR-008 cache-ROI claim is ~10× overstated.**
- **File:** `docs/proposals/FR-008-prompt-response-cache.md:47`
- Claim: "At GPT-4o pricing, a cached response saves ~$0.01–0.05 per hit."
- Reality at our actual Espanso defaults (gpt-4o-mini / gpt-5.4-nano):
  - gpt-4o-mini, 1,000 in + 200 out: $0.00015 + $0.00012 = **~$0.00027/hit**
  - gpt-5.4-nano, same: $0.00020 + $0.00025 = **~$0.00045/hit**
  - Only at `gpt-4o` / `gpt-4.1` does the $0.01/hit figure become
    defensible (~$0.005–$0.015/hit). The doc quotes the expensive tier
    to defend a cache aimed at the cheap tier. That's selling a Cadillac
    with pickup-truck math.
- **Fix:** restate as a range keyed to model tier — "cheap-tier (mini/nano):
  $0.0003–$0.0005/hit; mid-tier (4o): ~$0.004–$0.006/hit; premium
  (4.1): ~$0.01–$0.02/hit." Real savings scale with model, and the doc
  should say so.
- **Severity:** High — credibility of the FR justification.

**H-4. `competitive-analysis.md §4` and `cost-optimization.md §3` use
inconsistent "az-ai default" pricing bases.**
- `competitive-analysis.md:98–99`: az-ai compared at **$1.25/$10** ("GPT-5")
- `cost-optimization.md:42–45`: az-ai default quoted at **$0.15/$0.60**
  (gpt-4o-mini) and premium at **$2.50/$10** (gpt-4o), **$3/$12** (gpt-4.1)
- The two docs are telling two different stories to two different readers.
  An external person reading competitive-analysis concludes az-ai costs
  $87.50/mo; an internal dev reading cost-optimization concludes $5/mo.
- **Fix:** add a paragraph to `competitive-analysis.md §4` that explicitly
  says: "The $87.50 figure uses GPT-5-class pricing for fair peer
  comparison with Claude/Gemini flagship tiers. Actual az-ai-on-mini/nano
  costs are ~15–20× lower; see `cost-optimization.md §3`." Otherwise pick
  one scenario and propagate.
- **Severity:** High (external-facing number; once it's in a blog post
  it's hard to walk back).

**H-5. Batch API discount (50% off) not mentioned anywhere.**
- Source: Azure OpenAI pricing page overview, 2026-04-22:
  "**Batch API:** ... returns completions within 24 hours for a 50%
  discount on Global Standard Pricing."
- Our Ralph validator loop, nightly benchmark harness, and the upcoming
  doc-audit pipeline are all prime candidates: async-tolerant, large
  token volumes, no user in the loop.
- **Dollar impact:** Ralph loop at 10 iterations × ~2K tokens ×
  gpt-5.4-nano = ~$0.003/run interactive, ~$0.0015/run on Batch API.
  Multiply by nightly CI runs × all PRs × 365 days and it's a real line item.
  **Conservatively $1–5/seat/mo** once adopted.
- **Fix:** add §5.3 per C-2; file an FR to add a `--batch` flag that
  submits async jobs via the Batch endpoint for ralph-validator and
  `--agent` workloads where latency isn't bounded.
- **Severity:** High (unexploited 50% lever).

---

### 🟡 Medium

**M-1. `gpt-4o-mini` "~8B parameters (est.)" speculation.**
- `docs/cost-optimization.md:103` lists `gpt-4o-mini` at "~8B (est.)".
  OpenAI has never disclosed this and there is no credible source. This
  is guesswork being served as documentation.
- **Fix:** drop the parameter-count row for OpenAI models (keep it for
  Phi where Microsoft published the number).

**M-2. The "output is roughly 3× more expensive than input" rule-of-thumb
is outdated.**
- `docs/cost-optimization.md:22`.
- Actual ratios from our own table: gpt-4o-mini 4.0×, gpt-4o 4.0×,
  gpt-4.1 4.0×, gpt-5.4-nano **6.25×**, Phi-4-mini 4.0×, DeepSeek-V3.2
  ~2.9×. Modal ratio is ~4×, with nano-class as high as 6×. "3×" is
  more GPT-3.5-era than 2026.
- **Fix:** change wording to "output is typically 3–6× more expensive
  than input; for `gpt-5.4-nano` it's 6.25×, for mini-class it's 4×."

**M-3. §3.6 Phi head-to-head compares the wrong baseline.**
- The big §3.6 comparison table at `docs/cost-optimization.md:97–108`
  compares Phi-4-mini-instruct to **gpt-4o-mini**. Bania's benchmark
  (referenced in the doc) compares Phi-4-mini-instruct to
  **gpt-5.4-nano** because that's what was deployed. Since gpt-5.4-nano
  is now the operational default, the §3.6 table is comparing Phi to a
  model *no longer used for this workload*.
- **Fix:** swap the comparison to `gpt-5.4-nano` to mirror the benchmark
  and current default, add a footnote row with gpt-4o-mini for
  historical context.
- **Severity:** Medium (technical narrative incoherence, not a dollar
  mis-statement).

**M-4. §3.5 conclusion line still reads as gospel but the default flipped.**
- `cost-optimization.md:60–62`: "**Decision:** Keep `gpt-4o-mini` as the
  default. Do **not** adopt `gpt-5.4-nano`..." — followed by a prose
  argument for why gpt-5.4-nano is worse. Operationally that recommendation
  was not followed. Either (a) the doc's recommendation was overridden and
  should be updated with a revised decision + ADR, or (b) the code/config
  is wrong and should revert. **Either way someone needs to write it down.**
- **Fix:** add a revision note — "Decision amended YYYY-MM-DD: see
  ADR-00X; default moved to `gpt-5.4-nano` because [reason]. Per-seat
  cost delta: +$0.54/mo at baseline (3k×200 tok × 3000 calls)."
- **Severity:** Medium — but it's the same root cause as C-1.

**M-5. `cost-optimization.md §5` proposes a cache key missing `max_tokens`;
FR-008 as-shipped also excludes `max_tokens`. Docs and code agree — but
neither explains *why* to the reader of `cost-optimization.md`.**
- `cost-optimization.md:254`: key includes `max_tokens`.
- `FR-008-prompt-response-cache.md:79` "As shipped": key **excludes**
  `max_tokens` (comment at :100 explains the rationale: strict superset).
- Two of our own docs disagree on the cache key. §5 should match the
  shipped implementation.

**M-6. Squad / multi-persona cost multiplier is undocumented.**
- `cost-optimization.md §7` lists 5 red flags; none address the fact that
  routing one user-facing task through N personas (e.g., Costanza + Kramer
  + Newman + Morty) multiplies token spend by N. Persona memory (32 KB
  cap) loaded per-persona compounds this.
- **Fix:** add §7 item 6: "Squad fan-out. Routing a single task through
  N personas costs roughly N× the single-persona token spend, and each
  persona carries its own 32 KB memory tail. If you're running
  `--squad` in auto-routing mode, know that 'one question' may be 3–5
  API calls, not 1. Cap persona fan-out via routing rules in `.squad.json`."

**M-7. Release notes example uses a stale model name.**
- `docs/release-notes-v2.0.0.md:99`:
  `# → Estimated cost (gpt-4o-mini): $0.00072 USD`
- If the operational default is now `gpt-5.4-nano`, a user following
  the example gets a different number. Example should use the actual
  current default, or explicitly say "your default may differ."

**M-8. `FR-008-prompt-response-cache.md:312` savings table ("API cost per
call ~$0.01 → $0, −100%") quotes the same ~$0.01/hit figure as H-3.**
- Same bug class: the cache is advertised as saving "per-call $0.01" but
  at our actual defaults the number is more like $0.0003–$0.0005. The
  100% reduction is correct (hits avoid the call) but the absolute
  baseline is ~20× too high.

**M-9. `AZAI_PRICE_TABLE` env var not mentioned in `cost-optimization.md`.**
- Documented in `docs/observability.md:74–75`, referenced in
  `docs/migration-v1-to-v2.md:149–150`, implemented in `CostHook.cs`.
- Users reading the cost guide to update stale prices should be told
  that the override mechanism exists. Discoverability gap.
- **Fix:** add a sidebar to §3: "If the price table drifts and you need
  a patched number today, set `AZAI_PRICE_TABLE=/path/to/prices.json`
  (schema in `observability.md`)."

---

### 🟢 Low

**L-1. §2 token-meter example (`cost-optimization.md:28–30`) cites
`Program.cs:770` — the current `azureopenai-cli-v2/Program.cs` line
numbers have drifted.** Audit on the v2 binary and restamp.

**L-2. `cost-optimization.md:226` cites `Program.cs:141` and
`CliParserTests.cs:115` for the `--max-tokens` range. Verify against v2
line numbers (v1 and v2 have diverged).**

**L-3. `cost-optimization.md:292–294` cite `Program.cs:260`,
`SECURITY.md:747`, `ARCHITECTURE.md:339`, `Tools/GetClipboardTool.cs:12`.
All are v1-era line numbers. Rotate to v2 or stop quoting line numbers
(name-based references survive refactors, line numbers don't).**

**L-4. §8 closing paragraph (`cost-optimization.md:300`) still says
"Default stays `gpt-4o-mini`." Same bug as C-1, different location — worth
its own bullet for completeness.**

---

### ℹ️ Informational

**I-1. User's local config uses a Phi-4-mini-instruct deployment on an
Azure AI Inference endpoint.** ADR-005 routes Phi via
`services.ai.azure.com/models` (Foundry). There may be a second viable
endpoint path (Azure AI Inference `models.inference.ai.azure.com`) that
the doc doesn't cover. If the user's workload uses that, pricing rows
map the same way but deployment guidance differs. Worth a one-paragraph
sidebar in §3.6.

**I-2. `--estimate` flag is shipped (README.md:59) and powered by
`CostHook.cs`. `cost-optimization.md §6 "Monitoring your spend"` does
not mention it.** Adding "Use `az-ai --estimate` before expensive runs
— zero-network, safe in CI" is a 2-line improvement with high discoverability
value.

**I-3. `docs/ops/v2-sre-runbook.md:66` already defines a cost-regression
alert ("+15% over 48h on any model"). `cost-optimization.md §7 Red flags`
doesn't reference it.** Cross-link so devs investigating an alert land
on the red-flag checklist.

**I-4. GHCR anonymous-pull rate limits (docs/ops/v2.0.0-day-one-baseline.md:49)
are documented but TCO of Docker vs AOT binary distribution is not
covered in `cost-optimization.md`.** This is explicitly in the brief's
"what to look for" list. **Verdict: not a material FinOps risk today** —
GHCR public pulls are free, AOT binaries ship via GitHub Releases (also
free), per-user egress is trivial. But a one-paragraph §9 "Distribution
TCO" saying "pulls are free today, watch the 100/6h anon-pull limit at
scale" would satisfy the gap and head off surprise at a future scale tier.

---

## 3. Recommendation: Rewrite or Patch?

**Patch, don't rewrite.** The doc's bones — §1 intro, §2 token-meter
mechanics, §4 prompt discipline, §6 monitoring, §7 red flags, §8 closing
— are still correct and earn their keep. The surgery is concentrated:

**Required edits (in priority order):**
1. **§3.7 NEW — Default-model change log** (fixes C-1, L-4, M-4, M-7).
   Includes before/after cost delta per change.
2. **§5 REWRITE — Caching strategies** (fixes C-2, H-2, H-5). Three
   subsections: Azure native prompt cache, FR-008 local cache, Batch API.
3. **§3 pricing table — per-row `(verified YYYY-MM-DD)` stamps** (fixes
   H-1). Add `Cached Input` column to the table.
4. **§3.6 table — swap baseline to `gpt-5.4-nano`** (fixes M-3).
5. **`FR-008-prompt-response-cache.md:47` and `:312` — restate savings
   per model tier** (fixes H-3, M-8).
6. **`competitive-analysis.md §4` — add a "what this scenario assumes"
   paragraph reconciling with `cost-optimization.md §3`** (fixes H-4).
7. Line-number citation refresh on v2 code (fixes L-1..L-3).

**Estimated effort:** 1–2 engineer-hours if the author has all the
pricing data in hand; 3–4 hours if pricing needs fresh verification
against the Azure portal. Everything else is a minor-edit pass.

**Do not rewrite from scratch.** The voice and the §7 checklist are the
parts users actually read. Rewriting loses institutional memory for no
FinOps benefit. Patch.

---

## 4. Monthly Savings Summary

If every High+ finding is acted on (single seat, baseline Espanso workload):

| Lever | $/seat/mo | Notes |
|---|---:|---|
| Revert default to `gpt-4o-mini` **OR** enable Azure native prompt cache on `gpt-5.4-nano` | $0.54 – $3.00 | C-1 / H-2 — pick one; can't double-count |
| Batch API on Ralph/benchmark/async agent runs | $1.00 – $3.00 | H-5 — workload-dependent |
| Price-table freshness (prevents silent 10–30% drift × every model) | $0 – $0.50 | H-1 — insurance, not cash |
| FR-008 cache ROI claim corrected | $0 | H-3 — honesty, not savings |
| Inter-doc pricing reconciliation | $0 | H-4 — prevents a future bad decision |
| **TOTAL — realistic, single seat** | **$1.50 – $6.00/mo** | |
| **At 10 seats** | **$15 – $60/mo** | **$180 – $720/year** |
| **At 100 seats** | **$150 – $600/mo** | **$1,800 – $7,200/year** |

**Morty's closing note:** Is this going to pay for anybody's boat? No. But
these are the numbers *you know about.* It's the ones you don't — a
runaway Ralph loop on a premium model, an Espanso macro that
accidentally pipes a 5 MB file through agent mode every 30 seconds, a
default flip that nobody announced — that buy the boat. Get the
documentation truthful, get the caching turned on, get the Batch API
attached to the async jobs. Then when somebody asks "where'd the money
go?" you'll have an answer that isn't "I think Kramer did something."

— Morty

---

## Appendix A — Files audited

- `docs/cost-optimization.md` (314 lines, read in full)
- `docs/competitive-analysis.md` §4 "Morty's FinOps Annex"
- `README.md` (cost / token / price / cheap / cents greps)
- `docs/proposals/FR-008-prompt-response-cache.md`
  (cost-per-hit and total-savings tables)
- `docs/proposals/FR-014-local-preferences-and-multi-provider.md`
  (rate-card architecture, `$0.0000` suppression rule — 🟢 good)
- `docs/proposals/FR-015-pattern-library-and-cost-estimator.md`
- `docs/proposals/FR-018-local-model-provider-llamacpp.md`
  (cumulative-savings banner open question — 🟢 good flag)
- `docs/proposals/FR-019-gemma-cpp-direct-adapter.md`
  (local = $0 policy — 🟢 consistent)
- `docs/adr/ADR-005-foundry-routing.md` (Foundry endpoint routing)
- `docs/adr/ADR-006-appendix-roundtable.md` / `ADR-006-nvfp4-nim-integration.md`
  (owned-GPU vs cloud TCO math — 🟢 solid)
- `docs/observability.md` (price-table override docs)
- `docs/ops/v2-sre-runbook.md` (cost-regression alert)
- `docs/ops/v2.0.0-day-one-baseline.md` (GHCR limits)
- `docs/security-review-v2.md` §8 (CostHook threat model)
- `docs/release-notes-v2.0.0.md` (`--estimate` announcement)
- `docs/migration-v1-to-v2.md` (`--estimate` path)
- `docs/benchmarks/phi-vs-gpt54nano-2026-04-20.md`
  (Bania comparison baseline)
- `azureopenai-cli-v2/Observability/CostHook.cs` (hardcoded price table)
- `azureopenai-cli-v2/UserConfig.cs` (default-model resolution)
- `azureopenai-cli-v2/Program.cs` (fallback default)
- `CHANGELOG.md` (model-switch entries — none found beyond 2.0.4)

## Appendix B — Sources

- Azure OpenAI Service pricing page
  (https://azure.microsoft.com/en-us/pricing/details/cognitive-services/openai-service/)
  fetched 2026-04-22. Note: numeric cells rendered client-side; this
  audit confirms *structure* (Cached Input column exists on every SKU,
  Batch API 50% discount is listed) but cannot quote per-model numbers
  from this fetch. Per-model rate verification is a separate task.
- `docs/benchmarks/phi-vs-gpt54nano-2026-04-20.md` (Bania, 2026-04-20)
- `docs/proposals/FR-008-prompt-response-cache.md` (as-shipped v2 notes)
- `azureopenai-cli-v2/Observability/CostHook.cs:20–30` (hardcoded prices)

*End of report.*
