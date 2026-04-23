# Docker Image Hardening

> *The image is the primary distribution surface. Every CVE in a base
> layer is our problem.* -- Newman, S02E14 *The Container*.

**Status:** Canonical
**Last updated:** 2026-04 (S02E14)
**Audience:** maintainers cutting releases, downstream operators
auditing the image, security reviewers running the
[`hardening-checklist`](../security/hardening-checklist.md).

This page is the one-pager for the hardening posture of `Dockerfile`
(v1, the shipping image). It explains *what* is hardened, *why* each
choice was made, and *how* to verify the posture has not regressed.

## Posture summary

| Layer        | Choice                                                   | Why                                          |
|--------------|----------------------------------------------------------|----------------------------------------------|
| Base (build) | `mcr.microsoft.com/dotnet/sdk:10.0` digest-pinned        | Reproducible builds; Dependabot-tracked      |
| Base (run)   | `mcr.microsoft.com/dotnet/runtime-deps:10.0-alpine` digest-pinned | ~12 MB attack surface vs ~200 MB Debian |
| User         | `appuser:appgroup` UID/GID `10001:10001` (numeric)       | K8s `runAsNonRoot`, PSA `restricted` profile |
| Privileges   | `USER 10001:10001` before `ENTRYPOINT`                   | Drop root for runtime                        |
| Filesystem   | Binary `0755`, licenses `0444`, owned by `appuser`       | Least-privilege; user cannot self-modify     |
| Build tools  | None in final image (multi-stage)                        | SDK never shipped                            |
| Apk packages | `icu-libs` only, then `apk upgrade`, then cache wiped    | Smallest viable runtime; CVE catch-up        |
| Healthcheck  | `HEALTHCHECK NONE` (explicit)                            | CLI exits per-invocation; no daemon to probe |
| Telemetry    | `DOTNET_CLI_TELEMETRY_OPTOUT=1`, `DOTNET_NOLOGO=1`       | No phone-home from inside the image          |
| Secrets      | None baked; `.env` blocked via `.dockerignore`           | Credentials are runtime-only                 |
| Build context| `tests/`, `docs/`, `.github/`, `archive/` etc. excluded  | Smaller context, smaller blast radius        |

## Each hardening choice, with rationale

### 1. Pin base images by digest, not tag

Both `FROM` lines use `image:tag@sha256:<64-hex>` form. A tag like
`10.0-alpine` floats: Microsoft re-tags it whenever the underlying
Alpine snapshot is rebuilt, which means a `docker build` today and one
tomorrow can pull different bytes for the "same" Dockerfile. Digest
pinning makes the base layer immutable until we explicitly bump it.

**To refresh the digests:**

```bash
docker buildx imagetools inspect mcr.microsoft.com/dotnet/sdk:10.0 | grep Digest
docker buildx imagetools inspect mcr.microsoft.com/dotnet/runtime-deps:10.0-alpine | grep Digest
```

Update both `FROM ...@sha256:...` lines and re-run the Trivy scan to
confirm no new HIGH/CRITICAL CVEs appeared in the bump. Dependabot is
configured to open monthly digest-bump PRs.

### 2. Run as non-root with explicit numeric UID/GID

`addgroup --gid 10001` and `adduser --uid 10001` give a deterministic,
auditable identity. `USER 10001:10001` (numeric form) lets Kubernetes
`runAsNonRoot: true` and Pod Security Admission `restricted` profile
verify the user without resolving `/etc/passwd` inside the container.
The `--no-create-home` and `--shell /sbin/nologin` flags are
defense-in-depth: no home directory to mount surprises into, no
interactive shell if an attacker pops the process.

UID `10001` is above both the Alpine system-user range (<1000) and the
typical host-user range (1000-9999), so a host-mounted volume from a
developer workstation will not accidentally collide with the container
user.

### 3. Drop build tooling at the stage boundary

The build stage uses the full .NET SDK (~850 MB, includes `crossgen2`,
NuGet, MSBuild). The runtime stage is `runtime-deps:10.0-alpine`
(~12 MB) and inherits **nothing** from the build stage except the
single `COPY --from=build` line that pulls the published binary. There
is no `dotnet`, no `nuget`, no `msbuild`, no compiler in the shipped
image. Verify with:

```bash
docker run --rm --entrypoint sh azure-openai-cli:test -c 'command -v dotnet || echo absent'
# expect: absent
```

### 4. Minimize the apk surface and catch up CVEs

Only `icu-libs` is installed (required by `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false`).
`apk upgrade --no-cache` runs immediately after to pick up any CVE
fixes published to the Alpine main repo since the base image was
snapshotted -- without bumping the pinned digest. Cache directories
(`/var/cache/apk/*`, `/tmp/*`, `/var/tmp/*`) are wiped in the same
RUN to keep the layer thin.

### 5. `COPY --chown --chmod` over `RUN chown && chmod`

Doing chown/chmod in the COPY directive avoids a separate `RUN` layer
that would duplicate the binary's bytes on disk (chmod rewrites the
file's metadata in a new layer, which the storage driver materializes
as a full copy of the underlying inode in many cases). One layer,
correct ownership, correct mode, half the disk footprint for the hot
path.

### 6. `HEALTHCHECK NONE` is intentional

This image runs as `docker run --rm azure-openai-cli "your prompt"`
and exits within seconds. A `HEALTHCHECK CMD` would either fire after
the process has already exited (always unhealthy) or spawn a child
`dotnet --info` per interval (wasted CPU, no signal). Explicit `NONE`
overrides any HEALTHCHECK that a future base-image bump might
introduce, so the behavior stays declarative and audit-grade.

If a downstream consumer wraps this image in a long-running shell
(e.g., `docker run -d ... tail -f /dev/null` for debugging), they own
the healthcheck contract for that derived image.

### 7. License + attribution bundled, read-only

`LICENSE`, `NOTICE`, and `THIRD_PARTY_NOTICES.md` are copied into
`/licenses/` with mode `0444` (read-only for everyone, including the
runtime user). MIT redistribution and transitive-dependency
attribution are satisfied without giving the runtime user any reason
or means to mutate them.

### 8. Build context shrunk via `.dockerignore`

The build context excludes `tests/`, `docs/`, `*.md`, `.github/`,
`archive/`, `audits/`, `benchmarks/`, `demos/`, `launch/`,
`announce/`, `talks/`, `perf/`, `dist/`, `coverage/`, `.git/`,
`Makefile`, and `Dockerfile*` itself. Two wins:

- **Smaller upload to the daemon** -- faster `docker build`, less I/O.
- **Smaller blast radius** -- a stray `COPY .` (which we do not use,
  but a future contributor might) cannot accidentally bake test
  fixtures, audit notes, or `.git` history into the image.

`.env` and `.env.*` are also excluded; credentials must enter at
runtime via `--env-file` or `-e`, never via the build context. Three
allowlist exceptions (`!LICENSE`, `!NOTICE`, `!THIRD_PARTY_NOTICES.md`)
let the legal-attribution copy succeed despite the broad `*.md` block.

## How to verify the posture

Run all five checks before cutting a release. Any failure is a
release-blocker.

### 1. Build the image

```bash
docker build -t azure-openai-cli:audit .
```

Build must succeed against the pinned digests. If the digest moved
(network issue, registry pull failure), do **not** "fix" it by
removing the `@sha256:` -- root-cause the registry instead.

### 2. Confirm non-root execution

```bash
docker run --rm --entrypoint id azure-openai-cli:audit
# expect: uid=10001(appuser) gid=10001(appgroup) groups=10001(appgroup)
```

### 3. Confirm no build tools in the final image

```bash
docker run --rm --entrypoint sh azure-openai-cli:audit -c \
  'command -v dotnet; command -v nuget; command -v msbuild; command -v cc; command -v apk' \
  || echo "all absent (expected)"
```

`apk` will be present (Alpine package manager); the rest must be
absent.

### 4. Run Trivy locally

```bash
trivy image --severity CRITICAL,HIGH --exit-code 1 azure-openai-cli:audit
```

This mirrors what CI runs (with the caveat that CI's `exit-code: 0`
is currently advisory -- see the finding logged in S02E14).
HIGH/CRITICAL findings here mean the image is not release-quality.

### 5. Confirm image size is in the expected band

```bash
docker images --format '{{.Size}}' azure-openai-cli:audit
```

Expected: **~75-95 MB** as of Apr 2026 (~12 MB Alpine base + ~70 MB
self-contained .NET 10 single-file binary with R2R + ICU). A jump
above 100 MB warrants investigation: did a build tool leak into the
final stage, did `apk` pull a fat dependency, did a license bundle
balloon?

## Dependabot + bump cadence

- Base-image digests: monthly Dependabot digest-bump PRs.
- Out-of-band CVE: a HIGH/CRITICAL Trivy finding triggers a manual
  bump within 7 days regardless of the monthly cadence.
- The .NET 10 channel is GA; LTS handoffs are tracked separately by
  Jerry in `docs/dependencies/dotnet-channel-policy.md` (TBD).

## Cross-references

- [`docs/security/hardening-checklist.md`](../security/hardening-checklist.md)
  -- one-page rollup with check-boxes; this doc is the rationale.
- [`docs/security/scanners.md`](../security/scanners.md) -- Trivy vs
  Grype, why Trivy is authoritative.
- [`docs/security/v2-audit.md`](../security/v2-audit.md) -- v2 audit
  that explicitly deferred container review to S02E14.
- [`docs/exec-reports/s02e13-the-inspector.md`](../exec-reports/s02e13-the-inspector.md)
  -- v1 audit that explicitly deferred container review to S02E14.
- [`docs/exec-reports/s02e14-the-container.md`](../exec-reports/s02e14-the-container.md)
  -- the episode that produced this hardening.
- `Dockerfile` -- the source of truth; comments inline cite this doc.
- `.dockerignore` -- the build-context allowlist.
- `.github/workflows/ci.yml` -- the `docker` job + Trivy scan.
