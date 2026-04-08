# Feature Requests

## Priority Matrix

| ID | Title | Priority | Effort | Status |
|----|-------|----------|--------|--------|
| [FR-001](FR-001-stdin-pipe-context-injection.md) | Stdin Pipe & Context Injection | P0 | Small | Proposed |
| [FR-002](FR-002-interactive-chat-mode.md) | Interactive Chat Mode with Conversation Memory | P0 | Medium | Proposed |
| [FR-003](FR-003-local-user-preferences.md) | Local User Preferences & Config Command | P1 | Small-Medium | Proposed |
| [FR-004](FR-004-latency-and-startup-optimization.md) | Latency & Startup Optimization | P0 | Phased | Proposed |
| [FR-005](FR-005-shell-integration-and-output-intelligence.md) | Shell Integration & Output Intelligence | P1 | Medium | Proposed |

## Recommended Ship Order

```
Phase 1 — "Make It Usable" (Week 1-2)
├── FR-001: Stdin pipes (< 1 day, unlocks workflow integration)
├── FR-004 Phase 1: Spinner + connection pre-warm (< 1 day, perceived speed)
└── FR-003: --temperature / --system flags (1 day, quick wins)

Phase 2 — "Make It Sticky" (Week 3-4)
├── FR-002 Phase 1: Interactive chat REPL (2-3 days, retention driver)
├── FR-005: --code / --raw / --shell modes (2-3 days, viral moments)
└── FR-003: Profiles (1 day, personalization)

Phase 3 — "Make It Fast" (Week 5+)
├── FR-004 Phase 2: Daemon mode (3-5 days, performance leap)
├── FR-002 Phase 2: Session persistence (1-2 days, continuity)
└── FR-004 Phase 3: Native install / Homebrew (ongoing)
```

## Design Principles

These proposals follow a shared philosophy:

1. **Pipes over prompts** — CLI tools that compose with other tools win.
2. **Speed is the feature** — Every millisecond of startup is a decision point where the user considers ChatGPT.
3. **Progressively disclosed complexity** — Simple by default, powerful when needed.
4. **Azure-native is the moat** — Lean into enterprise identity, multi-model management, and security posture.
5. **Demo-able in 15 seconds** — If you can't GIF it, it won't go viral.
