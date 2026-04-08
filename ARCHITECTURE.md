# Architecture

## 1. Overview

Azure OpenAI CLI is a secure, containerized command-line interface for interacting with Azure OpenAI services. It is built with .NET 9, packaged as a self-contained single-file binary, and distributed exclusively through Docker.

### Design Philosophy

- **Simplicity** — A single binary with no external runtime dependencies. Arguments are the prompt; the response streams to stdout.
- **Security** — Runs as a non-root user inside a minimal Alpine container. Credentials live in a `.env` file that is baked into the image at build time and never committed to version control.
- **Containerization** — Docker is the only supported distribution mechanism, ensuring reproducible builds and isolated execution across Windows, macOS, and Linux.

---

## 2. System Architecture

```mermaid
flowchart LR
    User[User / Terminal]
    subgraph Docker Container
        CLI[CLI Binary<br/>AzureOpenAI_CLI]
        ENV[.env File]
        CFG[~/.azureopenai-cli.json]
    end
    API[Azure OpenAI API]

    User -->|prompt / flags| CLI
    ENV -.->|credentials & defaults| CLI
    CFG -.->|model preferences| CLI
    CLI -->|HTTPS streaming request| API
    API -->|chunked SSE response| CLI
    CLI -->|streamed text| User
```

| Component | Responsibility |
|---|---|
| **User / Terminal** | Invokes the container via `make run` or the `az-ai` alias. |
| **Docker Container** | Provides isolation, non-root execution, and a reproducible runtime. |
| **CLI Binary** | Parses arguments, loads configuration, calls Azure OpenAI, and streams the response. |
| **.env File** | Supplies Azure endpoint, API key, model deployment name(s), and system prompt. |
| **UserConfig JSON** | Persists the user's active model selection across invocations. |
| **Azure OpenAI API** | Microsoft-hosted LLM inference endpoint. |

---

## 3. Component Details

### Program.cs — Entry Point

`Program.cs` contains the entire request lifecycle in a single `Main` method plus helper methods for command routing.

#### Startup sequence

1. **Load `.env`** — Uses `dotenv.net` to read the `.env` file baked into the container, overwriting any pre-existing environment variables.
2. **Load UserConfig** — Deserializes `~/.azureopenai-cli.json` (if it exists) to retrieve saved model preferences.
3. **Initialize models** — Parses the `AZUREOPENAIMODEL` environment variable (comma-separated) and reconciles it with the persisted config.

#### Command routing

| Argument | Action | Exit Code |
|---|---|---|
| `--models` / `--list-models` | Print available models; mark the active one with `→` and `*`. | `0` |
| `--current-model` | Print the active model name. | `0` (set) / `1` (unset) |
| `--set-model <name>` | Validate the name against available models, persist the choice. | `0` (success) / `1` (error) |
| `--help` / `-h` | Print usage information. | `0` |
| _(any other text)_ | Treated as a prompt — all args are joined with spaces. | `0` (success) / `99` (unhandled error) |
| _(no args)_ | Print usage information. | `1` |

#### Chat completion flow

1. Build an `AzureOpenAIClient` with the endpoint URI and API key credential.
2. Obtain a `ChatClient` scoped to the active deployment name.
3. Construct a `ChatCompletionOptions` with hardcoded defaults (`MaxOutputTokenCount=10000`, `Temperature=0.55`, `TopP=1.0`).
4. Send a two-message conversation (`SystemChatMessage` + `UserChatMessage`) via `CompleteChatStreaming`.
5. Iterate `StreamingChatCompletionUpdate` chunks, writing each content part to stdout in real time.

#### Error handling

All exceptions are caught at the top level and written to stderr in the format `[UNHANDLED ERROR] <Type>: <Message>`. The process exits with code `99` on any unhandled error.

---

### UserConfig.cs — Configuration Manager

`UserConfig` manages persistent user preferences, stored as JSON at `~/.azureopenai-cli.json`.

#### Data model

```json
{
  "ActiveModel": "gpt-4o",
  "AvailableModels": ["gpt-4", "gpt-35-turbo", "gpt-4o"]
}
```

#### Key behaviors

| Method | Description |
|---|---|
| `Load()` | Reads and deserializes the config file. Returns a fresh instance if the file is missing or corrupt. |
| `Save()` | Serializes the current state to disk with indented JSON. Failures are logged to stderr as warnings. |
| `InitializeFromEnvironment(string?)` | Parses a comma-separated model string, deduplicates entries, and updates `AvailableModels`. Resets `ActiveModel` to the first entry if the current selection is no longer valid. |
| `SetActiveModel(string)` | Case-insensitive lookup against `AvailableModels`. Returns `true` on match, `false` otherwise. |

#### Thread safety

`UserConfig` is **not** thread-safe. It is designed for single-threaded CLI use — one invocation reads, optionally mutates, and writes the config file. Concurrent container invocations could race on the JSON file, but this is acceptable given the CLI's usage pattern.

---

## 4. Build Pipeline

```mermaid
flowchart TD
    subgraph "Stage 1 — Build (dotnet/sdk:9.0-preview)"
        A[Copy .sln + project files] --> B[dotnet restore]
        B --> C["dotnet publish<br/>-r linux-musl-x64<br/>--self-contained<br/>PublishSingleFile<br/>PublishTrimmed"]
        C --> D["/app/AzureOpenAI_CLI<br/>(single binary)"]
    end

    subgraph "Stage 2 — Runtime (runtime-deps:9.0-preview-alpine)"
        E[Install icu-libs] --> F[Copy binary from Stage 1]
        F --> G[Create appuser / appgroup]
        G --> H[Copy .env into /app]
        H --> I["USER appuser<br/>ENTRYPOINT ./AzureOpenAI_CLI"]
    end

    D -->|COPY --from=build| F
```

### Publish flags explained

| Flag | Purpose |
|---|---|
| `-r linux-musl-x64` | Target Alpine's musl libc instead of glibc. |
| `--self-contained true` | Bundle the .NET runtime — no framework install required. |
| `/p:PublishSingleFile=true` | Produce a single executable file. |
| `/p:PublishTrimmed=true` | Remove unused framework code to reduce binary size. |
| `/p:IncludeAllContentForSelfExtract=true` | Embed all content for self-extraction at startup. |

### Makefile targets

| Target | Description |
|---|---|
| `make build` | Build the Docker image (`azureopenai-cli:gpt-5-chat`). |
| `make run ARGS="..."` | Run the CLI inside a fresh container with the given arguments. |
| `make clean` | Remove `bin/obj` directories, prune dangling images, and clean the Docker builder cache. |
| `make alias` | Append an `az-ai` shell alias to the user's RC file (`.zshrc`, `.bashrc`, or `.profile`). |
| `make scan` | Run Grype vulnerability scanner against the built image. |
| `make test` | Clean, build, then run a sample prompt ("Tell me some unusual facts about cats"). |
| `make check` | Build verification — creates a placeholder `.env` if needed, runs `make build`, then cleans up. |

---

## 5. Data Flow

### Prompt → Response

```mermaid
sequenceDiagram
    participant U as User
    participant D as Docker
    participant CLI as CLI Binary
    participant Env as .env
    participant Cfg as UserConfig JSON
    participant API as Azure OpenAI

    U->>D: make run ARGS="Explain quantum computing"
    D->>CLI: ./AzureOpenAI_CLI Explain quantum computing
    CLI->>Env: DotEnv.Load()
    Env-->>CLI: endpoint, key, model(s), system prompt
    CLI->>Cfg: UserConfig.Load()
    Cfg-->>CLI: active model, available models
    CLI->>CLI: Join args → user prompt
    CLI->>CLI: Build AzureOpenAIClient + ChatClient
    CLI->>API: CompleteChatStreaming([system, user])
    loop Streaming chunks
        API-->>CLI: StreamingChatCompletionUpdate
        CLI-->>U: Console.Write(chunk.Text)
    end
    CLI-->>U: newline + exit code 0
```

### Configuration flow

```
.env file ──DotEnv.Load()──▶ Environment variables ──GetEnvironmentVariable()──▶ AzureOpenAIClient
                                                                                   │
AZUREOPENAIMODEL ──InitializeFromEnvironment()──▶ UserConfig.AvailableModels       │
                                                      │                            │
UserConfig.ActiveModel ────────────────────────────────┘──────────▶ deploymentName ─┘
```

---

## 6. Configuration Model

### Configuration sources

| Source | Variables | Purpose |
|---|---|---|
| `.env` file | `AZUREOPENAIENDPOINT` | Azure OpenAI resource URL |
| | `AZUREOPENAIAPI` | API key for authentication |
| | `AZUREOPENAIMODEL` | Comma-separated deployment name(s) |
| | `SYSTEMPROMPT` | System message prepended to every request |
| UserConfig JSON | `ActiveModel` | Currently selected model deployment |
| | `AvailableModels` | Full list of configured models |

### Hardcoded defaults (in `Program.cs`)

| Parameter | Value |
|---|---|
| `MaxOutputTokenCount` | `10000` |
| `Temperature` | `0.55` |
| `TopP` | `1.0` |
| `FrequencyPenalty` | `0.0` |
| `PresencePenalty` | `0.0` |

### Precedence rules

1. **`.env` file** is loaded with `overwriteExistingVars: true`, so it takes priority over any pre-existing environment variables.
2. **`UserConfig.ActiveModel`** overrides the first model from `AZUREOPENAIMODEL` if the user has explicitly set a model with `--set-model`.
3. If `ActiveModel` is null or no longer in `AvailableModels`, the CLI falls back to the first entry in the model list.

---

## 7. Security Architecture

> **Note:** No `SECURITY.md` file exists yet. The security posture is documented here.

### Containerization boundary

- The CLI runs exclusively inside Docker — no host installation is supported.
- The Alpine base image (`runtime-deps:9.0-preview-alpine`) minimizes the attack surface.
- Only `icu-libs` is added at runtime; all other packages are stripped.

### Non-root execution

- A dedicated `appuser` / `appgroup` is created in the Dockerfile.
- The `USER appuser` directive drops privileges before the entrypoint runs.
- File permissions are explicitly set with `chown` and `chmod`.

### Credential handling

- API keys and endpoints live in the `.env` file, which is **baked into the image** at build time.
- The `.env` file is listed in `.gitignore` and never committed to version control.
- No credentials are stored in the `UserConfig` JSON file — it only holds model names.

### Vulnerability scanning

- `make scan` runs [Grype](https://github.com/anchore/grype) against the built image.
- The Dockerfile comment recommends pinning image digests in production for supply-chain integrity.

---

## 8. Directory Structure

```
azure-openai-cli/
├── ARCHITECTURE.md              # This file
├── Dockerfile                   # Multi-stage build (SDK → Alpine runtime)
├── IMPLEMENTATION_PLAN.md       # Development roadmap
├── LICENSE                      # Project license
├── Makefile                     # Build, run, clean, scan, alias targets
├── README.md                    # User-facing documentation
├── azure-openai-cli.sln         # .NET solution file
├── azureopenai-cli/             # Application source
│   ├── .env.example             # Template for Azure credentials
│   ├── .env                     # Actual credentials (git-ignored)
│   ├── AzureOpenAI_CLI.csproj   # Project file (net9.0, package refs)
│   ├── Program.cs               # Entry point, command routing, chat flow
│   └── UserConfig.cs            # JSON-based model config manager
├── docs/
│   └── proposals/               # Design proposals
└── img/                         # Screenshots and demo GIFs
```

---

## 9. Extension Points

### Adding a new command

1. Add a new `case` to the `switch` block in `HandleModelCommands()` (`Program.cs`).
2. Implement a static helper method following the pattern of `ListModels()` / `SetModel()`.
3. Return an appropriate exit code (`0` for success, `1` for user error).
4. Update `ShowUsage()` to document the new flag.

### Adding a new configuration option

1. Add a property to `UserConfig.cs`. The `System.Text.Json` serializer will pick it up automatically.
2. If the option should be sourced from the environment, read it via `Environment.GetEnvironmentVariable()` in `Program.cs` and add a corresponding entry to `.env.example`.
3. Persist changes by calling `config.Save()`.

### Modifying the Docker build

- **Add a system package** — Append to the `apk add` command in Stage 2.
- **Change the .NET version** — Update both `FROM` image tags and the `<TargetFramework>` in the `.csproj`.
- **Add build-time arguments** — Use `ARG` / `--build-arg` in the Dockerfile and reference them in the Makefile's `docker buildx build` command.

---

## 10. Design Decisions

| Decision | Rationale |
|---|---|
| **Docker-first distribution** | Guarantees identical behavior across platforms. Eliminates "works on my machine" issues. Provides a natural security boundary. |
| **Single-file publish** | Produces one binary with no loose DLLs, reducing the attack surface and simplifying the container's filesystem. Enables fast cold-start since there is nothing to resolve at runtime. |
| **Alpine base image** | At ~5 MB, Alpine is the smallest mainstream Linux distribution. Fewer installed packages means fewer CVEs to patch. |
| **`dotenv.net`** | Lightweight library (~30 KB) for loading `.env` files. Avoids pulling in a full configuration framework like `Microsoft.Extensions.Configuration` for a single-purpose CLI. |
| **Self-contained runtime** | Bundles the .NET runtime so the runtime stage only needs `runtime-deps` (native libraries) instead of the full `runtime` image. Further reduces image size. |
| **Streaming responses** | Uses `CompleteChatStreaming` so the user sees tokens as they arrive, rather than waiting for the entire completion. This dramatically improves perceived latency for long responses. |
| **JSON config in home directory** | Familiar convention (`~/.<app>.json`). Survives container restarts if a volume is mounted. Keeps credentials separate from user preferences. |
