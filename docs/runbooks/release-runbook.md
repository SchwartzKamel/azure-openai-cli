# Release Runbook (v2 line)

> How to cut a release of `azure-openai-cli` on the v2 line without
> waking anyone up. Audience: maintainers with push + tag permissions
> on `origin`.

**Source of truth for the matrix:** `.github/workflows/release.yml`.
If this runbook and the workflow disagree, the workflow wins -- file a
PR to sync this doc.

**Post-v2.0.4 reality check:**
- Active ship path is **v2** (`azureopenai-cli-v2/`, binary `az-ai-v2`,
  GHCR image `ghcr.io/schwartzkamel/azure-openai-cli/az-ai-v2`).
- Release matrix is **4 legs**: `linux-x64`, `linux-musl-x64`,
  `osx-arm64`, `win-x64`. `osx-x64` was cut in v2.0.4; see
  [`docs/runbooks/macos-runner-triage.md`](macos-runner-triage.md) §5.
- v1 line (`azureopenai-cli/`, `az-ai`) still exists in `release.yml`
  for patch releases only; do not use this runbook for `v1.*` tags.

---

## 0. Inputs

| Variable  | Example      | Where it lives                                             |
|-----------|--------------|------------------------------------------------------------|
| `VERSION` | `2.0.5`      | `azureopenai-cli-v2/AzureOpenAI_CLI_V2.csproj` `<Version>` |
| `TAG`     | `v2.0.5`     | Git tag, annotated                                         |
| `DATE`    | `2026-04-30` | `CHANGELOG.md` `[X.Y.Z] -- DATE` header                     |

SemVer: **patch** = hot-fixes / docs / dependency bumps, **minor** =
backwards-compatible feature, **major** = breaking change to CLI flags,
config schema, or the persisted `~/.azureopenai-cli.json` contract.

---

## 1. Pre-flight checklist

Run **in order**. Do not proceed past a red box. If any step fails,
fix-forward on `main` -- do **not** cut the tag and hope CI saves you.

```bash
# 1a. Working tree: clean, on main, up-to-date.
git status
git -c commit.gpgsign=false pull --rebase origin main

# 1b. Format check -- CI runs this with --verify-no-changes and will fail on drift.
dotnet format azure-openai-cli.sln --verify-no-changes

# 1c. Full v1 test suite -- baseline 1025 tests.
dotnet test tests/AzureOpenAI_CLI.Tests/AzureOpenAI_CLI.Tests.csproj --verbosity minimal

# 1d. Full v2 test suite -- baseline 485+ tests (grows each release).
dotnet test tests/AzureOpenAI_CLI.V2.Tests/AzureOpenAI_CLI.V2.Tests.csproj --verbosity minimal

# 1e. Binary smoke -- build + confirm `--version --short` matches csproj.
make publish-linux-x64
./artifacts/publish/linux-x64/az-ai-v2 --version --short
#  ^ must print exactly `<csproj Version>\n` (≤ 10 bytes, trailing LF).
```

> **Contract test.** Kramer is landing `VersionContractTests.cs` under
> `tests/AzureOpenAI_CLI.V2.Tests/` that asserts `--version --short`
> equals the csproj `<Version>`. Once merged, that test is the
> authoritative gate; the manual smoke above becomes a belt-and-braces
> check you can skip in a rush. Until it's merged, **do the smoke
> manually.**

- [ ] `git status` clean, on `main`, `origin/main` fetched.
- [ ] `dotnet format … --verify-no-changes` passes.
- [ ] v1 test suite green (~1025).
- [ ] v2 test suite green (485+).
- [ ] `az-ai-v2 --version --short` == csproj `<Version>`.
- [ ] `CHANGELOG.md` has a populated `[X.Y.Z]` section and
      `[Unreleased]` is reset to a bare header.
- [ ] `azureopenai-cli-v2/AzureOpenAI_CLI_V2.csproj` `<Version>`
      matches the tag you're about to push (without the `v` prefix).
- [ ] `NOTICE` reflects the current direct dependency set.
- [ ] `.config/dotnet-tools.json` is committed (release workflow uses
      `dotnet tool restore` for CycloneDX SBOM generation).
- [ ] Latest `ci.yml` run on `main` is green for the commit you're
      about to tag: `gh run list --workflow ci.yml --branch main --limit 1`.

---

## 2. Version bump + CHANGELOG

```bash
VERSION=2.0.5
TAG="v${VERSION}"
DATE=$(date -u +%Y-%m-%d)

# 2a. Bump <Version> in the v2 csproj.
sed -i "s|<Version>.*</Version>|<Version>${VERSION}</Version>|" \
  azureopenai-cli-v2/AzureOpenAI_CLI_V2.csproj

# 2b. Confirm.
grep '<Version>' azureopenai-cli-v2/AzureOpenAI_CLI_V2.csproj
```

Then edit `CHANGELOG.md` by hand:

1. Insert a new `## [${VERSION}] -- ${DATE}` section directly under
   `## [Unreleased]`.
2. Move any `[Unreleased]` entries destined for this cut into the new
   section. Group by `Added` / `Changed` / `Fixed` / `Removed` /
   `Security` (Keep a Changelog conventions).
3. Reset `[Unreleased]` to a bare header (no entries; no empty
   subsections).

---

## 3. Commit + tag ritual

```bash
git add azureopenai-cli-v2/AzureOpenAI_CLI_V2.csproj CHANGELOG.md

git -c commit.gpgsign=false commit -m "release: v${VERSION} -- <one-line summary>

<2-3 sentences of narrative; what shipped, why it matters. Mr. Lippman
owns the longer release-body prose in docs/launch/; this is the commit
message, keep it tight.>

Co-authored-by: Copilot <copilot@github.com>"

git tag -a "${TAG}" -m "${TAG} -- <one-line summary, same as commit subject>"

# Push commit BEFORE tag. If the tag lands before the commit, the
# release workflow races and may build against a missing ref.
git push origin main
git push origin "${TAG}"
```

**Rules:**
- Tags **must** be annotated (`-a`). Lightweight tags break the
  release workflow's `actions/checkout` + metadata steps.
- Signed (`-s`) is preferred if you have a key configured locally.
  The workflow does not verify signatures today.
- **Never rewrite a tag once pushed.** Attestations and GHCR digests
  are anchored to it; rewriting invalidates downstream trust. Fix
  forward with the next patch.

---

## 4. Watch the release workflow

```bash
# Find the release run triggered by your tag push.
gh run list --event push --limit 3

# Pick the run-id for your tag and watch it.
RUN_ID=<paste>
gh run view "$RUN_ID"
gh run watch "$RUN_ID"
```

The `v2.*` tag path exercises three jobs in `release.yml`:

1. **`build-binaries-v2`** -- 4-leg matrix producing AOT single-file
   binaries, CycloneDX SBOMs, and SLSA build-provenance attestations:
   - `linux-x64` (ubuntu-latest)
   - `linux-musl-x64` (ubuntu-latest + musl toolchain)
   - `osx-arm64` (macos-14)
   - `win-x64` (windows-latest, PowerShell `Compress-Archive`)
2. **`docker-publish-v2`** -- builds `Dockerfile.v2`, pushes to
   `ghcr.io/schwartzkamel/azure-openai-cli/az-ai-v2:<version>` (plus
   `latest` on non-pre-releases), attests image provenance to Sigstore
   (logged to Rekor).
3. **`release-v2`** -- gated on both above; downloads all artifacts,
   computes digests, creates the GitHub Release with auto-generated
   notes + SBOMs + binaries attached.

> **Do not cross the streams.** The v1 jobs (`build-binaries`,
> `docker-publish`, `release`) are `if`-gated on `v1.*` tags and
> won't run for your `v2.*` tag. Confirm this in the run summary --
> v1 jobs should show as "skipped", not "failed".

---

## 5. Recovery levers (when the run goes red)

### 5.1 Rerun failed legs only

```bash
gh run rerun "$RUN_ID" --failed --repo SchwartzKamel/azure-openai-cli
```

`--failed` preserves green legs (their artifacts are already uploaded
and won't be rebuilt) and only reschedules failed/cancelled jobs. This
is the first lever for transient flake -- runner pool backlog, registry
hiccup, actions/upload-artifact 5xx, `dotnet restore` network drop.

### 5.2 The `workflow_dispatch` HTTP 422 trap

```bash
# DOES NOT WORK on pre-existing tags cut before the workflow_dispatch
# trigger was added:
gh workflow run release.yml --ref v2.0.1   # → HTTP 422
```

`workflow_dispatch` needs the `workflow_dispatch:` trigger to exist in
`release.yml` **at the ref's commit**, not at `HEAD`. Tags are
immutable snapshots. If the tag predates the trigger, dispatch against
it returns 422. Use `gh run rerun --failed` instead (§5.1).

This trap blew up ~9 hours of v2.0.2 recovery time. It is documented
in [`docs/launch/v2.0.2-publish-handoff.md`](../launch/v2.0.2-publish-handoff.md).

### 5.3 Fix-forward re-tag (when a runner pool is wedged)

When rerun-failed won't clear the backlog -- a platform's runner pool
is persistently unavailable, or you've rerun twice and both times got
stuck in `queued` -- cut the next patch:

```bash
# 1. Fix the root cause on main (e.g., drop the flaky RID from the
#    matrix, pin a newer action, bump a dependency).
# 2. Bump <Version> in the csproj (§2).
# 3. CHANGELOG entry explaining the fix-forward.
# 4. Tag + push (§3) using the next patch number.
```

Do **not** delete and re-push the broken tag. See §6 for the
macos-runner escalation path, and [`macos-runner-triage.md`](macos-runner-triage.md)
for the full macOS decision tree.

### 5.4 Partial publish -- Docker green, binaries red

`docker-publish-v2` can succeed while `build-binaries-v2` has a leg
still queued. GHCR will have the image but no GitHub Release exists
yet (release-v2 needs both). That is **not a recovery emergency** --
the GHCR image is fine, published, attested. Let the binary leg
finish; if it can't, fix-forward and the GHCR image for the new tag
will supersede.

---

## 6. Monitor + unblock macOS

The `osx-arm64` / `macos-14` leg is the most backlog-prone. If it has
been `queued` > 30 min while `linux-x64` is already green, you are
almost certainly hitting a runner pool backlog.

See **[`docs/runbooks/macos-runner-triage.md`](macos-runner-triage.md)**
for the full decision tree, escalation thresholds, and the "two
release cycles" cut heuristic.

Quick summary of thresholds:

| Elapsed in `queued` | Action                                                    |
|---------------------|-----------------------------------------------------------|
| < 30 min            | Wait. Normal.                                             |
| 30-60 min           | Check githubstatus.com; note in `#release`; keep waiting. |
| 60-90 min           | Post an explicit status comment on the release issue.     |
| 90 min              | `gh run rerun $RUN_ID --failed` (cancel first if needed). |
| 2h                  | Open a GitHub Support ticket.                             |
| Two cycles in a row | Consider a matrix cut (§5.3 + macos-runner-triage.md §5). |

---

## 7. Post-publish verification

```bash
TAG=v2.0.5
VERSION=${TAG#v}

# 7a. Release object has 4 binaries + 4 SBOMs + source archives.
gh release view "$TAG"
gh release view "$TAG" --json assets --jq '.assets[].name' | sort

# Expected (post-v2.0.4):
#   az-ai-v2-${VERSION}-linux-x64.tar.gz        + .cdx.json
#   az-ai-v2-${VERSION}-linux-musl-x64.tar.gz   + .cdx.json
#   az-ai-v2-${VERSION}-osx-arm64.tar.gz        + .cdx.json
#   az-ai-v2-${VERSION}-win-x64.zip             + .cdx.json

# 7b. Binary attestations -- SLSA provenance verified against the repo.
gh attestation verify \
  --repo SchwartzKamel/azure-openai-cli \
  "az-ai-v2-${VERSION}-linux-x64.tar.gz"

# 7c. Container image + attestation.
docker pull "ghcr.io/schwartzkamel/azure-openai-cli/az-ai-v2:${VERSION}"
gh attestation verify \
  --repo SchwartzKamel/azure-openai-cli \
  "oci://ghcr.io/schwartzkamel/azure-openai-cli/az-ai-v2:${VERSION}"

# 7d. Runtime smoke on the primary RID.
./az-ai-v2 --version --short   # must print exactly "${VERSION}\n"
```

---

## 8. Hash-sync to packaging manifests

> **Owner:** Mr. Lippman (release manager). Hash-sync is his ritual;
> Jerry's role in this runbook is to document the mechanics so Lippman
> doesn't have to keep finding the incantation.

After the release publishes, pull the tarball digests from the Release
assets and update:

- `packaging/homebrew/Formula/az-ai.rb` (rolling formula, always
  points at latest)
- `packaging/homebrew/Formula/az-ai-v2@${VERSION}.rb` (new sibling
  formula, pinned to this release -- copy from the previous `@X.Y.Z`
  formula and update `url`, `sha256`, and `version`)
- `packaging/nix/flake.nix` (SRI-format SHA-256, `sha256-<base64>`)
- `packaging/scoop/az-ai.json` (straight hex SHA-256, `win-x64` only)

```bash
VERSION=2.0.5

# Fetch digests directly from the Release (no re-hashing local tarballs).
for rid in linux-x64 linux-musl-x64 osx-arm64; do
  asset="az-ai-v2-${VERSION}-${rid}.tar.gz"
  gh release download "v${VERSION}" --pattern "${asset}" --clobber
  echo "${rid}: $(sha256sum "${asset}" | awk '{print $1}')"
done

# Windows zip (Scoop).
asset="az-ai-v2-${VERSION}-win-x64.zip"
gh release download "v${VERSION}" --pattern "${asset}" --clobber
echo "win-x64: $(sha256sum "${asset}" | awk '{print $1}')"

# Nix SRI: sha256-<base64(raw-sha256)>.
for f in az-ai-v2-${VERSION}-*.tar.gz; do
  echo "$f: sha256-$(sha256sum "$f" | awk '{print $1}' | xxd -r -p | base64)"
done
```

Commit the manifest updates as a single packaging commit:

```bash
git -c commit.gpgsign=false commit -m "packaging: hash-sync Homebrew/Nix/Scoop to v${VERSION} release artifacts

Co-authored-by: Copilot <copilot@github.com>"
git push origin main
```

The [`v2.0.4` hash-sync commit](https://github.com/SchwartzKamel/azure-openai-cli/commit/1884a8f)
is the canonical template.

---

## 9. Post-publish handoffs

- **Bob Sacamano** -- bump Homebrew tap + Scoop bucket + Nix flake
  consumers. Bob owns the downstream distribution fan-out once the
  manifests are updated.
- **Keith Hernandez** -- refresh the demo-gif + social snippets if the
  release ships user-visible behavior. Skip for pure infra/docs
  releases.
- **Mr. Lippman** -- publish the long-form release narrative to
  `docs/launch/v${VERSION}-release-body.md` (if minor or major) and
  the GitHub Discussion announcement. Keep the GitHub Release notes
  auto-generated; the prose lives in `docs/launch/`.
- **CHANGELOG** -- confirm `[Unreleased]` is reset to a bare header
  and the new section is populated.

---

## 10. Rollback

**You cannot truly rewind a release.** Tags, attestations, and GHCR
digests are permanent public artifacts. "Rollback" means: deprecate
the bad release and mark the previous one as current.

### 10.1 Deprecate the broken GitHub Release

```bash
TAG=v2.0.5

# Mark the Release as a pre-release (hides from "Latest"):
gh release edit "$TAG" --prerelease

# Or delete the Release object (keeps the tag + attestations intact):
gh release delete "$TAG" --cleanup-tag=false
```

### 10.2 Untag / retag GHCR images

```bash
VERSION=2.0.5
PREV=2.0.4

# Retag `latest` to point at the previous known-good image.
docker buildx imagetools create \
  --tag ghcr.io/schwartzkamel/azure-openai-cli/az-ai-v2:latest \
  ghcr.io/schwartzkamel/azure-openai-cli/az-ai-v2:${PREV}

# The ${VERSION} tag stays where it is (digest-pinned, attested);
# we do not delete GHCR tags that have attestations -- that would
# break Sigstore verification for any user who already pulled.
```

### 10.3 Revert packaging manifests

```bash
# On main, revert the hash-sync commit for the broken release.
git revert <hash-sync-sha>
git push origin main
```

Bob re-publishes the Homebrew tap / Scoop bucket with the prior
formula as HEAD. Nix flake consumers pin by commit, so Nix users are
already on the prior version until they `nix flake update`.

### 10.4 Communicate

- CHANGELOG: add a `### Deprecated` entry to `[Unreleased]` naming the
  broken version and the replacement.
- Release notes on the broken tag: edit to prepend a bold
  "**⚠️ DEPRECATED -- use v${PREV} or v${NEXT}**" header with a link to
  the postmortem under `docs/launch/`.
- Open a follow-up postmortem in `docs/launch/v${VERSION}-postmortem.md`
  following the template of existing `v2.0.x-release-attempt-diagnostic.md`
  files.

---

## 11. Common failure modes

| Symptom                                                                 | Root cause                                                                      | Fix                                                                                              |
|-------------------------------------------------------------------------|---------------------------------------------------------------------------------|--------------------------------------------------------------------------------------------------|
| `build-binaries-v2 / osx-arm64` queued > 60 min                         | `macos-14` runner pool backlog                                                  | See `macos-runner-triage.md` §3.1; `gh run rerun $RUN_ID --failed` at 90 min.                    |
| `build-binaries-v2 / win-x64` fails at "Stage artifact"                 | Info-ZIP `zip` not installed on `windows-latest`                                | Pre-fixed in v2.0.1: `stage.sh` uses PowerShell `Compress-Archive` on Windows.                   |
| `docker-publish-v2` fails at `dotnet publish` with `--no-restore` error | Dockerfile.v2 used `--no-restore` without prior `dotnet restore`                | Pre-fixed in v2.0.2: explicit `dotnet restore` step added.                                       |
| `release-v2` creates Release with fewer than 4 binaries                 | A binary leg was skipped (job condition drift) or artifact upload failed        | `gh run rerun $RUN_ID --failed`; if that fails, fix-forward.                                     |
| Attestation verify step fails locally: `no trusted_root was found`      | `gh` version < 2.50                                                             | `gh --version`; upgrade if < 2.50.                                                               |
| Attestation job fails with `Resource not accessible by integration`     | Job missing `id-token: write` / `attestations: write`                           | Check the `permissions:` block on the failing job in `release.yml`.                              |
| `softprops/action-gh-release` 403                                       | `release-v2` missing `contents: write`                                          | Check `permissions:` on `release-v2`.                                                            |
| Docker push 403 to GHCR                                                 | Missing `packages: write` on `docker-publish-v2`, or org-level token scope      | Fix job permissions, or use a dedicated PAT via repo secret.                                     |
| Tag pushed but no workflow run appears                                  | Lightweight tag, or tag pushed before commit                                    | `git push origin main` then `git push origin <tag>`. Re-tag as an annotated tag if you skipped `-a`. |
| CI green on `main`, release workflow red on `ci`                        | `ci.yml` changed after the tagged commit; release runs `ci.yml` at the tag ref  | Fix-forward to the next patch from a newer `main`.                                               |
| `gh workflow run release.yml --ref <old-tag>` → HTTP 422                | `workflow_dispatch` trigger didn't exist in `release.yml` at that tag's commit  | `gh run rerun --failed` instead (§5.2).                                                          |
| `dotnet format --verify-no-changes` fails in CI but passed locally      | Different `dotnet` SDK version; editor auto-formatted differently               | Match the SDK pinned in `global.json`; re-run locally with that version.                         |

---

## 12. After the release

- [ ] `CHANGELOG.md` `[Unreleased]` reset to a bare header.
- [ ] Close or re-label issues resolved by the release.
- [ ] Move any `[Unreleased]` items that *didn't* ship into a future
      version draft section or back to the tracker.
- [ ] If this release cut a platform from the matrix, update
      `docs/runbooks/macos-runner-triage.md` §1 and the "two cycles"
      example in §5.
- [ ] `docs/launch/` -- file the release-body (Lippman) and any demo
      refresh (Keith).

> "We're going to press." -- and we want to go to press with something
> that actually runs.
