{
  description = "Azure OpenAI CLI v2 — AOT agent with Microsoft Agent Framework, personas/squads, cost estimator";

  inputs.nixpkgs.url = "github:NixOS/nixpkgs/nixos-unstable";
  inputs.flake-utils.url = "github:numtide/flake-utils";

  outputs = { self, nixpkgs, flake-utils }:
    let
      version = "2.0.0";
      baseUrl = "https://github.com/SchwartzKamel/azure-openai-cli/releases/download/v${version}";

      # Per-system release artifact metadata.
      # NOTE: linux-aarch64 is not yet published. Once the release pipeline
      # emits az-ai-v2-${version}-linux-arm64.tar.gz, wire it in here.
      #
      # Hashes below are release-time placeholders: Mr. Lippman replaces each
      # `lib.fakeHash` with the real SRI digest when cutting v2.0.0, after the
      # release workflow uploads the tarballs. Do not invent hashes — Nix will
      # refuse the substitution if it doesn't match the published artifact.
      sources = {
        "x86_64-linux" = {
          url = "${baseUrl}/az-ai-v2-${version}-linux-x64.tar.gz";
          sha256 = nixpkgs.lib.fakeHash;
        };
        "x86_64-darwin" = {
          url = "${baseUrl}/az-ai-v2-${version}-osx-x64.tar.gz";
          sha256 = nixpkgs.lib.fakeHash;
        };
        "aarch64-darwin" = {
          url = "${baseUrl}/az-ai-v2-${version}-osx-arm64.tar.gz";
          sha256 = nixpkgs.lib.fakeHash;
        };
      };

      supportedSystems = builtins.attrNames sources;
    in
    flake-utils.lib.eachSystem supportedSystems (system:
      let
        pkgs = import nixpkgs { inherit system; };
        src = sources.${system};

        az-ai = pkgs.stdenv.mkDerivation {
          pname = "az-ai-v2";
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
            install -Dm755 az-ai-v2 "$out/bin/az-ai-v2"
            runHook postInstall
          '';

          # Bundle NOTICE + third-party attributions so every Nix-installed
          # copy carries the legal text alongside the binary. Backs Mr.
          # Lippman's release-notes claim that all distributed artifacts
          # ship NOTICE.
          postInstall = ''
            mkdir -p $out/share/doc/az-ai-v2
            cp LICENSE NOTICE THIRD_PARTY_NOTICES.md $out/share/doc/az-ai-v2/
          '';

          meta = with pkgs.lib; {
            description = "Azure OpenAI CLI v2 — AOT agent with Microsoft Agent Framework, personas/squads, cost estimator";
            homepage = "https://github.com/SchwartzKamel/azure-openai-cli";
            license = licenses.mit;
            platforms = supportedSystems;
            mainProgram = "az-ai-v2";
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
