using System;
using System.IO;

namespace AzureOpenAI_CLI_V2;

/// <summary>
/// Single chokepoint for every color / ANSI decision in <c>az-ai-v2</c>.
///
/// <para>
/// This helper implements the 7-rule precedence documented in
/// <c>.github/contracts/color-contract.md</c>. Call sites <b>MUST NOT</b>
/// mutate <c>Console.ForegroundColor</c>, assign <c>ConsoleColor</c>, or
/// embed raw ANSI SGR escapes in string literals; they must route through
/// <see cref="WriteColored"/> / <see cref="WriteLineColored"/> instead.
/// The <c>scripts/check-color-contract.sh</c> lint gate enforces this on
/// every preflight / CI run.
/// </para>
///
/// <para>
/// Precedence (first match wins, top-down):
/// <list type="number">
///   <item><description>Rule 1: <c>NO_COLOR</c> set and non-empty → color OFF.
///     Beats everything. <see href="https://no-color.org/"/>.</description></item>
///   <item><description>Rule 4: <c>TERM=dumb</c> → color OFF.</description></item>
///   <item><description>Rule 5: <c>CLICOLOR=0</c> → color OFF (wins over
///     <c>FORCE_COLOR</c> per contract rule 5).</description></item>
///   <item><description>Rule 2: <c>FORCE_COLOR</c> non-empty and ≠ "0" or
///     <c>CLICOLOR_FORCE=1</c> → color ON, even on non-TTY.</description></item>
///   <item><description>Rule 3: default — color ON iff stdout is a TTY
///     (i.e. <c>!Console.IsOutputRedirected</c>).</description></item>
///   <item><description>Fallthrough: color OFF.</description></item>
/// </list>
/// </para>
///
/// <remarks>
/// <b>Rule 6 (<c>--raw</c>)</b> is owned by the call-site argument parser, not
/// this helper — whether to emit anything at all is a per-command decision.
/// However, once <c>--raw</c> has been parsed, set
/// <see cref="RawMode"/> <c>= true</c> at startup and every
/// <see cref="WriteColored"/> / <see cref="WriteLineColored"/> becomes a
/// silent no-op, providing a defensive second layer against accidental
/// decoration in machine-readable output.
/// Wiring <c>--raw</c> into <see cref="RawMode"/> from <c>Program.cs</c> is
/// scheduled as v2.1 migration work alongside the existing ANSI call-site
/// audit. This class is AOT-safe (no reflection) and BCL-only.
/// </remarks>
/// </summary>
internal static class Theme
{
    // ANSI SGR reset — the one and only raw escape literal in the v2 tree.
    // Lives here (and not in call sites) by design.
    private const string AnsiReset = "\u001b[0m";

    /// <summary>
    /// Canonical error prefix per color-contract Rule 7. Screen readers
    /// (Orca / NVDA / VoiceOver) key off the literal token
    /// <c>[ERROR]</c> — do not hand-roll <c>Error:</c> or <c>ERR:</c>
    /// variants at call sites.
    /// </summary>
    public static string ErrorPrefix => "[ERROR]";

    /// <summary>
    /// Canonical warning prefix. Pairs with <see cref="ErrorPrefix"/> for
    /// consistent stderr token parsing.
    /// </summary>
    public static string WarnPrefix => "[warn]";

    /// <summary>
    /// When <see langword="true"/>, <see cref="WriteColored"/> and
    /// <see cref="WriteLineColored"/> are silent no-ops. Set this from the
    /// <c>--raw</c> argument parser at program startup (see remarks on
    /// <see cref="Theme"/>). Defaults to <see langword="false"/>.
    /// </summary>
    public static bool RawMode { get; set; }

    /// <summary>
    /// Returns <see langword="true"/> if color output is permitted under
    /// the current environment, per the 7-rule precedence.
    /// </summary>
    public static bool UseColor()
    {
        // Rule 1: NO_COLOR > everything. Presence alone is insufficient —
        // the spec requires a non-empty value.
        var noColor = Environment.GetEnvironmentVariable("NO_COLOR");
        if (!string.IsNullOrEmpty(noColor))
        {
            return false;
        }

        // Rule 4: TERM=dumb blocks ANSI (Emacs M-x shell, tramp, etc.).
        if (Environment.GetEnvironmentVariable("TERM") == "dumb")
        {
            return false;
        }

        // Rule 5: CLICOLOR=0 wins over FORCE_COLOR. Order matters.
        if (Environment.GetEnvironmentVariable("CLICOLOR") == "0")
        {
            return false;
        }

        // Rule 2: FORCE_COLOR / CLICOLOR_FORCE override auto-detect.
        var force = Environment.GetEnvironmentVariable("FORCE_COLOR");
        if (!string.IsNullOrEmpty(force) && force != "0")
        {
            return true;
        }

        if (Environment.GetEnvironmentVariable("CLICOLOR_FORCE") == "1")
        {
            return true;
        }

        // Rule 3: default off unless stdout is a TTY.
        return !Console.IsOutputRedirected;
    }

    /// <summary>
    /// Writes <paramref name="text"/> to <paramref name="writer"/>, wrapped
    /// in ANSI SGR codes for <paramref name="color"/> iff
    /// <see cref="UseColor"/> is <see langword="true"/> and
    /// <see cref="RawMode"/> is <see langword="false"/>. Otherwise writes
    /// plain text (or nothing, in raw mode). Never mutates
    /// <c>Console.ForegroundColor</c>; owns its own save/restore via the
    /// ANSI reset sequence.
    /// </summary>
    public static void WriteColored(TextWriter writer, ConsoleColor color, string text)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(text);

        if (RawMode)
        {
            return;
        }

        if (UseColor())
        {
            writer.Write(AnsiSgrFor(color));
            writer.Write(text);
            writer.Write(AnsiReset);
        }
        else
        {
            writer.Write(text);
        }
    }

    /// <summary>
    /// Line-terminated sibling of <see cref="WriteColored"/>. The newline
    /// is emitted outside the SGR reset so cursor-parked assistive tech
    /// doesn't get a styled blank line.
    /// </summary>
    public static void WriteLineColored(TextWriter writer, ConsoleColor color, string text)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(text);

        if (RawMode)
        {
            return;
        }

        if (UseColor())
        {
            writer.Write(AnsiSgrFor(color));
            writer.Write(text);
            writer.Write(AnsiReset);
            writer.WriteLine();
        }
        else
        {
            writer.WriteLine(text);
        }
    }

    /// <summary>
    /// Maps <see cref="ConsoleColor"/> to an ANSI SGR foreground escape.
    /// Uses the 16-color palette that every terminal since the VT100 has
    /// supported; no 256-color or truecolor codes (those require capability
    /// negotiation we deliberately don't do).
    /// </summary>
    private static string AnsiSgrFor(ConsoleColor color) => color switch
    {
        ConsoleColor.Black => "\u001b[30m",
        ConsoleColor.DarkRed => "\u001b[31m",
        ConsoleColor.DarkGreen => "\u001b[32m",
        ConsoleColor.DarkYellow => "\u001b[33m",
        ConsoleColor.DarkBlue => "\u001b[34m",
        ConsoleColor.DarkMagenta => "\u001b[35m",
        ConsoleColor.DarkCyan => "\u001b[36m",
        ConsoleColor.Gray => "\u001b[37m",
        ConsoleColor.DarkGray => "\u001b[90m",
        ConsoleColor.Red => "\u001b[91m",
        ConsoleColor.Green => "\u001b[92m",
        ConsoleColor.Yellow => "\u001b[93m",
        ConsoleColor.Blue => "\u001b[94m",
        ConsoleColor.Magenta => "\u001b[95m",
        ConsoleColor.Cyan => "\u001b[96m",
        ConsoleColor.White => "\u001b[97m",
        _ => string.Empty,
    };
}
