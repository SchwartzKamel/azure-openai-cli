# S03E08 -- *The Pick*

> *Costanza walks in with one slide. Sue Ellen makes the Anthropic case. The decision goes to OpenAI direct anyway. ADR-010 lands.*

**Commit:** `pending` (ships at end of episode)
**Branch:** `main` (direct push)
**Runtime:** ~50 min real time
**Director:** Larry David (showrunner) -- drafted by Costanza in the showrunner's voice; final cut signed off by Larry
**Cast:** Costanza (lead, ADR author), Sue Ellen Mischke (cameo, competitive case for Anthropic), Kramer (half-line, accepts E09 handoff), Newman (half-line, accepts E10 handoff), Jerry (one line, accepts E11 handoff), Morty Seinfeld (one line, accepts E12 handoff), Puddy (half-line, accepts E13 handoff), Larry David (sign-off)
**Arc:** Provider Abstraction Seam -- E08 of 13 (Arc 2 opener)
**Related ADRs/FRs:** ADR-010 (this episode's deliverable), ADR-007 (security guardrails inherited), ADR-009 (resolution chain extended), FR-014 (the umbrella), placeholder FR-024 (Anthropic, deferred)

---

## The pitch

Two episodes ago the project had no place to put a non-Azure provider. One episode ago it had a place but nothing to scrub the auth header that would inevitably leak from one. This episode, the place is built and the lock is on, and the only question left is which provider walks through the door first. That is what E08 is. One scene, three lines, one decision, one ADR. The Provider Abstraction Seam arc opens here in earnest.

Costanza wrote the ADR. He came in with one slide and one bullet -- *OpenAI direct, first* -- and he made the case in three reasons before anyone else had finished their coffee. The room argued for ten minutes. Sue Ellen filed the Anthropic case the writers' room knew was coming. Newman did not speak. Morty did the math under the table and nodded. Larry let it run, then signed it. ADR-010 went to disk. The five-episode implementation plan was dispatched as the meeting broke up. The Compat starts tomorrow.

The deeper structural reason this episode matters more than its line count suggests: the decision is not about a single provider. The decision is about the *abstraction shape* of the entire arc. Picking OpenAI commits the project to "OpenAI-compat first" as the seam, which means Arc 3 (local providers) ships as configuration presets against the same adapter. Picking Anthropic instead would have locked the project into a per-provider-adapter pattern that bills the same engineering cost for one-tenth the unlock. The multiplier is the decision. Costanza saw it; the room agreed; Sue Ellen documented the deferral risk for the record. Pretty, pretty, pretty good.

This is a decision episode, not a build episode. No production code shipped. No tests added. The deliverable is `docs/adr/ADR-010-first-non-azure-cloud.md`, this exec report, and the writers'-room update that closes out F-1 / F-2 / C2 / W-01 from the prior arc and opens Arc 2 for business. E09 is where the keys hit the keyboard.

---

## Scene-by-scene

### Cold open -- the seam is real

The schema landed in E06. The redactor landed in E07. Two episodes, two bricks, the wall has a footprint now. The drawer for `providers{}` exists. The lock on the drawer works -- Newman's six patterns, the 500 ms timeout, the P1 contract test. The Provider Abstraction Seam is no longer a slide deck; it is `azureopenai-cli/Preferences.cs` plus `azureopenai-cli/SecretRedactor.cs`, 327 lines on disk, 703 unit tests green, 39 integration tests green. The arc that opened on a metaphor closes its first chapter on a binary that compiles.

The framing question for E08 is the simplest question Arc 2 has and the most consequential: what flows through the seam *first*? Not eventually. Not in S04. First. The blueprint draft made a recommendation -- "OpenAI direct, because it slots straight into the generic OpenAI-compat adapter with zero bridge code" -- and the blueprint was honest enough to mark it as a recommendation rather than a decision. ADR-010 is where the recommendation becomes the decision. The episode is the meeting where that conversion happens.

What flows through the seam next? That is the meeting. Costanza has the floor.

He does not come in with a deck. He comes in with one slide. The slide has one bullet. The bullet says *OpenAI direct, first*. He explains it in three sentences. The first sentence says: the wire protocol is the same protocol Azure already speaks, which means zero bridge code in the adapter. The second sentence says: every local OpenAI-compatible runtime in the ecosystem -- Ollama, llama.cpp, NIM, vLLM -- comes along for free as a profile preset, which means Arc 3 ships as documentation rather than as engineering. The third sentence says: Anthropic is the second provider, on its own schedule, with its own ADR, and we will write the FR placeholder before the meeting ends.

Three sentences. The room exhales. Sue Ellen raises a hand.

### Act I -- The criteria

Costanza walks the criteria for the record. Six of them. Each gets one minute. The room does not dispute them; the room confirms them, because each one was already a working assumption that nobody had bothered to write down.

1. **Wire-protocol compatibility.** Azure OpenAI's chat-completions surface and OpenAI's public API are a near-superset relationship. Same message structure, same tool-call format, same streaming chunk shape, same finish-reason vocabulary, same usage accounting. The deltas (`api-version` query param vs none, `api-key` header vs `Authorization: Bearer`, deployment-name path segment vs model-id field) live in the adapter's request builder and never leak into the rest of the codebase. Anthropic does not share this property -- different request envelope, different streaming events, different tool-call shape. That is the headline.
2. **Streaming and tool-call parity.** The two highest-risk surfaces of any new provider. Both are tested on the OpenAI surface today against Azure. Picking the surface we already know turns S03E13 *The Stream* into a verification episode rather than a bring-up episode. Anthropic's tool_use blocks would land in a separate column on the capability matrix with at least three asterisks.
3. **Auth surface.** OpenAI direct uses a single bearer token. The redactor's pattern #1 already scrubs that shape. No new pattern required. Anthropic's `x-api-key` is also covered by pattern #2; auth-surface coverage is even between the two and not a differentiator.
4. **Cost story.** OpenAI's per-token rates for the comparable SKUs (`gpt-4o-mini`, `gpt-4o`) are within 5-15% of Azure's published rates for the same models. No surprise cost regression for current users on the fallback model. Anthropic's pricing is not directly comparable -- different models, different tiers -- which makes the S03E12 receipt a paragraph rather than a number. Morty signs off on the directional argument; the full receipt episode owns the precise rate-card numbers.
5. **Local-provider compatibility -- the multiplier.** The reason the decision is not close. Picking OpenAI's wire protocol unlocks Ollama, llama.cpp, NIM, vLLM, and the long tail of OpenAI-compat aggregators (Cloudflare Workers AI, Together, Groq, Fireworks, Perplexity API) as configuration presets against the same adapter. One adapter, eight to twelve providers. Anthropic does not have this multiplier. Picking Anthropic first commits the project to one provider for the same adapter cost.
6. **Maintenance burden.** Every adapter the project ships is a maintenance surface forever. Streaming drift, tool-call drift, error-envelope drift, retry-after semantics, model-list endpoint drift -- regression-tested against the live upstream forever. The cheapest adapter is the one that serves the most providers. OpenAI-compat is that adapter. Wilhelm would phrase this as a process risk -- the project does not have the bench depth to maintain two adapter codepaths in parallel through S04 and S05 without one rotting -- and Wilhelm would be right.

The room walks the six. Nobody pushes back on any of them in isolation. The push-back is on the conclusion, and the push-back has a name.

### Act II -- The alternatives

Sue Ellen Mischke takes the floor. She is the project's competitive analyst, she runs the briefing track that surfaces what users are asking for and what competitors are shipping, and she is here to make the Anthropic case for the record.

The case is straightforward. Claude is what users are asking for *by name*, *right now*, in issue trackers, in DevRel conversations, in conference Q&A. The brand recognition rivals OpenAI's in the developer segment. The model itself is widely regarded as best-in-class for code-adjacent tasks -- large-context reasoning, careful refusals, instruction following. If the decision were "which provider do users want most?", Anthropic would win. She is not arguing that the decision *should* be Anthropic. She is arguing that the deferral has a cost and the cost should be on the record.

Costanza does not budge. The decision is not "which provider do users want most." The decision is "which provider unlocks the most arc work for the least adapter cost." Anthropic loses that decision on three independent axes -- bespoke adapter cost, no local-provider multiplier, and worse sequencing (Anthropic done second is cheap because the seam is already in; Anthropic done first would force every other piece of the seam to be either Anthropic-shaped or generic-from-day-one, and the latter is over-engineering on speculation). The decision goes to OpenAI direct.

The deferral is documented honestly. A follow-up FR is reserved -- placeholder **FR-024 -- Anthropic Claude Adapter** -- to be authored by Costanza after S03E13 freezes the v3.0 capability matrix. The FR will scope the bespoke adapter, the streaming-protocol adapter, the tool_use translation layer, and the capability-matrix asterisks. Sue Ellen owns the competitive update that lands alongside it. She accepts the deferral on the record. *Users will ask for Claude. They are asking. Right now. Document that.* It is documented.

The other three alternatives get briefer hearings.

**AWS Bedrock** is structurally interesting -- one adapter, many model families behind a single API surface -- but Bedrock adds AWS-specific dependency surface (SigV4 request signing, STS token refresh, region-scoped endpoints, IAM-role auth) and an SDK that is heavyweight in AOT terms. Per ADR-001, AOT-friendliness is not negotiable. Bedrock is also second-tier on brand recognition relative to OpenAI direct or Anthropic direct. Deferred indefinitely; revisit if a multi-cloud enterprise FR materialises with a sponsor and a budget.

**Google Vertex AI / Gemini** carries an OAuth flow plus GCP-project scoping plus per-region endpoint conventions plus service-account JSON-key auth. The Gemini API surface itself is clean; the *enrolment* surface is the issue. Deferred to after Arc 4 ships and the wizard is mature enough to handle multi-step OAuth onboarding without making the first-run experience worse.

**Cloudflare Workers AI / Together / Groq / Fireworks / Perplexity** all expose OpenAI-compatible `/v1/chat/completions` endpoints. They are not separate adapters under this decision -- they are *profile presets* against the same `OpenAiCompatAdapter` that S03E09 ships. Picking OpenAI direct is what makes these come along for free.

The OpenAI-compat-first commitment is named explicitly. ADR-010 commits the project to that abstraction shape. Future non-OpenAI-compat providers (Anthropic, Bedrock, Vertex) require their own adapter, not an extension of this one. Costanza wants this on the record so a future contributor does not assume the adapter is generic; it is not. It is OpenAI-compat. By design. With consequences.

### Act III -- The consequences and the dispatch

The decision is logged. ADR-010 is written into existence, sections in place, sign-offs collected. The implementation plan is the rest of Arc 2. Five episodes, dispatched in narrative order in the back half of the meeting before anyone has stood up.

| Wave | Agents (parallel) | Outcome |
|------|-------------------|---------|
| **1** | costanza | ADR-010 drafted to disk; alternatives section absorbs Sue Ellen's competitive case verbatim; FR-024 placeholder reserved. |
| **2** | sue-ellen | Anthropic deferral risk documented in writers'-room open-questions; competitive update owner named for the FR-024 landing window. |
| **3** | costanza, larry-david | Episode dispatched: Compat -> Keychain -> Wizard -> Receipt -> Stream, leads named in-meeting, sequencing rule recorded. |
| **4** | larry-david | Sign-off. Episode draft accepted. ADR-010 status flipped from Proposed to Accepted in the same commit. |

The handoffs themselves are one-liners, in the order the work will land:

- **S03E09 -- *The Compat*.** Implement `OpenAiCompatAdapter` -- HTTP client, bearer auth, base-URL resolution, optional `OpenAI-Organization` header, request/response shape against `chat.completions`. Wire behind `--provider openai`. **Lead: Kramer.** Kramer accepts with a half-line and a hand-flap. *"Giddyup."*
- **S03E10 -- *The Keychain*.** Per-provider credential namespace -- `az-ai/openai/api_key` distinct from `az-ai/azure/api_key`, extending the S02E04 locksmith. **Lead: Newman.** Newman accepts with a hostile nod. He does not speak. The nod is the acceptance.
- **S03E11 -- *The Wizard, Reprise*.** First-run learns to ask "which provider?" and writes a `default` profile pointing at the chosen one; empty input falls back to `azure` to preserve current behaviour. **Lead: Jerry.** Jerry accepts. *"On it after the Compat lands -- I want a real adapter to wizard against."*
- **S03E12 -- *The Receipt*.** Per-provider rate-card stubs in the cost path -- enough to render `Azure: $0.0021 / OpenAI: $0.0018` in `--verbose`, no smart picking yet. **Lead: Morty Seinfeld.** Morty accepts. *"I'll watch the budget."*
- **S03E13 -- *The Stream*.** Verify streaming and tool-call parity on the new adapter; freeze the v3.0 capability matrix. **Lead: Puddy.** Puddy accepts. *"Either it streams or it doesn't."*

Sequencing rule recorded in ADR-010 §Implementation: E09 must land before E10 / E11 / E12; E10 / E11 may run in parallel; E12 depends on E10 (rate cards keyed by provider); E13 depends on E09 plus at least one of E10 / E11. The arc completes when E13 freezes the matrix.

Larry signs off. Costanza files the FR-024 placeholder (one paragraph, scope statement only -- the full FR lands after E13). The writers'-room file is updated to reflect the close-out of F-1 / F-2 (resolved by Newman's v2.1.1 re-audit), C2 (resolved in commit 215b2d3), and W-01 (resolved in commit de478d2), with the active-findings list trimmed accordingly. Arc 1 closes. Arc 1.5 closes. Arc 2 opens.

The dispatch table at the top of Act III is intentionally short -- four waves, all serial, all single-agent except for the Wave 3 dispatch that pairs Costanza's planning text with Larry's sign-off pen. There was no parallelism to schedule because the episode's only deliverable was a written decision, and writing decisions in parallel produces drift between the ADR and the exec report. The fleet-dispatch skill's rule -- "never solo background dispatch" -- is satisfied by the Wave 4 Larry-pass, which is the human-in-the-loop check on the lead's authored output. Future build episodes (E09 onward) will have wave tables that look more like S03E07's; this one is a decision episode and the wave table reflects that.

The meeting breaks. Kramer is already at the keyboard.

---

## The decision

**OpenAI direct is the first non-Azure cloud provider.** It is implemented via the generic `OpenAiCompatAdapter` shipped in S03E09 *The Compat*. The adapter is built once and serves every OpenAI-compatible endpoint -- OpenAI's own API, Cloudflare Workers AI, Together, Groq, and (via Arc 3) Ollama, llama.cpp, NIM, and vLLM -- as configuration presets, not as separate adapters.

Conclusion verbatim from ADR-010 §Decision; no drift between this report and the ADR is permitted. If they ever disagree, the ADR wins.

---

## Alternatives considered

Terse summary; full disposition in [ADR-010 §Alternatives considered](../adr/ADR-010-first-non-azure-cloud.md):

- **Anthropic Claude.** The serious challenger; bespoke adapter, no multiplier, no local-runtime unlock. Deferred to S03 Arc 4 / S04 Arc 1 via placeholder FR-024.
- **AWS Bedrock.** Multi-provider gateway, attractive in the abstract; AWS auth (SigV4, STS) is heavyweight in AOT terms. Deferred indefinitely.
- **Google Vertex AI / Gemini.** OAuth + GCP-project scoping = first-run UX heavy lift. Deferred until the wizard matures.
- **Cloudflare Workers AI / Together / Groq / Fireworks / Perplexity.** All OpenAI-compat; ride the same adapter as profile presets. Not separate decisions.
- **Status quo (Azure-only forever).** Not a serious alternative; would invalidate Arc 2, Arc 3, Arc 4, and FR-014.

---

## Implementation plan

The five-episode roadmap, repeated here for the reader who lands on this exec report from the writers' room and does not click through to ADR-010:

| # | Title | Lead | Acceptance |
|---|-------|------|------------|
| S03E09 | *The Compat* | Kramer | Passing integration test against a recorded OpenAI fixture; dry-run path for CI. |
| S03E10 | *The Keychain* | Newman | Keychain round-trip tests on each platform shim; ADR-007 §2 compliance preserved per provider. |
| S03E11 | *The Wizard, Reprise* | Jerry | First-run smoke against a clean home directory writes a valid `preferences.json` for either choice. |
| S03E12 | *The Receipt* | Morty Seinfeld | Cost line renders both rates when active profile is OpenAI; renders only Azure when not. |
| S03E13 | *The Stream* | Puddy | Parity test suite green against both Azure and OpenAI for streaming chunk shape, tool-call round trip, finish-reason vocabulary, usage accounting. |

Sequencing: E09 first; E10 + E11 parallel; E12 after E10; E13 after E09 + (E10 or E11). Arc completes at E13.

---

## What shipped

- **Production code.** None. This is a decision episode.
- **Tests.** None. This is a decision episode.
- **Docs.**
  - `docs/adr/ADR-010-first-non-azure-cloud.md` -- new, ~210 lines, the decision record.
  - `docs/exec-reports/s03e08-the-pick.md` -- this file.
  - `docs/exec-reports/s03-writers-room.md` -- updated: E06 / E07 / E08 rows added to the episodes-shipped table; F-1 / F-2 / C2 / W-01 transitioned to resolved with commit references; Arc 1 / Arc 1.5 closed; Arc 2 opened; Anthropic deferral risk and the JSON-quote follow-up captured as open questions.
- **Not shipped (intentional follow-ups).**
  - **`OpenAiCompatAdapter`** -- belongs to S03E09. Lead Kramer.
  - **Per-provider keychain namespacing** -- belongs to S03E10. Lead Newman.
  - **Wizard provider selection** -- belongs to S03E11. Lead Jerry.
  - **Per-provider rate-card stubs** -- belongs to S03E12. Lead Morty.
  - **Streaming + tool-call parity tests** -- belongs to S03E13. Lead Puddy.
  - **FR-024 -- Anthropic Claude Adapter** -- placeholder reserved in this episode; full FR drafted after E13 by Costanza, with a competitive update from Sue Ellen alongside.

---

## Findings rollup

None. This is a decision episode. No audit ran; no defects surfaced; no patches landed. The findings work this episode does is *administrative* -- F-1 / F-2 / C2 / W-01 are all resolved by prior commits and the writers'-room file is being updated to reflect that. No new finding IDs are opened.

---

## Lessons from this episode

1. **Decision episodes are episodes.** They take a slot, they need an exec report, they need an ADR, and they end with a dispatch. The temptation to fold the decision into the next build episode (E09 *The Compat*) was real and was rejected in favour of a clean record. Build episodes that smuggle decisions confuse the reviewer for the next contributor; decision episodes that smuggle builds underspecify the work. Keep them separate.
2. **The multiplier is the decision.** Sue Ellen's competitive case for Anthropic was correct on its own terms -- users do want Claude, by name, right now -- and the case was still not the decision. The decision was the multiplier: pick the wire protocol that turns one adapter into eight providers, not the wire protocol that turns one adapter into one provider. When two arguments compete and one of them has a multiplier, the multiplier wins on shape, not on volume.
3. **Document the deferral, name the owner, schedule the follow-up.** The Anthropic deferral has a real cost and the cost will arrive in the form of user complaints. Naming the placeholder FR (FR-024), the author (Costanza), the comms owner (Sue Ellen), and the trigger (post-E13) is what turns "we will get to it" into "here is the row in the writers' room." Without the row, the deferral becomes a forgotten promise. With the row, it becomes a calendared one.
4. **The abstraction shape is more consequential than the first instance.** Picking OpenAI as the first non-Azure provider is one decision. Committing to "OpenAI-compat first" as the abstraction shape is the *bigger* decision and the one that constrains Arc 3, Arc 4, and S04. The ADR documents both, in that order, so the lock-in is visible to a future contributor who only reads the §Decision section.
5. **A decision episode produces durable artifacts even with zero code.** ADR-010, this exec report, the writers'-room update, the FR-024 placeholder, and the dispatched implementation plan are all artifacts that survive past the meeting. Code is not the only kind of work that ships. Process artifacts that close out a decision are work, and they are billable to a slot.
6. **The arc opener sets the tone.** E08 is the first episode of Arc 2. The tone it sets -- one slide, three reasons, ten minutes of debate, a documented deferral, a five-episode dispatch -- is the tone Arc 2 will be reviewed against. If E13 lands and the matrix freezes cleanly, E08 will be remembered as the meeting that made it cheap. If E13 lands and the matrix has surprises, E08 will be the autopsy's first chapter.

---

## Metrics

- Diff size: +0 production code / +0 tests / 3 docs files (ADR-010 new, this exec report new, writers'-room updated).
- Test delta: n/a (decision episode).
- Preflight: docs-only -- per `docs-only-commit` skill, full preflight skipped; ASCII-validation grep run against all three files and clean.
- CI status at push time: docs-only push, expected green; will be confirmed on landing.

---

## Cross-references

- [`docs/adr/ADR-010-first-non-azure-cloud.md`](../adr/ADR-010-first-non-azure-cloud.md) -- the decision record this episode produced
- [`docs/adr/ADR-007-third-party-http-provider-security.md`](../adr/ADR-007-third-party-http-provider-security.md) -- the six guardrails every non-Azure provider inherits
- [`docs/adr/ADR-009-default-model-resolution.md`](../adr/ADR-009-default-model-resolution.md) -- the resolution chain S03E06 generalised to four layers
- [`docs/proposals/FR-014-local-preferences-and-multi-provider.md`](../proposals/FR-014-local-preferences-and-multi-provider.md) -- the umbrella FR
- [`docs/exec-reports/s03-blueprint.md`](s03-blueprint.md) §"Arc 2 -- First Non-Azure Cloud (E08-E13)"
- [`docs/exec-reports/s03e06-the-schema.md`](s03e06-the-schema.md) -- preferences.json v1 (the drawer)
- [`docs/exec-reports/s03e07-the-redactor.md`](s03e07-the-redactor.md) -- centralised secret scrubber (the lock)
- [`docs/exec-reports/s03-writers-room.md`](s03-writers-room.md) -- updated this push with E06 / E07 / E08 rows
- **FR-024 (placeholder)** -- Anthropic Claude Adapter, drafted post-E13 by Costanza, comms owned by Sue Ellen

---

## Next episode preview -- S03E09 *The Compat*

Kramer's lead. The first time az-ai talks to a non-Microsoft endpoint. `azureopenai-cli/Providers/OpenAiCompatAdapter.cs` lands as a single file -- HTTP client, bearer auth, base-URL resolution, the optional `OpenAI-Organization` header, the request and response shapes against `chat.completions`. Wired behind `--provider openai` and `preferences.providers["openai"]`. Acceptance is a passing integration test against a recorded OpenAI fixture and a dry-run path that lets CI exercise the adapter without a live key.

The expected drama is small. The wire protocol is the protocol az-ai already speaks against Azure. The redactor's six patterns already scrub the bearer. The schema already has a place for the provider entry. If anything goes sideways in E09, it will be a streaming-chunk shape mismatch on a content-part variant the existing Azure tests do not exercise -- which is the whole point of E13's parity work and not a problem E09 has to solve. E09 lands the adapter. E13 freezes the matrix. Arc 2 closes on time.

Tag scene preview: Kramer at the keyboard, second cup of coffee, hawaiian shirt, single-file diff in vim, no ceremony. Giddyup.

---

## Credits

- **Costanza** -- lead. Wrote the slide. Wrote the ADR. Argued the multiplier in three sentences and held the line through ten minutes of Anthropic push-back. Filed the FR-024 placeholder before the meeting broke. Took credit reflexively and handed it back when caught -- usually caught. Owner of the post-release "was it worth it?" note that lands after S03E13 freezes the matrix.
- **Sue Ellen Mischke** -- cameo. Made the Anthropic case for the record, accepted the deferral on the record, and named the comms ownership for the FR-024 landing window. Did not lobby. Did not relitigate. Filed the deferral risk to the writers'-room open-questions list and walked out. Exactly the competitive-analyst posture the project wants -- present, prepared, durable on record.
- **Kramer** -- half-line. Accepted the S03E09 *The Compat* handoff with a hand-flap and a "giddyup." Already at the keyboard before the meeting broke.
- **Newman** -- half-line. Hostile nod accepting the S03E10 *The Keychain* handoff. Did not speak. The nod is the acceptance and the writers' room has learned to read the nod.
- **Jerry** -- one line accepting the S03E11 *The Wizard, Reprise* handoff. Wants a real adapter to wizard against; sequencing on Kramer's E09 work is acknowledged.
- **Morty Seinfeld** -- one line accepting the S03E12 *The Receipt* handoff. *"I'll watch the budget."* Owns the rate-card stub schema and the cost-comparability paragraph in the ADR rationale.
- **Puddy** -- half-line accepting the S03E13 *The Stream* handoff. *"Either it streams or it doesn't."* Owns the v3.0 capability matrix freeze.
- **Larry David** -- showrunner sign-off. Triaged the Anthropic deferral, accepted the dispatch, signed the ADR from Proposed to Accepted in the same commit. Did not write production code. Did not write tests. Did sign this exec report and the ADR.

All commits in this push carry the `Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>` trailer per the commit skill. Conventional Commits format. `git -c commit.gpgsign=false` because sub-agents cannot sign.

---

## Appendix A -- Cast notes

Posture notes for the writers'-room cast-balance audit at E12:

- **Costanza (lead).** First S03 lead, closing the quota gap flagged in the E06-era writers'-room file. Posture: defensive, certain, mildly impatient with the obvious answer. The slide-with-one-bullet move is canonical Costanza and lands in this episode the way it should -- as a working method, not a rhetorical flourish. Future Costanza episodes are PM-shaped (FR drafting, latency-budget review, preference-schema proposals) rather than lead-shaped; E08 is the one where he leads a decision episode end-to-end.
- **Sue Ellen Mischke (cameo).** First S03 appearance. Posture: prepared, on-record, durable. The Anthropic case was real and the deferral was honest; the cast-balance note is that the project has a competitive analyst on the bench and will use her as the multi-provider arc generates competitive surface. Expect her in the FR-024 wave and again whenever a non-Azure cloud lands.
- **Kramer / Newman / Jerry / Morty / Puddy (one-liners).** Implementation-handoff cameos. Each is on-deck for the lead role in their named episode. The pattern -- one-line acceptance in the dispatch episode, lead in the build episode -- is the Arc 2 cadence and is intentional. Do not interpret the one-liners as under-casting; they are the pre-roll for the leads to come.
- **Larry David (showrunner).** Sign-off only. Did not direct on-screen, did not orchestrate sub-agents in this episode, did not own a code surface. Correct posture for a decision episode that another lead drafted. The hand-off pattern -- Costanza drafts in Larry's voice, Larry signs off after review -- is the canonical "decision-episode" workflow and will be repeated whenever a non-showrunner lead authors the ADR.

---

## Appendix B -- The slide

For the historical record, the slide Costanza walked in with:

```text
First non-Azure cloud: OpenAI direct.

Why:
1. Zero bridge code -- wire protocol matches Azure.
2. Multiplier -- unlocks Ollama, llama.cpp, NIM, vLLM as presets.
3. Anthropic = second, on its own ADR, post-E13.
```

That is the slide. That is the meeting. That is the episode.

---

## Appendix C -- What did not happen this episode

Decision episodes are easy to over-credit; for the record, what E08 did *not* deliver:

- No `OpenAiCompatAdapter.cs` was written. The adapter is S03E09's deliverable.
- No keychain shim was extended. Per-provider namespacing is S03E10's deliverable.
- No wizard prompt was added. Provider selection is S03E11's deliverable.
- No rate-card row was inserted. Per-provider cost lines are S03E12's deliverable.
- No streaming parity test was authored. Capability matrix freeze is S03E13's deliverable.
- No FR-024 was *written*. A placeholder was *reserved*; the full FR drafts after E13.
- No competitive analysis document was published. Sue Ellen's full Anthropic brief drops alongside FR-024.

The line between "decided" and "implemented" is the line E08 walks. If a reviewer comes back in three weeks asking why none of the above is in main yet, the answer is: by design, on schedule, in their own slots.

---

## Sign-off

Larry David, signing off in the showrunner chair.

This is the kind of episode that does not look like an episode and is. No code shipped. No tests added. No build broke and no build passed because no build ran. What shipped is a decision, written into a record that future contributors will read first when they want to know why the project picked the wire protocol it did. That is a real artifact. It is the only kind of artifact this episode was supposed to produce. It produced it.

Costanza got the slide right. One bullet, three reasons, six criteria, five alternatives, one deferral, five-episode dispatch. The slide was the meeting and the meeting was the episode. Sue Ellen filed the Anthropic case the way she was supposed to file it -- on the record, with a named owner for the comms and a placeholder FR for the work. The deferral is honest. The lock-in is named. The arc is open.

Kramer starts tomorrow. The compat is one file. The keychain is one platform shim per OS. The wizard is one prompt and one fallback. The receipt is one row in a rate-card table. The stream is one parity suite. Five episodes, leads named in this episode, sequencing rule in ADR-010. If it slips, it slips on E13 because parity work is honest work, and we have budgeted for it. If it does not slip, Arc 2 closes inside the season and Arc 3 opens against an adapter that already speaks every local runtime in the ecosystem.

That is the decision. That is the episode. That is enough.

E09 *The Compat* next.

Pretty, pretty, pretty good.
