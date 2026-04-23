using AzureOpenAI_CLI.Tools;

namespace AzureOpenAI_CLI.Tests.Adversary;

/// <summary>
/// Adversarial coverage for the <c>ReadFileTool</c> sensitive-path blocklist.
/// Filed by S02E26 *The Locked Drawer* (Newman) to close the 7
/// <c>e23-readfile-*</c> gaps logged in S02E23 *The Adversary*:
///   ssh-userdir, kube-config, gnupg, netrc, docker-config,
///   git-credentials, npmrc-pypirc. Plus evasion patterns (NFKC
///   lookalikes, percent-encoding, trailing slash, double slash,
///   symlink through <c>/tmp</c>).
///
/// Each fact has a rationale comment -- do not strip, per the
/// "every test has a rationale" rule in the Newman hardening spec.
/// No <c>Skip=</c> attributes in this file (and none allowed: if a
/// case cannot run on a given platform, gate it on runtime not on
/// <c>Skip</c>).
/// </summary>
public class ReadFileSensitivePathTests
{
    private static string Home =>
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    // ═══════════════════════════════════════════════════════════════════
    // 1. The 7 e23-readfile-* home-dir paths -- each must be blocked
    //    whether addressed via tilde-expansion or via the already-expanded
    //    absolute path.
    // ═══════════════════════════════════════════════════════════════════

    public static IEnumerable<object[]> SensitiveHomePaths => new[]
    {
        // e23-readfile-ssh-userdir -- OpenSSH user key material
        new object[] { "~/.ssh/id_rsa" },
        new object[] { "~/.ssh/id_ed25519" },
        new object[] { "~/.ssh/known_hosts" },
        new object[] { "~/.ssh/authorized_keys" },

        // e23-readfile-kube-config -- kubectl cluster creds + tokens
        new object[] { "~/.kube/config" },
        new object[] { "~/.kube/cache/token" },

        // e23-readfile-gnupg -- GPG private keyring
        new object[] { "~/.gnupg/private-keys-v1.d/anything.key" },
        new object[] { "~/.gnupg/trustdb.gpg" },

        // e23-readfile-netrc -- machine/login/password creds (curl, git, ftp)
        new object[] { "~/.netrc" },

        // e23-readfile-docker-config -- `docker login` registry auth tokens
        new object[] { "~/.docker/config.json" },

        // e23-readfile-git-credentials -- unencrypted git credential store
        new object[] { "~/.git-credentials" },
        new object[] { "~/.config/git/credentials" },

        // e23-readfile-npmrc-pypirc -- package-registry upload tokens
        new object[] { "~/.npmrc" },
        new object[] { "~/.pypirc" },
    };

    [Theory]
    [MemberData(nameof(SensitiveHomePaths))]
    public async Task ReadAsync_TildeForm_Blocked(string tildePath)
    {
        // Rationale: the 7 e23-readfile-* findings -- these credential
        // stores must be rejected before any filesystem access.
        var result = await ReadFileTool.ReadAsync(tildePath, CancellationToken.None);
        Assert.StartsWith("Error: access to", result);
        Assert.Contains("blocked", result);
    }

    [Theory]
    [MemberData(nameof(SensitiveHomePaths))]
    public async Task ReadAsync_ExpandedAbsoluteForm_Blocked(string tildePath)
    {
        // Rationale: if the LLM pre-expands "~" itself, the blocklist must
        // still trigger on the resulting absolute path.
        var expanded = Path.Combine(Home, tildePath[1..].TrimStart('/'));
        var result = await ReadFileTool.ReadAsync(expanded, CancellationToken.None);
        Assert.StartsWith("Error: access to", result);
        Assert.Contains("blocked", result);
    }

    [Theory]
    [MemberData(nameof(SensitiveHomePaths))]
    public void IsBlockedPath_ExpandedForm_ReturnsTrue(string tildePath)
    {
        // Rationale: pin the canonical-form prefix check independent of
        // the pipeline; future refactors of Validate must not regress
        // the core blocklist semantics.
        var expanded = Path.Combine(Home, tildePath[1..].TrimStart('/'));
        Assert.True(
            ReadFileTool.IsBlockedPath(expanded),
            $"expected IsBlockedPath to return true for {expanded}");
    }

    // ═══════════════════════════════════════════════════════════════════
    // 2. Evasion patterns -- the structural pipeline (NFKC + canonicalize
    //    + prefix match) must defeat these without a substring-on-raw
    //    fallback.
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task ReadAsync_NfkcFullwidthDotInDotSsh_Blocked()
    {
        // Rationale: Unicode fullwidth dot U+FF0E NFKC-normalizes to ASCII
        // ".". Without NFKC the prefix match "~/.ssh" would miss
        // "~/\uFF0Essh" and leak the key material. Same class of bypass
        // as E32's fullwidth-rm attack.
        var bypass = "~/\uFF0Essh/id_rsa";
        var result = await ReadFileTool.ReadAsync(bypass, CancellationToken.None);
        Assert.StartsWith("Error: access to", result);
        Assert.Contains("blocked", result);
    }

    [Fact]
    public async Task ReadAsync_PercentEncodedDotSsh_Rejected()
    {
        // Rationale: .NET file APIs do not URL-decode, so "%2Essh" is a
        // literal dirname on disk. A caller passing "%2E" is trying to
        // hide ".ssh" from the blocklist. Default-deny.
        var bypass = "~/%2Essh/id_rsa";
        var result = await ReadFileTool.ReadAsync(bypass, CancellationToken.None);
        Assert.StartsWith("Error:", result);
        Assert.Contains("percent-encoded", result);
    }

    [Fact]
    public async Task ReadAsync_PercentEncodedSlash_Rejected()
    {
        // Rationale: "%2F" evasion for directory separators. Same threat
        // model as above.
        var bypass = "~/.ssh%2Fid_rsa";
        var result = await ReadFileTool.ReadAsync(bypass, CancellationToken.None);
        Assert.StartsWith("Error:", result);
        Assert.Contains("percent-encoded", result);
    }

    [Fact]
    public async Task ReadAsync_NullByteTruncation_Rejected()
    {
        // Rationale: classic null-byte truncation attack -- if anything
        // downstream passes the path through a C-string API, "\0" would
        // truncate "/etc/hostname" off and leave "~/.ssh/id_rsa".
        var bypass = "~/.ssh/id_rsa\0/etc/hostname";
        var result = await ReadFileTool.ReadAsync(bypass, CancellationToken.None);
        Assert.StartsWith("Error:", result);
        Assert.Contains("control", result);
    }

    [Fact]
    public async Task ReadAsync_TrailingSlashOnDir_Blocked()
    {
        // Rationale: the blocklist entry is the directory "~/.ssh".
        // "~/.ssh/" must match too -- Path.GetFullPath strips trailing
        // separators, and IsBlockedPath checks the exact-equal case.
        var result = await ReadFileTool.ReadAsync("~/.ssh/", CancellationToken.None);
        Assert.StartsWith("Error: access to", result);
        Assert.Contains("blocked", result);
    }

    [Fact]
    public async Task ReadAsync_DoubleSlashInPath_Blocked()
    {
        // Rationale: "~/.ssh//id_rsa" must collapse through canonicalize
        // to "<home>/.ssh/id_rsa" and hit the blocklist. Raw-substring
        // prefix match would miss this if the blocklist stored "/.ssh/".
        var result = await ReadFileTool.ReadAsync("~/.ssh//id_rsa", CancellationToken.None);
        Assert.StartsWith("Error: access to", result);
        Assert.Contains("blocked", result);
    }

    [Fact]
    public async Task ReadAsync_DotDotTraversalIntoDotSsh_Blocked()
    {
        // Rationale: "/tmp/../<home>/.ssh/id_rsa" should canonicalize to
        // "<home>/.ssh/id_rsa" and hit the blocklist. The pipeline must
        // canonicalize BEFORE matching, not match on the raw input.
        var traversal = $"/tmp/../{Home.TrimStart('/')}/.ssh/id_rsa";
        var result = await ReadFileTool.ReadAsync(traversal, CancellationToken.None);
        Assert.StartsWith("Error: access to", result);
        Assert.Contains("blocked", result);
    }

    [Fact]
    public async Task ReadAsync_CaseVariantOnLinux_Blocked()
    {
        // Rationale: IsBlockedPath uses OrdinalIgnoreCase, so "~/.SSH"
        // must still hit even on case-sensitive Linux. Defence-in-depth
        // for filesystems (HFS+, APFS, exFAT) that are case-insensitive.
        var result = await ReadFileTool.ReadAsync("~/.SSH/id_rsa", CancellationToken.None);
        Assert.StartsWith("Error: access to", result);
        Assert.Contains("blocked", result);
    }

    [Fact]
    public async Task ReadAsync_SymlinkThroughTmpToBlockedTarget_Blocked()
    {
        // Rationale: create a symlink in /tmp whose real target is an
        // already-blocked sensitive path (/etc/shadow, which exists on
        // Linux CI). The logical path passes the prefix check, but the
        // symlink-resolution stage must catch the blocked final target.
        //
        // Skipped-at-runtime (not via [Skip=]) if /etc/shadow is absent
        // or we lack permission to stat it -- e.g. Windows, some
        // unprivileged containers. The skip is conditional, not wholesale.
        const string target = "/etc/shadow";
        if (!File.Exists(target))
            return;

        var linkPath = Path.Combine(Path.GetTempPath(), $"newman-e26-link-{Guid.NewGuid():N}");
        try
        {
            File.CreateSymbolicLink(linkPath, target);
        }
        catch (UnauthorizedAccessException)
        {
            return; // no symlink privilege on this runner
        }
        catch (IOException)
        {
            return;
        }

        try
        {
            var result = await ReadFileTool.ReadAsync(linkPath, CancellationToken.None);
            Assert.StartsWith("Error:", result);
            Assert.Contains("blocked", result);
        }
        finally
        {
            try { File.Delete(linkPath); } catch { /* best-effort cleanup */ }
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // 3. Negative controls -- legitimate paths must still pass validation
    //    so we know the blocklist is not over-broad.
    // ═══════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("/tmp/not-sensitive.txt")]
    [InlineData("/usr/share/doc/readme")]
    public void IsBlockedPath_NonSensitive_NotBlocked(string path)
    {
        // Rationale: regression gate against future blocklist widening
        // that would break legitimate reads.
        Assert.False(ReadFileTool.IsBlockedPath(path));
    }
}
