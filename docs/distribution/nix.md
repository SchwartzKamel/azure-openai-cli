# Nix distribution

> Status: **DRAFT**. Hashes in the flake are `nixpkgs.lib.fakeHash`
> placeholders. The flake will not build until they are filled at tag
> time.

## Approach: in-repo flake -> dedicated flake -> nixpkgs PR

Three escalating paths:

1. **In-repo flake** (this episode) -- `flake.nix` lives under
   `packaging/nix/azure-openai-cli/` (planned; not yet committed).
   Users consume it directly via
   `github:SchwartzKamel/azure-openai-cli?dir=packaging/nix/azure-openai-cli`.
   Zero infrastructure outside this repo. Good for early adopters.
2. **Dedicated flake repo** (`SchwartzKamel/nix-azure-openai-cli`) --
   if it simplifies semver-tagged inputs. Optional; only worth it if
   adoption justifies the maintenance cost.
3. **nixpkgs PR** -- the long game. Acceptance into `nixpkgs` puts
   the package in front of every NixOS / nix-darwin / Nix-on-anything
   user. Requires a stable upstream, predictable release cadence,
   reproducible build, and a maintainer entry. Bob's S03/S06 work.

**This episode delivers path 1.** Paths 2 and 3 are explicit
follow-ups.

## Why a subdirectory under `packaging/nix/`?

The repo already contains a v2-line flake at
[`packaging/nix/flake.nix`](../../packaging/nix/flake.nix) for the
`az-ai` binary. Two flakes cannot share the same path. Splitting
into per-package subdirectories keeps both consumable and signposts
the dual-binary transition cleanly:

```text
packaging/nix/
  flake.nix                       # v2 line, az-ai (existing)
  azure-openai-cli/
    flake.nix                     # v1 line, azure-openai-cli (NEW)
```

After the v1/v2 transition closes, both collapse into a single flake
exposing `packages.default` (whichever is current) and
`packages.<legacy-name>` aliases. That collapse is a separate episode.

## Draft flake skeleton

```nix
{
  description = "Azure OpenAI CLI v1 -- AOT single-binary agent (DRAFT flake)";

  inputs.nixpkgs.url = "github:NixOS/nixpkgs/nixos-unstable";
  inputs.flake-utils.url = "github:numtide/flake-utils";

  outputs = { self, nixpkgs, flake-utils }:
    let
      version = "1.9.1";
      latestHashes = {
        linux-x64 = nixpkgs.lib.fakeHash;     # fill at tag time
        osx-arm64 = nixpkgs.lib.fakeHash;     # fill at tag time
      };
      # ... see packaging/nix/azure-openai-cli/flake.nix for full skeleton
    in
    flake-utils.lib.eachSystem [ "x86_64-linux" "aarch64-darwin" ] (system: { ... });
}
```

Full file: `packaging/nix/azure-openai-cli/flake.nix` (planned; not yet
committed).

## Hash strategy (SRI form)

Nix expects SRI digests (`sha256-<base64>`) rather than raw hex. Two
ways to compute:

```sh
# Convert a sha256sum hex digest:
nix hash to-sri --type sha256 <hex>

# Or prefetch directly:
nix-prefetch-url --type sha256 \
  https://github.com/SchwartzKamel/azure-openai-cli/releases/download/v1.9.1/azure-openai-cli-linux-x64.tar.gz
```

Paste the SRI string into the matching `latestHashes.<rid>` slot, then:

```sh
cd packaging/nix/azure-openai-cli
nix flake check
nix build .#default --no-link
```

## Install / uninstall verification

```sh
# Run directly (no install)
nix run "github:SchwartzKamel/azure-openai-cli?dir=packaging/nix/azure-openai-cli" -- --version

# Add to a system flake
# inputs.azure-openai-cli.url = "github:SchwartzKamel/azure-openai-cli?dir=packaging/nix/azure-openai-cli";
# environment.systemPackages = [ inputs.azure-openai-cli.packages.${pkgs.system}.default ];

# Profile install (impure but quick)
nix profile install "github:SchwartzKamel/azure-openai-cli?dir=packaging/nix/azure-openai-cli"
azure-openai-cli --version       # expect: 1.9.1

# Uninstall
nix profile remove azure-openai-cli
```

## nixpkgs PR strategy (future)

When the time is right (Bob's S03/S06 episodes):

1. Move the derivation into `pkgs/by-name/az/azure-openai-cli/package.nix`
   per current nixpkgs layout conventions.
2. Add a `passthru.updateScript` so nixpkgs-update bots can roll us.
3. Add Bob (or a designated maintainer) to `maintainers/maintainer-list.nix`.
4. Open the PR; expect 2 weeks of review iteration.
5. Land the flake-side note in this doc pointing users at `nixpkgs`
   as the preferred install path; keep the in-repo flake as a fallback
   for nixpkgs-unstable users on day-zero releases.

## Validation

This episode validates by file shape only -- `nix` is **not installed**
in the agent's filming environment, so `nix flake check` cannot run
here. Verification gap recorded in the exec report; tag-time ritual on
a Nix-equipped workstation closes it.

## Open work (deferred)

- Fill SRI hashes at the next v1 tag.
- Add `linux-musl-x64` source (requires a separate `sourceRoot` /
  patchelf path; nixpkgs `pkgs.musl` makes this tractable but
  non-trivial).
- Dedicated flake repo if/when adoption justifies it.
- nixpkgs PR per the strategy above.

## Cross-refs

- [Distribution README](README.md) -- channel comparison.
- [`packaging/nix/flake.nix`](../../packaging/nix/flake.nix) -- the
  parallel v2-line flake; reference implementation for sibling-pin
  patterns we will adopt here when v1 needs versioned pins.
- [`docs/release/semver-policy.md`](../release/semver-policy.md) --
  release-artifact naming contract.
