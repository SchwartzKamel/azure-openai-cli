# GHCR tag lifecycle

> "Tags are a contract. `latest` is a convenience. Digests are
> truth." -- Mr. Lippman

Audience: whoever runs the release workflow and anyone operating a
deployment that pins on an image tag. This page is the written policy
behind which OCI tags on `ghcr.io/schwartzkamel/azure-openai-cli/az-ai-v2`
exist, when they move, and when they don't.

Companion docs:
- [`semver-policy.md`](semver-policy.md) -- what counts as a
  "contract break" on the image.
- [`pre-release-checklist.md`](pre-release-checklist.md) -- gate 18
  is the workflow that actually pushes these tags.
- [`artifact-inventory.md`](artifact-inventory.md) -- what else
  publishes alongside each image.
- [`../runbooks/packaging-publish.md`](../runbooks/packaging-publish.md)
  -- Bob's downstream tap/bucket work consumes these tags.

**Images this policy covers:**

| Image                                                          | Line | Status            |
|----------------------------------------------------------------|------|-------------------|
| `ghcr.io/schwartzkamel/azure-openai-cli/az-ai-v2`              | v2   | Active.           |
| `ghcr.io/schwartzkamel/azure-openai-cli` (unsuffixed, v1 image)| v1   | Maintenance-only. |

The rules below are written for the v2 image. The v1 image follows
the same rules with `v1 / v1.X` floats instead of `v2 / v2.X`, and
PATCH-only cadence per the SemVer policy §7.

---

## 1. Tag classes

We publish **three** classes of tag. Every push from the release
workflow lands all three. Every class has a different stability
contract.

### 1.1 Version tags -- permanent, immutable

Examples: `2.0.4`, `2.0.5`, `2.0.5-rc.1`.

- **Format:** `MAJOR.MINOR.PATCH` (plus optional pre-release
  identifier), **no** `v` prefix. Matches the csproj `<Version>`.
- **Pushed:** once, from the release workflow at tag time.
- **Moved:** **never.** A version tag is bound to exactly one image
  digest for the life of the registry.
- **Deleted:** **never**, except under a security-advisory retraction
  with Newman sign-off (see [§6](#6-retraction)).
- **Retention:** permanent. We do not GC version tags.
- **Signed / attested:** yes. Each version tag has a Sigstore
  attestation (Rekor log index recorded in the GitHub Release body).

This is the only tag class a production deployment should pin on.

### 1.2 Floating majors / minors -- move forward only

Examples: `2`, `2.0`.

- **Format:** `MAJOR` and `MAJOR.MINOR`, no `v` prefix.
- **Pushed:** on every GA release on that major / minor line.
- **Moved:** forward-only, to the latest version tag within their
  scope. `2` moves on every v2 release; `2.0` moves on every 2.0.Z.
  When (if) we cut `2.1.0`, `2` moves to `2.1.0`; `2.0` stops moving
  and is frozen at whatever the last `2.0.Z` was.
- **Rolled back:** never. If a release is bad, we fix-forward with a
  new version tag; we do not point `2` at an older digest.
- **Pre-releases do not move floats.** `2.0.5-rc.1` does not move
  `2` or `2.0`. Only GA version tags move floats.
- **Deleted:** only when the entire major line is end-of-lifed and
  the deprecation window has closed. Advance notice via
  `### Deprecated` in the CHANGELOG for at least two MINORs first.
- **Signed / attested:** yes -- inherits the attestation of whatever
  version tag it currently points to.

Use these in **non-production** pins where you want "latest v2" or
"latest 2.0 patch" semantics. Expect the digest under you to change
any time a release cuts.

### 1.3 `latest` -- moves on every GA release

- **Format:** literally `latest`.
- **Pushed:** on every GA release, full stop. Including MAJOR cuts
  across version boundaries.
- **Moved:** to the just-published version tag, every time.
  `latest` follows whatever we most recently published, not a
  specific major line. If we cut a `3.0.0`, `latest` moves to
  `3.0.0` and there is no warning for users pinned on `latest`.
- **Pre-releases do not move `latest`.** `-rc` / `-beta` tags stay
  off the float.
- **Deleted:** only if the image itself is retired (we are not).
- **Signed / attested:** inherits from the current target.

**Do not pin production on `latest`.** It is a convenience for
`docker run` demos and CI smokes, not a deployment contract.
Documented in `README.md`; repeated here because Frank Costanza's
on-call pager doesn't care what the README said six months ago.

### 1.4 What we do *not* publish

- **Commit-SHA tags.** We do not push `sha-<hex>` per-commit tags to
  GHCR. If you need to pin to a specific build, use the image
  **digest** (`@sha256:…`) recorded in the GitHub Release body.
- **Branch tags.** No `main`, no `dev`, no `release-v2`. Anything
  appearing under those names on GHCR is a bug -- file it.
- **Date tags.** No `2026-04-22`. The GitHub Release date + the
  version tag cover this.

---

## 2. What moves on a typical release

At `v2.0.Z` GA cutover (not a pre-release), the release workflow
pushes, in order:

1. `2.0.Z` -- new version tag, immutable.
2. `2.0` -- moved forward to the new digest.
3. `2` -- moved forward to the new digest.
4. `latest` -- moved forward to the new digest.

All four tags point at the **same** manifest digest. Record that
digest in the GitHub Release body (pre-release-checklist gate 19).

For a pre-release (`v2.0.Z-rc.1`):

1. `2.0.Z-rc.1` -- immutable.
2. No float moves. `2`, `2.0`, `latest` stay where they were.
3. GitHub Release is marked "Pre-release" in the UI.

For a cancelled tag (`v2.0.3` style):

- If the workflow got far enough to push the version tag, **leave it**.
  The v2.0.3 GHCR image exists and stays; CHANGELOG marks it cancelled;
  floats never moved forward to it because the workflow aborted before
  the float-move step.
- If the workflow failed before the version push, nothing landed on
  GHCR. Do not retry on the same tag; cut a new version.

---

## 3. Digest pinning -- the escape hatch

Every tag resolves to a manifest digest (`sha256:…`). Digests are
the only truly immutable reference.

- The release workflow prints the digest in the job summary.
- Gate 19 of the pre-release checklist requires recording it in the
  GitHub Release body under an `Image digests` section, one line per
  platform leg.
- Production consumers that want zero surprises pin by digest:
  `ghcr.io/…/az-ai-v2@sha256:…`. Tags give you convenience; digests
  give you reproducibility.

Attestations are stored against the digest, not the tag. Retagging
a float does not invalidate any attestation -- it just points the
float at a different (still-attested) digest.

---

## 4. Interaction with SemVer

Per [`semver-policy.md`](semver-policy.md) §2, item 8:

- Changing the image `ENTRYPOINT` in a way that breaks
  `docker run …/az-ai-v2 --help` is a **MAJOR** bump.
- Changing the image tag **scheme** documented on this page is a
  **MAJOR** bump. "Adding an extra float tag" is a MINOR. "Renaming
  `latest` to something else" is a MAJOR (and we won't do it).
- Switching the image base **distro / libc** is a MINOR; switching
  within the same distro/libc is a PATCH. The tag layout is
  unchanged either way.

---

## 5. Rollback

**We do not roll back by moving floats backward.** If `2.0.5` is bad:

1. Cut `2.0.6` that fixes it. Version tag `2.0.5` stays on GHCR,
   permanently, as a historical marker. CHANGELOG for `2.0.6`
   explains what was wrong with `2.0.5`.
2. The release workflow on `2.0.6` advances `2.0`, `2`, and
   `latest` to the new digest.
3. Consumers who pinned on `2.0.5` explicitly by version tag are
   unaffected; they opted out of updates.
4. Consumers on floats pick up the fix on next pull.
5. Never `docker manifest rm`, never retag, never force-push a
   signed attestation.

See the release-rollback runbook (audit H-3) for the full procedure
including coordination with Homebrew / Nix / Scoop downstream.

---

## 6. Retraction

A version tag can be deleted **only** under:

- A security-advisory retraction with Newman's written sign-off and
  an advisory published in GHSA.
- A licensing / legal mandate with Jackie's written sign-off.

Procedure (skeleton; the rollback runbook has the full steps):

1. Decision recorded in an ADR under `docs/adr/`.
2. Floats moved forward to the last-known-good release (not
   backward) before the retracted tag is removed.
3. GitHub Release for the retracted version kept but edited to
   point at the advisory.
4. `CHANGELOG.md` entry for the retracted version **kept** with a
   prominent `### Security` / `### Notes` block explaining the
   retraction. Do not rewrite history in the CHANGELOG.

Retractions are expected to be rare to never. If this procedure is
being consulted seriously, loop in Lippman, Newman, and Jackie
before the first `gh` command runs.

---

## 7. Quick reference

| Tag           | Class    | Immutable? | Moves on each release? | Safe to pin in prod? |
|---------------|----------|:----------:|:----------------------:|:--------------------:|
| `2.0.4`       | version  | yes        | n/a (set once)         | yes                  |
| `2.0.5-rc.1`  | version  | yes        | n/a                    | no (pre-release)     |
| `2.0`         | float    | no         | yes, on 2.0.Z GA       | no                   |
| `2`           | float    | no         | yes, on any 2.x GA     | no                   |
| `latest`      | float    | no         | yes, on any GA         | no                   |
| `@sha256:…`   | digest   | yes        | n/a                    | yes                  |

-- Mr. Lippman, release management
