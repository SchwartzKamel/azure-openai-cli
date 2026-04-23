namespace AzureOpenAI_CLI.ConsoleIO;

/// <summary>
/// Single chokepoint for the v1 binary's color / ANSI policy.
///
/// <para>
/// The v1 tree historically emitted no SGR escape sequences (the spinner
/// uses Braille glyphs only, prefixes are bare <c>[INFO]</c> / <c>[ERROR]</c>),
/// so this helper is preventative rather than corrective: any future call
/// site that wants to emit color <b>must</b> route through
/// <see cref="IsColorEnabled"/> instead of testing
/// <see cref="System.Console.IsOutputRedirected"/> in isolation.
/// </para>
///
/// <para>
/// Precedence (first match wins, top-down) — kept deliberately in lockstep
/// with <c>azureopenai-cli-v2/Theme.cs</c> and the user-facing contract in
/// <c>docs/accessibility.md</c>:
/// <list type="number">
///   <item><description><c>NO_COLOR</c> set to any non-empty value → color OFF.
///     Per <see href="https://no-color.org/"/>, presence alone is not enough;
///     an empty string does <b>not</b> disable.</description></item>
///   <item><description><c>FORCE_COLOR</c> non-empty and not <c>"0"</c> → color ON,
///     even when stdout is redirected. Standard cross-tool convention
///     (npm, Rust, etc.).</description></item>
///   <item><description>Default: color ON iff stdout is a TTY
///     (<c>!Console.IsOutputRedirected</c>).</description></item>
/// </list>
/// </para>
///
/// <para>
/// AOT-safe (no reflection) and BCL-only.
/// </para>
/// </summary>
internal static class AnsiPolicy
{
    /// <summary>
    /// Test-only override. When non-<see langword="null"/>, short-circuits
    /// the env-var / TTY precedence and returns the stored value directly.
    /// Production code never sets this; tests use <c>try/finally</c> to
    /// guarantee cleanup so cross-test parallelism cannot leak the value.
    /// </summary>
    internal static bool? Override { get; set; }

    /// <summary>
    /// Returns <see langword="true"/> when ANSI color is permitted under
    /// the current environment, per the precedence above.
    /// </summary>
    public static bool IsColorEnabled()
    {
        if (Override is bool forced)
        {
            return forced;
        }

        // Rule 1: NO_COLOR (non-empty) wins over everything else.
        var noColor = Environment.GetEnvironmentVariable("NO_COLOR");
        if (!string.IsNullOrEmpty(noColor))
        {
            return false;
        }

        // Rule 2: FORCE_COLOR (non-empty, not "0") forces color on even when
        // stdout is redirected — for CI log viewers that DO render ANSI.
        var force = Environment.GetEnvironmentVariable("FORCE_COLOR");
        if (!string.IsNullOrEmpty(force) && force != "0")
        {
            return true;
        }

        // Rule 3: default — color iff stdout is a TTY.
        return !System.Console.IsOutputRedirected;
    }
}
