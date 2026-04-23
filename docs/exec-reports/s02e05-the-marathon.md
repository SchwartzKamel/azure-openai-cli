# S02E05 -- *The Marathon*

> *Bania can't hand contributors a 5-minute bench gate and expect them
> to run it. Add a 5-second smoke and a CI canary that prints numbers
> on every push -- with a disclaimer the size of a Snickers wrapper.*

**Commit:** `42b14bb` (+ exec-report follow-up)
**Branch:** `main` (direct push, solo-led per `commit.md`)
**Runtime:** ~25 minutes
**Director:** Copilot (fleet orchestrator)
**Cast:** **Kenny Bania** (featured), **Jerry** (guest, CI wiring)

## The pitch

`make bench-full` is the canonical N=500 `--flag-matrix` sweep. It's
also a 5-10 minute job that nobody runs before commit. Contributors
either skip the bench step entirely or fall back to `make bench`
(N=100, ~30 s) -- still too slow for the dev loop, still single-rig,
still untracked in CI. Regressions slip through; Bania cries into his
Snickers.

This episode adds the missing rung at the bottom of the ladder
(`make bench-quick`, ~5-10 s) and a CI canary that posts the numbers
to the GitHub Actions step summary on every push / PR. Explicitly
**not** a regression gate -- shared-VM jitter makes that dishonest --
just a directional smoke that nudges contributors to look at numbers
without forcing them to wait for them.

Gold, Jerry. Gold.

## Scene-by-scene

### Act I -- Read the landscape

- `Makefile` already had `bench` (N=100, warm=5) and `bench-full`
  (N=500, warm=5, `--flag-matrix`, JSON to `docs/perf/runs/`). Both
  call `scripts/bench.py`.
- `scripts/bench.py` already supports `--n`, `--warmup`, `--flag-matrix`,
  `--json`. **Zero script changes needed** -- `bench-quick` is just a
  flag combination that wasn't named yet.
- `docs/perf/reference-hardware.md` is the rig pinning policy. Any
  CI-runner number must say "not bench-grade" loudly or it'll be cited
  as a baseline within a week.

### Act II -- Three small edits

- `Makefile`: new `bench-quick` target (`--n 50 --warmup 0`, stdout
  text only). Added a one-paragraph "when to use" comment above each
  of `bench-quick`, `bench`, `bench-full`. Added `bench-quick` to
  `.PHONY` and to the help text.
- `.github/workflows/ci.yml`: new `bench-canary` job, sibling to
  `integration-test` and `docker`, `needs: build-and-test`,
  `ubuntu-latest`. Steps: checkout → setup-dotnet → cache nuget →
  `make publish-aot` → `make bench-quick | tee bench-canary.txt`
  (with `continue-on-error: true`) → write the captured table to
  `$GITHUB_STEP_SUMMARY` under `## bench-canary (directional only)`
  preceded by a blockquote disclaimer pointing at the pinned-rig doc.
- `docs/perf/bench-workflow.md` (new): one-page table -- N, warm-up,
  flag-matrix, wall-clock, bench-grade -- plus three "when to run"
  sections and an explicit "CI's bench-canary is not authoritative"
  callout. CHANGELOG and README Performance section both point at it.

### Act III -- Ship

`make preflight` green: format, build 0/0, 150/150 unit (3 skipped),
all integration. Local `make bench-quick` ran clean on the WSL2
laptop in ~6 s (median 5.263 ms, p99 23.487 ms across N=50 -- noise
exactly as advertised). One commit, direct push to `main`.

## What shipped

**Production code** -- none. Tooling-only episode.

**Tooling**

- `Makefile` -- `bench-quick` target + scoped comments on `bench` /
  `bench-full`.
- `.github/workflows/ci.yml` -- 67-line `bench-canary` job appended
  after `docker`.

**Docs**

- `docs/perf/bench-workflow.md` -- new one-pager.
- `CHANGELOG.md` -- `[Unreleased] > Added` bullet (bench-quick + canary).
- `README.md` -- one-sentence pointer in the Performance section.

## Scope discipline (what this episode did NOT do)

Resisting the urge to do "one more thing" is half the production
budget. Explicitly **out of scope** for S02E05:

- ❌ **No perf optimizations.** Cold-start was not touched. This is a
  tooling polish episode.
- ❌ **No new baselines.** Nothing was written to `docs/perf/runs/`,
  `v2.0.5-baseline.md`, or `v2-cold-start-p99-investigation.md`.
- ❌ **No pinned-rig re-run.** `malachor` did not boot for this
  episode. Reference numbers in the doc come from existing artifacts.
- ❌ **No regression threshold / gate.** The canary is non-failing by
  construction (`continue-on-error: true`). A future episode can
  argue for thresholds *if and only if* the rig moves into CI.
- ❌ **No bench-trend artifact / upload.** The summary is the artifact.
- ❌ **No `scripts/bench.py` edits.** The existing flag surface
  covered every requirement.

## Lessons from this episode

1. **Three rungs is the right number.** A single `bench` target was
   asking contributors to choose between "too slow" and "nothing."
   Adding `bench-quick` doesn't replace `bench-full`; it makes
   `bench-full` more likely to actually run when it matters, because
   the dev-loop muscle is built on the cheap target.
2. **Noisy CI numbers are fine if you label them honestly.** A bench
   table with a 12-line disclaimer above it is more useful than no
   table at all. The lie is unlabeled directional data, not the data
   itself.
3. **Concurrent episodes need staging discipline.** S02E06 (Mickey,
   screen reader work) was running in parallel and had un-staged
   edits to `FirstRunWizard.cs` and `accessibility.md`. Caught at
   `git status` before commit; only the five bench files went into
   `42b14bb`. Worth noting in the orchestrator's playbook.

## Metrics

- **Diff size:** 194 insertions, 5 deletions across 5 files (1 commit).
- **Files touched:** `Makefile`, `.github/workflows/ci.yml`,
  `CHANGELOG.md`, `README.md`, `docs/perf/bench-workflow.md` (new).
- **Local `make bench-quick`:** N=50, warm=0, `--help` only.
  min 3.918 / p50 5.263 / mean 6.080 / p90 8.800 / p95 8.980 / p99 23.487
  / max 23.487 / sigma 2.945 (ms). Wall-clock ~6 s. Reference rig:
  Intel Ultra 7 265H, WSL2 (i.e. *not* the canonical `malachor` rig --
  these numbers are illustrative, not bench-grade).
- **Preflight:** green -- format-check, color-contract, dotnet-build,
  150/150 tests (3 skipped, no creds), full integration suite.
- **CI canary first run:** see commit `42b14bb` GHA run for the
  step-summary table; numbers will jitter between runs by construction.

## Credits

- **Kenny Bania** (featured) -- harness call, rung naming, the
  honesty doctrine that turned the canary disclaimer from a footnote
  into the lead paragraph.
- **Jerry** (guest) -- CI job wiring, `continue-on-error` placement,
  step-summary heredoc.
- **Elaine** -- bench-workflow.md prose, CHANGELOG bullet shape.
- **The Soup Nazi** -- silently approved the markdownlint discipline
  on the new doc (blank line above every numbered list, fence
  languages on every code block).
- **Co-author trailer:**
  `Copilot <223556219+Copilot@users.noreply.github.com>`

*-- end of episode --*
