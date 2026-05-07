using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AzureOpenAI_CLI.Capabilities;
using AzureOpenAI_CLI.Observability;
using AzureOpenAI_CLI.Resilience;
using AzureOpenAI_CLI.Tests.Benchmarks;
using Microsoft.Extensions.AI;
using Xunit;

namespace AzureOpenAI_CLI.V2.Tests;

/// <summary>
/// S03E22 -- The Fallback. Frank Costanza on-call: a chain that does not
/// exercise its hard-failure exits is a chain that pages at 3am with no
/// one to answer. This suite is the answer key.
///
/// Joins the <c>ConsoleCapture</c> collection because tests redirect
/// <c>Console.Error</c> for warn-line / telemetry capture and toggle the
/// <c>AZ_AI_TELEMETRY</c> env var. xUnit must serialize against the rest of
/// the stderr/env mutators.
///
/// FakeChatClient (Bania, S03E12 + S03E17) supplies the streaming + token
/// primitives; failure modes that FakeChatClient does not natively provide
/// (HTTP 5xx / 401 / 429 / network / capability-mismatch) are layered on
/// via the local <see cref="FaultyChatClient"/> wrapper -- a thin decorator
/// that delegates the success path to FakeChatClient and injects exception
/// types per knob. That keeps Bania's primitive intact while letting the
/// chain see the full failure-class taxonomy.
/// </summary>
[Collection("ConsoleCapture")]
public class FallbackChainTests
{
    // ── Test infrastructure ─────────────────────────────────────────────

    /// <summary>Lock + restore an env var around a scope. Pairs with ConsoleCapture lock.</summary>
    private sealed class EnvScope : IDisposable
    {
        private readonly string _name;
        private readonly string? _prev;
        public EnvScope(string name, string? value)
        {
            _name = name;
            _prev = Environment.GetEnvironmentVariable(name);
            Environment.SetEnvironmentVariable(name, value);
        }
        public void Dispose() => Environment.SetEnvironmentVariable(_name, _prev);
    }

    /// <summary>Capture stderr into a StringWriter for the duration of the scope.</summary>
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

    /// <summary>
    /// Test-only IChatClient decorator that injects controllable failure
    /// modes around an inner FakeChatClient. The streaming knobs from
    /// Bania's S03E17 extension (chunk sequence + throwAfterChunk) drive
    /// the success path; this wrapper supplies the failure-class taxonomy
    /// the chain has to discriminate against (transient vs auth vs
    /// client-error vs capability vs cancelled).
    /// </summary>
    private sealed class FaultyChatClient : IChatClient
    {
        private readonly IChatClient? _inner;
        private readonly Exception? _throwOnNonStream;
        private readonly Exception? _throwBeforeFirstChunk;
        private readonly int? _streamChunksBeforeThrow;
        private readonly Exception? _throwAfterStreamChunks;

        public int CallCount;

        /// <summary>Always throws <paramref name="onNonStream"/> when the chain calls the non-stream path; same for the stream path before first chunk.</summary>
        public static FaultyChatClient AlwaysThrow(Exception exNonStream, Exception exStream) =>
            new(null, exNonStream, exStream, null, null);

        /// <summary>Streams <paramref name="chunks"/> chunks of "ok" then throws.</summary>
        public static FaultyChatClient StreamThenThrow(int chunksBeforeThrow, Exception exAfterChunks) =>
            new(null, null, null, chunksBeforeThrow, exAfterChunks);

        /// <summary>Wraps an inner client (FakeChatClient) for the success path.</summary>
        public static FaultyChatClient Wrapping(IChatClient inner) =>
            new(inner, null, null, null, null);

        private FaultyChatClient(IChatClient? inner,
            Exception? throwOnNonStream,
            Exception? throwBeforeFirstChunk,
            int? streamChunksBeforeThrow,
            Exception? throwAfterStreamChunks)
        {
            _inner = inner;
            _throwOnNonStream = throwOnNonStream;
            _throwBeforeFirstChunk = throwBeforeFirstChunk;
            _streamChunksBeforeThrow = streamChunksBeforeThrow;
            _throwAfterStreamChunks = throwAfterStreamChunks;
        }

        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref CallCount);
            if (_throwOnNonStream is not null) throw _throwOnNonStream;
            if (_inner is not null) return _inner.GetResponseAsync(messages, options, cancellationToken);
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "ok")));
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref CallCount);
            if (_throwBeforeFirstChunk is not null)
            {
                throw _throwBeforeFirstChunk;
            }
            if (_streamChunksBeforeThrow is int k)
            {
                for (int i = 0; i < k; i++)
                {
                    await Task.Yield();
                    yield return new ChatResponseUpdate(ChatRole.Assistant, "ok");
                }
                if (_throwAfterStreamChunks is not null) throw _throwAfterStreamChunks;
                yield break;
            }
            if (_inner is not null)
            {
                await foreach (var u in _inner.GetStreamingResponseAsync(messages, options, cancellationToken))
                    yield return u;
                yield break;
            }
            yield return new ChatResponseUpdate(ChatRole.Assistant, "ok");
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }

    private static HttpRequestException Http(int status, string msg = "boom")
        => new(msg, inner: null, statusCode: (HttpStatusCode)status);

    private static List<ChatMessage> Hello() =>
        new() { new(ChatRole.User, "hello") };

    // ── 1-10. FallbackPolicy.Parse / Resolve ─────────────────────────────

    [Fact]
    public void Policy_Parse_EmptyInput_ReturnsNoneSourced()
    {
        var p = FallbackPolicy.Parse("", "test");
        Assert.False(p.IsActive);
        Assert.False(p.HasError);
    }

    [Fact]
    public void Policy_Parse_SinglePreset_OneProvider()
    {
        var p = FallbackPolicy.Parse("openai", "test");
        Assert.True(p.IsActive);
        Assert.Equal(new[] { "openai" }, p.Providers);
    }

    [Fact]
    public void Policy_Parse_TwoPresets_OrderPreserved()
    {
        var p = FallbackPolicy.Parse("ollama,openai", "test");
        Assert.True(p.IsActive);
        Assert.Equal(new[] { "ollama", "openai" }, p.Providers);
    }

    [Fact]
    public void Policy_Parse_ThreePresets_AcceptedAtMaxDepth()
    {
        var p = FallbackPolicy.Parse("ollama,openai,groq", "test");
        Assert.True(p.IsActive);
        Assert.Equal(3, p.Providers.Count);
    }

    [Fact]
    public void Policy_Parse_FourPresets_RejectedWithMaxDepthError()
    {
        var p = FallbackPolicy.Parse("ollama,openai,groq,together", "test");
        Assert.True(p.HasError);
        Assert.Contains("maximum of 3", p.ErrorMessage);
    }

    [Fact]
    public void Policy_Parse_UnknownPreset_RejectedWithKnownList()
    {
        var p = FallbackPolicy.Parse("ollama,bogus", "test");
        Assert.True(p.HasError);
        Assert.Contains("Unknown fallback provider preset 'bogus'", p.ErrorMessage);
        Assert.Contains("openai", p.ErrorMessage);
    }

    [Fact]
    public void Policy_Parse_DuplicatePreset_RejectedAsAmbiguous()
    {
        var p = FallbackPolicy.Parse("openai,openai", "test");
        Assert.True(p.HasError);
        Assert.Contains("more than once", p.ErrorMessage);
    }

    [Fact]
    public void Policy_Parse_WhitespaceTrimmed_AndCaseLowered()
    {
        var p = FallbackPolicy.Parse(" Ollama , OPENAI ", "test");
        Assert.True(p.IsActive);
        Assert.Equal(new[] { "ollama", "openai" }, p.Providers);
    }

    [Fact]
    public void Policy_Parse_EmptyTokenInList_Rejected()
    {
        var p = FallbackPolicy.Parse("openai,,groq", "test");
        Assert.True(p.HasError);
        Assert.Contains("empty entry", p.ErrorMessage);
    }

    [Fact]
    public void Policy_Resolve_CliFlagWinsOverEnv()
    {
        var argv = new[] { "az-ai", "--fallback", "openai" };
        var p = FallbackPolicy.Resolve(argv, name => name == FallbackPolicy.EnvVarName ? "ollama,groq" : null);
        Assert.True(p.IsActive);
        Assert.Equal(new[] { "openai" }, p.Providers);
        Assert.StartsWith("cli:", p.Source);
    }

    [Fact]
    public void Policy_Resolve_EnvUsedWhenCliAbsent()
    {
        var argv = new[] { "az-ai" };
        var p = FallbackPolicy.Resolve(argv, _ => "ollama");
        Assert.True(p.IsActive);
        Assert.Equal(new[] { "ollama" }, p.Providers);
        Assert.StartsWith("env:", p.Source);
    }

    [Fact]
    public void Policy_Resolve_NoFlagNoEnv_ReturnsNone()
    {
        var p = FallbackPolicy.Resolve(new[] { "az-ai" }, _ => null);
        Assert.False(p.IsActive);
        Assert.False(p.HasError);
    }

    [Fact]
    public void Policy_Resolve_FlagWithoutValue_ReturnsErrorPolicy()
    {
        var p = FallbackPolicy.Resolve(new[] { "az-ai", "--fallback" }, _ => null);
        Assert.True(p.HasError);
        Assert.Contains("requires a comma-separated list", p.ErrorMessage);
    }

    [Fact]
    public void Policy_IsKnownPreset_AcceptsKnown_RejectsBogus()
    {
        Assert.True(FallbackPolicy.IsKnownPreset("openai"));
        Assert.True(FallbackPolicy.IsKnownPreset("OLLAMA"));
        Assert.False(FallbackPolicy.IsKnownPreset("bogus"));
    }

    // ── S03E30 ordering-contract tests ───────────────────────────────────
    // These assert that FallbackPolicy.Resolve is purely syntactic: it never
    // inspects AZUREOPENAIENDPOINT, AZUREOPENAIAPI, or AZUREOPENAIMODEL.
    // A malformed --fallback flag must error (HasError=true) regardless of
    // whether creds are present or absent -- no env masking required.

    [Fact]
    public void FallbackParser_UnknownPreset_HasErrorWithNoCreds()
    {
        // Simulate the binary being invoked with no creds in the environment.
        // Resolve must report the parse error independent of creds.
        var argv = new[] { "az-ai", "--fallback", "bogus", "--", "hi" };
        var p = FallbackPolicy.Resolve(argv, name => name switch
        {
            // Explicitly absent: endpoint, key, model -- the "no creds" scenario.
            "AZUREOPENAIENDPOINT" => null,
            "AZUREOPENAIAPI" => null,
            "AZUREOPENAIMODEL" => null,
            _ => null,
        });
        Assert.True(p.HasError, "Unknown preset must be a parse error even with no creds set");
        Assert.Contains("Unknown fallback provider preset 'bogus'", p.ErrorMessage);
        Assert.Contains("openai", p.ErrorMessage);
    }

    [Fact]
    public void FallbackParser_DepthExceeded_HasErrorWithNoCreds()
    {
        var argv = new[] { "az-ai", "--fallback", "openai,groq,together,cloudflare", "--", "hi" };
        var p = FallbackPolicy.Resolve(argv, _ => null);
        Assert.True(p.HasError, "Depth > MaxDepth must be a parse error even with no creds set");
        Assert.Contains("exceeds the maximum", p.ErrorMessage);
    }

    [Fact]
    public void FallbackParser_DuplicatePreset_HasErrorWithNoCreds()
    {
        var argv = new[] { "az-ai", "--fallback", "openai,openai", "--", "hi" };
        var p = FallbackPolicy.Resolve(argv, _ => null);
        Assert.True(p.HasError, "Duplicate preset must be a parse error even with no creds set");
        Assert.Contains("more than once", p.ErrorMessage);
    }

    [Fact]
    public void FallbackParser_ValidPreset_NotAnErrorEvenWithNoCreds()
    {
        // A valid --fallback should parse cleanly (no HasError) regardless of
        // whether creds exist -- the creds check happens after the parse.
        var argv = new[] { "az-ai", "--fallback", "openai", "--", "hi" };
        var p = FallbackPolicy.Resolve(argv, _ => null);
        Assert.False(p.HasError, "Valid --fallback must not be a parse error");
        Assert.True(p.IsActive);
    }

    // ── 11+. FallbackChain.Wrap behaviour ────────────────────────────────

    [Fact]
    public void Wrap_EmptyPolicy_ReturnsPrimaryUnwrapped()
    {
        var primary = new FakeChatClient();
        var result = FallbackChain.Wrap(primary, "azure", "gpt-4o-mini",
            FallbackPolicy.None, (p, m) => AlternateBuildResult.Skipped("noop"), null, false);
        Assert.Same(primary, result);
    }

    [Fact]
    public async Task Wrap_PrimarySuccess_NonStream_NoFallbackTriggered()
    {
        var primary = new FakeChatClient();
        var altCalls = 0;
        var policy = FallbackPolicy.Parse("openai", "test");
        var wrapped = FallbackChain.Wrap(primary, "azure", "gpt-4o-mini", policy,
            (p, m) => { altCalls++; return AlternateBuildResult.Skipped("noop"); },
            null, false);
        var resp = await wrapped.GetResponseAsync(Hello());
        Assert.NotNull(resp);
        Assert.Equal(0, altCalls);
    }

    [Fact]
    public async Task Wrap_NonStream_PrimaryTransient_FallbackSucceeds_OneHopWarn()
    {
        using var stderr = new StderrScope();
        var faulty = FaultyChatClient.AlwaysThrow(Http(503), Http(503));
        var alt = new FakeChatClient(tokenWord: "alt");
        var policy = FallbackPolicy.Parse("openai", "test");
        var wrapped = FallbackChain.Wrap(faulty, "azure", "gpt-4o-mini", policy,
            (p, m) => AlternateBuildResult.Ready(alt),
            line => Console.Error.WriteLine(line), false);

        var resp = await wrapped.GetResponseAsync(Hello());
        Assert.NotNull(resp);
        Assert.Contains("[fallback] hop 1/1", stderr.Writer.ToString());
        Assert.Contains("openai", stderr.Writer.ToString());
    }

    [Fact]
    public async Task Wrap_NonStream_PrimaryAndFallbackBothFail_OriginalErrorSurfaces()
    {
        using var stderr = new StderrScope();
        var primary503 = Http(503, "primary-down");
        var faulty = FaultyChatClient.AlwaysThrow(primary503, primary503);
        var altFaulty = FaultyChatClient.AlwaysThrow(Http(502, "alt-down"), Http(502, "alt-down"));
        var policy = FallbackPolicy.Parse("openai", "test");
        var wrapped = FallbackChain.Wrap(faulty, "azure", "gpt-4o-mini", policy,
            (p, m) => AlternateBuildResult.Ready(altFaulty),
            line => Console.Error.WriteLine(line), false);

        var ex = await Assert.ThrowsAsync<FallbackChainExhaustedException>(
            async () => await wrapped.GetResponseAsync(Hello()));
        Assert.Same(primary503, ex.InnerException);
        Assert.Equal(1, ex.ChainDepth);
    }

    [Fact]
    public async Task Wrap_NonStream_Auth401_NoFallback_RaisesOriginal()
    {
        var auth = Http(401, "no-soup");
        var faulty = FaultyChatClient.AlwaysThrow(auth, auth);
        var altCalls = 0;
        var policy = FallbackPolicy.Parse("openai", "test");
        var wrapped = FallbackChain.Wrap(faulty, "azure", "gpt-4o-mini", policy,
            (p, m) => { altCalls++; return AlternateBuildResult.Skipped("never"); },
            null, false);
        var ex = await Assert.ThrowsAsync<HttpRequestException>(
            async () => await wrapped.GetResponseAsync(Hello()));
        Assert.Equal(HttpStatusCode.Unauthorized, ex.StatusCode);
        Assert.Equal(0, altCalls); // no fallback attempts on hard auth failure
    }

    [Fact]
    public async Task Wrap_NonStream_403_NoFallback()
    {
        var faulty = FaultyChatClient.AlwaysThrow(Http(403), Http(403));
        var altCalls = 0;
        var policy = FallbackPolicy.Parse("openai", "test");
        var wrapped = FallbackChain.Wrap(faulty, "azure", "gpt-4o-mini", policy,
            (p, m) => { altCalls++; return AlternateBuildResult.Skipped("never"); }, null, false);
        await Assert.ThrowsAsync<HttpRequestException>(async () => await wrapped.GetResponseAsync(Hello()));
        Assert.Equal(0, altCalls);
    }

    [Fact]
    public async Task Wrap_NonStream_400_NoFallback()
    {
        var faulty = FaultyChatClient.AlwaysThrow(Http(400), Http(400));
        var altCalls = 0;
        var policy = FallbackPolicy.Parse("openai", "test");
        var wrapped = FallbackChain.Wrap(faulty, "azure", "gpt-4o-mini", policy,
            (p, m) => { altCalls++; return AlternateBuildResult.Skipped("never"); }, null, false);
        await Assert.ThrowsAsync<HttpRequestException>(async () => await wrapped.GetResponseAsync(Hello()));
        Assert.Equal(0, altCalls);
    }

    [Fact]
    public async Task Wrap_NonStream_429_TreatedAsTransient_FallbackTried()
    {
        var faulty = FaultyChatClient.AlwaysThrow(Http(429), Http(429));
        var altOk = new FakeChatClient(tokenWord: "alt");
        var policy = FallbackPolicy.Parse("openai", "test");
        var wrapped = FallbackChain.Wrap(faulty, "azure", "gpt-4o-mini", policy,
            (p, m) => AlternateBuildResult.Ready(altOk), null, false);
        var resp = await wrapped.GetResponseAsync(Hello());
        Assert.NotNull(resp);
    }

    [Fact]
    public async Task Wrap_NonStream_CapabilityMismatch_NoFallback()
    {
        var capEx = ProviderCapabilities.Mismatch("azure", "gpt-foo", "tool_calls");
        var faulty = FaultyChatClient.AlwaysThrow(capEx, capEx);
        var altCalls = 0;
        var policy = FallbackPolicy.Parse("openai", "test");
        var wrapped = FallbackChain.Wrap(faulty, "azure", "gpt-foo", policy,
            (p, m) => { altCalls++; return AlternateBuildResult.Skipped("never"); }, null, false);
        await Assert.ThrowsAsync<CapabilityMismatchException>(async () => await wrapped.GetResponseAsync(Hello()));
        Assert.Equal(0, altCalls);
    }

    [Fact]
    public async Task Wrap_NonStream_Timeout_TreatedAsTransient_FallbackTried()
    {
        var faulty = FaultyChatClient.AlwaysThrow(new TaskCanceledException("timeout"), new TaskCanceledException("timeout"));
        var altOk = new FakeChatClient(tokenWord: "alt");
        var policy = FallbackPolicy.Parse("openai", "test");
        var wrapped = FallbackChain.Wrap(faulty, "azure", "gpt-4o-mini", policy,
            (p, m) => AlternateBuildResult.Ready(altOk), null, false);
        var resp = await wrapped.GetResponseAsync(Hello());
        Assert.NotNull(resp);
    }

    [Fact]
    public async Task Wrap_NonStream_FactorySkippedAlternate_ChainContinues()
    {
        using var stderr = new StderrScope();
        var faulty = FaultyChatClient.AlwaysThrow(Http(503), Http(503));
        var altOk = new FakeChatClient();
        var policy = FallbackPolicy.Parse("ollama,openai", "test");
        var wrapped = FallbackChain.Wrap(faulty, "azure", "gpt-4o-mini", policy,
            (preset, m) => preset == "ollama"
                ? AlternateBuildResult.Skipped("blocked by --offline mode")
                : AlternateBuildResult.Ready(altOk),
            line => Console.Error.WriteLine(line), false);
        var resp = await wrapped.GetResponseAsync(Hello());
        Assert.NotNull(resp);
        Assert.Contains("skipping ollama", stderr.Writer.ToString());
        Assert.Contains("blocked by --offline mode", stderr.Writer.ToString());
    }

    [Fact]
    public async Task Wrap_NonStream_AllAlternatesSkipped_OriginalErrorSurfaces()
    {
        var primaryEx = Http(503, "primary");
        var faulty = FaultyChatClient.AlwaysThrow(primaryEx, primaryEx);
        var policy = FallbackPolicy.Parse("ollama,openai", "test");
        var wrapped = FallbackChain.Wrap(faulty, "azure", "gpt-4o-mini", policy,
            (preset, m) => AlternateBuildResult.Skipped("no-fallback-creds"),
            null, false);
        var ex = await Assert.ThrowsAsync<FallbackChainExhaustedException>(
            async () => await wrapped.GetResponseAsync(Hello()));
        Assert.Same(primaryEx, ex.InnerException);
    }

    [Fact]
    public async Task Wrap_NonStream_FactoryThrows_TreatedAsSkip()
    {
        using var stderr = new StderrScope();
        var primaryEx = Http(503, "primary");
        var faulty = FaultyChatClient.AlwaysThrow(primaryEx, primaryEx);
        var altOk = new FakeChatClient();
        var policy = FallbackPolicy.Parse("ollama,openai", "test");
        var wrapped = FallbackChain.Wrap(faulty, "azure", "gpt-4o-mini", policy,
            (preset, m) => preset == "ollama"
                ? throw new InvalidOperationException("factory-bug")
                : AlternateBuildResult.Ready(altOk),
            line => Console.Error.WriteLine(line), false);
        var resp = await wrapped.GetResponseAsync(Hello());
        Assert.NotNull(resp);
        Assert.Contains("factory error", stderr.Writer.ToString());
    }

    [Fact]
    public async Task Wrap_NonStream_RawMode_SuppressesWarnLines()
    {
        using var stderr = new StderrScope();
        var faulty = FaultyChatClient.AlwaysThrow(Http(503), Http(503));
        var altOk = new FakeChatClient();
        var policy = FallbackPolicy.Parse("openai", "test");
        // warnSink == null mirrors the production --raw configuration
        var wrapped = FallbackChain.Wrap(faulty, "azure", "gpt-4o-mini", policy,
            (p, m) => AlternateBuildResult.Ready(altOk), warnSink: null, isRaw: true);
        await wrapped.GetResponseAsync(Hello());
        Assert.DoesNotContain("[fallback]", stderr.Writer.ToString());
    }

    // ── Stream-mode (the critical correctness invariant) ─────────────────

    [Fact]
    public async Task Wrap_Stream_PrimarySuccess_NoFallback()
    {
        var chunks = new List<ChatResponseUpdate> { new(ChatRole.Assistant, "a"), new(ChatRole.Assistant, "b") };
        var primary = new FakeChatClient(streamChunks: chunks);
        var policy = FallbackPolicy.Parse("openai", "test");
        var altCalls = 0;
        var wrapped = FallbackChain.Wrap(primary, "azure", "gpt-4o-mini", policy,
            (p, m) => { altCalls++; return AlternateBuildResult.Skipped("noop"); }, null, false);
        var collected = new List<string>();
        await foreach (var u in wrapped.GetStreamingResponseAsync(Hello()))
        {
            collected.Add(u.Text ?? "");
        }
        Assert.Equal(2, collected.Count);
        Assert.Equal(0, altCalls);
    }

    [Fact]
    public async Task Wrap_Stream_PreFirstChunkFailure_FallbackSucceeds()
    {
        using var stderr = new StderrScope();
        var faulty = FaultyChatClient.AlwaysThrow(Http(503), Http(503));
        var altChunks = new List<ChatResponseUpdate> { new(ChatRole.Assistant, "alt") };
        var alt = new FakeChatClient(streamChunks: altChunks);
        var policy = FallbackPolicy.Parse("openai", "test");
        var wrapped = FallbackChain.Wrap(faulty, "azure", "gpt-4o-mini", policy,
            (p, m) => AlternateBuildResult.Ready(alt), line => Console.Error.WriteLine(line), false);
        var collected = new List<string>();
        await foreach (var u in wrapped.GetStreamingResponseAsync(Hello()))
        {
            collected.Add(u.Text ?? "");
        }
        Assert.Single(collected);
        Assert.Equal("alt", collected[0]);
        Assert.Contains("streaming from openai", stderr.Writer.ToString());
    }

    [Fact]
    public async Task Wrap_Stream_PostFirstChunkFailure_NoFallback_StreamTruncatedWarn()
    {
        using var stderr = new StderrScope();
        // Primary yields 1 chunk then throws 503 — must NOT fall back.
        var faulty = FaultyChatClient.StreamThenThrow(chunksBeforeThrow: 1, Http(503, "bang"));
        var altCalls = 0;
        var policy = FallbackPolicy.Parse("openai", "test");
        var wrapped = FallbackChain.Wrap(faulty, "azure", "gpt-4o-mini", policy,
            (p, m) => { altCalls++; return AlternateBuildResult.Skipped("never"); },
            line => Console.Error.WriteLine(line), false);
        var collected = new List<string>();
        await Assert.ThrowsAsync<HttpRequestException>(async () =>
        {
            await foreach (var u in wrapped.GetStreamingResponseAsync(Hello()))
            {
                collected.Add(u.Text ?? "");
            }
        });
        Assert.Single(collected); // user already saw chunk 1
        Assert.Equal(0, altCalls);
        Assert.Contains("stream-truncated", stderr.Writer.ToString());
    }

    [Fact]
    public async Task Wrap_Stream_PreFirstChunkFailure_AllAlternatesFail_OriginalErrorSurfaces()
    {
        using var stderr = new StderrScope();
        var primaryEx = Http(503, "primary");
        var faulty = FaultyChatClient.AlwaysThrow(primaryEx, primaryEx);
        var altFaulty = FaultyChatClient.AlwaysThrow(Http(502), Http(502));
        var policy = FallbackPolicy.Parse("openai", "test");
        var wrapped = FallbackChain.Wrap(faulty, "azure", "gpt-4o-mini", policy,
            (p, m) => AlternateBuildResult.Ready(altFaulty),
            line => Console.Error.WriteLine(line), false);
        var ex = await Assert.ThrowsAsync<FallbackChainExhaustedException>(async () =>
        {
            await foreach (var _ in wrapped.GetStreamingResponseAsync(Hello())) { }
        });
        Assert.Same(primaryEx, ex.InnerException);
    }

    [Fact]
    public async Task Wrap_Stream_PreFirstChunkAuth_NoFallback()
    {
        var auth = Http(401);
        var faulty = FaultyChatClient.AlwaysThrow(auth, auth);
        var altCalls = 0;
        var policy = FallbackPolicy.Parse("openai", "test");
        var wrapped = FallbackChain.Wrap(faulty, "azure", "gpt-4o-mini", policy,
            (p, m) => { altCalls++; return AlternateBuildResult.Skipped("never"); }, null, false);
        await Assert.ThrowsAsync<HttpRequestException>(async () =>
        {
            await foreach (var _ in wrapped.GetStreamingResponseAsync(Hello())) { }
        });
        Assert.Equal(0, altCalls);
    }

    // ── Telemetry events (additive schema) ───────────────────────────────

    [Fact]
    public async Task Wrap_NonStream_TelemetryEmitsAttemptAndOutcomeEvents()
    {
        using var env = new EnvScope(TelemetryEmitter.EnvVarName, "1");
        using var stderr = new StderrScope();
        var faulty = FaultyChatClient.AlwaysThrow(Http(503), Http(503));
        var altOk = new FakeChatClient();
        var policy = FallbackPolicy.Parse("openai", "test");
        var wrapped = FallbackChain.Wrap(faulty, "azure", "gpt-4o-mini", policy,
            (p, m) => AlternateBuildResult.Ready(altOk), null, true /* raw */);
        await wrapped.GetResponseAsync(Hello());
        var output = stderr.Writer.ToString();
        Assert.Contains("\"event_type\":\"fallback_attempt\"", output);
        Assert.Contains("\"event_type\":\"fallback_outcome\"", output);
        Assert.Contains("\"primary_preset\":\"azure\"", output);
        Assert.Contains("\"source_preset\":\"openai\"", output);
    }

    [Fact]
    public void Telemetry_FallbackAttempt_StableKeyOrder()
    {
        var ev = new TelemetryEmitter.FallbackAttemptEvent(
            EventType: "fallback_attempt",
            EventId: "00000000-0000-0000-0000-000000000001",
            Ts: "2026-05-15T00:00:00.000Z",
            PrimaryPreset: "azure",
            AttemptIndex: 1,
            SourcePreset: "openai",
            Outcome: "success",
            ErrorClass: null);
        var json = TelemetryEmitter.SerializeFallbackAttempt(ev);
        // Stable key order: event_type, event_id, ts, primary_preset,
        // attempt_index, source_preset, outcome, error_class.
        var expected = "{\"event_type\":\"fallback_attempt\","
            + "\"event_id\":\"00000000-0000-0000-0000-000000000001\","
            + "\"ts\":\"2026-05-15T00:00:00.000Z\","
            + "\"primary_preset\":\"azure\","
            + "\"attempt_index\":1,"
            + "\"source_preset\":\"openai\","
            + "\"outcome\":\"success\","
            + "\"error_class\":null}";
        Assert.Equal(expected, json);
    }

    [Fact]
    public void Telemetry_FallbackOutcome_StableKeyOrder()
    {
        var ev = new TelemetryEmitter.FallbackOutcomeEvent(
            EventType: "fallback_outcome",
            EventId: "00000000-0000-0000-0000-000000000002",
            Ts: "2026-05-15T00:00:00.000Z",
            PrimaryPreset: "azure",
            PolicySource: "cli",
            Chain: "openai,groq",
            Outcome: "success",
            Winner: "openai",
            Attempts: 1,
            ErrorClass: null);
        var json = TelemetryEmitter.SerializeFallbackOutcome(ev);
        Assert.StartsWith("{\"event_type\":\"fallback_outcome\"", json);
        Assert.Contains("\"primary_preset\":\"azure\"", json);
        Assert.Contains("\"policy_source\":\"cli\"", json);
        Assert.Contains("\"chain\":\"openai,groq\"", json);
        Assert.Contains("\"winner\":\"openai\"", json);
        Assert.Contains("\"attempts\":1", json);
        Assert.EndsWith("\"error_class\":null}", json);
    }

    [Fact]
    public void Telemetry_FallbackEvents_NoOpWhenDisabled()
    {
        using var env = new EnvScope(TelemetryEmitter.EnvVarName, null);
        using var stderr = new StderrScope();
        TelemetryEmitter.EmitFallbackAttempt("azure", 1, "openai", "begin", null, null);
        TelemetryEmitter.EmitFallbackOutcome(
            "azure", "cli", new[] { "openai" }, "success", "openai", 1, null);
        Assert.Equal(string.Empty, stderr.Writer.ToString());
    }

    [Fact]
    public void Telemetry_FallbackAttempt_RedactsErrorClassValue()
    {
        // SecretRedactor scrubs api-key-shaped fragments. Pass a string the
        // redactor will mutate; assert the emitted line does NOT carry the
        // raw secret form.
        using var env = new EnvScope(TelemetryEmitter.EnvVarName, "1");
        using var stderr = new StderrScope();
        TelemetryEmitter.EmitFallbackAttempt("azure", 1, "openai", "transient_error",
            "boom: api-key=sk-leakedSECRETabcdef0123456789abcdef0123456789abcdef", null);
        var line = stderr.Writer.ToString();
        Assert.DoesNotContain("sk-leakedSECRETabcdef0123456789abcdef0123456789abcdef", line);
    }

    // ── Classification ───────────────────────────────────────────────────

    [Fact]
    public void Classify_5xx_IsTransient()
        => Assert.Equal(FallbackClass.Transient, FallbackChain.Classify(Http(503), CancellationToken.None));

    [Fact]
    public void Classify_429_IsTransient()
        => Assert.Equal(FallbackClass.Transient, FallbackChain.Classify(Http(429), CancellationToken.None));

    [Fact]
    public void Classify_401_IsAuth()
        => Assert.Equal(FallbackClass.Auth, FallbackChain.Classify(Http(401), CancellationToken.None));

    [Fact]
    public void Classify_403_IsAuth()
        => Assert.Equal(FallbackClass.Auth, FallbackChain.Classify(Http(403), CancellationToken.None));

    [Fact]
    public void Classify_400_IsClientError()
        => Assert.Equal(FallbackClass.ClientError, FallbackChain.Classify(Http(400), CancellationToken.None));

    [Fact]
    public void Classify_CapabilityMismatch_IsCapability()
    {
        var ex = ProviderCapabilities.Mismatch("azure", "gpt-foo", "tool_calls");
        Assert.Equal(FallbackClass.Capability, FallbackChain.Classify(ex, CancellationToken.None));
    }

    [Fact]
    public void Classify_Timeout_IsTransient()
        => Assert.Equal(FallbackClass.Transient, FallbackChain.Classify(new TaskCanceledException(), CancellationToken.None));

    [Fact]
    public void Classify_UserCancelled_PropagatesAsCancelled()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var ex = new OperationCanceledException(cts.Token);
        Assert.Equal(FallbackClass.UserCancelled, FallbackChain.Classify(ex, cts.Token));
    }

    [Fact]
    public void ErrorClassLabel_StableShortTokens()
    {
        Assert.Equal("AuthFailure", FallbackChain.ErrorClassLabel(Http(401)));
        Assert.Equal("RateLimited", FallbackChain.ErrorClassLabel(Http(429)));
        Assert.Equal("ClientError", FallbackChain.ErrorClassLabel(Http(400)));
        Assert.Equal("ServerError", FallbackChain.ErrorClassLabel(Http(503)));
        Assert.Equal("Timeout", FallbackChain.ErrorClassLabel(new TaskCanceledException()));
        Assert.Equal(CapabilityMismatchException.ErrorClass,
            FallbackChain.ErrorClassLabel(ProviderCapabilities.Mismatch("azure", "m", "tool_calls")));
    }

    // ── Edge cases ───────────────────────────────────────────────────────

    [Fact]
    public async Task Wrap_NonStream_SkippedAlternateDoesNotCountAsHop()
    {
        // Two alternates: first skipped (offline), second succeeds. The
        // success path still works -- the chain must not abort just because
        // one entry was unavailable.
        var faulty = FaultyChatClient.AlwaysThrow(Http(503), Http(503));
        var altOk = new FakeChatClient();
        var policy = FallbackPolicy.Parse("ollama,openai", "test");
        var wrapped = FallbackChain.Wrap(faulty, "azure", "gpt-4o-mini", policy,
            (preset, m) => preset == "ollama"
                ? AlternateBuildResult.Skipped("no-creds")
                : AlternateBuildResult.Ready(altOk),
            null, false);
        var resp = await wrapped.GetResponseAsync(Hello());
        Assert.NotNull(resp);
    }

    [Fact]
    public async Task Wrap_NonStream_ProductionFactoryAlwaysSkips_SurfacesPrimaryError()
    {
        // Mirrors the v2.x production wiring (no fallback creds plumbed).
        var primaryEx = Http(503, "primary");
        var faulty = FaultyChatClient.AlwaysThrow(primaryEx, primaryEx);
        var policy = FallbackPolicy.Parse("ollama,openai", "test");
        AlternateChatClientFactory factory =
            (preset, m) => AlternateBuildResult.Skipped("no-fallback-creds (frank-2026-05-FB-1)");
        var wrapped = FallbackChain.Wrap(faulty, "azure", "gpt-4o-mini", policy, factory, null, false);
        var ex = await Assert.ThrowsAsync<FallbackChainExhaustedException>(
            async () => await wrapped.GetResponseAsync(Hello()));
        Assert.Same(primaryEx, ex.InnerException);
    }

    [Fact]
    public void FallbackChainExhaustedException_CarriesPrimaryAsInner()
    {
        var inner = new InvalidOperationException("primary");
        var ex = new FallbackChainExhaustedException(2, inner);
        Assert.Same(inner, ex.InnerException);
        Assert.Equal(2, ex.ChainDepth);
        Assert.Equal("FallbackChainExhausted", FallbackChainExhaustedException.ErrorClass);
    }
}
