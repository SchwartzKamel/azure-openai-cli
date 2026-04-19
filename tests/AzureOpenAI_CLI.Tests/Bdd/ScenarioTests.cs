// ScenarioTests.cs — self-tests for the Scenario BDD DSL.
//
// Every test pairs a positive assertion (the DSL works when everything
// is green) with a negative assertion (the DSL produces the expected
// narrative and rethrows when something fails). Pass the pass, fail
// the fail.

using AzureOpenAI_CLI.Tests.Bdd;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace AzureOpenAI_CLI.Tests;

[Trait("type", "behavior")]
public class ScenarioDslTests
{
    // ── Given / When / Then happy path ─────────────────────────────

    [Fact]
    public void Given_When_Then_HappyPath_Succeeds()
    {
        var probe = Scenario
            .Given("a two-element list", () => new List<int> { 1, 2 })
            .When("we count it", l => l.Count)
            .Then("the count is 2", n => Assert.Equal(2, n));

        // Sanity: the chain returned the final context (pass-the-pass)
        Assert.NotNull(probe);
    }

    [Fact]
    public void Given_When_Then_FailureMessage_IncludesFullNarrative()
    {
        var ex = Assert.Throws<XunitException>(() =>
            Scenario
                .Given("a two-element list", () => new List<int> { 1, 2 })
                .When("we count it", l => l.Count)
                .Then("the count is 99", n => Assert.Equal(99, n)));

        // Positive: narrative present
        Assert.Contains("Given a two-element list", ex.Message);
        Assert.Contains("When  we count it", ex.Message);
        Assert.Contains("Then  the count is 99", ex.Message);

        // Negative: must NOT have silently swallowed — inner preserved
        Assert.NotNull(ex.InnerException);
    }

    // ── And chains ────────────────────────────────────────────────

    [Fact]
    public void And_ChainsAfterThen_AllAssertionsRun()
    {
        Scenario
            .Given("a positive number", () => 7)
            .When("we identity it", n => n)
            .Then("it is odd", n => Assert.True(n % 2 == 1))
            .And("it is less than 10", n => Assert.True(n < 10))
            .And("it is greater than zero", n => Assert.True(n > 0));
    }

    [Fact]
    public void And_FailsWhen_IncludesAndKeywordInNarrative()
    {
        var ex = Assert.Throws<XunitException>(() =>
            Scenario
                .Given("seven", () => 7)
                .When("we identity it", n => n)
                .Then("odd", n => Assert.True(n % 2 == 1))
                .And("greater than 100", n => Assert.True(n > 100)));

        Assert.Contains("And", ex.Message);
        Assert.Contains("greater than 100", ex.Message);
    }

    // ── Async Given/When/Then ─────────────────────────────────────

    [Fact]
    public async Task GivenAsync_WhenAsync_ThenAsync_HappyPath()
    {
        var ctx = await Scenario
            .GivenAsync("an async integer", () => Task.FromResult(42));

        var whenCtx = await ctx.WhenAsync(
            "we double it", async n => { await Task.Yield(); return n * 2; });

        await whenCtx.ThenAsync(
            "the result is 84",
            async n => { await Task.Yield(); Assert.Equal(84, n); });
    }

    [Fact]
    public async Task GivenAsync_FailureInArrange_IsWrappedWithGivenNarrative()
    {
        var ex = await Assert.ThrowsAsync<XunitException>(() =>
            Scenario.GivenAsync<int>("an arrange that blows up", () =>
                Task.FromException<int>(new InvalidOperationException("kaboom"))));

        Assert.Contains("Given an arrange that blows up", ex.Message);
        Assert.Contains("kaboom", ex.Message);
    }

    // ── Exception capture: WhenAttempting ─────────────────────────

    [Fact]
    public void WhenAttempting_CapturesThrownException()
    {
        Scenario
            .Given("a divisor of zero", () => 0)
            .WhenAttempting("dividing by it",
                d => { var _ = 10 / d; })
            .Then("a divide-by-zero exception was thrown",
                ex => Assert.IsType<DivideByZeroException>(ex));
    }

    [Fact]
    public void WhenAttempting_NoException_ResultIsNull()
    {
        Scenario
            .Given("a safe operation", () => 0)
            .WhenAttempting("doing nothing risky", _ => { /* no throw */ })
            .Then("no exception was captured", Assert.Null);
    }

    // ── Exception capture: WhenThrowing<T> ────────────────────────

    [Fact]
    public void WhenThrowing_Typed_CapturesExpectedException()
    {
        Scenario
            .Given("null input", () => (string?)null)
            .WhenThrowing<ArgumentNullException>(
                "calling ArgumentNullException.ThrowIfNull",
                s => ArgumentNullException.ThrowIfNull(s))
            .Then("the typed exception was captured",
                ex => Assert.NotNull(ex));
    }

    [Fact]
    public void WhenThrowing_Typed_RethrowsOnWrongException()
    {
        // If the act throws a different exception type than declared,
        // the scenario must fail LOUDLY rather than silently returning null.
        // This is the rubber-duck finding that distinguishes WhenThrowing
        // from WhenAttempting.
        var ex = Assert.Throws<XunitException>(() =>
            Scenario
                .Given("a value", () => 0)
                .WhenThrowing<ArgumentNullException>(
                    "doing a division that throws DivideByZero",
                    v => { var _ = 1 / v; })
                .Then("unreachable", _ => Assert.Fail("should not reach Then")));

        // Positive: message clearly states the mismatch
        Assert.Contains("Expected ArgumentNullException", ex.Message);
        Assert.Contains("DivideByZeroException", ex.Message);

        // Negative: the expected-type filter did NOT swallow the wrong exception
        Assert.NotNull(ex.InnerException);
    }

    [Fact]
    public void WhenThrowing_NoExceptionRaised_ResultIsNull()
    {
        Scenario
            .Given("a non-null string", () => "hello")
            .WhenThrowing<ArgumentNullException>(
                "calling ThrowIfNull on a non-null reference",
                s => ArgumentNullException.ThrowIfNull(s))
            .Then("no exception was captured — result is null", Assert.Null);
    }

    // ── Async exception capture ───────────────────────────────────

    [Fact]
    public async Task WhenAttemptingAsync_CapturesAsyncException()
    {
        var ctx = await Scenario
            .Given("an awaited failure", () => "x")
            .WhenAttemptingAsync("awaiting a faulted task",
                _ => Task.FromException(new InvalidOperationException("async boom")));

        ctx.Then("an exception was captured", Assert.NotNull)
           .And("the message is preserved",
               ex => Assert.Contains("async boom", ex!.Message));
    }

    [Fact]
    public async Task WhenThrowingAsync_WrongType_RethrowsLoudly()
    {
        var ex = await Assert.ThrowsAsync<XunitException>(async () =>
            await (await Scenario
                .Given("an async op", () => 0)
                .WhenThrowingAsync<ArgumentNullException>(
                    "throwing the wrong async exception",
                    _ => Task.FromException(new InvalidOperationException("wrong"))))
                .ThenAsync("unreachable", _ => Task.CompletedTask));

        Assert.Contains("Expected ArgumentNullException", ex.Message);
        Assert.Contains("InvalidOperationException", ex.Message);
    }

    // ── ITestOutputHelper narration on PASS ───────────────────────

    [Fact]
    public void Given_OutputHelper_NarrationIsWrittenOnPass()
    {
        var recorder = new RecordingOutputHelper();

        Scenario
            .Given("a recording output", () => recorder, recorder)
            .When("we add 2 + 2", _ => 2 + 2)
            .Then("the result is 4", r => Assert.Equal(4, r))
            .And("and it is even", r => Assert.True(r % 2 == 0));

        // Positive: narrative emitted for each step
        Assert.Contains("Given a recording output", recorder.Text);
        Assert.Contains("When  we add 2 + 2", recorder.Text);
        Assert.Contains("Then  the result is 4", recorder.Text);
        Assert.Contains("And   and it is even", recorder.Text);
        Assert.Contains("✔", recorder.Text);

        // Negative: the unused scenario prose must NOT leak
        Assert.DoesNotContain("unrelated phrase", recorder.Text);
    }

    // ── Argument validation ───────────────────────────────────────

    [Fact]
    public void Given_NullArrange_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            Scenario.Given<int>("desc", arrange: null!));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Given_NullOrEmptyDescription_Throws(string? desc)
    {
        Assert.ThrowsAny<ArgumentException>(() =>
            Scenario.Given(desc!, () => 1));
    }

    // ── Fluent chain returns the same context type ────────────────

    [Fact]
    public void Then_ReturnsContext_EnablesAndChain()
    {
        var ctx = Scenario
            .Given("one", () => 1)
            .When("identity", n => n)
            .Then("is 1", n => Assert.Equal(1, n));

        // Positive: Then returned a usable WhenContext<int> for And chaining
        Assert.NotNull(ctx);

        // Use it — must not throw
        ctx.And("still 1", n => Assert.Equal(1, n));
    }

    private sealed class RecordingOutputHelper : ITestOutputHelper
    {
        private readonly System.Text.StringBuilder _sb = new();
        public string Text => _sb.ToString();
        public void WriteLine(string message) => _sb.AppendLine(message);
        public void WriteLine(string format, params object[] args)
            => _sb.AppendLine(string.Format(format, args));
    }
}
