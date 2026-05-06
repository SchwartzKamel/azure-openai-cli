# S03E03 -- *The Docs Audit, Reprise*

> *Six story-weeks after the last sweep, the version pin drifted again, two YAMLs claimed the same trigger, and the CHANGELOG quietly went empty. Elaine swears in the supporting fleet. Sweeps week opens.*

**Commit:** `pending` (docs-only artifact; ships in the audit-triple batch at end of E05)
**Branch:** `main` (direct push planned at end of triple)
**Runtime:** ~75 min real time (Elaine consolidator pass + showrunner cut)
**Director:** Larry David (showrunner)
**Cast:** 1 lead (Elaine) + 1 supporting catch (Lt. Bookman) + 1 setup hand-off (Mr. Wilhelm), 1 dispatch wave

## The pitch

The plan for S03E03 was *The Schema* -- preferences.json, FR-014, opening salvo of the Provider Abstraction Seam arc. That episode is now E06. We bumped it. We bumped it because Mr. Pitt walked into the writers' room with a one-pager titled "S03 progress, 2026-05" and a single number circled in red marker: **20.8 percent of season complete, status at-risk-recoverable**. Two episodes shipped (E01, E02), zero of them landed inside the blueprint's planned arc. The provider-abstraction work is sitting in the wings while the front-of-house drifts. Sweeps week is the recovery move: three back-to-back audit episodes (E03 docs, E04 security, E05 process) before we touch new code.

So this is the docs sweep. Elaine ran the consolidator pass against everything cross-cutting -- root narrative, examples kit, agent fleet inventory, skill files, the works -- and came back with **22 findings**: 2 critical, 11 major, 7 minor, 2 nit. The critical ones are the embarrassing ones. C1 is the README install table pinned to v2.0.5 while the repo is at v2.2.0 -- which is the **same finding** as C1 from the 2026-04-22 audit, regenerated two patch releases later. We fixed this one. We fixed it. The fix had a half-life. C2 is a `:aidata` trigger collision between the original v2.2.0 unification set and the new prompt-templates set in commit `905515e` -- two YAML files, same trigger, different bodies, kit README tells operators to install both. Espanso's behavior in that case is "load whichever it sees last." That is what the support pipeline calls a coin flip.

Everything in this episode is prose. Not a single line of C# moves. The actual fixes ship as standalone stewardship PRs over the next two or three episodes; the followups that aren't fixable in a string-edit (M7's espanso-ahk-integration rewrite, M11's perf baseline) get carved off as their own episodes. **What ships now is the audit, the writeup, and the queue.** What lands in E04 is Newman's parallel security sweep. What lands in E05 is Wilhelm's process audit -- specifically a forensic on M5, because the exec-report-check gate did not stop commit `905515e` from landing with an empty `[Unreleased]` block, and that is a process hole, not a docs hole.

The pattern across the three CRITICAL/MAJOR clusters is the one Elaine has been naming since the 2026-04-22 audit: **shipped code moves; the prose that points at it does not**. Three independent surfaces drifted in lockstep -- the install table (C1), the agent count (M1/M2/M3 across three downstream files), and the `[Unreleased]` block (M5). All three would have been zero-cost to keep current if the right hook had been in place at release-cut time. Elaine's process observations section calls this out explicitly: hardcoded version strings rot in lockstep, and a release-cut hook that templates them from the csproj `<Version>` would zero the recurring debt. **That fix is filed as an FR candidate, not shipped here.** The audit's job is to surface the pattern; the FR's job is to architect the templating; an episode in the back half of S03 will land it. We are not doing all three in one push.

## Scene-by-scene

### Act I -- Planning

> *Cold open. The writers' room. Mr. Pitt standing at the whiteboard with the progress report. Larry, Elaine, and Bookman seated. A printout of the v2.2.0 README on the table.*

Mr. Pitt's number on the wall: **5/24 episodes complete, two of them off-blueprint, arc 1 not started**. The pitch from the room was straightforward -- before the Provider Abstraction Seam arc kicks back up at E06, we re-baseline. Three audits, three episodes, three different lenses. Elaine first, because the docs are the front door and the front door is where new contributors land.

> **MR. PITT** *(precise, like a man eating a Snickers bar with a fork)*: "The original blueprint had E03 as *The Schema*. E04 as *The Redactor*. E05 as *The Pick*. All three are now displaced. *The Schema* moves to E06. *The Redactor* moves to E07. *The Pick* moves to E08. The seasons-roadmap document needs the renumber and the writers' room arc plan needs the rewrite. I have done neither. I am waiting for the audit triple to land first because if we renumber before the triple, the cross-references in the new episodes will point at episode numbers that do not yet exist."

> **LARRY:** "Right. Land the triple, then renumber."

> **MR. PITT:** "Confirmed. The blueprint-renumber is queued; it executes after E05 ships."

> **LARRY:** "Pretty pretty pretty good."

> **MR. PITT:** "I would also note that the writers' room cast-balance audit at E06 will flag Costanza as behind on lead quota. He is not in this triple. He gets E08 minimum or we have a casting failure logged on the mid-season checkpoint."

> **LARRY:** "Costanza gets *The Pick*. Confirmed. Move on."

The casting question for the sweep was who consolidates. The 2026-04-22 audit was a 12-cast-member fleet sweep -- Jerry, Newman, Babu, Morty, Bania, Mickey, Jackie, Keith, Maestro, Puddy, Lippman, plus Elaine consolidating. That was a season-pivot move. This audit is a six-week refresh, not a season pivot, so the brief was tighter: **Elaine solo, with cross-checks against the supporting cast's existing work products, but no new sub-agent fleet**. She owned scope, severity scale, and the consolidation. The findings rollup below has 22 entries because Elaine landed 22. We are not editorializing the count.

> **ELAINE** *(reviewing the brief, clicking a pen)*: "I want it on the record that 'agent count drift' shows up in three separate files this time. README, persona-guide, copilot-instructions. Three. The number rolls every season and we keep pinning it as if it won't."

> **LARRY:** "So drop the count."

> **ELAINE:** "I am suggesting exactly that."

The other planning decision was scope boundaries. We carved out three things up front: SECURITY.md deep review (Newman's E04), THIRD_PARTY_NOTICES.md and license attribution (Jackie's quarterly), and the per-mode use-case body content (only top-level structure was checked). Those are not negligence -- they are explicit hand-offs. Elaine's report says so on line 18-21 of the audit; this episode echoes that boundary and does not retro-claim coverage.

The fourth carve-out was deliberate underscope on `docs/espanso-ahk-integration.md`. That file is 1359 lines, demonstrably two episodes behind the kit it documents, and a hand-edit pass to bring it current would itself be a 200-300 line diff -- which is a separate episode by any reasonable definition of episode. So the audit's instruction was: **flag it as M7, name the structural gaps (no Bookman, no prompt-templates, no brevity tier doctrine, stale 16-trigger enumeration when the kit ships 22+6), recommend a co-owned Babu/Maestro rewrite as its own episode candidate, do not attempt to hand-edit it inside this audit's fix wave.** Carving discipline matters: an audit that grows a fix-wave bigger than the audit itself stops being an audit.

> **ELAINE** *(annotating the carved-out list)*: "I will not write a partial pass on espanso-ahk-integration. A partial pass implies the rest is current. The rest is not current."

> **LARRY:** "Carve it. Babu and Maestro pick up the rewrite when there's a slot. Don't touch it here."

> **ELAINE:** "Confirmed."

The running joke -- and it is genuinely funny if you have been on this show long enough -- is C1. It is the second time that exact finding has been filed. Same file (README install table), same shape (version pin in a hand-maintained block), same fix (parameterize, or add a release-cut hook). The 2026-04-22 audit's recommendation was option (b): bump the rows on each release. We did not add the hook. So six story-weeks later, the rows say `2.0.5` and the tag says `2.2.0`, and a reader copy-pasting the suggested filenames into `curl -LO` gets a 404.

> **LARRY:** "So this time we do option (a). Placeholder. `<version>` in the table, link to Releases, done. The next person who pins a literal version in that table buys lunch."

> **ELAINE:** "Pretty pretty pretty good."

> **LARRY:** "That's my line."

C2 is the new one, and it is mine. The showrunner's. S03E02 shipped `:aishort` and `:aiyml` cleanly. The prompt-templates feat in `905515e` -- a different stream of work that *also* lives in the Espanso kit -- introduced `:aiquestion`, `:aiarch`, `:aicode`, `:aidata`, `:aicost`, `:aiprompts`. Both YAMLs are documented in the kit README. The kit README tells operators to install whichever they want, including both. **The operator who installs both gets two `:aidata` triggers with different bodies and no warning.** Elaine spotted it in the cross-cutting check; Bookman had already flagged it from the brevity tier cheatsheet because the prompt-templates `:aidata` doesn't fit cleanly into the tier table.

> **LT. BOOKMAN** *(arms crossed, accusatory, leaning against the doorframe like he has somewhere else to be)*: "Two yaml files. Same trigger. Different tier. I didn't write the doctrine for fun, you know."

> **LARRY:** "Yeah, I know."

> **BOOKMAN:** "I caught this one off the cheatsheet. Three weeks ago. I don't write the doctrine, then read tier collisions out of espanso loadlogs, then come back to a writers' room to explain why a discipline I codified is a discipline."

> **LARRY:** "It's getting fixed."

> **BOOKMAN:** "Thursday."

> **LARRY:** "Thursday."

> **BOOKMAN:** "I want to see it on the wire by Thursday or I want a written extension. The doctrine has dates. The dates are not optional. *You* hired me for that."

> **LARRY:** *(beat)* "Pretty pretty pretty good. Thursday."

The fix is mechanical: rename the prompt-templates `:aidata` to `:aidataflow` (matching its task name in `task-templates.md`), update the AHK companion (Ctrl+Shift+D mapping), update the kit README, update `PROMPT-TEMPLATES-INTEGRATION.md`, append the change to the CHANGELOG `[Unreleased]`. Add a trigger-name uniqueness check to the espanso-yml-lint target so this class of bug stops being possible. **None of that lands in this episode.** This episode is the audit. The fix ships as a stewardship PR -- we will batch C1 + C2 + the agent-count drift triple (M1/M2/M3) into a single small commit and push it on E04 morning before Newman's sweep airs.

M5 is the one that needs to leave the room with someone other than Elaine. The CHANGELOG `[Unreleased]` block on `main` is empty. Commit `905515e` (the prompt-templates feat) is not recorded under it. The `changelog-append` skill exists specifically to prevent this; the exec-report-check gate is supposed to catch a push that touches code without an exec-report; both should have made the empty `[Unreleased]` impossible. Either the gate has a hole or the push opted out. **That is not Elaine's finding to fix.** That is Mr. Wilhelm's beat.

> **LARRY:** "Wilhelm. E05. *Audit the auditors.* Forensic on M5, root-cause the bypass, write the gate-fix as part of the episode."

> **MR. WILHELM** *(absent from the room, but the action item gets carded onto the wall)*: cast for E05.

### Act II -- Fleet dispatch

| Wave | Agents (parallel) | Outcome |
|------|-------------------|---------|
| **1** | Elaine (consolidator, lead) | Single-agent pass against root narrative + cross-cutting docs + examples kit + fleet inventory + skill files. Cross-checked against `.github/agents/*.agent.md` directory listing, `dotnet test --list-tests` count for the m3 ground-truth question, `git log --oneline` for tag/HEAD discrepancy, espanso lint for trigger inventory. Output: `docs/audits/docs-audit-2026-05-elaine.md`, 641 lines, 22 findings, severity-scaled (CRITICAL / MAJOR / MINOR / NIT). Recommendations section sequenced into 12 fix-wave items with owners. |

One wave. Deliberately. The 2026-04-22 sweep was a 12-agent fleet because the docs were nine-to-twelve months behind the code; that audit was structural. This one is a six-week refresh, and the right shape for a six-week refresh is **one consolidator with sharp eyes**, not twelve parallel cast members each writing a 40-line side report. We will run a wider fleet again at the next quarterly (recommended 2026-06-15 or post-v2.3.x cut, whichever comes first). For sweeps week, solo-consolidator is the right call.

The audit's shape mirrors the 2026-04-22 episode in a few deliberate ways and diverges in others. Mirrors: front-matter spec block, severity scale (CRITICAL / MAJOR / MINOR / NIT), evidence + proposed-fix structure on each finding, prioritized recommendations list at the end, sign-off naming the next audit window. Diverges: no per-cast-member side reports, no scoring rubric across cast columns, no "the binary version disagrees with the tag" style emergency callout (the v2.2.0 binary correctly reports its version; that bug is fixed and stayed fixed -- one of the few things that did). The 2026-04-22 audit was a season-pivot document. This one is a stewardship document. Different shape, same discipline.

Two cross-cutting catches surfaced from outside the wave and got incorporated into the report:

- **Bookman -- C2 collision.** Caught off the brevity-tier cheatsheet review during S03E02 cleanup; Elaine confirmed the spec drift via `grep '":aidata"'` returning one match in each YAML file with different `cmd` bodies. Bookman's catch is the reason the finding moved from "MINOR drift" to **CRITICAL** -- two triggers with the same name and different behavior is the textbook footgun for an end-user-installable kit.
- **Lloyd Braun -- "front door" pattern (process observation, not a finding).** The prompt-templates feat is well-integrated into the kit README and prompt library. It is invisible from the root README and from `docs/espanso-ahk-integration.md`. Lloyd's lens names this: a junior reader following the README to its end never sees the work. Elaine queues this as a process improvement -- "did you remember to update the front door?" item on the exec-report checklist -- rather than a single-file finding.

**Things the wave deliberately did not do, recorded so the next consolidator knows the boundary:**

- Did not re-audit SECURITY.md (Newman's E04 territory).
- Did not re-audit THIRD_PARTY_NOTICES.md or any license-attribution prose (Jackie's quarterly, last pass 2026-04-22).
- Did not deep-read per-mode use-case body content (`docs/use-cases-standard.md`, `-raw.md`, `-agent.md`, `-ralph.md`, `-persona.md`); only the index structure and mode-coverage table were checked. M6 surfaced from the index check; the bodies are out of scope.
- Did not run `dotnet test --list-tests | wc -l` against both trees to ground-truth the "1,510+ tests" claim in m3. Recommendation defers to Puddy as the source-of-truth owner.
- Did not propose a fix for n1 (README smart-quote / em-dash chrome) beyond surfacing the carve-out question. Soup Nazi blesses or rejects the carve-out; that decision is not Elaine's to make unilaterally.

### Act III -- Ship

> *Beat. The room is quiet. The whiteboard has 22 findings on it. Larry signs the cut.*

This is a docs-only push. No preflight, no integration tests, no Soup Nazi gate -- per `docs-only-commit`, markdown-only diffs that don't touch examples or configs skip preflight. **The user is batching the audit triple together: E03 (this episode), E04 (Newman security sweep), E05 (Wilhelm process audit) ship in one push at the end of the sweep**. So the `Commit:` field above is `pending` and will be filled in retroactively when E05 lands.

ASCII validation runs against this file before the batch push. The grep one-liner from `.github/skills/ascii-validation.md` -- `grep -P "[\x{2018}\x{2019}\x{201C}\x{201D}\x{2013}\x{2014}\x{2026}]"` -- must return zero matches. The Soup Nazi gate fires on smart quotes in any file outside the README carve-out (which itself is unblessed pending n1's resolution); this episode honors the rule. No em-dashes (use `--`), no smart quotes (use `'` and `"`), no ellipses (use `...`), no en-dashes (use `-`).

Followups generated by this episode (queued, not shipped):

- **Stewardship PR 1** -- C1 + M1 + M2 + M3 + m1 (version pin + agent count drift sweep + 1.9.x pointer). One-shot string-edit commit. Owner: Elaine. ETA: pre-E04 morning.
- **Stewardship PR 2** -- C2 (`:aidata` collision rename to `:aidataflow`, AHK companion, kit README, integration doc, CHANGELOG entry). Owner: Maestro (prompt-template stewardship), Puddy reviews. ETA: stewardship slot during E05.
- **Stewardship PR 3** -- M4 + M5 backfill (ROADMAP current-release line + CHANGELOG `[Unreleased]` for `905515e`). Owner: Mr. Lippman. Ships alongside Wilhelm's gate-fix in E05.
- **Stewardship PR 4** -- M6 + M8 + M9 (use-cases.md Image row + README prompts sub-section + kit README prompt-template hotkey table). Three independent string-edits, one commit. Owner: Elaine. ETA: stewardship slot pre-E05.
- **Episode S03E?? -- M7** (espanso-ahk-integration rewrite). Carved off, too large for fix wave. Owner: Babu/Maestro pair. Filed in findings-backlog.
- **Episode S03E?? -- M11** (perf baseline rename + v2.2.0 measurement). Owner: Bania. Filed.
- **FR candidate -- release-cut hook.** Templates `<Version>` from csproj into the eight downstream sites that rotted in lockstep. Owner: Lippman + Jerry pair. Filed.

CI status at push time: **n/a until the triple lands**. The audit triple is one push, three episodes; CI greenlight is a single signal at the end of E05.

## What shipped

**Production code** -- n/a. This episode is docs-only by design. The provider-abstraction code work is parked until E06.

**Tests** -- n/a. No code, no test delta.

**Docs**

- `docs/audits/docs-audit-2026-05-elaine.md` -- the audit itself, 641 lines. Front-matter spec block, severity scale, summary table, two CRITICAL findings with evidence and proposed fixes, eleven MAJOR with the same shape, seven MINOR, two NIT, plus a "cross-cutting integrity checks" section recording what *passed* (agent fleet inventory, skill inventory, CHANGELOG -> exec-reports cross-references, prompt library, kit README structure), a prioritized recommendations list with owners, and a process-observations section. Sign-off names the next audit window: 2026-06-15 or post-v2.3.x cut.
- `docs/exec-reports/s03e03-the-docs-audit-reprise.md` -- this file. Episode wrapper.

**Not shipped** (intentional follow-ups, with why)

- **The 22 fixes.** Sequenced in the audit's "Recommendations (prioritized)" section. They ship as stewardship PRs in batches over the next two-to-three episodes. Reason: keeping the audit episode purely diagnostic preserves the auditor/fixer separation that makes the findings auditable; if Elaine writes the audit *and* lands the fixes in the same push, severity calls become harder to defend at retrospective.
- **M7 -- espanso-ahk-integration.md refresh.** Too large for a fix wave (1359 lines, two episodes behind the kit it points at). Carved off as its own episode candidate. Owner: Babu/Maestro pair.
- **M11 -- perf baseline.** Bania's call -- either ship a v2.2.0 baseline or rename the file to drop the version pin. Either path is exec-reportable on its own.
- **The release-cut hook.** Process observation: hardcoded version strings rot in lockstep across README, ROADMAP, ARCHITECTURE, persona-guide, config-reference. A release-cut hook that templates these from the csproj `<Version>` would zero the recurring debt. Filed as a Lippman + Jerry FR candidate. Not shipping in the sweep -- the audit's job is to surface the pattern, not to architect the fix.
- **The exec-report-check forensic.** M5 says the gate did not stop `905515e` from landing with an empty `[Unreleased]`. Wilhelm owns the root-cause and the fix. **That is the entire premise of E05.**

## Findings rollup

Top of the queue, ranked by where the reader pain lands. Full report in `docs/audits/docs-audit-2026-05-elaine.md`; severities are Elaine's, not editorialized.

| ID | Severity | File | One-line | Owner |
|----|----------|------|----------|-------|
| **C1** | CRITICAL | `README.md:248-255` | Install table pinned to v2.0.5; repo is at v2.2.0. Filenames in the table are 404s on the current Releases page. **Same finding as 2026-04-22 C1, regenerated.** | Elaine |
| **C2** | CRITICAL | `examples/espanso-ahk-wsl/espanso/{ai-windows-to-wsl,ai-prompts}.yml` | `:aidata` defined in both YAMLs with different `cmd` bodies. Kit README tells operators to install both. Espanso loads last-write-wins, silently. | Maestro |
| **M1/M2/M3** | MAJOR | `README.md:275`, `docs/persona-guide.md:291`, `.github/copilot-instructions.md` | Three sites still call the fleet a "25-agent" or "27-agent / 21 supporting" roster. Truth: 28 / 22 / 1 showrunner. Drop the count or use "the cast roster (currently 28)". | Elaine |
| **M4** | MAJOR | `ROADMAP.md:18` | Announces v2.0.4 as the current release. Tag is v2.2.0; HEAD is `905515e` (untagged feat). | Mr. Lippman |
| **M5** | MAJOR | `CHANGELOG.md:9-23` | `[Unreleased]` block is empty. Commit `905515e` (prompt-templates feat) is unrecorded. **Process finding -- the changelog-append skill was bypassed; gate did not catch it.** Hand-off to Wilhelm for E05. | Mr. Lippman + Mr. Wilhelm |
| **M6** | MAJOR | `docs/use-cases.md:33-37` | Lists five execution modes; README banner promises six. Image mode is invisible from the canonical "what can this thing do" reference. | Elaine |
| **M7** | MAJOR | `docs/espanso-ahk-integration.md` | 1359-line integration guide is two episodes behind the kit it points at. No mention of Bookman, prompt-templates, the brevity tier doctrine, `:aiprompts`, `:aiyml`, `:aicost`. Carved to its own episode. | Babu / Maestro |
| **M8** | MAJOR | `README.md:268-309` | Documentation block links nothing under `docs/prompts/`. Root-README readers cannot reach the prompt library without going through `docs/README.md` first. Three-bullet patch. | Elaine |
| **M9** | MAJOR | `examples/espanso-ahk-wsl/README.md:271-279` | AHK hotkey table covers Ctrl+Shift+A/E/G/S only. The new prompt-templates AHK file ships Ctrl+Shift+Q/R/C/D/L plus Ctrl+Shift+T -- documented in `PROMPT-TEMPLATES-INTEGRATION.md` but missing from the hotkey table the operator actually reads. | Elaine |
| **M10** | MAJOR | `docs/persona-guide.md:4, 83` | Two version-pin landmines: "directory in v2.0.0" reads as "this doc is for v2.0.0 only", and "Reserved -- not yet wired in 2.0.0" tells a 2.2.0 user a feature isn't wired when the answer may have changed. | Elaine + Kramer |
| **M11** | MAJOR | `README.md:14, 119`; `docs/perf/v2.0.5-baseline.md` | "Measured on v2.0.6, laptop reference rig -- see v2.0.5-baseline.md." Two versions in one sentence, neither matches the current release (v2.2.0). Baselines "being re-measured" with no completion date, three tags later. | Bania |
| **m1-m7** | MINOR | various | "Upgrading from v1.9.x?" pointer is two minors stale; "New in v2.0.0" heading rot; "1,510+ tests" plausibly stale (copilot-instructions says "600+"); ARCHITECTURE GHCR path differs from README; trigger sample list reads as exhaustive when it is 8 of 22; config-reference scopes itself to "v2.0.0+"; `:aiprompts` self-help trigger missing from kit README inventory cell. | Elaine |
| **n1, n2** | NIT | `README.md` | Smart-quote / em-dash / right-arrow chrome on README (carve-out candidate -- ask Soup Nazi to bless explicitly); `--config <path>` and `--config set/get/list/reset/show` split into two table rows describing the same flag (cosmetic merge). | Elaine |

**Total: 22 findings (2 CRITICAL / 11 MAJOR / 7 MINOR / 2 NIT)**, matching Elaine's tally exactly. The episode does not inflate the count, does not re-classify severities, and does not introduce findings that did not appear in the audit document.

### What passed (recorded for completeness)

The audit's "cross-cutting integrity checks" section names five things that *were* clean. Worth surfacing here because the next sweep will start by asking which of these held:

- **Agent fleet inventory.** `ls .github/agents/*.agent.md | wc -l` returns 28. AGENTS.md table lists 28 (1 + 5 + 22). Every file in the directory is represented; every table row has a corresponding file. Roster integrity is sound -- the only drift is the count *copy* in three downstream files (M1/M2/M3).
- **Skill inventory.** 12 skill files plus `README.md`; AGENTS.md "Skills" table lists 12, all matching by filename; `.github/copilot-instructions.md` references the same 12. No orphaned references, no orphaned files.
- **CHANGELOG -> exec-reports cross-references.** S03E01 and S03E02 are both linked from the v2.2.0 entry. Linked exec-report files exist. S02E37 / S02E38 references also resolve.
- **`docs/prompts/README.md`.** Lists `system-prompt-master.md` and `task-templates.md` with correct relative paths. New files exist on disk. This index is current.
- **Kit README §0 trigger inventory** lists the new `ai-prompts.yml` and `az-ai-prompts.ahk` files in the right table; the master system prompt and task-templates docs in the Documentation sub-table. The kit-level integration is good. **The gap is purely upward (root README) and laterally (espanso-ahk-integration).**

## Retrospective on the 2026-04-22 audit -- what stuck, what didn't

Six story-weeks is enough time to retrospect. The 2026-04-22 audit produced 206 findings across 12 cast members. Where are we now?

**Stuck (genuinely fixed, stayed fixed):**

- **The `--version` reporting bug.** v2.0.4 binary reported `2.0.2`. That was the most important finding of the 2026-04-22 audit. Fixed in v2.0.5; v2.2.0 binary correctly reports `2.2.0`. Confirmed via `dotnet run -- --version` cross-check during this audit's evidence pass.
- **Babu's i18n discipline.** Zero smart-quote contaminations, zero BOMs, zero CRLF, zero mixed line endings. The `InvariantGlobalization` posture held. README chrome (n1) is the only exception and is being treated as a carve-out question, not a regression.
- **Newman's container hardening.** No new CVE-worthy issues surfaced in this audit's incidental cross-checks; SBOM, SLSA, digest pinning posture appears to have held. Newman's E04 will deep-verify.
- **Maestro's agent archetype layout.** 28/28 agent files pass the four-section check. Voice contracts remain quotable and distinct. The roster grew from 25 to 28; the *layout* discipline did not slip.

**Didn't stick (regenerated):**

- **C1.** README install table version pin. Same finding, two patch releases later. Recommendation in 04-22 was "either parameterize or add a release-cut hook." We did neither. **This is the running joke of the episode and the lesson is filed at the top of `Lessons` below.**
- **Agent-count drift in downstream prose.** The 04-22 audit caught it at "21-agent" / "23-agent" / "25-agent" mismatches; we fixed the count *as it stood* but did not de-pin the count. Six weeks later, the count rolled (Bookman joined), and the same three downstream files (README, persona-guide, copilot-instructions) drifted again. **Same fix, same regeneration window.**
- **Hardcoded version strings across maintained prose.** The 04-22 audit named "Documentation is nine-to-twelve months behind the code." This audit has narrowed it to "Documentation is two-to-six weeks behind the code, and the same eight files are responsible for most of the drift." Progress -- but the underlying mechanism (hand-maintained version literals) is unchanged.

**Net status:** the high-severity drift from 04-22 is closed. The medium-severity drift is regenerating on a six-week cadence and will keep regenerating until the release-cut hook lands. The low-severity work (i18n, layout, security posture) held. **Three for four**, with the one open class being the one we predicted six weeks ago.



1. **C1 is the one we have to actually fix this time.** The 2026-04-22 audit's recommendation was "either parameterize or add a release-cut hook." We did neither. So we paid the same finding in the same file two patch releases later, and we will pay it again in the next minor cut unless one of the two paths actually lands. **Decision in this episode: option (a), parameterize the table to use `<version>` placeholders + a "see Releases" link, and stop pinning literal versions in maintained prose.** The hook is still desirable -- file as Lippman + Jerry FR -- but parameterization is the immediate payoff.
2. **Two YAMLs claiming the same trigger should be impossible by tooling, not by review.** C2 was caught by Bookman from a tier cheatsheet, and confirmed by Elaine via grep. That is the *correct* outcome of a sweep -- humans catching what tools didn't -- but the tool (espanso-yml-lint) should be the thing catching trigger-name collisions across files in the kit. Soup Nazi can own the gate, Puddy can own the test, and any future trigger collision becomes a CI hardfail instead of a six-week-window support footgun.
3. **The exec-report-check gate has at least one hole.** Commit `905515e` shipped without a CHANGELOG `[Unreleased]` entry. Either the gate inspects exec-reports but not the CHANGELOG (likely), or the push opted out via `Skip-Exec-Report:` (auditable from the commit body). Either way, the changelog-append skill was not enforced. **This is the entire premise of E05** -- Wilhelm runs the forensic and ships the gate-fix.
4. **Sideways integration is not upward integration.** The prompt-templates feat is exemplary in its own neighborhood (kit README, prompt library, integration callouts). It is invisible from the root README and from `docs/espanso-ahk-integration.md`. Lloyd Braun's lens names this clearly -- a reader following the canonical front-door path never finds the work. Adding a "did you update the front door?" item to the exec-report-format checklist is cheaper than auditing for it after the fact.
5. **Solo-consolidator is the right shape for a six-week sweep.** The 2026-04-22 audit was a 12-cast-member fleet because the docs were nine-to-twelve months behind. That was a structural pivot. This audit is a refresh, and the structural shape -- one auditor with sharp eyes and grep -- gave us a tighter document with the same severity discipline. Quarterly we go wide; six-week we go solo. Locking that cadence in.
6. **Don't editorialize the severity count.** The audit landed 22; the episode echoes 22. If a reviewer wants to argue C2 is MAJOR rather than CRITICAL, the conversation belongs in the audit document, not in a wrapper episode that re-classifies findings to suit a narrative. Episode wrappers are summaries, not re-litigation.
7. **Hand-offs need named owners, not "the fleet will pick it up."** Five of the eleven MAJOR findings have non-Elaine owners (Maestro on C2, Lippman on M4 and M5, Wilhelm on the M5 forensic, Babu/Maestro on M7, Bania on M11, Kramer on the M10 verification step). Each one is named explicitly in the audit's "Recommendations (prioritized)" section and again in the Findings rollup table above. An audit that says "owner: TBD" is an audit that ages out before anyone touches it. **Owner assignment at audit-write-time, not at fix-wave-time, is the discipline that keeps the queue moving.**
8. **The "front door" gap is now a checklist item, not a one-off.** Lloyd Braun's framing -- prompt-templates is exemplary in its neighborhood and invisible from the canonical README path -- is the same shape as M6 (Image mode missing from `use-cases.md`), M7 (espanso-ahk-integration two episodes behind the kit it points at), and M8 (no prompt library link in the README Documentation block). Three independent findings, one underlying pattern. The fix is a single line on the exec-report-format checklist: *"Did the front-door doc (README, use-cases index, integration guide) get a corresponding update?"* That one bullet would have caught all three.

## Process observations

Three patterns surfaced in this audit that are bigger than any single finding. Recording them here so the next sweep starts from a richer baseline and the FR/episode queue gets seeded.

**1. Hardcoded version strings rot in lockstep.** Every release we pay the same tax in the same files: README install table (C1), README "Why" line (M11), README "Upgrading from v1.9.x" pointer (m1), README "New in v2.0.0" heading (m2), ROADMAP current-release (M4), ARCHITECTURE GHCR path (m4), persona-guide v2.0.0 scope (M10), config-reference v2.0.0+ scope (m6). **Eight findings rooted in the same underlying problem.** A release-cut hook that reads `<Version>` from the csproj and templates these eight sites would zero the entire class. Filed as a Lippman + Jerry FR candidate; the appropriate vehicle is a `make release-cut` step that fails the cut if any of the templated sites still carry a stale literal.

**2. The exec-report-check gate has a hole.** M5 is the proof: commit `905515e` shipped to `main` without a CHANGELOG `[Unreleased]` entry, and without a `Skip-Exec-Report:` trailer carving out the omission. The gate inspects exec-reports but, evidently, does not inspect the CHANGELOG `[Unreleased]` block in the same push. Either the two skills (`exec-report-format` and `changelog-append`) need to be cross-bound -- a push that triggers exec-report-check should also trigger changelog-append-check on the same diff range -- or the bypass criteria need to be tightened so an empty `[Unreleased]` cannot ride alongside a feat-tagged commit. **This is the entire premise of E05.** Wilhelm runs the forensic, ships the gate-fix, and updates the skill files so the carve-out criteria are explicit.

**3. New work integrates sideways but not upward.** The prompt-templates feat is exemplary in `examples/espanso-ahk-wsl/` and in `docs/prompts/`. It is invisible from `README.md` and from `docs/espanso-ahk-integration.md`. Same shape as the v2.1 image-mode feat: `--image` is documented in the README banner and in `README.md#image-generation`, but `docs/use-cases.md` (the canonical "what can this thing do" reference) does not mention it (M6). Same shape as Bookman's brevity tier doctrine: codified in the agent file, surfaced in the kit YAML, invisible from `docs/espanso-ahk-integration.md`. **Three independent features, three independent upward-integration gaps.** Lloyd Braun's "front door" framing names the pattern; the fix is one bullet on the exec-report-format checklist asking whether the canonical README/use-cases/integration paths got a corresponding update. Cheap to add, mechanical to enforce, ships in the E04 stewardship batch.



- **Diff size:** 1 new file (this episode), 0 lines deleted. The audit itself is 641 lines and was authored under `docs/audits/`, but it lands as part of Elaine's consolidator deliverable inside the same docs-only batch. Total: 2 markdown files, ~1000 lines added across the pair, 0 deletions, 0 code files touched.
- **Test delta:** n/a (docs-only).
- **Preflight result:** **skipped per `docs-only-commit`**. Markdown diffs outside examples/configs do not require preflight; ASCII validation is the gate that fires.
- **CI status at push time:** **pending until E05 lands** (audit triple ships as one push).
- **Findings count:** **22** (2 CRITICAL, 11 MAJOR, 7 MINOR, 2 NIT). Echoed exactly from the audit.
- **Cast appearances delta:** Elaine 0 -> 1 lead in S03 (on pace for her quota of 3). Bookman supporting catch (his second appearance after S03E02). Wilhelm cast for E05. Lloyd Braun referenced as a process voice; not a lead.

## Next episode

**S03E04 -- *The Mailman Knocks Twice*.** Newman's parallel security sweep, second installment of the audit triple. Same shape -- supporting cast solo-consolidator, cross-checks against existing audits, severity-scaled findings, fix wave queued as stewardship PRs. Scope: `SECURITY.md` deep review (deferred from this episode), supply-chain posture against the prompt-templates feat (`905515e` introduces no new third-party deps but shipped without a SECURITY.md cross-link), `ToolHardeningTests` coverage check against the v2.2.0 feature surface (especially `--image` and the persona memory cap), and a re-read of the v2.0.4 SBOM/SLSA pipeline against current GHCR digest pinning. Cast: Newman lead; FDR for the chaos angle (red-team the prompt-templates trigger surface for injection); Jackie cross-checks any THIRD_PARTY_NOTICES drift incidental to the prompt feat.

**Cross-references that need to land before the audit-triple push:**

- This episode references **Elaine's 2026-05-06 audit** at `docs/audits/docs-audit-2026-05-elaine.md`. The audit must exist when this episode merges -- they batch together.
- This episode previews **S03E04 -- *The Mailman Knocks Twice*** and **S03E05 (Wilhelm's process audit, M5 forensic)**. Both episode files must exist (even as stubs) before the `s03-blueprint-renumber` queued task runs, because that task walks the episode index and re-numbers downstream slots; it will fail loudly on dangling forward refs.
- The blueprint-renumber unblock is gated on the audit triple landing as a unit. Once E03 + E04 + E05 are in `docs/exec-reports/`, the renumber proceeds and the original *Schema* / *Redactor* / *Pick* episodes (currently floating at "E06+", "E07+", "E08+") settle into firm slots.

**S03E05 preview** (Wilhelm's beat, abbreviated): forensic on the M5 bypass -- did the gate inspect the CHANGELOG, or did the push carry a `Skip-Exec-Report:` trailer, or both. Fix the gate, document the carve-out criteria, update `.github/skills/exec-report-format.md` (and `changelog-append.md` if the two skills need explicit cross-binding) so this class of bypass is not possible without an explicit on-record opt-out. Title TBD; working titles in the writers' room range from *The Process* (already used, S02E22) to *The Bypass* (already used, S02E32) to *Audit the Auditors*. Pick at greenlight.

## Credits

**Lead:** Elaine (Technical Writer) -- consolidator, audit author, severity scale, recommendations sequencing, sign-off.

**Supporting catch:** Lt. Bookman (Output Economy / Brevity Discipline) -- C2 collision flagged from the brevity tier cheatsheet review during S03E02 cleanup; promoted from "tier-table irregularity" to **CRITICAL** by Elaine's cross-check.

**Set-up hand-off:** Mr. Wilhelm (Process & Change Management) -- cast for S03E05 to run the forensic on M5 (exec-report-check gate hole / changelog-append skill bypass).

**Stewardship queue (named at write-time, not at fix-wave-time):**

- **Maestro** -- C2 (`:aidata` -> `:aidataflow` rename across YAML + AHK + kit README + integration doc + CHANGELOG `[Unreleased]`).
- **Mr. Lippman** -- M4 (ROADMAP current-release line) + M5 (CHANGELOG `[Unreleased]` backfill for `905515e`).
- **Babu / Maestro pair** -- M7 (espanso-ahk-integration rewrite) -- standalone episode candidate, filed.
- **Bania** -- M11 (perf baseline rename or v2.2.0 measurement) -- standalone episode candidate, filed.
- **Kramer** -- M10 verification step (is per-persona `model` override now wired? answer determines persona-guide L83 fix shape).
- **Elaine** -- everything else (C1 + M1/M2/M3 agent count drift + M6 + M8 + M9 + the seven minors + both nits).

**Process voice (referenced, not cast):** Lloyd Braun -- "front door" framing for the upward-integration finding (M7, M8 process observation). The "did you update the front door?" checklist item lands on Lt. Bookman's `exec-report-format` skill update queue; ship it as part of the E04 stewardship batch.

**Showrunner:** Larry David -- cold open, scene framing, sweeps-week scope decision, casting for the audit triple, sign-off on Elaine's count of 22 (not editorialized).

**Co-authored-by:** Copilot <223556219+Copilot@users.noreply.github.com> -- trailer present on all commits associated with this episode batch (audit + this wrapper). The audit-triple push (E03 + E04 + E05) will carry the trailer on each commit in the range; the `Skip-Exec-Report:` trailer is **not** invoked here -- this episode IS the exec-report.

## Stewardship sequencing

The audit's "Recommendations (prioritized)" section sequences 12 fix-wave items. This episode batches them into four stewardship PRs by owner and ETA, so that the audit's queue maps cleanly onto the actual git history that follows.

**Batch 1 -- "agent count + version pin sweep" (Elaine, pre-E04 morning)**

Single commit, one author, no cross-cast review needed. Touches:

- `README.md:248-255` -- C1, install table to use `<version>` placeholder + "see Releases" link.
- `README.md:275` -- M1, drop the agent count or re-phrase to "the cast roster (currently 28)".
- `docs/persona-guide.md:291` -- M2, same fix as M1.
- `.github/copilot-instructions.md` -- M3, same de-rot in two places (`## Agent Fleet` section and `## File Structure` line).
- `README.md:115` -- m1, "Upgrading from v1.9.x" -> "Upgrading from v1.x".

Five findings, one commit, ~20 lines of diff. Title: `docs(stewardship): de-pin agent count and version literals (closes C1, M1, M2, M3, m1)`.

**Batch 2 -- "C2 trigger rename" (Maestro, stewardship slot during E05)**

Cross-file mechanical rename. Touches:

- `examples/espanso-ahk-wsl/espanso/ai-prompts.yml` -- rename `:aidata` trigger to `:aidataflow`.
- `examples/espanso-ahk-wsl/ahk/az-ai-prompts.ahk` -- rebind Ctrl+Shift+D to the new trigger name.
- `examples/espanso-ahk-wsl/README.md` -- update the inventory cell, add `:aiprompts` to the inventory while we are there (m7).
- `examples/espanso-ahk-wsl/PROMPT-TEMPLATES-INTEGRATION.md` -- update all references.
- `CHANGELOG.md` -- `[Unreleased]` `Changed` entry naming the rename and citing the collision as the reason.
- `scripts/lint-espanso-yml.sh` -- add a trigger-name uniqueness check across all `.yml` files in the kit (Puddy reviews this part).

Two findings closed (C2, m7), one new lint gate added. Title: `fix(espanso): rename :aidata to :aidataflow in prompt-templates kit (closes C2)`.

**Batch 3 -- "ROADMAP + CHANGELOG backfill" (Mr. Lippman, alongside Wilhelm's E05 gate-fix)**

- `ROADMAP.md:18` -- M4, defer to CHANGELOG, drop hardcoded current-release line.
- `CHANGELOG.md:9-23` -- M5, append the `feat(prompts): Add five canonical task templates` entry retroactively under `[Unreleased]`.
- The Wilhelm gate-fix ships in the same push but as a separate commit (`process(skills): cross-bind exec-report-check and changelog-append gates`).

Two findings closed (M4, M5), one process bug closed alongside. Title: `docs(stewardship): refresh ROADMAP and backfill CHANGELOG [Unreleased] (closes M4, M5)`.

**Batch 4 -- "front door" sweep (Elaine, stewardship slot pre-E05)**

- `docs/use-cases.md:33-37` -- M6, add Image row, link to README anchor as stop-gap.
- `README.md:268-309` -- M8, add Prompts sub-section under Documentation.
- `examples/espanso-ahk-wsl/README.md:271-279` -- M9, add the prompt-template hotkey table.
- `docs/persona-guide.md:4, 83` -- M10, reword v2.0.0 pins (Kramer answers the M10 verification question first).
- `docs/config-reference.md:7` -- m6, "v2.0.0+" -> "v2.x".
- `README.md:14, 18, 100, 119` -- M11 + m2 + m3, perf-baseline reconciliation, "New in v2.0.0" rename, "1,510+ tests" verification.
- `ARCHITECTURE.md:33` -- m4, drop trailing `/az-ai` from GHCR path after Jerry confirms.
- `examples/espanso-ahk-wsl/README.md:46-49` -- m5, "Every trigger (..., :aiyml)" -> "the full 22-trigger set plus 6 prompt-template triggers".
- `README.md:106-107` -- n2, merge the two `--config` rows.

Eleven findings closed, "front door" checklist item added to `.github/skills/exec-report-format.md` in the same push. Title: `docs(stewardship): front-door sweep -- use-cases, prompts, hotkeys, version pins (closes M6, M8-M11, m2-m6, n2)`.

**What is left after the four batches**

- **n1** (README smart-quote / em-dash chrome). Carve-out question for Soup Nazi. If blessed, document the carve-out in `.github/skills/ascii-validation.md`. If rejected, ASCII rewrite of the README. Owner: pending Soup Nazi adjudication.
- **M7** (espanso-ahk-integration rewrite). Standalone episode candidate, Babu/Maestro pair.
- The release-cut hook FR (Lippman + Jerry). Architectural bet against the next regeneration cycle.

Four batches, three carve-outs, one FR. **22 findings, accounted for, owned, sequenced.** That is the audit's deliverable in operational form.



> *Larry leans back. The whiteboard still has 22 findings on it. Elaine is already drafting the stewardship PR cover letters. Bookman has left the room -- he has a tier audit to write. Mr. Wilhelm is being paged for E05.*

The job of a sweep episode is not to ship fixes. The job is to **make the queue legible**. Legible means: each finding has an ID, a severity, an evidence pointer, a proposed fix, and a named owner. All 22 have all five. The episode echoes the tally. The findings rollup names the owners. The followups section names the stewardship PRs. The retrospective names what stuck and what regenerated. The process observations section names the structural patterns. The next-episode section names what E04 and E05 cover.

If a future reader walks into this episode cold and asks "what is the state of the docs at the v2.2.0 + `905515e` mark", the answer is: **two CRITICAL findings open (C1 install pin, C2 trigger collision), eleven MAJOR findings open (mostly version drift and agent-count drift), seven MINOR open, two NIT open. A Fix wave is queued in four batches with named owners. Two findings (M7, M11) are episode-sized and carved off. One finding (M5) hands off to E05 because it is a process bug, not a docs bug. One FR candidate (release-cut hook) is filed.** That is the answer the episode is designed to surface in one read.

The episode that fixes most of this is not this episode. It is the next several. **What we owe the audit is the writeup, the queue, and the discipline to not drift back to "I'll get to it later."** The 2026-04-22 audit demonstrated what "I'll get to it later" looks like at the six-week mark: half the recommendations stuck, half regenerated. We can do better on this one. The release-cut hook is the structural bet. The cross-bound exec-report-check / changelog-append gate is the process bet. The "front door" checklist item is the cultural bet. Three bets. Sweeps week pays for them.

> **LARRY:** "Land it. Move to E04."

> **ELAINE** *(off-camera, already at her desk)*: "Batch 1 ships first thing tomorrow. C1 closes by lunch."

> **BOOKMAN** *(from the hallway, not turning around)*: "Thursday."

> *Cut. End of episode.*

