# Competitive Landscape Brief

> Sue Ellen Mischke walks the runway in differentiation as a fashion
> statement. Three things we wear better than the field. Three things
> we deliberately do not wear. No apologies, no padding.

**Episode:** S02E19 -- *The Competition*
**Lead:** Sue Ellen Mischke (Competitive Analysis & Market Positioning)
**Guest:** J. Peterman (catalog copy at the bottom of this doc)
**Companion doc:** [`docs/competitive-analysis.md`](./competitive-analysis.md)
is the long-form research and feature matrix. This file is the
opinionated brief: where we lead, where we yield, and the one paragraph
each that a reader can actually quote.

## Sue Ellen's read

The CLI LLM category has matured. Every credible entrant ships a
single binary or a one-line installer, every credible entrant supports
some flavor of tool calling, and -- as of 2026 -- every credible
entrant ships an MCP client. The differentiation game is no longer
"do you have a chat loop" but rather "what is the seam you fit into,
and how cleanly do you fit there." Most of the field is fitting itself
into the general-purpose "terminal copilot" seam: an interactive REPL
or TUI agent with a provider marketplace and an MCP slot, aimed at
developers doing exploratory work. That is a crowded runway, and
Charm's `crush` (the successor to the now-archived `mods`) just
walked it in heels.

`azure-openai-cli` is not on that runway. It is a single-shot,
Azure-OpenAI-native, sub-15-millisecond binary that exists primarily to
be invoked by Espanso, AHK, shell scripts, and agentic delegation --
not by a human typing at a prompt. Once you understand that, the
comparison stops being "do we have what `aichat` or `crush` has" and
starts being "do we have what the seam we chose actually requires."
The answer, in this brief, is yes for three things and no for the
three current category-default columns (multi-provider, MCP client,
public-package distribution). The no's are not accidents; two of the
three have dedicated season blueprints behind them (S03 for providers,
S05 for MCP, S02E16 for distribution).

## Comparison set

Eight competitors in two tiers. Tier 1 is the small set we are
actually substitutable with: single-shot or scriptable CLIs that a
user could realistically swap us for. Tier 2 is the vendor-flagship
coding-agent field -- not a substitute for an Azure-OpenAI-native
binary, but the field everyone is benchmarked against, so we list
their 2026 axes for completeness. The long-form per-tool research
lives in [`docs/competitive-analysis.md`](./competitive-analysis.md);
this brief is the trimmed, opinionated cut.

The 2026 axes the rest of the field has converged on -- and that
this table now scores explicitly -- are: provider coverage (one or
many), MCP client support, and single-binary distribution. All three
are in our active backlog (S03, S05, S02E16) and openly absent today.

### Tier 1 -- direct substitutes

| Tool | License | Lang / runtime | Single binary | Provider coverage | MCP support | Distribution | Status (2026) |
|------|---------|----------------|---------------|-------------------|-------------|--------------|---------------|
| `azure-openai-cli` (this) | MIT | C# / .NET 10 AOT | Yes (~13 MiB) | Azure OpenAI only (S03 will widen) | None (S05 will add) | GHCR image + GitHub Releases; Homebrew/Scoop/Nix pending S02E16 | v2 active; weekly cadence |
| `llm` (Simon Willison) | Apache 2.0 | Python (pluggy) | No (interpreter) | OpenAI, Anthropic, Gemini, Ollama, OpenRouter, llama.cpp, +others via `llm-*` plugins | Partial; plugin-route, dynamic-tool refactor in flight for the 2026 API redesign | `pip`, `uv tool install`, Homebrew | Very active; broadest plugin marketplace in the category |
| `aichat` (sigoden) | MIT / Apache 2.0 | Rust | Yes | 20+ first-class incl. Azure OpenAI, OpenAI, Anthropic, Gemini, Bedrock, VertexAI, Ollama, Groq, Cohere, OpenRouter | Yes; first-class MCP client | `cargo install`, Homebrew, prebuilt | Very active; closest functional peer |
| `crush` (Charmbracelet) | FSL-1.1-MIT (Charm-style source-available, MIT after 2-yr) | Go | Yes | Azure OpenAI, OpenAI, Anthropic, Gemini, Bedrock, VertexAI, Groq, Ollama, OpenRouter, OpenAI-compat | Yes; MCP-native (stdio / HTTP / SSE), LSP-aware | Homebrew, npm, Nix, AUR, prebuilt | New flagship; replaces `mods` (archived March 9 2026) |
| `mods` (Charmbracelet) | MIT | Go | Yes | OpenAI-compatible endpoint (incl. Azure OpenAI) | Yes (final v1.8.x) | Homebrew, Go install, prebuilt | Archived 2026-03-09; migrate to `crush`. Kept for historical context |
| `shell-gpt` / `sgpt` | MIT | Python | No (interpreter) | OpenAI direct, anything via LiteLLM (Azure OpenAI via base-URL hack) | No | `pip` | Maintained but slowing; v1.5.0 Jan 2026 |
| `chatblade` | MIT | Python | No | OpenAI only | No | `pip`, AUR | Archived; upstream points at `llm` / `fabric` |
| OpenAI Python CLI / `az` AI extension | Apache 2.0 / MIT | Python | No | First-party (OpenAI / Azure mgmt-plane) | No | `pip install openai`, `az extension add` | Active first-party; chat UX is not their job |

### Tier 2 -- vendor-flagship coding agents (here for axis comparison, not as substitutes)

| Tool | License | Lang / runtime | Single binary | Provider coverage | MCP support | Distribution | Status (2026) |
|------|---------|----------------|---------------|-------------------|-------------|--------------|---------------|
| `claude` (Anthropic Claude Code) | Proprietary | Rust + TS | Yes (native installer) | Anthropic direct, AWS Bedrock, GCP Vertex | Yes; MCP-native | Native installer, Homebrew, WinGet, npm (deprecated path) | $20 Pro / $100 Max; no free tier |
| `codex` (OpenAI Codex CLI) | Apache 2.0 | Rust (+ Node modules) | Yes | OpenAI direct (ChatGPT account or API key) | Yes; MCP-native, parallel tool calls | Native installer, Homebrew, npm | Very active; OSS CLI billed via ChatGPT Plus quota |
| `gemini` (Google Gemini CLI) | Apache 2.0 | TypeScript on Node | No (npm wrapper) | Google Gemini direct (incl. Vertex auth) | Yes; MCP-native | `npm i -g @google/gemini-cli`, Homebrew, MacPorts, Conda | Very active; weekly stable cadence |
| GitHub Copilot CLI (`gh-copilot` host of this conversation) | Proprietary | Node / TS | No (Node runtime) | OpenAI / Anthropic / Gemini routed through GitHub Models | Yes; ships with built-in GitHub MCP server, accepts custom MCP servers + hooks | npm, Homebrew, WinGet, `gh extension install` | GA Feb 2026; requires paid Copilot seat ($10-$39/user/mo) |
| `cursor` Cursor CLI | Proprietary | Bundled runtime | Yes (cask/AUR-distributed) | Cursor-routed (OpenAI / Anthropic / Gemini family) | Yes; MCP + marketplace | Homebrew cask, AUR, installer scripts | Active; tied to Cursor account |
| Continue.dev `cn` | Apache 2.0 | TypeScript on Node | No | BYOM -- OpenAI, Anthropic, Ollama, custom; same `config.yaml` as IDE | Yes; first-class MCP servers in agent loop | npm install, curl installer | Very active; headless mode for CI/CD |
| `opencode` (sst / community) | MIT | TypeScript (Bun-bundled) | Bundled binary | 75+ providers via routing layer (OpenAI, Anthropic*, Gemini, Copilot, Groq, Ollama, LM Studio, ...) | Yes; MCP-compatible | npm, install script, prebuilt | Very active; weekly releases. *Anthropic blocks unofficial CLI access -- bring an official Claude key |

A few notes on the matrix:

- "Single binary" means the artifact is a self-contained executable
  the user copies into `PATH`. Bundled-runtime tools (Node-on-pkg,
  Bun-bundled) get a "Bundled binary" mark; they ship as one file but
  the runtime is inside it, not the OS.
- "Provider coverage" lists what the tool supports without third-party
  plugins. Many of the Tier 1 tools can reach more providers via
  OpenAI-compatible base URLs; that does not count here.
- "MCP support" refers to MCP-client capability (the CLI can mount
  external MCP servers as tools). Server mode is mentioned per-tool
  where it exists. By 2026 this is table stakes -- a "No" in this
  column is now the headline weakness, including ours.
- Adjacent tools we do not score here -- `fabric` (pattern library),
  `ollama` (model runner), `elia` and `oterm` (TUI clients),
  `llama.cpp` (local engine) -- are covered in
  [`docs/competitive-analysis.md`](./competitive-analysis.md) §2 and
  §2.5 because they serve a different seam.

## Three things we do better

### 1. Per-OS LOLBin credential storage with zero new dependencies

We do not ask the user to install `pass`, `gpg`, `keyring`, or a Python
shim. On Windows we use DPAPI. On macOS we shell out to the system
`security` binary. On Linux we shell out to `secret-tool` when a
desktop session is present and fall back to a 0600 plaintext file with
a loud warning when it is not. The whole story lives in
`azureopenai-cli/Credentials/` -- six small files, each one a thin
adapter over a tool the operating system already ships. Compare the
field: `llm` and `chatblade` keep the key in a config file under
`~/.config/`. `aichat` and `mods` keep it in a YAML file. The
OpenAI Python CLI uses `OPENAI_API_KEY` and calls it a day. None of
them bind the key to the user account at the OS level. Episode lineage:
S02E01 (the wizard), S02E04 (the locksmith).

### 2. Single-binary AOT distribution with a Trivy-clean Alpine image

The shipped binary is ~13 MiB, statically linked, and starts in
~10 ms p50 on the reference rig (see
[`docs/perf/v2.0.5-baseline.md`](./perf/v2.0.5-baseline.md) -- Bania
already shipped that benchmark in S02E05; we are not re-running it
here). The Docker image is multi-stage Alpine with no Python, no Node,
no .NET runtime, and a Trivy scan in CI on every build. Among the
Azure-OpenAI-native field, this is uncontested: `aichat` has a
comparable Rust binary but is not Azure-native, and the official
`openai` and `azure ai` CLIs both require a Python interpreter at
runtime. The build configuration is in
`azureopenai-cli/AzureOpenAI_CLI.csproj` and the multi-stage image is
in the repo root `Dockerfile`. The cold-start number is the load-bearing
property: it is what makes the CLI viable as the back end of an Espanso
or AHK trigger, where 300 ms of Python startup would feel like lag.

### 3. First-run wizard that walks new users through Azure-specific config

Azure OpenAI has more required configuration than OpenAI direct: an
endpoint URL, an API key, a deployment name (which is not the model
name), and an optional secondary deployment. None of the
general-purpose competitors know this. They prompt for one API key and
move on. We ship `azureopenai-cli/Setup/FirstRunWizard.cs`, which
detects an unconfigured environment (`Setup/SetupDetection.cs`),
prompts for each Azure-specific value with masked input where
appropriate, validates the endpoint with a live request, and writes
the result to the keystore picked by the credential factory above.
For a user whose first exposure to Azure OpenAI is this CLI, the
wizard is the difference between a working tool in two minutes and a
working tool in two afternoons of reading Microsoft Learn pages. No
competitor in the comparison set ships an equivalent.

## Four things we don't do (yet)

The original brief listed three. The 2026 sweep promotes MCP from
implicit-table-stakes to its own column, so the list is now four. Two
of the four (multi-provider, MCP) have explicit season blueprints
behind them; the other two (no-TUI, no-router) are deliberate and not
on the roadmap.

### 1. We are Azure-OpenAI-only. No OpenAI direct, no Anthropic, no local models. (S03 will narrow this gap, deliberately.)

Every Tier 1 competitor supports multiple providers, often via
plugins (`llm`), first-class config sections (`aichat`, `crush`), or
OpenAI-compatible base URLs (everyone). We do not yet, and our S03
blueprint commits to expanding -- on our own terms -- to a small
hand-picked set (Azure OpenAI, OpenAI direct, AWS Bedrock, local
Ollama / llama.cpp), not to chasing the 20+ provider marketplaces
`aichat` and `crush` have built. The reason for the cap is
load-bearing: every differentiator above (the AOT binary, the
keystore factory, the wizard) is small and tight precisely because
each new provider doubles the configuration surface, the keystore
semantics, and the wizard branching. The user who needs every
provider on the planet is not our user; they should reach for `llm`
or `crush`. The user who lives inside an Azure tenant and occasionally
wants a local fallback is.

### 2. We do not ship an interactive TUI mode.

`aichat` has a REPL. `crush` has a polished TUI. `llm` has `llm chat`.
We have single-shot, an `--agent` tool-calling loop, and the autonomous
Ralph loop. There is no Charm-style `bubbletea` interface and we are
not building one. The seam we serve -- Espanso triggers, AHK hotkeys,
shell pipelines, agentic sub-agents -- is a fire-and-forget seam. A
TUI would be a different product. If a user wants a TUI on top of our
binary they can drive it with `fzf` or `gum`; we will not ship one in
the box.

### 3. No MCP client today. (S05 owns this -- it is the load-bearing gap.)

By 2026 every credible CLI in the comparison set ships an MCP client:
`claude`, `codex`, `gemini`, GitHub Copilot CLI, `cursor`, `crush`,
`aichat`, `opencode`, `continue`, even `ollama` via bridge clients,
and `llm` via plugin. MCP is the de facto standard for "let the
model reach external tools without each CLI inventing its own
plugin shape." We do not have one. Our internal six-tool registry
(`shell_exec`, `read_file`, `web_fetch`, `get_clipboard`,
`get_datetime`, `delegate_task`) is intentionally fixed and audited;
adding MCP means adding an unbounded surface, which is why we are
sequencing it carefully under the S05 blueprint rather than racing
the field. Until S05 lands, this is the column where we are visibly
behind, and we say so on the tin.

### 4. No multi-model routing and no curated prompt library shipped in the box.

Competitors increasingly ship a "use the cheap model for this, the
smart model for that" router and a curated prompt library to back it.
`fabric` is the leader here with hundreds of named patterns; `crush`
and `claude` route per-task to per-model. We have neither, by choice
for the router and by sequencing for the library. Multi-model routing
belongs to a CLI that owns the user's model selection; we want the
user to keep that decision in their squad config or their script.
The prompt library work landed in the Maestro episode (S02E18) as a
design sketch under `docs/prompts/`, not yet a runtime registry --
users who want a curated prompt collection should still layer one on
top, since the CLI is prompt-shaped on stdin and does not need to own
the prompt registry.

## Where this leaves us

We are not the most flexible CLI in the category and we will not try to
be. We are the Azure-OpenAI-native binary that disappears into a
keyboard shortcut. The three differentiators above are the things that
make that role viable; the four accepted gaps (one of which -- MCP --
is now a category-default rather than an exotic feature) are the
things we do not need to win that role today, with two of them having
explicit S03 / S05 fixes already scoped. A positioning sentence a
contributor can repeat without checking notes:
*the fastest, smallest, most Azure-native single-binary chat client,
optimized for text-injection and agentic delegation, not for sitting
inside a terminal REPL.*

## Re-running this analysis

Refresh on the earlier of:

- Every major release (next: 3.0.0).
- Annually, on the anniversary of this file's first commit.
- When a competitor in the matrix ships a feature that lands in our
  three "we do better" list (especially: per-OS keystore parity, true
  AOT in a Python or Node tool, or an Azure-first wizard).
- When one of our four "don't do" gaps closes -- in particular when
  S03 (multi-provider) or S05 (MCP) ships, the corresponding row in
  the comparison set should flip and the gap section should retire.

Last refresh: 2026 sweep that added the MCP-support, multi-provider
coverage, and single-binary distribution columns; promoted `crush`
to Tier 1 as `mods`'s successor; added a Tier 2 block for the
vendor-flagship coding agents (`claude`, `codex`, `gemini`,
GitHub Copilot CLI, `cursor`, `continue`, `opencode`).

Refresh delta lives in this file -- no separate "v2 of the brief"
document. Bump the date in the front matter and edit in place. Long-
form data refreshes go in
[`docs/competitive-analysis.md`](./competitive-analysis.md).

---

## Peterman positioning copy (draft -- not yet adopted)

The following three paragraphs are J. Peterman drafts. They package
each of the three differentiators above as a single quotable
catalog-narrative paragraph that could go in a README hero, a press
kit, or a release announcement. They are not in the README yet --
adoption is orchestrator-owned and waits on Mr. Lippman's release
polish pass.

### Draft A -- The Keystore (per-OS LOLBin credential storage)

> I was in Tallinn, summer of an unspecified year, watching a man feed
> his API key to a YAML file the way one feeds a postcard to a stranger
> on a train. Trusting. Hopeful. Doomed. There is a better way. On
> Windows the operating system already keeps secrets, and so we use it.
> On macOS the keychain has been waiting since 1999, and so we use it.
> On Linux the desktop session can hold the key behind a session lock,
> and so we use it. No new dependencies. No new daemons. No new
> Python. The key is the user's, bound to the user's account, in the
> place the operating system already keeps such things. We just had
> the manners to ask.

### Draft B -- The Binary (single-file AOT, Trivy-clean Alpine)

> Thirteen megabytes. One file. No interpreter. No package manager
> resolving its own existential crisis at three in the morning. The
> binary starts in roughly the time it takes a competent espresso
> machine to acknowledge your existence -- ten milliseconds, give or
> take, on a laptop -- and disappears just as quickly. We ship it as a
> file. We ship it as an Alpine image with no Python and no Node, and
> the security scanner in our pipeline finds nothing to complain
> about. It is the kind of artifact you can put on the back of a
> hotkey and forget you put it there. Which is, in our experience,
> exactly what a tool should be.

### Draft C -- The Wizard (first-run Azure-specific config)

> Azure OpenAI does not believe in shortcuts. There is an endpoint.
> There is a key. There is a deployment, which is not the model, which
> is the part that confuses everyone the first time. Most CLIs will
> ask you for an API key and wish you well. We do not. The first time
> you run the binary it sits down with you, asks the four questions
> Azure actually requires, masks the key as you type it, and validates
> the endpoint with a live request before it writes anything to disk.
> Two minutes, start to finish. The next person you onboard will thank
> you. They will not know why; they will simply not have spent an
> afternoon reading Microsoft Learn. That is the whole product, in
> miniature.
