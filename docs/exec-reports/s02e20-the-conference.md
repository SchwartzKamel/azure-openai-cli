# S02E20 -- *The Conference*

> Keith Hernandez takes the stage. J. Peterman polishes the catalog
> copy. Elaine keeps the slide text honest. The LOLBin credentials
> talk gets a complete CFP package, ready to submit.

**Commit:** `<docs commit>` (speaker package) + this report
**Branch:** `main` (direct push)
**Runtime:** ~25 min wall-clock
**Director:** Larry David (showrunner)
**Cast:** Keith Hernandez (lead, DevRel & Conference Speaking),
J. Peterman (guest, Storyteller / Marketing), Elaine Benes (guest,
technical writer)

## The pitch

Build the speaker package for one talk -- "Living Off the Land:
Per-OS Credential Storage in a Single-Binary CLI" -- end to end.
Abstract (long + short), speaker bio (long + short, project-anonymous
placeholders), demo script scripted to the keystroke, slide outline
with a time budget that has to actually sum to the slot, and stage
notes from the speaker who has been on stage enough to know what
fails live.

The submission itself is a human action and is explicitly out of
scope. This episode produces the package.

## Scene-by-scene

### Act I -- Abstract (Peterman drafts, Elaine tightens)

Two versions of the abstract: ~150-word long form for selection
committees, ~50-word short form for printed program / website.
Peterman's catalog instinct ("you press the trigger and the sentence
rewrites itself") tuned down to conference-committee register.
Elaine's pass: every term defined on first use, no "LOLBin" without
the gloss, no "AOT" without context.

Lands at 156 / 50 words, plus a one-paragraph pitch to the program
committee for the cover letter field that some CFP forms include.

### Act II -- Speaker bio

Project-anonymous. Two versions (~80 / ~25 words). Placeholders
`<Speaker Name>` and `<contact handle>` so any cast member who ends
up giving the talk can fill them in without rewriting the bio.
Bureau bio leans on what the speaker actually does (single-binary
CLIs, first-run wizards, AOT), not awards or follower counts.

### Act III -- Demo script

The load-bearing wall. Three beats: first-run wizard, Linux
libsecret store, macOS `security` CLI store. Each beat: exact
keystrokes, what the audience sees, one or two sentences of patter
to deliver while it runs, and a "fallback if this breaks live" line.

Three "do not improvise" callouts where ad-libbing has historically
blown demos: do not paste a real key, do not `secret-tool` against
a real keyring, do not type a real macOS account password into the
keychain prompt on stage. Walls Keith has hit, walls he would like
the next speaker to not re-hit.

Wall-clock total: 9:30 with a 30-second pad. Fits inside the
slide-outline allotment for "Slide 11 -- Demo (live)" (9.0 min) with
30 seconds of overflow into Slide 12.

### Act IV -- Slide outline

20 slides, 27.0 minutes total. Sums explicitly at the end of the
file. Fits a 25-30 minute slot with a 3-minute pad. If the slot is
exactly 25 minutes, an explicit cut is named (Slide 15, the audit
story; nice-to-have, not load-bearing) plus 30 seconds off the demo
wrap.

Each slide: title, one-line visual direction, 2-3 sentences of
speaker notes, time budget. No SVG / PDF / PPTX -- text outline
only, per scope.

### Act V -- Stage notes

Half-page Keith Hernandez doc. Pre-flight checklist (dongles,
charger, font size, dark background, `NO_COLOR` decision, Wi-Fi
off, Do Not Disturb on, browser tabs zero). Five "things that fail
live and how I recover" entries. Voice notes ("I'm Keith
Hernandez" once, not a catchphrase). Q&A discipline (repeat the
question, "I do not know" is a complete sentence).

### Act VI -- Index

`docs/talks/README.md` lists the two talks now in the directory
(WSL + Espanso, LOLBin credentials), documents the package shape
for future talks, and reserves four future-talk slots.

## What shipped

**Docs:**

- `docs/talks/README.md` (new) -- index for the directory.
- `docs/talks/lolbin-credentials/abstract.md` (new) -- 156 / 50 words
  plus a committee pitch paragraph.
- `docs/talks/lolbin-credentials/speaker-bio.md` (new) -- 79 / 22
  words, project-anonymous.
- `docs/talks/lolbin-credentials/demo-script.md` (new) -- three beats,
  9:30 wall-clock with pad, three "do not improvise" callouts.
- `docs/talks/lolbin-credentials/slide-outline.md` (new) -- 20 slides,
  27.0 minutes summed.
- `docs/talks/lolbin-credentials/stage-notes.md` (new) -- pre-flight,
  failure modes, voice, Q&A.
- `CHANGELOG.md` -- one bullet under `[Unreleased] > Added`.

**Production code:** none. Out of scope.

**Tests:** none. Docs-only episode.

**Not shipped (intentional, per scope):**

- Did NOT submit the talk anywhere. The package is the deliverable;
  submission is a human action.
- Did NOT design slide visuals (no SVG, PDF, or PPTX). Text outline
  only.
- Did NOT record a demo video. Out of scope; flagged for a future
  episode if asciinema fallbacks need to live in-repo.
- Did NOT write a blog post. J. Peterman's catalog copy lives with
  his S02E19 deliverable; cross-link only.
- Did NOT touch production code, glossary, user-stories, or other
  episode docs.
- Did NOT touch orchestrator-owned files (`AGENTS.md`,
  `.github/copilot-instructions.md`, top-level `README.md`,
  `CONTRIBUTING.md`, `.github/agents/*`, exec-reports README or
  writers-room file).

## Lessons from this episode

1. **The timing math is usually wrong on the first pass.** First
   draft of the slide outline came in at 24.5 minutes with a demo
   slot of 8.0 -- which would have put the talk under-time and the
   demo over-time at the same time. Reconciling against
   `demo-script.md` (9:30 with pad) forced a re-budget on Slides 6,
   7, 8, and 14. Slot-fit confidence: **medium-high**, contingent
   on the speaker actually rehearsing the demo to the 9:30 mark.
   Without a rehearsal, the demo will run long and the wrap will
   get cut. Flag this honestly to whoever gives the talk.
2. **"Project-anonymous bio" is a real constraint.** First draft of
   the bio said things only the current cast knows. Pulling that out
   left a generic-but-honest bio that any speaker can pick up.
   Worth doing once per talk package.
3. **"Do not improvise" callouts beat post-mortems.** Encoding three
   specific demo failures from Keith's stage history into the script
   is cheaper than another retro after the next live demo blows up.
4. **One demo, one terminal, one beat.** Encoded into the demo
   script's setup section. The temptation to open a second terminal
   "to show something" is the most common cause of unreadable demo
   slides. Closing that door in the script keeps the door closed on
   stage.

## Metrics

- Diff size: 7 new files, 1 surgical CHANGELOG edit. Roughly
  21 KB of new prose across the talk package.
- Slide count: 20.
- Time budget total: 27.0 minutes (within 25-30 minute slot).
- Demo wall-clock: 9:30 with 30-second pad.
- Tests: none (docs-only).
- Preflight: not applicable (docs-only).
- Markdown validation: ASCII-only check passed (no smart quotes,
  no em/en-dashes, fenced code blocks have language tags, lists
  have blank lines around them).
- CI status at push time: see push log; docs-only changes do not
  trigger build/test workflows.

## Credits

- **Keith Hernandez** (lead): demo script, slide outline, stage
  notes, voice across the package.
- **J. Peterman** (guest): abstract draft, committee pitch
  paragraph, hook on the opening sentence.
- **Elaine Benes** (guest): tightened the abstract from 200 to
  156 words, enforced "no jargon undefined on first use" across
  slides and abstract, scrubbed smart quotes and dashes from the
  full package.
- **Larry David** (director): cast the episode, scoped the "did
  not do" list, owns the `docs/exec-reports/README.md` and
  writers-room file (untouched here).

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>
