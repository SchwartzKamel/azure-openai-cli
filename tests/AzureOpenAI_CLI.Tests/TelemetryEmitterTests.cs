using System;
using System.IO;
using System.Text;
using AzureOpenAI_CLI.Observability;
using Xunit;

namespace AzureOpenAI_CLI.V2.Tests;

/// <summary>
/// S03E13 -- Frank Costanza opt-in telemetry tests. Joins the
/// <c>ConsoleCapture</c> collection because every test mutates the
/// <c>AZ_AI_TELEMETRY</c> env var and captures <c>Console.Error</c>;
/// xUnit must serialize them with the other stderr/env mutators.
///
/// What is NOT tested here (intentional):
///   * Bania's <c>FakeChatClient</c> does not factor into IsEnabled / schema /
///     bucketing -- those are pure functions, no client involved. The fake
///     does drive the integration-flavored "scope around a fake call" test
///     so the latency bucket lines up with the harness vocabulary from
///     S03E12.
///   * No filesystem writes: the emitter writes only to
///     <c>Console.Error</c>; the test redirects that stream into a
///     <c>StringWriter</c>.
/// </summary>
[Collection("ConsoleCapture")]
public class TelemetryEmitterTests
{
    /// <summary>Lock + restore env var; the emitter reads it on every call.</summary>
    private sealed class EnvScope : IDisposable
    {
        private readonly string? _prev;
        public EnvScope(string? value)
        {
            _prev = Environment.GetEnvironmentVariable(TelemetryEmitter.EnvVarName);
            Environment.SetEnvironmentVariable(TelemetryEmitter.EnvVarName, value);
        }
        public void Dispose()
        {
            Environment.SetEnvironmentVariable(TelemetryEmitter.EnvVarName, _prev);
        }
    }

    /// <summary>Lock + restore Console.Error; pairs with EnvScope under the collection lock.</summary>
    private sealed class StderrScope : IDisposable
    {
        private readonly TextWriter _prev;
        public StringWriter Writer { get; }
        public StderrScope()
        {
            _prev = Console.Error;
            Writer = new StringWriter();
            Console.SetError(Writer);
        }
        public void Dispose()
        {
            Console.SetError(_prev);
            Writer.Dispose();
        }
    }

    // -- IsEnabled: strict-equality "1" only ----------------------------------

    [Theory]
    [InlineData(null)]        // unset
    [InlineData("")]
    [InlineData("0")]
    [InlineData("false")]
    [InlineData("FALSE")]
    [InlineData("no")]
    [InlineData("off")]
    [InlineData("true")]      // documented: only "1" enables
    [InlineData("yes")]       // documented: only "1" enables
    [InlineData("1 ")]        // trailing space -- strict match required
    [InlineData(" 1")]        // leading space
    [InlineData("01")]
    [InlineData("garbageXYZ")]
    public void IsEnabled_NonExactValue_ReturnsFalse(string? value)
    {
        using var _ = new EnvScope(value);
        Assert.False(TelemetryEmitter.IsEnabled());
    }

    [Fact]
    public void IsEnabled_ExactlyOne_ReturnsTrue()
    {
        using var _ = new EnvScope("1");
        Assert.True(TelemetryEmitter.IsEnabled());
    }

    // -- Bucketing: edge cases at every boundary ------------------------------

    [Theory]
    [InlineData(0, "10")]
    [InlineData(1, "10")]
    [InlineData(10, "10")]      // exact boundary -> lower bucket
    [InlineData(11, "50")]      // 1ms over -> next bucket
    [InlineData(50, "50")]
    [InlineData(51, "100")]
    [InlineData(100, "100")]
    [InlineData(101, "250")]
    [InlineData(250, "250")]
    [InlineData(251, "500")]
    [InlineData(500, "500")]
    [InlineData(501, "1000")]
    [InlineData(1000, "1000")]
    [InlineData(1001, "2500")]
    [InlineData(2500, "2500")]
    [InlineData(2501, "5000")]
    [InlineData(5000, "5000")]
    [InlineData(5001, "10000")]
    [InlineData(10000, "10000")]
    [InlineData(10001, "+inf")]
    [InlineData(60_000, "+inf")]
    [InlineData(long.MaxValue, "+inf")]
    public void BucketLatency_EdgeCases_LandInExpectedBucket(long latencyMs, string expected)
    {
        Assert.Equal(expected, TelemetryEmitter.BucketLatency(latencyMs));
    }

    [Fact]
    public void BucketLatency_NegativeSample_ClampsToLowest()
    {
        Assert.Equal("10", TelemetryEmitter.BucketLatency(-5));
    }

    // -- Schema serialization: round-trip per outcome class -------------------

    [Theory]
    [InlineData("success", null)]
    [InlineData("client_error", "RequestFailedException: bad key")]
    [InlineData("server_error", "RequestFailedException: 500 internal")]
    [InlineData("cancelled", null)]
    [InlineData("unknown_error", "InvalidOperationException: weird")]
    public void Serialize_OutcomeClass_RoundTripsExpectedKeysAndValues(string outcome, string? errorClass)
    {
        var ev = new TelemetryEvent(
            EventId: "11111111-2222-3333-4444-555555555555",
            Ts: "2026-05-09T12:34:56.789Z",
            Model: "gpt-4o-mini",
            Provider: "azure",
            DispatchPath: "azure-default",
            LatencyMsBucket: "250",
            Outcome: outcome,
            ErrorClass: errorClass);

        var json = TelemetryEmitter.Serialize(ev);

        // Must be a single line (NDJSON-ready).
        Assert.DoesNotContain("\n", json);
        Assert.DoesNotContain("\r", json);

        // Required keys present.
        Assert.Contains("\"event_id\":\"11111111-2222-3333-4444-555555555555\"", json);
        Assert.Contains("\"ts\":\"2026-05-09T12:34:56.789Z\"", json);
        Assert.Contains("\"model\":\"gpt-4o-mini\"", json);
        Assert.Contains("\"provider\":\"azure\"", json);
        Assert.Contains("\"dispatch_path\":\"azure-default\"", json);
        Assert.Contains("\"latency_ms_bucket\":\"250\"", json);
        Assert.Contains("\"outcome\":\"" + outcome + "\"", json);
        if (errorClass is null)
        {
            Assert.Contains("\"error_class\":null", json);
        }
        else
        {
            Assert.Contains("\"error_class\":\"" + errorClass + "\"", json);
        }
    }

    [Fact]
    public void Serialize_KeyOrder_IsStableAndCanonicallyOrdered()
    {
        // Snapshot guard: any future re-ordering of the on-the-wire field
        // sequence would be a schema break for downstream consumers parsing
        // by position. The contract is the order in this assertion, not the
        // record declaration alone.
        var ev = new TelemetryEvent(
            EventId: "00000000-0000-0000-0000-000000000000",
            Ts: "2026-05-09T00:00:00.000Z",
            Model: "m",
            Provider: "azure",
            DispatchPath: "azure-default",
            LatencyMsBucket: "10",
            Outcome: "success",
            ErrorClass: null);
        var json = TelemetryEmitter.Serialize(ev);
        var expected =
            "{\"event_id\":\"00000000-0000-0000-0000-000000000000\""
            + ",\"ts\":\"2026-05-09T00:00:00.000Z\""
            + ",\"model\":\"m\""
            + ",\"provider\":\"azure\""
            + ",\"dispatch_path\":\"azure-default\""
            + ",\"latency_ms_bucket\":\"10\""
            + ",\"outcome\":\"success\""
            + ",\"error_class\":null}";
        Assert.Equal(expected, json);
    }

    // -- SecretRedactor wired to error_class ----------------------------------
    //
    // Cite known-leakable strings from SecretRedactorTests so a future
    // redactor regression that breaks one path (the redactor itself) does
    // not silently slip past the telemetry surface.

    [Fact]
    public void FormatErrorClass_BearerTokenInMessage_IsMasked()
    {
        // Same shape as SecretRedactorTests.P1_BearerTokenNeverAppearsInRedactedOutput.
        var raw = "RequestFailedException: Authorization: Bearer sk-very-secret retry?";
        var formatted = TelemetryEmitter.FormatErrorClass(raw);
        Assert.NotNull(formatted);
        Assert.DoesNotContain("sk-very-secret", formatted!, StringComparison.Ordinal);
        Assert.DoesNotContain("Bearer ", formatted, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("[REDACTED:bearer]", formatted, StringComparison.Ordinal);
    }

    [Fact]
    public void FormatErrorClass_ApiKeyHeaderInMessage_IsMasked()
    {
        var raw = "Upstream rejected: api-key: AbCdEf1234567890 -- 401 Unauthorized";
        var formatted = TelemetryEmitter.FormatErrorClass(raw);
        Assert.NotNull(formatted);
        Assert.DoesNotContain("AbCdEf1234567890", formatted!, StringComparison.Ordinal);
        Assert.Contains("[REDACTED:api-key]", formatted, StringComparison.Ordinal);
    }

    [Fact]
    public void FormatErrorClass_OverlongInput_IsTruncatedAt200()
    {
        var raw = new string('x', 5000);
        var formatted = TelemetryEmitter.FormatErrorClass(raw);
        Assert.NotNull(formatted);
        Assert.Equal(200, formatted!.Length);
    }

    [Fact]
    public void FormatErrorClass_NullOrEmpty_ReturnsNull()
    {
        Assert.Null(TelemetryEmitter.FormatErrorClass(null));
        Assert.Null(TelemetryEmitter.FormatErrorClass(string.Empty));
    }

    // -- Negative test: when disabled, Emit() is a no-op ----------------------

    [Fact]
    public void Emit_TelemetryDisabled_WritesNothingToStderr()
    {
        using var _env = new EnvScope(null);   // unset
        using var _err = new StderrScope();

        var ev = new TelemetryEvent(
            EventId: Guid.NewGuid().ToString("D"),
            Ts: DateTime.UtcNow.ToString("O"),
            Model: "gpt-4o-mini",
            Provider: "azure",
            DispatchPath: "azure-default",
            LatencyMsBucket: "10",
            Outcome: "success",
            ErrorClass: null);

        TelemetryEmitter.Emit(ev);

        var captured = _err.Writer.ToString();
        Assert.Equal(string.Empty, captured);
    }

    [Fact]
    public void Emit_TelemetryEnabled_WritesExactlyOneJsonLineToStderr()
    {
        using var _env = new EnvScope("1");
        using var _err = new StderrScope();

        var ev = new TelemetryEvent(
            EventId: "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
            Ts: "2026-05-09T01:02:03.456Z",
            Model: "gpt-4o-mini",
            Provider: "azure",
            DispatchPath: "azure-default",
            LatencyMsBucket: "100",
            Outcome: "success",
            ErrorClass: null);

        TelemetryEmitter.Emit(ev);

        var captured = _err.Writer.ToString();
        // Exactly one trailing newline (Console.Error.WriteLine).
        var lines = captured.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Single(lines);
        var line = lines[0].TrimEnd('\r');
        Assert.StartsWith("{", line);
        Assert.EndsWith("}", line);
        Assert.Contains("\"event_id\":\"aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee\"", line);
        // No ANSI escape sequence -- Mickey's a11y work (E14) may add color
        // to other stderr surfaces; telemetry stays raw JSON regardless.
        Assert.DoesNotContain("\u001b[", line);
    }

    // -- DispatchScope: success default + outcome override --------------------

    [Fact]
    public void DispatchScope_SuccessDefault_EmitsSuccessOutcome()
    {
        using var _env = new EnvScope("1");
        using var _err = new StderrScope();

        var scope = TelemetryEmitter.StartDispatch("gpt-4o-mini", "azure", "azure-default");
        scope.Emit();

        var captured = _err.Writer.ToString();
        Assert.Contains("\"outcome\":\"success\"", captured);
        Assert.Contains("\"error_class\":null", captured);
        Assert.Contains("\"provider\":\"azure\"", captured);
        Assert.Contains("\"dispatch_path\":\"azure-default\"", captured);
    }

    [Fact]
    public void DispatchScope_SetOutcome_OverridesAndRedactsErrorClass()
    {
        using var _env = new EnvScope("1");
        using var _err = new StderrScope();

        var scope = TelemetryEmitter.StartDispatch("gpt-4o-mini", "openai", "compat-allowlist");
        scope.SetOutcome("client_error", "401: Authorization: Bearer sk-leak1234");
        scope.Emit();

        var captured = _err.Writer.ToString();
        Assert.Contains("\"outcome\":\"client_error\"", captured);
        Assert.DoesNotContain("sk-leak1234", captured);
        Assert.Contains("[REDACTED:bearer]", captured);
        Assert.Contains("\"provider\":\"openai\"", captured);
    }

    [Fact]
    public void DispatchScope_DoubleEmit_IsIdempotent()
    {
        using var _env = new EnvScope("1");
        using var _err = new StderrScope();

        var scope = TelemetryEmitter.StartDispatch("gpt-4o-mini", "azure", "azure-default");
        scope.Emit();
        scope.Emit(); // second call must be a no-op

        var captured = _err.Writer.ToString();
        var lines = captured.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Single(lines);
    }

    [Fact]
    public void DispatchScope_TelemetryDisabled_ProducesNoStderrOutput()
    {
        using var _env = new EnvScope("0");
        using var _err = new StderrScope();

        var scope = TelemetryEmitter.StartDispatch("gpt-4o-mini", "azure", "azure-default");
        scope.SetOutcome("server_error", "boom");
        scope.Emit();

        Assert.Equal(string.Empty, _err.Writer.ToString());
    }

    // -- Integration-flavored: scope around a Bania FakeChatClient call -------

    [Fact]
    public async System.Threading.Tasks.Task DispatchScope_AroundFakeChatCall_LandsInExpectedLatencyBucket()
    {
        using var _env = new EnvScope("1");
        using var _err = new StderrScope();

        // Bania's S03E12 FakeChatClient: deterministic latency knob, no
        // network. ~120ms first-token latency lands the dispatch in the
        // "250" bucket (next boundary above 100ms).
        var fake = new AzureOpenAI_CLI.Tests.Benchmarks.FakeChatClient(
            firstTokenLatency: TimeSpan.FromMilliseconds(120),
            tokenCount: 1);

        var scope = TelemetryEmitter.StartDispatch("gpt-4o-mini", "azure", "azure-default");
        var resp = await fake.GetResponseAsync(
            new[] { new Microsoft.Extensions.AI.ChatMessage(Microsoft.Extensions.AI.ChatRole.User, "hi") });
        scope.Emit();

        Assert.NotNull(resp);
        var captured = _err.Writer.ToString();
        // 120ms baseline latency lands at-or-above the 250 bucket boundary.
        // Shared-runner CPU jitter (notably macos-latest GHA) can push elapsed
        // into the next bucket(s); accept any bucket >= 250 to keep the assertion
        // deterministic. Sub-250 buckets would indicate the fake-client knob is
        // broken; super-high buckets still confirm the scope measured wall-clock.
        // See S04SP4 *The Bucket* / Frank + Puddy escalation from SP2.
        string[] acceptableBuckets = { "250", "500", "1000", "2500", "5000", "10000", "+inf" };
        bool bucketMatched = false;
        foreach (var b in acceptableBuckets)
        {
            if (captured.IndexOf("\"latency_ms_bucket\":\"" + b + "\"", StringComparison.Ordinal) >= 0)
            {
                bucketMatched = true;
                break;
            }
        }
        Assert.True(bucketMatched,
            "Expected latency_ms_bucket in {250,500,1000,2500,5000,10000,+inf}; payload was: " + captured);
        Assert.Contains("\"outcome\":\"success\"", captured);
    }

    // -- S04E05 W3 *The Picker* resolver_decision surface ---------------------
    // Closes F-PICKER-TRACE-01. EmitResolverDecision shares the same gate
    // (AZ_AI_TELEMETRY=1, strict-equality) as the rest of TelemetryEmitter.

    [Fact]
    public void EmitResolverDecision_EnabledGate_WritesSingleNDJSONLineWithStableKeyOrder()
    {
        using var _env = new EnvScope("1");
        using var _err = new StderrScope();

        TelemetryEmitter.EmitResolverDecision(
            model: "gpt-4o-mini",
            reasonCode: "ALLOWLIST_HEAD",
            humanReason: "model 'gpt-4o-mini' chosen as head of AZUREOPENAIMODEL");

        var captured = _err.Writer.ToString().TrimEnd('\r', '\n');
        Assert.DoesNotContain("\n", captured);
        Assert.DoesNotContain("\r", captured);

        int iEvent = captured.IndexOf("\"event\":\"resolver_decision\"", StringComparison.Ordinal);
        int iModel = captured.IndexOf("\"model\":\"gpt-4o-mini\"", StringComparison.Ordinal);
        int iReason = captured.IndexOf("\"reason_code\":\"ALLOWLIST_HEAD\"", StringComparison.Ordinal);
        int iHuman = captured.IndexOf("\"human_reason\":", StringComparison.Ordinal);
        int iTs = captured.IndexOf("\"ts_ms\":", StringComparison.Ordinal);

        Assert.True(iEvent >= 0, "missing event key in: " + captured);
        Assert.True(iModel > iEvent, "model key not after event in: " + captured);
        Assert.True(iReason > iModel, "reason_code key not after model in: " + captured);
        Assert.True(iHuman > iReason, "human_reason key not after reason_code in: " + captured);
        Assert.True(iTs > iHuman, "ts_ms key not after human_reason in: " + captured);
    }

    [Fact]
    public void EmitResolverDecision_DisabledGate_WritesNothing()
    {
        using var _env = new EnvScope(null);
        using var _err = new StderrScope();

        TelemetryEmitter.EmitResolverDecision(
            model: "gpt-4o-mini",
            reasonCode: "EXPLICIT",
            humanReason: "model 'gpt-4o-mini' chosen because user passed --model");

        Assert.Equal(string.Empty, _err.Writer.ToString());
    }
}
