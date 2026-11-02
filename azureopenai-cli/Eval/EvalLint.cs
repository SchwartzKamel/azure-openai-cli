using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace AzureOpenAI_CLI.Eval;

/// <summary>One thing a lint pass found. Never contains the offending text, only a masked preview.</summary>
internal sealed record EvalLintFinding(
    [property: JsonPropertyName("file")] string File,
    [property: JsonPropertyName("location")] string Location,
    [property: JsonPropertyName("rule")] string Rule,
    [property: JsonPropertyName("message")] string Message);

internal sealed record EvalLintReport(
    [property: JsonPropertyName("result")] string Result,
    [property: JsonPropertyName("files")] int Files,
    [property: JsonPropertyName("findings")] List<EvalLintFinding> Findings);

/// <summary>
/// S04E13 -- The Redaction. The policy that decides whether an eval file may be committed.
///
/// Why it exists: the built-in corpora are compiled into a distributed binary, and baselines and suites
/// are committed to a public repository. Anything real in one of them (a pasted prompt, a key, an
/// address, a home directory) is published to everyone. Earlier tests guarded the three shipped corpora
/// only; this guards ANY eval file, including a contributor's, before it is committed.
///
/// Design rules, each earned by a way a leak or a bypass gets through:
/// <list type="bullet">
///   <item><b>Context-aware.</b> A regex assertion may contain a secret-shaped DETECTOR such as
///     <c>sk-[A-Za-z0-9]{16,}</c>; a prompt may not contain a secret. Strings are classified by where
///     they sit, so the check is neither blind to leaks nor noisy on detectors.</item>
///   <item><b>Parses before it scans.</b> JSON unicode escapes (<c>\u0073k-...</c>) are decoded first, so
///     escaping a character cannot hide it from the rules.</item>
///   <item><b>Scans everywhere text can hide:</b> string values, property NAMES (a baseline's fixture
///     ids), and comments, which the loader ignores but the repository publishes.</item>
///   <item><b>Never echoes a finding.</b> The report carries a rule, a location and a masked preview. A
///     lint log that printed the secret would be a second leak, into CI logs.</item>
///   <item><b>Unparseable is a finding.</b> A file that cannot be parsed still gets a raw-text scan and a
///     finding; it is never silently skipped.</item>
/// </list>
///
/// Not covered: meaning. The lint finds shapes (keys, emails, addresses, paths, PII patterns, high-entropy
/// tokens), not a sentence that happens to describe something private. Review still matters.
/// </summary>
internal static class EvalLint
{
    internal const string Clean = "clean";
    internal const string Findings = "findings";

    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(2);

    // Token shapes that are credentials in themselves. They live in SecretRedactor.BareTokenRules (S04E16),
    // so the runtime redactor and this lint share one definition of what a bare secret looks like.
    private static readonly (string Rule, Regex Pattern, string Message)[] TokenShapes =
        SecretRedactor.BareTokenRules
            .Select(r => ("secret-shape", r.Pattern, r.Description))
            .ToArray();

    private static readonly Regex Email = new(@"[A-Za-z0-9._%+-]+@([A-Za-z0-9-]+(?:\.[A-Za-z0-9-]+)+)", RegexOptions.CultureInvariant, Timeout);
    private static readonly Regex Ipv4 = new(@"(?<![\d.])((?:\d{1,3}\.){3}\d{1,3})(?![\d.])", RegexOptions.CultureInvariant, Timeout);
    private static readonly Regex UrlHost = new(@"\b[a-z][a-z0-9+.-]*://(?:[^/@\s]*@)?([A-Za-z0-9.-]+)", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, Timeout);
    private static readonly Regex LocalPath = new(@"(?:/home/|/Users/|[A-Za-z]:\\+Users\\+)([A-Za-z0-9._-]+)", RegexOptions.CultureInvariant, Timeout);
    private static readonly Regex Ssn = new(@"(?<!\d)\d{3}-\d{2}-\d{4}(?!\d)", RegexOptions.CultureInvariant, Timeout);
    private static readonly Regex Phone = new(@"(?<!\d)\(?\d{3}\)?[-. ]\d{3}[-. ]\d{4}(?!\d)", RegexOptions.CultureInvariant, Timeout);
    private static readonly Regex Card = new(@"(?<![\d-])(?:\d[ -]?){12,18}\d(?![\d-])", RegexOptions.CultureInvariant, Timeout);
    private static readonly Regex Token = new(@"[A-Za-z0-9+/=_-]{32,}", RegexOptions.CultureInvariant, Timeout);
    private static readonly Regex BlockComment = new(@"/\*.*?\*/", RegexOptions.Singleline | RegexOptions.CultureInvariant, Timeout);

    private static readonly HashSet<string> PlaceholderUsers = new(StringComparer.OrdinalIgnoreCase)
        { "user", "username", "you", "me", "example", "name", "yourname", "<user>", "alice", "bob" };

    /// <summary>Lint files from disk. Missing files are findings, not silent skips.</summary>
    internal static EvalLintReport LintFiles(IReadOnlyList<string> paths, bool requireProvenance)
    {
        var findings = new List<EvalLintFinding>();
        foreach (var path in paths)
        {
            if (!File.Exists(path))
            {
                findings.Add(new EvalLintFinding(path, "-", "unreadable", "file not found"));
                continue;
            }
            string text;
            try { text = File.ReadAllText(path); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                findings.Add(new EvalLintFinding(path, "-", "unreadable", "cannot read the file"));
                continue;
            }
            findings.AddRange(LintText(path, text, requireProvenance));
        }
        return new EvalLintReport(findings.Count == 0 ? Clean : Findings, paths.Count, findings);
    }

    /// <summary>Lint one file's text. <paramref name="name"/> is only used in findings.</summary>
    internal static List<EvalLintFinding> LintText(string name, string text, bool requireProvenance)
    {
        var findings = new List<EvalLintFinding>();

        // 1. Raw-text rules: characters that should not be in a committed eval file at all.
        ScanCharacters(name, text, findings);

        // 2. Comments: the loader ignores them, the repository publishes them.
        foreach (var (comment, line) in ExtractComments(text))
        {
            ScanText(name, $"line {line} (comment)", comment, strict: true, findings);
        }

        if (requireProvenance && !Regex.IsMatch(text, @"Origin:\s*synthetic", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            findings.Add(new EvalLintFinding(name, "-", "provenance",
                "missing the 'Origin: synthetic' header comment that declares where the fixtures came from"));
        }

        // 3. Parse, then scan every string and property name by CONTEXT.
        try
        {
            using var doc = JsonDocument.Parse(text, new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
            });
            Walk(name, "$", doc.RootElement, findings);
        }
        catch (JsonException)
        {
            // Never skip a file because it does not parse: scan it as raw text instead and say so.
            findings.Add(new EvalLintFinding(name, "-", "unparseable", "not valid JSON, so it was scanned as raw text only"));
            ScanText(name, "raw text", text, strict: true, findings);
        }

        return findings;
    }

    private static void Walk(string file, string path, JsonElement e, List<EvalLintFinding> findings)
    {
        switch (e.ValueKind)
        {
            case JsonValueKind.Object:
                // An assertion whose type is a regex holds a PATTERN in "value", not text.
                var patternValue = IsRegexAssertion(e);
                foreach (var p in e.EnumerateObject())
                {
                    // Property NAMES are text too: a baseline keeps fixture ids as names.
                    ScanText(file, path + "." + Truncate(p.Name), p.Name, strict: true, findings);
                    if (patternValue && p.NameEquals("value") && p.Value.ValueKind == JsonValueKind.String)
                    {
                        // Detector patterns may look like secrets. They still may not hide non-ASCII.
                        continue;
                    }
                    Walk(file, path + "." + p.Name, p.Value, findings);
                }
                break;
            case JsonValueKind.Array:
                var i = 0;
                foreach (var item in e.EnumerateArray())
                {
                    Walk(file, $"{path}[{i++}]", item, findings);
                }
                break;
            case JsonValueKind.String:
                ScanText(file, path, e.GetString() ?? "", strict: true, findings);
                break;
        }
    }

    private static bool IsRegexAssertion(JsonElement obj)
        => obj.TryGetProperty("type", out var t) && t.ValueKind == JsonValueKind.String
           && t.GetString() is "regex" or "not_regex";

    private static void ScanCharacters(string file, string text, List<EvalLintFinding> findings)
    {
        var line = 1;
        var col = 0;
        foreach (var ch in text)
        {
            col++;
            if (ch == '\n') { line++; col = 0; continue; }
            if (ch is '\r' or '\t') continue;
            // Anything outside printable ASCII is refused: homoglyphs, bidi controls and zero-width
            // characters are how a reviewed file differs from what runs.
            if (ch < 0x20 || ch > 0x7E)
            {
                findings.Add(new EvalLintFinding(file, $"line {line}, column {col}", "non-ascii",
                    $"character U+{(int)ch:X4} is not printable ASCII"));
                return; // one finding is enough to fail the file; avoid a wall of output
            }
        }
    }

    private static IEnumerable<(string Text, int Line)> ExtractComments(string text)
    {
        var lines = text.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var trimmed = lines[i].TrimStart();
            if (trimmed.StartsWith("//", StringComparison.Ordinal)) yield return (trimmed[2..], i + 1);
        }
        foreach (Match m in BlockComment.Matches(text))
        {
            yield return (m.Value, text[..m.Index].Count(c => c == '\n') + 1);
        }
    }

    /// <summary>Text rules. <paramref name="strict"/> is always true today; it names the intent.</summary>
    private static void ScanText(string file, string location, string text, bool strict, List<EvalLintFinding> findings)
    {
        if (!strict || string.IsNullOrEmpty(text)) return;

        // The raw-text pass cannot see characters that arrive through a JSON escape (\u0430 is ASCII in
        // the file and a Cyrillic letter once decoded), so the rule runs on DECODED strings as well.
        foreach (var ch in text)
        {
            if (ch is '\n' or '\r' or '\t') continue;
            if (ch < 0x20 || ch > 0x7E)
            {
                findings.Add(new EvalLintFinding(file, location, "non-ascii",
                    $"character U+{(int)ch:X4} is not printable ASCII (it may have arrived through a JSON escape)"));
                break;
            }
        }

        void Add(string rule, string message, string match)
            => findings.Add(new EvalLintFinding(file, location, rule, $"{message} ({Mask(match)})"));

        try
        {
            foreach (var (rule, pattern, message) in TokenShapes)
            {
                var m = pattern.Match(text);
                if (m.Success) Add(rule, message, m.Value);
            }

            // SecretRedactor covers assignments, headers, URL credentials and secret-named fields.
            // Structured rules only: a bare token was already reported above, with a message that names it.
            var redacted = SecretRedactor.RedactStructured(text);
            if (!string.Equals(redacted, text, StringComparison.Ordinal))
            {
                findings.Add(new EvalLintFinding(file, location, "secret-shape",
                    "contains something the project's SecretRedactor would redact (a credential assignment, header or URL credential)"));
            }

            foreach (Match m in Email.Matches(text))
            {
                if (!IsReservedHost(m.Groups[1].Value)) Add("email", "email address on a real domain; use a .invalid address", m.Value);
            }
            foreach (Match m in Ipv4.Matches(text))
            {
                if (IsValidIpv4(m.Value) && !IsReservedIp(m.Value)) Add("ip-address", "IP address outside the reserved documentation ranges (TEST-NET, loopback)", m.Value);
            }
            foreach (Match m in UrlHost.Matches(text))
            {
                var host = m.Groups[1].Value.TrimEnd('.');
                if (!IsReservedHost(host) && !(IsValidIpv4(host) && IsReservedIp(host)))
                    Add("url-host", "URL host is not a reserved name (.invalid, .example, example.com, localhost)", host);
            }
            foreach (Match m in LocalPath.Matches(text))
            {
                if (!PlaceholderUsers.Contains(m.Groups[1].Value)) Add("local-path", "home-directory path with a user name", m.Value);
            }
            foreach (Match m in Ssn.Matches(text)) Add("pii", "looks like a US social security number", m.Value);
            foreach (Match m in Phone.Matches(text))
            {
                if (!Regex.IsMatch(m.Value, @"555[-. ]?01\d\d")) Add("pii", "looks like a phone number (the reserved 555-01xx range is allowed)", m.Value);
            }
            foreach (Match m in Card.Matches(text))
            {
                var digits = new string(m.Value.Where(char.IsDigit).ToArray());
                if (digits.Length is >= 13 and <= 19 && PassesLuhn(digits)) Add("pii", "looks like a payment card number", m.Value);
            }
            foreach (Match m in Token.Matches(text))
            {
                if (IsHighEntropy(m.Value)) Add("high-entropy", "long high-entropy token; real secrets look like this", m.Value);
            }
        }
        catch (RegexMatchTimeoutException)
        {
            // A string that defeats a rule is itself suspicious. Fail closed.
            findings.Add(new EvalLintFinding(file, location, "scan-timeout", "a rule timed out on this string, so it could not be verified"));
        }
    }

    private static bool IsReservedHost(string host)
    {
        host = host.ToLowerInvariant().TrimEnd('.');
        return host == "localhost"
            || host.EndsWith(".invalid", StringComparison.Ordinal) || host == "invalid"
            || host.EndsWith(".example", StringComparison.Ordinal) || host == "example"
            || host.EndsWith(".test", StringComparison.Ordinal)
            || host.EndsWith(".localhost", StringComparison.Ordinal)
            || host is "example.com" or "example.net" or "example.org"
            || host.EndsWith(".example.com", StringComparison.Ordinal)
            || host.EndsWith(".example.net", StringComparison.Ordinal)
            || host.EndsWith(".example.org", StringComparison.Ordinal);
    }

    private static bool IsValidIpv4(string s)
        => s.Split('.').Length == 4 && s.Split('.').All(p => int.TryParse(p, out var n) && n is >= 0 and <= 255 && (p == "0" || !p.StartsWith('0')));

    private static bool IsReservedIp(string ip)
        => ip.StartsWith("192.0.2.", StringComparison.Ordinal)
        || ip.StartsWith("198.51.100.", StringComparison.Ordinal)
        || ip.StartsWith("203.0.113.", StringComparison.Ordinal)
        || ip.StartsWith("127.", StringComparison.Ordinal);

    private static bool PassesLuhn(string digits)
    {
        var sum = 0;
        var alt = false;
        for (var i = digits.Length - 1; i >= 0; i--)
        {
            var d = digits[i] - '0';
            if (alt) { d *= 2; if (d > 9) d -= 9; }
            sum += d;
            alt = !alt;
        }
        return sum % 10 == 0;
    }

    /// <summary>
    /// Long tokens of letters and digits whose character distribution looks random. Long plain words and
    /// repeated characters do not qualify; hex strings of key length do.
    /// </summary>
    private static bool IsHighEntropy(string s)
    {
        if (s.Length < 32) return false;
        var hasDigit = s.Any(char.IsDigit);
        var hasLetter = s.Any(char.IsLetter);
        if (!hasDigit || !hasLetter) return false;
        var freq = s.GroupBy(c => c).Select(g => g.Count() / (double)s.Length);
        var entropy = -freq.Sum(p => p * Math.Log2(p));
        var isHex = s.All(Uri.IsHexDigit);
        return entropy >= 4.0 || (isHex && entropy >= 3.0);
    }

    /// <summary>Show enough to find the match, never enough to reuse it.</summary>
    internal static string Mask(string match)
    {
        var shown = match.Length <= 3 ? new string('*', match.Length) : match[..2] + "...";
        return $"{shown} {match.Length} chars";
    }

    private static string Truncate(string s) => s.Length <= 24 ? s : s[..24] + "...";

    /// <summary>Plain labeled lines: ASCII, no color, no glyphs.</summary>
    internal static string[] Render(EvalLintReport r)
    {
        var lines = new List<string>();
        foreach (var f in r.Findings)
        {
            lines.Add($"{f.File}: {f.Location}: {f.Rule}: {f.Message}");
        }
        lines.Add(r.Findings.Count == 0
            ? $"eval-lint: clean ({r.Files} file{(r.Files == 1 ? "" : "s")} checked)"
            : $"eval-lint: {r.Findings.Count} finding{(r.Findings.Count == 1 ? "" : "s")} in {r.Files} file{(r.Files == 1 ? "" : "s")}; the matched text is masked and never printed");
        return lines.ToArray();
    }
}
