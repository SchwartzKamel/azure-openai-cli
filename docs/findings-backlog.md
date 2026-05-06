# Findings Backlog

Canonical, queryable record of every audit finding. Owned by Mr. Wilhelm
(process). Companion to the [`findings-backlog`](../.github/skills/findings-backlog.md)
skill: the skill defines the discipline, this file is the ledger.

The 5-state lifecycle (per the skill):

    open -> in-progress -> resolved -> deferred -> wontfix

## Conventions

- **ID format:** `<auditor>-<YYYY-MM>-<finding-id>`. Examples:
  `elaine-2026-05-C1`, `newman-2026-05-F-1`, `wilhelm-2026-05-W-01`.
- **Source:** relative link to the audit file under `docs/audits/`.
- **Severity:** verbatim from the audit (CRITICAL / HIGH / MAJOR / MEDIUM /
  LOW / MINOR / NIT / INFO). Gate-enforced tier:
  CRITICAL / HIGH / MAJOR / RED.
- **State:** one of `open`, `in-progress`, `resolved`, `deferred`, `wontfix`.
- **Last update:** ISO-8601 date of the most recent state change.
- **Resolved rows** carry the closing reference (commit / PR / episode) in
  the title cell or a trailing parenthetical.
- **Deferred / wontfix** rows must include a one-sentence rationale.

The CI gate (`make exec-report-check`) enforces that every CRITICAL / HIGH /
MAJOR / RED finding in any non-exempt audit under `docs/audits/` has a
matching row here. See [`scripts/exec-report-check.sh`](../scripts/exec-report-check.sh).

---

## Active

| ID | Source | Severity | State | Owner | Title | Last update |
|---|---|---|---|---|---|---|
| frank-2026-05-FB-1 | [s03e23-the-fallback.md](exec-reports/s03e23-the-fallback.md) | LOW | open | Frank Costanza | S03E22 *The Fallback* production `AlternateChatClientFactory` always returns `Skipped("no-fallback-creds")`; per-preset `AZ_AI_<PRESET>_*` cred discovery for alternates is not yet wired. Chain parses, validates, and emits telemetry but cannot recover today. Closes when a future episode wires per-preset cred discovery (likely paired with S03E23 Squad / S03E25 rotate creds). | 2026-05-09 |
| frank-2026-05-FB-2 | [s03e23-the-fallback.md](exec-reports/s03e23-the-fallback.md) | INFO | open | Frank Costanza | S03E22 *The Fallback* `--fallback` opt-in is not yet surfaced by `--config show`. An operator can read CLI/env state but the standard config snapshot doesn't echo the resolved chain + source. Closes when `--config show` (or `--doctor`) prints the chain. | 2026-05-09 |
| kramer-2026-05-PMP-1 | [s03e28-the-persona-multi-provider.md](exec-reports/s03e28-the-persona-multi-provider.md) | LOW | open | Kramer | S03E23 *The Persona, Multi-Provider* persona-pinned `provider` flows through `PreferencesResolver.Resolve` and produces a `persona:<name>:provider` source label, but actual dispatch routing in `BuildChatClient` still keys off env (`AZ_AI_COMPAT_MODELS`, `AZURE_FOUNDRY_MODELS`). An operator who pins `provider: groq` on a persona but does not also set `AZ_AI_COMPAT_MODELS=groq:<model>` will get the resolver label without the dispatch swap. Closes when a future episode adds an env-rewrite shim or when `BuildChatClient` accepts a resolved provider directly. | 2026-05-10 |
| kramer-2026-05-PMP-2 | [s03e28-the-persona-multi-provider.md](exec-reports/s03e28-the-persona-multi-provider.md) | INFO | open | Kramer | S03E23 *The Persona, Multi-Provider* `--config show` does not yet echo the persona rung in its source-field output. The resolver returns `persona:<name>:provider` / `persona:<name>:model` labels and the persona invocation site logs them to stderr, but the canonical config dump still only prints CLI/env/profile/default. Closes when `--config show` learns to take an optional persona name (or auto-load active persona) and surface the rung. | 2026-05-10 |
| elaine-2026-05-C1 | [docs-audit-2026-05-elaine.md](audits/docs-audit-2026-05-elaine.md) | CRITICAL | in-progress | Elaine | README pre-built binaries table pinned to v2.0.5 (repo at v2.2.0) | 2026-05-06 |
| elaine-2026-05-M1 | [docs-audit-2026-05-elaine.md](audits/docs-audit-2026-05-elaine.md) | MAJOR | open | Elaine | README still calls AGENTS.md a 25-agent roster | 2026-05-06 |
| elaine-2026-05-M2 | [docs-audit-2026-05-elaine.md](audits/docs-audit-2026-05-elaine.md) | MAJOR | open | Elaine | persona-guide.md also calls it a 25-agent roster | 2026-05-06 |
| elaine-2026-05-M3 | [docs-audit-2026-05-elaine.md](audits/docs-audit-2026-05-elaine.md) | MAJOR | open | Elaine | copilot-instructions.md disagrees on supporting-player count | 2026-05-06 |
| elaine-2026-05-M4 | [docs-audit-2026-05-elaine.md](audits/docs-audit-2026-05-elaine.md) | MAJOR | open | Elaine | ROADMAP.md announces v2.0.4 as current release | 2026-05-06 |
| elaine-2026-05-M5 | [docs-audit-2026-05-elaine.md](audits/docs-audit-2026-05-elaine.md) | MAJOR | in-progress | Mr. Lippman + Elaine | CHANGELOG `[Unreleased]` is empty; commit 905515e unrecorded | 2026-05-06 |
| elaine-2026-05-M6 | [docs-audit-2026-05-elaine.md](audits/docs-audit-2026-05-elaine.md) | MAJOR | open | Elaine | use-cases.md lists five execution modes; banner promises six | 2026-05-06 |
| elaine-2026-05-M7 | [docs-audit-2026-05-elaine.md](audits/docs-audit-2026-05-elaine.md) | MAJOR | open | Elaine | espanso-ahk-integration.md silent on prompt-templates and v2.2.0 triggers | 2026-05-06 |
| elaine-2026-05-M8 | [docs-audit-2026-05-elaine.md](audits/docs-audit-2026-05-elaine.md) | MAJOR | open | Elaine | README Documentation section omits prompt library link | 2026-05-06 |
| elaine-2026-05-M9 | [docs-audit-2026-05-elaine.md](audits/docs-audit-2026-05-elaine.md) | MAJOR | open | Elaine | AHK hotkey table missing prompt-template hotkeys | 2026-05-06 |
| elaine-2026-05-M10 | [docs-audit-2026-05-elaine.md](audits/docs-audit-2026-05-elaine.md) | MAJOR | open | Elaine | persona-guide.md still scopes itself to v2.0.0 | 2026-05-06 |
| elaine-2026-05-M11 | [docs-audit-2026-05-elaine.md](audits/docs-audit-2026-05-elaine.md) | MAJOR | open | Elaine | README Why section cites v2.0.6 but links v2.0.5 baseline | 2026-05-06 |
| elaine-2026-05-m1 | [docs-audit-2026-05-elaine.md](audits/docs-audit-2026-05-elaine.md) | MINOR | open | Elaine | README Upgrading-from-v1.9.x pointer two minors stale | 2026-05-06 |
| elaine-2026-05-m2 | [docs-audit-2026-05-elaine.md](audits/docs-audit-2026-05-elaine.md) | MINOR | open | Elaine | README New-in-v2.0.0 heading not refreshed since v2.0.0 | 2026-05-06 |
| elaine-2026-05-m3 | [docs-audit-2026-05-elaine.md](audits/docs-audit-2026-05-elaine.md) | MINOR | open | Elaine + Puddy | README claims 1,510+ passing tests; ground truth uncertain | 2026-05-06 |
| elaine-2026-05-m4 | [docs-audit-2026-05-elaine.md](audits/docs-audit-2026-05-elaine.md) | MINOR | open | Elaine | ARCHITECTURE.md GHCR path differs from README | 2026-05-06 |
| elaine-2026-05-m5 | [docs-audit-2026-05-elaine.md](audits/docs-audit-2026-05-elaine.md) | MINOR | open | Elaine | espanso-ahk-wsl/README.md lists 8 triggers as if exhaustive | 2026-05-06 |
| elaine-2026-05-m6 | [docs-audit-2026-05-elaine.md](audits/docs-audit-2026-05-elaine.md) | MINOR | open | Elaine | config-reference.md scopes itself to v2.0.0+ | 2026-05-06 |
| elaine-2026-05-m7 | [docs-audit-2026-05-elaine.md](audits/docs-audit-2026-05-elaine.md) | MINOR | open | Elaine | `:aiprompts` self-help trigger missing from kit README inventory | 2026-05-06 |
| elaine-2026-05-n1 | [docs-audit-2026-05-elaine.md](audits/docs-audit-2026-05-elaine.md) | NIT | open | Elaine | README chrome contains em-dash (U+2014) and right-arrow (U+2192) | 2026-05-06 |
| elaine-2026-05-n2 | [docs-audit-2026-05-elaine.md](audits/docs-audit-2026-05-elaine.md) | NIT | open | Elaine | README `--config` flag split across two table rows | 2026-05-06 |
| newman-2026-05-F-3 | [security-v2.1-post-prompts.md](audits/security-v2.1-post-prompts.md) | MEDIUM | open | Jerry | docs-lint.yml regressed to tag-pinned actions | 2026-05-06 |
| newman-2026-05-K-1 | [security-v2.1.2-keychain.md](audits/security-v2.1.2-keychain.md) | LOW | open | Elaine | README per-provider example block does not restate chmod 600 hygiene | 2026-05-07 |
| newman-2026-05-K-2 | [security-v2.1.2-keychain.md](audits/security-v2.1.2-keychain.md) | LOW | open | Costanza | ADR-010 references per-OS keychain but no ADR exists for it (file half delivered, keychain half deferred) | 2026-05-07 |
| newman-2026-05-K-3 | [security-v2.1.2-keychain.md](audits/security-v2.1.2-keychain.md) | INFO | open | Newman | Raw-mode pre-detection scans argv only; future raw-mode aliases (env / config) would not be honoured | 2026-05-07 |
| newman-2026-05-F-4 | [security-v2.1-post-prompts.md](audits/security-v2.1-post-prompts.md) | MEDIUM | open | Kramer | BuildImageClient duplicates BuildChatClient Foundry path | 2026-05-06 |
| newman-2026-05-F-5 | [security-v2.1-post-prompts.md](audits/security-v2.1-post-prompts.md) | MEDIUM | open | Kramer | ClipboardImageWriter.RunWSL interpolates wslpath into PowerShell string | 2026-05-06 |
| newman-2026-05-F-6 | [security-v2.1-post-prompts.md](audits/security-v2.1-post-prompts.md) | LOW | open | Kramer | ClipboardImageWriter.RunMacOS builds AppleScript by string concat | 2026-05-06 |
| newman-2026-05-F-7 | [security-v2.1-post-prompts.md](audits/security-v2.1-post-prompts.md) | LOW | open | Kramer | ClipboardImageWriter.RunX11 quotes filePath into Arguments string | 2026-05-06 |
| newman-2026-05-F-8 | [security-v2.1-post-prompts.md](audits/security-v2.1-post-prompts.md) | LOW | open | Kramer + Newman | LoadConfigEnvFrom does not verify file mode | 2026-05-06 |
| newman-2026-05-F-9 | [security-v2.1-post-prompts.md](audits/security-v2.1-post-prompts.md) | LOW | open | Puddy | SensitiveEnvVars parameterised test does not cover all 12 entries | 2026-05-06 |
| newman-2026-05-F-10 | [security-v2.1-post-prompts.md](audits/security-v2.1-post-prompts.md) | LOW | open | Puddy | No positive test that ReadFileTool rejects NFKC homoglyph for blocked path | 2026-05-06 |
| newman-2026-05-F-11 | [security-v2.1-post-prompts.md](audits/security-v2.1-post-prompts.md) | LOW | open | Newman + Kramer | WebFetchTool.IsPrivateAddress misses 100.64/10 (CGNAT) and 0.0.0.0/8 | 2026-05-06 |
| newman-2026-05-F-12 | [security-v2.1-post-prompts.md](audits/security-v2.1-post-prompts.md) | INFO | open | Newman | WebFetchTool performs a TOCTOU DNS check | 2026-05-06 |
| newman-2026-05-F-13 | [security-v2.1-post-prompts.md](audits/security-v2.1-post-prompts.md) | INFO | open | Elaine | env file chmod 600 expectation absent from README auto-load section | 2026-05-06 |
| newman-2026-05-F-14 | [security-v2.1-post-prompts.md](audits/security-v2.1-post-prompts.md) | INFO | open | Puddy | --debug absence not asserted by tests | 2026-05-06 |
| wilhelm-2026-05-W-01 | [audit-process-meta-2026-05.md](audits/audit-process-meta-2026-05.md) | HIGH | in-progress | Soup Nazi + Wilhelm | Findings-backlog skill documented and unused | 2026-05-06 |
| wilhelm-2026-05-W-02 | [audit-process-meta-2026-05.md](audits/audit-process-meta-2026-05.md) | HIGH | in-progress | Larry David | s03-writers-room.md does not exist | 2026-05-06 |
| wilhelm-2026-05-W-04 | [audit-process-meta-2026-05.md](audits/audit-process-meta-2026-05.md) | MEDIUM | open | Wilhelm | Audits do not cite themselves in fix commits | 2026-05-06 |
| wilhelm-2026-05-W-05 | [audit-process-meta-2026-05.md](audits/audit-process-meta-2026-05.md) | MEDIUM | open | Mr. Pitt + Wilhelm | No audit cadence policy | 2026-05-06 |
| wilhelm-2026-05-W-06 | [audit-process-meta-2026-05.md](audits/audit-process-meta-2026-05.md) | LOW | open | Wilhelm | Verdict discipline below 50% across audit corpus | 2026-05-06 |
| wilhelm-2026-05-W-07 | [audit-process-meta-2026-05.md](audits/audit-process-meta-2026-05.md) | LOW | open | Elaine + Wilhelm | ADR linkage is informal | 2026-05-06 |
| wilhelm-2026-05-W-08 | [audit-process-meta-2026-05.md](audits/audit-process-meta-2026-05.md) | LOW | open | Wilhelm | Meta-audit cadence undefined | 2026-05-06 |
| jerry-2026-04-C1 | [docs-audit-2026-04-22-jerry.md](audits/docs-audit-2026-04-22-jerry.md) | CRITICAL | open | Jerry | release-runbook.md matrix description wrong on v1 and v2 | 2026-04-22 |
| jerry-2026-04-C2 | [docs-audit-2026-04-22-jerry.md](audits/docs-audit-2026-04-22-jerry.md) | CRITICAL | open | Jerry | v2.0.0-day-one-baseline asset inventory inflates count | 2026-04-22 |
| jerry-2026-04-H1 | [docs-audit-2026-04-22-jerry.md](audits/docs-audit-2026-04-22-jerry.md) | HIGH | open | Jerry | release-v2-playbook macos-13 troubleshooting section stale | 2026-04-22 |
| jerry-2026-04-H2 | [docs-audit-2026-04-22-jerry.md](audits/docs-audit-2026-04-22-jerry.md) | HIGH | open | Jerry | v2.0.2-publish-handoff recovery recipe contradicts itself | 2026-04-22 |
| jerry-2026-04-H3 | [docs-audit-2026-04-22-jerry.md](audits/docs-audit-2026-04-22-jerry.md) | HIGH | open | Jerry | release.yml inline comment cites stale macos-13 backlog | 2026-04-22 |
| keith-2026-04-C-1 | [docs-audit-2026-04-22-keith.md](audits/docs-audit-2026-04-22-keith.md) | CRITICAL | open | Keith Hernandez | All three demo scripts call `az-ai`, not `az-ai-v2` | 2026-04-22 |
| keith-2026-04-C-2 | [docs-audit-2026-04-22-keith.md](audits/docs-audit-2026-04-22-keith.md) | CRITICAL | open | Keith Hernandez | docs/announce/v1.8.0-launch.md is a major version behind | 2026-04-22 |
| keith-2026-04-H-1 | [docs-audit-2026-04-22-keith.md](audits/docs-audit-2026-04-22-keith.md) | HIGH | open | Keith Hernandez | Default model drift in azureopenai-cli.sample.json | 2026-04-22 |
| keith-2026-04-H-2 | [docs-audit-2026-04-22-keith.md](audits/docs-audit-2026-04-22-keith.md) | HIGH | open | Keith Hernandez | README performance table cites v1 numbers under v2.0.4 tag | 2026-04-22 |
| keith-2026-04-H-3 | [docs-audit-2026-04-22-keith.md](audits/docs-audit-2026-04-22-keith.md) | HIGH | open | Keith Hernandez | Espanso/AHK guide WSL section cites v1 AOT numbers | 2026-04-22 |
| keith-2026-04-H-4 | [docs-audit-2026-04-22-keith.md](audits/docs-audit-2026-04-22-keith.md) | HIGH | open | Keith Hernandez | Demo scripts have no paired narration / talk-track docs | 2026-04-22 |
| keith-2026-04-H-5 | [docs-audit-2026-04-22-keith.md](audits/docs-audit-2026-04-22-keith.md) | HIGH | open | Keith Hernandez | No speaker bureau bio / headshot / COI disclosure | 2026-04-22 |
| newman-2026-04-F-1 | [docs-audit-2026-04-22-newman.md](audits/docs-audit-2026-04-22-newman.md) | HIGH | open | Newman | SECURITY.md is 9 months stale; v2 line unacknowledged | 2026-04-22 |
| newman-2026-04-F-2 | [docs-audit-2026-04-22-newman.md](audits/docs-audit-2026-04-22-newman.md) | HIGH | open | Newman | Secret-redaction control (UnsafeReplaceSecrets) undocumented | 2026-04-22 |
| newman-2026-04-F-3 | [docs-audit-2026-04-22-newman.md](audits/docs-audit-2026-04-22-newman.md) | HIGH | open | Newman + Jerry | Scanner posture mismatch: docs say Grype, CI runs Trivy | 2026-04-22 |
| newman-2026-04-F-4 | [docs-audit-2026-04-22-newman.md](audits/docs-audit-2026-04-22-newman.md) | HIGH | open | Newman | Prior security audits orphaned from SECURITY.md | 2026-04-22 |
| newman-2026-04-F-5 | [docs-audit-2026-04-22-newman.md](audits/docs-audit-2026-04-22-newman.md) | HIGH | open | Newman | DelegateTaskTool s12 describes v1 architecture; v2 differs | 2026-04-22 |
| maestro-2026-04-H1 | [docs-audit-2026-04-22-maestro.md](audits/docs-audit-2026-04-22-maestro.md) | HIGH | open | The Maestro | docs/prompts/ does not exist; prompt library implicit | 2026-04-22 |
| maestro-2026-04-H2 | [docs-audit-2026-04-22-maestro.md](audits/docs-audit-2026-04-22-maestro.md) | HIGH | open | The Maestro | No prompt-eval harness; the prompt-as-contract is a vibe | 2026-04-22 |
| maestro-2026-04-H3 | [docs-audit-2026-04-22-maestro.md](audits/docs-audit-2026-04-22-maestro.md) | HIGH | open | The Maestro | No temperature cookbook | 2026-04-22 |
| maestro-2026-04-H4 | [docs-audit-2026-04-22-maestro.md](audits/docs-audit-2026-04-22-maestro.md) | HIGH | open | The Maestro + Newman | Prompt-injection defense (SAFETY_CLAUSE) under-documented | 2026-04-22 |
| jackie-2026-04-F-01 | [docs-audit-2026-04-22-jackie.md](audits/docs-audit-2026-04-22-jackie.md) | HIGH | open | Jackie Chiles | img/its_alive_too.gif has no source, license, or fair-use basis | 2026-04-22 |
| jackie-2026-04-F-02 | [docs-audit-2026-04-22-jackie.md](audits/docs-audit-2026-04-22-jackie.md) | HIGH | open | Jackie Chiles | v1 block in NOTICE is stale and inconsistent with csproj | 2026-04-22 |
| elaine-2026-04-C1 | [docs-audit-2026-04-22-elaine.md](audits/docs-audit-2026-04-22-elaine.md) | CRITICAL | open | Elaine | README pre-built binaries lists platforms release workflow does not produce | 2026-04-22 |
| elaine-2026-04-C2 | [docs-audit-2026-04-22-elaine.md](audits/docs-audit-2026-04-22-elaine.md) | CRITICAL | open | Elaine | README pre-built artifact filenames use v1 naming scheme | 2026-04-22 |
| elaine-2026-04-C3 | [docs-audit-2026-04-22-elaine.md](audits/docs-audit-2026-04-22-elaine.md) | CRITICAL | open | Elaine | README Quickstart points at v1 source tree for .env.example | 2026-04-22 |
| elaine-2026-04-H1 | [docs-audit-2026-04-22-elaine.md](audits/docs-audit-2026-04-22-elaine.md) | HIGH | open | Elaine | IMPLEMENTATION_PLAN.md is a v1.9.0 planning doc; project at v2.0.4 | 2026-04-22 |
| elaine-2026-04-H2 | [docs-audit-2026-04-22-elaine.md](audits/docs-audit-2026-04-22-elaine.md) | HIGH | open | Elaine | ARCHITECTURE.md describes v1 architecture and source layout | 2026-04-22 |
| elaine-2026-04-H3 | [docs-audit-2026-04-22-elaine.md](audits/docs-audit-2026-04-22-elaine.md) | HIGH | open | Elaine | Credential handling model contradicts itself across README and ARCHITECTURE | 2026-04-22 |
| elaine-2026-04-H4 | [docs-audit-2026-04-22-elaine.md](audits/docs-audit-2026-04-22-elaine.md) | HIGH | open | Elaine | README references nonexistent v1.9.x | 2026-04-22 |
| elaine-2026-04-H5 | [docs-audit-2026-04-22-elaine.md](audits/docs-audit-2026-04-22-elaine.md) | HIGH | open | Elaine + Uncle Leo | CODE_OF_CONDUCT vs CONTRIBUTING reporting channels conflict | 2026-04-22 |
| fdr-2026-04-F-1 | [fdr-v2-dogfood-2026-04-22.md](audits/fdr-v2-dogfood-2026-04-22.md) | HIGH | open | FDR + Kramer | Cryptic type-initializer error on Azure HTTP errors | 2026-04-22 |
| fdr-2026-04-F-3 | [fdr-v2-dogfood-2026-04-22.md](audits/fdr-v2-dogfood-2026-04-22.md) | HIGH | open | FDR + Kramer | Ralph agent error swallows exit code (exits 0 on failure) | 2026-04-22 |
| newman-2026-05-F-17 | [security-v2.1.1-reaudit.md](audits/security-v2.1.1-reaudit.md) | INFO | open | Elaine + Kramer | Lint does not analyze data-flow out of heredoc-fed variables (doc/data-flow concern, future episode) | 2026-05-06 |
| fdr-2026-05-A-1 | [security-v2.1.3-allowlist.md](audits/security-v2.1.3-allowlist.md) | MEDIUM | open | FDR + Newman | TOCTOU between DNS pre-flight and TCP connect on compat dispatch path; mitigation lane is a custom SocketsHttpHandler.ConnectCallback pinning the resolved IP (queued S03E17) | 2026-05-12 |
| fdr-2026-05-A-2 | [security-v2.1.3-allowlist.md](audits/security-v2.1.3-allowlist.md) | LOW | open | FDR | IPv4 short-form ("127.1") parser-drift coverage gap in EndpointAllowlist tests; one-line-fix: extend Loopback theory | 2026-05-12 |
| fdr-2026-05-A-3 | [security-v2.1.3-allowlist.md](audits/security-v2.1.3-allowlist.md) | LOW | open | Elaine + Lloyd Braun | env-var name `AZ_AI_LOCAL_PROVIDERS` overstates scope (also opens RFC1918 / link-local / ULA, not only loopback); doc/UX, deferred to S03E17 sweep | 2026-05-12 |
| newman-2026-05-O-1 | [security-v2.1.4-offline.md](audits/security-v2.1.4-offline.md) | LOW | open | Newman + Elaine | `--offline` is a logical gate, not a kernel boundary; doc lane should recommend `unshare -n` / egress firewall pairing for paranoid runs (deferred to S03E27 doc sweep) | 2026-05-19 |
| newman-2026-05-O-2 | [security-v2.1.4-offline.md](audits/security-v2.1.4-offline.md) | INFO | open | FDR + Newman | offline does not close the v2.1.3 TOCTOU lane (`fdr-2026-05-A-1`); cross-reference, closes automatically when A-1 mitigation ships | 2026-05-19 |
| newman-2026-05-O-3 | [security-v2.1.4-offline.md](audits/security-v2.1.4-offline.md) | LOW | open | Frank Costanza + Newman | silent OTLP degrade under `--offline`; add a `--verbose` stderr note when both `AZ_AI_TELEMETRY=1` and offline are set (deferred to S03E27 telemetry sweep) | 2026-05-19 |
| newman-2026-05-R-1 | [s03e25-the-rotation.md](exec-reports/s03e25-the-rotation.md) | LOW | open | Newman + Frank Costanza | Backups accumulate forever -- `env.bak.<ts>[.N]` files pile up in `~/.config/az-ai/`. No automatic pruning policy today (defense: collision-bump, mode 0600). Wire a configurable retention (`AZ_AI_BACKUP_KEEP=N`) + a `--list-backups` / `--prune-backups` surface in a follow-up. Deferred -- not a security regression today, just a hygiene tax. | 2026-05-25 |
| newman-2026-05-R-2 | [s03e25-the-rotation.md](exec-reports/s03e25-the-rotation.md) | LOW | open | Newman + Costanza | Cross-provider atomic rotation not supported -- the operator must invoke `--rotate-creds <p>` once per provider. A future `--rotate-creds all` (or interactive multi-select) would take one backup per file and rewrite all sections in a single rename. Today's loop is correct but verbose. Deferred -- product call, no security gap. | 2026-05-25 |
| newman-2026-05-R-3 | [s03e25-the-rotation.md](exec-reports/s03e25-the-rotation.md) | INFO | open | Newman + Bob Sacamano | OS-keychain integration as a follow-up: today's flow rewrites a plaintext file under mode 0600. Linux Secret Service (`libsecret`) / macOS Keychain / Windows Credential Manager would lift the secret out of the filesystem entirely. Deferred -- crosses platforms, integrations, AOT trim concerns; sized for a dedicated arc, not a hotfix. | 2026-05-25 |
| lloyd-2026-05-L-1 | [s03e19-the-first-hour-local.md](exec-reports/s03e19-the-first-hour-local.md) | LOW | open | Elaine | README compat section implies `AZ_AI_COMPAT_MODELS` URL is user-supplied; today it is preset-baked in `OpenAiCompatAdapter.cs` (lines 55-80) | 2026-05-09 |
| lloyd-2026-05-L-2 | [s03e19-the-first-hour-local.md](exec-reports/s03e19-the-first-hour-local.md) | LOW | open | Elaine | README First-run wizard transcript reads as exhaustive; no "Ollama coming in S03E14" hint for readers tracking local providers | 2026-05-09 |
| lloyd-2026-05-L-3 | [s03e19-the-first-hour-local.md](exec-reports/s03e19-the-first-hour-local.md) | LOW | open | Lloyd Braun | `chmod 600` not co-located with the manual env-file write step in onboarding/local-providers.md or README per-provider block (related: newman-2026-05-K-1) | 2026-05-09 |
| lloyd-2026-05-L-4 | [s03e19-the-first-hour-local.md](exec-reports/s03e19-the-first-hour-local.md) | LOW | open | Newman | Opt-in env var name `AZ_AI_LOCAL_PROVIDERS=1` is referenced by the E19 tutorial but not yet pinned in any ADR; if E16 picks a different name the tutorial needs a sweep on arrival | 2026-05-09 |
| lloyd-2026-05-L-5 | [s03e19-the-first-hour-local.md](exec-reports/s03e19-the-first-hour-local.md) | LOW | open | Elaine | Glossary terms "Quantization" and "Context window" cited by onboarding/local-providers.md step 10 are not in master `docs/glossary.md`; tutorial defines them in-line as a stop-gap | 2026-05-09 |
| jerry-2026-05-J-1 | [s03e24-the-cve-log.md](exec-reports/s03e24-the-cve-log.md) | INFO | open | Jerry | Provider-attribution map (`scripts/provider-deps.json`) does not yet split compat presets (Groq / Together / Cloudflare / OpenAI direct) -- all bucket as `openai`. Add patterns when a per-preset SDK ships. | 2026-05-15 |
| jerry-2026-05-J-2 | [s03e24-the-cve-log.md](exec-reports/s03e24-the-cve-log.md) | LOW | open | Jerry + Newman | `ci.yml::docker` Trivy step still `exit-code: 0` -- provider-attributed report is reporting-only this episode. Promotion to a hard gate (with allowlist + bypass) tracked for S03E25 *The Rotation*. | 2026-05-15 |
| jerry-2026-05-J-3 | [s03e24-the-cve-log.md](exec-reports/s03e24-the-cve-log.md) | LOW | open | Jerry | `scripts/sbom-generate.sh` is a lightweight stand-in (parses `dotnet list package`); the auditor-grade signed CycloneDX SBOM still only ships at tag-time from `release.yml`. Per-PR SBOM is not interchangeable with the release artifact -- documented in `docs/security/sbom.md` section 5. | 2026-05-15 |
| jerry-2026-05-J-4 | [s03e24-the-cve-log.md](exec-reports/s03e24-the-cve-log.md) | INFO | open | Jerry | Local `make cve-report` requires Trivy on PATH; install path documented in Makefile target + `docs/security/cve-policy.md` but not in `CONTRIBUTING.md`. Roll into the next contributor-docs sweep. | 2026-05-15 |
| costanza-2026-05-CG-1 | [s03e18-the-capability-gate.md](exec-reports/s03e18-the-capability-gate.md) | LOW | open | Costanza | Capability matrix is a 2026-05 snapshot in time; provider model rosters churn (Groq tool-call list expanded twice in 90 days). Establish a quarterly review cadence; track upstream changelogs for `openai`, `groq`, `together`, `cloudflare`. | 2026-05-19 |
| costanza-2026-05-CG-2 | [s03e18-the-capability-gate.md](exec-reports/s03e18-the-capability-gate.md) | LOW | open | Costanza + Kramer | `together` and `cloudflare` ship with conservative preset-default only -- no per-model rows. First user override is a smell that we should have known. Land model-specific entries as Sue Ellen's competitive briefings flag the high-traffic deployments. | 2026-05-19 |
| costanza-2026-05-CG-3 | [s03e18-the-capability-gate.md](exec-reports/s03e18-the-capability-gate.md) | INFO | open | Costanza + Frank Costanza | Future enhancement: lightweight HEAD / OPTIONS probe at first call to autodetect tool-calling support per (preset, model), cached in `~/.cache/az-ai/capabilities.json` with TTL. Deferred -- adds a network round-trip on cold path; revisit after S03E19's first-hour latency budget lands. | 2026-05-19 |
| costanza-2026-05-S-1 | [s03e20-the-switch.md](exec-reports/s03e20-the-switch.md) | LOW | open | Costanza + Newman | Per-profile credential alias is still gated on E10's per-provider env-section work; today a profile pins `provider:` but the actual API key still resolves through the global env-var name (`AZUREOPENAIAPI`, `OPENAI_API_KEY`, ...). Wire a profile-scoped credential lookup once the per-provider section work lands a stable `[provider:NAME]` selector that profiles can reference. | 2026-05-19 |
| costanza-2026-05-S-2 | [s03e20-the-switch.md](exec-reports/s03e20-the-switch.md) → resolved in [s03e22-the-default.md](exec-reports/s03e22-the-default.md) | LOW | resolved | Costanza + Kramer | Resolver picks an arbitrary first compat preset when several keys are set (currently `openai > groq > together > cloudflare`). Stable and documented, but a real user with all four keys exported will get a surprising default. Consider a `default_provider` knob in `preferences.json` once the FR-014 schema gains a top-level field. **Resolved 2026-05-22 by S03E22 *The Default* / ADR-011**: documented six-rung heuristic replaces the preset-table walk; multi-endpoint cascade now emits a `multiple-presets-no-cli-no-profile-no-env-pin` warning so the alphabetical tie-break is visible instead of silent. Per-user `default_provider` knob still deferred (preferences-schema FR remains open). | 2026-05-19 |
| costanza-2026-05-S-3 | [s03e20-the-switch.md](exec-reports/s03e20-the-switch.md) | INFO | open | Elaine + Costanza | `--config show` JSON envelope (`ConfigShowJson`) was NOT extended in S03E20 to carry the new "Switch resolution" block -- the text path has it, JSON consumers still see only the legacy `Resolved` dictionary. Land the JSON addition once Elaine's `--config show` schema docs episode opens (deferred to keep AOT JSON context churn minimal). | 2026-05-19 |

## Resolved (last 90 days)

| ID | Source | Severity | State | Owner | Title | Last update |
|---|---|---|---|---|---|---|
| kramer-2026-05-CR-09-F3 | [s03e09-the-compat.md](exec-reports/s03e09-the-compat.md) | LOW | open | Kramer | `OpenAiCompatAdapter.Build()` accepts `HttpClient? http = null` but ignores it -- the OpenAI SDK's `OpenAIClientOptions` does not surface a transport hook without a custom `PipelineTransport`. Documented at S03E09 push, formally ledger'd at S03E17 *The Stream* during streaming-parity audit. Streaming + tool-call parity confirmed via `IChatClient`-seam fakes (`tests/AzureOpenAI_CLI.Tests/CompatStreamingTests.cs`) so this finding does NOT block streaming verification. Closes when a future "recorded-fixture transport" episode either implements a `PipelineTransport` shim or removes the parameter from the signature. | 2026-05-09 |
| kramer-2026-05-CR-09-F4 | [s03e09-the-compat.md](exec-reports/s03e09-the-compat.md) | MEDIUM | resolved | Kenny Bania | PrewarmAsync only warmed Azure-OpenAI / Foundry; compat dispatch was cold on first call (resolved S03E12 *The Receipt* via `Program.PrewarmCompatAsync` -- distinct-preset Build() loop, silent, no network) | 2026-05-08 |
| kramer-2026-05-CR-09-F5 | [s03e09-the-compat.md](exec-reports/s03e09-the-compat.md) | MEDIUM | resolved | Kenny Bania | CostEstimator carried no compat preset rates; compat-routed prompts hit the unknown-model branch (resolved S03E12 *The Receipt* via `Observability.CompatCostRates` placeholder table + `CostEstimator.EstimateForCompatPreset` -- four presets with PLACEHOLDER+TODO markers, redacted fall-through for unknown presets) | 2026-05-08 |
| frank-2026-05-CR-12-PR | [s03e13-the-telemetry.md](exec-reports/s03e13-the-telemetry.md) | MEDIUM | in-progress | Morty Seinfeld + Frank Costanza | `CompatCostRates` ships PLACEHOLDER per-preset rates from S03E12 *The Receipt*; quarterly upstream-pricing review cadence pinned in `docs/observability/slo.md` section 7 (review log seeded with 2026-Q2 initial pin). Stays in-progress until first quarterly cycle confirms the cadence holds. | 2026-05-09 |
| frank-2026-05-SLO-PROPOSED | [s03e13-the-telemetry.md](exec-reports/s03e13-the-telemetry.md) | LOW | open | Frank Costanza + Jerry | SLO charter in `docs/observability/slo.md` is PROPOSED only; no automated alert pipeline. Closes when a future episode wires CI / alerting against the dispatch.success and dispatch.latency.p95 SLIs. | 2026-05-09 |
| newman-2026-05-F-1 | [security-v2.1-post-prompts.md](audits/security-v2.1-post-prompts.md) | CRITICAL | resolved | Kramer + Maestro + Newman | `:aicode` shell injection via form fields (fixed in commit c25ca38) | 2026-05-06 |
| kramer-2026-05-K-1 | self-reported during aidata-collision lint sweep | HIGH | resolved | Kramer | F-1/F-2 class missed on Linux + macOS espanso variants -- :aiimg / :aiweb / :aitone applied the unified-S03 stdin/heredoc fix in this commit | 2026-05-06 |
| elaine-2026-05-C2 | [docs-audit-2026-05-elaine.md](audits/docs-audit-2026-05-elaine.md) | CRITICAL | resolved | Elaine + Kramer | `:aidata` trigger collides across two espanso match files (resolved 2026-05: prompt-templates entry renamed to `:aidataworkflow`; cross-file collision lint added to `scripts/lint-espanso-yml.sh`) | 2026-05-06 |
| newman-2026-05-F-2 | [security-v2.1-post-prompts.md](audits/security-v2.1-post-prompts.md) | HIGH | resolved | Kramer | Four prompt triggers shell-interpolated form fields (fixed in commit c25ca38) | 2026-05-06 |
| wilhelm-2026-05-W-03 | [audit-process-meta-2026-05.md](audits/audit-process-meta-2026-05.md) | MEDIUM | resolved | Wilhelm | Per-agent audits had no template (shipped `docs/audits/_template.md` in sweeps week) | 2026-05-06 |
| newman-2026-05-F-15 | [security-v2.1.1-reaudit.md](audits/security-v2.1.1-reaudit.md) | LOW | resolved | Kramer | Lint coverage gaps for non-`bash` POSIX shells and `<<-'TAG'` -- extended heredoc regex to match `<<-'TAG'` indented variant and added bash-class group (sh, wsl); commit pending | 2026-05-06 |
| newman-2026-05-F-16 | [security-v2.1.1-reaudit.md](audits/security-v2.1.1-reaudit.md) | LOW | resolved | Kramer | Lint false-positive on bash comment lines containing `{{ns.field}}` -- skip comment-only lines in bash cmd bodies before form-substitution check; commit pending | 2026-05-06 |

## Deferred / wontfix

| ID | Source | Severity | State | Owner | Title | Reason | Last update |
|---|---|---|---|---|---|---|---|

(none yet)
