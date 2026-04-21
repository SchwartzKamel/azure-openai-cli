using System;
using System.IO;
using Xunit;
using AzureOpenAI_CLI_V2;

namespace AzureOpenAI_CLI.V2.Tests;

/// <summary>
/// Dedicated xUnit collection for the color-contract test suite.
///
/// Rationale: <see cref="ThemeTests"/> mutates process-wide environment
/// variables (<c>NO_COLOR</c>, <c>FORCE_COLOR</c>, <c>CLICOLOR</c>,
/// <c>CLICOLOR_FORCE</c>, <c>TERM</c>) to exercise every branch of
/// <see cref="Theme.UseColor"/>. Env vars are global state shared across
/// the entire <see cref="AppDomain"/>; running these tests in parallel
/// with any other test that reads the same vars produces non-deterministic
/// results. Serializing the suite via <c>DisableParallelization</c> is the
/// minimum viable isolation.
///
/// Kept separate from <see cref="TelemetryGlobalStateCollection"/> on
/// purpose — those tests serialize around <c>Observability.Telemetry</c>
/// state, not env-var state, and combining the two needlessly slows
/// both suites.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ColorContractCollection
{
    public const string Name = "ColorContract";
}

/// <summary>
/// Verifies the 7-rule precedence documented in
/// <c>.github/contracts/color-contract.md</c> and implemented by
/// <see cref="Theme"/>.
/// </summary>
[Collection(ColorContractCollection.Name)]
public sealed class ThemeTests
{
    // Env-var names we touch. Centralized so the save/restore helper stays
    // a single source of truth and future rules are trivial to add.
    private static readonly string[] ManagedEnvVars =
    {
        "NO_COLOR",
        "FORCE_COLOR",
        "CLICOLOR",
        "CLICOLOR_FORCE",
        "TERM",
    };

    /// <summary>
    /// Snapshot every env var the tests will mutate, null them out so the
    /// test starts from a deterministic baseline, then return a disposable
    /// that restores the original values. Also snapshots
    /// <see cref="Theme.RawMode"/>.
    /// </summary>
    private static IDisposable IsolateEnvironment()
    {
        var originals = new System.Collections.Generic.Dictionary<string, string?>();
        foreach (var name in ManagedEnvVars)
        {
            originals[name] = Environment.GetEnvironmentVariable(name);
            Environment.SetEnvironmentVariable(name, null);
        }

        var originalRawMode = Theme.RawMode;
        Theme.RawMode = false;
        var originalUseColorOverride = Theme.UseColorOverride;
        Theme.UseColorOverride = null;

        return new Restorer(() =>
        {
            foreach (var kv in originals)
            {
                Environment.SetEnvironmentVariable(kv.Key, kv.Value);
            }
            Theme.RawMode = originalRawMode;
            Theme.UseColorOverride = originalUseColorOverride;
        });
    }

    private sealed class Restorer : IDisposable
    {
        private readonly Action _onDispose;
        public Restorer(Action onDispose) { _onDispose = onDispose; }
        public void Dispose() => _onDispose();
    }

    // -----------------------------------------------------------------
    // Rule 1: NO_COLOR > everything.
    // -----------------------------------------------------------------
    [Fact]
    public void NoColor_Beats_ForceColor()
    {
        using var _ = IsolateEnvironment();
        Environment.SetEnvironmentVariable("NO_COLOR", "1");
        Environment.SetEnvironmentVariable("FORCE_COLOR", "1");

        Assert.False(Theme.UseColor());
    }

    // -----------------------------------------------------------------
    // Rule 2: FORCE_COLOR forces on.
    // -----------------------------------------------------------------
    [Fact]
    public void ForceColor_Alone_EnablesColor()
    {
        using var _ = IsolateEnvironment();
        Environment.SetEnvironmentVariable("FORCE_COLOR", "1");

        Assert.True(Theme.UseColor());
    }

    // -----------------------------------------------------------------
    // Rule 2: CLICOLOR_FORCE forces on.
    // -----------------------------------------------------------------
    [Fact]
    public void CliColorForce_EnablesColor()
    {
        using var _ = IsolateEnvironment();
        Environment.SetEnvironmentVariable("CLICOLOR_FORCE", "1");

        Assert.True(Theme.UseColor());
    }

    // -----------------------------------------------------------------
    // Rule 4: TERM=dumb wins over FORCE_COLOR (per precedence table).
    // -----------------------------------------------------------------
    [Fact]
    public void TermDumb_Beats_ForceColor()
    {
        using var _ = IsolateEnvironment();
        Environment.SetEnvironmentVariable("TERM", "dumb");
        Environment.SetEnvironmentVariable("FORCE_COLOR", "1");

        Assert.False(Theme.UseColor());
    }

    // -----------------------------------------------------------------
    // Rule 5: CLICOLOR=0 wins over FORCE_COLOR.
    // -----------------------------------------------------------------
    [Fact]
    public void CliColorZero_Beats_ForceColor()
    {
        using var _ = IsolateEnvironment();
        Environment.SetEnvironmentVariable("CLICOLOR", "0");
        Environment.SetEnvironmentVariable("FORCE_COLOR", "1");

        Assert.False(Theme.UseColor());
    }

    // -----------------------------------------------------------------
    // Rule 3 default-off: no overrides + stdout redirected → OFF.
    // Under `dotnet test`, Console.IsOutputRedirected is true because the
    // test host captures stdout. That's the exact condition we want to
    // verify: the auto-detect branch returns false on non-TTY.
    // -----------------------------------------------------------------
    [Fact]
    public void Default_NonTty_DisablesColor()
    {
        using var _ = IsolateEnvironment();
        // No overrides set; Theme.UseColor() should fall through to
        // Console.IsOutputRedirected — which is true under the test host.
        Assert.True(Console.IsOutputRedirected,
            "Sanity: test host is expected to redirect stdout.");
        Assert.False(Theme.UseColor());
    }

    // -----------------------------------------------------------------
    // Rule 7: prefixes are the literal tokens screen readers key off.
    // -----------------------------------------------------------------
    [Fact]
    public void ErrorPrefix_Is_Canonical_Literal()
    {
        Assert.Equal("[ERROR]", Theme.ErrorPrefix);
        Assert.Equal("[warn]", Theme.WarnPrefix);
    }

    // -----------------------------------------------------------------
    // Rule 6 (defensive layer): RawMode silences WriteColored even when
    // UseColor() would otherwise return true.
    // -----------------------------------------------------------------
    [Fact]
    public void RawMode_Silences_WriteColored_Even_When_UseColor_True()
    {
        using var _ = IsolateEnvironment();
        Environment.SetEnvironmentVariable("FORCE_COLOR", "1");
        Assert.True(Theme.UseColor());

        Theme.RawMode = true;

        var sw = new StringWriter();
        Theme.WriteColored(sw, ConsoleColor.Red, "should not appear");

        Assert.Equal(string.Empty, sw.ToString());
    }

    [Fact]
    public void RawMode_Silences_WriteLineColored()
    {
        using var _ = IsolateEnvironment();
        Environment.SetEnvironmentVariable("FORCE_COLOR", "1");
        Theme.RawMode = true;

        var sw = new StringWriter();
        Theme.WriteLineColored(sw, ConsoleColor.Red, "should not appear");

        Assert.Equal(string.Empty, sw.ToString());
    }

    // -----------------------------------------------------------------
    // No color + not raw → plain text, zero ANSI bytes.
    //
    // 2.0.2 (Mickey flake fix): previously relied on NO_COLOR=1 env mutation,
    // which races with any cross-collection test that transiently touches
    // NO_COLOR. Switched to the internal UseColorOverride seam so the test
    // is deterministic regardless of ambient env state.
    // -----------------------------------------------------------------
    [Fact]
    public void UseColorFalse_WriteColored_EmitsPlainText_NoAnsi()
    {
        using var _ = IsolateEnvironment();
        Theme.UseColorOverride = false;
        Assert.False(Theme.UseColor());

        var sw = new StringWriter();
        Theme.WriteColored(sw, ConsoleColor.Red, "hello");

        var output = sw.ToString();
        Assert.Equal("hello", output);
        Assert.Equal(-1, output.IndexOf('\u001b'));
    }

    // -----------------------------------------------------------------
    // Color on → text is wrapped in an SGR escape + reset.
    //
    // 2.0.2: same env-race mitigation via UseColorOverride seam.
    // -----------------------------------------------------------------
    [Fact]
    public void UseColorTrue_WriteColored_EmitsAnsiWrappedText()
    {
        using var _ = IsolateEnvironment();
        Theme.UseColorOverride = true;
        Assert.True(Theme.UseColor());

        var sw = new StringWriter();
        Theme.WriteColored(sw, ConsoleColor.Red, "hello");

        var output = sw.ToString();
        Assert.Contains("\u001b[", output);
        Assert.Contains("hello", output);
        // Reset is mandatory so downstream writes aren't accidentally styled.
        Assert.EndsWith("\u001b[0m", output);
    }

    // -----------------------------------------------------------------
    // UseColorOverride seam — 2.0.2 Mickey follow-up.
    // Overrides the 7-rule precedence entirely. Production never sets it.
    // -----------------------------------------------------------------
    [Fact]
    public void UseColorOverride_True_BeatsNoColor()
    {
        using var _ = IsolateEnvironment();
        Environment.SetEnvironmentVariable("NO_COLOR", "1");
        Theme.UseColorOverride = true;

        Assert.True(Theme.UseColor());
    }

    [Fact]
    public void UseColorOverride_False_BeatsForceColor()
    {
        using var _ = IsolateEnvironment();
        Environment.SetEnvironmentVariable("FORCE_COLOR", "1");
        Theme.UseColorOverride = false;

        Assert.False(Theme.UseColor());
    }

    [Fact]
    public void UseColorOverride_Null_FallsBackToEnvPrecedence()
    {
        using var _ = IsolateEnvironment();
        Theme.UseColorOverride = null;
        Environment.SetEnvironmentVariable("FORCE_COLOR", "1");

        Assert.True(Theme.UseColor());
    }
}
