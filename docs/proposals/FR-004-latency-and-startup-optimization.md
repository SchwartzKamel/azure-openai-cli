# FR-004: Latency & Startup Optimization

> **✅ Phase 1 largely shipped** -- Spinner (v1.1.0) and Native AOT binary
> (v1.8.0, ~5.4 ms cold start at v1.8.0 ship -- current v2.0.6: 10.7 ms p50
> / 12.97 MiB, see [`docs/perf/v2.0.5-baseline.md`](../perf/v2.0.5-baseline.md))
> are done. Daemon mode and Homebrew/tool
> distribution remain deferred. The original proposal below describes the
> pre-AOT baseline for historical context.

---

**Priority:** P0 -- Critical  
**Impact:** Perceived speed is the #1 factor in whether a CLI tool gets habitual use  
**Effort:** Medium (phased)  
**Category:** Performance

---

## The Problem

Every prompt currently follows this path:

```text
User types command
  → Shell parses Makefile (50-100ms)
    → Docker creates container (~800-1500ms)
      → .NET self-extracting binary unpacks (~200-400ms)
        → DotEnv loads .env (~10ms)
          → Azure OpenAI client initializes (~50ms)
            → TLS handshake + HTTP request (~200-500ms)
              → First token arrives
```

**Estimated time to first token: 1.5-3.0 seconds.**

For comparison:

- `sgpt` (Python, no Docker): ~500ms to first token
- `mods` (Go, native binary): ~300ms to first token
- ChatGPT web: ~800ms to first token
- Typing a prompt in a terminal and staring at nothing for 2+ seconds: **an eternity**

The Docker-per-invocation model was a smart v1 choice for security isolation. But it's now the single biggest drag on user experience. Nobody forms a daily habit around a tool that makes them wait 2 seconds before anything happens. That's the difference between "I'll just use ChatGPT" and "let me fire off a quick az-ai."

---

## The Proposal

### Phase 1: Eliminate Perceived Latency (Quick Wins)

These require minimal architectural changes and can ship immediately.

#### 1a. Show a spinner/status indicator during startup

The silence between hitting Enter and seeing the first token is psychologically painful. A simple spinner changes the experience from "is it broken?" to "it's thinking":

```csharp
// Write to stderr so it doesn't pollute stdout (pipe-safe)
Console.Error.Write("⠋ Connecting...");
// ... after first token arrives:
Console.Error.Write("\r\x1b[K"); // Clear the spinner line
```

Use stderr so pipe workflows (`az-ai "query" | pbcopy`) aren't affected.

#### 1b. Pre-warm the HTTPS connection

The Azure OpenAI SDK creates a new `HttpClient` and TLS connection per invocation. Pre-warming by sending a minimal request or using `HttpClient` connection pooling can save 200-400ms:

```csharp
// Trigger DNS resolution and TLS handshake in parallel with config loading
var warmupTask = Task.Run(() => {
    try { new HttpClient().GetAsync(endpoint.ToString()).Wait(2000); }
    catch { /* best effort */ }
});

// ... load config, parse args, etc ...
await warmupTask; // Connection is now warm
```

#### 1c. Cache the extracted binary

The self-contained single-file binary extracts to a temp directory on every launch. Set `DOTNET_BUNDLE_EXTRACT_BASE_DIR` to a stable path and the extraction happens only once:

```dockerfile
ENV DOTNET_BUNDLE_EXTRACT_BASE_DIR=/home/appuser/.cache/dotnet
```

This is already partially done (`/tmp/dotnet_bundle`), but using a user-owned cache directory that can be volume-mounted would make subsequent launches faster.

### Phase 2: Persistent Container Mode (Medium Effort)

Instead of creating a new container per invocation, keep a warm container running and send prompts to it.

#### Architecture: Sidecar Daemon

```text
az-ai "prompt" ──► Unix socket/named pipe ──► Warm container ──► Azure API
                                                    │
                                      (persistent process,
                                       pre-authenticated,
                                       connection pool warm)
```

**Implementation:**

1. `az-ai --daemon start` launches a long-lived container:

   ```bash
   docker run -d --name az-ai-daemon --env-file .env \
       -v /tmp/az-ai.sock:/tmp/az-ai.sock \
       azureopenai-cli:gpt-5-chat --daemon
   ```

2. The daemon listens on a Unix socket and maintains a warm `ChatClient`.

3. The `az-ai` alias detects whether the daemon is running:
   - If yes → send the prompt via the socket (near-zero startup overhead)
   - If no → fall back to `docker run` (current behavior)

4. `az-ai --daemon stop` shuts it down cleanly.

**Expected latency improvement:** Eliminates Docker startup (~1-1.5s) and binary extraction (~200ms). Time to first token drops to ~500-800ms -- competitive with native Go tools.

### Phase 3: Native Install Option (Larger Effort)

Offer a non-Docker installation path for users who want maximum speed and are willing to install .NET or use a pre-built binary:

```bash
# Option A: dotnet tool
dotnet tool install -g azureopenai-cli

# Option B: Pre-built binaries (GitHub Releases)
curl -L https://github.com/SchwartzKamel/azure-openai-cli/releases/latest/download/az-ai-linux-x64 -o /usr/local/bin/az-ai
chmod +x /usr/local/bin/az-ai

# Option C: Homebrew (macOS)
brew install schwartzkamel/tap/az-ai
```

This doesn't replace Docker -- it complements it. Docker remains the "secure, zero-trust" mode. Native install is the "fast, daily-driver" mode. Let the user choose.

**Expected latency:** ~200-400ms to first token. Competitive with the fastest tools in the space.

---

## Latency Budget

| Phase | Component | Current | Target |
|---|---|---|---|
| -- | Shell + Make | ~100ms | ~50ms (direct alias) |
| 2 | Docker container create | ~1000ms | 0ms (daemon) |
| 1c | Binary extraction | ~300ms | 0ms (cached) |
| -- | Config loading | ~20ms | ~20ms |
| 1b | TLS handshake | ~300ms | 0ms (pre-warm) |
| -- | API server processing | ~500ms | ~500ms |
| **Total** | | **~2200ms** | **~570ms** |

---

## Why This Is P0

Speed is not a feature -- it's *the* feature. Every millisecond of startup time is a micro-decision point where the user asks: "should I just open ChatGPT instead?"

The research is clear: tools under 100ms feel instant, under 1s feel responsive, over 1s feel sluggish, over 3s feel broken. We're currently in the "sluggish" zone. Phase 1 gets us to "responsive." Phase 2 gets us to the edge of "instant."

The Docker-first design is a genuine differentiator for security-conscious environments. But it shouldn't come at the cost of being 5x slower than competitors. The daemon model preserves the security boundary while eliminating the startup tax.

**The meta-insight:** Developers share tools that feel magical. Magic requires speed. Nobody screenshots a loading spinner.

---

## Exit Criteria

### Phase 1

- [ ] Spinner/status indicator shows immediately on launch (stderr only)
- [ ] Spinner clears when first token arrives
- [ ] HTTPS pre-warm reduces TLS handshake latency
- [ ] Binary extraction is cached across invocations
- [ ] Measured: Time to first token < 1.5s on warm Docker cache

### Phase 2

- [ ] `--daemon start/stop/status` manages a persistent container
- [ ] Prompt submission via Unix socket works end-to-end
- [ ] Fallback to `docker run` when daemon is not running
- [ ] Measured: Time to first token < 800ms with daemon

### Phase 3

- [ ] Pre-built binaries available on GitHub Releases for linux-x64, osx-arm64, win-x64
- [ ] `dotnet tool install` works
- [ ] Measured: Time to first token < 400ms native
