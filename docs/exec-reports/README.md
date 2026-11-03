# Exec Reports — *Seinfeld Coding Cast*

> *"What's the deal with ... shipping software?"*

A post-facto log of every fleet-mode coding session on `azure-openai-cli`.
Each episode captures what the cast built, who showed up, what went
sideways, and what we learned -- structured like a proper TV show
because the [cast itself is](../../AGENTS.md).

## Seasons

| Season  | Theme                                    | Status                          | Blueprint |
|---------|------------------------------------------|---------------------------------|-----------|
| **S01** | The Pilot Years                          | Aired (back-fill pending)        | --        |
| **S02** | Production & Polish (v2 era)             | In production (24-arc + specials) | --        |
| **S03** | Local & Multi-Provider                   | Aired (30 episodes; index back-fill pending) | [s03-blueprint.md](s03-blueprint.md) |
| **S04** | Model Intelligence                       | In production (E01-E27 + 12 specials aired) | [s04-blueprint.md](s04-blueprint.md) |
| **S05** | Protocols & Plugins (incl. MCP)          | Pre-production                  | [s05-blueprint.md](s05-blueprint.md) |
| **S06** | Dogfooding (`az-ai` is the main tool)    | Pre-production (showrunner override) | [s06-blueprint.md](s06-blueprint.md) |
| **S07+** | Roadmap pad (Enterprise, Multimodal, ...) | Slate                            | [seasons-roadmap.md](seasons-roadmap.md) |

S06 baseline (the "before" picture before dogfooding starts):
[`dogfooding-baseline-2026-04.md`](../dogfooding-baseline-2026-04.md).

New seasons get declared when a major version ships, a theme shifts, or
the cast would tell you it's time for a retool.

## Episode index

### Season 2 -- Production & Polish

| #        | Title              | Episode file                                            | Commit    | What happened                                                |
|----------|--------------------|---------------------------------------------------------|-----------|--------------------------------------------------------------|
| S02E01   | *The Wizard*       | [s02e01-the-wizard.md](s02e01-the-wizard.md)            | `f57032f` | Interactive first-run wizard + per-OS LOLBin storage          |
| S02E02   | *The Cleanup*      | [s02e02-the-cleanup.md](s02e02-the-cleanup.md)          | `f65adbd` | docs-lint green + whitespace-key guard across all stores      |
| S02E03   | *The Warn-Only Lie* | [s02e03-the-warn-only-lie.md](s02e03-the-warn-only-lie.md) | `b36ec19` | docs-lint Summary step honesty -- label matches behaviour |
| S02E04   | *The Locksmith*    | [s02e04-the-locksmith.md](s02e04-the-locksmith.md)      | `ef8756c` | Opportunistic libsecret credential store on Linux             |
| S02E05   | *The Marathon*     | [s02e05-the-marathon.md](s02e05-the-marathon.md)        | `42b14bb` | `make bench-quick` + CI bench-canary (directional only)       |
| S02E06   | *The Screen Reader* | [s02e06-the-screen-reader.md](s02e06-the-screen-reader.md) | `6b22a7a` | NO_COLOR / FORCE_COLOR gates + wizard a11y hardening      |
| S02E07   | *The Observability* | [s02e07-the-observability.md](s02e07-the-observability.md) | `87d190d` | Telemetry posture doc + incident runbooks (no code yet)   |
| S02E08   | *The Translation*  | [s02e08-the-translation.md](s02e08-the-translation.md)  | `0c3a6ae` | i18n readiness audit + project glossary                       |
| S02E11   | *The Spec*         | [s02e11-the-spec.md](s02e11-the-spec.md)                | `8854588` | User-stories doc translating S02 features for non-engineers   |
| S02E12   | *The Apprentice*   | [s02e12-the-apprentice.md](s02e12-the-apprentice.md)    | `b6be974` | Lloyd Braun debut: friction log, glossary appends, starter PRs |
| S02E13   | *The Inspector*    | [s02e13-the-inspector.md](s02e13-the-inspector.md)      | `9bee782` | v2 surface security audit (5 PASS / 1 follow-up named)        |
| S02E14   | *The Container*    | [s02e14-the-container.md](s02e14-the-container.md)      | `09fa6d2` | Dockerfile hardening: numeric UID/GID, layer trim, CIS 4.1    |
| S02E15   | *The Lawyer*       | [s02e15-the-lawyer.md](s02e15-the-lawyer.md)            | `8d3e17f` | License audit -- 100% MIT, no GPL contagion, NOTICE bundling  |
| S02E16   | *The Catalog*      | [s02e16-the-catalog.md](s02e16-the-catalog.md)          | `ba96f6d` | Homebrew + Scoop + Nix packaging drafts (Bob's S02 debut)     |
| S02E17   | *The Newsletter*   | [s02e17-the-newsletter.md](s02e17-the-newsletter.md)    | `63c37db` | CONTRIBUTING refresh + named contributor wall                 |
| S02E18   | *The Maestro*      | [s02e18-the-maestro.md](s02e18-the-maestro.md)          | `5b502bd` | Prompt library inventory + ralph-mode temperature finding     |
| S02E19   | *The Competition*  | [s02e19-the-competition.md](s02e19-the-competition.md)  | `fc58fb4` | Competitive landscape brief + draft positioning copy          |
| S02E20   | *The Conference*   | [s02e20-the-conference.md](s02e20-the-conference.md)    | `3c5a319` | LOLBin credentials talk: 20 slides, 27 min, demo script       |
| S02E21   | *The Conscience*   | [s02e21-the-conscience.md](s02e21-the-conscience.md)    | `c047ea5` | Responsible-use ethics matrix (8 rows, 5 ENFORCED)            |
| S02E22   | *The Process*      | [s02e22-the-process.md](s02e22-the-process.md)          | `480e877` | Change-mgmt + ADR + CAB-lite + retrospective cadence docs     |
| S02E23   | *The Adversary*    | [s02e23-the-adversary.md](s02e23-the-adversary.md)      | `3f845fc` | Chaos drill: 21 findings, 9 CVE-shaped (FDR S02 debut)        |
| S02E27   | *The Bible*        | [s02e27-the-bible.md](s02e27-the-bible.md)              | `f3046e1` | 4 process skills (episode-brief / exec-report-format / ...)   |
| S02E28   | *The Style Guide*  | [s02e28-the-style-guide.md](s02e28-the-style-guide.md)  | `f3046e1` | 3 hygiene skills (ascii-validation / docs-only-commit / ...)  |
| S02E29   | *The Casting Call* | [s02e29-the-casting-call.md](s02e29-the-casting-call.md) | `4a4b894` | 2 cohesion skills (writers-room-cast-balance / findings-backlog) |
| S02E30   | *The Cast*         | [s02e30-the-cast.md](s02e30-the-cast.md)                | `93dfac7` | 12 cast personas baked as runtime defaults -- the show lives on |
| S02E31   | *The Audition*     | [s02e31-the-audition.md](s02e31-the-audition.md)        | `dae6145` | Adversarial audit of 5 generic personas (9 findings, 1 bug)   |
| S02E25   | *The Story Editor* | [s02e25-the-story-editor.md](s02e25-the-story-editor.md) | `7d57a01` | Doc-tree consolidation: new `docs/README.md` map + 8 cross-link footers |
| S02E09   | *The Receipt*      | [s02e09-the-receipt.md](s02e09-the-receipt.md)          | `8897880` | Opt-in `--show-cost` receipts (Morty's S02 debut, 11 priced models) |
| S02E32   | *The Bypass*       | [s02e32-the-bypass.md](s02e32-the-bypass.md)            | `a4fd184` | Structural shell-blocklist rewrite -- 8 reactivated bypass tests pass |
| S02E33   | *The Uninstaller*  | [s02e33-the-uninstaller.md](s02e33-the-uninstaller.md)  | `350e67a` | `make migrate-check` + `make migrate-clean` for v1 az-ai leftovers   |
| S02E34   | *The Index*        | [s02e34-the-index.md](s02e34-the-index.md)              | `54b9c19` | Docs orphan cleanup: 17→0 orphans, `docs/launch/README.md` authored  |
| S02E26   | *The Locked Drawer* | [s02e26-locked-drawer.md](s02e26-locked-drawer.md)     | `04be3ee` | `ReadFileTool` blocklist extended to 7 home-dir credential paths     |
| S02E10   | *The Press Kit*    | [s02e10-the-press-kit.md](s02e10-the-press-kit.md)      | `ee10121` | v2.1.0 release curation: CHANGELOG backfill + release notes + version bump |
| S02E36   | *The Attribution*  | [s02e36-the-attribution.md](s02e36-the-attribution.md)  | `6e26608` | Pre-v2.1.0 license audit -- 39 deps, zero copyleft, ship-cleared      |
| S02E24   | *The Finale*       | [s02e24-the-finale.md](s02e24-the-finale.md)            | `93fe4fb` | Season retrospective + S03 handoff -- Mr. Pitt leads, ensemble credited |

**S02 sealed at E24.** 35 episodes aired. S03 *Local & Multi-Provider*
is pre-production -- see [`s03-blueprint.md`](s03-blueprint.md).

### Season 4 -- Model Intelligence

[Blueprint](s04-blueprint.md). Commit column is the start of the commit
range recorded in each report. E06 is the mid-season cast-balance audit.

| Ep       | Title                  | Report | Commit | Summary |
|----------|------------------------|--------|--------|---------|
| S04E01   | *The Registry*         | [s04e01-the-registry.md](s04e01-the-registry.md) | `493c21b` | Model registry seam, 3 seed cards, ADR-012; Babu i18n special |
| S04E02   | *Embedded Cards*       | [s04e02-embedded-cards.md](s04e02-embedded-cards.md) | `d51beda` | Card reader, `--doctor` description column, symlink hotfix |
| S04E03   | *The Capabilities*     | [s04e03-the-capabilities.md](s04e03-the-capabilities.md) | `5e71331` | Per-model capability gate at request time, ADR-013 |
| S04E04   | *Reading Room*         | [s04e04-reading-room.md](s04e04-reading-room.md) | `457e06b` | `az-ai models list`, table renderer, ADR-014 output standard |
| S04E05   | *The Picker*           | [s04e05-the-picker.md](s04e05-the-picker.md) | `3bdac79` | Capability-aware default model picker, four reason codes |
| S04E06   | *The Cast Balance*     | [s04e06-the-cast-balance.md](s04e06-the-cast-balance.md) | docs-only | Mid-season audit: Rule 1 broken in the plan, corrected E08-E13 slate |
| S04E07   | *The Fallback*         | [s04e07-the-fallback.md](s04e07-the-fallback.md) | `c3deb8b` | Same-model retry, backoff and budget executor |
| S04E08   | *The Onboarding*       | [s04e08-the-onboarding.md](s04e08-the-onboarding.md) | docs + code | Wizard explains the default model; README keystore claim corrected |
| S04E09   | *The Override*         | [s04e09-the-override.md](s04e09-the-override.md) | code + docs | `--prefer` and `--why-this-model`; admits when the registry has no data |
| S04E10   | *The Score*            | [s04e10-the-score.md](s04e10-the-score.md) | code + docs | `az-ai eval`: deterministic scoring; empty suites and unverifiable safety fixtures fail |
| S04E11   | *The Corpus*           | [s04e11-the-corpus.md](s04e11-the-corpus.md) | code + docs | Three embedded eval corpora; each fixture proven satisfiable and violable; no real model run yet |
| S04E12   | *The Gate*             | [s04e12-the-gate.md](s04e12-the-gate.md) | code + CI | `--repeat`, baselines and a fail-closed eval gate; live path unexercised (no credentials) |
| S04E13   | *The Redaction*        | [s04e13-the-redaction.md](s04e13-the-redaction.md) | code + docs | `az-ai --lint-suite`: eval files cannot leak; a unicode-escape bypass found and fixed pre-release |
| S04E14   | *The Rebrand*          | [s04e14-the-rebrand.md](s04e14-the-rebrand.md) | code + docs | The command is `hopper`, `az-ai` stays as an alias; config paths guarded by tests; archive and package names held for the release |
| S04E15   | *The Denylist*         | [s04e15-the-denylist.md](s04e15-the-denylist.md) | code + docs | `shell_exec` guard: 30 of 32 bypasses measured, 0 after a tokenizer that reads commands the way the shell does |
| S04E16   | *The Bare Token*       | [s04e16-the-bare-token.md](s04e16-the-bare-token.md) | code + docs | One list of bare-token shapes shared by the runtime redactor and the eval lint; a provider error that echoes a key is masked |
| S04E17   | *The Long Tail*        | [s04e17-the-long-tail.md](s04e17-the-long-tail.md) | code + docs | A reviewer model found a bypass in the S04E15 guard; measuring its neighbours found seven routes that deleted a file; one shared rule closes them |
| S04E18   | *The Clean Exit*       | [s04e18-the-clean-exit.md](s04e18-the-clean-exit.md) | code + docs | Hopper killed MCP servers at once, so none could release what they held; a session-capped host failed on the ninth run |
| S04E19   | *The Stale Install*    | [s04e19-the-stale-install.md](s04e19-the-stale-install.md) | build + docs | `make install` copied the previous build under a new timestamp; the AOT binary now depends on its sources |
| S04E20   | *The Default*          | [s04e20-the-default.md](s04e20-the-default.md) | code + docs | A model that rejects `temperature` is retried once without it; 2 of 3 live deployments failed before, 0 after |
| S04E21   | *The Silence*          | [s04e21-the-silence.md](s04e21-the-silence.md) | code + docs | An empty reply that stopped at the output limit or the content filter exits 1 instead of 0; 2 of 3 small-budget live calls were silent before |
| S04E22   | *The Allowlist*        | [s04e22-the-allowlist.md](s04e22-the-allowlist.md) | code + docs | `--mcp-allow` / `--mcp-deny` choose which MCP tools the model is told about; a mistyped deny pattern warns instead of silently exposing the tool |
| S04E23   | *The Early Exit*       | [s04e23-the-early-exit.md](s04e23-the-early-exit.md) | tests + CI scripts | `producer \| grep -q` under `pipefail` failed matches by itself (2% to 89%, by size and load); fixed at the consumer in the suite and 5 gate scripts; 0 of 6,000 after |
| S04E24   | *The Copilot*          | [s04e24-the-copilot.md](s04e24-the-copilot.md) | code + docs | `copilot` preset: GitHub token from env or `gh auth token`, client headers, Responses API (stateless) for GPT-5.x/6.x and Grok; compat routes no longer vetoed by `AZUREOPENAIMODEL`; temperature-refusal memory persists across runs |
| S04E25   | *The Wrong Door*       | [s04e25-the-wrong-door.md](s04e25-the-wrong-door.md) | code + docs | A URL given to `--mcp-server` is named as the problem and the bridge as the way out; credentials in the URL are never echoed |
| S04E26   | *The Filter*           | [s04e26-the-filter.md](s04e26-the-filter.md) | code + docs | A platform content-filter block is its own eval outcome (`filtered`): unverified and fail-closed by default, `allow_filtered` per fixture; the live refusal suite goes from permanent FAIL to PASS, with the block shown |
| S04E27   | *The Strict Door*      | [s04e27-the-strict-door.md](s04e27-the-strict-door.md) | code + docs | `--mcp-strict` exposes only tools a server declares `readOnlyHint: true` (or you name), and a mistyped pattern stops the run; hopper's own server now declares its read-only tools |
| S04SP1   | *The Reruns*           | [s04sp1-the-reruns.md](s04sp1-the-reruns.md) | `eecfd74` | Release matrix retag after v2.2.0 |
| S04SP2   | *The Stenographer*     | [s04sp2-the-stenographer.md](s04sp2-the-stenographer.md) | docs-only | Paperwork audit; artifact contract locked for v2.3.0 |
| S04SP3   | *The Pre-Push*         | [s04sp3-the-pre-push.md](s04sp3-the-pre-push.md) | `1c8c787` | Pre-push docs-lint after ten consecutive red runs |
| S04SP4   | *The Bucket*           | [s04sp4-the-bucket.md](s04sp4-the-bucket.md) | `457e06b` | Release queue unblock and printf fix-forward |
| S04SP5   | *The Name Game*        | [s04sp5-the-name-game.md](s04sp5-the-name-game.md) | docs only | Six-round rebrand review; verdict reversed; three gates open, nothing renamed |
| S04SP6   | *The Name Game, Part II* | [s04sp6-the-name-game-part-ii.md](s04sp6-the-name-game-part-ii.md) | docs + script | 719 names screened incl. US trademarks; first shortlist withdrawn; `dunlin` selected, clearance open |
| S04SP7   | *The Name Game, Part III* | [s04sp7-the-name-game-part-iii.md](s04sp7-the-name-game-part-iii.md) | docs only | The owner: "those names don't make sense"; meaning-first rounds; `anymux` recommended |
| S04SP8   | *The Name Game, Part IV* | [s04sp8-the-name-game-part-iv.md](s04sp8-the-name-game-part-iv.md) | docs + script | The owner drops trademarks and asks for a competitor comparison; 26-tool table, 186 names, `hopper` recommended |
| S04SP9   | *The Sweep*            | [s04sp9-the-sweep.md](s04sp9-the-sweep.md) | docs only | 46 living docs say `hopper`; history and packaging docs left under the name they were written in |
| S04SP10  | *The Housekeeping*     | [s04sp10-the-housekeeping.md](s04sp10-the-housekeeping.md) | docs + script | AOT run of the embedded corpora; proposals index made true; a check for a script hidden by `.gitignore` |
| S04SP11  | *The Field Notes*      | [s04sp11-the-field-notes.md](s04sp11-the-field-notes.md) | docs + example | What hopper is for on a real server, found by using it; four product gaps and a tested read-only MCP proxy |
| S04SP12  | *The Bridge*           | [s04sp12-the-bridge.md](s04sp12-the-bridge.md) | docs + example | Hopper reaches scouter's HTTP MCP host through a tested bridge; an offline analysis graded on 10 runs |

### Season 3 -- Local & Multi-Provider

[Blueprint](s03-blueprint.md). 30 episodes aired (`s03e01` through `s03e30`
in this directory). A per-episode index with verified commit SHAs is a
follow-up; see the S04E06 report.

### Season 1 -- The Pilot Years

Retroactive coverage pending. See `git log` and `CHANGELOG.md` for the raw
timeline until an on-screen cast member back-fills the pilot episodes.

## Where to find non-episode docs

The exec-report log covers *episodes* (fleet-mode coding sessions). The
broader `docs/` tree has 170+ files across security audits, ADRs,
runbooks, proposals, perf baselines, launch playbooks, and prompt
engineering. Top-level entry points:

- [`docs/proposals/`](../proposals/) -- feature proposals (FR-NNN).
- [`docs/adr/`](../adr/) -- architecture decision records.
- [`docs/security/`](../security/), [`docs/legal/`](../legal/) -- audit trails.
- [`docs/runbooks/`](../runbooks/), [`docs/ops/`](../ops/) -- operational playbooks.
- [`docs/perf/`](../perf/), [`docs/benchmarks/`](../benchmarks/) -- performance.
- [`docs/launch/`](../launch/) -- release playbooks and announcements.
- [`docs/prompts/`](../prompts/) -- prompt library and personas.

A consolidation pass (S02E25 *The Story Editor*) is queued to thin
overlap between e.g. `competitive-analysis.md` + `competitive-landscape.md`,
`i18n-audit.md` + `i18n.md` + `i18n/`, and `licensing-audit.md` +
`legal/license-audit.md`.

## Naming conventions

- **Episodes** follow the Seinfeld convention: `The <Noun>`. Short,
  iconic, no cute subtitles. Think *The Contest*, *The Soup Nazi*,
  *The Marine Biologist* -- not *How We Shipped Feature X v3*.
- **Filenames:** `sNNeMM-kebab-case-title.md` (lowercase, hyphenated).
- **Season numbering** follows the major-version / era convention
  above, not calendar time.
- **Episode numbering** is strict: S02E01 ships before S02E02. No
  retroactive numbering within a season once published -- if an episode
  gets back-filled, it gets the next sequential number regardless of
  when the work happened.

## What goes in an episode

Every episode follows the [`_template.md`](_template.md) structure:

1. **Front matter** -- commit, branch, runtime, director, cast.
2. **The pitch** -- one-paragraph log line (TV Guide-style).
3. **Scene-by-scene** -- wave-by-wave or phase-by-phase breakdown.
4. **What shipped** -- production code, tests, docs, intentional non-goals.
5. **Lessons from this episode** -- blind spots, process misses, aha
   moments. Gold, Jerry! Gold! (Bania is watching.)
6. **Metrics** -- diff size, test delta, preflight state, CI at push time.
7. **Credits** -- which agents contributed and to what.

## Why this matters

Three months from now, `git blame` will give you the *what*. This log
gives you the *why* and the *who*, with enough personality to actually
re-read. That's how institutional memory survives contributor churn --
and how the Seinfeld cast metaphor earns its keep instead of being a
gag in `AGENTS.md` that nobody references.

*-- Elaine (docs), with notes from Mr. Pitt (program management) and a
reluctant nod from The Soup Nazi (who demands strict structure).*
