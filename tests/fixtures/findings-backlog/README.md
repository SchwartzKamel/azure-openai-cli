# tests/fixtures/findings-backlog

Regression fixtures for the findings-backlog gate baked into
`scripts/exec-report-check.sh` (Phase 1). The gate is the closing
deliverable for finding W-01 in
`docs/audits/audit-process-meta-2026-05.md`.

## What's here

- `audit-with-unindexed-finding.md` -- audit with a CRITICAL finding `T-1`
  that is NOT mirrored anywhere. Driving the gate against this audit must
  produce a non-zero exit and a "FAIL (findings-backlog gate)" message.
- `audit-with-indexed-finding.md` -- audit with a CRITICAL finding `T-1`
  that IS mirrored in `backlog-with-T-1.md`. Driving the gate against
  this pair must produce a zero exit.
- `backlog-with-T-1.md` -- the matching backlog row for the positive case.

## How they're driven

`tests/findings-backlog-lint-test.sh` builds a throw-away workspace under
the system temp directory, lays out a minimal repo skeleton with one
fixture audit + one backlog file, copies the gate script, points it at
the fake repo via `git -C`, and asserts pass / fail behavior.

Two assertions:

1. Negative fixture -> gate exits non-zero and prints the unindexed ID.
2. Positive fixture -> gate exits zero.

This is opt-in: `make findings-backlog-test`. It is not wired into
`make preflight` because preflight already runs the gate against the
real `docs/audits/` corpus, which is the production assertion. The
fixture harness exists for changes to the gate logic itself.
