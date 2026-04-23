class AzAiAT204 < Formula
  desc "Azure OpenAI CLI — AOT agent (pinned to v2.0.4)"
  homepage "https://github.com/SchwartzKamel/azure-openai-cli"
  version "2.0.4"
  license "MIT"

  # Versioned-pin formula. Users install with:
  #   brew install az-ai@2.0.4
  #
  # The class name must match Homebrew's versioned-formula camel-case
  # convention: `AzAiAT` + digits-only version (2.0.4 -> "204"). Keep
  # this formula frozen at v2.0.4 — SHA256s below are the digests of the
  # v2.0.4 GitHub Release artifacts (run 24789065975, published
  # 2026-04-22) and must never change once filled. If a v2.0.4 hotfix
  # retag ever happens, cut a new pinnable formula (e.g. @2.0.4-1)
  # rather than mutating this one.
  #
  # Context: v2.0.4 drops macOS Intel (osx-x64) from the release matrix;
  # GHA macos-13 runner pool backlog blocked v2.0.2 and v2.0.3 publishes
  # (v2.0.2 eventually published via `gh run rerun --failed`; v2.0.3 was
  # cancelled at cutover — no GitHub Release exists). v2.0.4 supersedes
  # both. See CHANGELOG [2.0.4] and docs/launch/v2.0.2-publish-handoff.md.
  #
  # ⚠️ Tarball-filename drift (audit finding C-1): the v2.0.4 release
  # tarballs are uploaded with `2.0.2` in the filename — neither
  # packaging/tarball/stage.sh VERSION nor the Program.cs version
  # constants were rolled past 2.0.2 in the v2.0.3/v2.0.4 commits. URLs
  # below therefore hardcode the literal `2.0.2` filename at the v2.0.4
  # tag. The `brew test` block below will also fail against this binary
  # (it reports `--version --short` → "2.0.2") until v2.0.5 rolls the
  # version strings in lock-step.

  keg_only :versioned_formula

  on_macos do
    on_arm do
      url "https://github.com/SchwartzKamel/azure-openai-cli/releases/download/v2.0.4/az-ai-v2-2.0.2-osx-arm64.tar.gz"
      sha256 "6c3051a4a574c09f51f7959b619e187acce37b2918dadd879a79e67ce7eb9874"
    end
    # No on_intel block — osx-x64 dropped from the v2.0.4 release matrix.
  end

  on_linux do
    on_intel do
      url "https://github.com/SchwartzKamel/azure-openai-cli/releases/download/v2.0.4/az-ai-v2-2.0.2-linux-x64.tar.gz"
      sha256 "9592a9620b0dde3745db0b571708dad22d6a6000686e7c0f07613a96aea798e6"
    end
  end

  def install
    bin.install "az-ai-v2" => "az-ai"
    doc.install "LICENSE", "NOTICE", "THIRD_PARTY_NOTICES.md"
  end

  test do
    assert_equal "2.0.4", shell_output("#{bin}/az-ai --version --short").strip
  end
end
