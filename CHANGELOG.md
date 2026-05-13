# Changelog

All notable changes to Azure OpenAI CLI will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- **feat(registry):** S04E01 *The Registry* -- typed model registry seam.
  New `azureopenai-cli/Registry/` namespace: `ModelCapability` (validator
  + allowed-tag set), `ModelRegistryEntry` (typed record), `ModelRegistry`
  (loader). Embedded `registry.json` seed (3 entries: `gpt-4o-mini`,
  `gpt-5.4-nano`, `llama-local`). User override at
  `~/.config/az-ai/registry.json` *replaces* the seed (does not merge --
  intentional: keeps offline behavior deterministic). Unknown capability
  tags fail at startup with rc=99 and a list of allowed tags. Card paths
  with no file emit `[WARN]` but are non-fatal. `--doctor` gains a new
  `[registry]` section listing each model with provider + configuration
  status + capability tags. `--raw` suppresses the section.
- **docs(model-cards):** New `docs/model-cards/` -- card format spec,
  three seed cards (`azure-gpt-4o-mini`, `azure-gpt-5.4-nano`,
  `local-llama`), plus a junior-lens onboarding review
  (`REVIEW-onboarding.md`, 30 observations).
- **docs(adr):** New `docs/adr/ADR-012-model-registry-seam.md` with
  adversarial review appendix (9 findings -- 0 critical, 1 high latent,
  4 medium, 3 low, 1 nit).
- **docs(i18n):** New `docs/i18n/` -- Japanese, Chinese (Simplified),
  Spanish, and Korean quick-starts. Off-roster episode brief at
  `docs/episode-briefs/s04off1-the-translation.md`.
- **test(registry):** New `tests/AzureOpenAI_CLI.Tests/RegistryTests.cs`
  (7 facts: happy-path, unknown-tag rc=99, missing-cardPath warn, empty
  override, override-replaces-seed, serialization round-trip, no-fetch).
- **test(i18n):** New `tests/AzureOpenAI_CLI.Tests/I18n/CjkRoundTripTests.cs`
  (29 round-trip facts across ja/zh/es/ko; validates UTF-8 console +
  `<InvariantGlobalization>true</>` + NFKC path normalization).

- **feat(registry):** S04E02 *Embedded Cards* -- `ModelCard` reader.
  New `azureopenai-cli/Registry/ModelCard.cs` typed record. New
  `ModelRegistry.ReadCard(cardPath, registryDir)` and `LoadCards(...)`
  bulk API parse YAML-style front matter (manual parser -- no YAML
  dependency, AOT-safe) from `docs/model-cards/*.md`. Three safety
  guards inline: F-01 path-prefix (rejects `..` traversal after
  canonicalisation), F-03 size cap (256 KB max), F-04 stream-type
  via `libc stat()` P/Invoke (.NET `FileAttributes` does not flag
  FIFOs/devices on Linux). Five new unit tests in `RegistryTests.cs`.
- **feat(--doctor):** S04E02 -- registry section now shows description
  + status columns. Each model row gets a card status (`active`,
  `preview`, `deprecated`, or `(no card)`) and a description (truncated
  to 60 chars with ASCII `...`). Capability tags wrap to a second line
  (`caps: ...`) when a description is present; inlined when not. New
  `AZ_AI_REGISTRY_DIR` env-var as operator escape hatch for card
  resolution. All user-supplied strings pass through `SanitizeForTerminal`.
  `--raw` still suppresses the entire section. Three seed cards now
  carry `description` + `status` front matter.
- **docs(model-cards):** S04E02 -- Lloyd Braun top-3 onboarding
  fix-forward: ADR-012 gains a "What is a seam?" sidebar; README.md
  step 4 gains a `> [!IMPORTANT]` admonition warning that new cards
  must also be registered in `registry.json`; new `## Glossary`
  section in README.md defining 7 jargon terms (embedded resource,
  capability tags, GGUF, quantisation, chat template, Espanso,
  streaming) with ADR cross-references. Sweeps 7 of 27 remaining
  Lloyd findings beyond the top 3.
- **docs(adr):** ADR-012 gains a Wave 2 adversarial review appendix
  (FDR, S04E02): 10 findings (1 critical CLOSED, 1 high, 4 medium,
  2 low, 2 nit). Critical F-EE-01 closed in this same release; the
  rest are filed for S04E03+ triage.
- **test(doctor):** New `tests/AzureOpenAI_CLI.Tests/DoctorRegistryTests.cs`
  (Puddy, S04E02) -- 5 integration facts covering the `--doctor`
  registry section: description-per-seed-card, `--raw` suppression,
  terminal-injection scrubbing via crafted override, override-replaces-
  seed for cards too, missing-card renders `(no card)` without crash.
- **test(a11y):** New `tests/AzureOpenAI_CLI.Tests/DoctorRegistryAccessibilityTests.cs`
  (Mickey, S04E02) -- 4 a11y facts: `NO_COLOR` honored (zero ANSI
  escapes), zero tab characters in the registry block, every model
  row leads with the model name (screen-reader-friendly), capability
  tags ASCII-only (braille-display-safe). Mickey's polish observations
  for E04 *Reading Room* filed in `docs/model-cards/REVIEW-onboarding.md`.
- **docs(s04):** S04 living running-order published in
  `docs/exec-reports/s04-blueprint.md` (Mr. Pitt) -- reconciles
  blueprint episode numbering with shipped reality (E01+E02 of original
  numbering collapsed into shipped E01) and projects E03-E09 cast
  assignments with cast-balance ledger.
- **docs(s04e03):** Episode brief for S04E03 *The Capabilities*
  drafted (Bookman) -- DRAFT status, awaiting showrunner greenlight.

### Changed
- **ci(release):** S04SP2 fix-forward -- `printf '- Homebrew ...'` in
  the `Build release body` step of `.github/workflows/release.yml` was
  parsing the leading `-` as a bash-builtin printf option flag,
  exiting rc=2 with `printf: - : invalid option` and failing every
  release run since the S03E30 *Audit Trilogy* rewrite. This was the
  real cause of the v2.2.0 -> silence gap; the `macos-13` queue
  starvation that SP1 hit was masking it. Four `printf` calls in the
  package-manager-install block now use `printf -- '- ...'` to
  terminate option parsing. Re-tagged `v2.3.0` once more on top of
  the SP1 base (`ffd2c1a`) to pick up the fix; still no published
  Release object at the prior SHA so no artifact contract was broken.
- **docs(release):** S04SP2 *The Stenographer* -- release-hygiene
  audit on top of SP1's matrix-drop retag. Verified the matrix-driven
  artifact table in `.github/workflows/release.yml` (lines 216-223)
  enumerates only the six shipping legs (`linux-x64`, `linux-musl-x64`,
  `linux-arm64`, `win-x64`, `win-arm64`, `osx-arm64`) -- no
  `macOS Intel` row, no `osx-x64` artifact path. Confirmed README.md
  line 435 still directs Intel-Mac users to the Docker image or
  source build per the v2.0.4 policy. No code or workflow changes;
  audit-only special. See `docs/exec-reports/s04sp2-the-stenographer.md`.

### Security
- **fix(--doctor):** Terminal-injection guard on registry output.
  User-supplied `Name`, `Provider`, and capability tag strings from
  `~/.config/az-ai/registry.json` are now scrubbed of C0/C1 control
  characters (incl. ESC/CSI/OSC) before being printed by `--doctor`.
  Printable Unicode (CJK, emoji, accented chars) passes through
  unchanged. Closes FDR finding F-02 (S04E01 Wave 2 adversarial review).
- **fix(registry):** S04E02 hotfix -- close F-EE-01 parent-directory
  symlink prefix bypass in `ModelRegistry.ReadCard` (CRITICAL, FDR
  S04E02 Wave 2). `Path.GetFullPath` collapses `..` segments lexically
  only and does not resolve symlinks anywhere along the path; an
  attacker who could drop a symlink at any *parent* directory of a
  card path (e.g. `<registryDir>/sub -> /etc`) defeated the F-01
  prefix guard and gained a read-arbitrary-file primitive as the
  `az-ai` user. Mitigation: canonicalise both `registryFull` and
  `resolved` through `realpath(3)` on Linux (new `LibcRealpath`
  P/Invoke alongside the existing `LibcStat` seam) -- with a per-
  ancestor `Directory.ResolveLinkTarget(returnFinalTarget: true)`
  walk as the cross-platform fallback -- before re-running
  `StartsWith(canonicalRegistryDir)`. macOS via the ancestor walk
  is best-effort (tracked as F-EE-05 in ADR-012). New regression
  tests: `ReadCard_ParentDirectorySymlink_ExitsRc99`,
  `ReadCard_LeafSymlinkOutsideDir_ExitsRc99`.

## [2.3.0] -- 2026-05-13

### Added
- **ci(release):** S03E30 *The Audit Trilogy* -- expanded release matrix
  to 7 legs (linux-x64, **linux-arm64** via `ubuntu-24.04-arm`, osx-x64
  on `macos-13`, osx-arm64, win-x64, **win-arm64** cross-published).
  Release notes are now extracted from `CHANGELOG.md` `[Unreleased]`
  via awk into `release-body.md` (replaces `generate_release_notes`).
  Per-asset SHA256 digests aggregated into `digests.txt`, attached to
  the release, and embedded in the body for offline verification.
- **ci(security):** New `.github/workflows/codeql.yml` CodeQL SAST
  workflow for csharp -- push + PR + weekly schedule, SHA-pinned
  actions, manual `dotnet build` (no autobuild for AOT/trim project),
  `security-events: write` + concurrency + 30-min timeout.
- **build(reproducibility):** `<Deterministic>true</>` plus
  `<ContinuousIntegrationBuild>` (gated on `CI`/`GITHUB_ACTIONS` env)
  in `azureopenai-cli/AzureOpenAI_CLI.csproj` for reproducible AOT
  output.
- **docs(process):** New `docs/process/release.md` 6-section release
  runbook (precheck, promote `[Unreleased]`, tag+push, CI takeover,
  post-release verification, open next `[Unreleased]`).
- **build(make):** `make release-precheck` (5 gates: clean tree,
  `[Unreleased]` non-empty, csproj > latest tag, README version
  reference, no open CRITICAL/HIGH findings) and
  `make release-notes-preview` -- both wired into `.PHONY` and
  `make help`.
- **test(fallback):** 4 new `FallbackParser_*` unit facts in
  `FallbackChainTests.cs` asserting `--fallback` parser exits with
  the right `HasError` *before* requiring credentials.

### Changed
- **ci(release):** S04SP1 *The Reruns* -- dropped `osx-x64/macos-13`
  leg from `release.yml` matrix (and matching artifact-table row).
  Re-establishes the v2.0.4 policy after the leg was silently re-added
  in a later PR. macOS Intel hardware is EOL; users on legacy hardware
  fall back to the Docker image or local source build (README line 435
  has documented this path since v2.0.4). v2.3.0 tag force-moved from
  `493c21b` to `ffd2c1a` (cherry-picked matrix fix on top of the
  original commit) -- no published Release object existed at the old
  SHA, so no artifact contract was broken. See
  `docs/exec-reports/s04sp1-the-reruns.md`.
- **docs(exec-reports):** S04SP1 fix-forward on three bullet-style
  errors (`MD004/MD032`) in already-shipped exec-reports
  (`s04e01-the-registry.md`, `s04e02-embedded-cards.md`) -- `+ ` prose
  continuations (English word "plus") were being parsed as
  unordered-list bullets of the wrong style, reddening `docs-lint` on
  every push since 2026-05-13 S04E01 close.
- **ci(security):** SHA-pinned `actions/checkout@11bd71901bbe...` (v4.2.2)
  and `actions/setup-node@49933ea52888...` (v4.4.0) in
  `.github/workflows/docs-lint.yml`.
- **ci(security):** Added `concurrency:` block + 15-min `timeout-minutes`
  to `.github/workflows/scorecards.yml` (was unbounded).
- **fix(cli):** S03E30 `--fallback` parser now fires alongside
  `--help`/`--version`/`--doctor`, *before* the `AZUREOPENAIENDPOINT`
  creds gate. Approach (a) -- lift `FallbackPolicy.Resolve` (purely
  syntactic, no shim needed). The S03E29 `fb_dummy_env` integration
  test workaround is removed; tests 2/3/4 in the S03E22 fallback
  block now run with no creds in env. 111/111 integration tests green.
- **docs:** Agent count drift in `README.md`: `25` -> `28`
  (1 showrunner + 5 main + 22 supporting per AGENTS.md).
- **docs(security):** `SECURITY.md` supported-versions table corrected
  -- 2.2.x active / 2.1.x maintenance / 2.0.x EOL / <2.0 unsupported
  (was incorrectly listing 2.0.x as active).

- **feat(squad):** S03E23 *The Persona, Multi-Provider* (Kramer; file slot
  s03e28) -- per-persona `provider` and/or `model` pins in `.squad.json`.
  When a persona is invoked AND it declares a `provider` and/or `model`
  field, those values flow through `PreferencesResolver.Resolve()` as a
  new precedence rung between profile and default: **cli > env > profile
  > persona > default**. CLI flags, `AZ_PROVIDER` / `AZ_MODEL`, and
  `--profile` pins still win every time -- the persona rung only fires
  when no higher rail resolved. Validation is up-front: an unknown
  provider in `.squad.json` (anything not in
  `[azure, foundry, openai, groq, together, cloudflare, llamacpp]`) is
  rejected at config-load with an actionable error naming the persona,
  the bad value, the source path, and the known providers list. Missing
  creds for a pinned provider (per
  `PreferencesResolver.GetCredEnvVarName`) drop the pin and emit a single
  `[persona:NAME] pinned provider 'X' has no credentials in Y; falling
  through to the global default-provider chain` warning to stderr (silent
  under `--raw` / `--json`); the persona's `model` pin survives and the
  persona memory file still loads. Capability gate (S03E18), endpoint
  allowlist (S03E16), and offline gate (S03E26) all stay in front of the
  dispatch path -- a persona pin is not an escape hatch. New helper
  `SquadCoordinator.ApplyPersonaPin(baseInputs, persona, env, warnSink)`
  is the public seam; pure, no Console writes, no env reads outside the
  passed-in snapshot. New source labels: `persona:<name>:provider` and
  `persona:<name>:model`. 42 new unit facts in
  [`tests/AzureOpenAI_CLI.Tests/PersonaProviderPinTests.cs`](tests/AzureOpenAI_CLI.Tests/PersonaProviderPinTests.cs)
  (precedence ladder + Validate gate + ApplyPersonaPin missing-creds +
  end-to-end Resolve round-trip + capability/offline gate cross-checks);
  5 new integration assertions
  (`tests/integration_tests.sh ▸ S03E23 persona pin`). Two findings filed
  [`kramer-2026-05-PMP-1`](docs/findings-backlog.md) (persona-pin
  dispatch routing relies on operator-set `AZ_AI_COMPAT_MODELS` -- no
  env-rewrite shim) and
  [`kramer-2026-05-PMP-2`](docs/findings-backlog.md) (`--config show`
  does not yet echo the persona rung).
- **docs(s03e27):** S03E27 *The Demo* (Larry David, solo) -- Season 3
  finale curtain call. New mock-only end-to-end demo at
  [`scripts/demo/season3-finale.sh`](scripts/demo/season3-finale.sh):
  five acts (Setup / Switch / Rules / Fallback / Curtain Call), 22
  asserted invariants, ASCII-only bordered banners, idempotent with
  cleanup trap, gates gracefully when the `az-ai` on PATH is missing
  or pre-S03 (exits 0 with a "build az-ai first" message so CI does
  not flap). The script exercises `--doctor`, `--rotate-creds --help`,
  `--config show` (default + `--provider` override + `AZ_PROFILE` from
  a throwaway `preferences.json`), `--fallback bogus` rejection (rc=2
  with the known-presets list), `--fallback openai,groq` parse,
  `AZ_AI_FALLBACK` env recognition, `AZ_AI_OFFLINE=1` short-circuit,
  and `AZ_AI_TELEMETRY=1` NDJSON emission to stderr -- all without a
  single real provider call. Companion docs:
  [`scripts/demo/README.md`](scripts/demo/README.md) (prereqs, run,
  asciinema record + replay recipe),
  [`docs/exec-reports/s03e27-the-demo.md`](docs/exec-reports/s03e27-the-demo.md)
  (Larry-voice exec report + full-season retrospective + S04 tag
  scene), and
  [`docs/season-recaps/season-3-recap.md`](docs/season-recaps/season-3-recap.md)
  (marketing-grade season recap, arc-by-arc prose + "By the numbers"
  stat block). Zero `.cs` changed -- the finale is a curtain call,
  not a feature. Season 3 closes at 27 / 27 episodes.
- **feat(resilience):** S03E22 *The Fallback* (Frank Costanza) -- opt-in
  best-effort fallback chain wrapping the primary chat client. New flag
  `--fallback <list>` (and env `AZ_AI_FALLBACK`, CLI wins) accepts a
  comma-separated chain of preset names (max 3 alternates, no
  duplicates, known presets only: azure / foundry / openai / groq /
  together / cloudflare / ollama). On a *transient* primary failure
  (5xx / 429 / network timeout) the chain is tried in order; *non-transient*
  failures (auth 401/403, other 4xx, `CapabilityMismatchException`,
  user-cancel via Ctrl-C) short-circuit and never trigger fallback.
  Streaming carries a load-bearing invariant: once the first chunk has
  been yielded, fallback is OFF -- mid-stream primary failure prints a
  one-line `[fallback] stream-truncated` warn to stderr and re-throws,
  because switching providers mid-flight would corrupt the transcript.
  Telemetry is additive and opt-in (existing `AZ_AI_TELEMETRY=1` strict-
  equality gate): two new event shapes `fallback_attempt` and
  `fallback_outcome` with stable key order, no prompts/completions/
  endpoints, `error_class` routed through `SecretRedactor` and bounded
  to 200 chars. New SLIs in [docs/observability/slo.md](docs/observability/slo.md):
  `fallback.rate` (target ≤ 5% / 28d), `fallback.recovery_rate`,
  `fallback.exhaustion_rate`, `fallback.stream_truncated_rate`; alert
  thresholds *info* > 5% / 1h, *page* > 20% / 15m. Default is OFF: the
  wrap is a no-op pass-through when policy is inactive, zero overhead,
  zero behaviour change for users who don't opt in. Production
  `AlternateChatClientFactory` currently always returns
  `Skipped("no-fallback-creds")` -- per-preset cred discovery for
  alternates is finding [`frank-2026-05-FB-1`](docs/findings-backlog.md).
  47 new unit facts (`tests/AzureOpenAI_CLI.Tests/FallbackChainTests.cs`),
  6 new integration assertions
  (`tests/integration_tests.sh ▸ S03E22 fallback chain`).
- **feat(compat):** S03E17 *The Server* (Kramer) -- OpenAI-compat preset
  for [llama.cpp's `llama-server`](https://github.com/ggml-org/llama.cpp/tree/master/tools/server).
  New built-in preset `llamacpp` points at `http://localhost:8080/v1` by
  default; runtime-overridable via `AZ_AI_LLAMACPP_ENDPOINT` (alt port /
  loopback IP). Authentication is opt-in: `RequiresApiKey=false` because
  llama-server is unauthenticated by default; operators who launch it
  with `--api-key` can still export `AZ_AI_LLAMACPP_API_KEY` and the
  preset will forward it as Bearer. Model name resolves via
  `AZ_AI_LLAMACPP_MODEL` env var or falls back to the literal
  `"llamacpp"` (llama-server ignores the field anyway -- only one model
  is loaded at a time). Capability profile is **Conservative**
  (`tool_calls=false, vision=false, json_mode=false, streaming=true`);
  flip individual bits per (preset, model) via
  `AZ_AI_CAPABILITY_OVERRIDES=llamacpp:<model>:tool_calls=true`. The
  loopback target is gated by the existing `AZ_AI_LOCAL_PROVIDERS=1`
  opt-in (S03E16); without it, dispatch is refused with the same
  actionable error as the other local-provider presets. ProviderDoctor
  auto-discovers the preset when `AZ_AI_COMPAT_MODELS=llamacpp:<model>`
  is set. +25 unit facts (`LlamaCppPresetTests`) covering preset shape,
  model resolution precedence, optional API-key path, endpoint override
  (happy path + malformed URL + non-loopback HTTP refusal), capability
  defaults + override, allowlist verdicts, and ProviderDoctor probe
  emission; +4 integration assertions in `tests/integration_tests.sh`
  covering `--doctor` table/json rows, capability gate refusal naming
  the override knob, and loopback gate refusal naming
  `AZ_AI_LOCAL_PROVIDERS`.

### Added (e22 -- preserved entry from before this commit)

- **feat(cli):** S03E22 *The Default* (Costanza) -- documented six-rung
  default-provider heuristic (ADR-011) replacing the ad-hoc preset-table
  walk in `PreferencesResolver.ResolveDefaultProvider`. The ladder is, in
  order: (1) `default:azure` when both `AZUREOPENAIENDPOINT` and
  `AZUREOPENAIAPI` are set; (2) `default:<preset>` when exactly one
  `AZ_AI_<PRESET>_ENDPOINT` is set; (3) `default:<preset>:local-detected`
  when ≥2 preset endpoints are set, `AZ_AI_LOCAL_PROVIDERS=1`, and at
  least one endpoint URL parses to a loopback host on the canonical port
  for that preset (alphabetical first match wins; URL-string parse only,
  no socket probe -- ProviderDoctor still owns the live probe);
  (4) `default:openai` when `OPENAI_API_KEY` is present; (5) alphabetical
  tie-break across multiple preset endpoints with no other signal,
  emitting warning `multiple-presets-no-cli-no-profile-no-env-pin`;
  (6) `default:azure:fallback` when nothing matches (fails closed at
  `BuildChatClient`). `SnapshotEnv()` extended additively with
  `AZUREOPENAIAPI`, `AZ_AI_LOCAL_PROVIDERS`, and the
  `AZ_AI_<PRESET>_ENDPOINT` family (ollama / llamacpp / lmstudio / openai
  / groq / together / cloudflare). 36 unit facts in
  `DefaultProviderHeuristicTests.cs`; 7 of the 44 e20 resolver tests
  updated to reflect the new label semantics (this is a behavioral
  change documented in ADR-011 § Migration: key-only envs like
  `GROQ_API_KEY` no longer trigger a preset default by themselves --
  pair them with `AZ_AI_GROQ_ENDPOINT` or use `AZ_PROVIDER=groq`).
- **security(cli):** S03E25 *The Rotation* (Newman) -- `az-ai
  --rotate-creds [provider]` BYOK rotation flow with atomic write,
  timestamped backup (`env.bak.<ISO-8601-Z>`, collision-bumped on
  re-use), and the mode 0600 invariant verified post-rename. Reuses
  `WizardSession` filesystem helpers (extracted: `BackupWithBump`,
  `AtomicWrite`, `SetRestrictivePermissions`); the masked-input loop
  is shared via the new `Cli/MaskedInput` helper which preserves
  the Newman H-1 invariant (fail-closed on `InvalidOperationException`,
  never falls back to `Console.ReadLine`). The rotated key value is
  never logged on success, failure, exception, or smoke-check paths;
  every textual line is routed through `SecretRedactor.Redact` as
  defense-in-depth. Refuses with exit 3 on `--raw` and on non-TTY
  stdin (mirrors the `--setup` gate). 35 unit facts in
  `CredsRotateTests.cs` + 6 integration assertions
  (`tests/integration_tests.sh`).
- **feat(cli):** S03E20 *The Switch* (Costanza) -- `--provider`,
  `--profile`, `--model` flags with a documented precedence chain
  (cli > env > profile > built-in default). `Preferences.Resolve()`
  is the single pure function the dispatcher consults; it returns a
  `ResolutionOutcome` whose `Source` field stamps every resolved value
  with a stable label (`cli` / `env:AZ_PROVIDER` /
  `profile:<name>:provider` / `default:azure` / etc.) so
  `--config show` and future debug paths speak one vocabulary. Missing
  profiles surface a friendly error listing the available names;
  profile-vs-`AZ_AI_COMPAT_MODELS` mismatches emit a stderr warning
  (profile wins, by design). `--config show` gains a
  "Switch resolution (S03E20):" block with per-rail source labels.
  44 unit facts in `ResolutionPrecedenceTests.cs` + 6 integration
  assertions; resolver is pure (no I/O, no Console writes).
- **feat(dispatch):** S03E18 *The Capability Gate* (Costanza) -- new
  provider+model feature matrix that refuses incompatible requests at
  dispatch time instead of letting them surface as a confusing 4xx from
  the wire. New `azureopenai-cli/Capabilities/` directory ships
  `CapabilityDescriptor` (record: `ToolCalls` / `Streaming` / `Vision` /
  `JsonMode` / `MaxContextTokens?`) and `ProviderCapabilities`
  (registry + `Get(preset, model)` + override parser + `Mismatch`
  factory). Built-in matrix as of 2026-05: `azure` / `foundry` =
  permissive (caller-owned deployment); `openai` = full tools / vision /
  json with model-specific narrowings (`gpt-3.5-turbo` no vision; `o1-*`
  no streaming; `o1-mini` no tools); `groq` = tools only on
  `llama-3.1-70b-versatile` and `llama-3.3-70b-versatile`, vision off,
  json on; `together` = tools off / vision off / json on (preset-default
  conservative until per-model rows land); `cloudflare` = streaming-only
  (everything else off, conservative). Override mechanism:
  `AZ_AI_CAPABILITY_OVERRIDES=preset:model:capability=bool[,...]` --
  case-insensitive, malformed entries warn to stderr (silent under
  `--raw` / `--json`) and are skipped. Dispatch wiring fires right after
  `BuildChatClient`: tool-call requests to a non-tool-call model
  (`--agent` / `--ralph` / persona-with-tools) throw
  `CapabilityMismatchException` -> friendly stderr + exit code 2; vision
  mismatch is wired the same way (reserved for future flag); `--schema`
  on a model without `json_mode` warns to stderr and degrades gracefully
  (request proceeds as a regular completion). Telemetry (E13) emits one
  event per refusal with `outcome="client_error"` and
  `error_class="CapabilityMismatch"`. `OpenAiCompatAdapter.Build` warns
  to stderr when an unknown preset falls through to `Conservative()`.
  +33 unit cases in `CapabilityGateTests` (ConsoleCapture-serialised) +
  5 integration assertions in `tests/integration_tests.sh`. Three LOW /
  INFO findings filed `costanza-2026-05-CG-1..3` for matrix review
  cadence, conservative coverage gaps on Together / Cloudflare, and
  future HEAD-probe autodetection. Exec-report at
  `docs/exec-reports/s03e18-the-capability-gate.md`.

### Fixed
- **fix(dispatch):** S03E18 -- a tool-call request against a non-tool-call
  Groq model previously surfaced as the upstream provider's confusing
  4xx after the dispatch round-trip. The capability gate now refuses
  preflight with a friendly error that names the override env var, so
  the user self-rescues without reading the wire response.

### Added
- **feat(security):** S03E26 *The Offline Mode* (Newman) -- new
  `--offline` flag (and strict-equality env twin `AZ_AI_OFFLINE=1`)
  forbids every non-loopback provider call across all six known network
  seams: Azure SDK construction, Foundry SDK construction, OpenAI-compat
  adapter, WebFetchTool, OTLP exporter, and the prewarm probe. Layered
  model: offline does NOT relax the existing loopback opt-in -- loopback
  hosts still require `AZ_AI_LOCAL_PROVIDERS=1` (e.g. Ollama at
  `http://127.0.0.1:11434`). Implemented as a process-wide latch read
  from the existing 2-arg `EndpointAllowlist.Check` overload, so legacy
  call sites pick up offline mode without signature churn. New
  `BlockOffline` verdict in the allowlist seam; friendly error names the
  rule and the env-var to flip; no credential ever appears in the error
  path (verified by adapter and doctor secret-shape leak guards).
  `ProviderDoctor` reflects the gate row by row (`dns: blocked-offline`,
  `healthy: false`) so operators can audit a process from the outside.
  +30 unit cases (16 facts + 6 theory rows in `EndpointAllowlistTests`,
  9 facts in new `OfflineModeTests`, ConsoleCapture-serialised) and +7
  hermetic integration assertions in `tests/integration_tests.sh`. Audit
  at `docs/audits/security-v2.1.4-offline.md` -- **GREEN**, three LOW /
  INFO follow-ups filed as `newman-2026-05-O-1..3`. Exec-report at
  `docs/exec-reports/s03e26-the-offline-mode.md`.

### Added
- **ci(security):** S03E24 *The CVE Log, Per Provider* (Jerry) --
  provider-attributed CVE pipeline. New `make cve-report` target joins
  Trivy findings against `scripts/provider-deps.json` to bucket
  vulnerabilities as `azure` / `openai` / `shared`; output at
  `dist/provider-cve-report.json` plus a markdown table on stdout / step
  summary. New `.github/workflows/sbom.yml` regenerates `dist/sbom.json`
  on every PR (lightweight; release-grade CycloneDX SBOM still ships
  from `release.yml`). Existing Trivy step in `ci.yml::docker` gains a
  non-blocking JSON-format invocation and the attribution summary --
  `exit-code: 0` unchanged, hard gate is a follow-up episode. Per-
  provider severity tolerances + weekly triage cadence at
  `docs/security/cve-policy.md`. Exec report at
  `docs/exec-reports/s03e24-the-cve-log.md`.
- **test(streaming):** S03E17 *The Stream* (Kramer; original blueprint slot
  E13, shipped at exec-report slot E17 because telemetry / a11y / doctor /
  allowlist / local-providers consumed E13-E16 + E19) -- streaming + tool-call
  parity verification for the OpenAI-compat dispatch path that landed in
  S03E09. New `tests/AzureOpenAI_CLI.Tests/CompatStreamingTests.cs` (15
  facts, `[Collection("ConsoleCapture")]`): five-chunk text reassembly,
  order preservation, empty-string deltas, aggregate-to-`ChatResponse` round
  trip, three-delta tool-call reassembly (callId / name / args union),
  mixed text + tool-call interleaving, mid-stream cancellation injection,
  pre-cancelled token, empty stream, MAF agent surface parity (text and
  tool-call), `--json`-mode dispatch-seam invariant, and a sub-second
  latency budget guard. `FakeChatClient` (S03E12 *The Receipt*) gained an
  explicit-chunk-sequence constructor `(IReadOnlyList<ChatResponseUpdate>,
  int? throwAfterChunk)` for deterministic wire-shape replay -- existing
  token-repeat constructor unchanged. No production code change: the
  audit confirmed the existing MAF `agent.RunStreamingAsync` path handles
  both Azure-OpenAI and compat-routed providers identically because the
  seam is `IChatClient`, and the OpenAI SDK adapter aggregates raw
  `tool_calls` deltas before yielding `FunctionCallContent`. Pre-existing
  Kramer finding (HttpClient parameter ignored on `OpenAiCompatAdapter.Build`)
  ledgered as `kramer-2026-05-CR-09-F3` and left **open** -- still deferred
  to the future recorded-fixture transport episode; out of scope for E17.
  Exec-report at `docs/exec-reports/s03e17-the-stream.md`.

### Added
- **feat(a11y):** S03E14 *The Screen Reader* (Mickey Abbott) -- `--plain`
  CLI flag suppresses banner / color / unicode glyphs / spinner.
  Equivalent to setting `NO_COLOR=1 AZ_AI_PLAIN=1` for one invocation;
  looser than `--raw` (status text on stderr is still allowed, just
  plain-ASCII). New `AZ_AI_PLAIN` env var honored in addition to
  `NO_COLOR` and `TERM=dumb`. Centralized in
  `azureopenai-cli/Plain.cs` (single chokepoint, AOT-clean). Wired
  early in `Main()` so the env-loader, banner, and any future spinner
  see a consistent picture without an explicit dependency on `Plain`.
  Bash / zsh / fish completion scripts learn the new flag.
- **docs(a11y):** S03E14 *The Screen Reader* -- accessibility policy
  appendix added to `docs/accessibility.md` (precedence table,
  glyph-alternatives map, screen-reader notes, key:value vs. table
  rendering rule). README gains a short "Accessibility" subsection
  pointing at it. Exec-report at
  `docs/exec-reports/s03e14-the-screen-reader.md`.
- **security(net):** S03E16 *The Allowlist* -- SSRF endpoint allowlist
  (`Net/EndpointAllowlist.cs`) gates compat-provider connections;
  localhost requires explicit `AZ_AI_LOCAL_PROVIDERS=1` opt-in.
  Strict-equality acceptance (mirrors `AZ_AI_TELEMETRY` from E13).
  Single seam shared by `WebFetchTool` (tool surface, opt-in always
  off) and `OpenAiCompatAdapter.Build()` (provider surface). Eight
  verdict states; friendly error names the rule that fired and the
  env-var to flip. 57 adversarial test cases under
  `tests/AzureOpenAI_CLI.Tests/EndpointAllowlistTests.cs` covering
  RFC1918, link-local (incl. cloud metadata 169.254.169.254), IPv6
  loopback / fe80 / fc00 / ff00, multicast, broadcast, 0.0.0.0,
  userinfo, privileged ports, octal / decimal / IPv6-mapped-IPv4 /
  trailing-dot localhost obfuscation, IDN homoglyph punycode
  normalization, mixed-case host, and DNS-rebinding (multi-record
  resolver stub). Audit verdict GREEN
  (`docs/audits/security-v2.1.3-allowlist.md`); three forward-
  hardening findings filed (`fdr-2026-05-A-1` MEDIUM,
  `fdr-2026-05-A-2` LOW, `fdr-2026-05-A-3` LOW).
- **docs(onboarding):** S03E19 *The First Hour, Local Edition* --
  `docs/onboarding/local-providers.md` tutorial walks a new user from
  install to first local-model response (Ollama). Path A (wizard) and
  Path B (manual env file) are both documented; every E14-E18
  dependency is tagged "(coming soon: S03ENN)" with a today-workaround
  paired so the page reads cleanly before, during, and after those
  episodes ship.
- **feat(diagnostics):** S03E15 *The Probe* -- `az-ai --doctor` subcommand
  probes endpoint reachability + credential presence + model allowlist
  for every configured provider (Azure, Foundry, OpenAI-compat presets);
  never emits credential values. DNS resolution is capped at 3s and
  parallelized via `Task.WhenAll`. Output formats: default ASCII table,
  `--json` (`{"providers":[...],"all_healthy":bool}`), and `--plain`
  key:value stanzas. Exit code 0 = all healthy, 1 = at least one
  unhealthy. Every textual output line routes through `SecretRedactor`
  as defense in depth. New file: `azureopenai-cli/Cli/ProviderDoctor.cs`.
- **feat(observability):** S03E13 *The Telemetry* -- opt-in structured
  telemetry on the compat-dispatch path. Set `AZ_AI_TELEMETRY=1` to
  enable; default off, strict-equality acceptance (any other value,
  including `"true"`, `"yes"`, `"1 "` with trailing space, keeps it
  off). Emits one NDJSON line per dispatch to stderr (never stdout, even
  under `--json`). Schema is fixed at eight fields: `event_id`, `ts`,
  `model`, `provider`, `dispatch_path`, `latency_ms_bucket`, `outcome`,
  `error_class`. Never emits prompts, completions, tokens, API keys,
  endpoints, file paths, stack traces, or user names -- the privacy
  guarantee is enforced by the schema, not by reviewer vigilance.
  `error_class` is run through `SecretRedactor` and truncated at 200
  chars. Initial SLO charter and pricing-review cadence proposed in
  `docs/observability/slo.md` (Morty Seinfeld + Frank Costanza,
  quarterly, 10% delta threshold).
- **feat(wizard):** S03E11 *The Wizard, Reprise* -- setup wizard now
  provider-aware (azure, openai, groq, together, cloudflare); writes
  `[provider:NAME]` sections to `~/.config/az-ai/env` (E10 format) plus
  default-section back-compat exports (`AZUREOPENAIENDPOINT`,
  `AZUREOPENAIAPI`, `AZUREOPENAIMODEL`, `AZ_AI_COMPAT_MODELS`). Compat
  model strings validated through `OpenAiCompatAdapter.ParseCompatModels`
  before write; existing files are backed up to `env.bak.<timestamp>`;
  identical re-runs are no-ops. chmod 600 on Unix. Refuses politely on
  non-TTY / `--raw` / `--json` instead of looping on closed stdin.
- **bench(perf):** S03E12 *The Receipt* lands the pre-merge bench harness
  under `tests/AzureOpenAI_CLI.Tests/Benchmarks/`: `FakeChatClient`
  (deterministic-latency `IChatClient` with configurable artificial delay
  and token-count emission, no network) and `BenchmarkHarness`
  (configurable warm-up + measured iterations; reports mean / p50 / p95 /
  p99 / stdev with R-7 linear-interpolation percentiles). Self-consistency
  tests assert the harness produces ordered statistics and tracks
  `Task.Delay`-driven floors within tolerance. A gated
  `Snapshot_EmitMarkdownTable` test (env: `AZ_AI_BENCH_FULL=1`) regenerates
  the markdown rows quoted in the exec report. The fast path stays under
  the preflight wall-clock budget.
- **feat(provider):** `OpenAiCompatAdapter` (S03E09 *The Compat*):
  route models to OpenAI-compatible endpoints (OpenAI, Groq,
  Together, Cloudflare presets) via the `AZ_AI_COMPAT_MODELS`
  allowlist (`preset:model` pairs). Built-in presets read API keys
  from per-provider env vars (`OPENAI_API_KEY`, `GROQ_API_KEY`,
  `TOGETHER_API_KEY`, `CLOUDFLARE_API_TOKEN`). Dispatch precedence:
  Azure Foundry allowlist > OpenAI-compat allowlist > default
  Azure OpenAI. Implements ADR-010.
- **feat(keychain):** Per-provider credential sections in
  `~/.config/az-ai/env` (S03E10 *The Keychain*): `[provider:openai]`,
  `[provider:azure]`, `[provider:foundry]`, `[provider:groq]`,
  `[provider:together]`, `[provider:cloudflare]`. Bare keys inside a
  section are namespaced by the provider (e.g. `API_KEY` under
  `[provider:openai]` becomes `OPENAI_API_KEY`). Default unsectioned
  content remains shell-export compatible -- existing files do not
  need to be edited. Unknown sections emit a `[WARNING]` (silent
  under `--raw`/`--json`) and skip without aborting. SecretRedactor
  extended with a `[REDACTED:provider-key]` label for
  `OPENAI_API_KEY`, `GROQ_API_KEY`, `TOGETHER_API_KEY`, and
  `CLOUDFLARE_API_TOKEN`.
- **feat(prompts):** Five canonical task templates land in the
  Espanso/AHK example kit -- `:aicode`, `:aiquestion`, `:aiarch`,
  `:aidata`, `:aicost` -- as `examples/espanso-ahk-wsl/espanso/ai-prompts.yml`,
  shipping a curated prompt library on top of the unified S03
  trigger pattern. Commit `905515e`.

### Fixed
- **fix(a11y):** S03E14 *The Screen Reader* (Mickey Abbott) -- glyph-leak
  audit scrubs every non-ASCII byte from default CLI output paths.
  Replaced: `\u2014` (em-dash) in banner, `--current-model` arrow, and
  error-chain joiner `Program.UnwrapException`; `\u2192` (RIGHTWARDS
  ARROW) in `--set-model` / `--config set` / token-usage line / help
  text; `\u2022` (BULLET) in `--personas` listing; `\u2713` (CHECK
  MARK) on every wizard / squad / config-set acknowledgement; theatre-
  mask emoji (`\ud83c\udfad`) in persona auto-route messages; `\u2026`
  (HORIZONTAL ELLIPSIS) in cached-response truncation marker. None of
  the previous glyphs survived screen-reader pronunciation cleanly.
  All replaced with their ASCII equivalents (`--`, `->`, `-`, `[ok]`,
  `[persona]`, `...`). Default CLI output is now ASCII-only and
  ANSI-free; verified by 28 new xUnit `AccessibilityTests` and 6 new
  `tests/integration_tests.sh` assertions covering `--help`,
  `--version`, `--version --short` under `NO_COLOR`, `TERM=dumb`,
  `AZ_AI_PLAIN=1`, and `--plain`.
- **fix(perf):** `PrewarmAsync` now also covers compat-routed providers
  via the new `PrewarmCompatAsync` wrapper (S03E12 *The Receipt*, closes
  Kramer Finding 4 from S03E09): when `AZ_AI_COMPAT_MODELS` is set the
  prewarm path resolves each distinct preset and exercises
  `OpenAiCompatAdapter.Build` (preset resolution, env-var read, SDK
  option construction) so the first real chat call through the compat
  seam no longer pays cold-start cost. Silent-by-contract; no network;
  per-entry build failures (missing API key, missing
  `CLOUDFLARE_ACCOUNT_ID`) are swallowed.
- **fix(observability):** `CostEstimator` now ships placeholder rates for
  the four OpenAI-compatible presets (`openai`, `groq`, `together`,
  `cloudflare`) via the new `CompatCostRates` table and
  `EstimateForCompatPreset` method (S03E12 *The Receipt*, closes Kramer
  Finding 5 from S03E09). Known presets emit numeric estimates with the
  approximation note flagging them as PLACEHOLDER values; unknown presets
  fall through to a `[REDACTED:provider]` sentinel + an "unknown rate, $?
  estimate" message rather than failing. Every entry carries an inline
  TODO marker referencing the upstream pricing URL the next maintainer
  should refresh from.

### Documentation
- **docs(audits):** S03 sweeps-week audit triple lands in
  `docs/audits/` -- `docs-audit-2026-05-elaine.md` (docs, YELLOW,
  22 findings), `security-v2.1-post-prompts.md` (security, RED,
  F-1 CRITICAL bash injection), and `audit-process-meta-2026-05.md`
  (process meta-audit, YELLOW). Commit `8b71d14`.
- **docs(s03):** Audit-triple episodes land as
  `docs/exec-reports/s03e03-the-docs-audit-reprise.md`,
  `s03e04-the-mailman-knocks-twice.md`, and
  `s03e05-the-auditors-auditor.md`; `s03-blueprint.md` renumbered
  so the unplanned sweep slots into Arc 1.5 without disturbing the
  provider-abstraction spine (E03-E05 now sweeps; original E03+
  shift to E06+). Commit `03d5559`.

### Security
- **fix(espanso):** Close form-input bash injection in
  `examples/espanso-ahk-wsl/espanso/ai-prompts.yml` -- F-1 CRITICAL
  (`:aicode` shell-interpolated form fields into the bash system
  prompt and into a double-quoted user-prompt argument) and F-2
  HIGH (apostrophe-driven shell breakout across `:aiquestion`,
  `:aiarch`, `:aidata`, `:aicost`). All five triggers rewritten on
  the WSLENV / `env VAR=...; bash -c '... "$VAR" ...'` pattern;
  free-form fields piped via stdin, choice fields `case`-mapped to
  fixed allowlists. As a side effect, triggers now accept English
  text containing apostrophes (`it's`, `don't`, `O'Reilly`) which
  previously failed silently. Audit:
  `docs/audits/security-v2.1-post-prompts.md` F-1 / F-2.
  Commit `c25ca38`. ([s03e04-the-mailman-knocks-twice])

## [2.2.0] -- 2026-04-30

### Added
- **chore(tooling):** Mechanical enforcement of the exec-report
  convention. New `scripts/exec-report-check.sh` detector fails when a
  push range touches files outside `docs/exec-reports/` and adds no new
  `sNNeMM-*.md` episode write-up. Wired into `make preflight` (now five
  gates, not four) and into a `pre-push` git hook installable via
  `make install-hooks`. Opt out per-commit with a `Skip-Exec-Report:`
  trailer (start-of-line, like `Co-authored-by:`) for genuinely trivial
  changes (typo fixes, dependency bumps, hotfix rollbacks). Replaces
  the prior advisory-only skill text that S02E37 demonstrated was
  insufficient. See S02E38 -- *The Soup Nazi Gets a Lawyer*.
- **feat(config):** New `--config export-env` subcommand resolves Azure
  OpenAI credentials (env > config) and prints them as
  `AZUREOPENAIENDPOINT=`/`AZUREOPENAIAPI=`/`AZUREOPENAIMODEL=` lines (or
  a JSON object under `--json`) so operators can `eval`/`env`-source them
  into a shell or CI pipeline. Refuses to run without
  `--i-understand-this-will-print-the-secret`; emits a STDERR `[WARNING]`
  before any plaintext output (suppressed under `--raw`/`--json`).
- **feat(wizard):** First-run interactive setup wizard. Running bare `az-ai`
  on an interactive terminal with no credentials configured now launches a
  guided setup that prompts for the Azure OpenAI endpoint, API key (masked
  input), and default model deployment, then persists them to
  `~/.azureopenai-cli.json` (0600 perms). Re-run any time with `az-ai --setup`
  (alias `--init-wizard`). `UserConfig` gained `endpoint` and `api_key`
  fields that serve as fallbacks when the matching environment variables
  are unset; `api_key` is redacted (`api_key=<redacted>`) in
  `az-ai --config list` output. The wizard is suppressed under `--raw`,
  `--json`, or when stdin/stdout is redirected so scripted and piped
  callers continue to see the existing env-var error.
- **feat(examples):** Espanso WSL config (`examples/espanso-ahk-wsl/espanso/ai-windows-to-wsl.yml`)
  now ships with the "yada yada yada" loading placeholder that the docs
  have been advertising. While `az-ai` runs, the trigger types
  `yada yada yada` (or `searching the web...` for `:aiweb `) at the
  cursor via `[System.Windows.Forms.SendKeys]`, then backspaces it away
  the moment the response arrives -- closing the 2-3 second feedback gap
  during the Azure round-trip. Wrapped in `try { ... } finally { ... }`
  so the placeholder always clears, even if the WSL pipeline errors.
- **feat(espanso):** New `:aishort ` snap-tier trigger (60 max-tokens,
  ~150 char target). Faster than `:ai ` for chat-app replies where you
  do not need a paragraph -- one short sentence, no preamble, no
  markdown. Hard token cap means generation latency is ~1.8 s less than
  an unbounded reply at GPT-4o-mini. ([s03e02-the-library-cops-word-limit])
- **feat(espanso):** New `:aiyml ` self-extension trigger. Type a
  natural-language description of a new Espanso AI trigger; the AI
  generates a YAML block conforming to the unified S03 pattern (trigger
  name match, SendKeys-safe `$ph`, BACKSPACE ordering, try/finally
  retype, here-string bash) and places it on the clipboard for you to
  paste under `matches:` in `ai-wsl.yml`. Espanso reloads on save.
  Maintains user config ownership -- no file write, no privilege
  surface. ([s03e02-the-library-cops-word-limit])
- **feat(governance):** New cast member **Lt. Bookman** (output economy
  / brevity discipline) at `.github/agents/bookman.agent.md`. Owns the
  response-length tier doctrine (S=Snap/M=Chat/L=Document/U=Mirror/
  F=Free), `--max-tokens` budgets per tier, and the system-prompt
  brevity language. Supporting players: 21 -> 22; total fleet: 27 -> 28.
  ([s03e02-the-library-cops-word-limit])
- **feat(examples):** Espanso WSL config gains 7 new triggers from the
  S02E37/S03E01 unification: `:aitr` (translate to chosen language,
  12-language picker), `:aishrink` (compress to ~50% length), `:aireply`
  (draft email/message reply, intent + tone form), `:aicommit`
  (Conventional Commit message from clipboard diff), `:airegex`
  (explain-or-generate regex), `:aianon` (PII redaction), `:aiq ` (one-
  line quick question). Total triggers: 13 -> 22.
  ([s02e37-the-yada-yada-yada], [s03e01-the-yada-yada-strikes-back],
  [s03e02-the-library-cops-word-limit])
- **chore(quality):** New `scripts/lint-espanso-yml.sh` enforces
  structural invariants on `ai-windows-to-wsl.yml` (parse, unique
  triggers, `$trigger`/`trigger:` value match, `$ph` SendKeys-safe
  charset, exactly two BACKSPACE refs in correct order, try+finally+
  SendWait($trigger) restoration). Also rejects `--system '...{{form1.X}}...'`
  patterns (the S03E01 root-cause bug class) and `Invoke-Expression`
  composed with form values. Wired into `tests/integration_tests.sh` so
  the CI `integration-test` job fails fast on structural drift.
  ([s03e01-the-yada-yada-strikes-back])

### Changed
- **espanso(brevity):** Tightened three triggers per Lt. Bookman's tier
  doctrine: `:aiq` 200 -> 60 max-tokens with stricter `<=150 char, 1
  sentence` system prompt; `:aireply` 800 -> 400 max-tokens with
  `<=4 short sentences` language; `:aitldr` 150 -> 120 max-tokens with
  explicit `<=300 chars total` cap. Mirror-tier triggers (`:aifix`,
  `:airw`, `:aitone`, `:aitr`, `:aishrink`, `:aiflip`, `:aianon`)
  intentionally NOT capped -- their output length must track input. Free-
  tier triggers (`:ai `, `:aiweb `, `:aiimg`) untouched per design.
  ([s03e02-the-library-cops-word-limit])
- **espanso(unification):** All 22 Espanso WSL triggers now use the same
  `shell: powershell` + `cmd: |` + `$bash = @'...'@` here-string +
  `wsl.exe -e bash -lc` pattern. The prior mix of `shell: cmd` + folded
  `cmd: >` scalars (which required hand-counted backslash escaping
  through three layers and exploded with `TerminatorExpectedAtEndOfString`
  on edit) is retired. Header comments document the unified pattern and
  the rationale for retirement. ([s03e01-the-yada-yada-strikes-back])
- **espanso(ux):** Loading-placeholder dance now backspaces the trigger
  text *before* typing the placeholder, then re-types the trigger in
  `finally` so Espanso's own delete-trigger step still lines up with
  what is on screen. Closes the prior visual glitch where the trigger
  appeared next to the placeholder (`:aifixyada yada yada`).
  ([s03e01-the-yada-yada-strikes-back])
- **espanso(ux):** All 19 WSL pipelines wrap output in an empty-stdout
  fallback banner (`if ([string]::IsNullOrWhiteSpace($out)) { '[az-ai:
  no response -- check connectivity, az-ai install, or env]' }`) so
  silent-failure paths now surface a diagnostic instead of injecting
  empty text into the user's document.
  ([s03e01-the-yada-yada-strikes-back])

### Fixed
- **fix(examples):** Espanso WSL config (`examples/espanso-ahk-wsl/espanso/ai-windows-to-wsl.yml`)
  no longer fails with PowerShell `TerminatorExpectedAtEndOfString` on
  the `:ai ` and `:aiweb ` form triggers. Three bugs fixed: (1) the
  multiline `cmd: |` heredoc under `shell: cmd` was silently truncated
  by `cmd.exe` to a single line, leaving an unterminated `"`; rewritten
  with `shell: powershell` and a `@'...'@` here-string so multiline works
  natively and the prompt is metachar-safe; (2) `az-ai-wrap` invocations
  replaced with `az-ai` -- the wrapper became unnecessary in v2.1.1 once
  `az-ai` started auto-loading `~/.azureopenai-cli.json` and
  `~/.config/az-ai/env` at startup; (3) `wsl.exe -e bash -c` (non-login,
  no PATH) replaced with `bash -lc` so `~/.local/bin/az-ai` resolves
  without a system install. `make espanso-install` now ships a config
  that works first-try on a stock `make install` setup.
- **fix(examples):** `:aiimg` temp filename now uses
  `[System.IO.Path]::GetRandomFileName()` instead of a fixed predictable
  name, and the file write is wrapped in try/finally that deletes the
  artifact regardless of outcome (was leaving stale PNGs in `%TEMP%`
  on errors). ([s03e01-the-yada-yada-strikes-back])
- **fix(tests):** Cross-class env-var race in xUnit caused `ExportEnvTests`,
  `ModelAllowlistTests`, and `UnicodeEncodingTests` to flake under
  parallel execution -- one class would clear `AZUREOPENAIMODEL` between
  another's set and read. Tagged all three (plus four others identified
  in the round-2 audit: `ImageGenerationTests`, `ToolHardeningTests`,
  `CliParserTests`, `ValidationTemperatureTests`) with
  `[Collection("ConsoleCapture")]` to serialize. Deleted the redundant
  `SafetyPatchCollection` and migrated its members into `ConsoleCapture`
  to close a related cross-collection `AZUREOPENAIAPI` race between
  `PromptCacheTests` and `V201ProgramPatchTests`.
  ([s03e01-the-yada-yada-strikes-back])

### Security
- **security(espanso):** Closed a HIGH bash-quote injection on
  `:aitone`, `:aitr`, and `:aireply.tone` form triggers. Espanso
  substitutes `{{form1.X}}` into the `cmd:` block as raw text BEFORE
  PowerShell parses it, and a choice value containing an apostrophe
  (e.g. `ELI5 / Explain Like a 5-year-old`) broke the bash single-quoted
  `--system '...'` argument. Now uses a `switch` whitelist mapping:
  user input is bounded by the choice list; choice value maps to a
  hardcoded safe phrase; the bash command line never sees user-controlled
  text. The ELI5 choice was renamed (no apostrophe) as a belt-and-
  suspenders measure. ([s03e01-the-yada-yada-strikes-back])
- **security(espanso):** Closed the `:aireply.intent` (single-line free-
  form field) bash injection. Captured via PowerShell here-string,
  sanitized (strip `"`, CR, LF), passed to bash via `WSLENV` + an
  `AZ_AI_INTENT` environment variable, referenced as `"$AZ_AI_INTENT"`
  inside the bash `--system` arg. Espanso's `multiline:false` makes the
  PS here-string `'@` boundary attack unreachable.
  ([s03e01-the-yada-yada-strikes-back])
- **security(espanso):** Trust-model header documents per-trigger
  privacy implications (`:aianon` egresses raw PII before redaction;
  `:aicommit` ships full diff incl. any staged secrets; `:aic` ships
  clipboard verbatim same secret-egress class as `:aicommit`; `:aireply`
  ships email bodies; `:aitr` ships source text; `:aiyml` writes
  generated YAML to the clipboard, design prompt egresses to Azure).
  Multi-line free-form prompt fields (`:ai `, `:aiweb `, `:aiimg`)
  carry a residual PS here-string `'@` boundary risk that cannot be
  closed without abandoning Espanso template substitution -- the
  threat model is essentially self-inflicted but documented honestly.
  ([s03e01-the-yada-yada-strikes-back])
- **security(espanso):** New `FOCUS HAZARD` callout in the trust-model
  header: SendKeys writes to whichever window has foreground focus when
  each call fires, including the placeholder-restore keystrokes that
  fire in the `finally` block. Users are warned to NOT switch windows
  while a trigger is in flight. ([s03e01-the-yada-yada-strikes-back])
- **chore(security):** `ShellExecTool` env-var scrubbing list audited;
  confirmed coverage for all Azure/Foundry/GH/OpenAI/Anthropic keys plus
  `AZURE_IMAGE_MODEL`. ([s03e01-the-yada-yada-strikes-back])
- Fail closed when masked input is unavailable: `SetupWizard.ReadMaskedLine`
  no longer falls back to unmasked `Console.ReadLine` when `Console.ReadKey`
  throws on pseudo-TTYs (some container runtimes, `dotnet test` capture, CI
  runners with `tty: true` but no `/dev/tty`, restricted hosts, WSL +
  `ssh -t` edge cases). The fallback echoed every keystroke of the API key
  to scrollback / tmux logs / TTY loggers. The wizard now emits a one-line
  `[ERROR]` warning to stderr and short-circuits to exit 130 without ever
  accepting plaintext input. Newman audit H-1.
- Refuse `az-ai --config get api_key` to prevent secret leakage via
  scrollback, shell history of pipe targets, screen-share, and terminal
  logs. The get-by-name path was an escape hatch around the `--config list`
  redaction. Users should re-run `az-ai --setup` or read
  `~/.azureopenai-cli.json` directly (file is mode 0600). `UserConfig.GetKey`
  itself still returns the raw value for in-process callers (e.g. the
  wizard); the refusal lives at the print site only. Newman audit H-2.

## [2.1.1] -- 2026-04-24

### Fixed
- **fix(config):** Empty or whitespace-only `~/.azureopenai-cli.json` no
  longer emits a `[WARNING] ... invalid JSON` line on every invocation.
  Empty files are now treated as "no config, use defaults" (the obvious
  intent when the file was created by `touch` or `:>`). Malformed
  non-empty JSON still warns as before.
- **fix(makefile):** `BIN_NAME` was still `AzureOpenAI_CLI` after the v2
  consolidation in `b913617` renamed `<AssemblyName>` to `az-ai`. This
  broke `make install`, `make publish-aot`, `make bench` /
  `make bench-quick`, and the `ci / bench-canary` gate -- which in turn
  blocked the v2.1.0 release workflow from publishing a GitHub Release
  (tag landed, artifacts never built). Fix-forward release. Also
  corrects 7 cross-RID `@ls` lines (linux-x64, linux-musl-x64,
  linux-arm64, osx-x64, osx-arm64, win-x64, win-arm64) for the same
  rename. No functional v2 code change.

## [2.1.0] — 2026-04-23

### Added
- **feat(makefile):** `make migrate-check` / `make migrate-clean` targets
  help v1.x `az-ai` users audit and remove stale install artifacts before
  switching to the v2 binary. `migrate-check` is read-only — it reports
  any remaining v1 shims on `$PATH`, stray config under
  `~/.config/az-ai`, and pre-2.0 keystore entries. `migrate-clean`
  performs the removal behind an explicit prompt and is safe to re-run.
  Pairs with [`docs/migration-v1-to-v2.md`](docs/migration-v1-to-v2.md).
  ([s02e33-the-uninstaller])
- **docs(index):** `docs/README.md` is now the canonical docs entry-point
  map — one page indexing every user-facing doc grouped by task (install,
  migrate, troubleshoot, contribute, audit), with 8 cross-link footers so
  every leaf page points back to the map. Closes the "where do I find X"
  friction Lloyd had been filing for five episodes.
  ([s02e25-the-story-editor])
- **feat(cost):** Opt-in cost summary via `--show-cost` -- prints a
  one-line receipt to stderr after the response (`[cost] in=N out=N
  total=N tokens (~$X.XXXX @ model)`). Token counts are always shown;
  dollar estimates appear only when the deployment name is in the
  hard-coded price table (snapshot 2026-04, see
  `azureopenai-cli/CostAccounting.cs`). Agent and Ralph modes
  accumulate across rounds / iterations and print one rollup at the
  end. Stderr-only -- raw stdout pipelines stay clean.
  ([s02e09-the-receipt])
- **feat(squad):** Bake the 12 Seinfeld-themed cast as default
  runtime personas in `--squad-init` -- `costanza`, `kramer`,
  `elaine`, `jerry`, `newman` (main cast), `larry-david` (showrunner),
  `lloyd-braun` (junior dev), and 5 supporting players (`maestro`,
  `mickey-abbott`, `frank-costanza`, `soup-nazi`, `mr-wilhelm`).
  Additive on top of the 5 generics (now 17 personas total). Direct
  cast-name routing wins over generic keyword scoring -- `az-ai
  --persona auto "kramer review this csproj"` resolves to `kramer`,
  not `reviewer`. Existing `.squad.json` files untouched. See
  `azureopenai-cli/Squad/SquadInitializer.cs` and
  [`docs/persona-guide.md`](docs/persona-guide.md#cast-personas-the-show-lives-on).
  ([s02e30-the-cast])
- **docs(distribution):** Homebrew, Scoop, and Nix packaging drafts
  for the v1 line under `packaging/{homebrew,scoop,nix/azure-openai-cli}/`,
  paired with `docs/distribution/{homebrew,scoop,nix,README}.md`
  (approach + comparison + install verification). Manifests are
  marked DRAFT -- no tap, bucket, or flake registry exists yet;
  SHA256s are tag-time placeholders. Pre-publish install paths
  documented for each channel. ([s02e16-the-catalog])
- **docs(onboarding):** `docs/onboarding.md` -- Lloyd Braun's
  first-hour walkthrough for new contributors: literal step-by-step
  from `git clone` to first PR, friction log of every assumption the
  existing docs skip, file-system map, and a rated list of starter
  PRs. Glossary expanded with Conventional Commits, LOLBin, Preflight,
  SBOM, SDK / runtime, and Trivy. ([s02e12-the-apprentice])
- **docs(prompts):** Prompt library, temperature cookbook, and
  eval-framework design sketch under `docs/prompts/` -- inventory
  of every system prompt, instruction string, and tool description
  the CLI puts in front of a model (12 IDs across `Program.cs`,
  `Squad/`, and `Tools/`), plus the design seam for a future small
  eval harness (no runner yet). ([s02e18-the-maestro])
- **docs(ethics):** Responsible-use posture and disclosure docs under
  `docs/ethics/` -- the Rabbi's eight-row ought / must matrix
  (five `ENFORCED`, two `PARTIAL`, one honest `NAMED-ONLY` for
  model bias) with Newman callouts mapping each "ought" to the
  implementing code path, plus a one-page user-facing AI-use
  disclosure naming the data path, the non-storage / non-training
  posture, and the per-OS keystore. ([s02e21-the-conscience])
- **docs(talks):** Speaker package for the LOLBin credentials talk
  under `docs/talks/lolbin-credentials/` (abstract, speaker bio,
  demo script, slide outline, stage notes; 27-minute outline that
  fits a 25-30 minute slot). ([s02e20-the-conference])
- **docs(security):** `docs/security/v2-audit.md` -- end-to-end
  audit of the v2 surface (credential stores, shell_exec,
  read_file, web_fetch SSRF, dependency vulns, subagent depth
  cap). Each protection paired with the attack it stops; verdict
  5 PASS / 1 NEEDS-FOLLOW-UP / 0 GAP. ([s02e13-the-inspector])
- **docs(legal):** `THIRD_PARTY_NOTICES.md` extended with a v1.x dependency manifest covering all 15 packages (3 direct, 12 transitive) in the production CLI closure, all MIT-licensed. ([s02e15-the-lawyer])
- **docs(legal):** `docs/legal/license-audit.md` is the v1 OSS license audit -- per-package classification, license-obligation posture, Lloyd callouts on MIT vs Apache 2.0 and GPL contagion, and the exact `dotnet` commands to refresh attribution before each release. ([s02e15-the-lawyer])
- **docs(market):** `docs/competitive-landscape.md` -- Sue Ellen
  Mischke's tight landscape brief against five credible CLI / TUI
  alternatives, naming three differentiators we lean into (per-OS
  keystore, AOT single binary, Azure-specific first-run wizard) and
  three gaps we accept (Azure-only, no TUI, no multi-model routing /
  prompt library). Includes three Peterman draft positioning
  paragraphs marked "not yet adopted." ([s02e19-the-competition])
- **docs(i18n):** `docs/i18n-audit.md` inventories every user-facing
  string in the v1 CLI binary and classifies each as locale-agnostic,
  translation-ready, or needing refactor before l10n can begin. No
  strings translated; no `--locale` flag added. ([s02e08-the-translation])
- **docs(glossary):** `docs/glossary.md` is the new project glossary --
  AOT, DPAPI, libsecret, i18n, l10n, RTL, CJK, MCP, TPM, RPM, SSRF.
  Single source of truth; future episodes append. ([s02e08-the-translation])
- **docs(observability):** New `docs/telemetry.md` -- one-page honesty
  pass on the project's zero-default-telemetry posture, with the grep
  commands a contributor can run to audit the tree themselves.
  ([s02e07-the-observability])
- **docs(observability):** New `docs/incident-runbooks.md` -- three
  short runbooks (401 auth, 429 rate limit, DNS/TLS) for the
  user-facing failures we see most often, plus a plain-English Lloyd
  callout on what an SLO is. ([s02e07-the-observability])
- **docs(product):** `docs/user-stories.md` -- one-paragraph user
  stories translating every shipped S02 feature (and a handful of
  pre-S02 catch-up entries) out of engineering jargon, grouped by
  user role. ([s02e11-the-spec])
- **feat(a11y):** `NO_COLOR` and `FORCE_COLOR` env-var gates land in v1
  via the new `AzureOpenAI_CLI.ConsoleIO.AnsiPolicy` helper — the
  forward-looking chokepoint for any future color in the v1 binary.
  Precedence is `NO_COLOR` (off, any non-empty) > `FORCE_COLOR` (on,
  any non-empty other than `"0"`, even when stdout is redirected) >
  TTY auto-detect. Kept in lockstep with v2's `Theme.UseColor()` so
  users get one set of rules across both binaries. The first-run
  wizard now prints a one-line announcement
  (`Your key will be masked as you type. Press Enter when done.`)
  before the masked key prompt — Mickey's gift to screen-reader
  users, since a stream of bullet glyphs with no warning is hostile.
  The announcement is suppressed on the redirected-stdin path where
  no masking actually happens. The `docs/accessibility.md` contract
  picks up a v1 section documenting all of the above plus the clean
  abort path. ([s02e06-the-screen-reader])
- **feat(bench):** `make bench-quick` target — a 5–10 s directional
  cold-start smoke (N=50, no warm-up, stdout only) for the pre-commit /
  dev loop. Sits below `make bench` (mid-PR, N=100) and `make bench-full`
  (pre-merge, N=500, `--flag-matrix`). See
  [`docs/perf/bench-workflow.md`](docs/perf/bench-workflow.md).
- **ci(bench-canary):** New `bench-canary` job in `.github/workflows/ci.yml`
  runs `make bench-quick` on every push / PR (`needs: build-and-test`) and
  posts the table to the GitHub Actions step summary under
  `## bench-canary (directional only)`. Explicitly **not** a regression
  gate — shared-runner jitter (±30 %) makes it useless for precise
  comparisons; pinned-rig numbers (`make bench-full` per
  `docs/perf/reference-hardware.md`) remain authoritative. The bench step
  uses `continue-on-error: true` so noisy numbers cannot redden CI.
- **feat(credentials):** Opportunistic libsecret credential store on
  Linux. When `/usr/bin/secret-tool` is present and a DBus session
  bus is available, `az-ai` now stores credentials via libsecret
  (GNOME Keyring / KDE Wallet) instead of plaintext. Falls back to
  plaintext on systems without libsecret or without a session bus
  (headless containers, minimal installs). Zero new NuGet deps.
- **docs(perf):** Reference-hardware pinning doc (`docs/perf/reference-hardware.md`)
  captures the canonical bench rig (`malachor`, i7-10710U, linux-x64), the
  pre-merge protocol (governor=performance, AC power, N=500, warm-up=5,
  `--flag-matrix`), and the tolerance bands (±5 % noise, 10 – 20 %
  regression flag, > 20 % regression block). ([bania-v2-03])
- **docs(perf):** v2 cold-start p99 investigation
  (`docs/perf/v2-cold-start-p99-investigation.md`) — closes the baseline's
  p99 watchlist item as *rig noise, not a code defect*, with N=500 evidence
  across flag matrix and a runtime-knob sweep. The 20.9 % `--help` p50
  drift item stays open but is re-scoped to the pinned-rig re-run.
  ([bania-v2-02])
- **scripts(bench):** `scripts/bench.py` promoted to a first-class
  pre-merge perf harness with `--n`, `--warmup`, `--flag-matrix`, and
  `--json` options, plus an env fingerprint (CPU model, governor, kernel,
  binary size) on every run. `make bench-full` wires the canonical
  `N=500 --flag-matrix` sweep and writes dated JSON + text bundles to
  `docs/perf/runs/`. ([bania-v2-03])
- **feat(setup):** First-run setup wizard — when credentials are
  unresolvable on an interactive terminal, `az-ai` now prompts for
  endpoint, API key (masked input), and model, then validates via a
  test ping before persisting. Re-runnable any time via `--init`
  (aliases `--configure`, `--login`).
- **feat(setup):** Per-OS native credential storage ("living off the
  land" — zero new NuGet dependencies, AOT-safe):
  - **Windows:** DPAPI via `crypt32.dll` P/Invoke, user-scoped.
  - **macOS:** Apple Keychain via `/usr/bin/security` (service
    `az-ai`).
  - **Linux / containers:** plaintext at `~/.azureopenai-cli.json`
    with mode `0600` (matches AWS CLI / GitHub CLI / Azure CLI
    baseline; rotate keys per documented guidance).
- **feat(config):** `ApiKeyFingerprint` (`sha256(key)[0..12]`) shown
  in `--config show` for safe display and tamper detection; the
  actual key is never printed.
- **docs(proposals):** `docs/proposals/FR-NNN-first-run-wizard.md`
  (number assigned by the docs-fr task) captures motivation, per-OS
  LOLBin design, rejected alternatives, and the Newman + Rabbi
  signoff for the Linux plaintext trade-off.
- **Backward compatibility:** environment variables retain full
  precedence. CI, Docker, Espanso, AHK, and any scripted workflow
  exporting `AZUREOPENAIENDPOINT` / `AZUREOPENAIAPI` /
  `AZUREOPENAIMODEL` is unaffected. The wizard runs only when
  credentials are unresolvable **and** the terminal is interactive
  **and** neither `--raw` nor `--json` is set **and** we are not
  in a container.
### Changed
- **docs(index):** Doc-tree cleanup — the 11 docs orphaned since E25 are
  either re-linked from `docs/README.md` or retired, and the launch
  artifacts get their own index at `docs/launch/README.md`. Closes
  findings `e25-orphan-docs` and `e25-launch-dir-no-index`.
  ([s02e34-the-index])
- **docs(process):** Change-management contract under `docs/process/` —
  ADR stewardship (`docs/adr/`), a CAB-lite review checklist for risky
  merges, and a bi-weekly retrospective cadence. Mr. Wilhelm's floor for
  how changes move from idea to main without surprising on-call.
  ([s02e22-the-process])
- **docs(process):** Writers' bible — consolidates the prior season's
  episode-shape, fleet-dispatch, and shared-file protocols into
  `.github/skills/{episode-brief,fleet-dispatch,shared-file-protocol}.md`
  so sub-agents stop re-deriving the rules per episode.
  ([s02e27-the-bible])
- **docs(process):** Three hygiene skills under `.github/skills/` —
  `ascii-validation.md`, `docs-only-commit.md`, and
  `changelog-append.md` — canonicalize the grep, decision tree, and
  CHANGELOG append protocol that doc-touching episodes had been inlining
  with minor drift. Reviewers now cite the skill instead of re-deriving
  the rules in PR comments. ([s02e28-the-style-guide])
- **docs(process):** Writers'-room cohesion skills —
  `.github/skills/writers-room-cast-balance.md` and
  `.github/skills/findings-backlog.md` — codify casting discipline for
  co-lead episodes and a standard shape for cross-episode findings
  triage. ([s02e29-the-casting-call])
- **test(adversary):** Chaos drill against the tool surface — a new
  adversarial suite under `tests/AzureOpenAI_CLI.Tests/Adversary/`
  exercises the `read_file`, `shell_exec`, and `web_fetch` chokepoints
  against fuzz / injection / unicode-bypass inputs. Suite is gated in
  CI; no production code-path changes. ([s02e23-the-adversary])
- **test(squad):** Adversarial coverage of the 5 pre-cast generic
  personas (`coder`, `reviewer`, `security`, `writer`, `analyst`) —
  9 findings filed, 1 bug fixed in the persona-routing scorer's
  tie-break path. ([s02e31-the-audition])
- **feat(ralph):** Ralph `--validate <cmd>` validation loop now defaults to a
  low sampling temperature (0.15) when the operator has not explicitly pinned
  one via `--temperature` or `AZURE_TEMPERATURE`. High creative temperature
  made the pass/fail verdict oscillate across iterations; 0.15 keeps the loop
  deterministic. Precedence: CLI flag > env var > validate default (0.15) >
  general default (0.55). Non-validate runs still get the 0.55 creative
  default. `Program.RALPH_VALIDATE_TEMPERATURE`, 5 tests in
  `ValidationTemperatureTests.cs`.
- **perf(telemetry):** Lazy-init OTLP exporters — the OpenTelemetry SDK
  pipeline (TracerProvider / MeterProvider + `AddOtlpExporter`) is now only
  constructed when `OTEL_EXPORTER_OTLP_ENDPOINT` is set. With no collector
  configured, `--otel` / `--metrics` / `--telemetry` previously paid
  ~2.7 ms / ~4.2 ms of cold-start tax building a pipeline that exported to
  nothing. Measured drops on the reference rig (`malachor`, AOT linux-x64,
  N=50 per variant): `--otel` −2.74 ms mean (−20.7 %), `--metrics` −4.58 ms
  (−31.1 %), `--otel --metrics` −5.28 ms (−34.5 %). The stderr FinOps
  cost-event channel is independent and continues to fire whenever
  `--metrics`/`--telemetry` is set. When an endpoint IS configured, eager
  construction is preserved so first-span export latency stays predictable.
  See `docs/perf/v2.0.5-baseline.md` §4.3 for the full before/after table.
  ([bania-v2-01])
- **docs(community):** Contributor onboarding refreshed (CONTRIBUTING.md,
  issue/PR templates). ([s02e17-the-newsletter])
- **docs(market):** Competitive landscape + analysis refreshed for 2026 (MCP table-stakes, multi-provider coverage, single-binary distribution).
### Deprecated
### Removed
### Fixed
### Security
- **security(tools):** Extend the `ReadFileTool` path blocklist to cover
  seven home-dir credential-path families — `~/.ssh/`, `~/.kube/`,
  `~/.gnupg/`, `~/.netrc`, `~/.docker/config.json`,
  `~/.git-credentials` + `$XDG_CONFIG_HOME/git/credentials`, and
  `~/.npmrc` / `~/.pypirc`. Paired with 53 new adversary-test facts
  exercising symlink, case-variant, and `$HOME`-override bypass paths.
  Closes the E26 findings queue. ([s02e26-locked-drawer])
- **security(tools):** Harden `shell_exec` blocklist against IFS,
  Unicode, and shell-tokenization bypass. Substring-on-raw-input is
  replaced with a defense-in-depth pipeline: reject shell-substitution
  metacharacters (`${`, `$()`, backticks, `<()`, `>()`), tab/newline,
  and `<`/`>` redirection up front; NFKC-normalize the input; then
  per-statement-segment tokenize, strip surrounding quotes/backslashes,
  basename, and exact-match against the command blocklist. Closes
  finding `e23-shell-ifs-tokenization`; activates 8 previously-Skipped
  bypass tests in `tests/AzureOpenAI_CLI.Tests/Adversary/ShellExecBypassTests.cs`.
  ([s02e32-the-bypass])
- **security(docker):** Hardened the Alpine multi-stage Docker image
  -- explicit numeric `USER 10001:10001` (Kubernetes `runAsNonRoot` /
  PSA `restricted` compatible), `COPY --chown --chmod` for binary
  (`0755`) and license bundle (`0444`), explicit `HEALTHCHECK NONE`
  for the short-lived CLI workload, `DOTNET_CLI_TELEMETRY_OPTOUT=1`
  baked in, expanded `apk` cache wipe, and a broader `.dockerignore`
  allowlist. Posture documented in
  `docs/distribution/docker-hardening.md`. ([s02e14-the-container])


> **Fix-forward from cancelled v2.0.5.** The v2.0.5 release-workflow run
> failed at CI integration-test because `tests/integration_tests.sh`
> Gate 2 hardcoded `"2.0.2"` as the expected output of
> `az-ai-v2 --version --short` — the same stale-literal bug class
> (C-1 / C-2) that v2.0.5 existed to eliminate. All release legs
> (`linux-x64`, `linux-musl-x64`, `osx-arm64`, `win-x64`,
> `docker-publish-v2`) were skipped, so no v2.0.5 binaries, tarballs,
> or GHCR tags were published. The `v2.0.5` git tag remains on
> `origin` (per tag-immutability policy in
> `docs/release/ghcr-tag-lifecycle.md`) but is a no-op marker — treat
> it as cancelled, same status as v2.0.3. v2.0.6 carries everything
> v2.0.5 intended to ship, plus the integration-test fix.
>
> **No user impact.** There was nothing to downgrade from — v2.0.5
> never produced artifacts. Users on v2.0.4 go straight to v2.0.6.

### Fixed
- **Integration-test version assertion (fix-forward from v2.0.5).**
  `tests/integration_tests.sh` Gate 2 now reads the expected version
  string dynamically from `azureopenai-cli-v2/AzureOpenAI_CLI_V2.csproj`
  `<Version>` instead of carrying a hardcoded `"2.0.2"` literal. The
  test was itself an instance of the C-1 / C-2 drift pattern — a
  stale constant that nobody rolled forward. Now it self-adjusts on
  every bump and hard-fails if the shipped binary disagrees with the
  csproj, matching the contract already enforced by
  `VersionContractTests.cs`.

### Notes
- **v2.0.5 status:** cancelled (tag pushed, workflow failed at CI
  gate, zero artifacts published). The `v2.0.5` tag is preserved on
  `origin` for audit-trail continuity; it is not a released version.
  Documented precedent: v2.0.3 was cancelled on the same lifecycle
  pattern (see v2.0.4 banner).

## [2.0.5] — 2026-04-22 (cancelled)

> **Cancelled.** This tag was pushed on 2026-04-22 but the release
> workflow failed at CI integration-test before any build or publish
> step executed. No binaries, tarballs, Docker images, or GHCR tags
> were produced. All substantive content from this entry shipped in
> v2.0.6. Entry retained for audit continuity only.

> **Version-string fix + 50+ marathon findings closed.** The headline
> defect: v2.0.3 and v2.0.4 binaries shipped with `--version --short`
> reporting `2.0.2` because `Program.VersionSemver` and
> `Telemetry.ServiceVersion` were hardcoded string literals that nobody
> rolled forward. v2.0.5 single-sources the version from the csproj
> `<Version>` element (AOT-safe via `Assembly.GetName().Version`), adds
> a xUnit contract pin so it can never drift again, and teaches
> `packaging/tarball/stage.sh` to read the same source of truth.
> No behavior change beyond the version string itself. The rest of
> this release is a documentation marathon: release policy, SemVer
> contract, pre-release checklist, CHANGELOG style guide, runbooks,
> perf baseline, and 45+ audit/dogfood findings closed across the
> wave 1/2/3 agent sweeps.

### Changed
- **Default-model canonicalization (ADR-009).** The "default model" is now
  formally a resolution chain — CLI flag → `AZUREOPENAIMODEL` env →
  `UserConfig.default_model` / smart-default → hardcoded fallback
  (`Program.DefaultModelFallback = "gpt-4o-mini"`). The two duplicated
  `"gpt-4o-mini"` literals in `Program.cs` have been replaced by the
  named constant; fallback behavior is unchanged. `cost-optimization.md`
  §3.5 rewritten to describe the override contract. Operators who prefer
  `gpt-5.4-nano` continue to set it via env/config. See
  `docs/adr/ADR-009-default-model-resolution.md`.

### Fixed
- **Shipped-version-string drift (audit findings C-1 / C-2).**
  `Program.VersionSemver`, `Program.VersionFull`, and
  `Telemetry.ServiceVersion` were hardcoded to `"2.0.2"` and never
  rolled past v2.0.2 — the v2.0.3 and v2.0.4 binaries reported
  `az-ai-v2 --version --short` → `2.0.2`, which would have failed
  `brew test az-ai-v2` on install and broken the Scoop / Nix install
  smoke tests. Version is now single-sourced from the csproj
  `<Version>` element via `typeof(Program).Assembly.GetName().Version`
  (AOT-safe, no reflection-on-metadata path). `packaging/tarball/stage.sh`
  now parses the csproj for tarball filenames (with `STAGE_VERSION`
  env-var override for exceptional re-stages) instead of carrying a
  stale hardcoded `VERSION="2.0.2"`.

### Added
- `tests/AzureOpenAI_CLI.V2.Tests/VersionContractTests.cs` — xUnit
  contract pin that runs on every PR and hard-fails if (a)
  `Program.VersionSemver` regresses to `"2.0.2"` or any other
  hardcoded literal, (b) `Telemetry.ServiceVersion` drifts from
  `Program.VersionSemver`, or (c) either drifts from the csproj
  `<Version>`. Gate row 4 of `docs/release/pre-release-checklist.md`.
- Perf baseline for v2.0.5 on bare-metal Linux (`malachor`):
  [`docs/perf/v2.0.5-baseline.md`](docs/perf/v2.0.5-baseline.md).
  Supersedes the v2.0.0 WSL2 baseline and the v2.0.2 dogfood bench.
  No regression vs. v2.0.2 on cold-start, warm-start, or binary size.

### Docs
- **Release discipline.** New `docs/release/` tree:
  [`semver-policy.md`](docs/release/semver-policy.md) (how to pick the
  bump), [`pre-release-checklist.md`](docs/release/pre-release-checklist.md)
  (20-row gate table, sign-offs, no-go triggers),
  [`ghcr-tag-lifecycle.md`](docs/release/ghcr-tag-lifecycle.md)
  (container tag immutability policy),
  [`artifact-inventory.md`](docs/release/artifact-inventory.md)
  (the asset list every release must match), and
  [`docs/CHANGELOG-style-guide.md`](docs/CHANGELOG-style-guide.md)
  (house style for this file).
- **Runbooks.** `docs/runbooks/release-runbook.md`,
  `docs/runbooks/packaging-publish.md`,
  `docs/runbooks/macos-runner-triage.md`,
  `docs/runbooks/finops-runbook.md`, and
  `docs/runbooks/threat-model-v2.md`.
- **Wave 1/2/3 marathon.** 45+ findings closed across DevRel (speaker
  bureau, swag brief, livestream checklist, announce template cleanup),
  legal (LICENSE year refresh, third-party notices sync, demo attribution
  audit), i18n, accessibility, ethics, QA test-matrix hygiene, and
  docs polish. Full receipts in `docs/audits/` and per-agent sweep
  commit range `9a6d54e..0d26566`.

### Packaging
- Homebrew / Scoop / Nix hash-sync deferred to a T+2h follow-up PR
  (per [`pre-release-checklist.md`](docs/release/pre-release-checklist.md)
  row 20 and [`ghcr-tag-lifecycle.md`](docs/release/ghcr-tag-lifecycle.md)) —
  sha256 digests are computed from the published GitHub Release
  artifacts, not guessed pre-tag. Install via Homebrew / Scoop will
  resolve once that PR merges; until then, direct-download from the
  GitHub Release works.

### Notes
- This release ships from `main` directly; no release branch. CI
  green on the tagged commit was the gate.

## [2.0.4] — 2026-04-22

> **Drop macOS Intel (`osx-x64`) from the release matrix + ship FDR dogfood
> fixes.** The v2.0.3 release pipeline was blocked for another ~9h on
> GitHub Actions' `macos-13` runner pool — the same backlog that blocked
> v2.0.2. macOS Intel is a diminishing platform (Apple's last Intel Macs
> were 2020; Rosetta 2 gives Intel-Mac users a stable Apple Silicon
> binary path), and letting infra flakiness on one leg gate every release
> is not a tenable ship discipline. v2.0.4 cuts macOS Intel from the
> official artifact matrix so every release can publish against the
> reliable legs (`linux-x64`, `linux-musl-x64`, `osx-arm64`, `win-x64`).
>
> **Intel-Mac paths that still work out-of-the-box:**
> - **Rosetta 2** — install `az-ai-v2-<version>-osx-arm64.tar.gz` on
>   macOS 11+; Rosetta 2 handles the translation transparently.
> - **Docker** — `ghcr.io/schwartzkamel/azure-openai-cli/az-ai-v2:2.0.4`
>   runs under `linux/amd64` emulation on Docker Desktop for Intel Mac.
> - **Build from source** — `dotnet publish -r osx-x64 -c Release
>   --self-contained -p:PublishAot=true` still produces a working Intel
>   binary; the csproj supports the RID, we just stop *shipping* it.

### Fixed
- **FDR v2.0.2 dogfood High-severity findings** (report:
  [`docs/audits/fdr-v2-dogfood-2026-04-22.md`](docs/audits/fdr-v2-dogfood-2026-04-22.md),
  commit `4842b6a`). Three items, all shipped to v2.0.2/v2.0.3 and now
  resolved:
  1. **`fdr-v2-err-unwrap`** — global catch now handles
     `Azure.RequestFailedException` before generic `Exception`, unwraps
     up to 5 levels of `InnerException`, and redacts `AZUREOPENAIAPI` +
     endpoint hostname from every error surface. Users see actionable
     status + errorCode instead of "A type initializer threw an
     exception" noise.
  2. **`fdr-v2-raw-config-warning`** — `UserConfig.Load(bool quiet)`
     gained a `quiet` gate; `Program.cs` passes `quiet: opts.Raw` so the
     `--raw` contract (nothing on stderr, ever) holds even when
     `~/.azureopenai-cli.json` fails to parse.
  3. **`fdr-v2-ralph-exit-code`** — `RalphWorkflow` now returns exit 1
     when `--max-iterations` is exhausted without validation passing and
     when every iteration errored. SIGINT-130 preserved.
- 16 new tests (`ExceptionUnwrapTests`, `UserConfigQuietTests`,
  `RalphExitCodeTests`). Full suite: v1 1025/1025 + v2 485/485 = 1510
  green.

### Changed
- **Release matrix** (`.github/workflows/release.yml`): removed `osx-x64`
  from v1 and v2 `build-binaries` matrices + release-body artifact
  tables. No more `macos-13` jobs.
- **Packaging manifests:**
  - `packaging/homebrew/Formula/az-ai.rb` — dropped `on_intel` macOS
    block; notes Rosetta 2 / Docker / source-build fallbacks.
  - `packaging/nix/flake.nix` — dropped `x86_64-darwin` from
    `sourcesFor`; `latestHashes` no longer tracks `osx-x64`. Frozen
    `pinnedHashes` for 2.0.0/2.0.1/2.0.2/2.0.3 retain `osx-x64` keys as
    unreferenced historical markers; they are now ignored by
    `sourcesFor` and can be dropped in a future cleanup.
- Versioned formulas/manifests for older releases (`@2.0.0`, `@2.0.1`,
  `@2.0.2`) keep their `osx-x64` URLs as historical records — those
  releases either never published artifacts at all or never published
  `osx-x64` specifically.

### Notes
- **v2.0.3** was tagged and got as far as publishing the Docker image
  (`ghcr.io/schwartzkamel/azure-openai-cli/az-ai-v2:2.0.3`) before the
  same `macos-13` runner pool wedged the binary matrix. The v2.0.3 run
  was cancelled at cutover; no GitHub Release exists for v2.0.3.
  v2.0.4 supersedes it.

### Packaging (post-publish, hash-sync)
- Homebrew, Nix, and Scoop manifests hash-synced against the v2.0.4
  GitHub Release (run `24789065975`, published 2026-04-22). Digests:
  - `linux-x64.tar.gz` — `sha256:9592a962...8e6` (SRI
    `sha256-lZKpYgsN3jdF2wtXFwja0i1qYABobnwPB2E6lq6nmOY=`)
  - `linux-musl-x64.tar.gz` — `sha256:48b0a81a...ceb` (Nix-only; Homebrew
    does not model musl)
  - `osx-arm64.tar.gz` — `sha256:6c3051a4...874` (SRI
    `sha256-bDBRpKV0wJ9R95WbYZ4YeszjeykY2t2HmnnmfOfrmHQ=`)
  - `win-x64.zip` — `sha256:2d3f8c67...943`
- New frozen pin siblings: `packaging/homebrew/Formula/az-ai-v2@2.0.4.rb`,
  `packaging/scoop/versions/az-ai-v2@2.0.4.json`, and a `"2.0.4"` entry
  in `packaging/nix/flake.nix` `pinnedHashes`. Unversioned manifests
  (`az-ai.rb`, `az-ai.json`, flake `latestHashes`) rolled to 2.0.4.
- **Known issue — tarball-filename drift (audit finding C-1).** The
  v2.0.4 tarballs were uploaded with `2.0.2` embedded in their
  filenames because `packaging/tarball/stage.sh` `VERSION` and the
  `Program.cs` / `Observability/Telemetry.cs` version constants were
  not rolled past `2.0.2` in the v2.0.3/v2.0.4 commits. Manifest URLs
  therefore hardcode the literal `az-ai-v2-2.0.2-<rid>.tar.gz`
  filenames at the `v2.0.4` tag, and the shipped binary still reports
  `--version --short` → `2.0.2`. `brew test az-ai-v2` will fail
  against v2.0.4 until v2.0.5 rolls the version strings in lock-step.
  Full diagnosis in `docs/audits/docs-audit-2026-04-22-lippman.md`
  (Critical findings C-1 / C-2).
- Tap / bucket publish is a separate workflow and is NOT done in this
  commit (owner: Bob Sacamano).

## [2.0.3] — 2026-04-22

> **Re-tag to recover from infra-stuck v2.0.2 release.** v2.0.2 (`fd4ddc7`)
> shipped GHCR image cleanly, but `release-v2` publish was blocked by a
> ~13-hour GitHub Actions `macos-13` runner backlog. The
> `workflow_dispatch` recovery lever in `d2dc627` only applies to
> workflows on `main`; at the v2.0.2 tag, `release.yml` predates that
> commit and rejects `gh workflow run --ref v2.0.2` with HTTP 422. An
> intermediate `gh run rerun --failed` re-queued the cancelled `osx-x64`
> leg but hit the same pool saturation. Re-tagging at HEAD of `main`
> puts `workflow_dispatch` in-tag and gives the release pipeline a fresh
> queue slot. Post-mortem: [`docs/launch/v2.0.2-publish-handoff.md`](docs/launch/v2.0.2-publish-handoff.md).
>
> **No runtime changes from v2.0.2.** Same AOT binaries, same tool
> surface, same security posture. GHCR images `2.0.2` and `2.0.3` are
> bit-identical apart from version-embedded metadata.

### Changed
- `AzureOpenAI_CLI_V2.csproj` Version bump `2.0.2` → `2.0.3`.
- Tag `v2.0.3` picks up `d2dc627` (workflow_dispatch on `release.yml`),
  `641918d` (handoff doc), `ddb76ff` (corrected recovery recipe),
  `315726b` (FDR dogfood report), `f7a83fb` (Bania dogfood report) —
  all docs, no runtime delta.

## [2.0.2] — 2026-04-21

> **Fix-forward from v2.0.1.** v2.0.1 was tagged on `039e6bd` but did not
> publish: `docker-publish-v2` failed at
> `COPY --from=build /app/az-ai-v2 ...: not found` with the same error
> signature as v2.0.0 attempt #1. v2.0.1's Debian→Alpine SDK swap did
> **not** address the real root cause — the failure was libc-independent.
> Post-mortem:
> [`docs/launch/v2.0.1-release-attempt-diagnostic.md`](docs/launch/v2.0.1-release-attempt-diagnostic.md).
> The `v2.0.1` tag is retained as an "attempted release" marker alongside
> `v2.0.0`; v2.0.2 is now the first publicly published v2.x release.

### Fixed
- **`Dockerfile.v2` AOT asset-graph mismatch (real root cause)** — the
  v2.0.0/v2.0.1 pattern split build into `dotnet restore -r linux-musl-x64
  /p:PublishReadyToRun=true` followed by `dotnet publish --no-restore
  -p:PublishAot=true`. R2R and AOT resolve different RID-specific NuGet
  asset graphs; `--no-restore` then forbade publish from pulling the AOT
  assets restore had never fetched. The .NET 10 SDK, rather than
  hard-erroring, **silently fell back to a framework-dependent managed
  publish** — emitting `/app/az-ai-v2.dll` (plus runtimeconfig/deps) and
  no ELF at `/app/az-ai-v2`. Publish exited 0 in ~21s (no ILC/link phase)
  and the runtime-stage COPY then failed because the expected native
  binary never existed. Fix in this release: drop the separate restore
  step and the `--no-restore` flag; a single `dotnet publish` invocation
  resolves the AOT asset graph in one shot. Libc was never the issue —
  the Alpine SDK base is retained for cost/size, not correctness. Cites
  Lippman's Round-2 diagnostic above.
- **Dockerfile.v2 AOT-output verification gates** — three new `RUN`
  lines immediately after the publish step (`test -f /app/az-ai-v2`,
  `file /app/az-ai-v2 | grep -q ELF`, `/app/az-ai-v2 --version`) turn a
  silent managed-fallback regression into a red build at the publish
  layer instead of a cryptic COPY miss ~30s later in the runtime stage.
  Non-negotiable per Lippman's Round-2 playbook update
  (`docs/launch/release-v2-playbook.md` §Troubleshooting). `apk add file`
  added to the build stage so the `file(1)` check works on Alpine.
- **Version strings bumped** — csproj `<Version>`, `Program.cs`
  `VersionSemver`/`VersionFull`, `Observability/Telemetry.cs`
  `ServiceVersion`, `packaging/tarball/stage.sh` `VERSION`,
  `tests/integration_tests.sh` Gate 2 assertion — all rolled
  `2.0.1 → 2.0.2` in lock-step.

### Packaging
- **Versioned-pin manifests for 2.0.2** — new frozen siblings:
  `packaging/homebrew/Formula/az-ai-v2@2.0.2.rb`,
  `packaging/scoop/versions/az-ai-v2@2.0.2.json`, plus a `"2.0.2"` entry
  in `packaging/nix/flake.nix` `pinnedHashes`. SHA256 / SRI slots carry
  `TODO_FILL_AT_RELEASE_TIME` / `lib.fakeHash` sentinels per the tag-time
  ritual in `packaging/README.md`. Tracking manifests (`az-ai.rb`,
  `az-ai.json`, flake `version`) rolled to `2.0.2` with sentinels intact
  — they never got hash-synced for v2.0.1 (release didn't publish), so
  no prior hash state to reset.
- **`@2.0.0.rb` + `@2.0.0.json` + flake `"2.0.0"` pinnedHash entry**
  retained unchanged as historical markers.
- **`@2.0.1.rb` + `@2.0.1.json` + flake `"2.0.1"` pinnedHash entry**
  retained unchanged as historical markers — v2.0.1 joins v2.0.0 as a
  "tagged but never published" marker. Two dead-end markers now.

### Note
- v2.0.2 supersedes v2.0.1, which was tagged on `039e6bd` but did not
  publish (same publish-gate failure mode as v2.0.0 attempt #1 — the
  Alpine SDK swap in v2.0.1 did not address root cause). The immutable
  `v2.0.1` tag remains in the repo for audit trail; no artifacts were
  ever uploaded under that tag.

## [2.0.1] — 2026-04-21

> **Fix-forward from v2.0.0.** The `v2.0.0` tag on `b1fd2cd` is immutable
> and remains in the repo as an "attempted release" marker, but no
> artifacts published — `release.yml` run
> [24736776551](https://github.com/SchwartzKamel/azure-openai-cli/actions/runs/24736776551)
> failed in `build-binaries-v2 / win-x64` and `docker-publish-v2` before
> `release-v2` could run. v2.0.1 is the first publicly published v2.x
> release and supersedes the v2.0.0 tag on every channel (tarballs, GHCR,
> Homebrew, Scoop, Nix). Post-mortem:
> [`docs/launch/v2-release-attempt-1-diagnostic.md`](docs/launch/v2-release-attempt-1-diagnostic.md).

### Fixed
- **`stage.sh` win-x64 packaging** — `packaging/tarball/stage.sh` no
  longer shells out to Info-ZIP `zip` on `win-*` RIDs. `windows-latest`
  runners don't ship `zip`, and neither does the bundled Git-for-Windows
  MSYS bash. The Windows branch now uses PowerShell's always-present
  `Compress-Archive` (via `powershell.exe -NoProfile`), with `cygpath`
  translation so the MSYS paths resolve on the Windows side. Archive
  layout preserved (`az-ai-v2-<ver>-<rid>/` top-level directory) — brew /
  scoop manifests unchanged.
- **`Dockerfile.v2` NativeAOT cross-libc mismatch** — build stage
  switched from Debian-glibc `dotnet/sdk:10.0` to musl-native
  `dotnet/sdk:10.0-alpine`. Publishing for `linux-musl-x64` from a glibc
  host was silently emitting no ELF at `/app/az-ai-v2` (ILC cross-link
  fell through without a diagnostic; `docker-publish-v2` failed at
  `COPY --from=build /app/az-ai-v2 ...: not found`). With host libc and
  target RID both musl, no cross-link is required. Build-stage packages
  updated from `apt-get install clang zlib1g-dev` to `apk add clang
  build-base zlib-dev`. Runtime-deps stage (`runtime-deps:10.0-alpine`)
  unchanged. Image is structurally unchanged from the consumer side.
- **Telemetry `ServiceVersion` drift** — `Observability/Telemetry.cs`
  still reported `"2.0.0-alpha.1"` on OTel spans and meters even after
  the 2.0.0 tag. Corrected to `"2.0.1"` on this release. Flagged by
  Frank in [`docs/ops/telemetry-schema-v2.0.0.md`](docs/ops/telemetry-schema-v2.0.0.md).
- **AOT size figures reconciled (documentation-only).** Prior
  `[2.0.0]` entry reported 15.10 MB / 1.62× / shipped-with-waiver. The
  real shipped AOT size is **12.91 MB / 1.456× / no waiver required**,
  matching `docs/release-notes-v2.0.0.md`, `docs/perf-baseline-v2.md`,
  `docs/aot-trim-investigation.md`, and the `OptimizationPreference=Size`
  + `StackTraceSupport=false` levers landed on the csproj in `056920f`.
  The 15.10 MB figure was the pre-trim measurement and should never
  have stayed in the CHANGELOG. Corrected in this release. Ground truth:
  `docs/aot-trim-investigation.md` §Levers (row 7+8 = 13,533,472 bytes).

### Packaging
- **Versioned-pin manifests for 2.0.1** — new frozen siblings:
  `packaging/homebrew/Formula/az-ai-v2@2.0.1.rb`,
  `packaging/scoop/versions/az-ai-v2@2.0.1.json`, plus a `"2.0.1"` entry
  in `packaging/nix/flake.nix` `pinnedHashes`. SHA256 / SRI slots carry
  the `TODO_FILL_AT_RELEASE_TIME` / `lib.fakeHash` sentinels per the
  tag-time ritual in `packaging/README.md`. Tracking manifests
  (`az-ai.rb`, `az-ai.json`) already at `version "2.0.1"` from the prior
  G6 sweep.

### Docs
- **v2.0.0 release-attempt #1 post-mortem committed** —
  [`docs/launch/v2-release-attempt-1-diagnostic.md`](docs/launch/v2-release-attempt-1-diagnostic.md)
  (Lippman). Captures the job-matrix outcome, the two failure roots, and
  the recommended fix-forward path — what landed in this release.
- **v2 release playbook §Troubleshooting extended** —
  [`docs/launch/release-v2-playbook.md`](docs/launch/release-v2-playbook.md)
  gains the two observed failure-mode recipes so the next red run has a
  recipe-level fix already on-page.
- **Release notes carry a v2.0.1 banner** —
  `docs/release-notes-v2.0.0.md` gets an in-place note that v2.0.0 was
  tagged but never published; v2.0.1 supersedes it. Filename is
  preserved to avoid orphaning ~13 inbound CHANGELOG / contract / launch
  doc references.

## [2.0.0] — 2026-04-20

> Release window opens 2026-04-20 (commit-cutoff date for the 2.0.0 line). See
> [`docs/release-notes-v2.0.0.md`](docs/release-notes-v2.0.0.md) for the
> user-facing narrative and [`docs/migration-v1-to-v2.md`](docs/migration-v1-to-v2.md)
> for upgrade guidance.

v2.0.0 replaces ~2,200 lines of hand-rolled chat / tool / workflow code with
Microsoft Agent Framework (MAF) primitives and ships alongside v1 as a
separate binary (`az-ai-v2`) during the dual-tree window. For end users
invoking `az-ai` from the command line, Espanso, or AutoHotkey, no flag,
env var, config file, or exit code changes behavior. The major-version bump
is driven by the public transitive dependency surface — MAF, OpenTelemetry,
and an Azure SDK bump — not by CLI contract changes.

### Added
- **Microsoft Agent Framework runtime** ([ADR-004](docs/adr/ADR-004-agent-framework-adoption.md),
  commit `0b2e655`) — `ChatClientAgent`, `AgentThread`, and MAF function-tool
  primitives back the chat, agent, and Ralph code paths. The v1 bespoke loop
  is gone.
- **Persona routing wired end-to-end** (commits `0b2e655`, `cbcc49b`) —
  `--persona <name>` and `--persona auto` now overlay the persona's system
  prompt, tool allow-list, and `.squad/history/<name>.md` memory, force
  agent mode on, and update memory on session exit. In earlier v2 previews
  the flag was parsed and silently ignored. Full reference in
  [`docs/persona-guide.md`](docs/persona-guide.md).
- **Cost estimator** — [FR-015](docs/proposals/FR-015-pattern-library-and-cost-estimator.md),
  commit `0b2e655`. `--estimate` (alias `--dry-run-cost`) prints predicted
  USD for a prompt without making an API call;
  `--estimate-with-output <n>` adds a worst-case output cost for `n`
  completion tokens. The estimator short-circuits before credential or
  endpoint resolution — safe to call from CI budget gates offline.
- **Opt-in OpenTelemetry observability** (commit `0b2e655`) — `--telemetry`
  (or `AZ_TELEMETRY=1`) emits spans and per-call cost events to stderr.
  `--otel` / `--metrics` narrow the export to traces or meters only.
  Suppressed entirely by `--raw`. Zero allocation on the hot path when
  disabled.
- **Connection prewarming** — [FR-007](docs/proposals/FR-007-parallel-startup-and-connection-prewarming.md),
  commit `8e53851`. `--prewarm` opens the Azure OpenAI connection in
  parallel with prompt assembly to cut latency on the first interactive
  token.
- **Shell + streaming agent parity with v1** — [FR-005](docs/proposals/FR-005-shell-integration-and-output-intelligence.md)
  and [FR-011](docs/proposals/FR-011-agent-streaming-output.md), verified
  in commit `8e53851`.
- **Nine new flags reaching v1 parity plus v2 additions** (commit `0b2e655`):
  `--estimate`, `--estimate-with-output`, `--telemetry`, `--otel`,
  `--metrics`, `--prewarm`, `--config <path>`, `--schema <json>` (captured
  but not yet enforced on the wire — see _Deprecated / deferred_),
  `--version --short`.
- **`.azureopenai-cli.json` config reference** (commit `a309154`) — new
  `docs/configuration-reference.md` with sample file and WSL path gotchas.
- **v2 user documentation** (commit `cbcc49b`) —
  [`docs/persona-guide.md`](docs/persona-guide.md) (recreated for v2),
  [`docs/migration-v1-to-v2.md`](docs/migration-v1-to-v2.md), and a slimmer
  `docs/use-cases.md`. README refreshed for v2.
- **Perf baseline harness** (commit `3de364a`) — `scripts/bench.sh` and
  [`docs/perf-baseline-v2.md`](docs/perf-baseline-v2.md) provide the first
  v1↔v2 comparison (50 runs, warmup 2, linux-x64 AOT + framework-dependent).
- **OSS license artifacts** (commit `81a1e3a`) — `NOTICE` and
  `THIRD_PARTY_NOTICES.md` at the repo root, with an accompanying
  [`docs/licensing-audit.md`](docs/licensing-audit.md). Every 39-package
  dependency is MIT, Apache-2.0, or BSD-3-Clause. No copyleft.
- **ADR-006 split** (commit `cf7901b`) — original roundtable ADR decomposed
  into three focused ADRs (`ADR-006-nvfp4-nim-integration.md`,
  `ADR-007-third-party-http-provider-security.md`,
  `ADR-008-gpu-provider-bench-policy.md`) with verbatim appendix preserved.
- **v2 integration suite** (commit `488aebd`) — `tests/integration_tests.sh`
  now exercises v1 and v2 in parallel (29 assertions across 14 cases).

### Changed
- **Packaged as `az-ai-v2` during the dual-tree window.** The v1 binary
  continues to install as `az-ai`. Post-cutover, v2 becomes `az-ai`; v1
  remains available as a pinned version (`azure-openai-cli@1.9.1` on
  Homebrew / Scoop / manual download). See
  [`docs/migration-v1-to-v2.md`](docs/migration-v1-to-v2.md) §4.
- **`--max-rounds` semantics** (migration guide §3) — unchanged as a cap,
  but the loop termination is now driven by MAF's tool-call accounting.
  No behavior change is visible at the CLI boundary; logged round counts
  may differ by ±1 on edge cases.
- **Ralph retry prompt shape** (migration guide §3) — the task is now
  carried by `AgentThread`; only the accumulated error context is
  re-injected on each iteration. v1 re-sent the full original task every
  round. Output and exit codes are unchanged.
- **Streaming ordering guarantees** (migration guide §3) — streamed through
  MAF's `RunStreamingAsync` primitive. Byte-for-byte identical to v1 on
  `--raw`; banner ordering on interactive stderr may differ by a few
  microseconds.
- **Estimator short-circuits before credential resolution** (migration
  guide §3) — `--estimate` does not read `AZUREOPENAIAPI`, does not touch
  the network, and works offline. v1 had no estimator.
- **`OpenAI` transitive bumped 2.1.0 → 2.9.1** (licensing audit, commit
  `0b2e655`) via the MAF stack. Still MIT.
- **NOTICE truthfulness** (commit `81a1e3a`) — v1's NOTICE claimed "all
  dependencies are MIT." v2 introduces Apache-2.0 (OpenTelemetry, 4
  packages) and BSD-3-Clause (Google.Protobuf, 1 package) via
  observability and MAF transitive closure. `NOTICE` is updated to
  reflect that truthfully.

### Deprecated
- **`--schema <json>` wire enforcement is deferred to 2.1.x.** The flag
  is parsed and captured in v2.0.0 but is not yet sent as a
  `response_format` strict schema. Use `--json` + post-validation in the
  meantime.

### Removed
- **Handrolled chat / tool / Ralph orchestration** — the ~2,200-line v1
  loop is replaced by MAF primitives. Not user-visible; called out because
  extension points that reached into those internals (there are none in
  the public API, but downstream forks may have relied on them) no longer
  exist.
- **`.smith/` scratch directory** (commits `781741f`, `cd5fdc6`, `ce377f6`,
  `b654d97`) — internal scratch state; never shipped as an API.

### Fixed
- **[FR-017](docs/proposals/FR-017-max-completion-tokens-compatibility.md)
  baked in** — `gpt-5.x`, `o1`, and `o3` deployments no longer crash on
  the `max_completion_tokens` wire property. Originally shipped as a v1.9.1
  hotfix; v2 incorporates the fix from day one.

### Security
- **v1 hardening preserved byte-for-byte.** Every `ShellExecTool`,
  `WebFetchTool`, `ReadFileTool`, `GetClipboardTool`, and `ToolRegistry`
  defense landed in v1.0.x–v1.9.x carries forward unchanged in v2:
  command blocklist and metacharacter rejection, `ArgumentList`-based
  process spawning, HTTPS-only web fetch, private-IP DNS rebinding block,
  redirect-final-URL validation, symlink-traversal blocking, exact-alias
  tool matching, CTRL+C signal handling with exit code 130.
- **License audit: clear** (commit `81a1e3a`,
  [`docs/licensing-audit.md`](docs/licensing-audit.md)). 39 packages
  reviewed. 34 MIT, 4 Apache-2.0, 1 BSD-3-Clause. Zero copyleft.
  Attribution obligations discharged via `NOTICE` +
  `THIRD_PARTY_NOTICES.md`.

### Performance
- **Shipping-form (AOT, linux-x64) startup gates pass**
  ([`docs/perf-baseline-v2.md`](docs/perf-baseline-v2.md)). `--version
  --short` p95 is 1.12× v1 (12.58 ms mean). `--help` p95 is 1.23× v1.
  `parse-heavy` is _faster_ than v1 (0.93× mean) — the ParseArgs rework
  vindicated. Memory RSS is at or below v1 for every scenario.
- **AOT binary size: 8.86 MB → 12.91 MB (1.456×, +4.05 MB).** Inside
  the proposed 1.5× ratio gate — **passes without a waiver.** Cause:
  MAF host assemblies (whole-subgraph DI), OpenTelemetry API +
  exporters, and `Azure.AI.OpenAI 2.1.0` trim warnings that prevent
  full elision. `OptimizationPreference=Size` and
  `StackTraceSupport=false` (commit `056920f`) trimmed ~1.5 MB off an
  initial 14.41 MB / 1.625× build. A further residual-reflection trim
  pass is tracked for 2.1.x but is not blocking. Full lever analysis in
  [`docs/aot-trim-investigation.md`](docs/aot-trim-investigation.md).
- **Framework-dependent (`dotnet <dll>`) startup is ~1.5–1.7× v1.** This
  is not the shipping form — it exercises the JIT path, where MAF / DI /
  OTel types pay a one-time compile cost. Documented for completeness;
  not a gate.

### Verified
- `488 / 488` v2 unit tests passing; v1 suite continues green in parallel.
- 29 integration assertions across 14 cases against both binaries.
- AOT publish clean on `linux-x64`; IL2104 / IL3053 warnings confined to
  third-party assemblies, tracked for the 2.0.1 trim pass.

## [1.9.1] - 2026-04-20

### Fixed
- **gpt-5.x / o1 / o3 compatibility** ([FR-017](docs/proposals/FR-017-max-completion-tokens-compatibility.md)) — Chat
  Completions now send `max_completion_tokens` on the wire instead of the
  legacy `max_tokens`, unblocking modern Azure OpenAI Responses-API
  deployments that reject the old parameter with HTTP 400. Applied via the
  `SetNewMaxCompletionTokensPropertyEnabled()` opt-in at both the standard
  and Ralph iteration call sites. Safe for older models too — `gpt-4o`,
  `gpt-4o-mini`, and earlier accept both field names.
- **AOT reflection regression** ([FR-016](docs/proposals/FR-016-aot-reflection-regression-hotfix.md)) — Native AOT
  binary no longer throws `InvalidOperationException: Reflection-based
  serialization has been disabled` when streaming from modern endpoints.
  Fixed incidentally by the SDK upgrade below. AOT binary is now 8.9 MB
  (down from 9.1 MB), zero new trim/AOT warnings.

### Changed
- Upgraded `Azure.AI.OpenAI` `2.1.0` → `2.9.0-beta.1` (pulls `OpenAI` `2.1.0` →
  `2.9.1`). Required for `max_completion_tokens` support and AOT cleanliness.

### Verified
- End-to-end against `gpt-5.4-nano` on
  `api-version=2025-04-01-preview` (both JIT and AOT).
- `1001/1001` unit tests passing.

## [Unreleased - pre-1.9.1 notes]

### Added
- **`az-ai --completions <shell>`** — Emit shell-completion scripts for
  `bash`, `zsh`, and `fish`. Pipe into your rc file (e.g.
  `az-ai --completions bash >> ~/.bash_completion`) for flag / subcommand
  tab-completion.
- **Packaging scaffolds** — Homebrew formula, Scoop manifest, and Nix flake
  under [`packaging/`](packaging/) for third-party distribution channels.
  Not yet submitted to upstream taps/buckets; see `packaging/README.md`.
- **Verifying Releases guide** — [`docs/verifying-releases.md`](docs/verifying-releases.md)
  walks users through cosign / GitHub attestation verification for binaries,
  container images, and SBOMs.
- **Cost Optimization guide** — [`docs/cost-optimization.md`](docs/cost-optimization.md)
  covers model selection, token budgeting, caching, and per-persona cost
  profiles.
- **Trademark Policy** — [`docs/legal/trademark-policy.md`](docs/legal/trademark-policy.md)
  clarifies permitted use of the "Azure OpenAI CLI" / `az-ai` names.
- **ADR-002: Squad persona + memory architecture** —
  [`docs/adr/ADR-002-squad-persona-memory.md`](docs/adr/ADR-002-squad-persona-memory.md)
  records the persona-config + `.squad/` memory design.
- **FR-012 proposal: Plugin / tool registry** —
  [`docs/proposals/FR-012-plugin-tool-registry.md`](docs/proposals/FR-012-plugin-tool-registry.md)
  outlines a future extensibility surface for third-party tools.
- **Asciinema demo scripts** — [`docs/demos/`](docs/demos/) ships rehearsed
  terminal demos for standard, raw, and agent modes, ready for recording.
- **Contributor onboarding** — New `CONTRIBUTORS.md`, structured issue forms
  under `.github/ISSUE_TEMPLATE/`, and a PR template at
  [`.github/PULL_REQUEST_TEMPLATE.md`](.github/PULL_REQUEST_TEMPLATE.md).
- **Property-based tests for `CliParser`** — Boundary and invariant coverage
  using FsCheck-style generators. Total test count **541 → 925**.

### Changed
- **CI matrix expanded to `{ubuntu, macos}`** — `build-and-test` now runs
  on Ubuntu **and** macOS in parallel (fail-fast off), catching
  platform-specific regressions before release. `windows-latest` was in
  scope for this Golden Run but surfaced 26 pre-existing POSIX-path
  assumptions (`/bin/sh`, `/etc/passwd`, `/root/.ssh/*`) in
  `ToolHardeningTests` / `SecurityToolTests` that predate this release.
  Tracked as a v1.9 follow-up; the cross-OS *publish* matrix in
  `release.yml` continues to build + smoke-test `win-x64` / `win-arm64`
  binaries, so shipped Windows artifacts remain covered.

## [1.8.1] — 2026-04-19

### Fixed
- **Release workflow: CycloneDX SBOM tool pin** — The v1.8.0 release workflow
  failed in all five `build-binaries` matrix jobs because
  `dotnet tool install --global CycloneDX --version 4.0.2` referenced a version
  that does not exist on NuGet (latest is 6.x). As a result, no binary artifacts
  or binary-provenance attestations shipped for v1.8.0 (the container image and
  its attestation shipped cleanly and remain the anchor for `v1.8.0`). The tool
  is now pinned via a `.config/dotnet-tools.json` local manifest (CycloneDX
  6.1.1), restored with `dotnet tool restore`, and invoked as
  `dotnet dotnet-CycloneDX` with the current `--output-format Json` flag.
  Dependabot will surface future CycloneDX bumps as reviewable PRs.
- **Release runbook** — New `docs/runbooks/release-runbook.md` codifies the
  pre-flight checklist, tag-and-push sequence, post-release verification, and
  rollback / hotfix procedure so the next release does not re-discover the
  lessons learned from v1.8.0.

## [1.8.0] — 2025-11-20

### Changed
- **Native AOT promoted from experimental to recommended** — `make publish-aot`
  produces a **~9 MB single-file binary with ~5.4 ms cold start** on Linux x64,
  compared to ~54 ms for ReadyToRun and ~400+ ms for the Docker container path.
  That is roughly **10× faster than R2R and ~75× faster than Docker**, which is
  significant for Espanso/AutoHotKey text-injection workflows where every key
  sequence spawns a fresh process. `make publish` is now an alias for
  `publish-aot`; `publish-fast` (ReadyToRun) is retained for compatibility.
- **`Azure.AI.OpenAI` downgraded to 2.1.0 (stable GA)** — The project previously
  tracked the `2.9.0-beta.1` pre-release for tool-calling coverage. Tool calling
  works correctly on the stable `2.1.0` release, so the dependency has been
  moved back to a supported GA build. This removes pre-release transitive
  packages from the supply chain.
- **Remaining AOT warnings fixed** — Migrated `SquadConfig.Load` / `Save` and
  `SquadInitializer.Initialize` off reflection-based `JsonSerializer` overloads
  onto source-generated `AppJsonContext.Default.SquadConfig`. Added
  `ReadCommentHandling`, `AllowTrailingCommas`, and `PropertyNameCaseInsensitive`
  to `AppJsonContext`'s shared options so Squad config parsing stays forgiving.
- **Anonymous type eliminated in `OutputJsonError`** — Replaced with the new
  `ErrorJsonResponse` record registered in `AppJsonContext`.
- **`DelegateTaskTool` single-file safety** — Replaced
  `Assembly.GetExecutingAssembly().Location` (empty in single-file/AOT builds)
  with `Environment.ProcessPath` + `AppContext.BaseDirectory`. Child agents can
  now be spawned correctly from the AOT-published binary.
- **Retry/backoff logic consolidated** — Shared backoff helper reused across
  streaming and non-streaming code paths (~60 lines of duplication removed)
  without behavior changes.
- **`ParseCliFlags` scoped to `internal`** — Argument parser exposed to the test
  assembly via `InternalsVisibleTo` rather than being part of the public API
  surface.

### Added
- **Graceful cancellation on CTRL+C (SIGINT)** — A top-level signal handler
  cancels the in-flight operation, flushes the Ralph log / persona memory, and
  exits with code **130** (128 + SIGINT) per POSIX convention. Previously a
  CTRL+C could leave `.ralph-log` partially written.
- **Cross-platform publish targets** — New Makefile targets for all 7 supported
  Runtime Identifiers: `publish-linux-x64`, `publish-linux-musl-x64`,
  `publish-linux-arm64`, `publish-osx-x64`, `publish-osx-arm64`,
  `publish-win-x64`, `publish-win-arm64`, plus an aggregate `publish-all`.
- **`make install` / `make uninstall`** — Installs the AOT binary as `az-ai`
  on the user's `PATH` (`~/.local/bin` on Linux/macOS) and removes it.
- **`make bench`** — Invokes `scripts/bench.py` to measure cold-start latency
  of the locally-built AOT binary against a configurable number of runs.
- **`scripts/bench.py`** — Portable Python startup benchmark that captures
  wall-clock invocation time with statistical summaries (min/median/p95/max).
- **`CliParser` test coverage** — 71 new unit tests covering flag parsing,
  precedence, validation, and error paths.

### Tests
- Suite now passes **538 tests** (up from 454) — primarily from new
  `CliParser` coverage and cancellation tests.

The only remaining AOT publish warnings come from third-party assemblies
(`Azure.AI.OpenAI`, `OpenAI`) and do not affect runtime behavior.

## [1.7.0] — 2025-07-21

### Added
- **Token usage tracking** — Displays `[tokens: X→Y, Z total]` on stderr after every API call. Included in `--json` output as `input_tokens` and `output_tokens` fields
- **`--raw` flag** — Suppresses all formatting (no spinner, no newline, no stderr output). Designed for Espanso/AHK text expansion integration
- **TTY-aware output** — Spinners auto-suppress when stdout is piped (`Console.IsOutputRedirected`). Works even without `--raw`
- **Espanso/AHK integration guide** — New `docs/espanso-ahk-integration.md` with working configs for Espanso (Linux/macOS/Windows) and AutoHotKey v2
- **24 new tests** (454 total unit tests)

### Changed
- **AOT anonymous type elimination** — Replaced 2 anonymous types with source-generated `ChatJsonResponse` and `AgentJsonResponse` records in `AppJsonContext`. Removes last Native AOT blocker
- **ErrorAndExit DRY helper** — Extracted shared error handler replacing 8 duplicated error patterns (~40 lines saved). Consistent `[ERROR]` prefix across all error paths
- **DotEnv resilience** — `.env` file loading now wrapped in try-catch (missing/malformed `.env` no longer crashes)

### Security
- **Shell injection hardening** — `ShellExecTool` now blocks `$()`, backticks, process substitution (`<()`, `>()`), `eval`, and `exec`. Switched from string-interpolated `Arguments` to `ArgumentList` for proper OS-level escaping

## [1.6.0] — 2025-07-20

### Added
- **JSON Source Generators (AOT)** — `JsonGenerationContext.cs` with `AppJsonContext` providing source-generated serialization for `UserConfig`, `SquadConfig`, `PersonaConfig`, and all Squad types. Unblocks Native AOT compilation
- **CLI Validation** — Temperature validated to 0.0–2.0 range, max-tokens validated to 1–128000 range in `ParseCliFlags`
- **Rate-limit aware backoff** — Streaming retry now respects `Retry-After` header from Azure API (capped at 60s)
- **60+ new tests** — `JsonSourceGeneratorTests` (16), `ToolHardeningTests` (33), `ProgramTests` validation (11), integration tests (8 new)

### Changed
- **Dockerfile Optimization** — Added `PublishReadyToRun=true` for ~50% startup improvement; improved layer caching by copying `.csproj` first, restoring, then copying source
- **Makefile** — Fixed stale .NET 9.0 references → .NET 10.0

### Security
- **WebFetchTool SSRF redirect protection** — Validates final URL after HTTP redirects (HTTPS-only, no private IPs)
- **Tool parameter hardening** — All tools replaced `GetProperty()` with `TryGetProperty()` for graceful error handling on missing parameters

### Fixed
- **Console.Out race condition** — `RunRalphLoop` now guarantees `Console.Out` restoration via try-finally

## [1.5.0] — 2026-04-09

### Added
- **Persona System** inspired by [bradygaster/squad](https://github.com/bradygaster/squad) — AI team members with persistent memory
- `--persona <name>` flag: select named persona (coder, reviewer, architect, writer, security)
- `--persona auto`: auto-route to best persona via keyword-based routing
- `--personas`: list available personas from `.squad.json`
- `--squad-init`: scaffold `.squad.json` and `.squad/` directory with default team
- Persistent persona memory in `.squad/history/` — knowledge compounds across sessions
- Shared decision log in `.squad/decisions.md`
- `SquadCoordinator` for intelligent task routing with keyword scoring
- `SquadConfig` for JSON-based team configuration (`.squad.json`)
- `PersonaMemory` for per-persona history management with 32 KB cap and tail truncation
- `SquadInitializer` for scaffolding default squad with 5 personas and routing rules
- 5 default personas with specialized system prompts and tool selections
- ~46 new unit tests for Squad system
- Zero new dependencies — built entirely with `System.Text.Json`

## [1.4.0] — 2025-07-13

### Added
- **Ralph Mode** (`--ralph`): Autonomous Wiggum loop for self-correcting agent workflows
- `--validate <cmd>`: External validation command for Ralph loop iterations
- `--task-file <path>`: Read task prompt from file
- `--max-iterations <n>`: Control Ralph loop iteration limit (default: 10, max: 50)
- **DelegateTaskTool**: New built-in tool for subagent calling (`delegate_task`)
- Subagent recursion depth control via `RALPH_DEPTH` env var
- `.ralph-log` iteration history file
- 44 new tests (28 Ralph mode unit tests + 16 delegate tool tests)
- 20 new integration tests for Ralph mode flags

## [1.3.0] — 2025-04-09

### Security
- **ReadFileTool**: Fixed symlink traversal vulnerability; added prefix-based path blocking
- **ShellExecTool**: Expanded blocked commands (sudo, su, crontab, vi, vim, nano, nc, ncat, netcat, wget); close stdin on child process; HasExited guard before Kill
- **WebFetchTool**: Added DNS rebinding protection (private IP blocklist); limited redirects to 3; dynamic User-Agent from assembly version
- **GetClipboardTool**: Enforced clipboard size cap with truncation warning; PATH-based command detection
- **ToolRegistry**: Replaced substring matching with exact alias dictionary

### Added
- Parallel tool call execution via `Task.WhenAll` for concurrent agent tool rounds
- Accurate tool call counting in JSON output (`tools_called` field)
- CI: code formatting check (`dotnet format --verify-no-changes`)
- CI: NuGet vulnerability audit (`dotnet list package --vulnerable`)
- CI: Trivy container image scanning (CRITICAL/HIGH severity)
- CI: integration test job
- Makefile: `format`, `format-check`, `audit`, `all-tests` targets
- 104 new security unit tests (138 total)
- 11 parallel execution unit tests

### Changed
- Agent loop now executes multiple tool calls concurrently instead of sequentially
- Tool name matching uses explicit alias dictionary instead of broad substring search

## [1.2.0] — 2026-04-08

### Added
- **Agentic mode** (`--agent`): model can call built-in tools before responding
- 5 built-in tools: `shell_exec`, `read_file`, `web_fetch`, `get_clipboard`, `get_datetime`
- `--tools <list>` flag to restrict which tools are available (comma-separated)
- `--max-rounds N` flag to limit tool-calling iterations (default: 5)
- Agent-aware system prompt injection with available tool names
- JSON output includes agent metadata (rounds, tools_called) when `--agent --json` combined
- Tool safety: shell command blocklist, HTTPS-only web fetch, file size caps, path blocking
- Unit tests for tool registry and built-in tools
- Integration tests for agent mode CLI flags

### Changed
- Upgraded Azure.AI.OpenAI from 2.1.0 to 2.9.0-beta.1 (required for tool calling)
- Upgraded Azure.Core from 1.47.2 to 1.51.1
- `--json` flag now detected anywhere in args (previously required first position)
- Removed experimental `SetNewMaxCompletionTokensPropertyEnabled` call (incompatible across SDK versions)

### Security
- Shell tool blocks dangerous commands (rm, kill, mkfs, dd, etc.) and pipe chains containing them
- Shell command timeout (10s) and output size cap (64KB)
- File read tool blocks sensitive paths (/etc/shadow, /etc/passwd, etc.)
- Web fetch enforces HTTPS-only with timeout and response size cap

## [1.1.0] — 2026-04-08

### Added
- Stdin pipe support: `echo "question" | az-ai`, `cat file | az-ai "summarize"` — combines piped content with prompt arguments
- `--json` output mode for scripting and automation
- `--version` / `-v` flag to display current version
- Progress spinner (braille animation) on stderr while waiting for first token
- Input validation with 32K character prompt limit
- Azure-specific exception handling for HTTP 401, 403, 404, and 429 responses
- Configurable streaming timeout via `AZURE_TIMEOUT` environment variable
- Configurable `AZURE_MAX_TOKENS` and `AZURE_TEMPERATURE` environment variables
- Restrictive file permissions (chmod 600) on config file at creation
- API key validation before client creation
- HTTPS endpoint validation
- xUnit test project with 16 unit tests
- GitHub Actions CI/CD pipeline
- SECURITY.md — comprehensive security documentation
- ARCHITECTURE.md — system design and component documentation
- CONTRIBUTING.md — developer onboarding guide
- CODE_OF_CONDUCT.md — Contributor Covenant v2.1
- GitHub issue templates (bug report, feature request)
- Pull request template
- Copilot agent archetypes: Costanza (PM), Kramer (engineer), Newman, Elaine, Jerry
- 5 feature proposals (FR-001 through FR-005) with priority matrix and shipping timeline

### Changed
- Upgraded from .NET 9.0-preview to .NET 10.0 stable
- Upgraded Dockerfile base images from preview tags to stable
- Optimized Dockerfile layer ordering for faster rebuilds
- Removed redundant COPY instruction in Dockerfile
- Credentials now injected via `--env-file` at runtime instead of baked into image
- Makefile: added `help`, `test`, and `smoke-test` targets
- README: added badges, configuration reference table, and exit code documentation

### Fixed
- Removed `.env` credential bundling from Docker image (security vulnerability)
- Generic exception handling replaced with Azure-specific error handlers
- Stream null check for content delta updates

### Security
- Container credentials are no longer baked into Docker images
- API key is validated before Azure OpenAI client creation
- HTTPS endpoint validation prevents insecure connections
- Config file restricted to owner-only access (chmod 600 on Unix)

## [1.0.1] — 2025-12-04

### Added
- Multi-model selection support: `--models`, `--set-model`, `--current-model` flags
- Feature proposals README with priority matrix
- Copilot agent definitions (Costanza, Kramer)

### Fixed
- Improved exception handling based on code review feedback (modern C# range syntax, named constants)

## [1.0.0] — 2025-08-17

### Added
- Initial release
- Azure OpenAI chat completion via Docker container
- Streaming responses (token-by-token output)
- System prompt configuration via `SYSTEM_PROMPT` environment variable
- Docker-first architecture with Alpine Linux base image
- Non-root container execution (`appuser`)
- `.dockerignore` for minimal build context

### Security
- Switched to Alpine Linux for reduced attack surface (OWASP/Snyk compliance)
- Fixed 2 critical and several high-severity container vulnerabilities
- Vulnerability scanning integrated into workflow

---

## Cancelled-release policy

A **cancelled release** is a git tag in the `vX.Y.Z` sequence for which the
release pipeline started but did **not** complete its full publish contract.
The tag is immutable — it is never deleted, retagged, or rewritten — but the
artifacts behind it are partial or absent. This policy defines how the
project handles those cases so users can tell at a glance whether a tag
corresponds to a shipped release.

### Taxonomy

| State | Git tag | GitHub Release | Tarballs | GHCR image | Homebrew / Scoop / Nix | User action |
|---|---|---|---|---|---|---|
| **Shipped** | ✅ exists | ✅ published | ✅ uploaded | ✅ pushed | ✅ hash-synced | Use it. |
| **Cancelled — Docker-only** | ✅ exists | ❌ none | ❌ none | ✅ pushed | ❌ not synced | Docker users may pull the image; everyone else skips to the next shipped version. |
| **Cancelled — nothing published** | ✅ exists | ❌ none | ❌ none | ❌ none | ❌ not synced | Skip the tag entirely; use the next shipped version. |
| **Attempted** | ✅ exists | ❌ none | ⚠️ partial | ⚠️ partial | ❌ not synced | Skip the tag; treat as historical marker only. |

### Rules

1. **Tags are immutable.** Once pushed, a `vX.Y.Z` tag is never deleted or
   moved, even if the release pipeline fails. This preserves the git
   history for post-mortems and keeps third-party references (SBOMs,
   lockfiles, blog posts) valid.
2. **Version numbers are not reused.** The next release takes the next
   SemVer number (`X.Y.(Z+1)`), regardless of how much of the cancelled
   release actually published. v2.0.3 cancelled → v2.0.4 is next.
3. **Partial artifacts are not retroactively completed.** If Docker
   published but binaries did not, the project does **not** go back and
   upload tarballs under the cancelled tag. The supersede-forward path
   ([`v2.0.4`](#204--2026-04-22) in the v2.0.3 case) carries the fix.
4. **Packaging manifests skip cancelled tags.** `az-ai.rb` /
   `az-ai.json` / flake `latestHashes` roll directly to the next
   shipped version. Frozen per-version pins
   (`az-ai-v2@2.0.3.rb`, etc.) are **not** created for cancelled
   releases.
5. **Cancelled entries stay in the changelog.** They are labelled
   `— *Cancelled release*` in the heading, with a callout paragraph
   explaining what shipped, what did not, and which tag supersedes.

### Known cancelled releases

| Tag | State | Notes |
|---|---|---|
| [`v2.0.1`](#201--2026-04-21) | Attempted | Tagged on `039e6bd`; `docker-publish-v2` failed on AOT asset-graph mismatch. Superseded by v2.0.2. |
| [`v2.0.3`](#203--2026-04-22--cancelled-release) | Cancelled — Docker-only | GHCR `2.0.3` is live; no GitHub Release. Superseded by v2.0.4. |
