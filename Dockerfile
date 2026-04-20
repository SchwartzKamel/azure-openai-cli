# ---------- Stage 1: Build ----------
# Pinned to a specific SHA256 digest for reproducible builds.
# Dependabot/Renovate can bump these via the `dotnet/sdk:10.0` tag hint.
# To refresh manually:
#   docker buildx imagetools inspect mcr.microsoft.com/dotnet/sdk:10.0 | grep Digest
FROM mcr.microsoft.com/dotnet/sdk:10.0@sha256:adc02be8b87957d07208a4a3e51775935b33bad3317de8c45b1e67357b4c073b AS build
WORKDIR /src

# Copy only .csproj first for restore-layer caching
COPY azureopenai-cli/AzureOpenAI_CLI.csproj ./
RUN dotnet restore ./AzureOpenAI_CLI.csproj

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

# Drop privileges for runtime
USER appuser

# Credentials should be injected at runtime, never baked into the image:
#   docker run --rm --env-file .env azureopenai-cli "your prompt"
#   docker run --rm -v /path/to/.env:/app/.env:ro azureopenai-cli "your prompt"
ENTRYPOINT ["./AzureOpenAI_CLI"]

