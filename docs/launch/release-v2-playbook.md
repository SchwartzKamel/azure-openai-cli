# v2.0.0 Release Playbook — Mr. Lippman

This is the exact ritual for cutting a `v2.x` tag now that
`.github/workflows/release.yml` is tag-pattern gated.

Use alongside:

- `docs/launch/v2-tag-rehearsal-report.md` — proven `stage.sh` mechanics
- `packaging/README.md` §Tag-time ritual — manifest hash-fill cookbook

---

## 0. Preconditions (do not skip)

- [ ] **CI green on the release commit.** `release.yml` calls `ci.yml`
      before any build job runs, but if CI is red the pipeline stalls for
      20+ min before failing — check first.
- [ ] **csproj `<Version>` matches the intended tag.**
      `azureopenai-cli-v2/AzureOpenAI_CLI_V2.csproj` → `<Version>2.0.0</Version>`.
      (Rehearsal report §4.6 flagged this; confirm before tagging.)
- [ ] **`stage.sh` `VERSION=` matches.**
      Currently `VERSION="2.0.0"` in `packaging/tarball/stage.sh`. Bump in
      lock-step with csproj.
- [ ] **CHANGELOG finalized + dated.**
- [ ] **Packaging manifests exist for this version.** For 2.0.0:
      - `packaging/homebrew/Formula/az-ai-v2@2.0.0.rb`
      - `packaging/scoop/versions/az-ai-v2@2.0.0.json`
      - `packaging/nix/flake.nix` (entry in `pinnedHashes` for `"2.0.0"`)
      All three carry `TODO_FILL_AT_RELEASE_TIME` / `fakeHash` placeholders
      — correct at this stage. They get filled post-publish.
- [ ] **No `TODO_FILL` in the unversioned manifests for a DIFFERENT version.**
      The unversioned `az-ai.rb` / `az-ai.json` / flake `latestHashes` point at
      whatever the current "latest" is; do not overwrite them with v2.0.0
      digests unless v2.0.0 IS the new latest.

---

## 1. Cut the tag

```bash
# From main, at the release commit.
git fetch --tags origin
git tag -s v2.0.0 -m "release v2.0.0"
git push origin v2.0.0
```

`-s` requires your GPG/SSH signing key configured via
`git config --global user.signingkey <keyid>`. Tags from CI or unsigned
tags are rejected by branch protection.

**Do not force-push. Do not re-tag.** If v2.0.0 needs a hotfix, cut
`v2.0.1`. Never move a published tag.

---

## 2. What the tag triggers

`.github/workflows/release.yml` fires on `push: tags: ['v*']` and gates by
`startsWith(github.ref, 'refs/tags/v2.')`:

| Job                   | Runs on       | Produces                                           |
|-----------------------|---------------|----------------------------------------------------|
| `ci`                  | reusable      | Gate — all v1 + v2 tests must pass                 |
| `build-binaries-v2`   | 5-way matrix  | 5 artifacts via `stage.sh`                         |
| `docker-publish-v2`   | ubuntu-latest | `ghcr.io/<repo>/az-ai-v2:2.0.0` (+ 2, 2.0, sha)   |
| `release-v2`          | ubuntu-latest | GitHub Release + attached archives + SBOMs        |

`build-binaries-v2` matrix expansion:

| RID              | Runner          | Output                                  |
|------------------|-----------------|-----------------------------------------|
| `linux-x64`      | `ubuntu-latest` | `dist/az-ai-v2-2.0.0-linux-x64.tar.gz`  |
| `linux-musl-x64` | `ubuntu-latest` | `dist/az-ai-v2-2.0.0-linux-musl-x64.tar.gz` |
| `osx-x64`        | `macos-13`      | `dist/az-ai-v2-2.0.0-osx-x64.tar.gz`    |
| `osx-arm64`      | `macos-14`      | `dist/az-ai-v2-2.0.0-osx-arm64.tar.gz`  |
| `win-x64`        | `windows-latest`| `dist/az-ai-v2-2.0.0-win-x64.zip`       |

Each matrix leg:

1. Checks out the tag
2. `dotnet tool restore` + `dotnet-CycloneDX`
3. `bash packaging/tarball/stage.sh <rid>`
4. Generates SBOM into `dist/<basename>.sbom.json`
5. Attests build provenance (Sigstore keyless)
6. Uploads `<archive>` + `<sbom>` under artifact name `<basename>`

`release-v2` downloads all `az-ai-v2-*` artifacts, computes sha256sums
into `digests.txt` (workflow log — also useful as a sanity check), and
publishes a GitHub Release with the body template that names every
v2-shaped file.

---

## 3. Monitor Actions

```bash
gh run watch --exit-status
# or:
gh run list --workflow=release.yml --limit=5
```

Expect ~20–30 min wall-clock (CI is 8–10 min, matrix build is 6–12 min,
Docker push is 3–5 min, release publish is < 1 min).

Red-flag symptoms:

- `build-binaries-v2 / linux-musl-x64` failing at the `stage.sh` step →
  NativeAOT musl cross-link issue. Check that `clang` + `zlib1g-dev`
  installed cleanly in the `Install musl AOT toolchain` step.
- `docker-publish-v2` OOM → buildx needs more RAM; retry or bump the
  runner class.
- Attestation step failing with "no id-token" → verify job's
  `permissions: id-token: write` is present (it is — do not remove).

### Troubleshooting — known failure modes (observed on live runs)

**`build-binaries-v2 / win-x64` fails with `stage.sh: error: 'zip' not on PATH`**
(observed: run 24736776551, v2.0.0 attempt #1). The `windows-latest`
runner's bash does not ship Info-ZIP `zip`. Fix in `stage.sh`: branch
`win-*` RIDs to use `powershell.exe -NoProfile -Command
"Compress-Archive -Path <stage> -DestinationPath <out>"` instead of
`zip -r`. Diagnostic: `docs/launch/v2-release-attempt-1-diagnostic.md`
§"Failure #1".

**`docker-publish-v2` fails at `COPY --from=build /app/az-ai-v2 ...: not found`**
(observed: run 24736776551, v2.0.0 attempt #1 — Debian/glibc SDK base;
**also observed: run 24739184465, v2.0.1 attempt #2 — Alpine/musl SDK
base**). Original hypothesis (glibc→musl cross-link silently dropping
the ELF) was **falsified by Round 2**: the Alpine base produced the
same failure.

Actual root cause (confirmed empirically across two rounds): the
publish step is running as a framework-dependent managed publish, not
as NativeAOT, and produces `/app/az-ai-v2.dll` instead of a native
`/app/az-ai-v2` ELF. Tell-tale in the build log: `dotnet publish` exits
in ~20s (too fast for ILC/link), the log has no "Generating native
code" line, and the last output line is `AzureOpenAI_CLI_V2 -> /app/`
without a trailing filename (as would appear for a native emit).

Most likely culprit is an asset-graph mismatch between
`dotnet restore /p:PublishReadyToRun=true -r linux-musl-x64` and
`dotnet publish --no-restore -p:PublishAot=true`: R2R and AOT resolve
different RID-specific asset sets, and `--no-restore` forbids publish
from pulling the missing AOT assets at publish time, so publish
silently falls back to a managed framework-dependent emit rather than
hard-erroring. Fix candidates for Jerry (v2.0.2 fix-forward, ranked):
(1) drop `--no-restore` from the publish invocation (cheapest, removes
the coupling footgun entirely); (2) replace `/p:PublishReadyToRun=true`
on the restore step with `-p:PublishAot=true` so restore resolves the
AOT asset graph; (3) add `apk add lld` to the build-stage deps in case
ILC is silently skipping codegen on a missing linker warning.

Diagnostic: `docs/launch/v2.0.1-release-attempt-diagnostic.md`
(Round 2), with cross-reference to
`docs/launch/v2-release-attempt-1-diagnostic.md` §"Failure #2"
(Round 1, now-falsified hypothesis documented for history).

**Pre-tag gate added (Puddy-owned):** before any future Dockerfile.v2
change ships behind a tag, run `docker build -f Dockerfile.v2 .`
locally OR in a throwaway workflow_dispatch branch and verify the
resulting image has `/app/az-ai-v2` as an ELF (not a .dll). The
sandbox where this repo's agents live cannot run docker, which is why
both v2.0.0 and v2.0.1 Dockerfile fixes shipped without local smoke.

Note: `docker-publish-v2` can be re-run via `workflow_dispatch` without
a re-tag **once binary legs are green** — but if binary legs also
failed, the whole release is a no-go and the fix ships in the next
patch tag.

**`build-binaries-v2 / osx-x64` stuck in `queued` for 30+ min**
(observed: run 24740882149, v2.0.2 attempt #3; also observed as a
nice-to-know on run 24739184465, v2.0.1 attempt #2). GitHub-hosted
`macos-13` runners are a constrained pool and can backlog 30–120 min
during peak demand. Diagnosis: not a code issue — the other four binary
legs (incl. `osx-arm64` on `macos-14`) completed normally; `osx-x64`
specifically is waiting for runner assignment and has no log because
it hasn't started.

**Do NOT re-tag** to work around this. Options, in order:
(1) wait — typical backlogs clear within 120 min;
(2) cancel the queued `osx-x64` job in the GitHub UI and re-dispatch
    the workflow on the same tag via `gh workflow run release.yml
    --ref v2.0.2` — `release.yml` is idempotent on tag, already-green
    downstreams (`docker-publish-v2`, other binary legs) will re-run
    cleanly;
(3) if macos-13 pool is down for maintenance, shelve the release until
    GitHub status goes green — a queue backlog is not a reason to burn
    a patch tag.

Diagnostic (if encountered again):
`docs/launch/v2.0.2-release-attempt-diagnostic.md`. The v2.0.2 run
demonstrated that `docker-publish-v2` succeeded, GHCR image published
and attested, and all three AOT verification gates fired — proving the
release pipeline code is healthy; only `release-v2` (and therefore
tarball hash-sync) was blocked by the infra backlog.

---

## 4. Post-publish hash sync

Follow `packaging/README.md` §7 (or equivalently `docs/launch/v2-tag-rehearsal-report.md`
§7) exactly. Abbreviated:

```bash
# 1. Download release artifacts into ./dist/
gh release download v2.0.0 --dir dist --pattern 'az-ai-v2-2.0.0-*'

# 2. Compute digests.
cd dist
for f in az-ai-v2-2.0.0-*.tar.gz az-ai-v2-2.0.0-*.zip; do
    [[ -f "$f" ]] || continue
    HEX=$(sha256sum "$f" | awk '{print $1}')
    SRI="sha256-$(printf '%s' "$HEX" | xxd -r -p | base64)"
    printf '%-50s  hex=%s\n  sri=%s\n\n' "$f" "$HEX" "$SRI"
done
cd ..

# 3. Fill hashes BY HAND in:
#    - packaging/homebrew/Formula/az-ai-v2@2.0.0.rb   (3x sha256, hex)
#    - packaging/homebrew/Formula/az-ai.rb            (3x sha256, hex — if promoting to latest)
#    - packaging/scoop/versions/az-ai-v2@2.0.0.json   (1x hash, hex, win-x64 only)
#    - packaging/nix/flake.nix pinnedHashes."2.0.0"   (3x sha256, SRI base64)

# 4. Verify no placeholders remain.
! grep -rn TODO_FILL_AT_RELEASE_TIME packaging/homebrew/Formula/az-ai-v2@2.0.0.rb \
                                     packaging/scoop/versions/az-ai-v2@2.0.0.json
! grep -n fakeHash packaging/nix/flake.nix | grep '"2.0.0"' -B 3

# 5. Commit + PR.
git add packaging/
git commit -m "packaging: v2.0.0 manifest digests (post-publish hash sync)"
git push origin main  # or open a PR if branch-protected
```

**Do NOT fill `linux-musl-x64` into Homebrew/Nix/Scoop** — none of those
manifests have a musl entry. It exists only as a GHCR image source +
direct-download for Alpine users.

---

## 5. Post-publish checklist (hard gate)

- [ ] GitHub Release `v2.0.0` shows 5 archives + 5 SBOMs attached
- [ ] Every archive has a Sigstore attestation (verify one:
      `gh attestation verify az-ai-v2-2.0.0-linux-x64.tar.gz --owner SchwartzKamel`)
- [ ] `ghcr.io/schwartzkamel/azure-openai-cli/az-ai-v2:2.0.0` exists
- [ ] GHCR image digest recorded in release notes (Frank Costanza mirrors to ops doc)
- [ ] `ghcr.io/...:1.9.1` (v1 image) is UNCHANGED — the v2 path publishes
      to a different image name by design
- [ ] Manifest hash-sync PR merged
- [ ] `brew install schwartzkamel/tap/az-ai-v2@2.0.0` works end-to-end
      (requires the tap repo to consume the updated formula — Bob Sacamano)
- [ ] Puddy signs off: fresh-machine install on macOS + Windows + Linux
- [ ] Newman signs off: SBOM + image scan clean
- [ ] Jackie signs off: NOTICE/THIRD_PARTY_NOTICES.md bundled in every artifact
- [ ] Elaine signs off: README version refs + migration doc land on `main`

---

## 6. Rollback

If the tag published but is broken:

1. **Do NOT delete the tag.** Tag hygiene is permanent.
2. Cut `v2.0.1` with the fix. GitHub Releases supports marking v2.0.0
   as "not the latest" after v2.0.1 publishes.
3. For the Docker image: retag `ghcr.io/.../az-ai-v2:latest` back to the
   previous good digest (or to `1.9.1` if v1 is still the supported line).
4. Packaging manifests: revert the hash-sync commit; users pinned to
   `az-ai-v2@2.0.0` will see the broken version, which is why we cut
   versioned pins in the first place.

---

## 7. v1 coexistence — what still works

`v1.*` tags exercise the untouched v1 jobs (`build-binaries`,
`docker-publish`, `release`). Proven shipping path. Do not modify those
jobs without a separate change.

— Mr. Lippman
