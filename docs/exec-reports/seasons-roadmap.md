# Seasons Roadmap -- S06 onward

> *Mr. Pitt's planning pad. Eats Snickers with a fork. Treat this
> document the same way.*

## Showrunner override (2026-04-22)

The showrunner-of-showrunners has reslotted **S06 = *Dogfooding***
(see [`s06-blueprint.md`](s06-blueprint.md)), displacing Mr. Pitt's
prior S06 recommendation of *Enterprise & Compliance* (which now
moves to S07 with no other change). The candidate slate below has
been cascaded down by one slot for every theme that previously sat at
S06+. Killed pitches are not renumbered. Mr. Pitt's recommended-next
ordering has been updated to put Dogfooding at #1 (formality -- it is
already locked) and otherwise preserves his original priority order.

## Showrunner pad

Larry David has greenlit the next three seasons as standalone
blueprints: **S03 Local & Multi-Provider**, **S04 Model Intelligence**,
and **S05 Protocols & Plugins**. Those blueprints already enumerate
their own episode arcs. This pad is the *bench* behind them -- 10
candidate themes for **S06 onward**, sized so the showrunner can
greenlight, defer, collapse-into-an-episode, or kill on first read.
Nothing in this document is a commitment. Some candidates will
absorb each other. Some will quietly become off-roster specials.
Some will die in the writers' room. That is the point of a pad.

## Methodology

Candidates were generated from four inputs:

1. **Existing FR-NNN proposals** in `docs/proposals/` that did not
   land in the S03/S04/S05 blueprints (notably FR-005 shell
   integration, FR-008 cache, FR-015 pattern library / cost
   estimator, FR-022/023 wizards).
2. **2026 market signals** -- one piece of current evidence per
   candidate, cited inline. No theme made the slate without one.
3. **Competitor moves** catalogued in S02E19 *The Competition*
   (Claude Code, Codex, Cursor CLI, Aider, Continue, Kilo).
4. **Technical debt patterns** observed across S02 (locksmith,
   apprentice, marathon, observability, translation) -- areas
   where one episode revealed a season's worth of follow-up.

A theme had to clear three bars to land here: (a) at least four
filmable episodes, (b) at least one anchor agent on the existing
roster, (c) a 2026 evidence point that is not just hype. Items
that failed (a) went to **Off-roster**. Items that failed (b) or
(c) went to **Killed**.

## Candidate slate

### S06 -- *Dogfooding* (locked by showrunner override)

- **Pitch.** `az-ai` becomes the tool we ship AND the tool we use
  daily to ship it. Commit-message generation, PR-description
  drafting, CI triage, release-note synthesis, code-review pre-pass,
  exec-report drafting, persona-spawned subagents for our own
  backlog, AHK/Espanso flows we actually use, and a self-hosted MCP
  server pointed at this repo. Win condition at finale: one
  non-trivial daily workflow runs through `az-ai` end-to-end with no
  other LLM CLI in the loop. Full 24-episode treatment in
  [`s06-blueprint.md`](s06-blueprint.md). Jerry leads (DevOps spine);
  Kramer guests as the engineer-using-it honesty check.
- **Why now.** 2026 dogfooding is product evidence, not marketing
  copy: Anthropic publishes 59% internal-use stats, Cursor runs
  every internal PR through their own AI review, GitHub Copilot's
  team uses Copilot to ship Copilot. We are visibly absent from this
  pattern. Commit-message and CI-triage tooling matured to 85-95%
  accuracy in 2026 (`aicommits`, `opencommit`, Phind CI Bot, Copilot
  CI), so the workflows are real and the bar is reachable.
- **Tentative lead arc.** *The Subject Line* (commit). *The
  Description* (PR). *The Triage* (CI). *The Release Note*
  (CHANGELOG synthesis). *The Pre-Pass* (review). *The Treatment*
  (exec-report drafting). *The Self-Server* (MCP pointed at this
  repo). *The Week* (finale -- one full az-ai-only workweek).
- **Casting weight.** **Anchors:** Jerry, Kramer. **Support:**
  Costanza (PM workflows), Newman (write-access trust + review
  pre-pass), Elaine (doc workflows), Frank Costanza (telemetry of
  dogfooding itself), The Maestro (prompt library used in real
  workflows), Lloyd Braun (junior-lens "first day using az-ai for
  everything"), Mr. Pitt (finale ensemble).
- **Dependencies.** **Hard.** Requires S03 (Local & Multi-Provider),
  S04 (Model Intelligence), and S05 (Protocols & Plugins) to have
  shipped. S06 cannot pre-empt them; if reordered, S06 either ships
  thin (~12 episodes) or waits. Mapping table in `s06-blueprint.md`.
- **Risk if killed.** Cannot be killed -- locked by showrunner
  override. If it slips, we keep paying the trust tax of building
  a dev tool we visibly don't depend on ourselves.

### S07 candidate -- *Enterprise & Compliance*

- **Pitch.** Take `az-ai` from "trustworthy single-user CLI" to
  "deployable inside a regulated org without a security review
  blocking it for six months." Ship Entra/Okta SSO for the agent
  loop, structured audit logging that survives forensic review,
  fine-grained policy (which tools are allowed for which user /
  trigger / repo), BYO-key management with rotation, and explicit
  data-residency controls (region pinning, no-egress mode). End
  state: a CISO can sign off on `az-ai` after a one-page memo, not
  a quarter-long procurement cycle. Newman owns the bar; Frank
  owns the audit trail; Jackie owns the compliance language.
- **Why now.** Okta's 2026 *Secure Agentic Enterprise* blueprint
  treats AI/CLI agents as first-class non-human identities, and
  ~70% of recent enterprise identity incidents are AI-related
  (Okta investor briefing + ETR research, 2026). Entra is shipping
  an "access fabric" specifically for agent identities. The
  buyers are asking for this *now*, not in 2027.
- **Tentative lead arc.** *The Sign-On* (Entra/Okta OIDC device
  flow). *The Ledger* (structured JSONL audit log + tamper-evident
  hash chain). *The Policy* (per-tool, per-trigger allowlists,
  config-as-code). *The Vault* (BYO-key with rotation hooks for
  Key Vault / AWS KMS / HCP Vault). *The Region* (data-residency
  pinning + no-egress mode). *The Memo* (CISO-ready security
  posture doc).
- **Casting weight.** **Anchors:** Newman, Frank Costanza, Jackie
  Chiles. **Support:** Kramer (implementation), Wilhelm (process /
  CAB), Elaine (memo).
- **Dependencies.** Independent of S03/S04/S05 in principle, but
  cleaner if S05 (plugin trust boundary) lands first so the
  policy engine has one surface to govern instead of two.
- **Risk if killed.** Ceiling on enterprise adoption. We stay a
  power-user CLI. Acceptable if that's the chosen identity --
  catastrophic if Costanza picks an enterprise GTM later.

### S08 candidate -- *Multimodal*

- **Pitch.** Break the text-only assumption. Image input for
  screenshot-driven workflows ("what's wrong with this dashboard"),
  audio input via STT for voice-triggered AHK/Espanso flows, and
  optional TTS output for accessibility. Land OpenAI Realtime
  API support for low-latency voice loops, with parity paths for
  Azure OpenAI's audio deployments. End state: `az-ai --image
  bug.png "explain"`, `az-ai --listen`, and a realtime voice mode
  that doesn't require a browser.
- **Why now.** GPT-4o-class multimodal endpoints (image + audio
  in/out) are GA across OpenAI and Azure OpenAI in 2026, and the
  Realtime API has stabilized to streaming-token latency. The
  competition (Claude Code, Cursor) already accepts image input.
  We are visibly behind on this axis.
- **Tentative lead arc.** *The Screenshot* (image input plumbing +
  token accounting). *The Microphone* (STT trigger + push-to-talk
  ergonomics). *The Voice* (TTS output, Mickey-approved). *The
  Realtime* (websocket loop + barge-in). *The Mode* (a "code-aware"
  mode that pairs vision with shell context).
- **Casting weight.** **Anchors:** Kramer, Maestro. **Support:**
  Mickey Abbott (a11y for voice/TTS), Russell (UX), Newman
  (image-borne prompt-injection defenses).
- **Dependencies.** Benefits from S03 multi-provider (so we can
  route image/audio to whichever backend supports it), but doesn't
  hard-require it.
- **Risk if killed.** We become a text-only dinosaur in a
  multimodal-default market. Real risk of irrelevance for AHK /
  desktop-automation users who want screenshot-to-fix loops.

### S09 candidate -- *Pipelines & Workflows*

- **Pitch.** Promote `az-ai` from "one-shot completion or one
  Ralph loop" to a workflow runtime. Multi-step prompt chains
  with conditional branches, on-failure retries with backoff,
  cron-style scheduling for unattended runs, and a persistent
  REPL that survives across invocations. End state: a user can
  declare a `.az-ai/workflow.yaml`, run it on a schedule, and
  trust it to recover from transient failures. This is the path
  from "interactive helper" to "automation substrate."
- **Why now.** LangGraph and DSPy have normalized the
  graph-of-prompts mental model in the Python ecosystem through
  2026; .NET CLI users are explicitly asking for the same shape
  without dragging in a Python runtime. FR-002 (interactive
  chat) and FR-011 (agent streaming) are already in the proposal
  drawer pointing at this.
- **Tentative lead arc.** *The Chain* (declarative multi-step
  YAML). *The Branch* (conditional routing on tool-call output).
  *The Retry* (backoff, dead-letter, idempotency keys). *The
  Schedule* (cron + systemd-timer + Windows Task Scheduler
  parity). *The REPL* (persistent session state, history,
  transcript export).
- **Casting weight.** **Anchors:** Kramer, Maestro. **Support:**
  Puddy (workflow regression coverage), Frank (failure-mode
  observability).
- **Dependencies.** Independent. Better after S05 lands so MCP
  tools are first-class workflow nodes.
- **Risk if killed.** Power users build this themselves with bash +
  cron + jq, badly. We cede the orchestrator surface to Python
  frameworks. Probably survivable.

### S10 candidate -- *Distribution & Packaging*

- **Pitch.** Bob Sacamano's full-court press. Ship through every
  channel a developer might already trust: Homebrew tap, Scoop
  bucket, Winget manifest, Nix flake, Snap, Flatpak, AppImage,
  APT/RPM repos, and (stretch) MS Store + Mac App Store. Each
  channel auto-updates from the release tag via CI. End state:
  `brew install az-ai`, `winget install az-ai`, `nix run
  github:.../az-ai` all Just Work, and Mr. Lippman cuts one
  release that fans out to every channel.
- **Why now.** 2026 packaging best-practice is explicitly
  "automated multi-manifest publishing from CI on tag" -- the
  Homebrew, Winget, Scoop, and Nix communities all document this
  as the recommended path. Container-only distribution is no
  longer enough for a developer-trust tool; the package manager
  *is* the trust signal for many users.
- **Tentative lead arc.** *The Tap* (Homebrew). *The Bucket*
  (Scoop + Winget). *The Flake* (Nix). *The Repo* (APT/RPM +
  Snap/Flatpak/AppImage). *The Store* (MS Store + Mac App Store
  -- stretch episode, may slip).
- **Casting weight.** **Anchors:** Bob Sacamano, Jerry, Mr.
  Lippman. **Support:** Newman (signing + checksums), Soup Nazi
  (manifest style gate).
- **Dependencies.** Independent. Best after S04 stabilizes the
  binary surface so we aren't republishing across every channel
  on every breaking change.
- **Risk if killed.** Adoption stays Docker-curious. Casual
  installers bounce. Real but not existential -- container path
  remains fine for the core audience.

### S11 candidate -- *Editor & IDE Integrations*

- **Pitch.** Meet developers where they live. VS Code extension
  that wraps the CLI (not a re-implementation), JetBrains plugin
  via the Platform SDK, Vim/Neovim plugin, Emacs package, and
  starter configs for Helix / Zed / Sublime. Each is a thin
  shell over the existing binary -- the CLI stays canonical, the
  editors are surfaces. End state: a developer can highlight
  code, hit a keybinding, and round-trip through `az-ai`
  without ever leaving the editor.
- **Why now.** The 2026 AI-coding-extension landscape (Copilot,
  Cursor, Continue, Cline, Kilo, Codex IDE) shows the editor
  surface is now the dominant entry point for AI tooling, even
  for users who prefer CLI under the hood. JetBrains shipped
  AI Assistant for VS Code in 2026 specifically to chase the
  cross-IDE pattern.
- **Tentative lead arc.** *The Extension* (VS Code, MVP). *The
  Plugin* (JetBrains Platform). *The Vimmer* (Neovim Lua
  plugin). *The Emacsen* (package + use-package recipe). *The
  Editors* (Helix / Zed / Sublime starter configs).
- **Casting weight.** **Anchors:** Bob Sacamano, Russell
  Dalrymple. **Support:** Kramer, Mickey (a11y of editor
  surfaces), Uncle Leo (community-contributed editor configs).
- **Dependencies.** Soft-depends on S05 (MCP) so editor surfaces
  can speak the same protocol the CLI does.
- **Risk if killed.** We stay terminal-only and lose the
  hybrid-workflow user. Tolerable -- the CLI-first identity is a
  feature, not a bug -- but it caps reach.

### S12 candidate -- *Observability & Telemetry (formal)*

- **Pitch.** Take what S02E07 *The Observability* started and
  ship it as a season. Opt-in OpenTelemetry pipeline aligned to
  the GenAI semantic conventions (`gen_ai.*` attributes), shipped
  exporters for OTLP / Prometheus / Application Insights, default
  dashboards (Grafana + Azure Monitor), SLOs as code, and -- the
  delta from E07 -- cost telemetry as a first-class metric
  alongside latency and error rate. End state: an org running
  `az-ai` at scale gets per-user, per-model, per-trigger cost and
  latency trend lines for free, without writing instrumentation.
- **Why now.** OpenTelemetry's GenAI semantic conventions are
  formalizing through 2026, with `gen_ai.*` attributes including
  token counts, model identity, and request-cost fields adopted
  by the major APMs (Datadog, New Relic, Grafana, Azure Monitor).
  Shipping aligned to the standard now is cheap; retrofitting
  later is not.
- **Tentative lead arc.** *The Conventions* (adopt `gen_ai.*`
  SemConv). *The Exporter* (OTLP + Prometheus + AppInsights).
  *The Dashboard* (Grafana JSON + Azure workbook). *The SLO*
  (declarative SLOs in repo, alerting hooks). *The Bill*
  (cost-as-a-metric, with Morty co-author).
- **Casting weight.** **Anchors:** Frank Costanza, Morty
  Seinfeld. **Support:** Kramer, Newman (PII scrubbing in
  exporter), Maestro (eval-loop telemetry).
- **Dependencies.** Independent. Greatly improved if S07
  enterprise lands first so audit log and OTel share a schema.
- **Risk if killed.** E07's audit becomes the high-water mark
  forever. We don't compete in any "ops at scale" RFP.

### S13 candidate -- *Microsoft Agent Framework, Deep*

- **Pitch.** Move beyond Ralph mode and the bespoke tool loop.
  Adopt Microsoft Agent Framework (MAF) 1.0 as a first-class
  agent runtime alongside the current loop, with optional
  Azure AI Foundry hosting for persistent server-side agents.
  Keep the AOT / single-binary ethos -- MAF is one runtime
  *option*, not a replacement. End state: a user can pick
  `--runtime ralph` (current), `--runtime maf` (in-process MAF),
  or `--runtime foundry` (cloud-hosted), with the same prompt
  surface.
- **Why now.** Microsoft Agent Framework 1.0 reached production
  GA for .NET and Python in April 2026, unifying Semantic Kernel
  and AutoGen with frozen APIs and LTS. Foundry hosting is
  fully operational. The bet becomes *cheap* in 2026 in a way it
  was not in 2025.
- **Tentative lead arc.** *The Framework* (MAF dependency,
  AOT-compatibility audit). *The Loop* (Ralph-equivalent loop
  on MAF). *The Foundry* (cloud-hosted agent path). *The
  Handoff* (multi-agent orchestration patterns -- group chat,
  handoff, sequential).
- **Casting weight.** **Anchors:** Kramer, Maestro. **Support:**
  Costanza (architecture), Bania (perf delta vs current loop),
  Newman (trust boundary if Foundry is enabled).
- **Dependencies.** Soft-depends on S05 (MCP) -- MAF speaks A2A
  and MCP natively, so the protocols-and-plugins work pays
  compound interest here.
- **Risk if killed.** We stay on a bespoke loop. Possibly fine
  -- Ralph works -- but we lose interop with the broader .NET
  agent ecosystem and any Azure-shop user who has standardized
  on MAF will route around us.

### S14 candidate -- *Performance Season*

- **Pitch.** A season dedicated entirely to AOT trim sharpening,
  cold-start floor, P99 latency, and memory ceiling. No new
  features -- just numbers going down. Establish a Bania-owned
  perf gate in CI that fails PRs on regression. Publish a public
  benchmark dashboard. End state: documented P50/P95/P99 startup +
  first-token + steady-state numbers, an enforced budget, and a
  tightened binary size.
- **Why now.** .NET 10 Native AOT in 2026 is delivering
  documented 70-90% cold-start reductions and 50-60% memory
  reductions on real workloads (Microsoft Learn + community
  benchmarks). The headroom is *available now*; if we don't
  capture it, a competitor will publish faster numbers and the
  story writes itself.
- **Tentative lead arc.** *The Baseline* (Bania establishes
  reproducible benchmark harness). *The Trim* (aggressive trim +
  reflection audit). *The Prewarm* (FR-007 connection
  prewarming, finally). *The Cache* (FR-008 prompt-response
  cache). *The Gate* (CI perf-budget enforcement). *The Board*
  (public benchmark dashboard).
- **Casting weight.** **Anchors:** Kenny Bania, Kramer.
  **Support:** Jerry (CI gate), Frank (telemetry on perf
  regressions in the wild).
- **Dependencies.** Independent. Best deferred until *after* a
  feature-heavy season so there's something worth optimizing.
- **Risk if killed.** Performance drifts. We lose the
  "single-binary, instant-start" identity that differentiates us
  from Python-based competitors. Medium risk.

### S15 candidate -- *Data & Privacy*

- **Pitch.** Make `az-ai` the obvious choice for users who can't
  let prompts leave the box. Ship a strict local-only mode that
  refuses to call any cloud endpoint, on-device PII redaction
  (with an auditable rule set) that runs *before* any prompt
  egress, GDPR/HIPAA posture documentation, and a clean opt-in
  story for telemetry that survives a privacy review. End state:
  a healthcare or EU-public-sector user can deploy `az-ai`
  without filing a DPIA exception.
- **Why now.** EU AI Act enforcement is escalating through 2026
  with mandatory data-minimization, residency, and
  transparency requirements for AI tooling. Healthcare and
  public-sector procurement is explicitly asking vendors for
  "on-device redaction before egress" as a checklist item. The
  regulatory deadline does the marketing for us.
- **Tentative lead arc.** *The Airlock* (strict local-only mode,
  network-call assertion in tests). *The Redactor* (rule-based
  PII detection + replacement, pluggable). *The Posture* (GDPR +
  HIPAA documentation, with Jackie + Rabbi). *The Telemetry*
  (formal opt-in story, with Frank + Morty). *The Audit*
  (export "what would have been sent" diff for review).
- **Casting weight.** **Anchors:** Newman, Rabbi Kirschbaum,
  Jackie Chiles. **Support:** Frank (telemetry boundary),
  Babu (locale-aware PII patterns).
- **Dependencies.** Soft-depends on S03 (local provider) so
  local-only mode has somewhere to route. Hard-pairs with S07
  enterprise compliance.
- **Risk if killed.** We are non-viable for regulated buyers.
  Acceptable only if we explicitly disclaim that market.

### S16 candidate -- *Doc & DX Season*

- **Pitch.** Treat documentation and developer experience as
  product, not byproduct. Ship a tutorial track (zero-to-Ralph
  in 60 minutes), video walkthroughs of the core flows, a
  learn-by-doing scaffold (`az-ai init --example=ahk`,
  `--example=espanso`, `--example=workflow`), and a refreshed
  first-run wizard (FR-022 / FR-023 finally landed). Lloyd Braun
  anchors -- he asks the obvious onboarding question Kramer
  assumes everyone knows the answer to. End state: a new user
  goes from `brew install` to first useful output in under
  five minutes without reading anything longer than a tweet.
- **Why now.** S02E08 (translation), S02E20 (conference), and
  S02E17 (newsletter) collectively surfaced that our reach is
  doc-bound. Competitor landscape per S02E19 shows Cursor and
  Claude Code differentiate aggressively on onboarding videos
  and starter templates. Contributor backlog has multiple
  "I tried it, got stuck on X" issues that all map to missing
  scaffolds.
- **Tentative lead arc.** *The Tutorial* (ordered learning
  track). *The Scaffold* (`az-ai init --example=*`). *The
  Wizard* (FR-022 native, FR-023 first-run). *The Video* (Keith
  Hernandez records the demo set). *The Welcome* (Uncle Leo
  refreshes contributor onboarding).
- **Casting weight.** **Anchors:** Elaine, Lloyd Braun.
  **Support:** Russell (UX of wizard), Keith Hernandez
  (videos), Uncle Leo (community), Babu (translated tutorials).
- **Dependencies.** Independent, but most valuable *after* a
  feature-heavy season so there is fresh material to onboard
  people *to*.
- **Risk if killed.** Adoption ceiling stays where it is.
  Contributor funnel stays narrow. Slow-burn risk, not acute.

## Dependency graph

- **Locked (showrunner override):** S06 Dogfooding -- hard-depends on
  S03, S04, and S05 (see [`s06-blueprint.md`](s06-blueprint.md)).
- **Independent (can ship any time):** S07 Enterprise, S09
  Pipelines, S10 Distribution, S14 Performance, S16 Doc/DX.
- **Soft-depends on S03 (Local & Multi-Provider):** S08
  Multimodal (provider-routing surface), S15 Data & Privacy
  (local provider for airlock mode).
- **Soft-depends on S05 (Protocols & Plugins):** S09 Pipelines
  (MCP tools as workflow nodes), S11 Editors (shared protocol
  with the CLI), S13 MAF (compounding interop with A2A/MCP).
- **Hard pairing:** S07 Enterprise + S15 Data & Privacy share
  enough surface (audit, residency, policy) that running them
  back-to-back is much cheaper than splitting them across the
  calendar.
- **Cross-pollination:** S12 Observability benefits from S07
  (shared schema with audit log) and feeds S14 Performance
  (telemetry catches regressions before the gate does). S06
  Dogfooding is itself a forcing function on S12 -- the telemetry
  episode (S06E15) uses the same plumbing.

## Recommended ordering (showrunner will override)

1. **S06 Dogfooding** -- locked by showrunner override; recommendation
   here is formality plus a dependency reminder (S03/S04/S05 must
   ship first; if reordered, S06 ships thin or waits).
2. **S07 Enterprise & Compliance** -- highest commercial unlock,
   buyers asking now, anchors (Newman/Frank/Jackie) are ready.
3. **S08 Multimodal** -- visible competitive gap, cheap to start
   once S03 lands, broad user appeal beyond enterprise.
4. **S12 Observability (formal)** -- compounds with S07 and is a
   prerequisite for any "at scale" story; SemConv window is open.
5. **S14 Performance Season** -- captures the .NET 10 AOT
   headroom while it's still a fresh story; protects identity.
6. **S10 Distribution & Packaging** -- Bob's quarter, low
   architectural risk, large reach-per-line-of-code ratio.
7. **S09 Pipelines & Workflows** -- highest power-user payoff,
   but waits until S05 MCP work makes the workflow nodes
   first-class.

S11 (Editors), S13 (MAF), S15 (Data & Privacy), and S16 (Doc/DX)
remain on the bench. S15 jumps to top-three the moment a regulated
buyer materializes; S13 jumps the moment Costanza decides we're
betting on the Microsoft agent ecosystem rather than running our
own loop forever.

## Off-roster / one-shot ideas

These do not justify a season but should not be forgotten:

- **systemd-creds Linux credential provider** -- one episode,
  pairs with S02E04 *The Locksmith*. Newman.
- **Mac Keychain test rewrite** -- the test suite skips Keychain
  paths on CI; one focused episode finishes the job. Puddy.
- **Filename-convention docs-lint hard-flip** -- E13 inspector
  shipped in warn-only; flip to enforce in one episode. Soup
  Nazi.
- **i18n push as a season** -- pre-evaluated, rejected as a
  season but kept as a recurring concern. Babu Bhatt audits
  per-season; promote only if a translated user base materializes.
- **Cross-platform parity audit** -- one Costanza-led episode to
  inventory Windows-vs-Linux-vs-macOS feature gaps, not a
  season. Output feeds whichever season needs the gap closed.
- **Pattern library / cost estimator (FR-015)** -- absorb into
  S12 Observability as the "cost-as-metric" episode rather
  than its own season.
- **Espanso / AHK reference packs** -- ship as a one-off
  community drop alongside S16 Doc/DX, not as standalone work.

## Killed pitches (with reason)

- **Cron-style scheduling as its own season.** Collapses cleanly
  into S08 *Pipelines & Workflows* as one episode. Standalone
  season would be padded.
- **Snap / Flatpak / AppImage as their own season.** Collapses
  into S09 *Distribution & Packaging*. No season-level theme.
- **Internationalization as a season.** Babu's S02E08 audit
  showed the ROI is real but bounded; no contributor or buyer
  signal in 2026 strong enough to justify ten episodes. Stays as
  a recurring per-season audit.
- **Cross-platform parity as a season.** A symptom, not a theme.
  Belongs as audit episodes inside whichever season exposes the
  gap (typically S07 Multimodal and S09 Distribution).
- **"Mobile companion app."** Out of scope for a CLI tool. Cedes
  the surface deliberately. If it ever matters, it's a separate
  project.
- **"Web UI for `az-ai`."** Same reasoning. We are a CLI. Drawing
  a UI is a different product, not a season.
- **"Fine-tune our own model."** Wrong layer. We are a client,
  not a model shop. Maestro can run evals against third-party
  fine-tunes inside S04, which is enough.

> *Note: killed-pitch entries retain their original season-number
> references (S06-S15 as labelled at kill time). They are not
> renumbered when the slate cascades.*

## Roadmap retrospective

One process finding from the showrunner override: dogfooding was
missing from the original 10-candidate slate. Mr. Pitt's pad
covered enterprise, multimodal, pipelines, distribution, editors,
observability, MAF, performance, data/privacy, and doc/DX -- and
none of them captured the meta-loop of using `az-ai` to ship
`az-ai`. Not blame; the methodology section explicitly sourced
candidates from FRs, market signals, competitor moves, and S02
debt patterns -- internal-use cadence wasn't one of the four
inputs. Process improvement for the next roadmap pass: add
"internal-use friction" as a fifth candidate-generation input,
and run a single "what do we use someone else's tool for that we
should use ours for?" prompt across the room before slate-lock.

---

*Larry: pick four. Mr. Pitt will have the season-one blueprint
written before you finish your Snickers.*
