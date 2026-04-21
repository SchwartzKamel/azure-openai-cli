class AzAiV2AT200 < Formula
  desc "Azure OpenAI CLI v2 — AOT agent (pinned to v2.0.0)"
  homepage "https://github.com/SchwartzKamel/azure-openai-cli"
  version "2.0.0"
  license "MIT"

  # Versioned-pin formula. Users install with:
  #   brew install az-ai-v2@2.0.0
  #
  # The class name must match Homebrew's versioned-formula camel-case
  # convention: `AzAiV2AT` + digits-only version (2.0.0 -> "200"). Keep
  # this formula frozen at v2.0.0 — SHA256s below are the digests of the
  # v2.0.0 GitHub Release artifacts and should never change once filled
  # at tag time. If a v2.0.0 hotfix retag ever happens, cut a new pinnable
  # formula (e.g. @2.0.0-1) rather than mutating this one.

  keg_only :versioned_formula

  on_macos do
    on_arm do
      url "https://github.com/SchwartzKamel/azure-openai-cli/releases/download/v2.0.0/az-ai-v2-2.0.0-osx-arm64.tar.gz"
      sha256 "TODO_FILL_AT_RELEASE_TIME"
    end
    on_intel do
      url "https://github.com/SchwartzKamel/azure-openai-cli/releases/download/v2.0.0/az-ai-v2-2.0.0-osx-x64.tar.gz"
      sha256 "TODO_FILL_AT_RELEASE_TIME"
    end
  end

  on_linux do
    on_intel do
      url "https://github.com/SchwartzKamel/azure-openai-cli/releases/download/v2.0.0/az-ai-v2-2.0.0-linux-x64.tar.gz"
      sha256 "TODO_FILL_AT_RELEASE_TIME"
    end
  end

  def install
    bin.install "az-ai-v2"
    doc.install "LICENSE", "NOTICE", "THIRD_PARTY_NOTICES.md"
  end

  test do
    assert_equal "2.0.0", shell_output("#{bin}/az-ai-v2 --version --short").strip
  end
end
