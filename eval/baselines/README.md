# Eval baselines

A baseline is a recorded run of a built-in eval corpus against one pinned model: per fixture, whether
it passed and how many runs passed. It contains **outcomes only, never model text**. The gate
(`scripts/eval-gate.sh`) compares each new run with the baseline and fails on a **regression**: a
fixture that passed then and does not now.

## Files

`<model>.<corpus>.json`, for example `gpt-4o-mini.smoke.json`. Slashes, colons and spaces in the model
name become underscores. Each model has its own baseline; comparing across models is allowed but is
flagged in the report.

## Recording baselines

You need credentials and a pinned model. This spends tokens: the policy runs each fixture
`repeat` times (3 by default) across 25 fixtures.

```bash
export AZUREOPENAIENDPOINT=... AZUREOPENAIAPI=...
export EVAL_GATE_MODEL=gpt-4o-mini
BIN=./azureopenai-cli/bin/Release/net10.0/az-ai scripts/eval-gate.sh baseline
git add eval/baselines && git commit -m "test(eval): record baselines for gpt-4o-mini"
```

Review the files before committing. A baseline recorded from a failing run records the failures as
failing; the command warns when that happens.

## Status

**No baselines are committed yet.** No live model has been run against the corpora, so there is nothing
honest to record. Until baselines exist the gate still enforces the per-corpus score thresholds in
`.github/eval-gate.json`; it just has no "used to pass" history to regress against.
