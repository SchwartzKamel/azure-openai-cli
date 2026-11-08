# homebrew-tap

Homebrew tap for [hopper](https://github.com/SchwartzKamel/hopper), the AOT command-line agent for Azure OpenAI.

```sh
brew install schwartzkamel/tap/hopper
```

That installs `hopper` and, as a link to the same binary, `az-ai` (the old command name, so existing scripts keep
working). The formula downloads the published release archive and Homebrew verifies its SHA-256.

- `Formula/hopper.rb` is the current release. `Formula/az-ai@<version>.rb` are frozen pins of the releases that were
  still named `az-ai` (`brew install schwartzkamel/tap/az-ai@2.0.6`).
- `Aliases/az-ai` points at `hopper`, so `brew install schwartzkamel/tap/az-ai` installs the current release.

**This repository is a mirror.** Do not edit it. The formulas live in
[`packaging/homebrew/`](https://github.com/SchwartzKamel/hopper/tree/main/packaging/homebrew) in the main repository and are
copied here by `scripts/publish-taps.sh` after each release, once `scripts/check-manifests.py` has confirmed that every pin
matches the release's published digests.
