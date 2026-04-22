class AzAiV2AT206 < Formula
  desc "Azure OpenAI CLI v2 — AOT agent (pinned to v2.0.6)"
  homepage "https://github.com/SchwartzKamel/azure-openai-cli"
  version "2.0.6"
  license "MIT"

  # Versioned-pin formula. Users install with:
  #   brew install az-ai-v2@2.0.6
  #
  # The class name must match Homebrew's versioned-formula camel-case
  # convention: `AzAiV2AT` + digits-only version (2.0.6 -> "206"). Keep
  # this formula frozen at v2.0.6 — SHA256s below are the digests of the
  # v2.0.6 GitHub Release artifacts (published 2026-04-22) and must
  # never change once filled. If a v2.0.6 hotfix retag ever happens,
  # cut a new pinnable formula (e.g. @2.0.6-1) rather than mutating
  # this one.
  #
  # Context: v2.0.6 is the clean-lockstep release — verify-version-strings.sh
  # gate 4 green, filenames carry the tag version (no C-1 drift), and
  # `az-ai-v2 --version --short` reports "2.0.6" on every RID. v2.0.5
  # was queued in-tree as the lockstep roll but never produced a
  # published packaging pin; v2.0.6 supersedes it. See CHANGELOG [2.0.6].

  keg_only :versioned_formula

  on_macos do
    on_arm do
      url "https://github.com/SchwartzKamel/azure-openai-cli/releases/download/v2.0.6/az-ai-v2-2.0.6-osx-arm64.tar.gz"
      sha256 "5794a415c04a12bf26ace35c87bc7075dc55205fc5f101f589cf7fe64ae13c74"
    end
    # No on_intel block — osx-x64 dropped from the release matrix at v2.0.4.
  end

  on_linux do
    on_intel do
      url "https://github.com/SchwartzKamel/azure-openai-cli/releases/download/v2.0.6/az-ai-v2-2.0.6-linux-x64.tar.gz"
      sha256 "732e422724379b40d3efdfc34d455cf21a56705fdaee46ec378897bde3082c31"
    end
  end

  def install
    bin.install "az-ai-v2"
    doc.install "LICENSE", "NOTICE", "THIRD_PARTY_NOTICES.md"
  end

  test do
    assert_equal "2.0.6", shell_output("#{bin}/az-ai-v2 --version --short").strip
  end
end
