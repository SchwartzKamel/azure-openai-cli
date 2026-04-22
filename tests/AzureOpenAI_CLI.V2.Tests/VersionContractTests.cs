using System;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using AzureOpenAI_CLI_V2;
using AzureOpenAI_CLI_V2.Observability;

namespace AzureOpenAI_CLI.V2.Tests;

/// <summary>
/// Version contract — pins the single-source-of-truth for the shipped <c>--version</c>
/// string to the csproj <c>&lt;Version&gt;</c> element. Introduced after the v2.0.4
/// audit finding C-1 (docs/audits/docs-audit-2026-04-22-lippman.md): the v2.0.3 and
/// v2.0.4 binaries shipped with <c>Program.VersionSemver</c> and
/// <c>Telemetry.ServiceVersion</c> hardcoded to <c>"2.0.2"</c>, so
/// <c>az-ai-v2 --version --short</c> reported the wrong version on the v2.0.4
/// tag and <c>brew test az-ai-v2</c> would fail against the published tarballs.
///
/// These tests are deliberately cheap — they run on every PR and hard-fail the
/// build if anyone re-introduces a hardcoded version literal that drifts from
/// the csproj. If this test ever fires, roll the csproj <c>&lt;Version&gt;</c>
/// (the binary will follow via <see cref="Assembly.GetName"/>), do NOT
/// hardcode a literal in <c>Program.cs</c> or <c>Telemetry.cs</c>.
/// </summary>
public class VersionContractTests
{
    private static readonly Regex SemverRegex = new(@"^\d+\.\d+\.\d+$", RegexOptions.Compiled);

    [Fact]
    public void VersionSemver_is_bare_semver_form()
    {
        Assert.Matches(SemverRegex, Program.VersionSemver);
    }

    [Fact]
    public void VersionSemver_is_not_the_stale_v2_0_2_literal()
    {
        // Negative contract: if this ever reverts to "2.0.2" on a post-2.0.2
        // csproj, we've regressed to the audit-finding-C-1 state.
        Assert.NotEqual("2.0.2", Program.VersionSemver);
        Assert.NotEqual("unknown", Program.VersionSemver);
    }

    [Fact]
    public void VersionFull_contains_semver_and_framework_tag()
    {
        Assert.Contains(Program.VersionSemver, Program.VersionFull);
        Assert.Contains("az-ai-v2", Program.VersionFull);
        Assert.Contains("Microsoft Agent Framework", Program.VersionFull);
    }

    [Fact]
    public void Telemetry_ServiceVersion_matches_Program_VersionSemver()
    {
        // Two independent derivations of the same value — if they ever drift,
        // someone reintroduced a literal. This is the exact shape of the
        // v2.0.4 bug (Program.cs and Telemetry.cs drifted from csproj together,
        // but could just as easily drift from each other).
        Assert.Equal(Program.VersionSemver, Telemetry.ServiceVersion);
    }

    [Fact]
    public void VersionSemver_matches_csproj_Version_element()
    {
        var csprojPath = LocateCsproj();
        Assert.True(File.Exists(csprojPath), $"csproj not found at {csprojPath}");

        var csprojText = File.ReadAllText(csprojPath);
        var match = Regex.Match(csprojText, @"<Version>([^<]+)</Version>");
        Assert.True(match.Success, "csproj is missing a <Version> element");

        var csprojVersion = match.Groups[1].Value.Trim();
        Assert.Equal(csprojVersion, Program.VersionSemver);
    }

    private static string LocateCsproj()
    {
        // Walk up from the test assembly's base directory to the repo root and
        // resolve the csproj path. Resilient to `dotnet test` from any cwd.
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(
                dir.FullName,
                "azureopenai-cli-v2",
                "AzureOpenAI_CLI_V2.csproj");
            if (File.Exists(candidate))
            {
                return candidate;
            }
            dir = dir.Parent;
        }
        throw new FileNotFoundException(
            "AzureOpenAI_CLI_V2.csproj not found walking up from " + AppContext.BaseDirectory);
    }
}
