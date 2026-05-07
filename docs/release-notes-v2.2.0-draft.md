# Azure OpenAI CLI v2.2.0 -- Release Notes (DRAFT)

> **Status: DRAFT.** This document is pre-staged by Mr. Lippman ahead of
> the v2.2.0 cut. When v2.2.0 ships, this file is renamed to
> `release-notes-v2.2.0.md` and re-reviewed against the final CHANGELOG
> entry. Do not link external announcements at this filename until the
> rename lands.
>
> A minor bump on the v2 line. No breaking changes -- v2.0.x and v2.1.x
> users upgrade in place. v1.x users start with
> [`docs/migration-v1-to-v2.md`](migration-v1-to-v2.md) and the
> `make migrate-check` / `migrate-clean` targets that landed in v2.1.0.

🚀

## Headline

v2.2.0 is the **bash hardening + brevity doctrine + structured prompting**
release. Three story arcs land in one tag: S03E01 closes the bash
injection surface that was lurking across eleven Espanso clipboard
triggers and unifies all 22 Windows-to-WSL triggers on a single proven
pattern; S03E02 hires Lt. Bookman, codifies a five-tier output-length
doctrine, and ships two new triggers (`:aishort`, `:aiyml`) that prove
the fleet can extend itself from inside Espanso; and the prompt-templates
feature pairs a master system prompt with five canonical task templates
(Knowledge Q&A, Architecture, Code Generation, Data Workflow, Cost/ROI)
wired into both Espanso and AutoHotkey.

No new required flags, no new env vars, no changes to existing exit
codes or `--raw` / `--json` output bytes.

## Why you'll care

- **The Espanso WSL kit is now stable under edit.** Every trigger uses
  the same `shell: powershell` + here-string + `wsl.exe -e bash -lc`
  pattern. The fragile `shell: cmd` + folded scalar approach that
  required hand-counted three-layer escaping is retired -- and lint-
  enforced retired (`scripts/lint-espanso-yml.sh`).
- **Bash injection surface is closed where it can be closed, documented
  where it cannot.** Form triggers that previously concatenated user
  input into bash `--system '...'` arguments now route through `WSLENV`
  - environment variables. The residual `'@` here-string risk on
  multi-line free-form prompts is documented in the trust-model header
  rather than papered over.
- **Lt. Bookman runs the brevity desk.** The new tier doctrine
  (Snap / Chat / Document / Mirror / Free) governs `--max-tokens`
  budgets across triggers. Three triggers were tightened, two new ones
  shipped, and mirror-tier triggers (rewrite / translate / fix) are
  intentionally left uncapped because their output length must track
  input.
- **Five canonical task templates ship as a kit.** A master system
  prompt (`docs/prompts/system-prompt-master.md`) plus five task
  templates (`docs/prompts/task-templates.md`) cover the most common
  knowledge-worker shapes: Knowledge Q&A, Architecture review, Code
  Generation, Data Workflow, and Cost / ROI analysis. Espanso triggers
  (`:aiquestion`, `:aiarch`, `:aicode`, `:aidataworkflow`, `:aicost`,
  `:aiprompts`) and AutoHotkey hotkeys (Ctrl+Shift+Q/R/C/D/L plus
  Ctrl+Shift+T for the reference card) wire the templates into the
  same flows.
- **Total trigger count: 22 in `ai-windows-to-wsl.yml`, plus six in
  the new `ai-prompts.yml`.** Up from 13 at the start of S03.

Full machine-readable list lives in
[`CHANGELOG.md`](../CHANGELOG.md#220--2026-04-30).

## What's new (grouped by theme)

### Security hardening (S03E01 *The Yada Yada Strikes Back*)

- **Bash injection closed on form triggers.** `:aitone`, `:aitr`, and
  `:aireply.tone` previously interpolated `{{form1.X}}` choice values
  directly into bash `--system '...'` arguments. A choice value
  containing an apostrophe (e.g. ELI5 / Explain Like a 5-year-old)
  broke the single-quoted argument and opened a command-injection seam.
  Fix: `switch` whitelist mapping -- user input is bounded by the
  choice list, the choice value maps to a hard-coded safe phrase, and
  the bash command line never sees user-controlled text. The ELI5
  choice was renamed (no apostrophe) as belt-and-suspenders.
- **`:aireply.intent` free-form field hardened.** Captured via
  PowerShell here-string, sanitized (strip `"`, CR, LF), passed to
  bash via `WSLENV` + an `AZ_AI_INTENT` environment variable,
  referenced as `"$AZ_AI_INTENT"` inside the bash `--system` arg.
  Espanso's `multiline:false` makes the PS here-string `'@` boundary
  attack unreachable on this surface.
- **Trust-model header documents per-trigger privacy implications.**
  `:aianon` egresses raw PII before redaction; `:aicommit` ships full
  diff including any staged secrets; `:aic` ships clipboard verbatim
  (same secret-egress class as `:aicommit`); `:aireply` ships email
  bodies; `:aitr` ships source text; `:aiyml` writes generated YAML to
  the clipboard, design prompt egresses to Azure. Multi-line free-form
  prompt fields (`:ai`, `:aiweb`, `:aiimg`) carry a residual PS
  here-string `'@` boundary risk that cannot be closed without
  abandoning Espanso template substitution -- documented honestly in
  the header.
- **`FOCUS HAZARD` callout.** SendKeys writes to whichever window has
  foreground focus when each call fires, including the placeholder-
  restore keystrokes that fire in the `finally` block. Users are warned
  not to switch windows while a trigger is in flight.
- **Unified S03 trigger pattern across all 22 triggers.** `shell:
  powershell` + `cmd: |` + `$bash = @'...'@` + `Get-Clipboard -Raw |
  wsl.exe -e bash -lc $bash` (or `$prompt | wsl.exe ...` for form
  triggers). Zero escape-stacking. Single source of truth.
- **`scripts/lint-espanso-yml.sh`** enforces structural invariants on
  `ai-windows-to-wsl.yml` (parse, unique triggers, `$trigger`/
  `trigger:` value match, `$ph` SendKeys-safe charset, exactly two
  BACKSPACE refs in correct order, try+finally+SendWait($trigger)
  restoration). Also rejects the S03E01 root-cause patterns
  (`--system '...{{form1.X}}...'`, `Invoke-Expression` composed with
  form values). Wired into `tests/integration_tests.sh`.

### Tier doctrine (S03E02 *The Library Cop's Word Limit*)

- **Lt. Bookman joins the supporting players.** New cast member at
  `.github/agents/bookman.agent.md`. Owns the response-length tier
  doctrine (canonical source), `--max-tokens` budgets, and the
  system-prompt brevity language ("Output ONLY the X. No preamble. No
  markdown."). Supporting players: 21 -> 22; total fleet: 27 -> 28.
- **Five-tier doctrine.** Snap (60 tok, ~150 char target) /
  Chat (250 tok) / Document (800 tok) / Mirror (1500 tok, length
  tracks input) / Free (4096 tok, user-controlled). Mirror tier
  exists so rewrite / translate / fix-grammar / anonymize triggers
  do not truncate the user's content.
- **Three triggers tightened to the doctrine.** `:aiq` 200 -> 60
  max-tokens with stricter `<=150 char, 1 sentence` system prompt;
  `:aireply` 800 -> 400 with `<=4 short sentences`; `:aitldr` 150
  -> 120 with explicit `<=300 chars total` cap.
- **Mirror-tier triggers intentionally NOT capped.** `:aifix`,
  `:airw`, `:aitone`, `:aitr`, `:aishrink`, `:aiflip`, `:aianon` --
  output length must track input. Free-tier triggers (`:ai`,
  `:aiweb`, `:aiimg`) untouched per design.

### Espanso ergonomics (S03E01 + S03E02)

- **Trigger count: 13 -> 22.** Seven new triggers from S02E37/S03E01
  unification: `:aitr` (translate, 12-language form picker),
  `:aishrink` (~50% length), `:aireply` (email/message reply with
  intent + tone form), `:aicommit` (Conventional Commit message from
  clipboard diff), `:airegex` (explain-or-generate regex), `:aianon`
  (PII redaction), `:aiq` (one-line quick question). Plus two new
  in S03E02: `:aishort` (snap-tier free-form, 60 max-tokens, ~150
  char target) and `:aiyml` (form-input trigger that generates a new
  Espanso YAML block from a natural-language description, places it
  on the clipboard for paste into `ai-wsl.yml`).
- **Self-extensible config.** With `:aiyml` shipped, the fleet can
  scaffold its own next trigger from inside Espanso. The system
  prompt teaches the AI the unified S03 pattern (`$trigger`/`trigger:`
  match, SendKeys-safe `$ph`, BACKSPACE ordering, try/finally retype,
  here-string bash). Output is `Set-Clipboard`'d, not typed -- typing
  a YAML block into a chat app would be a mess.
- **Loading-placeholder dance fixed.** Backspaces the trigger text
  *before* typing the placeholder, then re-types the trigger in
  `finally` so Espanso's own delete-trigger step still lines up with
  what is on screen. Closes the visual glitch where the trigger
  appeared next to the placeholder (`:aifixyada yada yada`).
- **Empty-stdout fallback banner.** All 19 WSL pipelines wrap output
  in `if ([string]::IsNullOrWhiteSpace($out)) { '[az-ai: no response
  -- check connectivity, az-ai install, or env]' }` so silent-failure
  paths surface a diagnostic instead of injecting empty text into the
  user's document.

### Prompt templates (commit `905515e`)

- **Master system prompt** at `docs/prompts/system-prompt-master.md`.
  One canonical voice: clear, precise, actionable; no hallucination;
  cite sources; refuse to emit secrets / PII; output structure of
  Plan -> Details -> Risks -> Next Steps.
- **Five task templates** at `docs/prompts/task-templates.md`:
  - **Knowledge Q&A** -- short factual / explanatory answers with
    citations. Routed at moderate `--max-tokens`, low temperature.
  - **Architecture** -- design-review and trade-off analysis. Asks
    for constraints up front; output is structured (context /
    options / recommendation / risks).
  - **Code Generation** -- language + intent + constraints in,
    runnable snippet + tests + caveats out. Higher `--max-tokens`,
    deterministic temperature.
  - **Data Workflow** -- ETL / SQL / pandas-style transforms.
    Includes schema-first prompting and validation steps.
  - **Cost / ROI** -- finance-grade analysis with assumption
    callouts and sensitivity ranges.
- **Espanso integration** at
  `examples/espanso-ahk-wsl/espanso/ai-prompts.yml`. Six new triggers:
  `:aiquestion`, `:aiarch`, `:aicode`, `:aidataworkflow`, `:aicost`,
  `:aiprompts` (reference card). Each pairs the master system prompt
  with the task-specific guidance and routes to `az-ai` with
  appropriate `--max-tokens` and `--temperature`.
- **AutoHotkey v2 hotkeys** at
  `examples/espanso-ahk-wsl/ahk/az-ai-prompts.ahk`. Ctrl+Shift+Q
  (Question), Ctrl+Shift+R (aRchitecture), Ctrl+Shift+C (Code),
  Ctrl+Shift+D (Data), Ctrl+Shift+L (cost / Ledger), Ctrl+Shift+T
  (reference card). InputBoxes drive the per-template parameters.
- **Setup guide** at
  `examples/espanso-ahk-wsl/PROMPT-TEMPLATES-INTEGRATION.md` with
  examples and troubleshooting.

## Breaking changes

**None.** v2.0.x / v2.1.x -> v2.2.0 is a drop-in upgrade.

- All v2 flags continue to parse and behave identically.
- Env-var contract unchanged (`AZUREOPENAIENDPOINT`, `AZUREOPENAIAPI`,
  `AZUREOPENAIMODEL`, optional Foundry vars).
- `--raw` / `--json` output bytes unchanged.
- Exit codes unchanged.
- `.squad.json` schema unchanged; `.squad/history/` files cross-
  compatible.
- The Espanso trigger-pattern changes are surface-compatible: every
  trigger still matches on the same characters and produces text at
  the same cursor position. The retired `cmd: >` pattern is removed
  from the shipped config, but users running mirrored configs from
  earlier kits will see the new pattern only after they re-mirror
  (see Upgrade notes).

If you have local forks of `ai-windows-to-wsl.yml` that still use the
`shell: cmd` + folded-scalar approach, this release does not break
them -- but you should plan a migration to the unified pattern. Run
`scripts/lint-espanso-yml.sh` against your fork to surface drift.

## Bug fixes

- **`:aifix` / `:aiexp` / `:aitldr` / `:airw` / `:aic` / `:aiimg` /
  `:aiexpand` / `:aitone` / `:aibullets` / `:aidata` / `:aiflip`** no
  longer crash with `TerminatorExpectedAtEndOfString` on edit. Root
  cause: folded `cmd: >` + `shell: cmd` required hand-counted three-
  layer escape stacking; one missed backslash collapsed the pipeline.
  All eleven triggers migrated to the unified S03 pattern.
- **`:aiimg` temp filename** now uses
  `[System.IO.Path]::GetRandomFileName()` instead of a fixed
  predictable name; file write wrapped in try/finally that deletes the
  artifact regardless of outcome (was leaving stale PNGs in `%TEMP%`
  on errors).
- **xUnit cross-class env-var race** that flaked
  `ExportEnvTests` / `ModelAllowlistTests` / `UnicodeEncodingTests` in
  CI. Tagged the affected classes (plus four more identified in the
  round-2 audit: `ImageGenerationTests`, `ToolHardeningTests`,
  `CliParserTests`, `ValidationTemperatureTests`) with
  `[Collection("ConsoleCapture")]` to serialize. Deleted the redundant
  `SafetyPatchCollection` and migrated its members into
  `ConsoleCapture` to close a related cross-collection
  `AZUREOPENAIAPI` race between `PromptCacheTests` and
  `V201ProgramPatchTests`.
- **`SetupWizard.ReadMaskedLine` fail-closed.** No longer falls back
  to unmasked `Console.ReadLine` when `Console.ReadKey` throws on
  pseudo-TTYs. The fallback echoed every keystroke of the API key to
  scrollback / tmux logs / TTY loggers. Wizard now emits a one-line
  `[ERROR]` to stderr and short-circuits to exit 130 without ever
  accepting plaintext input. Newman audit H-1.
- **`az-ai --config get api_key` refuses** to prevent secret leakage
  via scrollback, shell history, screen-share, and terminal logs. The
  get-by-name path was an escape hatch around the `--config list`
  redaction. `UserConfig.GetKey` itself still returns the raw value
  for in-process callers (e.g. the wizard); the refusal lives at the
  print site only. Newman audit H-2.

## Upgrade notes

### From v2.1.x

Drop in. No action required for the binary.

**For users mirroring Espanso configs from
`examples/espanso-ahk-wsl/`:** sync the new file
`espanso/ai-prompts.yml` into your Espanso match directory (typically
`%APPDATA%\espanso\match\` on Windows). The existing
`ai-windows-to-wsl.yml` should also be re-mirrored to pick up the
unified pattern, the tier-doctrine triggers (`:aishort`, `:aiyml`),
and the trust-model header. Espanso reloads automatically on file
save.

**For AutoHotkey users:** install
`examples/espanso-ahk-wsl/ahk/az-ai-prompts.ahk` alongside any
existing AHK scripts. The new hotkeys (Ctrl+Shift+Q/R/C/D/L/T) are
namespaced and do not collide with hotkeys shipped in earlier
releases. The setup guide is at
`examples/espanso-ahk-wsl/PROMPT-TEMPLATES-INTEGRATION.md`.

### From v2.0.x

Same as v2.1.x, plus the v2.1.0 additions still apply
(`make migrate-check` / `migrate-clean`, `--show-cost`, expanded
`ReadFileTool` blocklist).

### From v1.x

Start with [`docs/migration-v1-to-v2.md`](migration-v1-to-v2.md), then
run `make migrate-check` / `migrate-clean`. Then follow the v2.1.x
upgrade path above.

## Known issues

- **Post-release security review pending.** A full-fleet security
  audit (Newman + FDR + Puddy + code-review) is in flight on the S03
  surface area. The S03E01 round-2 audit closed the high-severity
  findings; the residual `'@` here-string risk on multi-line free-
  form prompt fields is documented in the trust-model header. Any
  new findings post-tag will land as v2.2.x patches per the established
  cadence.
- **`:aiyml` v2 lint-on-clipboard.** v1 ships clipboard-only;
  `lint-espanso-yml.sh` is not yet wrapped into the trigger's own
  output path. Tracked for a follow-up episode -- needs a discovery
  mechanism for the lint script outside the repo.
- **Bania tier-latency benchmarks.** Bookman set the budgets from
  first principles (~30 ms / token at GPT-4o-mini). Bania has not
  yet measured actuals. First benchmark report due once the
  v2.2.x measurement window opens.
- **Homebrew / Scoop / Nix manifests remain DRAFT.** Carried over
  from v2.1.0. Pre-publish install paths documented in
  `docs/distribution/README.md`.
- **Eval harness.** The prompt library and temperature cookbook
  under `docs/prompts/` define the seam for a future small eval
  runner; no runner ships in v2.2.0.

## Verifying the release

Tag, tarballs, GHCR image, and Cosign signatures are verified per
[`docs/verifying-releases.md`](verifying-releases.md). The
verification flow is unchanged from v2.1.0:

1. Verify the GitHub Release tarball checksum against
   `SHA256SUMS` published on the Release page.
2. Verify the Cosign signature against the Sigstore transparency log.
3. (Container) Pull `ghcr.io/schwartzkamel/azure-openai-cli:2.2.0`
   and verify the image digest against the Release notes.

If any verification step fails, do not proceed with the upgrade --
file an issue with the `release-integrity` label and pin to the
prior tag.

## Acknowledgments

v2.2.0 carries work from S03E01-E02 and the prompt-templates feature
arc. Cast credits, in episode-airing order:

- **S03E01** *The Yada Yada Strikes Back* -- Kramer (unified trigger
  pattern + 7 new triggers), Soup Nazi (markdownlint cleanup), Puddy
  (env-var race triage and serialization), Newman (round-1 + round-2
  security audit on the form-input surface), FDR (red-team on
  SendKeys / focus-theft / Espanso re-fire), code-review
  (round-2 verification).
- **S03E02** *The Library Cop's Word Limit* -- **Lt. Bookman** (new
  supporting player; tier-doctrine canonical source), Kramer (three
  triggers tightened, two new triggers shipped, trust-model header
  updated), Larry David (showrunner / casting decision), the user
  for the brevity bug report that started the episode.
- **Prompt templates** (commit `905515e`) -- The Maestro (template
  authorship and prompt engineering), Kramer (Espanso + AHK
  integration), Elaine (setup guide and master system prompt
  documentation), Russell Dalrymple (output-structure standards),
  Babu Bhatt (Unicode-correctness review on the template inputs).

Hello! Contributor! Hello! Thanks to everyone on the bench --
showrunner Larry David, Costanza on PM, and Jerry on the pipes -- for
getting us to the tag.

---

**Release manager:** Mr. Lippman.
**Status:** DRAFT -- pre-staged ahead of v2.2.0 cut.
**Questions?** File an issue or see
[`CONTRIBUTING.md`](../CONTRIBUTING.md).
