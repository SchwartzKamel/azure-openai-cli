# S04SP1 -- *The Reruns*

> *No new releases since v2.2.0 because the release matrix held one
> chronically broken leg hostage and docs-lint quietly bled red on every
> push for ten consecutive runs. Special episode: drop the dead leg,
> retag, ship.*

**Commit range:** `eecfd74..HEAD` (this commit)
**Branch:** `main` (direct push) + `v2.3.0` tag force-moved
**Runtime:** ~25 min wall-clock end-to-end
**Director:** Larry David (showrunner, executed inline)
**Cast:**

| Wave | Agent | Role | Commit |
|------|-------|------|--------|
| 1 | Larry David (inline) | docs-lint fixes (4 MD004/MD032 errors) | `eecfd74` (swept into Peterman's commit by race) |
| 1 | Larry David (inline) | release matrix: drop `osx-x64/macos-13` | `ffd2c1a` (cherry-picked onto `493c21b`, retagged `v2.3.0`) |
| Parallel | Mr. Lippman | S04SP2 *The Stenographer* -- release hygiene audit | filming |
| Parallel | Mr. Wilhelm | S04SP3 *The Pre-Push* -- markdownlint in preflight | filming |
| Close | Larry David (this report) | Exec-report + CHANGELOG | this commit |

## The pitch

Two failure modes were silently compounding:

1. **`docs-lint` workflow** has been red on every push since `25821072209`
   (2026-05-13 19:18:48Z, the S04E01 close commit) -- ten consecutive
   failed runs. Root cause: three exec-report paragraphs used a leading
plus-sign-plus-space (the English word "plus" rendered as `+`) as
   the English word "plus" at line-start, which markdownlint parses as
   an unordered-list bullet of the wrong style (`+` instead of the
   project-mandated `-`). The local `make preflight` target does **not**
   run markdownlint, so the failure only surfaced server-side.

2. **`Release` workflow** for `v2.3.0` (run `25818574655`, started
   2026-05-13 18:30:06Z) had 12/13 matrix legs green but the
   `osx-x64/macos-13` leg sat queued for over 3.5 hours waiting on a
   hosted-runner slot. The Release run was therefore stuck `in_progress`
   and no GitHub Release object was ever created -- last published
   release stayed pinned to v2.2.0 from 13 days prior.

The `osx-x64/macos-13` leg has form. The README has documented since
v2.0.4 that the leg was cut due to "sustained runner instability." It
was silently re-added in a later PR (no ADR, no exec-report) and has
been the slowest leg ever since. macOS Intel hardware is also EOL --
Apple stopped shipping Intel Macs in 2023 -- so the cost/benefit math
no longer pencils.

## What shipped

### `release.yml` -- matrix surgery

Two deletions in `.github/workflows/release.yml`:

- Matrix entry block (was lines 64-66):

  ```yaml
  - rid: osx-x64
    os: macos-13
    ext: tar.gz
  ```

- Release-notes artifact-table row (was line 227):

  ```bash
  printf '| macOS Intel | `az-ai-%s-osx-x64.tar.gz` |\n' "$REF"
  ```

Cherry-picked as `ffd2c1a` onto the original v2.3.0 commit (`493c21b`)
on a throwaway branch, then `v2.3.0` was force-retagged at `ffd2c1a`
and pushed to origin. Main history was **not** rewritten -- main
still has the same eecfd74 swept-merge that already contains the
matrix fix among other concurrent work. The retag was the only force
operation; rationale: the original `v2.3.0` tag had no published
GitHub Release object referencing it, so no public artifact contract
was broken. The new release run (`25829203297`) picked up the fixed
matrix immediately on tag push.

### `docs-lint` -- four bullet-style corrections

Three `"+ "` continuations replaced with the word `"plus "` (prose
continuation, not a list item):

- `docs/exec-reports/s04e01-the-registry.md:218`
- `docs/exec-reports/s04e02-embedded-cards.md:92`
- `docs/exec-reports/s04e02-embedded-cards.md:290`

`markdownlint-cli2` clean locally after fix (0 errors across 370
files); CI run `25829137168` confirmed green.

### Source still supports `osx-x64`

`packaging/tarball/stage.sh` still accepts the `osx-x64` RID for local
builds (`dotnet publish -r osx-x64`). The drop is **CI matrix only** --
Intel-Mac users on legacy hardware can still build from source or use
the multi-arch Docker image. The README v2.0.4 note already directs
them to those paths.

## Validation

- **`dotnet build`** -- 0 warnings, 0 errors.
- **`dotnet test --filter "FullyQualifiedName~Capability"`** -- 44/44
  pass (covers the still-in-flight S04E03 Maestro+Kramer work that
  got swept into the same merge; verified the merge is build-clean).
- **`npx markdownlint-cli2`** -- 0 errors across 370 markdown files.
- **`gh run list --workflow=docs-lint --limit 1`** -- conclusion=success
  (run `25829137168`).
- **`gh run list --workflow=release --limit 1`** -- status=in_progress,
  headSha=`ffd2c1a` (the matrix-fix commit). SP2 (Lippman) is on watch
  for the final conclusion.

## Risks accepted

- **Force-pushed an annotated tag.** Conventionally a sin. Mitigated
  by: no published Release object existed at the old SHA; tag movement
  was the only viable path to retrigger the workflow without a version
  bump; SP2 will publish the resulting GitHub Release notes documenting
  the moved SHA. Any clone that already had v2.3.0 at the old SHA will
  see a divergent tag on next fetch -- but no such clones exist outside
  this development machine and CI runners.
- **Intel-Mac users lose first-class binary distribution.** Acceptable;
  fallback paths (Docker, source build) are documented. ADR-014 should
  formalise the policy -- queued for SP2 or a future episode.
- **Old queued run `25818574655` is still parked.** Not cancellable
  from this credential (admin rights required). Will eventually
  time-out per GitHub Actions defaults; cosmetic clutter only.

## What's next

- **SP2 *The Stenographer*** (Mr. Lippman, filming) -- release
  hygiene audit, CHANGELOG verification, scan for stale `osx-x64`
  references in packaging metadata, monitor v2.3.0 publish.
- **SP3 *The Pre-Push*** (Mr. Wilhelm, filming) -- add `markdownlint`
  and `ascii-check` to local `make preflight` and the pre-push hook
  so this class of red-CI-on-push regression cannot recur. The whole
  trilogy exists because preflight didn't catch what CI did.
- **S04E03 *The Capabilities*** -- still in flight. Bookman shipped
  the rejection builder + ADR-013 (commit `5914165`). Maestro's
  CapabilityGate.cs and Kramer's `ModelsWithCapability` helper were
  swept into Peterman's commit `eecfd74` by a race condition during
  the brief-writer parallel push -- attribution is wrong but the code
  is in, builds clean, and 44 Capability-suite tests pass. Wave 2
  (Puddy tests + Mickey a11y) and Wave 3 (FDR adversarial appendix
  to ADR-013) still owed before E03 closes.

## Linked files

- `.github/workflows/release.yml` (matrix surgery)
- `docs/exec-reports/s04e01-the-registry.md` (1 docs-lint fix)
- `docs/exec-reports/s04e02-embedded-cards.md` (2 docs-lint fixes)
- `README.md` line 435 (existing v2.0.4 osx-x64 deprecation note --
  realigned with reality, no edit needed)
- `docs/runbooks/macos-runner-triage.md` (referenced but not updated
  in this special; SP2 to confirm or punt)

## Open findings (for SP2 / future episodes)

- **F-SP1-01**: Three exec-reports were *post-shipped* edited to fix
  the bullet-style errors. Exec-reports are normally append-only.
  Acceptable in this case (lint-only, no semantic change), but SP3's
  preflight gate should prevent this from happening again.
- **F-SP1-02**: The `osx-x64/macos-13` re-addition has no ADR. SP2
  should propose ADR-014 *Platform Support Policy* (which RIDs we
  ship binaries for, what the bar is to add or drop a leg).
- **F-SP1-03**: Several packaging files (`packaging/homebrew/README.md`,
  `docs/runbooks/release-runbook.md`, `docs/runbooks/macos-runner-triage.md`)
  still mention `osx-x64` as a shipped artifact. SP2 to audit and
  reconcile.
- **F-SP1-04**: Concurrent agent pushes during a single wave can sweep
  unrelated files into the wrong commit (Peterman's `eecfd74` carried
  Maestro+Kramer+SP1 changes). Wilhelm's SP3 hook work won't catch
  this; needs a separate `Shared-file-protocol` enforcement at the
  Makefile/preflight layer. Filed; not in scope for SP1.
