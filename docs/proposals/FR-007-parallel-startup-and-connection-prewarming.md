# FR-007: Parallel Startup Pipeline & Connection Pre-warming

**Priority:** P1 — High  
**Impact:** Saves 200–400ms per invocation by overlapping initialization with TLS handshake  
**Effort:** Small (3–5 hours)  
**Category:** Performance

---

## The Problem

Every invocation follows a strictly sequential startup path. Here's what happens between the user pressing Enter and the first API byte leaving the machine, annotated with the code locations:

```
Program.Main entered
  │
  ├─ [1] ParseCliFlags (line 59)                          ~1ms
  ├─ [2] DotEnv.Load (line 333)                           ~5–10ms
  ├─ [3] UserConfig.Load → File.ReadAllText (line 345)    ~2–5ms
  ├─ [4] InitializeFromEnvironment (line 390)             ~<1ms
  ├─ [5] HandleModelCommands check (line 402)             ~<1ms
  ├─ [6] Build prompt from args/stdin (line 410–449)      ~1–5ms
  ├─ [7] ValidateConfiguration (line 468)                 ~<1ms
  ├─ [8] new AzureOpenAIClient (line 475)                 ~5ms
  ├─ [9] GetChatClient (line 478)                         ~<1ms
  ├─ [10] GetEffectiveConfig (line 481)                   ~<1ms
  ├─ [11] Build ChatCompletionOptions (line 534)          ~<1ms
  │
  └─ [12] CompleteChatStreaming (line 613)                 ~200–500ms
           ├─ DNS resolution                               ~50–100ms (first call)
           ├─ TCP connect                                  ~20–50ms
           ├─ TLS handshake                                ~100–200ms
           └─ HTTP/2 negotiation + first byte              ~50–100ms
```

**Steps 1–11 total: ~15–25ms.** These are cheap.
**Step 12 network setup: ~200–500ms.** This dominates.

The critical insight: **steps 1–11 produce no network I/O**, and **step 12 needs nothing from steps 1–6**. The TLS handshake only needs the endpoint URL and API key, which are available as soon as DotEnv loads (step 2). But the current code waits until *everything* is parsed and validated before touching the network.

### Why the Azure SDK Doesn't Help

The `AzureOpenAIClient` (line 475–478) creates an internal `HttpClient` with connection pooling — but this pool starts cold. The first `CompleteChatStreaming` call pays the full DNS + TCP + TLS cost. There's no pre-connect or warmup API.

```csharp
// Line 475–478: Client created but no network activity until line 613
AzureOpenAIClient azureClient = new(
    endpoint,
    new AzureKeyCredential(apiKey));
ChatClient chatClient = azureClient.GetChatClient(deploymentName);
```

The SDK uses `SocketsHttpHandler` internally, which maintains connection pools keyed by (host, port). If we can trigger a lightweight HTTPS connection to the endpoint *before* the chat request, the pool will have a warm connection ready.

---

## The Solution

### Approach: Fire-and-Forget TLS Pre-warm

Immediately after loading environment variables (step 2, line 342), start a background task that opens a TLS connection to the Azure endpoint. By the time the code reaches `CompleteChatStreaming` (step 12), the connection pool will have a warm socket.

```csharp
// After DotEnv.Load (line 342), before UserConfig.Load:
string? rawEndpoint = Environment.GetEnvironmentVariable("AZUREOPENAIENDPOINT");
string? rawApiKey = Environment.GetEnvironmentVariable("AZUREOPENAIAPI");

// Fire-and-forget: pre-warm TLS connection to Azure endpoint
Task? prewarmTask = null;
if (!string.IsNullOrEmpty(rawEndpoint) && Uri.TryCreate(rawEndpoint, UriKind.Absolute, out var prewarmUri))
{
    prewarmTask = Task.Run(async () =>
    {
        try
        {
            using var warmClient = new HttpClient();
            warmClient.Timeout = TimeSpan.FromSeconds(5);
            // HEAD request to trigger DNS + TLS without transferring data
            var request = new HttpRequestMessage(HttpMethod.Head, prewarmUri);
            request.Headers.Add("api-key", rawApiKey ?? "");
            await warmClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        }
        catch { /* best-effort: if pre-warm fails, the real request will cold-start normally */ }
    });
}

// ... rest of startup (UserConfig.Load, arg parsing, prompt building) ...

// Before the chat call, optionally await to ensure the connection is ready:
if (prewarmTask != null)
    await prewarmTask.ConfigureAwait(false);
```

**Important:** The pre-warm `HttpClient` is disposable and separate from the Azure SDK's internal client. To get the SDK to *reuse* the pre-warmed connection, we need to share the same `SocketsHttpHandler`:

### Better Approach: Shared Handler with Pre-warm

```csharp
// Create a shared handler at startup
var sharedHandler = new SocketsHttpHandler
{
    PooledConnectionLifetime = TimeSpan.FromMinutes(5),
    EnableMultipleHttp2Connections = true,
};

// Pre-warm: trigger DNS + TLS in background
Task? prewarmTask = null;
if (!string.IsNullOrEmpty(rawEndpoint) && Uri.TryCreate(rawEndpoint, UriKind.Absolute, out var prewarmUri))
{
    prewarmTask = Task.Run(async () =>
    {
        try
        {
            using var warmClient = new HttpClient(sharedHandler, disposeHandler: false);
            warmClient.Timeout = TimeSpan.FromSeconds(5);
            var request = new HttpRequestMessage(HttpMethod.Head, prewarmUri);
            await warmClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        }
        catch { }
    });
}

// ... rest of startup ...

// Pass the shared handler to the Azure SDK client
var clientOptions = new AzureOpenAIClientOptions();
clientOptions.Transport = new Azure.Core.Pipeline.HttpClientTransport(new HttpClient(sharedHandler, disposeHandler: false));
AzureOpenAIClient azureClient = new(endpoint, new AzureKeyCredential(apiKey), clientOptions);
```

This ensures the warm connection in the handler's pool is reused by the Azure SDK.

### Parallel Config Loading (Bonus)

While we're restructuring startup, we can also overlap `DotEnv.Load` with `UserConfig.Load`:

```csharp
// These have no dependencies on each other — run in parallel
var envTask = Task.Run(() =>
{
    try { DotEnv.Load(new DotEnvOptions(envFilePaths: new[] { ".env" }, overwriteExistingVars: true, trimValues: true)); }
    catch { }
});
var configTask = Task.Run(() => UserConfig.Load());

await envTask;
var config = await configTask;
```

This saves the slower of the two (~5–10ms for DotEnv, ~2–5ms for UserConfig file read).

---

## Architecture Diagram

```
BEFORE (sequential):
  ┌─────────┐  ┌───────────┐  ┌──────────┐  ┌─────────────┐  ┌──────────────────┐
  │ DotEnv  │→│ UserConfig │→│ Parse    │→│ Create      │→│ CompleteChatStream │
  │ Load    │  │ Load       │  │ Args     │  │ AzureClient │  │ (DNS+TLS+HTTP)   │
  │ ~8ms    │  │ ~4ms       │  │ ~2ms     │  │ ~5ms        │  │ ~350ms           │
  └─────────┘  └───────────┘  └──────────┘  └─────────────┘  └──────────────────┘
  Total: ~370ms wall clock

AFTER (parallel pre-warm):
  ┌─────────┐  ┌───────────┐  ┌──────────┐  ┌─────────────┐  ┌──────────────────┐
  │ DotEnv  │→│ UserConfig │→│ Parse    │→│ Create      │→│ CompleteChatStream │
  │ Load    │  │ Load       │  │ Args     │  │ AzureClient │  │ (connection warm!)│
  │ ~8ms    │  │ ~4ms       │  │ ~2ms     │  │ ~5ms        │  │ ~50ms            │
  └─────────┘  └───────────┘  └──────────┘  └─────────────┘  └──────────────────┘
  ┌──────────────────────────────────────┐
  │ TLS Pre-warm (background)            │ ← runs in parallel with steps above
  │ DNS + TCP + TLS handshake ~300ms     │
  └──────────────────────────────────────┘
  Total: ~300ms wall clock (bounded by pre-warm, not sequential sum)
```

**Net savings: ~200–300ms** — the TLS cost is hidden behind startup work that was going to happen anyway.

---

## Risk Assessment

**Low risk.** The pre-warm is fire-and-forget with a `catch {}` — if it fails, the real request cold-starts exactly as it does today. Zero change to the happy path.

**One subtlety:** The `HEAD` request to the Azure endpoint may return 404 or 401 (no valid chat path). That's fine — we only care about the TLS handshake completing. The connection is pooled by (host, port), not by path.

**SDK compatibility:** The `Azure.Core` library (v1.51.1, referenced in `.csproj` line 35) supports custom `HttpClientTransport`. This is the documented extensibility point for sharing handlers.

---

## Expected Impact

| Metric | Before | After | Delta |
|---|---|---|---|
| Time: DotEnv + Config (sequential) | ~12ms | ~8ms (parallel) | **-4ms** |
| Time: TLS handshake (first call) | ~300ms (blocks) | ~0ms (pre-warmed) | **-300ms** |
| Time to first API byte | ~370ms | ~70ms | **-300ms** |
| Time to first token (end-to-end, native) | ~500ms | ~200ms | **-300ms** |

Combined with FR-006 (AOT, -85ms startup), the total pipeline drops from ~600ms to ~215ms — approaching the "feels instant" threshold.

---

## Files Affected

| File | Change |
|---|---|
| `azureopenai-cli/Program.cs` (lines 330–478) | Restructure startup: parallel DotEnv/Config load, TLS pre-warm task, shared `SocketsHttpHandler` |
| `azureopenai-cli/AzureOpenAI_CLI.csproj` | No changes needed |

---

## Exit Criteria

- [ ] TLS pre-warm task starts within 10ms of `Main` entry
- [ ] Pre-warm failure does not affect normal operation (graceful degradation)
- [ ] Azure SDK uses the shared `SocketsHttpHandler` (verified via HTTP/2 connection reuse in Wireshark or `DOTNET_SYSTEM_NET_HTTP_SOCKETSHTTPHANDLER_LOGGING=true`)
- [ ] Measured: first-call latency reduction ≥ 150ms vs baseline (on warm DNS cache)
- [ ] No regression in `make test` or `make integration-test`
- [ ] `DotEnv.Load` and `UserConfig.Load` run in parallel (verified via stopwatch logs in debug mode)
