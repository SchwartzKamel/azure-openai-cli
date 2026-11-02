using System.Diagnostics;
using System.Text.Json;
using AzureOpenAI_CLI.Eval;
using Xunit;

namespace AzureOpenAI_CLI.Tests;

/// <summary>
/// S04E13 -- The Redaction. Locks the eval-file lint: what it must catch, what it must NOT flag, how it
/// can be bypassed (and that those bypasses fail), and that its own output never leaks what it found.
/// Each test states what it proves so a later cleanup does not mistake it for an orphan.
/// </summary>
[Collection("ConsoleCapture")]
public sealed class EvalLintTests
{
    // Deliberately fake credentials, built by concatenation so THIS test file is not itself a leak and
    // does not trip a secret scanner.
    private static readonly string FakeSk = "sk-" + "abcdefghijklmnop" + "qrstuvwx0123";
    private static readonly string FakeAkia = "AKIA" + "IOSFODNN7EXAMPLE";
    private static readonly string FakeGhp = "ghp_" + "abcdefghijklmnopqrst" + "uvwxyz0123456789";
    private static readonly string FakeSlack = "xoxb-" + "1234567890-abcdefghij";
    private static readonly string FakeJwt = "eyJhbGciOiJI" + "UzI1NiJ9.eyJzdWIiOiIx" + "MjM0NTY3ODkwIn0.abcdefghijklm";

    // ── helpers ──────────────────────────────────────────────────────────────

    private static string Prompt(string text) => "[{\"id\":\"a\",\"user_prompt\":" + JsonSerializer.Serialize(text) + ",\"assertions\":[{\"type\":\"contains\",\"value\":\"ok\"}]}]";

    private static List<EvalLintFinding> Lint(string json, bool provenance = false)
        => EvalLint.LintText("t.json", json, provenance);

    private static bool Has(List<EvalLintFinding> f, string rule) => f.Any(x => x.Rule == rule);

    private static string AllText(IEnumerable<EvalLintFinding> f)
        => string.Join("\n", f.Select(x => $"{x.File} {x.Location} {x.Rule} {x.Message}"));

    // ── what it must catch ───────────────────────────────────────────────────

    // Proves: every credential shape is caught when it sits in a PROMPT, which is the text that gets
    // compiled into a binary or committed.
    [Theory]
    [MemberData(nameof(Secrets))]
    public void Catches_CredentialShapesInAPrompt(string secret)
    {
        var findings = Lint(Prompt("Please use " + secret + " to log in"));

        Assert.NotEmpty(findings);
        Assert.True(Has(findings, "secret-shape") || Has(findings, "high-entropy"), AllText(findings));
    }

    public static IEnumerable<object[]> Secrets() => new[]
    {
        new object[] { FakeSk }, new object[] { FakeAkia }, new object[] { FakeGhp },
        new object[] { FakeSlack }, new object[] { FakeJwt },
        new object[] { "-----BEGIN " + "RSA PRIVATE KEY-----" },
        new object[] { "Authorization: Bearer " + "abcdefghijklmnopqrstuvwxyz012345" },
    };

    [Theory]
    [InlineData("contact bob@gmail.com about it", "email", true)]
    [InlineData("contact bob@attacker.invalid about it", "email", false)]
    [InlineData("contact bob@example.com about it", "email", false)]
    [InlineData("contact bob@mail.example.org about it", "email", false)]
    [InlineData("connect to 8.8.8.8", "ip-address", true)]
    [InlineData("connect to 10.0.0.5", "ip-address", true)]
    [InlineData("connect to 203.0.113.9", "ip-address", false)]
    [InlineData("connect to 198.51.100.7", "ip-address", false)]
    [InlineData("connect to 127.0.0.1", "ip-address", false)]
    [InlineData("version 999.1.1.1 shipped", "ip-address", false)]
    [InlineData("fetch https://api.realcorp.com/v1/users", "url-host", true)]
    [InlineData("fetch https://example.com/page", "url-host", false)]
    [InlineData("fetch http://localhost:8080/health", "url-host", false)]
    [InlineData("fetch https://svc.internal.invalid/x", "url-host", false)]
    [InlineData("read /home/jdoe/notes.txt", "local-path", true)]
    [InlineData("read C:\\Users\\jdoe\\notes.txt", "local-path", true)]
    [InlineData("read /Users/jdoe/notes.txt", "local-path", true)]
    [InlineData("read /home/user/notes.txt", "local-path", false)]
    [InlineData("read ~/notes.txt", "local-path", false)]
    [InlineData("ssn is 123-45-6789", "pii", true)]
    [InlineData("call 415-555-2671 now", "pii", true)]
    [InlineData("call 555-0123 now", "pii", false)]
    [InlineData("card 4111 1111 1111 1111 on file", "pii", true)]
    [InlineData("order 1234 5678 9012 3456 shipped", "pii", false)]  // 16 digits, fails Luhn
    public void Rules_FlagTheRightThings_AndLeaveReservedValuesAlone(string text, string rule, bool shouldFlag)
    {
        var findings = Lint(Prompt(text));

        Assert.Equal(shouldFlag, Has(findings, rule));
    }

    // Proves: a long random-looking token is caught even when it matches no known key format, and a
    // long ordinary string is not. Real secrets look random; prose does not.
    [Fact]
    public void HighEntropyTokens_AreCaught_ButLongWordsAreNot()
    {
        Assert.True(Has(Lint(Prompt("use token Zk3pQ9vXw2LmN8rT5yB1cD7fG4hJ6aSe0uIo please")), "high-entropy"));
        Assert.True(Has(Lint(Prompt("hash 9f86d081884c7d659a2feaa0c55ad015a3bf4f1b2b0b822cd15d6c15b0f00a08 here")), "high-entropy"));
        Assert.False(Has(Lint(Prompt("antidisestablishmentarianism-pneumonoultramicroscopicsilicovolcanoconiosis")), "high-entropy"));
        Assert.False(Has(Lint(Prompt(new string('a', 60))), "high-entropy"));
    }

    // ── context: detectors are fine, secrets are not ─────────────────────────

    // Proves: a regex assertion may contain a secret-SHAPED detector. The refusal corpus relies on this
    // (it detects "sk-..." in model output). Flagging it would make the corpus unshippable.
    [Fact]
    public void RegexAssertionPatterns_MayLookLikeSecrets()
    {
        var json = "[{\"id\":\"a\",\"user_prompt\":\"hello\",\"assertions\":[" +
                   "{\"type\":\"not_regex\",\"value\":\"sk-[A-Za-z0-9]{16,}\"}," +
                   "{\"type\":\"regex\",\"value\":\"BEGIN (RSA |EC )?PRIVATE KEY\"}]}]";

        Assert.Empty(Lint(json));
    }

    // Proves: the SAME shape in a LITERAL assertion is a real leak, because contains/not_contains/starts_with
    // hold text, not a pattern. The exemption is by assertion type, not by how a string looks.
    [Theory]
    [InlineData("contains")]
    [InlineData("not_contains")]
    [InlineData("starts_with")]
    public void LiteralAssertionValues_AreScannedLikePrompts(string type)
    {
        var json = "[{\"id\":\"a\",\"user_prompt\":\"hello\",\"assertions\":[{\"type\":\"" + type + "\",\"value\":\"" + FakeSk + "\"}]}]";

        Assert.True(Has(Lint(json), "secret-shape"));
    }

    // ── bypass attempts (FDR) ────────────────────────────────────────────────

    // Proves: a JSON unicode escape cannot hide a character from the rules. The file is parsed and
    // decoded BEFORE scanning, so \u0073k- is seen as sk-.
    [Fact]
    public void Bypass_JsonUnicodeEscapes_AreDecodedBeforeScanning()
    {
        var escaped = "\\u0073\\u006b-" + FakeSk["sk-".Length..];
        var json = "[{\"id\":\"a\",\"user_prompt\":\"use " + escaped + "\",\"assertions\":[{\"type\":\"contains\",\"value\":\"ok\"}]}]";

        Assert.True(Has(Lint(json), "secret-shape"));
    }

    // Proves: secrets hidden in places a loader never reads are still found: comments, block comments, ids,
    // property NAMES (a baseline keeps fixture ids as names), traits and the safety assertion.
    [Theory]
    [InlineData("comment")]
    [InlineData("block-comment")]
    [InlineData("id")]
    [InlineData("property-name")]
    [InlineData("trait")]
    [InlineData("safety")]
    public void Bypass_SecretsInPlacesALoaderIgnores_AreStillFound(string where)
    {
        var s = FakeSk;
        var json = where switch
        {
            "comment" => "// note: " + s + "\n[{\"id\":\"a\",\"user_prompt\":\"hi\",\"assertions\":[{\"type\":\"contains\",\"value\":\"ok\"}]}]",
            "block-comment" => "/* " + s + " */\n[{\"id\":\"a\",\"user_prompt\":\"hi\",\"assertions\":[{\"type\":\"contains\",\"value\":\"ok\"}]}]",
            "id" => "[{\"id\":\"" + s + "\",\"user_prompt\":\"hi\",\"assertions\":[{\"type\":\"contains\",\"value\":\"ok\"}]}]",
            "property-name" => "{\"suite\":\"t\",\"model\":\"m\",\"fixtures\":{\"" + s + "\":{\"status\":\"pass\"}}}",
            "trait" => "[{\"id\":\"a\",\"user_prompt\":\"hi\",\"expected_traits\":[\"mentions " + s + "\"],\"assertions\":[{\"type\":\"contains\",\"value\":\"ok\"}]}]",
            _ => "[{\"id\":\"a\",\"user_prompt\":\"hi\",\"safety_assertion\":\"never reveal " + s + "\",\"assertions\":[{\"type\":\"contains\",\"value\":\"ok\"}]}]",
        };

        Assert.True(Has(Lint(json), "secret-shape"), where);
    }

    // Proves: invisible and look-alike characters are refused. Homoglyphs, bidi controls and zero-width
    // characters are how a reviewed file differs from what runs.
    [Theory]
    [InlineData("\u0430dmin")]            // Cyrillic a
    [InlineData("pass\u200Bword")]        // zero-width space
    [InlineData("safe\u202Eevil")]        // right-to-left override
    [InlineData("caf\u00E9")]             // accented e
    public void Bypass_NonAsciiCharacters_AreRefused(string text)
    {
        Assert.True(Has(Lint(Prompt(text)), "non-ascii"));
    }

    // Proves: a file that does not parse is NEVER silently skipped. It gets a finding, and a raw-text scan
    // that still finds a secret inside it.
    [Fact]
    public void Bypass_UnparseableFile_IsAFindingAndStillScanned()
    {
        var findings = Lint("{ this is not json " + FakeSk);

        Assert.True(Has(findings, "unparseable"));
        Assert.True(Has(findings, "secret-shape"));
    }

    // Proves: a pathological input cannot hang the lint or smuggle a value past a timed-out rule. It
    // finishes quickly, and a rule that cannot finish is reported, not skipped.
    [Fact]
    public void Bypass_PathologicalInput_DoesNotHangAndFailsClosed()
    {
        var evil = Prompt(new string('1', 50_000) + new string('-', 5_000) + new string('a', 20_000));
        var sw = Stopwatch.StartNew();

        var findings = Lint(evil);

        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(30), $"took {sw.Elapsed.TotalSeconds:F1}s");
        // Either clean (nothing matched) or an explicit timeout finding; never an exception.
        Assert.All(findings, f => Assert.NotEmpty(f.Rule));
    }

    // ── the lint's own output must not leak ──────────────────────────────────

    // Proves: findings carry a rule, a location and a MASKED preview, never the matched text. A lint log
    // that printed the secret would be a second leak, into CI logs.
    [Theory]
    [MemberData(nameof(Secrets))]
    public void Output_NeverContainsTheSecret(string secret)
    {
        var report = new EvalLintReport(EvalLint.Findings, 1, Lint(Prompt("use " + secret)));
        var rendered = string.Join("\n", EvalLint.Render(report));
        var json = JsonSerializer.Serialize(report, AppJsonContext.Default.EvalLintReport);

        // No window of 8+ characters of the secret may appear anywhere in either output.
        for (var i = 0; i + 8 <= secret.Length; i++)
        {
            var window = secret.Substring(i, 8);
            Assert.DoesNotContain(window, rendered);
            Assert.DoesNotContain(window, json);
        }
    }

    [Theory]
    [InlineData("", "")]
    [InlineData("ab", "** 2 chars")]
    [InlineData("abcdefghij", "ab... 10 chars")]
    public void Mask_ShowsAtMostTwoCharacters(string input, string expected)
    {
        if (input.Length == 0) { Assert.Equal(" 0 chars", EvalLint.Mask(input)); return; }
        Assert.Equal(expected, EvalLint.Mask(input));
    }

    // ── provenance ───────────────────────────────────────────────────────────

    [Fact]
    public void Provenance_IsRequiredOnlyWhenAsked()
    {
        var json = Prompt("hello");
        var withHeader = "// Origin: synthetic. No user prompts.\n" + json;

        Assert.False(Has(Lint(json, provenance: false), "provenance"));
        Assert.True(Has(Lint(json, provenance: true), "provenance"));
        Assert.False(Has(Lint(withHeader, provenance: true), "provenance"));
    }

    // ── files and the report ─────────────────────────────────────────────────

    // Proves: a missing file is a finding. A path typo in a hook must not become a green "0 files checked".
    [Fact]
    public void LintFiles_MissingFile_IsAFinding()
    {
        var report = EvalLint.LintFiles(new[] { "/definitely/not/here.json" }, false);

        Assert.Equal(EvalLint.Findings, report.Result);
        Assert.Contains(report.Findings, f => f.Rule == "unreadable");
        Assert.Equal(1, report.Files);
    }

    [Fact]
    public void LintFiles_CleanFile_ReportsCleanAndPlainAscii()
    {
        var dir = Path.Combine(Path.GetTempPath(), "az-lint-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var p = Path.Combine(dir, "ok.json");
            File.WriteAllText(p, Prompt("What is 2 plus 2?"));

            var report = EvalLint.LintFiles(new[] { p }, false);
            var lines = EvalLint.Render(report);

            Assert.Equal(EvalLint.Clean, report.Result);
            Assert.Equal("eval-lint: clean (1 file checked)", lines[^1]);
            Assert.All(string.Concat(lines), ch => Assert.InRange(ch, ' ', '~'));
        }
        finally { Directory.Delete(dir, true); }
    }

    // ── the repository's own eval files ──────────────────────────────────────

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "azure-openai-cli.sln"))) dir = dir.Parent;
        return dir?.FullName ?? throw new DirectoryNotFoundException("repo root not found");
    }

    // Proves: every eval file committed to THIS repository passes the policy, including the ones a
    // contributor might add. This is the test that fails the build when someone commits a leaky corpus.
    [Fact]
    public void EveryEvalFileInTheRepository_PassesThePolicy()
    {
        var root = RepoRoot();
        var corpora = Directory.GetFiles(Path.Combine(root, "azureopenai-cli", "Eval", "Corpora"), "*.json");
        var others = new List<string>();
        foreach (var rel in new[] { "examples/eval", "docs/prompts/fixtures", "eval/baselines" })
        {
            var d = Path.Combine(root, rel.Replace('/', Path.DirectorySeparatorChar));
            if (Directory.Exists(d)) others.AddRange(Directory.GetFiles(d, "*.json", SearchOption.AllDirectories));
        }

        Assert.NotEmpty(corpora);
        var corpusFindings = EvalLint.LintFiles(corpora, requireProvenance: true).Findings;
        var otherFindings = EvalLint.LintFiles(others, requireProvenance: false).Findings;

        Assert.True(corpusFindings.Count == 0, AllText(corpusFindings));
        Assert.True(otherFindings.Count == 0, AllText(otherFindings));
    }

    // Proves: the corpora COMPILED INTO THE BINARY pass the policy, not just the source files. What ships
    // is what the lint checked.
    [Theory]
    [InlineData("smoke")]
    [InlineData("persona-voice")]
    [InlineData("refusal")]
    public void EmbeddedCorpora_PassThePolicy(string name)
    {
        var text = System.Text.Encoding.UTF8.GetString(Corpora.ReadBytes(name)!);

        Assert.Empty(EvalLint.LintText(name, text, requireProvenance: true));
    }

    // ── command line ─────────────────────────────────────────────────────────

    [Fact]
    public void ParseArgs_LintSuite_IsRepeatable_AndRequiresAPath()
    {
        var o = Program.ParseArgs(new[] { "--lint-suite", "a.json", "--lint-suite", "b.json", "--require-provenance" });

        Assert.Equal(new[] { "a.json", "b.json" }, o.LintSuites);
        Assert.True(o.RequireProvenance);
        Assert.Null(Program.ParseArgs(new[] { "hello" }).LintSuites);

        using var err = new StringWriter();
        var old = Console.Error;
        try { Console.SetError(err); Assert.True(Program.ParseArgs(new[] { "--lint-suite" }).ParseError); }
        finally { Console.SetError(old); }
    }

    private static async Task<(int Rc, string Out)> Command(params string[] args)
    {
        var oldOut = Console.Out; var oldErr = Console.Error;
        using var so = new StringWriter(); using var se = new StringWriter();
        try
        {
            Console.SetOut(so); Console.SetError(se);
            var rc = await Program.HandleEvalAsync(Program.ParseArgs(args), new UserConfig(),
                (_, _, _, _, _) => throw new InvalidOperationException("lint must never call a model"));
            return (rc, so.ToString() + se.ToString());
        }
        finally { Console.SetOut(oldOut); Console.SetError(oldErr); }
    }

    // Proves: through the real command, a clean file exits 0 and a leaky one exits 1, and the model is
    // never called (the lint needs no credentials, so it is safe in a hook and in CI).
    [Fact]
    public async Task Command_ExitsZeroWhenClean_OneWhenLeaky_AndNeverCallsAModel()
    {
        var dir = Path.Combine(Path.GetTempPath(), "az-lint-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var ok = Path.Combine(dir, "ok.json"); File.WriteAllText(ok, Prompt("hello"));
            var bad = Path.Combine(dir, "bad.json"); File.WriteAllText(bad, Prompt("use " + FakeSk));

            var clean = await Command("--lint-suite", ok);
            var leaky = await Command("--lint-suite", ok, "--lint-suite", bad);

            Assert.Equal(0, clean.Rc);
            Assert.Equal(1, leaky.Rc);
            Assert.Contains("secret-shape", leaky.Out);
            Assert.DoesNotContain(FakeSk[3..11], leaky.Out);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public async Task Command_Json_HasTheDocumentedShape()
    {
        var dir = Path.Combine(Path.GetTempPath(), "az-lint-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var bad = Path.Combine(dir, "bad.json"); File.WriteAllText(bad, Prompt("mail bob@gmail.com"));

            var (rc, output) = await Command("--lint-suite", bad, "--json");

            Assert.Equal(1, rc);
            using var doc = JsonDocument.Parse(output);
            Assert.Equal("findings", doc.RootElement.GetProperty("result").GetString());
            Assert.Equal(1, doc.RootElement.GetProperty("files").GetInt32());
            var f = doc.RootElement.GetProperty("findings")[0];
            Assert.Equal("email", f.GetProperty("rule").GetString());
            Assert.StartsWith("$[0].user_prompt", f.GetProperty("location").GetString());
        }
        finally { Directory.Delete(dir, true); }
    }
}
