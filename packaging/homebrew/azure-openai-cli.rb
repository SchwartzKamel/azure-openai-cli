# DRAFT -- not yet published. Tap repo does not exist; SHA256s are placeholders.
# Owner: Bob Sacamano (packaging). Filed under S02E16 "The Catalog".
#
# Tracks the v1.x line of azure-openai-cli (binary `AzureOpenAI_CLI`,
# aliased to `azure-openai-cli` on install). The v2 line ships separately
# under packaging/homebrew/Formula/az-ai.rb -- do not collapse the two
# until the v1/v2 dual-binary transition completes.
#
# Release-artifact contract (see .github/workflows/release.yml v1 matrix):
#   azure-openai-cli-linux-x64.tar.gz
#   azure-openai-cli-osx-arm64.tar.gz
#   (linux-musl-x64 and win-x64 also built but not consumed by Homebrew.)
#
# Tag-time ritual (mirrors packaging/README.md "Tag-time ritual"):
#   1. Wait for the v1.<minor>.<patch> tag's release.yml to publish.
#   2. Fetch SHA256 for each tarball above.
#   3. sed-replace the TODO_FILL_AT_RELEASE_TIME tokens below.
#   4. Open a PR against the (future) tap repo.

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
    # The release tarball extracts AzureOpenAI_CLI at the archive root.
    # Install it under the package's libexec then expose a stable
    # `azure-openai-cli` shim so users get the project name on PATH.
    libexec.install "AzureOpenAI_CLI"
    bin.install_symlink libexec/"AzureOpenAI_CLI" => "azure-openai-cli"

    # Bundle legal text per the NOTICE-bundling rule (Mr. Lippman).
    # These three files MUST be staged into every release tarball by
    # the v1 release pipeline. If they are missing, the formula audit
    # will fail noisily and Lippman blocks the release.
    doc.install "LICENSE" if File.exist?("LICENSE")
    doc.install "NOTICE" if File.exist?("NOTICE")
    doc.install "THIRD_PARTY_NOTICES.md" if File.exist?("THIRD_PARTY_NOTICES.md")
  end

  test do
    # Smoke test only -- avoid hitting Azure during `brew test`.
    assert_match version.to_s, shell_output("#{bin}/azure-openai-cli --version")
  end
end
