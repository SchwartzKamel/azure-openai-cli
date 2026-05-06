using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AzureOpenAI_CLI.Observability;

// S03E13 -- *The Telemetry*. Frank Costanza (SRE / observability) build, on
// top of Bania's S03E12 bench harness. Privacy-first, opt-in, structured.
//
// Charter (see docs/observability/slo.md for full text):
//   * Default off. Anything other than the literal env value "1" is off.
//   * Sink is stderr only. Even when --json is active for the main run,
//     telemetry stays on stderr so stdout JSON is not polluted.
//   * Schema is fixed, narrow, and physically incapable of carrying user
//     content -- there is no "prompt" / "completion" / "tokens" / "endpoint"
//     parameter on TelemetryEvent. The privacy guarantee is enforced by the
//     compiler, not by reviewer vigilance.
//   * error_class is run through SecretRedactor before emission. ADR-007
//     section 2 mandate -- a bearer token MUST NOT survive Redact().
//   * AOT-clean: System.Text.Json source-gen via AppJsonContext, no
//     reflection, no dynamic types. Manual Utf8JsonWriter so the line is
//     compact (NDJSON) and key order is stable for snapshot tests.
//
// Frank: "If the user did not say yes, we do not collect. End of story."

/// <summary>
/// Structured telemetry event for the compat-dispatch hot path. Field set is
/// intentionally narrow -- there is no parameter for prompts, completions,
/// tokens, API keys, endpoints, file paths, stack traces, or user names.
/// Adding any such field requires a privacy review.
/// </summary>
public sealed record TelemetryEvent(
    [property: JsonPropertyName("event_id")] string EventId,
    [property: JsonPropertyName("ts")] string Ts,
    [property: JsonPropertyName("model")] string Model,
    [property: JsonPropertyName("provider")] string Provider,
    [property: JsonPropertyName("dispatch_path")] string DispatchPath,
    [property: JsonPropertyName("latency_ms_bucket")] string LatencyMsBucket,
    [property: JsonPropertyName("outcome")] string Outcome,
    [property: JsonPropertyName("error_class")] string? ErrorClass);

/// <summary>
/// Opt-in, stderr-only structured telemetry emitter. Activated by setting
/// <c>AZ_AI_TELEMETRY=1</c> in the process environment. Any other value
/// (including <c>"0"</c>, <c>""</c>, <c>"true"</c>, <c>"yes"</c>, <c>"1 "</c>
/// with trailing whitespace, unset, or malformed) keeps telemetry off.
/// <para>
/// The strict-equality acceptance rule was a deliberate call: reviewers are
/// human, environment files get edited by hand, and a single accepted alias
/// ("yes" / "true" / "TRUE" / "1") is one alias too many for a privacy
/// surface. A plain <c>"1"</c> is the only "yes" the emitter understands.
/// </para>
/// </summary>
internal static class TelemetryEmitter
{
    /// <summary>Env-var name. Strict-equality acceptance: only the literal "1" enables.</summary>
    public const string EnvVarName = "AZ_AI_TELEMETRY";

    // Bucket boundaries chosen to track Bania's bench thresholds (S03E12) --
    // 10ms covers prewarm hits, 50/100/250ms covers warm dispatch, 500ms-5s
    // covers normal completion windows, and 10000ms / +inf catches the
    // pathological / runaway / cancelled case. Keep ASCII labels; the
    // schema treats the bucket as an opaque string, not a number, so
    // serialization is stable across locales.
    private static readonly long[] BucketBoundsMs =
        { 10, 50, 100, 250, 500, 1000, 2500, 5000, 10000 };

    private static readonly string[] BucketLabels =
        { "10", "50", "100", "250", "500", "1000", "2500", "5000", "10000", "+inf" };

    /// <summary>
    /// Returns true if and only if the <c>AZ_AI_TELEMETRY</c> env var is the
    /// literal string <c>"1"</c>. Any other value, including unset, whitespace,
    /// case-variants of "true" or "yes", returns false. StringComparison.Ordinal.
    /// </summary>
    public static bool IsEnabled()
    {
        var v = Environment.GetEnvironmentVariable(EnvVarName);
        return string.Equals(v, "1", StringComparison.Ordinal);
    }

    /// <summary>
    /// Bucket a raw latency in milliseconds onto the fixed bucket-label set.
    /// Convention: a sample falls into the smallest bucket whose upper bound
    /// is &gt;= the sample. So 10ms -&gt; "10", 11ms -&gt; "50", 250ms -&gt; "250",
    /// 251ms -&gt; "500". Negative samples clamp to the lowest bucket.
    /// </summary>
    public static string BucketLatency(long latencyMs)
    {
        if (latencyMs < 0) latencyMs = 0;
        for (int i = 0; i < BucketBoundsMs.Length; i++)
        {
            if (latencyMs <= BucketBoundsMs[i]) return BucketLabels[i];
        }
        return BucketLabels[BucketLabels.Length - 1]; // "+inf"
    }

    /// <summary>
    /// Begin a dispatch scope. Cheap (no allocation beyond a small object)
    /// even when telemetry is disabled, so the call site does not have to
    /// branch on <see cref="IsEnabled"/> itself. Default outcome on the
    /// returned scope is <c>"success"</c>; failure paths must call
    /// <see cref="DispatchScope.SetOutcome"/> before <see cref="DispatchScope.Emit"/>.
    /// </summary>
    public static DispatchScope StartDispatch(string model, string provider, string dispatchPath)
        => new DispatchScope(model, provider, dispatchPath);

    /// <summary>
    /// Emit a fully-formed event. Public for tests; production code paths
    /// should prefer <see cref="StartDispatch"/> + <see cref="DispatchScope.Emit"/>.
    /// No-op when <see cref="IsEnabled"/> is false.
    /// </summary>
    public static void Emit(TelemetryEvent ev)
    {
        if (!IsEnabled()) return;
        WriteLine(Serialize(ev));
    }

    /// <summary>
    /// Serialize a <see cref="TelemetryEvent"/> to compact JSON with stable
    /// key order. Used by tests for snapshot assertions; same path as the
    /// stderr emission so the on-the-wire format is the contract.
    /// </summary>
    public static string Serialize(TelemetryEvent ev)
    {
        // Manual writer keeps key order stable (the record-declaration order)
        // and avoids the WriteIndented = true default on AppJsonContext.
        // AOT-safe: no reflection, no dynamic types.
        using var ms = new MemoryStream(256);
        using (var w = new Utf8JsonWriter(ms, new JsonWriterOptions { Indented = false }))
        {
            w.WriteStartObject();
            w.WriteString("event_id", ev.EventId);
            w.WriteString("ts", ev.Ts);
            w.WriteString("model", ev.Model);
            w.WriteString("provider", ev.Provider);
            w.WriteString("dispatch_path", ev.DispatchPath);
            w.WriteString("latency_ms_bucket", ev.LatencyMsBucket);
            w.WriteString("outcome", ev.Outcome);
            if (ev.ErrorClass is null)
            {
                w.WriteNull("error_class");
            }
            else
            {
                w.WriteString("error_class", ev.ErrorClass);
            }
            w.WriteEndObject();
        }
        return Encoding.UTF8.GetString(ms.ToArray());
    }

    /// <summary>
    /// Sanitize and bound an error-class string for emission. Routes through
    /// <see cref="AzureOpenAI_CLI.SecretRedactor.Redact"/> first so any
    /// auth-header / api-key fragment is masked, then truncates at 200 chars
    /// (post-redaction) to keep stderr lines bounded. Null in -&gt; null out.
    /// </summary>
    public static string? FormatErrorClass(string? raw)
    {
        if (string.IsNullOrEmpty(raw)) return null;
        var redacted = AzureOpenAI_CLI.SecretRedactor.Redact(raw);
        if (redacted.Length > 200) redacted = redacted.Substring(0, 200);
        return redacted;
    }

    // -- Internal sink seam ------------------------------------------------
    // Tests hold the ConsoleCapture lock + redirect Console.Error before
    // calling Emit; routing through Console.Error.WriteLine respects that
    // redirection. No background thread, no buffering -- one call, one line.
    private static void WriteLine(string json)
    {
        Console.Error.WriteLine(json);
    }

    /// <summary>
    /// Per-dispatch scope. Records start time and pre-fills the model /
    /// provider / dispatch_path tuple at construction; the call site fills
    /// outcome + error_class via <see cref="SetOutcome"/> on failure paths
    /// (success is the default). Call <see cref="Emit"/> exactly once,
    /// typically in a <c>finally</c>.
    /// </summary>
    public sealed class DispatchScope
    {
        private readonly string _model;
        private readonly string _provider;
        private readonly string _dispatchPath;
        private readonly long _startTicks;
        private string _outcome = "success";
        private string? _errorClass;
        private bool _emitted;

        internal DispatchScope(string model, string provider, string dispatchPath)
        {
            _model = model;
            _provider = provider;
            _dispatchPath = dispatchPath;
            _startTicks = Stopwatch.GetTimestamp();
        }

        /// <summary>
        /// Override the default <c>"success"</c> outcome. Only the four
        /// non-success classes are valid: <c>client_error</c>,
        /// <c>server_error</c>, <c>cancelled</c>, <c>unknown_error</c>.
        /// </summary>
        public void SetOutcome(string outcome, string? errorClass)
        {
            _outcome = outcome;
            _errorClass = TelemetryEmitter.FormatErrorClass(errorClass);
        }

        /// <summary>
        /// Emit the structured event to stderr. No-op when telemetry is
        /// disabled, and no-op on a second call (idempotent so a
        /// <c>finally</c> + a manual emit cannot double-fire).
        /// </summary>
        public void Emit()
        {
            if (_emitted) return;
            _emitted = true;
            if (!IsEnabled()) return;

            var elapsedMs = (long)((Stopwatch.GetTimestamp() - _startTicks)
                * 1000.0 / Stopwatch.Frequency);
            var ev = new TelemetryEvent(
                EventId: Guid.NewGuid().ToString("D"),
                Ts: DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                Model: _model,
                Provider: _provider,
                DispatchPath: _dispatchPath,
                LatencyMsBucket: BucketLatency(elapsedMs),
                Outcome: _outcome,
                ErrorClass: _errorClass);
            WriteLine(Serialize(ev));
        }
    }

    // -- S03E22 *The Fallback* additive surface ----------------------------
    // Two new event shapes, both emit-only (no scope objects). Stable key
    // order matches S03E13 pattern. Strict-equality opt-in (AZ_AI_TELEMETRY=1).
    // No prompts, no completions, no endpoints -- only preset names, indexes,
    // outcome codes, and a redaction-bounded error_class string.
    //
    // Frank: "Two events. Attempt and outcome. That's the spreadsheet."

    /// <summary>One row per attempt in a fallback chain (primary or alternate).</summary>
    public sealed record FallbackAttemptEvent(
        [property: JsonPropertyName("event_type")] string EventType,
        [property: JsonPropertyName("event_id")] string EventId,
        [property: JsonPropertyName("ts")] string Ts,
        [property: JsonPropertyName("primary_preset")] string PrimaryPreset,
        [property: JsonPropertyName("attempt_index")] int AttemptIndex,
        [property: JsonPropertyName("source_preset")] string SourcePreset,
        [property: JsonPropertyName("outcome")] string Outcome,
        [property: JsonPropertyName("error_class")] string? ErrorClass);

    /// <summary>One row per chain (the verdict). winner=null on exhausted/short_circuit/stream_truncated.</summary>
    public sealed record FallbackOutcomeEvent(
        [property: JsonPropertyName("event_type")] string EventType,
        [property: JsonPropertyName("event_id")] string EventId,
        [property: JsonPropertyName("ts")] string Ts,
        [property: JsonPropertyName("primary_preset")] string PrimaryPreset,
        [property: JsonPropertyName("policy_source")] string PolicySource,
        [property: JsonPropertyName("chain")] string Chain,
        [property: JsonPropertyName("outcome")] string Outcome,
        [property: JsonPropertyName("winner")] string? Winner,
        [property: JsonPropertyName("attempts")] int Attempts,
        [property: JsonPropertyName("error_class")] string? ErrorClass);

    /// <summary>Emit a fallback_attempt row. No-op when telemetry disabled.</summary>
    public static void EmitFallbackAttempt(
        string primaryPreset, int attemptIndex, string sourcePreset,
        string outcome, string? errorClass, string? message)
    {
        if (!IsEnabled()) return;
        var ev = new FallbackAttemptEvent(
            EventType: "fallback_attempt",
            EventId: Guid.NewGuid().ToString("D"),
            Ts: DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
            PrimaryPreset: primaryPreset,
            AttemptIndex: attemptIndex,
            SourcePreset: sourcePreset,
            Outcome: outcome,
            ErrorClass: FormatErrorClass(errorClass ?? message));
        WriteLine(SerializeFallbackAttempt(ev));
    }

    /// <summary>Emit a fallback_outcome row. No-op when telemetry disabled.</summary>
    public static void EmitFallbackOutcome(
        string primaryPreset, string policySource, System.Collections.Generic.IReadOnlyList<string> chain,
        string outcome, string? winner, int attempts, string? errorClass)
    {
        if (!IsEnabled()) return;
        var ev = new FallbackOutcomeEvent(
            EventType: "fallback_outcome",
            EventId: Guid.NewGuid().ToString("D"),
            Ts: DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
            PrimaryPreset: primaryPreset,
            PolicySource: policySource,
            Chain: string.Join(",", chain),
            Outcome: outcome,
            Winner: winner,
            Attempts: attempts,
            ErrorClass: FormatErrorClass(errorClass));
        WriteLine(SerializeFallbackOutcome(ev));
    }

    /// <summary>Stable-key-order serialization for fallback_attempt (test contract).</summary>
    public static string SerializeFallbackAttempt(FallbackAttemptEvent ev)
    {
        using var ms = new MemoryStream(256);
        using (var w = new Utf8JsonWriter(ms, new JsonWriterOptions { Indented = false }))
        {
            w.WriteStartObject();
            w.WriteString("event_type", ev.EventType);
            w.WriteString("event_id", ev.EventId);
            w.WriteString("ts", ev.Ts);
            w.WriteString("primary_preset", ev.PrimaryPreset);
            w.WriteNumber("attempt_index", ev.AttemptIndex);
            w.WriteString("source_preset", ev.SourcePreset);
            w.WriteString("outcome", ev.Outcome);
            if (ev.ErrorClass is null) w.WriteNull("error_class");
            else w.WriteString("error_class", ev.ErrorClass);
            w.WriteEndObject();
        }
        return Encoding.UTF8.GetString(ms.ToArray());
    }

    /// <summary>Stable-key-order serialization for fallback_outcome (test contract).</summary>
    public static string SerializeFallbackOutcome(FallbackOutcomeEvent ev)
    {
        using var ms = new MemoryStream(256);
        using (var w = new Utf8JsonWriter(ms, new JsonWriterOptions { Indented = false }))
        {
            w.WriteStartObject();
            w.WriteString("event_type", ev.EventType);
            w.WriteString("event_id", ev.EventId);
            w.WriteString("ts", ev.Ts);
            w.WriteString("primary_preset", ev.PrimaryPreset);
            w.WriteString("policy_source", ev.PolicySource);
            w.WriteString("chain", ev.Chain);
            w.WriteString("outcome", ev.Outcome);
            if (ev.Winner is null) w.WriteNull("winner");
            else w.WriteString("winner", ev.Winner);
            w.WriteNumber("attempts", ev.Attempts);
            if (ev.ErrorClass is null) w.WriteNull("error_class");
            else w.WriteString("error_class", ev.ErrorClass);
            w.WriteEndObject();
        }
        return Encoding.UTF8.GetString(ms.ToArray());
    }
}
