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

---

## v1.9.0 Roadmap (planned)

> Authored by **Mr. Pitt**, Executive / Program Manager. I want it done *correctly*.
> v1.8.0 is on the wire. v1.9.0 begins **now**. No drift, no hand-waving, no
> "we'll get to it." Each milestone has an owner, an acceptance bar, and a
> proposal number. If it doesn't have all three, it isn't on the roadmap — it's
> a suggestion, and I don't fund suggestions.

### Today's Fleet Manifest (v1.9 kickoff)

```
Phase 2 kickoff — 8 agents, 1 day, 1 standard: correctness.

1. Costanza  → docs/proposals/FR-011-agent-streaming-output.md
              Author the proposal. Top-priority v1.9 item. Slug: agent-streaming-output.
              Make the latency case. Show the diff between buffered and streamed
              for a 40-token agent response. Acceptance criteria spelled out.

2. Kramer    → QoL: implement `--version --short`.
              Single-line, bare semver to stdout, exit 0. No banner, no prefix,
              no newline variance. For `$(az-ai --version --short)` in scripts
              and Makefiles. Scope:
                • New flag path in CliParser: --version --short / -V -s
                • Emits e.g. `1.9.0` exactly (trailing \n acceptable)
                • 3+ unit tests in CliParserTests
                • README + `--help` updated
                • No regression to existing `--version` long form
              Acceptance: tests green, `az-ai --version --short | wc -c` ≤ 10.

3. Elaine    → docs/adr/ADR-001-native-aot-recommended.md
              First ADR. Capture AOT-first rationale. Numbers on the record:
              5.4 ms cold start, ~10× R2R, ~75× Docker. Tradeoffs must be
              documented: slower publish times, host-only cross-compile (no
              musl-from-glibc etc.), third-party AOT warnings from Azure.AI.OpenAI.
              Status: Accepted. Date: today. Supersedes: none.

4. Newman    → docs/audits/security-v1.8-post-release.md
              Verify — don't assume — that the v1.8.0 release workflow actually
              produced: SBOM artifact, Scorecards results, build provenance
              attestation, minimally-scoped `permissions:` blocks. Pull the run.
              Evidence links or it didn't happen.

5. Jackie    → NOTICE at repo root.
              Attribution for Azure.AI.OpenAI (MIT), Azure.Core (MIT),
              dotenv.net (MIT), OpenAI (MIT). Versions from
              `dotnet list package` — exact, not approximate. Copyright holders
              and upstream URLs included.

6. Jerry     → .github/dependabot.yml
              Ecosystems: nuget (azureopenai-cli/), github-actions (/).
              Interval: weekly. Grouped patch updates per ecosystem. Open PR
              limit: 5 nuget / 5 actions. Commit message prefix: `deps:`.
              Reviewers: Jerry + Newman (security-sensitive).

7. Puddy     → extend tests/integration_tests.sh with 3–5 adversarial cases:
                a) Missing AZUREOPENAIENDPOINT → clear error, exit ≠ 0
                b) Malformed env (e.g. endpoint without scheme) → clear error
                c) CTRL+C mid-response → exit 130, no partial ralph-log corruption
                   (skip gracefully if non-interactive runner can't signal)
                d) Invalid --model name → clear error, no crash
                e) Huge-prompt boundary (32K + 1 char) → validated rejection
              Yeah, that's right. All five.

8. Peterman → docs/announce/v1.8.0-launch.md
              Discussions/blog draft. Lead with the 5.4 ms number. AOT hero
              story. Espanso/AHK angle. Include install one-liner and a GIF
              hook. Not a release note — a *launch piece*.
```

Release closure (CHANGELOG finalization for v1.8.0, v1.9.0 version bump choreography) is routed to **Mr. Lippman** in phase 3. Not today's manifest.

### Milestones

| # | Milestone | Owner | Proposal |
|---|-----------|-------|----------|
| M1 | Agent streaming output mode | Costanza (spec) → Kramer (impl) | **FR-011** (`agent-streaming-output`) |
| M2 | Distribution: Homebrew + Scoop + Nix flake | Jerry | **FR-012** (`packaging-homebrew-scoop-nix`) |
| M3 | Dependabot adoption (NuGet + Actions, grouped) | Jerry | — (ops, no FR needed) |
| M4 | Matrix CI on macOS + Windows runners | Jerry + Puddy | — (deferred from ci-audit) |
| M5 | Config subcommands (`config set/get/list`) land FR-009 | Kramer | FR-009 (existing) |

#### M1 — Agent streaming output (FR-011)
The `--agent` loop currently buffers the entire assistant turn before printing.
At 40+ tokens per turn and multi-round tool-calling, perceived latency eats the
5.4 ms AOT win. Stream per-token to stdout; flush tool-call boundaries cleanly.

**Acceptance criteria:**
- First token visible to user within 1 round-trip + stream start (target p50 < 400 ms)
- Tool-call events bracketed with unambiguous markers on stderr, not stdout
- `--raw` and `--json` behavior preserved byte-for-byte (json stays buffered;
  raw streams cleanly with no spinner pollution)
- No regression in 538-test baseline; ≥ 10 new tests covering streaming + tool interleaving
- Works under AOT publish (no reflection regressions)

#### M2 — Packaging: Homebrew + Scoop + Nix (FR-012)
Post-AOT, the install story is still `curl | tar` or `make install`. Unacceptable.

**Acceptance criteria:**
- `brew install SchwartzKamel/tap/az-ai` works on macOS x64 + arm64
- Scoop manifest in a `scoop-bucket` branch or tap; `scoop install az-ai` works on win-x64
- `nix run github:SchwartzKamel/azure-openai-cli` produces runnable binary on Linux x64
- All three pinned to v1.9.0 release artifacts with SHA256 verification
- CI job verifies the Homebrew formula lints (`brew audit --strict --online`)

#### M3 — Dependabot
**Acceptance criteria:**
- `.github/dependabot.yml` committed (Jerry, today, see manifest)
- First weekly run produces ≤ 5 grouped PRs, all CI-green or clearly flagged
- No secret leakage in generated PR bodies (Newman signs off)

#### M4 — Matrix CI (macOS + Windows)
Deferred from the ci-audit. v1.8 shipped cross-platform binaries we don't test on CI.

**Acceptance criteria:**
- `ci.yml` matrix: `ubuntu-latest`, `macos-latest`, `windows-latest`
- Full test suite (538+) passes on all three; integration_tests.sh gated to Unix
- Build + smoke-test the AOT binary on each OS (at minimum `az-ai --version --short`)
- Total CI wall time ≤ 12 minutes p95

#### M5 — Config subcommands (FR-009 landing)
FR-009 has been "planned" long enough. Land it.

**Acceptance criteria:**
- `az-ai config set <key> <value>`, `config get <key>`, `config list`, `config reset [key]`
- Per-directory `.azureopenai-cli.json` override discovered via upward walk
- Precedence documented and tested: CLI flag > env var > local config > user config > defaults
- `--config set` never writes secrets (API keys explicitly blocked with clear error)
- Docs updated: README, FR-009 status → SHIPPED

### Suggested next proposal numbers
- **FR-011** — `agent-streaming-output` — Costanza, today
- **FR-012** — `packaging-homebrew-scoop-nix` — Jerry, follow-up week

### Non-goals for v1.9.0
Explicitly **not** in scope — don't scope-creep me:
- Plugin system for custom tools (too large; revisit v2.0)
- Telemetry opt-in (privacy posture needs a dedicated proposal cycle with Newman + Jackie before a line of code is written)
- Daemon mode (FR-004 Phase 2 — stays in Phase 3 bucket)
- Interactive REPL (FR-002 — defer to v1.10 once streaming lands; chat without streaming is a bad demo)
