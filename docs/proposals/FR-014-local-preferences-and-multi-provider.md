# FR-014 -- Local User Preferences + Multi-Provider Profiles

**Status:** Design (Costanza, 2026-04-24 -- supersedes 2026-04-20 stub)
**Owner:** Costanza (product shape), Kramer (implementation), Jerry (AOT + HTTP pipeline), Newman (credential handling), Morty (rate-card integration), Elaine (docs + ADR)
**Related & absorbed:** **FR-003** (local user preferences -- *absorbed*), **FR-009** (`--config set` + directory overrides -- *absorbed*), **FR-010** (model aliases & env-var normalization -- *absorbed*). Downstream consumers: **FR-015** (cost estimator -- per-provider rate cards), **FR-018** (llama.cpp/Ollama adapter), **FR-019** (gemma.cpp adapter), **FR-020** (NIM + per-trigger routing). Peer: **FR-013** (MCP tool allowlists feed the same preferences file).

---

## 1. Problem Statement

az-ai config is scattered across `.env`, `~/.azureopenai-cli.json`, and CLI flags. FR-003/009/010 proposed three adjacent fixes that never unified. Meanwhile, FR-018/019/020 all declare "hard dependency on FR-014" and assume a `preferences.toml` that does not exist. The user's mission is explicit: **local preferences and multi-provider selection are a first-order roadmap priority, second only to MCP.**

One file. One schema. One precedence chain. Multiple providers. Ship it.

---

## 2. Scope Decisions (the Costanza calls)

| Decision | Call | Rationale |
|---|---|---|
| **File format** | **JSON, not TOML** | AOT is non-negotiable (FR-006/016). No mainstream TOML parser is source-gen / reflection-free -- Tomlyn uses reflection; `System.Text.Json` already ships with a source generator we depend on. Every AOT trim/warning fight we avoid is 50 ms of cold-start banked. TOML stays on the FR-021+ wishlist; FR-018/020 references to `preferences.toml` are corrected in §12. |
| **Canonical path** | **`~/.config/az-ai/preferences.json`** (XDG), with legacy fallback to `~/.azureopenai-cli.json` | The legacy path stays *readable forever*; the new path is *written* going forward. See §9. |
| **Directory override** | **`./.az-ai/preferences.json`** (preferred) or `./.azureopenai-cli.json` (legacy) | Matches FR-009's walk-up-to-root behaviour; the `.az-ai/` directory also hosts local plugin/MCP manifests later. |
| **Absorbs** | FR-003, FR-009, FR-010 | Their exit criteria become FR-014 exit criteria. Those files stay in the tree as historical context and get a `**Status: Superseded by FR-014**` banner. |
| **Azure remains default** | Yes | Multi-provider is a safety valve, not a repositioning (per `docs/competitive-analysis.md`). Default provider is `azure` unless explicitly changed. |

---

## 3. Preferences File -- Location, Merge Order, Discovery

```
Precedence (highest wins):
  1. CLI flag                           (e.g. --provider, --model, --temperature)
  2. Environment variable               (AZ_PROVIDER, AZ_PROFILE, AZUREOPENAIAPI, …)
  3. Active profile                     (profiles.<name> in the merged preferences)
  4. Directory override                 (./.az-ai/preferences.json walking up to /)
  5. Global preferences                 (~/.config/az-ai/preferences.json)
  6. Legacy global                      (~/.azureopenai-cli.json -- read-only)
  7. Legacy .env file                   (.env at CWD -- credentials only)
  8. Hardcoded defaults
```

Merge semantics: deep-merge for objects; last-writer-wins for scalars; arrays replace (not concat) -- concat semantics are a footgun for allowlists.

Discovery: `az-ai --config show` prints the resolved value and the **source layer** for every key (already partially implemented per FR-009). New: it also prints which provider profile was resolved and the exact file path that contributed.

---

## 4. Preferences Schema -- Full Example

```jsonc
{
  "$schema": "https://raw.githubusercontent.com/<org>/az-ai/main/schemas/preferences-v1.json",
  "schemaVersion": 1,

  // ─── Global defaults ──────────────────────────────────────────────
  "defaultProvider": "azure",
  "defaultProfile":  "work",            // nullable

  "history": {
    "maxEntries": 500,
    "path": "~/.config/az-ai/history.jsonl",
    "redactApiKeys": true
  },

  // ─── Provider profiles (the adapter registry) ─────────────────────
  "providers": {
    "azure": {
      "kind": "azure-openai",
      "endpoint":    "${AZURE_OPENAI_ENDPOINT}",
      "apiKeyEnv":   "AZUREOPENAIAPI",
      "apiVersion":  "2024-10-21",
      "deployments": ["gpt-4o", "gpt-4.1-mini", "o3-mini"],
      "aliases": { "4o": "gpt-4o", "mini": "gpt-4.1-mini", "smart": "gpt-4o" },
      "capabilities": { "tools": true, "streaming": true, "vision": true }
    },

    "openai": {
      "kind": "openai",
      "endpoint":  "https://api.openai.com/v1",
      "apiKeyEnv": "OPENAI_API_KEY",
      "aliases":   { "fast": "gpt-5-nano", "smart": "gpt-5" }
    },

    "anthropic": {
      "kind": "anthropic",
      "endpoint":  "https://api.anthropic.com",
      "apiKeyEnv": "ANTHROPIC_API_KEY",
      "apiVersion": "2023-06-01",
      "aliases":   { "opus": "claude-opus-4", "sonnet": "claude-sonnet-4" }
    },

    "gemini": {
      "kind": "google-gemini",
      "endpoint":  "https://generativelanguage.googleapis.com/v1beta",
      "apiKeyEnv": "GEMINI_API_KEY",
      "aliases":   { "flash": "gemini-2.5-flash", "pro": "gemini-2.5-pro" }
    },

    "ollama": {
      "kind": "openai-compatible",
      "endpoint": "http://localhost:11434/v1",
      "apiKeyEnv": null,                // unauthenticated local
      "allowLan": false,                // Newman: SSRF guard (FR-018 §5.1)
      "capabilities": { "tools": false, "streaming": true },
      "aliases": { "g3": "gemma3:27b", "local": "gemma3:27b" }
    },

    "llamacpp": {
      "kind": "openai-compatible",
      "endpoint": "http://localhost:8080/v1",
      "apiKeyEnv": null,
      "capabilities": { "tools": false, "streaming": true }
    },

    "nim": {
      "kind": "openai-compatible",
      "endpoint": "http://localhost:8000/v1",
      "apiKeyEnv": "NIM_BEARER_TOKEN",
      "imageDigest": "sha256:…",        // FR-020 §5 pinning
      "capabilities": { "tools": false, "streaming": true },
      "aliases": { "grammar": "meta/llama-3.1-8b-instruct" }
    }
  },

  // ─── Named profiles (bundles of shared defaults) ──────────────────
  "profiles": {
    "work": {
      "provider": "azure",
      "model": "4o",
      "temperature": 0.3,
      "maxTokens": 8000,
      "timeoutSeconds": 120,
      "systemPrompt": "You are a concise CLI assistant."
    },
    "code": {
      "provider": "anthropic",
      "model": "opus",
      "temperature": 0.1,
      "systemPrompt": "Output only code unless asked to explain."
    },
    "creative": {
      "provider": "openai",
      "model": "smart",
      "temperature": 0.9
    },
    "offline": {
      "provider": "ollama",
      "model": "g3",
      "temperature": 0.4
    }
  },

  // ─── Per-trigger routing (feeds FR-020) ───────────────────────────
  "routing": {
    "ai-fast":    { "provider": "openai",    "model": "fast" },
    "ai-code":    { "provider": "anthropic", "model": "opus" },
    "ai-local":   { "provider": "ollama",    "model": "g3" },
    "ai-grammar": { "provider": "nim",       "model": "grammar",
                    "maxInputWords": 200,
                    "fallback":      { "provider": "azure", "model": "mini" } }
  },

  // ─── Tool allowlists (feeds FR-013 MCP + Tools/) ──────────────────
  "tools": {
    "allow": ["web_fetch", "shell", "file_read"],
    "deny":  ["file_write:/etc/**", "shell:rm -rf *"],
    "perProvider": {
      "ollama": { "allow": [] }         // local models: no tools by default
    }
  },

  // ─── Tool-specific config blocks (extensible, opaque to core) ─────
  "toolConfig": {
    "nim": {
      "systemdUnit": "nim-grammar.service",
      "warmupTimeoutSeconds": 45
    },
    "web_fetch": {
      "allowDomains": ["github.com", "*.microsoft.com"]
    }
  },

  // ─── Env-var mapping overrides ────────────────────────────────────
  // Lets users rename env vars without code changes.
  "envOverrides": {
    "AZURE_OPENAI_ENDPOINT": "AZUREOPENAIENDPOINT",    // legacy alias
    "AZURE_OPENAI_MODEL":    "AZUREOPENAIMODEL"
  }
}
```

**Dollar-brace substitution** (`${ENV_VAR}`) is evaluated at load time. Missing vars are tolerated for optional fields and hard-fail only when the resolved value is *used* -- lazy validation keeps cold-start fast when you're using only one provider.

---

## 5. Provider Adapter Abstraction

**Decision:** Reuse `Microsoft.Extensions.AI.IChatClient` as the universal contract. It already underlies the Azure OpenAI path in v2 (`azureopenai-cli-v2/Program.cs:197-198`), MAF/Agent Framework already speaks it, streaming is native, tool-calling is modeled.

Wrap it with a **thin factory**, not a second interface:

```
IProviderFactory
    ├─ IReadOnlyList<string> KnownKinds { get; }
    └─ IChatClient Create(ProviderProfile profile, ProviderContext ctx)

record ProviderProfile(
    string Name,
    string Kind,              // "azure-openai" | "openai" | "anthropic" |
                              //  "google-gemini" | "openai-compatible"
    string Endpoint,
    string? ApiKeyEnv,
    string? ApiVersion,
    IReadOnlyDictionary<string,string> Aliases,
    ProviderCapabilities Capabilities,
    bool AllowLan);

record ProviderContext(
    ILogger Log,
    HttpClient Http,          // shared, prewarmed (FR-007)
    CredentialResolver Cred);
```

### Per-kind adapter matrix

| Kind | Package / wire | Credential source | Tool calling | Streaming | Notes |
|---|---|---|---|---|---|
| `azure-openai` | `Azure.AI.OpenAI` (already in csproj) | `apiKeyEnv` → `DefaultAzureCredential` fallback | ✅ | ✅ | First-class; default. Unchanged from today. |
| `openai` | `OpenAI` official SDK OR direct HTTP to `/v1/chat/completions` | `OPENAI_API_KEY` | ✅ | ✅ | **Phase 1** -- cheapest adapter since wire == Azure's base. |
| `anthropic` | Direct HTTP to `/v1/messages` + message translator | `ANTHROPIC_API_KEY` header `x-api-key` | ✅ (different schema) | ✅ (SSE) | **Phase 2** -- needs a request/response transformer; use `IChatClient` adapter shim. |
| `google-gemini` | Direct HTTP to `v1beta/models/{model}:generateContent` | `GEMINI_API_KEY` query or header | ✅ (`functionDeclarations`) | ✅ | **Phase 3** -- translator similar to Anthropic. |
| `openai-compatible` | Direct HTTP to `/v1/chat/completions` | Bearer-token env or anonymous | Capability-gated via preferences | ✅ | Covers **Ollama, llama.cpp, NIM, LM Studio, vLLM** -- one adapter, N endpoints. |
| `gemma-cpp` (future) | Local subprocess + UDS (see FR-019) | none | ❌ | ✅ | FR-019 owns its own IChatClient wrapping the subprocess. |

`IChatClient`-shaped adapters mean **zero changes** to Program.cs call sites -- the existing `chatClient.CompleteStreamingAsync(...)` loop works for every provider.

---

## 6. CLI Surface

### New flags (layered on FR-003/009/010 surface)

| Flag | Behaviour |
|---|---|
| `--provider <name>` | Hard-select a provider profile for this invocation. Beats profile, beats env. |
| `--profile <name>` | Activate a named profile from `profiles.<name>`. Provides defaults for provider, model, temperature, system prompt. |
| `--trigger <name>` | (FR-020) Consult `routing.<name>` table. Ignored if `--provider` is set (precedence). |
| `--model <alias-or-name>` | Per-invocation override, resolved against the *chosen* provider's alias table. |
| `--endpoint <url>` | One-off endpoint override. Validated by Newman's SSRF allowlist. |
| `--list-providers` | Print resolved providers + capabilities + reachability status. |
| `--list-profiles` | Print profiles and their resolved (provider, model) tuples. |
| `--check <provider>` | Dry-run ping to `/v1/models`; no prompt sent. |
| `--config set <key> <value>` | Persist to global prefs (extends FR-009). |
| `--config set --local <key> <value>` | Persist to `./.az-ai/preferences.json`. |
| `--config set --profile <p> <key> <value>` | Write into a specific profile block. |
| `--config get <key>` | Show value + source layer. |
| `--config reset <key>` | Remove the override. |
| `--config migrate` | Copy `~/.azureopenai-cli.json` → new canonical path, annotate source. |

### Env-var fallbacks

| Env var | Effect |
|---|---|
| `AZ_PROVIDER` | Same as `--provider`. |
| `AZ_PROFILE` | Same as `--profile`. |
| `AZ_AI_PREFERENCES` | Override preferences file path (dev/test use). |
| `AZUREOPENAIENDPOINT` / `AZUREOPENAIAPI` / `AZUREOPENAIMODEL` | Legacy; still honoured. |
| `AZURE_OPENAI_*` | New canonical names (FR-010). Shim maps both. |
| `OPENAI_API_KEY`, `ANTHROPIC_API_KEY`, `GEMINI_API_KEY`, `NIM_BEARER_TOKEN` | Per-provider credentials. |

### Precedence (repeat for clarity -- this is *the* contract)

```
CLI flag  >  env var  >  --profile  >  routing[--trigger]  >
  directory prefs  >  global prefs  >  legacy ~/.azureopenai-cli.json  >
    .env  >  hardcoded default
```

---

## 7. Per-Trigger Routing (feeds FR-020)

The `routing` table is a `Dictionary<string, RoutingEntry>` where `RoutingEntry` is:

```
record RoutingEntry(
    string Provider,
    string? Model,
    int? MaxInputWords,       // length-gating threshold
    int? MaxInputTokens,      // alternative to words for binary-stream users
    RoutingEntry? Fallback,   // recursive: fallback chain length capped at 3
    string? SystemPromptRef); // name into a systemPrompts table (future)
```

Resolution algorithm (O(1) dict lookup, zero allocation on the hot path):

```
resolve(trigger, inputText):
    entry = routing.get(trigger) or return default_provider
    if entry.MaxInputWords and wordCount(inputText) > entry.MaxInputWords:
        return resolve_fallback(entry.Fallback)
    if not is_reachable(entry.Provider):          # cached probe result
        return resolve_fallback(entry.Provider.fallback_policy)
    return entry
```

Routing lives in `Preferences/TriggerRouter.cs`. It is **pure** (no I/O except reachability cache) so it's trivially unit-testable and safe under AOT.

---

## 8. Credential Management (Newman's bar)

1. **Never store secrets in `preferences.json`.** Parser rejects any value in `apiKey` fields that isn't a known dummy (`""`, `"ollama"`, `"not-needed"`). Real keys come from env vars or OS keyring only. Scanner runs at load time -- `refuseKeyLikePatterns(["^sk-", "^AIza", "^xai-", "^[A-Za-z0-9]{40,}$"])`.
2. **`apiKeyEnv` is an env-var *name*, not a value.** The loader dereferences it lazily (only when that provider is selected). Typos in env-var names produce a clear error with the name it tried.
3. **OS keyring hook (Phase 3).** `credentialSource: "keyring:az-ai/anthropic"` reads via `libsecret`/Keychain/WinCred through a platform shim. Not required for v1.
4. **Redaction.** The loader wraps every credential in a `CredentialHandle` whose `ToString()` returns `"[REDACTED]"`. Logging subsystem uses type-based filters (not regex). Telemetry (`--json`) never includes credentials.
5. **Existing `AZUREOPENAIAPI` keeps working** with zero user action -- it's the default `apiKeyEnv` for `providers.azure`.
6. **Per-directory prefs** cannot declare credentials at all (parse-time refusal). Directory overrides can reference only pre-declared global provider profiles by name, or re-point `apiKeyEnv` to a different env-var name. This closes the "cloned a repo with a `.az-ai/preferences.json` that silently exfils my key" vector.

---

## 9. Integration with FR-015 Cost Estimator

- Rate cards are provider-scoped: `RateCards/<provider>.json` (Azure, OpenAI, Anthropic, Gemini). Ollama/llama.cpp have no rate card; the estimator prints `"local -- no cost"` rather than `$0.0000` (Morty: don't encode "free" into the UX).
- Estimator consumes the *resolved* provider -- selection happens *before* the estimator runs, not after.
- Comparative-cost UX: `--estimate --compare azure,openai,anthropic` runs the estimator across providers in parallel and prints a table. Useful for convincing users Azure is cheaper without forcing them to switch.
- `routing.<trigger>` entries get a per-trigger estimator output showing which provider fires for that trigger.

---

## 10. AOT Compatibility

| Type | Registration | Notes |
|---|---|---|
| `Preferences` (root) | `AppJsonContext` | Top-level deserialization target. |
| `ProviderProfile`, `ProviderCapabilities`, `FallbackPolicy`, `RoutingEntry`, `RoutingTable`, `ProfileBundle`, `ToolConfig`, `HistoryConfig` | `AppJsonContext` | All `internal sealed`, all property-based (no constructor-arg-only records -- source gen limitation). |
| `Dictionary<string, ProviderProfile>`, `Dictionary<string, RoutingEntry>`, `Dictionary<string, ProfileBundle>`, `Dictionary<string, string>` | `AppJsonContext` | Already required for FR-010 aliases. |
| `CredentialHandle` | NOT serialized | Marked `[JsonIgnore]` everywhere. |

TOML is explicitly out -- see §2. Re-evaluate in v2.2 if a Microsoft-blessed AOT-safe TOML lib ships.

Cold-start budget: the whole preferences load + merge + validate path must stay ≤ 1.5 ms on linux-x64 so we don't eat our 5.4 ms lead. Lazy provider-profile resolution is non-negotiable -- we parse all providers but validate (env-var deref, endpoint check) only the one we actually use.

---

## 11. Backward Compatibility

1. **v1 users pasting `~/.azureopenai-cli.json` at the legacy path: zero breakage.** The loader reads it if the new path doesn't exist and treats it as a flat `providers.azure` + `profiles.default` equivalent.
2. **`--set-model`, `--config show`** keep working (already shipped).
3. **`AZUREOPENAIAPI` / `AZUREOPENAIENDPOINT` / `AZUREOPENAIMODEL`** keep working forever. The new `AZURE_OPENAI_*` names are documented as canonical but legacy names never deprecate.
4. **First run after upgrade:** if only the legacy file exists, print a one-line tip (`az-ai --config migrate` to move to the XDG path) and proceed. No forced migration.
5. **`.env` files in Docker** keep flowing through -- `.env` is credentials-plus-defaults and is now layer #7 in the precedence chain (above hardcoded defaults only).
6. **FR-003/009/010 JSON keys (`Temperature`, `MaxTokens`, `TimeoutSeconds`, `SystemPrompt`, `ModelAliases`)** are recognized and promoted into the new schema at load time. Users who already ran `--config set temperature 0.2` stay exactly where they were.

---

## 12. Prototype Class Layout

```
azureopenai-cli/
├─ Preferences/
│   ├─ Preferences.cs                 // root record + JsonSerializable types
│   ├─ PreferencesLoader.cs           // layer discovery + merge + env-subst
│   ├─ PreferencesWriter.cs           // --config set plumbing (global/local/profile)
│   ├─ PreferencesMigrator.cs         // legacy → new-path migration
│   ├─ ProfileResolver.cs             // --profile + env + routing resolution
│   ├─ TriggerRouter.cs               // per-trigger routing + length-gating
│   ├─ CredentialResolver.cs          // env-var deref + redaction handle
│   └─ PreferencesValidator.cs        // key-like-string refusal, SSRF allowlist
│
├─ Providers/
│   ├─ IProviderFactory.cs            // single entry point: Create(profile) → IChatClient
│   ├─ ProviderRegistry.cs            // kind → factory map (AOT-safe, no reflection)
│   ├─ AzureOpenAIProvider.cs         // wraps existing Azure.AI.OpenAI path (current behaviour)
│   ├─ OpenAIProvider.cs              // Phase 1
│   ├─ AnthropicProvider.cs           // Phase 2 -- includes message translator
│   ├─ GeminiProvider.cs              // Phase 3
│   ├─ OpenAICompatibleProvider.cs    // Ollama / llama.cpp / NIM / vLLM / LM Studio
│   └─ Translators/
│       ├─ AnthropicMessageTranslator.cs
│       └─ GeminiMessageTranslator.cs
│
└─ JsonGenerationContext.cs           // add all Preferences + Provider types
```

FR-018, FR-019, FR-020 all plug in as **`Providers/`-level contributions** -- they do *not* modify `Preferences/`. Their schemas are the `[providers.nim]`-style blocks in §4. **FR-018/020 prose that references `preferences.toml` is corrected by this FR to `preferences.json`.** I will file a 10-line edit against those two docs after this design lands.

---

## 13. Milestones

| Phase | Scope | Duration | Exit gate |
|---|---|---|---|
| **P1 -- Preferences spine + OpenAI-direct** | `Preferences/` tree; `AzureOpenAIProvider` (= today's code path, refactored behind factory); `OpenAIProvider`; `--provider`, `--profile`, `AZ_PROVIDER`, `AZ_PROFILE`; `--config migrate`; all FR-003/009/010 exit criteria met. | 4-5 days | `az-ai --provider openai "hello"` works with `OPENAI_API_KEY`; `az-ai "hello"` byte-identical to pre-FR-014. |
| **P2 -- Anthropic adapter** | `AnthropicProvider` + translator + capability probe; rate card feeds FR-015. | 3 days | `az-ai --provider anthropic --model opus "hello"` works; tool calls translate round-trip. |
| **P3 -- Gemini + `openai-compatible` (Ollama)** | `GeminiProvider` + `OpenAICompatibleProvider`; SSRF allowlist; `--list-providers`, `--check`; `allowLan` flag. | 3-4 days | `az-ai --provider ollama --model g3 "hello"` runs fully offline. |
| **P4 -- FR-018 local (llama.cpp)** | Owned by FR-018. Plugs into `OpenAICompatibleProvider` + capability gating. | (FR-018) | See FR-018 exit gates. |
| **P5 -- FR-020 NIM + per-trigger routing** | Owned by FR-020. `TriggerRouter` + routing table + length-gating ship in this phase even though the class lives in FR-014's tree. | (FR-020) | See FR-020 exit gates. |
| **P6 -- Keyring credential source (optional)** | `credentialSource: keyring:…`. Deferred unless a user asks. | 2 days if requested | libsecret/Keychain/WinCred pass-through. |

P1 is the merge gate. Nothing downstream lands until P1 is green and FR-003/009/010 are formally marked *Superseded*.

---

## 14. Testing Strategy

1. **Mock `IChatClient` per provider.** Use `Microsoft.Extensions.AI.Testing` (or a local fake if not AOT-clean) to record request shape per provider kind. Adapter tests assert: wire URL, auth header shape, request-body schema, tool-call translation round-trip.
2. **Round-trip preferences file.** Golden-file test: load → serialize → load again produces byte-identical JSON for a suite of 10 hand-crafted preferences files covering every feature in §4.
3. **Precedence chain table-driven test.** A 40-row matrix parameterizing (cli-flag, env-var, profile, directory, global, legacy, env-file, default) → assert expected value. Catches merge regressions.
4. **Credential refusal tests.** Inject 20 key-like strings into `apiKey` fields; assert all are refused at load time.
5. **SSRF guard tests.** Inject 169.254.169.254 (AWS metadata), file:// URLs, localhost with `allowLan=false` from a non-localhost endpoint field → all refused.
6. **Backward-compat tests.** Seed a tmp `$HOME` with only `~/.azureopenai-cli.json` from v1; assert behaviour unchanged.
7. **Cold-start perf test.** `hyperfine 'az-ai --config show'` must stay ≤ 15 ms end-to-end on linux-x64 (current baseline + budget).
8. **Integration: `--list-providers` with live Ollama.** Optional CI job; spins up `ollama serve` in a sidecar container.

---

## 15. Success Criteria

- [ ] Single file at `~/.config/az-ai/preferences.json` holds all preferences.
- [ ] `az-ai --provider <openai|anthropic|gemini|ollama|azure>` works end-to-end with only env-var credentials.
- [ ] `az-ai --profile code "question"` applies `profiles.code` without mutating global state.
- [ ] `az-ai --trigger ai-grammar` resolves through the routing table (feeds FR-020).
- [ ] `--config show` labels every value with the source layer (FR-009 UX preserved).
- [ ] `--config migrate` moves legacy → XDG without data loss.
- [ ] All FR-003, FR-009, FR-010 exit criteria satisfied; those three FRs are marked **Superseded**.
- [ ] FR-018 and FR-020 can cite *this* FR (not their pre-FR-014 assumptions) for preferences schema.
- [ ] No AOT trim warnings introduced. Cold-start ≤ 15 ms.
- [ ] Zero credentials serialized, logged, or telemetered.
- [ ] Backward compat: v1 users with only `~/.azureopenai-cli.json` experience zero breaking changes.

---

## 16. Open Questions

1. **Profile inheritance.** Should `profiles.code` be able to `extends: "work"`? Defer to v2.1 unless two users ask.
2. **Hot-reload.** No -- preferences are read once at startup. Matches FR-020 §12 Q9.
3. **Schema publication.** Do we publish `schemas/preferences-v1.json` for editor autocomplete? Low cost, high goodwill. Yes, Phase 1.
4. **Per-profile tool allowlists.** Schema supports it (`profiles.code.tools.allow`)? Not in v1 -- `tools.perProvider` is the v1 knob. Revisit when FR-013 lands.
5. **Named system-prompt library.** `systemPrompts.<name>` referenced by profiles and routing entries. Probably lives in FR-015 (pattern library) not here.

---

## 17. What This FR Consolidates -- Recommendation

| FR | Recommendation | Rationale |
|---|---|---|
| **FR-003** (local user preferences) | **Absorb -- mark Superseded** | Its `UserConfig` expansion is a strict subset of FR-014 §4. Exit criteria map 1:1. |
| **FR-009** (`--config set` + directory overrides) | **Absorb -- mark Superseded** | FR-014 §3 directly adopts the walk-up-to-root behaviour; `--config set/get/reset --local` flags carry forward. |
| **FR-010** (model aliases + env normalization) | **Absorb -- mark Superseded** | Aliases are now per-provider in `providers.<x>.aliases`; env-var normalization lives in `envOverrides`. |
| **FR-015** (pattern library + cost estimator) | **Leave separate** | Different scope; FR-014 provides the provider resolution it consumes. |
| **FR-018/019/020** | **Leave separate** | They are legitimate provider-adapter FRs that build *on top of* FR-014. |

After merge, the proposals tree should annotate FR-003, FR-009, FR-010 headers with `**Status: Superseded by FR-014**` but keep them in-tree as historical context. Elaine owns the banner edits; I own the PR.

---

*-- Costanza. You want a piece of me? Fine. Cite the schema version.*
