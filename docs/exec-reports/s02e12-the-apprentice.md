# S02E12 -- *The Apprentice*

> Lloyd Braun runs the project setup as a literal first-time
> contributor, writes down every moment of friction, and ships the
> onboarding doc he wishes had existed an hour ago.

**Commit:** `b6be974` (onboarding) + this report
**Branch:** `main` (direct push, per `.github/skills/commit.md`)
**Runtime:** ~30 minutes wall-clock
**Director:** Larry David (showrunner)
**Cast:** Lloyd Braun (lead, junior dev / onboarding lens), Elaine
Benes (guest, technical writer -- prose passes), Jerry Seinfeld
(guest, DevOps -- build / CI explanations), Cosmo Kramer (guest,
engineer -- on call when Lloyd hit a code path)

## The pitch

Lloyd's first episode as lead. The whole point of the casting is
that he asks the questions Kramer assumes everyone knows. The README
is good and `CONTRIBUTING.md` (S02E17) is good, but a literal
first-time contributor still bounces between five files trying to
figure out which directory to build, which env var is named what, and
where their key lands on disk. Nobody had walked through the setup as
a beginner and written down every moment of friction.

This episode does exactly that. It produces `docs/onboarding.md` --
the first-hour walkthrough -- and appends six entries to
`docs/glossary.md` for the acronyms that surfaced. No production code
touched, no existing docs rewritten. The friction log itself is the
most valuable artifact: it names the gaps so future episodes can fix
them.

## Scene-by-scene

### Act I -- Walk through it

Lloyd ran the setup from `git clone` forward, taking notes the whole
way. The friction log is the deliverable; see "Lessons" below for the
top entries. Roughly 47 minutes of wall-clock from a fresh checkout
on a Linux laptop with the .NET 10 SDK already installed. The
bottleneck was the env-var spelling (Q3 below), which cost ten
minutes by itself.

### Act II -- Cross-reference the existing surfaces

Reviewed each of the surfaces a first-timer hits and noted what each
tells them, what it assumes, what it omits:

| Surface | Tells | Assumes | Omits |
|---|---|---|---|
| `README.md` | quickstart, env vars, key storage table, install paths | reader knows v1 vs v2 dirs; reader spots `AZUREOPENAIAPI` vs `_KEY` | preflight is not in the quickstart; SDK vs runtime distinction |
| `CONTRIBUTING.md` (S02E17) | 30-sec orientation, preflight rules, first PR ideas, fleet | reader will run `make setup` themselves | does not walk a literal first 60 minutes; assumes reader will discover the wizard |
| `docs/prerequisites.md` | env-var reference + the `KEY` vs `API` callout | reader has clicked through to it | nothing -- this file is good |
| `azureopenai-cli/Setup/FirstRunWizard.cs` | the actual humans-first credential flow | reader will run `az-ai` first | not advertised in CONTRIBUTING |
| `Makefile` (`make help`) | curated subset of targets | reader will not skim the file | `bench-quick`, `scan`, `audit`, `preflight` are absent from `make help` |
| `.github/skills/preflight.md` | the four checks, why they exist | reader has been pointed here from CONTRIBUTING | nothing -- this file is good |
| `.github/skills/commit.md` | conventional commit rules, trailer | reader will go read it | nothing -- this file is good |

`docs/getting-started/` does not exist; this episode does not create
that directory either (out of scope), but `docs/onboarding.md` is the
practical equivalent.

### Act III -- Write the doc

Drafted `docs/onboarding.md` with five sections per the brief:

- **Lloyd intro paragraph** in his voice.
- **The first 60 minutes** -- ten numbered steps with actual commands
  and expected outputs, cross-linked to `docs/incident-runbooks.md`
  for failure modes.
- **Things I had to ask** -- twelve question / answer / pointer
  entries, the friction log made navigable.
- **Where to find things** -- file-system map of the repo so the next
  newcomer does not bounce.
- **Your first PR** -- eight starter ideas rated S/M/L, each pointing
  at the file or test that would change. Includes the existing
  `Lloyd flags:` callouts in `docs/user-stories.md`, the `:F0` culture
  bug from S02E08, and a coordination flag on the runbooks file.
- **Glossary cross-link** -- one-liner pointer.

### Act IV -- Append to the glossary

`docs/glossary.md` existed (Babu seeded it in S02E08; Lloyd is the
listed maintainer). Appended six entries in alphabetical order, each
following the existing one-H3-per-term shape:

- **Conventional Commits** -- the commit-message format
- **LOLBin** -- Living Off the Land Binary, the per-OS keystore pattern
- **Preflight** -- the local validation gate
- **SBOM** -- Software Bill of Materials
- **SDK / runtime (.NET)** -- the distinction Lloyd actually tripped on
- **Trivy** -- the container vulnerability scanner

ASCII-only, no smart quotes, fenced code blocks tagged.

### Act V -- CHANGELOG + ship

One bullet under `[Unreleased] > Added` referencing `docs/onboarding.md`
and the glossary additions. Two commits: docs payload, then this
exec report.

## What shipped

**Production code** -- none (intentional).

**Tests** -- none (intentional; docs-only).

**Docs**

- `docs/onboarding.md` (new) -- the first-hour walkthrough, friction
  log, file-system map, and starter-PR list.
- `docs/glossary.md` (appended) -- six entries: Conventional Commits,
  LOLBin, Preflight, SBOM, SDK / runtime, Trivy.
- `CHANGELOG.md` -- one bullet under `[Unreleased] > Added`.

**Not shipped** (intentional follow-ups, named so future episodes can
own them)

- `README.md` -- the quickstart still does not mention preflight or
  the SDK / runtime distinction. Orchestrator-owned; named in the
  friction log.
- `CONTRIBUTING.md` -- S02E17's territory. The "30-second orientation"
  could surface the env-var-name gotcha sooner; flagged but not
  edited.
- `make help` -- curated output omits `bench-quick`, `scan`, `audit`,
  `preflight`. Jerry's beat to fix in a later DevOps episode.
- `docs/getting-started/` -- the brief mentioned this hypothetical
  directory; did not create it. `docs/onboarding.md` is the equivalent.
- `docs/incident-runbooks.md` -- cross-linked from the walkthrough but
  not edited (Frank Costanza's territory).
- `docs/user-stories.md` -- referenced via the `Lloyd flags:` starter
  PR but not edited.
- `azureopenai-cli/Setup/FirstRunWizard.cs` -- not redesigned, per
  scope. Brief described, not changed.
- All other orchestrator-owned files (`AGENTS.md`,
  `.github/copilot-instructions.md`, `.github/agents/*`,
  `docs/exec-reports/README.md`, `docs/exec-reports/s02-writers-room.md`,
  `docs/telemetry.md`, `docs/i18n-audit.md`, `docs/security/*`,
  `docs/legal/*`, `THIRD_PARTY_NOTICES.md`,
  `docs/competitive-landscape.md`).

## Lessons from this episode

The friction log is the deliverable. The five most painful items, in
the order Lloyd hit them:

1. **Env-var name confusion (`AZUREOPENAIAPI` vs `AZUREOPENAIKEY`).**
   Cost ten minutes. The README and `docs/prerequisites.md` both call
   it out, but "API" reads as a noun, not a credential. Worth a
   one-line warning at the top of the README quickstart, not just in
   the env-var table.
2. **Two source trees with no signal in the file listing.** `azureopenai-cli/`
   (v1, maintenance) and `azureopenai-cli-v2/` (v2, default) sit
   side-by-side. CONTRIBUTING explains it; the README quickstart and
   `ls` do not. A `README` file in each directory pointing at the
   other would close this.
3. **`make help` is a curated subset, not a list.** Useful targets
   (`preflight`, `bench-quick`, `scan`, `audit`) are absent.
   First-timers do not know to read the `Makefile` directly.
4. **Preflight is not in the README quickstart.** It is in
   CONTRIBUTING and in the skill file, but a contributor who clones,
   edits, and tries to commit will not encounter it until CI tells
   them. "Serenity now -- insanity later."
5. **SDK vs runtime distinction is invisible.** README says ".NET 10";
   does not say "SDK, not runtime." A contributor with only the
   runtime installed will see confusing `dotnet build` errors.
   Glossary entry added; the README still omits the distinction.

These five are the highest-ROI follow-ups. None of them were fixed
this episode (scope discipline); each is named here so the next
applicable episode (README refresh, S02E17 follow-up, DevOps pass) can
pick them up.

## Metrics

- Diff size: 3 files modified, 1 file created. Roughly +330 lines
  (`docs/onboarding.md` ~290, glossary ~45, CHANGELOG ~7).
- Friction log items: 12 questions answered + 5 highest-ROI items
  surfaced as lessons.
- Starter-PR ideas: 8, rated S/M/L.
- Glossary entries added: 6 (Conventional Commits, LOLBin, Preflight,
  SBOM, SDK / runtime, Trivy).
- Preflight: not run (docs-only; allowed per
  `.github/skills/preflight.md`).
- CI status at push: pending.

## Credits

- **Lloyd Braun** -- lead. Wrote the doc in his own voice, owned the
  friction log, kept asking "wait, where would I have looked for that?"
  until every gap had a question / answer / pointer triple.
- **Elaine Benes** -- prose passes. Tightened the walkthrough,
  enforced ASCII-only, killed every smart quote and en-dash before
  they could ship.
- **Jerry Seinfeld** -- DevOps explanations for the `make` targets,
  the SDK / runtime split, and the preflight gate.
- **Cosmo Kramer** -- on call for code-path questions; mostly let
  Lloyd and Elaine drive so they could slow him down.
- **Larry David** -- showrunner. Cast Lloyd as lead and held the line
  on scope (no production code, no rewrite of orchestrator-owned
  surfaces).

Glossary co-ownership note: `docs/glossary.md` was seeded by **Babu
Bhatt** in S02E08 and is maintained by Lloyd. This episode appended;
no existing entry was rewritten.

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>
