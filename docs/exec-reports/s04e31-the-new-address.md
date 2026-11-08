# S04E31 -- *The New Address*

> *Renamed on a Sunday, with the old name still answering the door.*

**Commit:** `(this commit)` -- one commit, after the repository rename
**Branch:** `docs/s04e31-the-new-address` (PR, off `main`)
**Runtime:** one session, tenth of the gap-fixing batch
**Director:** Larry David (showrunner seat, filled by a single orchestrator)
**Lead:** Mr. Lippman (the release) with Bob (packaging) and Newman (provenance)
**Cast:** 0 sub-agents, inline passes by one orchestrator. Lippman (LEAD), Bob (the manifests), Newman (guest: what an attestation names), Jerry (guest: the image path)

## The pitch

ADR-016's last stage: the GitHub repository becomes `SchwartzKamel/hopper`. It was left for last on purpose, after the release and the manifest
hash-sync, so the rename could not complicate either. This episode is the rename's aftermath: what it broke (nothing), what it left behind
(addresses in 51 living files), and what it did not move (the container image).

## Scene-by-scene

### Act I -- The three public steps, in order

v2.4.0 was tagged and published (S04E29), the manifests were hash-synced and checked against its digests (S04E30), and only then was the
repository renamed with `gh repo rename`. The order mattered: renaming first would have put the release and a container-path change in one
event, and would have left the manifests pointing at an address the release had not used.

### Act II -- Prove the old name still works

A rename is only safe if the old address keeps working, so each form was checked rather than assumed, before and after:

- the old repository URL answers **301** to the new one;
- an old release-asset URL still answers **200** (the baseline was recorded first, so the comparison was real);
- the API endpoint redirects, and `git ls-remote` through the old SSH remote works;
- GitHub Actions runs under the new name: the packaging rehearsal, dispatched by hand, passed all seven jobs, and the manifest check follows the
  redirect.

### Act III -- Two things a rename does not carry

**Provenance.** `gh attestation verify --owner SchwartzKamel` still verifies a v2.4.0 archive, which is what the release body tells people to
use. But `--repo SchwartzKamel/hopper` does **not**: the existing attestations name the old repository, and only `--repo` with the old name matches.
A one-byte-corrupted copy is still rejected either way. It is recorded as `newman-2026-11-N-15`.

**The container image.** The image path is taken from the repository name when an image is published, so `ghcr.io/schwartzkamel/azure-openai-cli:2.4.0`
exists and `ghcr.io/schwartzkamel/hopper:2.4.0` does not. The next release creates the new path. The 70 documentation references to the image
path are left alone for that reason, and the old path's support window is a decision for then (`jerry-2026-11-JE-9`).

### Act IV -- The sweep, with two exclusions

136 occurrences of the old slug in 51 living files now name the new one: the issue templates (user-facing links), `SECURITY.md` (the advisory
link), the Dockerfile's OCI `source` and `documentation` labels (the next image will carry them), the packaging manifests and docs, and the
runbooks. Two things were deliberately not touched: the lowercase `ghcr.io` image path above, and the frozen `az-ai@<version>` pins and history
documents, which describe the repository as it was. Then it was verified, not trusted: the five real asset URLs the manifests name return 200
through the new slug, the manifests still match the published digests, CI's link check passes, and a fresh Homebrew install through the new
slug works and audits clean.

### Act V -- Something this repository cannot fix

Smith's own configuration names this repository by its GitHub slug (its project entry's `git_remote`). The redirect keeps it working, but the
line should change to the new address. It is smith's file, not this repository's, so it is reported rather than edited.

## What shipped

- The rename (an action on GitHub, not a commit), and a 51-file sweep of the old address, with the image path and the history left as they were.
- ADR-016 updated: Stages 2 and 3 are done. CHANGELOG, the findings backlog and this report.

**Not shipped**

- The container image path and its 70 references: they move with the next release.
- Renaming the local checkout directories: smith's configuration refers to the path `~/sandbox/azure-openai-cli`.

## Lessons from this episode

1. **Record the baseline before the irreversible step**, so "it still works" is a comparison.
2. **Check the old name and the new name both**, for the web, the API, the asset downloads and git.
3. **A rename does not carry everything.** An image path and an attestation both remember the old name.
4. **Sweep the living, leave the history.** A document about the past should describe the past.
5. **Report what you cannot edit.** Smith's configuration is smith's.

## Metrics

- **Redirects:** web 301, API 301, old asset URL 200, old SSH remote working; Actions: 7 of 7 jobs green under the new name.
- **Sweep:** 136 occurrences, 51 files; 70 image references and 10 frozen pins untouched.
- **Preflight:** `ascii-check`, `markdownlint`, `exec-report-check`, CI's link check, `scripts-ignore-check`, the integration suite.
- **CI status at push time:** pending at commit time; link in the PR.

## Credits

Lippman led because the subject is the release and where it lives. Bob's lens is the manifests, Newman's is what an attestation names, and
Jerry's is the image path. Commits carry `Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>`.
