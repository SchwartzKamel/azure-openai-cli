{
  description = "Azure OpenAI CLI — 5ms native AOT agent for text injection";

  inputs.nixpkgs.url = "github:NixOS/nixpkgs/nixos-unstable";
  inputs.flake-utils.url = "github:numtide/flake-utils";

  outputs = { self, nixpkgs, flake-utils }:
    let
      version = "1.8.1";
      baseUrl = "https://github.com/SchwartzKamel/azure-openai-cli/releases/download/v${version}";

      # Per-system release artifact metadata.
      # NOTE: linux-aarch64 is not yet published. Once the release pipeline
      # emits azure-openai-cli-linux-arm64.tar.gz, wire it in here.
      sources = {
        "x86_64-linux" = {
          url = "${baseUrl}/azure-openai-cli-linux-x64.tar.gz";
          sha256 = "93c28f53d98c0fa699512d515f26522981ba32a24d668593e0830ca30520f03d";
        };
        "x86_64-darwin" = {
          url = "${baseUrl}/azure-openai-cli-osx-x64.tar.gz";
          sha256 = "85c23888d140364818f624d3edd714ba6e121f5c071282c7bcec344afe26f8df";
        };
        "aarch64-darwin" = {
          url = "${baseUrl}/azure-openai-cli-osx-arm64.tar.gz";
          sha256 = "eddcfeb0319f3942ba7b518e42b6e3f56696930312497c795cec53a9806caf46";
        };
      };

      supportedSystems = builtins.attrNames sources;
    in
    flake-utils.lib.eachSystem supportedSystems (system:
      let
        pkgs = import nixpkgs { inherit system; };
        src = sources.${system};

        az-ai = pkgs.stdenv.mkDerivation {
          pname = "az-ai";
          inherit version;

          src = pkgs.fetchurl {
            inherit (src) url sha256;
          };

          sourceRoot = ".";

          nativeBuildInputs = pkgs.lib.optionals pkgs.stdenv.isLinux [
            pkgs.autoPatchelfHook
          ];

          buildInputs = pkgs.lib.optionals pkgs.stdenv.isLinux [
            pkgs.stdenv.cc.cc.lib
            pkgs.zlib
            pkgs.icu
            pkgs.openssl
          ];

          dontBuild = true;
          dontConfigure = true;

          installPhase = ''
            runHook preInstall
            install -Dm755 AzureOpenAI_CLI "$out/bin/az-ai"
            runHook postInstall
          '';

          meta = with pkgs.lib; {
            description = "Azure OpenAI CLI — 5ms native AOT agent for text injection";
            homepage = "https://github.com/SchwartzKamel/azure-openai-cli";
            license = licenses.mit;
            platforms = supportedSystems;
            mainProgram = "az-ai";
          };
        };
      in
      {
        packages.default = az-ai;
        packages.az-ai = az-ai;

        apps.default = {
          type = "app";
          program = "${az-ai}/bin/az-ai";
        };
      });
}
