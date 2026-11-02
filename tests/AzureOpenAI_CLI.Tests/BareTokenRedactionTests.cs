using System.IO;
using System.Text.Json;
using AzureOpenAI_CLI.Eval;

namespace AzureOpenAI_CLI.Tests;

/// <summary>
/// S04E16 -- a credential sitting bare in a sentence. <see cref="SecretRedactor"/> recognised a secret by
/// where it sat (a header, an assignment, a secret-named field) and so let through a provider error such as
/// "Incorrect API key provided: sk-...". These tests keep the bare-token shapes redacted, keep ordinary
/// text untouched, and pin that the eval-file lint reads the same rule list as the runtime redactor.
/// </summary>
[Collection("ConsoleCapture")]
public class BareTokenRedactionTests
{
    // Deliberately fake credentials, built by concatenation so THIS file is not itself a leak and does not
    // trip a secret scanner.
    private static readonly string FakeSk = "sk-" + "abcdefghijklmnop" + "qrstuvwx0123";
    private static readonly string FakeSkProj = "sk-proj-" + "AbCdEfGhIjKlMnOpQrStUvWxYz" + "0123456789";
    private static readonly string FakeAkia = "AKIA" + "IOSFODNN7EXAMPLE";
    private static readonly string FakeGhp = "ghp_" + "abcdefghijklmnopqrst" + "uvwxyz0123456789";
    private static readonly string FakeGhPat = "github_pat_" + "11ABCDEFG0abcdefghijkl" + "_mnopqrstuvwxyz0123456789";
    private static readonly string FakeSlack = "xoxb-" + "1234567890-abcdefghij";
    private static readonly string FakeJwt = "eyJhbGciOiJI" + "UzI1NiJ9.eyJzdWIiOiIx" + "MjM0NTY3ODkwIn0.abcdefghijklm";
    private static readonly string KeyHeader = "-----BEGIN " + "RSA PRIVATE KEY-----";
    private static readonly string KeyFooter = "-----END " + "RSA PRIVATE KEY-----";

    // ── The headline: a provider error that echoes the key ──────────────────────

    [Fact]
    public void ProviderError_EchoingAnOpenAiKey_IsRedactedInTheSentence()
    {
        var error = $"Incorrect API key provided: {FakeSk}. You can find your API key at https://platform.openai.com/account/api-keys.";

        var redacted = SecretRedactor.Redact(error);

        Assert.DoesNotContain(FakeSk, redacted);
        Assert.Contains("[REDACTED:openai-key]", redacted);
        Assert.StartsWith("Incorrect API key provided: ", redacted);
        Assert.Contains("You can find your API key at", redacted);
    }

    [Theory]
    [InlineData("sk-", "openai-key")]
    [InlineData("sk-proj-", "openai-key")]
    public void OpenAiKeys_BothFormats_AreRedacted(string prefix, string kind)
    {
        var token = prefix == "sk-" ? FakeSk : FakeSkProj;

        var redacted = SecretRedactor.Redact($"request failed for key {token} (status 401)");

        Assert.DoesNotContain(token, redacted);
        Assert.Contains($"[REDACTED:{kind}]", redacted);
        Assert.Contains("(status 401)", redacted);
    }

    public static IEnumerable<object[]> Shapes() => new[]
    {
        new object[] { "aws-key", "AccessDenied for access key " + FakeAkia + " on bucket b" },
        new object[] { "github-token", "remote: invalid token " + FakeGhp + " for repo" },
        new object[] { "github-token", "remote: invalid token " + FakeGhPat + " for repo" },
        new object[] { "slack-token", "slack rejected " + FakeSlack + " with invalid_auth" },
        new object[] { "jwt", "bad bearer-less token " + FakeJwt + " expired" },
    };

    [Theory]
    [MemberData(nameof(Shapes))]
    public void EveryBareShape_IsRedactedWithItsOwnLabel(string kind, string sentence)
    {
        var redacted = SecretRedactor.Redact(sentence);

        Assert.Contains($"[REDACTED:{kind}]", redacted);
        foreach (var secret in new[] { FakeAkia, FakeGhp, FakeGhPat, FakeSlack, FakeJwt })
            Assert.DoesNotContain(secret, redacted);
    }

    [Fact]
    public void PrivateKeyBlock_IsMaskedWholeAndTheTextAroundItSurvives()
    {
        var text = $"before\n{KeyHeader}\nMIIEowIBAAKCAQEAxxxxxxxx\nyyyyyyyyyyyyyyyy\n{KeyFooter}\nafter";

        var redacted = SecretRedactor.Redact(text);

        Assert.DoesNotContain("MIIEow", redacted);
        Assert.DoesNotContain("yyyyyyyy", redacted);
        Assert.StartsWith("before\n[REDACTED:private-key]", redacted);
        Assert.EndsWith("\nafter", redacted);
    }

    [Fact]
    public void TruncatedPrivateKey_IsMaskedToTheEndOfTheText()
    {
        // The footer was cut off (a log line, a truncated error body). The body must still go.
        var redacted = SecretRedactor.Redact($"leaked: {KeyHeader}\nMIIEowIBAAKCAQEAxxxxxxxx\nyyyy");

        Assert.DoesNotContain("MIIEow", redacted);
        Assert.StartsWith("leaked: [REDACTED:private-key]", redacted);
    }

    [Fact]
    public void SeveralTokensInOneString_AreAllRedacted()
    {
        var redacted = SecretRedactor.Redact($"tried {FakeSk}, then {FakeGhp}, then {FakeAkia}");

        Assert.DoesNotContain(FakeSk, redacted);
        Assert.DoesNotContain(FakeGhp, redacted);
        Assert.DoesNotContain(FakeAkia, redacted);
        Assert.Contains("[REDACTED:openai-key]", redacted);
        Assert.Contains("[REDACTED:github-token]", redacted);
        Assert.Contains("[REDACTED:aws-key]", redacted);
    }

    // ── What must NOT be touched ────────────────────────────────────────────────

    [Theory]
    [InlineData("task-force-assembled-for-the-quarterly-review")]
    [InlineData("risk-assessment-for-the-quarterly-review-meeting")]
    [InlineData("ask-me-anything-about-this-particular-topic")]
    [InlineData("the skeleton key to the sk-short case")]
    [InlineData("AKIA is a prefix, AKIASHORT is too short")]
    [InlineData("ghp_short and gho_short")]
    [InlineData("xoxb-short")]
    [InlineData("a JWT starts with eyJ and has three parts")]
    [InlineData("-----BEGIN CERTIFICATE----- is not a private key")]
    [InlineData("Incorrect API key provided: sk-****************************1234")]
    [InlineData("Request failed with status 401 for model gpt-4o-mini")]
    [InlineData("part number SK-ABCDEFGHIJKLMNOPQRSTUVWXYZ0123 is back-ordered")]
    [InlineData("akiaiosfodnn7example is lower case, so it is not an access key id")]
    public void OrdinaryText_IsLeftAlone(string text)
    {
        Assert.Equal(text, SecretRedactor.Redact(text));
    }

    // ── How it composes with the structured rules ───────────────────────────────

    [Fact]
    public void AssignmentWithABareKey_KeepsItsSpecificLabel_NotADoubleMask()
    {
        var redacted = SecretRedactor.Redact($"api_key={FakeSk}");

        Assert.Equal("api_key=[REDACTED:api-key]", redacted);
    }

    [Fact]
    public void ProviderKeyAssignment_KeepsTheProviderLabel()
    {
        var redacted = SecretRedactor.Redact($"OPENAI_API_KEY={FakeSk}");

        Assert.Equal("OPENAI_API_KEY=[REDACTED:provider-key]", redacted);
    }

    [Fact]
    public void Redaction_IsIdempotent()
    {
        var once = SecretRedactor.Redact($"x {FakeSk} y {FakeGhp} z {KeyHeader}\nbody");

        Assert.Equal(once, SecretRedactor.Redact(once));
    }

    [Fact]
    public void RedactStructured_LeavesBareTokensAlone()
    {
        // For a caller that reports bare tokens itself and must not report them twice.
        Assert.Equal($"found {FakeSk}", SecretRedactor.RedactStructured($"found {FakeSk}"));
        Assert.Equal("api_key=[REDACTED:api-key]", SecretRedactor.RedactStructured($"api_key={FakeSk}"));
    }

    // ── The path a user actually hits ───────────────────────────────────────────

    private static string Stderr(Action action)
    {
        var old = Console.Error;
        using var sw = new StringWriter();
        try { Console.SetError(sw); action(); return sw.ToString(); }
        finally { Console.SetError(old); }
    }

    [Fact]
    public void ErrorAndExit_RedactsABareKeyInAProviderError()
    {
        var stderr = Stderr(() => Program.ErrorAndExit($"Incorrect API key provided: {FakeSk}.", 1, jsonMode: false));

        Assert.DoesNotContain(FakeSk, stderr);
        Assert.Contains("[REDACTED:openai-key]", stderr);
    }

    [Fact]
    public void ErrorAndExit_InJsonMode_RedactsABareKeyToo()
    {
        var stderr = Stderr(() => Program.ErrorAndExit($"auth failed for {FakeGhp}", 1, jsonMode: true));

        Assert.DoesNotContain(FakeGhp, stderr);
        using var doc = JsonDocument.Parse(stderr);
        Assert.Contains("[REDACTED:github-token]", doc.RootElement.GetProperty("message").GetString());
    }

    // ── One definition of what a secret looks like ──────────────────────────────

    public static IEnumerable<object[]> OneTokenPerRule() => new[]
    {
        new object[] { "openai-key", FakeSk },
        new object[] { "aws-key", FakeAkia },
        new object[] { "github-token", FakeGhp },
        new object[] { "slack-token", FakeSlack },
        new object[] { "jwt", FakeJwt },
        new object[] { "private-key", KeyHeader },
    };

    [Theory]
    [MemberData(nameof(OneTokenPerRule))]
    public void RedactorAndEvalLint_AgreeOnEveryRule(string kind, string token)
    {
        var rule = SecretRedactor.BareTokenRules.Single(r => r.Kind == kind);

        // the runtime redactor masks it ...
        Assert.Contains($"[REDACTED:{kind}]", SecretRedactor.Redact("seen " + token));

        // ... and the eval-file lint, which reads the same rule list, reports it, once, with the rule's sentence.
        var suite = "[{\"id\":\"a\",\"user_prompt\":" + JsonSerializer.Serialize("use " + token + " now") +
                    ",\"assertions\":[{\"type\":\"contains\",\"value\":\"x\"}]}]";
        var findings = EvalLint.LintText("t.json", suite, requireProvenance: false);
        var matching = findings.Where(f => f.Message.StartsWith(rule.Description, StringComparison.Ordinal)).ToList();

        Assert.Single(matching);
        Assert.DoesNotContain(findings, f => f.Message.Contains("SecretRedactor would redact"));
    }

    [Fact]
    public void EveryRule_HasADistinctKindAndASentence()
    {
        var rules = SecretRedactor.BareTokenRules;

        Assert.Equal(rules.Count, rules.Select(r => r.Kind).Distinct().Count());
        Assert.All(rules, r => Assert.False(string.IsNullOrWhiteSpace(r.Description)));
        Assert.Equal(6, rules.Count);
    }
}
