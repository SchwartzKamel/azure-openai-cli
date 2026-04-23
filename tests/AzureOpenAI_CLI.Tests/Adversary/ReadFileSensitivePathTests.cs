using System.Text.Json;
using AzureOpenAI_CLI.Tools;

namespace AzureOpenAI_CLI.Tests.Adversary;

/// <summary>
/// S02E23 -- The Adversary. FDR's path-traversal, symlink, case-folding,
/// and Unicode-normalization attacks against
/// <see cref="ReadFileTool"/>'s sensitive-path blocklist.
///
/// Tests confirming the CURRENT defense are <c>[Fact]</c>. Tests that
/// surface a gap (sensitive home-directory path absent from the
/// blocklist) are <c>[Fact(Skip = "Live finding: e23-...")]</c>.
/// Cross-references S02E13 finding
/// <c>e13-readfile-blocklist-home-dir-gap</c> -- this episode pins the
/// individual paths so the S02E26 *Locked Drawer* fix can flip each
/// Skip to a passing assertion one-by-one.
/// </summary>
public class ReadFileSensitivePathTests
{
    private static JsonElement Args(string path)
    {
        var json = JsonSerializer.Serialize(new { path });
        return JsonDocument.Parse(json).RootElement;
    }

    private static string Home =>
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    // ===================================================================
    // Defenses that hold today
    // ===================================================================

    [Theory]
    [InlineData("/etc/shadow")]
    [InlineData("/etc/passwd")]
    [InlineData("/etc/sudoers")]
    [InlineData("/etc/hosts")]
    [InlineData("/proc/self/environ")]
    [InlineData("/proc/self/cmdline")]
    [InlineData("/var/run/secrets")]
    [InlineData("/run/secrets")]
    [InlineData("/var/run/docker.sock")]
    public async Task Read_DirectAbsolutePath_Rejected(string path)
    {
        var tool = new ReadFileTool();
        var result = await tool.ExecuteAsync(Args(path), CancellationToken.None);
        Assert.StartsWith("Error:", result);
        Assert.Contains("blocked", result);
    }

    [Theory]
    [InlineData("/etc/passwd/../shadow")]
    [InlineData("/etc//shadow")]
    [InlineData("/etc/./shadow")]
    [InlineData("/etc/foo/../shadow")]
    public async Task Read_PathTraversal_NormalizesAndRejects(string path)
    {
        // Path.GetFullPath collapses the .. and duplicate separators
        // before the blocklist check, so each variant resolves to
        // /etc/shadow and is blocked.
        var tool = new ReadFileTool();
        var result = await tool.ExecuteAsync(Args(path), CancellationToken.None);
        Assert.StartsWith("Error:", result);
        Assert.Contains("blocked", result);
    }

    [Fact]
    public void Read_CaseFolding_BlocklistIsCaseInsensitive()
    {
        // OrdinalIgnoreCase comparison means /ETC/SHADOW matches even
        // on Linux where the filesystem is case-sensitive. Defense is
        // a strict superset of what the kernel enforces -- harmless
        // false positives are acceptable.
        Assert.True(ReadFileTool.IsBlockedPath("/ETC/SHADOW"));
        Assert.True(ReadFileTool.IsBlockedPath("/Etc/Shadow"));
        Assert.True(ReadFileTool.IsBlockedPath("/etc/SHADOW"));
    }

    [Fact]
    public async Task Read_TildeExpansion_AwsCredentials_Rejected()
    {
        var tool = new ReadFileTool();
        var result = await tool.ExecuteAsync(Args("~/.aws/credentials"), CancellationToken.None);
        // Either blocked outright or "file not found" if the path doesn't exist;
        // critically, must not return file contents.
        Assert.StartsWith("Error:", result);
    }

    [Fact]
    public void Read_TildeExpansion_AzureDir_IsBlocked()
    {
        var tool = new ReadFileTool();
        var probe = System.IO.Path.Combine(Home, ".azure", "accessTokens.json");
        Assert.True(ReadFileTool.IsBlockedPath(probe));
    }

    [Fact]
    public void Read_DotEnv_IsBlocked()
    {
        Assert.True(ReadFileTool.IsBlockedPath("/some/project/.env"));
        Assert.True(ReadFileTool.IsBlockedPath("/some/project/PROD.env"));
    }

    [Fact]
    public void Read_DotEnvExample_IsAllowed()
    {
        Assert.False(ReadFileTool.IsBlockedPath("/some/project/.env.example"));
        Assert.False(ReadFileTool.IsBlockedPath("/some/project/.env.sample"));
        Assert.False(ReadFileTool.IsBlockedPath("/some/project/.env.template"));
    }

    // ===================================================================
    // LIVE FINDINGS: sensitive home-dir paths NOT in the current blocklist
    // (cross-refs S02E13 finding e13-readfile-blocklist-home-dir-gap;
    //  individual paths pinned here for S02E26 follow-up.)
    // ===================================================================

    [Fact(Skip = "Live finding: e23-readfile-ssh-userdir-not-blocked")]
    public void IsBlockedPath_UserSshDir_ShouldBeBlocked()
    {
        // Blocklist covers /root/.ssh but NOT ~/.ssh for non-root users.
        // Most attacker-relevant SSH keys live under the invoking user's
        // home, not /root.
        var probe = System.IO.Path.Combine(Home, ".ssh", "id_rsa");
        Assert.True(ReadFileTool.IsBlockedPath(probe));
    }

    [Fact(Skip = "Live finding: e23-readfile-kube-config-not-blocked")]
    public void IsBlockedPath_KubeConfig_ShouldBeBlocked()
    {
        var probe = System.IO.Path.Combine(Home, ".kube", "config");
        Assert.True(ReadFileTool.IsBlockedPath(probe));
    }

    [Fact(Skip = "Live finding: e23-readfile-gnupg-not-blocked")]
    public void IsBlockedPath_GnuPGDir_ShouldBeBlocked()
    {
        var probe = System.IO.Path.Combine(Home, ".gnupg", "private-keys-v1.d");
        Assert.True(ReadFileTool.IsBlockedPath(probe));
    }

    [Fact(Skip = "Live finding: e23-readfile-netrc-not-blocked")]
    public void IsBlockedPath_Netrc_ShouldBeBlocked()
    {
        var probe = System.IO.Path.Combine(Home, ".netrc");
        Assert.True(ReadFileTool.IsBlockedPath(probe));
    }

    [Fact(Skip = "Live finding: e23-readfile-docker-config-not-blocked")]
    public void IsBlockedPath_DockerConfig_ShouldBeBlocked()
    {
        var probe = System.IO.Path.Combine(Home, ".docker", "config.json");
        Assert.True(ReadFileTool.IsBlockedPath(probe));
    }

    [Fact(Skip = "Live finding: e23-readfile-git-credentials-not-blocked")]
    public void IsBlockedPath_GitCredentials_ShouldBeBlocked()
    {
        var probe = System.IO.Path.Combine(Home, ".git-credentials");
        Assert.True(ReadFileTool.IsBlockedPath(probe));
    }

    [Fact(Skip = "Live finding: e23-readfile-npmrc-pypirc-not-blocked")]
    public void IsBlockedPath_PackageRegistryAuth_ShouldBeBlocked()
    {
        // ~/.npmrc and ~/.pypirc commonly contain registry auth tokens.
        var npmrc = System.IO.Path.Combine(Home, ".npmrc");
        var pypirc = System.IO.Path.Combine(Home, ".pypirc");
        Assert.True(ReadFileTool.IsBlockedPath(npmrc));
        Assert.True(ReadFileTool.IsBlockedPath(pypirc));
    }

    // ===================================================================
    // Edge cases (documenting current behavior, no Skip needed)
    // ===================================================================

    [Fact]
    public void IsBlockedPath_UnicodeNormalizationVariant_NotBlocked_ByDesign()
    {
        // Filesystems on Linux store paths as opaque byte strings.
        // A composed/decomposed Unicode variant of "/etc/shadow" is a
        // distinct byte string and would resolve to a different file
        // (or not at all) -- the blocklist comparing byte-equal is not
        // a vulnerability because the kernel will not open the secret
        // file at the variant path either. Pinned here so the
        // by-design status is not relitigated.
        var combining = "/etc/shado\u0077\u0301"; // 'w' + combining acute
        Assert.False(ReadFileTool.IsBlockedPath(combining));
    }

    [Fact]
    public void IsBlockedPath_PrefixCollision_NotOverblocked()
    {
        // /etc/shadowbackup must NOT match /etc/shadow even though the
        // strings share a prefix. Defense uses prefix + separator.
        Assert.False(ReadFileTool.IsBlockedPath("/etc/shadowbackup"));
        Assert.False(ReadFileTool.IsBlockedPath("/etc/passwd_old"));
    }
}
