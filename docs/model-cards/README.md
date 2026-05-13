# Model Cards

## Purpose

Model cards are the human-readable companion to `azureopenai-cli/Registry/registry.json`.
Where the registry supplies machine-readable metadata (capability tags, context window,
cost tier) for routing and validation, a model card supplies the qualitative context a
developer needs to choose the right model for a given task: what it excels at, where it
falls short, and what hard stops to anticipate before sending a prompt.

## Format

Each card is a Markdown file with a YAML front matter block followed by four required
prose sections.

### YAML front matter

```yaml
---
model: <deployment name -- matches registry.json "name" field>
provider: <"azure" | "foundry" | "local">
version_noted: "<ISO date of the model snapshot this card describes>"
capabilities: [<comma-separated list from ModelCapability.AllowedTags>]
context_window: <integer token count; 0 = unknown>
cost_tier: <"low" | "medium" | "high" | "unknown">
---
```

All six fields are required. `version_noted` must be a quoted ISO date string
(`"YYYY-MM-DD"`). `capabilities` must be a YAML inline sequence containing only
values from the closed set defined in `ModelCapability.AllowedTags`:
`tool_calls`, `vision_in`, `vision_out`, `json_mode`, `streaming`, `system_prompt`.

### Required prose sections

The following H2 sections must appear in this order after the front matter:

- `## Strengths` -- what the model does measurably well; be specific about task
  classes, not generic about quality.
- `## Weaknesses` -- known failure modes and degraded-quality scenarios; honest, not
  diplomatic.
- `## Default use case` -- the prompt class for which this model is the right default
  choice within `az-ai`.
- `## Known limitations` -- hard stops: capabilities absent from the model despite
  being listed by the provider, edge cases at context-window boundaries, streaming
  quirks, and any other constraint a developer would not discover until they hit it.

## Index

| Model | Provider | Cost tier | Card |
|-------|----------|-----------|------|
| `gpt-4o-mini` | azure | low | [azure-gpt-4o-mini.md](azure-gpt-4o-mini.md) |
| `gpt-5.4-nano` | azure | low | [azure-gpt-5.4-nano.md](azure-gpt-5.4-nano.md) |
| `llama-local` | local | unknown | [local-llama.md](local-llama.md) |

## Adding a new card

1. Copy the front matter block from an existing card (e.g., `azure-gpt-4o-mini.md`)
   into a new file named `<provider>-<model-slug>.md` in this directory.
2. Fill in all six YAML front matter fields. Use only allowed capability tags.
3. Write the four required H2 prose sections (`Strengths`, `Weaknesses`,
   `Default use case`, `Known limitations`) beneath the front matter.
4. Add a `cardPath` entry to `azureopenai-cli/Registry/registry.json` pointing to
   the new file (path relative to the repo root, e.g.,
   `"docs/model-cards/azure-gpt-4o-mini.md"`).

   > [!IMPORTANT]
   > **You must also add the new card's `cardPath` entry to
   > `azureopenai-cli/Registry/registry.json`, or the model will silently fail
   > to load.** Forgetting this is the #1 reason a freshly-authored card never
   > shows up under `az-ai --doctor`. The card file alone is not enough -- the
   > registry is what makes the model visible to the CLI.
5. Add a row to the Index table above.
6. Run the ASCII validation grep and markdownlint before committing:

```bash
grep -nP '[\x{2018}\x{2019}\x{201C}\x{201D}\x{2013}\x{2014}]' docs/model-cards/<new-file>.md
NODE_OPTIONS="--max-old-space-size=4096" npx markdownlint-cli2 "docs/model-cards/<new-file>.md"
```

## Glossary

Plain-English definitions of terms used across these cards, the registry, and
[ADR-012](../adr/ADR-012-model-registry-seam.md). One-stop reference so a
first-time contributor does not have to triangulate across files.

- **Embedded resource** -- a file (here, `registry.json`) compiled *into* the
  `az-ai` binary rather than shipped alongside it. The binary reads the JSON
  from its own compiled data, which is what lets a fresh install work offline
  and with zero configuration. See ADR-012 Decision section.
- **Capability tags** -- the closed vocabulary that describes what a model can
  do: `tool_calls`, `vision_in`, `vision_out`, `json_mode`, `streaming`,
  `system_prompt`. Defined in `ModelCapability.AllowedTags`. Any tag outside
  this set causes the registry loader to exit with `rc=99`. See ADR-012
  Decision section.
- **GGUF** -- a single-file model format used by `llama.cpp` to store local
  LLM weights, tokenizer, and metadata together. Relevant only to local-provider
  cards (e.g., `local-llama.md`).
- **Quantisation** -- the precision at which a local model's weights are stored
  (e.g., 4-bit, 5-bit, 8-bit). Lower precision means a smaller file and faster
  inference, at the cost of some output quality. Cards describe the *default
  expectation*; actual behaviour depends on the specific GGUF and quantisation
  the user loads. (British spelling preserved for consistency with existing
  cards.)
- **Chat template** -- the prompt format that tells a local LLM how to delimit
  system, user, and assistant turns (and tool calls, when supported). Loading a
  model without the correct chat template typically produces malformed output;
  on Azure providers the template is handled server-side and is not the
  author's concern.
- **Espanso** -- a cross-platform text expander
  (<https://espanso.org>). Together with AutoHotkey (AHK) on Windows, it is the
  primary headless use case for `az-ai`: a keystroke trigger pipes a snippet
  through the CLI and pastes the model's response into whatever app has focus.
- **Streaming** -- delivering the model's output one token at a time as it is
  generated, instead of waiting for the full response. Listed as a capability
  tag because some models or modes (notably image generation) do not stream;
  cards note streaming behaviour when it materially affects latency.
