# Release Runbook

> How to cut a release of `azure-openai-cli` without waking anyone up.
> Audience: maintainers with push + tag permissions on `origin`.

This runbook codifies the procedure we wish we had before cutting `v1.8.0`
(whose binary matrix failed silently on a non-existent CycloneDX version pin).
If you follow this checklist, the release workflow should turn green on the
first try.

---

## 0. Inputs

| Variable      | Example      | Where it lives                                       |
|---------------|--------------|------------------------------------------------------|
| `VERSION`     | `1.8.1`      | `azureopenai-cli/AzureOpenAI_CLI.csproj` `<Version>` |
| `TAG`         | `v1.8.1`     | Git tag, signed + annotated                          |
| `DATE`        | `2026-04-19` | `CHANGELOG.md` header                                |

Semver rules: **patch** for hot-fixes, **minor** for backwards-compatible
features, **major** for breaking changes to CLI flags, config schema, or the
persisted `~/.azureopenai-cli.json` contract.

---

## 1. Pre-flight checklist

Run these **in order**. Do not proceed past a red box.

- [ ] `git status` — working tree clean, on `main`, up-to-date with `origin`.
- [ ] `dotnet test tests/AzureOpenAI_CLI.Tests/AzureOpenAI_CLI.Tests.csproj`
      — all tests pass locally (current baseline: 541 passing).
- [ ] `make publish-aot` builds clean on Linux x64 (smoke-test the AOT path
      documented in ADR-001).
- [ ] `CHANGELOG.md` has a section for the new version with today's date
      and a non-empty set of entries. `[Unreleased]` is reset to a bare header.
- [ ] `azureopenai-cli/AzureOpenAI_CLI.csproj` `<Version>` matches the tag
      you are about to push (without the `v` prefix).
- [ ] `NOTICE` reflects the current dependency set (update if you added or
      removed a direct NuGet reference).
- [ ] `.config/dotnet-tools.json` is committed and `dotnet tool restore`
      succeeds — this is what the release workflow will use to build the SBOM.
- [ ] `gh run list --workflow ci.yml --branch main --limit 1` is green for
      the commit you're about to tag.

---

## 2. Cut the tag

```bash
VERSION=1.8.1
TAG="v${VERSION}"

git pull --ff-only origin main
git tag -a "$TAG" -m "$TAG - <one-line summary>"
git push origin "$TAG"
```

Tags **must** be annotated (`-a`) so they carry a message and author; signed
(`-s`) is preferred if you have a key configured. Lightweight tags break
`release.yml`'s semver metadata step and must never be pushed.

---

## 3. Watch the release workflow

```bash
gh run list --workflow release.yml --limit 3
gh run watch <run-id>
```

The workflow has four job groups, all of which must succeed:

1. `ci` — the full test suite (reused from `ci.yml`).
2. `build-binaries` — 5-way matrix (linux-x64, linux-musl-x64, win-x64,
   osx-x64, osx-arm64). Each produces a self-contained single-file binary,
   a CycloneDX SBOM, and an `attest-build-provenance` attestation.
3. `docker-publish` — builds and pushes `ghcr.io/…:{tag}` with image
   provenance attested and pushed to the registry.
4. `release` — creates the GitHub Release with all binaries, SBOMs, and
   auto-generated release notes.

---

## 4. Post-release verification

```bash
TAG=v1.8.1

# Release object exists and has all 5 platforms + 5 SBOMs attached
gh release view "$TAG"

# Binary attestations — each should return a verified SLSA provenance
gh attestation verify \
  --repo SchwartzKamel/azure-openai-cli \
  azure-openai-cli-linux-x64.tar.gz

# Container image + attestation
docker pull "ghcr.io/schwartzkamel/azure-openai-cli:${TAG#v}"
gh attestation verify --repo SchwartzKamel/azure-openai-cli \
  oci://ghcr.io/schwartzkamel/azure-openai-cli:${TAG#v}
```

Announcement checklist:

- [ ] `docs/announce/<tag>-launch.md` is published (or skipped for patch
      releases — editorial judgment).
- [ ] `IMPLEMENTATION_PLAN.md`'s "What's Shipped" section mentions the
      new version.

---

## 5. Rollback & hotfix

**Never rewrite a tag that has already been pushed.** Attestations and
container digests are anchored to it; rewriting invalidates downstream trust.

If a release is broken:

1. **Container-only rollback** — retag the previous known-good image:
   ```bash
   docker buildx imagetools create \
     --tag ghcr.io/schwartzkamel/azure-openai-cli:latest \
     ghcr.io/schwartzkamel/azure-openai-cli:1.8.0
   ```
2. **Hotfix patch** — bump `<Version>` to the next patch, add a `Fixed`
   section to `CHANGELOG.md` that explicitly references the failure in the
   broken release, commit, tag, push. See v1.8.0 → v1.8.1 as the canonical
   worked example.
3. **Delete the GitHub Release** (optional, if it was partially created)
   with `gh release delete <tag> --cleanup-tag=false` — this removes the
   Release object but leaves the git tag and any attestations intact.

---

## 6. Common failure modes

| Symptom | Root cause | Fix |
|--------|------------|-----|
| `build-binaries` all fail at "Generate SBOM" with *"Version X.Y.Z of package cyclonedx is not found in NuGet feeds"* | Hard-coded `dotnet tool install --global CycloneDX --version X.Y.Z` pinned to a version that doesn't exist or was yanked. This is **exactly what happened on v1.8.0** (pinned `4.0.2`; the 4.x line was never released — CycloneDX went 3.x → 5.x → 6.x). | The release workflow now uses `.config/dotnet-tools.json` + `dotnet tool restore`. Bump the manifest via Dependabot or `dotnet tool update CycloneDX` and commit the manifest change. |
| `dotnet CycloneDX … --json` fails with *"unknown option"* | CycloneDX 6.x replaced `--json` with `--output-format Json`. | Use `--output-format Json` (already in `release.yml`). |
| Attestation step fails with `Resource not accessible by integration` | Job is missing `id-token: write` and/or `attestations: write` permission. | Confirm per-job `permissions:` block matches `release.yml`'s current shape. |
| `softprops/action-gh-release` 403 | Release job missing `contents: write`. | Same — check `permissions:` on the `release` job. |
| Docker push 403 to GHCR | Missing `packages: write` on `docker-publish`, or `GITHUB_TOKEN` scope restricted at org level. | Fix job permissions; if org-level restriction, use a dedicated PAT via repo secret. |
| Tag pushed but workflow never triggers | Pushed a lightweight tag, or pushed tag before commit was on `origin/main`. | `git push origin main` first, then `git tag -a` and `git push origin <tag>`. |
| CI green, release workflow red on `ci` job | `ci.yml` was changed after the tagged commit. | The release workflow calls `ci.yml` at the tagged ref — re-tag a new patch from a newer commit; do not force-push the old tag. |

---

## 7. After the release

- Close or re-label any issues resolved by the release.
- Move `[Unreleased]` items that didn't make it into the cut back under their
  intended future version header in `CHANGELOG.md`.
- Open a fresh `[Unreleased]` skeleton so v-next work has somewhere to land.

> "We're going to press." — and we want to go to press with something that
> actually runs.
