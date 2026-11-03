using Microsoft.Extensions.AI;

namespace AzureOpenAI_CLI.Tests;

/// <summary>
/// S04E21 -- a reasoning model with a small <c>--max-tokens</c> spends the whole budget thinking and returns no text
/// (finish reason "length"). Hopper printed nothing and exited 0, which a text expander or a pipeline reads as success.
/// </summary>
public class EmptyReplyTests
{
    [Fact]
    public void OutputLimitWithNoText_IsAnError_NamingTheFlagAndTheValue()
    {
        var msg = Program.DescribeEmptyReply(anyText: false, ChatFinishReason.Length, maxTokens: 16);

        Assert.NotNull(msg);
        Assert.Contains("--max-tokens 16", msg);
        Assert.Contains("Raise --max-tokens", msg);
    }

    [Fact]
    public void OutputLimitWithNoText_AndNoExplicitLimit_StillErrors_WithoutInventingANumber()
    {
        var msg = Program.DescribeEmptyReply(anyText: false, ChatFinishReason.Length, maxTokens: null);

        Assert.NotNull(msg);
        Assert.Contains("the model's output limit", msg);
        Assert.DoesNotContain("--max-tokens null", msg);
        Assert.DoesNotContain("(--max-tokens", msg);
    }

    [Fact]
    public void ContentFilterWithNoText_IsAnError_SayingSo()
    {
        var msg = Program.DescribeEmptyReply(anyText: false, ChatFinishReason.ContentFilter, maxTokens: 500);

        Assert.NotNull(msg);
        Assert.Contains("content filter", msg);
        Assert.DoesNotContain("--max-tokens", msg); // raising the limit would not help, so it must not be suggested
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Stop")]
    [InlineData("ToolCalls")]
    public void AModelThatStoppedNormallyWithNothingToSay_IsNotAnError(string? reason)
    {
        ChatFinishReason? fr = reason is null ? null : new ChatFinishReason(reason == "Stop" ? "stop" : "tool_calls");

        Assert.Null(Program.DescribeEmptyReply(anyText: false, fr, maxTokens: 16));
    }

    [Theory]
    [InlineData("length")]
    [InlineData("content_filter")]
    [InlineData("stop")]
    public void AnyText_IsNeverAnError_WhateverTheFinishReason(string reason)
    {
        // Truncated output is a different problem (and an open one); this check is only about an empty reply.
        Assert.Null(Program.DescribeEmptyReply(anyText: true, new ChatFinishReason(reason), maxTokens: 16));
    }

    [Fact]
    public void TheLimitReason_IsMatchedByValue_NotByReference()
    {
        // The SDK builds its own ChatFinishReason instance from the wire string "length".
        var fromWire = new ChatFinishReason("length");

        Assert.NotNull(Program.DescribeEmptyReply(anyText: false, fromWire, maxTokens: 16));
    }
}
