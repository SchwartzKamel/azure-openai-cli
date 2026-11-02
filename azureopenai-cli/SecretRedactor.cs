using System;
using System.Text.RegularExpressions;
using System.Threading;

namespace AzureOpenAI_CLI;

// S03E07 -- The Redactor. Centralised secret redactor on every log /
// exception path. ADR-007 section 2 mandate: "any log path, exception
// message, or structured trace that could carry an auth header must
// route through a redactor. Auth header appearing in an exception
// message is a P1 bug."
//
// This class is intentionally tiny. No I/O. Patterns compile once.
// Every regex carries a 500ms match timeout; on timeout we return
// the input unchanged and bump the static counter so Frank Costanza
// can wire it into telemetry later. ASCII-only; StringComparison.Ordinal
// where applicable. Native-AOT safe (no reflection, no dynamic types).

/// <summary>
/// Centralised secret-scrubber. Pass any string headed for stderr,
/// stdout, structured-error JSON, or a log sink through <see cref="Redact"/>.
/// Mask format: <c>[REDACTED:&lt;kind&gt;]</c>.
///
/// Two families of rule. The structured ones recognise a secret by where it sits (a Bearer header, an
/// <c>api-key:</c> header, an env assignment, URL credentials, a secret-named JSON field). The bare-token
/// rules (S04E16, <see cref="BareTokenRules"/>) recognise a credential by its own shape, so a key that a
/// provider error echoes back in the middle of a sentence is caught too.
/// </summary>
internal static class SecretRedactor
{
    /// <summary>
    /// Number of regex match-timeouts observed since process start.
    /// Stub for telemetry -- Frank Costanza (SRE) will wire to counters.
    /// </summary>
    internal static long TimeoutCount;

    private static readonly TimeSpan MatchTimeout = TimeSpan.FromMilliseconds(500);

    /// <summary>
    /// A credential recognised by its own shape, wherever it appears. <see cref="Kind"/> is the label in the
    /// mask; <see cref="Description"/> is the sentence a linter uses. <c>EvalLint</c> reads this list, so
    /// the runtime redactor and the eval-file lint cannot disagree about what a secret looks like.
    /// </summary>
    internal sealed record BareTokenRule(string Kind, string Description, Regex Pattern);

    // The prefixes are fixed-case, so these are case-sensitive: IgnoreCase would only add false positives
    // (a word that happens to start "SK-"). Every pattern keeps the 500 ms match timeout.
    private const RegexOptions BareOpts = RegexOptions.Compiled | RegexOptions.CultureInvariant;

    /// <summary>
    /// Bare-token shapes: OpenAI-style keys (<c>sk-</c>, including <c>sk-proj-</c>), AWS access key ids,
    /// GitHub tokens (classic and fine-grained), Slack tokens, JWTs, and PEM private keys. A private key is
    /// matched as a whole block, or from its header to the end of the text when the footer was cut off,
    /// so the key body is masked and not just its first line.
    /// </summary>
    internal static readonly IReadOnlyList<BareTokenRule> BareTokenRules = new BareTokenRule[]
    {
        new("openai-key", "looks like an API key (sk-...)",
            new Regex(@"\bsk-[A-Za-z0-9_-]{16,}", BareOpts, MatchTimeout)),
        new("aws-key", "looks like an AWS access key id",
            new Regex(@"\bAKIA[0-9A-Z]{16}\b", BareOpts, MatchTimeout)),
        new("github-token", "looks like a GitHub token",
            new Regex(@"\b(?:gh[pousr]_[A-Za-z0-9]{20,}|github_pat_[A-Za-z0-9_]{22,})", BareOpts, MatchTimeout)),
        new("slack-token", "looks like a Slack token",
            new Regex(@"\bxox[baprs]-[A-Za-z0-9-]{10,}", BareOpts, MatchTimeout)),
        new("jwt", "looks like a JWT",
            new Regex(@"\beyJ[A-Za-z0-9_-]{8,}\.eyJ[A-Za-z0-9_-]{8,}\.[A-Za-z0-9_-]{8,}", BareOpts, MatchTimeout)),
        new("private-key", "contains a private-key header",
            new Regex(@"-----BEGIN [A-Z ]*PRIVATE KEY-----(?:[\s\S]*?-----END [A-Z ]*PRIVATE KEY-----|[\s\S]*$)", BareOpts, MatchTimeout)),
    };

    private const RegexOptions Opts =
        RegexOptions.Compiled |
        RegexOptions.IgnoreCase |
        RegexOptions.CultureInvariant;

    // 1. "Authorization: Bearer <token>" -- the headline ADR-007 P1 case.
    //    Tolerates JSON-quoted form: "Authorization":"Bearer xyz".
    private static readonly Regex BearerRx =
        new(@"Authorization\s*[""']?\s*:\s*[""']?\s*Bearer\s+[^\s,;""'\\]+", Opts, MatchTimeout);

    // 2. api-key / x-api-key headers (preserve the header name).
    private static readonly Regex ApiKeyHeaderRx =
        new(@"(?<name>x-api-key|api-key)\s*:\s*[^\s,;""']+", Opts, MatchTimeout);

    // 3. AZUREOPENAIAPI=<value> in env exports / shell dumps.
    //    Also covers AZURE_OPENAI_API_KEY-style variants.
    private static readonly Regex AzureKeyEnvRx =
        new(@"(?<name>AZURE[_]?OPENAI[_]?API(?:[_]?KEY)?)\s*=\s*[^\s""']+", Opts, MatchTimeout);

    // 3b. S03E10 -- per-provider env-var namespaces (ADR-010 / The Keychain).
    //     OPENAI_API_KEY, GROQ_API_KEY, TOGETHER_API_KEY, CLOUDFLARE_API_TOKEN.
    //     The generic api[_-]?key / token pattern in KvSecretRx already
    //     catches these by tail-match, but a dedicated rule makes the
    //     coverage explicit so SecretRedactorTests can assert by name and
    //     a future provider rename can't silently widen the gap.
    private static readonly Regex ProviderKeyEnvRx =
        new(
            @"(?<name>OPENAI_API_KEY|GROQ_API_KEY|TOGETHER_API_KEY|CLOUDFLARE_API_TOKEN)\s*=\s*[^\s""']+",
            Opts,
            MatchTimeout);

    // 4. URL credentials -- https://user:pass@host/...
    private static readonly Regex UrlCredRx =
        new(@"(?<scheme>https?://)[^:/?#\s]+:[^@/?#\s]+@", Opts, MatchTimeout);

    // 5. JSON-ish "key": "value" for sensitive field names (any nesting).
    //    Matches both snake_case and camelCase variants.
    private static readonly Regex JsonSecretFieldRx =
        new(
            @"""(?<name>api[_-]?key|apikey|secret|token|password|access[_-]?token|refresh[_-]?token|key)""\s*:\s*""(?<val>(?:[^""\\]|\\.)*)""",
            Opts,
            MatchTimeout);

    // 6. key= / token= / api_key= followed by a value in query strings,
    //    env exports, or "key=AbC123...". Bounded by whitespace, &, or
    //    semicolon to avoid runaway matches.
    private static readonly Regex KvSecretRx =
        new(
            @"(?<name>api[_-]?key|apikey|access[_-]?token|refresh[_-]?token|token|secret|password)\s*=\s*[^\s&;""']+",
            Opts,
            MatchTimeout);

    /// <summary>
    /// Redact secrets from <paramref name="input"/>. Null/empty in,
    /// safe-empty out. On regex timeout, returns the input unchanged
    /// (and increments <see cref="TimeoutCount"/>). Static / non-secret
    /// strings are no-ops.
    /// </summary>
    public static string Redact(string? input) => RedactCore(input, includeBareTokens: true);

    /// <summary>
    /// Only the structured rules (headers, assignments, URL credentials, secret-named fields), not the
    /// bare-token shapes. For a caller that reports bare tokens separately and must not report them twice.
    /// </summary>
    internal static string RedactStructured(string? input) => RedactCore(input, includeBareTokens: false);

    private static string RedactCore(string? input, bool includeBareTokens)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input ?? string.Empty;
        }

        try
        {
            var s = input;
            s = BearerRx.Replace(s, "Authorization: [REDACTED:bearer]");
            s = ApiKeyHeaderRx.Replace(s, m => m.Groups["name"].Value + ": [REDACTED:api-key]");
            s = AzureKeyEnvRx.Replace(s, m => m.Groups["name"].Value + "=[REDACTED:azure-key]");
            s = UrlCredRx.Replace(s, m => m.Groups["scheme"].Value + "[REDACTED:url-cred]@");
            s = JsonSecretFieldRx.Replace(s, m => "\"" + m.Groups["name"].Value + "\": \"[REDACTED:api-key]\"");
            s = KvSecretRx.Replace(s, m => m.Groups["name"].Value + "=[REDACTED:api-key]");
            // S04E16: a credential sitting bare in prose. Order against the structured rules does not change
            // any output: they also match the mask text, so `api_key=sk-...` ends up `api_key=[REDACTED:api-key]`
            // whichever runs first (BareTokenRedactionTests pins that). It sits before ProviderKeyEnvRx, which
            // must stay last so the provider label wins.
            if (includeBareTokens)
            {
                foreach (var rule in BareTokenRules)
                {
                    s = rule.Pattern.Replace(s, "[REDACTED:" + rule.Kind + "]");
                }
            }
            // ProviderKeyEnvRx runs LAST so the specific provider-key label
            // overrides any generic api-key label that KvSecretRx wrote
            // earlier for the same variable (S03E10).
            s = ProviderKeyEnvRx.Replace(s, m => m.Groups["name"].Value + "=[REDACTED:provider-key]");
            return s;
        }
        catch (RegexMatchTimeoutException)
        {
            Interlocked.Increment(ref TimeoutCount);
            return input;
        }
    }

    /// <summary>
    /// Redact <see cref="Exception.ToString"/> output -- includes the
    /// type, message, inner exceptions, and stack frames, all routed
    /// through <see cref="Redact"/>.
    /// </summary>
    public static string RedactException(Exception? ex)
    {
        if (ex is null) return string.Empty;
        return Redact(ex.ToString());
    }
}
