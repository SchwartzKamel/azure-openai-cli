# v2.0.0 Release Attempt #1 — Diagnostic (NO-GO)

**Status:** :red_circle: **FAILED.** Release not published. No manifests touched.
**Run:** <https://github.com/SchwartzKamel/azure-openai-cli/actions/runs/24736776551>
**Tag:** `v2.0.0` → commit `b1fd2cd` (immutable; **do not re-tag**).
**Decision owner:** repo maintainer. Lippman recommends **patch-forward to v2.0.1** from `main` after fixes land.

---

## Job matrix outcome

| Job                                              | Conclusion   |
|--------------------------------------------------|--------------|
| `ci / build-and-test (ubuntu-latest)`            | ✅ success   |
| `ci / build-and-test (macos-latest)`             | ✅ success   |
| `ci / integration-test`                          | ✅ success   |
| `ci / docker`                                    | ✅ success   |
| `build-binaries-v2 (linux-x64)`                  | ✅ success   |
| `build-binaries-v2 (linux-musl-x64)`             | ✅ success   |
| `build-binaries-v2 (osx-arm64)`                  | ✅ success   |
| `build-binaries-v2 (osx-x64)`                    | (cancelled — matrix failed fast) |
| `build-binaries-v2 (win-x64)`                    | ❌ **failure** |
| `docker-publish-v2`                              | ❌ **failure** |
| `release-v2`                                     | (skipped — needs failed)         |
| v1 jobs (`build-binaries`, `docker-publish`, `release`) | skipped (correct — tag is v2.*) |

Surprise of the rehearsal: the flagged-risk leg (`linux-musl-x64` AOT
cross-build) **passed**. The two failures came from elsewhere.

---

## Failure #1 — `build-binaries-v2 (win-x64)`

**Symptom (from job log, 17:36:15Z):**

```text
>> Creating /d/a/azure-openai-cli/azure-openai-cli/dist/az-ai-v2-2.0.0-win-x64.zip
stage.sh: error: 'zip' not on PATH — required to stage Windows artifact
##[error]Process completed with exit code 1.
```

**Root cause:** `packaging/tarball/stage.sh` line 73–74 requires the
Info-ZIP `zip` CLI to be on PATH on the Windows runner. GitHub's
`windows-latest` image does NOT ship `zip` by default (the MSYS2 /
Git-for-Windows bash that runs the script also doesn't include it).

The AOT publish + stage steps all succeeded — the binary is sitting at
`D:\a\azure-openai-cli\azure-openai-cli\artifacts\stage\az-ai-v2-2.0.0-win-x64\`.
Only the zip packaging step fails.

**Fix options (for the v2.0.1 patch):**

1. **Preferred — use PowerShell `Compress-Archive` on `win-*` RIDs.**
   Replace the `zip -r` invocation in `stage.sh` with a `win-*` branch
   that shells out to `powershell.exe -NoProfile -Command
   "Compress-Archive -Path ... -DestinationPath ..."`. `Compress-Archive`
   ships with PowerShell and is always on `windows-latest`.
2. **Alternative — install zip in the workflow.** Add a pre-stage step
   on win-x64:
   `choco install zip -y` or `pacman -S --noconfirm zip` (via the bundled
   Git bash's pacman if available). Less portable, more surface area.
3. **Alternative — switch to `7z`.** `7z.exe` IS on `windows-latest`.
   Branch `stage.sh` to prefer `7z a -tzip` when `zip` is missing.

Lippman picks (1). Zero new dependencies, zero PATH gymnastics,
deterministic across runners.

---

## Failure #2 — `docker-publish-v2`

**Symptom (from job log, 17:35:08Z):**

```text
#19 DONE 20.3s                              ← dotnet publish succeeded
#20 [runtime 5/7] COPY --from=build /app/az-ai-v2 /app/az-ai-v2
#20 ERROR: failed to calculate checksum of ref ...: "/app/az-ai-v2": not found
ERROR: failed to build: failed to solve: failed to compute cache key: ...
```

**Root cause (suspected):** NativeAOT cross-compile to `linux-musl-x64`
from a **glibc** SDK base image (`mcr.microsoft.com/dotnet/sdk:10.0`,
Debian) without the musl cross-link toolchain. The publish reports
success (`AzureOpenAI_CLI_V2 -> /app/`) but does not emit a musl-linked
ELF at `/app/az-ai-v2` — ILC silently falls back or the output goes
somewhere else (likely `/app/az-ai-v2.dll` + no native host, because the
ObjWriter emitted an object file the linker couldn't finish).

Evidence: the `stage.sh` path — same `dotnet publish` invocation on a
native `ubuntu-latest` runner for `linux-musl-x64` — **succeeded.** The
difference is the host toolchain:

| Build path                               | Host libc | Result  |
|------------------------------------------|-----------|---------|
| `stage.sh` on `ubuntu-latest`            | glibc     | ✅ (matrix passed) |
| `Dockerfile.v2` on glibc SDK image       | glibc     | ❌ (this failure) |

Both cross-compile to musl from glibc, so the delta is likely in the
installed packages. `stage.sh`'s runner has a richer default toolchain
(build-essential, full binutils); the SDK image only adds `clang` +
`zlib1g-dev`. **Suspected missing piece:** the `musl-tools` /
`musl-dev` cross-link libraries, or a `--self-contained true` +
`-p:LinkerFlavor=lld` directive the SDK container needs to link against
musl stubs.

**Fix hypothesis (for the v2.0.1 patch):** either
(a) install `musl-tools` in the `build` stage (`apt-get install -y
musl-tools`), OR
(b) switch `Dockerfile.v2` to base its build stage on
`mcr.microsoft.com/dotnet/sdk:10.0-alpine`, so host + target are both
musl and no cross-link is needed. Option (b) is structurally cleaner.

**Non-blocking for the release itself:** `docker-publish-v2` is
independent of binary publishing, so when the binary legs are green it
can be re-run via `workflow_dispatch` without a re-tag. But because
`build-binaries-v2 (win-x64)` ALSO failed, the whole release is a no-go
regardless.

**This failure does NOT impact Bob Sacamano's homebrew/scoop/nix work.**
Those manifests reference the GitHub Release tarballs directly — not the
GHCR image. The GHCR image is a separate distribution channel.

---

## What was NOT done

- **No packaging manifest was touched.** `packaging/homebrew/Formula/az-ai-v2@2.0.0.rb`,
  `packaging/scoop/versions/az-ai-v2@2.0.0.json`, and `packaging/nix/flake.nix`
  still carry their `TODO_FILL_AT_RELEASE_TIME` / `fakeHash` sentinels. Correct
  state — there is no release for them to reference yet.
- **No commit, no push, no re-tag.** Per playbook §1 / §6, `v2.0.0` is
  immutable.
- **No GitHub Release was published** (the `release-v2` job never ran).
- **Bob Sacamano is still blocked** on the hash-sync commit, which will
  not land until v2.0.1 (or whatever fix-forward tag) publishes cleanly.

---

## Recommended path forward

1. Patch `packaging/tarball/stage.sh` to use `Compress-Archive` on
   `win-*` RIDs (Failure #1).
2. Patch `Dockerfile.v2` — either install `musl-tools` or switch to the
   `sdk:10.0-alpine` base (Failure #2). Prefer alpine base.
3. Dry-run both fixes locally (`stage.sh win-x64` needs a Windows host;
   mock verify on CI via a branch push with a `workflow_dispatch` on
   a preview of `release.yml`).
4. Bump `azureopenai-cli-v2/AzureOpenAI_CLI_V2.csproj` `<Version>` →
   `2.0.1`, same for `stage.sh` `VERSION=`, update `CHANGELOG.md`,
   create `packaging/homebrew/Formula/az-ai-v2@2.0.1.rb`,
   `packaging/scoop/versions/az-ai-v2@2.0.1.json`,
   and add `"2.0.1"` entry to `packaging/nix/flake.nix` `pinnedHashes`.
5. Tag `v2.0.1`, push, monitor, hash-sync.

The v2.0.0 tag stays as a permanent "attempted release" marker. GitHub
supports marking a tag as "not latest" after v2.0.1 publishes — use it.

— Mr. Lippman, on deadline
