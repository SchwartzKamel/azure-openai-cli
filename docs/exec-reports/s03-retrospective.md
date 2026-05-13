# Season 3 -- Retrospective -- *Local & Multi-Provider*

> *Thirty episodes, seven provider presets, one binary: the Azure-only
> assumption dies quietly in E09, and the season finale proves the corpse
> stayed buried.*

---

## Front matter

| Field | Value |
|---|---|
| **Status** | Shipped -- season closed |
| **Season span** | S03E01 -- S03E30 |
| **Showrunner** | Larry David (E07 onward; Copilot director-of-record pre-E07) |
| **Blueprint** | [`s03-blueprint.md`](s03-blueprint.md) -- *Local & Multi-Provider* |
| **Blueprint episode count** | 27 (24 planned + 3 sweeps-week inserted) |
| **Shipped episode count** | 30 (E01-E27 per blueprint scope + E28-E30 post-blueprint) |
| **Cast** | 28 agents total: 1 showrunner (Larry David) + 5 main cast (Costanza, Kramer, Elaine, Jerry, Newman) + 22 supporting players |
| **Writers' room** | [`s03-writers-room.md`](s03-writers-room.md) |
| **Version at season open** | 2.2.0 |
| **Next season** | S04 -- *Model Intelligence* ([`s04-blueprint.md`](s04-blueprint.md)) |

---

## 1. The premise

S02 was a polish season. By the time it closed, `az-ai` had moved credentials
off the land, the wizard was friendly, docs lint was enforced mechanically, the
binary weighed 13 MiB, and Trivy came back clean. The tool looked and felt
serious. It was also, undeniably, an Azure-only single-shot binary.

S03's mandate was the category pivot: introduce a provider-abstraction seam,
ship at least one non-Azure cloud and one local OpenAI-compatible runtime
through it, and prove that the LOLBin / single-binary / ASCII-clean ergonomics
survived the journey. The Costanza pitch was pithy: "End of S02 we are
excellent at one thing on one provider. End of S03 the same binary speaks at
least three providers in production." The Kramer corollary: "The seam is the
season. Get the seam right and the adapters write themselves."

The blueprint planned 24 episodes in five arcs -- provider seam, first
non-Azure cloud, first local provider, switch ergonomics, hardening -- plus
three already-shipped sweeps-week episodes (the audit triple) inserted as
Arc 1.5. End state per the blueprint: `azure`, `openai`, and at least one
local runtime (`ollama` or `llama-server`), with provider selection driven by
a named profile in a new preferences file. Automatic routing, cost-aware
fallback, and MCP were explicitly out of scope -- S04 and S05 territory. S03
was the seam, not the intelligence.

What actually shipped was more than the blueprint promised in some dimensions
and exactly the blueprint's boundary in others. The seam is real. Seven
provider presets shipped. Persona pinning made it into the season despite not
being in the original 27-episode slate. The season closed with a reproducible
release pipeline, a CodeQL SAST baseline, and a 7-leg release matrix. Three
episodes the blueprint never planned -- E28, E29, E30 -- turned out to be
load-bearing.

---

## 2. What shipped, by arc

The table below maps each aired episode to its blueprint arc (or marks it
as an insertion). "File slot" is the `sNNeMM` file number. "H1 number" is
the episode label in the file's H1 heading, which can differ from the file
slot due to the mid-season numbering cascade explained in Section 3.

| File slot | H1 label | Title | Blueprint arc | Lead | Verdict |
|---|---|---|---|---|---|
| S03E01 | S03E01 | *The Yada Yada Strikes Back* | Pre-season hotfix | Larry David / Kramer | GREEN |
| S03E02 | S03E02 | *The Library Cop's Word Limit* | Pre-season feature | Lt. Bookman | GREEN |
| S03E03 | S03E03 | *The Docs Audit, Reprise* | Arc 1.5 -- Audit Triple | Elaine | YELLOW |
| S03E04 | S03E04 | *The Mailman Knocks Twice* | Arc 1.5 -- Audit Triple | Newman | RED (fixed same sweep) |
| S03E05 | S03E05 | *The Auditor's Auditor* | Arc 1.5 -- Audit Triple | Mr. Wilhelm | YELLOW |
| S03E06 | S03E06 | *The Schema* | Arc 1 -- Provider Seam | Kramer | GREEN |
| S03E07 | S03E07 | *The Redactor* | Arc 1 -- Provider Seam | Newman | GREEN |
| S03E08 | S03E08 | *The Pick* | Arc 2 -- First Non-Azure | Costanza | -- (decision) |
| S03E09 | S03E09 | *The Compat* | Arc 2 -- First Non-Azure | Kramer | GREEN |
| S03E10 | S03E10 | *The Keychain* | Arc 2 -- First Non-Azure | Newman | GREEN |
| S03E11 | S03E11 | *The Wizard, Reprise* | Arc 2 -- First Non-Azure | Jerry | GREEN |
| S03E12 | S03E12 | *The Receipt* | Arc 2 -- First Non-Azure | Kenny Bania | GREEN |
| S03E13 | S03E13 | *The Telemetry* | Arc 2 insertion (extra) | Frank Costanza | GREEN |
| S03E14 | S03E14 | *The Screen Reader* | Arc 3 insertion (extra) | Mickey Abbott | GREEN |
| S03E15 | S03E15 | *The Probe* | Arc 3 -- Local Provider | Costanza | GREEN |
| S03E16 | S03E16 | *The Allowlist* | Arc 3 -- Local Provider | FDR | GREEN |
| S03E17 | S03E17 | *The Stream* | Arc 2 (blueprint E13, cascaded) | Kramer | GREEN |
| S03E18 | S03E18 | *The Capability Gate* | Arc 3 -- Local Provider | Costanza | GREEN |
| S03E19 | S03E19 | *The First Hour, Local Edition* | Arc 3 -- Local Provider | Lloyd Braun | GREEN |
| S03E20 | S03E20 | *The Switch* | Arc 4 -- Switch Ergonomics | Costanza | GREEN |
| S03E21 | S03E17 | *The Server* | Arc 3 (blueprint E17, file slot 21) | Kramer | GREEN |
| S03E22 | S03E22 | *The Default* | Arc 4 -- Switch Ergonomics | Costanza | GREEN |
| S03E23 | S03E22 | *The Fallback* | Arc 4 -- Switch Ergonomics (file slot 23) | Frank Costanza | GREEN |
| S03E24 | S03E24 | *The CVE Log, Per Provider* | Arc 5 -- Hardening | Jerry | GREEN |
| S03E25 | S03E25 | *The Rotation* | Arc 5 -- Hardening | Newman | GREEN |
| S03E26 | S03E26 | *The Offline Mode* | Arc 5 -- Hardening | Newman | GREEN |
| S03E27 | S03E27 | *The Demo* | Finale | Larry David | GREEN |
| S03E28 | -- | *The Persona, Multi-Provider* | Post-blueprint (E23 content, file slot 28) | Kramer | GREEN |
| S03E29 | -- | *The Season Special* | Post-blueprint (CI fix) | Larry David / Frank Costanza | GREEN |
| S03E30 | -- | *The Audit Trilogy* | Post-blueprint (release hardening) | Larry David | GREEN |

---

## 3. What pivoted

### 3a. E01-E02 were not the seam

The blueprint opened with S03E01 *The Adapter* -- define `IProviderAdapter`,
ship the seam with Azure as the only registered adapter, no user-visible
change. What actually aired as E01 was a bug fix: eleven Espanso clipboard
triggers were still on the brittle `TerminatorExpectedAtEndOfString` pattern
that S02E37 was supposed to have killed. The fix only covered form triggers.
Real users found the rest. E01 unified on the proven powershell+heredoc
approach, added 7 new triggers, and cleared 18 docs-lint errors.

E02 introduced Lt. Bookman and the response-tier doctrine (`snap / short /
standard / long / unlimited`) -- a real feature, but not the provider seam
the blueprint wanted in the lead slot. The seam moved to E06 (*The Schema*)
and E07 (*The Redactor*), arriving later but more thoroughly grounded.

The more important divergence: the blueprint called for `IProviderAdapter` --
a formal interface hierarchy with `chat`, `stream`, `capabilities`, and
`model resolution` methods, registered in a `ProviderSelector`. What shipped
instead was `OpenAiCompatAdapter`, a flat HTTP client with bearer auth and a
preset registry (`OpenAiCompatPreset`). No interface. No `ProviderSelector`.
The Kramer corollary proved correct: the seam was a pragmatic HTTP adapter,
and it wrote itself.

### 3b. The audit triple inserted Arc 1.5

Three episodes were inserted before the planned Arc 1 seam work:

- **E03 -- *The Docs Audit, Reprise*** (Elaine): version pins drifted, two
  YAMLs collided on `:aidata`, CHANGELOG had gone empty. YELLOW verdict --
  22 findings.
- **E04 -- *The Mailman Knocks Twice*** (Newman): RED verdict -- F-1
  CRITICAL bash injection and F-2 HIGH in the prompt templates the showrunner
  had shipped the same morning. Both fixed same sweep in commit `c25ca38`.
- **E05 -- *The Auditor's Auditor*** (Mr. Wilhelm): meta-audit of the
  audits. Found 50% follow-through rate on prior findings. YELLOW. The fix
  was mechanical: commit `de478d2` wired the findings-backlog gate into
  `make exec-report-check`, so a push with untracked CRITICAL/HIGH findings
  now fails preflight.

Inserting E03-E05 shifted all downstream arc numbering by three. The blueprint
had planned E03 as the start of Arc 1. Arcs 1-5 effectively ran as E06-E27
in the shipped season. This cascade is the primary source of the H1/file-slot
mismatch described in the next subsection.

### 3c. The H1/file-slot cascade and E27 reconciliation

By Wave 6 (telemetry + a11y), the episode-counter inside exec-report H1
headings had diverged from the file-slot counter. Two concrete artifacts:

- `s03e21-the-server.md` has H1 `# S03E17 -- *The Server*` (the blueprint
  story number) while the file slot is 21 (the actual sequential position).
- `s03e23-the-fallback.md` has H1 `# S03E22 -- *The Fallback*` while
  the file slot is 23 (because *The Default* claimed slot 22 after
  *The Server* slid to 21).

The E27 writers'-room reconciliation pass (`docs/exec-reports/s03e27-the-demo.md`
and `s03-writers-room.md`) documented the canonical lookup table. The rule
adopted: exec-report file paths are stable historical artifacts and are never
renamed; the writers'-room table is the canonical cross-reference. Story-arc
numbers (blueprint labels) remain the narrative order; file-slot numbers are
the file-system order. Both are valid references; neither is wrong.

### 3d. Telemetry and a11y replaced two planned episodes

Blueprint E13 was *The Stream* -- verify streaming and tool-call parity on
the new adapter, freeze the capability matrix. Blueprint E14 was *The Daemon*
-- Ollama via the OpenAI-compat adapter.

What shipped in those slots:

- **E13 -- *The Telemetry*** (Frank Costanza): opt-in NDJSON telemetry
  (`AZ_AI_TELEMETRY=1`, strict equality, bucketed latency, stderr JSON
  line, never writes PII). SLO charter seeded. The blueprint never mentioned
  telemetry -- Frank walked into the writers' room with it.
- **E14 -- *The Screen Reader*** (Mickey Abbott): `Plain.cs` chokepoint,
  `--plain` flag, `NO_COLOR` / `TERM=dumb` / `AZ_AI_PLAIN` honored,
  18-site ASCII glyph audit, 28 unit + 6 integration tests. Also not in
  the blueprint.

*The Stream* cascaded to E17 as a verification-only episode (no production
code change, 15 streaming + tool-call parity facts). *The Daemon* (Ollama)
was absorbed into E19 *The First Hour, Local Edition* -- Lloyd Braun covered
Ollama in the onboarding tutorial rather than in a standalone episode.

### 3e. Three post-blueprint episodes closed the season

The blueprint ended at E27 (*The Demo*). Three more episodes shipped:

- **E28 -- *The Persona, Multi-Provider*** (Kramer): per-persona `provider`
  and `model` pins in `.squad.json`, new `cli > env > profile > persona >
  default` precedence rung. File slot 28 because slots 22-27 were already
  claimed. The blueprint had this as E23 in its story arc; the content
  landed on schedule but the file could not claim its intended slot.
- **E29 -- *The Season Special*** (Larry David / Frank Costanza): the
  season finale (E27) shipped with three CI red lines -- integration-test
  creds-gate ordering, markdownlint baseline collapse, and smart-quote
  accumulation across the prompt-templates wave. E29 fixed all three.
  466 markdownlint errors reduced to zero. 111/111 integration tests green.
- **E30 -- *The Audit Trilogy*** (Larry David): four parallel audits, eight
  fixes, one reproducible release pipeline. CodeQL SAST baseline. 7-leg
  release matrix (linux-x64, linux-arm64, osx-x64, osx-arm64, win-x64,
  win-arm64). CHANGELOG-sourced release notes via awk. `make
  release-precheck` (5 gates). Deterministic AOT build. Per-asset SHA256
  digests. `docs/process/release.md` runbook. The season was not
  production-release-ready without E30.

---

## 4. Tentpole episodes

Six episodes defined the season's shape. Everything else either enabled one
of these or cleaned up after one.

### E06 -- *The Schema* (Kramer)

`Preferences.cs` (212 lines), `ProviderEntry`, `ProfileEntry`, `--config
show`. The seam became real the moment this landed. Before E06 there was a
blueprint with a premise. After E06 there was a `preferences.json` file on
disk and a resolution chain with four named rungs. The 657 prior tests stayed
green. Fifteen new facts shipped. This is the episode the entire arc pivoted
on -- every provider, every profile, every precedence question in E09-E28
depends on the schema E06 defined.

### E09 -- *The Compat* (Kramer)

`OpenAiCompatAdapter` ships. OpenAI direct, Groq, Together, and Cloudflare
ride one HTTP adapter with bearer auth and a base-URL registry. The blueprint
worried about three SDKs and AOT binary pressure. The pragmatic HTTP adapter
eliminated the problem -- one file, one HTTP client, zero new SDK references,
four providers covered. The Kramer corollary paid off in full.

### E16 -- *The Allowlist* (FDR)

`EndpointAllowlist.cs`, eight-state `AllowlistVerdict` enum, 57 adversarial
test cases. This episode is the SSRF gate that stands between the
OpenAI-compat adapter and every malicious base URL an operator might configure.
Without it, `--provider llamacpp` is a vector: a crafted
`AZ_AI_LLAMACPP_ENDPOINT` could exfiltrate the Azure API key to an attacker-
controlled host. FDR's episode -- the adversarial red-team lead, not Newman --
is the right casting choice here: the threat model is not "what is our
security posture" but "what happens when someone actively tries to break it."
The `AZ_AI_LOCAL_PROVIDERS=1` opt-in requirement is FDR's explicit default-
deny design; the gate in `OpenAiCompatAdapter.Build()` enforces it.

### E20 -- *The Switch* (Costanza)

`ResolutionInputs` record. `--provider` / `--profile` / `--model` CLI flags.
`AZ_PROVIDER` / `AZ_PROFILE` env vars. `--config show` source field. 44
unit facts. 6 integration assertions. The precedence chain went from folklore
to a pure function with a deterministic, testable output. Before E20, "which
provider does az-ai pick in this environment" was a question you answered by
reading `Program.cs`. After E20, it is a question you answer by calling
`PreferencesResolver.Resolve()` with a `ResolutionInputs` snapshot and
reading the `Source` field. E22 (*The Default*) and E23 (*The Fallback*) are
both built directly on the E20 resolution contract.

### E28 -- *The Persona, Multi-Provider* (Kramer)

The `cli > env > profile > persona > default` rung. Per-persona `provider`
and `model` pins in `.squad.json`. `SquadCoordinator.ApplyPersonaPin()` --
pure, no Console writes, no env reads outside the passed-in snapshot.
Missing-creds pin drops silently with a single warning line (suppressed under
`--raw`). `SquadConfig.Validate()` rejects unknown providers at load time with
an actionable error. 42 new unit facts in `PersonaProviderPinTests.cs`. The
capability gate (E18), endpoint allowlist (E16), and offline gate (E26) all
remain in front of dispatch -- the persona pin is not an escape hatch, it is a
rung in the chain. This is the episode that proved the S03 premise end-to-end
for the squad use case: same binary, three personas, three providers, one
giddyup.

### E30 -- *The Audit Trilogy* (Larry David)

The season could not ship v3.0 without E30. Four parallel audits (release
matrix, release notes, fallback parser, CodeQL) found eight gaps. Every one
was fixed in the same episode. The 7-leg release matrix means arm64 on both
Linux and Windows ships as a first-class artifact. CHANGELOG-sourced release
notes mean the release body can never drift from what was documented during
development. Deterministic AOT means two independent builds of the same
commit produce bit-identical output. The CodeQL SAST baseline means the next
C# vulnerability class has a gate. `make release-precheck` means the
operator has 5 automated checks before they tag a release. None of this is
glamorous. All of it is irreversible -- once you have it, you cannot imagine
shipping without it.

---

## 5. Cross-episode lessons

### Lesson 1 -- Parallel-agent dispatch is now a production work pattern

S01 used agents experimentally. S02 used them with increasing regularity.
By S03E08 (*The Pick*), the dispatch brief was the primary work artifact: a
structured document naming wave participants, go/no-go gates, and which agent
must not commit. By S03E13-E16 (the four-agent Wave 6), parallel dispatch was
the default mode for any episode touching more than one concern. The
`fleet-dispatch` skill and the `episode-brief` skill exist because the pattern
had stabilized enough to codify.

The failure mode we learned to avoid: cascading commits from parallel agents
writing to the same shared file (CHANGELOG, writers-room, README). The
`shared-file-protocol` skill was the fix -- an explicit "orchestrator owns
the shared files; agents stage, orchestrator merges." The E27-E28 wave
demonstrated this protocol working cleanly under a six-agent concurrent
dispatch.

### Lesson 2 -- Audits without gates are theater

E05 (*The Auditor's Auditor*) measured 50% follow-through on prior findings.
The number was not surprising; it was embarrassing. The response was
mechanical, not motivational: commit `de478d2` wired the findings-backlog gate
into `make exec-report-check`. Now a push that has open CRITICAL or HIGH
findings in the tracked backlog fails preflight. The gate is not a substitute
for remediation; it is a forcing function that prevents the next E05 from
finding the same gap.

The audit-triple structure (docs audit, security audit, meta-audit of the
audits) is itself a lesson in epistemic discipline: you cannot know what the
security surface looks like from inside the room that shipped it. Newman's RED
verdict in E04 (bash injection, F-1 CRITICAL, found the morning after the
showrunner shipped the prompt templates) is the proof point. The templates
passed subjective review. They failed adversarial review. The distinction
matters.

### Lesson 3 -- "Green locally" is not "green in CI"

E29 (*The Season Special*) existed entirely because of this lesson. Frank's
integration tests for the `--fallback` parser passed on developer machines
where `~/.config/az-ai/env` auto-populates `AZUREOPENAIENDPOINT`. CI has no
such file. The fix was structural: parse-error tests must self-supply dummy
creds, or the production binary must reorder gates so flag-parse errors fire
before the creds check. E30 implemented approach (a) -- `FallbackPolicy.Resolve`
is now purely syntactic and fires alongside `--help` / `--version` / `--doctor`,
before the endpoint gate. The `fb_dummy_env` workaround from E29 was removed;
111/111 integration tests are green without it.

The companion lesson: when one CI step is chronically red, every other step
is invisible behind it. The smart-quote grep ran first and hard-failed often
enough that markdownlint never executed against `main`. When the quotes went
green, 466 markdownlint errors appeared. The Soup Nazi's standing rule is "no
merge for you" -- it is also "no shadow for you."

### Lesson 4 -- CHANGELOG-as-truth or chaos

The original release pipeline used GitHub's `generate_release_notes`. E30
replaced it with awk extraction from `CHANGELOG.md [Unreleased]`. The reason
is not elegance -- it is correctness. `generate_release_notes` produces a
commit-title soup that does not match the prose a human wrote in the changelog.
The changelog is the source of truth because it is written by a human (or a
human-supervised agent) at the time of the work. The release notes are a
projection of that truth. If the projection and the truth diverge, the
projection is wrong. E30 closed the divergence permanently.

### Lesson 5 -- One HTTP adapter beats five SDKs

The blueprint's biggest risk item was: "Native .NET SDKs for Anthropic,
Gemini, Bedrock -- their reflection footprint under Native AOT is unverified.
Mitigation: keep the generic OpenAI-compat adapter as the only shipped
non-Azure path in S03." The mitigation was the right call and better than
anticipated. The OpenAI-compat adapter covers not three providers but seven:
OpenAI direct, Groq, Together, Cloudflare, llama.cpp, and any
OpenAI-compatible endpoint an operator configures. Zero new SDK references.
Binary size stayed in budget. TTFT was unaffected. The blueprint worried about
SDK complexity; the shipped season has less SDK surface than S02, not more.

### Lesson 6 -- The redactor should have been E01

`SecretRedactor` (E07) scrubs credentials from all nineteen `ErrorAndExit`
call sites in `Program.cs`, plus the top-level catch in `Main`. The design
is centralised: `ErrorAndExit` runs every message through `Redact` before
writing to stderr. If something escapes from `RunAsync` -- a third-party SDK
exception, anything -- it cannot reach stderr without going through the
scrubber first.

The lesson is not that the redactor is hard to build (it is not -- six regex
patterns, one timeout, 400 lines including tests). The lesson is that the
redactor should have shipped before the first credential path, not after ten
months of production use. The nineteen call sites that could have leaked are
a direct measure of how debt accumulates. Going forward: `SecretRedactor.Redact`
wraps every new error path at the time the path is written, not in a follow-up
episode.

### Lesson 7 -- Episode insertions have a numbering cost

Inserting three episodes mid-season (E03-E05) shifted all downstream arc
numbering. The resulting mismatch between blueprint story-arc numbers, file
slot numbers, and H1 headings in certain files (most visibly
`s03e21-the-server.md` headered as `S03E17` and `s03e23-the-fallback.md`
headered as `S03E22`) required a dedicated reconciliation pass and leaves a
permanently visible discontinuity in the historical record. For future seasons:
if an insertion is discovered early enough, renumber forward immediately.
If discovered too late to rename files, write the reconciliation table at
insertion time (not at E27).

### Lesson 8 -- Supporting-player episodes are underrated

E05 (Wilhelm), E13 (Frank Costanza), E14 (Mickey Abbott), E19 (Lloyd Braun),
E23 (Frank Costanza), E24 (Jerry), E30 (Larry David orchestrating parallel
audits) -- none of these are "main cast" headline episodes. All of them were
load-bearing. Wilhelm's meta-audit in E05 produced the findings-backlog gate.
Mickey's a11y episode in E14 produced `Plain.cs`, which is now the
single chokepoint for all output formatting. Lloyd's onboarding episode in E19
produced `docs/onboarding/local-providers.md` (~590 lines) and the "front
door" process improvement. The supporting bench is not decoration -- it is
the difference between a product that works and a product that is maintainable.

---

## 6. What S03 did NOT ship

These items were explicitly out of scope per the blueprint, or deferred during
the season. None are regressions; most are S04 or S05 work.

**By design (boundary items):**

- **Intelligent / cost-aware routing.** The preferences file has a routing
  table in the schema. No automatic decisions fire against it. S04
  (*Model Intelligence*) owns this.
- **MCP client or server.** S05 (*Protocols & Plugins*) owns this.
- **SDK-based Anthropic / Gemini / Bedrock adapters.** AOT reflection
  footprint unverified. S04 candidate gated on verification. FR-024 filed.
- **Full FR-015 cost estimator.** E12 (*The Receipt*) shipped rate-card
  stubs only. The S03 `--verbose` cost output shows per-provider rates;
  smart routing against them is S04.
- **FR-019 gemma.cpp direct adapter.** Non-OpenAI-compat wire format;
  native adapter cost is not justified against the S03 adapter seam.
  Deferred.
- **Enterprise SSO, audit logging, data-residency controls.** S07 candidate
  per `seasons-roadmap.md`.
- **`IProviderAdapter` formal interface.** The pragmatic HTTP adapter made
  the interface unnecessary. The ADR-010 decision is correct; the blueprint's
  interface sketch was over-engineered.

**Deferred from blueprint scope:**

- **`--config set` / directory overrides.** E06 shipped `--config show`
  (read path). The write path (`--config set`) was deferred. The schema is
  stable; the write command is S04 housekeeping.
- **`az-ai providers doctor` as `providers` subcommand.** E15 shipped
  `az-ai --doctor`, not `az-ai providers doctor`. The ergonomic difference
  was evaluated and the single-flag form was cleaner for the use case.
- **Capability matrix freeze (FR-014 SS4.4).** The capability gate (E18)
  ships the matrix and the dispatch guard. The "freeze" as a versioned
  artifact was not pursued -- capability overrides (`AZ_AI_CAPABILITY_OVERRIDES`)
  serve the same purpose more flexibly.
- **Per-provider rate-card full implementation.** E12 stubs exist;
  the full pattern library (FR-015) and cost-aware picker are S04.
- **NIM (NVIDIA) adapter.** OpenAI-compat and could land in the E14 slot
  per the blueprint's open question. Decision was to keep S03 provider-
  agnostic and leave NIM to S04.

**Filed open (post-E27):**

- **Fallback cred discovery (`frank-2026-05-FB-1`).** The production
  `AlternateChatClientFactory` returns `Skipped("no-fallback-creds")`.
  Per-preset credential discovery -- knowing whether a fallback provider
  is reachable before the primary fails -- is the missing piece. S04.
- **`--config show` persona rung echo (`kramer-2026-05-PMP-2`).** `--config
  show` does not yet display the persona rung in the resolution source chain.
  Minor UX gap; filed.
- **Recorded asciinema cast.** The season-3 finale demo script
  (`scripts/demo/season3-finale.sh`) is recordable; the recording itself
  is a Peterman / Keith Hernandez follow-up for the v3.0 release moment.
- **Node-20 era action major bumps.** `actions/upload-artifact@v3` and
  friends are warnings today; hard-fail September 2026. Deferred to a
  dedicated CI-hygiene episode -- not mixed with the E30 security pass to
  avoid causal confusion.

---

## 7. Numbers

These figures are best-effort snapshots from the season-close state. The
per-episode exec reports are the authoritative record for any specific metric.

| Metric | Value | Source |
|---|---|---|
| Episodes aired | 30 (E01-E30) | This file |
| Blueprint episodes | 27 | `s03-blueprint.md` |
| Post-blueprint episodes | 3 (E28, E29, E30) | `s03-writers-room.md` |
| Unit tests at season close | 1,299 | E28 preflight output |
| Integration tests at season close | 111 | E29/E30 preflight output |
| Provider presets shipped | 7 (azure, foundry, openai, groq, together, cloudflare, llamacpp) | `OpenAiCompatAdapter.cs` |
| Program.cs lines at close | ~3,689 | `wc -l azureopenai-cli/Program.cs` |
| Program.cs lines at S03 open | ~2,200 | S02 season-close reference |
| New source files (production) | ~15 | (`Preferences.cs`, `OpenAiCompatAdapter.cs`, `SecretRedactor.cs`, `EndpointAllowlist.cs`, `CapabilityGate.cs`, `ProviderDoctor.cs`, `Plain.cs`, `Observability/CompatCostRates.cs`, `Observability/CostEstimator.cs`, `Resilience/FallbackPolicy.cs`, `Resilience/FallbackChain.cs`, `Cli/CredsRotate.cs`, `Cli/MaskedInput.cs`, `Net/EndpointAllowlist.cs`, `Squad/SquadCoordinator.cs` extension) |
| New test files | ~10 | (`PreferencesTests.cs`, `OpenAiCompatAdapterTests.cs`, `ResolutionPrecedenceTests.cs`, `CapabilityGateTests.cs`, `FallbackChainTests.cs`, `PersonaProviderPinTests.cs`, `LlamaCppPresetTests.cs`, and others) |
| Agent cast at season close | 28 (1 showrunner + 5 main + 22 supporting) | `AGENTS.md` |
| Release matrix legs at close | 7 (linux-x64, linux-arm64, osx-x64, osx-arm64, win-x64, win-arm64) | `.github/workflows/release.yml` |
| CI workflows | 4 (build-and-test, integration-test, docker, codeql) | `.github/workflows/` |
| New docs | ~20 files across `docs/`, `scripts/demo/`, `docs/onboarding/`, `docs/process/` | Cumulative across arcs |
| Espanso triggers at close | 22 (from 13 at S02 close) | `examples/espanso-ahk-wsl/` |
| Commits in S03 range | ~35 | `git log --oneline` |

---

## 8. Setup for S04 -- *Model Intelligence*

S03 built the orchestra. S04 is the conductor. The Maestro's framing from the
S04 blueprint kickoff: "Multi-provider was the orchestra. This season is the
conductor. A score with no one waving the baton is just a hundred people
tuning."

By the time S03 closes, `az-ai` can talk to seven providers through a single
adapter seam. What it cannot do is decide which one to use for a given prompt,
now, given cost constraints, quality requirements, and the user's current
context. The preferences file has a routing table. No automatic decisions fire
against it. The capability gate knows what each provider can do. Nothing uses
that knowledge to make a decision. The fallback chain exists but requires
explicit opt-in and explicit configuration. The missing piece is intelligence:
defensible defaults, per-request strategy hints, an eval harness that can prove
a change made a prompt better or worse.

S04 does not break any S03 contract. The adapter seam, the preferences schema,
the resolution chain, the capability gate -- all of these are the stable
foundation S04 builds on. The S04 blueprint is explicit that every feature
must clear two bars: improve the default behavior (no flag required) AND
produce machine-readable output the eval harness can consume. If a feature
cannot be evaluated, it does not ship. That discipline is possible because
S03 wired the telemetry (`AZ_AI_TELEMETRY=1`, bucketed latency, NDJSON line
on stderr), the capability matrix, and the resolution source chain -- three
S04 requirements that S03 provided before S04 asked for them.

See [`docs/exec-reports/s04-blueprint.md`](s04-blueprint.md) for the full
S04 slate.

---

## 9. Credits

### Showrunner

- **Larry David** -- Episode conception, fleet dispatch, writers' room
  ownership, sign-off, and the post-blueprint episodes that turned a
  27-episode season into a 30-episode one. Director-of-record from E07
  onward; Copilot held the director chair for E01-E06 per the pre-E07
  convention.

### Main cast

- **George Costanza** (PM) -- Led E08 (*The Pick*), E15 (*The Probe*),
  E18 (*The Capability Gate*), E20 (*The Switch*), E22 (*The Default*).
  Owner of the resolution chain, the ADR-010 decision, ADR-011, and the
  `--config show` ergonomic surface.
- **Cosmo Kramer** (Engineer) -- Led E06 (*The Schema*), E09 (*The Compat*),
  E21/slot-17 (*The Server*), E28 (*The Persona, Multi-Provider*). Shipped
  `OpenAiCompatAdapter`, `Preferences.cs`, the llama.cpp preset, and the
  persona provider-pin seam.
- **Elaine Benes** (Technical Writer) -- Led E03 (*The Docs Audit, Reprise*).
  Consulting presence across every docs wave. Owns the "front door" process
  improvement and the docs-audit cadence that caught the `:aidata` collision.
- **Jerry Seinfeld** (DevOps) -- Led E11 (*The Wizard, Reprise*), E24
  (*The CVE Log*). Owned the provider-aware wizard, the CVE pipeline, and
  the integration-test CI repairs that made green mean green.
- **Newman** (Security) -- Led E04 (*The Mailman Knocks Twice*), E07
  (*The Redactor*), E10 (*The Keychain*), E25 (*The Rotation*), E26
  (*The Offline Mode*). Newman's five-episode count is not an accident --
  per-provider security review is the load-bearing wall of this season. The
  redactor, the keychain namespacing, the BYOK rotation, and the offline gate
  are all Newman.

### Supporting players (episode leads and key contributors)

- **Lt. Bookman** -- Led E02 (*The Library Cop's Word Limit*). Owner of
  the response-tier doctrine and the `--max-tokens` budget surface.
- **Mr. Wilhelm** -- Led E05 (*The Auditor's Auditor*). The meta-audit that
  produced the findings-backlog gate and the audit-cadence policy.
- **Frank Costanza** -- Led E13 (*The Telemetry*), E23 (*The Fallback*).
  Opt-in NDJSON telemetry, SLO charter, fallback chain, and the creds-gate
  ordering diagnosis that drove E29.
- **FDR** (Franklin Delano Romanowski) -- Led E16 (*The Allowlist*). The
  SSRF gate, 57 adversarial cases, the `AZ_AI_LOCAL_PROVIDERS` opt-in
  architecture.
- **Mickey Abbott** -- Led E14 (*The Screen Reader*). `Plain.cs` chokepoint,
  `--plain` flag, 18-site ASCII glyph audit, `NO_COLOR` / `TERM=dumb`
  honored.
- **Lloyd Braun** -- Led E19 (*The First Hour, Local Edition*). The
  `docs/onboarding/local-providers.md` tutorial (~590 lines) and the "front
  door" process improvement (junior lens: "wait, where would I have looked
  for that?").
- **Kenny Bania** -- Led E12 (*The Receipt*). Bench harness, prewarm probe,
  `CompatCostRates.cs`, rate-card stubs.
- **The Maestro** -- Contributed to E18 (*The Capability Gate*).
  Provider+model capability matrix, the Conservative/Permissive factory
  shape, the `AZ_AI_CAPABILITY_OVERRIDES` escape hatch.
- **Morty Seinfeld** -- E12 consulting; cost-rate economics review.
- **J. Peterman** -- E27 (*The Demo*) launch copy; `docs/season-recaps/
  season-3-recap.md` marketing-grade retrospective.
- **Keith Hernandez** -- E27 demo recording guidance.
- **Mr. Pitt** -- OKR and roadmap oversight at mid-season checkpoints.
- **Mr. Lippman** -- Release-notes review and SemVer discipline.
- **Sue Ellen Mischke** -- Competitive-analysis consulting; Anthropic deferral
  comms (FR-024 ledger).
- **Jackie Chiles** -- License compliance review on new dependencies.
- **Uncle Leo** -- Community onboarding tone review.
- **Bob Sacamano** -- Ecosystem packaging recommendations for the
  multi-provider surface.
- **Rabbi Kirschbaum** -- Ethics review on opt-in telemetry and capability-gate
  design (does refusal messaging respect user agency?).
- **Russell Dalrymple** -- UX review on `--config show` output formatting,
  `--doctor` output structure, and first-run wizard menu presentation.
- **Babu Bhatt** -- ASCII sweep on the prompt-templates wave (E29 found the
  smart-quote drift).
- **The Soup Nazi** -- Markdownlint baseline enforcement; merge gatekeeping.
  E29 was largely a Soup Nazi cleanup.

### Co-author trailer

All commits in the S03 range include the standard Copilot co-author trailer:

```text
Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>
```

---

*S03 closed. S04 opens.*

*The seam is in place. The adapters wrote themselves, as promised.*
*The conductor picks up the baton.*
