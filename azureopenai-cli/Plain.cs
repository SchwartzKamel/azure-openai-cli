using System;

namespace AzureOpenAI_CLI;

/// <summary>
/// Single chokepoint for the &quot;is this a plain-output run?&quot; decision.
///
/// <para>
/// S03E14 -- Mickey Abbott's accessibility pass. Plain mode is broader than
/// the color contract (<see cref="Theme"/>): it suppresses the banner, any
/// unicode glyphs in CLI output, and any spinner / cursor decoration. It is
/// the ergonomics counterpart to <c>--raw</c>, which is a stricter
/// machine-readable contract.
/// </para>
///
/// <para>
/// Plain is active when ANY of the following is true:
/// <list type="bullet">
///   <item><description><c>--plain</c> CLI flag was passed (sets
///     <see cref="OverrideActive"/>).</description></item>
///   <item><description><c>AZ_AI_PLAIN</c> env var is set to a non-empty,
///     non-zero value.</description></item>
///   <item><description><c>NO_COLOR</c> is set and non-empty
///     (https://no-color.org/).</description></item>
///   <item><description><c>TERM=dumb</c> -- legacy / Emacs M-x shell
///     compatibility.</description></item>
/// </list>
/// </para>
///
/// <para>
/// The codebase itself emits ASCII-only output by default (post-S03E14
/// glyph audit). <see cref="IsActive"/> is the gate for any future
/// optional decoration -- it MUST be checked before any spinner, banner,
/// or non-ASCII glyph is emitted to stdout / stderr.
/// </para>
///
/// <para>
/// AOT-safe: BCL-only, no reflection.
/// </para>
/// </summary>
internal static class Plain
{
    /// <summary>
    /// Test seam for unit tests. When non-<see langword="null"/>,
    /// <see cref="IsActive"/> returns this value directly, bypassing the
    /// env-var probe. Production code never sets this; the
    /// <c>InternalsVisibleTo("AzureOpenAI_CLI.Tests")</c> attribute grants
    /// only the test project access.
    /// </summary>
    internal static bool? Override { get; set; }

    /// <summary>
    /// Set by the <c>--plain</c> CLI flag at parse time. Distinct from
    /// <see cref="Override"/> so production wiring (CLI flag) and test
    /// wiring (override) never collide.
    /// </summary>
    public static bool FlagSet { get; set; }

    /// <summary>
    /// Returns <see langword="true"/> when plain output is required.
    /// Computed on every call (env vars are mutable across an
    /// invocation, e.g. when the wizard loads a config file).
    /// </summary>
    public static bool IsActive()
    {
        if (Override is bool forced)
        {
            return forced;
        }

        if (FlagSet)
        {
            return true;
        }

        var envPlain = Environment.GetEnvironmentVariable("AZ_AI_PLAIN");
        if (!string.IsNullOrEmpty(envPlain)
            && !string.Equals(envPlain, "0", StringComparison.Ordinal))
        {
            return true;
        }

        var noColor = Environment.GetEnvironmentVariable("NO_COLOR");
        if (!string.IsNullOrEmpty(noColor))
        {
            return true;
        }

        if (string.Equals(
                Environment.GetEnvironmentVariable("TERM"),
                "dumb",
                StringComparison.Ordinal))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Wiring helper -- when <c>--plain</c> is parsed at startup, set the
    /// <see cref="FlagSet"/> latch and propagate the equivalent env vars
    /// (<c>NO_COLOR=1</c>, <c>AZ_AI_PLAIN=1</c>) for the duration of the
    /// process so child code paths that re-probe (e.g.
    /// <see cref="Theme.UseColor"/>) see a consistent picture without an
    /// extra explicit dependency on this class.
    /// </summary>
    public static void Activate()
    {
        FlagSet = true;
        Environment.SetEnvironmentVariable("NO_COLOR", "1");
        Environment.SetEnvironmentVariable("AZ_AI_PLAIN", "1");
    }

    /// <summary>
    /// Reset all state (test seam). Production never calls this.
    /// </summary>
    internal static void ResetForTests()
    {
        Override = null;
        FlagSet = false;
    }
}
