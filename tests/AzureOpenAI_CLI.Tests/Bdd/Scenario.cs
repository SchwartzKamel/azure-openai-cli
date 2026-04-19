// Scenario.cs — BDD DSL for xUnit. Test-code only. See ADR-003.
//
// Usage:
//
//   Scenario
//       .Given("a fresh tool registry with no filter", () => ToolRegistry.Create(null))
//       .When("we read the tool count",                r => r.All.Count)
//       .Then("all six built-in tools are registered", n => Assert.Equal(6, n));
//
// On assertion failure, the thrown exception's message is wrapped with
// the full narrative:
//
//     Scenario failed:
//       Given a fresh tool registry with no filter
//       When  we read the tool count
//       Then  all six built-in tools are registered
//
//     Expected: 6
//     Actual:   5
//
// Async overloads (GivenAsync / WhenAsync) mirror the sync shape.
//
// WhenAttempting catches *any* exception; WhenThrowing<T> catches only
// the declared type and lets unexpected exceptions propagate so the
// scenario fails loudly (rubber-duck finding, ADR-003).
//
// Optional ITestOutputHelper writes the narrative on PASS too, so CI
// logs carry the scenario description without requiring a failure.

using System.Runtime.ExceptionServices;

namespace AzureOpenAI_CLI.Tests.Bdd;

/// <summary>
/// Entry point for the BDD DSL. See ADR-003.
/// </summary>
public static class Scenario
{
    public static GivenContext<T> Given<T>(
        string description,
        Func<T> arrange,
        Xunit.Abstractions.ITestOutputHelper? output = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(description);
        ArgumentNullException.ThrowIfNull(arrange);

        T value;
        try
        {
            value = arrange();
        }
        catch (Exception ex)
        {
            throw WrapGivenFailure(description, ex);
        }
        return new GivenContext<T>(description, value, output);
    }

    public static async Task<GivenContext<T>> GivenAsync<T>(
        string description,
        Func<Task<T>> arrange,
        Xunit.Abstractions.ITestOutputHelper? output = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(description);
        ArgumentNullException.ThrowIfNull(arrange);

        T value;
        try
        {
            value = await arrange().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            throw WrapGivenFailure(description, ex);
        }
        return new GivenContext<T>(description, value, output);
    }

    internal static Exception WrapGivenFailure(string given, Exception inner)
        => new Xunit.Sdk.XunitException(
            $"Scenario failed in Given step:{Environment.NewLine}" +
            $"  Given {given}{Environment.NewLine}{Environment.NewLine}" +
            inner.Message,
            inner);
}

/// <summary>
/// Arranged state, ready to be acted on.
/// </summary>
public sealed class GivenContext<T>
{
    private readonly string _given;
    private readonly T _value;
    private readonly Xunit.Abstractions.ITestOutputHelper? _output;

    internal GivenContext(string given, T value, Xunit.Abstractions.ITestOutputHelper? output)
    {
        _given = given;
        _value = value;
        _output = output;
    }

    public WhenContext<TResult> When<TResult>(string description, Func<T, TResult> act)
    {
        ArgumentException.ThrowIfNullOrEmpty(description);
        ArgumentNullException.ThrowIfNull(act);

        TResult result;
        try
        {
            result = act(_value);
        }
        catch (Exception ex)
        {
            throw WrapActFailure(description, ex);
        }
        return new WhenContext<TResult>(_given, description, result, _output);
    }

    public async Task<WhenContext<TResult>> WhenAsync<TResult>(
        string description, Func<T, Task<TResult>> act)
    {
        ArgumentException.ThrowIfNullOrEmpty(description);
        ArgumentNullException.ThrowIfNull(act);

        TResult result;
        try
        {
            result = await act(_value).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            throw WrapActFailure(description, ex);
        }
        return new WhenContext<TResult>(_given, description, result, _output);
    }

    /// <summary>
    /// Executes <paramref name="act"/> and captures any thrown exception as
    /// the result. Use for testing that an operation throws. For typed
    /// exception assertions prefer <see cref="WhenThrowing{TException}"/>.
    /// </summary>
    public WhenContext<Exception?> WhenAttempting(string description, Action<T> act)
    {
        ArgumentException.ThrowIfNullOrEmpty(description);
        ArgumentNullException.ThrowIfNull(act);

        Exception? captured = null;
        try { act(_value); }
        catch (Exception ex) { captured = ex; }
        return new WhenContext<Exception?>(_given, description, captured, _output);
    }

    public async Task<WhenContext<Exception?>> WhenAttemptingAsync(
        string description, Func<T, Task> act)
    {
        ArgumentException.ThrowIfNullOrEmpty(description);
        ArgumentNullException.ThrowIfNull(act);

        Exception? captured = null;
        try { await act(_value).ConfigureAwait(false); }
        catch (Exception ex) { captured = ex; }
        return new WhenContext<Exception?>(_given, description, captured, _output);
    }

    /// <summary>
    /// Executes <paramref name="act"/> expecting <typeparamref name="TException"/>.
    /// If a *different* exception type is thrown, the scenario fails loudly
    /// (the unexpected exception is not swallowed). If no exception is thrown,
    /// the captured value is <c>null</c> — the <c>Then</c> step can assert
    /// that as a missed-expectation failure.
    /// </summary>
    public WhenContext<TException?> WhenThrowing<TException>(
        string description, Action<T> act)
        where TException : Exception
    {
        ArgumentException.ThrowIfNullOrEmpty(description);
        ArgumentNullException.ThrowIfNull(act);

        TException? captured = null;
        try { act(_value); }
        catch (TException ex) { captured = ex; }
        catch (Exception ex)
        {
            // Wrong exception type — fail loudly with narrative so the
            // author sees that their WhenThrowing<T> guess was wrong.
            throw WrapActFailure(
                description,
                new Xunit.Sdk.XunitException(
                    $"Expected {typeof(TException).Name} but {ex.GetType().Name} was thrown: {ex.Message}",
                    ex));
        }
        return new WhenContext<TException?>(_given, description, captured, _output);
    }

    public async Task<WhenContext<TException?>> WhenThrowingAsync<TException>(
        string description, Func<T, Task> act)
        where TException : Exception
    {
        ArgumentException.ThrowIfNullOrEmpty(description);
        ArgumentNullException.ThrowIfNull(act);

        TException? captured = null;
        try { await act(_value).ConfigureAwait(false); }
        catch (TException ex) { captured = ex; }
        catch (Exception ex)
        {
            throw WrapActFailure(
                description,
                new Xunit.Sdk.XunitException(
                    $"Expected {typeof(TException).Name} but {ex.GetType().Name} was thrown: {ex.Message}",
                    ex));
        }
        return new WhenContext<TException?>(_given, description, captured, _output);
    }

    private Exception WrapActFailure(string when, Exception inner)
        => new Xunit.Sdk.XunitException(
            $"Scenario failed in When step:{Environment.NewLine}" +
            $"  Given {_given}{Environment.NewLine}" +
            $"  When  {when}{Environment.NewLine}{Environment.NewLine}" +
            inner.Message,
            inner);
}

/// <summary>
/// Acted-on result, ready to be asserted.
/// </summary>
public sealed class WhenContext<TResult>
{
    private readonly string _given;
    private readonly string _when;
    private readonly TResult _result;
    private readonly Xunit.Abstractions.ITestOutputHelper? _output;

    internal WhenContext(string given, string when, TResult result,
        Xunit.Abstractions.ITestOutputHelper? output)
    {
        _given = given;
        _when = when;
        _result = result;
        _output = output;
    }

    public WhenContext<TResult> Then(string description, Action<TResult> assert)
        => Check("Then", description, assert);

    public WhenContext<TResult> And(string description, Action<TResult> assert)
        => Check("And", description, assert);

    public async Task<WhenContext<TResult>> ThenAsync(
        string description, Func<TResult, Task> assert)
        => await CheckAsync("Then", description, assert).ConfigureAwait(false);

    public async Task<WhenContext<TResult>> AndAsync(
        string description, Func<TResult, Task> assert)
        => await CheckAsync("And", description, assert).ConfigureAwait(false);

    private WhenContext<TResult> Check(string keyword, string description, Action<TResult> assert)
    {
        ArgumentException.ThrowIfNullOrEmpty(description);
        ArgumentNullException.ThrowIfNull(assert);

        try
        {
            assert(_result);
        }
        catch (Exception ex)
        {
            var wrapped = new Xunit.Sdk.XunitException(
                $"Scenario failed:{Environment.NewLine}" +
                $"  Given {_given}{Environment.NewLine}" +
                $"  When  {_when}{Environment.NewLine}" +
                $"  {keyword,-5} {description}{Environment.NewLine}{Environment.NewLine}" +
                ex.Message,
                ex);
            ExceptionDispatchInfo.SetCurrentStackTrace(wrapped);
            throw wrapped;
        }

        _output?.WriteLine(
            $"  Given {_given}{Environment.NewLine}" +
            $"  When  {_when}{Environment.NewLine}" +
            $"  {keyword,-5} {description}  ✔");
        return this;
    }

    private async Task<WhenContext<TResult>> CheckAsync(
        string keyword, string description, Func<TResult, Task> assert)
    {
        ArgumentException.ThrowIfNullOrEmpty(description);
        ArgumentNullException.ThrowIfNull(assert);

        try
        {
            await assert(_result).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            throw new Xunit.Sdk.XunitException(
                $"Scenario failed:{Environment.NewLine}" +
                $"  Given {_given}{Environment.NewLine}" +
                $"  When  {_when}{Environment.NewLine}" +
                $"  {keyword,-5} {description}{Environment.NewLine}{Environment.NewLine}" +
                ex.Message,
                ex);
        }

        _output?.WriteLine(
            $"  Given {_given}{Environment.NewLine}" +
            $"  When  {_when}{Environment.NewLine}" +
            $"  {keyword,-5} {description}  ✔");
        return this;
    }
}
