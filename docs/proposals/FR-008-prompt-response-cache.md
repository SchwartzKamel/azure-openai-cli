# FR-008: Prompt Response Cache for Espanso/AHK Workflows

**Priority:** P1 — High  
**Impact:** Turns repeated prompts from ~500ms (network round-trip) to <5ms (local cache hit)  
**Effort:** Medium (1–2 days)  
**Category:** Performance / Espanso Integration

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

A simple file-based cache in `~/.cache/azureopenai-cli/` (XDG-compliant) with deterministic keys:

```
~/.cache/azureopenai-cli/
├── responses/
│   ├── a3f2b8c1...json    # SHA256(model + prompt + temperature + system_prompt)
│   ├── 7e91d4a0...json
│   └── ...
└── cache.meta              # TTL index, total size tracker
```

#### Cache Key Derivation

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

```json
{
  "model": "gpt-4o",
  "response": "Docker networking uses bridge, host, and overlay drivers...",
  "cached_at": "2026-05-15T10:30:00Z",
  "ttl_hours": 24,
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

```
--no-cache          Skip cache for this invocation (always hit the API)
--cache-ttl <hours> Override default cache TTL (default: 24h)
--cache-clear       Clear the response cache and exit
```

### Cache Policy

| Parameter | Default | Rationale |
|---|---|---|
| TTL | 24 hours | Long enough for daily workflows, short enough that info stays fresh |
| Max entries | 1000 | ~50MB worst case at 50KB average response |
| Max total size | 100MB | Prevents unbounded growth |
| Eviction | LRU when full | Most-recently-used responses survive |
| Temperature threshold | Cache all | Even non-zero temperatures are useful for repeated prompts within a session |

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

| File | Change |
|---|---|
| `azureopenai-cli/PromptCache.cs` | New file: cache implementation |
| `azureopenai-cli/JsonGenerationContext.cs` | Add `CachedResponse` record + `[JsonSerializable]` |
| `azureopenai-cli/Program.cs` (line 574–715) | Insert cache check before API call, cache write after response |
| `azureopenai-cli/Program.cs` (ParseCliFlags) | Add `--no-cache`, `--cache-ttl`, `--cache-clear` flags |

---

## Exit Criteria

- [ ] Identical prompts return cached response in <10ms (measured)
- [ ] `--no-cache` always hits the API
- [ ] `--cache-clear` removes all cached entries
- [ ] Cache entries expire after TTL (default 24h)
- [ ] Agent mode and Ralph mode bypass the cache
- [ ] Cache is stored in `~/.cache/azureopenai-cli/` (XDG-compliant)
- [ ] `[cached]` indicator appears on stderr for cache hits (non-raw mode)
- [ ] `CachedResponse` uses source-generated JSON (AOT-compatible, per FR-006)
- [ ] Total cache size stays under 100MB (LRU eviction)
