# Homebrew distribution

> Status: **DRAFT**. Tap repository does not yet exist; SHA256s in the
> formula are placeholders. This document describes the approach.

## Approach: custom tap first, homebrew-core later

Two paths exist for shipping a Homebrew formula:

1. **Custom tap** (`SchwartzKamel/homebrew-tap`) -- we own the
   repository, push at our own cadence, accept any version-control
   workflow we want. Users install with `brew tap` + `brew install`.
2. **homebrew-core** -- formula lives in
   [`Homebrew/homebrew-core`](https://github.com/Homebrew/homebrew-core).
   Higher trust signal, automatic CI on every macOS / Linux Homebrew
   user, but acceptance criteria are strict (notable project, stable
   release cadence, no `head` URLs, must pass `brew audit --new --strict --online`).

**We start with the custom tap.** Reasons:

- We control the publish cadence; no third-party review on every patch.
- We can iterate the formula shape (version pinning, NOTICE bundling)
  without affecting Homebrew's audit gate.
- homebrew-core nomination becomes viable after we have a year of
  stable monthly releases and visible adoption -- a future episode for
  Bob and Mr. Lippman to co-pilot.

## Draft formula

The draft lives at `packaging/homebrew/azure-openai-cli.rb` (not yet
committed; see `packaging/homebrew/Formula/az-ai.rb` for the current
v2-line formula).
Inline preview:

```ruby
class AzureOpenaiCli < Formula
  desc "Azure OpenAI CLI -- AOT single-binary agent for terminal and text-expander workflows"
  homepage "https://github.com/SchwartzKamel/azure-openai-cli"
  license "MIT"
  version "1.9.1"

  on_macos do
    on_arm do
      url "https://github.com/SchwartzKamel/azure-openai-cli/releases/download/v#{version}/azure-openai-cli-osx-arm64.tar.gz"
      sha256 "TODO_FILL_AT_RELEASE_TIME"
    end
  end

  on_linux do
    on_intel do
      url "https://github.com/SchwartzKamel/azure-openai-cli/releases/download/v#{version}/azure-openai-cli-linux-x64.tar.gz"
      sha256 "TODO_FILL_AT_RELEASE_TIME"
    end
  end

  def install
    libexec.install "AzureOpenAI_CLI"
    bin.install_symlink libexec/"AzureOpenAI_CLI" => "azure-openai-cli"
    doc.install "LICENSE", "NOTICE", "THIRD_PARTY_NOTICES.md"
  end

  test do
    assert_match version.to_s, shell_output("#{bin}/azure-openai-cli --version")
  end
end
```

## Checksum-pinning strategy

Homebrew requires `sha256` for every release tarball. Three rules:

1. **Never invent a digest.** Always fetch from the published release.
2. **Never edit a published digest.** Once a tag is public, the digest
   is immutable. If the tarball changes, cut a new tag.
3. **Use the release tarball, not the GitHub source archive.** The
   source archive's digest changes when GitHub re-renders metadata.
   Our pipeline produces stable `azure-openai-cli-<rid>.tar.gz`
   artifacts -- pin those.

Tag-time computation:

```sh
VERSION=1.9.1
BASE="https://github.com/SchwartzKamel/azure-openai-cli/releases/download/v${VERSION}"

curl -sL "${BASE}/azure-openai-cli-osx-arm64.tar.gz" | sha256sum
curl -sL "${BASE}/azure-openai-cli-linux-x64.tar.gz" | sha256sum
```

Paste each digest into the matching `sha256` line in the formula, then
verify:

```sh
brew audit --strict ./packaging/homebrew/azure-openai-cli.rb
```

## Install / uninstall verification

Once the tap exists:

```sh
# Install
brew tap SchwartzKamel/tap
brew install azure-openai-cli
azure-openai-cli --version       # expect: 1.9.1

# Smoke
azure-openai-cli --help | head -5

# Uninstall
brew uninstall azure-openai-cli
brew untap SchwartzKamel/tap
which azure-openai-cli           # expect: not found
```

Pre-tap (today) install path:

```sh
brew install --formula ./packaging/homebrew/azure-openai-cli.rb
```

## Open work (deferred)

- Tap repository creation (`SchwartzKamel/homebrew-tap`).
- Linux ARM64 build added to the v1 release matrix; `on_linux do on_arm do`
  block added to the formula.
- `brew bump-formula-pr` automation in `.github/workflows/release.yml`
  to open the tap PR automatically after each tag.
- homebrew-core nomination after twelve consecutive stable monthly
  releases.

## Cross-refs

- [Distribution README](README.md) -- channel comparison.
- [`packaging/README.md`](../../packaging/README.md) -- v2-line
  packaging reference, mirrored here.
- [`docs/release/semver-policy.md`](../release/semver-policy.md) --
  artifact naming contract.
