# FR-008: Prompt Response Cache for Espanso/AHK Workflows

**Status:** Shipped (v2.0.0, opt-in variant)
**Priority:** P1 — High
**Impact:** Turns repeated prompts from ~500ms (network round-trip) to <5ms (local cache hit)
**Effort:** Medium (1–2 days)
**Category:** Performance / Espanso Integration

---

## Shipped in v2.0.0

- **Date:** 2026-04-20 window
- **Commit:** [`4f1acdd`](../../) — `feat(v2): FR-008 prompt cache + UX fixes (json-stderr, unknown-flag, comment)`
- **Implementation:** [`azureopenai-cli-v2/Cache/PromptCache.cs`](../../azureopenai-cli-v2/Cache/PromptCache.cs)
- **Tests:** `tests/AzureOpenAI_CLI.V2.Tests/PromptCacheTests.cs` (19 cases), `tests/AzureOpenAI_CLI.V2.Tests/V2UxAndCacheWaveTests.cs` (cache-flag cases)
- **Release notes:** [`CHANGELOG.md`](../../CHANGELOG.md) v2.0.0 section, [`docs/release-notes-v2.0.0.md`](../release-notes-v2.0.0.md)

Three deviations from the original proposal were accepted during implementation. They are called out inline below under **"As shipped:" / "Originally proposed:"** blocks. The original prose is preserved so the archaeological record survives.

### Deviations at a glance

| Area | Originally proposed | As shipped | Why |
|---|---|---|---|
| Flag semantics | `--no-cache` (default on, opt-out) | `--cache` (default off, opt-in) + `AZ_CACHE=1` | Silent-by-default preserves existing behavior for users who neither know nor want caching. |
| TTL default | 24 hours | 7 days (168h) | Espanso triggers repeat on weekly cadences, not daily. 24h was too aggressive. |
| Eviction | Strict access-time LRU | mtime-based, 20% oldest when dir > 50 MB | Touching files on read violates 0600 roundtrip and adds hit-path latency. mtime approximates recency without the write amplification. |

---

## The Problem

The primary use case for this tool is Espanso/AHK text expansion — a user types a trigger (like `:ai:explain docker`) and the CLI fires, sends the prompt to Azure, and injects the response as typed text. The `--raw` flag (Program.cs line 252–255) and the Espanso integration doc (`docs/espanso-ahk-integration.md`) confirm this is a first-class workflow.

Here's the problem: **text expansion triggers are often repetitive.** A developer explaining Docker networking to three different colleagues fires the same prompt three times. A support engineer using `:ai:summarize-ticket` on similar tickets gets semantically identical responses. Every invocation pays the full network round-trip cost:

```
Prompt → DNS → TLS → Azure API server processing (~300–500ms) → Stream response → Inject text
```

There is **zero caching** anywhere in the pipeline today. Every invocation is a cold network call, even for byte-identical prompts with identical parameters.

### What Makes This Especially Painful

1. **Espanso triggers are synchronous** — the user's typing is blocked until the expansion completes. 500ms feels like a hiccup. 5ms feels like autocomplete.

2. **Azure API costs money** — repeated identical prompts burn tokens for no new information. At GPT-4o pricing, a cached response saves ~$0.01–0.05 per hit. Over hundreds of daily expansions, this adds up.

3. **Rate limits** — Azure OpenAI enforces per-minute token limits. Caching identical prompts keeps headroom for novel queries.

4. **Offline resilience** — with a cache, previously-seen prompts work even when the network is down. For a CLI tool, "works offline for common queries" is a power feature.

---

## The Solution

### Cache Design: File-Based with SHA256 Keys

A simple file-based cache with deterministic keys:

> **As shipped:**
> - Linux/macOS: `$XDG_CACHE_HOME/azureopenai-cli/v1/<hash>.json`, falling back to `~/.cache/azureopenai-cli/v1/<hash>.json`.
> - Windows: `%LOCALAPPDATA%\azureopenai-cli\v1\<hash>.json`.
> - Unix permissions: **files `0600`, directory `0700`** (enforced via `File.SetUnixFileMode`). Windows relies on `%LOCALAPPDATA%` ACLs.
> - Schema version `v1` is baked into the path so a future layout change can coexist without wiping user data.
> - Tests override the directory via `AZ_CACHE_DIR` (undocumented internal hook — not a user-facing knob).

```
~/.cache/azureopenai-cli/
├── responses/
│   ├── a3f2b8c1...json    # SHA256(model + prompt + temperature + system_prompt)
│   ├── 7e91d4a0...json
│   └── ...
└── cache.meta              # TTL index, total size tracker
```

#### Cache Key Derivation

> **As shipped:** the key is SHA-256 (hex, lowercase) of a **canonical sorted-JSON** payload over exactly five fields, in this property order: `max_tokens`, `model`, `system_prompt`, `temperature` (formatted `F4`, invariant culture), `user_prompt`. Endpoint URL and API key are **intentionally excluded** — rotating credentials must not invalidate the cache, and credentials must never touch disk. The `ToolHardeningTests.ComputeKey_Ignores_Endpoint_And_ApiKey_By_Design` test pins this contract. See [`PromptCache.ComputeKey`](../../azureopenai-cli-v2/Cache/PromptCache.cs).
>
> **Originally proposed:** a newline-delimited string over `model`, `systemPrompt`, `userPrompt`, `temperature` — `max_tokens` was explicitly excluded. The shipped version includes `max_tokens` because truncation *is* user-visible when the cap is hit, and canonical JSON is more auditable than ad-hoc string concatenation.

The cache key must capture everything that affects the response:

```csharp
static string ComputeCacheKey(string model, string systemPrompt, string userPrompt, float temperature)
{
    var input = $"{model}\n{systemPrompt}\n{userPrompt}\n{temperature:F2}";
    var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
    return Convert.ToHexStringLower(hash);
}
```

**Why these fields:**
- `model` — different models give different responses
- `systemPrompt` — changes behavior fundamentally
- `userPrompt` — the actual query
- `temperature` — at `0.0`, responses are deterministic; at `0.9`, caching is less useful (but still valid for the same session)

**Why NOT `maxTokens`:** Changing max tokens may truncate but doesn't change the content of what's generated. A cached response from a higher max-tokens call is strictly a superset.

#### Cache Entry Format

> **As shipped:** the on-disk JSON contains exactly `response`, `cached_at`, `ttl_hours`, `model`, `input_tokens`, `output_tokens`. **No endpoint. No API key. No username.** The cache is credential-independent by construction.

```json
{
  "model": "gpt-4o",
  "response": "Docker networking uses bridge, host, and overlay drivers...",
  "cached_at": "2026-05-15T10:30:00Z",
  "ttl_hours": 168,
  "input_tokens": 45,
  "output_tokens": 312
}
```

### Integration Point: Program.cs Standard Mode (line 574–715)

The cache check slots in between prompt building and the API call:

```csharp
// === STANDARD MODE (single-shot streaming) ===

// Check cache before hitting the network
if (!opts.NoCache)  // new --no-cache flag
{
    var cacheKey = PromptCache.ComputeKey(deploymentName, effectiveSystemPrompt, userPrompt, temperature);
    var cached = PromptCache.TryGet(cacheKey);
    if (cached != null)
    {
        if (jsonMode)
        {
            var jsonOutput = new ChatJsonResponse(deploymentName, cached.Response, 0,
                cached.InputTokens, cached.OutputTokens);
            Console.WriteLine(JsonSerializer.Serialize(jsonOutput, AppJsonContext.Default.ChatJsonResponse));
        }
        else
        {
            Console.Write(cached.Response);
            if (!opts.Raw)
            {
                if (!Console.IsErrorRedirected)
                    Console.Error.WriteLine("  [cached]");
                Console.WriteLine();
            }
        }
        return 0;
    }
}

// ... existing streaming code (line 575+) ...

// After successful response, write to cache
if (!opts.NoCache && responseBuilder != null)
{
    var cacheKey = PromptCache.ComputeKey(deploymentName, effectiveSystemPrompt, userPrompt, temperature);
    PromptCache.Put(cacheKey, responseBuilder.ToString(), promptTokens, completionTokens);
}
```

### CLI Flags

> **As shipped — opt-in, not opt-out:**
>
> | Flag / Env | Effect |
> |---|---|
> | `--cache` | Enables the cache for this invocation (read + write). Default is **off**. |
> | `AZ_CACHE=1` | Enables the cache when the flag is absent. Strict match — only the literal string `"1"` counts. Any other value (including `true`, `yes`, `on`) is ignored. |
> | `--cache-ttl <hours>` | Overrides the default TTL for this invocation. |
> | `AZ_CACHE_TTL_HOURS` | Env-fallback TTL override. |
>
> **Originally proposed:** `--no-cache` (cache default-on, opt-out) and `--cache-clear`. Both were reconsidered:
> - Default-on was reverted to **default-off** so users who upgrade don't silently acquire a new on-disk artifact they didn't ask for. Opt-in is the safer default; Espanso power-users add `--cache` once to their trigger config.
> - `--cache-clear` was **deferred** to a follow-up (see bottom of this doc). `rm -rf ~/.cache/azureopenai-cli/v1` is the current workaround and is documented in the release notes.

#### Never-cache gates (shipped)

The cache is bypassed — regardless of `--cache` or `AZ_CACHE` — for any of the following:

- `--agent` (tool execution is non-deterministic)
- `--ralph` (each iteration depends on the prior)
- `--persona` (persona memory writes make responses stateful)
- `--json` (structured output callers want fresh responses)
- `--schema` (structured output callers want fresh responses)
- `--estimate` (token accounting, not a real call)
- Empty responses (nothing to cache)
- Errors (never cache failures)

#### Stderr indicators (shipped)

- Non-`--raw` mode: `[cache] hit` or `[cache] miss` written to stderr.
- `--raw` mode: silent. Espanso injection must not carry cache chatter into the typed output.

### Cache Policy

> **As shipped:**
>
> | Parameter | Value | Notes |
> |---|---|---|
> | TTL default | **168 hours (7 days)** | Originally 24h. Overridable via `--cache-ttl` or `AZ_CACHE_TTL_HOURS`. |
> | Max total size | **50 MB** | Originally 100 MB. Soft cap; checked after every `Put`. |
> | Eviction | **mtime-based, oldest 20%** when cap exceeded | Originally strict access-time LRU. See deviation #3 below. |
> | Max entries | *not enforced* | Size cap is the only bound. Originally 1000. |
> | Temperature threshold | Cache all | Unchanged from proposal. |
>
> **Originally proposed (preserved for the record):**

| Parameter | Default | Rationale |
|---|---|---|
| TTL | 24 hours | Long enough for daily workflows, short enough that info stays fresh |
| Max entries | 1000 | ~50MB worst case at 50KB average response |
| Max total size | 100MB | Prevents unbounded growth |
| Eviction | LRU when full | Most-recently-used responses survive |
| Temperature threshold | Cache all | Even non-zero temperatures are useful for repeated prompts within a session |

#### Deviation #3 — why mtime instead of access-time LRU

Strict access-time LRU requires **writing to each file on read** (either the file's atime, or a sidecar index). That has two costs:

1. **Perms roundtrip** — the 0600 contract means every hit-path read would need to re-assert perms after a write. More code, more failure modes.
2. **Hit-path latency** — the cache's entire point is sub-5ms hits. Adding a write per read defeats it, especially on slow filesystems or encrypted home dirs.

mtime-based eviction uses `LastWriteTimeUtc` (stamped only on `Put`) as a recency proxy. It evicts **entries no one has re-queried recently enough to rewrite**, which is functionally close to LRU for the Espanso workload where a cache miss is what produces a fresh write.

An opt-in true-LRU mode is captured in follow-ups below.

### Temperatures Above 0

For `temperature > 0`, the API returns non-deterministic responses. The cache still helps because:

1. **Within a session**, a user often wants the *same* response they got 5 minutes ago (e.g., re-expanding a trigger that didn't paste correctly)
2. **For Espanso**, consistency matters more than variety — if the expansion gave a good answer, repeating the trigger should give the same answer
3. The `--no-cache` flag provides an escape hatch for users who want a fresh response

### The `PromptCache` Class

```csharp
internal static class PromptCache
{
    private static readonly string CacheDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".cache", "azureopenai-cli", "responses");

    public static string ComputeKey(string model, string systemPrompt, string userPrompt, float temp)
    {
        var input = $"{model}\n{systemPrompt}\n{userPrompt}\n{temp:F2}";
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(input)));
    }

    public static CachedResponse? TryGet(string key)
    {
        var path = Path.Combine(CacheDir, $"{key}.json");
        if (!File.Exists(path)) return null;

        try
        {
            var json = File.ReadAllText(path);
            var entry = JsonSerializer.Deserialize(json, AppJsonContext.Default.CachedResponse);
            if (entry == null) return null;

            // Check TTL
            if (entry.CachedAt.AddHours(entry.TtlHours) < DateTime.UtcNow)
            {
                File.Delete(path);  // Expired
                return null;
            }
            return entry;
        }
        catch { return null; }  // Corrupt cache entry — treat as miss
    }

    public static void Put(string key, string response, int? inputTokens, int? outputTokens, int ttlHours = 24)
    {
        try
        {
            Directory.CreateDirectory(CacheDir);
            var entry = new CachedResponse(response, DateTime.UtcNow, ttlHours, inputTokens, outputTokens);
            File.WriteAllText(
                Path.Combine(CacheDir, $"{key}.json"),
                JsonSerializer.Serialize(entry, AppJsonContext.Default.CachedResponse));
        }
        catch { /* best-effort caching */ }
    }
}
```

Register in `JsonGenerationContext.cs`:
```csharp
[JsonSerializable(typeof(CachedResponse))]
```

---

## What About Agent Mode?

> **As shipped:** the never-cache list is broader than just agent/ralph. See the *Never-cache gates* table above — `--persona`, `--json`, `--schema`, and `--estimate` also bypass the cache, as do empty responses and errors.

Agent mode (`--agent`) should **bypass the cache entirely**. Agent responses depend on real-time tool execution (file contents, shell output, clipboard, web fetches) that change between invocations. Caching an agent response would return stale tool results.

Ralph mode (`--ralph`) similarly bypasses the cache — each iteration depends on the previous iteration's output.

The cache only applies to **standard single-shot mode** (the Espanso/AHK primary path).

---

## Expected Impact

| Metric | Cache Miss | Cache Hit | Delta |
|---|---|---|---|
| Time to response | ~500ms | ~3ms | **-497ms (99.4%)** |
| API tokens consumed | ~500 | 0 | **-100%** |
| API cost per call | ~$0.01 | $0 | **-100%** |
| Works offline | No | Yes | **New capability** |

For an Espanso user who fires 50 expansions/day with ~30% prompt repetition:
- **15 cache hits/day × 497ms saved = 7.5 seconds/day** of eliminated latency
- **15 hits × ~500 tokens = 7,500 tokens/day** of API cost savings
- **Qualitative:** repeated triggers feel instant, building trust in the tool

---

## Files Affected

> **As shipped:**
>
> | File | Change |
> |---|---|
> | [`azureopenai-cli-v2/Cache/PromptCache.cs`](../../azureopenai-cli-v2/Cache/PromptCache.cs) | New: cache implementation + `CachedResponse` record. |
> | `azureopenai-cli-v2/AppJsonContext.cs` | `[JsonSerializable(typeof(CachedResponse))]` for AOT. |
> | `azureopenai-cli-v2/Program.cs` | `--cache`, `--cache-ttl`, `AZ_CACHE`, `AZ_CACHE_TTL_HOURS` wiring; never-cache gates; stderr `[cache] hit`/`[cache] miss`. |
> | `tests/AzureOpenAI_CLI.V2.Tests/PromptCacheTests.cs` | 19 unit cases: key determinism, credential-independence, TTL, eviction, perms, corrupt-entry miss. |
> | `tests/AzureOpenAI_CLI.V2.Tests/V2UxAndCacheWaveTests.cs` | Flag-wiring and never-cache gate cases. |

Legacy `azureopenai-cli/` paths below are preserved from the original proposal for context; the shipped implementation lives under `azureopenai-cli-v2/`.

| File | Change |
|---|---|
| `azureopenai-cli/PromptCache.cs` | New file: cache implementation |
| `azureopenai-cli/JsonGenerationContext.cs` | Add `CachedResponse` record + `[JsonSerializable]` |
| `azureopenai-cli/Program.cs` (line 574–715) | Insert cache check before API call, cache write after response |
| `azureopenai-cli/Program.cs` (ParseCliFlags) | Add `--no-cache`, `--cache-ttl`, `--cache-clear` flags |

---

## Exit Criteria

> **As shipped — all met:**
>
> - [x] Identical prompts return cached response in <10ms (measured in `PromptCacheTests`).
> - [x] Cache is **opt-in** via `--cache` / `AZ_CACHE=1`; default omits the cache entirely.
> - [x] Cache entries expire after TTL (**default 168h / 7 days**).
> - [x] Agent, Ralph, persona, json, schema, and estimate modes bypass the cache.
> - [x] Cache stored in `$XDG_CACHE_HOME/azureopenai-cli/v1/` (Unix) or `%LOCALAPPDATA%\azureopenai-cli\v1\` (Windows), files `0600`, dir `0700`.
> - [x] `[cache] hit` / `[cache] miss` on stderr in non-raw mode; silent under `--raw`.
> - [x] `CachedResponse` uses source-generated JSON via `AppJsonContext` (AOT-compatible, per FR-006).
> - [x] Total cache size stays under **50 MB** via mtime-based eviction of oldest 20%.
> - [x] Key derivation excludes endpoint and API key (`ToolHardeningTests.ComputeKey_Ignores_Endpoint_And_ApiKey_By_Design`).
> - [ ] `--cache-clear` — **deferred** (see follow-ups).

**Originally proposed checklist (preserved):**

- [ ] Identical prompts return cached response in <10ms (measured)
- [ ] `--no-cache` always hits the API
- [ ] `--cache-clear` removes all cached entries
- [ ] Cache entries expire after TTL (default 24h)
- [ ] Agent mode and Ralph mode bypass the cache
- [ ] Cache is stored in `~/.cache/azureopenai-cli/` (XDG-compliant)
- [ ] `[cached]` indicator appears on stderr for cache hits (non-raw mode)
- [ ] `CachedResponse` uses source-generated JSON (AOT-compatible, per FR-006)
- [ ] Total cache size stays under 100MB (LRU eviction)

---

## Follow-ups captured for future FRs

The following were scoped out of the v2.0.0 ship but are worth their own proposals:

1. **`--cache-prewarm <file>`** — a subcommand that reads a file of prompts (one per line, or a structured YAML) and populates the cache ahead of time. Targets Espanso power-users who want first-call latency on known trigger sets (onboarding, demos, conference talks).

2. **`--cache-clear`** — a subcommand to wipe the cache directory without shelling out to `rm`. Simple, but the v2.0.0 ship deliberately deferred any destructive cache-management surface until the read/write path had settled. Current workaround: delete the directory manually.

3. **Access-time LRU mode** — an opt-in `--cache-lru=atime` flag (or `AZ_CACHE_LRU=atime`) that re-stamps `LastWriteTimeUtc` on cache hits, converting mtime-based eviction into true LRU. Off by default because it re-introduces the hit-path write cost documented in deviation #3. Worth offering for users with enormous cache dirs and long-tail access patterns.

4. **Telemetry counters** — when FR-XXX telemetry lands (`--telemetry`), emit `cache.hit` / `cache.miss` counters and a `cache.latency.ms` histogram. Gives users (and us) real data on hit rates instead of anecdote. Must remain strictly opt-in under `--telemetry`; the cache itself must never phone home.
