using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using AzureOpenAI_CLI;
using AzureOpenAI_CLI.Registry;
using Xunit;

namespace AzureOpenAI_CLI.Tests;

/// <summary>
/// S04E02 Wave 2b (Mickey Abbott): accessibility regression suite for the
/// <c>--doctor</c> <c>[registry]</c> section that Russell Dalrymple landed in
/// commit 57f21ec. File-disjoint from <c>RegistryTests.cs</c> (Newman) and
/// from <c>AccessibilityTests.cs</c> (the older S03E14 surface-level a11y
/// sweep) so this file can move independently as the registry presentation
/// evolves through E04 Reading Room.
///
/// Locks in the contracts a screen-reader / NO_COLOR / braille-display user
/// depends on:
///   1. NO_COLOR=1 produces zero ANSI escape bytes anywhere in the
///      <c>[registry]</c> block. (Russell's layout already emits no ANSI; the
///      assertion is here so a future "let's add color to the status column"
///      patch fails CI loudly.)
///   2. The <c>[registry]</c> block contains zero TAB (<c>\t</c>) characters.
///      Spaces only. Screen readers announce TAB as "tab", which is hostile
///      noise between every column.
///   3. After leading whitespace is stripped, every model row begins with
///      the model name -- not an empty padding column. A screen-reader user
///      listening line-by-line gets the most identifying token first.
///   4. Every byte in the <c>[registry]</c> block is 7-bit ASCII -- no
///      Unicode glyphs (no <c>\u2026</c> ellipsis, no box-drawing, no
///      pictographs) so braille displays and 8-bit terminals render cleanly.
/// </summary>
[Collection("ConsoleCapture")]
public sealed class DoctorRegistryAccessibilityTests
{
    // ESC [ ... <final-letter>. Mirrors the regex called out in the brief.
    private static readonly Regex AnsiEscape =
        new("\u001B\\[[0-9;]*[A-Za-z]", RegexOptions.Compiled);

    // Anything outside 7-bit ASCII (printable + whitespace + control).
    private static readonly Regex NonAscii =
        new("[^\u0000-\u007F]", RegexOptions.Compiled);

    // Sentinel marking the start of the section we're auditing.
    private const string SectionHeaderPrefix = "[registry]";

    /// <summary>
    /// Invokes the private <c>Program.WriteRegistrySection</c> with the
    /// supplied environment overrides applied, and returns the captured
    /// stdout text. Any prior value of <c>RegistryEntries</c> /
    /// <c>AZ_AI_REGISTRY_DIR</c> / <c>NO_COLOR</c> / <c>HOME</c> is restored
    /// on exit so tests are hermetic.
    /// </summary>
    private static string CaptureRegistrySection(
        bool isRaw,
        string? noColor = null,
        string? registryDir = null,
        string? home = null)
    {
        var prevRegistry = Environment.GetEnvironmentVariable("AZ_AI_REGISTRY_DIR");
        var prevNoColor = Environment.GetEnvironmentVariable("NO_COLOR");
        var prevHome = Environment.GetEnvironmentVariable("HOME");

        // Snapshot the static cache so we can restore it.
        var entriesProp = typeof(Program).GetProperty(
            "RegistryEntries",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        var prevEntries = entriesProp.GetValue(null);

        try
        {
            Environment.SetEnvironmentVariable("AZ_AI_REGISTRY_DIR", registryDir);
            Environment.SetEnvironmentVariable("NO_COLOR", noColor);
            if (home is not null)
                Environment.SetEnvironmentVariable("HOME", home);

            // Force the registry to (re)load from the seeded resource so the
            // section has 3 known entries and we don't rely on test-order.
            var loaded = ModelRegistry.Load(isRaw: false);
            entriesProp.SetValue(null, loaded);

            using var sw = new StringWriter();
            var method = typeof(Program).GetMethod(
                "WriteRegistrySection",
                BindingFlags.NonPublic | BindingFlags.Static)!;
            method.Invoke(null, new object[] { sw, isRaw });
            return sw.ToString();
        }
        finally
        {
            Environment.SetEnvironmentVariable("AZ_AI_REGISTRY_DIR", prevRegistry);
            Environment.SetEnvironmentVariable("NO_COLOR", prevNoColor);
            Environment.SetEnvironmentVariable("HOME", prevHome);
            entriesProp.SetValue(null, prevEntries);
        }
    }

    // -----------------------------------------------------------------------
    // Assertion 1 -- NO_COLOR honored. Zero ANSI bytes regardless of env.
    // -----------------------------------------------------------------------
    [Fact]
    public void RegistrySection_NoColor_ContainsZeroAnsiEscapes()
    {
        var output = CaptureRegistrySection(isRaw: false, noColor: "1");

        Assert.Contains(SectionHeaderPrefix, output, StringComparison.Ordinal);

        var matches = AnsiEscape.Matches(output);
        Assert.True(
            matches.Count == 0,
            $"NO_COLOR=1 [registry] block must contain zero ANSI escape bytes; "
            + $"found {matches.Count}. Output:\n{output}");
    }

    // -----------------------------------------------------------------------
    // Assertion 2 -- No tabs in registry output. Spaces only, so screen
    // readers do not announce "tab tab tab" between columns.
    // -----------------------------------------------------------------------
    [Fact]
    public void RegistrySection_ContainsZeroTabCharacters()
    {
        var output = CaptureRegistrySection(isRaw: false);

        Assert.Contains(SectionHeaderPrefix, output, StringComparison.Ordinal);

        var tabCount = output.Count(c => c == '\t');
        Assert.True(
            tabCount == 0,
            $"[registry] block must contain zero TAB characters; "
            + $"found {tabCount}. Use spaces for column alignment.");
    }

    // -----------------------------------------------------------------------
    // Assertion 3 -- Each model row starts with the model name once leading
    // whitespace is stripped. A screen-reader user listening line-by-line
    // gets the most identifying token first.
    // -----------------------------------------------------------------------
    [Fact]
    public void RegistrySection_EachModelRow_LeadsWithModelName()
    {
        var output = CaptureRegistrySection(isRaw: false);

        Assert.Contains(SectionHeaderPrefix, output, StringComparison.Ordinal);

        // Pull the names of the seeded entries. We assert on the live set
        // rather than hard-coding so the test continues to lock in the
        // contract as new models are added.
        var entries = ModelRegistry.Load(isRaw: false);
        Assert.NotEmpty(entries);

        // Split into trimmed lines and find each model's row. The "caps:"
        // continuation lines and the "[registry] N known model(s)" header
        // are intentionally skipped.
        var trimmedLines = output
            .Split('\n')
            .Select(l => l.TrimEnd('\r').TrimStart())
            .Where(l => l.Length > 0
                && !l.StartsWith(SectionHeaderPrefix, StringComparison.Ordinal)
                && !l.StartsWith("caps:", StringComparison.Ordinal))
            .ToArray();

        foreach (var entry in entries)
        {
            // The model row, after trimming leading spaces, must START with
            // the model name -- never with the provider, env-status, or
            // card-status column.
            var match = trimmedLines.FirstOrDefault(
                l => l.StartsWith(entry.Name, StringComparison.Ordinal));
            Assert.True(
                match is not null,
                $"Expected a [registry] row for '{entry.Name}' to lead with "
                + $"its name after leading whitespace is stripped. "
                + $"Trimmed lines were:\n  {string.Join("\n  ", trimmedLines)}");
        }
    }

    // -----------------------------------------------------------------------
    // Assertion 4 (bonus) -- ASCII-only. Capability tags and the truncation
    // ellipsis stay in the 7-bit range so braille displays render the row
    // without falling back to "?" glyphs.
    // -----------------------------------------------------------------------
    [Fact]
    public void RegistrySection_NoColor_IsAsciiOnly()
    {
        var output = CaptureRegistrySection(isRaw: false, noColor: "1");

        var nonAscii = NonAscii.Matches(output);
        Assert.True(
            nonAscii.Count == 0,
            $"[registry] block must be 7-bit ASCII only; "
            + $"found {nonAscii.Count} non-ASCII byte(s). "
            + $"First offender: U+{(nonAscii.Count > 0 ? ((int)nonAscii[0].Value[0]).ToString("X4") : "----")}.");
    }
}
