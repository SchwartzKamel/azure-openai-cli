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

## Resolved (last 90 days)

| ID | Source | Severity | State | Owner | Title | Last update |
|---|---|---|---|---|---|---|
| kramer-2026-05-CR-09-F4 | [s03e09-the-compat.md](exec-reports/s03e09-the-compat.md) | MEDIUM | resolved | Kenny Bania | PrewarmAsync only warmed Azure-OpenAI / Foundry; compat dispatch was cold on first call (resolved S03E12 *The Receipt* via `Program.PrewarmCompatAsync` -- distinct-preset Build() loop, silent, no network) | 2026-05-08 |
| kramer-2026-05-CR-09-F5 | [s03e09-the-compat.md](exec-reports/s03e09-the-compat.md) | MEDIUM | resolved | Kenny Bania | CostEstimator carried no compat preset rates; compat-routed prompts hit the unknown-model branch (resolved S03E12 *The Receipt* via `Observability.CompatCostRates` placeholder table + `CostEstimator.EstimateForCompatPreset` -- four presets with PLACEHOLDER+TODO markers, redacted fall-through for unknown presets) | 2026-05-08 |
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
