# ---------- Stage 1: Build ----------
# For production deployments, pin images to specific SHA256 digests
# e.g. mcr.microsoft.com/dotnet/sdk:9.0@sha256:<digest>
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy only .NET project files for restore caching
COPY *.sln ./
COPY azureopenai-cli/ ./
RUN dotnet restore ./AzureOpenAI_CLI.csproj

ENV LANG=en_US.UTF-8 \
    LC_ALL=en_US.UTF-8

RUN dotnet publish ./AzureOpenAI_CLI.csproj \
        -c Release \
        -r linux-musl-x64 \
        --self-contained true \
        /p:PublishSingleFile=true \
        /p:PublishTrimmed=true \
        /p:IncludeAllContentForSelfExtract=true \
        -o /app
# Changed runtime identifier to linux-musl-x64 for Alpine compatibility

# ---------- Stage 2: Runtime ----------
# For production deployments, pin images to specific SHA256 digests
FROM mcr.microsoft.com/dotnet/runtime-deps:9.0-alpine AS runtime
# Switched to Alpine variant to drastically reduce attack surface

WORKDIR /app

# HEALTHCHECK is not applicable for CLI tools that run and exit.
# This container is intended to be invoked via `docker run`, not kept running.

# Credentials should be injected at runtime, never baked into the image:
#   docker run --rm --env-file .env azureopenai-cli "your prompt"
#   docker run --rm -v /path/to/.env:/app/.env:ro azureopenai-cli "your prompt"
ENTRYPOINT ["./AzureOpenAI_CLI"]

# Install runtime dependencies first so these layers are cached unless deps change
RUN apk add --no-cache \
    icu-libs \
 && apk upgrade --no-cache \
 && rm -rf /var/cache/apk/*

# Copy published app in one step
COPY --from=build /app/AzureOpenAI_CLI /app/AzureOpenAI_CLI
COPY --from=build /app ./

# Create non-root user and set permissions in one layer
RUN addgroup --system appgroup \
 && adduser --system --ingroup appgroup appuser \
 && mkdir -p /opt/config \
 && chown -R appuser:appgroup /app /opt/config \
 && chmod +x /app/AzureOpenAI_CLI

ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false
# Drop privileges for runtime
# Set DOTNET_BUNDLE_EXTRACT_BASE_DIR to a writable location for non-root user
ENV DOTNET_BUNDLE_EXTRACT_BASE_DIR=/tmp/dotnet_bundle
RUN mkdir -p /tmp/dotnet_bundle && chown -R appuser:appgroup /tmp/dotnet_bundle
USER appuser

