# S03E19 -- *The First Hour, Local Edition*

> *Lloyd Braun gets the call sheet a week early, opens the README, and
> says the words every showrunner dreads: "wait, where would I have
> looked for that?" The cold-open is a junior dev with a fresh laptop;
> the act-out is the line that says "Hello!" coming back from a model
> that never touched a cloud.*

**Commit:** `pending` (docs-only push)
**Branch:** `main` (direct push, docs-only per `docs-only-commit` skill)
**Runtime:** ~50 min real time
**Director:** Larry David (showrunner)
**Cast:** Lloyd Braun (lead, junior-dev / onboarding lens),
Elaine (consult, doc structure), Kramer (consult, compat seam facts),
Newman (consult, allowlist threat-model framing), Soup Nazi (gate,
ASCII + style)

---

## The pitch

S03 Arc 3 is *First Local Provider*. The technical episodes that fill
the arc -- E14 *The Daemon*, E15 *The Probe*, E16 *The Allowlist*,
E17 *The Server*, E18 *The Capability Gate* -- are all in flight or
queued. The slate has known for two weeks that the *user-facing* hour
of this arc would land last. That hour is this episode.

Why ship the user doc *before* every dependency has shipped? Because
"first hour" content is not a release-note style post-mortem -- it is
a tutorial that reads cleanly the moment the feature lands. If we
wait until E14-E18 are all green, we ship the tutorial against a
backlog of users who tried Ollama in the meantime, hit walls,
reverse-engineered the env file, and wrote their own notes in
Discord. Beat them to it. Document the path while the path is still
being paved -- mark "(coming soon: S03ENN)" on every dependency, and
the doc converts cleanly to general-availability when the green
lights come on.

The other reason is structural: this is Lloyd's first lead. The whole
point of the Lloyd casting (S02E29 *The Casting Call*; S02E30 *The
Cast*) was to keep a learner-shaped lens permanently on the show.
Letting him lead an onboarding-flavored episode mid-arc is the lens
working as designed -- Lloyd asks the dumb questions a senior cast
would skip, the answers become the doc, the doc becomes the
onboarding floor for everyone who joins after him.

## Scene-by-scene

### Act I -- Planning

Larry David and Mr. Pitt agreed at the S03 mid-season checkpoint that
Arc 3 needed a Lloyd-led lens episode regardless of how E14-E18
landed. The blueprint slot was already there (`s03-blueprint.md`
section E19). Pitt set the scope: "tutorial, ~250-450 lines, ASCII, no code
edits, walks one full path from install to Hi". Costanza signed off
on framing local-as-default-for-some-workloads, citing the FR-018 +
ADR-007 path; Newman pre-cleared the threat-model paragraph for E16
("explicit opt-in to localhost dispatch") so Lloyd could write it
without waiting for the full audit doc.

Decisions locked:

1. **Ollama, not llama-server.** Lowest first-hour friction. Server
   episode (E17) gets its own walkthrough later.
2. **Path B (manual env file) is the honest workaround.** Path A
   (wizard with an `ollama` choice) is documented as **(coming soon:
   S03E14)** rather than handwaved. Lloyd refused to write the wizard
   transcript as if it worked today.
3. **Caveat the workaround precisely.** The built-in `openai` preset
   has `https://api.openai.com/v1` baked in -- you cannot retarget it
   at `localhost` without code. Lloyd called this out in step 6
   instead of letting a reader paste the snippet and get a "DNS
   resolved to a public IP" surprise.
4. **Glossary in-page, not just linked.** Junior readers will not
   click out for "what is a token". Repeat the canonical definitions
   from `docs/glossary.md` and link the master file at the bottom.
5. **Each "coming soon" tag is paired with a workaround.** Lloyd's
   rule: never tell the reader "wait" without telling them what they
   can do today.

### Act II -- Fleet dispatch

| Wave | Agents (parallel) | Outcome |
|------|-------------------|---------|
| **1** | Lloyd Braun (lead) | First-pass tutorial draft, 13 required sections, all "coming soon" tags placed |
| **2** | Elaine (consult), Kramer (consult) | Doc structure cross-check vs. existing `docs/onboarding.md`; compat-seam fact check (preset record shape, env-var conventions, dispatch precedence) |
| **3** | Newman (consult) | Threat-model paragraph for step 5 cleared without waiting on the full E16 audit doc |
| **4** | Soup Nazi (gate) | ASCII validation, em-dash / smart-quote sweep, list-vs-prose ratio kept inside doctrine |
| **5** | Lloyd Braun (review) | Findings-backlog rows opened for every "this assumes prior knowledge" gap surfaced in adjacent docs (see "Lessons" below) |

### Act III -- Ship

- `docs/onboarding/local-providers.md` -- new, ~590 lines (ran long;
  see Lessons #4).
- `docs/exec-reports/s03e19-the-first-hour-local.md` -- this file.
- `README.md` -- one new bullet under Documentation > Operating the CLI,
  linking the new tutorial.
- `CHANGELOG.md` -- one Added line under `[Unreleased]`.
- `docs/exec-reports/s03-writers-room.md` -- E19 row appended to the
  episodes table.
- `docs/findings-backlog.md` -- five new `lloyd-2026-05-L-*` rows
  filed against existing docs that assume context Lloyd did not have.

Preflight: format-check + exec-report-check + ascii-validation. No
code touched, no test deltas. Test count unchanged at the v3 line:
600+ xUnit + ~174 bash integration assertions. (The README banner of
"1,510+ passing tests" is a known stale figure tracked in
`elaine-2026-05-m3` and is not adjusted by this docs-only push.)

## What shipped

**Production code.** None. This is doc-only.

**Tests.** None added; none removed. `make preflight` exec-report-check
is the gate that fires.

**Docs.**

- `docs/onboarding/local-providers.md`. The 13-section tutorial:
  what-is-a-local-provider, prerequisites, install, pull-small-first,
  confirm-serving, tell-az-ai, opt-in, run, verify-it-is-local,
  glossary, gotchas, other-backends, where-to-ask. Every E14-E18
  dependency tagged. Every workaround paired.
- `README.md` -- one-line link from the Documentation > Operating the
  CLI section to the new tutorial.
- `CHANGELOG.md` -- one Added entry in `[Unreleased]`.
- `docs/exec-reports/s03-writers-room.md` -- E19 row appended.
- `docs/findings-backlog.md` -- five new rows under Active.

**Not shipped (intentional follow-ups).**

- The wizard does not yet have an `ollama` choice. That is E14's job
  by design; the tutorial documents the workaround until then.
- `az-ai --doctor` shipped in S03E15 *The Probe*. Step 7 has been
  updated from "(coming soon)" to authoritative; the tutorial reads
  cleanly today.
- The full SSRF / localhost-allowlist threat model lives in E16's
  audit doc when that episode lands. Step 5 is the executive summary;
  the audit will be the long form.
- A Windows-native walkthrough video / GIF is not in scope.
  Russell Dalrymple is welcome to produce one in a launch episode.

## Lessons from this episode

### 1. The senior cast genuinely had a "compat preset has a baked URL" blind spot

Initial drafts of the tutorial waved at "set the base URL to localhost
in `AZ_AI_COMPAT_MODELS`" as if you could. You cannot today -- the
URL is part of the preset record, not the env var. Lloyd caught it on
his first pass through the code (S03E09's `OpenAiCompatAdapter.cs`,
lines 55-80). Filed as `lloyd-2026-05-L-1`: the README's compat
section reads as if the URL is user-supplied. It is not, and that
should be obvious before a reader tries.

### 2. "Wizard supports Ollama" was implied but not promised

The README's First-run section enumerates the five wizard providers.
A reader paging through cannot tell that Ollama is "coming" vs.
"unsupported by design". The exec-report for E11 makes the slate
clear, but a user does not read exec reports. Filed as
`lloyd-2026-05-L-2` against `README.md` First-run section.

### 3. The implicit `chmod 600` is implicit twice

The wizard does it; the README mentions it; nothing in the
hand-edited workflow says "by the way, set 600 on the file you just
wrote". For a user following Path B, this is a real foot-gun --
they are about to put a placeholder credential plus a localhost URL
in a world-readable file. Filed as `lloyd-2026-05-L-3`. The
underlying root cause is `newman-2026-05-K-1` (LOW, open) -- per-provider
example block does not restate chmod 600 hygiene -- so this is a
related-not-duplicate finding scoped to the new local-providers
walkthrough.

### 4. The doc ran long

Target was 250-450 lines. We landed at ~590. The required-sections
list (13 numbered topics) plus glossary plus gotchas plus
forward-references baked in the floor. We are not trimming for
trim's sake -- a junior reader benefits from the redundancy. But the
overage is documented and Lt. Bookman is on standby for a brevity
pass after the E14-E18 forward references go live and we can drop
the workaround prose.

### 5. The opt-in env var (E16) needs a name *now*

The tutorial uses `AZ_AI_LOCAL_PROVIDERS=1` because that is the name
in the slate. If E16 picks a different name, this doc has to change
in step 5, step 11, and the gotcha box. Filed as
`lloyd-2026-05-L-4`: pin the name in an ADR-pending entry before E16
ships so this tutorial does not need a search-and-replace pass on
arrival. Newman owns.

### 6. "First-hour" content has to ship before the polish

A junior dev who tries Ollama with `az-ai` today and fails will not
come back next week. They will write a Discord post titled "az-ai
doesn't work with Ollama" and we will spend a quarter denying that.
Better to ship the tutorial early, marked clearly as forward-looking,
than to ship it late behind a perfect feature.

## Findings opened

| ID | Doc | Line / section | Gap |
|---|---|---|---|
| `lloyd-2026-05-L-1` | `README.md` Documentation section + `docs/onboarding/local-providers.md` step 6 | First-run section | Implies `AZ_AI_COMPAT_MODELS` URL is user-supplied; today it is preset-baked |
| `lloyd-2026-05-L-2` | `README.md` | First-run wizard transcript | Wizard provider list reads as exhaustive; no "Ollama coming in S03E14" hint |
| `lloyd-2026-05-L-3` | `docs/onboarding/local-providers.md` step 4 + `README.md` per-provider block | env-file write step | `chmod 600` not co-located with the manual-write instruction (related: `newman-2026-05-K-1`) |
| `lloyd-2026-05-L-4` | (cross-doc) | E16 owner area | Opt-in env var name `AZ_AI_LOCAL_PROVIDERS=1` not yet pinned in any ADR; tutorial step 5 + step 11 will need a sweep if E16 picks a different name |
| `lloyd-2026-05-L-5` | `docs/glossary.md` | (missing entries) | "Quantization" and "Context window" terms cited by tutorial step 10 are not in the master glossary; tutorial defines them in-line, master file should pick them up |

All five rows added to `docs/findings-backlog.md` under Active with
state `open`, severity `LOW` (these are gaps in user-facing prose, not
runtime defects). Owners: Elaine for L-1 / L-2 / L-5; Lloyd Braun for
L-3 (with a hand-off to Newman if K-1 is closed first); Newman for
L-4.

## Metrics

- Diff size: 5 files touched (1 new tutorial, 1 new exec report, 3
  appends: README, CHANGELOG, writers-room, findings-backlog --
  technically 4 appends counting findings-backlog).
- Lines added: ~590 (tutorial) + ~280 (this report) + ~10 (the four
  short edits) = ~880.
- Lines removed: 0.
- Tests: unchanged (docs-only push).
- Preflight: format-check + exec-report-check + ascii-validation.
  No build or test invocation expected to react.
- CI status at push time: green at HEAD before push; expected green
  after (no code paths exercised).

## Credits

- **Lloyd Braun** -- lead, drafting, gap-finding, voice. Wrote the
  tutorial, this exec report, and the five findings rows.
- **Elaine** -- consult on doc structure, glossary cross-link
  discipline, and the "what does docs/onboarding.md already cover"
  scope question.
- **Kramer** -- consult on the compat-seam facts: preset record
  shape, env-var conventions, dispatch precedence as shipped in
  S03E09 *The Compat*. Confirmed the "URL is baked into the preset"
  caveat is accurate.
- **Newman** -- consult on the step-5 threat-model paragraph,
  pre-clearing language ahead of E16's full audit.
- **Soup Nazi** -- ASCII gate, smart-quote / em-dash sweep, list-vs-prose
  ratio audit. NO MERGE FOR YOU averted on the first pass.
- **Larry David** -- showrunner sign-off, casting Lloyd as the lead
  for an onboarding-shaped mid-arc episode rather than waiting for
  the arc finale to give him a turn.

```text
Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>
```

---

## Process notes

For the next Lloyd-led episode, three small process tweaks fell out of
this one:

1. **Pre-flight the cross-references.** Lloyd spent a chunk of the
   first hour verifying which "coming soon" episode numbers were real
   vs. aspirational. The slate (`s03-blueprint.md`) is the source of
   truth and was, in fact, accurate -- but a short crib-sheet of
   "episode numbers cited by this doc, dated as of push" embedded in
   the exec report would let a future re-read confirm the references
   in seconds. Process gap, not a content gap.
2. **Findings rows for forward-looking docs.** Three of the five
   findings opened by this episode are technically against doc text
   that does not yet exist (the future E14 wizard transcript, the
   future E15 doctor output). The findings-backlog skill was written
   for "audit found a gap in shipped artifact"; we are stretching it
   to "tutorial author found a gap that will appear if E14-E16 ship
   the wrong way". Mr. Wilhelm flagged this as a meta-question for
   the season finale retro; logged for now, not blocking.
3. **Doctrine question for Lt. Bookman.** This tutorial is long. The
   brevity tier doctrine (S03E02) does not yet have an explicit
   tier for "first-hour onboarding tutorial", which is structurally
   long because it inlines the glossary, the gotchas, and the
   forward references for the user. Suggest adding a tier or an
   explicit exemption. Bookman to decide; not blocking this push.

## Tag scene -- next episode preview

**S03E20 -- *The Switch*.** `az-ai --provider`, `--profile`,
`AZ_PROVIDER`, `AZ_PROFILE`. Costanza leads. The precedence chain
finally gets named in `--config show`. Lloyd already has a stack of
"how do I switch back to Azure for one prompt?" questions queued up
for the lens pass. Hello!
