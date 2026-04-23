# Competitive Analysis -- CLI AI Tooling Landscape (April 2026)

**Authors:** Costanza (product), Sue Ellen Mischke (competitive), Morty Seinfeld (FinOps)
**Date:** 2026-04-20 (last refresh: 2026 sweep, see §2.5)
**Status:** Research deliverable for roadmap input

**Companion doc:** [`docs/competitive-landscape.md`](./competitive-landscape.md)
is the brief, brand-tone version with the opinionated "where we lead /
where we yield" framing and the J. Peterman positioning copy. This file
is the long-form research underneath it -- per-tool feature matrix,
research-question answers, FinOps annex, and footnoted sources. The
two are deliberately not duplicated: if you want one paragraph per
competitor, read the brief; if you want the matrix and the receipts,
stay here.

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
| **llm** (Simon Willison) [^2] | 0.30 (Apr 2026) | Python (pluggy) | `pip` / `uv tool install llm` | ❌ No | ✅ Deepest plugin ecosystem (llm-*) incl. Anthropic, Gemini, Ollama, toolboxes | ⚠️ Community plugin only | ⚠️ Partial via plugins; 2026 API-redesign work explicitly tracking native MCP | Richest plugin/tool model of any Python CLI; SQLite log of all calls | Cold start ~0.5-1.0 s, not designed for inline text-injection |
| **aichat** (sigoden) [^3] | 0.30.0 | Rust | `cargo install`, homebrew, prebuilt | ✅ Single binary | ✅ `llm-functions` repo; 20+ providers; RAG; sessions | ✅ First-class Azure OpenAI provider section (not just base-URL hack) | ✅ "AI Tools & MCP" first-class | Most complete OSS: CMD + REPL + HTTP server modes; RAG built-in | Heavier than az-ai (~10+ MB Rust binary), RAG requires local embedding setup |
| **gh copilot cli** [^4] | GA Feb 2026 | Node/TS (prebuilt) | `gh extension install`; Homebrew/winget/npm | ❌ Node runtime wrapper | Built-in agents (Explore/Task/Review), custom MCP servers, hooks | ❌ OpenAI/Anthropic/Gemini via GitHub | ✅ Full MCP + hooks | Plan/Autopilot/Fleet modes, repo memory, enterprise policy | Paid ($10-$39/user/mo), GitHub-account gated, no Azure-native, telemetry mandatory |
| **mods** (Charmbracelet) [^5] | **ARCHIVED Mar 9 2026** | Go | Homebrew/Go | ✅ Single binary | ✅ (MCP, final v1.8.x) | ⚠️ OpenAI-compatible endpoint | ✅ (final releases) | RIP -- use `crush` | Abandoned; migrate path to `crush` largely complete by Apr 2026 |
| **crush** (Charmbracelet) [^5] | Active flagship | Go | Homebrew, npm, Nix, AUR, prebuilt | ✅ Single binary | ✅ MCP-native (stdio/HTTP/SSE), LSP-aware | ✅ First-class Azure OpenAI provider section | ✅ Yes | Charm's polished agentic TUI; multi-provider (10+) | FSL-1.1-MIT (source-available, MIT after 2 yr) -- not a pure-OSS license; some shops will block |
| **fabric** (Daniel Miessler) [^6] | Active (Go rewrite) | Go | Prebuilt binaries, brew | ✅ Single binary | ✅ Pattern library (hundreds of prompts) | ⚠️ `--model` via OpenAI-compatible | ✅ Pattern/session MCP | Huge curated *pattern library* + YAML config | Pattern-centric (not text-injection); plugin system less mature than llm |
| **chatblade** (npiv) [^7] | 0.7.0 -- **ARCHIVED** | Python | `pip`, Arch | ❌ No | ❌ No | ❌ No | ❌ No | Early "swiss army knife"; historical footprint | **Not maintained**; upstream recommends llm/fabric |
| **llama.cpp + chat clients** | Active | C++ | Source build or `brew install llama.cpp` | ✅ Yes | ⚠️ Tool calling via model templates | N/A (local) | ⚠️ Emerging | Best local-first perf; GGUF ecosystem | Model ops complexity; not a chat UX by itself |
| **ollama** [^9] | Active; Agent Mode via `ollmcp`/bridges | Go | One-line installer | ✅ Single binary | ✅ Model tool-calling + MCP bridges (`mcp-client-for-ollama`) | ❌ (local) | ✅ Via bridge clients | Easiest local LLM runner; privacy-first | Not a chat CLI per se; tool-calling requires bridge |
| **OpenAI Codex CLI** [^10] | Apache 2.0; included w/ ChatGPT Plus $20/mo | Go (+Rust modules) | Single binary | ✅ Yes | ✅ Sandboxed exec (Docker/kernel) | ❌ No | ✅ Yes | Suggest/Auto-Edit/Full-Auto; strong Python/scripting | Closed model family, ChatGPT Plus tie-in for generous quota |
| **Gemini CLI** [^10] | Active (Apache 2.0) | Go (npm distribution) | `npm i -g @google/gemini-cli` | ⚠️ Node wrapper around Go core | ✅ ReAct agent + Google Search tool | ❌ No | ✅ Yes | 1 M token context, free tier 1,000 req/day | npm install surface, Google account tie-in |
| **Claude Code** [^10] | $20 Pro / $100 Max | Proprietary (Rust/TS) | Prebuilt | ✅ Yes | ✅ Agent Teams, MCP | ❌ (Anthropic direct or Bedrock) | ✅ Yes | SWE-bench ~81%, multi-file refactors | Proprietary, no free tier |
| **Azure AI CLI** (`az cognitiveservices`) [^11] | Built into Azure CLI | Python (azure-cli) | `az` install | ❌ No | ❌ chat -- **management only** | ✅ (management plane) | ❌ No | First-party Microsoft tool | Provisioning tool; **not a chat client**. Gap az-ai fills. |

**Key observation:** Among tools that deliver Azure-OpenAI-native *chat/agent* UX, az-ai is the only one with true AOT, sub-15 ms startup, and a text-injection pipeline. Microsoft's own `az cognitiveservices` is purely control-plane.

## 2.5 -- 2026 Update Notes

The 2026 sweep validates every row in §2 against current upstream
state and adds the three axes the category has converged on
(provider coverage, MCP support, single-binary distribution). Per-tool
deltas since the last review:

### Existing tools -- status changes

- **`mods` -- archived 2026-03-09.** Charm sunset the repo; final
  releases shipped MCP. Migration target is `crush`. Kept in the
  matrix for historical context only.
- **`crush` -- promoted from "new entrant" to Tier-1 substitute.**
  AGPL-style FSL-1.1-MIT (source-available now, MIT after two years).
  10+ first-class providers including Azure OpenAI as a configured
  section, MCP-native client (stdio / HTTP / SSE), LSP integration,
  and distribution via Homebrew, npm, Nix, AUR, and prebuilt binaries.
  Closest functional peer to `aichat` in the OSS field.
- **`chatblade` -- archive notice landed upstream.** Repository now
  carries an explicit "use `llm` or `fabric` instead" notice. No
  features in 2026; remains in the matrix for the same reason as `mods`.
- **`llm` -- MCP shifted from "no" to "partial".** The 2026 API
  redesign work (Willison's `research-llm-apis 2026-04-04` post)
  explicitly tracks native MCP-client behaviour; Anthropic donated MCP
  to the Linux Foundation Agentic AI Foundation in Dec 2025, which
  unblocked neutral-governance integration work.
- **`aichat` -- Azure OpenAI is now first-class, not base-URL.** The
  provider catalog ships with an Azure OpenAI section. MCP support
  remains first-class.
- **`shell-gpt` -- maintained but slowing.** v1.5.0 in Jan 2026; no
  native MCP; MCP integration is the open question for any 2026
  Python CLI that wants to stay relevant.
- **`gh copilot cli` -- GA Feb 2026 confirmed.** Built-in GitHub MCP
  server, accepts custom MCP servers and hooks, config files at
  `~/.copilot/mcp-config.json` (user) and `.copilot/mcp-config.json`
  (project). Distribution: npm, Homebrew, WinGet, `gh extension install`.
- **`Codex CLI` -- now MCP-central, not bolted on.** OpenAI's Rust
  CLI ships with `codex mcp add/list/login`, parallel MCP tool calls,
  and shared MCP config across CLI / VS Code / macOS app. Apache 2.0.
- **`Gemini CLI` -- weekly stable cadence.** Apache 2.0; npm primary
  with Homebrew, MacPorts, Conda. Official MCP servers shipped
  (chrome-devtools-mcp, Data Commons MCP).
- **`Claude Code` -- distribution channel matrix expanded.** Native
  installer (recommended), Homebrew, WinGet, npm (deprecated). MCP
  v2.1.76 introduces lazy-loaded MCP elicitation. Bedrock and Vertex
  paths officially documented.

### New entrants worth flagging

- **`opencode` (sst / community).** MIT, TypeScript on Bun (single
  bundled binary), 75+ providers via routing, MCP-compatible, weekly
  releases, ~11.5k GitHub stars. Caveat: Anthropic blocks unofficial
  CLI access to Claude models; users need an official Claude key or
  must route around. Not a substitute for us (different seam) but
  notable as a "bring your own everything" hub. Adjacent to `aichat`
  / `crush` in design intent.
- **`cursor` Cursor CLI.** Proprietary, bundled-runtime binary
  distributed via Homebrew cask and AUR. Tied to a Cursor account;
  Plan / Ask modes; MCP integration. Not a substitute -- it's a
  terminal companion to the Cursor IDE -- but lands in the same
  developer-mindshare bucket as `claude` and `codex`.
- **`continue.dev` `cn` CLI.** Apache 2.0, TypeScript on Node. The
  CLI matured significantly in 2026: TUI mode (`cn`), headless mode
  (`cn -p ...`) for CI/CD, persistent sessions, fine-grained
  permissions (`--allow` / `--ask` / `--exclude`), MCP-native agent
  loop, BYOM provider model. Worth tracking as the
  "rules-as-markdown enforcement on every PR" pattern is novel.

### Net effect on positioning

The category-default 2026 axes are now **single-binary distribution +
multi-provider coverage + MCP client**. Of those three, `az-ai`
ships #1, has #2 on the S03 blueprint, and has #3 on the S05
blueprint. We do not race the field on raw axis count -- our seam
(text-injection / Espanso / agentic delegation) is orthogonal -- but
shipping S03 + S05 is what closes the only two columns where we
currently look behind to a casual reader of the comparison matrix.

The opinionated cut of all of the above lives in
[`docs/competitive-landscape.md`](./competitive-landscape.md) and is
the canonical place to read the "where we lead / where we yield"
framing. This file is the receipts.

## 3. Research Question Answers

### Q1 -- Cold start winner

**az-ai: 10.73 ms p50** (measured, linux-x64, `--help`, N=50, v2.0.6 on laptop reference rig -- see [`docs/perf/v2.0.5-baseline.md`](./perf/v2.0.5-baseline.md)). Nearest competitors: Rust single-binaries like `aichat` and Go binaries (`mods`/`crush`/`fabric`) land in the 30-150 ms range; Python tools (sgpt, llm, chatblade) are 300-1000 ms due to interpreter startup [^8]. Node tools (Gemini CLI, gh copilot) are 200-500 ms.

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
- **sgpt** -- Python veteran for shell-command suggestions; Python tax on every call; no MCP.
- **llm** -- Simon Willison's plugin superpower; richest ecosystem, slowest cold start; MCP shifting from "no" to native in the 2026 API redesign.
- **aichat** -- Rust swiss-army; CMD+REPL+HTTP, RAG, MCP, first-class Azure OpenAI. Closest functional peer.
- **gh copilot cli** -- GA Feb 2026; MCP-first with built-in GitHub server; paid; no Azure-native.
- **mods** -- RIP March 2026; migrate to crush.
- **crush** -- Charm's new flagship MCP-native multi-provider agent; FSL-1.1-MIT (source-available now, MIT in 2 yr); Azure OpenAI first-class.
- **fabric** -- Pattern library king; Go rewrite; session + MCP.
- **chatblade** -- Archived (notice landed upstream); historical interest only.
- **llama.cpp** -- Local-first inference engine; not a chat UX.
- **ollama** -- Easiest local LLM runner; agent mode via bridges (`ollmcp`).
- **Codex CLI / Gemini CLI / Claude Code** -- Vendor-flagship coding agents; each MCP-native; each tied to its own cloud.
- **opencode** -- BYOM router CLI; 75+ providers, MIT, MCP-compatible; Anthropic blocks unofficial Claude access.
- **cursor CLI** -- IDE-tethered terminal companion; MCP + plan / ask modes; not a substitute for an Azure-native binary.
- **continue.dev `cn`** -- BYOM CLI with headless CI mode and rules-as-markdown PR enforcement; MCP-native.
- **Azure AI CLI** -- Microsoft's management-plane tool; not a chat client.

---

## Footnotes (Sources)

[^1]: TheR1D/shell_gpt releases -- <https://github.com/TheR1D/shell_gpt/releases> ; PyPI <https://pypi.org/project/shell-gpt/>
[^2]: simonw/llm releases -- <https://github.com/simonw/llm/releases> ; <https://simonwillison.net/2025/May/27/llm-tools/> ; DeepWiki plugin ecosystem <https://deepwiki.com/simonw/llm/4.1-plugin-ecosystem>
[^3]: sigoden/aichat README -- <https://github.com/sigoden/aichat/blob/main/README.md> ; docs.rs/crate/aichat
[^4]: GitHub Blog -- "GitHub Copilot CLI is now generally available" (2026-02-25) <https://github.blog/changelog/2026-02-25-github-copilot-cli-is-now-generally-available/> ; pricing <https://devtoolsreview.com/pricing/copilot-pricing/>
[^5]: charmbracelet/mods archived 2026-03-09 -- <https://github.com/charmbracelet/mods> ; successor <https://github.com/charmbracelet/crush> ; MCP impl <https://deepwiki.com/charmbracelet/mods/5.1-mcp-tool-integration>
[^6]: danielmiessler/fabric -- <https://pkg.go.dev/github.com/danielmiessler/fabric/cli> ; <https://deepwiki.com/danielmiessler/fabric/3.4-creating-custom-patterns>
[^7]: npiv/chatblade -- archival notice, <https://github.com/npiv/chatblade> ; PyPI 0.7.0 <https://pypi.org/project/chatblade/>
[^8]: "AI in the Terminal: My Experience with Shell-GPT" -- <https://pinnsg.com/ai-in-the-terminal-my-experience-with-shell-gpt/> ; TheR1D/shell_gpt Ollama wiki "not optimized for local models"
[^9]: mcp-client-for-ollama (ollmcp) -- <https://github.com/jonigl/mcp-client-for-ollama> ; <https://www.ollama.com/blog/connect-local-llm-mcp-server>
[^10]: 2026 terminal-agent comparisons -- <https://gocodelab.com/en/blog/en-codex-cli-vs-claude-code-vs-gemini-cli-terminal-agent-comparison-2026> ; <https://tokencalculator.com/blog/best-ai-ide-cli-tools-april-2026-claude-code-wins> ; <https://dev.to/rahulxsingh/claude-code-vs-codex-cli-vs-gemini-cli-which-ai-terminal-agent-wins-in-2026-55f5>
[^11]: az cognitiveservices reference -- <https://learn.microsoft.com/en-us/cli/azure/cognitiveservices?view=azure-cli-latest>
[^12]: az-ai Espanso/AHK integration guide -- `docs/espanso-ahk-integration.md` in this repo.
[^13]: Microsoft Azure OpenAI FedRAMP High -- <https://fedscoop.com/microsoft-azure-openai-service-fedramp/> ; <https://www.fedramp.gov/ai/>
[^14]: ChatGPT Enterprise / API FedRAMP reference -- <https://help.openai.com/en/articles/20001070-chatgpt-enterprise-and-api-platform-for-fedramp>
[^15]: Azure OpenAI 2026 pricing -- <https://inference.net/content/azure-openai-pricing-explained/> ; <https://devtk.ai/en/blog/openai-api-pricing-guide-2026/>
[^16]: Anthropic / Google 2026 pricing -- <https://devtk.ai/en/blog/claude-api-pricing-guide-2026/> ; <https://intuitionlabs.ai/articles/ai-api-pricing-comparison-grok-gemini-openai-claude>
