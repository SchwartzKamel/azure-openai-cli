class AzAiV2 < Formula
  desc "Azure OpenAI CLI v2 — AOT agent with Microsoft Agent Framework, personas/squads, cost estimator"
  homepage "https://github.com/SchwartzKamel/azure-openai-cli"
  version "2.0.1"
  license "MIT"

  # SHA256s below are release-time placeholders. Mr. Lippman fills these
  # during the v2.0.1 tag cut, after the release workflow publishes the
  # tarballs and emits their digests. Do not invent hashes at authoring
  # time — audits will bounce anything not matching the uploaded artifact.
  #
  # Fill workflow (post-tag):
  #   brew bump-formula-pr --url=<tarball-url> --sha256=<sha> az-ai-v2
  # or edit in-place with the digests emitted by the release workflow's
  # sha256sum step (see packaging/README.md §Tag-time ritual).

  on_macos do
    on_arm do
      url "https://github.com/SchwartzKamel/azure-openai-cli/releases/download/v#{version}/az-ai-v2-#{version}-osx-arm64.tar.gz"
      sha256 "TODO_FILL_AT_RELEASE_TIME"
    end
    on_intel do
      url "https://github.com/SchwartzKamel/azure-openai-cli/releases/download/v#{version}/az-ai-v2-#{version}-osx-x64.tar.gz"
      sha256 "TODO_FILL_AT_RELEASE_TIME"
    end
  end

  on_linux do
    on_intel do
      url "https://github.com/SchwartzKamel/azure-openai-cli/releases/download/v#{version}/az-ai-v2-#{version}-linux-x64.tar.gz"
      sha256 "TODO_FILL_AT_RELEASE_TIME"
    end
    # NOTE: linux-arm64 not yet published. Add `on_arm` block once the release
    # pipeline emits az-ai-v2-#{version}-linux-arm64.tar.gz.
  end

  def install
    bin.install "az-ai-v2"
    # Ship NOTICE + third-party attributions with every distributed artifact.
    # `brew info az-ai-v2` surfaces the doc dir; users can read with:
    #   cat "$(brew --prefix az-ai-v2)/share/doc/az-ai-v2/NOTICE"
    doc.install "LICENSE", "NOTICE", "THIRD_PARTY_NOTICES.md"
  end

  # Versioned pin support: sibling `az-ai-v2@2.0.0.rb` formula exists in
  # this directory for users who want `brew install az-ai-v2@2.0.0`. Each
  # tagged release gets its own versioned sibling at tag time; the
  # unversioned formula tracks the latest published release.

  test do
    assert_equal "2.0.1", shell_output("#{bin}/az-ai-v2 --version --short").strip
  end
end
