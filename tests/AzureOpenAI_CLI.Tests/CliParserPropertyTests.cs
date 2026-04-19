using System.Globalization;
using AzureOpenAI_CLI;

namespace AzureOpenAI_CLI.Tests;

/// <summary>
/// Property-based / systematic-boundary tests for Program.ParseCliFlags.
/// Option B (hand-rolled): deterministic pseudo-random generators feed
/// [Theory] + MemberData — no external dependency added.
///
/// Each property asserts BOTH the expected success AND the expected failure
/// side of the boundary. Pass the pass, fail the fail.
/// </summary>
public class CliParserPropertyTests
{
    // Deterministic seed so CI runs are reproducible.
    private const int Seed = 0xC0FFEE;

    private static Program.CliOptions ParseOk(params string[] args)
    {
        var (opts, err) = Program.ParseCliFlags(args);
        Assert.Null(err);
        Assert.NotNull(opts);
        return opts!;
    }

    private static Program.CliParseError ParseErr(params string[] args)
    {
        var (opts, err) = Program.ParseCliFlags(args);
        Assert.Null(opts);
        Assert.NotNull(err);
        return err!;
    }

    // =========================================================================
    // Property 1 — Valid temperature roundtrip.
    // For every t in [0.00, 2.00] at 0.01 step, --temperature t parses to t
    // (tolerance 0.001). 201 cases.
    // =========================================================================
    public static IEnumerable<object[]> ValidTemperatures()
    {
        for (int i = 0; i <= 200; i++)
        {
            double t = i / 100.0;
            yield return new object[] { t };
        }
    }

    [Theory]
    [MemberData(nameof(ValidTemperatures))]
    public void Property_TemperatureRoundtrip_ValidRange(double t)
    {
        var s = t.ToString("0.00", CultureInfo.InvariantCulture);
        var o = ParseOk("--temperature", s);
        Assert.NotNull(o.Temperature);
        Assert.InRange(Math.Abs(o.Temperature!.Value - (float)t), 0.0, 0.001);
    }

    // =========================================================================
    // Property 2 — Invalid temperature rejected.
    // Random t ∉ [0.0, 2.0] must yield a CliParseError.
    // =========================================================================
    public static IEnumerable<object[]> InvalidTemperatures()
    {
        var rng = new Random(Seed);
        for (int i = 0; i < 40; i++)
        {
            // Half below 0, half above 2.
            double v = (i % 2 == 0)
                ? -(rng.NextDouble() * 100.0 + 0.0001)  // (-100, 0)
                :  (2.0001 + rng.NextDouble() * 100.0); // (2, 102)
            yield return new object[] { v };
        }
    }

    [Theory]
    [MemberData(nameof(InvalidTemperatures))]
    public void Property_Temperature_OutOfRange_Rejected(double t)
    {
        var s = t.ToString("G17", CultureInfo.InvariantCulture);
        var e = ParseErr("--temperature", s);
        Assert.Contains("between 0.0 and 2.0", e.Message);
        Assert.Equal(1, e.ExitCode);
    }

    // =========================================================================
    // Property 3 — max-tokens bounds.
    // Valid: n ∈ {1, 128000, 40 random in [1, 128000]}.
    // Invalid: n ∈ {0, -1, 128001, 40 random outside}.
    // =========================================================================
    public static IEnumerable<object[]> ValidMaxTokens()
    {
        yield return new object[] { 1 };
        yield return new object[] { 128000 };
        var rng = new Random(Seed);
        for (int i = 0; i < 40; i++)
            yield return new object[] { rng.Next(1, 128001) };
    }

    [Theory]
    [MemberData(nameof(ValidMaxTokens))]
    public void Property_MaxTokens_InRange_Accepted(int n)
    {
        var o = ParseOk("--max-tokens", n.ToString(CultureInfo.InvariantCulture));
        Assert.Equal(n, o.MaxTokens);
    }

    public static IEnumerable<object[]> InvalidMaxTokens()
    {
        yield return new object[] { 0 };
        yield return new object[] { -1 };
        yield return new object[] { 128001 };
        var rng = new Random(Seed + 1);
        for (int i = 0; i < 40; i++)
        {
            int v = (i % 2 == 0)
                ? -rng.Next(1, 1_000_000)          // negatives
                :  rng.Next(128_001, 10_000_000);  // too large
            yield return new object[] { v };
        }
    }

    [Theory]
    [MemberData(nameof(InvalidMaxTokens))]
    public void Property_MaxTokens_OutOfRange_Rejected(int n)
    {
        var e = ParseErr("--max-tokens", n.ToString(CultureInfo.InvariantCulture));
        Assert.Contains("between 1 and 128000", e.Message);
    }

    // =========================================================================
    // Property 4 — Flag order invariance.
    // Any permutation of a fixed valid flag set yields the same CliOptions
    // (modulo RemainingArgs order, which reflects positional ordering).
    // We use only flag/value pairs here — no positionals — so RemainingArgs
    // must be empty and the parsed values must match the canonical run.
    // =========================================================================
    public static IEnumerable<object[]> FlagPermutations()
    {
        // Each block is an (flag, value?) pair treated as a single unit.
        var blocks = new List<string[]>
        {
            new[] { "--temperature", "0.7" },
            new[] { "--max-tokens", "500" },
            new[] { "--max-rounds", "5" },
            new[] { "--agent" },
            new[] { "--raw" },
            new[] { "--tools", "shell,file" },
            new[] { "--system", "canonical-system" },
        };

        // Canonical order is the declaration order above.
        // Deterministically shuffle it 12 times.
        var rng = new Random(Seed + 2);
        for (int i = 0; i < 12; i++)
        {
            var shuffled = blocks.OrderBy(_ => rng.Next()).ToList();
            var args = shuffled.SelectMany(b => b).ToArray();
            yield return new object[] { args };
        }
    }

    [Theory]
    [MemberData(nameof(FlagPermutations))]
    public void Property_FlagOrder_Invariant(string[] permutedArgs)
    {
        var canonical = ParseOk(
            "--temperature", "0.7",
            "--max-tokens", "500",
            "--max-rounds", "5",
            "--agent",
            "--raw",
            "--tools", "shell,file",
            "--system", "canonical-system");

        var o = ParseOk(permutedArgs);

        Assert.Equal(canonical.Temperature, o.Temperature);
        Assert.Equal(canonical.MaxTokens, o.MaxTokens);
        Assert.Equal(canonical.MaxAgentRounds, o.MaxAgentRounds);
        Assert.Equal(canonical.AgentMode, o.AgentMode);
        Assert.Equal(canonical.Raw, o.Raw);
        Assert.Equal(canonical.SystemPrompt, o.SystemPrompt);
        Assert.NotNull(o.EnabledTools);
        Assert.Equal(canonical.EnabledTools!.Count, o.EnabledTools!.Count);
        foreach (var t in canonical.EnabledTools!)
            Assert.Contains(t, o.EnabledTools!);

        // No positionals were passed.
        Assert.Empty(o.RemainingArgs);

        // Negative side: shuffling must not introduce parse errors.
        var (opts, err) = Program.ParseCliFlags(permutedArgs);
        Assert.Null(err);
        Assert.NotNull(opts);
    }

    // =========================================================================
    // Property 5 — Unknown-flag quarantine.
    // Per current semantics (see UnknownFlag_FallsThroughToRemainingArgs in
    // CliParserTests), any "--unknown-xxx" token falls through to
    // RemainingArgs and does NOT cause a parse error.
    // This property asserts that invariant across many random suffixes.
    // =========================================================================
    public static IEnumerable<object[]> UnknownFlagSuffixes()
    {
        var rng = new Random(Seed + 3);
        const string alpha = "abcdefghijklmnopqrstuvwxyz0123456789-";
        for (int i = 0; i < 25; i++)
        {
            int len = rng.Next(3, 20);
            var chars = new char[len];
            for (int j = 0; j < len; j++) chars[j] = alpha[rng.Next(alpha.Length)];
            yield return new object[] { "--unknown-" + new string(chars) };
        }
    }

    [Theory]
    [MemberData(nameof(UnknownFlagSuffixes))]
    public void Property_UnknownFlag_FallsThroughConsistently(string unknownFlag)
    {
        // Positive: the unknown flag lands in RemainingArgs, no error.
        var o = ParseOk(unknownFlag);
        Assert.Contains(unknownFlag, o.RemainingArgs);

        // Combined with a valid flag the valid one still parses normally.
        var o2 = ParseOk(unknownFlag, "--agent");
        Assert.True(o2.AgentMode);
        Assert.Contains(unknownFlag, o2.RemainingArgs);

        // Negative-side assertion: it is NEVER silently swallowed (must appear
        // in RemainingArgs) AND it NEVER produces a CliParseError.
        var (opts, err) = Program.ParseCliFlags(new[] { unknownFlag });
        Assert.Null(err);
        Assert.NotNull(opts);
        Assert.Single(opts!.RemainingArgs);
    }

    // =========================================================================
    // Property 6 (bonus) — Prompt passthrough.
    // Random non-flag-looking strings used as positional prompts roundtrip
    // into RemainingArgs unchanged.
    // =========================================================================
    public static IEnumerable<object[]> PromptStrings()
    {
        var rng = new Random(Seed + 4);
        string[] corpus =
        {
            "hello world",
            "write me a haiku",
            "42",
            "unicode: ☃ 日本語",
            "quotes \"inside\" string",
            "tabs\tand\nnewlines",
            "emoji 🚀🔥",
            "slashes / and \\ back",
        };
        foreach (var s in corpus) yield return new object[] { s };
        for (int i = 0; i < 8; i++)
        {
            int len = rng.Next(1, 40);
            var sb = new System.Text.StringBuilder(len);
            for (int j = 0; j < len; j++)
            {
                // Keep first char non-dash to guarantee it doesn't look like a flag.
                char c = (char)rng.Next('a', 'z' + 1);
                sb.Append(j == 0 ? c : (rng.Next(10) == 0 ? ' ' : c));
            }
            yield return new object[] { sb.ToString() };
        }
    }

    [Theory]
    [MemberData(nameof(PromptStrings))]
    public void Property_Prompt_PositionalRoundtrip(string prompt)
    {
        var o = ParseOk(prompt);
        Assert.Single(o.RemainingArgs);
        Assert.Equal(prompt, o.RemainingArgs[0]);

        // Negative: prefixing with "--" should NOT roundtrip to RemainingArgs[0]
        // as-is when it matches a known flag like --agent; verify the distinction.
        var (opts, err) = Program.ParseCliFlags(new[] { "--agent", prompt });
        Assert.Null(err);
        Assert.NotNull(opts);
        Assert.True(opts!.AgentMode);
        Assert.Contains(prompt, opts.RemainingArgs);
    }
}
