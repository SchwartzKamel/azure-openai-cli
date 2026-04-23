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

## Aired so far (32 episodes; 2 main-arc remaining)

| # | Title | Featured cast | Status |
|---|-------|---------------|--------|
| S02E01 | *The Wizard* | Kramer (lead), Newman + Costanza (guests) | aired |
| S02E02 | *The Cleanup* | Kramer (lead), Puddy (guest) | aired |
| S02E03 | *The Warn-Only Lie* | Elaine (lead), Soup Nazi (guest) | aired |
| S02E04 | *The Locksmith* | Kramer (lead), Newman (guest) | aired |
| S02E05 | *The Marathon* | Kenny Bania (lead), Jerry (guest) | aired |
| S02E06 | *The Screen Reader* | Mickey Abbott (lead), Russell (guest) | aired |
| S02E07 | *The Observability* | Frank Costanza (lead), Newman (guest) | aired |
| S02E08 | *The Translation* | Babu Bhatt (lead), Mickey + Lloyd (guests) | aired |
| S02E09 | *The Receipt* | Morty Seinfeld (lead), Costanza (guest) | aired |
| S02E11 | *The Spec* | George Costanza (lead), Lloyd + Elaine (guests) | aired |
| S02E12 | *The Apprentice* | Lloyd Braun (lead), Elaine + Jerry + Kramer (guests) | aired |
| S02E13 | *The Inspector* | Newman (lead), FDR + Jackie (guests) | aired |
| S02E14 | *The Container* | Jerry (lead), Newman (guest) | aired |
| S02E15 | *The Lawyer* | Jackie Chiles (lead), Lloyd (guest) | aired |
| S02E16 | *The Catalog* | Bob Sacamano (lead), Mr. Lippman (guest) | aired |
| S02E17 | *The Newsletter* | Uncle Leo (lead), Elaine (guest) | aired |
| S02E18 | *The Maestro* | The Maestro (lead), Kramer (guest) | aired |
| S02E19 | *The Competition* | Sue Ellen Mischke (lead), Peterman (guest) | aired |
| S02E20 | *The Conference* | Keith Hernandez (lead), Peterman + Elaine (guests) | aired |
| S02E21 | *The Conscience* | Rabbi Kirschbaum (lead), Newman (guest) | aired |
| S02E22 | *The Process* | Mr. Wilhelm (lead), Soup Nazi + Jerry (guests) | aired |
| S02E23 | *The Adversary* | FDR (lead), Newman + Puddy (guests) | aired |
| S02E25 | *The Story Editor* | Elaine (lead), Lloyd + Mickey (guests) | aired (off-roster) |
| S02E27 | *The Bible* | Mr. Wilhelm (lead), Elaine (guest) | aired (off-roster) |
| S02E28 | *The Style Guide* | Soup Nazi (lead), Newman (guest) | aired (off-roster) |
| S02E29 | *The Casting Call* | Mr. Pitt (lead), Sue Ellen (guest) | aired (off-roster) |
| S02E30 | *The Cast* | Elaine (lead), Kramer (guest) | aired (off-roster) |
| S02E31 | *The Audition* | David Puddy (lead), Maestro (guest) | aired (off-roster) |
| S02E32 | *The Bypass* | Newman (lead), Kramer (guest) | aired (off-roster, security hotfix) |
| S02E33 | *The Uninstaller* | Jerry (lead), Lloyd (guest) | aired (off-roster, Jerry floor corrective) |
| S02E34 | *The Index* | Lloyd Braun (lead), Elaine (guest) | aired (off-roster, docs orphan cleanup) |
| S02E26 | *The Locked Drawer* | Newman (lead), Kramer (guest) | aired (off-roster, security hotfix -- ReadFile) |

**Remaining S02 main arc:** E10 *Press Kit* (Lippman + Peterman + Elaine with Costanza promoted to co-lead -- closes Costanza's floor + curates the 8-episode CHANGELOG backlog), E24 *Finale* (Pitt + ensemble -- absolute last; hands off to S03).

**Off-roster pending:** *(none -- Wave 6 cleared the off-roster queue)*

## Casting drift -- multi-lead floor failure (per writers-room-cast-balance audit)

S02E29 *The Casting Call* introduced the cast-balance audit and immediately surfaced a failure: in the planned 24-arc, **Costanza, Elaine, Jerry, and Newman each had only ONE lead**. After Wave 5 the actual aired counts are:

- **Kramer:** 3 leads (E01, E02, E04) -- floor met.
- **Elaine:** 3 leads (E03, E30, E25) -- floor met.
- **Newman:** 3 leads (E13, E32, E26) -- floor met after E32 + E26 correctives.
- **Jerry:** 2 leads (E14, E33) plus S06 blueprint off-roster -- floor met after E33 corrective.
- **Lloyd:** 2 leads (E12, E34) -- junior lens keeps earning it.
- **Morty:** 1 lead (E09) -- supporting-floor met (was at 0).
- **Costanza (George):** 1 lead (E11), substantial guest in E09 -- still one short of main-cast floor.

**Corrective for the closing wave:** Costanza is the sole remaining main-cast floor miss. Plan stands: promote him to co-lead on E10 *Press Kit* (customer-story angle opposite Lippman's SemVer mechanics). No other correctives needed.

Logged for the closing-wave dispatch decision.

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

- **S02E25 *The Story Editor*** (Elaine lead, Lloyd + Mickey guests).
  Doc-tree consolidation pass: thin overlap between `competitive-
  analysis.md` + `competitive-landscape.md`, `i18n-audit.md` +
  `i18n.md` + `i18n/`, `licensing-audit.md` + `legal/license-audit.md`,
  `security-review-v2.md` + `security/v2-audit.md` +
  `security/reaudit-v2-phase5.md`. Add cross-links. Archive duplicates
  to `docs/archive/`
  with a redirect note. Triggered by 2026-04-22 audit -- 170+ doc files
  with no top-level discovery surface for non-episode docs.
- **S02E26 *The Locked Drawer*** (Newman lead, Kramer guest). Surfaced
  by E13 audit; **expanded by S02E23 *The Adversary*** which confirmed 7
  uncovered home-dir paths via xUnit. Extend
  `ReadFileTool.BlockedPathPrefixes` to cover `~/.ssh`, `~/.kube`,
  `~/.gnupg`, `~/.netrc`, `~/.docker/config.json`, `~/.git-credentials`,
  `~/.config/git/credentials`, `~/.config/gh/hosts.yml`,
  `~/.npmrc`/`~/.pypirc`. Activate the 7 currently-Skipped tests
  in `tests/AzureOpenAI_CLI.Tests/Adversary/ReadFileSensitivePathTests.cs`.
  One-PR scope. Preflight required.
- **S02E32 *The Bypass*** (Newman lead, Kramer guest). Surfaced by
  S02E23 finding `e23-shell-ifs-tokenization` (CVE-shape). `${IFS}` and
  related tokenization tricks route any blocked command past the
  `ShellExecTool` blocklist. Structural fix: word-boundary regex or
  drop the shell pre-execution. Activate the 8 currently-Skipped
  bypass tests in `tests/AzureOpenAI_CLI.Tests/Adversary/ShellExecBypassTests.cs`.
  Preflight required. **Should dispatch BEFORE E26 in the security
  hotfix wave** -- exploitable today, broader scope.
- **Findings backlog from S02 audits** (becomes B-plots or one-line
  fixes; do not lose). Per [`findings-backlog`](../../.github/skills/findings-backlog.md)
  format. Per-episode findings detail lives in the sibling
  `sNNeMM-findings.md` files; this is the rolled-up index.

  **From early waves:**
  - Dual-telemetry reality (E07): v2 has opt-in OTel pipeline at
    `azureopenai-cli-v2/Observability/Telemetry.cs`.
  - `:F0` against current culture in `Program.cs:1445` (E08): latent
    de-DE bug, one-line fix candidate.
  - Plural shortcut `iteration(s)` in Ralph mode (E08).
  - `,-N` padding-spec column alignment in 3 sites (E08, CJK-blocker).
  - Arabic list-separator U+060C silently rejected (E08).
  - Lone-surrogate masked-input edge case (E08).
  - Binary-split confusion / `--config show` precedence (E11 product
    smells).
  - MCP support gap (E19): table-stakes among premium CLIs; already
    tracked as FR-013.

  **From S02E12 *Apprentice*:**
  - `AZUREOPENAIAPI` env var reads as a noun, costs ~10 min for new
    contributors (smell, b-plot).
  - Two source trees discoverability gap (`azureopenai-cli/` vs
    `azureopenai-cli-v2/`) (smell, b-plot).

  **From S02E18 *Maestro*:**
  - **`ralph-mode-appendix` inherits temperature 0.55 instead of
    0.0-0.1 for a convergent validator loop** (bug, queued-as-episode
    -- candidate for S03 prompt arc).
  - Orchestrator process: brief said "temperature-cookbook.md (new)"
    when file already existed; verify file existence before brief
    writes "new" (process improvement).

  **From S02E14 *Container*:**
  - `e14-trivy-non-blocking` (gap, b-plot): Trivy CI step uses
    `exit-code: '0'`. HIGH/CRITICAL CVEs in shipped image will not
    redden `main`. Jerry refused to flip mid-episode to avoid CI red
    on an unowned CVE. Owner-decision needed.

  **From S02E16 *Catalog* (path/process):**
  - `packaging/nix/flake.nix` was already taken by v2-line; v1 lives
    at `packaging/nix/azure-openai-cli/flake.nix`. Brief should
    verify path availability (orchestrator process improvement,
    same root cause as the E18 "(new)" miss).

  **From S02E22 *Process*:**
  - `e22-pr-template-process-doc-crosslink` (gap, one-line-fix):
    PR template did not reference `docs/process/`. **FIXED in this
    orchestrator batch.**
  - `e22-wilhelm-archetype-deliverables-drift` (smell, one-line-fix):
    Wilhelm archetype said `docs/process.md` (singular file); we
    shipped a directory. **FIXED in this orchestrator batch.**
  - `e22-agents-md-process-bucket-missing` (gap, one-line-fix):
    AGENTS.md skills section did not enumerate process docs.
    **FIXED in this orchestrator batch.**

  **From S02E23 *Adversary* (full detail in `s02e23-findings.md`,
  21 findings, 9 CVE-shape):**
  - **`e23-shell-ifs-tokenization`** (bug, queued-as-S02E32 *The
    Bypass*): `${IFS}` routes any blocked command past the gate.
    Trivially exploitable. Highest priority.
  - **`e23-readfile-{ssh-userdir,kube-config,gnupg,netrc,docker-config,git-credentials,npmrc-pypirc}-not-blocked`**
    (7 gaps, queued-as-S02E26 *Locked Drawer*).
  - **`e23-webfetch-dns-rebinding-toctou`** (CVE-shape, queued for
    S03 hardening arc -- bigger episode, structural rewrite of
    resolve-then-connect path).
  - 7 additional shell-bypass attempts (`&&` after eval, tab/newline
    separators, quoted/escaped/env-indirected command names, fullwidth
    Unicode lookalikes) -- all queued for S02E32 *Bypass*.
  - 3 additional WebFetch SSRF gaps (multicast/broadcast, CGNAT
    100.64/10, decimal IP encoding) -- queued for S03 hardening.
  - 2 stream-chaos findings (non-string param throws, delegate
    negative-depth bypass) -- b-plot.

  **From S02E30 *The Cast*:**
  - Pre-existing em-dashes in 5 generic prompts and `PersonaMemory.cs`
    would fail strict ASCII validation (lint, b-plot -- future Soup
    Nazi + Elaine cleanup episode).
  - **No prompt eval cases for the 12 cast personas** (gap,
    queued-as-episode for S04 Maestro arc -- "no eval, no merge"
    standard violated by shipping prompts without eval cases).
  - No archetype-to-prompt regen tooling; voice drift in
    `.github/agents/*.agent.md` requires manual re-compression
    (gap, candidate for S03 or S06 Kramer code episode).

  **From S02E31 *The Audition* (full detail in `s02e31-findings.md`,
  9 findings):**
  - **`e31-routing-substring-coder-overshadow`** (bug, b-plot):
    Substring keyword matching causes wrong-persona dispatch in
    `SquadCoordinator`. Routing test pinned as Skipped. Headline
    find from the audition.
  - 8 additional persona behavior gaps (no stay-in-character clause,
    `write` not in writer keywords, auto-routing silent fallback,
    no kebab/snake normalization, empty system prompt not validated,
    tool-availability contradiction, ralph composition untested,
    agent tool-override untested).

  **From S02E25 *The Story Editor* (full detail in `s02e25-the-story-editor.md`):**
  - `e25-tv-guide-row-lag` (process, b-plot): TV guide rows for newly-aired
    episodes lag behind landings. Closed by this orchestrator batch
    adding rows for E09/E25/E32.
  - `e25-adr-fr-backlinks-gap` (gap, b-plot): ADRs and FRs do not
    cross-link to their parent proposals/decisions.
  - `e25-accessibility-doc-redundancy` (smell, b-plot): a11y guidance
    spread across multiple files without a canonical home.
  - `e25-cost-doc-split` (gap, b-plot): cost concerns split between
    Morty docs and FinOps notes; consolidate.
  - `e25-competitive-doc-duplication` (smell, b-plot): overlap between
    `competitive-analysis.md` and `competitive-landscape.md`.
  - `e25-launch-dir-no-index` (gap, b-plot): `docs/launch/` has no
    index file.
  - `e25-orphan-docs` (smell, b-plot): several `docs/*.md` files have
    no inbound link from `docs/README.md` map.
  - `e25-readme-documentation-section-flat` (smell, b-plot): top-level
    README's Documentation section is a flat list, not categorized.

  **From S02E09 *The Receipt* (full detail in `s02e09-the-receipt.md`):**
  - `e09-cost-receipt-json-mode-gap` (gap, b-plot): `--json` output
    mode does not embed the cost block. Either add a `cost` key to
    JSON output or document the gap. Future Maestro / Russell episode.
  - `e09-price-table-staleness-gap` (gap, b-plot): `PriceTableAsOf`
    is a comment, not an enforced check. No reminder when prices
    drift past N months. Candidate for a Morty + Jerry refresh
    process episode.

  **From S02E32 *The Bypass* (full detail in `s02e32-the-bypass.md`):**
  - **CLOSED:** `e23-shell-ifs-tokenization` -- structural fix shipped
    in `a4fd184`; all 8 reactivated bypass tests pass; no new findings
    surfaced during the rewrite.
  - **CLOSED (Wave 6):** all seven `e23-readfile-*` findings --
    structural fix shipped in `04be3ee` (S02E26 *The Locked Drawer*);
    53 new facts in `ReadFileSensitivePathTests.cs` covering `~/.ssh`,
    `~/.kube`, `~/.gnupg`, `~/.netrc`, `~/.docker/config.json`,
    `~/.git-credentials` (+ XDG variant), `~/.npmrc`, `~/.pypirc`.
    Each of `e23-readfile-{ssh-userdir,kube-config,gnupg,netrc,
    docker-config,git-credentials,npmrc-pypirc}-not-blocked` is closed.
  - The DNS rebinding TOCTOU finding remains open and routes to a
    future S03 hardening episode.

  **From S02E34 *The Index* (Wave 6, Lloyd + Elaine):**
  - **CLOSED:** `e25-orphan-docs` -- 17 orphan `docs/*.md` files linked
    from `docs/README.md`. Three new H2 sections added additively
    (Recent additions / Observability and telemetry / Quality audits
    and reviews). No existing anchors broken.
  - **CLOSED:** `e25-launch-dir-no-index` -- new `docs/launch/README.md`
    indexes the 18 launch artifacts with "when you want this" Lloyd-voice
    descriptions.
  - **CLOSED (already-resolved):** `e25-readme-documentation-section-flat`
    -- top-level `README.md` Documentation section was already
    categorized into six H3 sub-sections at audit time. Finding was
    stale; no code change needed.
  - Flagged for future pass (B-plot): subdirectory link convention --
    `announce/`, `talks/`, `devrel/` are directory-linked rather than
    README-linked. Navigable via GitHub directory rendering; flagged
    for a consistency pass.

  **Still open from S02E25 *The Story Editor* (route to future episodes):**

  **Process / orchestration findings (orchestrator-owned):**
  - **Five cross-sub-agent file sweeps via `git add -A`** (now five,
    not four): `f3046e1`, `4a4b894`, `3bd0acb`, `93dfac7`, plus the
    Wave 5 stash-isolate-restore turbulence on `Program.cs` (Morty
    + Newman). The Wave 5 case did NOT result in a sweep -- the
    `shared-file-protocol` "Shared working tree" rule (added in
    `207d042`) caught it: Morty performed 7 stash-isolate-restore
    cycles, Newman performed 5+, all WIP recovered, no collateral.
    **The new rule earned its keep on its first wave.** Skill is
    working as intended; mark as success, not new finding.
  - **Concurrent-dispatch collision on S02E27** (two parallel sub-agents
    wrote the same skill files). `fleet-dispatch.md` "wave on collision
    risk" rule applies; orchestrator process improvement: check
    `list_agents` for in-flight names before dispatching.
  - **Repo autocommit/sync layer surprise** (per S02E28 lessons): some
    sub-agents report their commits were bundled under unrelated
    subjects. Investigate or relax the prescribed-commit-message
    contract in episode briefs. Wilhelm + Jerry follow-up.
- Mac Keychain test-body rewrite (needs a Mac owner -- held open).
- Linux `systemd-creds` provider (seam exists; not this season).
- The `filename-convention` docs-lint step hard-flip when convenient
  (currently warn-only by design, no urgency).
- `docs/audits/docs-audit-2026-04-22-EPISODE.md` -- placeholder filename
  from a parallel naming scheme that didn't make it into the canonical
  exec-reports tree. Decide: rename + integrate, or archive.

*-- Mr. Pitt (program management), with corrective notes from
George Costanza (product, returning from undeserved bench), Elaine
(structure), Jerry (ops), and a casting assist from Russell
Dalrymple. Lloyd Braun joins the room as junior-lens
reader-of-record.*
