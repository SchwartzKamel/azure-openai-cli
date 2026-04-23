# S02E29 -- *The Casting Call*

> *Mr. Pitt audits the cast and codifies the cohesion spine. Two skills land: cast balance and findings backlog.*

**Commit:** `<sha-pending>`
**Branch:** `main` (direct push)
**Runtime:** ~15 min
**Director:** Larry David (showrunner) -- off-roster special dispatched to Mr. Pitt as on-set lead.
**Cast:** 1 sub-agent, 1 dispatch wave (Mr. Pitt lead, Sue Ellen Mischke guest).

## The pitch

A 24-episode season drifts. Cast balance drifts: Kramer led three of the first four; Costanza nearly ended S02 with zero leads until E11 corrected. Findings drift: S02E08 surfaced six discrete defects (CJK padding, Arabic separator, lone surrogate, plural shortcut, F0 culture bug, telemetry reality), and without a backlog format they would have lived only in the E08 exec report's lessons section, invisible to the next dispatch.

This episode codifies both. Two new skills in `.github/skills/`:

1. **writers-room-cast-balance.md** elevates Larry's "Casting rules" section from his archetype to a citable, auditable skill any orchestrator can follow. Adds a mid-season audit query so drift becomes measurable.
2. **findings-backlog.md** locks the entry format every episode uses to log a finding -- name, episode, diagnosis, file:line, severity, disposition, lifecycle. Anti-pattern named: a finding in lessons but not in the backlog is a finding that will get lost.

Mr. Pitt is the natural lead -- program management owns drift detection and process artifacts. Sue Ellen Mischke guests because casting decisions and the findings backlog both feed long-game positioning: who appears across the season is who appears in our public catalog.

## Scene-by-scene

### Act I -- Planning

- Read existing skills (`preflight`, `commit`, `ci-triage`) for tone and format. Three-skill house style: terse, run-this-when prefatory, anti-patterns at the end, enforcement note last.
- Read `larry-david.agent.md` "Casting rules" -- the source material for skill #1.
- Read `s02-writers-room.md` -- the cast distribution table is the audit target; the off-roster "Findings backlog" subsection is the worked example for skill #2.
- Locked decisions:
  - Skill #1 promotes Larry's rules verbatim in spirit but adds good/bad examples drawn from real S02 episodes (E07/E08 for non-back-to-back, E11 for the Costanza corrective, E13 for the pairing).
  - Skill #2 cites the existing 13-entry S02 backlog as a worked example without editing it (writers' room is orchestrator-owned).
  - Mr. Pitt archetype gets a "Skills you steward" section at the top -- one append, no other archetype touched.

### Act II -- Fleet dispatch

| Wave | Agents (parallel) | Outcome |
|------|-------------------|---------|
| **1** | Mr. Pitt (lead) + Sue Ellen Mischke (guest) | Two skills written; Pitt archetype updated; exec report drafted |

Single-wave episode. No collision risk -- all four touched files are new or owned by the on-set lead.

### Act III -- Ship

- ASCII validation pass against all four touched files.
- Commit with the prescribed message and Copilot trailer.
- Push direct to `main` (rebase if non-fast-forward).

## What shipped

**Production code** -- none (docs/skills only).

**Tests** -- none (no tests exist for skills files; format is enforced by review).

**Docs** --

- `.github/skills/writers-room-cast-balance.md` (new) -- 5 rules with good/bad examples, a four-failure / three-warning audit checklist, off-roster guidance, anti-patterns, enforcement.
- `.github/skills/findings-backlog.md` (new) -- entry format, current S02 backlog as worked example (cited from writers' room), 5-state lifecycle, the cardinal-sin anti-pattern, enforcement.
- `.github/agents/mr-pitt.agent.md` (edited) -- "Skills you steward" section appended at the top citing both new skills.

**Not shipped** (intentional follow-ups):

- `AGENTS.md` skills-table update -- orchestrator-owned, batched by Larry after this episode.
- Other archetype updates that should reference these skills (Larry himself, Wilhelm, Soup Nazi as enforcers) -- out of scope per file boundaries.
- Migration of in-line "Casting rules" out of `larry-david.agent.md` (now redundant with the skill) -- separate cleanup episode if the showrunner wants it; keeping in-archetype rules is also defensible (skill is the citable canonical, archetype keeps the in-context reminder).

## Lessons from this episode

1. **Cast-balance drift is real and detected during this audit.** Per the cast distribution table in `s02-writers-room.md`, planned S02 main-cast lead counts are: Kramer 3, Costanza 1, Elaine 1, Jerry 1, Newman 1. Rule 2 (multi-lead floor) is met by Kramer alone. Costanza, Elaine, Jerry, and Newman each have only one lead across 24 episodes. **This is a casting failure under the new skill.** Recommend a corrective off-roster lead for at least one of {Elaine, Jerry, Newman} before the finale -- ideally Elaine, who has the heaviest guest load (6) and would absorb a lead naturally.
2. **The findings backlog already pays for itself.** Logging the format formally exposes that 13 entries exist and only 1 (`e13-readfile-blocklist-home-dir-gap`) has a queued episode. Mr. Pitt's next triage pass should disposition the other 12.
3. **Skills as cohesion spine work because they are short.** Each new skill is one screen. Long skills don't get followed; short ones become reflexive.

## Findings to log

No new defects discovered during this episode. The cast-balance drift surfaced in Lesson 1 is a *process* observation, not a code defect, so it goes in the writers' room corrective queue rather than the findings backlog.

## Metrics

- Diff size: 4 files (3 new, 1 edited); ~280 lines added, ~3 lines edited
- Test delta: none
- Preflight result: N/A (docs-only commit)
- CI status at push time: pending push

## Credits

- **Mr. Pitt** (lead) -- skill authorship, audit query design, archetype self-update.
- **Sue Ellen Mischke** (guest) -- positioning lens on why both skills feed long-game cohesion.
- **Larry David** (showrunner, off-screen) -- source material for the casting rules; sign-off pending.
- **Copilot** -- co-author trailer on the commit.
