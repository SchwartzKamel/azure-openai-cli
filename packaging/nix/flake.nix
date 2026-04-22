{
  description = "Azure OpenAI CLI v2 — AOT agent with Microsoft Agent Framework, personas/squads, cost estimator";

  inputs.nixpkgs.url = "github:NixOS/nixpkgs/nixos-unstable";
  inputs.flake-utils.url = "github:numtide/flake-utils";

  outputs = { self, nixpkgs, flake-utils }:
    let
      version = "2.0.6";
      baseUrlFor = v: "https://github.com/SchwartzKamel/azure-openai-cli/releases/download/v${v}";

      # Build the per-system sources attrset for a given release version.
      # All frozen versioned-pin derivations go through this helper so the
      # URL/arch shape stays consistent across releases.
      #
      # ⚠️ Tarball-filename drift (audit finding C-1): v2.0.4 tarballs were
      # uploaded with `2.0.2` in the filename because
      # packaging/tarball/stage.sh VERSION was not rolled past 2.0.2 in
      # the v2.0.3/v2.0.4 commits. The `tarballVersion` arg below lets
      # pinned entries override the filename-version independently of
      # the tag-version. Default = v (clean case). v2.0.4 passes "2.0.2".
      sourcesFor = v: tarballVersion: hashes: {
        "x86_64-linux" = {
          url = "${baseUrlFor v}/az-ai-v2-${tarballVersion}-linux-x64.tar.gz";
          sha256 = hashes.linux-x64;
        };
        "aarch64-darwin" = {
          url = "${baseUrlFor v}/az-ai-v2-${tarballVersion}-osx-arm64.tar.gz";
          sha256 = hashes.osx-arm64;
        };
        # NOTE: "x86_64-darwin" (osx-x64) dropped as of v2.0.4 — GHA
        # macos-13 runner pool backlog blocked multiple publishes. Intel-Mac
        # users should use Rosetta 2 on aarch64-darwin (set system =
        # "aarch64-darwin" and pull the osx-arm64 binary), or the OCI image.
      };

      # Hash table for the tracking (latest) release, filled from the
      # v2.0.6 GitHub Release (published 2026-04-22).
      # SRI digests (sha256-<base64>) per Nix convention.
      latestHashes = {
        linux-x64 = "sha256-cy5CJyQ3m0DT79/DTUVc8hpWcF/a7kbsN4iXveMILDE=";
        osx-arm64 = "sha256-V5SkFcBKEr8mrONch7xwddxVIF/F8QH1ic9/5krhPHQ=";
      };

      # Frozen hash tables for pinnable releases. These should only ever be
      # mutated to replace a placeholder with the real SRI at tag time; once
      # filled, they stay put forever. If a retag is needed, add a new entry
      # (e.g. `v2_0_0-1`) rather than mutating an existing one.
      pinnedHashes = {
        "2.0.0" = {
          # v2.0.0 was tagged but never published (release.yml run
          # 24736776551 failed pre-publish). The tag is immutable but no
          # tarballs were uploaded, so these hashes can never be filled.
          # Retained as an "attempted release" marker for parity with the
          # Homebrew / Scoop sibling manifests. v2.0.1 supersedes. See
          # CHANGELOG [2.0.1] and docs/launch/v2-release-attempt-1-diagnostic.md.
          linux-x64 = nixpkgs.lib.fakeHash;
          osx-x64   = nixpkgs.lib.fakeHash;
          osx-arm64 = nixpkgs.lib.fakeHash;
        };
        "2.0.1" = {
          # v2.0.1 was tagged but never published (release workflow run
          # 24739184465 failed in docker-publish-v2 with the same
          # asset-graph mismatch that broke v2.0.0; the Alpine SDK swap
          # did not address the real root cause — see
          # docs/launch/v2.0.1-release-attempt-diagnostic.md). The tag is
          # immutable but no tarballs were uploaded, so these hashes can
          # never be filled. Retained as an "attempted release" marker
          # alongside v2.0.0. v2.0.2 supersedes.
          linux-x64 = nixpkgs.lib.fakeHash;
          osx-x64   = nixpkgs.lib.fakeHash;
          osx-arm64 = nixpkgs.lib.fakeHash;
        };
        "2.0.2" = {
          # v2.0.2 GitHub Release assets published via `gh run rerun --failed`
          # recovery path (2026-04-22). Never hash-synced in-repo at the
          # time — hashes retained as placeholders for parity. Users who
          # want a verifiable pin should use "2.0.4" or build from the tag.
          linux-x64 = nixpkgs.lib.fakeHash;
          osx-x64   = nixpkgs.lib.fakeHash;
          osx-arm64 = nixpkgs.lib.fakeHash;
        };
        "2.0.4" = {
          # v2.0.4 — drop osx-x64 from matrix; FDR High fixes shipped.
          # Digests from release run 24789065975 (published 2026-04-22).
          # No osx-x64 key: platform dropped from the release matrix.
          linux-x64 = "sha256-lZKpYgsN3jdF2wtXFwja0i1qYABobnwPB2E6lq6nmOY=";
          osx-arm64 = "sha256-bDBRpKV0wJ9R95WbYZ4YeszjeykY2t2HmnnmfOfrmHQ=";
        };
        "2.0.6" = {
          # v2.0.6 — clean-lockstep release. verify-version-strings.sh
          # gate 4 green, tarball filenames match the tag version (no
          # C-1 drift from v2.0.4), so URLs use the default clean pattern
          # (tarballVersionFor returns v). v2.0.5 was queued in-tree as
          # the lockstep roll but never produced a published packaging
          # pin; v2.0.6 supersedes it. Digests captured 2026-04-22 from
          # the v2.0.6 GitHub Release. The `linux-musl-x64` and `win-x64`
          # keys are recorded here as a complete digest ledger for the
          # release even though only `linux-x64` / `osx-arm64` are wired
          # through `sourcesFor` into Nix-buildable derivations (musl
          # needs a separate sourceRoot/autoPatchelf path; win-x64 has
          # no x86_64-windows platform in nixpkgs). Nix ignores unknown
          # keys on the attrset — safe to leave as data.
          linux-x64      = "sha256-cy5CJyQ3m0DT79/DTUVc8hpWcF/a7kbsN4iXveMILDE=";
          linux-musl-x64 = "sha256-mNjW2I9wGzxyFFTi7xIdXVbcOUmPa0wz+a4PFHA0du0=";
          osx-arm64      = "sha256-V5SkFcBKEr8mrONch7xwddxVIF/F8QH1ic9/5krhPHQ=";
          win-x64        = "sha256-rfORgTvRIG1UT/gbExXVnl1shedY/VslgZ9EDmMfZt8=";
        };
      };

      # Per-version tarball-filename overrides. Default to the tag version
      # when absent. v2.0.4 tarballs were uploaded with a stale `2.0.2`
      # filename (audit finding C-1); other tags use the clean pattern.
      tarballVersionFor = v: if v == "2.0.4" then "2.0.2" else v;

      sources = sourcesFor version (tarballVersionFor version) latestHashes;
      supportedSystems = builtins.attrNames sources;

      mkAzAi = { pkgs, system, v, srcMeta }:
        pkgs.stdenv.mkDerivation {
          pname = "az-ai-v2";
          version = v;

          src = pkgs.fetchurl {
            inherit (srcMeta) url sha256;
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
            install -Dm644 LICENSE              $out/share/licenses/az-ai-v2/LICENSE
            install -Dm644 NOTICE               $out/share/licenses/az-ai-v2/NOTICE
            install -Dm644 THIRD_PARTY_NOTICES.md $out/share/licenses/az-ai-v2/THIRD_PARTY_NOTICES.md
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
    flake-utils.lib.eachSystem supportedSystems (system:
      let
        pkgs = import nixpkgs { inherit system; };

        az-ai = mkAzAi {
          inherit pkgs system;
          v = version;
          srcMeta = sources.${system};
        };

        # Versioned-pin derivations. Consumers pin via:
        #   inputs.az-ai.url = "github:SchwartzKamel/azure-openai-cli?dir=packaging/nix";
        #   environment.systemPackages = [ inputs.az-ai.packages.${pkgs.system}."az-ai-v2_2_0_0" ];
        pinnedPackages = nixpkgs.lib.mapAttrs' (v: hashes:
          nixpkgs.lib.nameValuePair
            "az-ai-v2_${builtins.replaceStrings ["."] ["_"] v}"
            (mkAzAi {
              inherit pkgs system;
              v = v;
              srcMeta = (sourcesFor v (tarballVersionFor v) hashes).${system};
            })
        ) pinnedHashes;
      in
      {
        packages = {
          default = az-ai;
          az-ai = az-ai;
        } // pinnedPackages;

        apps.default = {
          type = "app";
          program = "${az-ai}/bin/az-ai-v2";
        };
      });
}
