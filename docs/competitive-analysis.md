# Competitive Analysis -- CLI AI Tooling Landscape (April 2026)

**Authors:** Costanza (product), Sue Ellen Mischke (competitive), Morty Seinfeld (FinOps)
**Date:** 2026-04-20
**Status:** Research deliverable for roadmap input

## 1. Scope

We benchmark `az-ai` (this project) against 12 CLI tools that either serve adjacent use cases (terminal AI chat, text-injection, automation, agentic coding) or represent the 2025-2026 entrants that reshape the category.

Methodology:
- Neutral sources preferred (GitHub READMEs/releases, DeepWiki, independent 2026 comparison posts).
- Vendor pricing pages cross-checked against third-party rate cards.
- We call out archived/abandoned projects explicitly.

## 2. Competitive Matrix

| Tool | Latest (Apr 2026) | Language / Runtime | Install Surface | AOT / Single-binary | Plugins / Tools | Azure OpenAI native? | MCP support | Primary USP | Known Weaknesses |
|------|-------------------|--------------------|-----------------|---------------------|------------------|----------------------|-------------|-------------|------------------|
| **az-ai** (this repo) | 2.0.6 | .NET 10, Native AOT | Single ~13 MiB binary; `make install`, GHCR image | ✅ True AOT, zero runtime | Internal tool registry (`shell`, `file`, `web`, `clipboard`, `delegate`); FR-012 external plugins planned | ✅ First-class (only target) | ❌ Not yet (opportunity) | 10.7 ms p50 cold start, Espanso-grade latency, persona+memory `.squad/` | Single-provider lock, no MCP yet, no public package repos, small GH footprint |
| **shell-gpt / sgpt** [^1] | 1.5.0 (Jan 2026) | Python | `pip install shell-gpt` | ❌ Python cold start (~300-800 ms typical) | Function calling + LiteLLM backends | ⚠️ Via `API_BASE_URL` hack | ❌ No | Shell command suggestion w/ hotkey completion (Bash/Zsh) | Python startup latency [^8], local-model story "not optimized" per own wiki |
| **llm** (Simon Willison) [^2] | 0.30 (Apr 2026) | Python (pluggy) | `pip` / `uv tool install llm` | ❌ No | ✅ Deepest plugin ecosystem (llm-*) incl. Anthropic, Gemini, Ollama, toolboxes | ⚠️ Community plugin only | ⚠️ Partial via plugins (v0.30 roadmap explores server-side tools) | Richest plugin/tool model of any Python CLI; SQLite log of all calls | Cold start ~0.5-1.0 s, not designed for inline text-injection |
| **aichat** (sigoden) [^3] | 0.30.0 | Rust | `cargo install`, homebrew, prebuilt | ✅ Single binary | ✅ `llm-functions` repo; 20+ providers; RAG; sessions | ✅ Supported via OpenAI-compatible base URL | ✅ "AI Tools & MCP" first-class | Most complete OSS: CMD + REPL + HTTP server modes; RAG built-in | Heavier than az-ai (~10+ MB Rust binary), RAG requires local embedding setup |
| **gh copilot cli** [^4] | GA Feb 2026 | Node/TS (prebuilt) | `gh extension install`; Homebrew/winget/npm | ❌ Node runtime wrapper | Built-in agents (Explore/Task/Review), custom MCP servers, hooks | ❌ OpenAI/Anthropic/Gemini via GitHub | ✅ Full MCP + hooks | Plan/Autopilot/Fleet modes, repo memory, enterprise policy | Paid ($10-$39/user/mo), GitHub-account gated, no Azure-native, telemetry mandatory |
| **mods** (Charmbracelet) [^5] | **ARCHIVED Mar 9 2026** | Go | Homebrew/Go | ✅ Single binary | ✅ (MCP) | ⚠️ OpenAI-compatible endpoint | ✅ (final releases) | RIP -- use `crush` | Abandoned; migrate path to `crush` incomplete for some users |
| **crush** (Charmbracelet) [^5] | Active (successor to mods) | Go | Homebrew/Go | ✅ Single binary | ✅ MCP | ⚠️ Endpoint-based | ✅ Yes | Charm's polished TUI/TTY-first agent | Still maturing; feature parity w/ mods not 100% |
| **fabric** (Daniel Miessler) [^6] | Active (Go rewrite) | Go | Prebuilt binaries, brew | ✅ Single binary | ✅ Pattern library (hundreds of prompts) | ⚠️ `--model` via OpenAI-compatible | ✅ Pattern/session MCP | Huge curated *pattern library* + YAML config | Pattern-centric (not text-injection); plugin system less mature than llm |
| **chatblade** (npiv) [^7] | 0.7.0 -- **ARCHIVED** | Python | `pip`, Arch | ❌ No | ❌ No | ❌ No | ❌ No | Early "swiss army knife"; historical footprint | **Not maintained**; upstream recommends llm/fabric |
| **llama.cpp + chat clients** | Active | C++ | Source build or `brew install llama.cpp` | ✅ Yes | ⚠️ Tool calling via model templates | N/A (local) | ⚠️ Emerging | Best local-first perf; GGUF ecosystem | Model ops complexity; not a chat UX by itself |
| **ollama** [^9] | Active; Agent Mode via `ollmcp`/bridges | Go | One-line installer | ✅ Single binary | ✅ Model tool-calling + MCP bridges (`mcp-client-for-ollama`) | ❌ (local) | ✅ Via bridge clients | Easiest local LLM runner; privacy-first | Not a chat CLI per se; tool-calling requires bridge |
| **OpenAI Codex CLI** [^10] | Apache 2.0; included w/ ChatGPT Plus $20/mo | Go (+Rust modules) | Single binary | ✅ Yes | ✅ Sandboxed exec (Docker/kernel) | ❌ No | ✅ Yes | Suggest/Auto-Edit/Full-Auto; strong Python/scripting | Closed model family, ChatGPT Plus tie-in for generous quota |
| **Gemini CLI** [^10] | Active (Apache 2.0) | Go (npm distribution) | `npm i -g @google/gemini-cli` | ⚠️ Node wrapper around Go core | ✅ ReAct agent + Google Search tool | ❌ No | ✅ Yes | 1 M token context, free tier 1,000 req/day | npm install surface, Google account tie-in |
| **Claude Code** [^10] | $20 Pro / $100 Max | Proprietary (Rust/TS) | Prebuilt | ✅ Yes | ✅ Agent Teams, MCP | ❌ (Anthropic direct or Bedrock) | ✅ Yes | SWE-bench ~81%, multi-file refactors | Proprietary, no free tier |
| **Azure AI CLI** (`az cognitiveservices`) [^11] | Built into Azure CLI | Python (azure-cli) | `az` install | ❌ No | ❌ chat -- **management only** | ✅ (management plane) | ❌ No | First-party Microsoft tool | Provisioning tool; **not a chat client**. Gap az-ai fills. |

**Key observation:** Among tools that deliver Azure-OpenAI-native *chat/agent* UX, az-ai is the only one with true AOT, sub-15 ms startup, and a text-injection pipeline. Microsoft's own `az cognitiveservices` is purely control-plane.

## 3. Research Question Answers

### Q1 -- Cold start winner
**az-ai: 10.73 ms p50** (measured, linux-x64, `--help`, N=50, v2.0.6 on laptop reference rig — see [`docs/perf/v2.0.5-baseline.md`](./perf/v2.0.5-baseline.md)). Nearest competitors: Rust single-binaries like `aichat` and Go binaries (`mods`/`crush`/`fabric`) land in the 30-150 ms range; Python tools (sgpt, llm, chatblade) are 300-1000 ms due to interpreter startup [^8]. Node tools (Gemini CLI, gh copilot) are 200-500 ms.

### Q2 -- AOT / single-binary / zero-Python
az-ai, aichat, mods/crush, fabric, ollama, llama.cpp, Codex CLI, Claude Code. **Not** AOT: sgpt, llm, chatblade, gh copilot cli (Node), Azure AI CLI (Python).

### Q3 -- Azure OpenAI support
**First-class, only target:** az-ai. **Via OpenAI-compatible base URL:** aichat, mods/crush, fabric, sgpt, llm (plugin). **None:** gh copilot cli, Codex CLI, Gemini CLI, Claude Code (use their own providers), ollama (local), chatblade.

### Q4 -- Espanso / text-injection
**First-class (`--raw` mode, documented integration):** az-ai [^12]. Others can be shoehorned via stdout parsing but have startup cost and spinner/ANSI output that needs stripping. **This is our sharpest moat.**

### Q5 -- Tool-calling / agent quality
Tiered:
- **Tier 1 (mature, agent teams, sandbox):** Claude Code, Codex CLI, gh copilot cli.
- **Tier 2 (solid single-agent + MCP):** aichat, crush, fabric, ollama+MCP, gemini-cli.
- **Tier 3 (agent loop, smaller toolset):** **az-ai** (`--agent`, `--ralph` autonomous loop, `--persona`), llm (via toolbox plugins), sgpt.
- **Tier 4:** chatblade (none), Azure AI CLI (none).

### Q6 -- Persona / persistent memory
**Built-in persistent, per-persona memory:** az-ai (`.squad/`). **Via plugins or sessions:** llm (logs), aichat (roles+sessions), fabric (sessions). **Repo-level memory:** gh copilot, Claude Code.
**az-ai's Squad pattern is genuinely differentiated** -- named teammates with scoped tools/system prompts and disk-persistent memory is not standard elsewhere.

### Q7 -- MCP state of the art (2026)
MCP is now the *de facto* standard. All major enterprise CLIs shipped it in 2025-26: Claude Code, Codex CLI, gh copilot, Gemini CLI, crush, aichat, fabric, ollama-via-bridge. **az-ai not supporting MCP is now a competitive liability** -- see FR-013.

### Q8 -- User complaints (per-tool)
- **sgpt / llm / chatblade:** Python cold start, dependency resolution delays [^8].
- **mods:** officially archived; community frustration with migration to crush.
- **gh copilot cli:** premium-request quota confusion [^4]; mandatory telemetry; requires paid Copilot seat.
- **Codex / Claude Code:** context window ceiling; "Full Auto" safety concerns; Claude has no free tier.
- **Gemini CLI:** "YOLO mode" less sandboxed than Codex; npm install footprint.
- **fabric:** pattern discovery (too many patterns, no ranking).
- **aichat:** config complexity, RAG setup friction.

### Q9 -- Pricing model
All CLI tools themselves are **free/OSS** *except*:
- **GitHub Copilot CLI** -- requires Copilot seat ($10-$39/user/mo) [^4].
- **Claude Code** -- Pro $20/mo, Max $100+/mo (no free tier) [^10].
- **Codex CLI** -- OSS CLI but most practical usage is billed through ChatGPT Plus ($20/mo) quota.

**Verdict for Morty:** "Free CLI + user pays model" is the dominant model. Charging for the CLI itself would put us on an island with the two premium entrants -- and they bundle a first-party model. We have no model to bundle, so **free is the right play**. Revenue opportunity lies *elsewhere* (managed deployment templates, enterprise support, regulated-industry consulting).

### Q10 -- FedRAMP / SOC2 / enterprise
- **Azure OpenAI in Azure Government**: FedRAMP High + DoD IL5 (Aug 2024 onward), SOC2 inherited [^13] [^14].
- **OpenAI (direct), Anthropic, Google Vertex**: various partner-cloud FedRAMP paths, but Azure OpenAI has the earliest and broadest US-Gov authorization.
- **CLI tools targeting these**: az-ai is the only single-binary chat CLI that can target Azure Gov endpoints *today* with zero runtime deps (critical for locked-down workstations where installing Python/Node is forbidden).
- **Sue Ellen's wedge:** az-ai → `AzureUSGovernment` cloud → FedRAMP High model = a defensible regulated-industry niche no competitor serves cleanly.

## 4. Morty's FinOps Annex -- Cost-per-Task

**Scenario:** 10,000 requests/month, 3,000 input tokens + 500 output tokens per request = 30 M input, 5 M output per month.

Pricing (April 2026, public rate cards) [^15] [^16]:

| Model (competitor default) | Input $/M | Output $/M | Monthly Input | Monthly Output | **Total / mo** | Per-task |
|---|---:|---:|---:|---:|---:|---:|
| Gemini 2.5 Pro (gemini-cli) | $1.25 | $10.00 | $37.50 | $50.00 | **$87.50** | $0.00875 |
| Azure OpenAI GPT-5 (az-ai default-capable) | $1.25 | $10.00 | $37.50 | $50.00 | **$87.50** | $0.00875 |
| OpenAI GPT-5 (Codex CLI) | $1.25 | $10.00 | $37.50 | $50.00 | **$87.50** | $0.00875 |
| Azure OpenAI GPT-4o (az-ai typical) | $2.50 | $10.00 | $75.00 | $50.00 | **$125.00** | $0.01250 |
| Claude 3.5 Sonnet (Claude Code Pro) | $3.00 | $15.00 | $90.00 | $75.00 | **$165.00** | $0.01650 |
| Claude Opus 4.6 (Claude Code Max) | $5.00 | $25.00 | $150.00 | $125.00 | **$275.00** | $0.02750 |

Plus fixed subscription costs (CLI-licence):

| Tool | Monthly seat | Effective add-on | 10k-task all-in |
|---|---:|---:|---:|
| az-ai | $0 | $0 | **$87.50** (GPT-5) |
| gh copilot cli Pro | $10 | $10 | ~$97.50 (+ premium-request caps) |
| Claude Code Pro | $20 | $20 | ~$185.00 |
| Claude Code Max | $100 | $100 | ~$375.00 |
| Codex CLI (ChatGPT Plus) | $20 | $20 | ~$107.50 (within Plus quota caveats) |

**Winner (cost-per-task):** az-ai on GPT-5 or Gemini-backed tools tie at **$0.00875/task** raw, but az-ai has **no seat surcharge**, making it the cheapest once any team scale is applied. At 10 seats, az-ai saves $1,200-$12,000/yr over Claude Code Pro/Max.

**Loser:** **Claude Code Max** at **$0.02750/task + $100 seat** -- 3.1× az-ai on raw tokens, up to **4.3× all-in** for a single seat. Premium capability, premium price; defensible only for SWE-bench-grade repo surgery.

**Morty's takeaway:** Free-CLI + Azure-PAYG is the strongest cost story in the market. We should *prove* it with a public calculator (see FR-014 opportunity).

## 5. Summary -- One-liners Per Tool

- **az-ai** -- 10.7 ms p50 AOT Azure-native with persona memory; Espanso's native speaking partner.
- **sgpt** -- Python veteran for shell-command suggestions; Python tax on every call.
- **llm** -- Simon Willison's plugin superpower; richest ecosystem, slowest cold start.
- **aichat** -- Rust swiss-army; CMD+REPL+HTTP, RAG, MCP. Closest functional peer.
- **gh copilot cli** -- GA Feb 2026; polished Plan/Autopilot, paid, no Azure-native.
- **mods** -- RIP March 2026; migrate to crush.
- **crush** -- Charm's new flagship TUI agent, MCP-first.
- **fabric** -- Pattern library king; Go rewrite; session + MCP.
- **chatblade** -- Archived; historical interest only.
- **llama.cpp** -- Local-first inference engine; not a chat UX.
- **ollama** -- Easiest local LLM runner; agent mode via bridges.
- **Codex CLI / Gemini CLI / Claude Code** -- Vendor-flagship coding agents; each tied to its own cloud.
- **Azure AI CLI** -- Microsoft's management-plane tool; not a chat client.

---

## Footnotes (Sources)

[^1]: TheR1D/shell_gpt releases -- https://github.com/TheR1D/shell_gpt/releases ; PyPI https://pypi.org/project/shell-gpt/
[^2]: simonw/llm releases -- https://github.com/simonw/llm/releases ; https://simonwillison.net/2025/May/27/llm-tools/ ; DeepWiki plugin ecosystem https://deepwiki.com/simonw/llm/4.1-plugin-ecosystem
[^3]: sigoden/aichat README -- https://github.com/sigoden/aichat/blob/main/README.md ; docs.rs/crate/aichat
[^4]: GitHub Blog -- "GitHub Copilot CLI is now generally available" (2026-02-25) https://github.blog/changelog/2026-02-25-github-copilot-cli-is-now-generally-available/ ; pricing https://devtoolsreview.com/pricing/copilot-pricing/
[^5]: charmbracelet/mods archived 2026-03-09 -- https://github.com/charmbracelet/mods ; successor https://github.com/charmbracelet/crush ; MCP impl https://deepwiki.com/charmbracelet/mods/5.1-mcp-tool-integration
[^6]: danielmiessler/fabric -- https://pkg.go.dev/github.com/danielmiessler/fabric/cli ; https://deepwiki.com/danielmiessler/fabric/3.4-creating-custom-patterns
[^7]: npiv/chatblade -- archival notice, https://github.com/npiv/chatblade ; PyPI 0.7.0 https://pypi.org/project/chatblade/
[^8]: "AI in the Terminal: My Experience with Shell-GPT" -- https://pinnsg.com/ai-in-the-terminal-my-experience-with-shell-gpt/ ; TheR1D/shell_gpt Ollama wiki "not optimized for local models"
[^9]: mcp-client-for-ollama (ollmcp) -- https://github.com/jonigl/mcp-client-for-ollama ; https://www.ollama.com/blog/connect-local-llm-mcp-server
[^10]: 2026 terminal-agent comparisons -- https://gocodelab.com/en/blog/en-codex-cli-vs-claude-code-vs-gemini-cli-terminal-agent-comparison-2026 ; https://tokencalculator.com/blog/best-ai-ide-cli-tools-april-2026-claude-code-wins ; https://dev.to/rahulxsingh/claude-code-vs-codex-cli-vs-gemini-cli-which-ai-terminal-agent-wins-in-2026-55f5
[^11]: az cognitiveservices reference -- https://learn.microsoft.com/en-us/cli/azure/cognitiveservices?view=azure-cli-latest
[^12]: az-ai Espanso/AHK integration guide -- `docs/espanso-ahk-integration.md` in this repo.
[^13]: Microsoft Azure OpenAI FedRAMP High -- https://fedscoop.com/microsoft-azure-openai-service-fedramp/ ; https://www.fedramp.gov/ai/
[^14]: ChatGPT Enterprise / API FedRAMP reference -- https://help.openai.com/en/articles/20001070-chatgpt-enterprise-and-api-platform-for-fedramp
[^15]: Azure OpenAI 2026 pricing -- https://inference.net/content/azure-openai-pricing-explained/ ; https://devtk.ai/en/blog/openai-api-pricing-guide-2026/
[^16]: Anthropic / Google 2026 pricing -- https://devtk.ai/en/blog/claude-api-pricing-guide-2026/ ; https://intuitionlabs.ai/articles/ai-api-pricing-comparison-grok-gemini-openai-claude
