using System;
using System.Net;
using AzureOpenAI_CLI;
using AzureOpenAI_CLI.Net;
using Xunit;

namespace AzureOpenAI_CLI.Tests;

/// <summary>
/// S03E26 -- The Offline Mode (Newman). End-to-end facts that wire the
/// `--offline` flag through the parser, the static latch, and into the
/// build seams (OpenAiCompatAdapter, ProviderDoctor, WebFetchTool). Joins
/// the ConsoleCapture collection because every test mutates either the
/// process env (AZ_AI_OFFLINE / AZ_AI_LOCAL_PROVIDERS / preset API keys)
/// or the EndpointAllowlist.OfflineMode latch -- racing with parallel
/// tests would produce non-deterministic verdicts.
///
/// Threat model recap (mirrors docs/audits/security-v2.1.4-offline.md):
///   * --offline forbids every non-loopback provider call.
///   * Loopback still requires AZ_AI_LOCAL_PROVIDERS=1 (layered model).
///   * --offline does NOT relax the loopback opt-in gate.
///   * Friendly error names the rule and the env-var to flip.
///   * No credential value ever appears in the error path.
/// </summary>
[Collection("ConsoleCapture")]
public class OfflineModeTests : IDisposable
{
    // Snapshot of state we mutate, restored in Dispose.
    private readonly string? _priorOfflineEnv;
    private readonly string? _priorOptInEnv;
    private readonly string? _priorAzEndpoint;
    private readonly string? _priorAzKey;
    private readonly string? _priorAzModel;
    private readonly string? _priorCompatModels;
    private readonly string? _priorOpenAiKey;
    private readonly bool _priorLatch;

    public OfflineModeTests()
    {
        _priorOfflineEnv = Environment.GetEnvironmentVariable(EndpointAllowlist.OfflineEnvVar);
        _priorOptInEnv = Environment.GetEnvironmentVariable(EndpointAllowlist.OptInEnvVar);
        _priorAzEndpoint = Environment.GetEnvironmentVariable("AZUREOPENAIENDPOINT");
        _priorAzKey = Environment.GetEnvironmentVariable("AZUREOPENAIAPI");
        _priorAzModel = Environment.GetEnvironmentVariable("AZUREOPENAIMODEL");
        _priorCompatModels = Environment.GetEnvironmentVariable("AZ_AI_COMPAT_MODELS");
        _priorOpenAiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        _priorLatch = EndpointAllowlist.OfflineMode;
        // Clean slate per test.
        Environment.SetEnvironmentVariable(EndpointAllowlist.OfflineEnvVar, null);
        Environment.SetEnvironmentVariable(EndpointAllowlist.OptInEnvVar, null);
        EndpointAllowlist.OfflineMode = false;
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(EndpointAllowlist.OfflineEnvVar, _priorOfflineEnv);
        Environment.SetEnvironmentVariable(EndpointAllowlist.OptInEnvVar, _priorOptInEnv);
        Environment.SetEnvironmentVariable("AZUREOPENAIENDPOINT", _priorAzEndpoint);
        Environment.SetEnvironmentVariable("AZUREOPENAIAPI", _priorAzKey);
        Environment.SetEnvironmentVariable("AZUREOPENAIMODEL", _priorAzModel);
        Environment.SetEnvironmentVariable("AZ_AI_COMPAT_MODELS", _priorCompatModels);
        Environment.SetEnvironmentVariable("OPENAI_API_KEY", _priorOpenAiKey);
        EndpointAllowlist.OfflineMode = _priorLatch;
    }

    // ----------------------------------------------------------------
    // Parser facts: --offline lifts to CliOptions.Offline.
    // ----------------------------------------------------------------

    [Fact]
    public void ParseArgs_OfflineFlag_SetsOfflineTrue()
    {
        // Defends: the additive end-of-parser branch (placed last in
        // ParseArgs to minimise merge friction with Kramer's S03E17
        // streaming dispatch hunk) wires through to CliOptions.Offline.
        var opts = Program.ParseArgs(new[] { "--offline", "hello" });
        Assert.True(opts.Offline);
        Assert.False(opts.ParseError);
        Assert.Equal("hello", opts.Prompt);
    }

    [Fact]
    public void ParseArgs_OfflineFlag_DefaultsFalse()
    {
        // Defends: opt-in posture. Absent the flag, Offline=false; existing
        // behavior is unchanged for every prior caller.
        var opts = Program.ParseArgs(new[] { "hello" });
        Assert.False(opts.Offline);
    }

    [Fact]
    public void ParseArgs_OfflineEnv_StrictEqualityOnly_LiftsToOptions()
    {
        // Defends: AZ_AI_OFFLINE=1 (strict) is honored as a fallback when
        // --offline is not on the command line. "true" / "yes" / "1 " do
        // not flip the gate.
        Environment.SetEnvironmentVariable(EndpointAllowlist.OfflineEnvVar, "1");
        var opts = Program.ParseArgs(new[] { "hello" });
        Assert.True(opts.Offline);

        Environment.SetEnvironmentVariable(EndpointAllowlist.OfflineEnvVar, "true");
        var opts2 = Program.ParseArgs(new[] { "hello" });
        Assert.False(opts2.Offline);
    }

    // ----------------------------------------------------------------
    // Compat dispatch: OpenAiCompatAdapter.Build refuses non-loopback
    // under offline, with friendly error and no key in the message.
    // ----------------------------------------------------------------

    [Fact]
    public void Adapter_Build_OfflineBlocksPublicHttpsEndpoint()
    {
        // Defends: the compat dispatch seam picks up the static latch via
        // the existing 2-arg Check(uri, optIn) call site (no signature
        // change required). Public HTTPS preset under --offline -> throws
        // ArgumentException with the friendly --offline phrase.
        try
        {
            EndpointAllowlist.OfflineMode = true;
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", "sk-stub-not-real-redacted");
            var preset = OpenAiCompatAdapter.ResolveOrThrow("openai");
            var ex = Assert.Throws<ArgumentException>(() => OpenAiCompatAdapter.Build("gpt-4o-mini", preset));
            Assert.Contains("compat preset 'openai'", ex.Message, StringComparison.Ordinal);
            Assert.Contains("--offline", ex.Message, StringComparison.Ordinal);
            // Defense-in-depth: the secret-shape stub MUST NOT appear in
            // the error message even though the message interpolates the
            // endpoint string.
            Assert.DoesNotContain("sk-stub-not-real-redacted", ex.Message, StringComparison.Ordinal);
        }
        finally
        {
            EndpointAllowlist.OfflineMode = false;
        }
    }

    [Fact]
    public void Adapter_Build_OfflinePlusOptIn_LoopbackPresetSucceeds()
    {
        // Defends: --offline + AZ_AI_LOCAL_PROVIDERS=1 + an Ollama-shape
        // loopback preset -> Build succeeds. This is the air-gapped /
        // demo-recording happy path.
        try
        {
            EndpointAllowlist.OfflineMode = true;
            Environment.SetEnvironmentVariable(EndpointAllowlist.OptInEnvVar, "1");
            Environment.SetEnvironmentVariable("OLLAMA_API_KEY", "loopback-no-auth");
            var preset = new OpenAiCompatPreset(
                "ollama-local",
                new Uri("http://127.0.0.1:11434/v1"),
                "OLLAMA_API_KEY");
            var client = OpenAiCompatAdapter.Build("llama3.1:8b", preset);
            Assert.NotNull(client);
            if (client is IDisposable d) d.Dispose();
        }
        finally
        {
            EndpointAllowlist.OfflineMode = false;
            Environment.SetEnvironmentVariable("OLLAMA_API_KEY", null);
        }
    }

    [Fact]
    public void Adapter_Build_OfflineWithoutOptIn_LoopbackStillBlocked()
    {
        // Defends: layered model. --offline does NOT relax the loopback
        // opt-in gate. A loopback preset under --offline alone still
        // refuses with BlockLoopback (not BlockOffline) -- the older
        // verdict carries the actionable env-var hint.
        try
        {
            EndpointAllowlist.OfflineMode = true;
            Environment.SetEnvironmentVariable(EndpointAllowlist.OptInEnvVar, null);
            Environment.SetEnvironmentVariable("OLLAMA_API_KEY", "loopback-no-auth");
            var preset = new OpenAiCompatPreset(
                "ollama-local",
                new Uri("http://127.0.0.1:11434/v1"),
                "OLLAMA_API_KEY");
            var ex = Assert.Throws<ArgumentException>(() => OpenAiCompatAdapter.Build("llama3.1:8b", preset));
            Assert.Contains("AZ_AI_LOCAL_PROVIDERS", ex.Message, StringComparison.Ordinal);
        }
        finally
        {
            EndpointAllowlist.OfflineMode = false;
            Environment.SetEnvironmentVariable("OLLAMA_API_KEY", null);
        }
    }

    // ----------------------------------------------------------------
    // ProviderDoctor: every non-loopback provider shows blocked-offline.
    // ----------------------------------------------------------------

    [Fact]
    public void Doctor_Offline_AzureProvider_ReportsBlockedOffline()
    {
        // Defends: --offline reflected in --doctor output. The Azure row
        // (a real-shaped cognitiveservices endpoint) shows dns="blocked-offline"
        // and exit code is non-zero.
        Environment.SetEnvironmentVariable("AZUREOPENAIENDPOINT", "https://contoso.cognitiveservices.azure.com/");
        Environment.SetEnvironmentVariable("AZUREOPENAIAPI", "sk-stub-key-12345");
        Environment.SetEnvironmentVariable("AZUREOPENAIMODEL", "gpt-4o-mini");
        try
        {
            EndpointAllowlist.OfflineMode = true;
            using var stdout = new System.IO.StringWriter();
            using var stderr = new System.IO.StringWriter();
            var rc = AzureOpenAI_CLI.Cli.ProviderDoctor.Run(jsonMode: false, plain: true, stdout, stderr);
            Assert.NotEqual(0, rc);
            var output = stdout.ToString();
            Assert.Contains("blocked-offline", output, StringComparison.Ordinal);
            // Secret-shape leak guard: the stub key must not surface.
            Assert.DoesNotContain("sk-stub-key-12345", output, StringComparison.Ordinal);
        }
        finally
        {
            EndpointAllowlist.OfflineMode = false;
        }
    }

    [Fact]
    public void Doctor_Offline_LocalhostProvider_NotMarkedOffline()
    {
        // Defends: a loopback-named provider does not get the offline
        // override. The dispatch path's layered loopback opt-in check
        // is still where the user is funneled if they actually try to
        // use it without AZ_AI_LOCAL_PROVIDERS=1.
        Environment.SetEnvironmentVariable("AZ_AI_COMPAT_MODELS", "openai:noop");
        // Built-in "openai" preset is api.openai.com (public). Use Azure
        // env for the loopback case via a custom shape -- here we just
        // assert that NO compat row is dropped to blocked-offline when
        // pointed at api.openai.com vs when env explicitly sets a loopback
        // shaped endpoint. Simplest valid loopback row: AZUREOPENAIENDPOINT
        // pinned to http://localhost.
        Environment.SetEnvironmentVariable("AZ_AI_COMPAT_MODELS", null);
        Environment.SetEnvironmentVariable("AZUREOPENAIENDPOINT", "http://localhost:11434/");
        Environment.SetEnvironmentVariable("AZUREOPENAIAPI", "stub");
        Environment.SetEnvironmentVariable("AZUREOPENAIMODEL", "llama");
        try
        {
            EndpointAllowlist.OfflineMode = true;
            using var stdout = new System.IO.StringWriter();
            using var stderr = new System.IO.StringWriter();
            var rc = AzureOpenAI_CLI.Cli.ProviderDoctor.Run(jsonMode: false, plain: true, stdout, stderr);
            // Doctor exit code is unrelated to offline here; we only
            // assert that the loopback row does NOT carry the offline
            // marker. A non-loopback row would.
            var output = stdout.ToString();
            Assert.DoesNotContain("blocked-offline", output, StringComparison.Ordinal);
            // Sanity: doctor did emit something.
            Assert.Contains("localhost", output, StringComparison.Ordinal);
            _ = rc; // exit code drift is not part of this assertion
        }
        finally
        {
            EndpointAllowlist.OfflineMode = false;
        }
    }

    // ----------------------------------------------------------------
    // WebFetchTool: offline propagates via the static latch.
    // ----------------------------------------------------------------

    [Fact]
    public async System.Threading.Tasks.Task WebFetchTool_OfflineRefusesAnyHttpsUrl()
    {
        // Defends: WebFetchTool's localProvidersOptIn is hard-coded false
        // (tool surface), so the only way for offline to reach it is via
        // the EndpointAllowlist.OfflineMode static latch. We assert the
        // friendly error names the offline rule.
        try
        {
            EndpointAllowlist.OfflineMode = true;
            var msg = await AzureOpenAI_CLI.Tools.WebFetchTool.FetchAsync("https://example.com/");
            Assert.StartsWith("Error:", msg, StringComparison.Ordinal);
            Assert.Contains("--offline", msg, StringComparison.Ordinal);
        }
        finally
        {
            EndpointAllowlist.OfflineMode = false;
        }
    }
}
