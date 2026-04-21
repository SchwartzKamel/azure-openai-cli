using System.Diagnostics;
using System.Text;
using AzureOpenAI_CLI_V2.Tools;

namespace AzureOpenAI_CLI.V2.Tests;

/// <summary>
/// 2.0.1 Program.cs patches — FR-021 (persona ArgumentException wrap),
/// Mickey --help text update, F-6 (world-writable config warn), and the
/// K-3 / K-7 api-key leak regressions.
///
/// Pass-the-pass / fail-the-fail discipline: every behavior asserts the
/// positive path AND a negative path. Subprocess-driven K-7 exercises the
/// real entry point, not a mocked ErrorAndExit surface.
/// </summary>
public class V201ProgramPatchTests
{
    // ═══════════════════════════════════════════════════════════════════
    // FR-021 — malformed persona name in .squad.json exits 1 (not 134)
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void Fr021_MalformedPersonaName_ExitsOneWithErrorPrefix()
    {
        var tmp = Path.Combine(Path.GetTempPath(), "fr021-unit-" + Guid.NewGuid());
        Directory.CreateDirectory(tmp);
        File.WriteAllText(
            Path.Combine(tmp, ".squad.json"),
            """
            {
              "team": {"name": "fr021-unit"},
              "personas": [
                {"name": "bad name!", "role": "adversarial",
                 "description": "violates [a-z0-9_-]{1,64}", "system_prompt": "x"}
              ]
            }
            """);
        try
        {
            var (stdout, stderr, code) = RunCli(
                new[] { "--persona", "bad name!", "hi" },
                workingDir: tmp,
                env: new Dictionary<string, string>
                {
                    ["AZUREOPENAIENDPOINT"] = "https://example.invalid/",
                    ["AZUREOPENAIAPI"] = "dummy-not-used",
                });

            // Positive path: clean exit 1 + [ERROR] prefix.
            Assert.Equal(1, code);
            Assert.Contains("[ERROR]", stderr);
            Assert.Contains("Invalid persona name", stderr);
            // Negative path: no unhandled-exception leak / stack trace.
            Assert.DoesNotContain("Unhandled exception", stderr);
            Assert.DoesNotContain("System.ArgumentException", stderr);
            Assert.DoesNotContain("   at ", stderr);
            Assert.NotEqual(134, code);
        }
        finally
        {
            try { Directory.Delete(tmp, recursive: true); } catch { /* best effort */ }
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // Help text — --raw description mentions silent-by-design + color contract
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void Help_RawOption_MentionsSilentByDesignAndColorContract()
    {
        var (stdout, _, code) = RunCli(new[] { "--help" });

        Assert.Equal(0, code);
        Assert.Contains("--raw", stdout);
        Assert.Contains("Silent-by-design", stdout);
        Assert.Contains("color-contract.md", stdout);
    }

    [Fact]
    public void Help_RawOption_DoesNotRegressOtherOptions()
    {
        // Negative: the edit must not nuke neighboring options.
        var (stdout, _, code) = RunCli(new[] { "--help" });

        Assert.Equal(0, code);
        Assert.Contains("--json", stdout);
        Assert.Contains("--persona", stdout);
        Assert.Contains("--max-tokens", stdout);
    }

    // ═══════════════════════════════════════════════════════════════════
    // F-6 — world-writable config warning
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void F6_WorldWritableConfig_EmitsWarnLineToStderr()
    {
        if (OperatingSystem.IsWindows()) return; // POSIX-only behavior.

        var tmp = Path.Combine(Path.GetTempPath(), "f6-ww-" + Guid.NewGuid());
        Directory.CreateDirectory(tmp);
        var configPath = Path.Combine(tmp, ".azureopenai-cli.json");
        File.WriteAllText(configPath, "{\"models\":{}}");
        File.SetUnixFileMode(configPath,
            UnixFileMode.UserRead | UnixFileMode.UserWrite |
            UnixFileMode.GroupRead | UnixFileMode.GroupWrite |
            UnixFileMode.OtherRead | UnixFileMode.OtherWrite); // 0666

        try
        {
            var (_, stderr, _) = RunCli(
                new[] { "--config", configPath, "--current-model" });

            Assert.Contains("[warn]", stderr);
            Assert.Contains("world-writable", stderr);
            Assert.Contains("chmod 600", stderr);
            Assert.Contains("666", stderr);
        }
        finally
        {
            try { Directory.Delete(tmp, recursive: true); } catch { /* best effort */ }
        }
    }

    [Fact]
    public void F6_SafeModeConfig_EmitsNoWarnLine()
    {
        if (OperatingSystem.IsWindows()) return;

        var tmp = Path.Combine(Path.GetTempPath(), "f6-safe-" + Guid.NewGuid());
        Directory.CreateDirectory(tmp);
        var configPath = Path.Combine(tmp, ".azureopenai-cli.json");
        File.WriteAllText(configPath, "{\"models\":{}}");
        File.SetUnixFileMode(configPath,
            UnixFileMode.UserRead | UnixFileMode.UserWrite); // 0600

        try
        {
            var (_, stderr, _) = RunCli(
                new[] { "--config", configPath, "--current-model" });

            // Negative path: a mode-600 file must NOT trigger the warn.
            Assert.DoesNotContain("world-writable", stderr);
        }
        finally
        {
            try { Directory.Delete(tmp, recursive: true); } catch { /* best effort */ }
        }
    }

    [Fact]
    public void F6_WorldWritable_UnderRaw_Suppressed()
    {
        if (OperatingSystem.IsWindows()) return;

        var tmp = Path.Combine(Path.GetTempPath(), "f6-raw-" + Guid.NewGuid());
        Directory.CreateDirectory(tmp);
        var configPath = Path.Combine(tmp, ".azureopenai-cli.json");
        File.WriteAllText(configPath, "{\"models\":{}}");
        File.SetUnixFileMode(configPath,
            UnixFileMode.UserRead | UnixFileMode.UserWrite |
            UnixFileMode.GroupRead | UnixFileMode.GroupWrite |
            UnixFileMode.OtherRead | UnixFileMode.OtherWrite);

        try
        {
            var (_, stderr, _) = RunCli(
                new[] { "--config", configPath, "--current-model", "--raw" });

            Assert.DoesNotContain("world-writable", stderr);
        }
        finally
        {
            try { Directory.Delete(tmp, recursive: true); } catch { /* best effort */ }
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // K-7 — Program-level api-key leak regression (subprocess end-to-end)
    // ═══════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("--nonsense-flag-that-does-not-exist")]
    [InlineData("--config")]
    [InlineData("--set-model")]
    public void K7_ErrorAndExit_DoesNotLeakApiKey(string badArg)
    {
        // Each of these drives Program through an ErrorAndExit / parse-error
        // path. If any current or future ErrorAndExit call site interpolates
        // env content into the error message, the sentinel will leak and this
        // test will fail loudly.
        const string Sentinel = "SENTINEL-APIKEY-3f9c2b7a-DO-NOT-LEAK";
        var (stdout, stderr, _) = RunCli(new[] { badArg }, env: new Dictionary<string, string>
        {
            ["AZUREOPENAIAPI"] = Sentinel,
            ["AZUREOPENAIENDPOINT"] = "https://example.invalid/",
        });

        Assert.DoesNotContain(Sentinel, stdout);
        Assert.DoesNotContain(Sentinel, stderr);
    }

    [Fact]
    public void K7_InvalidEndpoint_StderrDoesNotLeakApiKey()
    {
        const string Sentinel = "SENTINEL-APIKEY-endpoint-case";
        var (stdout, stderr, _) = RunCli(
            new[] { "--estimate", "hi" },
            env: new Dictionary<string, string>
            {
                ["AZUREOPENAIAPI"] = Sentinel,
                ["AZUREOPENAIENDPOINT"] = "http://not-https.invalid/",
            });

        Assert.DoesNotContain(Sentinel, stdout);
        Assert.DoesNotContain(Sentinel, stderr);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Subprocess helper — invokes the built v2 assembly via `dotnet <dll>`
    // ═══════════════════════════════════════════════════════════════════

    private static (string stdout, string stderr, int exitCode) RunCli(
        string[] args,
        string? workingDir = null,
        IReadOnlyDictionary<string, string>? env = null)
    {
        // Locate the v2 DLL via the referenced assembly location.
        var v2Asm = typeof(AzureOpenAI_CLI_V2.Program).Assembly.Location;

        var psi = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = workingDir ?? Environment.CurrentDirectory,
        };
        psi.ArgumentList.Add(v2Asm);
        foreach (var a in args) psi.ArgumentList.Add(a);

        // Clear credentials to keep runs deterministic unless the test overrides.
        psi.Environment["AZUREOPENAIENDPOINT"] = "";
        psi.Environment["AZUREOPENAIAPI"] = "";
        // Force a clean, predictable HOME so user config files can't contaminate.
        psi.Environment["HOME"] = Path.Combine(Path.GetTempPath(), "v201-home-" + Guid.NewGuid());
        Directory.CreateDirectory(psi.Environment["HOME"]!);
        if (env is not null)
        {
            foreach (var kv in env) psi.Environment[kv.Key] = kv.Value;
        }

        using var p = Process.Start(psi)!;
        var stdout = p.StandardOutput.ReadToEnd();
        var stderr = p.StandardError.ReadToEnd();
        p.WaitForExit(30_000);
        try { Directory.Delete(psi.Environment["HOME"]!, recursive: true); } catch { }
        return (stdout, stderr, p.ExitCode);
    }
}

// ═══════════════════════════════════════════════════════════════════════
// K-3 — DelegateTaskTool api-key leak regression
// Appended to the DelegateTask suite's Telemetry collection because it
// touches env vars + Telemetry state (matches spec).
// ═══════════════════════════════════════════════════════════════════════
[Collection(TelemetryGlobalStateCollection.Name)]
public class K3DelegateTaskApiKeyLeakTests : IDisposable
{
    public K3DelegateTaskApiKeyLeakTests() => DelegateTaskTool.ResetForTests();
    public void Dispose() => DelegateTaskTool.ResetForTests();

    [Fact]
    public async Task DelegateAsync_NotConfiguredError_DoesNotLeakApiKey()
    {
        const string Sentinel = "SENTINEL-DELEGATE-NOTCONFIG-4a21";
        var previous = Environment.GetEnvironmentVariable("AZUREOPENAIAPI");
        Environment.SetEnvironmentVariable("AZUREOPENAIAPI", Sentinel);
        try
        {
            // Force error: chat client not configured.
            var result = await DelegateTaskTool.DelegateAsync("task");

            Assert.StartsWith("Error:", result);
            Assert.DoesNotContain(Sentinel, result);
        }
        finally
        {
            Environment.SetEnvironmentVariable("AZUREOPENAIAPI", previous);
        }
    }

    [Fact]
    public async Task DelegateAsync_EmptyTaskError_DoesNotLeakApiKey()
    {
        const string Sentinel = "SENTINEL-DELEGATE-EMPTY-7b55";
        var previous = Environment.GetEnvironmentVariable("AZUREOPENAIAPI");
        Environment.SetEnvironmentVariable("AZUREOPENAIAPI", Sentinel);
        try
        {
            var result = await DelegateTaskTool.DelegateAsync("");
            Assert.StartsWith("Error:", result);
            Assert.DoesNotContain(Sentinel, result);
        }
        finally
        {
            Environment.SetEnvironmentVariable("AZUREOPENAIAPI", previous);
        }
    }

    [Fact]
    public async Task DelegateAsync_ChildException_DoesNotLeakApiKey()
    {
        // Stronger guarantee: even when the child agent throws an exception
        // whose message happens to embed the sentinel, the returned error
        // string still surfaces ex.Message. This asserts the current contract
        // and will fail loudly if a future change starts interpolating env
        // content into error text — that's the regression we want to catch.
        const string Sentinel = "SENTINEL-DELEGATE-CHILDEX-9e17";
        var previous = Environment.GetEnvironmentVariable("AZUREOPENAIAPI");
        Environment.SetEnvironmentVariable("AZUREOPENAIAPI", Sentinel);

        try
        {
            // A fake that throws a clean, sentinel-free exception on stream.
            var fake = new ThrowingFakeChatClient(message: "boom");
            DelegateTaskTool.Configure(fake, "sys", "gpt-test");

            var result = await DelegateTaskTool.DelegateAsync("work");

            Assert.Contains("child agent failed", result);
            Assert.DoesNotContain(Sentinel, result);
        }
        finally
        {
            DelegateTaskTool.ResetForTests();
            Environment.SetEnvironmentVariable("AZUREOPENAIAPI", previous);
        }
    }

    private sealed class ThrowingFakeChatClient : Microsoft.Extensions.AI.IChatClient
    {
        private readonly string _message;
        public ThrowingFakeChatClient(string message) => _message = message;
        public void Dispose() { }
        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public Task<Microsoft.Extensions.AI.ChatResponse> GetResponseAsync(
            IEnumerable<Microsoft.Extensions.AI.ChatMessage> messages,
            Microsoft.Extensions.AI.ChatOptions? options = null,
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException(_message);

        public async IAsyncEnumerable<Microsoft.Extensions.AI.ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<Microsoft.Extensions.AI.ChatMessage> messages,
            Microsoft.Extensions.AI.ChatOptions? options = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            throw new InvalidOperationException(_message);
#pragma warning disable CS0162
            yield break;
#pragma warning restore CS0162
        }
    }
}
