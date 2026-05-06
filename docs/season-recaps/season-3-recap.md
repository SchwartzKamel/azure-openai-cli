# Season 3 -- *Local & Multi-Provider*

> **Headline.** Season 3 made `az-ai` a provider-agnostic CLI. The same
> 15 MiB single-file binary that opened the season as an Azure-OpenAI-only
> tool now speaks Azure OpenAI, Azure AI Foundry, the OpenAI-compatible
> HTTP family (Ollama, llama.cpp, Groq, Together, Cloudflare, OpenAI
> direct), and gates each one with a shared capability matrix, an opt-in
> fallback chain, and an air-gapped offline mode. Twenty-seven episodes,
> one season, no rewrites.

The Season 2 story was *can people trust us*. The Season 3 story is
*can people use us when "the cloud" is not a sentence they're allowed
to say*. The pivot is not a fork and not a re-architecture -- it's a
seam, sliced cleanly through the binary, with one new abstraction
(`Preferences`), one new HTTP adapter (`OpenAiCompatAdapter`), and
roughly a dozen new flags arranged around them. By the finale, the
seam is load-bearing for at least four standing FRs that have been
blocked for months on the absence of exactly this work.

## Five arcs

### Arc 1 -- The Setup (E10 / E11 / E15 / E25 / E26)

Provider-agnostic only matters if the *first hour* on a new host is
bearable. Arc 1 closes the credentials-and-configuration story for
multiple providers at once. **The Keychain** (E10) splits the
`~/.config/az-ai/env` schema into per-provider sections and extends
the secret-redactor's pattern set. **The Wizard, Reprise** (E11)
makes the interactive setup flow provider-aware -- the wizard knows
the difference between an Azure deployment, a Foundry endpoint, an
OpenAI key, and a local Ollama server, and writes the right env-file
section for each. **The Probe** (E15) introduces `az-ai --doctor`, a
zero-cred provider matrix that tells you which providers your shell
is actually configured for and which would fail. **The Rotation**
(E25) wires `--rotate-creds [provider]` -- BYOK rotation that's
atomic, mode-0600 verified, and never logs the rotated key. **The
Offline Mode** (E26) is the air-gap latch: `--offline` (and
`AZ_AI_OFFLINE=1`) refuses every non-loopback dispatch, layered on
top of the local-providers gate. Newman led three of the five.

### Arc 2 -- The Switch (E08 / E09 / E14 / E17 / E18 / E20 / E21)

This is where the seam itself gets cut. **The Pick** (E08, ADR-010)
chooses OpenAI direct as the first non-Azure cloud; Anthropic is
deferred to FR-024. **The Compat** (E09) ships `OpenAiCompatAdapter`,
the thin `HttpClient`-backed shim that lets every OpenAI-compatible
endpoint share a single dispatch path. **The Stream** (E17) is a
verification beat -- 15 streaming and tool-call parity facts pinned
against the new adapter, with no production code change.
**The Capability Gate** (E18) builds the per-provider, per-model
feature matrix and refuses tool-call / vision / JSON-mode requests
to incompatible models with a friendly error and a stable
`error_class=CapabilityMismatch`. **The Switch** (E20) codifies the
precedence chain (`cli > env > preferences.json > built-in default`)
in a single resolver and gives each switch a *source* label that
shows up in `--config show`. **The Server** (E21) lands the
`llamacpp` preset -- the second local runtime alongside the existing
Ollama path. **The Default** (E22) makes the default-provider
heuristic deterministic, six-rung, and ADR-recorded -- no more
ad-hoc preset-table walks, every resolution returns a stable
`default:azure` / `default:<preset>` / `default:azure:fallback`
label. Costanza led four of the seven.

### Arc 3 -- The Rules (E04 / E07 / E16 / E18 / E22)

The seam exists; now it has to refuse bad inputs. **The Mailman
Knocks Twice** (E04) and **The Redactor** (E07) close two security
findings against the prompts and the secret-redaction pipeline.
**The Allowlist** (E16) adds the SSRF endpoint allowlist seam --
57 adversarial cases, three forward-hardening findings filed, and
`AZ_AI_LOCAL_PROVIDERS=1` becomes the explicit opt-in for any
loopback dispatch. **The Capability Gate** (E18) refuses
incompatible model/feature pairings *before* dispatch, with a
documented `AZ_AI_CAPABILITY_OVERRIDES` escape hatch. **The
Default**'s validation logic (E22) is the last rule in this arc:
`--fallback bogus` exits 2 with the *list of valid presets*. The
finale demo asserts every one of those rules in Act III.

### Arc 4 -- The Fallback (E13 / E22 / E23)

Off-by-default resilience. **The Telemetry** (E13, Frank Costanza)
ships the opt-in `AZ_AI_TELEMETRY=1` NDJSON emitter and the initial
SLO charter -- this is the observability seam every later resilience
episode hooks into. **The Default** (E22) makes default-provider
selection deterministic enough to *talk about* in alerts. **The
Fallback** (E23) is the marquee episode: an opt-in `--fallback`
chain (or `AZ_AI_FALLBACK` env) wrapped around the primary chat
client, max-depth 3, transient-only retries, capability-mismatch
short-circuit, and a load-bearing stream invariant -- once the
first chunk has been yielded to the user, fallback is OFF, mid-stream
provider switches are forbidden, the chain prints
`[fallback] stream-truncated` and re-throws. Two new telemetry
event shapes (`fallback_attempt` / `fallback_outcome`), four new
SLIs (`fallback.rate`, `fallback.recovery_rate`,
`fallback.exhaustion_rate`, `fallback.stream_truncated_rate`), and a
production factory that *deliberately* returns `Skipped` until
per-preset cred discovery lands -- so the wrap is a no-op for users
who don't opt in. Zero overhead. Zero behaviour change. The wire is
ready; the current is throttled until S04.

### Arc 5 -- The Demo (E12 / E13 / E14 / E19 / E24 / E27)

The finale is the curtain call, but the demo arc is older than the
finale. **The Receipt** (E12) gives Bania a bench harness and a
compat-prewarm flow. **The Telemetry** (E13) is reused for opt-in
demos. **The Screen Reader** (E14) makes every CLI surface
`--plain`-friendly with a single output chokepoint, an 18-site
glyph audit, and `NO_COLOR` / `TERM=dumb` / `AZ_AI_PLAIN` honoured
in priority order. **The First Hour, Local Edition** (E19, Lloyd
Braun) writes the onboarding tutorial that Arc 1's plumbing makes
possible. **The CVE Log, Per Provider** (E24, Jerry) wires a
provider-attributed Trivy pipeline and `make cve-report`. **The
Demo** (E27) is the artefact -- a 5-act, mock-only, idempotent bash
script with 22 asserted invariants that exercises every load-bearing
S03 surface end-to-end without touching a single real API key. If
the demo ever stops returning `rc=0` against a fresh
`make publish-aot`, a regression has slipped past every other gate.

## What stayed off the slate

Naming what we *didn't* ship is half the discipline. **Anthropic
Claude** stayed off (deferred to FR-024 / S04 Arc 1 per ADR-010).
**Automatic, cost-aware routing** stayed off (the seam exists; the
intelligence does not). **MCP integration** stayed off. **Plugin
marketplaces, multimodal generation, BYO model packaging,
multi-tenant preference profiles, OS keychain integration** -- all
deferred. The season's job was the seam, not what flows through it.
S04 spends what S03 saved.

## By the numbers

| Metric | Start of S03 | End of S03 (post-finale) | Delta |
|---|---|---|---|
| Episodes shipped | 0 | **27** | +27 |
| Provider presets supported | 1 (azure) | **7** (azure, foundry, openai, groq, together, cloudflare, ollama, llamacpp) | +7 |
| Local runtimes supported | 0 | **2** (ollama, llamacpp) | +2 |
| Unit tests | ~860 (post-S02) | **1019+** | +159+ |
| Integration assertions | ~52 (post-S02) | **73** | +21 |
| New CLI flags | 0 | **8** (`--provider`, `--profile`, `--doctor`, `--rotate-creds`, `--plain`, `--fallback`, `--offline`, `--config show` source labels) | +8 |
| New env-var contracts | 0 | **5** (`AZ_PROVIDER`, `AZ_PROFILE`, `AZ_AI_OFFLINE`, `AZ_AI_FALLBACK`, `AZ_AI_LOCAL_PROVIDERS`) -- plus the existing `AZ_AI_TELEMETRY` and `AZ_AI_CAPABILITY_OVERRIDES` | +5 |
| ADRs landed | n/a | **2** (ADR-010 *The Pick*, ADR-011 *The Default*) | +2 |
| Source files added | n/a | `Preferences.cs`, `OpenAiCompatAdapter.cs`, `Capabilities/ProviderCapabilities.cs`, `Resilience/FallbackPolicy.cs`, `Resilience/FallbackChain.cs`, `Cli/Plain.cs`, `Cli/MaskedInput.cs`, `Squad/`-side per-provider seams | ~8 |
| Audit reports filed | n/a | 6 (RED -> GREEN sweep, plus per-arc audits for keychain, allowlist, capability gate, offline, rotation) | +6 |
| Findings tracked | 0 | 30+ (with mid-season backlog gate wired into `make exec-report-check`) | +30+ |
| AOT binary size | ~13 MiB | ~15 MiB | +2 MiB |
| Trivy clean | yes | **yes** (per-provider attribution added) | maintained |

## Cast distribution

| Lead | Episodes |
|---|---|
| Costanza | 5 (E08 ADR, E15, E18, E20, E22) |
| Kramer | 4 (E01, E06, E09, E17, E21) |
| Newman | 4 (E04, E07, E10, E25, E26) |
| Frank Costanza | 2 (E13, E23) |
| Jerry | 2 (E11, E24) |
| Elaine | 1 (E03) |
| FDR | 1 (E16) |
| Lt. Bookman | 1 (E02) |
| Mr. Wilhelm | 1 (E05) |
| Mickey Abbott | 1 (E14) |
| Kenny Bania | 1 (E12) |
| Lloyd Braun | 1 (E19) |
| Larry David | 1 (E27 finale) |

The five main-cast members all led; ten supporting players got a
lead each; the finale belongs to the showrunner. The casting goal
of "every main-cast lead, multiple supporting leads, no back-to-back
repeats" hit at 26/27 -- the one near-violation (two consecutive
Newman leads on E25 *Rotation* and E26 *Offline*) was deliberate,
because the security-and-rotation story is one continuous beat and
splitting the lead would have produced two half-stories.

## What the binary feels like now

A user who installed `az-ai` at the start of S03 and re-installed at
the end will notice this:

- `az-ai --doctor` works without secrets and tells them what their
  shell is actually configured for.
- `az-ai --setup` walks them through Azure, OpenAI, Foundry, or a
  local Ollama / llama.cpp runtime, writing the right env-file
  section for each.
- `az-ai --provider openai "hello"` (or `--profile dev-local`)
  switches providers per-invocation without re-exporting envs.
- `az-ai --config show` tells them where every effective setting
  came from, with a stable *source* label for each.
- `az-ai --fallback openai,groq` opts them into a best-effort chain
  (when their host has the alternate creds wired -- production
  factory currently `Skipped`).
- `AZ_AI_OFFLINE=1` makes the binary refuse every non-loopback call,
  which is what they want before they record a demo or run a
  conference talk.
- `AZ_AI_TELEMETRY=1` opts them into NDJSON dispatch events on
  stderr -- nothing in the request body, nothing about prompts,
  bounded `error_class`, redacted, ready to grep.
- The `--plain` flag (and `NO_COLOR` / `TERM=dumb`) is honoured
  consistently across every output path.

None of this required a re-install path beyond `make publish-aot`.
None of it broke anything S02 shipped. The binary stayed a single
file, AOT-published, Trivy-clean, fits-on-a-USB-stick.

## Closing line

The season opened with a question -- *can the same binary be a
serious tool on a laptop with no internet and a serious tool against
a regulated cloud, without splitting in two*. The answer is yes, and
the proof is a 5-act bash script that runs in under thirty seconds
and ends in `rc=0`. Roll credits. Curtain. Pretty, pretty, pretty
good.

---

*Drafted by Larry David (showrunner) for J. Peterman to riff on
during the v3.0 launch beat. Numbers compiled from
`docs/exec-reports/s03-writers-room.md` and the per-episode reports
under `docs/exec-reports/s03e*.md`.*
