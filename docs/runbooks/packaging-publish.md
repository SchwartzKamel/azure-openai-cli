# Packaging publish runbook -- Homebrew tap + Scoop bucket

> "I know a guy." -- Bob Sacamano

Audience: whoever holds push access to `SchwartzKamel/*`. This runbook
takes the canonical, in-repo packaging manifests and mirrors them into
the external tap + bucket repos so end-users can `brew install` and
`scoop install` without cloning this repo first.

**Scope.** This runbook is version-agnostic. Currently the formulae
and manifests in `packaging/` are pinned to **v2.0.4**; **v2.0.5** is
queued as a fix-forward (rolling the `stage.sh` VERSION drift called
out in C-1 of `docs/audits/docs-audit-2026-04-22-lippman.md`). The
steps below apply to whichever version Lippman has most recently
signed off -- do not hard-code a version into muscle memory.

**Out of scope.** Creating formulae/manifests, bumping versions,
filling SHA256s. Those happen as part of the tag-time ritual in
[`packaging/README.md`](../../packaging/README.md). This runbook
starts from the moment the in-repo manifests are green.

---

## 0. Reality check -- execution vs documentation

> **This runbook documents the process. Actual publishing requires
> push access to external `SchwartzKamel/homebrew-az-ai` and
> `SchwartzKamel/scoop-az-ai` repos. If those repos do not exist
> yet, they must be created manually (GitHub UI or `gh repo create`)
> before this runbook runs.**

Until those repos exist, the install instructions in the top-level
`README.md` continue to point at the in-repo manifests (install from
a local path). Do **not** advertise `brew tap …` / `scoop bucket
add …` in user-facing docs before §6 is green.

---

## 1. Prerequisites

### External repositories (must exist before step 2)

| Repo                                   | Purpose              | Short name for user |
|----------------------------------------|----------------------|---------------------|
| `SchwartzKamel/homebrew-az-ai`         | Homebrew tap         | `schwartzkamel/az-ai` |
| `SchwartzKamel/scoop-az-ai`            | Scoop bucket         | `schwartzkamel`     |

Homebrew's naming convention requires the `homebrew-` prefix --
`brew tap schwartzkamel/az-ai` resolves to
`github.com/SchwartzKamel/homebrew-az-ai` automatically.

Create them (one-time, from the maintainer's workstation):

```sh
gh repo create SchwartzKamel/homebrew-az-ai --public \
    --description "Homebrew tap for az-ai (Azure OpenAI CLI)"
gh repo create SchwartzKamel/scoop-az-ai --public \
    --description "Scoop bucket for az-ai (Azure OpenAI CLI)"
```

### Access

- Push access to both repos above (maintainer only).
- `gh` CLI authenticated as a user with push rights (`gh auth status`).
- Local clone of `SchwartzKamel/azure-openai-cli` at the tag currently
  being published (e.g. `git checkout v2.0.4`).

### Secrets/tokens

**None required for manual publishing.** Push uses the maintainer's
own credentials via `gh`/`git`. If this is ever automated from CI
(future work), a fine-grained PAT scoped to **contents:write** on
just `homebrew-az-ai` + `scoop-az-ai` should be stored as
`PACKAGING_PUBLISH_TOKEN` -- never reuse `GITHUB_TOKEN` from
`release.yml`, which is scoped to this repo only.

### Tools

- `brew` (macOS or Linuxbrew) for `§3` verification
- `scoop` (Windows or a Windows VM/CI runner) for `§4` verification
- `sha256sum` + `curl` + `jq` for `§5` hash sync verification

---

## 2. Homebrew tap publishing

### 2.1 Create the tap repo (skip if it already exists)

See §1. Initialize with an empty `Formula/` directory:

```sh
gh repo clone SchwartzKamel/homebrew-az-ai /tmp/homebrew-az-ai
cd /tmp/homebrew-az-ai
mkdir -p Formula
printf "# homebrew-az-ai\n\nHomebrew tap for [az-ai](https://github.com/SchwartzKamel/azure-openai-cli).\n\n\`\`\`sh\nbrew tap schwartzkamel/az-ai\nbrew install schwartzkamel/az-ai/az-ai\n\`\`\`\n" > README.md
git add . && git -c commit.gpgsign=false commit -m "chore: initialize tap"
git push origin main
```

### 2.2 Mirror the formulae

From an up-to-date `azure-openai-cli` checkout at the release tag:

```sh
cd /path/to/azure-openai-cli
git checkout v2.0.4   # or whatever tag is currently being published

rsync -av --delete packaging/homebrew/Formula/ /tmp/homebrew-az-ai/Formula/

cd /tmp/homebrew-az-ai
git add Formula/
git -c commit.gpgsign=false commit -m "formula: mirror az-ai @ v2.0.4 from azure-openai-cli"
git push origin main
```

`--delete` is intentional: the tap repo is a pure mirror. If a pinned
formula disappears from `packaging/homebrew/Formula/`, it must
disappear from the tap. **Exception:** never delete a pinned
`@<version>.rb` unless Lippman + Jackie sign off -- users may have
Brewfiles that pin that version.

### 2.3 Test before announcing

On a fresh Mac (or Linux box, or clean CI runner):

```sh
brew untap schwartzkamel/az-ai 2>/dev/null || true
brew tap schwartzkamel/az-ai
brew audit --strict --online schwartzkamel/az-ai/az-ai
brew install schwartzkamel/az-ai/az-ai
az-ai --version --short   # expect 2.0.2 at v2.0.4 (filename drift), 2.0.5 at v2.0.5
```

Also verify the pinned alias installs:

```sh
brew install schwartzkamel/az-ai/az-ai@2.0.4
```

### 2.4 Intel Mac (Rosetta 2) fallback

v2.0.4 dropped osx-x64. Document for Intel-Mac users:

```sh
softwareupdate --install-rosetta --agree-to-license
arch -arm64e brew tap schwartzkamel/az-ai
arch -arm64e brew install schwartzkamel/az-ai/az-ai
```

The `osx-arm64` bottle runs under Rosetta 2 on Intel. This is a
stop-gap, not a supported configuration -- if Intel users report
issues, route to `docs/runbooks/macos-runner-triage.md` §5.

---

## 3. Scoop bucket publishing

### 3.1 Create the bucket repo (skip if it already exists)

```sh
gh repo clone SchwartzKamel/scoop-az-ai /tmp/scoop-az-ai
cd /tmp/scoop-az-ai
mkdir -p bucket bucket/versions
printf "# scoop-az-ai\n\nScoop bucket for [az-ai](https://github.com/SchwartzKamel/azure-openai-cli).\n\n\`\`\`powershell\nscoop bucket add schwartzkamel https://github.com/SchwartzKamel/scoop-az-ai\nscoop install schwartzkamel/az-ai\n\`\`\`\n" > README.md
git add . && git -c commit.gpgsign=false commit -m "chore: initialize bucket"
git push origin main
```

Scoop looks for manifests under `bucket/` by default when the bucket
has a `bucket/` subdirectory. Keep that convention so we inherit
Scoop's default resolution rules.

### 3.2 Mirror the manifests

```sh
cd /path/to/azure-openai-cli
git checkout v2.0.4

cp packaging/scoop/az-ai.json           /tmp/scoop-az-ai/bucket/az-ai.json
rsync -av --delete packaging/scoop/versions/ /tmp/scoop-az-ai/bucket/versions/

cd /tmp/scoop-az-ai
git add bucket/
git -c commit.gpgsign=false commit -m "manifest: mirror az-ai @ v2.0.4 from azure-openai-cli"
git push origin main
```

**Rename note.** The in-repo file is `packaging/scoop/az-ai.json`
(historical name), but Scoop resolves the **filename** as the package
name -- so it lands as `bucket/az-ai.json` in the bucket. The
`versions/az-ai@<version>.json` files already match their resolved
names and are copied as-is.

### 3.3 Versions directory convention

The `bucket/versions/` layout matches the upstream
[`scoopinstaller/versions`](https://github.com/ScoopInstaller/Versions)
bucket so `scoop install schwartzkamel/az-ai@2.0.4` resolves
natively. Never mutate a pinned manifest -- if a digest is wrong,
publish a follow-up pin (`@2.0.4-1.json`) rather than editing in
place.

### 3.4 Test before announcing

On a Windows host (or sandbox VM):

```powershell
scoop bucket rm schwartzkamel -ErrorAction SilentlyContinue
scoop bucket add schwartzkamel https://github.com/SchwartzKamel/scoop-az-ai
scoop install schwartzkamel/az-ai
az-ai --version --short   # expect 2.0.2 at v2.0.4 (filename drift)

scoop uninstall az-ai
scoop install schwartzkamel/az-ai@2.0.4
```

---

## 4. Hash sync verification

The formula/manifest digests in `packaging/` **must** match the
GitHub Release's `digests.txt` artifact, produced by the
`Compute digests` step in
[`.github/workflows/release.yml`](../../.github/workflows/release.yml)
(search for `sha256sum az-ai-*.tar.gz az-ai-*.zip`).

### 4.1 Fetch the release digests

```sh
VERSION=2.0.4   # replace with the version being published
BASE="https://github.com/SchwartzKamel/azure-openai-cli/releases/download/v${VERSION}"

# Compute digests live from the published artifacts:
for asset in \
    az-ai-2.0.2-linux-x64.tar.gz \
    az-ai-2.0.2-osx-arm64.tar.gz \
    az-ai-2.0.2-win-x64.zip \
    ; do
    printf "%s  " "$asset"
    curl -sL "${BASE}/${asset}" | sha256sum | awk '{print $1}'
done
```

> ⚠️ **Filename drift:** at v2.0.4 the tarballs are named `…-2.0.2-…`
> because `packaging/tarball/stage.sh` VERSION was not rolled. v2.0.5
> rolls this back into lock-step. For any tag **other than v2.0.4**,
> substitute `${VERSION}` for the literal in the filenames above.

### 4.2 Compare against the formula

```sh
grep -E '^\s*sha256 ' packaging/homebrew/Formula/az-ai.rb
grep -E '^\s*sha256 ' packaging/homebrew/Formula/az-ai@${VERSION}.rb
```

Every `sha256 "…"` line must appear in the output of §4.1. If any
digest disagrees, **the release is broken, not the formula** -- stop
the publish, file a fix-forward tag, and re-run §2.

### 4.3 Compare against the Scoop manifest

```sh
jq -r '.architecture."64bit".hash' packaging/scoop/az-ai.json
jq -r '.architecture."64bit".hash' packaging/scoop/versions/az-ai@${VERSION}.json
```

Same rule as §4.2. `autoupdate.hash.url` pointing at `$url.sbom.json`
means `scoop checkver -u` will re-derive the hash from CycloneDX at
the next bump -- but the currently-pinned `hash` must still match
§4.1 output today.

### 4.4 Cross-check with SLSA provenance (optional but recommended)

```sh
gh attestation verify az-ai-${VERSION}-linux-x64.tar.gz \
    --owner SchwartzKamel
```

If attestation fails, the release artifact was tampered with or the
Sigstore bundle is missing -- do **not** publish the tap/bucket
mirror. Route to Newman.

---

## 5. Announce -- what to update after tap/bucket go live

Only after §2.3 and §3.4 are both green on a fresh machine (Puddy
gate):

- [ ] **`README.md`** top-level install section -- replace "install from
      local manifest" with the `brew tap` / `scoop bucket add` lines.
- [ ] **`packaging/README.md`** -- flip the status column for Homebrew
      and Scoop from "not yet created" to "live".
- [ ] **`packaging/homebrew/README.md`** -- drop the "before the tap is
      live" fallback block.
- [ ] **`packaging/scoop/README.md`** -- drop the "before the bucket is
      live" fallback block.
- [ ] **`docs/runbooks/release-runbook.md`** -- add a post-release step
      pointing at this runbook.
- [ ] **`CHANGELOG.md`** -- under the current in-flight version, add an
      entry under *Packaging*: "Homebrew tap + Scoop bucket now live".
- [ ] **Release notes** for the next tag -- Mr. Lippman adds a
      "Package manager install" callout pointing at the tap/bucket.
- [ ] Close the corresponding tracking ticket (`v202-tap-bucket-publish`
      or its successor).

Do **not** announce externally (blog, social, OSS index PRs) until
Jackie has cleared the trademark / attribution story for the tap
name. Sacamano will chase that separately.

---

## 6. Troubleshooting

| Symptom                                              | Likely cause                                                      | Fix |
|------------------------------------------------------|-------------------------------------------------------------------|-----|
| `brew install` reports `SHA256 mismatch`             | Formula digest disagrees with release artifact                    | §4.2 -- if mismatch is real, fix-forward a new tag |
| `scoop install` reports `Hash check failed`          | Manifest `hash` disagrees with release zip                        | §4.3 |
| `brew audit` warns about `keg_only`                  | Expected for pinned `@<version>` formulae                         | Ignore -- that's the whole point |
| `scoop checkver -u` mutates a pinned `versions/*`    | Ran `checkver` inside `versions/`                                 | Revert; only run against `bucket/az-ai.json` |
| Intel-Mac user reports "bad CPU type in executable"  | Rosetta 2 not installed                                           | §2.4 |
| Tap tracking formula reports stale version           | `packaging/homebrew/Formula/az-ai.rb` not bumped at tag time      | Run the tag-time ritual in `packaging/README.md` |

---

## 7. Status

- `SchwartzKamel/homebrew-az-ai` -- **not yet created** (blocked on
  maintainer action per §0)
- `SchwartzKamel/scoop-az-ai` -- **not yet created** (blocked on
  maintainer action per §0)

Once both repos exist and §2-§5 have been exercised once end-to-end
on a live release, update this section to "live" and remove §0's
reality-check banner.
