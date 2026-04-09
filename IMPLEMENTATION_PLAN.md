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

---

## 🔜 What's Next

### Phase 2 — "Make It Sticky" (Target: v1.5.0)

| Feature | Proposal | Effort | Impact |
|---------|----------|--------|--------|
| Interactive Chat REPL | [FR-002](docs/proposals/FR-002-interactive-chat-mode.md) | Medium | 🔥 Retention driver — `/exit`, `/clear`, `/help`, `/model` commands |
| Config Command & Profiles | [FR-003](docs/proposals/FR-003-local-user-preferences.md) | Small–Med | `--config`, `--temperature`, named profiles (code, creative) |
| Output Modes | [FR-005](docs/proposals/FR-005-shell-integration-and-output-intelligence.md) | Medium | `--code`, `--raw`, `--shell` extraction modes |

### Phase 3 — "Make It Fast" (Target: v1.6.0+)

| Feature | Proposal | Effort | Impact |
|---------|----------|--------|--------|
| Daemon Mode | [FR-004](docs/proposals/FR-004-latency-and-startup-optimization.md) | Large | Persistent container + Unix socket = near-zero startup |
| Session Persistence | [FR-002](docs/proposals/FR-002-interactive-chat-mode.md) | Small | `--continue`, `--sessions` for conversation history |
| Markdown Rendering | [FR-005](docs/proposals/FR-005-shell-integration-and-output-intelligence.md) | Medium | TTY-aware ANSI formatting + clipboard integration |
| Native Install | [FR-004](docs/proposals/FR-004-latency-and-startup-optimization.md) | Ongoing | `dotnet tool install`, Homebrew, GitHub Releases |

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
