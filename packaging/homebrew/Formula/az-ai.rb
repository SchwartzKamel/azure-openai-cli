class AzAiV2 < Formula
  desc "Azure OpenAI CLI v2 — AOT agent with Microsoft Agent Framework, personas/squads, cost estimator"
  homepage "https://github.com/SchwartzKamel/azure-openai-cli"
  version "2.0.4"
  license "MIT"

  # SHA256s below are the digests of the v2.0.4 GitHub Release artifacts
  # (run 24789065975, published 2026-04-22). Do not invent hashes at
  # authoring time — audits will bounce anything not matching the
  # uploaded artifact. Post-tag fill ritual:
  #   brew bump-formula-pr --url=<tarball-url> --sha256=<sha> az-ai-v2
  # or edit in-place with the sha256sum emitted by release.yml.
  #
  # ⚠️ Tarball-filename drift (tracked in docs/audits/
  # docs-audit-2026-04-22-lippman.md, finding C-1): the v2.0.4 release
  # tarballs are uploaded with `2.0.2` embedded in the filename because
  # `packaging/tarball/stage.sh` VERSION + the Program.cs version
  # constants were not rolled past 2.0.2 in the v2.0.3/v2.0.4 commits.
  # URLs below therefore hardcode the literal `2.0.2` filename at the
  # v2.0.4 tag. Next patch MUST roll all version strings in lock-step;
  # until then `brew test az-ai-v2` will also fail against 2.0.4 since
  # the shipped binary reports `--version --short` → "2.0.2".

  on_macos do
    on_arm do
      url "https://github.com/SchwartzKamel/azure-openai-cli/releases/download/v2.0.4/az-ai-v2-2.0.2-osx-arm64.tar.gz"
      sha256 "6c3051a4a574c09f51f7959b619e187acce37b2918dadd879a79e67ce7eb9874"
    end
    # NOTE: macOS Intel (osx-x64) dropped from the release pipeline as of
    # v2.0.4 — GHA `macos-13` runner pool backlog blocked multiple publishes.
    # Intel-Mac users should install via Rosetta 2 on Apple Silicon, use the
    # Docker image (`ghcr.io/schwartzkamel/azure-openai-cli/az-ai-v2`), or
    # build from source (`dotnet publish -r osx-x64`).
  end

  on_linux do
    on_intel do
      url "https://github.com/SchwartzKamel/azure-openai-cli/releases/download/v2.0.4/az-ai-v2-2.0.2-linux-x64.tar.gz"
      sha256 "9592a9620b0dde3745db0b571708dad22d6a6000686e7c0f07613a96aea798e6"
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

  # Versioned pin support: sibling `az-ai-v2@2.0.0.rb` formula exists in
  # this directory for users who want `brew install az-ai-v2@2.0.0`. Each
  # tagged release gets its own versioned sibling at tag time; the
  # unversioned formula tracks the latest published release.

  test do
    assert_equal "2.0.4", shell_output("#{bin}/az-ai-v2 --version --short").strip
  end
end
