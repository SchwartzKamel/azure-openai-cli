using System;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace AzureOpenAI_CLI.Localization;

/// <summary>
/// East Asian Width measurement for terminal column math.
///
/// <para>
/// Implements a pragmatic subset of Unicode TR-11 (East Asian Width) so that
/// <c>az-ai</c> table renderers, progress bars, and truncation helpers can
/// compute the *display column* footprint of a string -- not its
/// <c>string.Length</c> in UTF-16 code units, which would be wrong for CJK,
/// fullwidth forms, emoji, and combining marks.
/// </para>
///
/// <para>S04E04 -- Babu Bhatt. AOT-safe (no reflection, no dynamic codegen).</para>
///
/// <para>Rules (per the brief):</para>
/// <list type="bullet">
///   <item>ASCII printable: 1 column each</item>
///   <item>East Asian Wide (W) / Fullwidth (F): 2 columns each</item>
///   <item>East Asian Narrow / Half-Width / Neutral / Ambiguous: 1 column each</item>
///   <item>Combining marks (Mn / Me): 0</item>
///   <item>Format characters (Cf), incl. ZWJ / ZWNJ / BOM: 0</item>
///   <item>Control chars (Cc): 0 (best effort -- caller should sanitize)</item>
///   <item>Emoji ZWJ sequences: best-effort 2 per visible grapheme (the cluster's max rune width)</item>
/// </list>
/// </summary>
public static class EastAsianWidth
{
    /// <summary>
    /// Measure the display width of <paramref name="s"/> in terminal columns.
    /// Returns 0 for null / empty input. Surrogate-pair safe. Grapheme-cluster aware
    /// (a ZWJ emoji family is one visible glyph, width 2).
    /// </summary>
    public static int MeasureDisplayWidth(string? s)
    {
        if (string.IsNullOrEmpty(s))
        {
            return 0;
        }

        int total = 0;
        var en = StringInfo.GetTextElementEnumerator(s);
        while (en.MoveNext())
        {
            string cluster = (string)en.Current;
            total += MeasureClusterWidth(cluster);
        }
        return total;
    }

    /// <summary>
    /// Width of a single grapheme cluster: the maximum width of any rune in the
    /// cluster (so base + combining marks => width of the base; ZWJ-joined emoji
    /// family => width 2, not 8).
    /// </summary>
    private static int MeasureClusterWidth(string cluster)
    {
        int max = 0;
        int i = 0;
        while (i < cluster.Length)
        {
            if (Rune.TryGetRuneAt(cluster, i, out Rune rune))
            {
                int w = RuneWidth(rune);
                if (w > max)
                {
                    max = w;
                }
                i += rune.Utf16SequenceLength;
            }
            else
            {
                // Unpaired surrogate: treat as 1 (replacement character behavior).
                if (max < 1)
                {
                    max = 1;
                }
                i++;
            }
        }
        return max;
    }

    /// <summary>
    /// Width of a single Unicode scalar value.
    /// </summary>
    private static int RuneWidth(Rune rune)
    {
        int cp = rune.Value;

        // Fast path: ASCII printable.
        if (cp >= 0x20 && cp < 0x7F)
        {
            return 1;
        }

        // C0 + DEL + C1 control characters -- 0 columns.
        if (cp < 0x20 || (cp >= 0x7F && cp < 0xA0))
        {
            return 0;
        }

        // Explicit zero-width: ZWSP, ZWNJ, ZWJ, BOM, word joiner, etc.
        if (cp == 0x200B || cp == 0x200C || cp == 0x200D ||
            cp == 0x2060 || cp == 0xFEFF)
        {
            return 0;
        }

        // Combining marks and format characters.
        var cat = Rune.GetUnicodeCategory(rune);
        if (cat == UnicodeCategory.NonSpacingMark ||
            cat == UnicodeCategory.EnclosingMark ||
            cat == UnicodeCategory.Format)
        {
            return 0;
        }

        // East Asian Wide / Fullwidth ranges (Unicode TR-11, subset sufficient
        // for terminal column math; ambiguous EAW classes are treated as narrow).
        return IsWide(cp) ? 2 : 1;
    }

    /// <summary>
    /// True if the code point is East Asian Wide (W) or Fullwidth (F).
    /// Sorted, non-overlapping ranges; binary search.
    /// </summary>
    private static bool IsWide(int cp)
    {
        // Sorted inclusive [start, end] pairs.
        ReadOnlySpan<int> r = WideRanges;
        int lo = 0;
        int hi = (r.Length / 2) - 1;
        while (lo <= hi)
        {
            int mid = (lo + hi) >>> 1;
            int start = r[mid * 2];
            int end = r[mid * 2 + 1];
            if (cp < start)
            {
                hi = mid - 1;
            }
            else if (cp > end)
            {
                lo = mid + 1;
            }
            else
            {
                return true;
            }
        }
        return false;
    }

    // Flat pairs: start, end (inclusive). Keep sorted ascending and disjoint.
    private static readonly int[] WideRanges = new[]
    {
        0x1100, 0x115F, // Hangul Jamo
        0x2329, 0x232A, // Angle brackets
        0x2E80, 0x303E, // CJK Radicals .. CJK Symbols (excl. U+303F narrow)
        0x3041, 0x33FF, // Hiragana .. CJK Compat
        0x3400, 0x4DBF, // CJK Unified Ext A
        0x4E00, 0x9FFF, // CJK Unified Ideographs
        0xA000, 0xA4CF, // Yi
        0xAC00, 0xD7A3, // Hangul Syllables
        0xF900, 0xFAFF, // CJK Compat Ideographs
        0xFE10, 0xFE19, // Vertical forms
        0xFE30, 0xFE6F, // CJK Compat Forms + Small Form Variants
        0xFF00, 0xFF60, // Fullwidth Forms (halfwidth begins at FF61)
        0xFFE0, 0xFFE6, // Fullwidth signs
        0x16FE0, 0x16FE4, // Tangut / Khitan
        0x17000, 0x187F7, // Tangut
        0x18800, 0x18CD5, // Tangut Components / Khitan Small Script
        0x18D00, 0x18D08, // Tangut Supplement
        0x1AFF0, 0x1B16F, // Kana Ext / Small Kana / Nushu
        0x1B170, 0x1B2FF, // Nushu
        0x1F004, 0x1F004, // Mahjong red dragon
        0x1F0CF, 0x1F0CF, // Playing card black joker
        0x1F18E, 0x1F18E, // Negative squared AB
        0x1F191, 0x1F19A, // Squared CL..VS
        0x1F200, 0x1F320, // Enclosed Ideographic + Misc Symbols and Pictographs (start)
        0x1F32D, 0x1F335, // emoji
        0x1F337, 0x1F37C, // emoji
        0x1F37E, 0x1F393, // emoji
        0x1F3A0, 0x1F3CA, // emoji
        0x1F3CF, 0x1F3D3, // emoji
        0x1F3E0, 0x1F3F0, // emoji
        0x1F3F4, 0x1F3F4, // emoji
        0x1F3F8, 0x1F43E, // emoji
        0x1F440, 0x1F440, // eyes
        0x1F442, 0x1F4FC, // emoji
        0x1F4FF, 0x1F53D, // emoji
        0x1F54B, 0x1F54E, // religious
        0x1F550, 0x1F567, // clocks
        0x1F57A, 0x1F57A, // man dancing
        0x1F595, 0x1F596, // emoji
        0x1F5A4, 0x1F5A4, // black heart
        0x1F5FB, 0x1F64F, // emoticons
        0x1F680, 0x1F6C5, // transport / map
        0x1F6CC, 0x1F6CC, // sleeping accom
        0x1F6D0, 0x1F6D2, // emoji
        0x1F6D5, 0x1F6D7, // emoji
        0x1F6DC, 0x1F6DF, // emoji
        0x1F6EB, 0x1F6EC, // airplane departure / arrival
        0x1F6F4, 0x1F6FC, // emoji
        0x1F7E0, 0x1F7EB, // colored circles / squares
        0x1F7F0, 0x1F7F0, // heavy equals
        0x1F90C, 0x1F93A, // emoji
        0x1F93C, 0x1F945, // emoji
        0x1F947, 0x1F9FF, // emoji
        0x1FA70, 0x1FA7C, // emoji
        0x1FA80, 0x1FA88, // emoji
        0x1FA90, 0x1FABD, // emoji
        0x1FABF, 0x1FAC5, // emoji
        0x1FACE, 0x1FADB, // emoji
        0x1FAE0, 0x1FAE8, // emoji
        0x1FAF0, 0x1FAF8, // emoji
        0x20000, 0x2FFFD, // CJK Ext B..F + CJK Compat Ideographs Supplement
        0x30000, 0x3FFFD, // CJK Ext G..
    };

    [Conditional("DEBUG")]
    internal static void DebugSanityCheck()
    {
        Debug.Assert(MeasureDisplayWidth("a") == 1, "ASCII 'a' must be 1 column");
        Debug.Assert(MeasureDisplayWidth("\u4E2D") == 2, "CJK U+4E2D must be 2 columns");
    }
}
