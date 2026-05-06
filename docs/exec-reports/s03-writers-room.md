# Season 3 -- *Writers' Room*

> *Backfilled mid-season per Wilhelm's W-02 finding (`docs/audits/audit-process-meta-2026-05.md`): the retrospective-cadence handoff from the S02 finale never produced this artifact. This file is the fix-forward. It will be kept current going forward as episodes land.*

**Status:** backfilled 2026-05 (mid-season). Source of truth for slate is [`s03-blueprint.md`](s03-blueprint.md); progress snapshot is [`s03-progress-2026-05.md`](s03-progress-2026-05.md).

## Season pitch

End of S02, `azure-openai-cli` is an Azure-OpenAI-native single-shot binary -- excellent at one thing on one provider. S03 is the pivot from *being a tool* to *being a category entrant*: introduce a provider-abstraction seam, ship at least one non-Azure cloud and one local OpenAI-compatible runtime through it, and prove the LOLBin / single-binary / ASCII-clean ergonomics survive the journey. End-state: same binary, three providers, named profiles in the FR-014 preferences file. The seam, not the intelligence -- automatic routing, cost-aware fallback, MCP, and multimodal stay in S04 and beyond. (Paraphrased from `s03-blueprint.md` §Theme statement.)

## Episodes shipped

> **Blueprint-vs-shipped reconciliation (closes `arc4-5-renumber`):**
>
> Wave 6 consumed slots E13/E14 with telemetry + accessibility (which the blueprint had reserved for *Stream* / *Daemon*). Subsequent waves cascaded:
>
> | Blueprint title | File slot shipped | Cast |
> |---|---|---|
> | *Stream* | s03e17 | Kramer (audit) |
> | *Daemon* (Ollama) | absorbed into s03e19 *First Hour Local* | Lloyd |
> | *Probe* | s03e15 ✓ | Costanza |
> | *Allowlist* | s03e16 ✓ | FDR |
> | *Server* (llama.cpp) | s03e21 | Kramer |
> | *Capability Gate* | s03e18 ✓ | Costanza |
> | *Switch* | s03e20 ✓ | Costanza |
> | *Default* (ADR-011) | s03e22 | Costanza |
> | *Fallback* | s03e23 | Frank Costanza |
> | *Persona Multi-Provider* | s03e28 | Kramer |
> | *CVE Log* | s03e24 ✓ | Jerry |
> | *Rotation* | s03e25 ✓ | Newman |
> | *Offline* | s03e26 ✓ | Newman |
> | *Demo* (finale) | s03e27 ✓ | Larry David |
> | *Telemetry* (extra) | s03e13 | Frank Costanza |
> | *Screen Reader* (extra) | s03e14 | Mickey Abbott |
> | *First Hour Local* | s03e19 ✓ | Lloyd |
>
> Blueprint narrative numbering remains the canonical *story* order; this table is the canonical *file* lookup. No file renames are planned -- exec-report URLs are stable historical artifacts.

| Slot | Title | Lead | Verdict | Date |
|------|-------|------|---------|------|
| S03E01 | *The Yada Yada Strikes Back* | Kramer | shipped (audit clean after wave 9) | 2026-05 |
| S03E02 | *The Library Cop's Word Limit* | Lt. Bookman | shipped (tier doctrine + 3 triggers) | 2026-05 |
| S03E03 | *The Docs Audit, Reprise* | Elaine | YELLOW (22 findings: 2C / 11M / 7m / 2n) | 2026-05 |
| S03E04 | *The Mailman Knocks Twice* | Newman | RED (F-1 CRITICAL + F-2 HIGH; patched same sweep, commit `c25ca38`) | 2026-05 |
| S03E05 | *The Auditor's Auditor* | Mr. Wilhelm | YELLOW (50 percent follow-through on prior audit findings) | 2026-05 |
| S03E06 | *The Schema* | Kramer | -- (clean episode) | 2026-05 |
| S03E07 | *The Redactor* | Newman | -- (clean episode) | 2026-05 |
| S03E08 | *The Pick* | Costanza (ADR), Larry David (episode) | -- (decision episode) | 2026-05 |
| S03E10 | *The Keychain* | Newman | GREEN (per-provider env sections + redactor patterns; 2 LOW + 1 INFO findings filed) | 2026-05 |
| S03E09 | *The Compat* | Kramer | -- (clean episode; OpenAiCompatAdapter shipped) | 2026-05 |
| S03E11 | *The Wizard, Reprise* | Jerry | GREEN (provider-aware wizard, env-file writer, 32 unit tests + 5 integration; closes K-1 chmod-600 README gap) | 2026-05 |
| S03E12 | *The Receipt* | Kenny Bania | -- (bench harness + compat prewarm + compat cost rates; closes CR-09 F4 + F5) | 2026-05 |
| S03E13 | *The Telemetry* | Frank Costanza | GREEN (opt-in `AZ_AI_TELEMETRY=1` emitter + initial SLO charter; pricing-review cadence pinned) | 2026-05 |
| S03E16 | *The Allowlist* | FDR | GREEN (SSRF endpoint allowlist seam; 57 adversarial cases; `AZ_AI_LOCAL_PROVIDERS=1` opt-in; 3 forward-hardening findings filed) | 2026-05 |
| S03E19 | *The First Hour, Local Edition* | Lloyd Braun | -- (docs-only; tutorial `docs/onboarding/local-providers.md`, 5 lloyd-2026-05-L-* findings opened against forward-looking docs and adjacent README/glossary gaps) | 2026-05 |
| S03E20 | *The Switch* | Costanza | -- (precedence chain codified in `Preferences.Resolve()`; `--provider` / `--profile` / `--model` flags; `--config show` source field; 44 unit + 6 integration; 2 costanza-2026-05-S-* findings filed for shadow follow-ups) | 2026-05 |
| S03E14 | *The Screen Reader* | Mickey Abbott | GREEN (`--plain` flag + `Plain.cs` chokepoint; 18-site ASCII glyph audit; `NO_COLOR` / `TERM=dumb` / `AZ_AI_PLAIN` honored; 28 unit + 6 integration tests) | 2026-05 |
| S03E15 | *The Probe* | Costanza | GREEN (`az-ai --doctor` subcommand; DNS + creds-presence + model-count probe across azure / foundry / compat presets; never emits credential values; 21 unit tests + 3 integration; exit 0/1) | 2026-05 |
| S03E17 | *The Stream* (originally blueprint E13; renumbered: telemetry / a11y / doctor / allowlist / local-providers consumed E13-E16 + E19) | Kramer | -- (verification episode; 15 streaming + tool-call parity facts; no production code change; ledger'd existing HttpClient finding as `kramer-2026-05-CR-09-F3`, left open pending recorded-fixture episode) | 2026-05 |
| S03E18 | *The Capability Gate* | Costanza | GREEN (provider+model feature matrix + dispatch-time gate; refuses tool-call / vision requests to incompatible models with friendly error + exit 2; `AZ_AI_CAPABILITY_OVERRIDES` escape hatch; telemetry `error_class=CapabilityMismatch`; +33 unit + 5 integration; 3 LOW/INFO findings filed `costanza-2026-05-CG-1..3`) | 2026-05 |
| S03E24 | *The CVE Log, Per Provider* | Jerry | GREEN (provider-attributed Trivy pipeline; `make cve-report`; per-provider severity tolerances at `docs/security/cve-policy.md`; reporting-only -- hard gate deferred to S03E25 *The Rotation*) | 2026-05 |
| S03E25 | *The Rotation* | Newman | GREEN (`--rotate-creds [provider]` BYOK flow; atomic write + collision-bumped timestamped backup + mode 0600 invariant verified post-rename; reuses extracted `WizardSession.{BackupWithBump,AtomicWrite,SetRestrictivePermissions}` and shared `Cli/MaskedInput` (Newman H-1 preserved); rotated key never logged; refuses `--raw` / non-TTY; +35 unit + 6 integration; 3 LOW/INFO findings filed `newman-2026-05-R-1..3`) | 2026-05 |
| S03E22 | *The Default* (file slot 22) | Costanza | GREEN (six-rung default-provider heuristic codified in ADR-011; replaces the ad-hoc preset-table walk in `PreferencesResolver.ResolveDefaultProvider` with deterministic rungs returning a stable `default:azure` / `default:<preset>` / `default:<preset>:local-detected` / `default:openai` / `default:azure:fallback` source label; URL-string loopback match only -- ProviderDoctor still owns the live probe; tie-break across presets emits `multiple-presets-no-cli-no-profile-no-env-pin` warning; `SnapshotEnv()` extended additively with `AZUREOPENAIAPI` + `AZ_AI_LOCAL_PROVIDERS` + `AZ_AI_<PRESET>_ENDPOINT` family; +36 unit + 6 integration; resolves `costanza-2026-05-S-2` (substantive match -- brief named `S-3` which is the unrelated JSON-envelope finding); 7 of the 44 e20 resolver assertions updated to reflect new label semantics, documented as ADR-011 § Migration) | 2026-05 |
| S03E26 | *The Offline Mode* | Newman | GREEN (`--offline` + `AZ_AI_OFFLINE=1` strict-equality env; six gated network seams: Azure SDK / Foundry SDK / OpenAI-compat / WebFetchTool / OTLP exporter / prewarm probe; `BlockOffline` verdict in EndpointAllowlist; layered with `AZ_AI_LOCAL_PROVIDERS=1` (offline does NOT relax loopback opt-in); +30 unit cases + 7 integration assertions; 3 LOW/INFO findings filed `newman-2026-05-O-1..3`) | 2026-05 |
| S03E17 | *The Server* (file slot 21 -- E17 title slot 17 already burned by *The Stream*; new file at `s03e21-the-server.md`) | Kramer | GREEN (llama.cpp `llama-server` OpenAI-compat preset `llamacpp` -> `http://localhost:8080/v1`; runtime endpoint override via `AZ_AI_LLAMACPP_ENDPOINT`, optional Bearer via `AZ_AI_LLAMACPP_API_KEY` (`RequiresApiKey=false` because llama-server is unauth by default), model resolution via `AZ_AI_LLAMACPP_MODEL` -> `DefaultModel="llamacpp"`; Capability=Conservative (no tool_calls / vision / json_mode -- streaming yes); reuses S03E16 loopback gate (`AZ_AI_LOCAL_PROVIDERS=1` required) and S03E18 capability gate (`AZ_AI_CAPABILITY_OVERRIDES` escape hatch); auto-discovered by ProviderDoctor when routed via `AZ_AI_COMPAT_MODELS=llamacpp:<model>`; +25 unit facts in `LlamaCppPresetTests.cs` + 4 integration assertions in `tests/integration_tests.sh`; one finding filed `kramer-2026-05-LCPP-1` (model-name resolution is honored at `OpenAiCompatAdapter.Build` seam but not at `Preferences.ResolveDefaultModel` -- fine for `--model llamacpp` happy path, surfaces as a gap when operators rely on Preferences-side resolution)) | 2026-05 |
| S03E22 | *The Fallback* (file slot 23 -- e21 *The Default* claimed slot 22; new file at `s03e23-the-fallback.md`) | Frank Costanza | GREEN (opt-in best-effort fallback chain; `--fallback <list>` flag + `AZ_AI_FALLBACK` env (CLI wins); new `Resilience/FallbackPolicy.cs` parses + validates (max-depth=3, no dups, known-presets gate: azure/foundry/openai/groq/together/cloudflare/ollama); new `Resilience/FallbackChain.cs` decorator wraps `IChatClient` post-`BuildChatClient`; transient (5xx/429/timeout) advances chain, auth/4xx-non-429/CapabilityMismatch/user-cancel short-circuit; **stream invariant**: post-first-chunk failure prints `[fallback] stream-truncated` warn + re-throws (never switches mid-flight); two new additive telemetry events `fallback_attempt` + `fallback_outcome` under existing `AZ_AI_TELEMETRY=1` strict-equality gate, stable key order, redacted+bounded `error_class`; new SLIs in `docs/observability/slo.md` (`fallback.rate` ≤5%/28d, `fallback.recovery_rate` ≥80%, `fallback.exhaustion_rate`, `fallback.stream_truncated_rate`) + alert thresholds (info >5%/1h, page >20%/15m); **production factory always Skipped("no-fallback-creds")** -- per-preset cred discovery filed as `frank-2026-05-FB-1`; `--config show` echo filed as `frank-2026-05-FB-2`; Wrap is no-op pass-through when policy inactive (zero overhead, zero behaviour change for users who don't opt in); +47 unit facts in `FallbackChainTests.cs`, +6 integration assertions in `▸ S03E22 fallback chain` section) | 2026-05 |
| S03E23 | *The Persona, Multi-Provider* (file slot 28 -- prior slots 22/23/24/25/26 already burned; new file at `s03e28-the-persona-multi-provider.md`) | Kramer | GREEN (per-persona `provider` / `model` pins in `.squad.json`; new precedence rung **cli > env > profile > persona > default** in `PreferencesResolver.Resolve` (the rung's record fields had been pre-staged in Wave 9 and are now wired to a real caller); new `SquadCoordinator.ApplyPersonaPin(baseInputs, persona, env, warnSink)` helper folds the pin into `ResolutionInputs` and drops the pin + warns when creds are missing for the pinned provider (uses `IsKnownProvider` + `GetCredEnvVarName` from e21 -- no duplicated provider list); new `SquadConfig.Validate()` runs at `SquadConfig.Load()` and rejects unknown providers with a message naming the persona, the bad value, the source path, and the known-providers list; capability gate (S03E18), endpoint allowlist (S03E16), and offline gate (S03E26) all unchanged -- the pin is NOT an escape hatch; persona memory `.squad/history/<name>.md` 32 KB cap unchanged; +42 unit facts in `PersonaProviderPinTests.cs` (precedence ladder × 11, Validate gate × 8, ApplyPersonaPin missing-creds × 8, end-to-end Resolve × 2, capability/offline cross-checks × 4, round-trip × 1, Theory expansion × 7); +5 integration assertions in `▸ S03E23 persona pin` block; two findings filed `kramer-2026-05-PMP-1` (provider-pin dispatch still piggybacks on operator-set `AZ_AI_COMPAT_MODELS` -- no env-rewrite shim) and `kramer-2026-05-PMP-2` (`--config show` does not yet echo the persona rung)) | 2026-05 |
| S03E27 | *The Demo* (season finale) | Larry David (showrunner, solo) | GREEN (5-act mock-only end-to-end demo at `scripts/demo/season3-finale.sh` with 22 asserted invariants, rc=0; no `.cs` change; `scripts/demo/README.md` (record + replay), `docs/exec-reports/s03e27-the-demo.md` (Larry-voice cold open + full-season retrospective + Lessons + Season-4 tag scene), `docs/season-recaps/season-3-recap.md` (Peterman-pluckable marketing recap, arc-by-arc, By-the-numbers stat block); CHANGELOG `[Unreleased] / Added` + README `Demo` subsection wired; ASCII-only banners; idempotent + cleanup trap; gates gracefully when binary is missing or pre-S03) | 2026-05 |

**Season closed.** S03 ships at 27 / 27 episodes (E01-E27). The same
binary that opened the season as Azure-only now speaks 8 provider
presets across 2 local runtimes + Azure / Foundry / OpenAI direct,
with `--provider`, `--profile`, `--doctor`, `--rotate-creds`,
`--plain`, `--fallback`, `--offline`, opt-in NDJSON telemetry, a
deterministic six-rung default heuristic (ADR-011), and a
mock-only finale demo that proves the arc end-to-end. Hand-off to
S04 (lock-in for v3.0, OS keychain, autodetect capability probe,
multi-tenant prep, cloud setup-steps, recorded asciinema cast)
in [`docs/season-recaps/season-3-recap.md`](../season-recaps/season-3-recap.md)
and the S03E27 exec report's tag scene. Curtain.

## Active findings

Tracked in-line until a season-wide findings backlog ships (per `findings-backlog` skill; S03 backlog file not yet seeded).

- **W-02 (audit-process-meta-2026-05):** S03 writers' room missing -- *closed by this file*.
- **C1 (docs-audit-2026-05-elaine):** README install table pinned to v2.0.5 -- *closed in post-sweep cleanup batch*.
- **M5 (docs-audit-2026-05-elaine):** CHANGELOG `[Unreleased]` empty despite shipped commits -- *closed in post-sweep cleanup batch*.
- **C2 (docs-audit-2026-05-elaine):** `:aidata` trigger collision between unification set and prompt-templates set -- *resolved in commit `215b2d3` (Linux/macOS heredoc port + `:aidata` rename)*.
- **F-1 / F-2 (security-v2.1-post-prompts):** bash injection in `ai-prompts.yml` -- *resolved; Newman v2.1.1 re-audit confirms RED -> GREEN closure (`docs/audits/security-v2.1.1-reaudit.md`, commit `a4de7bd`)*.
- **W-01 (audit-process-meta-2026-05):** findings-backlog gate not wired into preflight -- *resolved in commit `de478d2` (findings-backlog gate wired into `make exec-report-check`)*.
- **Audit follow-through gap (audit-process-meta-2026-05):** 50 percent of sampled top-3 findings from 2026-04-22 docs-audit set still unactioned -- **open**, ownership Mr. Wilhelm + Mr. Pitt.

## Arc status

Arc 1 (E01-E02 shipped, plus the displaced spine) **closed**. Arc 1.5 (sweeps-week audit triple, E03-E05) **closed**. Arc 2 (First Non-Azure Cloud, E06-E13) **in flight** -- E06 *The Schema* (drawer), E07 *The Redactor* (lock), and E08 *The Pick* (decision: OpenAI direct, ADR-010) shipped this push; E09-E13 dispatched per ADR-010 §Implementation, leads named (Kramer / Newman / Jerry / Morty / Puddy in narrative order). Velocity posture revised from prior file: *on-track on theme, recovering on velocity, lead-cast quotas closing* -- Costanza took E08 and Newman led E07, leaving Jerry and the supporting bench as the next quotas to honour.

## Open questions for next mid-season checkpoint

- **Anthropic deferral risk (Sue Ellen):** ADR-010 defers Anthropic to S03 Arc 4 / S04 Arc 1 via placeholder FR-024. Sue Ellen owns the comms ledger and the competitive update that lands alongside FR-024. Risk: user-complaint volume between now and FR-024 landing. Mitigation: documented deferral, named owner, scheduled trigger (post-E13). Revisit at E13 sign-off.
- **JSON-quote round-trip (E07 open question #2):** Maestro and Frank Costanza co-own; 30-day clock from S03E07 push date. Failure mode: a JSON-encoded log line containing a redacted secret is consumed by a downstream parser that strips the redaction mask. Owner check-in due before E13 ships.
- **Findings backlog file:** the `findings-backlog` skill defines the format but the S03 backlog file has not been seeded. Owner unassigned. Candidate: Mr. Wilhelm (continuing from the meta-audit). The W-01 wiring (commit `de478d2`) is the gate; the file itself is the next missing piece.
- **Cast quotas (writers-room-cast-balance):** Costanza and Newman have now led S03 episodes (E08 and E04/E07 respectively). Jerry is still un-led at S03E08 -- queued for S03E11 *The Wizard, Reprise*. Supporting bench (Sue Ellen, Mickey, Babu, Russell, Lloyd Braun, Rabbi Kirschbaum) leads remain open; FR-024 is one candidate for Sue Ellen to lead authoring.

## Cross-references

- [`s03-blueprint.md`](s03-blueprint.md) -- canonical 27-episode slate
- [`s03-progress-2026-05.md`](s03-progress-2026-05.md) -- mid-season exec progress report
- [`s02-writers-room.md`](s02-writers-room.md) -- prior-season structural reference
- [`docs/audits/audit-process-meta-2026-05.md`](../audits/audit-process-meta-2026-05.md) -- W-02 source finding
- [`.github/skills/writers-room-cast-balance.md`](../../.github/skills/writers-room-cast-balance.md) -- audit cadence
- [`.github/skills/findings-backlog.md`](../../.github/skills/findings-backlog.md) -- finding format spec
