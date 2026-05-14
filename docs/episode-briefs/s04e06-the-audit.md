**Status:** GREENLIT (Mr. Pitt, 2026-05-21; filmed in this commit -- audit doc landed at `docs/audits/s04-midseason-cast-balance.md`)

**Date:** 2026-05-21
**Lead:** Mr. Pitt -- mid-season cast-balance audit, findings authorship
**Co-lead:** none (single-agent episode)
**Support:** none in Wave 1; Larry David reviews the cut before merge
**Dependencies:** S04E01 *The Registry* (GREENLIT, on `main`); S04E02 *Embedded Cards* (filming); S04E03 *The Capabilities* (GREENLIT); S04E04 *Reading Room* (GREENLIT); S04E05 *The Picker* (GREENLIT); off-roster S04off1 *The Translation* (aired). All five regular episodes and the one off-roster special in the E01..E05 window must be in the commit log before the audit can cite them.

# S04E06 -- The Audit

> Log line: Five episodes in. Twenty-four-episode season. The showrunner
> needs to know who has been on screen, who has been benched, and which
> of the five casting rules has quietly slipped. This is the mid-season
> physical. No code changes hands. One markdown file lands.
> Recommendations -- not rewrites -- shape E07 through E12.

---

You are filming **S04E06 *The Audit*** for `azure-openai-cli`. Working
directory: `/home/tweber/tools/azure-openai-cli`. Branch: `main`.

---

## The pitch

The `writers-room-cast-balance` skill mandates a mid-season audit at
E06, E12, and E18 of every 24-episode season. S04 is the first season
where the skill exists as a citable artifact, so this is the first
audit run under it. The skill names Mr. Pitt as the auditor.

The deliverable is one markdown file: `docs/audits/s04-midseason-cast-balance.md`.
It enumerates every commit that landed between the start of E01 Wave 1
and the close of E05 Wave 3, attributes each commit to a leading agent
(based on the brief's Lead field plus commit subject), counts
LEAD / CO-LEAD / SUPPORT / REVIEW appearances per agent, runs the five
casting rules against the distribution, and surfaces concrete rebalance
recommendations for E07 through E12.

This episode does **not** rewrite any future brief. It only audits the
past and recommends. The rewrites (if any are warranted) are E07+
scope and ride in their own briefs. The audit's job is to produce
findings, not patches.

---

## Scope (in)

- `docs/audits/s04-midseason-cast-balance.md` (NEW) -- single deliverable.
- Window: first commit of E01 Wave 1 to last commit of E05 Wave 3
  (inclusive on both ends; airing order, not plan order).
- Commit attribution table: each commit SHA (short), subject, episode
  it filmed, leading agent, supporting agents named in the body.
- Cast-balance table: every agent in the roster (28 rows total --
  1 showrunner, 5 main, 22 supporting) with columns LEAD / CO-LEAD /
  SUPPORT / REVIEW and a totals column.
- Five casting rules walked, each ticked (PASS), warned (WARN), or
  failed (FAIL). Rule numbers and names match the skill exactly.
- At least three concrete rebalance recommendations targeting E07
  through E12 (the remaining first-half slate). Each recommendation
  names the episode slot, the change, and the rule it satisfies.
- Off-roster special S04off1 *The Translation* counted under whatever
  convention Open Questions resolves (see below; assume "counts as a
  lead appearance for the named lead, does not contribute to the
  multi-lead floor" until ruled otherwise).
- If any rule fails, a finding is filed at
  `docs/findings/F-CAST-S04MID-NN.md` per the `findings-backlog`
  skill format.

## Scope (out)

- Rewriting any existing or planned brief. The audit recommends; E07+
  briefs may adopt the recommendations in their own dispatches.
- Cross-season trend analysis vs S03 (deferred; see Open Questions).
- Re-casting Wave 1 of an in-flight episode. Once a brief is GREENLIT,
  the audit cannot retroactively swap its lead -- it can only
  recommend a corrective episode.
- Skill or process rewrites. If the audit reveals the skill itself is
  flawed, the fix is a separate episode (e.g., S04off3) with Mr.
  Wilhelm and the Soup Nazi co-leading process change.
- Code, tests, CI, release artifacts. Zero code touched.

---

## Acceptance criteria

1. `make ascii-check && make docs-lint` exits 0.
2. The file `docs/audits/s04-midseason-cast-balance.md` exists and is
   the only file added by the audit commit.
3. Every commit between E01 Wave 1 start and E05 Wave 3 close
   (inclusive) is cited by short SHA and subject in the audit's
   attribution table. Zero gaps -- run `git log --oneline <start>..<end>`
   and confirm the line count matches the audit table row count.
4. Every cited commit names a leading agent. Commits whose lead is
   ambiguous are flagged and dispositioned in a "Disputed attributions"
   subsection.
5. The cast-balance table covers all 28 agents (1 showrunner, 5 main,
   22 supporting). Agents with zero appearances appear in the table
   with explicit zeros, not omitted.
6. The five casting rules are each evaluated with a verdict of PASS,
   WARN, or FAIL. The rule numbers and names match
   `.github/skills/writers-room-cast-balance.md` exactly.
7. At least three rebalance recommendations are written. Each names
   (a) the target episode slot in E07..E12, (b) the proposed change,
   (c) the rule the change addresses, and (d) the agent who would be
   added or benched.
8. If any of the five rules returns FAIL, a finding file
   `docs/findings/F-CAST-S04MID-NN.md` exists for it, in the canonical
   `findings-backlog` format with state `OPEN`.
9. ASCII-only: no smart quotes, en-dash, or em-dash anywhere in the
   audit file. The grep one-liner from `.github/skills/ascii-validation.md`
   returns zero matches.
10. Markdownlint clean: dash bullets only (no asterisk bullets,
    MD004 = `dash`), trailing newline present, no level-1 heading
    duplication.
11. The audit recommends Mr. Pitt's own rebalance. He has led S04 W2
    (this brief is his second consecutive S04 lead-tier appearance
    after the S05/S06 plan dispatch) and is about to lead this E06
    audit. The back-to-back risk is acknowledged in-document and one
    of the three+ recommendations explicitly benches him from any
    lead role in E07-E08.

---

## Dispatch plan

| Wave | Agent | Files touched | Output |
|------|-------|---------------|--------|
| 1 | **Mr. Pitt (lead)** | `docs/audits/s04-midseason-cast-balance.md` (NEW); optionally `docs/findings/F-CAST-S04MID-NN.md` files if any rule FAILs | The audit doc + any findings. Single agent, single file (plus per-FAIL finding files). No other agent edits in Wave 1. |
| 2 | Larry David (sign-off only) | none | Showrunner reviews the cut, signs off (greenlight commit pairs with this sign-off) or kicks back with notes. |

Shared-file note: this episode has one author for the audit document.
No shared-file handoff is needed. If findings are filed, they are
separate files (one per FAIL), each authored solely by Mr. Pitt and
co-signed by the natural specialist for the rule violated when the
finding is later actioned (E07+).

---

## Risks and mitigations

- **Pitt back-to-back appearances.** Mr. Pitt dispatched the S05/S06
  plan in the immediately preceding work block and now leads this E06
  audit. The very rule he is auditing (Rule 1, no back-to-back leads)
  applies to the auditor. *Mitigation:* the audit acknowledges this
  in-document under "Auditor's own appearance count" and includes a
  recommendation that Mr. Pitt take zero lead seats in E07 and E08,
  and that the S05/S06 plan is treated as a planning dispatch
  (showrunner tier) rather than an episode lead for rule-counting
  purposes. If the showrunner rejects that classification, the
  recommendation falls back to slotting Mr. Pitt's next lead no
  earlier than E09.

- **Scope creep into rewrites.** A mid-season audit can surface
  uncomfortable demands -- "rewrite E07 to bench Kramer" -- and the
  temptation is to do the rewrite in the same episode. That is two
  episodes wearing one badge. *Mitigation:* the audit produces
  RECOMMENDATIONS and FINDINGS only. Any rewrite of an existing brief
  is out of scope and is dispatched as its own episode in the E07+
  window. The audit may name the proposed corrective episode (e.g.,
  "schedule S04E07-corrective" or "augment E08 with a Lloyd Braun
  guest") but does not author the corrective brief.

- **Disputed lead attribution.** Some commits in the window may not
  cleanly belong to a single agent (e.g., a shared-file handoff
  commit in E05 has both Costanza and Maestro touching
  `ResolveSmartDefault.cs`). *Mitigation:* the audit creates a
  "Disputed attributions" subsection and applies one of three
  conventions consistently: split-credit (0.5 LEAD each), primary
  signatory (commit author trailer wins), or brief Lead-field rule
  (whoever the brief named as Lead gets the lead credit regardless of
  who pushed the commit). Decision pinned in Open Questions.

- **Off-roster specials muddy the count.** S04off1 *The Translation*
  is in the window. If off-roster leads count toward the multi-lead
  floor, a main-cast member who only led an off-roster might appear
  to clear Rule 2 without doing regular-slate work. *Mitigation:*
  default convention is "off-roster appearances count for Rule 3
  (one-appearance floor) but not for Rule 2 (multi-lead floor)".
  Open Question confirms or overrides.

- **Audit becomes a roast.** A mid-season audit naming under-used
  agents can read like a performance review. *Mitigation:* the
  document frames findings around the rule violated, not the agent
  who underperformed. The bench did not fail; the casting did. The
  recommendations target casting decisions, not agents.

---

## Audit document outline (Pitt owns the structure)

```text
1. Window definition (SHA range, date range, episode list).
2. Commit attribution table (one row per commit, sorted by date).
3. Cast-balance table (28 rows, LEAD/CO-LEAD/SUPPORT/REVIEW columns).
4. Rule walk:
   4.1 Rule 1 -- No back-to-back leads          [PASS | WARN | FAIL]
   4.2 Rule 2 -- Main cast multi-lead floor     [PASS | WARN | FAIL]
   4.3 Rule 3 -- Supporting players floor       [PASS | WARN | FAIL]
   4.4 Rule 4 -- Lloyd Braun junior lens        [PASS | WARN | FAIL]
   4.5 Rule 5 -- Complementary guest pairings   [PASS | WARN | FAIL]
5. Disputed attributions (if any).
6. Auditor's own appearance count (Pitt-on-Pitt section).
7. Rebalance recommendations for E07..E12 (>= 3, numbered).
8. Findings filed (links to F-CAST-S04MID-NN.md, one per FAIL).
9. Sign-off block for showrunner countersignature.
```

Each rule's verdict line states the count(s) it measured, the
threshold, and the verdict. Example:

```text
Rule 2 verdict: PASS. Main-cast lead counts: Costanza 2, Kramer 1,
Elaine 0, Jerry 0, Newman 0. Floor is "multiple leads by mid-season
(E12)"; this is the E06 checkpoint, so the floor is not yet binding,
but Elaine, Jerry, and Newman are flagged WARN-ADJACENT and named in
recommendations 1, 2, and 3 respectively.
```

---

## Open questions

- **S04-only or trend vs S03?** The skill mandates a mid-season
  audit; it does not mandate a cross-season comparison. Proposed:
  S04-only for this audit; S03 trend data appears as a one-paragraph
  appendix if it falls out of the analysis for free, otherwise it is
  deferred to the S04 finale retrospective.
- **Advisory or binding recommendations?** The audit produces
  recommendations for E07..E12. Are they advisory (E07+ briefs may
  consider them) or binding (E07+ briefs must adopt them unless the
  showrunner overrides in writing)? Proposed: advisory by default;
  FAIL-tier recommendations are binding (they exist to clear a rule
  violation and skipping them re-raises the FAIL at the next
  checkpoint).
- **Off-roster lead counting convention.** Off-roster specials (e.g.,
  S04off1, S04off2) -- do their leads count toward Rule 2's
  multi-lead floor, or only toward Rule 3's one-appearance floor?
  Proposed: count for Rule 3, not for Rule 2. Pin in the audit.
- **Disputed attribution convention.** When a commit has two natural
  leads (shared-file handoff), which of split-credit / primary
  signatory / brief Lead-field wins? Proposed: brief Lead-field
  wins; the brief is the contract.
- **Does the showrunner (Larry David) appear in the cast-balance
  table?** He signs off on every episode but does not lead any.
  Proposed: include him with a REVIEW-only count; he never receives
  LEAD or CO-LEAD credit because the showrunner tier is structurally
  above episode leadership.

---

## References

- `.github/skills/writers-room-cast-balance.md` -- the five casting
  rules; this audit is the citing artifact
- `.github/skills/findings-backlog.md` -- finding file format for any
  FAIL-tier rule violation
- `.github/skills/episode-brief.md` -- canonical brief format this
  document follows
- `.github/skills/ascii-validation.md` -- grep one-liner for the
  validation step
- `.github/agents/mr-pitt.agent.md` -- auditor's voice and remit
- `.github/agents/larry-david.agent.md` -- showrunner sign-off authority
- `AGENTS.md` -- 28-agent roster used to size the cast-balance table
- `docs/episode-briefs/s04e01-the-registry.md` through `s04e05-the-picker.md`
  -- source of Lead-field truth for each in-window commit
- `docs/episode-briefs/s04off1-the-translation.md` -- off-roster
  special in the window

---

## Validation

```bash
# ASCII punctuation -- 0 matches required
grep -nP '[\x{2018}\x{2019}\x{201C}\x{201D}\x{2013}\x{2014}]' \
  docs/episode-briefs/s04e06-the-audit.md

# Markdownlint and ASCII gates
make ascii-check && make docs-lint
```

---

## On completion

```sql
UPDATE todos SET status = 'done' WHERE id = 's04e06-brief';
```

Return to showrunner: commit SHA, line count, one-sentence elevator
pitch, the open-question list, and confirmation that the brief
recommends benching Mr. Pitt himself for E07-E08.
