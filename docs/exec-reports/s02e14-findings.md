# S02E14 -- Findings backlog (sub-agent staging)

> Staging file for findings surfaced by S02E14 *The Container*. The
> orchestrator integrates these into `s02-writers-room.md` ->
> "Findings backlog" subsection on the next writers' room update.
>
> Format follows [`findings-backlog`](../../.github/skills/findings-backlog.md)
> verbatim.

## Findings (1)

- **`e14-trivy-non-blocking`** [gap, b-plot]
  Surfaced by S02E14 *The Container*. The Trivy image-scan step in
  CI is wired with `exit-code: '0'`, meaning HIGH/CRITICAL CVEs in
  the shipped Docker image are reported but do **not** fail the
  build. The comment at `ci.yml:138` documents the intent to flip
  to `exit-code: '1'` "once the v2.0.x CVE backlog is clean," but
  no episode owns that flip. Without an owner, the advisory state
  is the de-facto permanent state.
  File: `.github/workflows/ci.yml:138-146`.
  Suggested disposition: queue as a future audit episode (S02E22
  candidate, paired with the v2 CVE-baseline cleanup) where Newman +
  Jerry confirm the baseline is green and flip the gate in the
  same PR. Do **not** flip unilaterally outside an episode -- a
  single newly-disclosed CVE could redden `main` indefinitely if
  no one is on-call to fix-forward.
