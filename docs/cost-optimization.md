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
| `gpt-4o`       |   ~$2.50   |   ~$10.00   | Reasoning matters. Multi-step explanations, code review, non-trivial refactors. |
| `gpt-4.1`      |   ~$3.00   |   ~$12.00   | Complex agent tool-calling, long-context synthesis, reliable JSON over many turns. |
| `o1-mini`      |   ~$3.00   |   ~$12.00*  | Hard problems where you can afford seconds-to-minutes of latency. *Reasoning tokens bill as output.* |

> ⚠️ **Unverified numbers.** I am flagging these as estimates — I cannot hit the live Azure pricing API from this doc. The **ratios** (mini ~15× cheaper than 4o, 4o output ~4× its input) are stable; the absolute dollars drift. Always cite the pricing page above in a PR that changes a model default.

**Morty's rule of thumb:** if you can't write a one-sentence justification for why `gpt-4o-mini` *cannot* do the job, you're using the wrong model. "It feels smarter" is not a sentence. That's how you buy a Cadillac to go to the mailbox.

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

Look. I'm not telling you to eat at the Bistro every night. Sometimes you need `gpt-4.1` — sometimes the job is worth it. I'm telling you to **know which night it is**.

**Morty's top three savings tips:**

1. **Default to `gpt-4o-mini`.** Escalate with a written reason. ~15× cheaper on input. Every team that switches sees the bill drop the same week.
2. **Cap `--max-tokens` on every short-fix workflow.** `--max-tokens 150` on an Espanso trigger is the single best ROI change in this codebase.
3. **Read the `[tokens: X→Y, Z total]` line.** If you're not reading it, you're not managing it. If you're not managing it, somebody's buying a boat with your money and it isn't you.

You paid HOW much for a pair of sneakers? And now you want the premium model for a yes/no classifier? **Not on my watch.**

— Morty

---

*See also: `SECURITY.md` (caps and guards), `docs/use-cases-standard.md` §8 (`--max-tokens`), `docs/espanso-ahk-integration.md` (real-world thin prompts), Azure pricing: https://azure.microsoft.com/pricing/details/cognitive-services/openai-service/*
