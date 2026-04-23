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
some flavor of tool calling, and every credible entrant has a story for
MCP. The differentiation game is no longer "do you have a chat loop"
but rather "what is the seam you fit into, and how cleanly do you fit
there." Most of the field is fitting itself into the general-purpose
"terminal copilot" seam: an interactive REPL with a plugin marketplace
aimed at developers doing exploratory work. That is a crowded runway.

`azure-openai-cli` is not on that runway. It is a single-shot,
Azure-OpenAI-native, sub-15-millisecond binary that exists primarily to
be invoked by Espanso, AHK, shell scripts, and agentic delegation --
not by a human typing at a prompt. Once you understand that, the
comparison stops being "do we have what `aichat` has" and starts being
"do we have what the seam we chose actually requires." The answer, in
this brief, is yes for three things and no for three things, and the
no's are not accidents.

## Comparison set

Five competitors. Picked because each one represents a distinct point
in the design space we could plausibly be confused with.

| Tool | Language / Runtime | Distribution | Auth model | Tools / Agents | Docker / AOT | License | Maintenance signal |
|------|--------------------|--------------|------------|----------------|--------------|---------|--------------------|
| `azure-openai-cli` (this) | C# / .NET 10, Native AOT | Single ~13 MiB binary; GHCR Alpine image; `make install` | Per-OS keystore (DPAPI / Keychain / libsecret) with plaintext fallback | Internal registry: `shell_exec`, `read_file`, `web_fetch`, `get_clipboard`, `get_datetime`, `delegate_task` (capped at depth 3); persona memory in `.squad/` | True AOT, multi-stage Alpine image, Trivy-clean | MIT | v2 line active; weekly cadence; small contributor count |
| `llm` (Simon Willison) | Python (pluggy) | `pip` / `uv tool install llm` | API key in env or file; provider plugins manage their own auth | Deepest plugin ecosystem (`llm-*`); SQLite log of every call; toolbox plugins for function calling | No AOT; no first-party container | Apache 2.0 | Very active; broad contributor base; mature plugin marketplace |
| `aichat` (sigoden) | Rust | `cargo install`, Homebrew, prebuilt binaries | Config file with provider sections; env-var override | First-class function calling, RAG, sessions, `llm-functions` tool repo, MCP support | Single Rust binary; community Docker images | MIT / Apache 2.0 | Very active; large contributor base; frequent releases |
| `mods` (Charmbracelet) | Go | Homebrew, Go install, prebuilt | YAML config with provider sections; env-var override | Pipe-friendly single-shot; MCP client in late releases | Single Go binary | MIT | Archived March 2026; superseded by `crush`. Listed for historical context |
| OpenAI Python CLI / `azure ai` extension | Python (`openai`, `azure-cli`) | `pip install openai`, `az extension add` | Env vars and `~/.azure/` profile; `az login` for management plane | OpenAI SDK exposes function calling but the CLIs themselves are mostly thin REST wrappers; the Azure extension is provisioning, not chat | No AOT; Python interpreter required | Apache 2.0 | First-party, very active; chat UX is not their job |
| `chatblade` | Python | `pip`, Arch | API key in env or config | None (early "swiss army knife" pattern) | No AOT | MIT | Effectively archived; upstream points users at `llm` / `fabric` |

Two notes on the matrix:

- "Maintenance signal" is a ballpark from public release cadence and
  contributor counts at time of writing. Not a quality judgment.
- We did not include Claude Code, Codex CLI, Gemini CLI, or
  `gh copilot cli` in this brief because they are vertically integrated
  with their parent platform's billing and auth. They are not
  substitutes for an Azure-OpenAI-native binary; they are substitutes
  for buying a different cloud relationship. The long-form analysis in
  [`docs/competitive-analysis.md`](./competitive-analysis.md) covers
  them anyway.

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

## Three things we don't do

### 1. We are Azure-OpenAI-only. No OpenAI direct, no Anthropic, no local models.

Every competitor in the matrix supports multiple providers, often via
plugins (`llm`), config sections (`aichat`), or OpenAI-compatible base
URLs (`mods`). We do not, and we are not planning to. The reason is
load-bearing: every differentiator above (the AOT binary, the keystore
factory, the wizard) is small and tight precisely because we are
solving one provider's auth model and one provider's deployment-vs-model
distinction. Adding a second provider doubles the configuration
surface, the keystore semantics, and the wizard branching. The user
who needs three providers is not our user. The user who lives inside
an Azure tenant and wants the binary to disappear into their workflow
is.

### 2. We do not ship an interactive TUI mode.

`aichat` has a REPL. `crush` has a polished TUI. `llm` has `llm chat`.
We have single-shot, an `--agent` tool-calling loop, and the autonomous
Ralph loop. There is no Charm-style `bubbletea` interface and we are
not building one. The seam we serve -- Espanso triggers, AHK hotkeys,
shell pipelines, agentic sub-agents -- is a fire-and-forget seam. A
TUI would be a different product. If a user wants a TUI on top of our
binary they can drive it with `fzf` or `gum`; we will not ship one in
the box.

### 3. No multi-model routing and (until S02E18 lands) no prompt template library.

Competitors increasingly ship a "use the cheap model for this, the
smart model for that" router and a curated prompt library to back it.
`fabric` is the leader here with hundreds of named patterns. We have
neither, by choice for the router and by sequencing for the library.
Multi-model routing belongs to a CLI that owns the user's model
selection; we want the user to keep that decision in their squad
config or their script. The prompt library is a real gap that the
Maestro episode (S02E18) is expected to close. Until then, users who
want a curated prompt collection should layer one on top -- the CLI is
prompt-shaped on stdin and does not need to own the prompt registry.

## Where this leaves us

We are not the most flexible CLI in the category and we will not try to
be. We are the Azure-OpenAI-native binary that disappears into a
keyboard shortcut. The three differentiators above are the things that
make that role viable; the three accepted gaps are the things we do
not need to win that role and would slow us down if we tried. A
positioning sentence a contributor can repeat without checking notes:
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
