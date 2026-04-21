# Packaging — distribution channels for `az-ai-v2`

> "You need it on Homebrew? I know a guy. Scoop bucket? Done. Nix flake? Already wired."
> — Bob Sacamano

This directory holds the third-party packaging manifests that pin a specific
release of the Azure OpenAI CLI (published as `az-ai-v2`) to each ecosystem's
conventions. Manifests are versioned in-repo; the release pipeline is
expected to update SHA256 digests and version strings as part of each tag.
Hand-edited drift will be caught at release review.

Current pinned release: **v2.0.0** (GitHub Releases artifacts).

The upstream binary name inside each archive is `AzureOpenAI_CLI`
(`AzureOpenAI_CLI.exe` on Windows). Every manifest here renames it to
`az-ai-v2` on install so users get a single, consistent command across
platforms.

---

## Channels at a glance

| Channel   | File                                   | Platforms                               | Status                  |
|-----------|----------------------------------------|-----------------------------------------|-------------------------|
| Homebrew  | `homebrew/Formula/az-ai.rb`            | macOS arm64/x64, Linux x64              | Tap not yet created     |
| Scoop     | `scoop/az-ai.json`                     | Windows x64                             | Bucket not yet created  |
| Nix       | `nix/flake.nix`                        | Linux x64, macOS x64, macOS arm64       | Flake ready             |
| Tarball   | `tarball/stage.sh`                     | Any .NET RID (linux/osx/win)            | Used by release CI      |

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

## Release checklist (for Lippman)

On every tagged release:

- [ ] Run `packaging/tarball/stage.sh <rid>` for each target RID to build
      the tarballs (binary + LICENSE + NOTICE + THIRD_PARTY_NOTICES.md +
      README.md).
- [ ] Update `version` in all three manifests.
- [ ] Update the three SHA256 lines in `homebrew/Formula/az-ai.rb`
      (replace `TODO_FILL_AT_RELEASE_TIME`).
- [ ] Update the `hash` in `scoop/az-ai.json` (or let `scoop checkver -u`
      do it in the bucket repo).
- [ ] Update the three SHA256s in `nix/flake.nix` `sources`
      (replace `lib.fakeHash` — `nix build` will print the real SRI hash
      on mismatch).
- [ ] Confirm `NOTICE` + `THIRD_PARTY_NOTICES.md` are present in every
      staged tarball before upload (Puddy gate).
- [ ] Verify fresh-machine install on each platform before advertising in
      the top-level README (per Puddy).
- [ ] If linux-arm64 artifacts start shipping, add them to the Homebrew
      formula and Nix flake.
