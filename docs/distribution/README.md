# Distribution channels

> "Three more pipes off the same release tarball -- mac, windows, linux. I know a guy in each store." -- Bob Sacamano

This directory documents the **packaging approaches** for `azure-openai-cli`
beyond the existing Docker image and the `make publish-aot` raw binary
download. Three channels are drafted here; **none are published yet**.

The matching draft manifests live under [`/packaging`](../../packaging/):

- [`packaging/homebrew/azure-openai-cli.rb`](../../packaging/homebrew/azure-openai-cli.rb)
- [`packaging/scoop/azure-openai-cli.json`](../../packaging/scoop/azure-openai-cli.json)
- [`packaging/nix/azure-openai-cli/flake.nix`](../../packaging/nix/azure-openai-cli/flake.nix)

For each channel see the dedicated doc:

- [Homebrew](homebrew.md) -- macOS (and Linuxbrew); custom tap first, core later
- [Scoop](scoop.md) -- Windows; custom bucket first, `extras` later
- [Nix](nix.md) -- linux/nixos/darwin; in-repo flake first, nixpkgs PR later

## At a glance

| Channel  | Audience              | Update cadence    | Signing / verification        | Maintainer burden | Publish path                             |
|----------|-----------------------|-------------------|-------------------------------|-------------------|------------------------------------------|
| Homebrew | macOS + Linuxbrew     | Per release tag   | SHA256 in formula; brew audit | Low (sed + PR)    | Custom tap (`SchwartzKamel/homebrew-tap`) -> nominate to homebrew-core after stability |
| Scoop    | Windows (PowerShell)  | Per release tag   | SHA256 in manifest; checkver auto-bump | Low (auto-PR via `scoop checkver -u`) | Custom bucket (`SchwartzKamel/scoop-bucket`) -> submit to `scoopinstaller/extras` |
| Nix      | NixOS / nix-darwin / linux nix users | Per release tag | SRI digest in flake; reproducible build | Medium (SRI conversion + flake check)  | In-repo flake -> dedicated flake repo -> nixpkgs PR |

## Scope and non-goals

**In scope for this episode (S02E16 The Catalog):** manifests + docs.
The drafts compile / parse but every `sha256` is a `TODO_FILL_AT_RELEASE_TIME`
or `lib.fakeHash` placeholder. Filling them is a tag-time task per
[`packaging/README.md`](../../packaging/README.md) ritual.

**Out of scope:** creating the tap / bucket repositories, opening the
homebrew-core / scoop-extras / nixpkgs PRs, registering with package
search indexes. Those require maintainer accounts and registry
credentials -- handled in a follow-up episode.

## Why three channels (and not more)

| Skipped channel  | Reason                                                                                |
|------------------|----------------------------------------------------------------------------------------|
| `apt` / `.deb`   | Snap or PPA infrastructure required; ROI low until v2 stabilizes.                      |
| `dnf` / `.rpm`   | Same -- COPR account, signing keys; defer.                                             |
| `winget`         | Microsoft Store identity required for publisher; defer to a dedicated episode.         |
| `chocolatey`     | Scoop covers the power-user Windows surface that overlaps our user base.               |
| `cargo install`  | We are not Rust; PATH-style install via Homebrew/Scoop/Nix covers the same ergonomic.  |
| `pip install`    | We are not Python; same reason.                                                        |
| `flatpak` / `snap` | GUI-app channels; CLI tools are second-class citizens.                               |

The three drafted channels cover the three operating systems we
already publish binaries for and match the install-style users expect
in 2026.

## Dual-binary note (v1 / v2)

The current main-line release is **v1.9.1** (`AzureOpenAI_CLI`,
aliased to `azure-openai-cli`). The v2 line ships as `az-ai-v2` and
already has its own packaging under
[`/packaging/homebrew/Formula/az-ai.rb`](../../packaging/homebrew/Formula/az-ai.rb),
[`/packaging/scoop/az-ai.json`](../../packaging/scoop/az-ai.json), and
[`/packaging/nix/flake.nix`](../../packaging/nix/flake.nix). The two
sets coexist intentionally during the dual-binary transition; collapse
is a follow-up episode after v2 GA.

## Cross-refs

- [`packaging/README.md`](../../packaging/README.md) -- v2-line
  packaging reference, including the tag-time digest-filling ritual
  this directory borrows from.
- [`docs/release/semver-policy.md`](../release/semver-policy.md) --
  Mr. Lippman's contract; release-artifact naming is load-bearing for
  every manifest here.
- [`docs/release/pre-release-checklist.md`](../release/pre-release-checklist.md)
  -- the gate every tag passes through; packaging-bump lives here.
