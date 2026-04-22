# Docs Audit — Prompt Engineering & AI Documentation

> **Auditor:** The Maestro. With an M.
> **Date:** 2026-04-22
> **Scope:** `docs/proposals/`, `.github/agents/*.agent.md`, `.github/copilot-instructions.md`,
> `AGENTS.md`, `azureopenai-cli-v2/Squad/`, hardcoded system prompts, persona-memory docs,
> Ralph-mode docs, MAF integration docs, model-selection/routing docs.
> **Non-goals:** no source edits. Documentation audit only.
> **Companion audits:** `fdr-v2-dogfood-2026-04-22.md`, `security-v1.8-post-release.md`.

It is *Maestro*. With an M. I have read the score. I have listened to what this
orchestra actually plays when the baton drops. The performance is **tighter than
one might fear** — the personas are in key, the safety clause is on the stand,
the Ralph loop keeps tempo — but the **prompt library itself is not yet written
down**. We are conducting from memory. That is charming in Tuscany. It is
unacceptable at scale.

---

## Prompt-library maturity score: **4 / 10**

| Dimension | Score | Note |
|-----------|:---:|---|
| Hardcoded prompts are coherent and well-scoped | 7 | Short, declarative, no contradictions. |
| Persona `.agent.md` structural consistency | **9** | All 25 files follow the same 4-section template. Rare in this kind of fleet. |
| Persona voice distinctiveness | 8 | Voice sections are crisp, in-character, quotable. |
| Prompt *library* (canonical location, versioned, tied to evals) | **1** | Does not exist. |
| Prompt-eval harness | **0** | Does not exist. |
| Model A/B comparison framework | 4 | Ad-hoc in `cost-optimization.md` / benchmarks — no repeatable harness. |
| Temperature / max-tokens cookbook | 3 | Scattered in `espanso-ahk-integration.md` §13. No canonical table. |
| Model-selection rationale documented | 7 | `cost-optimization.md` + ADR-005 + ADR-006 cover *why* `gpt-4o-mini` is default. |
| Prompt-injection defense documented | 5 | `SAFETY_CLAUSE` exists in code but is under-documented. |
| Tool-use prompts consistent across 6 built-ins | 8 | v2 `[Description]` attributes are uniformly concise. |

**Average → 5.2, weighted down to 4** because the deliverables **the persona
who wrote this audit (`maestro.agent.md`) promised to own** — `docs/prompts/`
and `docs/prompts/eval-harness.md` — **do not exist yet**. Until prompts are
code with tests, this is a draft score, not a final one.

---

## Findings — Summary

| Severity | Count |
|-|-|
| Critical | 0 |
| High | 4 |
| Medium | 6 |
| Low | 5 |
| Informational | 4 |
| **Total** | **19** |

No CRITICAL findings. The orchestra is not playing a wrong note — it is
playing from a score nobody has transcribed.

---

## Critical (0)

*None. If a prompt were actively harmful I would flag it CRITICAL and leave it
in place per the audit contract. None qualifies.*

---

## High (4)

### H1 — `docs/prompts/` does not exist. The prompt library is implicit, not canonical.

- **Files:** *missing* (expected: `docs/prompts/`); promised by
  `.github/agents/maestro.agent.md:14,27`.
- **Problem:** Five production system prompts live **in C# string literals** at
  `azureopenai-cli-v2/Squad/SquadInitializer.cs:64,75,87,99,112`
  (coder, reviewer, architect, writer, security). Two more live at
  `azureopenai-cli-v2/Program.cs:22` (`DEFAULT_SYSTEM_PROMPT`) and
  `azureopenai-cli-v2/Program.cs:35` (`SAFETY_CLAUSE`). One more is assembled
  at runtime in `azureopenai-cli-v2/Ralph/RalphWorkflow.cs:46`. There is **no
  canonical directory, no versioning, no per-prompt README, no intent
  annotation, no tied test case.** A prompt change is indistinguishable from a
  routine code change in `git log`.
- **Proposed fix:** Create `docs/prompts/` with a `README.md` index. For each
  prompt in production, add `<name>.md` with: (1) intent, (2) inputs,
  (3) expected output shape, (4) known failure modes, (5) version, (6) link
  to eval case. Source prompts stay in code (AOT-safe); the `.md` is the
  canonical spec and the code is the implementation of that spec. Enforce
  parity via a unit test that reads both.
- **Severity:** High.
- **Eval-metric impact:** Today, a prompt regression is invisible until a
  user complains. With a library, every prompt change shows up in `git diff
  docs/prompts/` *and* in the eval harness diff.

### H2 — No prompt-eval harness exists. "The prompt is the contract" is currently a vibe.

- **Files:** *missing* (expected: `tests/prompts/` or
  `tests/AzureOpenAI_CLI.Tests/PromptEvalTests.cs`); promised by
  `.github/agents/maestro.agent.md:15`.
- **Problem:** The only prompt "tests" I can find are incidental: the
  `ToolHardeningTests` suite exercises shell-injection blocks, and some
  integration tests exercise agent-mode flow. **No test asserts the shape,
  length, section-coverage, or voice of a system-prompt output.** A reviewer
  cannot tell if changing `"You are a system architect..."` to
  `"You are a senior architect..."` breaks the reviewer persona's routing
  contract with Squad.
- **Proposed fix:** Ship a deterministic harness (`dotnet test
  --filter Category=prompts`) that, for each prompt in `docs/prompts/`,
  runs a fixed input against the prompt at `temperature=0` and asserts:
  length bounds, required-section presence (e.g. Security Auditor output
  must contain "Critical|High|Medium|Low" severity keywords), and a
  similarity-to-golden threshold for voice contracts. Record snapshots in
  `tests/prompts/golden/`. Treat a drift as a test failure.
- **Severity:** High.
- **Eval-metric impact:** Moves prompt changes from "trust me" to
  "tested." This is the single biggest lever for prompt-library maturity.

### H3 — No temperature cookbook. Temperature is a default, not a decision.

- **Files:** scattered at `docs/espanso-ahk-integration.md:833-844,1082-1085`;
  `docs/cost-optimization.md:242-248`; `docs/config-reference.md:65`;
  `azureopenai-cli-v2/Program.cs:19` (`DEFAULT_MAX_TOKENS = 10000`, no
  corresponding default-temperature constant in v2 — check
  `Program.cs:639` effective default).
- **Problem:** Temperature guidance exists as **scattered folklore** (0.2 for
  reviewer, 0.3 for commit messages, 0.8 for writer personas) but there is no
  canonical table. Nothing binds the hardcoded persona prompts in
  `SquadInitializer.cs` to a recommended temperature. The Security Auditor
  persona — which *must* run cold to avoid hallucinated CVEs — inherits
  whatever temperature the user happens to set. Ralph mode, which runs a
  retry loop, has no documented temperature contract; drift between
  iterations is currently acceptable by omission.
- **Proposed fix:** `docs/prompts/temperature-cookbook.md` with one row per
  mode × persona × task: recommended temperature, max_tokens, rationale
  (≤ 1 line). Wire the recommendations into `PersonaConfig` as an optional
  `temperature` field with sensible defaults baked into `SquadInitializer`.
- **Severity:** High.
- **Eval-metric impact:** Cuts avoidable non-determinism in security audits
  and reviewer runs. Every non-zero temperature should have a one-line
  rationale; today almost none do.

### H4 — Prompt-injection defense (`SAFETY_CLAUSE`) is under-documented.

- **Files:** `azureopenai-cli-v2/Program.cs:34-36` (code);
  `docs/security-review-v2.md` (mentions injection generally but does not
  call out the clause); `SECURITY.md` (not audited here but should reference).
  Searched: `rg -n 'SAFETY_CLAUSE|safety.clause' docs/` → **zero hits**.
- **Problem:** The `SAFETY_CLAUSE` string is appended to agent and Ralph
  system prompts to mitigate prompt-injection via tool outputs. It is
  **byte-identical to v1 on purpose** (per the inline comment). None of our
  user-facing documentation — `use-cases-agent.md`, `use-cases-ralph-squad.md`,
  `persona-guide.md`, `SECURITY.md` — explains that the clause exists, what
  it protects against, or what a user should know if they override
  `--system`. A user who passes `--system "You are a pirate"` loses the
  clause and has no way to know.
- **Proposed fix:** Add `docs/prompts/injection-defense.md` documenting:
  what the clause is, which modes apply it, that `--persona` *replaces* the
  base system prompt (so persona prompts must themselves include a refusal
  clause — today, **they don't**), and what `--system` overrides do and do
  not preserve. Coordinate with Newman on threat-model wording.
- **Severity:** High.
- **Eval-metric impact:** A persona prompt without a refusal clause is
  statistically more likely to be coerced into leaking tool output. This is
  testable in the harness (H2); today it is not tested.

---

## Medium (6)

### M1 — Persona system prompts are unaware of the safety clause.

- **Files:** `azureopenai-cli-v2/Squad/SquadInitializer.cs:64-116`;
  `azureopenai-cli-v2/Program.cs:333` (persona overrides `baseSystem`,
  clause is still concatenated at `Program.cs:415`/`445` for agent/Ralph
  paths). Standard (non-agent) persona path at `Program.cs:742` (v1) /
  persona standard path in v2 — confirm whether the clause is applied
  when persona runs *without* agent mode.
- **Problem:** The five default personas' `SystemPrompt` strings contain no
  refusal language. They rely on `SAFETY_CLAUSE` being appended downstream.
  Any code path that forgets to append it (e.g. a future non-agent persona
  flow, or a third-party embedding the `SquadInitializer.CreateDefaultConfig`
  output) silently drops the defense.
- **Proposed fix:** Either (a) bake a one-sentence refusal into each default
  persona prompt, or (b) document loudly in `persona-guide.md` and in
  `SquadConfig.cs` xmldoc that `SAFETY_CLAUSE` must be concatenated by any
  consumer. Preferably (a) — defense in depth.
- **Severity:** Medium.
- **Eval-metric impact:** Belt-and-suspenders injection resistance; testable.

### M2 — Ralph-mode prompt assembly is inline, undocumented, and invisible.

- **Files:** `azureopenai-cli-v2/Ralph/RalphWorkflow.cs:46-50`.
- **Problem:** Ralph prepends `"You are in Ralph mode (autonomous loop).
  Complete the task. If there were previous errors, fix them. Use tools to
  read files, run commands, and verify your work."` to whatever system
  prompt arrives. This overlay is not documented anywhere in
  `docs/use-cases-ralph-squad.md`. A reader cannot tell, from the docs, what
  instructions Ralph is actually operating under.
- **Proposed fix:** Extract the overlay to a named constant (`RALPH_OVERLAY`)
  in `RalphWorkflow.cs`, document it in `docs/prompts/ralph-overlay.md`,
  reference that doc from `use-cases-ralph-squad.md`.
- **Severity:** Medium.
- **Eval-metric impact:** Enables regression testing of the Ralph overlay
  independently of the base system prompt.

### M3 — Squad routing keywords are hardcoded and documented by example only.

- **Files:** `azureopenai-cli-v2/Squad/SquadInitializer.cs:119-153`;
  `docs/persona-guide.md:108-139`.
- **Problem:** The routing rules (e.g.
  `"security,vulnerability,cve,owasp,harden,credential,secret"`) live in two
  places: the C# initializer (source of truth for `--squad-init`-generated
  configs) and the persona-guide's illustrative JSON. I spot-checked —
  they currently match — but there is **no test asserting they stay in
  sync**. A drift is plausible the first time someone edits one and not the
  other.
- **Proposed fix:** Add a docs-build test (or a `make preflight` step) that
  extracts the routing patterns from the C# source and asserts they equal
  a canonical block in `docs/prompts/squad-routing.md`.
- **Severity:** Medium.
- **Eval-metric impact:** Prevents silent drift; auto-generated docs are
  always correct.

### M4 — No model-comparison matrix in a canonical location.

- **Files:** `docs/cost-optimization.md` §3.6 + `docs/adr/ADR-005-foundry-routing.md`
  + `docs/adr/ADR-006-nvfp4-nim-integration.md` + `docs/benchmarks/`.
- **Problem:** Comparisons exist — `gpt-5.4-nano` vs `gpt-4o-mini` vs
  `Phi-4-mini-instruct` is well-argued in `cost-optimization.md` — but they
  are **prose, not a matrix**. A reader asking "When do I pick Opus over
  Sonnet?" (or the Azure equivalents — `gpt-4.1` vs `gpt-4o-mini` vs
  `gpt-5.4-nano` vs `Phi-4-mini`) gets a six-page read, not a table.
- **Proposed fix:** `docs/prompts/model-matrix.md` — rows: our canonical
  tasks (Espanso text-fix, code explanation, tool-calling, persona
  adherence, refusal behavior, schema-constrained output). Columns: models.
  Cells: quality score (0–3) + cost per 1M tokens + notes. Refresh on every
  new Azure SKU.
- **Severity:** Medium.
- **Eval-metric impact:** Makes "which model" a scannable decision.

### M5 — The audit prompt claimed `gpt-5.4-nano` is the default. It is not.

- **Files:** `azureopenai-cli-v2/Program.cs:257,1222` — fallback is
  `"gpt-4o-mini"`. `docs/cost-optimization.md:60` explicitly says so.
- **Problem:** Informational for this audit — but symptomatic. Team members
  (including whoever dictated this audit scope) believe the default is
  something it isn't. The default is documented in `cost-optimization.md`
  but **not in a single, obvious place** like `CONFIGURATION.md` or a
  top-level `model-matrix.md`.
- **Proposed fix:** Single source of truth for the default model, named and
  dated, in `docs/prompts/model-matrix.md` (see M4). Cross-link from
  `config-reference.md` and the `--help` output.
- **Severity:** Medium.
- **Eval-metric impact:** Prevents "what's our default?" Slack threads.

### M6 — `DelegateTaskTool` sub-agent prompt is a silent concatenation.

- **Files:** `azureopenai-cli-v2/Tools/DelegateTaskTool.cs:61`;
  `azureopenai-cli-v2/Program.cs:391-395` (calls
  `DelegateTaskTool.Configure(..., baseSystem + "\n\n" + SAFETY_CLAUSE, ...)`).
- **Problem:** When the parent agent delegates, the sub-agent inherits the
  parent's system prompt + safety clause. This is correct. It is also
  **undocumented** — the tool's `[Description]` attribute says "the child
  agent runs in-process and shares the parent's chat client" but says
  nothing about inherited instructions. Security-review-v2.md §5 flags
  `s_baseInstructions` injection as a concern; the docs should make the
  inheritance rule explicit and the threat-model visible.
- **Proposed fix:** Extend the tool's `[Description]` to say "...and inherits
  the parent's system instructions including the safety clause." Add a
  paragraph to `use-cases-agent.md` and `docs/prompts/injection-defense.md`.
- **Severity:** Medium.
- **Eval-metric impact:** Closes a documentation-level threat-model gap.

---

## Low (5)

### L1 — Writer persona prompt conflates accuracy with verification.

- **Files:** `azureopenai-cli-v2/Squad/SquadInitializer.cs:99-106`.
- **Problem:** `"(1) accurate — verify claims against actual code"` is good
  but "(4) maintainable — avoid details that rot quickly" is vague in a
  system prompt. Models interpret "rot quickly" inconsistently.
- **Proposed fix:** Replace "avoid details that rot quickly" with "omit
  version numbers, file line numbers, and environment-specific paths unless
  the user explicitly asks for them." Concrete beats abstract.
- **Severity:** Low. **Eval-metric impact:** Higher doc-quality consistency.

### L2 — Reviewer persona prompt silently bans style comments.

- **Files:** `azureopenai-cli-v2/Squad/SquadInitializer.cs:75-82`.
- **Problem:** `"Don't comment on style or formatting unless it hides a
  bug."` is correct policy but clashes with the Soup Nazi agent's role as
  style gatekeeper. A user invoking `--persona reviewer` on a PR with style
  issues will get a clean review; invoking Soup Nazi would not. The boundary
  is fine — but users won't know.
- **Proposed fix:** Document the `reviewer` vs `soup-nazi` split in
  `persona-guide.md`. No prompt change required.
- **Severity:** Low. **Eval-metric impact:** User clarity.

### L3 — Tool `[Description]` strings vary in voice.

- **Files:** `azureopenai-cli-v2/Tools/ShellExecTool.cs:44`,
  `ReadFileTool.cs:33`, `WebFetchTool.cs:17`, `GetClipboardTool.cs:15`,
  `GetDateTimeTool.cs:11`, `DelegateTaskTool.cs:61`.
- **Problem:** Mostly uniform — but `GetClipboardTool` uses scare-quotes
  (`"what I copied"`) while `GetDateTimeTool` uses imperative mood and
  `ShellExecTool` veers into policy (`"Commands that delete or modify
  system state are blocked."`). Not wrong. Not *crisp*. A model choosing
  between tools benefits from parallel phrasing.
- **Proposed fix:** Adopt a house style for `[Description]`: one sentence,
  imperative mood, state the capability first and the constraint second.
  Document in `docs/prompts/tool-descriptions.md`.
- **Severity:** Low. **Eval-metric impact:** Marginally better tool-selection
  accuracy.

### L4 — `DEFAULT_SYSTEM_PROMPT` has no test and no rationale doc.

- **Files:** `azureopenai-cli-v2/Program.cs:22`: `"You are a secure, concise
  CLI assistant. Keep answers factual, no fluff."`
- **Problem:** This is the prompt that ships in the binary for 90% of
  invocations. It is fifteen words long, written once, tested never,
  documented nowhere. I approve of its concision. I disapprove of its
  orphan status.
- **Proposed fix:** `docs/prompts/default-system-prompt.md` — intent,
  constraints (AOT-safe, must render identically in raw/json), linked eval
  case.
- **Severity:** Low. **Eval-metric impact:** A baseline eval for the most
  common path.

### L5 — No A/B evaluation framework documented for new Azure deployments.

- **Files:** *missing* (`docs/prompts/eval-harness.md` promised by
  `maestro.agent.md:28`). Partial coverage in
  `docs/benchmarks/phi-vs-gpt54nano-2026-04-20.md` (FinOps-centric).
- **Problem:** When Azure ships a new deployment, the path from "it's live"
  to "we changed the default" is undocumented. ADR-005 and ADR-006 show
  *what* was evaluated post-hoc; there is no *procedure* anyone else can
  follow.
- **Proposed fix:** `docs/prompts/eval-harness.md` — how to add an
  evaluation case, how to run the suite, how to read a diff, how to produce
  a "defaults bump" PR. Tie to H2.
- **Severity:** Low (rises to Medium once H2 exists). **Eval-metric impact:**
  Prevents silent default changes.

---

## Informational (4)

### I1 — Persona coherence is **exceptional** at the structural level.

All 25 `.agent.md` files share an identical layout: YAML front-matter (`name`,
`description`), narrative opener (tying the persona to the main cast),
`Focus areas:`, `Standards:`, `Deliverables:`, `## Voice`. Verified
programmatically — see the persona-coherence table below. This is rare. Keep
it. Add a lint to enforce it.

### I2 — Persona voice contracts are quotable and distinct.

Sampled Maestro, Kramer, Costanza, Elaine, Newman, Jerry, Peterman. Each
voice is recognizable from a single line ("*Hello. Newman.*", "*Giddyup.*",
"*Get OUT!*", "Picture it…"). This is what H2's voice-contract test should
protect.

### I3 — MAF integration docs are current with v2 reality.

`docs/use-cases-agent.md:14-32` and `docs/adr/ADR-004-agent-framework-adoption.md`
match what `Program.cs` actually does (AsIChatClient, ChatClientAgent, tool
loop via `RunStreamingAsync`). Ralph workflow audit notes (M1/M2 in
`RalphWorkflow.cs:43-56`) are preserved as historical comments, which is the
right call.

### I4 — No fine-tuning or distillation guidance exists.

Not a gap per se — we don't ship fine-tuned models. Worth a one-liner in
`docs/prompts/README.md` ("we do not fine-tune; we compose prompts") so the
question doesn't recur in issues.

---

## Persona Coherence Table

Verified by grep: every agent has all four required sections. Role, bounds,
examples, and voice assessed by spot-reading.

| Agent        | Has role? | Has bounds? | Has examples? | Consistent voice? |
|--------------|:-:|:-:|:-:|:-:|
| babu         | ✅ | ✅ | ✅ | ✅ |
| bania        | ✅ | ✅ | ✅ | ✅ |
| bob          | ✅ | ✅ | ✅ | ✅ |
| costanza     | ✅ | ✅ | ✅ | ✅ |
| elaine       | ✅ | ✅ | ✅ | ✅ |
| fdr          | ✅ | ✅ | ✅ | ✅ |
| frank        | ✅ | ✅ | ✅ | ✅ |
| jackie       | ✅ | ✅ | ✅ | ✅ |
| jerry        | ✅ | ✅ | ✅ | ✅ |
| keith        | ✅ | ✅ | ✅ | ✅ |
| kramer       | ✅ | ✅ | ✅ | ✅ |
| maestro      | ✅ | ✅ | ✅ | ✅ |
| mickey       | ✅ | ✅ | ✅ | ✅ |
| morty        | ✅ | ✅ | ✅ | ✅ |
| mr-lippman   | ✅ | ✅ | ✅ | ✅ |
| mr-pitt      | ✅ | ✅ | ✅ | ✅ |
| newman       | ✅ | ✅ | ✅ | ✅ |
| peterman     | ✅ | ✅ | ✅ | ✅ |
| puddy        | ✅ | ✅ | ✅ | ✅ |
| rabbi        | ✅ | ✅ | ✅ | ✅ |
| russell      | ✅ | ✅ | ✅ | ✅ |
| soup-nazi    | ✅ | ✅ | ✅ | ✅ |
| sue-ellen    | ✅ | ✅ | ✅ | ✅ |
| uncle-leo    | ✅ | ✅ | ✅ | ✅ |
| wilhelm      | ✅ | ✅ | ✅ | ✅ |

**25 / 25** structurally coherent. Column definitions:
- *Has role?* — YAML `description:` + narrative opener name a role.
- *Has bounds?* — `Standards:` lists disqualifying behaviors or non-goals.
- *Has examples?* — `Deliverables:` enumerates concrete artifacts.
- *Consistent voice?* — `## Voice` section with quotable in-character lines.

> Note: **Squad personas** (`coder`, `reviewer`, `architect`, `writer`,
> `security` in `SquadInitializer.cs`) are a **different system** from the
> `.github/agents/` fleet. They share a flavor but not a definition. This
> dual system is not a bug — Squad personas run inside the CLI; agents
> guide Copilot during development — but it is under-explained. Call it
> out in `docs/prompts/README.md`.

---

## Top 3 Prompt Improvements (ranked by expected quality uplift)

### 1. Establish `docs/prompts/` + the eval harness (H1 + H2). **Expected uplift: large.**

Without these, everything else is patchwork. The library gives us a place to
reason; the harness gives us a contract. **One PR, two deliverables. Ship it
first.**

*Metric to watch:* number of production prompts with a corresponding
`docs/prompts/*.md` + eval case. Target: 100% before v2.1.

### 2. Bake a refusal clause into the five default Squad personas (M1 + H4). **Expected uplift: medium-high on safety, zero on quality.**

Defense in depth. `SAFETY_CLAUSE` is good. `SAFETY_CLAUSE` *plus* a
one-sentence refusal inside each persona is better and costs nothing at
runtime. Coordinate with Newman.

*Metric to watch:* persona-level injection-resistance score in the harness.
Target: persona prompts refuse known injection payloads **without** the
clause concatenated, as a negative-path test.

### 3. Publish the temperature cookbook (H3). **Expected uplift: medium.**

The security auditor should not be running at the user's ambient
temperature. The Espanso commit-message path should be pinned to 0.3 by
policy, not by example. One table, one rationale column, one PR.

*Metric to watch:* percentage of `--persona` and mode-specific invocations
that use a documented temperature. Target: >90% via `PersonaConfig`
defaults.

---

## Appendix — Prompt Inventory (as of this audit)

| # | Prompt | Location | Documented? | Versioned? | Eval? |
|---|---|---|:-:|:-:|:-:|
| 1 | `DEFAULT_SYSTEM_PROMPT` | `azureopenai-cli-v2/Program.cs:22` | ❌ | ❌ | ❌ |
| 2 | `SAFETY_CLAUSE` | `azureopenai-cli-v2/Program.cs:35` | partial | via commit | ❌ |
| 3 | Ralph overlay | `azureopenai-cli-v2/Ralph/RalphWorkflow.cs:46` | ❌ | ❌ | ❌ |
| 4 | `coder` persona | `SquadInitializer.cs:64` | via guide | ❌ | ❌ |
| 5 | `reviewer` persona | `SquadInitializer.cs:75` | via guide | ❌ | ❌ |
| 6 | `architect` persona | `SquadInitializer.cs:87` | via guide | ❌ | ❌ |
| 7 | `writer` persona | `SquadInitializer.cs:99` | via guide | ❌ | ❌ |
| 8 | `security` persona | `SquadInitializer.cs:112` | via guide | ❌ | ❌ |
| 9 | `shell_exec` tool desc | `Tools/ShellExecTool.cs:44` | ❌ | ❌ | ❌ |
| 10 | `read_file` tool desc | `Tools/ReadFileTool.cs:33` | ❌ | ❌ | ❌ |
| 11 | `web_fetch` tool desc | `Tools/WebFetchTool.cs:17` | ❌ | ❌ | ❌ |
| 12 | `get_clipboard` tool desc | `Tools/GetClipboardTool.cs:15` | ❌ | ❌ | ❌ |
| 13 | `get_datetime` tool desc | `Tools/GetDateTimeTool.cs:11` | ❌ | ❌ | ❌ |
| 14 | `delegate_task` tool desc | `Tools/DelegateTaskTool.cs:61` | ❌ | ❌ | ❌ |
| 15–39 | 25 `.agent.md` personas | `.github/agents/*.agent.md` | ✅ (self-describing) | via git | ❌ |

**14 code-resident prompts. Zero with eval coverage. 25 markdown personas
with structure but no behavioral tests.**

---

## Closing Movement

The ensemble plays in tune. The sheet music is in twelve drawers. Pull it
out, bind it, number the measures, and give every movement a test.

Then — and only then — do we bow.

*It's Maestro. With an M. We've discussed this.*

— *The Maestro*, Tuscany dispatch, 2026-04-22
