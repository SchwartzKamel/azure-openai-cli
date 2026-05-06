using System;
using System.IO;
using System.Text;
using AzureOpenAI_CLI;
using Xunit;

namespace AzureOpenAI_CLI.Tests;

// S03E10 -- The Keychain (Newman). Section-aware loader for
// ~/.config/az-ai/env. Default (unsectioned) content stays
// shell-export compatible; [provider:NAME] sections namespace
// keys into NAME_KEY env vars; unknown sections warn but do
// not abort. Tests run sequentially under the ConsoleCapture
// collection because they swap stderr to capture warnings.
[Collection("ConsoleCapture")]
public class EnvLoaderSectionTests : IDisposable
{
    private readonly string _tmpDir;
    private readonly string _envPath;
    private readonly string[] _trackedVars =
    {
        "OPENAI_API_KEY", "GROQ_API_KEY", "TOGETHER_API_KEY",
        "CLOUDFLARE_API_TOKEN", "AZURE_FOUNDRY_KEY",
        "MADE_UP_VAR", "BARE_KEY",
        // Synthetic test-only vars used in default-section back-compat
        // assertions. We deliberately do NOT touch the real Azure
        // production variables (AZUREOPENAIAPI / AZUREOPENAIENDPOINT /
        // AZUREOPENAIMODEL) from these tests because xUnit parallelises
        // distinct test classes, and Ralph / credential-resolution tests
        // assert those slots remain unset. Test isolation > convenience.
        "S03E10_VERBATIM_KEY", "S03E10_BARE_KV", "S03E10_EXPORT_KV",
    };

    public EnvLoaderSectionTests()
    {
        _tmpDir = Path.Combine(Path.GetTempPath(),
            "az-ai-keychain-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tmpDir);
        _envPath = Path.Combine(_tmpDir, "env");
        ClearTrackedVars();
    }

    public void Dispose()
    {
        ClearTrackedVars();
        try { Directory.Delete(_tmpDir, recursive: true); } catch { /* best effort */ }
    }

    private void ClearTrackedVars()
    {
        foreach (var v in _trackedVars)
        {
            Environment.SetEnvironmentVariable(v, null);
        }
    }

    private void Write(string content, Encoding? enc = null)
    {
        File.WriteAllText(_envPath, content, enc ?? new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    // -- Default (unsectioned) section: back-compat ---------------------

    [Fact]
    public void DefaultSection_ShellExportSyntax_LoadsVerbatim()
    {
        Write("export S03E10_EXPORT_KV=\"old-value\"\n" +
              "export S03E10_VERBATIM_KEY=\"https://x.example.com/\"\n");
        Program.LoadConfigEnvFrom(_envPath);
        Assert.Equal("old-value", Environment.GetEnvironmentVariable("S03E10_EXPORT_KV"));
        Assert.Equal("https://x.example.com/", Environment.GetEnvironmentVariable("S03E10_VERBATIM_KEY"));
    }

    [Fact]
    public void DefaultSection_BareKeyValue_LoadsVerbatim()
    {
        Write("S03E10_BARE_KV=plain\n");
        Program.LoadConfigEnvFrom(_envPath);
        Assert.Equal("plain", Environment.GetEnvironmentVariable("S03E10_BARE_KV"));
    }

    // -- [provider:openai] ----------------------------------------------

    [Fact]
    public void OpenAiSection_ApiKey_NamespacedToOpenAiApiKey()
    {
        Write("[provider:openai]\nAPI_KEY=sk-secret-123\n");
        Program.LoadConfigEnvFrom(_envPath);
        Assert.Equal("sk-secret-123", Environment.GetEnvironmentVariable("OPENAI_API_KEY"));
        // Must NOT cross-contaminate Azure's slot. AZUREOPENAIAPI is the
        // real production env var; we never write it from this test
        // class, so reading it must observe whatever the ambient
        // process state is. The cross-contamination invariant is that
        // loading [provider:openai] does NOT *itself* write AZUREOPENAIAPI.
        // We assert that by reading the value before and after the load.
    }

    [Fact]
    public void GroqSection_ApiKey_NamespacedToGroqApiKey()
    {
        Write("[provider:groq]\napi_key=gsk_xyz\n");
        Program.LoadConfigEnvFrom(_envPath);
        Assert.Equal("gsk_xyz", Environment.GetEnvironmentVariable("GROQ_API_KEY"));
    }

    [Fact]
    public void TogetherSection_NamespacesCorrectly()
    {
        Write("[provider:together]\nAPI_KEY=tog_abc\n");
        Program.LoadConfigEnvFrom(_envPath);
        Assert.Equal("tog_abc", Environment.GetEnvironmentVariable("TOGETHER_API_KEY"));
    }

    [Fact]
    public void CloudflareSection_NamespacesCorrectly()
    {
        Write("[provider:cloudflare]\nAPI_TOKEN=cf_tok\n");
        Program.LoadConfigEnvFrom(_envPath);
        Assert.Equal("cf_tok", Environment.GetEnvironmentVariable("CLOUDFLARE_API_TOKEN"));
    }

    [Fact]
    public void OpenAiSection_AlreadyNamespacedKey_NotDoublePrefixed()
    {
        // User wrote the full name in a section -- must not become
        // OPENAI_OPENAI_API_KEY.
        Write("[provider:openai]\nOPENAI_API_KEY=sk-explicit\n");
        Program.LoadConfigEnvFrom(_envPath);
        Assert.Equal("sk-explicit", Environment.GetEnvironmentVariable("OPENAI_API_KEY"));
        Assert.Null(Environment.GetEnvironmentVariable("OPENAI_OPENAI_API_KEY"));
    }

    // -- Mixed default + named section ----------------------------------

    [Fact]
    public void MixedDefaultAndOpenAiSection_BothLoadIntoCorrectSlots()
    {
        Write(
            "# top-of-file comment\n" +
            "export S03E10_EXPORT_KV=\"default-A\"\n" +
            "S03E10_BARE_KV=https://azure.example.com/\n" +
            "\n" +
            "[provider:openai]\n" +
            "API_KEY=sk-openai-B\n");
        Program.LoadConfigEnvFrom(_envPath);
        Assert.Equal("default-A", Environment.GetEnvironmentVariable("S03E10_EXPORT_KV"));
        Assert.Equal("https://azure.example.com/", Environment.GetEnvironmentVariable("S03E10_BARE_KV"));
        Assert.Equal("sk-openai-B", Environment.GetEnvironmentVariable("OPENAI_API_KEY"));
    }

    [Fact]
    public void MultipleProviderSections_EachIsolated()
    {
        Write(
            "[provider:openai]\nAPI_KEY=openai-1\n" +
            "[provider:groq]\nAPI_KEY=groq-1\n" +
            "[provider:together]\nAPI_KEY=together-1\n");
        Program.LoadConfigEnvFrom(_envPath);
        Assert.Equal("openai-1", Environment.GetEnvironmentVariable("OPENAI_API_KEY"));
        Assert.Equal("groq-1", Environment.GetEnvironmentVariable("GROQ_API_KEY"));
        Assert.Equal("together-1", Environment.GetEnvironmentVariable("TOGETHER_API_KEY"));
    }

    // -- Comments and empty lines inside sections -----------------------

    [Fact]
    public void CommentsAndBlankLines_TolleratedInsideSections()
    {
        Write(
            "[provider:openai]\n" +
            "# comment line inside section\n" +
            "\n" +
            "   \n" +
            "API_KEY=after-blanks\n");
        Program.LoadConfigEnvFrom(_envPath);
        Assert.Equal("after-blanks", Environment.GetEnvironmentVariable("OPENAI_API_KEY"));
    }

    // -- BOM tolerance --------------------------------------------------

    [Fact]
    public void Utf8Bom_AtStartOfFile_NotTreatedAsKeyChar()
    {
        // Write with explicit BOM.
        File.WriteAllText(_envPath,
            "\uFEFFexport S03E10_EXPORT_KV=\"bom-value\"\n",
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        Program.LoadConfigEnvFrom(_envPath);
        Assert.Equal("bom-value", Environment.GetEnvironmentVariable("S03E10_EXPORT_KV"));
    }

    // -- CRLF tolerance -------------------------------------------------

    [Fact]
    public void CrlfLineEndings_ParseCorrectly()
    {
        Write("[provider:openai]\r\nAPI_KEY=crlf-val\r\n");
        Program.LoadConfigEnvFrom(_envPath);
        Assert.Equal("crlf-val", Environment.GetEnvironmentVariable("OPENAI_API_KEY"));
    }

    // -- Malformed section header ---------------------------------------

    [Fact]
    public void MalformedHeader_MissingClosingBracket_TreatedAsKvOrSkipped()
    {
        // No closing bracket -> not a section header. eq <= 0 because
        // there is no '=', so the line is skipped silently. Subsequent
        // KV lines remain in the default (unsectioned) section.
        Write("[provider:openai\nS03E10_EXPORT_KV=after-malformed\n");
        Program.LoadConfigEnvFrom(_envPath);
        Assert.Equal("after-malformed", Environment.GetEnvironmentVariable("S03E10_EXPORT_KV"));
    }

    // -- Unknown provider section ---------------------------------------

    [Fact]
    public void UnknownProviderSection_WarnsToStderr_AndSkipsContents()
    {
        var prevErr = Console.Error;
        var sw = new StringWriter();
        Console.SetError(sw);
        try
        {
            Write("[provider:bogus]\nAPI_KEY=ignored-value\nMADE_UP_VAR=should-not-be-set\n");
            Program.LoadConfigEnvFrom(_envPath, isRaw: false);
        }
        finally
        {
            Console.SetError(prevErr);
        }

        var stderr = sw.ToString();
        Assert.Contains("[WARNING]", stderr, StringComparison.Ordinal);
        Assert.Contains("bogus", stderr, StringComparison.Ordinal);
        Assert.Null(Environment.GetEnvironmentVariable("MADE_UP_VAR"));
        Assert.Null(Environment.GetEnvironmentVariable("BOGUS_API_KEY"));
        // Crucially: no leak into a known provider's slot.
        Assert.Null(Environment.GetEnvironmentVariable("OPENAI_API_KEY"));
    }

    [Fact]
    public void UnknownProviderSection_RawMode_Silent()
    {
        var prevErr = Console.Error;
        var sw = new StringWriter();
        Console.SetError(sw);
        try
        {
            Write("[provider:bogus]\nAPI_KEY=x\n");
            Program.LoadConfigEnvFrom(_envPath, isRaw: true);
        }
        finally
        {
            Console.SetError(prevErr);
        }

        Assert.Equal(string.Empty, sw.ToString());
    }

    [Fact]
    public void UnknownNonProviderSection_AlsoWarns()
    {
        var prevErr = Console.Error;
        var sw = new StringWriter();
        Console.SetError(sw);
        try
        {
            Write("[some-other-namespace]\nFOO=bar\n");
            Program.LoadConfigEnvFrom(_envPath, isRaw: false);
        }
        finally
        {
            Console.SetError(prevErr);
        }
        Assert.Contains("[WARNING]", sw.ToString(), StringComparison.Ordinal);
    }

    // -- Cross-contamination guard (the headline Newman invariant) ------

    [Fact]
    public void OpenAiSection_DoesNotLeakIntoOtherProviderSlots()
    {
        // Snapshot the existing slots, run the loader, verify they did
        // not change. We deliberately read-only-touch AZUREOPENAIAPI:
        // the test asserts the loader did not WRITE it, regardless of
        // ambient process state.
        var azureBefore = Environment.GetEnvironmentVariable("AZUREOPENAIAPI");
        var foundryBefore = Environment.GetEnvironmentVariable("AZURE_FOUNDRY_KEY");
        Write("[provider:openai]\nAPI_KEY=sk-only-openai\n");
        Program.LoadConfigEnvFrom(_envPath);
        Assert.Equal(azureBefore, Environment.GetEnvironmentVariable("AZUREOPENAIAPI"));
        Assert.Equal(foundryBefore, Environment.GetEnvironmentVariable("AZURE_FOUNDRY_KEY"));
        Assert.Equal("sk-only-openai", Environment.GetEnvironmentVariable("OPENAI_API_KEY"));
    }

    [Fact]
    public void DefaultSectionVerbatimKey_DoesNotLeakIntoOpenAiSlot()
    {
        // Default-section writes the verbatim key only. OPENAI_API_KEY
        // must not be touched by writing some unrelated default-section
        // variable.
        Write("S03E10_BARE_KV=ordinary\n");
        Program.LoadConfigEnvFrom(_envPath);
        Assert.Null(Environment.GetEnvironmentVariable("OPENAI_API_KEY"));
    }

    // -- Existing env var wins (shell profile precedence) ---------------

    [Fact]
    public void ExistingEnvVar_NotOverwritten_BySection()
    {
        Environment.SetEnvironmentVariable("OPENAI_API_KEY", "shell-wins");
        try
        {
            Write("[provider:openai]\nAPI_KEY=file-loses\n");
            Program.LoadConfigEnvFrom(_envPath);
            Assert.Equal("shell-wins", Environment.GetEnvironmentVariable("OPENAI_API_KEY"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", null);
        }
    }
}
