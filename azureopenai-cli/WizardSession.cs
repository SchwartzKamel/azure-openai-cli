using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

namespace AzureOpenAI_CLI;

// S03E11 -- The Wizard, Reprise.
//
// Pure-function builder for the env file content the interactive wizard
// persists. Lifted out of SetupWizard so the prompt-sequence state machine
// can be exercised hermetically -- no Console, no stdin, no sleeps. Tests
// feed in canned answers, assert on the resulting string + on the
// idempotency contract (same answers in => byte-identical body, modulo the
// single timestamp comment line). The wizard layer wraps this with prompts,
// validation, and the file-system write + chmod 600 + backup dance.

/// <summary>
/// Canonical provider names recognised by the wizard. Mirrors the
/// <c>KnownProviderSections</c> set in <see cref="Program.LoadConfigEnvFrom"/>
/// (E10) and the preset names in <see cref="OpenAiCompatAdapter.BuiltIn"/>
/// (E09). Stored as constants so callers compare with
/// <see cref="StringComparison.Ordinal"/> instead of free-form strings.
/// </summary>
internal static class WizardProviders
{
    internal const string Azure = "azure";
    internal const string OpenAI = "openai";
    internal const string Groq = "groq";
    internal const string Together = "together";
    internal const string Cloudflare = "cloudflare";

    /// <summary>Ordered for menu display + tests; do not reorder casually.</summary>
    internal static readonly string[] All = { Azure, OpenAI, Groq, Together, Cloudflare };

    /// <summary>Canonicalises a user-typed provider name to lowercase.</summary>
    internal static bool TryCanonicalize(string? input, out string canonical)
    {
        canonical = string.Empty;
        if (string.IsNullOrWhiteSpace(input)) return false;
        var t = input.Trim();
        foreach (var p in All)
        {
            if (string.Equals(t, p, StringComparison.OrdinalIgnoreCase))
            {
                canonical = p;
                return true;
            }
        }
        return false;
    }

    /// <summary>True for OpenAI-compatible (non-Azure) providers routed through E09.</summary>
    internal static bool IsCompat(string provider) =>
        string.Equals(provider, OpenAI, StringComparison.Ordinal)
        || string.Equals(provider, Groq, StringComparison.Ordinal)
        || string.Equals(provider, Together, StringComparison.Ordinal)
        || string.Equals(provider, Cloudflare, StringComparison.Ordinal);
}

/// <summary>
/// One provider's worth of wizard answers. Records, not classes -- the
/// builder treats them as immutable value objects. ApiKey lives in memory
/// only as long as the wizard is running; the SecretRedactor patterns from
/// E07/E10 cover any accidental log path.
/// </summary>
internal sealed record ProviderAnswer(
    string Provider,
    string ApiKey,
    string Models,
    string? Endpoint = null,
    string? AccountId = null);

/// <summary>
/// Pure-function builder for <c>~/.config/az-ai/env</c>. No I/O, no Console,
/// no env reads. Tests pass in a fixed timestamp to lock the body.
/// </summary>
internal static class WizardSession
{
    /// <summary>
    /// Resolve <c>~/.config/az-ai/env</c> using the same rules as
    /// <see cref="Program.LoadConfigEnv"/>. Honours <c>XDG_CONFIG_HOME</c>
    /// on Unix; falls back to <c>%APPDATA%/az-ai/env</c> on Windows.
    /// </summary>
    internal static string DefaultEnvFilePath()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appData, "az-ai", "env");
        }
        var xdg = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        var configHome = !string.IsNullOrWhiteSpace(xdg)
            ? xdg
            : Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".config");
        return Path.Combine(configHome, "az-ai", "env");
    }

    /// <summary>
    /// Validate a comma-separated compat model list against
    /// <see cref="OpenAiCompatAdapter.ParseCompatModels"/>'s contract. The
    /// wizard only collects bare model names per provider (e.g.
    /// <c>"gpt-4o,gpt-4o-mini"</c>); we synthesise <c>preset:model</c> pairs
    /// before validation so the user does not have to retype the preset.
    /// Returns null on success; otherwise a friendly rejection message.
    /// </summary>
    internal static string? ValidateCompatModels(string provider, string commaSeparated)
    {
        if (!WizardProviders.IsCompat(provider))
        {
            return null; // azure validation lives elsewhere
        }
        if (string.IsNullOrWhiteSpace(commaSeparated))
        {
            return "Model list is required (e.g. gpt-4o-mini or llama3-70b-8192).";
        }
        var synth = string.Join(",",
            commaSeparated
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(m => provider + ":" + m));
        try
        {
            var parsed = OpenAiCompatAdapter.ParseCompatModels(synth);
            if (parsed is null || parsed.Count == 0)
            {
                return "Model list is required (e.g. gpt-4o-mini or llama3-70b-8192).";
            }
            return null;
        }
        catch (ArgumentException ex)
        {
            // E09 throws ArgumentException with an actionable message; surface it.
            return ex.Message;
        }
    }

    /// <summary>
    /// Build the env-file body from a list of provider answers. The result
    /// is deterministic given the same inputs except for a single
    /// <c># Generated ...</c> timestamp comment, which idempotency tests
    /// strip before comparing.
    /// </summary>
    internal static string BuildEnvFileContent(
        IReadOnlyList<ProviderAnswer> answers,
        string defaultProvider,
        DateTimeOffset timestamp)
    {
        ArgumentNullException.ThrowIfNull(answers);
        if (answers.Count == 0)
        {
            throw new ArgumentException("At least one provider answer is required.", nameof(answers));
        }
        if (!WizardProviders.TryCanonicalize(defaultProvider, out var defProv))
        {
            throw new ArgumentException(
                "Default provider must be one of: " + string.Join(", ", WizardProviders.All),
                nameof(defaultProvider));
        }
        // Default provider must be in the answers set.
        if (!answers.Any(a => string.Equals(a.Provider, defProv, StringComparison.Ordinal)))
        {
            throw new ArgumentException(
                $"Default provider '{defProv}' is not among the configured providers.",
                nameof(defaultProvider));
        }

        var sb = new StringBuilder();
        // Header. The timestamp line is the ONLY non-deterministic surface.
        // Idempotency tests skip lines starting with "# Generated".
        sb.Append("# az-ai env file -- generated by `az-ai --setup` (S03E11).\n");
        sb.Append("# Generated ").Append(timestamp.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture)).Append('\n');
        sb.Append("# Edit by re-running `az-ai --setup` or hand-editing this file.\n");
        sb.Append("# File mode SHOULD be 0600 on Unix; the wizard enforces it on save.\n");
        sb.Append("# Provider sections use [provider:NAME] -- see README, S03E10.\n");
        sb.Append('\n');
        sb.Append("# Default provider: ").Append(defProv).Append('\n');
        sb.Append('\n');

        // ---- Default section (back-compat shell-export form) ----
        var azure = answers.FirstOrDefault(a => string.Equals(a.Provider, WizardProviders.Azure, StringComparison.Ordinal));
        if (azure is not null)
        {
            sb.Append("export AZUREOPENAIENDPOINT=\"").Append(EscapeForDoubleQuotes(azure.Endpoint ?? string.Empty)).Append("\"\n");
            sb.Append("export AZUREOPENAIAPI=\"").Append(EscapeForDoubleQuotes(azure.ApiKey)).Append("\"\n");
            sb.Append("export AZUREOPENAIMODEL=\"").Append(EscapeForDoubleQuotes(azure.Models)).Append("\"\n");
        }

        // AZ_AI_COMPAT_MODELS aggregates every compat provider's models
        // as preset:model pairs (E09 contract).
        var compatPairs = new List<string>();
        foreach (var a in answers.Where(a => WizardProviders.IsCompat(a.Provider)))
        {
            foreach (var m in a.Models.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                compatPairs.Add(a.Provider + ":" + m);
            }
        }
        if (compatPairs.Count > 0)
        {
            sb.Append("export AZ_AI_COMPAT_MODELS=\"").Append(EscapeForDoubleQuotes(string.Join(",", compatPairs))).Append("\"\n");
        }

        // Cloudflare account id is not a secret per se but lives outside
        // the per-provider section because the URL rewrite happens before
        // section-namespacing kicks in (E09 Build()).
        var cf = answers.FirstOrDefault(a => string.Equals(a.Provider, WizardProviders.Cloudflare, StringComparison.Ordinal));
        if (cf is not null && !string.IsNullOrWhiteSpace(cf.AccountId))
        {
            sb.Append("export CLOUDFLARE_ACCOUNT_ID=\"").Append(EscapeForDoubleQuotes(cf.AccountId)).Append("\"\n");
        }

        // ---- Per-provider sections (E10 format) ----
        // Skip [provider:azure] -- the default section already carries
        // AZUREOPENAIAPI; doubling up would just leak secrets in two slots.
        foreach (var a in answers.Where(a => !string.Equals(a.Provider, WizardProviders.Azure, StringComparison.Ordinal)))
        {
            sb.Append('\n');
            sb.Append('[').Append("provider:").Append(a.Provider).Append("]\n");
            // Cloudflare's E09 preset reads CLOUDFLARE_API_TOKEN, not _API_KEY.
            // The loader namespaces bare "API_TOKEN" under [provider:cloudflare]
            // to "CLOUDFLARE_API_TOKEN" via uppercase + prefix.
            var keyName = string.Equals(a.Provider, WizardProviders.Cloudflare, StringComparison.Ordinal)
                ? "API_TOKEN"
                : "API_KEY";
            sb.Append(keyName).Append('=').Append(a.ApiKey).Append('\n');
        }

        return sb.ToString();
    }

    /// <summary>
    /// Strip the single non-deterministic timestamp comment line so two
    /// invocations with identical answers compare byte-for-byte. Used by
    /// the idempotency tests AND by the wizard itself when deciding
    /// whether a backup is worth taking.
    /// </summary>
    internal static string StripTimestampComment(string content)
    {
        if (string.IsNullOrEmpty(content)) return content;
        var sb = new StringBuilder(content.Length);
        foreach (var line in content.Split('\n'))
        {
            if (line.StartsWith("# Generated ", StringComparison.Ordinal)) continue;
            sb.Append(line).Append('\n');
        }
        // Trim the trailing extra '\n' we always append.
        if (sb.Length > 0 && sb[^1] == '\n') sb.Length -= 1;
        return sb.ToString();
    }

    /// <summary>
    /// Persist <paramref name="content"/> to <paramref name="path"/>. If the
    /// file already exists, the caller is responsible for having confirmed
    /// the overwrite; this helper handles the backup atomically. Returns
    /// the backup path created (or null if no pre-existing file).
    /// </summary>
    internal static string? WriteEnvFile(string path, string content, DateTimeOffset timestamp)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(content);

        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        string? backup = null;
        if (File.Exists(path))
        {
            // Idempotency check: don't take a backup when the new content
            // is byte-identical to the old (modulo the timestamp comment).
            var existing = File.ReadAllText(path);
            if (string.Equals(
                    StripTimestampComment(existing),
                    StripTimestampComment(content),
                    StringComparison.Ordinal))
            {
                // No-op write: rewrite (so the timestamp refreshes) but no backup.
                File.WriteAllText(path, content);
                SetRestrictivePermissions(path);
                return null;
            }
            // Different content: backup, then overwrite.
            backup = path + ".bak."
                + timestamp.ToUniversalTime().ToString("yyyyMMddTHHmmssZ", CultureInfo.InvariantCulture);
            File.Copy(path, backup, overwrite: false);
            SetRestrictivePermissions(backup);
        }

        File.WriteAllText(path, content);
        SetRestrictivePermissions(path);
        return backup;
    }

    private static void SetRestrictivePermissions(string path)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;
        try
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        catch
        {
            // Best-effort -- mirrors UserConfig and Preferences precedent.
        }
    }

    /// <summary>
    /// Escape a value for inclusion inside a shell-style double-quoted
    /// export line. We only escape <c>"</c>, <c>\</c>, <c>$</c>, and
    /// backtick because the loader (<see cref="Program.LoadConfigEnvFrom"/>)
    /// treats the inside of quotes as a literal string and does no
    /// expansion -- this is belt-and-braces for users who hand-source the
    /// file from a real shell.
    /// </summary>
    private static string EscapeForDoubleQuotes(string value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        var sb = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            if (ch == '\\' || ch == '"' || ch == '$' || ch == '`')
            {
                sb.Append('\\');
            }
            sb.Append(ch);
        }
        return sb.ToString();
    }
}
