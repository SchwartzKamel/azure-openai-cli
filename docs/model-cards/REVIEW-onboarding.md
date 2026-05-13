# Onboarding review -- S04E01 *The Registry* (Lloyd Braun)

> Junior-developer-lens review of the Wave 1 model-card and registry docs.
> The question this review answers: could a new contributor read just these
> files and add a fourth model card correctly on their first try?
> Findings below are observations, not blockers; Elaine triages.

## Reading order I followed

1. `docs/model-cards/README.md` (format spec + index)
2. `docs/model-cards/azure-gpt-4o-mini.md` (example card)
3. `docs/model-cards/azure-gpt-5.4-nano.md` (example card)
4. `docs/model-cards/local-llama.md` (example card)
5. `docs/adr/ADR-012-model-registry-seam.md` (design decision + vocabulary)
6. `docs/episode-briefs/s04e01-the-registry.md` (deliverables + acceptance criteria)

---

## Things I had to look up (or would have had to)

### J-01 -- "AOT" (Ahead-of-Time compilation) -- first use without definition

**File:** `docs/adr/ADR-012-model-registry-seam.md`, Status section, line 43.

**Jargon:** "Typed record: `ModelRegistryEntry` is a C# `record` with fields... all fields are AOT-serializable via `AppJsonContext`."

**Why it tripped me up:** I'm a junior; I don't know whether "AOT" is a C# concept, a JVM concept (JIT vs AOT compilation), a project requirement, or a vendor lock-in term. I'd reach for Google before reading further.

**Fix sketch:** Add one-sentence definition inline in ADR-012 Status section, or link to `.github/copilot-instructions.md` where it's explained as "Native AOT single-file binary." Since AOT appears 6+ times across the 6 docs, a forward-link to the context would help.

---

### J-02 -- "Seam" (design pattern term) -- used without introducing the concept

**File:** `docs/adr/ADR-012-model-registry-seam.md`, title and line 29.

**Jargon:** "Introduce a `ModelRegistry` loader backed by an embedded JSON resource (`azureopenai-cli/Registry/registry.json`) and an optional user-override file at `~/.config/az-ai/registry.json`." ... "a typed data structure backed by an embedded JSON resource, with a stable schema that Acts II and III can consume without reopening the implementation."

**Why it tripped me up:** The title says "Model Registry Seam." Line 29 says "plant the seam first." I don't know whether "seam" means a boundary layer, a joint where two things attach, a test-doubles pattern, or something else. The ADR frames it as a design pattern but never explicitly defines it.

**Fix sketch:** In ADR-012 Decision section, add one sentence: "A 'seam' is a boundary layer that decouples one part of the system from another, allowing Acts II and III to depend on the registry interface without rewriting the registry implementation." Or link to where the project uses seams elsewhere (`ModelRegistry` is the canonical example here, so defining it once in situ is fine).

---

### J-03 -- "rc=99" (exit code convention) -- used without explaining the number

**File:** `docs/adr/ADR-012-model-registry-seam.md`, line 47, and ADR-012 Consequences section, line 110.

**Jargon:** "Any tag not in `AllowedTags` causes the registry load to exit with `rc=99`. This is intentionally strict."

**Why it tripped me up:** Why 99? Is it a Unix convention? Does 99 mean "data error"? Is it project-specific? The brief says "Unknown capability tag -> rc=99", but there's no explanation of the number's meaning.

**Fix sketch:** Add a note in the episode brief's "Acceptance criteria" section or in ADR-012 Consequences: "`rc=99` is the exit code for registry/schema validation failures (data shape errors)."

---

### J-04 -- "Embedded resource" (C# / .NET build concept) -- assumed reader knows how it works

**File:** `docs/adr/ADR-012-model-registry-seam.md`, line 35, and episode brief line 57-58.

**Jargon:** "Introduce a `ModelRegistry` loader backed by an embedded JSON resource (`azureopenai-cli/Registry/registry.json`)..."

**Why it tripped me up:** I don't know what "embedded resource" means in C# context. Is it a .NET Framework feature? Does it compile into the binary? Does it ship in a separate file? How does it differ from just including the JSON file in the source tree? The episode brief says "Kramer must register `ModelRegistryEntry` and `ModelRegistryEntry[]` there; otherwise the AOT linker will trim the type," which assumes I know what the linker does.

**Fix sketch:** Add one sentence in ADR-012 Decision section: "An embedded resource is a file compiled into the binary itself; when the binary runs, it reads the JSON from its own compiled data, not from disk, ensuring zero-config startup even on offline systems."

---

### J-05 -- "AllowedTags" (closed set of capability tags) -- the list buried in a note, not front-and-center

**File:** `docs/model-cards/README.md`, line 31-32, and `docs/adr/ADR-012-model-registry-seam.md`, line 45-46.

**Jargon:** "Only values from the closed set defined in `ModelCapability.AllowedTags`: `tool_calls`, `vision_in`, `vision_out`, `json_mode`, `streaming`, `system_prompt`."

**Why it tripped me up:** I found the list, but it's buried in a prose sentence. If I'm adding a fourth card and want to use a capability tag, I need to know the full allowed set. This is mentioned in three places (README, ADR, episode brief) with slightly different phrasing. I'd have to cross-reference to be sure I have the canonical list.

**Fix sketch:** Create a canonical capability-tags glossary or put the allowed list in a fenced code block in README.md for easy reference. Or link to `ModelCapability.AllowedTags` in the codebase.

---

### J-06 -- "YAML front matter" (format convention) -- referred to by name without showing an example

**File:** `docs/model-cards/README.md`, line 16.

**Jargon:** "Each card is a Markdown file with a YAML front matter block followed by four required prose sections."

**Why it tripped me up:** I see "YAML front matter" and immediately scroll down to the example block. But if I'm new to the project and Jekyll/Hugo conventions aren't familiar, I might not know what front matter *is*. The README shows the YAML block clearly, so this is minor, but the prose introduces the term before the example.

**Fix sketch:** This is okay-as-is; the code block at line 18-26 is immediately below the introduction. The term is clarified by the example. Not a blocker.

---

### J-07 -- "Capabilities" (abstract term, defined late) -- used before the vocabulary is locked down

**File:** `docs/model-cards/README.md`, line 6.

**Jargon:** "Where the registry supplies machine-readable metadata (capability tags, context window, cost tier)..."

**Why it tripped me up:** Line 6 uses "capability tags" without defining what they are. I have to read to line 31 to see the allowed set. A junior reader would pause here and ask, "What are capability tags? Should I read ahead or look elsewhere?"

**Fix sketch:** Add a one-sentence forward-reference in README.md Purpose section: "Capability tags (such as `tool_calls`, `vision_in`, `json_mode`) describe what the model can do."

---

### J-08 -- "GGUF" and "quantisation" (ML model format) -- assumed reader knows both terms

**File:** `docs/model-cards/local-llama.md`, line 21.

**Jargon:** "Model identity is entirely determined by the GGUF file the user loads into llama.cpp. This card describes the **default expectation** for a capable instruction-tuned model (e.g., Llama 3 8B or equivalent); actual behaviour depends on the specific GGUF and quantisation level."

**Why it tripped me up:** I'm a junior C# / CLI developer. I don't know what GGUF is (it's a LLaMA model file format), and "quantisation" is an ML term for reducing model precision. A junior contributor adding a card for a new Azure model would have no use for this knowledge, but the card assumes they do.

**Fix sketch:** Add brief inline definitions: "GGUF (a LLaMA model file format) and quantisation level (the precision at which the model weights are stored)."

---

### J-09 -- "Instruction-tuned model" (ML training concept) -- used without definition

**File:** `docs/model-cards/local-llama.md`, line 22.

**Jargon:** "This card describes the **default expectation** for a capable instruction-tuned model (e.g., Llama 3 8B or equivalent)..."

**Why it tripped me up:** Does "instruction-tuned" mean fine-tuned to follow instructions? Is it different from a base model? I'd have to Google "instruction-tuned LLM" to understand the distinction.

**Fix sketch:** One-sentence inline definition: "An instruction-tuned model is one that has been fine-tuned to follow user prompts and instructions reliably, unlike a base model which predicts text without explicit instruction."

---

### J-10 -- "Chat template" (llama.cpp config concept) -- used without explaining what it does

**File:** `docs/model-cards/local-llama.md`, line 25.

**Jargon:** "Tool-call support requires a model that was fine-tuned for function calling; a base model loaded without the correct chat template will produce malformed or absent tool-call payloads."

**Why it tripped me up:** I don't know what a "chat template" is or why it matters. Is it a prompt format? A config file? Does the local provider set it automatically?

**Fix sketch:** One-sentence definition: "A chat template is a prompt format that tells the local LLM how to structure its response; without the correct template, tool calls won't be formatted correctly."

---

### J-11 -- "Espanso and AHK text-expansion workflows" (use-case jargon) -- assumed reader knows what these are

**File:** `docs/model-cards/azure-gpt-4o-mini.md`, line 14; `docs/episode-briefs/s04e01-the-registry.md`, line 42.

**Jargon:** "Espanso and AHK text-expansion workflows that are the primary `az-ai` use case."

**Why it tripped me up:** I'm a junior contributor. I might not know Espanso (a cross-platform text expander) or AHK (AutoHotkey). The brief assumes I know what "text-expansion workflows" means for `az-ai`. If I'm new to the project, I'd stop and search for Espanso.

**Fix sketch:** Add a forward-reference in the episode brief or README: "Espanso (a cross-platform text-expansion tool) and AHK (AutoHotkey, a Windows automation language) are the primary use cases for `az-ai`."

---

### J-12 -- "Streaming" (LLM delivery method) -- used without explaining token-at-a-time vs. complete response

**File:** `docs/model-cards/azure-gpt-4o-mini.md`, line 16.

**Jargon:** "Streaming latency is low enough that single-sentence expansions feel instantaneous."

**Why it tripped me up:** The card says streaming latency is low, but it doesn't say *what streaming is*. I'd infer from context that it means tokens arrive one at a time instead of all at once, but that's a guess.

**Fix sketch:** One-sentence definition in the Strengths section of a card that mentions streaming: "Streaming (delivering tokens as they are generated, one at a time) has low latency, so single-sentence expansions feel instantaneous."

---

## Assumed prerequisites (what wasn't said)

### P-01 -- Knowledge of Azure OpenAI and Azure AI Foundry

**Files:** All six documents reference these services without intro.

**Assumption:** Readers know what Azure OpenAI is, what Azure AI Foundry is, and the difference between them.

**Why it matters:** A junior C# developer might not have Azure experience. They might not know that Azure OpenAI and Azure AI Foundry are different services, or why a model might live in one vs. the other.

**Fix sketch:** Add a one-sentence forward-reference in the README.md Purpose section: "Azure OpenAI and Azure AI Foundry are Microsoft cloud services that host LLMs."

---

### P-02 -- Understanding of what "deployment name" means in Azure context

**File:** `docs/model-cards/README.md`, line 20.

**Assumption:** "model: <deployment name -- matches registry.json 'name' field>"

**Why it matters:** A junior might not know that in Azure, you deploy a model (e.g., `gpt-4o-mini`) to a named *deployment* (e.g., `my-org-gpt-4o-mini`). The difference is crucial: the model is the software, the deployment is a running instance.

**Fix sketch:** One-sentence definition in the front matter block description: "deployment name is the name of the model instance in your Azure subscription; it is set by you when you deploy the model to Azure."

---

### P-03 -- Understanding of JSON schema validation and why `rc=99` is strict

**File:** `docs/adr/ADR-012-model-registry-seam.md`, line 47.

**Assumption:** Readers know what schema validation is and why a strict exit code on error is better than graceful degradation.

**Why it matters:** A junior might ask, "Why not just warn and skip unknown tags?" The ADR explains the rationale (locked vocabulary for E03), but doesn't explain *why strict is better than permissive* from a design perspective.

**Fix sketch:** Add a note in ADR-012 Consequences: "Strict schema validation prevents silent failures: a mistyped tag like 'tool-call' (missing the 's') is caught at load time, not discovered later when a routing rule silently ignores the tag."

---

### P-04 -- Understanding of what a "seam" pattern is and why it matters

**File:** `docs/adr/ADR-012-model-registry-seam.md`, throughout.

**Assumption:** Readers know what a seam is and why it's important for multi-act episodes.

**Why it matters:** The entire episode is framed around "planting the seam." A junior might not understand why the registry *must* exist before E05 (smart defaults), or what the relationship is between the registry and future episodes.

**Fix sketch:** Add a one-sentence forward-reference at the top of ADR-012 Context: "A 'seam' is a clear boundary in the code where future episodes can plug in new functionality without reopening the registry implementation."

---

### P-05 -- Understanding of what "embedded resource" means in C# / .NET context

**File:** `docs/adr/ADR-012-model-registry-seam.md`, line 35.

**Assumption:** Readers know how embedded resources work in .NET and why they matter for AOT (offline startup, no disk I/O).

**Why it matters:** This is a key architectural choice. A junior might not know that embedding the JSON in the binary is different from shipping it as a separate file, or why that distinction matters for offline first use.

**Fix sketch:** Add one sentence in ADR-012 Decision: "An embedded resource is compiled into the binary itself; this ensures zero-config startup and offline-first operation (the binary can load the registry without reading a file from disk)."

---

### P-06 -- Understanding of what the `AppJsonContext` is and why it matters for AOT

**File:** `docs/adr/ADR-012-model-registry-seam.md`, line 43.

**Assumption:** Readers know what `AppJsonContext` is and why it's required for AOT serialization.

**Why it matters:** The ADR says "All fields are AOT-serializable via `AppJsonContext`" but doesn't explain *why* this matters. A junior might not know that the AOT linker needs explicit type registration.

**Fix sketch:** One sentence in ADR-012 Neutral section: "`AppJsonContext` is a C# source-generated class that tells the AOT linker which types can be serialized to JSON; without registering `ModelRegistryEntry`, the linker might strip the type from the binary."

---

## Ordering / structure suggestions

1. **README.md "Adding a new card" section** (lines 56-72): The six steps are clear, but step 4 is a landmine. Adding a `cardPath` entry to `registry.json` is mentioned *only* in step 4, not in the preceding steps. A junior might write the card, try to commit, and hit a validation error they don't understand. **Suggestion:** Reorder steps so that "Check the allowed capability tags" comes first (before writing the card), and add a note: "If you don't update `registry.json` with the card's `cardPath`, the registry loader will skip this model."

2. **README.md front matter block** (lines 18-26): The `model` field is explained as "deployment name -- matches registry.json 'name' field." This assumes readers know what `registry.json` is. **Suggestion:** Add a forward-reference: "The `model` field must match the `name` field in `registry.json`, which is the name used in `AZUREOPENAIMODEL`."

3. **ADR-012 Status section** (line 5): The decision is accepted as of a date, but readers don't know *who* decided or *when it was approved*. The brief says "Greenlit 2026-05-13 -- Larry David," but ADR-012 Status just says "Accepted." **Suggestion:** Duplicate the greenlight date and authority from the brief into ADR-012 Status for self-contained reading.

4. **Episode brief "Read FIRST" section** (line 65): Points readers to six documents. But a junior might not know *where* these files are or whether they should read them all. **Suggestion:** Add a one-sentence guidance: "Start with this brief (you're reading it); then read docs/adr/ADR-012 (the design decision); then read docs/model-cards/README.md (the format spec); then look at the three example cards."

5. **ADR-012 Deferred section** (line 123): The table lists deferred items and which episode will handle them. But there's no guidance on *what to do if someone asks for one of these features in E01*. **Suggestion:** Add a note: "If you encounter a request for one of these items while implementing E01, refer the requester to the episode brief's Scope section and note which later episode will handle it."

---

## Silent footguns

1. **Capability-tag governance is not written down:** README.md says to use "values from the closed set defined in `ModelCapability.AllowedTags`," but a junior reader doesn't know what happens if they add a new capability (e.g., `audio_in`) without amending `ModelCapability.AllowedTags`. The registry loader will exit with `rc=99`, but the README doesn't warn about this. **Footgun:** A junior adds a new card with a capability they invent (e.g., `audio_out`), commits it, and the registry loader fails. **Fix:** Add a note in README.md "Adding a new card" section: "If the registry loader exits with `rc=99` on the new card, you likely used an unlisted capability tag. Check `ModelCapability.AllowedTags` in the codebase for the allowed set."

2. **Card path must be updated in `registry.json` or the card is ignored:** Step 4 of README.md "Adding a new card" mentions `registry.json`, but doesn't emphasize that if you forget this step, the card is valid Markdown but the registry won't load it. **Footgun:** A junior writes a great card, commits it, but the card is never used because `registry.json` was never updated. **Fix:** Add a bold warning in README.md step 4: "**Do not forget this step.** If you don't add a `cardPath` entry to `registry.json`, the model card will not be loaded by the registry, and your model will not appear in `--doctor` output."

3. **Front matter is YAML, not Markdown:** A junior might not know that the YAML block *must* follow the exact format. For example, if they forget the quotes around the ISO date (`"2024-07-18"` vs. `2024-07-18`), the YAML parser might accept the unquoted version, but downstream tools might fail. **Footgun:** Unquoted date in YAML looks okay but breaks downstream consumers. **Fix:** Add a note in README.md after the front matter example: "All YAML fields must be exactly as shown. The `version_noted` field must be a quoted ISO date string; unquoted dates will parse differently than expected."

4. **`capabilities` must be a YAML array, not a string:** A junior might write `capabilities: "tool_calls, json_mode"` (a string) instead of `capabilities: [tool_calls, json_mode]` (an array). The YAML parser will accept it, but the registry loader might fail. **Footgun:** Subtle YAML syntax error that looks correct but breaks the loader. **Fix:** Add a syntax note in README.md front matter section: "The `capabilities` field must be a YAML inline sequence (array) in square brackets, not a comma-separated string."

5. **Prose sections must appear in a specific order:** README.md line 36 says "The following H2 sections must appear in this order: Strengths, Weaknesses, Default use case, Known limitations." A junior might reorder them and the README doesn't say *why* the order matters. **Footgun:** A junior rearranges sections for logical flow and the registry loader or validation tool silently skips the card. **Fix:** Add a note in README.md: "The H2 section order must be exact (Strengths, Weaknesses, Default use case, Known limitations). Parsers depend on this ordering."

6. **The local provider env-var name is not final:** README.md says "llama.cpp-served model running on the user's own hardware via `LLAMACPP_ENDPOINT`." But `local-llama.md` line 51 says "The local provider env-var `LLAMACPP_ENDPOINT` is a placeholder name; the final name will be confirmed when the local-provider adapter lands." A junior might hardcode this name and it could change. **Footgun:** A junior adds a note to their docs about `LLAMACPP_ENDPOINT`, only to have it renamed. **Fix:** Add a note in the local-llama card itself (or in README.md) flagging this as provisional, or add a TODO comment in the registry.json entry for `llama-local`.

7. **Model deployment names vary by Azure region and subscription:** The episode brief mentions this (line 45), but the model cards don't emphasize it. A junior might assume the deployment name `gpt-5.4-nano` is the same in every Azure region. **Footgun:** A junior's `AZUREOPENAIMODEL` config has `gpt-5.4-nano`, but their Azure subscription calls the deployment `my-gpt-5-4-nano`, and the model isn't found. **Fix:** Add a bold note in `azure-gpt-5.4-nano.md` Known limitations section (which already mentions this): "The deployment name `gpt-5.4-nano` is a placeholder. Confirm the exact deployment name in your Azure portal. Deployment names vary by subscription and region."

---

## What worked well

1. **Model cards are honest about weaknesses:** The cards don't pretend all models are equally good. `azure-gpt-4o-mini.md` Weaknesses section says "Complex multi-step reasoning chains degrade before GPT-4o does" and "Code generation... is adequate for small functions but unreliable for cross-file changes." This is the kind of real-world context a junior needs to make good choices. Clear win for `Elaine`.

2. **The front matter YAML schema is self-documenting:** The example in README.md line 18-26 shows exactly what fields are required and what they look like. A junior can copy-paste it and fill in the blanks. No guessing about YAML syntax or missing fields.

3. **The episode brief Acceptance criteria section is crystal-clear:** Lines 255-276 specify exactly what has to pass before the episode is done. A junior can read this and know whether they've succeeded without hunting for implicit requirements.

4. **The ADR explains *why* each design decision was made:** ADR-012 Alternatives section explains why the three alternatives (hardcoded array, external JSON only, free-string tags) were rejected. A junior reader gets not just the decision but the reasoning.

5. **Three diverse example cards (Azure, Azure, Local):** Having three seed cards that cover Azure, Foundry (implicit in future scope), and local providers shows the range of what a card can describe. A junior can see a pattern and extend it.

---

## My summary as a junior reader

Could I add a fourth model card from just these docs? **Mostly yes, but with four stubborn moments.**

**First attempt would work 70% of the time.** I'd follow the README steps, copy the front matter from an existing card, fill in the fields, write the four prose sections, and add the entry to `registry.json`. Markdownlint and ASCII validation are clear commands to run.

**The 30% failure modes are silent footguns:** (1) I might use an unlisted capability tag and not realize `rc=99` means "schema error" until I read the ADR. (2) I'd commit a beautiful card and forget to add it to `registry.json`, not realizing the card is now orphaned. (3) I might not know that the YAML `version_noted` field *must* be quoted, or that `capabilities` must be an array. (4) I wouldn't know to check whether the model is available in my Azure region before writing the card's constraints.

**The biggest blocker is tribal knowledge:** The term "seam," the concept of "embedded resources," why `rc=99` is strict, what "instruction-tuned" means, and why the registry must lock the capability vocabulary before E03 -- all of this is explained *somewhere* in the six documents, but none of it is foregrounded in the README where a new contributor starts.

**What I'd do on a second reading:** I'd read ADR-012 first (not last), then the README, then the example cards. The ADR explains the vocabulary and the "why." Without it, the README's "Adding a new card" section feels like a checklist with hidden requirements.

**Elaine could ship this as-is,** because the blocking issues are all explainable in 2-3 sentences. But a contributor who reads only the README (not the ADR or brief) will have 3-4 false starts before they succeed. The fix is to copy the key definitions and constraints into the README as forward-references or a glossary.

---

## Finding count summary

| Category | Count | Severity |
|----------|-------|----------|
| Jargon terms without definition | 12 | Medium |
| Assumed prerequisites | 6 | Medium |
| Ordering / structure issues | 5 | Low |
| Silent footguns | 7 | High |
| **Total** | **30** | **--** |

---

## Top 3 actionable items for Elaine's triage

1. **Add a one-sentence definition of "seam" (design pattern) to ADR-012 Decision section.** It's the title and framing device for the whole ADR, but readers don't know what it means. 30 seconds to fix, eliminates mental friction.

2. **Add a bold warning to README.md step 4: "Do not forget to add the `cardPath` entry to `registry.json`, or the model card will not be loaded."** This is the most likely silent failure a junior will encounter. Make it impossible to miss.

3. **Add a "Glossary" or "Key terms" section to README.md with brief one-sentence definitions for: embedded resource, capability tags, GGUF, quantisation, chat template, Espanso, streaming.** Or create `docs/model-cards/GLOSSARY.md` and link to it from the README. This centralizes the tribal knowledge.
