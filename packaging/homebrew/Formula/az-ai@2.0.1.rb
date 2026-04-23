class AzAiAT201 < Formula
  desc "Azure OpenAI CLI — AOT agent (pinned to v2.0.1)"
  homepage "https://github.com/SchwartzKamel/azure-openai-cli"
  version "2.0.1"
  license "MIT"

  # Versioned-pin formula. Users install with:
  #   brew install az-ai@2.0.1
  #
  # The class name must match Homebrew's versioned-formula camel-case
  # convention: `AzAiAT` + digits-only version (2.0.1 -> "201"). Keep
  # this formula frozen at v2.0.1 — SHA256s below are the digests of the
  # v2.0.1 GitHub Release artifacts and should never change once filled
  # at tag time. If a v2.0.1 hotfix retag ever happens, cut a new pinnable
  # formula (e.g. @2.0.1-1) rather than mutating this one.
  #
  # Context: v2.0.1 is the first publicly published v2.x release.
  # v2.0.0 was tagged but never published (see CHANGELOG [2.0.1] and
  # docs/launch/v2-release-attempt-1-diagnostic.md). The `@2.0.0.rb`
  # sibling retains placeholder sentinels and will never be filled.

  keg_only :versioned_formula

  on_macos do
    on_arm do
      url "https://github.com/SchwartzKamel/azure-openai-cli/releases/download/v2.0.1/az-ai-v2-2.0.1-osx-arm64.tar.gz"
      sha256 "TODO_FILL_AT_RELEASE_TIME"
    end
    on_intel do
      url "https://github.com/SchwartzKamel/azure-openai-cli/releases/download/v2.0.1/az-ai-v2-2.0.1-osx-x64.tar.gz"
      sha256 "TODO_FILL_AT_RELEASE_TIME"
    end
  end

  on_linux do
    on_intel do
      url "https://github.com/SchwartzKamel/azure-openai-cli/releases/download/v2.0.1/az-ai-v2-2.0.1-linux-x64.tar.gz"
      sha256 "TODO_FILL_AT_RELEASE_TIME"
    end
  end

  def install
    bin.install "az-ai-v2" => "az-ai"
    doc.install "LICENSE", "NOTICE", "THIRD_PARTY_NOTICES.md"
  end

  test do
    assert_equal "2.0.1", shell_output("#{bin}/az-ai --version --short").strip
  end
end
