class AzAiV2 < Formula
  desc "Azure OpenAI CLI v2 — AOT agent with Microsoft Agent Framework, personas/squads, cost estimator"
  homepage "https://github.com/SchwartzKamel/azure-openai-cli"
  version "2.0.6"
  license "MIT"

  # SHA256s below are the digests of the v2.0.6 GitHub Release artifacts
  # (published 2026-04-22). v2.0.6 rolls all version strings in lock-step
  # with the tag (verify-version-strings.sh gate 4 green) — the v2.0.4
  # tarball-filename drift (audit finding C-1) is resolved here, so
  # download URLs use the clean `az-ai-v2-<VERSION>-<RID>` pattern again.
  # Do not invent hashes at authoring time — audits will bounce anything
  # not matching the uploaded artifact.

  on_macos do
    on_arm do
      url "https://github.com/SchwartzKamel/azure-openai-cli/releases/download/v2.0.6/az-ai-v2-2.0.6-osx-arm64.tar.gz"
      sha256 "5794a415c04a12bf26ace35c87bc7075dc55205fc5f101f589cf7fe64ae13c74"
    end
    # NOTE: macOS Intel (osx-x64) dropped from the release pipeline as of
    # v2.0.4 — GHA `macos-13` runner pool backlog blocked multiple publishes.
    # Intel-Mac users should install via Rosetta 2 on Apple Silicon, use the
    # Docker image (`ghcr.io/schwartzkamel/azure-openai-cli/az-ai-v2`), or
    # build from source (`dotnet publish -r osx-x64`).
  end

  on_linux do
    on_intel do
      url "https://github.com/SchwartzKamel/azure-openai-cli/releases/download/v2.0.6/az-ai-v2-2.0.6-linux-x64.tar.gz"
      sha256 "732e422724379b40d3efdfc34d455cf21a56705fdaee46ec378897bde3082c31"
    end
    # NOTE: linux-arm64 not yet published. Add `on_arm` block once the release
    # pipeline emits az-ai-v2-<version>-linux-arm64.tar.gz. linux-musl-x64
    # is published to the GitHub Release but Homebrew does not model musl;
    # Alpine/musl users pin via Nix or the OCI image.
  end

  def install
    bin.install "az-ai-v2"
    # Ship NOTICE + third-party attributions with every distributed artifact.
    # `brew info az-ai-v2` surfaces the doc dir; users can read with:
    #   cat "$(brew --prefix az-ai-v2)/share/doc/az-ai-v2/NOTICE"
    doc.install "LICENSE", "NOTICE", "THIRD_PARTY_NOTICES.md"
  end

  # Versioned pin support: sibling `az-ai-v2@2.0.0.rb`, `@2.0.1.rb`,
  # `@2.0.2.rb`, `@2.0.4.rb`, `@2.0.6.rb` formulae exist in this
  # directory for users who want `brew install az-ai-v2@2.0.6`. Each
  # tagged release gets its own versioned sibling at tag time; the
  # unversioned formula tracks the latest published release.

  test do
    assert_equal "2.0.6", shell_output("#{bin}/az-ai-v2 --version --short").strip
  end
end
