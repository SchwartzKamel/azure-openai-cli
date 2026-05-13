> **Status: GREENLIT 2026-05-13 -- Larry David. S03 retrospective shipped (`c550416`); v2.3.0 tag cut concurrent with Wave 1 dispatch.**

# S04E01 -- The Registry

> Log line: Plant the model-registry seam. A typed data structure backed by an
> embedded JSON resource lists every model the CLI knows, with capability tags,
> context window, cost tier, and a card path. Wire `--doctor` to surface it.
> The smart-defaults engine (Act II) has nowhere to anchor without this foundation.

---

You are filming **S04E01 *The Registry*** for `azure-openai-cli`. Working
directory: `/home/tweber/tools/azure-openai-cli`. Branch: `main`.

---

## Casting

- **Lead: The Maestro** -- owns the model-card schema and the capability-tag
  vocabulary. The card format must be a machine-readable contract, not just prose.
  Maestro decides what metadata matters for routing and why.
- **Co-lead: Costanza** -- owns the registry UX seam: what `--doctor` prints, how
  the registry loads at startup, and where the boundaries sit so Act II can plug in
  without reopening this file.
- **Impl: Kramer** -- C# implementation: `ModelRegistryEntry` record, `ModelRegistry`
  loader, `ModelCapability` constants, `AppJsonContext` wiring, `--doctor` handler
  changes in `Program.cs`, embedded JSON resource, and load-time validation
  (unknown capability tag -> rc=99).
- **Tests: Puddy** -- xUnit tests for registry load (happy path, unknown tag, missing
  card path, empty registry), AOT serialization round-trip, and offline
  survivability. Adversarial cases deferred to FDR.
- **Docs: Elaine** -- three seed model cards (`azure-gpt-4o-mini.md`,
  `azure-gpt-5.4-nano.md`, `local-llama.md`), `docs/model-cards/README.md`,
  and `ADR-012`. Elaine works in parallel with Kramer; her scope is `docs/` only.
- **Adversarial: FDR** -- malformed registry JSON, unknown capability tags at load,
  path traversal in card paths, and rc=99 confirmation. Runs after Wave 1 lands.

---

## Theme

S03 broke the Azure-only assumption and gave us multi-provider dispatch. The
instant a user has more than one model visible, one question replaces all the
others: *which one for this prompt, right now?* S04 answers that question -- but
not with guesswork. It answers with structured data.

E01 plants the seam: a `ModelRegistry` that lists every model the CLI knows about,
with the metadata a routing engine needs. The smart-defaults engine that *consumes*
this data ships in Act II (E05). Today we build the shelf.

Three concrete forcing functions make this the right first episode:

1. `--doctor` currently prints env-var status but knows nothing about models. After
   E01 it lists every registered model, its capability tags, and whether its provider
   env-vars are configured. That output is the first thing a new user sees when
   something goes wrong.
2. `AppJsonContext` must register `ModelRegistryEntry[]` before E02 (in-binary card
   embedding) or we face an AOT linker regression at the worst possible moment.
3. The capability-tag vocabulary must be locked now. E03 (*The Capabilities*) builds
   the `--capabilities` query subcommand directly on top of this set. Free strings
   in E01 mean E03 reopens this file. Define the enum once.

---

## Read FIRST

1. `docs/exec-reports/s04-blueprint.md` -- Act I (E01-E04), Showrunner note, the
   Landscape snapshot, and the Risks section. The theme statement ("Smart means three
   small boring things") is the acceptance bar for this entire season.
2. `azureopenai-cli/Program.cs` -- `ParseModelEnv()`, `BuildChatClient()`, the
   existing `--doctor` handler, and `ErrorAndExit()`. Understand the model-resolution
   path before touching it.
3. `azureopenai-cli/JsonGenerationContext.cs` -- `AppJsonContext` and how new types
   get registered. Every new serializable type in this episode goes here.
4. `docs/adr/ADR-009-default-model-resolution.md` -- canonical upstream for the
   smart-defaults engine. The registry is the data layer that ADR-009's
   `ResolveSmartDefault()` (E05) will consume.
5. `docs/proposals/FR-010-model-aliases-and-smart-defaults.md` -- the alias surface
   this episode touches. Check the Status section; some items are superseded by
   FR-014.
6. `.github/skills/ascii-validation.md` -- the grep one-liner. Run it on every new
   file before committing; card prose especially tends to attract smart quotes.

---

## Deliverables

| Status | Path | Notes |
|--------|------|-------|
| NEW | `azureopenai-cli/Registry/ModelCapability.cs` | Capability constants + allowed-set validator |
| NEW | `azureopenai-cli/Registry/ModelRegistryEntry.cs` | Typed record: name, provider, capabilities, context window, cost tier, card path |
| NEW | `azureopenai-cli/Registry/ModelRegistry.cs` | Loader: embedded JSON -> `ModelRegistryEntry[]`, user-override path, load-time validation |
| NEW | `azureopenai-cli/Registry/registry.json` | Seed registry: 3 entries (gpt-4o-mini, gpt-5.4-nano, llama-local) |
| NEW | `tests/AzureOpenAI_CLI.Tests/RegistryTests.cs` | xUnit tests (see Acceptance criteria) |
| NEW | `docs/model-cards/README.md` | Card format spec + index |
| NEW | `docs/model-cards/azure-gpt-4o-mini.md` | First seed card |
| NEW | `docs/model-cards/azure-gpt-5.4-nano.md` | Second seed card |
| NEW | `docs/model-cards/local-llama.md` | Third seed card (local provider) |
| NEW | `docs/adr/ADR-012-model-registry-seam.md` | Records the registry design decision and deferred alternatives |
| EDIT | `azureopenai-cli/JsonGenerationContext.cs` | Register `ModelRegistryEntry` and `ModelRegistryEntry[]` with `AppJsonContext` |
| EDIT | `azureopenai-cli/Program.cs` | Load registry at startup (after `LoadConfigEnvFrom`); extend `--doctor` handler |
| EDIT | `azureopenai-cli/AzureOpenAI_CLI.csproj` | Add `registry.json` as `<EmbeddedResource>` |

---

## Required structure

### `ModelRegistryEntry` (record, AOT-serializable)

Fields (all required unless marked optional):

| Field | Type | Notes |
|-------|------|-------|
| `name` | `string` | Deployment name; matches AZUREOPENAIMODEL entry or `"llama-local"` |
| `provider` | `string` | `"azure"`, `"foundry"`, or `"local"` |
| `capabilities` | `string[]` | Must be a subset of `ModelCapability.AllowedTags`; unknown tag -> rc=99 |
| `contextWindow` | `int` | Token count; 0 = unknown |
| `costTier` | `string` | `"low"`, `"medium"`, `"high"`, or `"unknown"` |
| `cardPath` | `string?` | Optional; relative to repo root; null -> warn, not fatal |

### `registry.json` (seed, embedded resource)

Three entries following the shape below. Repeat for `gpt-5.4-nano` and
`llama-local` (provider `"local"`, contextWindow `8192`, costTier `"unknown"`,
capabilities `["tool_calls", "streaming"]`).

```json
[
  {
    "name": "gpt-4o-mini",
    "provider": "azure",
    "capabilities": ["tool_calls", "json_mode", "streaming", "system_prompt"],
    "contextWindow": 128000,
    "costTier": "low",
    "cardPath": "docs/model-cards/azure-gpt-4o-mini.md"
  }
]
```

### Allowed capability tags (`ModelCapability.AllowedTags`)

`tool_calls`, `vision_in`, `vision_out`, `json_mode`, `streaming`, `system_prompt`

Any other string in a `capabilities` array causes `rc=99` at registry load. This
set is intentionally small. E03 (*The Capabilities*) adds more. Changes to
`AllowedTags` in E01 require a migration note in ADR-012.

### `--doctor` output (new `[registry]` section)

```text
[registry] 3 known models
  gpt-4o-mini     azure    configured   tool_calls json_mode streaming system_prompt
  gpt-5.4-nano    azure    configured   tool_calls json_mode streaming system_prompt
  llama-local     local    NOT SET      tool_calls streaming
```

"configured" means the provider's primary env-vars are non-empty:

- `azure` -> `AZUREOPENAIENDPOINT` + `AZUREOPENAIAPI`
- `foundry` -> `AZURE_FOUNDRY_ENDPOINT` + `AZURE_FOUNDRY_KEY`
- `local` -> `LLAMACPP_ENDPOINT` (or whatever S03 settled on; use a TODO comment
  if the name is not yet finalized)

Output must respect `--raw` (suppress the `[registry]` prefix and all formatting).
Output emits to stdout so scripts can consume it.

### Model card front matter and prose (for all three seed cards)

```yaml
---
model: gpt-4o-mini
provider: azure
version_noted: "2024-07-18"
capabilities: [tool_calls, json_mode, streaming, system_prompt]
context_window: 128000
cost_tier: low
---
```

Required prose H2 sections (in this order):

- `## Strengths` -- what the model does well; be specific
- `## Weaknesses` -- known failure modes; be honest, not diplomatic
- `## Default use case` -- the prompt class this model is the right default for
- `## Known limitations` -- hard stops (e.g., no vision, function-call streaming
  gaps, context-window edge cases)

### `ADR-012-model-registry-seam.md` skeleton

```text
H1: ADR-012: Model Registry Seam
H2: Status  (Accepted)
H2: Context
H2: Decision
H2: Alternatives considered
H2: Consequences
H2: Deferred to later episodes
```

### `docs/model-cards/README.md` skeleton

```text
H1: Model Cards
H2: Purpose
H2: Format  (front matter spec + required prose H2 sections)
H2: Index   (table: model, provider, cost tier, card link)
H2: Adding a new card
```

---

## Scope -- In / Out

**In E01:** `ModelRegistryEntry` record + `ModelRegistry` loader, `ModelCapability`
constants + rc=99 on unknown tag, `AppJsonContext` registration, `--doctor` registry
section, 3 seed model cards + `docs/model-cards/README.md`, `ADR-012`, unit tests.

**Not in E01:** smart-defaults engine (E05), `--capabilities` subcommand (E03),
in-binary card embedding (E02), rate-card pricing (E17/E18), routing rules (E21),
new provider adapters (S03), changes to `ParseModelEnv()` / `AZUREOPENAIMODEL` path.

---

## Files MAY touch

```text
azureopenai-cli/Registry/             (new directory + all files within)
azureopenai-cli/JsonGenerationContext.cs
azureopenai-cli/Program.cs            (--doctor handler + startup load only)
azureopenai-cli/AzureOpenAI_CLI.csproj
tests/AzureOpenAI_CLI.Tests/RegistryTests.cs
docs/model-cards/                     (new directory + README + 3 seed cards)
docs/adr/ADR-012-model-registry-seam.md
CHANGELOG.md                          (Unreleased only, per changelog-append skill)
docs/exec-reports/s04e01-*.md         (exec report -- written after work ships)
```

---

## Files MUST NOT touch

| File | Reason |
|------|--------|
| `AGENTS.md` | Orchestrator-owned; showrunner batches all roster changes |
| `docs/exec-reports/README.md` | TV guide -- orchestrator-owned; sub-agents do not edit |
| `docs/exec-reports/s04-blueprint.md` | Blueprint is frozen once greenlit; E01 implements, does not revise |
| `docs/episode-briefs/` | Planning layer; agents executing an episode do not modify its brief |
| `azureopenai-cli/Tools/*.cs` | Tool implementations are out of scope; registry does not touch the tool system in E01 |
| `azureopenai-cli/Squad/*.cs` | Squad persona system is a separate track; registry does not route personas in E01 |
| `tests/AzureOpenAI_CLI.Tests/ToolHardeningTests.cs` | In-flight in other tracks; do not touch without explicit episode scope |
| `docs/adr/ADR-009-default-model-resolution.md` | Read-only reference; ADR-009 is upstream of this registry, not downstream |

---

## Acceptance criteria

All of the following must pass before the dispatch agent declares the episode done:

1. `make preflight` exits 0.
2. `dotnet test --filter Registry` passes with at minimum:
   - `LoadRegistry_HappyPath_ReturnsThreeEntries`
   - `LoadRegistry_UnknownCapabilityTag_ExitsRc99`
   - `LoadRegistry_MissingCardPath_WarnsNotFatal`
   - `LoadRegistry_EmptyFile_ReturnsEmptyList`
   - `LoadRegistry_UserOverrideFile_ReplacesSeedEntries`
   - `ModelRegistryEntry_Serialization_RoundTrip` (no reflection; AOT-safe)
   - `LoadRegistry_OfflineFlag_DoesNotAttemptFetch`
3. `az-ai --doctor` includes a `[registry]` section listing the 3 seed models with
   capability tags and "configured" / "NOT SET" provider status.
4. `az-ai --doctor --raw` suppresses the `[registry]` prefix and formatting.
5. All 3 seed cards pass `markdownlint-cli2` with 0 errors.
6. `ADR-012` is present in the correct ADR format.
7. ASCII grep returns 0 matches on all new files (see Validation section).
8. `make publish-aot` binary is no more than 15 KB larger than the pre-episode
   baseline.

---

## Risks

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|-----------|
| `rc=99` too strict for forward compat | Medium | Low | Document in ADR-012; E03 adds `--strict-registry` to downgrade. Strict is correct for E01. |
| Registry load adds startup latency | Low | Medium | Seed is 3 entries; JSON.Deserialize under 5 KB is sub-millisecond. Add timing assertion in `RegistryTests.cs` if it shows. |
| Card paths are repo-relative but binary runs from arbitrary CWD | Medium | Medium | Resolve relative to `AppContext.BaseDirectory`. Show resolved absolute path in `--doctor`. |
| Override merge semantics ambiguous (replace vs. merge) | Medium | Low | E01 decision: override *replaces* seed list. Document in ADR-012. |
| S03 local-provider env-var not yet finalized | Low | Low | Use `LLAMACPP_ENDPOINT` as placeholder with a TODO comment. |
| AOT linker trims `ModelRegistryEntry` (only reached via JSON deserialize) | Low | High | Kramer must run `make publish-aot`, not just `dotnet build`. Add `[DynamicDependency]` if the linker drops the type. |

---

## Dispatch plan

Single-wave fleet dispatch with internal dependency ordering. Do not dispatch
Wave 2 until Wave 1 is committed and pushed to `main`.

```text
Wave 1 (parallel):
  Kramer -- Registry/*.cs, Program.cs, JsonGenerationContext.cs, AzureOpenAI_CLI.csproj
  Elaine -- docs/model-cards/, docs/adr/ADR-012-model-registry-seam.md

Wave 2 (parallel, after Wave 1 on main):
  Puddy  -- tests/AzureOpenAI_CLI.Tests/RegistryTests.cs
  FDR    -- adversarial review of Wave 1 deliverables (no code; findings go to ADR-012 appendix)

Wave 3 (after Puddy + FDR clear):
  Soup Nazi -- format + lint gate sign-off
```

Shared-file note: `Program.cs` and `JsonGenerationContext.cs` are Kramer's in Wave 1.
No other agent may edit those files until Kramer's commit is on `main`. Elaine's
scope is `docs/` only; she does not open any `.cs` file.

---

## Validation

Run before each wave's commit:

```bash
# ASCII punctuation -- 0 matches required
grep -nP '[\x{2018}\x{2019}\x{201C}\x{201D}\x{2013}\x{2014}]' \
  azureopenai-cli/Registry/*.cs docs/model-cards/*.md \
  docs/adr/ADR-012-model-registry-seam.md

# Markdown lint -- 0 errors required
NODE_OPTIONS="--max-old-space-size=4096" \
  npx markdownlint-cli2 "docs/model-cards/**/*.md" \
  "docs/adr/ADR-012-model-registry-seam.md"

# Build, registry tests, AOT publish, full preflight
dotnet build azureopenai-cli/AzureOpenAI_CLI.csproj
dotnet test tests/AzureOpenAI_CLI.Tests/AzureOpenAI_CLI.Tests.csproj \
  --filter Registry --verbosity minimal
make publish-aot   # Kramer must run this -- not optional; verifies AOT linker
make preflight     # must exit 0 before any push
```

---

## Commit conventions

See `.github/skills/commit.md`. Use `-c commit.gpgsign=false`. Trailer required.

Wave 1 Kramer (primary deliverable -- use `feat`):

```text
feat(registry): introduce ModelRegistry seam and seed data

<3-5 line body: what was added and why>

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>
```

Elaine uses `docs(model-cards)`. Puddy uses `test(registry)`. Do not use `chore`.

---

## Push instruction

```bash
git push origin main
```

On non-fast-forward (another agent pushed concurrently):

```bash
git pull --rebase origin main
git push origin main
```

Do not force-push. Rebase only. No merge commits.

---

## On completion

```sql
UPDATE todos SET status = 'done' WHERE id = 's04e01-the-registry';
```

Return to showrunner:

- Commit SHA(s) per wave
- Exact list of files created and edited, with line counts for new files
- Any deviation from the Deliverables table and the reason
- Any capability tags added to `AllowedTags` beyond the E01 baseline, and why
- Gaps noticed but not fixed -- flag for E02 or ADR-012 appendix

---

## References

- `docs/exec-reports/s04-blueprint.md` -- Act I (E01-E04), Showrunner note, Risks
- `docs/adr/ADR-009-default-model-resolution.md` -- upstream for smart defaults
- `docs/proposals/FR-010-model-aliases-and-smart-defaults.md` -- alias surface
- `docs/proposals/FR-015-pattern-library-and-cost-estimator.md` -- cost-tier vocabulary
- `.github/skills/episode-brief.md` -- canonical brief format (this file follows it)
- `.github/skills/shared-file-protocol.md` -- file ownership rules for concurrent agents
- `docs/exec-reports/s03e01-the-yada-yada-strikes-back.md` -- S03 opener; shape reference
