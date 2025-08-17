# ---------- Stage 1: Build ----------
FROM mcr.microsoft.com/dotnet/sdk:9.0-preview AS build
WORKDIR /src

# Copy only .NET project files for restore caching
COPY *.sln ./
COPY azureopenai-cli/ ./
RUN dotnet restore ./AzureOpenAI_CLI.csproj


# Restore & publish .NET self-contained binary
# Changed target runtime from linux-musl-x64 to linux-x64 to avoid entrypoint detection issues
# Removed trimming to prevent compiler stripping Main method in preview SDK
# Removed explicit StartupObject and let compiler detect Main automatically
# Added LANG and LC_ALL to ensure UTF-8 encoding and avoid BOM issues
# Added step to list source files to verify Program.cs is present
ENV LANG=en_US.UTF-8 \
    LC_ALL=en_US.UTF-8

RUN dotnet publish ./AzureOpenAI_CLI.csproj \
        -c Release \
        -r linux-x64 \
        --self-contained true \
        /p:PublishSingleFile=true \
        /p:PublishTrimmed=true \
        /p:IncludeAllContentForSelfExtract=true \
        -o /app

# ---------- Stage 2: Runtime ----------
FROM mcr.microsoft.com/dotnet/runtime-deps:9.0-preview AS runtime

WORKDIR /app
ENTRYPOINT ["./AzureOpenAI_CLI"]

# Install runtime dependencies first so these layers are cached unless deps change
RUN apt-get update \
 && apt-get install -y --no-install-recommends libicu72 \
 && rm -rf /var/lib/apt/lists/*

# Copy published app in one step
COPY --from=build /app/AzureOpenAI_CLI /app/AzureOpenAI_CLI
COPY --from=build /app ./

# Create config dir, copy .env, and set permissions in one layer
RUN mkdir -p /opt/config \
 && chmod +x /app/AzureOpenAI_CLI

COPY --from=build /src/.env .env

ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

