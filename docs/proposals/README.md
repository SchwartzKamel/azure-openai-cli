# Feature Requests

> Last updated: 2026-04-08 — After v1.1.0 release

> **v2 Migration in progress**: See [v2-migration.md](../v2-migration.md) for the Microsoft Agent Framework transition plan (v1 → v2.0). Feature requests below are v1.x scope unless noted.

## Priority Matrix

| ID | Title | Priority | Effort | Status |
|----|-------|----------|--------|--------|
| [FR-001](FR-001-stdin-pipe-context-injection.md) | Stdin Pipe & Context Injection | P0 | Small | ✅ IMPLEMENTED (v1.1.0) |
| [FR-002](FR-002-interactive-chat-mode.md) | Interactive Chat Mode with Conversation Memory | P0 | Medium | 📋 PLANNED — Next priority |
| [FR-003](FR-003-local-user-preferences.md) | Local User Preferences & Config Command | P1 | Small-Medium | 🔧 IN PROGRESS — Phase 1 |
| [FR-004](FR-004-latency-and-startup-optimization.md) | Latency & Startup Optimization | P0 | Phased | ✅ LARGELY SHIPPED — Spinner (v1.1.0) + AOT (v1.8.0); daemon mode deferred |
| [FR-005](FR-005-shell-integration-and-output-intelligence.md) | Shell Integration & Output Intelligence | P1 | Medium | 🔄 PARTIAL — `--json`, `--raw` shipped; `--code`/`--shell`/markdown pending |
| [FR-006](FR-006-unblock-native-aot-compilation.md) | Unblock Native AOT Compilation | P0 | Small | ✅ SHIPPED (v1.8.0) |
| [FR-007](FR-007-parallel-startup-and-connection-prewarming.md) | Parallel Startup & Connection Pre-warming | P1 | Small | 📋 PLANNED — Complements FR-006 |
| [FR-008](FR-008-prompt-response-cache.md) | Prompt Response Cache | P1 | Medium | 📋 PLANNED — Espanso/AHK use case |
| [FR-009](FR-009-config-set-and-directory-overrides.md) | `--config set` Commands & Per-Directory Overrides | P1 | Medium | 📋 PLANNED — Completes FR-003 |
| [FR-010](FR-010-model-aliases-and-smart-defaults.md) | Model Aliases & Smart Defaults | P2 | Small | 📋 PLANNED — DX polish |
| [FR-011](FR-011-agent-streaming-output.md) | Agent Mode Streaming Output | P0 | Small-Medium | 📋 PLANNED — v1.9.0 top priority |
| [FR-012](FR-012-plugin-tool-registry.md) | Plugin/Tool Registry System | P2 | Medium-Large | 📋 PLANNED — Extensibility platform |

---

## Status Details

### ✅ FR-001: Stdin Pipe & Context Injection — SHIPPED in v1.1.0

Fully implemented on 2026-04-08. Key capabilities delivered:

- **Stdin piping:** `echo "question" | az-ai` reads from stdin when input is redirected
- **Combined mode:** `cat file.py | az-ai "summarize this"` merges piped content with prompt argument
- **Input validation:** 32K character prompt limit with clear error messaging
- **No regressions:** `az-ai "hello"` continues to work when stdin is a TTY

**Remaining from proposal (deferred to future release):**
- `--file <path>` flag for explicit file injection (not yet implemented)

### 📋 FR-002: Interactive Chat Mode — PLANNED (Next Priority)

No implementation started. This is the highest-priority unshipped feature. Phases:
1. **REPL mode** with `--chat` flag and `/exit`, `/clear`, `/help`, `/model` slash commands
2. **Session persistence** with `--continue` and `--sessions`
3. **Context window management** with token tracking

**Depends on:** FR-003 (profiles enhance chat experience)

### 🔧 FR-003: Local User Preferences — IN PROGRESS (Phase 1)

Phase 1 is currently being implemented:
- `AZURE_MAX_TOKENS` and `AZURE_TEMPERATURE` are now configurable via environment variables (shipped in v1.1.0)
- `AZURE_TIMEOUT` configurable streaming timeout (shipped in v1.1.0)

**Still pending:**
- `--config` command suite for interactive settings management
- `--temperature` and `--system` inline override flags
- Named profiles (code, creative, etc.)
- Expanded `UserConfig` JSON schema with full precedence chain

### 🔄 FR-004: Latency & Startup Optimization — PARTIAL

**Phase 1 (shipped in v1.1.0):**
- ✅ Braille-animation progress spinner on stderr while waiting for first token
- ✅ Spinner clears cleanly when first token arrives
- ✅ Pipe-safe: spinner writes to stderr only, won't pollute piped output

**Phase 2 (not started):**
- ❌ Persistent daemon container mode (`--daemon start/stop/status`)
- ❌ Unix socket communication for near-zero startup overhead

**Phase 3 (not started):**
- ❌ Native install via `dotnet tool install`, GitHub Releases, Homebrew

### 📋 FR-005: Shell Integration & Output Intelligence — PLANNED

Blocked on FR-002 and FR-003. Partial overlap with shipped features:
- ✅ `--json` output mode shipped in v1.1.0 (originally proposed here as sub-feature)

**Still pending:**
- Markdown-aware terminal rendering (TTY detection + ANSI formatting)
- `--shell` execute mode with confirmation prompt
- `--code` mode for extracting only code blocks
- `--raw` flag for forced plain-text output
- Clipboard integration

### ✅ FR-006: Unblock Native AOT Compilation — SHIPPED in v1.8.0

The `OutputJsonError` anonymous type was replaced with the source-generated
`ErrorJsonResponse` record registered in `AppJsonContext`, and the remaining
`SquadConfig.Load` / `Save` / `Initialize` paths were migrated off reflection-
based `JsonSerializer` overloads. `make publish-aot` produces a ~9 MB
single-file self-contained binary with **~5.4 ms cold start** on Linux x64
(vs ~54 ms for ReadyToRun, ~400+ ms for Docker). `make publish` is now an
alias for `publish-aot`.

### 📋 FR-007: Parallel Startup & Connection Pre-warming — PLANNED

The CLI's startup sequence is strictly sequential: DotEnv load → UserConfig load → arg parsing → Azure client creation → TLS handshake. The TLS handshake (~200–500ms) can start in parallel with config loading since it only needs the endpoint URL. Uses a shared `SocketsHttpHandler` to ensure the Azure SDK reuses the pre-warmed connection.

**Depends on:** Nothing. Complements FR-006 (combined savings: ~385ms).

### 📋 FR-008: Prompt Response Cache — PLANNED

For Espanso/AHK text expansion, the same prompts fire repeatedly. A file-based SHA256-keyed cache in `~/.cache/azureopenai-cli/` turns cache hits from ~500ms (network) to <5ms (disk read). Includes TTL expiration, LRU eviction, `--no-cache` flag, and `--cache-clear` command. Bypassed automatically in agent and Ralph modes.

**Depends on:** FR-006 (cache records should use AOT-safe source-generated JSON).

### 📋 FR-009: `--config set` Commands & Per-Directory Overrides — PLANNED

Completes the work started in FR-003. The `UserConfig` data model has Temperature, MaxTokens, TimeoutSeconds, and SystemPrompt fields — but no CLI commands to set them. Adds `--config set/get/reset` commands and per-directory `.azureopenai-cli.json` overrides (like `.editorconfig`), enabling project-specific AI behavior without global changes.

**Depends on:** FR-003 Phase 1 (already shipped).

### 📋 FR-010: Model Aliases & Smart Defaults — PLANNED

Adds short aliases for model deployment names (`4o` → `gpt-4o-2024-08-06`), a `--model` flag for per-invocation model switching without persistence, auto-generated aliases for common patterns, and normalized environment variable names (`AZURE_OPENAI_ENDPOINT` alongside legacy `AZUREOPENAIENDPOINT`).

**Depends on:** FR-009 (uses `--config set alias` infrastructure).

---

## Shipping Timeline

```
✅ Phase 1 — "Make It Usable" (SHIPPED — v1.1.0, 2026-04-08)
├── ✅ FR-001: Stdin pipes — DONE
├── ✅ FR-004 Phase 1: Spinner — DONE
└── 🔧 FR-003 Phase 1: Env var config — PARTIAL (inline flags pending)

🔜 Phase 2 — "Make It Fast" (Target: v1.2.0)
├── FR-006: Unblock Native AOT (2-4 hours, highest ROI)
├── FR-007: Parallel startup + TLS pre-warming (3-5 hours)
├── FR-009: --config set commands + per-directory overrides (1-2 days)
└── FR-010: Model aliases + --model flag (4-6 hours)

📋 Phase 3 — "Make It Sticky" (Target: v1.3.0)
├── FR-002 Phase 1: Interactive chat REPL (2-3 days, retention driver)
├── FR-008: Prompt response cache (1-2 days, Espanso power feature)
├── FR-005 Phase 1: --code / --raw / --shell modes (2-3 days, viral moments)
└── FR-003 Phase 2: Named profiles (1-2 days, personalization)

📋 Phase 4 — "Make It Competitive" (Target: v1.4.0+)
├── FR-004 Phase 2: Daemon mode (3-5 days, performance leap)
├── FR-002 Phase 2: Session persistence (1-2 days, continuity)
├── FR-005 Phase 2: Markdown rendering + clipboard (2-3 days, polish)
└── FR-004 Phase 3: Native install / Homebrew (ongoing)
```

---

## Design Principles

These proposals follow a shared philosophy:

1. **Pipes over prompts** — CLI tools that compose with other tools win.
2. **Speed is the feature** — Every millisecond of startup is a decision point where the user considers ChatGPT.
3. **Progressively disclosed complexity** — Simple by default, powerful when needed.
4. **Azure-native is the moat** — Lean into enterprise identity, multi-model management, and security posture.
5. **Demo-able in 15 seconds** — If you can't GIF it, it won't go viral.
