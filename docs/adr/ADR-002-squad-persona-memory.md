# ADR-002: Squad Persona + Memory Architecture

- **Status**: Accepted — 2026-04-09
- **Deciders**: Core maintainers
- **Related**: [`ADR-001-native-aot-recommended.md`](./ADR-001-native-aot-recommended.md),
  CHANGELOG v1.5.0, [bradygaster/squad](https://github.com/bradygaster/squad)

## Context

`azure-openai-cli` is positioned as a **one-process, one-binary** agent runtime
for Espanso/AutoHotkey-style workflows (see [ADR-001](./ADR-001-native-aot-recommended.md)).
Users frequently asked for multiple specialized assistants — a coder, a
reviewer, a security auditor — each with its own system prompt, tool set, and
accumulated project knowledge.

The reference point was [`bradygaster/squad`](https://github.com/bradygaster/squad),
a Node/npm-based orchestrator that treats AI personas as committable team
members. The concept fit our use case; the implementation did not:

- **npm runtime** contradicts ADR-001's single-binary, ~5 ms cold-start target.
- **Cloud-side routing** (LLM-decided dispatch) adds a full round-trip before
  the actual task even begins — unacceptable for interactive injection.
- **Stateless agents** reset their knowledge every session; users wanted
  context that *compounds* across runs and survives in version control.

The hard requirements we needed to satisfy:

1. **Multiple personas** with independent system prompts, tool selections,
   and optional model overrides.
2. **Persistent memory per persona** that lives in the repo, is committable,
   and is readable by humans.
3. **Deterministic routing** — given the same prompt, the same persona is
   selected, with no cloud round-trip and no perceptible latency.
4. **AOT-clean** — no reflection-based JSON, no dynamic code generation,
   no runtime assembly loading.
5. **Zero new dependencies** — everything must build with the existing
   `System.Text.Json` + .NET 10 BCL surface.
6. **Shared decision log** that any persona can append to, giving the team
   a single audit trail.

## Decision

The Squad system is implemented as four small, internal-sealed types under
`azureopenai-cli/Squad/`, backed by plain files in the working directory.

### File layout

```
<project root>/
├── .squad.json              # team config (committed)
└── .squad/
    ├── history/             # per-persona memory (committed)
    │   ├── coder.md
    │   ├── reviewer.md
    │   └── ...
    ├── decisions.md         # shared decision log (committed)
    └── README.md            # user-facing explainer
```

### Component responsibilities

| Component          | Responsibility                                                                 |
| ------------------ | ------------------------------------------------------------------------------ |
| `SquadConfig`      | Load/save `.squad.json`; expose `Personas`, `Routing`, `Team` metadata.        |
| `SquadInitializer` | Scaffold `.squad.json` + `.squad/` tree with 5 default personas and rules.    |
| `SquadCoordinator` | Deterministic keyword-score routing from prompt → `PersonaConfig`.             |
| `PersonaMemory`    | Read/append per-persona history; read/append shared decisions; enforce cap.   |

### Routing algorithm

`SquadCoordinator.Route` lowercases the incoming prompt and each rule pattern,
splits the pattern on commas (trimmed, empty-removed), counts how many
keywords appear in the prompt, and picks the **highest-scoring rule**. Ties
go to declaration order. If no rule scores > 0, the first persona is returned
as the fallback; if the prompt is empty or no rules are configured, likewise.

This is intentionally boring: it runs in microseconds, has no network
dependency, and produces the same answer on every machine.

### Memory model

- Each persona owns a single Markdown file at `.squad/history/<name>.md`.
- On each session, `PersonaMemory.AppendHistory` appends a timestamped
  section: UTC timestamp, truncated task (200 chars), truncated result
  (500 chars).
- On read, history is capped at **32 KB (`MaxHistoryBytes = 32_768`)**.
  When exceeded, only the **tail** is returned, prefixed with
  `...(earlier history truncated)...`. Most-recent wins.
- Decisions follow the same 32 KB tail-truncation rule via
  `PersonaMemory.ReadDecisions`.
- All writes use `File.AppendAllText` — no locking, no database, no cache.

### JSON contract

`.squad.json` is serialized through the source-generated
`AppJsonContext.Default.SquadConfig` serializer. No reflection-based
`JsonSerializer` overloads appear in the Squad code path. This was enforced
as part of the v1.8.0 AOT-cleanup pass (see CHANGELOG).

## Consequences

### Positive

- **Zero new dependencies.** The entire Squad subsystem uses
  `System.Text.Json`, `System.IO`, and LINQ — already present for other
  reasons.
- **AOT-clean.** Source-generated JSON + no reflection means Squad adds zero
  AOT warnings and no runtime-codegen risk (preserves ADR-001's guarantees).
- **Deterministic, zero-latency routing.** Keyword scoring completes before
  any network call; repeated prompts always pick the same persona, which is
  testable and debuggable.
- **Memory survives across runs and is Git-friendly.** `.squad/` is plain
  Markdown — diffable, reviewable in PRs, auditable long after a session.
- **One process, one binary.** Nothing in Squad requires a background
  service, a database, or an external indexer.
- **Per-persona model override.** `PersonaConfig.Model` lets the architect
  persona run on a reasoning model while the coder runs on a cheaper one,
  without per-invocation plumbing.

### Negative

- **Keyword routing is not semantic.** A prompt like *"document the refactor
  that fixed the bug"* matches both `writer` and `coder` patterns; the
  highest raw keyword count wins. Users who need stricter routing must
  either pass `--persona <name>` explicitly or curate the pattern list.
- **32 KB cap requires future pruning logic.** Tail truncation keeps reads
  bounded but the **on-disk file grows unbounded** across sessions. A
  follow-up compaction pass (e.g., LLM-summarize the head) is not yet
  implemented.
- **No cloud sync, no multi-agent coordination.** Two developers on the
  same repo generate independent history entries; merges are manual
  Git merges of Markdown. There is no CRDT, no server, no conflict
  resolution beyond what Git provides.
- **Write-path is not concurrent-safe.** `File.AppendAllText` on two
  simultaneous `az-ai` invocations against the same persona can interleave
  entries. Acceptable for interactive single-user CLI use; not acceptable
  for a shared daemon (which we explicitly do not build — see ADR-001).
- **Default personas are opinionated.** Five personas with English
  system prompts are scaffolded by `SquadInitializer.CreateDefaultConfig`.
  Users with different team shapes must edit `.squad.json` after
  `--squad-init`.

## Alternatives Considered

### LangChain-style agent framework (rejected)

Full agent/graph frameworks (LangChain, CrewAI, AutoGen) provide
persona orchestration, tool routing, and memory out of the box. **Rejected**
because:

- Primary ecosystems are Python/TypeScript — incompatible with a single
  AOT-compiled .NET binary.
- Bring heavyweight transitive dependency graphs that would dwarf the rest
  of the CLI.
- Runtime cost (framework init, dynamic dispatch) is incompatible with
  ADR-001's ~5 ms cold-start budget.

### Microsoft Semantic Kernel routing (rejected)

Semantic Kernel has first-party .NET support and a `Planner` abstraction
that could drive persona selection. **Rejected** because:

- Adds a substantial `Microsoft.SemanticKernel.*` dependency surface,
  several of which pull `Microsoft.Extensions.*` host abstractions the
  CLI otherwise avoids.
- Planner-based routing is LLM-driven — it requires at least one extra
  cloud round-trip per dispatch, which we explicitly did not want.
- Measurably slower startup due to DI container construction.

### Embedding-based semantic routing (rejected)

Route by embedding the prompt and computing cosine similarity against
per-persona descriptor embeddings. **Rejected** because:

- Requires shipping an embedding model (ONNX runtime, model weights) or
  making a cloud embedding call — both break the single-binary, zero-
  cold-start story.
- ONNX runtime is not trivially AOT-compatible on all target RIDs.
- The observed accuracy gain over keyword scoring did not justify the
  complexity for the short, keyword-dense prompts this CLI receives.

### SQLite-backed memory (rejected)

Store persona history and decisions in a local SQLite file. **Rejected**
because:

- Markdown is human-readable and diff-friendly in PRs; SQLite blobs are
  not.
- Adds `Microsoft.Data.Sqlite` + a native `e_sqlite3` dependency, which
  complicates the AOT publish matrix.
- Concurrency benefits are marginal for a single-user interactive CLI.

## References

- [`azureopenai-cli/Squad/SquadConfig.cs`](../../azureopenai-cli/Squad/SquadConfig.cs)
- [`azureopenai-cli/Squad/SquadCoordinator.cs`](../../azureopenai-cli/Squad/SquadCoordinator.cs)
- [`azureopenai-cli/Squad/SquadInitializer.cs`](../../azureopenai-cli/Squad/SquadInitializer.cs)
- [`azureopenai-cli/Squad/PersonaMemory.cs`](../../azureopenai-cli/Squad/PersonaMemory.cs)
- [`CHANGELOG.md`](../../CHANGELOG.md) v1.5.0 — Squad introduction;
  v1.8.0 — AOT cleanup of Squad JSON path.
- [`ADR-001-native-aot-recommended.md`](./ADR-001-native-aot-recommended.md) —
  startup/size constraints that shaped this design.
