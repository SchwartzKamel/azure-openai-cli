# S03 -- Progress Report -- 2026-05

> *Mr. Pitt, Executive / Program Manager. Mid-flight check against the
> S03 blueprint. We are not there yet. Headline number: ~21 percent of
> season slate complete or imminent. Provider arc has slipped +3
> episodes against the blueprint numbering. Slate is recoverable
> without a rewrite. Detail follows.*

**Author:** Mr. Pitt (PM)
**Reporting period:** S03E01 through pending S03E03/E04/E05 audit triple
**Source of truth:** [`s03-blueprint.md`](s03-blueprint.md) (Draft v2)
**Posture:** At-risk on velocity, on-track on theme, drifting on lead-cast quotas.

---

## Executive summary

Two episodes shipped (E01, E02). Three more (E03, E04, E05) imminent
as a docs / security / audit-audit sweep that was *not* in the
blueprint. Counting the imminent triple as if it were already in the
can: **5 of 24 episodes -- 20.8 percent of the season**. The
blueprint's six-arc structure is still intact, but the inserted
audit triple displaces the original Provider-Abstraction-Seam content
by +3 numerically; the *Schema* (originally E03), *Redactor*
(originally E04), and *Pick* decision (originally E05) all push to
E06+. No blueprint deadline is published, so feasibility is judged
against scope-velocity (current run rate of one episode per push, no
external date gate). On scope, we are fine. On lead-cast quotas, we
are already drifting -- Kramer and supporting players have absorbed
load that was budgeted for the main five.

---

## Episodes shipped

| # | Title | Lead (declared) | Effective lead | Theme | What shipped |
|---|---|---|---|---|---|
| **S03E01** | *The Yada Yada Strikes Back* | Kramer | Kramer + multi-cast audit | Bug-fix premiere -- Espanso `:aifix` regression, 7 new triggers, lint debt cleanup, env-var race | 13 -> 20 triggers, uniform powershell+heredoc pattern, MD-lint debt cleared, `[Collection("ConsoleCapture")]` serialization, `scripts/lint-espanso-yml.sh` wired into integration tests, post-ship audit by Newman + FDR + Puddy + code-review with hotfixes landed in-episode |
| **S03E02** | *The Library Cop's Word Limit* | Lt. Bookman (new, supporting) | Bookman + Kramer | Brevity discipline -- snap/chat/document/mirror/free tier doctrine | New cast member (Bookman), 22 triggers (added `:aishort`, `:aiyml`), three triggers retuned (`:aiq`, `:aireply`, `:aitldr`), tier doctrine codified in `bookman.agent.md`, 28-agent roster |

Two-line read: E01 was a pilot bugfix that turned into a four-act
audit; E02 was a UX response that hired a new agent and shipped a
brevity contract. Neither episode was on the blueprint slate. The
blueprint opens with *The Adapter* (Kramer, `IProviderAdapter` seam).
We have not yet started arc 1.

---

## Imminent (E03 / E04 / E05) -- the audit triple

Per the active session plan, the next push ships three episodes
back-to-back as a coordinated quality sweep:

| # | Working title | Projected lead | Blueprint slot it displaces |
|---|---|---|---|
| **S03E03** | Docs sweep | Elaine | E03 *The Schema* (preferences.json v1) |
| **S03E04** | Security sweep | Newman | E04 *The Redactor* (centralised secret scrub) |
| **S03E05** | Audit-audit (audit the auditors) | Wilhelm (supporting) | E05 *The Pick* (Anthropic vs OpenAI direct ADR) |

**Net effect on the slate:** +3-episode shift on the Provider
Abstraction Seam arc. *The Schema* -> E06. *The Redactor* -> E07.
*The Pick* -> E08. Arc 1 (E01-E04 in the blueprint) was supposed
to close by episode four; under the new numbering it closes at
episode eight. Arc 2 (First Non-Azure Cloud, originally E05-E10)
slides into E08-E13. Etc.

**Trade we are making:** the audit triple is preventive. E01 needed
a four-wave post-ship audit to close two HIGH security findings
before the push completed. E02 was clean but only because Bookman's
scope was narrow. Shipping a docs / security / audit-audit sweep
now buys us a baseline that arcs 2-6 can lean on. The cost is three
slots of forward-arc velocity. PM read: acceptable trade if we
hold the line at three. A fourth inserted episode and we eat into
finale slack.

---

## Lead-cast quota tracking

Blueprint quota (line 113): **Costanza 3, Kramer 4, Elaine 3,
Jerry 3, Newman 3 = 16 of 24** for the main five; **8 supporting-
player slots**. NB: the blueprint's own *Lead-cast tally* table
(lines 240-245) shows the actual scheduled count as Costanza 4 /
Kramer 4 / Newman 5 / Jerry 4 / Elaine 2 = 19 of 24. The
blueprint contradicts itself. **Action item: Costanza to reconcile
in the next blueprint revision; treating line 113 as policy and
line 240 as draft.**

| Cast (main 5) | Quota (line 113) | Led so far (E01-E02) | Projected after E05 | Status |
|---|---|---|---|---|
| Costanza | 3 | 0 | 0 | **Behind.** First Costanza-led slot was originally E05 (*The Pick*); displaced to E08 |
| Kramer | 4 | 1 (E01) | 1 | On pace at 25 percent of quota with 21 percent of slate gone |
| Elaine | 3 | 0 | 1 (projected E03) | On pace |
| Jerry | 3 | 0 | 0 | **Behind.** First Jerry-led slot was E08 (*The Wizard, Reprise*); now E11 |
| Newman | 3 | 0 | 1 (projected E04) | On pace |
| **Total main 5** | **16** | **1** | **3** | 19 percent of main-cast quota delivered against 21 percent of slate -- inside tolerance |

| Supporting | Used so far (E01-E02) | Projected after E05 | Quota (rough) |
|---|---|---|---|
| Lt. Bookman | 1 (E02 lead) | 1 | n/a -- new cast, hired this season |
| Mr. Wilhelm | 0 | 1 (projected E05) | 1 (process episode) |
| Soup Nazi | inline cameo (E01) | inline cameo | n/a |
| Puddy | inline cameo (E01) | inline cameo | 1 (E10 *The Stream*) |
| FDR | inline cameo (E01) | inline cameo | n/a (red-team is cross-cutting) |
| **Supporting slots used** | **1** | **2** | **8** |

**Reading the table.** Main-cast delivery (3 of 16 projected after
E05) is numerically aligned with slate progress (5 of 24). Supporting
slots burn rate is fine (2 of 8 used at 21 percent of season). The
risk is *distribution within the main five*: Costanza and Jerry have
both been displaced by the audit triple and now have a back-loaded
schedule. Two displaced leads in one quarter of the season is a
yellow flag, not a red one. If the audit triple stretches to four,
Costanza in particular falls behind in a way the finale cannot
absorb (E24 is Costanza-led).

---

## Arc progress

Six arcs in the blueprint. Verified boundaries:

| Arc | Blueprint episodes | Theme | Status | Net displacement |
|---|---|---|---|---|
| **1** Provider Abstraction Seam | E01-E04 | `IProviderAdapter`, `ProviderSelector`, schema, redactor | **Not started.** E01/E02 shipped under different (bugfix + brevity) themes | +3 |
| **2** First Non-Azure Cloud (Pick the Cloud) | E05-E10 | OpenAI-compat adapter, keychain, wizard, rate-card, streaming parity | **Not started.** Pick decision (E05) still pending greenlight | +3 |
| **3** First Local Provider (OpenAI-Compat) | E11-E16 | Ollama, doctor, SSRF allowlist, llama.cpp, capability gate, onboarding | **Not started** | +3 |
| **4** Provider Switch Ergonomics (Multi-Profile UX) | E17-E20 | Switch, default, fallback, persona-per-provider | **Not started** | +3 |
| **5** Hardening | E21-E23 | Per-provider CVE, BYOK rotation, offline mode | **Not started** | +3 |
| **6** Finale | E24 | Three-provider Espanso demo + launch | **Not started** | +3 |

Arc nomenclature reconciliation: the user-facing prompt referred to
arcs as "Provider Abstraction Seam E01-E04 / Pick the Cloud E05-E08
/ OpenAI-Compat Adapter E09-E12 / Local-First E13-E16 / Multi-Profile
UX E17-E20 / Hardening & Polish E21-E24". The **blueprint** uses a
slightly different decomposition: arc 1 (E01-E04), arc 2 (E05-E10),
arc 3 (E11-E16), arc 4 (E17-E20), arc 5 (E21-E23), finale (E24).
Episode count is identical (24). Arc bounds for arcs 2 and 3 differ
because the blueprint wraps OpenAI-compat *and* OpenAI-direct rollout
into one six-episode arc. **Action: Mr. Pitt + Costanza align on
single arc taxonomy in next blueprint pass.** Treating the blueprint
as authoritative for this report.

---

## FR status

The blueprint cites four standing FRs as blocked on the provider
abstraction. All four files exist in `docs/proposals/`. None has a
`Status: Implemented` marker.

| FR | File | Status (per file header) | S03 episodes that touch it | Unblock state |
|---|---|---|---|---|
| **FR-014** | `FR-014-local-preferences-and-multi-provider.md` | Design (Costanza, 2026-04-24) | E01-E20 (the spine of S03) | **Still blocked.** Adapter / selector / schema not started |
| **FR-018** | `FR-018-local-model-provider-llamacpp.md` | Draft (2026-04-22) | E11, E12, E13, E14, E15, E16, E23 | Still blocked. Depends on E11 (Ollama via OpenAI-compat) |
| **FR-019** | `FR-019-gemma-cpp-direct-adapter.md` | Draft (2026-04-21) | **Deferred to S04 per blueprint** | Out of S03 scope; not a slip |
| **FR-020** | `FR-020-nvidia-nim-provider-per-trigger-routing.md` | Draft (2026-04-23) | Adapter portion only; per-trigger routing deferred to S04 | NIM-as-OpenAI-compat could ride along E11 if greenlit; otherwise deferred |

**Aggregate.** Two FRs (014, 018) remain hard-blocked on arc 1; one
is intentionally deferred (019); one is partially deferred with an
optional in-S03 ride-along (020). The blueprint claimed these had
been blocked for two months at draft time -- they remain blocked
two episodes into the season, which is consistent with the slate
not yet having reached arc 1 content.

---

## Velocity / pace

- **Episodes in the can:** 2 (E01, E02).
- **Episodes imminent:** 3 (E03, E04, E05 audit triple).
- **Total projected after the imminent push:** 5 of 24 = **20.8 percent**.
- **Blueprint deadline:** none published. The blueprint cites
  "competitor pressure" and "two months of blocked FRs" as
  motivation but does not commit a calendar date for S03 GA.
- **Run rate:** one episode per push. The cadence between E01 and
  E02 is the only data point; insufficient to project a calendar
  finish.
- **Scope-velocity feasibility:** delivering 19 more episodes
  (E06-E24 in the new numbering) at one-per-push is achievable if
  pushes continue at the recent cadence. The audit triple does not
  reduce slate length; it shifts content right by three slots.
  Slate length remains 24 in either numbering.

PM judgement: feasibility is intact for the *content* of the season.
Calendar feasibility cannot be projected without (a) a target date
or (b) a longer run-rate sample. **Action: Costanza or Larry to
publish a target finale date so this report can give a yes/no on
calendar.**

---

## Risks (top down)

1. **Arc 1 has not started, and arc 1 is the seam the rest of the
   season hangs on.** *The Adapter* (E01 in blueprint, now E06)
   defines `IProviderAdapter`. Until it ships, every other arc is
   speculative. Five episodes in and we still have not introduced
   the abstraction the blueprint titled the season around. **Owner
   to unblock: Kramer (lead) + Costanza (PM signoff).**

2. **The Pick decision (Anthropic vs OpenAI direct) is gated and
   not scheduled.** Originally E05; displaced to E08. Larry has not
   greenlit either option per the blueprint's open-questions
   section (line 326). If the decision slips past E08 we cannot
   start arc 2, which delays arcs 3-5 in turn. **Owner: Larry David
   (showrunner). PM ask: time-box the decision to before the next
   push after E05.**

3. **Audit-triple scope creep.** Three displaced episodes is a
   tolerable trade. A fourth would push Costanza's first lead
   (E08 *The Pick*) to the back half of the season and eat finale
   slack. **Mitigation: hard-cap the audit sweep at three. If a
   fourth issue surfaces, queue it as a post-S03E05 finding rather
   than a new episode.**

4. **Lead-cast distribution drift.** Costanza and Jerry both have
   their first leads displaced. Jerry's first slot (originally E08
   *Wizard Reprise*) now lands at E11. Two main-cast members
   waiting until episode 8+ for their first lead means a back-loaded
   schedule that is fragile to any further slip.

5. **Blueprint internal contradiction.** Line 113 quotes
   3/4/3/3/3 = 16; line 240 tallies 4/4/5/4/2 = 19. Three episodes
   of difference is non-trivial -- it is the difference between
   eight supporting-player slots and five. **Action: reconcile in
   the next blueprint revision before the writers' room casts E06.**

6. **Single-binary AOT pressure (blueprint risk #2).** Not
   exercised yet because no new SDK has been added. Becomes hot
   the moment arc 2 decides between OpenAI-compat HTTP (cheap) and
   a native SDK route (potentially 2-3 MiB binary growth). Bania
   gate is in place per blueprint.

7. **Credential-store schema migration (blueprint risk #3).**
   Not yet relevant. Becomes hot at E07 (originally E07, now E10
   *The Keychain*). Newman's lead.

8. **Local-runtime install drift (blueprint risk #7).** Not yet
   relevant. Becomes hot at arc 3.

9. **No published calendar.** Real risk: without a finale date,
   "are we there yet?" cannot be answered with a percent-of-time
   number, only a percent-of-slate number. Stakeholders may read
   the slate progress as faster or slower than reality.

---

## Recommendations

1. **Hard-cap the audit triple at three.** Do not let a fourth
   inserted episode push Costanza's first lead past E08. If new
   findings surface during E03-E05, queue them in the
   findings-backlog (per the `findings-backlog` skill) and address
   in arc 5 hardening.

2. **Greenlight the Pick before E06.** Larry to call Anthropic vs
   OpenAI direct. The blueprint recommends OpenAI direct (zero
   bridge code via the OpenAI-compat adapter). Recommend accepting
   the recommendation unless competitive optics has shifted since
   the S02E19 brief.

3. **Ship arc 1 in two pushes, not four.** The blueprint allocates
   four episodes (Adapter, Factory, Schema, Redactor) to arc 1.
   The Schema and Redactor are partially scoped by the imminent
   E03 (docs sweep) and E04 (security sweep) -- if those sweeps
   touch the right surface, the formal Schema/Redactor episodes
   become *integration* episodes rather than *introduction*
   episodes. Costanza to confirm overlap before E06 casting.

4. **Reconcile the blueprint quota table.** Either line 113 (3/4/3/3/3)
   or line 240 (4/4/5/4/2) is wrong. Picking one and updating the
   other resolves a downstream casting ambiguity.

5. **Publish a target finale date.** Even a soft target (end of
   2026-Q4, end of 2027-Q1) lets this report answer "are we there
   yet" against calendar, not just slate.

6. **Front-load Jerry.** Jerry has no lead until E08 in the original
   numbering, now E11. Consider giving Jerry a CI / preflight /
   Make-target episode in the E06-E07 window to balance the
   lead-cast burn-down.

7. **Cap the lead-cast supporting bench at three new hires for
   S03.** Bookman was hire #1 (S03E02). Two more is plenty;
   beyond that and we are running out of supporting-slot budget
   for blueprint-cast members like Maestro (E15), Frank (E19),
   Lloyd (E16), Peterman + Keith (E24).

---

## Verdict

**Are we there yet? No. We are roughly one-fifth of the way there.**

Specifically:
- **5 of 24 episodes in the can or imminent (20.8 percent).**
- **0 of 6 arcs complete.** Arc 1 has not started in earnest;
  E03/E04 audit triple touches arc 1 surfaces but does not
  introduce `IProviderAdapter`, `ProviderSelector`, or
  `preferences.json` v1.
- **0 of 4 blocked FRs unblocked.** FR-014 / FR-018 remain
  hard-blocked on arc 1. FR-019 deferred. FR-020 partial defer.
- **Lead-cast distribution is on volumetric pace but back-loaded.**
  Costanza's first lead now lands at E08; that is the load-bearing
  number.

**Posture: at-risk, recoverable.** No blueprint commitment has been
broken. No calendar gate has slipped (because none was published).
Arc 1 not yet starting at episode 5 is the single biggest concern
on the board. Call the Pick, ship the Adapter, and the season
recovers.

If the audit triple closes at three episodes and arc 1 starts on
E06, this report's next revision (after E10 in the new numbering --
the arc-1-and-2-complete checkpoint) should read **on track**.
If a fourth audit episode is inserted, or the Pick decision slips
past E08, this report's next revision will read **red**.

**Mr. Pitt out. Get me the Pick by E06.**

---

*Prepared by Mr. Pitt, Executive / Program Manager. Source documents:
`docs/exec-reports/s03-blueprint.md`, `docs/exec-reports/s03e01-the-yada-yada-strikes-back.md`,
`docs/exec-reports/s03e02-the-library-cops-word-limit.md`,
`docs/proposals/FR-014`, `FR-018`, `FR-019`, `FR-020`.*
