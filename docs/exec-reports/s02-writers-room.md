# Season 2 -- *Writers' Room*

> *The 24-episode arc plan for S02. Every cast member gets at least
> one appearance; main cast gets multiple. Costanza re-enters as lead
> after a glaring absence. Jerry and Elaine pick up several guest
> beats. A new junior-dev character -- Lloyd Braun -- joins the bench
> to surface assumptions the senior cast skips.*

**Season theme:** Production & Polish (v2 era).
**Episode count:** 24 (network-standard season order).
**Showrunner note:** No two consecutive episodes share the same lead.

## New cast addition

**Lloyd Braun -- Junior Developer.** Eager learner. Asks the obvious
question Kramer assumes everyone already knows. Owns onboarding
ergonomics and learner-grade docs. Pairs naturally with Elaine
(docs), Jerry (CI / DevOps), Kramer (code), and Newman (security
fundamentals). His catchphrase, *"Where would I have looked for
that?"* is the most valuable question in the room.

See [`.github/agents/lloyd-braun.agent.md`](../../.github/agents/lloyd-braun.agent.md).

## Aired so far

| # | Title | Featured cast | Status |
|---|-------|---------------|--------|
| S02E01 | *The Wizard* | Kramer (lead), Newman + Costanza (guests) | aired |
| S02E02 | *The Cleanup* | Kramer (lead), Puddy (guest) | aired |
| S02E03 | *The Warn-Only Lie* | Elaine (lead), Soup Nazi (guest) | aired |
| S02E04 | *The Locksmith* | Kramer (lead), Newman (guest) | aired |
| S02E05 | *The Marathon* | Kenny Bania (lead), Jerry (guest) | filming |
| S02E06 | *The Screen Reader* | Mickey Abbott (lead), Russell (guest) | filming |

## Casting corrective

Two issues identified after the original 7-episode plan:

1. **Costanza was missing entirely** despite being main cast (PM).
   He now leads E11 *The Spec* and guests E09 / E19.
2. **Jerry and Elaine were under-used.** Jerry now leads E14
   *The Container* and guests in five other episodes. Elaine guests in
   five additional episodes on top of her E03 lead.
3. **No junior-dev voice.** Lloyd Braun debuts in E07, leads E12
   *The Apprentice*, guests four episodes total.

## Rest of the season -- proposed (E07-E24)

Each episode is scoped to one or two waves of work. Fits a single
sub-agent except the finale, which is ensemble.

### S02E07 -- *The Observability*

**Featured:** Frank Costanza (lead), Lloyd Braun (guest), George
Costanza (guest).

**Pitch.** Telemetry-off honesty pass. Confirm any opt-in telemetry
leaks zero PII (prompt text, endpoint URL, key fingerprint). Add a
visible-in-`--verbose` line stating the posture. Three incident
runbooks: 401 auth, 429 rate-limit, DNS / TLS. Lloyd asks "what's
an SLO?" and Frank's answer becomes a glossary entry. Costanza
weighs in on which user-visible signals matter for product.

### S02E08 -- *The Translation*

**Featured:** Babu Bhatt (lead), Elaine (guest), Lloyd Braun (guest).

**Pitch.** i18n readiness inventory. Catalog all user-facing strings
and classify each. No translations yet -- the audit doc that makes
S03 translation work trivial. Unicode correctness check on the
masked-input path. Lloyd asks "what does i18n mean?" -- the answer
goes in `docs/glossary.md`.

### S02E09 -- *The Receipt*

**Featured:** Morty Seinfeld (lead), George Costanza (guest), Jerry
(guest).

**Pitch.** Cost-watch polish. Verify token accounting honesty. CI
cost audit (Jerry: are we burning Actions minutes on the matrix?).
Costanza pushes for a user-facing per-call cost line so people
*see* what they're spending.

### S02E10 -- *The Press Kit*

**Featured:** Mr. Lippman (lead), J. Peterman (guest), Elaine (guest).

**Pitch.** Release readiness for the v2.x polish cut. Lippman edits
CHANGELOG; Peterman writes hero copy; Elaine owns structural
consistency.

### S02E11 -- *The Spec*

**Featured:** George Costanza (lead), Lloyd Braun (guest), Elaine
(guest).

**Pitch.** Costanza writes the user-stories doc the project has
never had. Every shipped S02 feature gets translated from
engineering jargon into a one-paragraph user story: "As a [user],
I want [thing], so that [outcome]." Lloyd reads each story and
flags the ones still confusing. Elaine owns final structure.
Output: `docs/user-stories.md`.

### S02E12 -- *The Apprentice*

**Featured:** Lloyd Braun (lead), Elaine (guest), Jerry (guest),
Kramer (guest).

**Pitch.** Lloyd runs the project setup as a literal first-time
contributor and writes `docs/onboarding.md` capturing every
assumption the existing docs skip. Pairs with Elaine on prose,
Jerry on the build/CI sections, Kramer when he hits a code path
that needs explaining. Bonus deliverable: `docs/glossary.md` for
acronyms (AOT, DPAPI, MCP, libsecret, etc.) accumulated through
S02. *"Serenity now -- insanity later"* if any explanation
includes "you just have to know."

### S02E13 -- *The Inspector*

**Featured:** Newman (lead), FDR (guest), Jackie Chiles (guest).

**Pitch.** Full security audit of the v2 surface. Cred stores
(libsecret, DPAPI, mac security CLI), shell-exec hardening,
file-read blocklist, SSRF protection, dependency CVEs. FDR
proposes one adversarial scenario per surface; Jackie spot-checks
any third-party code paths for license obligations.

### S02E14 -- *The Container*

**Featured:** Jerry (lead), Newman (guest), Kramer (guest).

**Pitch.** Docker hardening pass. Multi-stage build review, image
size audit, Trivy zero-known-criticals confirmation, non-root
execution, healthcheck, signal handling. Newman owns the security
delta; Kramer owns any AOT / publish profile changes.

### S02E15 -- *The Lawyer*

**Featured:** Jackie Chiles (lead), Lloyd Braun (guest), Newman
(guest).

**Pitch.** OSS license audit. Every direct + transitive dep
classified (MIT / Apache / BSD / other). Generate `THIRD-PARTY-NOTICES.md`
or equivalent. Lloyd asks "what's the difference between MIT and
Apache?" and the answer becomes a glossary entry. Newman flags any
GPL contagion.

### S02E16 -- *The Catalog*

**Featured:** Bob Sacamano (lead), Jerry (guest), J. Peterman
(guest).

**Pitch.** Packaging strategy. Homebrew tap draft, Scoop manifest,
Nix flake stub, VS Code extension scaffold (deferred), GitHub
release artifact list. Peterman writes the install-line copy;
Jerry verifies the publish workflow.

### S02E17 -- *The Newsletter*

**Featured:** Uncle Leo (lead), Elaine (guest).

**Pitch.** Community + contributor-experience audit. Issue templates,
PR template, CONTRIBUTING.md polish, CODE_OF_CONDUCT review,
labeled-as-good-first-issue triage. Tone stewardship -- every
contributor gets a warm welcome by name. Elaine owns the prose
consistency pass.

### S02E18 -- *The Maestro*

**Featured:** The Maestro (lead), Kramer (guest).

**Pitch.** Prompt library + eval harness. Codify the system prompts
the CLI uses, set up a small eval suite (model A/B for accuracy +
latency), document the temperature cookbook. Kramer wires any
`Program.cs` changes; Maestro owns prompt content.

### S02E19 -- *The Competition*

**Featured:** Sue Ellen Mischke (lead), George Costanza (guest),
J. Peterman (guest).

**Pitch.** Competitive landscape brief. Compare against `llm`
(Simon Willison), `aichat`, `mods`, OpenAI's official CLI,
`promptfoo`. Identify three differentiators we lean into and three
gaps we accept. Costanza weighs in on roadmap implications;
Peterman on positioning copy.

### S02E20 -- *The Conference*

**Featured:** Keith Hernandez (lead), J. Peterman (guest), Elaine
(guest).

**Pitch.** CFP submission package. One talk pitch ("Living off the
Land: per-OS credential storage in a single-binary CLI"), demo
script, slide outline, speaker bio. Peterman polishes the abstract;
Elaine owns slide-text consistency.

### S02E21 -- *The Conscience*

**Featured:** Rabbi Kirschbaum (lead), Newman (guest).

**Pitch.** Responsible-use review. Where does this CLI sit on the
ethics spectrum? Document the "ought" boundaries (PII handling,
prompt-injection guardrails, shell-exec policy intent). Newman
maps each "ought" to its corresponding "must" technical control.

### S02E22 -- *The Process*

**Featured:** Mr. Wilhelm (lead), Soup Nazi (guest), Jerry (guest).

**Pitch.** Codify what we already do. ADR template, change-control
flow doc, branch protection settings audit, the *preflight* skill
review. Soup Nazi enforces the enforcement (formatting in the
template itself); Jerry maps each gate to its CI workflow.

### S02E23 -- *The Adversary*

**Featured:** FDR (lead), Newman (guest), David Puddy (guest).

**Pitch.** Chaos drill. FDR proposes five evil-input scenarios
(malformed JSON in tool calls, partial-stream truncation, shell
injection attempts via prompt, libsecret tampering, env-var
poisoning). Newman scores defenses; Puddy converts each scenario
into a permanent regression test.

### S02E24 -- *The Finale*

**Featured:** Mr. Pitt (lead), full ensemble.

**Pitch.** Season-wrap exec report. Aggregate S02 metrics across
all 24 episodes (commits, lines, tests added, CI incidents and
their MTTR, perf deltas, doc-page count). Call out the biggest
lessons. Hand off to the S03 blueprint. Lloyd Braun reviews the
finale doc one last time as the junior-lens reader-of-record.

## Cast distribution target for S02 (aired + planned)

| Cast member | Leads | Guests | Total |
|-------------|-------|--------|-------|
| Kramer | E01, E02, E04 | E09, E12, E14, E18 | 7 |
| Elaine | E03 | E08, E10, E11, E12, E17, E20 | 7 |
| Jerry | E14 | E05, E09, E12, E16, E22 | 6 |
| Newman | E13 | E01, E04, E14, E21, E23 | 6 |
| George Costanza | E11 | E01, E07, E09, E19 | 5 |
| Lloyd Braun (NEW) | E12 | E07, E08, E11, E15 | 5 |
| Kenny Bania | E05 | -- | 1 |
| Mickey Abbott | E06 | -- | 1 |
| Frank Costanza | E07 | -- | 1 |
| Babu Bhatt | E08 | -- | 1 |
| Morty Seinfeld | E09 | -- | 1 |
| Mr. Lippman | E10 | -- | 1 |
| Jackie Chiles | E15 | E13 | 2 |
| Bob Sacamano | E16 | -- | 1 |
| Uncle Leo | E17 | -- | 1 |
| The Maestro | E18 | -- | 1 |
| Sue Ellen Mischke | E19 | -- | 1 |
| Keith Hernandez | E20 | -- | 1 |
| Rabbi Kirschbaum | E21 | -- | 1 |
| Mr. Wilhelm | E22 | -- | 1 |
| FDR | E23 | E13 | 2 |
| Mr. Pitt | E24 | -- | 1 |
| J. Peterman | -- | E10, E16, E19, E20 | 4 |
| Russell Dalrymple | -- | E06 | 1 |
| Soup Nazi | -- | E03, E22 | 2 |
| David Puddy | -- | E02, E23 | 2 |

**26 distinct cast members across the season.** Main cast lead
counts: Kramer 3, Elaine 1, Jerry 1, Newman 1, Costanza 1. Every
supporting player gets at least one appearance. Lloyd Braun debuts
in E07 and recurs through E15.

## Dispatch order / waves

Episodes within a wave can film in parallel. Waves are sequential
because of shared-file collision risk on `CHANGELOG.md` and
`README.md` and because some later episodes audit earlier ones.

- **Wave A (ergonomics):** E05 *Marathon*, E06 *Screen Reader* -- in flight.
- **Wave B (honesty / docs):** E07 *Observability*, E08 *Translation*, E11 *The Spec*.
- **Wave C (junior + onboarding):** E12 *The Apprentice*, E15 *The Lawyer*, E17 *Newsletter*.
- **Wave D (security + container):** E13 *Inspector*, E14 *Container*, E23 *Adversary*.
- **Wave E (cost + perf + prompts):** E09 *Receipt*, E18 *Maestro*.
- **Wave F (community + market):** E16 *Catalog*, E19 *Competition*, E20 *Conference*.
- **Wave G (ethics + process):** E21 *Conscience*, E22 *Process*.
- **Wave H (release):** E10 *Press Kit*.
- **Wave I (finale, must be last):** E24 *Finale*.

## Off-roster, season-independent

Items that could slot into any episode as a B-plot or stand alone as
an unaired special:

- Mac Keychain test-body rewrite (needs a Mac owner -- held open).
- Linux `systemd-creds` provider (seam exists; not this season).
- The `filename-convention` docs-lint step hard-flip when convenient
  (currently warn-only by design, no urgency).

*-- Mr. Pitt (program management), with corrective notes from
George Costanza (product, returning from undeserved bench), Elaine
(structure), Jerry (ops), and a casting assist from Russell
Dalrymple. Lloyd Braun joins the room as junior-lens
reader-of-record.*
