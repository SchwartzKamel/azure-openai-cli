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
5. Add a row to the Index table above.
6. Run the ASCII validation grep and markdownlint before committing:

```bash
grep -nP '[\x{2018}\x{2019}\x{201C}\x{201D}\x{2013}\x{2014}]' docs/model-cards/<new-file>.md
NODE_OPTIONS="--max-old-space-size=4096" npx markdownlint-cli2 "docs/model-cards/<new-file>.md"
```
