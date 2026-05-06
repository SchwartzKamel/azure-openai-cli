# Espanso Tier Cheatsheet

> "60 tokens. That's it. You went over once -- I noticed. You won't go over twice."
> -- Lt. Bookman, S03E02

This card lists every Espanso trigger shipped under `examples/espanso-ahk-wsl/espanso/`,
grouped by the response-length tier doctrine established in S03E02
(`docs/exec-reports/s03e02-the-library-cops-word-limit.md`). Tier discipline is
not optional. Every new trigger MUST declare a tier in its PR description and
pick the smallest tier that fits.

Source files (read-only -- do not edit from this card):

- `examples/espanso-ahk-wsl/espanso/ai-windows-to-wsl.yml` -- 22 triggers (S03E01 + S03E02)
- `examples/espanso-ahk-wsl/espanso/ai-prompts.yml` -- 6 triggers (commit `905515e`)

Provenance tags used below:

- `[S03E01]` -- original 20 triggers shipped in S03E01 (the form-trigger episode).
- `[S03E02]` -- triggers added when Bookman tightened the doctrine (`:aishort`, `:aiyml`).
- `[905515e]` -- Maestro's prompt-template triggers from commit `905515e`.

## Tier doctrine

| Tier | max-tokens | Char target | When to reach for it |
|------|-----------:|------------:|----------------------|
| **Snap**     |   60 | ~150         | Chat-app fast-lane, 1 sentence, you'll send it in Slack |
| **Chat**     |  250 | ~700         | Standard chat reply, 2-4 short sentences |
| **Document** |  800 | ~2400        | Bullet lists, structured data, commit messages, regex + explain |
| **Mirror**   | 1500 | matches input | Rewrite, translate, fix, shrink, anonymize -- length tracks input |
| **Free**     | 4096 | unbounded    | User-controlled open prompt -- the user knows what they asked for |

Generation runs at roughly 30 ms per token at GPT-4o-mini speeds. A Snap reply
lands ~1.8 s sooner than an unbounded one. *That is the entire game.*

## Quick decision tree

- Need a one-liner for chat? -> **Snap**
- Replying in a thread, want a short answer? -> **Chat**
- Drafting documentation, a commit, a regex, a structured list? -> **Document**
- Rewriting, translating, fixing, anonymizing existing text? -> **Mirror**
- Don't know? -> **Free**, then narrow the tier next time.

If you find yourself re-running the same prompt twice (once short, once long
with `:ai`), the tier is wrong. File a finding. Bookman re-tiers it.

---

## Snap tier (60 max-tokens, ~150 chars, 1 sentence)

> "One sentence. One hundred and fifty characters. You think this is a
> negotiation? It is not."

| Trigger | Max-tokens | Description | Example input | Example output |
|---------|-----------:|-------------|---------------|----------------|
| `:aiq ` `[S03E01]`     | 60 | Free-form one-shot question via form. No clipboard. | `capital of Vermont` | `Montpelier.` |
| `:aishort ` `[S03E02]` | 60 | Snap-tier free-form -- faster `:ai` for chat replies. | `is bash arithmetic 0-indexed` | `Yes, bash arrays are 0-indexed.` |

**Total: 2 triggers.**

---

## Chat tier (250 max-tokens, ~4 short sentences)

> "Two to four sentences. If sentence five shows up, sentence five is
> unauthorized."

| Trigger | Max-tokens | Description | Example input | Example output |
|---------|-----------:|-------------|---------------|----------------|
| `:aiexp` `[S03E01]`   | 300 | Explain the clipboard in 2-3 sentences. Code-aware. | clipboard: a Linq expression | "This filters the list to even numbers, then doubles them. Returns a new IEnumerable<int>. Lazy-evaluated." |
| `:aireply` `[S03E01]` | 400 | Draft a reply to clipboard email/message. Form: intent + tone. | clipboard: "Can you ship by Friday?" / intent: "decline politely" | A 3-sentence diplomatic decline. |
| `:aitldr` `[S03E01]`  | 120 | TL;DR of clipboard in exactly 2 short sentences (<=300 chars). | clipboard: long Slack thread | "Team agreed to delay launch one week. Frank owns the comms." |

**Total: 3 triggers.** (`:aireply` and `:aiexp` ride above the 250-token nominal but stay in tier by sentence-count discipline -- documented compromise in the trust-model header.)

---

## Document tier (800 max-tokens, structured / bulleted / paragraph form)

> "Be thorough. Be concise. Pick one filler word, I dare you."

| Trigger | Max-tokens | Description | Example input | Example output |
|---------|-----------:|-------------|---------------|----------------|
| `:aibullets` `[S03E01]`        | 1000 | Convert clipboard prose to a clean bullet-point list, grouped under bold headings if multi-topic. | clipboard: meeting notes | `**Decisions**\n- Ship Friday\n- Hire QA lead\n**Risks**\n- ...` |
| `:aic` `[S03E01]`              |  500 | Senior code review of clipboard. Bugs, security, design. Bullets only. (Untiered slip -- 500 tokens lands between Chat and Document.) | clipboard: a C# method | `- Null-deref risk on line 4 ...\n- Use Span<T> to avoid allocation` |
| `:aicommit` `[S03E01]`         |  400 | Conventional Commit message from a clipboard `git diff`. | clipboard: `git diff --cached` output | `feat(parser): support trailing comma in tuple literals\n\n...` |
| `:aidata` `[S03E01]`           | 1000 | Extract facts/dates/decisions/action-items from clipboard, grouped by category. | clipboard: meeting transcript | `**People**\n- ...\n**Action Items**\n- ...` |
| `:aiexpand` `[S03E01]`         | 2000 | Expand clipboard text -- richer detail, same voice. (Brushes the Mirror line; review in next audit.) | clipboard: 2-sentence outline | 4-paragraph elaboration. |
| `:aifix` `[S03E01]`            |  500 | Fix grammar/spelling in clipboard. Preserves tone and formatting. | clipboard: `their going too the store` | `They're going to the store.` |
| `:aiquestion` `[905515e]`      |  800 | Template A: structured Q&A / guidance with system-prompt scaffolding. | form: `How do I rotate Azure OpenAI keys?` | Step-by-step grounded answer. |
| `:airegex` `[S03E01]`          |  600 | Two-way: regex -> explanation, or NL description -> PCRE regex + examples. | clipboard: `^\d{3}-\d{4}$` | `Matches a 3-digit area code...\nExamples: 555-1234 ...` |

**Total: 8 triggers.** Note: `:aic` was never assigned a tier in the original
header table. Bookman flagged it. It lives here by token-count gravity.

---

## Mirror tier (1500 max-tokens, length tracks input)

> "If the input is two lines, the output is two lines. We do not pad."

| Trigger | Max-tokens | Description | Example input | Example output |
|---------|-----------:|-------------|---------------|----------------|
| `:aianon` `[S03E01]`           | 1500 | Redact PII in clipboard -- replace with role-based placeholders (`[PERSON_1]`, `[EMAIL_1]`, ...). | clipboard: email with names + phone | Same email, names/phones replaced with placeholders. |
| `:aiarch` `[905515e]`          | 1500 | Template B: end-to-end Azure AI architecture / solution plan. | form: project goals + constraints | Architecture proposal with components, data flow, risks. |
| `:aicode` `[905515e]`          | 1200 | Template C: minimal reproducible code/template with setup, tests, troubleshooting. | form: language + runtime + task | Working code block + install + smoke test. |
| `:aicost` `[905515e]`          | 1200 | Template E: cost / ROI / break-even analysis with optimization recommendations. | form: workload assumptions | Cost model + recommendations. |
| `:aidataworkflow` `[905515e]`  | 1500 | Template D: data pipeline / ETL / MLOps workflow design. Renamed from `:aidata` in 2026-05 to resolve the collision with the original `:aidata` in `ai-windows-to-wsl.yml` -- the original wins, the prompt-templates entry got the more specific name. | form: source + sink + cadence | Pipeline plan with stages, governance, observability hooks. |
| `:aiflip` `[S03E01]`           | 1000 | Devil's-advocate analysis of clipboard. Assumptions, counter-arguments, risks. | clipboard: a proposal | 3-section critique. |
| `:airw` `[S03E01]`             |  600 | Rewrite clipboard in a more professional, polished tone. Preserve meaning. | clipboard: blunt Slack message | Polished email-grade rewrite. |
| `:aishrink` `[S03E01]`         | 1000 | Compress clipboard to ~half length, preserve every key fact. | clipboard: 4-paragraph note | 2-paragraph version. |
| `:aitone` `[S03E01]`           | 1000 | Rewrite clipboard in a chosen tone (form: 8 tones, whitelisted). | clipboard + tone="ELI5" | Plainspoken rewrite. |
| `:aitr` `[S03E01]`             | 1500 | Translate clipboard to a chosen language (form choice). | clipboard EN + tone="Japanese" | Idiomatic JA translation. |
| `:aiyml ` `[S03E02]`           | 1500 | Generate a new espanso YAML trigger block from a description. Output is NOT auto-typed -- manual paste. | form: `:aithesaurus -- synonyms for clipboard word` | Valid YAML block matching the project pattern. |

**Total: 11 triggers.** The four `905515e` template triggers were authored before
the tier audit; they exceed the strict Mirror definition (length tracks input)
but fit by token budget. Slated for re-tier review.

---

## Free tier (4096 max-tokens, user-controlled)

> "You asked. You wait. Just don't ask twice."

| Trigger | Max-tokens | Description | Example input | Example output |
|---------|-----------:|-------------|---------------|----------------|
| `:ai ` `[S03E01]`     | 4096 | Open prompt via multi-line form. The escape valve. | form prompt | Whatever you asked for. |
| `:aiimg` `[S03E01]`   | n/a  | Image generation via `--image` mode. Output goes to clipboard as PNG. | form prompt | PNG on clipboard. |
| `:aiweb ` `[S03E01]`  | 2000 | Agent-mode prompt with `web_fetch` tool, 5 rounds, citation-bearing answer. | form prompt | Synthesized answer with `[Source](URL)` citations. |

**Total: 3 triggers.**

---

## Reference / no LLM call

| Trigger | Max-tokens | Description |
|---------|-----------:|-------------|
| `:aiprompts` `[905515e]` | n/a | Static replace -- prints the prompt-template index (`:aiquestion`, `:aiarch`, `:aicode`, `:aidataworkflow`, `:aicost`). No az-ai invocation, no token cost. |

**Total: 1 trigger.**

---

## Cast totals

- 28 triggers across two YAML files.
- Snap: 2 | Chat: 3 | Document: 8 | Mirror: 11 | Free: 3 | Reference: 1.
- Untiered-at-time-of-ship: `:aic` (Document by token gravity) and the four
  `905515e` template triggers (Mirror by budget, pending audit).
- Name collision resolved 2026-05: the second `:aidata` (in `ai-prompts.yml`)
  was renamed to `:aidataworkflow`. The original `:aidata` in
  `ai-windows-to-wsl.yml` keeps its slot. Cross-file collision detection now
  enforced by `scripts/lint-espanso-yml.sh`.

## Authoring new triggers

Reach for **`:aiyml`** (Mirror tier, output not auto-typed -- you paste it
yourself) when you draft a new trigger. The system prompt enforces the
project's unified S03 pattern: trigger key, SendKeys-safe placeholder, two
backspace substitutions, the `wsl.exe -e bash -lc` pipeline, finally-block
restore. It also forces you to declare a tier and set `--max-tokens`
accordingly.

Rules of the road, in order of how often Bookman has had to enforce them:

1. **Pick the smallest tier that fits.** The user can always escalate to `:ai`.
   They cannot un-wait the 6 seconds you made them sit through.
2. **No magic numbers.** `--max-tokens 750` "because it felt right" gets
   re-tiered on sight.
3. **End every system prompt with "Output ONLY the X. No preamble."** The
   token budget is the ceiling. The brevity language is the soft enforcement.
   Belt and suspenders.
4. **Document the residual.** If the tier choice is a compromise (say, a
   long-thread `:aireply` clipping at Chat tier) note that in the trust-model
   header so users know to escalate.
5. **Snap is the default for ambiguous cases.** Not Chat. Not Document.
   *Snap.*

If you ship a 4096-token trigger to answer "yes or no", I will know. I will
come for that trigger the way I came for *Tropic of Cancer* in 1971.

## Bookman's audit log (read this before you argue with the tier)

Every entry below is a real lesson paid for in latency, tokens, or both.
Read them before the next PR.

- **Snap was almost named "Tweet".** Killed in review. Snap is shorter and
  does not date-stamp the doctrine to a specific product. Lesson: tier names
  describe the response, not the channel.
- **`:aitldr` wanted 250 max-tokens.** It got 120. Two short sentences fit in
  120. Two long sentences fit in 120. Two padded sentences with three filler
  clauses each do not fit in 120, and the user does not want them anyway.
- **`:aireply` wanted 800.** It got 400. A four-sentence email reply fits in
  400 with room to spare. The trust-model header says so. Argue with the
  header, not the budget.
- **`:aiexpand` brushes Mirror at 2000 tokens.** Acknowledged. On the docket
  for re-tier. If it gets used to expand 200-character notes into 5-paragraph
  essays, it gets cut to 1000.
- **`:aic` slipped through with no tier at all.** The original 20 triggers
  shipped before the doctrine. `:aic` was one of them. It is a 500-token
  Document-tier trigger by token gravity. Documented now so it is not
  forgotten in the next quarterly audit.
- **The four `905515e` template triggers (`:aiarch`, `:aicode`, `:aicost`,
  `:aidataworkflow` from prompts) were authored under Maestro's prompt-library
  doctrine, not the tier doctrine.** They sit at 1200-1500 tokens and do
  not strictly mirror input length. They are tagged Mirror by budget,
  flagged for re-tier review, and may end up split: a Document-tier
  short-form for chat, a Mirror-tier long-form for the form variants.
- **`:aidata` collision -- resolved 2026-05.** Originally `:aidata` was
  defined in both `ai-windows-to-wsl.yml` (extract facts from clipboard)
  and `ai-prompts.yml` (data-pipeline design template). The prompt-templates
  entry was renamed to `:aidataworkflow` -- a more specific name that
  reflects its end-to-end data workflow scope. The original `:aidata` in
  `ai-windows-to-wsl.yml` is unchanged. `scripts/lint-espanso-yml.sh` now
  fails on cross-file trigger collisions between the prompt-templates kit
  and any platform-variant kit, so this class of bug cannot land again.

## How to use this card

- New contributor wiring up Espanso for the first time? Read the Quick
  decision tree and the tier doctrine table. Pick a trigger. Skip the rest
  until you need it.
- Reviewing a PR that adds a new trigger? Check that the PR description
  declares a tier, and that the tier matches the `--max-tokens` value in
  the YAML. If they disagree, the PR description wins -- update the YAML.
- Doing the quarterly tier audit? Re-grep this card against
  `examples/espanso-ahk-wsl/espanso/*.yml`. Any new trigger missing from
  this card is missing a tier. Any tier that drifted from the source YAML
  is a finding.

> "I don't judge. I budget."

-- Lt. Bookman, badge number `--max-tokens`
