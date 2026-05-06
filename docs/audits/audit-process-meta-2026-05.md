# Meta-Audit: The Audit Process Itself

**Auditor:** Mr. Wilhelm (Process and Change Management)
**Date:** 2026-05 (S03E02 has aired; S03E03+ still on the board)
**Scope:** the audit ecosystem as a system -- inventory, format, follow-through,
linkage to ADRs and findings backlog, cadence, and whether any of it actually
runs.
**Read-only.** No patches in this report. Process changes are recommendations
to the showrunner and Mr. Pitt, not unilateral edits.

---

## 0. Executive summary

We have audits. We have a findings-backlog skill. We have a retrospective
cadence document. We have ADRs that occasionally cite audits. What we do
**not** have is a system that connects them. Audits are produced, filed,
and then -- in the majority of sampled cases -- abandoned. The findings-
backlog skill is well-written and largely unused. The retrospective cadence
defines a season-finale carry-forward path that, six in-story weeks past the
S02 finale, has not produced the artifact it requires (`s03-writers-room.md`
does not exist).

**Headline number: 50% follow-through rate** on the sampled top-3
findings from the 2026-04-22 docs-audit set (12 supporting reports,
36 findings). 36% shipped with traceable evidence; 14% deferred to a
named episode or backlog; **50% buried** -- no commit, no exec report,
no CHANGELOG bullet, no backlog entry. Half the findings the cast was
paid to produce do not exist as far as the next reader of this
repository can tell.

This is not a writing problem. The audits are well-written. This is a
*throughput* problem -- the pipeline from audit to disposition is broken,
and nobody owns the broken segment.

The three findings that matter most:

1. **The findings-backlog skill is documented and unused** (W-01).
2. **No `s03-writers-room.md` exists** despite the retrospective-cadence
   doc requiring the finale to seed it (W-02).
3. **Per-agent audits have no template, no severity rubric, no shared
   structure** (W-03).

The rest of this report walks the evidence. The "Process Recommendations"
section at the end names eight concrete proposals.

---

## 1. Scope

This meta-audit covers every file in `docs/audits/` (15 files,
~6 000 lines), the 12 per-agent docs-audit reports dated 2026-04-22
plus the EPISODE consolidator and the FDR / security v1.8 sidecars,
the findings-backlog skill (`.github/skills/findings-backlog.md`),
the retrospective-cadence process doc
(`docs/process/retrospective-cadence.md`), the exec-report corpus
(49 files) and CHANGELOG.md for follow-through evidence, and the
ADR corpus (10 files) for audit-to-ADR linkage.

Out of scope: the contents of the audits themselves -- the question
is *what happened to them*, not *were they correct*.

---

## 2. Methodology

1. **Inventory.** `ls docs/audits/`, group by date and theme.
2. **Format scan.** Extract top-level `##` headings and any string
   matching `verdict` (case-insensitive) per report.
3. **Follow-through sample.** From each of the 12 per-agent reports,
   take the top three findings (by stated severity, else section
   order). Classify each as **shipped** (evidence in CHANGELOG, an
   exec report, an ADR, or an extant artifact named in the finding),
   **deferred** (punted to a named episode or to the findings-backlog
   with all required fields), or **buried** (no commit, no exec
   report, no CHANGELOG bullet, no backlog entry, no artifact).
4. **Skill compliance.** Grep the canonical bullet shape across
   `s02-writers-room.md` and any other plausible target.
5. **Cadence check.** Read `docs/process/retrospective-cadence.md`;
   identify the carry-forward artifact; confirm whether it exists for
   S03.
6. **Audit-to-ADR linkage.** `grep -i audit docs/adr/*.md`.

Sample size: **12 audits x 3 findings = 36**. The sample is the
highest-severity findings (the *easiest* to track), so the reported
rate is likely an upper bound. The lower-severity tail almost
certainly fares worse.

---

## 3. Audit inventory

### 3.1 By count

| Group                        | Files | Notes                                  |
|------------------------------|-------|----------------------------------------|
| Docs-audit set, 2026-04-22   | 13    | EPISODE consolidator + 12 per-agent    |
| Sidecar, 2026-04-22          | 1     | `fdr-v2-dogfood-2026-04-22.md`         |
| Post-release, 2026-04        | 1     | `security-v1.8-post-release.md`        |
| Total                        | 15    | ~6 000 lines                           |

### 3.2 By size

Reports range from 135 lines (Newman, security v1.8) to 622 lines
(Keith). Mickey (176) is the shortest per-agent report; Keith (622) the
longest. No documented length budget. Mickey covers the same archetype-
scope surface in roughly a quarter of Keith's page count.

### 3.3 By verdict discipline

A "verdict" is a one-line top-of-document classification (red / yellow /
green, ship / no-ship, OK / not-OK). Six of fifteen reports state one
explicitly; the rest bury the bottom line in prose.

| Report                | Explicit verdict line | Form                          |
|-----------------------|-----------------------|-------------------------------|
| security-v1.8         | Yes                   | "RED -- release pipeline failed" |
| fdr-v2-dogfood        | Yes                   | "Ship-blocker? No, with caveats" |
| jackie                | Yes                   | "No distribution blocker"     |
| mickey                | Yes (mismatch)        | "Code is correct; docs are silent" |
| morty                 | Yes (per-doc)         | "Needs ~40% rewrite"          |
| puddy                 | Yes                   | "Either docs match or they don't. They don't." |
| EPISODE               | Implied via section heading, not a single line | -- |
| babu / bania / elaine / jerry / keith / lippman / maestro / newman | No top-line verdict | scattered through findings |

Six of fifteen is a 40% verdict-discipline rate. The reader who scans for
the bottom line finds it in fewer than half of these documents.

### 3.4 By naming convention

The 2026-04-22 set is consistent: `docs-audit-2026-04-22-<archetype>.md`.
The earlier security audit (`security-v1.8-post-release.md`) and the FDR
report (`fdr-v2-dogfood-2026-04-22.md`) follow no shared scheme. There is
no documented filename convention.

---

## 4. Findings follow-through

### 4.1 The sample

For each of the 12 per-agent 2026-04-22 reports, the top-three (by stated
severity, else section order) findings were extracted and classified.
Detail per audit:

| Audit    | Top-3 findings (abbreviated)                                                               | Status (S=shipped, D=deferred, B=buried) |
|----------|--------------------------------------------------------------------------------------------|------------------------------------------|
| elaine   | C1 install table; C2 v1 artifact filenames; C3 Quickstart -> v1 .env.example               | S, S, S                                  |
| lippman  | C-1 v2.0.4 tarball name drift; C-2 binary --version --short = 2.0.2; C-3 release-runbook v1 | S, S, S (v2.0.5 fix-forward, CHANGELOG)  |
| maestro  | H1 docs/prompts missing; H2 no eval harness; H3 no temperature cookbook                    | S, S, B                                  |
| babu     | H-01 InvariantGlobalization undocumented; H-02 docs/i18n.md missing; M-01 USD-only never disclosed | B, S, B                          |
| newman   | F-1 SECURITY.md 9 months stale; F-2 UnsafeReplaceSecrets undocumented; F-3 Grype/Trivy doc mismatch | B, B, B                         |
| jackie   | F-01 gif source/license; F-02 NOTICE v1 block stale; F-03 LICENSE single-year                | D (S02E36), D (S02E36), B               |
| jerry    | C1 release-runbook matrix wrong; C2 asset inventory phantom platforms; H1 macos-13 troubleshooting | S, B, B                         |
| bania    | C1 README v1.8 perf; C2 scripts/bench.py CLI missing; H1 ARCHITECTURE.md 5.4ms claim       | B, B, B                                  |
| keith    | C-1 demo scripts call `az-ai` not `az-ai-v2`; C-2 announce/ has only v1.8 file; H-1 default model drift | B, S, S (ADR-009)               |
| mickey   | H-001 NO_COLOR / FORCE_COLOR / TERM=dumb undocumented; H-002 hero gif no fallback; M-001 --raw a11y angle unsold | B, D (E36 license), B    |
| morty    | C-tier pricing dashboard staleness; doc lies on default model; cost-optimization rewrite   | D (ADR-009), S (ADR-009), B              |
| puddy    | C1 tests/README.md v1-only; C2 `make test` runs v1; H1 README "538 passing tests"          | B, B, B                                  |

Tally:

- **Shipped: 13 of 36 (36%).**
- **Deferred to a named target: 5 of 36 (14%).**
- **Buried: 18 of 36 (50%).**

**Follow-through rate (shipped + deferred): 50%.**

### 4.2 What the headline hides

- **Lippman, Elaine, and Maestro carry the report.** Their top-3 hits
  shipped at 100%, 100%, and 67% respectively. Without them the
  remaining nine audits land at 22% follow-through (8 of 27).
- **The shipped fixes correlate with mechanical CI gates, not with
  audit-as-such.** Lippman's three shipped because the version-drift
  bug broke a release; Elaine's three shipped as a byproduct of the
  release-pipeline rebuild; Maestro's H1/H2 shipped because
  `docs/prompts/` was scaffolded by an unrelated arc. **None of the 13
  shipped fixes cite the audit that caught them in the commit body.**
- **The 18 buried findings include CVE-shape items.** Newman F-2
  (`UnsafeReplaceSecrets` undocumented) is a control whose contract is
  invisible to users. Bania C2 (`scripts/bench.py`) is an advertised
  capability that does not exist.
- **Puddy's 0-of-3 is the most disappointing line in the table.** A QA
  audit flagging `tests/README.md` as a v1-only relic and `make test`
  as running the wrong project should not produce zero traceable
  follow-through. It did.

### 4.3 The deferred bucket is misleading

Of the five deferred entries, four route to S02E36 *The Attribution*
(Jackie + Mickey on the gif) or to ADR-009 (Morty). Only one is
deferred to a still-pending target. The findings-backlog format -- the
canonical place where "deferred" is supposed to live -- accounts for
**zero** of the five. See section 5.

---

## 5. Findings-backlog skill adherence

### 5.1 The skill

`.github/skills/findings-backlog.md` is precise. It mandates a one-bullet
entry per finding with five required fields: kebab-case ID prefixed by the
discovering episode, discovering episode, one-sentence diagnosis, file
path + line, severity tag (`bug` / `smell` / `gap` / `lint`), and
disposition (`queued-as-episode` / `b-plot` / `one-line-fix` / `wontfix`).
It includes a worked example, an anti-pattern catalogue, and a five-state
lifecycle. It is one of the better-written skill docs in the tree.

### 5.2 Compliance

The canonical bullet shape (the regex
`- \*\*\`e[0-9]+-[a-z-]+\`\*\* \[(bug|smell|gap|lint)`) appears in
`docs/exec-reports/s02-writers-room.md` **zero times.**

The S02 backlog (writers' room, "Findings backlog from S02 audits"
subsection, lines 375-470) uses a different shape -- a hierarchical
"From S02E07/E08/E11..." narrative with prose bullets that omit at least
one required field per entry. Some have a severity tag, some do not.
Some have a file path, some do not. Some have a disposition, some do not.

The skill also says: "A finding mentioned in an exec report's 'Lessons'
section but NOT logged to the backlog is a finding that will get lost."
The 2026-04-22 audit set produced an estimated 200+ findings (12 reports,
average ~15 findings each). The S02 backlog references *zero* of them
in the canonical format. The cardinal sin the skill exists to prevent
has been committed at scale.

### 5.3 Why it isn't being used

Three plausible causes: (1) **No mechanical gate** -- exec-report-format
got CI enforcement in S02E38; findings-backlog has no analogue.
(2) **Format too heavy for volume** -- 12 audits x ~15 findings is 180
entries; at five fields each, 900 fields to author manually with no
scaffolding. (3) **The skill describes format, not trigger** -- it says
"every time an episode surfaces a finding" but does not name "every
audit batch produces a backlog migration step before the next dispatch
wave." The audit-to-backlog handoff is undefined.

---

## 6. Cadence

### 6.1 What the process doc says

`docs/process/retrospective-cadence.md` defines three cadences:

- Season-finale retro (every season's last episode).
- Monthly mini-retro (first working day of each month).
- Post-incident retro (red-CI streak >= 2, fix-forward within 24 h, etc.).

It says nothing about audits. The word "audit" appears 23 times in
`retrospective-cadence.md` -- all in the context of the cast-distribution
audit and the findings-backlog audit (Mr. Pitt at E06/E12/E18). There is
no policy on **when a content audit (docs / security / a11y / perf) should
run**, who triggers it, or how its outputs flow back into the cycle.

### 6.2 What the practice has been

| Audit                | Trigger                     | Cadence implication |
|----------------------|------------------------------|---------------------|
| security-v1.8        | Post-release                 | event-triggered     |
| docs-audit-2026-04-22| Pre-v2.0.5 fix-forward push  | event-triggered     |
| fdr-v2-dogfood       | Pre-v2.0.5 dogfood window    | event-triggered     |

All three audits in the corpus were event-triggered. None were calendar-
driven. The 2026-04-22 sweep was, in practice, the project's first
attempt at a coordinated cross-cast audit -- and it has not recurred.

### 6.3 The carry-forward artifact

`retrospective-cadence.md` section 5 says:

> The writers' room file for S0(N+1) is initialized by the finale, not by
> the first episode of the new season. The showrunner commits the seeded
> file as part of the finale wave so the next season starts with the
> carry-forward already on the board.

S02E24 *The Finale* shipped (`docs/exec-reports/s02e24-the-finale.md`,
2 304 lines). S03E01 and S03E02 have aired. There is no
`docs/exec-reports/s03-writers-room.md`. This is a process drift, not a
content question. The retrospective-cadence doc is being treated as
aspirational rather than load-bearing.

### 6.4 Recommendation in advance of section 9

Audits should be **both** event-triggered (security audit on every release,
docs audit on every major version) **and** calendar-triggered (one full
docs audit every six in-story weeks, mirroring the monthly mini-retro
cadence but quarterly). Codify in `docs/process/retrospective-cadence.md`
under a new section "Audit cadence."

---

## 7. Format consistency

### 7.1 No template

There is no `docs/audits/_template.md`. Each archetype invents its own
structure. Heading patterns sampled (abbreviated):

- **Babu / Bania / Newman / Mickey / Morty / Puddy:** numbered sections
  (`## 0. Executive` / `## 1. ...` / `## 2. Findings`), severity prefixes
  one of `H-NN`, `C1`/`H1`, `F-N`, `H-001`, emoji-tagged.
- **Elaine / Jerry / Lippman / Maestro:** unnumbered (`## Summary` /
  `## Critical` / `## High` / `## Findings`), prefixes `C1`/`H1`,
  `C-N`/`H-N`.
- **Jackie / Keith:** numbered with explicit severity rubric section,
  prefixes `F-NN` / `C-N`.
- **EPISODE consolidator:** theatrical -- `## 1. Cold open` ->
  `## 2. The verdict` -> `## 4. The emergency` ->
  `## 5. Acts (prioritized work)`.

Twelve audits, twelve shapes. Finding-ID convention has seven variants.

### 7.2 Why this matters

A reader scanning for "what's the worst thing in this audit" must learn
each archetype's local convention before the document yields its top
finding. A future tool that wants to scrape findings into the backlog
cannot rely on a single regex. The EPISODE consolidator must hand-translate
between conventions every time -- which it does, badly, when the audit set
is twelve strong.

### 7.3 Minimum viable template

A `docs/audits/_template.md` would specify:

- Filename: `<theme>-<YYYY-MM-DD>-<archetype>.md`.
- Frontmatter: archetype, date, scope-one-sentence, top-line verdict
  (red / yellow / green), commit / tag / version under audit.
- Section 1: Executive summary (<= 200 words).
- Section 2: Severity rubric (one-line definitions of CRITICAL / HIGH /
  MEDIUM / LOW; identical wording across all audits).
- Section 3: Findings (each with stable ID `<archetype-letter>-NN`,
  one-sentence diagnosis, file path + line, evidence, recommendation,
  proposed disposition per the findings-backlog skill).
- Section 4: Out-of-scope (what the audit deliberately did not check).

The EPISODE consolidator gets its own template
(`docs/audits/_template-consolidator.md`) since its job is different
(rolling up N per-agent audits into one verdict and one act-structured
work plan).

---

## 8. Audit-to-ADR linkage

### 8.1 What the corpus shows

Five ADRs reference "audit." Four use it generically (audit trail,
auditable, "Wilhelm audits," "future auditors") -- not citations.
ADR-003 cites `docs/testing/test-sanity-audit.md` (real linkage,
predates the 2026-04-22 set). **ADR-009 (default model resolution)
is the exemplar:** it cites two specific 2026-04-22 audits (Morty +
Maestro) by filepath and section anchor.

### 8.2 The pattern

ADR-009 is the only ADR in the corpus that follows the linkage pattern
this meta-audit recommends: when an audit finding triggers an
architectural decision, the ADR cites the audit by filepath and anchor.
The other ADRs that mention "audit" use the word generically. There is
no policy in `docs/process/adr-stewardship.md` requiring such citation.

### 8.3 The miss

The version-drift fix-forward (Lippman C-1/C-2/C-3, the headline finding
of the 2026-04-22 set) produced a CHANGELOG bullet, an exec report, and
a perf-baseline doc. It did not produce an ADR. That may have been the
right call (mechanical bug, not architectural), but the question was
not asked -- the change-management process did not surface "should this
audit finding earn an ADR?" as a checkpoint.

---

## 9. The third one -- meta-audits as a recurring artifact

This is the project's first meta-audit. The question is whether it
should be the last.

**For recurrence:** half the findings are buried (will drift to 100%
without periodic check); the retrospective-cadence doc is being treated
as advisory and someone outside the showrunner needs to verify the
carry-forward artifacts; a meta-audit is cheap (this report: ~6 hours
of an archetype's time, read-only, no code changes).

**Against:** a meta-audit can become a ritual that produces no
carry-forward (the retro anti-pattern). If *this* report's
recommendations sit unaddressed for two seasons, the next meta-audit
is paperwork.

The right cadence is **once per season finale, immediately following
the finale exec report**. The finale already audits cast distribution
and the open backlog. Adding a meta-audit step (one section in the
finale exec report, owned by Wilhelm, citing a separate
`audit-process-meta-sNN.md` file) folds the practice into the existing
ritual without standing up a new meeting cadence.

---

## 10. Findings (this report)

### W-01 -- Findings-backlog skill is documented and unused (HIGH)

**Evidence.** Zero canonical-format bullets in `s02-writers-room.md`.
The 2026-04-22 set produced ~200+ findings; none entered the backlog
in the prescribed format (section 5.2).

**Recommendation.** Wire backlog compliance into exec-report-format:
every report whose body matches `(?i)\b(bug|defect|gap|smell|finding)\b`
must include a `## Findings to log` section with canonical-format
bullets, validated by a CI script analogous to `exec-report-check.sh`.
Owner: Soup Nazi + Wilhelm. Target: S03E04.

### W-02 -- `s03-writers-room.md` does not exist (HIGH)

**Evidence.** `retrospective-cadence.md` section 5 mandates the finale
seed it; S02E24 shipped without it; S03E01 and S03E02 have aired
without it (section 6.3).

**Recommendation.** Showrunner produces `s03-writers-room.md` as
fix-forward in the next dispatch wave, seeded from S02 carry-forward
(open backlog, casting drift, follow-through gaps from this report).
Add CI check: if any `s0Ne01-*.md` exists, then `s0N-writers-room.md`
must exist on the same commit. Target: S03E03.

### W-03 -- Per-agent audits have no template (MEDIUM)

**Evidence.** Twelve audits, twelve heading conventions, seven
finding-ID variants, six of fifteen reports state an explicit verdict
(section 7).

**Recommendation.** Add `docs/audits/_template.md` and
`_template-consolidator.md` per section 7.3. Future audits fail review
without matching structure. Target: before next audit batch.

### W-04 -- Audits do not cite themselves in fix commits (MEDIUM)

**Evidence.** Sampled 13 shipped fixes in section 4; none reference
the audit doc by filepath in the commit body.

**Recommendation.** Update `.github/skills/commit.md`: when a commit
addresses an audit finding, the body must include a
`Closes: docs/audits/<file>#<anchor>` trailer. The exec report cites
the same. Target: S03E04.

### W-05 -- No audit cadence policy (MEDIUM)

**Evidence.** Three audits in the corpus, all event-triggered, no
calendar trigger, six in-story weeks since last full sweep (section 6).

**Recommendation.** Add an "Audit cadence" section to
`docs/process/retrospective-cadence.md`. **Calendar:** full docs-audit
sweep every six in-story weeks (next due ~2026-06-03); security and
a11y audits every release; perf audit every minor. **Event:** major
version bumps trigger a cross-cast audit (mandatory); CVE-shape
findings trigger a Newman re-audit on the affected surface within
seven days. Target: S03E04.

### W-06 -- Verdict discipline is below 50% (LOW)

**Evidence.** Six of fifteen reports state a one-line verdict (section 3.3).

**Recommendation.** The proposed `_template.md` requires
`**Verdict:** RED | YELLOW | GREEN -- <one sentence>`. Reviewers fail
audits without it. Bundle with W-03.

### W-07 -- ADR linkage is informal (LOW)

**Evidence.** Five ADRs mention "audit"; only ADR-009 follows the
filepath-and-anchor citation pattern (section 8).

**Recommendation.** Update `docs/process/adr-stewardship.md`: when an
ADR is motivated by an audit finding, "Context" or "References" must
cite the audit by filepath + anchor (the ADR-009 pattern).

### W-08 -- Meta-audit cadence undefined (LOW)

**Evidence.** This is the first meta-audit. The retrospective-cadence
doc has no slot for one (section 9).

**Recommendation.** Codify the meta-audit as a finale-wave deliverable
in `retrospective-cadence.md`. One per season, owned by Wilhelm, filed
as `docs/audits/audit-process-meta-sNN.md`, cited from the finale exec
report. Target: S03E24.

---

## 11. Process Recommendations (consolidated)

The eight proposals from section 10 as a single batch the showrunner
can greenlight:

1. **`docs/audits/_template.md`** + `_template-consolidator.md` skeleton
   for all per-agent audits, with mandatory verdict line. (W-03, W-06.)
2. **Wire findings-backlog into exec-report-format**: every defect-
   naming exec report appends canonical-format backlog bullets,
   CI-validated. (W-01.)
3. **Schedule meta-audits every season finale** (S03E24, S04E24, ...),
   owned by Wilhelm, cited from the finale exec report. (W-08.)
4. **CI check linking per-agent audits to consolidator**: if any
   `<theme>-<date>-<archetype>.md` exists, then `<theme>-<date>-
   EPISODE.md` must exist and link all sibling reports.
5. **CI check for `s0N-writers-room.md`** when any `s0Ne01-*.md`
   exists. (W-02.)
6. **"Audit cadence" section in `retrospective-cadence.md`**, calendar
   + event-triggered policies. (W-05.)
7. **`commit.md` update**: `Closes: docs/audits/<file>#<anchor>`
   trailer required for audit-finding fixes. (W-04.)
8. **`adr-stewardship.md` update**: ADRs motivated by audit findings
   must cite the audit by filepath + anchor (the ADR-009 pattern). (W-07.)

---

## 12. Headline metric

**Audit follow-through rate, 2026-04-22 sweep, top-3 sample:
36 findings classified. 13 shipped (36%), 5 deferred (14%),
18 buried (50%). Follow-through (shipped + deferred) = 50%.**

Half of what the cast was paid to produce did not survive the trip
from audit doc to fix. The audits are not the problem. The pipeline
is.

---

## 13. Sign-off

**Status:** complete. Read-only. No commits, no patches, no edits to
orchestrator-owned files. Out of scope: correctness of the original
2026-04-22 findings (taken on faith); cost of the proposed CI checks
(Morty should price W-01, W-04, W-05 before they ship); implementation
(lands in S03E03+).

**Recommended dispatch order** (showrunner discretion):

1. W-02 (`s03-writers-room.md`) -- highest leverage, lowest cost,
   fixes a current process violation.
2. W-03 + W-06 (templates) -- unblock the next audit batch.
3. W-01 (backlog wiring) -- pays compounding interest; the longer it
   waits, the more findings rot.
4. W-05 (cadence policy) -- cheap doc edit; sets the schedule.
5. W-04, W-07, W-08 -- doc-only updates; bundle as an off-roster
   one-pager.

The system is recoverable. The audits are good. The pipeline between
them is the work.

-- Mr. Wilhelm
   Process and Change Management
