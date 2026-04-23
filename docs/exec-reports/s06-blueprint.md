# Season 6 -- Blueprint -- *Dogfooding*

> *Pre-season treatment. Twenty-four candidate episodes, in slate
> form. Showrunner-of-showrunners override locked; Larry's greenlight
> per-episode still required.*

**Lead:** Jerry (DevOps / dev-workflow automation -- first blueprint lead)
**Guest:** Kramer (engineer-using-it; the honesty check on every pitch)
**Drafted:** 2026-04-22
**Supersedes:** Mr. Pitt's prior S06 recommendation (Enterprise &
Compliance) per the showrunner override below. Enterprise & Compliance
moves to S07.

---

## Showrunner override note

The showrunner-of-showrunners directed S06 = *Dogfooding*, overriding
Mr. Pitt's prior recommendation of S06 = *Enterprise & Compliance*
(seasons-roadmap commit `4efe8a7`). Enterprise & Compliance moves to
S07 with no other change to its content; the Newman / Frank / Jackie
anchor block stays intact and the buyers-asking-now urgency note is
preserved verbatim. Mr. Pitt's roadmap pad missed dogfooding as a
candidate -- an honest planning miss, not a casting issue. Logged as a
process finding for the next roadmap pass (see *Roadmap retrospective*
in `seasons-roadmap.md`). Jerry leads S06 because the spine of the
season is dev-workflow automation -- CI, scripts, the Makefile,
release plumbing -- and Jerry has been bench-warm for too long.

## Theme statement

`az-ai` becomes the tool we *ship* AND the tool we *use* daily to ship
it. Every workflow on this team that today routes through a non-az-ai
LLM CLI -- commit messages, PR descriptions, exec-report drafting, CI
triage, release-note synthesis, code-review pre-pass, persona-spawned
sub-tasks, AHK/Espanso desktop flows -- gets a documented az-ai path.
Where the path is good enough, we adopt it. Where it isn't, we file
the gap as a feature request against ourselves, and Kramer (the
honesty check) gets a veto on any pitch he wouldn't actually run on a
real workday.

This is a meta season. The win condition at finale is binary:
**a non-trivial daily workflow runs through `az-ai` end-to-end with no
other LLM CLI in the loop**. Not "az-ai plus Cursor plus Copilot Chat
with az-ai doing 30% of the typing" -- az-ai end-to-end, for at least
one multi-day workflow that produces a shippable artifact (a release,
a merged PR with az-ai-authored description, an exec report). Anything
short of that is a partial season we ship and re-greenlight in S08+.

## Why this season, why now

The 2026 dogfooding norm has shifted from "marketing claim" to
"product evidence." Anthropic publicly reports 59% of daily work at
the company runs through Claude Code (up from 28% a year prior),
backed by 200,000 internal transcripts and 130 engineer surveys --
*"antfooding"* is now table-stakes language for any AI dev-tool team
("Claude Code: The Ultimate Dogfooding Success Story", FourWeekMBA,
2026; "How Anthropic Teams Use Claude Code", ernestchiang.com, 2025).
Cursor runs every internal PR through their own AI review pipeline,
sometimes fully autonomously (Unite.AI, Dec 2025). GitHub Copilot's
own engineering team uses Copilot for code completion, PR generation,
and code review, and instruments the result as primary input for the
next iteration. The pattern is consistent: the teams that ship the
best AI dev tools are the ones whose own daily workflow depends on
them.

The cost of *not* dogfooding is also documented. Sue Ellen's S02E19
*The Competition* brief flagged that we were the only credible CLI
in the 2026 landscape with no public statement of internal use. The
Charlie-Sheen-rule failure mode -- "make me a sandwich" tools whose
own teams use someone else's sandwich -- shows up in user trust
surveys as the top reason a power user bounces from a developer
tool's docs. We have shipped four seasons of capability. S06 turns
that capability inward and proves it.

The tooling layer matured in the same window. `aicommits` and
`opencommit` normalized LLM-generated commit messages with reported
85-95% accuracy on small atomic commits (di-sukharev/opencommit and
Nutlope/aicommits, 2026). PR-description generation is now a
checklist item across Devin, Cursor, and GitHub Copilot for PRs.
CI-triage bots (Phind CI Bot, Copilot CI) report 85-90% accuracy on
routine failure classes. None of these are *az-ai* -- and none of
them have to be -- but they prove the workflows are real, the
accuracy is acceptable, and a single-binary AOT CLI with our agent
loop, our persona memory, and our cost guardrails can land each one
without inventing a new pattern.

## Hard dependencies on prior seasons

S06 is **not independent.** The blueprint assumes S03, S04, and S05
have all shipped before S06E01 starts filming. State this loudly: if
the showrunner-of-showrunners reorders the calendar and S03/S04/S05
slip, S06 either *ships thin* (a 12-episode mini-season covering only
what the available primitives support) or *waits.* We cannot
pre-empt the seasons we depend on.

| S06 episode arc | Required from S03 | Required from S04 | Required from S05 |
|---|---|---|---|
| E01-E04 git/GitHub loop (commits, PRs, branches, issues) | S03E07 keystore (no leaked PAT in prompts), S03E04 redactor | S04E05 picker / S04E08 `--prefer cost` for cheap, fast, hot-path | -- |
| E05-E08 CI triage, log summarisation, flake detection, release notes | S03E12 providers doctor (which provider is alive when CI is on fire) | S04E06 fallback (don't go down with the model), S04E19 budget | -- |
| E09-E12 code review pre-pass, rubber duck, ADR drafting, spec-from-issue | S03E20 multi-provider personas | S04E09 eval (rubber-duck quality regression test), S04E13 cache | S05E08 self-MCP-server round-trip (rubber-duck consumes az-ai as MCP server) |
| E13-E16 exec-report drafting, writers'-room casting, persona memory across sessions | S03E20 multi-provider personas | S04E10 corpus (exec-report quality corpus) | -- |
| E17-E20 AHK/Espanso integration, squad personas spawned for backlog | S03E08 wizard reprise | S04E17 estimate (per-keystroke cost) | S05E09 manifest plugins (AHK/Espanso as plugins, not just snippets) |
| E21-E23 self-hosted MCP server, az-ai-driven backlog grooming, release readiness | -- | S04E11 CI-eval gate | **Hard:** S05E05 `mcp serve`, S05E13 workspace-trust, S05E24 reference plugins |
| E24 finale: one full week of "az-ai only" | All of S03 | All of S04 | All of S05 |

Soft note: if S05 slips one season, E21-E24 retarget to a stub MCP
server that talks only to this repo, no plugin marketplace surface.
The finale still ships, just with a smaller blast radius.

## Landscape snapshot (2026)

Compact view -- full long-form lives across the cited references in
the *Why this season* section.

| Team | Pattern | Public-evidence claim | What we steal | What we skip |
|---|---|---|---|---|
| Anthropic (Claude Code) | "Antfooding" -- internal use as primary product input | 59% of daily work runs through Claude Code; published 200k transcripts | Self-verification loop (codegen -> tests -> CI -> fix); cross-team adoption (not just engineers) | We don't have 200k internal transcripts and we shouldn't pretend we do |
| Cursor | Every PR through AI review, sometimes autonomous | Public statements via Unite.AI, Dec 2025 | PR pre-pass on every internal PR, opt-in autonomous on a labelled subset | We don't merge autonomously -- Larry's call, see open questions |
| GitHub Copilot | IDE-embedded, telemetry-driven, self-improvement loop | Internal use across GitHub eng | Telemetry of dogfooding itself (Frank Costanza E15); fix-our-own-bugs-with-our-own-tool feedback loop | We are not building an IDE this season -- editor surfaces are S10 |
| `aicommits` / `opencommit` | Stand-alone CLI, OpenAI/Claude/Gemini backends, 85-95% accuracy on small commits | GitHub README + 2026 reviews | Workflow shape (`az-ai commit`), prompt, fail-mode language ("diff too large -- split commit") | The standalone-tool framing -- ours is one subcommand on one binary |
| Phind CI Bot / Copilot CI | LLM diagnoses pipeline failures, suggests fix | 85-90% accuracy on routine triage | The triage-tier classification table (already in `ci-triage.md`); pair it with `az-ai ci diagnose <run-id>` | The "auto-comment on PR" surface for now; that's a S07 enterprise nicety |

Negative example -- the Charlie-Sheen-rule failure: dev tools whose
own engineering teams use a competitor's product internally pay a
trust tax that shows up in adoption surveys. The classic 2024-25
examples (we won't name them; not the point) all share the same
post-mortem: *"we built it for users, we never used it ourselves,
and the daily-driver pain points only surface when you're the one
hitting them."* That is the *don't* of this season.

## Daily-workflow inventory (the "before" picture)

Cross-reference: Frank Costanza is producing
`docs/dogfooding-baseline-2026-04.md` in parallel. That doc is
**Frank's territory** -- this blueprint cites it; it does not edit
it. The inventory below is a forward-look working list; Frank's
baseline is the canonical "before" measurement.

| # | Workflow | Today's tool / process | Target az-ai integration | Friction today | Friction after |
|---|---|---|---|---|---|
| 1 | Commit message authoring | Hand-typed; occasionally Copilot Chat in IDE | `az-ai commit` consumes `git diff --staged`, emits Conventional Commits per `commit.md` skill | 30-90s per commit, occasional embarrassing subjects | <5s, consistent style, trailer auto-applied |
| 2 | PR description drafting | Hand-typed against branch diff; sometimes copy-pasted into Claude/Cursor | `az-ai pr describe` reads commits + diff + linked issue | 5-15 min per non-trivial PR | <60s, structurally consistent |
| 3 | Branch / issue naming | Ad hoc | `az-ai name <branch|issue>` from short intent | Inconsistent slugs | Conformant to `<agent>/<kebab>` in `commit.md` |
| 4 | Issue creation from idea | Hand-typed in GitHub web UI | `az-ai issue draft` produces title + body + labels | 3-10 min; often skipped, ideas lost | <60s, captured |
| 5 | CI failure diagnosis | `gh run view --log-failed` + manual classification per `ci-triage.md` | `az-ai ci diagnose <run-id>` runs the triage flow as a tool call | 5-15 min per red run | <90s + classification confidence score |
| 6 | Release-note synthesis from CHANGELOG | Hand-curated by Mr. Lippman | `az-ai release notes <tag>..<tag>` reads CHANGELOG + commit log | 30-60 min per cut | <5 min, Lippman edits, doesn't draft |
| 7 | Exec-report drafting (this doc, every blueprint) | Sub-agent in another LLM CLI | `az-ai persona <name> --task "draft exec-report"` | This doc itself is evidence -- not in az-ai today | Self-evident at finale |
| 8 | Code-review pre-pass | Manual; sometimes a second agent | `az-ai review --diff <range>` runs the rubber-duck pre-pass | 10-30 min per PR | <2 min; reviewer confirms, doesn't originate |
| 9 | ADR drafting from a kitchen-table decision | Hand-typed | `az-ai adr new --from-thread <issue>` | Decisions slip into commit bodies, never get an ADR | Captured per FR-NNN cadence |
| 10 | AHK / Espanso desktop snippet expansion | Pre-canned text; occasionally LLM call out-of-band | First-class az-ai snippet with persona context | Friction-free *expansion*, friction-heavy *authoring* | Authoring loop closes |
| 11 | Backlog grooming (off-roster items, S02 findings) | Manual review every few weeks | `az-ai backlog groom` reads `docs/exec-reports/s02-writers-room.md` *Off-roster* + `docs/proposals/` and proposes triage | Drift; off-roster items rot | Surfaced weekly |
| 12 | Release readiness checklist | Lippman runs it from memory; sometimes a markdown file | `az-ai release ready` automates the checklist (CHANGELOG, version bumps, CI green, Trivy clean, docs lint clean) | One missed step per ~5 releases | Zero, or fail loud |

## 24-episode candidate slate

Casting weights observed: **Jerry 5** (lead -- DevOps spine), **Kramer
4** (engineer-using-it honesty), **Costanza 3** (PM workflows),
**Elaine 2** (doc workflows), **Newman 2** (review pre-pass +
write-access trust), **Lloyd Braun 1** (junior-lens "first day using
az-ai for everything"), **The Maestro 1** (prompt library used in
real workflows), **Frank Costanza 1** (telemetry of dogfooding
itself), **Kenny Bania 1** (perf-as-we-use-it), **Mickey Abbott 1**
(a11y of the daily workflow), **Mr. Pitt 1** (finale ensemble).
Supporting players each get at least one named appearance.

### Arc 1 -- the daily git/GitHub loop (E01-E04)

- **S06E01 -- *The Subject Line*.** **Jerry (lead), Kramer (guest).**
  `az-ai commit` ships. Reads `git diff --staged`, emits a
  Conventional Commits subject + body that conforms to
  `.github/skills/commit.md`. Auto-applies the Copilot trailer.
  Default model from S04 picker; `--prefer cost` for hot path. Fail
  mode: "diff too large -- split commit" copied verbatim from the
  opencommit playbook. Kramer's honesty check: he uses it for one
  full day before sign-off.
- **S06E02 -- *The Description*.** **Jerry (lead), Costanza (guest).**
  `az-ai pr describe` -- reads `git log <base>..HEAD`, the diff, and
  any linked issue body via `gh`. Outputs the PR template the repo
  uses. Costanza vetoes any output that buries the user-visible
  change below the implementation detail.
- **S06E03 -- *The Slug*.** **Jerry (lead), Soup Nazi (guest).**
  `az-ai name <branch|issue|file>` -- generates kebab-case slugs
  conformant to the `<agent>/<kebab-topic>` convention. Soup Nazi
  enforces the slug regex. Tiny episode, big quality-of-life win.
- **S06E04 -- *The Backlog Item*.** **Costanza (lead), Lloyd Braun
  (guest).** `az-ai issue draft` from a one-line intent. Reads
  `docs/proposals/` for FR-NNN style, suggests labels. Lloyd asks
  "where would I have looked for this template?" -- the answer ships
  as a docstring on the subcommand.

### Arc 2 -- CI, logs, flakes, releases (E05-E08)

- **S06E05 -- *The Triage*.** **Jerry (lead), Frank Costanza (guest).**
  `az-ai ci diagnose <run-id>` -- talks to GitHub Actions via `gh`,
  pulls failed-job logs, runs the classification table from
  `.github/skills/ci-triage.md`, returns a classification + suggested
  owner + a one-paragraph fix-forward. 85% target accuracy on the
  triage tier; eval gate from S04E11.
- **S06E06 -- *The Long Log*.** **Jerry (lead), Kenny Bania (guest).**
  Log summarisation -- a generic `az-ai summarize --logs` that
  handles 200KB+ inputs by chunking with the S04 cache. Bania
  benchmarks it against `tail -200 | grep -i error`; the LLM has to
  *win* on signal-to-noise or the episode is killed.
- **S06E07 -- *The Flake*.** **Puddy (lead), Kramer (guest).**
  `az-ai flake suspect <test-name>` -- correlates the test against
  recent runs, looks for environmental confounders, recommends
  quarantine vs fix. Pairs with the existing flake-quarantine
  pattern in `ci-triage.md`. Puddy's call on every output.
- **S06E08 -- *The Release Note*.** **Mr. Lippman (lead), Elaine
  (guest).** `az-ai release notes <tag>..<tag>` -- reads CHANGELOG +
  the commit log, drafts the release post in the house voice.
  Lippman edits; he does not author. Elaine owns final structural
  pass.

### Arc 3 -- review, rubber-duck, ADRs, specs (E09-E12)

- **S06E09 -- *The Pre-Pass*.** **Newman (lead), Kramer (guest).**
  `az-ai review --diff <range>` runs a security + correctness
  pre-pass. Newman's seal: zero false-positive secret-leak hits in
  the eval corpus or the episode does not ship. Output is a
  *suggestion* surface, not a merge gate (the merge gate is
  Soup Nazi's, see Wilhelm's S07).
- **S06E10 -- *The Rubber Duck*.** **The Maestro (lead), Kramer
  (guest).** Promotes the rubber-duck pattern to a first-class
  `az-ai duck` subcommand. Reads stdin, persona-aware (defaults to
  Kramer's persona), routes via S05E08 self-MCP-server when
  available. Maestro owns the prompt library entry; eval corpus from
  S04E10.
- **S06E11 -- *The Decision*.** **Costanza (lead), Elaine (guest).**
  `az-ai adr new --from-thread <issue|pr>` -- drafts an ADR-XXX in
  the house template from a kitchen-table decision thread. Closes
  the gap between "we decided" and "we wrote it down."
- **S06E12 -- *The Spec*.** **Elaine (lead), Lloyd Braun (guest).**
  `az-ai spec from <issue>` -- one issue in, one user-story shaped
  spec out, conformant to the `docs/user-stories.md` style established
  in S02E11. Lloyd reads each output and flags the still-confusing
  ones; that becomes the eval corpus.

### Arc 4 -- exec reports, writers'-room, persona memory (E13-E16)

- **S06E13 -- *The Treatment*.** **Costanza (lead), Larry David (guest).**
  `az-ai exec-report draft --season <n> --topic <slug>` -- this
  blueprint shape, generated. Eats `docs/exec-reports/_template.md`,
  prior blueprints, and a one-paragraph intent. Larry's veto on tone
  is final.
- **S06E14 -- *The Casting*.** **Costanza (lead), Russell Dalrymple
  (guest).** `az-ai writers-room cast --task <one-liner>` reads
  `AGENTS.md` and the agent archetype files, proposes 1 lead + 2
  guests with rationale. Russell vetoes any cast that violates the
  "no two consecutive episodes share the same lead" rule.
- **S06E15 -- *The Telemetry of the Dog*.** **Frank Costanza (lead),
  Morty Seinfeld (guest).** Telemetry of dogfooding *itself* -- per
  internal user, per workflow, count and outcome. Opt-in. Output is a
  weekly internal-use report. Morty co-authors the cost view. This is
  also the episode where we decide whether to ship the dogfooding
  metrics back out to public users (open question 3).
- **S06E16 -- *The Continuity*.** **The Maestro (lead), Babu Bhatt
  (guest).** Persona memory across our own sessions. Today personas
  forget between invocations on different machines. This episode
  ships an opt-in syncable persona memory store (file under
  `.squad/history/`, optional git-tracked). Babu owns Unicode/encoding
  correctness across hosts.

### Arc 5 -- desktop integrations + persona-spawned subagents (E17-E20)

- **S06E17 -- *The Snippet, Authored*.** **Bob Sacamano (lead), Jerry
  (guest).** AHK/Espanso integration *we ship* gets paired with the
  one *we use* daily. Bob ships a reference Espanso package; Jerry
  wires the local triggers we actually want.
- **S06E18 -- *The Snippet, Used*.** **Kramer (lead), Mickey Abbott
  (guest).** Kramer drives a full workday on the Espanso/AHK loop.
  Mickey audits the keystroke ergonomics and screen-reader output of
  every triggered surface. If a snippet doesn't pass Mickey's a11y
  audit, it doesn't ship in the reference pack.
- **S06E19 -- *The Subagent*.** **Kramer (lead), The Maestro (guest).**
  Squad personas spawned for our backlog items. `az-ai persona
  reviewer --task "triage docs/exec-reports/s02-writers-room.md
  Off-roster"` runs and produces a triage table. Maestro owns the
  spawn-prompt library.
- **S06E20 -- *The Loop*.** **Kramer (lead), Newman (guest).**
  Self-verification loop -- `az-ai` runs codegen, runs `make
  preflight`, reads the failure, fixes it, retries. Bounded depth (3,
  per `RALPH_DEPTH`). Newman's gate: the loop **never** gets write
  access to `main` without a human approval; commit signing remains
  human-only per `commit.md`.

### Arc 6 -- self-MCP, grooming, release readiness (E21-E23)

- **S06E21 -- *The Self-Server*.** **Kramer (lead), Newman (guest).**
  Self-hosted MCP server pointed at this repo. `az-ai mcp serve
  --workspace .` exposes our tools to ourselves (and, deliberately,
  to Claude Code / Codex CLI / gh copilot if a developer wires them
  in). Reuses S05E05 + S05E13 workspace-trust. Newman audits the
  trust boundary every day this episode films.
- **S06E22 -- *The Grooming*.** **Costanza (lead), Mr. Pitt (guest).**
  `az-ai backlog groom` reads `s02-writers-room.md` *Off-roster*,
  `docs/proposals/`, and the candidate slate in `seasons-roadmap.md`,
  and proposes a weekly triage. Mr. Pitt's roadmap pad becomes the
  output target.
- **S06E23 -- *The Checklist*.** **Mr. Lippman (lead), Jerry (guest).**
  `az-ai release ready` automates the release checklist: CHANGELOG
  hygiene, version bumps, CI green, Trivy clean, docs lint clean,
  AOT size budget intact, integration suite green. Lippman cuts the
  release; Jerry owns the `make` plumbing underneath.

### Arc 7 -- the finale (E24)

- **S06E24 -- *The Week*.** **Mr. Pitt (lead, ensemble), every main
  cast member as guest.** One full calendar week of "az-ai only"
  daily workflow. No Cursor, no Copilot Chat, no Claude Code at the
  prompt. Jerry, Kramer, Costanza, Elaine, Newman each commit to one
  shippable artifact during the week (a release, a merged PR with
  az-ai-authored description, a published exec report, a security
  pre-pass log, a doc refresh). End-of-week retro: per-workflow
  go / no-go on permanent adoption. Frank publishes the metrics.
  Larry signs the verdict.

## Cross-references

- **FR-NNN proposals.** FR-005 (shell integration) and FR-022/023
  (wizards) feed the Espanso/AHK arc (E17-E18). FR-008 (cache) is a
  hard prerequisite for the rubber-duck cost story (E10) and is
  shipping in S04E13. FR-015 (cost estimator) feeds E15 (telemetry)
  and E20 (loop budget).
- **Skills.** Every Arc-1 episode consumes
  [`commit.md`](../../.github/skills/commit.md) as the prompt
  contract. Arc-2 consumes
  [`ci-triage.md`](../../.github/skills/ci-triage.md) verbatim.
  Arc-7 consumes [`preflight.md`](../../.github/skills/preflight.md)
  inside the self-verification loop. The S02E27/E28/E29 skill
  specials produce additional skills the season may consume; they
  are in flight and we will cite the final filenames once they land.
- **Dependent S03/S04/S05 episodes.** See *Hard dependencies* table
  above. Top three to flag for re-prioritisation if the
  showrunner-of-showrunners is open to nudging the calendar:
  **S05E05** (`mcp serve`) -- without it E10/E21/E24 fall back to a
  stub server; **S04E11** (CI eval gate) -- without it Arc 2's
  accuracy claims have no enforcement; **S03E20** (multi-provider
  personas) -- without it E16 persona-memory work is single-provider
  only and E19 spawn library is brittle.
- **Cited research.** *"Claude Code: The Ultimate Dogfooding Success
  Story"* (FourWeekMBA, 2026); *"How Anthropic Teams Use Claude
  Code"* (Ernest Chiang, 2025); *"Cursor Bets on Product, Not
  Models"* (Unite.AI, Dec 2025); `Nutlope/aicommits` and
  `di-sukharev/opencommit` repos (commit-message accuracy data,
  2026); Phind CI Bot and Copilot CI public docs (CI-triage accuracy,
  2026).

## Risks and known unknowns

1. **Circular debug.** When `az-ai` breaks, we cannot use `az-ai` to
   fix it. Mitigation: keep one non-az-ai LLM CLI installed and
   *unused* on every dev machine as a break-glass; `make preflight`
   stays runnable without `az-ai` in the loop.
2. **Sample-size-of-one bias.** Our team is not the user base. Every
   workflow we adopt internally must come with a public-user proxy:
   either a published recipe, a user-survey signal, or an issue from
   an outside contributor asking for the same thing. Costanza owns
   this gate.
3. **Cost run-up.** If we use it for everything, we pay for
   everything. Morty Seinfeld co-authors E15 (telemetry) specifically
   to catch this; soft cap from S04E19 is mandatory in every
   internal-use shell profile.
4. **Eval drift.** Tuning prompts and defaults for our workflows may
   regress them for other users. Every Arc-3 / Arc-4 episode that
   touches a prompt library entry must add a public-user case to the
   S04E10 corpus, not just an internal one.
5. **Security risk -- write access to our own repo.** Self-verification
   loops, persona-spawned subagents, and self-MCP-server arcs all
   point at giving `az-ai` write access to `main`. **Hard rule:**
   `az-ai` does not push to `main`. It produces commits in branches,
   opens PRs, and lets a human merge. Newman owns this boundary
   across E09, E20, and E21.
6. **Workflow lock-in.** If everything goes through `az-ai`, the
   cost of unwinding is high. Every adopted workflow must keep its
   non-az-ai fallback documented and tested -- if Espanso, the raw
   snippet still works without LLM expansion; if `az-ai commit`, the
   raw `git commit -m` path stays first-class.
7. **Bonus -- the dependency stack.** S06 leaning on S03 + S04 + S05
   means a slip anywhere upstream slips us. Mr. Pitt and Mr. Wilhelm
   own the calendar protection.

## What S06 does NOT cover (boundary)

- **NOT new external integrations.** We are *consuming* what S03
  (providers), S04 (intelligence), and S05 (protocols + plugins)
  shipped. We are not adding more integrations this season.
- **NOT Enterprise / SSO / compliance.** That moved to S07 per the
  showrunner override. Newman / Frank / Jackie's anchor block is
  intact; they appear as guests here, not leads, and the audit-log
  / SSO / policy work waits.
- **NOT building features just for our own use without a user case.**
  Costanza's gate from Risk #2 -- if a pitch only serves us, kill
  it before it becomes an episode.
- **NOT "rewrite the whole dev workflow in az-ai."** Pragmatic
  adoption, not religion. Where a non-az-ai tool wins, we keep it
  and document why. The finale's win condition is *one* non-trivial
  workflow end-to-end, not *every* workflow.

## Open questions for showrunner greenlight

1. **Do we accept `az-ai` writing PR descriptions for the
   orchestrator's own commits?** That is, do we close the loop where
   `az-ai` describes the work `az-ai` co-authored? Newman flags
   prompt-injection surface; Maestro flags eval bias.
2. **Do we allow `az-ai` to merge?** Default position: no. But the
   self-verification loop in E20 raises the question explicitly --
   does *labelled* auto-merge for, say, a `style: dotnet format
   cleanup` commit class qualify, or is the human-merge rule
   absolute?
3. **Do we ship dogfooding metrics back out to public users?**
   Frank's E15 telemetry produces them; the question is whether the
   weekly internal-use report becomes a public-facing
   "transparency log" the way Anthropic's antfooding numbers are.
4. **Do we eat our own Ralph-mode dog food on this season's CI?**
   That is, does the S06 development cycle itself run through Ralph
   mode for backlog grooming and triage? High-signal demo if it
   works, embarrassing demo if it doesn't.
5. **Does the finale's "az-ai only week" include Larry?** The
   showrunner-of-showrunners using `az-ai` for the season-finale
   blueprint of S07 would be the strongest possible signal -- and
   the strongest possible failure mode. Larry's call.
