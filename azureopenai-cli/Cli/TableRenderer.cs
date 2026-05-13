using System;
using System.Collections.Generic;
using System.Text;

namespace AzureOpenAI_CLI.Cli;

// S04E04 -- Reading Room (Mickey Abbott, Wave 1).
//
// Pure, deterministic, ASCII-only table renderer for the `models` subcommand.
// Elaine's ModelsCommand consumes this; Babu's EastAsianWidth.cs supplies the
// display-width helper. The whole accessibility story lives in this file.
//
// See ADR-014 (Reading Room) for the v1 monochrome / ASCII-only deferrals.

/// <summary>
/// Accessibility-first table renderer. Monochrome, ASCII-only, deterministic.
/// </summary>
/// <remarks>
/// <para>
/// A11y invariants enforced by <see cref="Render"/> (see ADR-014):
/// </para>
/// <list type="number">
///   <item><description>No ANSI escape sequences emitted. Ever. Color is out of scope for v1.</description></item>
///   <item><description>No tab characters. Padding is spaces only -- screen readers handle space columns predictably.</description></item>
///   <item><description>Header/body separator is ASCII <c>-</c> only. Never <c>=</c>, never em-dash, never box-drawing.</description></item>
///   <item><description>Marker glyphs are ASCII words in parentheses: <c>(default)</c>, <c>(allow)</c>, <c>(deny)</c>, <c>(preview)</c>. Never symbols (asterisk, check, dagger) -- screen readers pronounce words, not glyphs.</description></item>
///   <item><description><c>NO_COLOR</c> env var honored: trivially today (we emit no color); defensive assert here so the day color is added we cannot regress.</description></item>
///   <item><description>Display width measured via <c>EastAsianWidth.MeasureDisplayWidth</c> (Babu) -- CJK full-width = 2, Latin half-width = 1, combining marks = 0. Until Babu's helper lands, <see cref="MeasureDisplayWidth"/> falls back to <c>string.Length</c>.</description></item>
///   <item><description>Truncation happens at the last whitespace inside the column budget; ellipsis is three ASCII dots <c>...</c>, never U+2026.</description></item>
///   <item><description>Empty / missing cells render as the literal lowercase word <c>unknown</c> -- never <c>-</c>, <c>n/a</c>, or <c>(no card)</c>.</description></item>
///   <item><description>Right-aligned columns pad on the LEFT with spaces; the header stays left-aligned so screen readers read header text in natural flow.</description></item>
///   <item><description>No trailing whitespace on any line (markdownlint MD009 equivalent).</description></item>
/// </list>
/// <para>
/// <b>Purity:</b> <see cref="Render"/> is a pure function. No I/O, no clock,
/// no environment reads (the <c>NO_COLOR</c> check happens at
/// signature-validation time and only asserts a precondition on the caller-
/// supplied <see cref="RenderOptions.NoColor"/>). Same inputs always produce
/// the same output -- safe to snapshot-test.
/// </para>
/// <para>
/// <b>AOT:</b> no reflection, no dynamic codegen, no generic virtual dispatch.
/// Safe for Native AOT trim.
/// </para>
/// </remarks>
public static class TableRenderer
{
    /// <summary>Column descriptor.</summary>
    /// <param name="Header">Header text (left-aligned regardless of <paramref name="Align"/>).</param>
    /// <param name="MaxWidth">Optional cap on column display-width. Cells wider than this are word-boundary truncated with an ASCII <c>...</c> ellipsis.</param>
    /// <param name="Align">Cell alignment. Header is always left-aligned.</param>
    public record Column(string Header, int? MaxWidth = null, Alignment Align = Alignment.Left);

    /// <summary>Cell alignment. Left or right only -- no center (screen-reader unfriendly).</summary>
    public enum Alignment
    {
        Left,
        Right,
    }

    /// <summary>Render options.</summary>
    /// <param name="NoColor">Must be <c>true</c> in v1. Reserved for future color support.</param>
    /// <param name="Raw">When <c>true</c>, suppress header row and separator -- emit data rows only. Stable contract for scripting.</param>
    /// <param name="TruncateAtWordBoundary">When <c>true</c>, truncate at the last whitespace within budget; otherwise hard-cut.</param>
    public record RenderOptions(
        bool NoColor = true,
        bool Raw = false,
        bool TruncateAtWordBoundary = true);

    /// <summary>The literal rendered for any null or empty cell. See invariant 8.</summary>
    public const string EmptyCellMarker = "unknown";

    /// <summary>ASCII ellipsis used by truncation. Never U+2026.</summary>
    public const string Ellipsis = "...";

    /// <summary>
    /// Render a table to a string with embedded <c>\n</c> newlines. ASCII only.
    /// </summary>
    /// <param name="columns">Column descriptors. Must be non-null and non-empty.</param>
    /// <param name="rows">Data rows. Each row must have the same arity as <paramref name="columns"/>; shorter rows are padded with <see cref="EmptyCellMarker"/>, longer rows are truncated.</param>
    /// <param name="options">Render options.</param>
    /// <returns>Rendered table. No trailing newline on the final line; no trailing whitespace on any line.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="columns"/>, <paramref name="rows"/>, or <paramref name="options"/> is null.</exception>
    /// <exception cref="ArgumentException">If <paramref name="columns"/> is empty, or if <paramref name="options"/>.<c>NoColor</c> is false while <c>NO_COLOR</c> is set in the environment.</exception>
    public static string Render(
        IReadOnlyList<Column> columns,
        IReadOnlyList<IReadOnlyList<string>> rows,
        RenderOptions options)
    {
        ArgumentNullException.ThrowIfNull(columns);
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(options);
        if (columns.Count == 0)
        {
            throw new ArgumentException("columns must be non-empty", nameof(columns));
        }

        // Invariant 5: NO_COLOR defensive check. v1 cannot produce color, but
        // if a future caller flips NoColor=false we refuse when NO_COLOR is
        // set. This is the only environment read in the method, and it only
        // validates the caller's contract -- it does not change output.
        if (!options.NoColor)
        {
            var noColorEnv = Environment.GetEnvironmentVariable("NO_COLOR");
            if (!string.IsNullOrEmpty(noColorEnv))
            {
                throw new ArgumentException(
                    "NoColor=false is incompatible with NO_COLOR environment variable being set",
                    nameof(options));
            }
        }

        int colCount = columns.Count;

        // Normalize rows to colCount, replacing null/empty with EmptyCellMarker
        // and stripping any control characters defensively (invariants 1, 2).
        var normalized = new string[rows.Count][];
        for (int r = 0; r < rows.Count; r++)
        {
            var src = rows[r];
            var row = new string[colCount];
            for (int c = 0; c < colCount; c++)
            {
                string cell = (src != null && c < src.Count) ? src[c] : string.Empty;
                cell = SanitizeCell(cell);
                if (string.IsNullOrEmpty(cell))
                {
                    cell = EmptyCellMarker;
                }

                // Apply MaxWidth truncation up front so width measurement uses
                // the final cell text.
                if (columns[c].MaxWidth is int max && max > 0)
                {
                    cell = Truncate(cell, max, options.TruncateAtWordBoundary);
                }

                row[c] = cell;
            }

            normalized[r] = row;
        }

        // Compute the rendered width for each column: max of header width and
        // every cell's display width, capped at MaxWidth if specified.
        var widths = new int[colCount];
        for (int c = 0; c < colCount; c++)
        {
            int w = MeasureDisplayWidth(SanitizeCell(columns[c].Header));
            for (int r = 0; r < normalized.Length; r++)
            {
                int cw = MeasureDisplayWidth(normalized[r][c]);
                if (cw > w)
                {
                    w = cw;
                }
            }

            if (columns[c].MaxWidth is int max && max > 0 && w > max)
            {
                w = max;
            }

            widths[c] = w;
        }

        var sb = new StringBuilder();

        // Header + separator (suppressed in Raw mode -- invariant ties to
        // the --raw / machine-readable contract).
        if (!options.Raw)
        {
            AppendRow(sb, columns, widths, BuildHeaderRow(columns), headerRow: true);
            AppendSeparator(sb, widths);
        }

        for (int r = 0; r < normalized.Length; r++)
        {
            AppendRow(sb, columns, widths, normalized[r], headerRow: false);
        }

        // Invariant 10: no trailing newline on the final line. We append "\n"
        // between rows below; strip any trailing newline here defensively.
        int len = sb.Length;
        while (len > 0 && sb[len - 1] == '\n')
        {
            len--;
        }

        sb.Length = len;
        return sb.ToString();
    }

    private static string[] BuildHeaderRow(IReadOnlyList<Column> columns)
    {
        var headers = new string[columns.Count];
        for (int c = 0; c < columns.Count; c++)
        {
            // Header is sanitized but never replaced with EmptyCellMarker --
            // an empty header column header stays empty (caller's choice).
            headers[c] = SanitizeCell(columns[c].Header);
        }

        return headers;
    }

    private static void AppendRow(
        StringBuilder sb,
        IReadOnlyList<Column> columns,
        int[] widths,
        string[] cells,
        bool headerRow)
    {
        for (int c = 0; c < cells.Length; c++)
        {
            // Invariant 9: header is ALWAYS left-aligned, even if the column
            // declares Right alignment. Screen readers read headers in
            // natural left-to-right flow.
            var align = headerRow ? Alignment.Left : columns[c].Align;
            var padded = PadToWidth(cells[c], widths[c], align);

            // On the last column we trim trailing spaces to honor invariant
            // 10 (no trailing whitespace on any line). Interior columns keep
            // their padding so subsequent columns align.
            if (c == cells.Length - 1)
            {
                padded = TrimEnd(padded);
                sb.Append(padded);
            }
            else
            {
                sb.Append(padded);
                sb.Append("  "); // two-space column gap. Spaces only (invariant 2).
            }
        }

        sb.Append('\n');
    }

    private static void AppendSeparator(StringBuilder sb, int[] widths)
    {
        for (int c = 0; c < widths.Length; c++)
        {
            // Invariant 3: '-' only.
            sb.Append('-', widths[c]);
            if (c < widths.Length - 1)
            {
                sb.Append("  ");
            }
        }

        sb.Append('\n');
    }

    private static string PadToWidth(string cell, int width, Alignment align)
    {
        int w = MeasureDisplayWidth(cell);
        if (w >= width)
        {
            return cell;
        }

        int pad = width - w;
        return align == Alignment.Right
            ? new string(' ', pad) + cell
            : cell + new string(' ', pad);
    }

    private static string TrimEnd(string s)
    {
        int end = s.Length;
        while (end > 0 && s[end - 1] == ' ')
        {
            end--;
        }

        return end == s.Length ? s : s[..end];
    }

    /// <summary>
    /// Truncate <paramref name="s"/> so that its display-width does not
    /// exceed <paramref name="maxWidth"/>. If <paramref name="atWordBoundary"/>
    /// is true, truncate at the last ASCII space within budget; otherwise
    /// hard-cut. Always appends the ASCII <see cref="Ellipsis"/> when
    /// truncation occurred. Invariant 7.
    /// </summary>
    private static string Truncate(string s, int maxWidth, bool atWordBoundary)
    {
        int w = MeasureDisplayWidth(s);
        if (w <= maxWidth)
        {
            return s;
        }

        // If maxWidth is too small to hold even the ellipsis, hard-cut by
        // characters so we never overflow.
        int ellipsisWidth = Ellipsis.Length; // ASCII, so width == length.
        if (maxWidth <= ellipsisWidth)
        {
            // Take prefix by display width; this is a pathological case but
            // we still respect the budget.
            return TakePrefixByWidth(s, maxWidth);
        }

        int budget = maxWidth - ellipsisWidth;
        string prefix = TakePrefixByWidth(s, budget);

        if (atWordBoundary)
        {
            int lastSpace = prefix.LastIndexOf(' ');
            if (lastSpace > 0)
            {
                prefix = prefix[..lastSpace];
            }
        }

        // Strip any trailing space before appending the ellipsis so we don't
        // emit "foo ..." with a stray space.
        prefix = TrimEnd(prefix);
        return prefix + Ellipsis;
    }

    private static string TakePrefixByWidth(string s, int maxWidth)
    {
        if (maxWidth <= 0)
        {
            return string.Empty;
        }

        var sb = new StringBuilder(s.Length);
        int w = 0;
        foreach (var rune in s.EnumerateRunes())
        {
            int rw = MeasureRuneWidth(rune);
            if (w + rw > maxWidth)
            {
                break;
            }

            sb.Append(rune.ToString());
            w += rw;
        }

        return sb.ToString();
    }

    /// <summary>
    /// Sanitize a cell: strip ASCII control characters and tabs (invariants
    /// 1, 2) and replace embedded newlines with a single space. We do not
    /// strip non-ASCII -- CJK and accented Latin pass through; Babu's width
    /// helper measures them correctly.
    /// </summary>
    private static string SanitizeCell(string? s)
    {
        if (string.IsNullOrEmpty(s))
        {
            return string.Empty;
        }

        // Fast path: scan; only allocate if a fix is needed.
        bool needsFix = false;
        for (int i = 0; i < s.Length; i++)
        {
            char ch = s[i];
            if (ch == '\t' || ch == '\n' || ch == '\r' || char.IsControl(ch))
            {
                needsFix = true;
                break;
            }
        }

        if (!needsFix)
        {
            return s;
        }

        var sb = new StringBuilder(s.Length);
        for (int i = 0; i < s.Length; i++)
        {
            char ch = s[i];
            if (ch == '\t' || ch == '\n' || ch == '\r')
            {
                sb.Append(' ');
            }
            else if (char.IsControl(ch))
            {
                // Drop ANSI escapes and other control chars entirely.
                continue;
            }
            else
            {
                sb.Append(ch);
            }
        }

        // Collapse runs of double-spaces produced by newline replacement so
        // we don't end up with "foo  bar" inside a cell.
        return CollapseSpaces(sb.ToString());
    }

    private static string CollapseSpaces(string s)
    {
        if (s.IndexOf("  ", StringComparison.Ordinal) < 0)
        {
            return s;
        }

        var sb = new StringBuilder(s.Length);
        bool prevSpace = false;
        for (int i = 0; i < s.Length; i++)
        {
            char ch = s[i];
            if (ch == ' ')
            {
                if (!prevSpace)
                {
                    sb.Append(' ');
                }

                prevSpace = true;
            }
            else
            {
                sb.Append(ch);
                prevSpace = false;
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Measure the terminal display width of <paramref name="s"/> in cells.
    /// Delegates to Babu's <c>EastAsianWidth.MeasureDisplayWidth</c> which is
    /// grapheme-cluster aware (ZWJ emoji families, combining marks). See
    /// invariant 6.
    /// </summary>
    private static int MeasureDisplayWidth(string s)
        => AzureOpenAI_CLI.Localization.EastAsianWidth.MeasureDisplayWidth(s);

    // Per-rune width fallback used only by TakePrefixByWidth to enumerate
    // a prefix one rune at a time during truncation. Babu's helper measures
    // by grapheme cluster (correct for combining marks and ZWJ sequences);
    // this per-rune approximation is acceptable here because truncation
    // budgets are coarse and the last-space rewind in Truncate snaps back
    // to a word boundary anyway.
    private static int MeasureRuneWidth(System.Text.Rune rune)
    {
        // Combining marks and zero-width chars: width 0.
        if (rune.Value == 0)
        {
            return 0;
        }

        var cat = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(rune.Value);
        if (cat == System.Globalization.UnicodeCategory.NonSpacingMark ||
            cat == System.Globalization.UnicodeCategory.EnclosingMark ||
            cat == System.Globalization.UnicodeCategory.Format)
        {
            return 0;
        }

        // Coarse East Asian wide ranges (best-effort fallback only -- Babu's
        // helper is authoritative once it lands). Covers CJK unified
        // ideographs, hiragana, katakana, hangul syllables, fullwidth forms.
        int cp = rune.Value;
        if ((cp >= 0x1100 && cp <= 0x115F) ||  // Hangul Jamo
            (cp >= 0x2E80 && cp <= 0x303E) ||  // CJK Radicals, Kangxi
            (cp >= 0x3041 && cp <= 0x33FF) ||  // Hiragana, Katakana, CJK Symbols
            (cp >= 0x3400 && cp <= 0x4DBF) ||  // CJK Ext A
            (cp >= 0x4E00 && cp <= 0x9FFF) ||  // CJK Unified
            (cp >= 0xA000 && cp <= 0xA4CF) ||  // Yi
            (cp >= 0xAC00 && cp <= 0xD7A3) ||  // Hangul Syllables
            (cp >= 0xF900 && cp <= 0xFAFF) ||  // CJK Compat Ideographs
            (cp >= 0xFE30 && cp <= 0xFE4F) ||  // CJK Compat Forms
            (cp >= 0xFF00 && cp <= 0xFF60) ||  // Fullwidth Forms
            (cp >= 0xFFE0 && cp <= 0xFFE6) ||  // Fullwidth signs
            (cp >= 0x20000 && cp <= 0x2FFFD) || // CJK Ext B-F
            (cp >= 0x30000 && cp <= 0x3FFFD))   // CJK Ext G
        {
            return 2;
        }

        return 1;
    }
}
