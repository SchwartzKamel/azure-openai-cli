# Slow Test Policy — `[Trait("type", "slow")]`

> **Status:** YAGNI for now. Not wired, not enforced, not decorative.
> **Owner:** Puddy. **Revisit:** if the xUnit wall-clock crosses 60 s on CI.

## The three-line justification

1. Suite wall-clock is currently under 30 s on CI (`make test` runs both
   projects in one `dotnet test` pass). There is no pain to relieve — a fast
   lane exists, it's called "the whole suite."
2. The handful of intentionally-slow tests (`RetryTests.BackoffTimingIs*`,
   cancellation-timing tests) are already small in absolute count and already
   paid for inside the 30 s budget. Tagging them buys nothing today.
3. Traits without CI wiring are decorative — they lie to readers about
   infrastructure that doesn't exist. We'd rather delete the promise than
   fake the infrastructure.

**Decision:** the BDD guide's `[Trait("type", "slow")]` reference is
aspirational. `docs/testing/README.md §7` already flags this honestly. We are
not going to tag tests `slow` today. We are not going to ship a
`make test-fast` target. We are not going to add `--filter type!=slow` to CI.

## Revisit criteria (when YAGNI expires)

Re-open this policy if **any** of the following become true:

- The full xUnit wall-clock (`dotnet test azure-openai-cli.sln`) crosses
  **60 s** on the CI Linux runner.
- A contributor opens a PR that adds a test over **2 s** wall-clock for a
  signal that cannot be pinned by a fake clock or a mock I/O surface.
- The property-test surface grows past one file (`CliParserPropertyTests`)
  and the generators routinely burn >500 ms each.
- We hit a regression where CI pipeline time becomes the bottleneck on merge
  throughput (Kenny Bania owns that signal).

Any of those → we wire the trait, add a filter, and update this doc. Until
then: **no `slow` trait. No exceptions. No decoration.**

## What to do instead (today)

- Sleep-based tests that can use a fake clock → use a fake clock. That's the
  deterministic fix, not a tag.
- Wall-clock assertions that exist to prove *structure* (e.g. "Task.WhenAll
  ran N things") should be bounded loosely (≤5 s) so they fail only on
  catastrophic re-serialisation regressions, not on CI jitter. See
  `ParallelToolExecutionTests` for the pattern and `c861c2e` for the
  narrative behind it.
- If you genuinely need a slow test (multi-second real-time backoff proof):
  keep it, write it deterministically against a bounded clock, and leave it
  in the main suite. We'll pay the seconds.

## Cross-references

- [`bdd-guide.md`](./bdd-guide.md) — still mentions the `slow` trait;
  treated as aspirational per §7 of the README.
- [`README.md §7`](./README.md) — honesty note on `[Trait]` status.
- Audit finding H6 in
  [`../audits/docs-audit-2026-04-22-puddy.md`](../audits/docs-audit-2026-04-22-puddy.md)
  — the finding this policy closes.

*Either it works or it doesn't. The trait doesn't. We stopped pretending.
High-five.*
