# Implementation Plan & Roadmap

> Tracks what's shipped, what's next, and where to contribute.
> For detailed feature proposals, see [`docs/proposals/`](docs/proposals/README.md).

---

## ✅ What's Shipped

### v1.0.0 — Foundation (2025-08-17)
- Azure OpenAI chat completion via Docker container
- Streaming responses, system prompt configuration
- Alpine Linux base, non-root container execution

### v1.0.1 — Multi-Model Selection (2025-12-04)
- `--models`, `--set-model`, `--current-model` flags
- Persistent model selection in `~/.azureopenai-cli.json`

### v1.1.0 — Developer Experience (2026-04-08)
- Stdin pipe support (`echo "question" | az-ai`)
- `--json` output mode, `--version` flag
- Progress spinner, input validation (32K limit)
- Configurable `AZURE_MAX_TOKENS`, `AZURE_TEMPERATURE`, `AZURE_TIMEOUT`
- Security hardening: HTTPS validation, API key checks, chmod 600 config
- Full documentation suite: SECURITY.md, ARCHITECTURE.md, CONTRIBUTING.md

### v1.2.0 — Agent Mode (2026-04-08)
- `--agent` flag with 5 built-in tools (shell, file, web, clipboard, datetime)
- `--tools` and `--max-rounds` flags for tool control
- Tool safety: command blocklist, HTTPS-only fetch, path blocking

### v1.3.0 — Security & Parallel Execution (2025-04-09)
- Parallel tool call execution via `Task.WhenAll`
- Security fixes: symlink traversal, DNS rebinding, expanded shell blocklist
- Exact alias matching in tool registry
- CI: format check, NuGet audit, Trivy container scanning, integration tests
- 115 new tests (104 security + 11 parallel execution)

### v1.4.0 — Ralph Mode (2025-07-13)
- `--ralph` autonomous Wiggum loop for self-correcting agent workflows
- `--validate`, `--task-file`, `--max-iterations` flags
- `delegate_task` tool for subagent spawning
- 64 new tests (28 Ralph mode + 16 delegate tool + 20 integration)

### v1.5.0 — Persona / Squad System (2026-04-09)
- `--persona`, `--persona auto`, `--personas`, `--squad-init` flags
- Persistent per-persona memory in `.squad/history/` with 32 KB cap
- Shared decision log in `.squad/decisions.md`
- Keyword-based routing via `SquadCoordinator`
- 5 default personas (coder, reviewer, architect, writer, security)
- ~46 new tests; zero new dependencies

### v1.6.0 — AOT Groundwork & Rate-Limit Awareness (2025-07-20)
- `AppJsonContext` source generators unblock Native AOT serialization
- CLI validation for temperature (0.0–2.0) and max-tokens (1–128000)
- Rate-limit-aware streaming retry honors `Retry-After` (cap 60 s)
- `WebFetchTool` SSRF redirect protection; tools hardened via `TryGetProperty`
- Dockerfile adds `PublishReadyToRun=true`; Makefile fixed to .NET 10.0
- 60+ new tests

### v1.7.0 — Token Tracking & Text-Injection Polish (2025-07-21)
- Token usage tracking on stderr and in `--json` output
- `--raw` flag + TTY-aware spinner suppression for Espanso/AHK workflows
- Espanso/AHK integration guide
- `ErrorAndExit` DRY helper; DotEnv loading hardened against missing/malformed files
- `ShellExecTool` blocks `$()`, backticks, `<()`, `>()`, `eval`, `exec`; uses
  `ArgumentList` for OS-level escaping
- 24 new tests (454 total)

### v1.8.0 — Native AOT GA & Cross-Platform Publish (Unreleased)
- Native AOT promoted to recommended: ~9 MB binary, **~5.4 ms cold start**
  (~10× faster than R2R, ~75× faster than Docker)
- `Azure.AI.OpenAI` pinned to stable **2.1.0** (no more pre-release)
- Graceful CTRL+C cancellation — flushes Ralph log, exits 130
- Cross-platform publish targets for 7 RIDs + `make publish-all`
- `make install` / `make uninstall` / `make bench`
- Portable `scripts/bench.py` startup benchmark
- 71 new `CliParser` tests (538 total); `ParseCliFlags` scoped `internal`
- Retry/backoff DRY'd up (~60 lines removed)

---

## 🔜 What's Next

### Phase 2 — "Make It Sticky" (Target: v1.9.0+)

| Feature | Proposal | Effort | Impact |
|---------|----------|--------|--------|
| Interactive Chat REPL | [FR-002](docs/proposals/FR-002-interactive-chat-mode.md) | Medium | 🔥 Retention driver — `/exit`, `/clear`, `/help`, `/model` commands |
| `--config set` & Per-Directory Overrides | [FR-009](docs/proposals/FR-009-config-set-and-directory-overrides.md) | Medium | Completes FR-003 — `--config set/get/reset`, `.azureopenai-cli.json` in any dir |
| Output Modes (`--code`, `--shell`) | [FR-005](docs/proposals/FR-005-shell-integration-and-output-intelligence.md) | Medium | `--raw` / `--json` shipped; `--code`, `--shell`, markdown rendering pending |
| Prompt Response Cache | [FR-008](docs/proposals/FR-008-prompt-response-cache.md) | Medium | SHA-256 keyed cache for Espanso/AHK repeat prompts |
| Model Aliases | [FR-010](docs/proposals/FR-010-model-aliases-and-smart-defaults.md) | Small | DX polish — `4o` → `gpt-4o-2024-08-06`, `--model` flag |

### Phase 3 — "Make It Faster Still"

| Feature | Proposal | Effort | Impact |
|---------|----------|--------|--------|
| Parallel Startup & Connection Pre-warming | [FR-007](docs/proposals/FR-007-parallel-startup-and-connection-prewarming.md) | Small | TLS handshake in parallel with config load |
| Daemon Mode | [FR-004](docs/proposals/FR-004-latency-and-startup-optimization.md) Phase 2 | Large | Persistent process + Unix socket for near-zero per-invocation overhead |
| Session Persistence | [FR-002](docs/proposals/FR-002-interactive-chat-mode.md) Phase 2 | Small | `--continue`, `--sessions` for conversation history |

---

## 🤝 Contribution Opportunities

Looking for a way to contribute? These are great starting points:

| Area | What's Needed | Difficulty |
|------|---------------|------------|
| **Interactive Chat** | Implement REPL loop with slash commands ([FR-002](docs/proposals/FR-002-interactive-chat-mode.md)) | ⭐⭐⭐ |
| **Config CLI** | Build `--config` command for managing preferences ([FR-003](docs/proposals/FR-003-local-user-preferences.md)) | ⭐⭐ |
| **Output Modes** | Add `--code` / `--raw` flags for extraction ([FR-005](docs/proposals/FR-005-shell-integration-and-output-intelligence.md)) | ⭐⭐ |
| **Test Coverage** | Add tests for edge cases in existing tools | ⭐ |
| **Documentation** | Improve inline code comments, add usage examples | ⭐ |
| **Cross-platform** | Test and fix issues on Windows/macOS | ⭐⭐ |

### How to Pick Up Work

1. Check the [open issues](https://github.com/SchwartzKamel/azure-openai-cli/issues) for anything labeled `good first issue` or `help wanted`
2. Read the relevant [feature proposal](docs/proposals/) for context
3. Follow the [Contributing Guide](CONTRIBUTING.md) for setup and PR process
4. Ask questions in the issue thread — we're happy to help

---

## Design Principles

All roadmap work follows these guiding principles:

1. **Pipes over prompts** — CLI tools that compose with other tools win
2. **Speed is the feature** — every millisecond of startup is a decision point where the user considers alternatives
3. **Progressively disclosed complexity** — simple by default, powerful when needed
4. **Azure-native is the moat** — lean into enterprise identity, multi-model management, and security posture
5. **Demo-able in 15 seconds** — if you can't GIF it, it won't go viral
