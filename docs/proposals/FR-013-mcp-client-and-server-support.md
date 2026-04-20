# FR-013 — MCP (Model Context Protocol) Client and Server Support

**Status:** Stub (Costanza/Sue Ellen competitive recon, 2026-04-20)
**Related:** FR-012 (plugin registry), `docs/competitive-analysis.md`

## Problem Statement

By April 2026, **Model Context Protocol (MCP)** has become the de facto integration standard for CLI AI tools. Claude Code, OpenAI Codex CLI, GitHub Copilot CLI, Google Gemini CLI, aichat, Charmbracelet `crush`, fabric, and ollama-via-bridge all ship MCP client support; several also act as MCP servers. az-ai currently ships *neither* role. The gap shows up in two concrete ways: (1) teams that standardized on MCP-based tool catalogs cannot plug az-ai into their existing workflow, and (2) az-ai cannot be surfaced as a sub-tool inside higher-level agents like Claude Code or `gh copilot cli`, which is the single largest distribution vector we are leaving on the table.

## Competitive Gap Justification

Every direct competitor ships MCP in 2026. See competitive matrix row "MCP support": the only tools without it are archived (`chatblade`, `mods`), management-only (`Azure AI CLI`), or have limited client-side plugin workarounds (`llm`, `sgpt`). az-ai is the only *actively-maintained, AOT, Azure-native* chat CLI without MCP — and that is now a liability, not a focus choice.

Being an MCP **server** additionally flips the distribution equation: instead of asking users to install az-ai, we make az-ai reachable from the tool they already run.

## Rough Effort

**Large (4/5).** MCP spec is non-trivial (JSON-RPC transport, tool schema, streaming, server lifecycle). C# MCP SDK availability should be audited first; if acceptable, ~2–3 engineer-weeks for client, +2 weeks for server role. Keep AOT-compatibility as a hard constraint — no reflection-heavy JSON libraries.
