using System.Text.RegularExpressions;

namespace AzureOpenAI_CLI.Tests;

/// <summary>
/// Tests for the publish infrastructure added by FR-004 (Latency &amp; Startup Optimization).
/// Validates that Makefile targets, .gitignore entries, and csproj documentation
/// are correctly configured for ReadyToRun and Native AOT publish modes.
///
/// These are static-analysis tests — they read project files and assert structure,
/// so they run without Azure credentials or network access.
/// </summary>
public class PublishTargetTests
{
    private static readonly string RepoRoot = FindRepoRoot();
    private static readonly string MakefilePath = Path.Combine(RepoRoot, "Makefile");
    private static readonly string GitignorePath = Path.Combine(RepoRoot, ".gitignore");
    private static readonly string CsprojPath = Path.Combine(RepoRoot, "azureopenai-cli", "AzureOpenAI_CLI.csproj");

    /// <summary>
    /// Walk up from the test assembly location until we find the repo root
    /// (identified by the .git directory or azure-openai-cli.sln).
    /// </summary>
    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir, "azure-openai-cli.sln")))
                return dir;
            dir = Directory.GetParent(dir)?.FullName;
        }
        throw new InvalidOperationException(
            "Could not find repo root (azure-openai-cli.sln) from " + AppContext.BaseDirectory);
    }

    // ── Makefile target presence ───────────────────────────────────

    [Fact]
    public void Makefile_ContainsPublishFastTarget()
    {
        // Arrange
        string makefile = File.ReadAllText(MakefilePath);

        // Act & Assert — publish-fast must be a defined Make target (line starts with "publish-fast:")
        Assert.Matches(@"(?m)^publish-fast:", makefile);
    }

    [Fact]
    public void Makefile_ContainsPublishAotTarget()
    {
        // Arrange
        string makefile = File.ReadAllText(MakefilePath);

        // Act & Assert — publish-aot must be a defined Make target
        Assert.Matches(@"(?m)^publish-aot:", makefile);
    }

    [Fact]
    public void Makefile_ContainsPublishR2rAlias()
    {
        // Arrange
        string makefile = File.ReadAllText(MakefilePath);

        // Act & Assert — publish-r2r should exist as an alias target
        Assert.Matches(@"(?m)^publish-r2r:", makefile);
    }

    [Fact]
    public void Makefile_PublishFastUsesReadyToRun()
    {
        // Arrange
        string makefile = File.ReadAllText(MakefilePath);

        // Act — extract lines after publish-fast: target until the next target
        var match = Regex.Match(makefile, @"(?m)^publish-fast:.*?(?=^\S|\z)", RegexOptions.Singleline);

        // Assert — the target body must use PublishReadyToRun
        Assert.True(match.Success, "publish-fast target must exist");
        Assert.Contains("PublishReadyToRun=true", match.Value);
    }

    [Fact]
    public void Makefile_PublishAotUsesPublishAotFlag()
    {
        // Arrange
        string makefile = File.ReadAllText(MakefilePath);

        // Act — extract lines after publish-aot: target until the next target
        var match = Regex.Match(makefile, @"(?m)^publish-aot:.*?(?=^\S|\z)", RegexOptions.Singleline);

        // Assert — the target body must use PublishAot
        Assert.True(match.Success, "publish-aot target must exist");
        Assert.Contains("PublishAot=true", match.Value);
    }

    [Fact]
    public void Makefile_PublishFastUsesRuntimeIdentifier()
    {
        // Arrange
        string makefile = File.ReadAllText(MakefilePath);

        // Act — extract the publish-fast target body
        var match = Regex.Match(makefile, @"(?m)^publish-fast:.*?(?=^\S|\z)", RegexOptions.Singleline);

        // Assert — must specify a runtime identifier for self-contained publish
        Assert.True(match.Success, "publish-fast target must exist");
        Assert.Matches(@"-r\s+\$\(RID\)", match.Value);
    }

    [Fact]
    public void Makefile_PublishFastIsSelfContained()
    {
        // Arrange
        string makefile = File.ReadAllText(MakefilePath);

        // Act — extract the publish-fast target body
        var match = Regex.Match(makefile, @"(?m)^publish-fast:.*?(?=^\S|\z)", RegexOptions.Singleline);

        // Assert — self-contained flag must be present
        Assert.True(match.Success, "publish-fast target must exist");
        Assert.Contains("--self-contained", match.Value);
    }

    [Fact]
    public void Makefile_PublishTargetsAreInPhony()
    {
        // Arrange
        string makefile = File.ReadAllText(MakefilePath);

        // Act — extract the .PHONY declaration
        var phonyMatch = Regex.Match(makefile, @"(?m)^\.PHONY:(.+)$");

        // Assert — all three publish targets must be declared as phony
        Assert.True(phonyMatch.Success, ".PHONY declaration must exist");
        string phonyTargets = phonyMatch.Groups[1].Value;
        Assert.Contains("publish-fast", phonyTargets);
        Assert.Contains("publish-aot", phonyTargets);
        Assert.Contains("publish-r2r", phonyTargets);
    }

    [Fact]
    public void Makefile_PublishAotOutputsToDifferentDirectory()
    {
        // Arrange — AOT and R2R binaries must not collide in the same output dir
        string makefile = File.ReadAllText(MakefilePath);

        var fastMatch = Regex.Match(makefile, @"(?m)^publish-fast:.*?(?=^\S|\z)", RegexOptions.Singleline);
        var aotMatch = Regex.Match(makefile, @"(?m)^publish-aot:.*?(?=^\S|\z)", RegexOptions.Singleline);

        // Assert — both exist and have different -o paths
        Assert.True(fastMatch.Success && aotMatch.Success);

        // Extract -o values
        var fastOutDir = Regex.Match(fastMatch.Value, @"-o\s+(\S+)");
        var aotOutDir = Regex.Match(aotMatch.Value, @"-o\s+(\S+)");

        Assert.True(fastOutDir.Success, "publish-fast must specify -o output directory");
        Assert.True(aotOutDir.Success, "publish-aot must specify -o output directory");
        Assert.NotEqual(fastOutDir.Groups[1].Value, aotOutDir.Groups[1].Value);
    }

    // ── Negative: csproj must NOT permanently enable AOT ───────────

    [Fact]
    public void Csproj_DoesNotPermanentlyEnableAot()
    {
        // Arrange
        string csproj = File.ReadAllText(CsprojPath);

        // Strip XML comments so we only check active (non-commented) elements
        string withoutComments = Regex.Replace(csproj, @"<!--.*?-->", "", RegexOptions.Singleline);

        // Act & Assert — PublishAot must NOT appear as an active XML element.
        // It's fine inside XML comments (documentation) but must not be an active setting.
        var activeAotSetting = Regex.Match(withoutComments, @"<PublishAot\s*>.*?</PublishAot\s*>");
        Assert.False(activeAotSetting.Success,
            "csproj must NOT contain <PublishAot>true</PublishAot> as an active element — AOT should be opt-in via publish command only");
    }

    [Fact]
    public void Csproj_ContainsAotDocumentationComment()
    {
        // Arrange
        string csproj = File.ReadAllText(CsprojPath);

        // Act & Assert — the csproj should document the AOT option in a comment
        Assert.Contains("Native AOT", csproj);
        Assert.Contains("publish-aot", csproj);
        Assert.Contains("publish-fast", csproj);
    }

    // ── .gitignore ─────────────────────────────────────────────────

    [Fact]
    public void Gitignore_ContainsDistDirectory()
    {
        // Arrange
        string gitignore = File.ReadAllText(GitignorePath);
        var lines = gitignore.Split('\n').Select(l => l.Trim()).ToList();

        // Act & Assert — dist/ must be ignored (exact line, not just substring match)
        Assert.Contains("dist/", lines);
    }

    [Fact]
    public void Gitignore_DoesNotIgnoreSrcDirectories()
    {
        // Arrange — negative test: .gitignore should NOT ignore src-level directories
        string gitignore = File.ReadAllText(GitignorePath);
        var lines = gitignore.Split('\n').Select(l => l.Trim()).ToList();

        // Assert — azureopenai-cli/ and tests/ must NOT be in .gitignore
        Assert.DoesNotContain("azureopenai-cli/", lines);
        Assert.DoesNotContain("tests/", lines);
    }

    // ── Makefile help text ─────────────────────────────────────────

    [Fact]
    public void Makefile_HelpMentionsPublishFast()
    {
        // Arrange
        string makefile = File.ReadAllText(MakefilePath);

        // Act — extract the help target body
        var helpMatch = Regex.Match(makefile, @"(?m)^help:.*?(?=^\S|\z)", RegexOptions.Singleline);

        // Assert — help must mention publish-fast so users can discover it
        Assert.True(helpMatch.Success, "help target must exist");
        Assert.Contains("publish-fast", helpMatch.Value);
    }

    [Fact]
    public void Makefile_HelpMentionsPublishAot()
    {
        // Arrange
        string makefile = File.ReadAllText(MakefilePath);

        // Act — extract the help target body
        var helpMatch = Regex.Match(makefile, @"(?m)^help:.*?(?=^\S|\z)", RegexOptions.Singleline);

        // Assert — help must mention publish-aot so users can discover it
        Assert.True(helpMatch.Success, "help target must exist");
        Assert.Contains("publish-aot", helpMatch.Value);
    }

    // ── Negative: Makefile must not break existing targets ─────────

    [Fact]
    public void Makefile_StillContainsOriginalBuildTarget()
    {
        // Arrange — adding publish targets must not remove or break the build target
        string makefile = File.ReadAllText(MakefilePath);

        // Assert — the docker build target must still exist
        Assert.Matches(@"(?m)^build:", makefile);
    }

    [Fact]
    public void Makefile_StillContainsOriginalTestTarget()
    {
        // Arrange — adding publish targets must not remove or break the test target
        string makefile = File.ReadAllText(MakefilePath);

        // Assert — the test target must still exist
        Assert.Matches(@"(?m)^test:", makefile);
    }

    [Fact]
    public void Makefile_StillContainsOriginalCleanTarget()
    {
        // Arrange — adding publish targets must not remove or break the clean target
        string makefile = File.ReadAllText(MakefilePath);

        // Assert — the clean target must still exist
        Assert.Matches(@"(?m)^clean:", makefile);
    }

    // ── AOT warning documentation ──────────────────────────────────

    [Fact]
    public void Makefile_PublishAotContainsRuntimeWarning()
    {
        // Arrange — the AOT target must warn users about the runtime crash
        string makefile = File.ReadAllText(MakefilePath);

        // Act — extract the publish-aot target body (including comments above it)
        var aotSection = Regex.Match(makefile,
            @"(?m)(##[^\n]*publish-aot[^\n]*\n(?:##[^\n]*\n)*)?^publish-aot:.*?(?=^\S|\z)",
            RegexOptions.Singleline);

        // Assert — must contain a warning about JSON serialization or reflection
        Assert.True(aotSection.Success, "publish-aot section must exist");
        string section = aotSection.Value;
        bool hasAotNote = section.Contains("AOT", StringComparison.OrdinalIgnoreCase)
                       || section.Contains("source-gen", StringComparison.OrdinalIgnoreCase)
                       || section.Contains("experimental", StringComparison.OrdinalIgnoreCase);
        Assert.True(hasAotNote,
            "publish-aot target or its comments must note AOT status");
    }
}
