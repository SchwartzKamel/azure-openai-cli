# Cost Optimization Guide

*Maintained by Morty Seinfeld, President of the Condo Association and Unpaid Chief Financial Officer of this Repository.*

---

## 1. "What are you, made of money?"

Listen to me. I watched Jerry spend forty-seven dollars on a salad last Tuesday — *a salad* — and you people are out here piping the entire contents of `/var/log` into `gpt-4.1` to ask if a semicolon is missing. We're going to have a little talk about the bill.

This document exists because nobody, **nobody**, reads the Azure invoice until it arrives with an extra digit. Model calls are not free. Tokens are not free. "Just rerun it with the bigger model" is not a strategy — it's how my son-in-law bought a boat.

Read this before you ship a feature. Read it again before you set `temperature=0` and walk away from a Ralph loop.

## 2. Understanding the bill

Azure OpenAI charges you for **two** things on every chat completion:

1. **Input tokens** — everything you send: system prompt, persona memory, piped stdin, the prompt, tool definitions, the whole schmear.
2. **Output tokens** — what the model generates back.

**Output is roughly 3× more expensive than input for most GPT-family models.** That's not a typo. Generating tokens costs more than reading them. So a 200-token answer costs more than 600 tokens of input context. Remember that the next time you ask the model to "explain in detail."

### Measuring what you spend

Good news — we already instrumented this. `az-ai` prints token usage on **stderr** after every call:

```
  [tokens: 1250→480, 1730 total]
```

That's `input → output, total`. It's also in `--json` output as `input_tokens` and `output_tokens` (see `docs/use-cases-standard.md` §1, `Program.cs:770`). If you're not reading this line, you're flying blind. Pipe stderr to a log, sum it per day, cry accordingly.

## 3. Model economics

Prices change. Regions differ. PTU vs PAYG differs. **Always confirm at the source:** https://azure.microsoft.com/pricing/details/cognitive-services/openai-service/

Rough order-of-magnitude as of **2026-04** (USD per 1M tokens, global PAYG, confirm before quoting in a PR):

| Model          | Input $/1M | Output $/1M | Use when                                                   |
|----------------|-----------:|------------:|------------------------------------------------------------|
| `gpt-4o-mini`  |   ~$0.15   |    ~$0.60   | **Default.** Espanso/AHK text fixes, summaries, commit messages, yes/no classifiers, anything a first-year intern could do. |
| `gpt-5.4-nano` |   $0.20    |    $1.25    | Fast reasoning model. Espanso workflows if speed matters more than cost; better latency than 4o-mini. |
| `gpt-4o`       |   ~$2.50   |   ~$10.00   | Reasoning matters. Multi-step explanations, code review, non-trivial refactors. |
| `gpt-4.1`      |   ~$3.00   |   ~$12.00   | Complex agent tool-calling, long-context synthesis, reliable JSON over many turns. |
| `DeepSeek-V3.2`|   $0.58    |    $1.68    | Ultra-cheap fallback. Non-OpenAI lineage; serverless on Azure Foundry. **Data residency caveats — see §3.5.** |
| `Phi-4-mini-instruct` | **$0.075** | **$0.300** | **Cheapest sane option.** Microsoft-first-party SLM on Foundry. Espanso rewrites, commit messages, yes/no classifiers. See §3.6. |
| `Phi-4-mini-reasoning`| $0.080 | $0.320 | Math / logic / Ralph-validator duty. NOT for Espanso — reasoning overhead kills TTFT. See §3.6. |
| `o1-mini`      |   ~$3.00   |   ~$12.00*  | Hard problems where you can afford seconds-to-minutes of latency. *Reasoning tokens bill as output.* |

> ⚠️ **Unverified numbers.** I am flagging these as estimates — I cannot hit the live Azure pricing API from this doc. The **ratios** (mini ~15× cheaper than 4o, 4o output ~4× its input) are stable; the absolute dollars drift. Always cite the pricing page above in a PR that changes a model default.
> 
> gpt-5.4-nano rates verified against Azure OpenAI pricing (2025-08-07 refresh); DeepSeek-V3.2 and both Phi-4-mini variants pulled from Azure Foundry serverless catalog (Microsoft Community Hub announcement + cloudprice.net corroboration, April 2026). Region and deployment type may drift prices ±10%.

**Morty's rule of thumb:** if you can't write a one-sentence justification for why `gpt-4o-mini` *cannot* do the job, you're using the wrong model. "It feels smarter" is not a sentence. That's how you buy a Cadillac to go to the mailbox.

### 3.5 The new kids on the block

> **TL;DR**
> - **Recommendation:** The **hardcoded fallback** stays `gpt-4o-mini`. A fresh install, no env, no config → you get the conservative, cheap, well-behaved SKU.
> - **Operator override supported:** `AZUREOPENAIMODEL=gpt-5.4-nano` (or UserConfig `default_model`) flips the *operational* default per ADR-009's precedence chain. That's how the current operator runs today — reasoning-forward workloads where the 4.3× input-cost bump pays for itself.
> - **Revisit the fallback itself when:** a reasoning-required use case becomes the *majority* of fresh-install traffic, or Morty approves a new cost ceiling for the default-no-config user.

**Canonicalization note (ADR-009):** the "default model" is a *resolution chain*, not a single value. Precedence: CLI flag → `AZUREOPENAIMODEL` env → UserConfig (`default_model` / smart-default) → hardcoded fallback (`Program.DefaultModelFallback = "gpt-4o-mini"`). Any doc that says "the default is X" without naming the tier is lying — call it out in review.

Listen, Kramer came to me with two hot new models and I held his feet to the fire. Let me give it to you straight.

**`gpt-5.4-nano`: The speedster with the price tag**

So you got `gpt-5.4-nano` deployed now. It's a reasoning model — that means it thinks before it answers. You know what that sounds good for? *Nothing in your primary use case.* Your Espanso triggers need a yes/no, a rewrite, a commit message. You don't need reasoning, you need *speed* and *cost-predictability*. And guess what: `gpt-5.4-nano` is **4.3× more expensive** than `gpt-4o-mini` on input tokens (33% cheaper on output, but who cares — you're sending the big payload, not receiving it).

Latency matters? Maybe. If you're piping full files through `--max-tokens 100` in a real-time Espanso workflow, the ~200ms reasoning overhead might *feel* slower. Proof: measure it. But the bill? The bill is the opposite of "speedy." **For the fresh-install fallback, `gpt-4o-mini` stands.** Operators who *want* reasoning-forward behavior flip `AZUREOPENAIMODEL=gpt-5.4-nano` — that's a supported, documented override, not a contradiction of this section. The fallback remains conservative because "what a brand-new user gets by accident" and "what an informed operator chose deliberately" are not the same contract.

**`DeepSeek-V3.2`: The bargain-basement trap**

Alright, now here's where I get nervous. DeepSeek-V3.2 on Azure Foundry looks cheap on paper—$0.58 input, $1.68 output. That's 3.9× cheaper than `gpt-4o-mini` on input. Your son-in-law is already hearing the boat horn, isn't he?

**But.** DeepSeek is *not OpenAI*. It's a non-OpenAI model from a Chinese research org. Now, Microsoft runs it on Azure Foundry with serverless deployment, so you get Azure's data centers and compliance framing. Here's what I don't like: **data residency and audit trail.** Your Espanso workflow reads clipboard content—potentially sensitive stuff: API keys, diff context, personal notes—and pipes it through an LLM. With OpenAI models, you get clear compliance: FedRAMP, BAA, SOC2. With DeepSeek on Foundry? You get Foundry's terms, which *are* enterprise-grade, but DeepSeek itself is a third-party model *routed through* Azure. If your clipboard contains secrets, or if you need auditable isolation, this is a **non-starter**. 

The SECURITY.md in this repo caps clipboard at 32 KB for a reason — we assume the content is sensitive. The threat model is "untrusted stdin, clipboard, and network responses." DeepSeek doesn't change the risk, but it *does* change the compliance profile. **Do not default to DeepSeek for Espanso workflows without signed approval from your security team.** For throw-away research or non-sensitive summarization? Sure, burn the tokens cheap. For the primary use case? No.

---

### 3.6 The Phi-4-mini twins — finally, something I can endorse

> **TL;DR**
> - **Decision:** Do **not** swap the Espanso default to `Phi-4-mini-instruct` yet. `gpt-4o-mini` stands.
> - **Why:** Cost wins are real (~6.5× cheaper per call), but empirical benchmarks (2026-04-20) show HTTP 422 on `--schema`, broken function calling, and 20% instruction-following failure on JSON-constrained prompts.
> - **Revisit when:** Foundry ships fixes for strict-JSON / tool-call parity — or when a new Phi-family release clears Bania's benchmark gate. Track via ADR-005 (Foundry routing) and the benchmark report linked below.

*[Morty, visibly relieved, loosening his tie]*

After the `gpt-5.4-nano` sticker-shock and the DeepSeek compliance headache, Kramer comes back with `Phi-4-mini`. Microsoft's own small language model family. 3.8B parameters. MIT-licensed. And — get ready — **seventy-five cents per million input tokens.** I read it twice. I made him read it twice. It's real.

> **⚠️ UPDATE 2026-04-20 — Empirical data arrived. Verdict: `Phi-4-mini-instruct` is NOT ready for the Espanso default.** The five open questions below are now answered with live benchmark data (Bania, commit `9dc4d2b`). Three kill-shots: HTTP 422 on `--schema`, broken function calling (emits `<|tool_call|>` as content, not valid `tool_calls[]`), and 20% instruction-following failure on JSON-constrained prompts. Cost wins are real (6.5× cheaper: ¢0.000135 vs ¢0.000885 per call), but UX regressions in core workflows would tank the product. **`gpt-4o-mini` default stands.** See [Benchmark Report](docs/benchmarks/phi-vs-gpt54nano-2026-04-20.md) for full data.

Let me lay it out:

| Metric                          | `gpt-4o-mini` (today's default) | `Phi-4-mini-instruct` |
|---------------------------------|:-------------------------------:|:---------------------:|
| Input $/1M                      | ~$0.15                          | **$0.075** (**2× cheaper**) |
| Output $/1M                     | ~$0.60                          | **$0.300** (**2× cheaper**) |
| Vendor                          | OpenAI (via Azure OpenAI)       | Microsoft (first-party, Foundry) |
| Compliance posture              | FedRAMP / BAA / SOC2            | Same — it's Microsoft's own model, not a third-party routed one |
| Parameters                      | ~8B (est.)                      | 3.8B                  |
| Context window                  | 128K                            | 128K                  |
| Chat Completions wire protocol  | ✅ native                        | ✅ OpenAI-compatible on Foundry |
| Function calling                | ✅ strong                        | ✅ supported (reliability: unverified at scale) |
| Strict JSON Schema output       | ✅ battle-tested                 | ⚠️ supported, needs live validation in our `--schema` path |

**`Phi-4-mini-instruct`: The real candidate for an Espanso-default swap**

This isn't DeepSeek. This is Microsoft's own model, running on Microsoft's infrastructure, under Microsoft's compliance framework. The clipboard threat model doesn't get worse — the data never leaves the same Azure tenancy you're already paying for. So the security objection that killed DeepSeek does **not** apply here.

For the primary use case — Espanso text rewrites, commit messages, one-paragraph summaries, yes/no classifiers — a 3.8B model is *plenty*. That's not a controversial take, that's Microsoft's entire Phi thesis: small, focused, cheap, good-enough. **It is literally the model they built for this.**

The catch? **Integration cost.** Our hand-rolled path in `Program.cs` currently points at Azure OpenAI endpoints. Foundry uses the same OpenAI-compatible chat-completions dialect (confirmed via Foundry Models API), but it's a *different endpoint host* and a *different deployment-name convention*. That's not a port, that's a config change — but someone has to route `AZURE_DEPLOYMENT=Phi-4-mini-instruct` through the Foundry hostname instead of the Azure OpenAI hostname. See Phase 0 pt 2 on `plan.md` — the spike's Foundry path is still a `NotImplementedException` stub.

**`Phi-4-mini-reasoning`: Not for Espanso. Ralph validator, maybe.**

Five-thousandths of a dollar more per 1M input tokens than the instruct variant ($0.080 vs $0.075). You're not paying for the parameters — you're paying for the reasoning overhead at inference time, which burns output tokens AND latency. Reasoning tokens bill as output. Sound familiar? That's the `o1-mini` pattern all over again, just at pauper rates.

**Do not put this behind an Espanso trigger.** The reasoning warm-up is going to blow your TTFT and nobody types a commit message while waiting 2 seconds for the model to "think."

**Where it earns its keep:** the Ralph validator loop. Ralph iterates up to 10 times by default; each iteration wants a judgment — "is the output correct / complete?" — which is exactly the logic-inference task Phi-4-mini-reasoning was trained on. At $0.08/$0.32, ten iterations of a 2K-token exchange costs pennies. That's a future FR — not a default change today, but worth a benchmark sprint. File it.

**"What about Phi-3.5-mini-instruct?" — No. Next question.**

Kramer asked. I checked. Here's the whole story in one table so nobody burns another hour on this:

| Metric              | `Phi-3.5-mini-instruct` (Aug 2024) | `Phi-4-mini-instruct` (Feb 2025) |
|---------------------|:----------------------------------:|:--------------------------------:|
| Input $/1M          | $0.13                              | **$0.075** (**1.73× cheaper**)   |
| Output $/1M         | $0.52                              | **$0.300** (**1.73× cheaper**)   |
| Parameters          | 3.8B                               | 3.8B                             |
| Context window      | 128K                               | 128K                             |
| MMLU / reasoning    | Lower                              | **Significantly higher**          |
| Multilingual        | Yes                                | Improved                         |

Phi-3.5 is a **strict Pareto loss** vs. Phi-4-mini: costs more, performs worse, same hardware footprint. Microsoft is pricing the older SKU *up* to push you to the new one — that's not a suggestion, that's a traffic cop. Unless you're mid-migration on a Phi-3.5 deployment or you've got a regional-availability quirk we don't (we don't — we're on neither), **there is no cost-ROI scenario in which Phi-3.5-mini-instruct wins.** Moving on.

**Open questions before making `Phi-4-mini-instruct` the new default — NOW ANSWERED:**

1. **Strict JSON Schema mode (`--schema`):** ❌ **REJECTED — HTTP 422 error.** Foundry's Phi endpoint does NOT respect `response_format: json_schema` with `strict: true`. Every `--schema` caller gets a hard error. This is a non-starter for structured-output workflows. (see [Benchmark Report](docs/benchmarks/phi-vs-gpt54nano-2026-04-20.md) §5)

2. **Function-calling reliability under agent mode:** ❌ **FAIL.** The 3.8B model does NOT emit valid OpenAI-protocol `tool_calls[]`. Instead, it outputs the literal string `<|tool_call|>` as content. This breaks every `--agent` mode invocation. Gpt-5.4-nano: 1/1 PASS. Phi: 0/1 FAIL. (see [Benchmark Report](docs/benchmarks/phi-vs-gpt54nano-2026-04-20.md) §6)

3. **Availability in the user's region:** ⚠️ **Still unverified.** Phi-4-mini on Foundry may not be in every region yet. Out of scope for this benchmark sprint — Bania focused on US-East. Regional parity check deferred.

4. **Cold-start behavior on Foundry serverless:** ✅ **Phi WINS.** First-call latency: Phi ~0.78s, gpt-5.4-nano ~1.14s. Phi is 31% faster on cold-start TTFT. This is a genuine advantage for Espanso, which fires sporadically. (see [Benchmark Report](docs/benchmarks/phi-vs-gpt54nano-2026-04-20.md) §3)

5. **Instruction-following at 3.8B parameters:** ⚠️ **80% pass rate (4/5).** Phi nails single-word directives (P1, P3, P4, P5 — all PASS). But P2 ("respond with only a JSON object") FAILS: Phi wraps the JSON in markdown fences (```json {...}```), which the regex strict-match rejects. This is a *specific failure mode* — Phi has instruction-following capability but breaks when JSON is the **only** constraint (no preamble prose). Gpt-5.4-nano: 5/5 PASS. (see [Benchmark Report](docs/benchmarks/phi-vs-gpt54nano-2026-04-20.md) §4)

**Head-to-head numbers from Bania's full run:**

| Metric                    | gpt-5.4-nano  | Phi-4-mini-instruct | Winner | Notes |
|---------------------------|:-------------:|:-------------------:|:------:|-------|
| **TTFT (mean)**           | 1.14s         | 0.62s               | 🔥 Phi | 46% faster cold-start |
| **Throughput (chars/sec)** | 428 cps       | 157 cps             | gpt-5.4 | 2.7× more chars/sec |
| **Cost per call** (¢USD)   | ¢0.000885     | ¢0.000135           | 🔥 Phi | **6.5× cheaper** |
| **Instruction-follow**    | 5/5 (100%)    | 4/5 (80%)           | gpt-5.4 | Phi fails JSON-only |
| **Strict JSON Schema**    | ✅ PASS       | ❌ HTTP 422         | gpt-5.4 | Phi unsupported |
| **Function calling**      | ✅ PASS       | ❌ FAIL             | gpt-5.4 | Phi emits literal `<\|tool_call\|>` |
| **Streaming stability**   | 5/5 (100%)    | 4/5 (80% hang rate) | gpt-5.4 | Phi had 1 of 5 timeouts |

**Morty's new verdict:**

You paid HOW much for a model that can't even output JSON when you ask for JUST JSON?

Here's the thing: the cost savings are *real*. ¢0.000135 per call vs ¢0.000885 is a **6.5× reduction** — at scale that's money you don't have to explain to anybody. The TTFT win (0.78s vs 1.14s, 46% faster) is also real. Phi-4-mini is built for exactly our use case. I get it.

But look at the body count:
- No `--schema` support (HTTP 422 hard error).
- No agent mode (function calling emits garbage).
- 20% instruction-following failure specifically on JSON-constrained prompts (the exact thing Espanso users want to do: "give me ONLY the fixed string").
- Streaming hangs at non-trivial rates.

That's not a product regression — that's a **user experience crater.** You ask Espanso for a one-liner rewrite, it hangs for 90 seconds, or returns wrapped-in-backticks garbage, you've just trained every user to switch back to manual typing.

**Verdict: `gpt-4o-mini` default stands. Phi-4-mini is NOT ready for Espanso yet.**

The cost savings don't justify the UX loss. Full stop.

*Could Phi be fine for a narrow, opt-in use case?* Yes — read-only Espanso triggers with simple prose constraints, supervised by users who explicitly set `AZURE_DEPLOYMENT=Phi-4-mini-instruct` in a config. That's a future feature request, not a default change. File it after we close the gate on the three blockers above.

**Conditions for a future default flip:**

Before I sign off on making Phi-4-mini-instruct the Espanso default, Microsoft Foundry upstream would need to fix:

- [ ] Proper `tool_calls[]` emission in agent mode (not literal `<|tool_call|>` strings).
- [ ] `response_format=json_schema` HTTP 200 support with `strict: true` (currently HTTP 422).
- [ ] Instruction-following parity on single-line JSON constraints ("output ONLY the JSON" without markdown wrappers).
- [ ] Streaming stability (eliminate the 20% hang rate — this isn't about speed, it's about reliability).

When all four are checked, revisit this. Until then, the `gpt-4o-mini` default is not just safer — it's the *right* choice for the product.

---

## 4. Prompt-engineering for cheap

Every token in your prompt is a token you're paying for on **every single call**. Persona memory? Every call. System prompt? Every call. Hey! Where's the beef?!

### Shave the system prompt

The default is:
```
"You are a secure, concise CLI assistant. Keep answers factual, no fluff."
```
(see `Program.cs:25`). That's ~15 tokens. Honestly? Fine. Leave it alone. But if *you* are shipping a feature with a 400-token system prompt full of "You are a highly skilled, world-class, senior-principal..." — stop. The model doesn't care. You're paying to flatter a matrix.

### Cap output aggressively with `--max-tokens`

`--max-tokens` is a ceiling on generation. For short-fix workflows it is the single highest-leverage knob you have:

| Use case                        | Recommended `--max-tokens` |
|---------------------------------|---------------------------:|
| Espanso inline rewrite          | **150–200**                |
| Commit message generator        | **100**                    |
| One-paragraph summary           | **150**                    |
| Code explanation (2–3 sentences)| **200**                    |
| Long-form writing               | 1500–2000                  |

Example from `docs/espanso-ahk-integration.md`:
```bash
xclip -selection clipboard -o | az-ai --raw --max-tokens 100 \
  --system 'Write a concise conventional commit message for this diff.'
```
At `--max-tokens 100` on `gpt-4o-mini` you're talking fractions of a cent per call. That's the game. Valid range is 1–128000 (`Program.cs:141`, enforced; tests in `CliParserTests.cs:115`).

### Use `--raw` for machine-consumed output

`--raw` strips framing (no `[tokens:]` line on stderr is *not* affected — that still prints; `--raw` just means the stdout payload is ready for pipes). It doesn't change API cost, but it prevents downstream parsers from triggering a *second* call to "fix the format." A second call is a second bill.

### Respect the 32 KB persona memory cap

`PersonaMemory` tail-truncates at 32,768 bytes (`Squad/PersonaMemory.cs:13`). That's **the right discipline** — don't raise it. Unbounded history = unbounded input tokens = unbounded bill. If you think you need more, you need summarization, not a bigger cap.

## 5. Caching strategies (mostly future work)

The cheapest API call is the one you don't make.

### What's safely cacheable

- **Deterministic tasks** — `temperature=0`, no tools, no clock-dependent input.
- **Idempotent transforms** — "classify this sentence," "extract the commit type," "is this SQL valid."
- **Stable system prompt + stable user prompt** — hash them together as the cache key.

### What's **not** safely cacheable

- Anything with `temperature > 0` (non-deterministic by design).
- Agent mode with tool calls (side effects, fresh context each turn).
- Anything that reads the filesystem, clipboard, or stdin with changing content.

### Proposed: `--cache-dir` flag (future FR)

A file-backed local cache keyed by `sha256(system_prompt || user_prompt || model || temperature || max_tokens)`, with explicit opt-in per invocation. Espanso triggers are the perfect first customer — same snippet, same prompt, same answer, charged once.

> **Open proposal.** Not implemented today. If you want to write the FR, see `docs/proposals/` for the template.

## 6. Monitoring your spend

### Daily one-liner via Azure CLI

```bash
# Yesterday's spend on the Cognitive Services resource, summarized by meter.
az consumption usage list \
  --start-date $(date -d 'yesterday' +%Y-%m-%d) \
  --end-date $(date +%Y-%m-%d) \
  --query "[?contains(instanceName, 'openai')].{date:date, meter:meterName, cost:pretaxCost, currency:currency}" \
  -o table
```

### Per-invocation tracking (DIY)

Since we already print `[tokens: X→Y, Z total]` on stderr, log it:

```bash
az-ai "summarize this" 2> >(tee -a ~/.az-ai-tokens.log >&2)
# Then at end of day:
grep -oP 'tokens: \d+→\d+, \K\d+' ~/.az-ai-tokens.log \
  | awk '{s+=$1} END {print s " tokens today"}'
```

Multiply by the per-model rate from §3 and weep.

### Azure Cost Management API

For dashboards: `GET /subscriptions/{sub}/providers/Microsoft.CostManagement/query` with a filter on `ResourceType eq 'Microsoft.CognitiveServices/accounts'`. Full reference on the Azure docs site — link above.

## 7. Red flags to watch

These are the ways the bill gets away from you. Memorize them.

1. **Ralph mode in a runaway loop.** `--max-iterations` is the guard — **default 10, hard cap 50** (`Program.cs:260`, `SECURITY.md:747`, enforced in tests). Do NOT bump it to 50 "just in case." Fifty iterations of `gpt-4o` with full context is a real number of dollars. If Ralph isn't converging in 10, the validator is wrong, not the limit.
2. **Oversized prompts from piped stdin.** The 32 KB prompt cap (`MAX_PROMPT_LENGTH`) is your friend. Someone `cat file.json | az-ai` with a 5 MB file is a budget event. The cap is already there — don't disable it.
3. **Agent mode with `shell_exec` producing huge outputs.** Stdout is capped at **64 KB** per child invocation (`ARCHITECTURE.md:339`, `use-cases-agent.md:282`). That cap exists *because* an unbounded `find /` round-trips back into the model as input tokens. Keep the cap. If you need more, write a tool that returns a summary, not a firehose.
4. **Clipboard tool abuse.** `GetClipboardTool` truncates at 32 KB (`Tools/GetClipboardTool.cs:12`). Someone copies a CSV, feeds it to the agent, the agent quotes it back at you in the answer — you just paid for that CSV twice.
5. **Premium model on trivial tasks.** If `AZURE_DEPLOYMENT=gpt-4.1` is your default in `.env`, every one-line commit message is premium-priced. Set the default to `gpt-4o-mini` and override explicitly when you need the muscle.

## 8. "Why pay more?"

Look. I'm not telling you to eat at the Bistro every night. Sometimes you need `gpt-4.1` — sometimes the job is worth it. I'm telling you to **know which night it is**. (And no, `gpt-5.4-nano` doesn't change this. Neither does DeepSeek. `Phi-4-mini-instruct` *looked* promising — 6.5× cheaper, 46% faster TTFT — but Bania's benchmark killed it: HTTP 422 on `--schema`, broken function calling, JSON-constrained instruction-following failures, streaming hangs. The UX loss outweighs the cost win. Default stays `gpt-4o-mini`.)

**Morty's top three savings tips:**

1. **Default to `gpt-4o-mini`.** Escalate with a written reason. ~15× cheaper on input. Every team that switches sees the bill drop the same week.
2. **Cap `--max-tokens` on every short-fix workflow.** `--max-tokens 150` on an Espanso trigger is the single best ROI change in this codebase.
3. **Read the `[tokens: X→Y, Z total]` line.** If you're not reading it, you're not managing it. If you're not managing it, somebody's buying a boat with your money and it isn't you.

You paid HOW much for a pair of sneakers? And now you want the premium model for a yes/no classifier? **Not on my watch.**

— Morty

---

*See also: `SECURITY.md` (caps and guards), `docs/use-cases-standard.md` §8 (`--max-tokens`), `docs/espanso-ahk-integration.md` (real-world thin prompts), Azure pricing: https://azure.microsoft.com/pricing/details/cognitive-services/openai-service/*
