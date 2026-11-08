# S04E33 -- *The Mirror*

> *The new address had no mail. The obvious fix would have shredded the old mail.*

**Commit:** `(this commit)` -- one commit; the mirror is run after it merges (S04E34)
**Branch:** `ci/s04e33-the-mirror` (PR, off `main`)
**Runtime:** one session, twelfth of the gap-fixing batch
**Director:** Larry David (showrunner seat, filled by a single orchestrator)
**Lead:** Jerry (CI and the registry)
**Cast:** 0 sub-agents, inline passes by one orchestrator. Jerry (LEAD), Newman (guest: what an attestation claims), Mr. Lippman (guest: what a release is allowed to change)

## The pitch

After the rename, `ghcr.io/schwartzkamel/hopper` had no images: the image path comes from the repository name at publish time, and every
release so far was published under `ghcr.io/schwartzkamel/azure-openai-cli`. The v2.4.0 image is right there (an index of two manifests, the
`linux/amd64` image and its build attestation, all four tags on one digest). It needs to be reachable at the new path.

## Scene-by-scene

### Act I -- The tempting fix, and why it is wrong

`release.yml` can be re-run against the existing tag with a workflow dispatch, and it would publish the image under the new name. It would also
**rebuild the archives**. AOT builds are not byte-reproducible, so the new archives would have different checksums from the ones in
`digests.txt`, which the manifests pin, the tap and bucket mirror, and the provenance describes. One re-run would quietly invalidate everything
the last three episodes verified, and `release` would try to overwrite the published assets. So the image gets its own narrow workflow.

### Act II -- Copy, do not build

`scripts/mirror-image.sh` copies by digest with `docker buildx imagetools create <source>@<digest>`, so the mirrored image **is** the published
one, attestation manifest included. It reads the destination digest back and fails if it differs. It mirrors a tag only if the **source** has
that tag at the same digest: `2.4.0` always, `2.4`, `2` and `latest` only when the source points them at this version, so mirroring an old
version can never move `latest` backwards. It refuses a version that is not `X.Y.Z`, a missing source tag, and an image copied onto itself, and
it lowercases the default target (image references must be lowercase).

### Act III -- No new provenance, on purpose

The workflow has no attestation step, and a test fails if one is added. This repository did not build that image, so a new attestation would claim
a build that did not happen here. The original build's attestation travels with the image, and GitHub's attestation lookup is by digest, so
`gh attestation verify oci://... --owner SchwartzKamel` finds it. (Whether it does is checked after the run, in S04E34.)

### Act IV -- Testing a registry operation without a registry

The tests use a stub `docker` that answers `buildx imagetools inspect` and `create` from a text file and treats **anything else** (a build, a
push, a tag) as a violation written to a log. That proves, offline, that exactly one `create` runs, from `<source>@<digest>`, and nothing else
does. The cases cover: the full tag set, an old version that must not move `latest`, a source with no major tag, a missing version, a corrupted
destination digest, a failed copy, five malformed versions, an image onto itself, a missing target, and the lowercased default.

### Act V -- A test that was too literal

One workflow test failed on its first run because it grepped the whole file for "attest" and found the word in the workflow's own comment,
which explains why there is no attestation. It now inspects only what the steps `use` and `run`.

## What shipped

- `scripts/mirror-image.sh`, `.github/workflows/mirror-image.yml` (manual dispatch, `packages: write`, actions pinned to commits), and the
  `.gitignore` whitelist the script needed.
- 16 integration cases. **Mutation-checked:** 14 mutations (10 on the script, 4 on the workflow), each caught, each verified applied.
- CHANGELOG, the findings backlog and this report.

**Not shipped yet**

- **Running it.** A workflow can only be dispatched once it is on `main`, and I cannot push to GHCR from here. S04E34 runs it, verifies the result
  (including anonymous pull, since a new package may be private), and points the documentation at the new path.
- **A decision on the old path.** It is frozen at v2.4.0 and stays up; the support window is recorded in S04E34.

## Lessons from this episode

1. **Re-running a release is not free.** It rebuilds, and a rebuild is a different artifact.
2. **Copy by digest, then read it back.** The check is that the destination is the same bytes.
3. **Do not attest what you did not build.** Say where it came from instead.
4. **Stub the registry, and make the stub strict.** Anything unexpected is a logged violation.
5. **A test of a workflow should read steps, not text.** The comment that explains an omission is not the omission.
6. **A test must control every variable the code reads.** The pull request's first CI run failed because one case assumed `GITHUB_REPOSITORY` was
   unset and GitHub Actions always sets it; the same class as the credentials file that made a test pass only on this machine. It was reproduced locally
   by setting the variable, fixed with `env -u`, and the whole suite was then run with CI-like variables.

## Metrics

- **Test delta:** +16 integration cases; unit unchanged.
- **Mutations:** 14 caught.
- **Preflight:** format, 0 warnings, unit, integration, `ascii-check`, `markdownlint`, `exec-report-check`, `scripts-ignore-check`, CI's link check.
- **CI status at push time:** pending at commit time; link in the PR.

## Credits

Jerry led because the subject is the registry path. Newman's lens is what an attestation claims and Lippman's is what a release may change.
Commits carry `Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>`.
