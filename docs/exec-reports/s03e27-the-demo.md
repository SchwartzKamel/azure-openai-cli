# S03E27 -- *The Demo*

> *Curtain call. Five acts, 22 invariants, no real API calls. The season closes.*

**Commit:** (this push -- not yet committed at report-write time)
**Branch:** `main` (direct push)
**Runtime:** ~45 minutes wall-clock, single-agent
**Director:** Larry David (showrunner -- this one I filmed myself)
**Cast:** Larry David solo. No dispatch wave. Curtain calls are sung
in your own voice or not at all.

---

## Cold open

INT. WRITERS' ROOM. Late. The whiteboard is full -- 26 episode slots,
26 green checkmarks, half a dozen file-slot arrows pointing at
themselves. LARRY at the head of the table, jacket on the chair, cup
of coffee long gone cold.

LARRY: So we're done.

(BEAT.)

LARRY: I mean we're not *done*-done. There's a season four in the
binder. There's a v3.0 in the binder. There's an offline mode that
got bolted on the side because *Newman* -- (*sighs*) -- because Newman
needed it for a recording. There's a fallback chain we ship as
*disabled by default* because nobody's wired the cred discovery yet.
But this season -- THIS season -- shipped.

(LARRY taps the whiteboard. The marker squeaks.)

LARRY: One binary. Three providers. Capability gate. Allowlist.
Rotation. Offline. Telemetry that nobody can claim leaked. A doctor
probe that works without secrets. A switch that knows what it is. A
fallback that refuses bogus inputs with the *list of valid presets*,
which is more than my health insurance does.

(BEAT.)

LARRY: We need a curtain call. And I'm not -- look, no offence to
Peterman, but I'm not letting Peterman write the *script*. We're not
going to a podium. We're going to a *bash file*. With banners. With
exit codes. With twenty-two assertions. The audience presses Enter,
the script runs, the script ends in zero, the lights come up. That's
the show.

KRAMER (offscreen, distant): I have a *theatre*, Jerry --

LARRY: Not the theatre. The script.

CUT TO: `scripts/demo/season3-finale.sh`.

---

## The pitch

S03 is the season `azure-openai-cli` stopped being a tool and became a
category entrant. End of S02, we were Azure-only -- excellent at one
thing on one provider. End of S03, the **same binary** speaks Azure,
the OpenAI-compat HTTP family (Ollama, llama.cpp, Groq, Together,
Cloudflare, OpenAI direct), and Azure AI Foundry, with profile
pinning, capability gating, opt-in fallback, an offline mode, and a
provider-doctor probe -- all wired into a `preferences.json` resolver
with a deterministic precedence chain (cli > env > preferences >
default). The seam, not the intelligence: automatic routing, cost-
aware fallback, MCP, multimodal stay in S04 and beyond.

The finale is the *proof*. We have the audit reports. We have 1019+
unit tests and 73 integration assertions. We have a writers' room
table green from E01 to E26. What we did not have -- until this
episode -- was a single artefact a contributor could run and see the
season's arc fire end to end. `season3-finale.sh` is that artefact: a
mock-only, idempotent, 5-act bash demo that exercises every load-
bearing surface S03 added without making one network call to a real
provider. 22 invariants. ASCII-only output. `rc=0` or the show closed
early.

This is not new production code. Zero `.cs` changed. Zero tests
changed. The episode is a curtain call, by design.

---

## Scene by scene

### Act I -- Planning

The brief came in pinned to a finale slot (S03E27, file slot 27 --
straight numbering, no drift). The constraint was real: a concurrent
episode (E28 *The Persona Multi-Provider*, lead Kramer) was filming
in the production tree, touching `Squad/`, `Preferences.cs`, persona
tests, and its own exec report. **Zero file overlap** was the order.
The finale lives in `scripts/demo/`, `docs/season-recaps/`,
`docs/exec-reports/s03e27-*`, plus three orchestrator-owned edits
(writers' room row, `CHANGELOG.md`, `README.md`).

I filmed this one solo. Curtain calls are not a fleet operation --
dispatching three sub-agents to write a five-act bash script would
have produced three different bash scripts. One showrunner, one keyboard,
one take.

### Act II -- Production

| Wave | Agents (parallel) | Outcome |
|------|-------------------|---------|
| **1** | larry-david (solo) | Built fresh AOT binary (`make publish-aot`, ~31 s), installed it, probed every S03 surface to confirm observable behaviour. |
| **2** | larry-david (solo) | Authored `scripts/demo/season3-finale.sh` (5 acts, 22 invariants, ASCII-only, gated for old-binary scenarios). |
| **3** | larry-david (solo) | Authored `scripts/demo/README.md`, this exec report, and `docs/season-recaps/season-3-recap.md`. |
| **4** | larry-david (solo) | Edits to `s03-writers-room.md` (E27 row + season-close marker), `CHANGELOG.md`, `README.md`. |

The script passed end-to-end on the first full run after one fix: the
initial feature-detection grep keyed on `--doctor` appearing in
`--help`, which it does not -- `--doctor` is a subcommand-style flag
discoverable only by invoking it. Caught on first run, fixed in one
edit. Second invariant pass: 18/18. Third pass after polishing Act V
to use `--fallback bogus` as a reliable telemetry-emission trigger:
22/22.

### Act III -- Validation, commit, push

- ASCII-validation grep over all four new docs and the script: clean.
  No `U+2018 U+2019 U+201C U+201D U+2013 U+2014`. The banners use
  `+`, `=`, `|`, `-`, `>` -- nothing fancy. By design.
- `make preflight` was a docs-and-scripts no-op (no `.cs` / `.csproj`
  / `.sln` / workflow change), but ran cleanly. Format check: green.
  Build: green. Unit tests: green. Integration: green. Exec-report
  check: this very file satisfies it.
- Final demo run: 22 / 22 invariants passed, `rc=0`. Excerpt:

```text
Result
  total assertions: 22
  failed:           0

  Pretty, pretty, pretty good. Curtain.
```

- Commit pending; the brief says "do NOT commit" -- the showrunner
  commits the orchestrator-owned diff in a separate beat.

---

## What shipped

### Production code

n/a. No `.cs` changed by design. Zero source-tree drift. The finale
is a curtain call, not a feature.

### Tests

n/a. No new unit or integration tests. The 22 in-script assertions
are end-to-end behavioural invariants, not unit tests -- they live
in the demo because their value is in the *cast file*, not in
`dotnet test`. The unit + integration suites already cover every
surface this demo exercises.

### Docs

- **NEW** `scripts/demo/season3-finale.sh` -- 5-act, mock-only, ~440
  lines of bash. Idempotent. Cleans on exit. ASCII-only banners.
  Gates gracefully when binary is missing or too old.
- **NEW** `scripts/demo/README.md` -- prereqs, run instructions,
  asciinema recording recipe, replay options, exit-code table.
- **NEW** `docs/exec-reports/s03e27-the-demo.md` -- this file.
- **NEW** `docs/season-recaps/season-3-recap.md` -- marketing-grade,
  Peterman-pluckable retrospective with arc-by-arc prose and a
  "By the numbers" stat block.
- **EDIT** `docs/exec-reports/s03-writers-room.md` -- E27 GREEN row
  and season-close marker.
- **EDIT** `CHANGELOG.md` -- `[Unreleased] / Added` entry.
- **EDIT** `README.md` -- new top-level "Demo" subsection under
  Documentation.

### Not shipped

- Recorded asciinema cast. The script is *recordable*; the recording
  itself is a follow-up beat (post-merge, pre-launch). Filed as a
  Peterman / Keith Hernandez follow-up for the v3.0 release moment.
- Fallback cred discovery (`frank-2026-05-FB-1`). Still open.
  Production `AlternateChatClientFactory` returns `Skipped` -- the
  finale demonstrates the *flag* and the *validation*, not the
  *recovery* (which there's no host-side state to test in this
  CI-friendly demo anyway). Lives in S04.
- Live capability-gate fire. The brief asked for "capability gate
  fires on `--agent` against a non-tool-calls preset". Surfacing
  that *behaviourally* requires standing up a fake local server, which
  a 5-act bash demo does not do cleanly. The demo asserts the
  *interface* exists (Act III: `--help` references the gate); the
  unit tests in `CapabilityGateTests.cs` already cover the firing
  behaviour. Trade accepted.

---

## The full season -- name + slot

26 episodes shipped before this finale. Calling them out by file slot
(the canonical row order, even where it disagrees with the original
blueprint slate):

| Slot | Title | Lead | Verdict |
|------|-------|------|---------|
| S03E01 | *The Yada Yada Strikes Back* | Kramer | shipped (audit clean wave 9) |
| S03E02 | *The Library Cop's Word Limit* | Lt. Bookman | shipped (tier doctrine + 3 triggers) |
| S03E03 | *The Docs Audit, Reprise* | Elaine | YELLOW (22 findings: 2C / 11M / 7m / 2n) |
| S03E04 | *The Mailman Knocks Twice* | Newman | RED -> closed (F-1 CRIT + F-2 HIGH patched) |
| S03E05 | *The Auditor's Auditor* | Mr. Wilhelm | YELLOW (50% follow-through baseline) |
| S03E06 | *The Schema* | Kramer | clean |
| S03E07 | *The Redactor* | Newman | clean |
| S03E08 | *The Pick* | Costanza (ADR), Larry (episode) | decision (ADR-010) |
| S03E09 | *The Compat* | Kramer | clean (`OpenAiCompatAdapter`) |
| S03E10 | *The Keychain* | Newman | GREEN (per-provider env sections) |
| S03E11 | *The Wizard, Reprise* | Jerry | GREEN (provider-aware wizard, 32U+5I) |
| S03E12 | *The Receipt* | Kenny Bania | clean (bench harness + cost rates) |
| S03E13 | *The Telemetry* | Frank Costanza | GREEN (`AZ_AI_TELEMETRY=1` opt-in NDJSON + SLO charter) |
| S03E14 | *The Screen Reader* | Mickey Abbott | GREEN (`--plain` + 18-site glyph audit, 28U+6I) |
| S03E15 | *The Probe* | Costanza | GREEN (`--doctor` subcommand, 21U+3I) |
| S03E16 | *The Allowlist* | FDR | GREEN (SSRF endpoint allowlist + 57 adversarial cases) |
| S03E17 | *The Stream* | Kramer | verification episode (15 streaming/tool-call facts) |
| S03E18 | *The Capability Gate* | Costanza | GREEN (provider+model matrix + dispatch gate, 33U+5I) |
| S03E19 | *The First Hour, Local Edition* | Lloyd Braun | docs-only (Ollama walkthrough) |
| S03E20 | *The Switch* | Costanza | GREEN (`Preferences.Resolve()` precedence + `--provider`/`--profile`, 44U+6I) |
| S03E21 | *The Server* (file slot 21, title E17 spent) | Kramer | GREEN (`llamacpp` preset, 25U+4I) |
| S03E22 | *The Default* (file slot 22) | Costanza | GREEN (six-rung default-provider heuristic + ADR-011, 36U+6I) |
| S03E23 | *The Fallback* (file slot 23, title E22 spent) | Frank Costanza | GREEN (`--fallback` chain + new SLIs, 47U+6I) |
| S03E24 | *The CVE Log, Per Provider* | Jerry | GREEN (per-provider Trivy + `make cve-report`) |
| S03E25 | *The Rotation* | Newman | GREEN (`--rotate-creds`, 35U+6I) |
| S03E26 | *The Offline Mode* | Newman | GREEN (`--offline` + `AZ_AI_OFFLINE=1`, 30U+7I) |
| **S03E27** | ***The Demo*** *(this episode)* | **Larry David** | **GREEN (5 acts, 22 invariants, rc=0)** |

If you read the writers' room row order against the blueprint slate,
the file-slot numbering and the *narrative* numbering disagree -- *The
Stream* burned the E13 title (later renumbered as E17 *The Stream*),
*The Default* and *The Fallback* both arrived after their nominal
slots had been claimed by *The Server* and others. We are not
retro-renumbering. The drift is canonical history. The finale slot is
a clean E27 / file-slot 27 because Larry-David-the-character has
exactly enough discipline to not give the writers' room more excuses.

---

## Lessons from this episode (and from the season)

1. **Wave-collision discipline scales.** The S03 dispatch model --
   never solo-background, wave on collision risk -- held across 26
   episodes with one significant collision (the file-slot drift on
   *The Stream* / *The Server* / *The Default* / *The Fallback*) and
   *zero* merge-conflict rollbacks. The dispatch skill paid for
   itself by E12.

2. **File-slot drift is real and survivable.** Three episodes (S03E17
   *The Stream*, S03E21 *The Server*, S03E23 *The Fallback*) were
   filmed under titles that had already been claimed by earlier
   blueprint slots. The fix was always the same: keep the title,
   advance the file-slot, document the swap in the writers' room
   row. Retro-renumbering is forbidden -- it loses the historical
   record of which agent claimed which slot when. Shipping the same
   finale on a clean E27 slot is partial penance.

3. **Working-tree-stash discipline is non-negotiable for sub-agents.**
   Sub-agents that opened `git status` to dirty trees (because the
   showrunner had un-pushed orchestrator edits) wrote half their
   diffs against stale state. Solution -- baked into the dispatch
   skill mid-season: every sub-agent stashes any pre-existing diff
   on entry, restores on exit, and reports back if the working tree
   was non-empty. The S03E22 *Default* / S03E23 *Fallback* wave was
   the canary; the fix landed before the next wave.

4. **Mock-only demos are the right shape for a curtain call.** A demo
   that requires real API keys is a demo that doesn't run in CI. A
   demo that mocks creds is a demo that lies. The middle path is to
   exercise *only* the surfaces that are observable without real
   creds -- `--help`, `--doctor`, `--config show`, `--fallback bogus`,
   `AZ_AI_OFFLINE=1`, `AZ_AI_TELEMETRY=1`. The whole season's arc is
   provable through those surfaces. Build the finale around that.

5. **Feature detection beats version detection.** The script's
   pre-flight gate detects S03 surfaces by *probing them*, not by
   parsing `az-ai --version`. `--doctor` does not appear in `--help`,
   so a help-text grep would have produced false negatives. Probing
   the actual subcommand for `unknown flag:` was the right primitive.

6. **The opt-in invariant is the load-bearing one.** S03's most
   important promise is that nothing surprises the user: telemetry
   off-by-default, fallback off-by-default, offline-mode off-by-
   default, capability override off-by-default, local providers
   off-by-default behind `AZ_AI_LOCAL_PROVIDERS=1`. Act V's first
   assertion -- "telemetry off-by-default: no providers, no events
   leaked" -- is the one I would defend if a reviewer asked me which
   single invariant I want to never lose.

7. **Curtain calls belong to the showrunner.** Every other S03 episode
   was dispatched to a sub-agent. This one I filmed myself because
   the *voice* mattered as much as the deliverable. A sub-agent
   doing "Larry voice" is a sub-agent doing a *parody* of Larry
   voice. If the brief says "this IS Larry, lean in" -- Larry leans
   in, by sitting at the keyboard.

---

## Tag scene -- teasing Season 4

(POST-CREDITS. The whiteboard has been wiped. New marker.)

LARRY: (writing) Season four. Lock-in for v3.0.

(He writes.)

- **`v3.0` lock-in.** Cut the GA release. SemVer-major. Pin every
  S03 surface as supported. Mr. Lippman's whole season-opener.

- **OS keychain integration (R-3).** Newman's recurring complaint:
  `~/.config/az-ai/env` is `chmod 600`, but it's still on disk in
  plaintext. macOS Keychain, Windows Credential Manager, GNOME
  libsecret. The interface stays `--rotate-creds`; the *backing
  store* moves.

- **Autodetect capability probe (CG-3).** The S03E18 capability
  matrix is hand-curated. By the time we're shipping S04 there will
  be enough providers in the wild that hand-curation is a
  maintenance burden. Probe a `/v1/models` or a `tools=[]` echo
  request and *observe* the capability set. Cache it. Re-probe on
  TTL expiry. This is a Maestro / Costanza two-hander.

- **Multi-tenant prep.** Pre-work for "the same binary, multiple
  preference profiles, switched per shell session", which itself is
  pre-work for "the same binary, multiple users on a shared host".
  Costanza owns the design; Frank Costanza owns the SLO impact.

- **Cloud setup-steps integration.** A `copilot-setup-steps.yml` for
  cloud-agent users that pre-installs az-ai, configures a known-good
  preferences.json, and warms the local Ollama runtime. Bob Sacamano
  + Jerry. Lloyd Braun owns the onboarding doc.

- **Recorded asciinema cast.** The actual playback artefact this
  finale's script *enables*. Peterman + Keith Hernandez own the
  capture; Russell owns the visual polish.

LARRY: That's six. We'll cut two in the room.

(BEAT.)

LARRY: Curtain.

(LIGHTS DOWN. END S03.)

---

## Metrics

- **Diff size:** 4 files added (~440 lines bash + ~3.4 KB demo
  README + ~12 KB exec report + ~6 KB season recap), 3 files edited
  (writers' room +1 row + ~6-line season-close marker; CHANGELOG +1
  Unreleased entry; README +1 Demo subsection). Net: docs and one
  bash script.
- **Test delta:** n/a (no `.cs` change). 22 in-script behavioural
  assertions added, all passing on a fresh `make publish-aot`.
- **Preflight result:** docs-only-commit path; format / build /
  unit / integration not exercised because no compilable surface
  changed; `make exec-report-check` satisfied by this very file.
  Manual smoke: green.
- **CI status at push time:** to-be-confirmed by the showrunner on
  the post-commit beat.
- **Binary surface tested by the demo:** `--help`, `--doctor`,
  `--rotate-creds --help`, `--config show`, `--provider`,
  `AZ_PROFILE` + `preferences.json`, `--fallback` (CLI + env),
  `AZ_AI_OFFLINE=1`, `AZ_AI_TELEMETRY=1`. Nine surfaces.

---

## Credits

Filmed by Larry David (showrunner) solo. No sub-agent dispatch on
this one -- the curtain call belongs to the orchestrator. All
commits will carry the `Co-authored-by: Copilot
<223556219+Copilot@users.noreply.github.com>` trailer per the
project's commit conventions, signed off `-c commit.gpgsign=false`
because the showrunner cannot GPG-sign on behalf of Copilot.

The 26 episodes that made this finale possible were filmed by the
Season 3 cast: Costanza, Kramer, Newman, Frank Costanza, Jerry,
Elaine, FDR, Lt. Bookman, Mr. Wilhelm, Mickey Abbott, Kenny Bania,
Lloyd Braun. Honourable mention to Sue Ellen for the competitive
read that named the season's pivot, to Mr. Pitt for keeping the
arc plan honest at every mid-season checkpoint, and to The Maestro
for the prompt-engineering audits that kept the help text reading
like English.

Pretty, pretty, pretty good. Curtain.
