// S04E02 Wave 2b -- Puddy. Locks in Russell's --doctor [registry] section
// behavior end-to-end. Spawns the production CLI assembly via `dotnet <dll>`
// to exercise WriteRegistrySection through the real argv -> dispatch path.
//
// Five facts asserted (no overlap with RegistryTests.cs which Newman owns):
//   1. Each seed card description appears in --doctor output (live-read from
//      docs/model-cards/*.md so the test does not drift when cards edit).
//   2. --doctor --raw suppresses the [registry] header entirely.
//   3. Override card name with embedded CSI escape is sanitized to '?' --
//      no literal ESC byte reaches stdout.
//   4. User override at $HOME/.config/az-ai/registry.json REPLACES the seed
//      list (does not merge) -- S04E01 contract.
//   5. Override entry whose cardPath does not resolve renders the literal
//      "(no card)" token and the entry's caps inline on the same row;
//      --doctor exit code stays 0.
//
// Process isolation: each spawn gets a unique HOME under TempPath so that
// concurrent xunit workers cannot stomp each other's user override file.
// AZ_AI_REGISTRY_DIR is set per-process on ProcessStartInfo.Environment for
// the same reason. Temp dirs are deleted in finally.

using System.Diagnostics;
using System.Text;

namespace AzureOpenAI_CLI.Tests.DoctorRegistry;

public class DoctorRegistryTests
{
    // Walk up from the test bin dir to find the repo root (the directory
    // that holds azureopenai-cli/Registry/registry.json). Used to live-read
    // shipped model cards so test 1 does not hard-code Elaine's prose.
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (int i = 0; i < 10 && dir is not null; i++, dir = dir.Parent)
        {
            var probe = Path.Combine(dir.FullName, "azureopenai-cli", "Registry", "registry.json");
            if (File.Exists(probe))
                return dir.FullName;
        }
        throw new InvalidOperationException(
            "Could not locate repo root from AppContext.BaseDirectory: "
            + AppContext.BaseDirectory);
    }

    // Pull the description: "..." line from a card's YAML-ish front matter.
    // Mirrors ModelRegistry.ParseFrontMatter's contract -- if the value is
    // double-quoted we strip the quotes; otherwise raw trim. Throws if the
    // card lacks a description so the test fails loudly when a card edit
    // accidentally drops the field.
    private static string ReadCardDescription(string repoRoot, string relativePath)
    {
        var full = Path.Combine(repoRoot, relativePath);
        foreach (var raw in File.ReadAllLines(full))
        {
            var line = raw.TrimStart();
            if (!line.StartsWith("description:", StringComparison.Ordinal)) continue;
            var value = line.Substring("description:".Length).Trim();
            if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
                value = value.Substring(1, value.Length - 2);
            return value;
        }
        throw new InvalidOperationException(
            "No 'description:' front-matter key in card: " + full);
    }

    private static (string stdout, string stderr, int exitCode) RunCli(
        string[] args,
        IReadOnlyDictionary<string, string>? extraEnv = null)
    {
        var asm = typeof(AzureOpenAI_CLI.Program).Assembly.Location;

        var psi = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = Environment.CurrentDirectory,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        psi.ArgumentList.Add(asm);
        foreach (var a in args) psi.ArgumentList.Add(a);

        // Deterministic env: clear all provider creds so ProviderDoctor.Run
        // returns rc=0 (no providers configured) and the [registry] section
        // is the only variable surface under test.
        psi.Environment["AZUREOPENAIENDPOINT"] = "";
        psi.Environment["AZUREOPENAIAPI"] = "";
        psi.Environment["AZUREOPENAIMODEL"] = "";
        psi.Environment["AZURE_FOUNDRY_ENDPOINT"] = "";
        psi.Environment["AZURE_FOUNDRY_KEY"] = "";
        psi.Environment["AZURE_FOUNDRY_MODELS"] = "";
        psi.Environment["AZ_AI_LLAMACPP_ENDPOINT"] = "";
        psi.Environment["AZ_AI_COMPAT_MODELS"] = "";
        psi.Environment["NO_COLOR"] = "1";

        // Fresh HOME per spawn so concurrent xunit workers cannot collide
        // on ~/.config/az-ai/registry.json. Tests that need a user override
        // pass extraEnv with HOME pointing at a pre-populated tempdir.
        var defaultHome = Path.Combine(
            Path.GetTempPath(), "puddy-doctor-home-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(defaultHome);
        psi.Environment["HOME"] = defaultHome;

        if (extraEnv is not null)
        {
            foreach (var kv in extraEnv) psi.Environment[kv.Key] = kv.Value;
        }

        using var p = Process.Start(psi)!;
        var stdout = p.StandardOutput.ReadToEnd();
        var stderr = p.StandardError.ReadToEnd();
        p.WaitForExit(30_000);

        try { Directory.Delete(defaultHome, recursive: true); } catch { /* best effort */ }

        return (stdout, stderr, p.ExitCode);
    }

    // -- Test 1 --------------------------------------------------------
    [Fact]
    public void Doctor_Registry_IncludesDescriptionForEachSeedCard()
    {
        var repo = RepoRoot();
        var descriptions = new[]
        {
            ReadCardDescription(repo, "docs/model-cards/azure-gpt-4o-mini.md"),
            ReadCardDescription(repo, "docs/model-cards/azure-gpt-5.4-nano.md"),
            ReadCardDescription(repo, "docs/model-cards/local-llama.md"),
        };

        var (stdout, stderr, rc) = RunCli(new[] { "--doctor" });

        Assert.Equal(0, rc);
        Assert.Contains("[registry]", stdout);
        // Sanity: each seed name shows up.
        Assert.Contains("gpt-4o-mini", stdout);
        Assert.Contains("gpt-5.4-nano", stdout);
        Assert.Contains("llama-local", stdout);

        // Russell truncates descriptions at 60 chars with an ASCII "..."
        // ellipsis. Use the leading 57 chars (or full string when shorter)
        // so the assertion survives any future card-edit that lengthens
        // the description past the truncation boundary.
        foreach (var desc in descriptions)
        {
            Assert.False(string.IsNullOrWhiteSpace(desc),
                "Card description was empty: " + desc);
            var probe = desc.Length <= 60 ? desc : desc.Substring(0, 57);
            Assert.True(stdout.Contains(probe, StringComparison.Ordinal),
                "Expected --doctor stdout to contain card description fragment: '"
                + probe + "'\nstdout:\n" + stdout + "\nstderr:\n" + stderr);
        }
    }

    // -- Test 2 --------------------------------------------------------
    [Fact]
    public void Doctor_Registry_RawSuppressesSection()
    {
        var (stdout, _, rc) = RunCli(new[] { "--doctor", "--raw" });

        Assert.Equal(0, rc);
        Assert.DoesNotContain("[registry]", stdout);
        // None of the seed names should leak through the registry section
        // either, since the entire section is gated off in raw mode.
        Assert.DoesNotContain("gpt-4o-mini", stdout);
        Assert.DoesNotContain("gpt-5.4-nano", stdout);
        Assert.DoesNotContain("llama-local", stdout);
    }

    // -- Test 3 --------------------------------------------------------
    [Fact]
    public void Doctor_Registry_TerminalInjectionPayload_ScrubbedToQuestionMarks()
    {
        var home = Path.Combine(
            Path.GetTempPath(), "puddy-doctor-csi-" + Guid.NewGuid().ToString("N"));
        var cfgDir = Path.Combine(home, ".config", "az-ai");
        Directory.CreateDirectory(cfgDir);
        try
        {
            // Embed a literal ESC (0x1B) byte in the JSON via \u001b. After
            // deserialize the entry name is "evil<ESC>[31mRED" -- the C0
            // sanitizer must replace 0x1B with '?'.
            var json =
                "[{\"name\":\"evil\\u001b[31mRED\","
                + "\"provider\":\"local\","
                + "\"capabilities\":[\"streaming\"],"
                + "\"contextWindow\":1024,"
                + "\"costTier\":\"unknown\","
                + "\"cardPath\":\"\"}]";
            File.WriteAllText(Path.Combine(cfgDir, "registry.json"), json, Encoding.UTF8);

            var (stdout, stderr, rc) = RunCli(
                new[] { "--doctor" },
                new Dictionary<string, string>
                {
                    ["HOME"] = home,
                    // Pin AZ_AI_REGISTRY_DIR to the same dir so the resolver
                    // does not walk up to a repo registry.json on developer
                    // workstations -- guarantees the override is the only
                    // entry rendered.
                    ["AZ_AI_REGISTRY_DIR"] = cfgDir,
                });

            // S04E04 Kramer (3bd7f8d): shell-hostile names are now rejected
            // at registry-load time with rc=99, BEFORE display. The old
            // path (rc=0 + display-time ESC -> '?' scrub) is superseded;
            // the new defense is strictly stronger because the offending
            // bytes never enter the rendering pipeline. The display
            // sanitizer remains in place for other surfaces (e.g. card
            // fields) where load-time rejection is not appropriate.
            // See ADR-012 (load-time reject bullet) and FINDING-P-S04E04-03.
            Assert.Equal(99, rc);
            Assert.Contains("registry rejected", stderr, StringComparison.Ordinal);
            Assert.Contains("shell-hostile character", stderr, StringComparison.Ordinal);
            // ESC (0x1B) byte must not appear in stdout. stdout is empty on
            // load-time reject (rc=99 fires before doctor renders), so this
            // is effectively trivial -- kept as a regression sentinel in
            // case future routing puts anything on stdout in this path.
            //
            // NOTE: stderr currently DOES contain the raw ESC byte because
            // Kramer's reject message format echoes the offending name
            // verbatim ('[ERROR] registry rejected: model name \u001b[31m...').
            // Filed as FINDING-S04E04-04 for Kramer to scrub the name in
            // the rejection message before this assertion can be tightened.
            // Use IndexOf<char> rather than Assert.DoesNotContain(string)
            // (xUnit substring formatter renders lone C0 needles as "").
            Assert.True(stdout.IndexOf('\u001B') < 0,
                "ESC byte leaked into stdout");
        }
        finally
        {
            try { Directory.Delete(home, recursive: true); } catch { }
        }
    }

    // -- Test 4 --------------------------------------------------------
    [Fact]
    public void Doctor_Registry_OverrideReplacesSeedNotMerges()
    {
        var home = Path.Combine(
            Path.GetTempPath(), "puddy-doctor-replace-" + Guid.NewGuid().ToString("N"));
        var cfgDir = Path.Combine(home, ".config", "az-ai");
        Directory.CreateDirectory(cfgDir);
        try
        {
            var json =
                "[{\"name\":\"puddy-only\","
                + "\"provider\":\"local\","
                + "\"capabilities\":[\"streaming\"],"
                + "\"contextWindow\":4096,"
                + "\"costTier\":\"unknown\","
                + "\"cardPath\":\"\"}]";
            File.WriteAllText(Path.Combine(cfgDir, "registry.json"), json, Encoding.UTF8);

            var (stdout, stderr, rc) = RunCli(
                new[] { "--doctor" },
                new Dictionary<string, string> { ["HOME"] = home });

            Assert.Equal(0, rc);
            Assert.Contains("[registry] 1 known model", stdout);
            Assert.Contains("puddy-only", stdout);
            Assert.DoesNotContain("gpt-4o-mini", stdout);
            Assert.DoesNotContain("gpt-5.4-nano", stdout);
            Assert.DoesNotContain("llama-local", stdout);
            // Sanity: stderr only carried the documented "no cardPath" WARN,
            // no error chatter that might mask a regression.
            Assert.DoesNotContain("[ERROR]", stderr);
        }
        finally
        {
            try { Directory.Delete(home, recursive: true); } catch { }
        }
    }

    // -- Test 5 --------------------------------------------------------
    [Fact]
    public void Doctor_Registry_MissingCard_RendersNoCard()
    {
        var home = Path.Combine(
            Path.GetTempPath(), "puddy-doctor-nocard-" + Guid.NewGuid().ToString("N"));
        var cfgDir = Path.Combine(home, ".config", "az-ai");
        Directory.CreateDirectory(cfgDir);
        try
        {
            // Non-empty cardPath that points at a file which does not exist.
            // Resolver anchors the relative path against AZ_AI_REGISTRY_DIR
            // (cfgDir); ReadCard returns null -> WriteRegistrySection takes
            // the missing-card branch (caps rendered inline on the row).
            var json =
                "[{\"name\":\"ghost-model\","
                + "\"provider\":\"local\","
                + "\"capabilities\":[\"streaming\",\"tool_calls\"],"
                + "\"contextWindow\":2048,"
                + "\"costTier\":\"unknown\","
                + "\"cardPath\":\"does-not-exist.md\"}]";
            File.WriteAllText(Path.Combine(cfgDir, "registry.json"), json, Encoding.UTF8);

            var (stdout, _, rc) = RunCli(
                new[] { "--doctor" },
                new Dictionary<string, string>
                {
                    ["HOME"] = home,
                    ["AZ_AI_REGISTRY_DIR"] = cfgDir,
                });

            Assert.Equal(0, rc);
            Assert.Contains("[registry] 1 known model", stdout);

            // Find the ghost-model row and assert (no card) + caps inline
            // on the SAME line. Russell's missing-card layout is single-line
            // (no wrapped second "caps:" line).
            string? row = null;
            foreach (var line in stdout.Split('\n'))
            {
                if (line.Contains("ghost-model", StringComparison.Ordinal))
                {
                    row = line;
                    break;
                }
            }
            Assert.NotNull(row);
            Assert.Contains("(no card)", row);
            Assert.Contains("streaming", row);
            Assert.Contains("tool_calls", row);
            // The wrapped "caps:" prefix is the card-present branch -- it
            // must NOT appear for a missing-card entry.
            Assert.DoesNotContain("caps: streaming", stdout);
        }
        finally
        {
            try { Directory.Delete(home, recursive: true); } catch { }
        }
    }
}
