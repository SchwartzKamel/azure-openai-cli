using AzureOpenAI_CLI.ConsoleIO;

namespace AzureOpenAI_CLI.Tests;

/// <summary>
/// Tests for <see cref="AnsiPolicy"/>. The helper reads process-global env
/// vars, so each test scrubs <c>NO_COLOR</c> / <c>FORCE_COLOR</c> in a
/// <c>try/finally</c> and resets <see cref="AnsiPolicy.Override"/> after
/// each assertion. The collection serializes on a shared name so concurrent
/// xUnit workers don't race the env-var sandbox.
/// </summary>
[Collection("AnsiPolicyEnv")]
public class AnsiPolicyTests
{
    private const string NoColor = "NO_COLOR";
    private const string ForceColor = "FORCE_COLOR";

    private static IDisposable Sandbox()
    {
        var prevNo = Environment.GetEnvironmentVariable(NoColor);
        var prevForce = Environment.GetEnvironmentVariable(ForceColor);
        var prevOverride = AnsiPolicy.Override;
        Environment.SetEnvironmentVariable(NoColor, null);
        Environment.SetEnvironmentVariable(ForceColor, null);
        AnsiPolicy.Override = null;
        return new Restore(() =>
        {
            Environment.SetEnvironmentVariable(NoColor, prevNo);
            Environment.SetEnvironmentVariable(ForceColor, prevForce);
            AnsiPolicy.Override = prevOverride;
        });
    }

    private sealed class Restore : IDisposable
    {
        private readonly Action _a;
        public Restore(Action a) { _a = a; }
        public void Dispose() => _a();
    }

    [Fact]
    public void IsColorEnabled_NoEnvVarsSet_DefersToTty()
    {
        // Without an override and with both env vars unset, the helper's
        // answer is the inverse of stdout-redirected. Under xUnit stdout
        // is redirected, so we expect false; we assert the contract by
        // pinning the override and verifying the env-precedence path.
        using (Sandbox())
        {
            // Auto-detect path: under the test harness stdout IS redirected,
            // so color is off.
            Assert.False(AnsiPolicy.IsColorEnabled());
        }
    }

    [Fact]
    public void IsColorEnabled_NoColorEmpty_PerSpecDoesNotDisable()
    {
        // The no-color.org spec is explicit: "If this environment variable is
        // set to a non-empty string..." Empty string MUST NOT disable.
        using (Sandbox())
        {
            Environment.SetEnvironmentVariable(NoColor, string.Empty);
            // With NO_COLOR empty and FORCE_COLOR unset, fall through to TTY
            // detection. Stdout is redirected under xUnit → false.
            Assert.False(AnsiPolicy.IsColorEnabled());

            // Prove it's the TTY path, not NO_COLOR, by flipping FORCE_COLOR
            // on. Since NO_COLOR-empty doesn't disable, FORCE_COLOR wins.
            Environment.SetEnvironmentVariable(ForceColor, "1");
            Assert.True(AnsiPolicy.IsColorEnabled());
        }
    }

    [Theory]
    [InlineData("1")]
    [InlineData("0")]            // per spec, presence alone (any non-empty) disables
    [InlineData("true")]
    [InlineData("anything")]
    public void IsColorEnabled_NoColorNonEmpty_DisablesColor(string value)
    {
        using (Sandbox())
        {
            Environment.SetEnvironmentVariable(NoColor, value);
            Assert.False(AnsiPolicy.IsColorEnabled());
        }
    }

    [Fact]
    public void IsColorEnabled_ForceColor_OverridesRedirectedTty()
    {
        using (Sandbox())
        {
            Environment.SetEnvironmentVariable(ForceColor, "1");
            // Under xUnit stdout is redirected; FORCE_COLOR must override.
            Assert.True(AnsiPolicy.IsColorEnabled());
        }
    }

    [Fact]
    public void IsColorEnabled_ForceColorZero_DoesNotForce()
    {
        using (Sandbox())
        {
            Environment.SetEnvironmentVariable(ForceColor, "0");
            // FORCE_COLOR=0 is the explicit opt-out — fall through to TTY
            // detection (which is false under the test harness).
            Assert.False(AnsiPolicy.IsColorEnabled());
        }
    }

    [Fact]
    public void IsColorEnabled_NoColorAndForceColorBoth_NoColorWins()
    {
        using (Sandbox())
        {
            Environment.SetEnvironmentVariable(NoColor, "1");
            Environment.SetEnvironmentVariable(ForceColor, "1");
            Assert.False(AnsiPolicy.IsColorEnabled());
        }
    }

    [Fact]
    public void Override_ShortCircuitsAllPrecedence()
    {
        using (Sandbox())
        {
            Environment.SetEnvironmentVariable(NoColor, "1");
            AnsiPolicy.Override = true;
            Assert.True(AnsiPolicy.IsColorEnabled());

            AnsiPolicy.Override = false;
            Environment.SetEnvironmentVariable(NoColor, null);
            Environment.SetEnvironmentVariable(ForceColor, "1");
            Assert.False(AnsiPolicy.IsColorEnabled());
        }
    }
}
