# Opportunity Analysis -- What az-ai Has, What We Lack, What to Build

**Companion to:** `docs/competitive-analysis.md` (2026-04-20 scan)
**Purpose:** Convert competitive findings into a ranked backlog.

## 1. What az-ai has that nobody else has

1. **Sub-10 ms cold start via .NET Native AOT.** Measured 5.4 ms on linux-x64. No other *chat* CLI in the landscape is under ~30 ms. This is the foundation of the Espanso/AHK text-injection use case.
2. **Azure OpenAI as the first-class target**, including Azure US Government (FedRAMP High / IL5 reachable via `AzureUSGovernment` cloud endpoints). Every other Azure-capable CLI treats it as an "OpenAI-compatible base URL" hack.
3. **Persona+Squad memory (`.squad/`)** -- named AI teammates with per-persona tools and persistent memory. Closest analogues (llm's toolboxes, fabric sessions) lack the named-persona + bench-of-25 dispatch model.
4. **Raw mode (`--raw`)** designed for text-expander injection. No competitor treats in-field text replacement as a first-class use case.
5. **Ralph mode** -- autonomous validator-driven retry loop. Simple, transparent, and uses the same agent harness. Codex/Claude Code have richer sandboxes but more opaque behavior.

## 2. What we lack (honest list)

1. **MCP support.** Now table-stakes (Claude Code, Codex, gh copilot, Gemini CLI, aichat, crush, fabric, ollama-via-bridge all ship it). We are visibly behind.
2. **Multi-provider flexibility.** Azure-only is great for our ICP but narrows TAM. Power users expect to bring Anthropic/Gemini/local-Ollama as fallbacks.
3. **Local user preferences / profiles.** Config is currently .env-driven; no `~/.config/az-ai/preferences.toml` with model aliases, per-project overrides, or tool allowlists (partially addressed by FR-003/FR-009/FR-010 on paper but not unified).
4. **Plugin marketplace / registry.** FR-012 stubbed Kramer's plugin registry; nothing shipped. llm and fabric both out-ship us here.
5. **Prompt/pattern library.** fabric has hundreds of curated patterns; we have none shipped. Low-cost, high-leverage.
6. **Package-manager surface.** We ship GHCR + make. No Homebrew tap, no Scoop manifest, no Nix flake, no winget. Bob Sacamano's beat.
7. **Public cost calculator / token budgeter.** Morty flagged this -- we have the cheapest story but don't prove it in the UX.
8. **Response cache** (FR-008 stubbed). Cold-start wins are partially negated if we roundtrip the API on repeated identical prompts.
9. **Streaming-agent output parity** (FR-011 stubbed).
10. **MCP-as-server** -- being an MCP *server* (not just client) would let az-ai plug into Claude Code / gh copilot / Codex sessions. Huge distribution play.

## 3. Ranked opportunities (effort × impact)

Scored 1-5. **Score = Impact × (6 − Effort)** so low effort + high impact wins.

| # | Opportunity | Effort | Impact | Score | FR stub |
|---|---|:-:|:-:|:-:|---|
| 1 | **MCP client + server support** -- close the table-stakes gap and unlock distribution inside Claude Code / gh copilot | 4 | 5 | 10 | **FR-013** |
| 2 | **Local preferences file + model/provider profiles** -- `~/.config/az-ai/preferences.toml`, per-project overrides, aliases, multi-provider (Anthropic/Gemini/Ollama adapters behind `AZ_PROVIDER=…`) | 3 | 5 | 15 | **FR-014** |
| 3 | **Pattern/prompt library + cost calculator bundle** -- ship curated `az-ai pattern run <name>` set (steal fabric's idea, Azure-tuned) and a `--estimate` flag that prints predicted cost before execution | 2 | 4 | 16 | **FR-015** |
| 4 | Plugin marketplace (builds on FR-012) | 5 | 4 | 4 | *defer* |
| 5 | Homebrew/Scoop/Nix/winget packaging | 2 | 3 | 12 | *Bob Sacamano separately* |
| 6 | Response cache (FR-008) | 2 | 3 | 12 | *already stubbed* |

**Top 3 to file as new stubs now:** FR-013 (MCP), FR-014 (preferences + multi-provider), FR-015 (pattern library + cost estimator).

The user's mission explicitly emphasized (a) lowering latency for near-instant feedback, and (b) local user preferences / model selection. FR-013 doesn't change latency but protects distribution. FR-014 directly addresses local preferences. FR-015 directly lowers *perceived* latency (estimator shows cost instantly, pattern library skips the "compose the prompt" round trip) and gives Morty a public artifact for the "cheapest in class" claim.

## 4. What we are NOT doing (and why)

- **Not charging for the CLI.** Every free CLI has more mindshare than every paid CLI except the two that bundle a first-party model. We have no model to bundle. Free stays.
- **Not rewriting in Rust/Go.** .NET AOT already wins the cold-start benchmark; rewrite is negative ROI.
- **Not building a TUI** à la `crush`. Our wedge is in-field text injection, not in-terminal dashboards. Stay narrow.
- **Not pursuing non-Azure as the default.** Multi-provider *adapters* (FR-014) are a safety valve, not a repositioning.
