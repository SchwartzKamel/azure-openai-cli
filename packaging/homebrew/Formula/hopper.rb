class Hopper < Formula
  desc "AOT command-line agent for Azure OpenAI, built on the Microsoft Agent Framework"
  homepage "https://github.com/SchwartzKamel/hopper"
  license "MIT"

  # SHA256s below are the digests of the v2.4.0 GitHub Release archives. scripts/check-manifests.py compares
  # them with the release's published digests.txt, so do not invent or hand-copy hashes: regenerate them from
  # the release, then run the checker. The archives are named hopper-<version>-<rid> (ADR-016 stage 2); the
  # old command name `az-ai` is installed as an alias of `hopper`.

  on_macos do
    on_arm do
      url "https://github.com/SchwartzKamel/hopper/releases/download/v2.4.0/hopper-2.4.0-osx-arm64.tar.gz"
      sha256 "27b214f491baa7491b505a022c420c84f456f7f6147332012fb8dba2ebb40dc2"
    end
    # NOTE: macOS Intel (osx-x64) was dropped from the release pipeline in v2.0.4 (runner pool backlog).
    # Intel-Mac users can use Rosetta 2 on Apple Silicon, the Docker image, or build from source
    # (`dotnet publish -r osx-x64`).
  end

  on_linux do
    on_intel do
      url "https://github.com/SchwartzKamel/hopper/releases/download/v2.4.0/hopper-2.4.0-linux-x64.tar.gz"
      sha256 "6eae9180783ae943c48d844408cef37199f240da69f4ddf5636b41c1c2a96204"
    end
    on_arm do
      url "https://github.com/SchwartzKamel/hopper/releases/download/v2.4.0/hopper-2.4.0-linux-arm64.tar.gz"
      sha256 "211b89a93782d5b3e604226ddcb7a4ab60ced07c4786c55cc7cea829720111ce"
    end
    # NOTE: linux-musl-x64 is published to the GitHub Release but Homebrew does not model musl;
    # Alpine/musl users use the OCI image or the archive directly.
  end

  def install
    bin.install "hopper"
    # The old command name keeps working, as a link to the same binary.
    bin.install_symlink "hopper" => "az-ai"
    # Ship NOTICE + third-party attributions with every distributed artifact.
    doc.install "LICENSE", "NOTICE", "THIRD_PARTY_NOTICES.md"
  end

  test do
    assert_equal "2.4.0", shell_output("#{bin}/hopper --version --short").strip
    assert_equal "2.4.0", shell_output("#{bin}/az-ai --version --short").strip
  end
end
