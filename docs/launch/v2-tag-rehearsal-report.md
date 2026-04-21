# v2.0.0 Tag-Time Packaging Rehearsal — Mr. Lippman

**Status:** rehearsal only. No tag cut. No push. No manifest edits applied.
**RID exercised:** `linux-x64` (builder's native RID).
**Outcome:** **GREEN for linux-x64 mechanics.** Cross-platform RIDs deferred to
CI/tag-time as designed. One low-severity metadata mismatch flagged for Jerry.
**Recommendation:** **GO** for real tag-time pipeline once CI cross-builds the
remaining RIDs.

---

## 1. Pipeline at a glance

```
  ┌──────────────┐   ┌──────────┐   ┌──────────┐   ┌──────────────┐   ┌─────┐   ┌────────┐
  │ dotnet       │   │ stage.sh │   │ sha256   │   │ manifest     │   │ git │   │ GitHub │
  │ publish AOT  │──▶│ tar/zip  │──▶│ digest   │──▶│ fill         │──▶│ tag │──▶│ Release│
  │ -r <rid>     │   │ + NOTICE │   │ (+ SRI)  │   │ brew/scoop/  │   │ -s  │   │ + CI   │
  │              │   │ + LICENSE│   │          │   │ nix          │   │     │   │ docker │
  └──────────────┘   └──────────┘   └──────────┘   └──────────────┘   └─────┘   └────────┘
         ▲                                                                          │
         └─────── CI cross-builds linux-arm64 / osx-* / win-x64 in parallel ◀───────┘
```

Docker/GHCR is a **separate** CI job (`.github/workflows/ci.yml` → `docker:`),
not `stage.sh`'s responsibility. Confirmed.

---

## 2. What ran green in this rehearsal

`bash packaging/tarball/stage.sh linux-x64` — **exit 0.**

| Check | Expected | Actual | Result |
|---|---|---|---|
| Artifact path | `dist/az-ai-v2-2.0.0-linux-x64.tar.gz` | same | ✅ |
| Tarball contents | `az-ai-v2`, `LICENSE`, `NOTICE`, `THIRD_PARTY_NOTICES.md`, `README.md` | all 5 present | ✅ |
| `--version --short` | exits 0, prints `2.0.0` exactly | `2.0.0` | ✅ |
| `--help` | exits 0 | exit 0 | ✅ |
| Binary type | AOT native ELF (not apphost wrapper) | `ELF 64-bit LSB pie executable, x86-64, dynamically linked, stripped, BuildID present` | ✅ (NativeAOT — dynamically linked to libc/libssl is expected; an apphost wrapper would be ~70 KB, this is 13 MB native code) |
| Binary size | ≤ 15 MB (ship target 12.91 MB) | **13 MB** | ✅ |
| NOTICE / THIRD_PARTY_NOTICES | non-empty, not placeholder | 180 + 183 lines, real package manifest | ✅ |
| ILC AOT warnings | known Azure.AI.OpenAI IL2104/IL3053 trim warnings | present, same as CI baseline | ✅ (expected) |

**Tarball SHA256 (real, rehearsal build):**

```
93a658f7c321f82232c20da7a8356314ea343e1df7e9800da3d6385c45d3aea7  dist/az-ai-v2-2.0.0-linux-x64.tar.gz
```

> Note: this digest is **build-reproducibility-dependent**. The real tag-time
> hash will differ unless the build is bit-identical (it won't be — BuildID,
> timestamps, toolchain minor versions). The value above is rehearsal-only.
> **Do not paste this into a manifest.**

---

## 3. Simulated hash-fill (NOT applied — diffs only)

All three diffs below are **pasted as proposals**, not applied to files.
At real tag time, substitute the CI-emitted digest from the uploaded artifact.

### 3a. `packaging/homebrew/Formula/az-ai.rb` — linux-x64 row only

```diff
   on_linux do
     on_intel do
       url "https://github.com/SchwartzKamel/azure-openai-cli/releases/download/v2.0.0/az-ai-v2-2.0.0-linux-x64.tar.gz"
-      sha256 "TODO_FILL_AT_RELEASE_TIME"
+      sha256 "93a658f7c321f82232c20da7a8356314ea343e1df7e9800da3d6385c45d3aea7"
     end
```

`osx-arm64` and `osx-x64` rows left on `TODO_FILL_AT_RELEASE_TIME` — this
rehearsal did **not** cross-build Darwin, so no legitimate digest exists.

### 3b. `packaging/scoop/az-ai.json` — win-x64

**Deferred.** This rehearsal did not cross-build `win-x64` (would require
`dotnet publish -r win-x64 -p:PublishAot=true`, which Microsoft supports from
Linux but still produces an `.exe` we couldn't round-trip test here — Puddy's
Windows gate is where that gets validated). At real tag time, CI emits
`az-ai-v2-2.0.0-win-x64.zip` and Lippman fills `hash` identically.

Illustrative (do not apply):

```diff
         "url": "https://github.com/SchwartzKamel/azure-openai-cli/releases/download/v2.0.0/az-ai-v2-2.0.0-win-x64.zip",
-        "hash": "TODO_FILL_AT_RELEASE_TIME"
+        "hash": "<sha256 of the CI-uploaded win-x64 zip>"
```

### 3c. `packaging/nix/flake.nix` — linux-x64 SRI

Nix wants SRI (base64) not hex. From the rehearsal tarball:

```
sha256-k6ZY98Mh+CIywg2nqDVjFOo0Ph336YANo9Y4XEXTrqc=
```

Proposed diff (rehearsal, not applied):

```diff
         "x86_64-linux" = {
           url = "${baseUrl}/az-ai-v2-${version}-linux-x64.tar.gz";
-          sha256 = nixpkgs.lib.fakeHash;
+          sha256 = "sha256-k6ZY98Mh+CIywg2nqDVjFOo0Ph336YANo9Y4XEXTrqc=";
         };
```

`x86_64-darwin` and `aarch64-darwin` stay on `lib.fakeHash` until CI emits
those tarballs.

---

## 4. Gap list

### 4.1 `stage.sh` RID coverage
`stage.sh` **accepts** `linux-x64 | linux-arm64 | osx-x64 | osx-arm64 | win-x64 | win-arm64`.
It does **not** loop over all of them — it's one-RID-per-invocation.
**Implication:** the release pipeline needs to call `stage.sh <rid>` six times
(or matrix it in CI). Current `packaging/README.md` checklist says "for each
target RID" but does not spell out the exact matrix. **Recommendation:** add a
`packaging/tarball/stage-all.sh` wrapper or bake the matrix into the release
workflow. Not a blocker — a convenience.

### 4.2 Cross-platform tooling (win-* from Linux)
`stage.sh` uses `zip -r` for `win-*` — available on a stock Linux GHA runner
(`apt install zip` or pre-installed on `ubuntu-latest`). No PowerShell
needed. **Verified by reading script.** `tar` alone is not enough because
Scoop expects `.zip`. `stage.sh` already `die`s cleanly if `zip` is missing.

### 4.3 Homebrew versioned-pin note vs reality
Formula comment says *"first pinnable version is 2.0.1."* This matches the
cutover decision (`docs/v2-cutover-decision.md` §G6) which defers versioned
pin scaffolding to 2.0.1. Consistent. **No action.**

### 4.4 Docker/GHCR
Built separately in `.github/workflows/ci.yml` (`docker:` job, line ~96).
**Not** `stage.sh`'s job. Confirmed. Release workflow will tag the image
`ghcr.io/.../azure-openai-cli:2.0.0` independently. Lippman records the
image digest in the release notes alongside the tarball digests.

### 4.5 Secret-leakage in `stage.sh`
Reviewed. Script uses:
- `set -euo pipefail` ✓
- no `set -x` ✓
- no env vars echoed ✓
- paths are all `REPO_ROOT`-relative ✓
- `dotnet publish` inherits caller env (could leak `DOTNET_*`, `NUGET_*`,
  proxy settings) — standard and unavoidable; the script itself doesn't
  log them.

**Clean.** No action.

### 4.6 ⚠️ csproj Version vs shipped version — **low-severity metadata drift**
`azureopenai-cli-v2/AzureOpenAI_CLI_V2.csproj` line 19:
```xml
<Version>2.0.0-alpha.1</Version>
```
…but `Program.cs` hardcodes `VersionSemver = "2.0.0"` and `stage.sh` hardcodes
`VERSION="2.0.0"`. The binary ships `2.0.0` (runtime behavior is correct), but
the assembly metadata claims `2.0.0-alpha.1`. This will leak into:
- `az-ai-v2.dll` assembly version (ILC will bake it into the native image)
- NuGet-style introspection tools
- SBOM generators that read assembly metadata

**Recommendation for Jerry before tag cut:** bump csproj `<Version>` to
`2.0.0` (remove the `-alpha.1` pre-release suffix) so assembly metadata,
runtime output, `stage.sh`, and the git tag all agree.

**Severity:** low — does not affect `--version --short` (which reads from
`Program.cs` constant, not assembly metadata). Not a blocker. But Lippman
doesn't ship metadata that contradicts the tag.

### 4.7 AOT trim warnings (Azure.AI.OpenAI)
IL2104 + IL3053 + two "will always throw" ILC warnings against
`Azure.AI.OpenAI.Chat.AzureChatClient.{PostfixSwapMaxTokens, PostfixClearStreamOptions}`.
These are the **known** baseline documented in
`docs/aot-trim-investigation.md`. No regression. Puddy already owns.

### 4.8 Binary linkage
`file` reports *dynamically linked* — correct for NativeAOT on Linux
(libc, libssl, libicu via `InvariantGlobalization=true` avoids ICU).
**Not** "statically linked" as the task brief suggested; the brief's
expected-string was loose. NativeAOT `pie executable … stripped, BuildID`
is the correct signature. Nix flake already handles this via
`autoPatchelfHook` + `stdenv.cc.cc.lib + zlib + icu + openssl` in
`buildInputs`. ✓

---

## 5. Deferred to real tag time (environment, not bugs)

| Item | Why deferred | Who |
|---|---|---|
| linux-arm64, linux-musl-x64, osx-x64, osx-arm64, win-x64 tarballs | This sandbox is linux-x64-only; NativeAOT cross-builds run in CI matrix | CI |
| Real tarball SHA256s for all 6 RIDs | Hashes are build-reproducibility-dependent; only CI's uploaded artifacts count | Lippman fills from CI output |
| Nix SRI hashes for darwin | No darwin build in this env | Lippman, post-CI |
| Scoop `hash` | No win-x64 round-trip here | Lippman, post-CI |
| Docker image digest | Built in parallel CI job | Jerry records → Lippman pastes into release notes |
| SBOM attach | Built in CI release workflow | Newman signs off, Lippman attaches |
| Tag signature | Requires Lippman's signing key, not present in rehearsal env | Lippman, at tag time |

---

## 6. Blockers

**None that block GO.**

One low-severity metadata fix recommended before tag cut:
csproj `<Version>2.0.0-alpha.1</Version>` → `<Version>2.0.0</Version>`
(§4.6). Can ride on the release-commit PR.

---

## 7. Hash-fill cookbook — exact commands for Lippman at tag time

Assumes the release CI workflow has uploaded the six artifacts to
`dist/` (or downloaded them from the draft release into `dist/`).

```bash
# 0. Pre-flight: confirm all six artifacts present.
cd /path/to/azure-openai-cli
ls -1 dist/az-ai-v2-2.0.0-*.{tar.gz,zip} 2>/dev/null
# Expect exactly 6 files: linux-x64, linux-arm64 (if shipping), osx-x64, osx-arm64, win-x64 (zip).

# 1. Compute all digests (hex for brew/scoop, SRI for nix).
for f in dist/az-ai-v2-2.0.0-*.tar.gz dist/az-ai-v2-2.0.0-*.zip; do
    [[ -f "$f" ]] || continue
    HEX=$(sha256sum "$f" | awk '{print $1}')
    SRI="sha256-$(printf '%s' "$HEX" | xxd -r -p | base64)"
    printf '%-55s  hex=%s\n  sri=%s\n\n' "$(basename "$f")" "$HEX" "$SRI"
done | tee dist/digests.txt
```

### 7a. Homebrew (`packaging/homebrew/Formula/az-ai.rb`)

Three `TODO_FILL_AT_RELEASE_TIME` lines, in order: `osx-arm64`, `osx-x64`,
`linux-x64`. **Do it by hand** — `sed` is tempting but risky when the
placeholder is identical on three lines and order matters.

```bash
# Open the file in your editor and replace each TODO_FILL_AT_RELEASE_TIME
# with the hex digest matching the URL on the line immediately above it.
${EDITOR:-vi} packaging/homebrew/Formula/az-ai.rb

# Verify no placeholders remain.
! grep -n TODO_FILL_AT_RELEASE_TIME packaging/homebrew/Formula/az-ai.rb
```

### 7b. Scoop (`packaging/scoop/az-ai.json`)

Single `hash` field for win-x64 — `sed` is safe here (placeholder is unique).

```bash
WIN_HEX=$(sha256sum dist/az-ai-v2-2.0.0-win-x64.zip | awk '{print $1}')
sed -i "s/TODO_FILL_AT_RELEASE_TIME/${WIN_HEX}/" packaging/scoop/az-ai.json
! grep TODO_FILL_AT_RELEASE_TIME packaging/scoop/az-ai.json
# Remove the _comment_hash line manually once satisfied, or leave it — Scoop ignores underscore keys.
```

### 7c. Nix (`packaging/nix/flake.nix`)

Three `nixpkgs.lib.fakeHash` occurrences, in file order: `x86_64-linux`,
`x86_64-darwin`, `aarch64-darwin`.

```bash
# Build the three SRI values.
LINUX_SRI="sha256-$(sha256sum dist/az-ai-v2-2.0.0-linux-x64.tar.gz | awk '{print $1}' | xxd -r -p | base64)"
OSXX_SRI="sha256-$(sha256sum dist/az-ai-v2-2.0.0-osx-x64.tar.gz    | awk '{print $1}' | xxd -r -p | base64)"
OSXA_SRI="sha256-$(sha256sum dist/az-ai-v2-2.0.0-osx-arm64.tar.gz  | awk '{print $1}' | xxd -r -p | base64)"

# Replace by hand (three identical fakeHash tokens — order-sensitive).
${EDITOR:-vi} packaging/nix/flake.nix
#   x86_64-linux   -> "$LINUX_SRI"
#   x86_64-darwin  -> "$OSXX_SRI"
#   aarch64-darwin -> "$OSXA_SRI"

! grep fakeHash packaging/nix/flake.nix

# Belt-and-braces: Nix will itself reject wrong hashes at `nix build` time.
( cd packaging/nix && nix build .#default --no-link ) || echo "Nix hash mismatch — re-check."
```

### 7d. Commit, verify, tag

```bash
git add packaging/homebrew/Formula/az-ai.rb \
        packaging/scoop/az-ai.json \
        packaging/nix/flake.nix \
        CHANGELOG.md docs/release-notes-v2.0.0.md
git commit -m "release: v2.0.0 manifest digests + release notes"

# Final local checks.
grep -r TODO_FILL_AT_RELEASE_TIME packaging/ && echo "STOP — placeholders remain." && exit 1
grep -r fakeHash packaging/nix/ && echo "STOP — Nix placeholders remain." && exit 1

# Sign + push tag.
git tag -s v2.0.0 -m "az-ai v2.0.0"
git push origin main
git push origin v2.0.0
```

### 7e. Post-tag checklist (do not skip)

- [ ] GitHub Actions release workflow green on `v2.0.0`
- [ ] Six artifacts + SBOM + signatures attached to GitHub Release
- [ ] `ghcr.io/schwartzkamel/azure-openai-cli:2.0.0` digest recorded in release
      notes (Frank Costanza mirrors to ops doc)
- [ ] `ghcr.io/...:latest` re-tagged to the `:2.0.0` digest
- [ ] `ghcr.io/...:1.9.1` digest set aside for rollback (per
      `docs/v2-cutover-checklist.md` §3.1)
- [ ] Puddy signs off: fresh-machine install on macOS + Windows + Linux
- [ ] Newman signs off: SBOM + image scan clean
- [ ] Jackie signs off: NOTICE/THIRD_PARTY_NOTICES.md bundled in every
      artifact (this rehearsal confirmed the mechanism works)
- [ ] Elaine signs off: README version refs + migration doc land on `main`

---

## 8. Go / No-go

**GO** for the real tag-time pipeline, conditioned on:

1. CI produces the remaining 5 RIDs cleanly (linux-arm64 optional per
   `packaging/README.md`; if omitted, drop from the checklist — don't fake it).
2. Jerry bumps csproj `<Version>` from `2.0.0-alpha.1` to `2.0.0` before
   the release commit (§4.6).
3. Lippman runs §7 cookbook against CI-uploaded artifacts — not against the
   rehearsal digests in this report.

We're going to press. But not tonight.

— Mr. Lippman
