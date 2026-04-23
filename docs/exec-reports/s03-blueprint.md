# Season 3 -- Blueprint -- *Local & Multi-Provider*

**Status:** Draft v2 -- awaiting showrunner greenlight
**Lead writer:** George Costanza (PM)
**Guest in the room:** Cosmo Kramer (engineering, feasibility seams)
**Supersedes:** the v1 three-theme bake-off treatment of this same file.

## Showrunner note

S02 was a polish season -- credentials moved off the land, the wizard
got friendly, the docs lint went teeth-out, the AOT binary stayed
13 MiB and Trivy-clean. We finished the season looking and feeling
like a serious tool. S03 is where we stop *being* a tool and start
*being a category entrant*. The pivot is not a rewrite, it is a seam:
introduce a provider abstraction, ship one non-Azure cloud and one
local backend through it, and prove the LOLBin / single-binary /
ASCII-clean ergonomics survive the journey. If S02 was *can people
trust us*, S03 is *can people use us when "the cloud" is not a
sentence they are allowed to say*.

## Theme statement

End of S02, `azure-openai-cli` is an Azure-OpenAI-native single-shot
binary with persona memory, OS-keystore credentials, and a clean
agent loop. It is excellent at one thing on one provider. End of S03,
the **same binary** speaks at least three providers in production:
Azure OpenAI (the default, the one we offer SLAs against), one
non-Azure cloud (Anthropic Claude or OpenAI direct -- pick one in
E05), and at least one local OpenAI-compatible runtime (Ollama in
E11, llama.cpp `llama-server` in E14). Provider selection is a
named profile in the FR-014 preferences file, not a recompile.

Equally important: end of S03, we have the *seam* but not yet the
*intelligence*. There is no automatic routing, no cost-aware fallback,
no MCP, no plugin marketplace, no multimodal. Those live in S04, S05,
and beyond. S03's job is to make all of that possible without
breaking what S02 already shipped.

## Why this season, why now

The 2026 CLI-LLM landscape (see `docs/competitive-landscape.md` and
the E19 brief): every credible competitor -- `aichat`, `llm`,
`fabric`, `mods`/`crush` -- ships at least three providers and at
least one local backend. Sue Ellen's read in S02E19 was blunt:
single-provider is no longer "focused", it is "narrow". Users who
hit our README and search "ollama" inside it and find nothing bounce
to `aichat` inside thirty seconds. Meanwhile our own roadmap has
**four** standing FRs (FR-014, FR-018, FR-019, FR-020) all blocked on
the same thing -- a real provider abstraction wired into a real
preferences file. They have been blocked for two months. S03 unblocks
them.

The market timing is also right. As of 2026, every major non-Azure
provider (Anthropic, Google, Mistral, Groq, Together, Fireworks,
OpenAI direct) ships either a native .NET SDK or an OpenAI-compatible
REST endpoint -- usually both. Local runtimes have converged:
`ollama serve` and `llama-server` both speak OpenAI chat-completions
on a configurable base URL. The integration cost in 2026 is dramatically
lower than it would have been in 2024. Waiting another season means
shipping the seam in S04 instead of using S04 for the *interesting*
work (intelligent routing, cost gating) that the seam enables.

## Landscape snapshot (2026)

Compact view -- full long-form lives in
`docs/competitive-analysis.md`. "OpenAI-compat" means the endpoint
accepts a stock OpenAI chat-completions request without a translator
layer.

| Backend | Type | OpenAI-compat REST | Native .NET SDK | Single-binary install | Auth model | On-prem option |
|---|---|---|---|---|---|---|
| Azure OpenAI | Cloud | Yes (via deployment) | `Azure.AI.OpenAI` 2.1.0 | n/a (we are the binary) | API key + endpoint | Yes (Azure Stack / Foundry) |
| OpenAI direct | Cloud | Yes (canonical) | `OpenAI` 2.x | n/a | Bearer | No |
| Anthropic Claude | Cloud | Partial (Messages != ChatCompletions; bridge needed) | `Anthropic` NuGet | n/a | Bearer | Via Bedrock |
| Google Gemini | Cloud | Partial (Vertex bridge) | `Google.GenAI` | n/a | Bearer / ADC | Via Vertex |
| AWS Bedrock | Cloud meta | No (per-model body) | `AWSSDK.BedrockRuntime` | n/a | SigV4 | Yes |
| Mistral La Plateforme | Cloud | Yes | Community | n/a | Bearer | No |
| Groq | Cloud | Yes | Community | n/a | Bearer | No |
| Together.ai | Cloud | Yes | Community | n/a | Bearer | No |
| Fireworks | Cloud | Yes | Community | n/a | Bearer | No |
| Ollama | Local runtime | Yes (`/v1`) | n/a (HTTP) | Yes (single daemon) | Optional bearer | Yes |
| llama.cpp `llama-server` | Local runtime | Yes (`/v1`) | n/a (HTTP) | Yes (single binary) | Optional bearer | Yes |
| LM Studio | Local runtime + GUI | Yes | n/a | GUI app | Optional bearer | Yes (desktop) |
| MLX (Apple) | Local runtime | Via wrapper | n/a | Apple Silicon only | n/a | Yes (device) |
| vLLM | Local server | Yes | n/a | No (Python stack) | Optional bearer | Yes (GPU host) |
| GPT4All | Local runtime + GUI | Partial | n/a | Desktop | n/a | Yes |
| LiteLLM | Routing proxy | Yes (re-emits OpenAI) | n/a (HTTP) | Python | Bearer | Yes (self-host) |
| OpenRouter | Routing service | Yes | n/a (HTTP) | n/a | Bearer | No |
| Helicone | Observability proxy | Yes (transparent) | n/a (HTTP) | n/a | Bearer | Yes (self-host) |

**Costanza take.** Two seams give us coverage of the table without
swallowing five SDKs into our AOT binary:

1. **Native Azure path** -- stays as-is (`Azure.AI.OpenAI` 2.1.0,
   the one path we offer SLAs against).
2. **Generic OpenAI-compat HTTP path** -- a thin
   `HttpClient`-based adapter that targets *any* OpenAI-compatible
   `/v1/chat/completions` endpoint. That single adapter covers
   OpenAI direct, Mistral, Groq, Together, Fireworks, Ollama,
   llama.cpp, LM Studio, vLLM, LiteLLM, OpenRouter, and Helicone
   essentially for free.

Anthropic, Gemini, and Bedrock get a *capability-flagged stub* in
S03 (acknowledged in the schema, deferred to S04 or later) because
their wire formats need real bridge code and the AOT cost of three
extra SDKs is not yet justified. **Kramer concurs**: "One adapter,
one base URL, one bearer -- I can build that in a weekend. Three
SDKs and a credential shape per cloud -- that's a season."

## 24-episode candidate slate

Each episode is one PR-sized unit of work. Lead-cast quotas: Costanza
3, Kramer 4, Elaine 3, Jerry 3, Newman 3. Supporting players one
each (with overlaps where natural). Lloyd Braun explicitly leads at
least one onboarding episode (E16) and consults on the wizard
extension in E18.

### Arc 1 -- Provider Abstraction Seam (E01-E04)

- **S03E01 -- *The Adapter*.** Define `IProviderAdapter`
  (chat, stream, capabilities, model resolution). Ship the seam with
  Azure as the only registered adapter. No user-visible change. **Lead:
  Kramer.** *FR-014, FR-018 §4.1.*
- **S03E02 -- *The Factory*.** `ProviderSelector` resolves
  flag -> env -> profile -> default per ADR-009. `--provider <name>`
  flag introduced; only `azure` is a legal value. **Lead: Costanza.**
  *FR-014, ADR-009.*
- **S03E03 -- *The Schema*.** Land `preferences.json` v1 schema with
  `providers{}` and `profiles{}` sections. `az-ai --config show`
  prints resolved provider + source layer. **Lead: Elaine.**
  *FR-014, FR-003 (absorbed), FR-009 (absorbed).*
- **S03E04 -- *The Redactor*.** Centralised secret redactor on every
  log / exception path; `Authorization: Bearer ...` in any error
  message becomes a P1 unit-test failure. **Lead: Newman.**
  *ADR-007 §2.*

### Arc 2 -- First Non-Azure Cloud (E05-E10)

- **S03E05 -- *The Pick*.** Decision episode -- Anthropic vs OpenAI
  direct as the first non-Azure cloud. Costanza writes the decision
  ADR; Sue Ellen weighs in on competitive optics. (Recommendation in
  this draft: **OpenAI direct**, because it slots straight into the
  generic OpenAI-compat adapter with zero bridge code.) **Lead:
  Costanza.** *FR-014.*
- **S03E06 -- *The Compat*.** Implement the generic
  `OpenAiCompatAdapter` (HTTP + bearer + base URL + optional
  org-header). Wire it behind `--provider openai`. **Lead: Kramer.**
  *FR-014, FR-018 §4 (shared adapter).*
- **S03E07 -- *The Keychain*.** Extend the per-OS credential store
  from S02E04 (the locksmith) to namespace by provider:
  `az-ai/openai/api_key` distinct from `az-ai/azure/api_key`.
  **Lead: Newman.** *ADR-007 §2, FR-014 §3.*
- **S03E08 -- *The Wizard, Reprise*.** First-run wizard learns to
  ask "which provider?" and writes a `default` profile. Falls back
  to Azure-only path if user just hits enter. **Lead: Jerry.**
  *FR-022, FR-023, FR-014.*
- **S03E09 -- *The Receipt*.** Per-provider rate-card stubs in the
  cost path -- enough to render "Azure: $0.0021 / OpenAI: $0.0018"
  in `--verbose`, no smart picking yet. **Lead: Morty Seinfeld.**
  *FR-015 stub, FR-014.*
- **S03E10 -- *The Stream*.** Verify streaming + tool-call parity
  on the new adapter; freeze the capability matrix for v3.0.
  **Lead: Puddy.** *FR-014 §4, FR-018 §4.4.*

### Arc 3 -- First Local Provider (E11-E16)

- **S03E11 -- *The Daemon*.** Ollama via the OpenAI-compat adapter --
  zero new code, only docs + a `providers.ollama` example profile.
  Proves the seam was right. **Lead: Kramer.** *FR-018, FR-014.*
- **S03E12 -- *The Probe*.** `az-ai providers doctor` -- pings each
  configured provider, reports reachable / authed / streaming /
  tools. Mirrors the `az-ai doctor` ergonomics from S02. **Lead:
  Jerry.** *FR-018 §4.5.*
- **S03E13 -- *The Allowlist*.** SSRF allowlist for local provider
  base URLs -- `127.0.0.1/32`, `::1/128`, `localhost`, plus a
  user-extendable list with a loud warning. Covers ADR-007 §3
  for every future HTTP adapter. **Lead: Newman.** *ADR-007 §3,
  FR-018 §4.6.*
- **S03E14 -- *The Server*.** llama.cpp `llama-server` adapter --
  same OpenAI-compat path, but document the `--api-key`,
  `--host 127.0.0.1`, and digest-pinned binary build flow.
  **Lead: Kramer.** *FR-018, ADR-007 §1.*
- **S03E15 -- *The Capability Gate*.** Tool calling is not universal
  -- some local models don't support function calls. Adapter probes
  `/v1/models`, caches capability per model, and refuses
  `--agent` cleanly when unsupported. **Lead: Maestro.**
  *FR-014, FR-018 §4.4.*
- **S03E16 -- *The First Hour, Local Edition*.** Lloyd Braun walks
  the onboarding path again -- "I just installed Ollama, can I use
  az-ai right now?" -- and we patch every spot it doesn't
  Just Work. **Lead: Lloyd Braun.** *FR-014, FR-018, S02 onboarding
  arc continuation.*

### Arc 4 -- Provider Switch Ergonomics (E17-E20)

- **S03E17 -- *The Switch*.** `az-ai --provider`, `--profile`,
  `AZ_PROVIDER`, `AZ_PROFILE` env vars. Precedence chain documented,
  tested, and printed by `--config show`. **Lead: Costanza.**
  *FR-014 §3.*
- **S03E18 -- *The Default*.** Smart default selection per ADR-009
  extended to multi-provider: if `AZ_PROVIDER=local` and no model
  flag, pick the smallest model the local runtime advertises. Lloyd
  Braun consult on UX. **Lead: Jerry.** *ADR-009, FR-014.*
- **S03E19 -- *The Fallback*.** Best-effort fallback policy: if
  `azure` returns 429/5xx and a `fallbackProvider` is configured,
  retry once on the fallback. Off by default. Frank Costanza signs
  off on the SLO contract. **Lead: Frank Costanza.**
  *FR-014 §5, FR-018 §5.*
- **S03E20 -- *The Persona, Multi-Provider*.** Squad personas grow
  an optional `provider:` and `model:` field so reviewer can be
  Azure GPT-4o while coder is local Gemma. **Lead: Elaine.**
  *FR-014, squad system docs.*

### Arc 5 -- Hardening (E21-E23)

- **S03E21 -- *The CVE Log, Per Provider*.** Trivy/CVE tracking grows
  a per-provider column. Newman's monthly sweep covers Ollama image
  digests and llama.cpp release tarballs. **Lead: Newman.**
  *ADR-007 §1.*
- **S03E22 -- *The Rotation*.** BYOK rotation flow -- `az-ai creds
  rotate --provider openai` re-prompts and overwrites in keystore
  without leaking the old value. **Lead: Newman.** *ADR-007 §2.*
- **S03E23 -- *The Offline Mode*.** `--offline` flag forbids any
  non-loopback HTTP. Useful in air-gapped labs and as a CI guardrail.
  **Lead: Jerry.** *FR-018 motivation §1.*

### Finale (E24)

- **S03E24 -- *The Demo*.** End-to-end: same binary, three providers,
  one Espanso trigger config -- `:aifix` on local Ollama, `:aitldr`
  on OpenAI direct, `:aic` on Azure GPT-4o. Peterman writes the
  launch copy; Keith Hernandez records the demo. **Lead:
  Costanza** with **J. Peterman** + **Keith Hernandez** in the
  writers' room. *FR-005, FR-014, FR-018.*

### Lead-cast tally

| Cast | Episodes led |
|---|---|
| Costanza | E02, E05, E17, E24 (4) |
| Kramer | E01, E06, E11, E14 (4) |
| Newman | E04, E07, E13, E21, E22 (5) |
| Jerry | E08, E12, E18, E23 (4) |
| Elaine | E03, E20 (2) |
| Supporting (one each) | Morty (E09), Puddy (E10), Maestro (E15), Lloyd (E16), Frank (E19), Peterman+Keith (co-E24) |

Newman ends up with five because per-provider security review is
the load-bearing wall of this season. Elaine gets two with heavy
consult work everywhere. Both are deliberate.

## Cross-references to FR-NNN proposals

| FR | Title | Episodes that touch it |
|---|---|---|
| **FR-003** | Local user preferences | E03 (absorbed into FR-014) |
| **FR-005** | Shell integration & output intelligence | E24 (Espanso multi-trigger demo) |
| **FR-009** | `--config set` + directory overrides | E03 (absorbed) |
| **FR-010** | Model aliases & smart defaults | E18 |
| **FR-014** | Local prefs + multi-provider profiles | E01-E20 (the spine of S03) |
| **FR-015** | Pattern library + cost estimator | E09 (rate-card stub only; full estimator is S04) |
| **FR-018** | llama.cpp / Ollama adapter | E11, E12, E13, E14, E15, E16, E23 |
| **FR-019** | gemma.cpp direct adapter | *Deferred to S04* -- direct adapter, not OpenAI-compat, so it doesn't fit the S03 seam cheaply |
| **FR-020** | NVIDIA NIM + per-trigger routing | *Partially deferred* -- the NIM adapter (OpenAI-compat) can land in E11's slot if greenlit; **per-trigger routing is S04 by definition** |
| **FR-022 / FR-023** | Native / first-run wizard | E08 |
| **ADR-007** | Third-party HTTP provider security | E04, E07, E13, E14, E21, E22 |
| **ADR-009** | Default model resolution | E02, E18 |

## Risks and known unknowns

1. **Anthropic / Gemini / Bedrock SDK maturity for AOT.** Native .NET
   SDKs exist (per 2026 research) but their reflection footprint
   under Native AOT is unverified. If we promote any of them from
   "stub" to "shipped" inside S03, we risk a 2-3 MiB binary growth
   and a fresh round of trim warnings. Mitigation: keep the generic
   OpenAI-compat adapter as the *only* shipped non-Azure path in
   S03; defer SDK-based providers to S04.
2. **Single-binary AOT pressure with multiple SDKs.** Even keeping
   Azure + OpenAI-compat HTTP, we need to verify the binary stays
   under ~15 MiB and TTFT stays under 50 ms. Bania owns the gate.
3. **Credential-store schema change.** Keys move from
   `az-ai/api_key` to `az-ai/<provider>/api_key`. Migration is
   one-shot on first v3 launch but it must be idempotent and must
   leave the old key readable for one rollback window.
4. **Telemetry / cost-shape skew.** `--verbose` cost output and any
   future receipt format must include a `provider` field from day
   one. If we forget, we replay the gpt-4o-mini / gpt-5.4-nano
   ambiguity from ADR-009 -- *the doc lies* -- but per-provider.
5. **Default-model resolution across providers.** ADR-009 was
   written for one provider. With three, "the default model" depends
   on the active profile. Either ADR-009 gets a §amendment in E02
   or we ship ADR-010 in E18.
6. **Test matrix explosion.** {Azure, OpenAI, Ollama, llama.cpp} x
   {single-shot, agent, streaming, tools} = 16 cells. CI minutes
   are not free; Puddy in E10 must propose a tiered matrix
   (smoke / nightly / pre-release).
7. **Local-runtime install drift.** Ollama and llama.cpp ship weekly.
   A capability that worked at E11 may regress at E16. Mitigation:
   pin a tested version in docs and CI (digest where possible per
   ADR-007 §1).
8. **User confusion: "is this still an Azure tool?"** The repo name,
   the README banner, the binary name (`az-ai`) -- all Azure-coded.
   Sue Ellen + Peterman own the messaging in E24 so we don't
   accidentally repositional ourselves out of the Azure-trust seam
   that S02E19 said was our moat.

## What S03 does NOT cover (boundary)

- **NOT** intelligent / cost-aware / quality-aware routing -- that
  is **S04 ("Model Intelligence")**. S03 ships a routing *table* in
  the schema (per FR-020) but no automatic decisions.
- **NOT** MCP client or server, plugin registry, or any extension
  protocol -- that is **S05 ("Protocols & Plugins")**.
- **NOT** enterprise / compliance / SSO / audit-log shipping -- a
  candidate for **S06+**.
- **NOT** multimodal (vision in / image out / audio) -- separate
  season candidate; the capability flag exists in the schema but
  no code path consumes it in S03.
- **NOT** prompt cache, response cache, pattern library full
  implementation -- FR-008 / FR-015 stay open into S04.
- **NOT** SDK-based Anthropic / Gemini / Bedrock adapters -- S03
  ships only the generic OpenAI-compat HTTP path. Native SDK
  adapters are an S04 candidate gated on AOT verification.

## Open questions for showrunner greenlight

1. **First non-Azure cloud: Anthropic or OpenAI direct?** This
   draft recommends **OpenAI direct** (zero bridge code, strongest
   developer-recognition value, drops in via the OpenAI-compat
   adapter). Anthropic is the better *competitive* answer (E19
   said so) but costs us a wire-format bridge. Larry calls.
2. **Do we promote NIM (FR-020) from S04 into S03?** The NIM
   adapter is OpenAI-compat and could land in E11's slot instead
   of (or alongside) Ollama. That would let Bania start the
   per-trigger latency benchmarks one season early -- but it also
   pulls hardware-specific concerns into a season we want to keep
   provider-agnostic.
3. **Default provider on a fresh install -- still `azure`?**
   The draft assumes yes (FR-014 §2 says yes, S02E19 says yes).
   But if S03's hero demo is multi-provider, the wizard might want
   to ask earlier and louder. Lloyd Braun + Russell Dalrymple have
   opinions.
4. **Binary name.** `az-ai` is Azure-coded. Do we rename to `ai`
   or `omni-ai` for v3, ship a transitional symlink, and accept
   one season of breakage? Or do we stay `az-ai` forever and let
   the README do the work? Peterman has a strong "do not rename"
   position on file; Sue Ellen has a softer "rename eventually"
   position. Costanza is undecided.
5. **CI cost ceiling.** Adding a local-runtime test job (Ollama
   in a container, model pull cached) roughly doubles CI minutes.
   Are we okay with that, or do we tier it to nightly-only? Frank +
   Morty co-own the answer.

---

*Pitch closed. Costanza out. Kramer's last word from the back of
the room: "The seam is the season. Get the seam right and the
adapters write themselves."*

---

## Adjacent blueprints

- Previous season (in production): [`s02-writers-room.md`](s02-writers-room.md).
- Next blueprint: [`s04-blueprint.md`](s04-blueprint.md) -- *Model Intelligence*.
- Long-horizon slate: [`seasons-roadmap.md`](seasons-roadmap.md).
