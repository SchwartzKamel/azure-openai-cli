# Skill: writers-room-cast-balance

**Run before every casting decision.** This is the cohesion spine for a 24-episode season. The showrunner (or any orchestrator filling that seat) follows these rules when picking a lead and guests for a new episode. Skipping the rules is how a season drifts into the Kramer Show with everyone else on the bench.

The rules below were lifted from the `larry-david` archetype's "Casting rules" section and elevated to a citable, auditable skill so any orchestrator -- not just Larry -- can apply them.

## When to run

- Before dispatching any new episode (regular or off-roster special).
- At every mid-season checkpoint (E06, E12, E18 of a 24-episode season).
- Any time the user gives a casting note ("more Jerry", "no love for Costanza").

## The five rules

### Rule 1 -- No back-to-back leads

If a cast member led the previous aired episode, do not cast them as lead in the next one. Bench them at minimum one episode. Airing order is what counts, not plan order.

- **Good.** S02E07 lead is Frank Costanza; S02E08 lead is Babu Bhatt. Different leads, no back-to-back.
- **Bad.** S02E04 *The Locksmith* (Kramer lead) followed immediately by a hypothetical S02E05 with Kramer leading again. Two consecutive Kramer leads -- bench him.

### Rule 2 -- Main cast multi-lead floor

Each of the five main cast (Costanza, Kramer, Elaine, Jerry, Newman) gets multiple leads per season. A main-cast member with zero leads by mid-season (E12 of a 24-episode order) is a casting failure that demands a corrective episode.

- **Good.** Kramer leads E01, E02, E04 -- well above the multi-lead floor.
- **Bad.** Costanza had zero leads in the original 7-episode plan; the corrective was to give him E11 *The Spec*. If E11 had not been added, he would have ended S02 with zero leads.

### Rule 3 -- Supporting players one-appearance floor

Every supporting player on the bench gets at least one appearance per season -- as lead OR as guest. Zero appearances for any supporting player is a casting failure.

- **Good.** Russell Dalrymple guests E06 *The Screen Reader*. One appearance, floor met.
- **Bad.** A supporting player whose name never shows up in any episode's lead-or-guest column for the whole season. The bench was paid; use them.

### Rule 4 -- Lloyd Braun is the junior lens

Cast Lloyd Braun in any episode where a senior cast member's instinct to skip explanation will hurt junior readers. He is not optional ornament; he is the reader-of-record for everything onboarding-shaped.

- **Good.** S02E08 *The Translation* casts Lloyd as a guest so the i18n vocabulary gets explained instead of assumed.
- **Bad.** A docs-heavy episode (e.g., a glossary or onboarding pass) led by Elaine alone with no Lloyd guest. Senior writers will ship terms they assume the reader knows.

### Rule 5 -- Pair complementary guests

Lead a senior cast member with a guest whose specialty fills the lead's natural blind spot. The canonical pairings:

| Lead | Guest | Why |
|------|-------|-----|
| Newman | FDR | Offense paired with defense -- security audit + adversarial scenarios |
| Kramer | Elaine | Code paired with docs -- the implementation lands with the explanation |
| Costanza | Lloyd Braun | Product paired with learner -- features get the user-story translation |
| Frank Costanza | Newman | SRE paired with security -- reliability signals don't leak secrets |
| Mr. Wilhelm | Soup Nazi | Process paired with gatekeeping -- rules + the enforcement of them |
| Jackie Chiles | Newman | Legal paired with security -- license obligations + supply-chain risk |
| The Maestro | Costanza | Prompts paired with smart-defaults UX -- prompt quality is product quality |

- **Good.** S02E13 *The Inspector* casts Newman (lead) + FDR + Jackie. Security audit gets adversarial scenarios and a license sweep in one episode.
- **Bad.** Newman leading a security episode with only Newman in the room. No FDR means no red-team perspective; the audit ships with rose-colored glasses.

## Casting audit query

Run this checklist against the writers' room cast distribution table. Drift detected mid-season triggers a corrective episode.

**Failures (block on these):**

- [ ] Has any main-cast member zero leads after E12? **Casting failure.** Schedule a corrective lead in the next available wave.
- [ ] Has any supporting player zero appearances after E18? **Casting failure.** Slot them as guest in a remaining episode or write an off-roster special.
- [ ] Are there two consecutive episodes (by airing order, not plan order) with the same lead? **Casting failure.** Re-cast the second one.

**Warnings (flag, don't block):**

- [ ] Are pairings repeating without rotation (e.g., Newman + FDR three episodes in a row)? **Warning.** Vary the guest seat.
- [ ] Is one supporting player at 5+ appearances while another is at 1? **Warning.** Imbalanced bench utilization; rebalance future episodes.
- [ ] Has Lloyd Braun been absent from the last three onboarding-shaped episodes? **Warning.** Junior lens drifting -- add him to the next docs-heavy beat.

## Off-roster specials

Off-roster specials (E25 / E26 / E27 / E28 / E29 in S02 numbering) follow the same rules but are easier to cast against under-used bench because they have no arc dependencies. Use them deliberately:

- A supporting player about to fail Rule 3? Give them an off-roster lead.
- A pairing that hasn't fired all season? Slot it into a special.
- A finding from the backlog that needs an owner? Cast the natural specialist as lead.

The leniency is that off-roster specials don't inherit the multi-lead floor -- they exist precisely to backfill rule violations the regular slate created.

## Anti-patterns

- **Casting by inertia.** "Kramer led the last three; he's hot, keep going." That violates Rule 1 and starves the bench. Bench him.
- **Casting the strongest agent for everything.** The point of a 27-agent fleet is range. If three episodes in a row are Kramer leads, the season has range of one.
- **Treating supporting players as decoration.** Every name in the supporting table is paid bench. If you don't use them, drop them from the roster -- but don't fake-include them.
- **Skipping the pairing.** A solo Newman security episode misses FDR's adversarial scenarios. A solo Kramer feature episode misses Elaine's docs. Pair complementary guests on purpose.
- **Casting without checking the distribution table.** Drift is invisible until you measure. The writers' room cast distribution table is the measurement.

## Enforcement

- The showrunner (default: Larry David) owns the writers' room cast distribution table and the corrective dispatches.
- Mr. Pitt (program management) audits drift at mid-season checkpoints and surfaces failures.
- Any orchestrator -- human or sub-agent -- filling the showrunner seat for a one-off dispatch must run this skill before casting.
