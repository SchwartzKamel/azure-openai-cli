using System.ClientModel;
using AzureOpenAI_CLI.Resilience;
using Microsoft.Extensions.AI;

namespace AzureOpenAI_CLI.Tests;

/// <summary>
/// S04E20 -- some deployments reject any temperature but the default (HTTP 400 unsupported_value). Hopper always sends
/// one, so two of the three Azure deployments tried in S04SP11 failed on every call. These tests pin the retry, and,
/// as important, what must NOT be retried: only that exact error, only once, only when there was something to drop.
/// </summary>
[Collection("TemperatureFallback")] // the "already known to reject" memory is process-wide, so these run one at a time
public class TemperatureTolerantChatClientTests : IDisposable
{
    // The message the OpenAI SDK produces for the real error.
    private const string RejectsTemperature =
        "HTTP 400 (invalid_request_error: unsupported_value)\n\nParameter: temperature\n\n" +
        "Unsupported value: 'temperature' does not support 0.55 with this model. Only the default (1) value is supported.";

    public TemperatureTolerantChatClientTests() => TemperatureTolerantChatClient.ForgetAll();
    public void Dispose() => TemperatureTolerantChatClient.ForgetAll();

    private sealed class RecordingClient : IChatClient
    {
        private readonly Func<float?, Exception?> _failure;
        public List<float?> Temperatures { get; } = new();
        public List<ChatOptions?> Seen { get; } = new();

        public RecordingClient(Func<float?, Exception?> failure) => _failure = failure;

        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            Temperatures.Add(options?.Temperature);
            Seen.Add(options);
            var failure = _failure(options?.Temperature);
            if (failure is not null) throw failure;
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "pong")));
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            Temperatures.Add(options?.Temperature);
            Seen.Add(options);
            await Task.Yield();
            var failure = _failure(options?.Temperature);
            if (failure is not null) throw failure;
            yield return new ChatResponseUpdate(ChatRole.Assistant, "po");
            yield return new ChatResponseUpdate(ChatRole.Assistant, "ng");
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }

    private static Exception? RejectAnyTemperature(float? t) => t is null ? null : new ClientResultException(RejectsTemperature);

    private static ChatOptions WithTemperature(float t) => new() { Temperature = t, MaxOutputTokens = 50 };

    private static readonly IEnumerable<ChatMessage> Hello = new[] { new ChatMessage(ChatRole.User, "ping") };

    // ── The retry ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task ModelThatRejectsTemperature_IsRetriedOnceWithout_AndAnswers()
    {
        var inner = new RecordingClient(RejectAnyTemperature);
        var notes = new List<string>();
        var client = new TemperatureTolerantChatClient(inner, "m-reasoning", notes.Add);

        var response = await client.GetResponseAsync(Hello, WithTemperature(0.55f));

        Assert.Equal("pong", response.Text);
        Assert.Equal(new float?[] { 0.55f, null }, inner.Temperatures);
        Assert.Single(notes);
        Assert.Contains("m-reasoning", notes[0]);
        Assert.Contains("retrying without it", notes[0]);
    }

    [Fact]
    public async Task TheRetryKeepsTheOtherOptions()
    {
        var inner = new RecordingClient(RejectAnyTemperature);
        var client = new TemperatureTolerantChatClient(inner, "m-keep", _ => { });

        await client.GetResponseAsync(Hello, WithTemperature(0.55f));

        Assert.Equal(50, inner.Seen[1]!.MaxOutputTokens);
    }

    [Fact]
    public async Task TheCallersOptionsAreNotMutated()
    {
        var inner = new RecordingClient(RejectAnyTemperature);
        var client = new TemperatureTolerantChatClient(inner, "m-immutable", _ => { });
        var options = WithTemperature(0.55f);

        await client.GetResponseAsync(Hello, options);

        Assert.Equal(0.55f, options.Temperature); // the next agent round reuses these options
    }

    [Fact]
    public async Task AModelThatRejectedOnce_IsNotAskedAgain_AndTheNoteIsPrintedOnce()
    {
        var inner = new RecordingClient(RejectAnyTemperature);
        var notes = new List<string>();
        var client = new TemperatureTolerantChatClient(inner, "m-remembered", notes.Add);

        await client.GetResponseAsync(Hello, WithTemperature(0.55f));
        await client.GetResponseAsync(Hello, WithTemperature(0.55f));
        await client.GetResponseAsync(Hello, WithTemperature(0.55f));

        Assert.Equal(new float?[] { 0.55f, null, null, null }, inner.Temperatures); // one failed round trip, then none
        Assert.Single(notes);
    }

    // -- S04E24: the memory survives the process when the CLI gives it a file ---------

    private static string TempMemoryPath() =>
        Path.Combine(Path.GetTempPath(), "hopper-temp-memory-" + Guid.NewGuid().ToString("N"), "temperature-rejecting-models");

    [Fact]
    public async Task WithAMemoryFile_TheNextProcessSkipsTheFailedRoundTrip()
    {
        var path = TempMemoryPath();
        try
        {
            var first = new RecordingClient(RejectAnyTemperature);
            await new TemperatureTolerantChatClient(first, "gpt-6-luna", _ => { }, path).GetResponseAsync(Hello, WithTemperature(0.55f));
            Assert.Equal(new[] { "gpt-6-luna" }, File.ReadAllLines(path));

            TemperatureTolerantChatClient.ForgetAll(); // what a fresh process starts with

            var notes = new List<string>();
            var second = new RecordingClient(RejectAnyTemperature);
            await new TemperatureTolerantChatClient(second, "gpt-6-luna", notes.Add, path).GetResponseAsync(Hello, WithTemperature(0.55f));
            Assert.Equal(new float?[] { null }, second.Temperatures); // no failed round trip at all
            Assert.Empty(notes);
        }
        finally { Directory.Delete(Path.GetDirectoryName(path)!, recursive: true); }
    }

    [Fact]
    public async Task WithoutAMemoryFile_NothingIsWritten_AndANewProcessRelearns()
    {
        var first = new RecordingClient(RejectAnyTemperature);
        await new TemperatureTolerantChatClient(first, "m-no-file", _ => { }).GetResponseAsync(Hello, WithTemperature(0.55f));
        TemperatureTolerantChatClient.ForgetAll();

        var second = new RecordingClient(RejectAnyTemperature);
        await new TemperatureTolerantChatClient(second, "m-no-file", _ => { }).GetResponseAsync(Hello, WithTemperature(0.55f));
        Assert.Equal(new float?[] { 0.55f, null }, second.Temperatures);
    }

    [Fact]
    public async Task TheMemoryFile_IgnoresUnsafeOrJunkLines()
    {
        var path = TempMemoryPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        try
        {
            File.WriteAllText(path, "good-model\n\nbad model with spaces\n" + new string('x', 200) + "\n");
            var good = new RecordingClient(_ => null);
            await new TemperatureTolerantChatClient(good, "good-model", _ => { }, path).GetResponseAsync(Hello, WithTemperature(0.4f));
            Assert.Equal(new float?[] { null }, good.Temperatures); // listed, so no temperature is sent
            var other = new RecordingClient(_ => null);
            await new TemperatureTolerantChatClient(other, "bad model with spaces", _ => { }, path).GetResponseAsync(Hello, WithTemperature(0.4f));
            Assert.Equal(new float?[] { 0.4f }, other.Temperatures);
        }
        finally { Directory.Delete(Path.GetDirectoryName(path)!, recursive: true); }
    }

    [Theory]
    [InlineData("gpt-6-luna", true)]
    [InlineData("org/model:v1@latest", true)]
    [InlineData("", false)]
    [InlineData("has space", false)]
    [InlineData("new\nline", false)]
    public void IsPersistableModel_AcceptsOnlyPlainIdentifiers(string model, bool expected)
    {
        Assert.Equal(expected, TemperatureTolerantChatClient.IsPersistableModel(model));
    }

    [Fact]
    public async Task TheMemoryIsPerModel()
    {
        var first = new RecordingClient(RejectAnyTemperature);
        await new TemperatureTolerantChatClient(first, "m-a", _ => { }).GetResponseAsync(Hello, WithTemperature(0.55f));

        var other = new RecordingClient(_ => null); // a model that accepts temperatures
        await new TemperatureTolerantChatClient(other, "m-b", _ => { }).GetResponseAsync(Hello, WithTemperature(0.3f));

        Assert.Equal(new float?[] { 0.3f }, other.Temperatures); // m-b still receives its temperature
    }

    [Fact]
    public async Task AModelThatAcceptsTheTemperature_GetsExactlyWhatWasAsked()
    {
        var inner = new RecordingClient(_ => null);
        var client = new TemperatureTolerantChatClient(inner, "m-fine", _ => { });

        await client.GetResponseAsync(Hello, WithTemperature(0.2f));

        Assert.Equal(new float?[] { 0.2f }, inner.Temperatures);
    }

    // ── What must NOT be retried ────────────────────────────────────────────────

    [Fact]
    public async Task ADifferentBadRequest_StillSurfacesAsAnError()
    {
        var inner = new RecordingClient(_ => new ClientResultException(
            "HTTP 400 (invalid_request_error: unsupported_value)\n\nParameter: max_completion_tokens\n\nUnsupported value."));
        var client = new TemperatureTolerantChatClient(inner, "m-other", _ => { });

        await Assert.ThrowsAsync<ClientResultException>(() => client.GetResponseAsync(Hello, WithTemperature(0.55f)));

        Assert.Single(inner.Temperatures); // not retried
    }

    [Fact]
    public async Task AnErrorThatIsNotAnSdkRequestError_IsNotRetried()
    {
        var inner = new RecordingClient(t => t is null ? null : new InvalidOperationException(RejectsTemperature));
        var client = new TemperatureTolerantChatClient(inner, "m-not-sdk", _ => { });

        await Assert.ThrowsAsync<InvalidOperationException>(() => client.GetResponseAsync(Hello, WithTemperature(0.55f)));

        Assert.Single(inner.Temperatures);
    }

    [Fact]
    public async Task WithNoTemperatureToDrop_NothingIsRetried()
    {
        var inner = new RecordingClient(_ => new ClientResultException(RejectsTemperature));
        var client = new TemperatureTolerantChatClient(inner, "m-none", _ => { });

        await Assert.ThrowsAsync<ClientResultException>(() => client.GetResponseAsync(Hello, new ChatOptions { MaxOutputTokens = 50 }));

        Assert.Single(inner.Temperatures);
    }

    [Fact]
    public async Task IfTheRetryAlsoFails_ThatFailureIsReported_NotRetriedAgain()
    {
        var calls = 0;
        var inner = new RecordingClient(t => ++calls == 1 ? new ClientResultException(RejectsTemperature) : new ClientResultException("HTTP 401 (invalid_api_key)"));
        var client = new TemperatureTolerantChatClient(inner, "m-second-fail", _ => { });

        var ex = await Assert.ThrowsAsync<ClientResultException>(() => client.GetResponseAsync(Hello, WithTemperature(0.55f)));

        Assert.Contains("401", ex.Message);
        Assert.Equal(2, inner.Temperatures.Count);
    }

    // ── Streaming ───────────────────────────────────────────────────────────────

    private static async Task<string> Drain(IAsyncEnumerable<ChatResponseUpdate> updates)
    {
        var text = "";
        await foreach (var u in updates) text += u.Text;
        return text;
    }

    [Fact]
    public async Task Streaming_ARejectionOnTheFirstRead_IsRetriedWithout()
    {
        var inner = new RecordingClient(RejectAnyTemperature);
        var client = new TemperatureTolerantChatClient(inner, "m-stream", _ => { });

        var text = await Drain(client.GetStreamingResponseAsync(Hello, WithTemperature(0.55f)));

        Assert.Equal("pong", text);
        Assert.Equal(new float?[] { 0.55f, null }, inner.Temperatures);
    }

    [Fact]
    public async Task Streaming_AFailureAfterOutputHasStarted_IsNotRetried()
    {
        // Retrying after "po" was yielded would print it twice.
        var inner = new MidStreamFailure();
        var client = new TemperatureTolerantChatClient(inner, "m-mid", _ => { });
        var seen = "";

        await Assert.ThrowsAsync<ClientResultException>(async () =>
        {
            await foreach (var u in client.GetStreamingResponseAsync(Hello, WithTemperature(0.55f))) seen += u.Text;
        });

        Assert.Equal("po", seen);
        Assert.Equal(1, inner.Calls);
    }

    private sealed class MidStreamFailure : IChatClient
    {
        public int Calls;
        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            Calls++;
            await Task.Yield();
            yield return new ChatResponseUpdate(ChatRole.Assistant, "po");
            throw new ClientResultException(RejectsTemperature); // even this exact message must not be retried now
        }
        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }
}

/// <summary>The decorator is only useful if the product builds its clients through it.</summary>
[Collection("ConsoleCapture")] // sets AZURE_FOUNDRY_ENDPOINT, so it joins the serial collection of env-mutating tests
public class TemperatureFallbackWiringTests
{
    [Fact]
    public void BuildChatClient_ReturnsTheTolerantWrapper()
    {
        var saved = Environment.GetEnvironmentVariable("AZURE_FOUNDRY_ENDPOINT");
        try
        {
            Environment.SetEnvironmentVariable("AZURE_FOUNDRY_ENDPOINT", null);
            var client = Program.BuildChatClient("https://test.openai.azure.com/", "k", "gpt-4o", jsonMode: false);

            Assert.IsType<TemperatureTolerantChatClient>(client);
        }
        finally { Environment.SetEnvironmentVariable("AZURE_FOUNDRY_ENDPOINT", saved); }
    }
}
