using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AzureOpenAI_CLI.Cli;

namespace AzureOpenAI_CLI.Eval;

/// <summary>
/// The deterministic assertion evaluator. Pure: output text and an assertion in, a pass/fail
/// out. No model, no I/O, no clock, so the same output always scores the same.
///
/// Failure messages describe the EXPECTATION and never echo the model's output: output can be
/// long, can hold control characters, and can be adversarial in exactly the fixtures that
/// matter most (safety).
/// </summary>
internal static class EvalAssertions
{
    internal static readonly string[] Types =
    {
        "contains", "not_contains", "starts_with", "regex", "not_regex",
        "min_chars", "max_chars", "json_valid",
    };

    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(1);
    private const int EchoLimit = 60;

    internal static bool IsKnown(string? type) => type is not null && Array.IndexOf(Types, type) >= 0;

    /// <summary>Schema check for one assertion. Returns null when it is well-formed.</summary>
    internal static string? Validate(EvalAssertion a)
    {
        if (!IsKnown(a.Type))
        {
            return $"unknown type '{Truncate(Clean(a.Type))}'. Known types: {string.Join(", ", Types)}";
        }

        switch (a.Type)
        {
            case "contains" or "not_contains" or "starts_with":
                if (string.IsNullOrEmpty(a.Value)) return $"'{a.Type}' requires a non-empty \"value\"";
                break;
            case "regex" or "not_regex":
                if (string.IsNullOrEmpty(a.Value)) return $"'{a.Type}' requires a non-empty \"value\"";
                try { _ = new Regex(a.Value, Options(a), RegexTimeout); }
                catch (ArgumentException ex) { return $"'{a.Type}' has an invalid pattern: {Truncate(Clean(ex.Message))}"; }
                break;
            case "min_chars" or "max_chars":
                if (a.Limit is null or < 0) return $"'{a.Type}' requires a non-negative \"limit\"";
                break;
        }
        return null;
    }

    /// <summary>Null means the assertion passed; otherwise a one-line description of what failed.</summary>
    internal static string? Check(EvalAssertion a, string output)
    {
        output ??= string.Empty;
        var cmp = a.IgnoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

        switch (a.Type)
        {
            case "contains":
                return output.Contains(a.Value!, cmp) ? null : $"expected text not found: \"{Echo(a.Value)}\"";
            case "not_contains":
                return output.Contains(a.Value!, cmp) ? $"forbidden text was present: \"{Echo(a.Value)}\"" : null;
            case "starts_with":
                return output.TrimStart().StartsWith(a.Value!, cmp) ? null : $"output does not start with \"{Echo(a.Value)}\"";
            case "regex":
                return Matches(a, output, out var rxErr) ? null : rxErr ?? $"pattern did not match: /{Echo(a.Value)}/";
            case "not_regex":
                var hit = Matches(a, output, out var nrxErr);
                if (nrxErr is not null) return nrxErr;
                return hit ? $"forbidden pattern matched: /{Echo(a.Value)}/" : null;
            case "min_chars":
                return output.Length >= a.Limit ? null : $"output has {output.Length} characters; at least {a.Limit} required";
            case "max_chars":
                return output.Length <= a.Limit ? null : $"output has {output.Length} characters; at most {a.Limit} allowed";
            case "json_valid":
                return IsValidJson(output) ? null : "output is not valid JSON";
            default:
                // Unreachable after Validate, but a scorer must never pass on an unknown type.
                return $"unknown assertion type '{Echo(a.Type)}'";
        }
    }

    private static RegexOptions Options(EvalAssertion a)
        => RegexOptions.CultureInvariant | (a.IgnoreCase ? RegexOptions.IgnoreCase : RegexOptions.None);

    private static bool Matches(EvalAssertion a, string output, out string? error)
    {
        error = null;
        try
        {
            return new Regex(a.Value!, Options(a), RegexTimeout).IsMatch(output);
        }
        catch (RegexMatchTimeoutException)
        {
            // A pattern that cannot finish is a failed assertion, never a pass.
            error = "pattern timed out (catastrophic backtracking?)";
            return false;
        }
    }

    /// <summary>
    /// Valid JSON, allowing the common case of a single fenced block (a model told to answer in
    /// JSON often wraps it in a code fence).
    /// </summary>
    internal static bool IsValidJson(string output)
    {
        var text = output.Trim();
        if (text.StartsWith("```", StringComparison.Ordinal))
        {
            var firstNewline = text.IndexOf('\n');
            var lastFence = text.LastIndexOf("```", StringComparison.Ordinal);
            if (firstNewline < 0 || lastFence <= firstNewline) return false;
            text = text[(firstNewline + 1)..lastFence].Trim();
        }
        if (text.Length == 0) return false;
        try { using var _ = JsonDocument.Parse(text); return true; }
        catch (JsonException) { return false; }
    }

    private static string Echo(string? s) => Truncate(Clean(s));

    private static string Clean(string? s) => CapabilityRejection.Scrub(s ?? string.Empty);

    private static string Truncate(string s)
        => s.Length <= EchoLimit ? s : s[..EchoLimit] + "...";
}
