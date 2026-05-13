# ADR-012: Model Registry Seam

## Status

Accepted -- 2026-05-13 (S04E01)

## Context

S03 (*Local & Multi-Provider*) broke the implicit assumption that `az-ai` talks to
exactly one Azure OpenAI endpoint. After S03, a single binary can route prompts to
Azure OpenAI, Azure AI Foundry, and (in the local-provider track) a llama.cpp
server. The instant a user has more than one model visible, one question displaces all
others: *which model for this prompt, right now?*

Three concrete gaps make a structured registry urgent at the start of S04:

1. `--doctor` knows nothing about models. It reports env-var presence but cannot
   tell the user which models are registered, what they support, or whether the
   necessary env-vars for each provider are set. A new user whose `--doctor` output
   shows "configured" still has no idea what the model can do.
2. The smart-defaults engine planned for E05 needs somewhere to anchor. Without a
   typed data structure it has no capability surface to query, no cost tier to prefer,
   and no context window to respect. Building E05 on free strings or a hardcoded
   array is a dead end.
3. The capability-tag vocabulary must be locked before E03 (*The Capabilities*)
   builds the `--capabilities` subcommand. Free strings in E01 force E03 to reopen
   this file and risk a flag-day rename.

`docs/exec-reports/s04-blueprint.md` Act I frames this as "plant the seam first":
a typed data structure backed by an embedded JSON resource, with a stable schema that
Acts II and III can consume without reopening the implementation.

## Decision

Introduce a `ModelRegistry` loader backed by an embedded JSON resource
(`azureopenai-cli/Registry/registry.json`) and an optional user-override file at
`~/.config/az-ai/registry.json`.

Key design points:

- **Typed record:** `ModelRegistryEntry` is a C# `record` with fields `name`,
  `provider`, `capabilities`, `contextWindow`, `costTier`, and optional `cardPath`.
  All fields are AOT-serializable via `AppJsonContext`.
- **Capability tags closed set:** `ModelCapability.AllowedTags` is the single source
  of truth for valid capability strings. The E01 baseline set is:
  `tool_calls`, `vision_in`, `vision_out`, `json_mode`, `streaming`, `system_prompt`.
  Any tag not in `AllowedTags` causes the registry load to exit with `rc=99`. This
  is intentionally strict. E03 (*The Capabilities*) adds `--strict-registry` if a
  graceful-degradation path is needed; strict is correct for E01.
- **User override replaces, does not merge:** if `~/.config/az-ai/registry.json`
  exists, it replaces the embedded seed list entirely. There is no deep-merge
  semantics. This is the simplest model and the one least likely to produce
  surprising results when a user experiments with a local model. See Risk row 4 in
  the episode brief.
- **Card paths resolve relative to `AppContext.BaseDirectory`:** the binary may run
  from an arbitrary working directory. Resolving card paths from the binary's own
  directory (typically `~/.local/bin/`) rather than `CWD` ensures `--doctor` can
  show a consistent resolved path. A missing card path is a warning, not a fatal
  error; the registry entry is still returned.
- **Load happens after `LoadConfigEnvFrom()` at startup:** provider env-var status
  in `--doctor` is evaluated against the loaded environment, so auto-config must run
  first.

## Alternatives considered

### (a) Hardcoded `static readonly` C# array

A `static readonly ModelRegistryEntry[]` in `Program.cs` gives AOT-safe access with
zero file I/O overhead. Rejected because the user cannot override it without
recompiling the binary. Any user who needs a local model or a Foundry deployment not
in the seed list is stuck. The registry's most important property is that it is
user-extensible without a rebuild.

### (b) External JSON file only (no embedded seed)

Ship no embedded data; require the user to create `~/.config/az-ai/registry.json`
manually. Rejected on two grounds: (1) zero-config UX is a first-class `az-ai`
requirement -- a fresh install must work without manual file creation; (2) the
offline-first property (binary works without network access) depends on embedded
seed data being present. An absent file at startup would either degrade gracefully
(empty registry, useless `--doctor`) or fail fatally (rc=99, broken UX).

### (c) Free-string capability tags

Store capability tags as arbitrary strings with no validation. Rejected because E03
(*The Capabilities*) builds a query subcommand on top of this vocabulary. Without a
closed set, E03 must defensively handle any string a user or future agent writes into
a registry file, and the flag-day cost of renaming a tag after E03 ships is high.
Locking the vocabulary now costs one enum amendment per new tag, which is cheap
compared to a migration.

## Consequences

### Positive

- E03 (*The Capabilities*), E05 (*Smart Defaults*), E17 (*Cost Visibility*), and
  E21 (*Routing Rules*) all have a typed, structured data source to anchor against.
  None of those episodes need to solve the "where does model metadata live?" question.
- `--doctor` becomes genuinely useful for new users: it lists every registered model,
  its capability surface, and its provider configuration status in a single command.
- The user-override file enables local-model experimentation without recompiling or
  modifying the source tree.

### Negative

- The capability set is closed. Adding a new tag (`vision_in`, a future
  `audio_in`, etc.) requires amending `ModelCapability.AllowedTags` and appending a
  migration note to this ADR. Each amendment is cheap in isolation but accumulates if
  the tag set grows rapidly. This ADR will be annotated, not superseded, on each
  amendment.
- The override-replaces-seed semantic means a user who wants to extend the seed list
  must copy all seed entries into their override file. There is no partial override.
  This may be revisited in a later episode if the friction proves meaningful.

### Neutral

- AOT serialization burden falls on `AppJsonContext` in
  `azureopenai-cli/JsonGenerationContext.cs`. Kramer must register
  `ModelRegistryEntry` and `ModelRegistryEntry[]` there; otherwise the AOT linker
  will trim the type. This is consistent with every other serializable type in the
  codebase and adds no new pattern.

## Deferred to later episodes

| Item | Episode |
|------|---------|
| Smart-defaults engine consuming registry capability tags | E05 |
| `--capabilities` query subcommand | E03 |
| In-binary card embedding (card prose as embedded resource) | E02 |
| Rate-card pricing metadata in registry entries | E17/E18 |
| Routing rules driven by capability tags | E21 |
| `--strict-registry` flag to downgrade rc=99 to a warning | E03 |
