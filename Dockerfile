# =============================================================================
# Multi-stage Dockerfile — v1 (azureopenai-cli/)
# =============================================================================
# Two stages, strictly separated:
#
#   Stage 1 (build):   mcr.microsoft.com/dotnet/sdk:10.0 (Debian-based, ~850 MB)
#                      — full .NET 10 SDK, crossgen2, restore + publish toolchain.
#                      Only needed to PRODUCE the single-file self-contained
#                      binary. Never shipped to users.
#
#   Stage 2 (runtime): mcr.microsoft.com/dotnet/runtime-deps:10.0-alpine (~12 MB)
#                      — Alpine + minimum libs (icu-libs) to exec a
#                      self-contained .NET 10 app. No SDK, no NuGet, no shell
#                      tooling beyond BusyBox. This is what ships.
#
# Why self-contained + single-file PublishReadyToRun?
#   - Self-contained: the .NET runtime is bundled into the binary, so the
#     runtime image doesn't need `dotnet` installed — `runtime-deps` suffices.
#   - Single-file: one artifact to `COPY` and one to exec; simpler layer,
#     simpler ENTRYPOINT.
#   - PublishReadyToRun: pre-JITs hot methods for ~100 ms startup vs ~400 ms
#     cold JIT. The per-invocation cost matters for CLI workflows (Espanso,
#     AHK) that spawn a fresh process on every key sequence.
#
# Why Alpine (musl) for runtime?
#   Attack-surface reduction: Alpine's runtime-deps is ~12 MB vs ~200 MB for
#   the Debian runtime-deps image. The corresponding `-r linux-musl-x64`
#   publish RID matches the runtime libc; mixing glibc publish with musl
#   runtime would SIGSEGV on startup. Newman owns the hardening posture; see
#   docs/security/hardening-checklist.md for the full rationale.
#
# Layer ordering (cache efficiency — order matters):
#   1. FROM (base image, pinned digest — invalidates only on Dependabot bump)
#   2. WORKDIR
#   3. COPY *.csproj (project metadata only)   ← restore layer boundary
#   4. RUN dotnet restore                       ← reused across source edits
#   5. COPY source                              ← invalidates on every code edit
#   6. RUN dotnet publish                       ← rebuilds on source edits only
#   Changing step 5 (a .cs edit) does NOT invalidate steps 1–4. Changing
#   step 3 (adding a NuGet dep) invalidates 4 but not 1–2.
#
# Base-image pinning policy:
#   Both FROMs are pinned to an immutable @sha256 digest, not just a tag.
#   "latest" (and even stable tags like `10.0-alpine`) can float underneath
#   us; a pinned digest is reproducible until we explicitly bump. Dependabot
#   is configured to open digest-bump PRs on a monthly cadence.
#
# To refresh a digest manually:
#   docker buildx imagetools inspect mcr.microsoft.com/dotnet/sdk:10.0 | grep Digest
#   docker buildx imagetools inspect mcr.microsoft.com/dotnet/runtime-deps:10.0-alpine | grep Digest
# =============================================================================

# ---------- Stage 1: Build ----------
# Pinned to a specific SHA256 digest for reproducible builds.
# Dependabot/Renovate can bump these via the `dotnet/sdk:10.0` tag hint.
# To refresh manually:
#   docker buildx imagetools inspect mcr.microsoft.com/dotnet/sdk:10.0 | grep Digest
FROM mcr.microsoft.com/dotnet/sdk:10.0@sha256:adc02be8b87957d07208a4a3e51775935b33bad3317de8c45b1e67357b4c073b AS build
WORKDIR /src

# Copy only .csproj first for restore-layer caching
COPY azureopenai-cli/AzureOpenAI_CLI.csproj ./
# Restore must match publish flags so --no-restore below succeeds:
#  - -r linux-musl-x64: avoids NETSDK1047 (RID-agnostic assets vs RID publish)
#  - PublishReadyToRun=true at restore time: avoids NETSDK1094 (R2R needs the
#    crossgen2 runtime package pulled during restore)
RUN dotnet restore ./AzureOpenAI_CLI.csproj \
        -r linux-musl-x64 \
        /p:PublishReadyToRun=true

# Then copy source (changes here don't invalidate the restore cache)
COPY azureopenai-cli/ ./

ENV LANG=en_US.UTF-8 \
    LC_ALL=en_US.UTF-8

RUN dotnet publish ./AzureOpenAI_CLI.csproj \
        --no-restore \
        -c Release \
        -r linux-musl-x64 \
        --self-contained true \
        /p:PublishSingleFile=true \
        /p:PublishTrimmed=true \
        /p:PublishReadyToRun=true \
        /p:IncludeAllContentForSelfExtract=true \
        -o /app
# Changed runtime identifier to linux-musl-x64 for Alpine compatibility

# ---------- Stage 2: Runtime ----------
# Pinned to a specific SHA256 digest for reproducible builds.
# To refresh manually:
#   docker buildx imagetools inspect mcr.microsoft.com/dotnet/runtime-deps:10.0-alpine | grep Digest
FROM mcr.microsoft.com/dotnet/runtime-deps:10.0-alpine@sha256:f8a0978d56136514d1d2f9c893a8797eb47d42f6522da7b8d1b2fcdc51e95198 AS runtime
# Switched to Alpine variant to drastically reduce attack surface

# OCI image metadata (see docs/licensing-audit.md)
LABEL org.opencontainers.image.licenses="MIT" \
      org.opencontainers.image.source="https://github.com/SchwartzKamel/azure-openai-cli" \
      org.opencontainers.image.documentation="https://github.com/SchwartzKamel/azure-openai-cli/blob/main/docs/licensing-audit.md"

WORKDIR /app

# HEALTHCHECK is not applicable for CLI tools that run and exit.
# This container is intended to be invoked via `docker run`, not kept running.

# Install runtime dependencies first so these layers are cached unless deps change
RUN apk add --no-cache \
    icu-libs \
 && apk upgrade --no-cache \
 && rm -rf /var/cache/apk/*

# Create non-root user and set permissions in one layer
RUN addgroup --system appgroup \
 && adduser --system --ingroup appgroup appuser \
 && mkdir -p /opt/config \
 && chown -R appuser:appgroup /app /opt/config

ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false
# Set DOTNET_BUNDLE_EXTRACT_BASE_DIR to a writable location for non-root user
ENV DOTNET_BUNDLE_EXTRACT_BASE_DIR=/tmp/dotnet_bundle
RUN mkdir -p /tmp/dotnet_bundle && chown -R appuser:appgroup /tmp/dotnet_bundle

# Copy published self-contained single-file binary last (changes most often)
COPY --from=build /app/AzureOpenAI_CLI /app/AzureOpenAI_CLI
RUN chmod +x /app/AzureOpenAI_CLI

# Bundle license + third-party attribution notices into the image so redistribution
# complies with MIT and the transitive dependency attribution requirements.
COPY LICENSE NOTICE THIRD_PARTY_NOTICES.md /licenses/

# Drop privileges for runtime
USER appuser

# Credentials should be injected at runtime, never baked into the image:
#   docker run --rm --env-file .env azureopenai-cli "your prompt"
#   docker run --rm -v /path/to/.env:/app/.env:ro azureopenai-cli "your prompt"
ENTRYPOINT ["./AzureOpenAI_CLI"]

