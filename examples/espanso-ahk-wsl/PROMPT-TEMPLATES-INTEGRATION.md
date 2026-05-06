# Prompt Templates — Espanso & AutoHotkey Integration

> *The five canonical prompting templates, mapped to real text-expansion hotkeys.*

This directory contains the prompt framework from `docs/prompts/task-templates.md` wired directly into Espanso (YAML) and AutoHotkey v2 (AHK) so you can invoke them as **keyboard shortcuts** in any editor, terminal, or text field.

---

## Quick Start

### Espanso (Windows / WSL)

**Windows (Shaka setup):**

The prompt templates file is already in your Espanso config:

```
%APPDATA%\espanso\match\ai-prompts.yml
```

Restart Espanso to activate:
- Right-click Espanso tray icon → Restart

Or copy manually if needed:

```bash
# From WSL, into your Windows Espanso:
cp examples/espanso-ahk-wsl/espanso/ai-prompts.yml /mnt/c/Users/YOUR-USERNAME/AppData/Roaming/espanso/match/ai-prompts.yml
```

**Linux / macOS (native Espanso):**

```bash
cp examples/espanso-ahk-wsl/espanso/ai-prompts.yml ~/.config/espanso/match/ai-prompts.yml
espanso restart
```

Now in any text field, type one of these triggers:

| Trigger | Template | Invokes |
|---------|----------|---------|
| `:aiquestion` | Knowledge Q&A | Template A: Ask Azure question → get answer + checklist |
| `:aiarch` | Architecture Design | Template B: Design solution → get architecture + cost + milestones |
| `:aicode` | Code Generation | Template C: Describe task → get minimal reproducible example |
| `:aidataworkflow` | Data Workflow | Template D: Describe pipeline → get ETL/MLOps design |
| `:aicost` | Cost & ROI | Template E: Describe solution → get financial model + optimization |
| `:aiprompts` | Reference Card | Show all available templates and triggers |

Each trigger:
1. **Pops a form** for you to enter task-specific parameters (goal, constraints, language, etc.)
2. **Constructs a system prompt** = master prompt + task template
3. **Sends to az-ai** via stdin with `--raw --system` flags
4. **Replaces the trigger** with the structured response

Example: Type `:aiquestion`, fill in "How should I monitor Azure OpenAI latency?", press Enter → get a concise answer with implementation checklist.

### AutoHotkey v2 (Windows)

Install the prompt templates hotkey script:

```batch
copy examples\espanso-ahk-wsl\ahk\az-ai-prompts.ahk "%APPDATA%\Microsoft\Windows\Start Menu\Programs\Startup\az-ai-prompts.ahk"
double-click az-ai-prompts.ahk or restart Windows
```

Now use these hotkeys anywhere:

| Hotkey | Template | Action |
|--------|----------|--------|
| `Ctrl+Shift+Q` | Knowledge Q&A | Input dialog → answer + checklist |
| `Ctrl+Shift+R` | Architecture Design | Input dialog → architecture + cost + milestones |
| `Ctrl+Shift+C` | Code Generation | Input dialog → minimal reproducible example |
| `Ctrl+Shift+D` | Data Workflow | Input dialog → ETL/MLOps design |
| `Ctrl+Shift+L` | Cost & ROI | Input dialog → financial model + optimizations |
| `Ctrl+Shift+T` | Reference Card | Show all templates and hotkeys |

Each hotkey:
1. **Pops an InputBox** for task-specific parameters
2. **Constructs a system prompt** = master prompt + task template
3. **Sends to WSL az-ai** (same as existing AHK hotkeys)
4. **Pastes result** at your cursor

Example: In VS Code, press `Ctrl+Shift+R`, describe your goal → get full architecture design pasted into your document.

---

## System Prompt Structure

All prompts follow this pattern:

```
[Master System Prompt from system-prompt-master.md]

[Task-Specific Template from task-templates.md, Template A–E]
```

The **master prompt** is constant across all five templates:
- Defines tone (clear, precise, actionable)
- Sets safety guardrails (no secrets, no PII)
- Specifies output structure (plan → details → risks → next steps)

The **task template** is specific to each use case:
- Template A: Knowledge Q&A
- Template B: Architecture Design
- Template C: Code Generation
- Template D: Data Workflow / ETL / MLOps
- Template E: Cost & ROI Assessment

See `docs/prompts/system-prompt-master.md` and `docs/prompts/task-templates.md` for full details.

---

## Output Examples

### Template A: Knowledge Q&A

**Input**: "How should I monitor Azure OpenAI latency?"

**Output**:
```
1. Clarifying Question:
   What's your acceptable latency threshold (SLA)?

2. Answer:
   - Enable Application Insights on your OpenAI resource
   - Track "Request Duration" and "Gateway Latency" metrics
   - Set alerts for p95 latency > your threshold
   - Review model concurrency and token throughput

3. Implementation Checklist:
   [ ] Enable Application Insights in Azure portal
   [ ] Create custom alert rule for latency
   [ ] Set up dashboard with key metrics
   [ ] Configure Diagnostic Logs (30-day retention)
   [ ] Review OpenAI token-per-minute (TPM) limits

4. References:
   https://learn.microsoft.com/azure/cognitive-services/openai/how-to/monitoring
```

### Template B: Architecture Design

**Input**: "Design a solution for batch processing 10k documents daily with Azure OpenAI embeddings."

**Output**:
```
Plan:
  Azure Functions + Queue Storage trigger document processing.
  Each function embeds via Azure OpenAI, stores vectors in Cognitive Search.
  Monitoring via Application Insights; cost ~$150–300/month at scale.

Architecture Components:
  • Blob Storage (raw documents): ~$50/month
  • Queue Storage (job queue): ~$5/month
  • Azure Functions (processing): ~$80/month (1M invocations)
  • Azure OpenAI (embeddings): $20–50/month (text-embedding-ada-002)
  • Cognitive Search (vector index): ~$50/month (S1)
  • Application Insights (monitoring): ~$10/month

Data Flow:
  Documents uploaded → Blob trigger → Queue → Function processes
  → Calls Azure OpenAI embedding model → Stores vectors in Cognitive Search
  → Search index ready for queries

Security & Compliance:
  • Use Managed Identity for all service-to-service auth (no API keys in code)
  • Enable VNet integration to restrict traffic to private networks
  • Encrypt data at rest and in transit (TLS 1.2+)
  • Audit all API calls via Diagnostic Logs

Milestones:
  1. Set up storage, queue, search (1 day)
  2. Implement Function trigger and embedding logic (2 days)
  3. Test with 1k documents, measure latency and cost (1 day)
  4. Optimize batch size and concurrency (1 day)
  5. Deploy to production with monitoring (1 day)
```

### Template E: Cost & ROI

**Input**: "Estimate cost for batch processing 10k documents/day with embeddings. Budget is $500/month."

**Output**:
```
Assumptions:
  • 10,000 documents per day (300k/month)
  • Average 500 tokens per document
  • text-embedding-ada-002 model ($0.02 per 1M tokens)
  • Processing time: 5 min per 1k documents
  • Standard Azure pricing (East US region)

Cost Model:
  Azure OpenAI (embeddings): 300k docs × 500 tokens × $0.02/1M = $3/month
  Azure Functions: 300k invocations × $0.0000002 per exec ≈ $0.06/month
  Blob Storage: ~10 GB × $0.018/GB = $0.18/month
  Cognitive Search (S1): $250/month
  Application Insights: $10/month
  ─────────────────────────────────────────────
  Total: ~$263/month (well within $500 budget)

ROI / Business Value:
  Assuming embeddings enable a recommendation engine that increases revenue by $2k/month:
  Break-even: <1 week

Optimization Options:
  1. Reduce Search tier to B (dev/test): Save $200/month, limit to 15k docs/day
  2. Batch embedding calls in larger chunks: Save 10% compute costs
  3. Use reserved capacity for OpenAI: Save 15–20% on embeddings
  4. Implement caching for repeated queries: Save 30% API calls

Decision:
  Standard tier (S1) is appropriate. Consider reserved OpenAI capacity at scale.
```

---

## Design Principles

1. **One command = one structured response**: Each template follows the same output format (Plan → Details → Risks → Next Steps).

2. **System prompt is visible**: In `docs/prompts/`, every prompt template is documented and versioned. No black-box magic.

3. **Keyboard-accessible**: Espanso triggers and AHK hotkeys make these templates as fast as a paste-from-clipboard.

4. **No extra metaprogramming**: Forms and input dialogs capture your task parameters explicitly; system prompt is static.

5. **Reusable across tools**: You can use these prompts in:
   - Espanso (Linux / macOS)
   - AutoHotkey v2 (Windows)
   - Copy-paste into ChatGPT / Copilot chat directly (see `docs/prompts/task-templates.md`)
   - Azure Copilot studio
   - Any LLM chat interface

---

## Troubleshooting

### Espanso trigger not firing

1. Check that `ai-prompts.yml` is in `~/.config/espanso/match/`
2. Restart Espanso: `espanso restart`
3. Ensure `az-ai` is in your `$PATH` (or in WSL `/usr/local/bin/az-ai`)
4. Check logs: `espanso log`

### AHK hotkey not working

1. Verify AutoHotkey **v2** is installed (not v1)
2. Run the script with admin rights (required for WSL.exe calls)
3. Ensure `az-ai-wrap` is in WSL `/usr/local/bin/`
4. Check that AZUREOPENAI* env vars are set in WSL `~/.bashrc`

### Empty or truncated response

1. Check Azure OpenAI quota and rate limits
2. Verify `--max-tokens` in the config is appropriate for your task
3. Increase `--temperature` slightly if responses are too rigid (default 0.3)
4. Check `az-ai` logs: run `az-ai` directly with `--debug` flag

---

## Files

| File | Purpose |
|------|---------|
| `espanso/ai-prompts.yml` | Espanso triggers for all five templates (copy to `~/.config/espanso/match/`) |
| `ahk/az-ai-prompts.ahk` | AutoHotkey v2 hotkeys for all five templates (copy to Startup folder) |
| `docs/prompts/system-prompt-master.md` | Master system prompt (foundation for all templates) |
| `docs/prompts/task-templates.md` | Five canonical task templates with examples and quick-start prompts |

---

## See Also

- **Full prompting guide**: `docs/prompts/README.md`
- **System prompt specification**: `docs/prompts/system-prompt-master.md`
- **Task template reference**: `docs/prompts/task-templates.md`
- **Existing az-ai hotkeys**: `examples/espanso-ahk-wsl/espanso/ai.yml`, `ahk/az-ai.ahk`

---

*Last updated: 2026-05. Maintained by Maestro.*
