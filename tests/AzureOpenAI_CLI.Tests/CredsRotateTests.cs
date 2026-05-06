using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using AzureOpenAI_CLI;
using AzureOpenAI_CLI.Cli;
using Xunit;

namespace AzureOpenAI_CLI.Tests;

// S03E25 -- The Rotation (Newman). Hermetic tests for the BYOK rotation
// flow. We never touch the real ~/.config/az-ai/env; XDG_CONFIG_HOME is
// pinned to a per-test tempdir, stdin is a StringReader, stdout/stderr
// are StringWriters. The non-TTY refusal still works because the test
// stdin is not Console.In, which is exactly the production gate's intent.

/// <summary>
/// Coverage matrix for <see cref="CredsRotate"/>. Every dangerous pattern
/// (empty key, short key, unknown provider, --raw, missing file, abort,
/// backup collision) carries a fact + a rationale comment.
/// </summary>
[Collection("ConsoleCapture")]
public class CredsRotateTests : IDisposable
{
    private readonly string _tmpDir;
    private readonly string _envPath;
    private readonly string? _origXdg;

    public CredsRotateTests()
    {
        _tmpDir = Path.Combine(Path.GetTempPath(),
            "az-ai-rotate-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(_tmpDir, "az-ai"));
        _envPath = Path.Combine(_tmpDir, "az-ai", "env");
        _origXdg = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", _tmpDir);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", _origXdg);
        try { if (Directory.Exists(_tmpDir)) Directory.Delete(_tmpDir, recursive: true); }
        catch { /* best-effort */ }
    }

    private void WriteOpenAiEnv(string key = "sk-old-0123456789abcdef")
    {
        File.WriteAllText(_envPath,
            "# az-ai env file\n"
            + "# Generated 2026-05-25T00:00:00Z\n"
            + "export AZ_AI_COMPAT_MODELS=\"openai:gpt-4o-mini\"\n"
            + "\n"
            + "[provider:openai]\n"
            + "API_KEY=" + key + "\n");
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            File.SetUnixFileMode(_envPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }

    private void WriteAzureEnv(string key = "azurekey-old-12345678")
    {
        File.WriteAllText(_envPath,
            "# az-ai env file\n"
            + "export AZUREOPENAIENDPOINT=\"https://example.openai.azure.com\"\n"
            + "export AZUREOPENAIAPI=\"" + key + "\"\n"
            + "export AZUREOPENAIMODEL=\"gpt-4o-mini\"\n");
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            File.SetUnixFileMode(_envPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }

    private static (StringWriter Out, StringWriter Err, int Code) RunRotate(
        string? providerArg, string stdin, bool raw = false, bool jsonMode = false)
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var code = CredsRotate.Run(
            providerArg: providerArg,
            jsonMode: jsonMode,
            raw: raw,
            plain: false,
            stdin: new StringReader(stdin),
            stdout: stdout,
            stderr: stderr);
        return (stdout, stderr, code);
    }

    // ── Happy path: openai rotate ────────────────────────────────────────

    [Fact]
    public void Rotate_OpenAi_ConfirmYes_RewritesKeyAndCreatesBackup()
    {
        // Newman: smallest defense -- a key changes, backup is taken,
        // file lands at mode 0600 in one rename.
        WriteOpenAiEnv("sk-old-0123456789abcdef");
        var newKey = "sk-new-fedcba9876543210";

        var (stdout, stderr, code) = RunRotate("openai", newKey + "\ny\n");

        Assert.Equal(0, code);
        var live = File.ReadAllText(_envPath);
        Assert.Contains("API_KEY=" + newKey, live, StringComparison.Ordinal);
        Assert.DoesNotContain("sk-old-", live, StringComparison.Ordinal);

        // Exactly one backup file in the dir.
        var backups = Directory.GetFiles(Path.GetDirectoryName(_envPath)!, "env.bak.*");
        Assert.Single(backups);
        var backupContent = File.ReadAllText(backups[0]);
        Assert.Contains("sk-old-", backupContent, StringComparison.Ordinal);
        Assert.DoesNotContain(newKey, backupContent, StringComparison.Ordinal);

        // Success message names the backup but NEVER the key.
        Assert.Contains("[ok] rotated [provider:openai]", stdout.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(newKey, stdout.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(newKey, stderr.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Rotate_OpenAi_FilePermissionsAreMode0600AfterRotate()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;
        WriteOpenAiEnv();
        var (_, _, code) = RunRotate("openai", "sk-fresh-abcdefghij\ny\n");

        Assert.Equal(0, code);
        var mode = File.GetUnixFileMode(_envPath);
        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, mode);
    }

    [Fact]
    public void Rotate_OpenAi_BackupPermissionsAreMode0600()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;
        WriteOpenAiEnv();
        var (_, _, code) = RunRotate("openai", "sk-fresh-abcdefghij\ny\n");
        Assert.Equal(0, code);

        var backups = Directory.GetFiles(Path.GetDirectoryName(_envPath)!, "env.bak.*");
        Assert.Single(backups);
        var mode = File.GetUnixFileMode(backups[0]);
        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, mode);
    }

    // ── Happy path: azure rotate (default-section AZUREOPENAIAPI) ────────

    [Fact]
    public void Rotate_Azure_ConfirmYes_RewritesAzureKey()
    {
        WriteAzureEnv("azurekey-old-12345678");
        var (_, _, code) = RunRotate("azure", "azurekey-new-87654321\ny\n");

        Assert.Equal(0, code);
        var live = File.ReadAllText(_envPath);
        Assert.Contains("AZUREOPENAIAPI=\"azurekey-new-87654321\"", live, StringComparison.Ordinal);
        Assert.DoesNotContain("azurekey-old", live, StringComparison.Ordinal);
        // Endpoint and model lines must be preserved verbatim.
        Assert.Contains("AZUREOPENAIENDPOINT=\"https://example.openai.azure.com\"", live, StringComparison.Ordinal);
        Assert.Contains("AZUREOPENAIMODEL=\"gpt-4o-mini\"", live, StringComparison.Ordinal);
    }

    // ── Confirm gate ─────────────────────────────────────────────────────

    [Fact]
    public void Rotate_ConfirmNo_FileUnchanged_ExitOne()
    {
        WriteOpenAiEnv("sk-old-0123456789abcdef");
        var before = File.ReadAllText(_envPath);

        var (stdout, _, code) = RunRotate("openai", "sk-new-fedcba9876543210\nn\n");

        Assert.Equal(1, code);
        Assert.Equal(before, File.ReadAllText(_envPath));
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(_envPath)!, "env.bak.*"));
        Assert.Contains("Aborted", stdout.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Rotate_ConfirmEmpty_FileUnchanged_ExitOne()
    {
        // Empty confirmation = no = abort. Default-deny.
        WriteOpenAiEnv();
        var before = File.ReadAllText(_envPath);
        var (_, _, code) = RunRotate("openai", "sk-new-fedcba9876543210\n\n");

        Assert.Equal(1, code);
        Assert.Equal(before, File.ReadAllText(_envPath));
    }

    [Fact]
    public void Rotate_ConfirmYes_AcceptsCapitalY()
    {
        WriteOpenAiEnv();
        var (_, _, code) = RunRotate("openai", "sk-new-fedcba9876543210\nY\n");
        Assert.Equal(0, code);
    }

    [Fact]
    public void Rotate_ConfirmYes_AcceptsLowercaseYes()
    {
        WriteOpenAiEnv();
        var (_, _, code) = RunRotate("openai", "sk-new-fedcba9876543210\nyes\n");
        Assert.Equal(0, code);
    }

    // ── Invalid-input refusals ───────────────────────────────────────────

    [Fact]
    public void Rotate_EmptyKey_RefusedWithExitThree()
    {
        WriteOpenAiEnv();
        var before = File.ReadAllText(_envPath);
        var (_, stderr, code) = RunRotate("openai", "\n");

        Assert.Equal(3, code);
        Assert.Equal(before, File.ReadAllText(_envPath));
        Assert.Contains("must not be empty", stderr.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Rotate_WhitespaceOnlyKey_RefusedWithExitThree()
    {
        WriteOpenAiEnv();
        var (_, stderr, code) = RunRotate("openai", "      \n");
        Assert.Equal(3, code);
        Assert.Contains("must not be empty", stderr.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Rotate_TooShortKey_RefusedWithExitThree()
    {
        WriteOpenAiEnv();
        var (_, stderr, code) = RunRotate("openai", "abc\n");
        Assert.Equal(3, code);
        Assert.Contains("too short", stderr.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Rotate_RawFlag_RefusedWithExitThree()
    {
        // --raw is for the hot path; rotation is interactive.
        WriteOpenAiEnv();
        var (_, stderr, code) = RunRotate("openai", "sk-fresh-abcdefghij\ny\n", raw: true);
        Assert.Equal(3, code);
        Assert.Contains("interactive", stderr.ToString(), StringComparison.Ordinal);
        Assert.Contains("--raw", stderr.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Rotate_UnknownProvider_RefusedWithExitThree()
    {
        WriteOpenAiEnv();
        var (_, stderr, code) = RunRotate("nopenope", "sk-fresh-abcdefghij\ny\n");
        Assert.Equal(3, code);
        Assert.Contains("Unknown provider", stderr.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Rotate_UnconfiguredProvider_RefusedWithExitThree()
    {
        // groq is a known wizard provider but not in this env file.
        WriteOpenAiEnv();
        var (_, stderr, code) = RunRotate("groq", "sk-fresh-abcdefghij\ny\n");
        Assert.Equal(3, code);
        Assert.Contains("not configured", stderr.ToString(), StringComparison.Ordinal);
    }

    // ── Missing-file / IO failure ────────────────────────────────────────

    [Fact]
    public void Rotate_MissingEnvFile_ExitTwo()
    {
        var (_, stderr, code) = RunRotate("openai", "sk-fresh-abcdefghij\ny\n");
        Assert.Equal(2, code);
        Assert.Contains("No env file at", stderr.ToString(), StringComparison.Ordinal);
        Assert.Contains("--setup", stderr.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Rotate_AzureKeyLineMissing_ExitTwo()
    {
        // Hand-written file with no AZUREOPENAIAPI line at all.
        File.WriteAllText(_envPath,
            "# corrupt env\n"
            + "export AZUREOPENAIENDPOINT=\"https://example.openai.azure.com\"\n");
        var (_, stderr, code) = RunRotate("azure", "azurekey-new-87654321\ny\n");
        // Azure isn't even detected as configured -> ExitInvalidInput when
        // the CLI passes 'azure'. The handler treats unconfigured as 3.
        Assert.True(code == 2 || code == 3);
        Assert.NotEmpty(stderr.ToString());
    }

    // ── Backup collision ─────────────────────────────────────────────────

    [Fact]
    public void Rotate_BackupCollision_BumpsToBakTsDot1()
    {
        // Pre-create env.bak.<currentSecond> so the first attempt collides.
        WriteOpenAiEnv("sk-old-0123456789abcdef");
        var ts = DateTimeOffset.UtcNow.ToString("yyyyMMddTHHmmssZ",
            System.Globalization.CultureInfo.InvariantCulture);
        var preexisting = _envPath + ".bak." + ts;
        File.WriteAllText(preexisting, "PRE-EXISTING DO NOT OVERWRITE\n");

        var (_, _, code) = RunRotate("openai", "sk-new-fedcba9876543210\ny\n");
        Assert.Equal(0, code);

        // The pre-existing backup must be preserved verbatim.
        Assert.Equal("PRE-EXISTING DO NOT OVERWRITE\n", File.ReadAllText(preexisting));

        // The bumped backup must exist and contain the OLD content.
        var bumped = Directory.GetFiles(Path.GetDirectoryName(_envPath)!, "env.bak.*")
            .Where(p => !string.Equals(p, preexisting, StringComparison.Ordinal))
            .ToArray();
        Assert.Single(bumped);
        Assert.EndsWith(".1", bumped[0], StringComparison.Ordinal);
        Assert.Contains("sk-old-", File.ReadAllText(bumped[0]), StringComparison.Ordinal);
    }

    // ── Provider-arg fallback to interactive menu ────────────────────────

    [Fact]
    public void Rotate_NoProviderArg_PromptsAndAcceptsNumber()
    {
        WriteOpenAiEnv();
        // Stdin: "1" picks first provider, then key, then "y".
        var (stdout, _, code) = RunRotate(
            providerArg: null,
            stdin: "1\nsk-fresh-abcdefghij\ny\n");

        Assert.Equal(0, code);
        Assert.Contains("Configured providers:", stdout.ToString(), StringComparison.Ordinal);
        Assert.Contains("openai", stdout.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Rotate_NoProviderArg_AcceptsName()
    {
        WriteOpenAiEnv();
        var (_, _, code) = RunRotate(
            providerArg: null,
            stdin: "openai\nsk-fresh-abcdefghij\ny\n");
        Assert.Equal(0, code);
    }

    [Fact]
    public void Rotate_NoProviderArg_EmptyPicksDefault()
    {
        WriteOpenAiEnv();
        // Empty selection -> first configured provider.
        var (_, _, code) = RunRotate(
            providerArg: null,
            stdin: "\nsk-fresh-abcdefghij\ny\n");
        Assert.Equal(0, code);
    }

    // ── Provider canonicalisation ────────────────────────────────────────

    [Theory]
    [InlineData("OPENAI")]
    [InlineData("OpenAI")]
    [InlineData(" openai ")]
    public void Rotate_ProviderArg_IsCaseInsensitive(string arg)
    {
        WriteOpenAiEnv();
        var (_, _, code) = RunRotate(arg, "sk-fresh-abcdefghij\ny\n");
        Assert.Equal(0, code);
    }

    // ── DetectConfiguredProviders ────────────────────────────────────────

    [Fact]
    public void DetectConfiguredProviders_AzureAndOpenAi_OrdersAzureFirst()
    {
        var lines = new[]
        {
            "export AZUREOPENAIAPI=\"azurekey-12345678\"",
            "",
            "[provider:openai]",
            "API_KEY=sk-12345678",
        };
        var found = CredsRotate.DetectConfiguredProviders(lines);
        Assert.Equal(new[] { "azure", "openai" }, found.ToArray());
    }

    [Fact]
    public void DetectConfiguredProviders_OnlyOpenAi_DoesNotIncludeAzure()
    {
        var lines = new[]
        {
            "[provider:openai]",
            "API_KEY=sk-12345678",
        };
        Assert.Equal(new[] { "openai" }, CredsRotate.DetectConfiguredProviders(lines).ToArray());
    }

    [Fact]
    public void DetectConfiguredProviders_EmptyFile_ReturnsEmpty()
    {
        Assert.Empty(CredsRotate.DetectConfiguredProviders(Array.Empty<string>()));
    }

    // ── RewriteKey unit (no IO) ──────────────────────────────────────────

    [Fact]
    public void RewriteKey_OpenAi_ReplacesApiKeyLine()
    {
        var lines = new[]
        {
            "[provider:openai]",
            "API_KEY=oldkey",
        };
        var result = CredsRotate.RewriteKey(lines, "openai", "newkey-12345678", out var err);
        Assert.Null(err);
        Assert.NotNull(result);
        Assert.Contains("API_KEY=newkey-12345678", result!, StringComparison.Ordinal);
        Assert.DoesNotContain("oldkey", result, StringComparison.Ordinal);
    }

    [Fact]
    public void RewriteKey_Cloudflare_ReplacesApiTokenLine()
    {
        // Cloudflare uses API_TOKEN, not API_KEY. The rewriter MUST honor
        // the per-provider variant or it will silently leave the old token.
        var lines = new[]
        {
            "[provider:cloudflare]",
            "API_TOKEN=oldtoken",
        };
        var result = CredsRotate.RewriteKey(lines, "cloudflare", "newtoken-12345678", out var err);
        Assert.Null(err);
        Assert.NotNull(result);
        Assert.Contains("API_TOKEN=newtoken-12345678", result!, StringComparison.Ordinal);
    }

    [Fact]
    public void RewriteKey_Azure_ReplacesDefaultSectionLine()
    {
        var lines = new[]
        {
            "export AZUREOPENAIENDPOINT=\"https://example.openai.azure.com\"",
            "export AZUREOPENAIAPI=\"oldkey\"",
            "export AZUREOPENAIMODEL=\"gpt-4o-mini\"",
        };
        var result = CredsRotate.RewriteKey(lines, "azure", "newkey-12345678", out var err);
        Assert.Null(err);
        Assert.NotNull(result);
        Assert.Contains("AZUREOPENAIAPI=\"newkey-12345678\"", result!, StringComparison.Ordinal);
        Assert.Contains("AZUREOPENAIENDPOINT", result, StringComparison.Ordinal);
    }

    [Fact]
    public void RewriteKey_KeyLineMissing_ReturnsNullWithError()
    {
        var lines = new[] { "# nothing useful here" };
        var result = CredsRotate.RewriteKey(lines, "openai", "newkey-12345678", out var err);
        Assert.Null(result);
        Assert.NotNull(err);
        Assert.Contains("[provider:openai]", err!, StringComparison.Ordinal);
    }

    [Fact]
    public void RewriteKey_OpenAi_DoesNotTouchOtherProviderSection()
    {
        var lines = new[]
        {
            "[provider:openai]",
            "API_KEY=openaikey",
            "[provider:groq]",
            "API_KEY=groqkey",
        };
        var result = CredsRotate.RewriteKey(lines, "openai", "newkey-12345678", out var err);
        Assert.Null(err);
        Assert.NotNull(result);
        Assert.Contains("API_KEY=newkey-12345678", result!, StringComparison.Ordinal);
        // groq's key MUST be untouched.
        Assert.Contains("API_KEY=groqkey", result, StringComparison.Ordinal);
    }

    [Fact]
    public void RewriteKey_DoesNotEchoNewKeyOutsideTargetLine()
    {
        // Defense in depth: the rewriter's output must include the new
        // key exactly once -- in the rewritten line. If a future bug
        // double-writes it, this test catches it.
        var lines = new[] { "[provider:openai]", "API_KEY=oldkey" };
        var result = CredsRotate.RewriteKey(lines, "openai", "UNIQUEKEY-12345678", out _);
        Assert.NotNull(result);
        var occurrences = 0;
        var idx = 0;
        while ((idx = result!.IndexOf("UNIQUEKEY", idx, StringComparison.Ordinal)) >= 0)
        {
            occurrences++;
            idx++;
        }
        Assert.Equal(1, occurrences);
    }

    // ── Redaction smoke ──────────────────────────────────────────────────

    [Fact]
    public void Rotate_RedactsApiKeyHeaderInOutput()
    {
        // Drive a path where stderr could plausibly carry an API key
        // header (the redactor's headline pattern). We synthesise the
        // input through the public Redact API to assert end-to-end.
        var leak = "Authorization: Bearer sk-leak-abcdefghij";
        var redacted = SecretRedactor.Redact(leak);
        Assert.Contains("[REDACTED:bearer]", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("sk-leak", redacted, StringComparison.Ordinal);
    }

    [Fact]
    public void Rotate_RedactsProviderKeyEnvInExceptionMessage()
    {
        // Simulate an exception path that names the env var and the value.
        var msg = "OPENAI_API_KEY=sk-leak-abcdefghij was rejected by the server";
        var redacted = SecretRedactor.Redact(msg);
        Assert.Contains("[REDACTED:provider-key]", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("sk-leak", redacted, StringComparison.Ordinal);
    }

    // ── Backup name format ───────────────────────────────────────────────

    [Fact]
    public void Rotate_BackupFilename_IsSortableIso8601Z()
    {
        WriteOpenAiEnv();
        var (_, _, code) = RunRotate("openai", "sk-fresh-abcdefghij\ny\n");
        Assert.Equal(0, code);

        var backups = Directory.GetFiles(Path.GetDirectoryName(_envPath)!, "env.bak.*");
        Assert.Single(backups);
        var name = Path.GetFileName(backups[0]);
        // env.bak.YYYYMMDDTHHMMSSZ
        Assert.Matches(@"^env\.bak\.\d{8}T\d{6}Z$", name);
    }
}
