# S04E28 -- *The Estimate*

> *The price table was right. Nothing ever asked it.*

**Commit:** `(this commit)` -- one code commit plus the episode report
**Branch:** `fix/s04e28-the-estimate` (PR, off `main`)
**Runtime:** one session, the morning after S04E24
**Director:** Larry David (showrunner seat, filled by a single orchestrator)
**Lead:** Morty Seinfeld (the receipt)
**Cast:** 0 sub-agents, inline passes by one orchestrator. Morty (LEAD), Puddy (guest: the run against the installed binary)

## The pitch

S04E24 added per-model Copilot list prices and said `--estimate` would use them. A check against the
installed binary, run after the merge, said otherwise: `hopper --estimate -m gpt-6.1-terra` failed
with "Unknown model". `RunEstimate` only ever called the Azure deployment estimator.
`CostEstimator.EstimateForCompatPreset`, written in S03E12 for exactly this case, had no caller, so
every model routed through `AZ_AI_COMPAT_MODELS` -- OpenAI, Groq, Together, Cloudflare and now
Copilot -- could not be estimated at all.

The fix is a dispatch decision: `RunEstimate` asks `ResolveDispatchInfo` where the model would be
sent, and a compat-routed model goes to its preset's estimator. A Copilot model the price table does
not list now reads "unpriced, $? estimate" with its real label, instead of calling Copilot an unknown
preset.

## Scene-by-scene

### Act I -- The claim

The S04E24 CHANGELOG and README both say `--estimate` uses Copilot list prices. Its unit tests
priced the table directly and checked the estimator's label, so they passed; none went through the
`--estimate` flag. The installed-binary run found it within minutes of the merge.

### Act II -- Fleet dispatch

| Wave | Agents (parallel) | Outcome |
|------|-------------------|---------|
| **1** | Morty | `RunEstimate` routes compat models to `EstimateForCompatPreset`; `HasModelTable` gives unpriced per-model presets an honest note |
| **2** | Puddy | Three CLI-level tests through `ParseArgs` and `RunEstimate`: routed and priced, routed and unpriced, unrouted and unknown |

### Act III -- Ship

Preflight in a clean worktree of the commit (integration against the AOT binary, as in S04E24),
then the commit on `fix/s04e28-the-estimate` and a PR.

## What shipped

- **Production code** -- `Program.RunEstimate` resolves the dispatch path before pricing;
  `CompatCostRates.HasModelTable`; a `CostEstimator.EstimateForCompatPreset` branch for a per-model
  preset that lacks the model.
- **Tests** -- 3 new cases in `CopilotPresetTests`, all driving the real flag parser and
  `RunEstimate`; the existing estimator suites are unchanged and pass.
- **Docs** -- CHANGELOG (Fixed); this report and its index row. The S04E24 README and CHANGELOG lines
  about `--estimate` become true with this change and are left as written.
- **Not shipped** -- a per-model table for the other compat presets (they keep their per-preset
  placeholders, `CompatCostRates` TODOs).

## Lessons from this episode

1. A claim about a CLI flag needs a test that goes through the flag. S04E24 tested the price table
   and the estimator function, and both were correct and unreachable.
2. A function with no caller is a finding. `EstimateForCompatPreset` sat unused for a season; a
   reference search when it was written would have shown it.
3. Check the installed binary after a merge, not just the build directory: that run found this.

## Metrics

- Diff size: 7 files (code, tests, docs).
- Test delta: +3 cases (CLI-level).
- Preflight: see the PR.
- CI status at push time: pending at the time of writing.

## Credits

Morty fixed the dispatch and the unpriced wording; Puddy's installed-binary run found the gap. The
commit carries the `Co-authored-by: Copilot` trailer.
