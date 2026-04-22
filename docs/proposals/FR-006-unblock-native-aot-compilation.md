# FR-006: Unblock Native AOT Compilation

> **✅ Shipped in v1.8.0 (Unreleased).** Native AOT is now the recommended
> publish mode. `make publish-aot` produces a ~9 MB self-contained binary with
> **~5.4 ms cold start** on Linux x64 -- ~10× faster than ReadyToRun and
> ~75× faster than the Docker container path. All app-level `IL2026` / `IL3050`
> warnings are fixed via source generators in `JsonGenerationContext.cs`
> (`AppJsonContext`). The historical proposal below is preserved for context.

---

**Priority:** P0 -- Critical  
**Impact:** Cuts binary startup from ~55 ms (ReadyToRun) to ~5 ms (AOT) -- the single highest-ROI change for Espanso/AHK latency  
**Effort:** Small (2-4 hours)  
**Category:** Performance / Build Infrastructure

---

## The Problem

The `.csproj` file (line 5-16) documents that Native AOT is blocked:

> AOT produces the smallest/fastest binary (~8MB, ~50ms startup) but currently
> crashes at runtime because UserConfig.cs and Program.cs use reflection-based
> System.Text.Json serialization (JsonSerializer.Serialize with anonymous types).

But here's the thing: **UserConfig already migrated to source generators.** The `AppJsonContext` in `JsonGenerationContext.cs` covers `UserConfig`, all Squad types, and all CLI response records. The comment in `.csproj` is stale -- there's only **one remaining blocker**, and it's 8 lines of code.

### The Last Blocker: `OutputJsonError` (Program.cs:1068-1078)

```csharp
static void OutputJsonError(string message, int exitCode)
{
    var errorObj = new                                          // ← anonymous type
    {
        error = true,
        message = message,
        exit_code = exitCode
    };
    var options = new JsonSerializerOptions { WriteIndented = true }; // ← allocates on every call
    Console.WriteLine(JsonSerializer.Serialize(errorObj, options));   // ← reflection-based serialize
}
```

This method:
1. Uses an **anonymous type** (`new { error = true, ... }`) that cannot be analyzed by the JSON source generator at compile time
2. Creates a **new `JsonSerializerOptions` instance on every call** -- a known performance anti-pattern even outside AOT (Microsoft's own docs warn against this)
3. Falls back to **reflection-based serialization** because there's no source-generated context for the anonymous type

The irony: this method only fires on errors. The *happy path* is already AOT-compatible. But the trimmer and AOT compiler can't know that at compile time -- the reflection dependency poisons the whole binary.

### Why This Matters for Espanso/AHK

The Espanso integration doc and `Makefile` (line 134-141) both call out that the primary use case is text injection where startup latency is critical:

> ```makefile
> ## This is the recommended publish mode for AHK/Espanso text injection workflows
> ## where startup latency is critical. R2R pre-compiles IL to native code at publish
> ## time, eliminating most JIT overhead while retaining full .NET runtime compatibility.
> ```

ReadyToRun gives ~100ms startup. Native AOT gives ~8-15ms. For a tool that fires on every keystroke trigger, that 85ms difference compounds into perceived snappiness. It's the difference between "fast enough" and "invisible."

---

## The Solution

### Step 1: Create a typed error record (5 minutes)

Add to `JsonGenerationContext.cs`:

```csharp
/// <summary>JSON error response for --json mode.</summary>
internal record JsonErrorResponse(
    [property: JsonPropertyName("error")] bool Error,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("exit_code")] int ExitCode
);
```

Register it in the source generator context:

```csharp
[JsonSerializable(typeof(JsonErrorResponse))]  // ← add this line
internal partial class AppJsonContext : JsonSerializerContext { }
```

### Step 2: Replace `OutputJsonError` (5 minutes)

```csharp
static void OutputJsonError(string message, int exitCode)
{
    var errorObj = new JsonErrorResponse(true, message, exitCode);
    Console.WriteLine(JsonSerializer.Serialize(errorObj, AppJsonContext.Default.JsonErrorResponse));
}
```

This eliminates:
- The anonymous type (AOT blocker)
- The per-call `JsonSerializerOptions` allocation
- All reflection-based serialization in the binary

### Step 3: Verify AOT builds and runs (30 minutes)

```bash
make publish-aot
./dist/aot/AzureOpenAI_CLI --version          # Should not crash
./dist/aot/AzureOpenAI_CLI --json "test"      # Should produce JSON output
./dist/aot/AzureOpenAI_CLI --json 2>&1        # Should produce JSON error (no prompt)
```

### Step 4: Update `.csproj` comment and `Makefile` warning

Remove the outdated AOT warning from `.csproj` lines 3-17 and the "⚠ WARNING" line from `Makefile` line 155. AOT is no longer experimental.

### Step 5: Update `publish-aot` to be the recommended Espanso target

```makefile
## Publish Native AOT binary (fastest startup, ~15ms, RECOMMENDED for Espanso/AHK)
publish-aot:
	dotnet publish azureopenai-cli/AzureOpenAI_CLI.csproj -c Release -r linux-x64 -p:PublishAot=true -o dist/aot/
	@echo "Published AOT binary to dist/aot/AzureOpenAI_CLI"
	@ls -lh dist/aot/AzureOpenAI_CLI
```

---

## Risk Assessment

**Low risk.** The change is mechanical: replace an anonymous type with a named record. The source generator pattern is already used everywhere else in the codebase. The `AppJsonContext` already handles 8 other types successfully.

**One caveat:** The Azure.AI.OpenAI SDK (v2.9.0-beta.1) itself may have internal reflection usage that surfaces under AOT. If `publish-aot` builds but crashes on the first API call, the fix is to add `[DynamicDependency]` attributes or SDK-specific rd.xml trimmer directives. This would be a follow-up, not a blocker for the `OutputJsonError` fix.

---

## Expected Impact

| Metric | Before (R2R) | After (AOT) | Delta |
|---|---|---|---|
| Binary startup time | ~100ms | ~8-15ms | **-85ms** |
| Binary size | ~65MB | ~8-12MB | **-80%** |
| Memory at startup | ~30MB | ~8MB | **-73%** |
| Time to first token (native) | ~400ms | ~315ms | **-85ms** |

For Espanso/AHK workflows where the binary is invoked on every text expansion trigger, 85ms saved per invocation × dozens of expansions per day = meaningfully snappier experience.

---

## Files Affected

| File | Change |
|---|---|
| `azureopenai-cli/JsonGenerationContext.cs` | Add `JsonErrorResponse` record + `[JsonSerializable]` registration |
| `azureopenai-cli/Program.cs` (line 1068-1078) | Replace anonymous type with `JsonErrorResponse` |
| `azureopenai-cli/AzureOpenAI_CLI.csproj` (line 3-17) | Remove stale AOT-blocker comment |
| `Makefile` (line 146-156) | Update `publish-aot` target description, remove warning |

---

## Exit Criteria

- [ ] `OutputJsonError` uses a named record registered with `AppJsonContext`
- [ ] Zero anonymous types remain in the codebase (`grep -rn "new {" azureopenai-cli/` returns nothing)
- [ ] `make publish-aot` succeeds without warnings
- [ ] AOT binary passes: `--version`, `--help`, `--json "hello"`, `--json` (error case)
- [ ] `publish-aot` Makefile target is no longer marked experimental
- [ ] Binary size < 15MB, startup time < 20ms (measured with `time`)
