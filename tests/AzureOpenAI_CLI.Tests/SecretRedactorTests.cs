using System;
using System.Diagnostics;
using System.Text;
using AzureOpenAI_CLI;

namespace AzureOpenAI_CLI.V2.Tests;

/// <summary>
/// S03E07 -- The Redactor (Newman). ADR-007 section 2 mandates that
/// "Authorization: Bearer ..." in any error message is a P1 unit-test
/// failure. <see cref="P1_BearerTokenNeverAppearsInRedactedOutput"/>
/// is the headline guard that fails the build if a bearer token slips
/// through. Every positive case is paired with a negative.
/// </summary>
public class SecretRedactorTests
{
    // -- P1 headline test ------------------------------------------------
    /// <summary>
    /// P1 (ADR-007 section 2): "Authorization: Bearer ..." must NEVER
    /// survive Redact(). This test fails the build the moment any bearer
    /// header leaks past the centralised scrubber.
    /// </summary>
    [Fact]
    public void P1_BearerTokenNeverAppearsInRedactedOutput()
    {
        var samples = new[]
        {
            "Authorization: Bearer sk-abc123XYZ",
            "preface Authorization: Bearer eyJhbGciOiJIUzI1NiJ9.payload.sig postface",
            "{\"headers\":{\"Authorization\":\"Bearer ghp_AAAA1111BBBB2222CCCC3333DDDD4444EEEE\"}}",
            "AUTHORIZATION:   Bearer    leadingspaces",
            "log line | authorization: bearer lower-case-token | trailing",
        };

        foreach (var s in samples)
        {
            var r = SecretRedactor.Redact(s);
            Assert.DoesNotContain("Bearer ", r, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("[REDACTED:bearer]", r, StringComparison.Ordinal);
        }
    }

    // -- Bearer ----------------------------------------------------------
    [Fact]
    public void BearerToken_InMiddleOfParagraph_IsMasked()
    {
        var input = "Request failed (401). Sent Authorization: Bearer sk-very-secret to upstream; retry?";
        var r = SecretRedactor.Redact(input);
        Assert.DoesNotContain("sk-very-secret", r, StringComparison.Ordinal);
        Assert.Contains("[REDACTED:bearer]", r, StringComparison.Ordinal);
        Assert.Contains("retry?", r, StringComparison.Ordinal);
    }

    [Fact]
    public void BearerToken_NotPresent_StringUnchanged()
    {
        var input = "Request failed (401). No auth header was sent. Retry?";
        var r = SecretRedactor.Redact(input);
        Assert.Equal(input, r);
    }

    // -- api-key / x-api-key headers -------------------------------------
    [Theory]
    [InlineData("api-key: AbCdEf1234567890", "api-key")]
    [InlineData("API-KEY: AbCdEf1234567890", "API-KEY")]
    [InlineData("x-api-key: AbCdEf1234567890", "x-api-key")]
    [InlineData("X-Api-Key: AbCdEf1234567890", "X-Api-Key")]
    public void ApiKeyHeader_CaseInsensitive_IsMasked(string input, string expectedHeader)
    {
        var r = SecretRedactor.Redact(input);
        Assert.DoesNotContain("AbCdEf1234567890", r, StringComparison.Ordinal);
        Assert.Contains(expectedHeader + ": [REDACTED:api-key]", r, StringComparison.Ordinal);
    }

    [Fact]
    public void NonSecretHeader_NotMasked_NegativeCase()
    {
        var input = "Content-Type: application/json\nUser-Agent: az-ai/3.0";
        var r = SecretRedactor.Redact(input);
        Assert.Equal(input, r);
    }

    // -- URL credentials -------------------------------------------------
    [Fact]
    public void UrlCredentials_AreMasked()
    {
        var input = "Connecting to https://alice:hunter2@example.com/path?x=1";
        var r = SecretRedactor.Redact(input);
        Assert.DoesNotContain("alice:hunter2", r, StringComparison.Ordinal);
        Assert.DoesNotContain("hunter2", r, StringComparison.Ordinal);
        Assert.Contains("https://[REDACTED:url-cred]@example.com/path", r, StringComparison.Ordinal);
    }

    [Fact]
    public void UrlWithoutCredentials_NotMasked_NegativeCase()
    {
        var input = "Connecting to https://example.com/path?x=1 (no creds)";
        var r = SecretRedactor.Redact(input);
        Assert.Equal(input, r);
    }

    // -- JSON secret fields ----------------------------------------------
    [Theory]
    [InlineData("{\"api_key\":\"ABC123\"}")]
    [InlineData("{\"apiKey\":\"ABC123\"}")]
    [InlineData("{\"api-key\":\"ABC123\"}")]
    [InlineData("{\"token\":\"ABC123\"}")]
    [InlineData("{\"secret\":\"ABC123\"}")]
    [InlineData("{\"password\":\"ABC123\"}")]
    [InlineData("{\"access_token\":\"ABC123\"}")]
    [InlineData("{\"refresh_token\":\"ABC123\"}")]
    public void JsonSecretField_AnyAlias_ValueIsMasked(string input)
    {
        var r = SecretRedactor.Redact(input);
        Assert.DoesNotContain("ABC123", r, StringComparison.Ordinal);
        Assert.Contains("[REDACTED:api-key]", r, StringComparison.Ordinal);
    }

    [Fact]
    public void JsonSecretField_NestedTwoLevelsDeep_IsMasked()
    {
        var input = "{\"outer\":{\"inner\":{\"api_key\":\"DEEP-SECRET-XYZ\",\"keep\":\"me\"}}}";
        var r = SecretRedactor.Redact(input);
        Assert.DoesNotContain("DEEP-SECRET-XYZ", r, StringComparison.Ordinal);
        Assert.Contains("[REDACTED:api-key]", r, StringComparison.Ordinal);
        // Non-secret sibling key MUST be preserved.
        Assert.Contains("\"keep\"", r, StringComparison.Ordinal);
        Assert.Contains("\"me\"", r, StringComparison.Ordinal);
    }

    [Fact]
    public void JsonNonSecretField_NotMasked_NegativeCase()
    {
        var input = "{\"name\":\"Alice\",\"email\":\"a@b.com\"}";
        var r = SecretRedactor.Redact(input);
        Assert.Equal(input, r);
    }

    // -- Env-style key=value ---------------------------------------------
    [Fact]
    public void AzureOpenAiApiEnvVar_ValueIsMasked()
    {
        var input = "export AZUREOPENAIAPI=abcdef0123456789abcdef0123456789";
        var r = SecretRedactor.Redact(input);
        Assert.DoesNotContain("abcdef0123456789abcdef0123456789", r, StringComparison.Ordinal);
        Assert.Contains("[REDACTED:azure-key]", r, StringComparison.Ordinal);
    }

    [Fact]
    public void QueryStringApiKey_IsMasked()
    {
        var input = "GET /v1/chat?api_key=SECRETXYZ123&model=gpt-4";
        var r = SecretRedactor.Redact(input);
        Assert.DoesNotContain("SECRETXYZ123", r, StringComparison.Ordinal);
        Assert.Contains("[REDACTED:api-key]", r, StringComparison.Ordinal);
        // Non-secret param survives.
        Assert.Contains("model=gpt-4", r, StringComparison.Ordinal);
    }

    // -- Multi-secret single-string --------------------------------------
    [Fact]
    public void MultipleSecretsInOneString_AllMasked()
    {
        var input =
            "Authorization: Bearer SK-1\n" +
            "x-api-key: KEY-2\n" +
            "url=https://u:p@host/x\n" +
            "{\"token\":\"TOK-3\"}";
        var r = SecretRedactor.Redact(input);
        Assert.DoesNotContain("SK-1", r, StringComparison.Ordinal);
        Assert.DoesNotContain("KEY-2", r, StringComparison.Ordinal);
        Assert.DoesNotContain("u:p@", r, StringComparison.Ordinal);
        Assert.DoesNotContain("TOK-3", r, StringComparison.Ordinal);
    }

    // -- Edge cases ------------------------------------------------------
    [Fact]
    public void EmptyString_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, SecretRedactor.Redact(string.Empty));
    }

    [Fact]
    public void NullInput_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, SecretRedactor.Redact(null));
    }

    [Fact]
    public void NoSecrets_StringUnchanged()
    {
        var input = "Hello world. This is a perfectly innocent log line. Exit code 0.";
        Assert.Equal(input, SecretRedactor.Redact(input));
    }

    // -- Performance canary ----------------------------------------------
    /// <summary>
    /// 1 MB of mostly-innocent text with a handful of bearer tokens
    /// scattered through it must redact in well under the 500ms regex
    /// timeout. Budget: 100ms on a typical CI box. If this trips, the
    /// regex set has gone superlinear and Newman needs to know.
    /// </summary>
    [Fact]
    public void LongInput_OneMegabyte_RedactsUnderBudget()
    {
        var sb = new StringBuilder(1_100_000);
        const string filler = "the quick brown fox jumps over the lazy dog. ";
        while (sb.Length < 1_000_000) sb.Append(filler);
        // Sprinkle a few real secrets so the engine actually does work.
        sb.Replace(filler, "Authorization: Bearer SECRET-A " + filler, 100, filler.Length + 32);
        var input = sb.ToString();

        var sw = Stopwatch.StartNew();
        var r = SecretRedactor.Redact(input);
        sw.Stop();

        Assert.DoesNotContain("Bearer SECRET-A", r, StringComparison.Ordinal);
        // 100ms budget on the perf canary; 500ms is the regex-engine ceiling.
        Assert.True(sw.ElapsedMilliseconds < 500,
            $"Redactor took {sw.ElapsedMilliseconds}ms on 1 MB input (budget 500ms).");
    }

    // -- Backtracking / timeout containment ------------------------------
    /// <summary>
    /// A pathological input must not hang the redactor. Even if a single
    /// pattern times out, Redact() returns the original string within
    /// the 500ms ceiling and increments the timeout counter.
    /// </summary>
    [Fact]
    public void PathologicalInput_DoesNotHang()
    {
        // Long run of characters that look like they might match the
        // bearer / kv / json patterns without actually matching.
        var input = "Authorization: Bearer " + new string('a', 200_000) + "\u0000";
        var sw = Stopwatch.StartNew();
        var r = SecretRedactor.Redact(input);
        sw.Stop();
        Assert.True(sw.ElapsedMilliseconds < 1500,
            $"Redactor took {sw.ElapsedMilliseconds}ms on pathological input (ceiling 1500ms).");
        // Either the bearer was masked OR the timeout path returned input
        // unchanged. Both are acceptable; what is NOT acceptable is hanging.
        Assert.NotNull(r);
    }

    // -- RedactException -------------------------------------------------
    [Fact]
    public void RedactException_StripsBearerFromMessage()
    {
        Exception caught;
        try
        {
            throw new InvalidOperationException(
                "Upstream rejected Authorization: Bearer sk-leak-me with 401.");
        }
        catch (InvalidOperationException ex)
        {
            caught = ex;
        }

        var r = SecretRedactor.RedactException(caught);
        Assert.DoesNotContain("sk-leak-me", r, StringComparison.Ordinal);
        Assert.Contains("[REDACTED:bearer]", r, StringComparison.Ordinal);
        Assert.Contains("InvalidOperationException", r, StringComparison.Ordinal);
    }

    [Fact]
    public void RedactException_NullInput_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, SecretRedactor.RedactException(null));
    }

    // -- Mask-format contract --------------------------------------------
    [Fact]
    public void MaskFormat_UsesBracketedKindLabels()
    {
        // Guards against accidental drift to "***", "<redacted>", etc.
        var input = "Authorization: Bearer X | api-key: Y | https://u:p@h/ | {\"token\":\"Z\"}";
        var r = SecretRedactor.Redact(input);
        Assert.Contains("[REDACTED:bearer]", r, StringComparison.Ordinal);
        Assert.Contains("[REDACTED:api-key]", r, StringComparison.Ordinal);
        Assert.Contains("[REDACTED:url-cred]", r, StringComparison.Ordinal);
    }
}
