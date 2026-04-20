# FR-014 — Local User Preferences + Multi-Provider Profiles

**Status:** Stub (Costanza, 2026-04-20)
**Related:** FR-003 (local-user-preferences), FR-009 (config-set + directory overrides), FR-010 (model aliases)

## Problem Statement

az-ai configuration today lives in `.env` files and CLI flags. Users who work across multiple projects, Azure tenants, and providers pay a flag-juggling tax on every invocation — the very thing that undercuts our 5.4 ms cold-start advantage at the *human* latency layer. Competitors (aichat, llm, fabric) ship user-preference files and multi-provider profiles as baseline ergonomics. We need a single, AOT-safe preferences format (`~/.config/az-ai/preferences.toml`) that unifies FR-003/009/010 and adds **provider profiles** so az-ai can transparently target Anthropic, Gemini, or local Ollama as fallbacks — while keeping Azure OpenAI the default and the only first-class target.

## Competitive Gap Justification

- aichat's per-user config + 20+ provider support is the pattern users expect.
- llm's profile/model-alias system is the most-copied feature of that ecosystem.
- The user mission statement for this recon explicitly called out "enabling the user to set preferences locally (models to interact with, etc other features)" as a first-order roadmap priority.
- Multi-provider adapters *do not* dilute our Azure positioning; they add a safety valve for power users and reduce the #1 reason users bounce to aichat.

## Rough Effort

**Medium (3/5).** TOML parsing (AOT-safe via source-gen), preference schema design, merge semantics (global → project → env → flag), and two-provider adapter skeletons (Anthropic + OpenAI-direct, reusing existing HTTP client). ~1.5–2 engineer-weeks to ship a defensible v1 that absorbs FR-003/009/010 stubs.
