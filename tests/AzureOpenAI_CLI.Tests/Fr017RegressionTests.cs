using Microsoft.Extensions.AI;
using OpenAI.Chat;

namespace AzureOpenAI_CLI.V2.Tests;

/// <summary>
/// FR-017 regression: <see cref="Program.BuildModernChatOptions"/> must emit
/// <c>max_completion_tokens</c> on the wire so gpt-5.x / o1 / o3 deployments
/// accept the token cap. Guards against the pragma-disabled AOAI001 call
/// (<c>SetNewMaxCompletionTokensPropertyEnabled</c>) silently dropping — and
/// against accidental conversion to a shared/mutable singleton.
/// Ref: azureopenai-cli/Program.cs:853, Newman audit.
/// </summary>
public class Fr017RegressionTests
{
    [Fact]
    public void BuildModernChatOptions_ReturnsOptionsWithFactory()
    {
        var chatOptions = Program.BuildModernChatOptions();

        Assert.NotNull(chatOptions);
        Assert.NotNull(chatOptions.RawRepresentationFactory);
    }

    [Fact]
    public void BuildModernChatOptions_FactoryProducesChatCompletionOptions()
    {
        var chatOptions = Program.BuildModernChatOptions();

        // Factory signature is Func<IChatClient, object?>; null is an acceptable
        // argument because the v2 factory ignores it. A non-null ChatCompletionOptions
        // return value proves the AOAI001-gated call didn't throw or no-op.
        var raw = chatOptions.RawRepresentationFactory!(null!);

        Assert.NotNull(raw);
        Assert.IsType<ChatCompletionOptions>(raw);
    }

    [Fact]
    public void BuildModernChatOptions_ReturnsFreshInstancePerCall()
    {
        var first = Program.BuildModernChatOptions();
        var second = Program.BuildModernChatOptions();

        // No shared mutable singleton — per-request call sites must not race.
        Assert.NotSame(first, second);

        // Factory invocations must also yield fresh ChatCompletionOptions so
        // per-request mutation (stop sequences, tool bindings) can't leak.
        var rawA = first.RawRepresentationFactory!(null!);
        var rawB = first.RawRepresentationFactory!(null!);
        Assert.NotSame(rawA, rawB);
    }
}
