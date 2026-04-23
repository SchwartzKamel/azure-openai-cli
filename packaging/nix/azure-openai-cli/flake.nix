{
  # DRAFT -- not yet published. Hashes are nixpkgs.lib.fakeHash placeholders;
  # fill at tag time from the v1.x GitHub Release.
  #
  # Owner: Bob Sacamano (packaging). Filed under S02E16 "The Catalog".
  #
  # Why this lives at packaging/nix/azure-openai-cli/ and not at
  # packaging/nix/flake.nix: the parent directory already holds a flake
  # for the v2 line (binary `az-ai-v2`). Collapsing both into one flake
  # is a follow-up episode after the v1/v2 dual-binary transition closes.
  #
  # Tag-time ritual (mirrors packaging/README.md):
  #   1. Wait for the v1.<minor>.<patch> tag's release.yml run.
  #   2. nix-prefetch-url --type sha256 <tarball-url>     # emits SRI
  #   3. Paste each SRI into latestHashes below.
  #   4. nix build .#default --no-link
  description = "Azure OpenAI CLI v1 -- AOT single-binary agent (DRAFT flake)";

  inputs.nixpkgs.url = "github:NixOS/nixpkgs/nixos-unstable";
  inputs.flake-utils.url = "github:numtide/flake-utils";

  outputs = { self, nixpkgs, flake-utils }:
    let
      version = "1.9.1";
      baseUrl = "https://github.com/SchwartzKamel/azure-openai-cli/releases/download/v${version}";

      # Fill at tag time. SRI form (sha256-<base64>) per Nix convention.
      latestHashes = {
        linux-x64 = nixpkgs.lib.fakeHash;
        osx-arm64 = nixpkgs.lib.fakeHash;
      };

      sources = {
        "x86_64-linux" = {
          url = "${baseUrl}/azure-openai-cli-linux-x64.tar.gz";
          sha256 = latestHashes.linux-x64;
        };
        "aarch64-darwin" = {
          url = "${baseUrl}/azure-openai-cli-osx-arm64.tar.gz";
          sha256 = latestHashes.osx-arm64;
        };
      };

      supportedSystems = builtins.attrNames sources;
    in
    flake-utils.lib.eachSystem supportedSystems (system:
      let
        pkgs = import nixpkgs { inherit system; };
        srcMeta = sources.${system};

        azure-openai-cli = pkgs.stdenv.mkDerivation {
          pname = "azure-openai-cli";
          inherit version;

          src = pkgs.fetchurl { inherit (srcMeta) url sha256; };

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
            install -Dm755 AzureOpenAI_CLI "$out/bin/azure-openai-cli"
            runHook postInstall
          '';

          # Bundle NOTICE + THIRD_PARTY_NOTICES.md per Lippman's gate.
          # If these files are not in the release tarball, the build
          # will fail loudly -- that is intentional.
          postInstall = ''
            install -Dm644 LICENSE                $out/share/licenses/azure-openai-cli/LICENSE
            install -Dm644 NOTICE                 $out/share/licenses/azure-openai-cli/NOTICE
            install -Dm644 THIRD_PARTY_NOTICES.md $out/share/licenses/azure-openai-cli/THIRD_PARTY_NOTICES.md
          '';

          meta = with pkgs.lib; {
            description = "Azure OpenAI CLI -- AOT single-binary agent for terminal and text-expander workflows";
            homepage = "https://github.com/SchwartzKamel/azure-openai-cli";
            license = licenses.mit;
            platforms = supportedSystems;
            mainProgram = "azure-openai-cli";
          };
        };
      in
      {
        packages.default = azure-openai-cli;
        packages.azure-openai-cli = azure-openai-cli;

        apps.default = {
          type = "app";
          program = "${azure-openai-cli}/bin/azure-openai-cli";
        };
      });
}
