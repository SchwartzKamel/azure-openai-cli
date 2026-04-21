# Packaging — distribution channels for `az-ai-v2`

> "You need it on Homebrew? I know a guy. Scoop bucket? Done. Nix flake? Already wired."
> — Bob Sacamano

This directory holds the third-party packaging manifests that pin a specific
release of the Azure OpenAI CLI (published as `az-ai-v2`) to each ecosystem's
conventions. Manifests are versioned in-repo; the release pipeline is
expected to update SHA256 digests and version strings as part of each tag.
Hand-edited drift will be caught at release review.

Current pinned release: **v2.0.1** (GitHub Releases artifacts).
Versioned-pin scaffolding (`@2.0.0` syntax for Homebrew / Scoop / Nix) lands
at the v2.0.1 boundary per [`docs/v2-cutover-decision.md` §G6](../docs/v2-cutover-decision.md).

The upstream binary name inside each archive is `AzureOpenAI_CLI`
(`AzureOpenAI_CLI.exe` on Windows). Every manifest here renames it to
`az-ai-v2` on install so users get a single, consistent command across
platforms.

---

## Channels at a glance

| Channel   | File                                         | Platforms                               | Status                  |
|-----------|----------------------------------------------|-----------------------------------------|-------------------------|
| Homebrew  | `homebrew/Formula/az-ai.rb` (+ `@<ver>.rb`)  | macOS arm64/x64, Linux x64              | Tap not yet created     |
| Scoop     | `scoop/az-ai.json` (+ `versions/*.json`)     | Windows x64                             | Bucket not yet created  |
| Nix       | `nix/flake.nix` (tracking + pinned attrs)    | Linux x64, macOS x64, macOS arm64       | Flake ready             |
| Tarball   | `tarball/stage.sh`                           | Any .NET RID (linux/osx/win)            | Used by release CI      |

Linux arm64 artifacts are not yet published by the release pipeline; when
they are, add them to the Homebrew formula (`on_linux do on_arm do ...`)
and to `sources` in `nix/flake.nix`.

> **Dual-binary transition (v2.0.0):** the v2 binary ships as `az-ai-v2`
> (`az-ai-v2.exe` on Windows) alongside the v1 `az-ai` binary. All three
> manifests install as `az-ai-v2` during this transition. Post-cutover, a
> follow-up manifest update will rename back to plain `az-ai`.

> **NOTICE bundling:** every manifest now stages `LICENSE`, `NOTICE`, and
> `THIRD_PARTY_NOTICES.md` alongside the binary so Mr. Lippman's
> release-notes claim — *"all distributed artifacts include NOTICE"* —
> holds across Homebrew, Scoop, Nix, and the raw tarballs.

---

## Homebrew

### Install (today, pre-tap)

Until the tap repo exists, install directly from the formula file:

```sh
brew install --formula ./packaging/homebrew/Formula/az-ai.rb
```

### Install (once the tap is live)

```sh
brew tap SchwartzKamel/tap
brew install az-ai-v2
```

### Publish (owner action — Lippman TBD)

1. Create the tap repository: `SchwartzKamel/homebrew-tap`
   (Homebrew resolves `SchwartzKamel/tap` → `SchwartzKamel/homebrew-tap`.)
2. Copy `packaging/homebrew/Formula/az-ai.rb` into the tap's `Formula/`
   directory on each release.
3. The release workflow should open a PR against the tap repo bumping
   `version`, the three `url` lines, and the three `sha256` lines.
4. Verify with `brew audit --new --strict --online az-ai` before merging.

### Verify locally

```sh
brew audit --strict ./packaging/homebrew/Formula/az-ai.rb
brew install --build-from-source --formula ./packaging/homebrew/Formula/az-ai.rb
az-ai-v2 --version --short   # expect: 2.0.0
```

---

## Scoop

### Install (today, pre-bucket)

```powershell
scoop install ./packaging/scoop/az-ai.json
```

### Install (once the bucket is live)

```powershell
scoop bucket add schwartzkamel https://github.com/SchwartzKamel/scoop-bucket
scoop install az-ai-v2
```

### Publish (owner action — Lippman TBD)

1. Create the bucket repository: `SchwartzKamel/scoop-bucket`.
2. Copy `packaging/scoop/az-ai.json` to the bucket's `bucket/az-ai.json`.
3. Run `scoop checkver az-ai -u` in the bucket checkout after each release
   to auto-bump `version` / `hash` from the `checkver` + `autoupdate`
   blocks already baked into the manifest.
4. Submit to `scoopinstaller/extras` once the project is stable (Sacamano
   will handle the upstream PR).

### Verify locally

```powershell
scoop install ./packaging/scoop/az-ai.json
az-ai-v2 --version --short   # expect: 2.0.0
```

---

## Nix

### Install (today, from this repo)

```sh
# Run directly
nix run github:SchwartzKamel/azure-openai-cli?dir=packaging/nix -- --version --short

# Or add to a flake:
#   inputs.az-ai.url = "github:SchwartzKamel/azure-openai-cli?dir=packaging/nix";
#   environment.systemPackages = [ inputs.az-ai.packages.${pkgs.system}.default ];
```

### Publish (owner action — future)

- Short term: the flake lives in-repo under `packaging/nix/`. No extra
  action required; consumers point their inputs at this subdirectory.
- Medium term: mirror to a dedicated `SchwartzKamel/nix-az-ai` flake repo
  if it simplifies versioning.
- Long term: upstream to `nixpkgs` once the project earns it (Sacamano
  handles the PR).

### Verify locally

```sh
cd packaging/nix
nix flake check
nix build .#default
./result/bin/az-ai-v2 --version --short   # expect: 2.0.0
```

---

## Versioned-pin install (`@<version>` syntax) — G6 scaffolding

Shipped at the v2.0.1 boundary. Users can now pin a specific historical
release natively through each channel's version-suffix convention, without
needing to install from a raw URL.

### Homebrew — pinnable sibling formulae

Every tagged release gets a frozen sibling formula alongside the tracking
`az-ai.rb`:

```
packaging/homebrew/Formula/
├── az-ai.rb                  # tracks latest (currently v2.0.1)
└── az-ai-v2@2.0.0.rb         # frozen pin — keg_only, class AzAiV2AT200
```

Install:

```sh
brew install az-ai-v2          # latest
brew install az-ai-v2@2.0.0    # pinned
```

The versioned class name follows Homebrew's convention: `AzAiV2AT` + the
version with dots stripped (`2.0.0` → `AT200`). `keg_only :versioned_formula`
prevents the pinned binary from shadowing `az-ai-v2` on `PATH` — use
`brew link --force` or call `$(brew --prefix az-ai-v2@2.0.0)/bin/az-ai-v2`
directly.

### Scoop — `versions/` subdirectory

Scoop resolves `<bucket>/<pkg>@<version>` by looking for a manifest under
`bucket/versions/<pkg>@<version>.json`. The layout matches the upstream
[`scoopinstaller/versions`](https://github.com/ScoopInstaller/Versions)
bucket convention:

```
packaging/scoop/
├── az-ai.json                       # tracks latest
└── versions/
    └── az-ai-v2@2.0.0.json          # frozen pin
```

Install:

```powershell
scoop install schwartzkamel/az-ai-v2          # latest
scoop install schwartzkamel/az-ai-v2@2.0.0    # pinned
```

### Nix — frozen derivation attributes

The flake exposes a `packages.az-ai-v2_<version-underscored>` attribute per
frozen release, in addition to `packages.default` which tracks the latest:

```nix
{
  inputs.az-ai.url = "github:SchwartzKamel/azure-openai-cli?dir=packaging/nix";

  # Latest:
  environment.systemPackages = [ inputs.az-ai.packages.${pkgs.system}.default ];

  # Pinned to v2.0.0:
  environment.systemPackages = [
    inputs.az-ai.packages.${pkgs.system}."az-ai-v2_2_0_0"
  ];
}
```

New pinned attributes are generated at tag time by adding an entry to
`pinnedHashes` in `nix/flake.nix`. Old entries must never be mutated — if a
retag is required, add a new key (e.g. `"2.0.0-1"`) rather than changing an
existing digest.

---

## Tag-time ritual — G6 gate

This section closes the **G6** gate called out in
[`docs/v2-cutover-decision.md`](../docs/v2-cutover-decision.md) and rehearsed
in [`docs/launch/v2-tag-rehearsal-report.md`](../docs/launch/v2-tag-rehearsal-report.md).
Run the ritual on the tagger's workstation after pushing the `v<version>`
tag and waiting for `.github/workflows/release.yml` to publish the tarballs.

### Step 1 — fetch digests from the release artifacts

```sh
VERSION=2.0.1
BASE="https://github.com/SchwartzKamel/azure-openai-cli/releases/download/v${VERSION}"

for rid in linux-x64 osx-x64 osx-arm64; do
    curl -sL "${BASE}/az-ai-v2-${VERSION}-${rid}.tar.gz" \
        | sha256sum | awk -v rid="$rid" '{printf "%s  %s\n", rid, $1}'
done

curl -sL "${BASE}/az-ai-v2-${VERSION}-win-x64.zip" \
    | sha256sum | awk '{printf "win-x64   %s\n", $1}'
```

Keep the digest table open — the next three steps paste each value into
the matching manifest slot.

### Step 2a — Homebrew

Either in-place edit (current tap-less mode):

```sh
sed -i.bak \
    -e "0,/TODO_FILL_AT_RELEASE_TIME/s//${OSX_ARM64_SHA}/" \
    -e "0,/TODO_FILL_AT_RELEASE_TIME/s//${OSX_X64_SHA}/" \
    -e "0,/TODO_FILL_AT_RELEASE_TIME/s//${LINUX_X64_SHA}/" \
    packaging/homebrew/Formula/az-ai.rb
! grep -q TODO_FILL_AT_RELEASE_TIME packaging/homebrew/Formula/az-ai.rb
```

Or (once the tap exists, preferred):

```sh
brew bump-formula-pr --tap=SchwartzKamel/tap \
    --url="${BASE}/az-ai-v2-${VERSION}-linux-x64.tar.gz" \
    --sha256="${LINUX_X64_SHA}" \
    az-ai-v2
```

### Step 2b — Scoop

```sh
sed -i "s/TODO_FILL_AT_RELEASE_TIME/${WIN_X64_SHA}/" packaging/scoop/az-ai.json
jq . packaging/scoop/az-ai.json > /dev/null
! grep -q TODO_FILL_AT_RELEASE_TIME packaging/scoop/az-ai.json
```

Once the bucket repo is live, `scoop checkver az-ai -u` run inside the
bucket checkout auto-bumps from the `checkver` + `autoupdate` blocks.

### Step 2c — Nix

```sh
# Convert each sha256sum hex digest to SRI form Nix expects:
for hex in $LINUX_X64_SHA $OSX_X64_SHA $OSX_ARM64_SHA; do
    nix hash to-sri --type sha256 "$hex"
done

# Or prefetch directly (emits SRI):
nix-prefetch-url --type sha256 "${BASE}/az-ai-v2-${VERSION}-linux-x64.tar.gz"

${EDITOR:-vi} packaging/nix/flake.nix     # paste into latestHashes
(cd packaging/nix && nix build .#default --no-link) || echo "hash mismatch — recheck"
! grep -q fakeHash packaging/nix/flake.nix
```

### Step 3 — add the new version to pin scaffolding

Before the *next* release ships, copy the just-filled hashes into the
pinnable sibling so the version remains installable forever:

- Homebrew: `cp az-ai.rb az-ai-v2@${VERSION}.rb`, rename the class
  (`AzAiV2` → `AzAiV2AT${VERSION//./}`), strip `checkver`-style drift,
  and add `keg_only :versioned_formula`.
- Scoop: `cp scoop/az-ai.json scoop/versions/az-ai-v2@${VERSION}.json`,
  strip `checkver` / `autoupdate`.
- Nix: add a `"${VERSION}" = { linux-x64 = "sha256-..."; ... };` entry to
  `pinnedHashes` in `flake.nix`.

### Step 4 — commit and gate close

```sh
git add packaging/
git commit -m "release(packaging): v${VERSION} — fill digests + pin scaffolding"
git push origin main
```

Puddy verifies fresh-machine install on each platform before G6 is marked
**done** in Lippman's release checklist.

### CI hook (future)

There is no `.github/workflows/packaging-release.yml` yet. Current state:
`.github/workflows/release.yml` builds and publishes the tarballs but does
**not** auto-fill packaging manifests. Follow-up work (tracked separately):
add a `packaging-bump` job that runs post-release, computes the digests,
and opens a PR against `main` with the manifests updated.

---

## Release checklist (for Lippman)

On every tagged release:

- [ ] Run `packaging/tarball/stage.sh <rid>` for each target RID to build
      the tarballs (binary + LICENSE + NOTICE + THIRD_PARTY_NOTICES.md +
      README.md).
- [ ] Update `version` in all three tracking manifests.
- [ ] Fill SHA256s via the Step-2a/2b/2c commands above (replaces all
      `TODO_FILL_AT_RELEASE_TIME` / `lib.fakeHash` placeholders).
- [ ] Add a new versioned-pin sibling for the previous release per Step 3.
- [ ] Confirm `NOTICE` + `THIRD_PARTY_NOTICES.md` are present in every
      staged tarball and in each manifest's install phase (Puddy gate).
- [ ] Verify fresh-machine install on each platform before advertising in
      the top-level README (per Puddy).
- [ ] If linux-arm64 artifacts start shipping, add them to the Homebrew
      formula and Nix flake.
- [ ] G6 marked **done** in `docs/v2-cutover-decision.md` go-list.
