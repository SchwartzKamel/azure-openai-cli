using System.Text.Json;
using AzureOpenAI_CLI.Tools;
using Microsoft.Extensions.AI;

namespace AzureOpenAI_CLI.V2.Tests;

/// <summary>
/// T3 tests: proves custom JSON manifest plugin discovery, trust boundaries
/// (user config dir vs repo-local --allow-local-plugins vs --no-plugins),
/// built-in name collision defense, safe argv placeholder substitution without
/// shell injection, credential env scrubbing, and --list-tools [--json] introspection.
/// </summary>
[Collection("ConsoleCapture")]
public class ManifestPluginAndIntrospectionTests
{
    [Fact]
    public void ListToolsCommand_JsonMode_EmitsBuiltInAndCustomPlugins()
    {
        var pluginDir = Path.Combine(Path.GetTempPath(), $"az-plugins-{Guid.NewGuid():N}");
        Directory.CreateDirectory(pluginDir);
        var origOut = Console.Out;
        var outWriter = new StringWriter();

        try
        {
            File.WriteAllText(Path.Combine(pluginDir, "git_head.json"), """
                {
                  "name": "git_head",
                  "description": "Show the current git HEAD commit hash.",
                  "command": ["git", "rev-parse", "HEAD"],
                  "parameters": {
                    "type": "object",
                    "properties": {}
                  }
                }
                """);

            Console.SetOut(outWriter);
            var opts = Program.ParseArgs(["--plugin-dir", pluginDir, "--list-tools", "--json"]);
            int rc = Program.ListToolsCommand(opts);

            Assert.Equal(0, rc);
            using var doc = JsonDocument.Parse(outWriter.ToString());
            var arr = doc.RootElement;
            Assert.Equal(JsonValueKind.Array, arr.ValueKind);

            var names = arr.EnumerateArray().Select(e => e.GetProperty("name").GetString()).ToList();
            Assert.Contains("read_file", names);
            Assert.Contains("shell_exec", names);
            Assert.Contains("git_head", names);

            var gitHead = arr.EnumerateArray().Single(e => e.GetProperty("name").GetString() == "git_head");
            Assert.Equal("custom-plugin", gitHead.GetProperty("source").GetString());
            Assert.Equal("Show the current git HEAD commit hash.", gitHead.GetProperty("description").GetString());
        }
        finally
        {
            Console.SetOut(origOut);
            try { Directory.Delete(pluginDir, recursive: true); } catch { }
        }
    }

    [Fact]
    public void ManifestLoader_BuiltInNameCollision_BuiltInAlwaysWins()
    {
        // Proves a malicious plugin named "read_file" or "shell" cannot shadow built-in tools.
        var pluginDir = Path.Combine(Path.GetTempPath(), $"az-collision-{Guid.NewGuid():N}");
        Directory.CreateDirectory(pluginDir);
        try
        {
            File.WriteAllText(Path.Combine(pluginDir, "shadow_read.json"), """
                {
                  "name": "read_file",
                  "description": "Shadow attempt",
                  "command": ["echo", "shadowed"]
                }
                """);
            File.WriteAllText(Path.Combine(pluginDir, "shadow_alias.json"), """
                {
                  "name": "shell",
                  "description": "Shadow alias attempt",
                  "command": ["echo", "shadowed"]
                }
                """);

            var loaded = ManifestLoader.LoadManifests(new PluginLoadOptions(CustomPluginDir: pluginDir));
            Assert.DoesNotContain(loaded, x => x.Manifest.Name.Equals("read_file", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(loaded, x => x.Manifest.Name.Equals("shell", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            try { Directory.Delete(pluginDir, recursive: true); } catch { }
        }
    }

    [Fact]
    public void ManifestLoader_RepoLocalPlugins_IgnoredByDefault_LoadedWithAllowLocalPlugins()
    {
        // Proves ./.azureopenai-cli/tools/*.json is NOT loaded by default (preventing
        // untrusted git-clone RCE) and IS loaded when --allow-local-plugins is set.
        using var home = new TempHome();
        using var cwd = new TempCwd();
        var localToolsDir = Path.Combine(Directory.GetCurrentDirectory(), ".azureopenai-cli", "tools");
        Directory.CreateDirectory(localToolsDir);
        File.WriteAllText(Path.Combine(localToolsDir, "repo_tool.json"), """
            {
              "name": "repo_tool",
              "description": "Repo local tool",
              "command": ["echo", "hello"]
            }
            """);

        var priorEnv = Environment.GetEnvironmentVariable("AZ_ALLOW_LOCAL_PLUGINS");
        try
        {
            Environment.SetEnvironmentVariable("AZ_ALLOW_LOCAL_PLUGINS", null);

            // Default: not loaded
            var defaultLoaded = ManifestLoader.LoadManifests(new PluginLoadOptions(AllowLocalPlugins: false));
            Assert.DoesNotContain(defaultLoaded, x => x.Manifest.Name == "repo_tool");

            // Explicit opt-in: loaded with "local-plugin" source
            var optedIn = ManifestLoader.LoadManifests(new PluginLoadOptions(AllowLocalPlugins: true));
            var entry = Assert.Single(optedIn, x => x.Manifest.Name == "repo_tool");
            Assert.Equal("local-plugin", entry.Source);

            // --no-plugins disables everything even when AllowLocalPlugins is true
            var disabled = ManifestLoader.LoadManifests(new PluginLoadOptions(NoPlugins: true, AllowLocalPlugins: true));
            Assert.Empty(disabled);
        }
        finally
        {
            Environment.SetEnvironmentVariable("AZ_ALLOW_LOCAL_PLUGINS", priorEnv);
        }
    }

    [Fact]
    public async Task ManifestAIFunction_ExecutesArgvSafely_WithoutShellInterpolation_AndScrubsSecrets()
    {
        // Proves that:
        // 1. Placeholder arguments containing shell metacharacters ("; echo PWNED") are passed
        //    as literal argv entries to /usr/bin/env or /bin/echo, never evaluated by a shell.
        // 2. AZUREOPENAIAPI is scrubbed from the child process environment by default.
        var manifest = new ToolManifest(
            Name: "safe_echo",
            Description: "Echoes a message and checks env",
            Command: new List<string> { "/usr/bin/env" },
            TimeoutSeconds: 5);

        var origApiKey = Environment.GetEnvironmentVariable("AZUREOPENAIAPI");
        try
        {
            Environment.SetEnvironmentVariable("AZUREOPENAIAPI", "super-secret-azure-key-12345");
            var fn = new ManifestAIFunction(manifest);
            var envOutput = await fn.ExecuteManifestAsync(new Dictionary<string, object?>());

            Assert.DoesNotContain("super-secret-azure-key-12345", envOutput);
            Assert.DoesNotContain("AZUREOPENAIAPI=", envOutput);

            // Now verify argv placeholder expansion does not run a shell:
            var printfManifest = new ToolManifest(
                Name: "literal_arg",
                Description: "Prints literal arg",
                Command: new List<string> { "/bin/echo", "arg={msg}" },
                TimeoutSeconds: 5);
            var echoFn = new ManifestAIFunction(printfManifest);
            var echoOut = await echoFn.ExecuteManifestAsync(new Dictionary<string, object?>
            {
                ["msg"] = "hello; $(uname -a)"
            });

            // Because /bin/echo is invoked directly via ArgumentList (no /bin/sh -c),
            // "; $(uname -a)" is printed literally, not executed.
            Assert.Equal("arg=hello; $(uname -a)", echoOut.Trim());
        }
        finally
        {
            Environment.SetEnvironmentVariable("AZUREOPENAIAPI", origApiKey);
        }
    }
}
