# Season 3 -- *Writers' Room*

> *Backfilled mid-season per Wilhelm's W-02 finding (`docs/audits/audit-process-meta-2026-05.md`): the retrospective-cadence handoff from the S02 finale never produced this artifact. This file is the fix-forward. It will be kept current going forward as episodes land.*

**Status:** backfilled 2026-05 (mid-season). Source of truth for slate is [`s03-blueprint.md`](s03-blueprint.md); progress snapshot is [`s03-progress-2026-05.md`](s03-progress-2026-05.md).

## Season pitch

End of S02, `azure-openai-cli` is an Azure-OpenAI-native single-shot binary -- excellent at one thing on one provider. S03 is the pivot from *being a tool* to *being a category entrant*: introduce a provider-abstraction seam, ship at least one non-Azure cloud and one local OpenAI-compatible runtime through it, and prove the LOLBin / single-binary / ASCII-clean ergonomics survive the journey. End-state: same binary, three providers, named profiles in the FR-014 preferences file. The seam, not the intelligence -- automatic routing, cost-aware fallback, MCP, and multimodal stay in S04 and beyond. (Paraphrased from `s03-blueprint.md` §Theme statement.)

## Episodes shipped

| Slot | Title | Lead | Verdict | Date |
|------|-------|------|---------|------|
| S03E01 | *The Yada Yada Strikes Back* | Kramer | shipped (audit clean after wave 9) | 2026-05 |
| S03E02 | *The Library Cop's Word Limit* | Lt. Bookman | shipped (tier doctrine + 3 triggers) | 2026-05 |
| S03E03 | *The Docs Audit, Reprise* | Elaine | YELLOW (22 findings: 2C / 11M / 7m / 2n) | 2026-05 |
| S03E04 | *The Mailman Knocks Twice* | Newman | RED (F-1 CRITICAL + F-2 HIGH; patched same sweep, commit `c25ca38`) | 2026-05 |
| S03E05 | *The Auditor's Auditor* | Mr. Wilhelm | YELLOW (50 percent follow-through on prior audit findings) | 2026-05 |

## Active findings

Tracked in-line until a season-wide findings backlog ships (per `findings-backlog` skill; S03 backlog file not yet seeded).

- **W-02 (audit-process-meta-2026-05):** S03 writers' room missing -- *closed by this file*.
- **C1 (docs-audit-2026-05-elaine):** README install table pinned to v2.0.5 -- *closed in post-sweep cleanup batch (this push)*.
- **M5 (docs-audit-2026-05-elaine):** CHANGELOG `[Unreleased]` empty despite shipped commits -- *closed in post-sweep cleanup batch (this push)*.
- **C2 (docs-audit-2026-05-elaine):** `:aidata` trigger collision between unification set and prompt-templates set -- **open**, ownership Kramer / Elaine.
- **F-1 / F-2 (security-v2.1-post-prompts):** bash injection in `ai-prompts.yml` -- *patched in working tree (commit `c25ca38`); pending Newman re-audit, planned `docs/audits/security-v2.1.1-reaudit.md`*.
- **Audit follow-through gap (audit-process-meta-2026-05):** 50 percent of sampled top-3 findings from 2026-04-22 docs-audit set still unactioned -- **open**, ownership Mr. Wilhelm + Mr. Pitt.

## Arc status

Per [`s03-progress-2026-05.md`](s03-progress-2026-05.md) (Mr. Pitt, mid-season): two episodes shipped on the blueprint (E01, E02) plus three sweeps-week audit episodes (E03-E05) inserted as Arc 1.5. The provider-abstraction spine (Arc 1, originally E01-E05) shifts to E06-E07; downstream arcs renumber accordingly per `s03-blueprint.md` §"27-episode slate (24 planned + 3 sweeps-week shipped)". Velocity posture: *at-risk on velocity, on-track on theme, drifting on lead-cast quotas* -- the sweep was unplanned but corrective, not a re-pitch.

## Open questions for next mid-season checkpoint

- **E06 dispatch:** does *The Schema* (preferences.json v1, FR-014) ship before or after the Newman re-audit lands? If after, we eat another sweep slot; if before, we risk a third audit pass against unfinished schema work.
- **Cast quotas:** Costanza, Jerry, and the supporting bench have not led an S03 episode yet. Per `writers-room-cast-balance` audit cadence, E06 / E12 / E18 are the mid-season checkpoints -- E06 should slot Costanza or Jerry as lead.
- **Findings backlog file:** the `findings-backlog` skill defines the format but the S03 backlog file has not been seeded. Owner unassigned. Candidate: Mr. Wilhelm (continuing from the meta-audit).
- **Audit-triple close-out:** Newman's re-audit (planned `docs/audits/security-v2.1.1-reaudit.md`) has no reserved episode slot. Either annex onto E05 as an addendum or claim the next available off-roster slot.

## Cross-references

- [`s03-blueprint.md`](s03-blueprint.md) -- canonical 27-episode slate
- [`s03-progress-2026-05.md`](s03-progress-2026-05.md) -- mid-season exec progress report
- [`s02-writers-room.md`](s02-writers-room.md) -- prior-season structural reference
- [`docs/audits/audit-process-meta-2026-05.md`](../audits/audit-process-meta-2026-05.md) -- W-02 source finding
- [`.github/skills/writers-room-cast-balance.md`](../../.github/skills/writers-room-cast-balance.md) -- audit cadence
- [`.github/skills/findings-backlog.md`](../../.github/skills/findings-backlog.md) -- finding format spec
