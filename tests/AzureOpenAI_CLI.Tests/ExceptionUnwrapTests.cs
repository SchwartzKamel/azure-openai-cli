namespace AzureOpenAI_CLI.V2.Tests;

/// <summary>
/// FDR v2 dogfood High-severity (fdr-v2-err-unwrap) tests:
/// Program.UnwrapException + Program.UnsafeReplaceSecrets.
/// Users hit TypeInitializationException under AOT when a static ctor blew
/// up; they deserve actionable errors, not "type initializer threw" noise.
/// Both pass-the-pass AND fail-the-fail paths covered.
/// </summary>
public class ExceptionUnwrapTests
{
    [Fact]
    public void UnwrapException_SingleLevel_ReturnsMessage()
    {
        var ex = new InvalidOperationException("boom");

        var result = Program.UnwrapException(ex);

        Assert.Equal("boom", result);
    }

    [Fact]
    public void UnwrapException_NestedInner_JoinsWithArrow()
    {
        var inner2 = new InvalidOperationException("root cause");
        var inner1 = new InvalidOperationException("middle", inner2);
        var outer = new InvalidOperationException("outer", inner1);

        var result = Program.UnwrapException(outer);

        Assert.Contains("outer", result);
        Assert.Contains("middle", result);
        Assert.Contains("root cause", result);
        Assert.Contains(" → ", result);
        // Order preserved: outer first, root last.
        Assert.True(result.IndexOf("outer") < result.IndexOf("middle"));
        Assert.True(result.IndexOf("middle") < result.IndexOf("root cause"));
    }

    [Fact]
    public void UnwrapException_TypeInitializer_IncludesTypeName()
    {
        // Synthesize a TypeInitializationException — real AOT ones name
        // Azure.AI.OpenAI.AzureChatClient; we use a benign type here.
        var tie = new TypeInitializationException(
            "MyNamespace.FakeType",
            new InvalidOperationException("static ctor failed"));

        var result = Program.UnwrapException(tie);

        Assert.Contains("MyNamespace.FakeType", result);
        Assert.Contains("static ctor failed", result);
    }

    [Fact]
    public void UnwrapException_RedactsApiKeyAndEndpoint()
    {
        const string apiKey = "sk-super-secret-1234567890abcdef";
        const string endpoint = "https://contoso-prod.openai.azure.com/";
        var msg = $"Auth failed for key={apiKey} against {endpoint}";

        var redacted = Program.UnsafeReplaceSecrets(msg, apiKey, endpoint);

        Assert.DoesNotContain(apiKey, redacted);
        Assert.DoesNotContain("contoso-prod", redacted);
        Assert.Contains("[REDACTED]", redacted);
    }

    [Fact]
    public void UnsafeReplaceSecrets_NullInputs_DoesNotCrash()
    {
        // Negative: helper must tolerate missing secrets (common when the
        // error fires before env resolution has completed).
        var r1 = Program.UnsafeReplaceSecrets("plain text", null, null);
        Assert.Equal("plain text", r1);

        var r2 = Program.UnsafeReplaceSecrets("plain text", "", "");
        Assert.Equal("plain text", r2);
    }

    [Fact]
    public void UnwrapException_ChainStopsAtFiveLevels()
    {
        // Build a 10-deep chain; the unwrap helper must cap at 5 (+1 outer)
        // and not stack-overflow even on pathological inputs.
        Exception deepest = new InvalidOperationException("level-0");
        for (int i = 1; i <= 10; i++)
        {
            deepest = new InvalidOperationException($"level-{i}", deepest);
        }

        var result = Program.UnwrapException(deepest);

        // Outer is level-10; we expect to see level-10..level-5 at most.
        Assert.Contains("level-10", result);
        // Levels below 5 must NOT surface — the cap is real.
        Assert.DoesNotContain("level-0", result);
        Assert.DoesNotContain("level-1 ", result); // trailing space avoids matching "level-10"
    }

    [Fact]
    public void UnwrapException_CycleSafe_DoesNotStackOverflow()
    {
        // Guard: even if the InnerException chain somehow self-referenced,
        // the unwrap helper's seen-set must break the cycle.
        var ex = new InvalidOperationException("only message");

        // Can't construct a real cycle via InnerException (it's set-once), but
        // we can at least verify the single-entry case terminates cleanly.
        var result = Program.UnwrapException(ex, maxDepth: 5);
        Assert.Equal("only message", result);
    }
}
