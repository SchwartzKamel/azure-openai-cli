using Xunit;
using AzureOpenAI_CLI;

namespace AzureOpenAI_CLI.Tests;

/// <summary>
/// Regression tests for Unicode preservation across env-file parsing
/// and model-name splitting. Covers CJK, RTL, emoji, BOM, and mixed
/// quoting styles. Triggered by a real bug where Japanese text became
/// "???" under InvariantGlobalization.
/// </summary>
[Collection("ConsoleCapture")]
public class UnicodeEncodingTests : IDisposable
{
    // Track every env var we touch so Dispose can restore them.
    private readonly Dictionary<string, string?> _savedEnv = new();

    public void Dispose()
    {
        foreach (var kv in _savedEnv)
            Environment.SetEnvironmentVariable(kv.Key, kv.Value);
    }

    /// <summary>Save the current value of an env var before we mutate it.</summary>
    private void SaveAndClear(string name)
    {
        if (!_savedEnv.ContainsKey(name))
            _savedEnv[name] = Environment.GetEnvironmentVariable(name);
        Environment.SetEnvironmentVariable(name, null);
    }

    // ── LoadConfigEnvFrom: CJK ─────────────────────────────────────────

    [Fact]
    public void LoadConfigEnvFrom_CjkValues_PreservesUnicode()
    {
        var tmp = Path.GetTempFileName();
        try
        {
            SaveAndClear("TEST_UNI_MODEL");
            SaveAndClear("TEST_UNI_ENDPOINT");

            File.WriteAllText(tmp,
                "export TEST_UNI_MODEL=\"gpt-4o,\u6D4B\u8BD5\u6A21\u578B\"\n" +   // 测试模型
                "export TEST_UNI_ENDPOINT=\"https://example.com\"\n",
                System.Text.Encoding.UTF8);

            Program.LoadConfigEnvFrom(tmp);

            Assert.Equal("gpt-4o,\u6D4B\u8BD5\u6A21\u578B",
                         Environment.GetEnvironmentVariable("TEST_UNI_MODEL"));
            Assert.Equal("https://example.com",
                         Environment.GetEnvironmentVariable("TEST_UNI_ENDPOINT"));
        }
        finally
        {
            File.Delete(tmp);
        }
    }

    // ── LoadConfigEnvFrom: RTL (Arabic / Hebrew) ───────────────────────

    [Fact]
    public void LoadConfigEnvFrom_RtlValues_PreservesUnicode()
    {
        var tmp = Path.GetTempFileName();
        try
        {
            SaveAndClear("TEST_UNI_RTL");

            File.WriteAllText(tmp,
                "export TEST_UNI_RTL=\"\u0645\u0631\u062D\u0628\u0627\"\n",  // مرحبا
                System.Text.Encoding.UTF8);

            Program.LoadConfigEnvFrom(tmp);

            Assert.Equal("\u0645\u0631\u062D\u0628\u0627",
                         Environment.GetEnvironmentVariable("TEST_UNI_RTL"));
        }
        finally
        {
            File.Delete(tmp);
        }
    }

    // ── LoadConfigEnvFrom: Emoji ───────────────────────────────────────

    [Fact]
    public void LoadConfigEnvFrom_EmojiValues_PreservesUnicode()
    {
        var tmp = Path.GetTempFileName();
        try
        {
            SaveAndClear("TEST_UNI_EMOJI");

            File.WriteAllText(tmp,
                "export TEST_UNI_EMOJI=\"model-\U0001F680-test\"\n",  // rocket emoji
                System.Text.Encoding.UTF8);

            Program.LoadConfigEnvFrom(tmp);

            Assert.Equal("model-\U0001F680-test",
                         Environment.GetEnvironmentVariable("TEST_UNI_EMOJI"));
        }
        finally
        {
            File.Delete(tmp);
        }
    }

    // ── ParseModelEnv: Unicode model names ─────────────────────────────

    [Fact]
    public void ParseModelEnv_UnicodeModelNames_SplitsAndPreserves()
    {
        SaveAndClear("AZUREOPENAIMODEL");

        Environment.SetEnvironmentVariable("AZUREOPENAIMODEL",
            "gpt-4o,\u6A21\u578B\u540D,\u043C\u043E\u0434\u0435\u043B\u044C");
        // 模型名 (Chinese), модель (Russian)

        var (defaultModel, allowed) = Program.ParseModelEnv();

        Assert.Equal("gpt-4o", defaultModel);
        Assert.NotNull(allowed);
        Assert.Equal(3, allowed!.Count);
        Assert.Contains("\u6A21\u578B\u540D", allowed);   // 模型名
        Assert.Contains("\u043C\u043E\u0434\u0435\u043B\u044C", allowed); // модель
    }

    // ── LoadConfigEnvFrom: does not clobber existing Unicode var ───────

    [Fact]
    public void LoadConfigEnvFrom_ExistingUnicodeVar_NotOverwritten()
    {
        var tmp = Path.GetTempFileName();
        try
        {
            SaveAndClear("TEST_UNI_NOCLOBBER");

            // Set existing value with CJK content
            Environment.SetEnvironmentVariable("TEST_UNI_NOCLOBBER",
                "\u3044\u3044\u5B50");  // いい子

            File.WriteAllText(tmp,
                "export TEST_UNI_NOCLOBBER=\"overwrite_attempt\"\n",
                System.Text.Encoding.UTF8);

            Program.LoadConfigEnvFrom(tmp);

            Assert.Equal("\u3044\u3044\u5B50",
                         Environment.GetEnvironmentVariable("TEST_UNI_NOCLOBBER"));
        }
        finally
        {
            File.Delete(tmp);
        }
    }

    // ── LoadConfigEnvFrom: UTF-8 BOM prefix ────────────────────────────

    [Fact]
    public void LoadConfigEnvFrom_Utf8Bom_ParsesCorrectly()
    {
        var tmp = Path.GetTempFileName();
        try
        {
            SaveAndClear("TEST_UNI_BOM");

            // Write with BOM (UTF8 with preamble)
            File.WriteAllText(tmp,
                "export TEST_UNI_BOM=\"bom-\u6D4B\u8BD5\"\n",  // bom-测试
                new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

            Program.LoadConfigEnvFrom(tmp);

            Assert.Equal("bom-\u6D4B\u8BD5",
                         Environment.GetEnvironmentVariable("TEST_UNI_BOM"));
        }
        finally
        {
            File.Delete(tmp);
        }
    }

    // ── LoadConfigEnvFrom: mixed quoting styles with Unicode ───────────

    [Theory]
    [InlineData("export TEST_UNI_MQ=\"\u4F60\u597D\"", "\u4F60\u597D")]   // double-quoted: 你好
    [InlineData("export TEST_UNI_MQ='\u4F60\u597D'", "\u4F60\u597D")]   // single-quoted: 你好
    [InlineData("export TEST_UNI_MQ=\u4F60\u597D", "\u4F60\u597D")]   // bare (unquoted): 你好
    public void LoadConfigEnvFrom_MixedQuotingUnicode_PreservesValue(
        string line, string expected)
    {
        var tmp = Path.GetTempFileName();
        try
        {
            SaveAndClear("TEST_UNI_MQ");

            File.WriteAllText(tmp, line + "\n", System.Text.Encoding.UTF8);

            Program.LoadConfigEnvFrom(tmp);

            Assert.Equal(expected,
                         Environment.GetEnvironmentVariable("TEST_UNI_MQ"));
        }
        finally
        {
            File.Delete(tmp);
        }
    }

    // ── Supplementary plane (astral) characters ────────────────────────

    [Fact]
    public void LoadConfigEnvFrom_SupplementaryPlane_PreservesCharacters()
    {
        var tmp = Path.GetTempFileName();
        try
        {
            SaveAndClear("TEST_UNI_ASTRAL");

            // U+1F600 (grinning face) + U+20000 (CJK Unified Ext B)
            File.WriteAllText(tmp,
                "export TEST_UNI_ASTRAL=\"\U0001F600\U00020000\"\n",
                System.Text.Encoding.UTF8);

            Program.LoadConfigEnvFrom(tmp);

            Assert.Equal("\U0001F600\U00020000",
                         Environment.GetEnvironmentVariable("TEST_UNI_ASTRAL"));
        }
        finally
        {
            File.Delete(tmp);
        }
    }
}
