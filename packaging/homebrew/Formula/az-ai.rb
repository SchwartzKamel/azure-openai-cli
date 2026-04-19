class AzAi < Formula
  desc "Azure OpenAI CLI — 5ms native AOT agent for text injection"
  homepage "https://github.com/SchwartzKamel/azure-openai-cli"
  version "1.8.1"
  license "MIT"

  on_macos do
    on_arm do
      url "https://github.com/SchwartzKamel/azure-openai-cli/releases/download/v1.8.1/azure-openai-cli-osx-arm64.tar.gz"
      sha256 "eddcfeb0319f3942ba7b518e42b6e3f56696930312497c795cec53a9806caf46"
    end
    on_intel do
      url "https://github.com/SchwartzKamel/azure-openai-cli/releases/download/v1.8.1/azure-openai-cli-osx-x64.tar.gz"
      sha256 "85c23888d140364818f624d3edd714ba6e121f5c071282c7bcec344afe26f8df"
    end
  end

  on_linux do
    on_intel do
      url "https://github.com/SchwartzKamel/azure-openai-cli/releases/download/v1.8.1/azure-openai-cli-linux-x64.tar.gz"
      sha256 "93c28f53d98c0fa699512d515f26522981ba32a24d668593e0830ca30520f03d"
    end
    # NOTE: linux-arm64 not yet published. Add `on_arm` block once the release
    # pipeline emits azure-openai-cli-linux-arm64.tar.gz.
  end

  def install
    bin.install "AzureOpenAI_CLI" => "az-ai"
  end

  test do
    assert_match "1.8.1", shell_output("#{bin}/az-ai --version --short")
  end
end
