# Season 2 -- *Writers' Room*

> *The arc plan for the rest of S02. Seven more episodes, every one
> with a clear featured cast member, most with a second-banana pairing.
> No episode repeats the same featured lead as the one before it --
> we spread the love.*

**Season theme:** Production & Polish (v2 era).

## Aired so far

| # | Title | Featured cast | Status |
|---|-------|---------------|--------|
| S02E01 | *The Wizard* | Kramer (lead), Newman (guest) | aired |
| S02E02 | *The Cleanup* | Kramer, Puddy | aired |
| S02E03 | *The Warn-Only Lie* | Elaine (lead), Soup Nazi (guest) | aired |
| S02E04 | *The Locksmith* | Kramer (lead), Newman (guest) | filming |

**Cast coverage audit after E04.** Kramer has carried 3 episodes in a
row in lead or co-lead. Time to bench him for a few and spotlight the
rest of the supporting cast.

## Rest of the season -- proposed

Each episode below is scoped to be *small-to-medium* -- one or two
wave's worth of work, a single-subagent job for most -- so the season
can finish cleanly and the pilot arc (S03) can pick up with energy.

### S02E05 -- *The Marathon*

**Featured:** Kenny Bania (lead), Jerry (guest).

**Pitch.** `make bench-full` takes a while. Add `make bench-quick` for
the pre-commit flow (N=50, no flag-matrix), document when to use each,
and wire a tiny canary benchmark into CI that posts a one-line summary
to every PR. No new baselines, no perf *optimisation* -- just make the
perf tooling ergonomic enough that contributors actually run it.

**Scope notes.** `scripts/bench.py` already supports `--n` and
`--json`. Just add a thin Makefile target and a GH Actions step that
runs `bench-quick` on push and PRs, parses the JSON, and posts to the
step summary (not a PR comment -- avoids noise). Bania signs off on
the numbers; Jerry owns the workflow wiring.

### S02E06 -- *The Screen Reader*

**Featured:** Mickey Abbott (lead), Russell Dalrymple (guest).

**Pitch.** Accessibility + CLI ergonomics audit. Verify `NO_COLOR`
end-to-end. Verify the first-run wizard works with a screen reader
(masked input is the sharp edge). Confirm keyboard-only abort
(Ctrl+C and Ctrl+D) leaves no partial config behind. Document the
a11y contract in `docs/accessibility.md` so future regressions are
obvious.

**Scope notes.** Mickey audits; Russell co-reviews the presentation
side (colour-safe palettes, consistent spacing in error output). Net
changes likely small -- a few formatting tweaks, one new doc.
If `Console.IsInputRedirected` / `Console.IsOutputRedirected` are
already respected, great; if not, that's the diff.

### S02E07 -- *The Observability*

**Featured:** Frank Costanza (lead), Newman (guest).

**Pitch.** Telemetry-off honesty pass. Confirm the opt-in telemetry
(if enabled) leaks zero PII -- including prompt text, endpoint URL,
and key fingerprint. Add a startup line when telemetry is disabled
(silent by default but visible in `--verbose` / `--config show`).
Write incident runbooks for the three most-likely user-facing
failures: 401 auth, 429 rate-limit, DNS / TLS.

**Scope notes.** If there's no telemetry surface yet, this episode
pivots to "document *why* there isn't one" -- a brief
`docs/telemetry.md` stating the posture. Either way it ships a
single markdown file and possibly one `--config show` tweak.

### S02E08 -- *The Translation*

**Featured:** Babu Bhatt (lead), Elaine (guest).

**Pitch.** i18n readiness inventory. Catalog all user-facing strings
(wizard prompts, error messages, help text) and classify each as
(a) locale-agnostic today, (b) translation-ready (stable key, no
concatenation), or (c) needs work. No actual translations yet --
this is the audit episode that makes S03-or-later translation work
trivial. Also: Unicode correctness check on the masked-input path.
An API key with a trailing non-ASCII byte should not silently
corrupt.

**Scope notes.** Tight. One audit doc + maybe one defensive fix if
an edge case surfaces.

### S02E09 -- *The Receipt*

**Featured:** Morty Seinfeld (lead), Kramer (guest).

**Pitch.** Cost-watch polish. Verify token accounting in `--verbose`
output is honest (input + output + tool-call overhead, no
undercounting). Document the default per-call ceiling and how to
raise it. Prepare the seam for S03 Theme C's prompt/response cache
without implementing the cache itself.

**Scope notes.** Morty is opinionated about costs; pair with Kramer
to implement any diff. Likely a few small `Program.cs` print-path
adjustments and one doc update.

### S02E10 -- *The Press Kit*

**Featured:** Mr. Lippman (lead), J. Peterman (guest).

**Pitch.** Release readiness for the v2.x polish cut. Tidy the
`CHANGELOG.md` Unreleased block, draft release notes for the next
tag, update the README hero copy if the feature mix since v2.0
warrants it. Does NOT cut a release -- just makes one ship-ready.

**Scope notes.** Lippman owns the CHANGELOG edit; Peterman writes
any new prose that actually makes it into the README. Elaine
consults on structure.

### S02E11 -- *The Finale*

**Featured:** Mr. Pitt (lead), whole cast (ensemble).

**Pitch.** Season-wrap exec report. Aggregate S02 metrics across
all episodes (commits, lines, tests added, CI incidents and their
MTTR), call out the biggest lessons, and formally close S02 so the
showrunner can greenlight S03.

**Scope notes.** Ensemble episode. No code changes. Just a well-
written retrospective that sets the stage for the S03 blueprint
(which already exists and can be promoted once a theme is picked).

## Off-roster, season-independent

Items that could slot into any episode as a B-plot or stand alone as
an unaired special:

- Mac Keychain test-body rewrite (needs a Mac owner -- held open).
- Linux `systemd-creds` provider (seam exists; not this season).
- The `filename-convention` docs-lint step hard-flip when convenient
  (currently warn-only by design, no urgency).

## Cast distribution target for S02 (aired + planned)

| Cast member | Leads | Guests |
|-------------|-------|--------|
| Kramer | E01, E02, E04 | E09 |
| Newman | -- | E01, E04, E07 |
| Elaine | E03 | E08, E10 |
| Puddy | -- | E02 |
| Soup Nazi | -- | E03 |
| Kenny Bania | E05 | -- |
| Jerry | -- | E05 |
| Mickey Abbott | E06 | -- |
| Russell Dalrymple | -- | E06 |
| Frank Costanza | E07 | -- |
| Babu Bhatt | E08 | -- |
| Morty Seinfeld | E09 | -- |
| Mr. Lippman | E10 | -- |
| J. Peterman | -- | E10 |
| Mr. Pitt | E11 | -- |

Twelve distinct cast members appear across the season. Kramer is
benched for E05-E08 and returns only as a guest in E09, which is the
right corrective after his E01-E04 streak. Every supporting-player
lead gets exactly one episode.

## Dispatch order

Episodes are mostly independent and can film in parallel, but some
natural grouping:

- **Wave A (ship ergonomics):** E05 (Marathon), E06 (Screen Reader)
- **Wave B (honesty / docs):** E07 (Observability), E08 (Translation)
- **Wave C (polish):** E09 (Receipt)
- **Wave D (ship prep):** E10 (Press Kit), then E11 (Finale)

E11 must be last -- it retrospects everything above it. Otherwise the
showrunner can pick up the season in any order.

*-- Mr. Pitt (program management), with notes from Costanza (product),
Elaine (structure), and a cast-rotation assist from Russell Dalrymple.*
