# syntax=docker/dockerfile:1.7
#
# Dockerfile — hopper container image (NativeAOT, Alpine-based).
#
# Build locally:
#   docker build -t hopper:dev .
#   docker run --rm --env-file .env hopper:dev --help
#
# Binary: /app/hopper (matches AssemblyName in
# azureopenai-cli/AzureOpenAI_CLI.csproj).

# ---------- Stage 1: Build ----------
# Alpine SDK image. NativeAOT requires host libc (musl) and target RID
# (linux-musl-x64) to match — no cross-link, no silent-emit trap.
# Dependabot/Renovate bumps `dotnet/sdk:10.0-alpine` via the tag hint.
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine@sha256:732cd42c6f659814c9804ad7b05c7f761e83ef8379c5b2fdc3af673353caff73 AS build
WORKDIR /src

# NativeAOT on musl needs clang + build-base (binutils/make) + zlib headers
# for ILC to link. Alpine equivalents of the Debian clang/zlib1g-dev set.
# `file` is added for the post-publish ELF verification gate below — without
# it the silent managed-fallback regression (v2.0.0/v2.0.1) can't be
# detected inside this build stage.
RUN apk add --no-cache \
        clang \
        build-base \
        zlib-dev \
        file

# Copy full source before publish. A single `dotnet publish` invocation
# resolves the correct AOT asset graph in one shot. Cache invalidation
# cost is ~2s warm — worth it.
COPY azureopenai-cli/ ./azureopenai-cli/

ENV LANG=en_US.UTF-8 \
    LC_ALL=en_US.UTF-8

# NativeAOT publish. PublishAot=true is already set in the csproj; we pass
# it explicitly so this Dockerfile documents the contract even if the
# csproj drifts. InvariantGlobalization=true is also on in the csproj and
# is why the runtime image does NOT need icu-libs. Restore happens
# implicitly here with the AOT asset graph — do NOT re-introduce
# `--no-restore` + a separate `dotnet restore` step.
RUN dotnet publish ./azureopenai-cli/AzureOpenAI_CLI.csproj \
        -c Release \
        -r linux-musl-x64 \
        -p:PublishAot=true \
        -o /app

# Verification gates (Lippman Round-2 playbook — non-negotiable).
# These three RUN lines guarantee that a silent managed-fallback
# (publish exits 0 but emits only hopper.dll) surfaces as a red build
# HERE, not as a cryptic COPY miss in the runtime stage. If any of these
# fails, the publish step above has regressed to framework-dependent
# emission — fix there, do not weaken the gates.
RUN test -f /app/hopper || (echo "ERROR: NativeAOT ELF /app/hopper not found — AOT silently fell back to managed publish?" && ls -la /app/ && exit 1)
RUN file /app/hopper | grep -q 'ELF' || (echo "ERROR: /app/hopper is not an ELF binary" && file /app/hopper && exit 1)
RUN /app/hopper --version 2>&1 | head -1

# ---------- Stage 2: Runtime ----------
# Pinned to match runtime-deps digest.
FROM mcr.microsoft.com/dotnet/runtime-deps:10.0-alpine@sha256:f8a0978d56136514d1d2f9c893a8797eb47d42f6522da7b8d1b2fcdc51e95198 AS runtime

# OCI image metadata.
LABEL org.opencontainers.image.licenses="MIT" \
      org.opencontainers.image.source="https://github.com/SchwartzKamel/hopper" \
      org.opencontainers.image.documentation="https://github.com/SchwartzKamel/hopper/blob/main/docs/licensing-audit.md" \
      org.opencontainers.image.title="hopper" \
      org.opencontainers.image.description="hopper (formerly az-ai / Azure OpenAI CLI) — pipe a prompt in, hop to any model, get plain text back. AOT agent with Microsoft Agent Framework, personas/squads, cost estimator"

WORKDIR /app

# NativeAOT + InvariantGlobalization=true means no icu-libs required.
# Only `apk upgrade` for latest CVE fixes on top of the pinned base.
RUN apk upgrade --no-cache \
 && rm -rf /var/cache/apk/*

# Create non-root user and prepare writable config dir.
RUN addgroup --system appgroup \
 && adduser --system --ingroup appgroup appuser \
 && mkdir -p /opt/config \
 && chown -R appuser:appgroup /app /opt/config

# NativeAOT binary is a self-contained native ELF; no DOTNET_BUNDLE_EXTRACT_BASE_DIR
# shenanigans required (no apphost, no bundle extract).

COPY --from=build /app/hopper /app/hopper
RUN chmod +x /app/hopper
# The old command name keeps working inside the image too (FR-024, ADR-016).
RUN ln -s hopper /app/az-ai

# Bundle license + third-party attribution notices into the image so
# redistribution complies with MIT + transitive attribution requirements.
# Matches the tarball bundle contract stage.sh enforces.
COPY LICENSE NOTICE THIRD_PARTY_NOTICES.md /licenses/

USER appuser

# Credentials injected at runtime, never baked in:
#   docker run --rm --env-file .env ghcr.io/schwartzkamel/azure-openai-cli "your prompt"
ENTRYPOINT ["/app/hopper"]
