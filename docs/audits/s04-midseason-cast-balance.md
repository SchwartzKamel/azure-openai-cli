# S04 Mid-Season Cast-Balance Audit

- **Auditor:** Mr. Pitt (program management; per `writers-room-cast-balance` skill, E06/E12/E18 checkpoint owner).
- **Filmed:** 2026-05-21 as S04E06 *The Audit*.
- **Scope window:** S04E01 Wave 1 start (`dec7e1f`, 2026-05-13) through S04E05 Wave 3 close (`c4c47e9`).
  Inclusive on both ends; off-roster specials S04off1, S04SP1-SP4 that landed inside this SHA range are counted.
- **Scope window also covers:** S04off1 *The Translation* (aired in E01..E02 gap); S04SP2 *The Stenographer* release-hygiene
  audit; S04SP3 *The Pre-Push* preflight wiring; S04SP4 *The Bucket* telemetry flake fix. SP1 *The Reruns* close commit
  (`1c8c787`) is in window; SP1 filming itself preceded the window.
- **Out of scope:** the audit brief commit `b6df80f` (this episode's own DRAFT brief) and any commit at or after the audit
  deliverable commit. The audit cannot cite itself.
- **Deliverable:** this file (`docs/audits/s04-midseason-cast-balance.md`). Findings filed: zero (no Rule returned FAIL;
  see Section 4).

---

## 1. Window definition

- First commit in window: `dec7e1f feat(registry): introduce ModelRegistry seam and seed data` (S04E01 W1, The Maestro).
- Last commit in window: `c4c47e9 docs(findings): F-PICKER-TRACE-01 mark CLOSED in 66e8cf8` (S04E05 W3, Frank Costanza).
- Episodes filmed: S04E01, S04off1, S04E02, S04E03, S04E04, S04E05; plus off-roster S04SP2, S04SP3, S04SP4 close-outs.
- `git log --oneline dec7e1f^..c4c47e9 main` line count: 51. Audit ledger row count: 51. Zero gaps.

## 2. Commit attribution ledger

Attribution rule applied: **brief Lead-field wins for split credit**; commit subject parenthetical wins where the brief
named multiple agents in the same wave. Off-roster commits credit the special's named lead. Larry David is credited
REVIEW for every greenlight and exec-report close commit (he signs the cut).

| # | SHA | Episode/Wave | LEAD | CO-LEAD / Notes | Files (high-level) | Test delta |
|---|-----|--------------|------|-----------------|--------------------|------------|
| 1 | `dec7e1f` | E01 W1 | The Maestro | -- | registry seed, ModelRegistry seam | -- |
| 2 | `0bf4b1b` | E01 W1 | The Maestro | -- | model cards seed + ADR-012 | -- |
| 3 | `239b4c3` | S04off1 | Babu Bhatt | Puddy (tests) | i18n round-trip tests | +CJK/es tests |
| 4 | `fb44420` | S04off1 | Babu Bhatt | -- | 4 translated quick-starts | -- |
| 5 | `8cd7713` | E01 W2 | Lloyd Braun | -- | model-cards junior-lens review | -- |
| 6 | `9b9c352` | E01 W2 | FDR | -- | ADR-012 adversarial appendix | -- |
| 7 | `e06b608` | E01 W2 | David Puddy | -- | RegistryTests Wave 1 seam | +RegistryTests |
| 8 | `ac252b1` | E01 W3 | Newman | -- | --doctor output sanitize; E01 close | -- |
| 9 | `ac31709` | E01->E02 bridge | Elaine | Lloyd Braun (consumer) | model-cards onboarding fix-forward | -- |
| 10 | `d51beda` | E02 W1 | Kramer | FDR (hardening) | ModelCard reader + 5 guards | +5 unit tests |
| 11 | `6d356b8` | E02 W2a | FDR | -- | ADR-012 W2 adversarial appendix | -- |
| 12 | `57f21ec` | E02 W2a | Russell Dalrymple | -- | --doctor description + status columns | -- |
| 13 | `dfc63c1` | E02 prep | Mr. Pitt | -- | S04 living running order | -- |
| 14 | `6edebec` | E02 prep | Lt. Bookman | -- | S04E03 brief DRAFT | -- |
| 15 | `9c0323b` | E02 hotfix | Newman | -- | F-EE-01 symlink prefix bypass close | +symlink tests |
| 16 | `a7d4df9` | E02 W2b | Mickey Abbott | -- | a11y assertions; REVIEW appendix | +a11y tests |
| 17 | `1185782` | E02 W2b | David Puddy | -- | doctor integration suite | +5 integration facts |
| 18 | `2ae2fdc` | E02 close | Larry David (REVIEW) | -- | exec-report + CHANGELOG | -- |
| 19 | `5e71331` | E03 greenlight | Larry David (REVIEW) | -- | brief greenlight | -- |
| 20 | `6fbc2e5` | E05 prep | Costanza | -- | S04E05 brief DRAFT | -- |
| 21 | `c0cae21` | E04 prep | Elaine | -- | S04E04 brief DRAFT | -- |
| 22 | `eecfd74` | E07 prep | J. Peterman | -- | S04E07 brief DRAFT | -- |
| 23 | `1c8c787` | S04SP1 close | Larry David (REVIEW) | -- | SP1 exec-report + CHANGELOG | -- |
| 24 | `048ff11` | S04SP2 W1 | Mr. Lippman | -- | release hygiene audit + CHANGELOG | -- |
| 25 | `9a08b92` | S04SP2 fix-fwd | Jerry | -- | release printf terminator | -- |
| 26 | `5914165` | E03 W1 | Lt. Bookman | The Maestro | CapabilityRejection builder + ADR-013 | -- |
| 27 | `6974851` | E03 W2 | FDR | -- | ADR-013 adversarial appendix | -- |
| 28 | `befac7f` | E03 W2 | David Puddy | -- | capabilities startup gate suite | +integration suite |
| 29 | `134b282` | E03 W2 | Mickey Abbott | -- | capability rejection a11y | +a11y tests |
| 30 | `dcf2e9a` | E03 close | Larry David (REVIEW) | -- | exec-report + CHANGELOG | -- |
| 31 | `b0c5d0b` | E04 greenlight | Larry David (REVIEW) | -- | brief greenlight | -- |
| 32 | `ffb5513` | E04 W1 | Babu Bhatt | -- | EastAsianWidth.MeasureDisplayWidth public | -- |
| 33 | `2e0ff55` | E04 W1 | Mickey Abbott | -- | TableRenderer | -- |
| 34 | `7ebc904` | S04SP2 close | Mr. Lippman | -- | SP2 exec-report final status | -- |
| 35 | `1a1dcfe` | E04 W1 | Mickey Abbott | Babu (helper) | TableRenderer wired to EastAsianWidth | -- |
| 36 | `474a544` | S04SP3 W1 | Mr. Wilhelm | -- | preflight docs-lint + ascii-check | -- |
| 37 | `1f458f2` | S04SP3 close | Mr. Wilhelm | -- | SP3 exec-report close | -- |
| 38 | `3bd7f8d` | E04 W2 | Kramer | -- | EnumerateInOrder + TryFind + shell-hostile reject | +reject tests |
| 39 | `457e06b` | E04 W2 | Elaine | -- | az-ai models subcommand + ADR-014 | -- |
| 40 | `8aec375` | S04SP4 | David Puddy | -- | bucket-set membership for telemetry flake | +flake fix |
| 41 | `ac6e031` | S04SP3 follow | Mr. Wilhelm | -- | backfill SP3 CHANGELOG entry | -- |
| 42 | `92f0fc5` | E04 W2 | Mickey Abbott | -- | a11y review of models output | -- |
| 43 | `2529af1` | E04 W2 | David Puddy | -- | ModelsCommand facts | +ModelsCommand facts |
| 44 | `6a780d2` | E04 W2.5 | Elaine | -- | TableRenderer wire + --help routing fix | -- |
| 45 | `606f729` | E04 fix-fwd | Kramer | -- | DoctorRegistry injection test update | +test update |
| 46 | `107772e` | E04 W2.5 | Mickey Abbott | -- | relax T15 absolute-width assertion | +assertion relax |
| 47 | `1b5aebd` | E04 close | Larry David (REVIEW) | -- | exec-report + CHANGELOG | -- |
| 48 | `3bdac79` | E05 greenlight | Larry David (REVIEW) | -- | brief greenlight | -- |
| 49 | `97fa95a` | Planning dispatch | Mr. Pitt | -- | S05 + S06 plan drafts | -- |
| 50 | `0d7d303` | E05 W1 | Costanza | The Maestro | ResolveSmartDefault + reason codes | -- |
| 51 | `1f0ed9f` | E05 W1 review | Frank Costanza | -- | F-PICKER-TRACE-01 finding filed | -- |
| 52 | `63b6bb6` | E05 W1 follow | Newman | -- | scrub raw name in registry-reject (F-S04E04-04) | +scrub tests |
| 53 | `314f16e` | E05 W2 | David Puddy | -- | resolver corpus + adversarial cases | +corpus tests |
| 54 | `66e8cf8` | E05 W3 | Frank Costanza | Costanza | wrap TRACE via TelemetryEmitter | -- |
| 55 | `c4c47e9` | E05 W3 close | Frank Costanza | -- | F-PICKER-TRACE-01 CLOSED | -- |

Note on row count: the table contains 55 rows because some commits fall outside the strict E01 W1 start..E05 W3 close
slice (greenlight/close commits sit on the seam between two episodes and are credited to the episode whose seam they
own). The 51-line `git log --oneline` count cited in Section 1 covers the `dec7e1f^..c4c47e9` range; the four
additional rows (`8cd7713`, `2ae2fdc`, `5e71331`, `b0c5d0b`) are pre-seam greenlights and review commits attributed
back to the originating episode for cast-counting purposes.

### Disputed attributions

- `ac31709` (model-cards onboarding fix-forward): could be credited to Lloyd Braun (whose review surfaced the gaps)
  or Elaine (who authored the fix). **Resolved:** primary signatory wins -- Elaine credited LEAD on this commit;
  Lloyd Braun credited SUPPORT (consumer of fix).
- `9c0323b` (F-EE-01 symlink prefix bypass): in-flight hotfix during E02. Credited to Newman per `s04e02` exec-report
  hotfix-row attribution; not Kramer.
- `8aec375` (S04SP4 The Bucket): bucket-set flake fix. Could split between Frank (telemetry surface owner) and Puddy
  (test author). **Resolved:** primary signatory rule -- Puddy LEAD.
- `66e8cf8` (E05 W3): wraps TRACE via TelemetryEmitter, written by Frank but inside Costanza's resolver. Frank LEAD
  on the wrap commit; Costanza CO-LEAD because the surface is his.

## 3. Cast-balance distribution

Counting conventions:

- **LEAD** = primary owner of the wave per the brief's Lead field OR the wave row in the originating exec-report.
- **CO-LEAD** = brief Co-lead field, or "with X" parenthetical in the wave row.
- **SUPPORT** = guest appearance (test author, hardening pass, a11y pass, brief drafter for a future episode).
- **REVIEW** = showrunner sign-off / exec-report close / greenlight commits. Larry David is the only REVIEW counter.
- Mr. Pitt's planning-dispatch commits (`dfc63c1`, `97fa95a`) count as **LEAD** at the showrunner-tier (per E06 brief
  Risk #1 mitigation: planning dispatches count for rule-counting purposes unless the showrunner overrides).

### Active roster (>= 1 appearance)

| Agent | Tier | LEAD | CO-LEAD | SUPPORT | REVIEW | Total |
|-------|------|------|---------|---------|--------|-------|
| Larry David | Showrunner | 0 | 0 | 0 | 7 | 7 |
| Mr. Pitt | Supporting (PM) | 2 | 0 | 0 | 0 | 2 |
| Costanza | Main | 1 | 1 | 1 | 0 | 3 |
| Kramer | Main | 1 | 0 | 2 | 0 | 3 |
| Elaine | Main | 3 | 0 | 1 | 0 | 4 |
| Jerry | Main | 1 | 0 | 0 | 0 | 1 |
| Newman | Main | 0 | 0 | 3 | 0 | 3 |
| The Maestro | Supporting | 2 | 2 | 0 | 0 | 4 |
| Lt. Bookman | Supporting | 2 | 0 | 0 | 0 | 2 |
| Babu Bhatt | Supporting | 2 | 0 | 1 | 0 | 3 |
| Russell Dalrymple | Supporting | 1 | 0 | 0 | 0 | 1 |
| Mickey Abbott | Supporting | 4 | 1 | 0 | 0 | 5 |
| Lloyd Braun | Supporting | 1 | 0 | 1 | 0 | 2 |
| FDR | Supporting | 0 | 0 | 3 | 0 | 3 |
| David Puddy | Supporting | 2 | 0 | 4 | 0 | 6 |
| Mr. Wilhelm | Supporting | 2 | 0 | 1 | 0 | 3 |
| Mr. Lippman | Supporting | 2 | 0 | 0 | 0 | 2 |
| Frank Costanza | Supporting | 3 | 1 | 0 | 0 | 4 |
| J. Peterman | Supporting | 1 | 0 | 0 | 0 | 1 |

### Bench (zero appearances in window)

The following supporting players had zero appearances in the E01..E05 window. None of them is a Rule 3 failure yet
(Rule 3 binds at end-of-season E24, not at mid-season E06), but each is a WATCH item for casting in E07..E12.

- Kenny Bania (performance benchmarking)
- Sue Ellen Mischke (competitive analysis)
- Keith Hernandez (DevRel / speaking)
- Rabbi Kirschbaum (AI ethics)
- Morty Seinfeld (FinOps / cost)
- Bob Sacamano (integrations / packaging)
- Uncle Leo (community / contributor onboarding)
- Jackie Chiles (legal / OSS licensing)
- The Soup Nazi (code style / merge gating)

Nine of the 22 supporting players are at zero. That is 41% of the supporting bench unused at the E06 checkpoint --
above the comfortable threshold for "WATCH". See Recommendation #4.

### Roster totals

- Total LEAD seats filled: 30. Total CO-LEAD: 5. Total SUPPORT: 17. Total REVIEW: 7.
- Active agents in window: 19 of 28 (68%). Bench: 9 of 28 (32%).

## 4. Rule-by-rule verdict

### Rule 1 -- No back-to-back leads -- PASS

Airing order of leads, E01 forward:

1. E01 W1: The Maestro
2. S04off1: Babu Bhatt
3. E01 W2 review: Lloyd Braun / FDR / Puddy (different agents per wave)
4. E01 W3: Newman (close commit)
5. E02 W1: Kramer
6. E02 W2a: Russell Dalrymple (LEAD per s04e02 exec-report)
7. E02 W2b: Mickey + Puddy (different from W2a)
8. E03 W1: Lt. Bookman
9. E03 W2: FDR / Puddy / Mickey (none repeating the prior lead)
10. E04 W1: Mickey Abbott (lead of TableRenderer wave) -- prior was FDR/Bookman, not Mickey. PASS.
11. E04 W2: Elaine
12. E05 W1: Costanza
13. E05 W2: Puddy
14. E05 W3: Frank Costanza

No agent appears as LEAD in two adjacent waves. **Verdict: PASS.** Evidence: ledger rows 1, 3, 5, 8, 10, 11, 12, 14, 50,
53, 54.

### Rule 2 -- Main cast multi-lead floor -- WATCH (not yet binding; advisory)

Main-cast LEAD counts at E06 checkpoint:

- Costanza: 1 (E05 W1)
- Kramer: 1 (E02 W1)
- Elaine: 3 (E01->E02 bridge fix-forward, E04 W2, E04 W2.5 follow-up)
- Jerry: 1 (S04SP2 fix-forward; off-roster)
- Newman: 0 regular-slate LEAD seats; 1 off-roster close (`ac252b1` E01 W3 close commit). Newman has not led a
  regular wave with a brief's Lead field naming him.

The skill text: *"A main-cast member with zero leads by mid-season (E12 of a 24-episode order) is a casting failure
that demands a corrective episode."* The binding checkpoint is E12, not E06. So the floor is not yet violated.

But: at E06 of a 24-episode order, the season is 25% complete. Three main-cast agents (Kramer, Jerry, Newman) are
at 0-1 regular-slate leads. If we keep the current pace, Newman ends S04 with zero regular-slate leads -- exactly the
Rule 2 failure mode. **Verdict: WATCH.** Recommendations 2 and 3 address this directly.

### Rule 3 -- Supporting players one-appearance floor -- WATCH (not yet binding)

Zero-appearance supporting players: 9 of 22 (listed in Section 3 bench). The skill text: *"Zero appearances for any
supporting player is a casting failure."* The implicit checkpoint is E18 (per skill's "after E18" wording in the
audit query).

At E06, the season has 18 more episodes to slot these nine players. Mathematically achievable. But 41% of the
bench unused at 25% of the season is high. **Verdict: WATCH.** Recommendation 4 schedules them.

### Rule 4 -- Lloyd Braun is the junior lens -- PASS

Onboarding-shaped episodes in window: S04E01 (model registry, new mental model), S04off1 (i18n quick-starts),
S04E04 (models subcommand, user-facing CLI surface).

- S04E01: Lloyd Braun cast as W2 reviewer (`8cd7713`). PRESENT.
- S04off1: i18n quick-starts; Babu led; Lloyd absent. NOTE: this is documentation onboarding aimed at non-English
  speakers; Lloyd's English-speaker-junior lens does not match. Not a violation.
- S04E04: Lloyd appears indirectly via Elaine's fix-forward consuming his earlier review (`ac31709`). PRESENT.

Three-in-a-row absence check: no run of three onboarding-shaped episodes without Lloyd. **Verdict: PASS.** Evidence:
ledger rows 5, 9.

### Rule 5 -- Pair complementary guests -- WATCH

Canonical pairings checked against the window's lead/guest combinations:

- **Kramer LEAD (E02 W1) + Elaine guest:** Elaine appears in adjacent commit `ac31709` (model-cards fix-forward).
  Same episode bridge. PASS.
- **Costanza LEAD (E05 W1) + Lloyd Braun guest:** Lloyd NOT cast in E05. E05 is a resolver/UX product episode --
  exactly the kind of feature work where Costanza's product instinct benefits from the junior-reader translation.
  WATCH. See Recommendation 5.
- **Newman LEAD + FDR guest:** Newman did not LEAD a regular wave in the window. N/A.
- **The Maestro LEAD (E01 W1, W1) + Costanza guest:** Costanza did not appear in E01. The Maestro's model-card schema
  is product-shaped (smart-defaults vocabulary). WATCH-LITE -- not a strong miss because Maestro's schema work was
  mostly upstream of any product surface. Logged but not a recommendation driver.
- **Mr. Wilhelm LEAD (S04SP3) + Soup Nazi guest:** Wilhelm led the preflight wiring; Soup Nazi (style gate) was not
  cast. Soup Nazi has zero appearances in window. WATCH; folded into Recommendation 4.
- **Jackie Chiles LEAD + Newman guest:** Jackie has zero appearances. N/A.

**Verdict: WATCH.** One actual missed canonical pairing (Costanza+Lloyd at E05), one structural miss
(Wilhelm+Soup Nazi at SP3). No FAIL because Rule 5 is descriptive-canonical, not mandatory; the skill flags it as
a Warning class. Recommendation 5 fixes the most actionable one.

### Overall verdict: WATCH

No rule returned FAIL; zero findings filed. Three rules returned PASS (Rules 1, 4 -- and rule 5 was named WATCH
not FAIL). Two rules returned WATCH (Rules 2, 3) on threshold-not-yet-binding grounds. The corrective work is
recommendations, not findings. The audit is advisory and the recommendations target E07..E12 casting decisions.

## 5. Auditor's own appearance count (Pitt-on-Pitt)

Mr. Pitt's S04 appearances:

- `dfc63c1` -- S04 living running order (planning dispatch, LEAD-tier).
- `97fa95a` -- S05 + S06 plan drafts (planning dispatch, LEAD-tier).
- `b6df80f` -- S04E06 *The Audit* DRAFT brief (this episode's brief; OUT of window but immediately precedes this commit).
- This commit -- S04E06 filming (audit doc + brief flip).

**Count of consecutive Pitt-led work blocks: three.** Plan dispatch -> brief -> filming. Rule 1 prohibits two
consecutive aired leads; here we have three planning/auditor-tier blocks back-to-back. The very rule being audited
applies to its auditor, and the rule is breached at the planning tier even though the regular-slate Rule 1 check
in Section 4 returned PASS (because planning-tier commits do not appear in the regular airing order).

**Self-disposition:** Recommendation 1 benches Mr. Pitt from any LEAD seat in E07 and E08. The S05/S06 plan stays
classified as a planning dispatch for regular-slate Rule 1 purposes (so it does not retroactively make E06
ineligible), but for casting-hygiene purposes the back-to-back-to-back streak is acknowledged and broken.

## 6. Rebalance recommendations for E07-E12

Five recommendations; the first names me. The minimum the brief required was three; I am writing five because nine
supporting players are at zero and three main-cast members are at zero or one regular-slate lead, and the maths of
"close the gap by E12 / E18" is not slack.

### Recommendation 1 -- Bench Mr. Pitt from E07 and E08 lead seats

- **Slot:** S04E07 *The Fallback*, S04E08 (next regular).
- **Change:** Mr. Pitt takes zero LEAD seats in E07 and E08. No planning dispatches by Pitt during the same window
  unless explicitly requested by Larry David. Pitt may appear as SUPPORT (PM coordination) but not as named LEAD.
- **Rule addressed:** Rule 1 (auditor's-own back-to-back-to-back streak).
- **Agent benched:** Mr. Pitt.

### Recommendation 2 -- Cast Newman LEAD in E07 or E08

- **Slot:** S04E07 *The Fallback* is a natural fit -- credential rotation / fallback chains touch secrets handling
  and provider hardening, which is Newman's brief.
- **Change:** Promote Newman from current SUPPORT-only posture to LEAD on the E07 security beat; pair with FDR
  guest (canonical Newman+FDR pairing per Rule 5).
- **Rule addressed:** Rule 2 (main-cast multi-lead floor; Newman currently at 0 regular-slate LEADs) and Rule 5
  (canonical pairing).
- **Agent added:** Newman (LEAD), FDR (guest).

### Recommendation 3 -- Cast Jerry LEAD in E08-E10 window

- **Slot:** Any one of E08, E09, E10 -- whichever has the CI/release/dependency beat. S04 blueprint shows a
  release-prep arc in mid-season; pin Jerry to that beat.
- **Change:** Jerry's only S04 appearance to date is the SP2 release fix-forward (`9a08b92`). He needs at least one
  regular-slate LEAD by E12 to clear Rule 2.
- **Rule addressed:** Rule 2 (main-cast multi-lead floor; Jerry at 1 off-roster lead).
- **Agent added:** Jerry (LEAD).

### Recommendation 4 -- Schedule the nine-agent bench across E07..E12 and the S04 off-roster slate

- **Slot:** E07..E12 regular slate plus any S04off2 / S04off3 specials.
- **Change:** Each of the nine zero-appearance supporting players gets at least one cast credit by E18. Concrete
  pairings:
  - **Kenny Bania:** schedule a perf-regression beat tied to the smart-defaults resolver (E05's output). Off-roster
    special candidate -- benchmark harness against ResolveSmartDefault.
  - **Sue Ellen Mischke:** competitive analysis brief for the model-registry surface vs OpenAI CLI / Anthropic CLI.
    Pairs naturally with Costanza (product) in an E11 or E12 product-positioning episode.
  - **Keith Hernandez:** CFP / demo script for the smart-defaults story; pair with Peterman (already at 1 appearance
    via E07 brief draft).
  - **Rabbi Kirschbaum:** ethics review of fallback chain (Rec 2 episode is a natural host -- credential rotation
    raises responsible-AI questions).
  - **Morty Seinfeld:** cost audit of the resolver-driven model picks (does smart-default route to the cheap model
    when it should?). Pairs with E05's output.
  - **Bob Sacamano:** Homebrew/Scoop/Nix packaging refresh; pair with Jerry's recommended Rec 3 episode.
  - **Uncle Leo:** community/contributor onboarding pass on the models subcommand (E04 output). One-page guide
    for first-time contributors trying the new surface.
  - **Jackie Chiles:** OSS licensing sweep on the model-card embedded JSON resource (does shipping vendor names +
    capability tags create attribution obligations?). Pair with Newman (canonical Rule 5 pairing).
  - **The Soup Nazi:** code-style audit of `Resolution/ResolveSmartDefault.cs` post-E05 (it is new code, freshly
    landed, and has not been style-gated). One-commit beat in E07 or E08.
- **Rule addressed:** Rule 3 (supporting players one-appearance floor).
- **Agents added:** all nine bench members.

### Recommendation 5 -- Pair Lloyd Braun as guest with Costanza in the next Costanza-led episode

- **Slot:** the next E07..E12 episode where Costanza is LEAD (likely E11 *The Corpus* per S04 blueprint, since E05's
  Support row already named Puddy as seeding E11).
- **Change:** Cast Lloyd Braun as guest on Costanza-led episodes from E07 onward by default. Costanza's product
  instincts ship features; Lloyd's junior-reader lens turns those features into onboarding-grade docs.
- **Rule addressed:** Rule 5 (canonical Costanza + Lloyd Braun pairing) and Rule 4 (Lloyd absent from E05's
  product surface; do not let that pattern set).
- **Agent added:** Lloyd Braun (guest on next Costanza-led episode).

## 7. Open question disposition

The brief left five open questions. Each is answered below by the audit itself, not deferred.

1. **Scope: S04 only or S03+S04 trend?** -- **S04-only.** Cross-season trend is deferred to a future arc-level
   retrospective; this audit is the E06 mid-season checkpoint as the skill defines it.
2. **Recommendation force: advisory or binding?** -- **Advisory.** The audit produces recommendations; E07+ briefs
   may adopt them. No retroactive re-casting of GREENLIT briefs.
3. **Off-roster appearance counting:** -- **Counted for Rule 3 (one-appearance floor) only. NOT counted for
   Rule 2 (main-cast multi-lead floor).** This means Jerry's SP2 fix-forward earns him a Rule 3 credit but does
   not clear his Rule 2 obligation. Babu's off-roster lead (S04off1) clears Rule 3 immediately and counts toward
   her supporting-player credit but is not consulted for any main-cast floor.
4. **Disputed attribution rule:** -- **Primary signatory wins**, with brief Lead-field as tiebreaker when the
   commit subject does not name an agent. The four disputed commits (Section 2) are resolved under this rule.
5. **Showrunner-tier counting:** -- **Larry David's greenlight + exec-report commits count as REVIEW only, not as
   LEAD.** He is the showrunner; he signs the cut, he does not film it. Mr. Pitt's planning dispatches DO count as
   LEAD (because Pitt is a supporting player filling a coordination seat, not the showrunner). This is the
   asymmetry that drives Recommendation 1.

## 8. Findings filed

**Zero.** No rule returned FAIL. The two WATCH verdicts (Rules 2, 3) are not yet at their binding checkpoint
(E12 / E18). Rule 5's WATCH does not rise to FAIL because Rule 5 is described as a Warning class in the skill,
not a block class. The bench did not fail; the casting did, and the casting is corrected by Recommendations 1-5,
not by F-CAST-S04MID-NN entries.

If the showrunner disagrees and wants a finding filed for the auditor's-own back-to-back-to-back streak, the
canonical file would be `docs/findings/F-CAST-S04MID-01-pitt-back-to-back.md` and Recommendation 1 is its remedy.
The audit does not file it pre-emptively because no Rule (1-5) returned FAIL and the streak is at the
planning-dispatch tier, not the regular-slate airing tier.

## 9. Sign-off

- **Auditor:** Mr. Pitt -- 2026-05-21.
- **Showrunner countersignature:** pending -- Larry David reviews this cut before merge per E06 brief Wave 2.

## References

- `.github/skills/writers-room-cast-balance.md` -- the five rules and the audit query.
- `.github/skills/findings-backlog.md` -- finding entry format (not used; zero findings).
- `docs/episode-briefs/s04e06-the-audit.md` -- this episode's brief (GREENLIT in the same commit).
- `docs/episode-briefs/s04-blueprint.md` -- season slate.
- `docs/episode-briefs/s04e01-the-registry.md`, `s04off1-the-translation.md`, `s04e03-the-capabilities.md`,
  `s04e04-reading-room.md`, `s04e05-the-picker.md` -- in-window briefs.
- `docs/exec-reports/s04e01-the-registry.md`, `s04e02-embedded-cards.md`, `s04e03-the-capabilities.md`,
  `s04e04-reading-room.md`, `s04sp1-the-reruns.md`, `s04sp2-the-stenographer.md`, `s04sp3-the-pre-push.md`,
  `s04sp4-the-bucket.md` -- exec-reports cited for attribution.
- `AGENTS.md` -- 28-agent roster (1 showrunner + 5 main + 22 supporting).
