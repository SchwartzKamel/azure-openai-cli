# AOT Trim Investigation -- v2 cutover size-gate

**Owner:** Kenny Bania
**Scope:** `azureopenai-cli-v2` only. v1 binary is the immutable baseline.
**Baseline commit:** `488aebd` (main)
**Reference hardware:** local dev (linux-x64, .NET SDK 10.0.x, `dotnet publish -c Release -r linux-x64 -p:PublishAot=true`)
**Purpose:** Close (or at minimum narrow) the AOT size gap v1→v2 so the cutover size-gate (≤1.50× v1) clears without Costanza having to waive it.

## Outcome, up top

| Metric | v1 (baseline) | v2 (before) | v2 (after) |
|---|---:|---:|---:|
| AOT binary bytes | 9,294,968 | 15,105,904 | **13,533,472** |
| AOT binary MB | 8.864 MB | 14.406 MB | **12.906 MB** |
| Ratio vs v1 | 1.000× | 1.625× | **1.456×** |
| Gate (≤1.50×) | -- | ❌ fail | ✅ **pass** |
| 301 v2 tests | -- | pass | pass |
| 1025 v1 tests | -- | pass | pass |
| `dotnet format --verify-no-changes` | -- | pass | pass |

**Net savings: 1,572,432 bytes (≈1.50 MB, −10.4%)** from two MSBuild properties in `AzureOpenAI_CLI_V2.csproj`. No code changes. No package changes. No Dockerfile changes.

The cutover size-gate **clears at 1.456×**. No waiver required.

## Methodology

For each lever under evaluation:

1. Wipe `azureopenai-cli-v2/{bin,obj}/Release` to eliminate cache effects.
2. `dotnet publish azureopenai-cli-v2/AzureOpenAI_CLI_V2.csproj -c Release -r linux-x64 -p:PublishAot=true -p:<lever>=<value> -o <out>/`.
3. `stat -c %s <out>/az-ai-v2` → bytes.
4. Compare against baseline (bytes, MB, ratio vs v1).
5. For winners: run full `dotnet test tests/AzureOpenAI_CLI.V2.Tests/...` (301 cases) + `make format-check` + v1 suite (1025 cases).

Single-sample measurement per lever. Variance on `stat -c %s` is zero for a deterministic AOT output with fixed inputs -- repeat builds are bit-identical. Sample size for CPU/wall-clock perf numbers would need more runs, but **size is deterministic**, so n=1 is honest here. No outliers to discard.

Levers that save <100 KB are declared "not worth the risk" and skipped in the final csproj, per the pre-registered rule.

## Levers -- measured

Baseline for all deltas: 15,105,904 bytes (14.41 MB, 1.625× v1).

| # | Lever | MSBuild property | Bytes after | Δ bytes | Δ MB | Ratio vs v1 | Status | Ship? |
|---|---|---|---:|---:|---:|---:|---|---|
| 1 | Stack-trace metadata drop | `IlcGenerateStackTraceData=false` | 15,105,904 | 0 | 0.00 | 1.625× | no-op in net10.0 | **reject** |
| 2 | Debugger support off | `DebuggerSupport=false` | 15,105,920 | +16 | 0.00 | 1.625× | already-on upstream (`--feature:System.Diagnostics.Debugger.IsSupported=false` in ILC rsp) | **reject** |
| 3 | Invariant globalization | `InvariantGlobalization=true` | -- | -- | -- | -- | already set in csproj | -- |
| 4 | HTTP activity propagation off | `HttpActivityPropagationSupport=false` | 15,064,480 | −41,424 | −0.04 | 1.621× | <100 KB, and disables OTLP trace propagation we ship | **reject** |
| 5 | OTel exporter runtime opt-in | (conditional `PackageReference`) | -- | -- | -- | -- | not viable; trimming is compile-time, runtime env var check cannot drive it | **reject** |
| 6 | Root-descriptor XML for `AppJsonContext` | -- | -- | -- | -- | -- | already handled via source-generated JSON context | -- |
| 7 | **Optimize for size** | `OptimizationPreference=Size` | 14,610,816 | −495,088 | −0.47 | 1.572× | ILC favors size over speed | ✅ **ship** |
| 8 | **Strip rich stack traces** | `StackTraceSupport=false` | 13,999,888 | −1,106,016 | −1.05 | 1.506× | v2 only surfaces `ex.Message`; no `ex.StackTrace` print sites | ✅ **ship** |
| **7+8** | **Combined (applied)** | both of the above | **13,533,472** | **−1,572,432** | **−1.50** | **1.456×** | 301 v2 tests green | ✅ **shipped** |

### Lever 1 -- `IlcGenerateStackTraceData=false`

Expected: −2 to −3 MB (common wisdom from older .NET AOT docs). Measured: **0 bytes**. Explanation: on net10.0 the stack-trace metadata subsystem is gated by the newer `StackTraceSupport` feature switch. The `Ilc…` property is retained for backcompat but is a no-op when the higher-level switch is present. Lever 8 is the real knob.

### Lever 2 -- `DebuggerSupport=false`

Expected: −0.5 to −1 MB. Measured: **+16 bytes** (noise). The ILC response file already emits `--feature:System.Diagnostics.Debugger.IsSupported=false` as a net10.0 AOT default when no debugger attach is rooted. v2 code is clean: one `Trace.WriteLine` site in `Ralph/CheckpointManager.cs` for checkpoint I/O failures, zero `Debug.Assert`, zero `Debugger.IsAttached`. No win because there's nothing left to strip.

### Lever 4 -- `HttpActivityPropagationSupport=false`

Measured: **−41 KB**. Below the 100 KB floor and, more importantly, **functionally wrong**: v2 ships OTLP tracing (`--otel` / `--telemetry`) and relies on `DiagnosticsHandler` injecting `traceparent`/`tracestate` headers on outbound HTTP calls. Disabling this switch would silently break distributed-trace propagation the one time a user actually has telemetry turned on. **Reject on correctness, not size.**

### Lever 5 -- OTel exporter runtime opt-in via `AZ_TELEMETRY=1`

Not viable as specified. Linker trimming is a compile-time decision. A runtime env-var check cannot drive whether `OpenTelemetry.Exporter.OpenTelemetryProtocol.dll` is AOT-compiled into the binary. The only ways to drop it:

* **Conditional `<PackageReference>`** behind an MSBuild property (e.g. `-p:IncludeOtlp=false`). This ships two different binaries -- one with telemetry, one without -- and complicates the single-binary cutover narrative. Rejected for v2.0.0.
* **Post-cutover:** consider a `slim` RID-specific build flavor if size pressure returns. Not required to clear the gate.

Note that the OTLP exporter's managed DLL is already modest on disk (156 KB managed; likely ~300-500 KB after AOT). Even if we could strip it, upside is capped.

### Levers 7 + 8 -- the actual wins

**`OptimizationPreference=Size` (−495 KB):** Instructs ILC to prefer smaller machine code over faster. Cold-start is already <10 ms; we are not CPU-bound on the hot path, we are I/O-bound against the Azure OpenAI endpoint. Trading a few % of code-gen speed for half a MB is a clean trade. No behavior change.

**`StackTraceSupport=false` (−1.05 MB):** Drops the `System.Private.StackTraceMetadata` initialization assembly and rich per-method frame metadata. Exceptions still unwind, `ex.Message` is unchanged, `catch` semantics unchanged. What changes: `Exception.StackTrace` returns a minimal / empty string for in-process stack dumps.

**Safety audit** -- every v2 catch site:

```text
grep -n 'ex\.StackTrace\|Console\.Error.*ex\b' azureopenai-cli-v2/**/*.cs
```

returned **zero matches**. All 9 `catch (Exception ex)` sites in v2 emit only `ex.Message` to either stderr or a `Trace.WriteLine`. No user-visible behavior change. The tradeoff is that if v2 crashes in the wild with an uncaught exception, the native unhandled-exception dump will show less rich frame info. Acceptable for a CLI; documented here and in the `csproj` comment. If a user files a crash report, `DOTNET_EnableDiagnostics=1` on a dev machine with a full-fat build still gives us everything we need for forensics.

## Azure.AI.OpenAI 2.1.0 trim warnings -- IL2104 / IL3053

Baseline build emits, unchanged by any lever:

```text
warning IL2104: Assembly 'Azure.AI.OpenAI' produced trim warnings.
warning IL3053: Assembly 'Azure.AI.OpenAI' produced AOT analysis warnings.
ILC: Method 'Azure.AI.OpenAI.Chat.AzureChatClient.PostfixSwapMaxTokens' will always throw
     because: Missing method 'OpenAI.Chat.ChatCompletionOptions.get_SerializedAdditionalRawData()'
ILC: Method 'Azure.AI.OpenAI.Chat.AzureChatClient.PostfixClearStreamOptions' will always throw
     for the same reason.
```

**Diagnosis.** `Azure.AI.OpenAI` 2.1.0 ships as `netstandard2.0` and was built against an earlier `OpenAI` SDK surface. The two `Postfix*` helpers reach for an `internal` property `SerializedAdditionalRawData` that was removed/renamed on the current `OpenAI` package. Under JIT the methods would throw at runtime; under AOT the ILC sees missing-method and rewrites the bodies to always throw. v2 **does not call these Postfix helpers** on its hot path (chat completion goes through the MAF `AIAgent` abstraction, which uses `Microsoft.Extensions.AI.OpenAI` → `OpenAI` directly). The warnings are noise, not latent bugs for v2.

**Disposition for cutover:**

1. **Keep the warnings visible** in the build log. Do not add `<NoWarn>IL2104;IL3053</NoWarn>` -- suppressing blinds us to future warnings on other assemblies.
2. **Upstream:** file a tracking issue against `Azure/azure-sdk-for-net` noting `Azure.AI.OpenAI 2.1.0` AOT-incompatibility with the current `OpenAI` SDK surface. (Not this PR's job; Mr. Wilhelm's change-management queue.)
3. **Exit plan** -- documented in the "Future work" section below.

## Azure.AI.OpenAI -- can we drop it?

Reading the v2 package graph:

```text
Microsoft.Agents.AI            1.1.0
Microsoft.Agents.AI.OpenAI     1.1.0  ──┐
Azure.AI.OpenAI                2.1.0  ──┼──► OpenAI (transitive)
                                        └──► Azure.Core
```

**On-disk managed DLL sizes** in `bin/Release/net10.0/linux-x64/`:

| DLL | Bytes |
|---|---:|
| `OpenAI.dll` | 4,655,568 |
| `Microsoft.Extensions.AI.Abstractions.dll` | 581,152 |
| `Azure.Core.dll` | 426,552 |
| `Microsoft.Extensions.AI.dll` | 402,504 |
| `Azure.AI.OpenAI.dll` | 324,136 |
| `Microsoft.Agents.AI.dll` | 318,536 |
| `Microsoft.Extensions.AI.OpenAI.dll` | 239,176 |
| `Microsoft.Agents.AI.Abstractions.dll` | 236,576 |
| `OpenTelemetry.dll` | 252,416 |
| `OpenTelemetry.Exporter.OpenTelemetryProtocol.dll` | 156,672 |
| `OpenTelemetry.Api.dll` | 64,512 |
| `Microsoft.Agents.AI.OpenAI.dll` | 46,656 |

**Observation.** v2 uses `Azure.AI.OpenAI` in exactly one place (`Program.cs:344`): `new AzureOpenAIClient(endpointUri, new ApiKeyCredential(apiKey))` to construct a client that is then handed to MAF via `.GetChatClient(model).AsIChatClient()`. The MAF agent layer itself is built on top of `Microsoft.Extensions.AI.OpenAI` → `OpenAI` (the open package), which has first-class support for an override `endpoint` via `OpenAIClientOptions.Endpoint`. The Azure-specific client adds: AAD token flow, Azure deployment-name mapping, `api-version` query-string handling, `api-key` header vs `Authorization: Bearer`.

**Rough size upper-bound** if we cut `Azure.AI.OpenAI` + `Azure.Core`: ≈ 750 KB of managed DLL input to ILC. AOT inflation factor varies 1.5-2.5× depending on reachable surface; realistic published-binary savings after trim would land in the **300 KB - 900 KB** range. Non-trivial, but the engineering cost is meaningful:

* Re-implement Azure header conventions (`api-key`, `api-version` query param) on a plain `OpenAIClient` with a custom pipeline policy.
* Re-implement deployment-name routing (`/openai/deployments/{deploymentId}/chat/completions`).
* Give up AAD / Entra ID auth path (we currently support only API-key, so this is moot for v2.0.0 -- but forecloses a likely v2.1 feature).
* Regression surface: every `Azure.AI.OpenAI` integration test needs a parallel path.

**Recommendation:** **defer to v2.1.** We are already under the gate at 1.456×. The juice here is not worth the squeeze on cutover week. Track as "Future work" below.

## Other levers considered but not individually measured

* `<TrimMode>full</TrimMode>` -- **already set** in the csproj. No-op delta.
* `<InvariantGlobalization>true</InvariantGlobalization>` -- **already set**. Confirmed `--feature:System.Globalization.Invariant=true` and `--feature:System.Globalization.PredefinedCulturesOnly=true` in the ILC rsp.
* `<EventSourceSupport>false</EventSourceSupport>` -- already emitted by the SDK defaults (`--feature:System.Diagnostics.Tracing.EventSource.IsSupported=false`).
* `<UseSystemResourceKeys>true</UseSystemResourceKeys>` -- already set by the net10.0 AOT template.
* `<IlcFoldIdenticalMethodBodies>true</IlcFoldIdenticalMethodBodies>` -- default in Size preference; folded in by lever 7.

Only levers measured in the main table are ones the csproj had **not** already committed to, or where the stated expected savings were in play.

## Applied changes

Single file touched: `azureopenai-cli-v2/AzureOpenAI_CLI_V2.csproj`.

```xml
<PropertyGroup>
  <!-- existing AOT switches preserved -->
  <IsTrimmable>true</IsTrimmable>
  <TrimMode>full</TrimMode>
  <InvariantGlobalization>true</InvariantGlobalization>

  <!-- NEW: Kenny Bania trim investigation. See docs/aot-trim-investigation.md. -->
  <OptimizationPreference>Size</OptimizationPreference>
  <StackTraceSupport>false</StackTraceSupport>
</PropertyGroup>
```

No changes to `Program.cs`, `UserConfig.cs`, `Tools/*`, `Dockerfile`, `Observability/*.cs`, any package reference, or any test.

## Gate decision

**Original gate:** v2 AOT binary ≤ 1.50× v1.
**Measured:** **1.456× v1 (12.91 MB vs 8.86 MB)** -- passes.

No revised gate needed. The prior memo's fallback (≤1.75× **and** ≤20 MB absolute) is not invoked. If a future refactor pushes us back over 1.50×, the fallback is still on the books and Costanza can pull it down without another memo round.

## Future work (post-cutover, not blocking)

* **`Azure.AI.OpenAI` direct-OpenAI-SDK rewrite** -- est. 0.3-0.9 MB additional saving. Prerequisite: spike the Azure routing on a plain `OpenAIClient` + custom pipeline policy. Blocks/enabled by: any decision on v2.1 AAD/Entra auth.
* **Upstream `Azure.AI.OpenAI` AOT compat report** -- file issue on `Azure/azure-sdk-for-net` for the IL3053 / "always throw" pair. Owner: Mr. Wilhelm's queue.
* **Slim flavor** -- optional `-p:IncludeOtlp=false` build flavor if size pressure returns (would drop ~150-300 KB of OTel exporter surface).
* **Benchmark harness** -- add size-regression gating to `scripts/bench.sh`: post AOT byte count on every PR, fail PR if Δ ≥ +5%.

---

**Kenny Bania says:** It's gold, Jerry. *Gold!* 1.572 megabytes -- gone! Two lines in a csproj! The 301 tests -- green! The 1025 v1 tests -- green! The format-check -- green! Who wants to review the flamegraph? Jerry? *Jerry?*
