# Feature Requests

> Last updated: 2026-04-08 — After v1.1.0 release

## Priority Matrix

| ID | Title | Priority | Effort | Status |
|----|-------|----------|--------|--------|
| [FR-001](FR-001-stdin-pipe-context-injection.md) | Stdin Pipe & Context Injection | P0 | Small | ✅ IMPLEMENTED (v1.1.0) |
| [FR-002](FR-002-interactive-chat-mode.md) | Interactive Chat Mode with Conversation Memory | P0 | Medium | 📋 PLANNED — Next priority |
| [FR-003](FR-003-local-user-preferences.md) | Local User Preferences & Config Command | P1 | Small-Medium | 🔧 IN PROGRESS — Phase 1 |
| [FR-004](FR-004-latency-and-startup-optimization.md) | Latency & Startup Optimization | P0 | Phased | 🔄 PARTIAL — Spinner shipped, daemon pending |
| [FR-005](FR-005-shell-integration-and-output-intelligence.md) | Shell Integration & Output Intelligence | P1 | Medium | 📋 PLANNED — After FR-002 & FR-003 |

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

---

## Shipping Timeline

```
✅ Phase 1 — "Make It Usable" (SHIPPED — v1.1.0, 2026-04-08)
├── ✅ FR-001: Stdin pipes — DONE
├── ✅ FR-004 Phase 1: Spinner — DONE
└── 🔧 FR-003 Phase 1: Env var config — PARTIAL (inline flags pending)

🔜 Phase 2 — "Make It Sticky" (Target: v1.2.0)
├── FR-002 Phase 1: Interactive chat REPL (2-3 days, retention driver)
├── FR-003 Phase 2: --config command + profiles (1-2 days, personalization)
└── FR-005 Phase 1: --code / --raw / --shell modes (2-3 days, viral moments)

📋 Phase 3 — "Make It Fast" (Target: v1.3.0+)
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
