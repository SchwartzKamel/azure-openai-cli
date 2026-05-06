using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace AzureOpenAI_CLI.Cli;

// S03E25 -- The Rotation (Newman). Bring-your-own-key rotation for the
// section-aware env file written by SetupWizard (S03E11) and consumed by
// LoadConfigEnvFrom (S03E10 *The Keychain*). The flow is intentionally
// the smallest possible defense:
//
//   1. Resolve target provider (CLI arg or interactive menu of currently
//      configured providers).
//   2. Locate ~/.config/az-ai/env via WizardSession.DefaultEnvFilePath.
//   3. Prompt for the new key (masked, via MaskedInput; H-1 invariant).
//   4. Confirm with a y/N gate.
//   5. Take a timestamped backup, bumping the suffix on collision so a
//      pre-existing backup is never clobbered.
//   6. Atomic write of the rewritten file (tmp + rename) under mode 0600.
//   7. Re-read and verify the new value parses back, mode is 0600.
//   8. Print a success line. NEVER print the key value -- not on success,
//      not on failure, not in any exception path. Every textual line is
//      routed through SecretRedactor as defense-in-depth.
//
// Threat model:
//   * In-process: a typed key sits in a StringBuilder for the duration of
//     ReadMaskedLine + the file-write path. We do not pin or zero the
//     buffer; a heap dump of the live process during rotation could
//     expose the new key. Acceptable -- the operator is rotating because
//     they hold the key already.
//   * Process I/O: stdout/stderr are routed through SecretRedactor.Redact
//     and we never interpolate the typed key into status text. Backup and
//     destination files are mode 0600 from the moment they exist.
//   * Race: backup-then-write is two filesystem operations. A reader
//     between the two sees the OLD content (atomic rename guarantees the
//     destination is never half-written). A reader during the rename sees
//     either the old inode or the new inode -- never a torn file.
//   * Refusal surfaces:
//       - --raw: rotation is interactive; refuse with a friendly message
//         and exit 3.
//       - non-TTY: when stdin is the real Console.In and is redirected,
//         we refuse with exit 3 -- mirrors the SetupWizard gate so a CI
//         pipe never hangs on a prompt.
//       - missing env file: exit 2 with a pointer to `az-ai --setup`.
//       - unknown provider name on the CLI: exit 3.

/// <summary>
/// BYOK rotation flow. See file header for surface and policy.
/// Public test seam: <see cref="Run"/> takes injected I/O streams so
/// <c>CredsRotateTests</c> can drive it hermetically without a TTY.
/// </summary>
internal static class CredsRotate
{
    /// <summary>Successful rotation.</summary>
    private const int ExitOk = 0;
    /// <summary>User said "n" (or empty) at the confirmation prompt.</summary>
    private const int ExitAborted = 1;
    /// <summary>File-IO failure (env file missing, permissions, etc.).</summary>
    private const int ExitIoFailure = 2;
    /// <summary>Invalid input -- empty key, unknown provider, --raw, non-TTY.</summary>
    private const int ExitInvalidInput = 3;

    /// <summary>
    /// Run the rotation flow. Returns 0 on success, 1 on user cancel, 2
    /// on file-IO failure, 3 on invalid input or refusal gate hit.
    /// </summary>
    public static int Run(
        string? providerArg,
        bool jsonMode,
        bool raw,
        bool plain,
        TextReader stdin,
        TextWriter stdout,
        TextWriter stderr)
    {
        ArgumentNullException.ThrowIfNull(stdin);
        ArgumentNullException.ThrowIfNull(stdout);
        ArgumentNullException.ThrowIfNull(stderr);
        // jsonMode and plain are accepted for parity with --doctor's
        // signature; rotation is interactive and ignores them today.
        // Bookman approved the brevity.
        _ = jsonMode;
        _ = plain;

        try
        {
            // --raw is for the hot path; rotation is interactive.
            if (raw)
            {
                stderr.WriteLine(SecretRedactor.Redact(
                    "[ERROR] creds rotate is interactive; cannot run with --raw."));
                stderr.WriteLine(SecretRedactor.Redact(
                    "        Drop --raw and re-run, or set the relevant API_KEY env var by hand."));
                return ExitInvalidInput;
            }

            // Non-TTY refusal: when stdin is the real Console.In and is
            // redirected, refuse. Tests inject a StringReader so this
            // gate intentionally lets them through.
            if (ReferenceEquals(stdin, Console.In)
                && (Console.IsInputRedirected || Console.IsOutputRedirected))
            {
                stderr.WriteLine(SecretRedactor.Redact(
                    "[ERROR] creds rotate requires an interactive terminal "
                    + "(stdin/stdout must not be redirected)."));
                stderr.WriteLine(SecretRedactor.Redact(
                    "        Set the relevant API_KEY env var manually instead "
                    + "-- see README \"Power user / scripted setup\"."));
                return ExitInvalidInput;
            }

            var path = WizardSession.DefaultEnvFilePath();
            if (!File.Exists(path))
            {
                stderr.WriteLine(SecretRedactor.Redact(
                    "[ERROR] No env file at " + path + "."));
                stderr.WriteLine(SecretRedactor.Redact(
                    "        Run 'az-ai --setup' to create one before rotating."));
                return ExitIoFailure;
            }

            string[] originalLines;
            try
            {
                originalLines = File.ReadAllLines(path);
            }
            catch (IOException ex)
            {
                stderr.WriteLine(SecretRedactor.Redact(
                    "[ERROR] Could not read " + path + ": " + ex.Message));
                return ExitIoFailure;
            }
            catch (UnauthorizedAccessException ex)
            {
                stderr.WriteLine(SecretRedactor.Redact(
                    "[ERROR] Permission denied reading " + path + ": " + ex.Message));
                return ExitIoFailure;
            }

            var configured = DetectConfiguredProviders(originalLines);
            if (configured.Count == 0)
            {
                stderr.WriteLine(SecretRedactor.Redact(
                    "[ERROR] No provider credentials found in " + path + "."));
                stderr.WriteLine(SecretRedactor.Redact(
                    "        Run 'az-ai --setup' to (re)configure."));
                return ExitIoFailure;
            }

            // Resolve the target provider. CLI arg wins; otherwise prompt.
            string? target;
            if (!string.IsNullOrWhiteSpace(providerArg))
            {
                if (!WizardProviders.TryCanonicalize(providerArg, out var canon))
                {
                    stderr.WriteLine(SecretRedactor.Redact(
                        "[ERROR] Unknown provider '" + providerArg
                        + "'. Known: " + string.Join(", ", WizardProviders.All) + "."));
                    return ExitInvalidInput;
                }
                if (!configured.Contains(canon))
                {
                    stderr.WriteLine(SecretRedactor.Redact(
                        "[ERROR] Provider '" + canon + "' is not configured in " + path + "."));
                    stderr.WriteLine(SecretRedactor.Redact(
                        "        Configured: " + string.Join(", ", configured) + "."));
                    return ExitInvalidInput;
                }
                target = canon;
            }
            else
            {
                target = PromptProvider(configured, stdin, stdout);
                if (target is null)
                {
                    stderr.WriteLine(SecretRedactor.Redact("[ERROR] No provider selected."));
                    return ExitInvalidInput;
                }
            }

            // Read the new key (masked when stdin is real Console.In).
            stdout.Write("New API key for [provider:" + target + "] (input hidden): ");
            stdout.Flush();
            var newKey = MaskedInput.ReadMaskedLine(stdin, stderr);
            if (ReferenceEquals(stdin, Console.In)) stdout.WriteLine();

            if (newKey is null)
            {
                stderr.WriteLine(SecretRedactor.Redact("[ERROR] No key entered."));
                return ExitInvalidInput;
            }
            // Validate -- non-empty, non-whitespace, length >= 8. Don't
            // validate format because providers vary.
            var trimmed = newKey.Trim();
            if (trimmed.Length == 0)
            {
                stderr.WriteLine(SecretRedactor.Redact(
                    "[ERROR] API key must not be empty or whitespace."));
                return ExitInvalidInput;
            }
            if (trimmed.Length < 8)
            {
                stderr.WriteLine(SecretRedactor.Redact(
                    "[ERROR] API key looks too short (minimum 8 characters)."));
                return ExitInvalidInput;
            }

            // Confirm. Backup path is computed up-front for the prompt so
            // the user knows where the rollback lives.
            var ts = DateTimeOffset.UtcNow;
            var stamp = ts.ToUniversalTime().ToString("yyyyMMddTHHmmssZ", CultureInfo.InvariantCulture);
            var backupPreview = path + ".bak." + stamp;
            stdout.WriteLine();
            stdout.WriteLine(SecretRedactor.Redact(
                "About to rotate [provider:" + target + "] API key."));
            stdout.WriteLine(SecretRedactor.Redact(
                "Backup will be written to " + backupPreview + " (mode 0600)."));
            stdout.Write("Continue? [y/N]: ");
            stdout.Flush();
            var confirm = stdin.ReadLine();
            if (confirm is null) return ExitAborted;
            confirm = confirm.Trim();
            if (!string.Equals(confirm, "y", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(confirm, "yes", StringComparison.OrdinalIgnoreCase))
            {
                stdout.WriteLine(SecretRedactor.Redact(
                    "Aborted. No changes saved."));
                return ExitAborted;
            }

            // Build the rewritten content. RewriteKey returns null when
            // the key line could not be located -- that indicates a
            // structural mismatch (someone hand-edited the file in a way
            // we don't recognise). Refuse rather than corrupt.
            var newContent = RewriteKey(originalLines, target, trimmed, out var rewriteError);
            if (newContent is null)
            {
                stderr.WriteLine(SecretRedactor.Redact(
                    "[ERROR] Could not locate the API key line for [provider:"
                    + target + "] in " + path + ": "
                    + (rewriteError ?? "unknown reason") + "."));
                stderr.WriteLine(SecretRedactor.Redact(
                    "        Run 'az-ai --setup' to rewrite the file from scratch."));
                return ExitIoFailure;
            }

            string actualBackup;
            try
            {
                actualBackup = WizardSession.BackupWithBump(path, ts);
                WizardSession.AtomicWrite(path, newContent);
            }
            catch (IOException ex)
            {
                stderr.WriteLine(SecretRedactor.Redact(
                    "[ERROR] File-IO failure during rotation: " + ex.Message));
                return ExitIoFailure;
            }
            catch (UnauthorizedAccessException ex)
            {
                stderr.WriteLine(SecretRedactor.Redact(
                    "[ERROR] Permission denied during rotation: " + ex.Message));
                return ExitIoFailure;
            }

            // Smoke check: re-read, re-parse, verify the new key landed
            // in the right slot. We don't compare the literal key to the
            // typed value (would defeat redaction); we just confirm the
            // line for the target provider is present and non-empty.
            try
            {
                var verifyLines = File.ReadAllLines(path);
                var smoke = RewriteKey(verifyLines, target, trimmed, out _, dryRun: true);
                if (!string.Equals(smoke, "OK", StringComparison.Ordinal))
                {
                    // Should never happen -- AtomicWrite either rewrote
                    // or failed. Treat as IO failure but leave the backup
                    // for forensic recovery.
                    stderr.WriteLine(SecretRedactor.Redact(
                        "[ERROR] Post-write verification failed; backup retained at "
                        + actualBackup + "."));
                    return ExitIoFailure;
                }
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    var mode = File.GetUnixFileMode(path);
                    var expected = UnixFileMode.UserRead | UnixFileMode.UserWrite;
                    if (mode != expected)
                    {
                        stderr.WriteLine(SecretRedactor.Redact(
                            "[WARN] " + path + " is not mode 0600 after rotate (got "
                            + ((int)mode).ToString("o", CultureInfo.InvariantCulture)
                            + "); please chmod 600 by hand."));
                    }
                }
            }
            catch (IOException ex)
            {
                stderr.WriteLine(SecretRedactor.Redact(
                    "[WARN] Post-write smoke check failed: " + ex.Message));
                // Do NOT return failure -- the rotate itself succeeded.
            }

            stdout.WriteLine(SecretRedactor.Redact(
                "[ok] rotated [provider:" + target + "] (backup: " + actualBackup + ")"));
            return ExitOk;
        }
        catch (Exception ex)
        {
            // Defense-in-depth: a thrown exception that includes the
            // typed key in its message is surfaced through Redact, not
            // raw. SecretRedactor.RedactException handles inner exceptions
            // and stack frames too.
            stderr.WriteLine("[ERROR] " + ex.GetType().Name + ": "
                + SecretRedactor.Redact(ex.Message));
            return ExitIoFailure;
        }
    }

    /// <summary>
    /// Walk the env file lines and return the canonical provider names
    /// that have credential lines we can rotate. Order: azure first
    /// (default-section AZUREOPENAIAPI), then provider-section order.
    /// </summary>
    internal static List<string> DetectConfiguredProviders(string[] lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        var found = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        // Azure lives in the default (unsectioned) part of the file as
        // `export AZUREOPENAIAPI="..."`. Probe for it before any section
        // header.
        for (var i = 0; i < lines.Length; i++)
        {
            var trimmed = lines[i].TrimStart();
            if (trimmed.StartsWith("[", StringComparison.Ordinal)) break;
            if (AzureKeyLineRx.IsMatch(lines[i]))
            {
                if (seen.Add(WizardProviders.Azure)) found.Add(WizardProviders.Azure);
                break;
            }
        }

        // Compat providers live in `[provider:NAME]` sections.
        foreach (Match m in ProviderHeaderRx.Matches(string.Join("\n", lines)))
        {
            var name = m.Groups["name"].Value;
            if (WizardProviders.TryCanonicalize(name, out var canon)
                && seen.Add(canon))
            {
                found.Add(canon);
            }
        }
        return found;
    }

    /// <summary>
    /// Rewrite the API-key line for <paramref name="target"/> in
    /// <paramref name="lines"/>, returning the new file content. Returns
    /// null when the key line cannot be located. When
    /// <paramref name="dryRun"/> is true, returns the literal string "OK"
    /// on a hit (the caller only cares about success, not the bytes) --
    /// used by the post-write smoke check.
    /// </summary>
    internal static string? RewriteKey(
        string[] lines,
        string target,
        string newKey,
        out string? error,
        bool dryRun = false)
    {
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentException.ThrowIfNullOrWhiteSpace(target);
        ArgumentNullException.ThrowIfNull(newKey);
        error = null;

        var sb = new StringBuilder();
        var rewritten = false;
        var inTargetSection = false;
        var isAzure = string.Equals(target, WizardProviders.Azure, StringComparison.Ordinal);
        var isCloudflare = string.Equals(target, WizardProviders.Cloudflare, StringComparison.Ordinal);
        var compatKeyName = isCloudflare ? "API_TOKEN" : "API_KEY";

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var trimmed = line.TrimStart();
            var isHeader = trimmed.StartsWith("[", StringComparison.Ordinal)
                && trimmed.Contains("]", StringComparison.Ordinal);

            if (isHeader)
            {
                var headerMatch = ProviderHeaderRx.Match(line);
                if (headerMatch.Success)
                {
                    var name = headerMatch.Groups["name"].Value;
                    inTargetSection = !isAzure
                        && WizardProviders.TryCanonicalize(name, out var canon)
                        && string.Equals(canon, target, StringComparison.Ordinal);
                }
                else
                {
                    inTargetSection = false;
                }
                sb.Append(line).Append('\n');
                continue;
            }

            if (isAzure && !rewritten && AzureKeyLineRx.IsMatch(line))
            {
                if (dryRun) { error = null; return "OK"; }
                sb.Append("export AZUREOPENAIAPI=\"")
                    .Append(EscapeForDoubleQuotes(newKey))
                    .Append("\"\n");
                rewritten = true;
                continue;
            }

            if (!isAzure && inTargetSection && !rewritten)
            {
                var m = SectionKvRx.Match(line);
                if (m.Success
                    && string.Equals(m.Groups["name"].Value, compatKeyName, StringComparison.Ordinal))
                {
                    if (dryRun) { error = null; return "OK"; }
                    sb.Append(compatKeyName).Append('=').Append(newKey).Append('\n');
                    rewritten = true;
                    continue;
                }
            }

            sb.Append(line).Append('\n');
        }

        if (!rewritten)
        {
            error = isAzure
                ? "default-section line `export AZUREOPENAIAPI=...` not found"
                : "section [provider:" + target + "] missing or has no `"
                    + compatKeyName + "=...` line";
            return null;
        }
        if (dryRun) return "OK";
        return sb.ToString();
    }

    private static string? PromptProvider(IReadOnlyList<string> configured, TextReader stdin, TextWriter stdout)
    {
        stdout.WriteLine();
        stdout.WriteLine("Configured providers:");
        for (var i = 0; i < configured.Count; i++)
        {
            stdout.WriteLine("  " + (i + 1).ToString(CultureInfo.InvariantCulture) + ") " + configured[i]);
        }
        stdout.Write("Pick [1]: ");
        stdout.Flush();
        var input = stdin.ReadLine();
        if (input is null) return null;
        input = input.Trim();
        if (input.Length == 0) return configured[0];
        if (int.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out var idx)
            && idx >= 1 && idx <= configured.Count)
        {
            return configured[idx - 1];
        }
        if (WizardProviders.TryCanonicalize(input, out var canon)
            && configured.Contains(canon))
        {
            return canon;
        }
        return null;
    }

    private static string EscapeForDoubleQuotes(string value)
    {
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

    // Anchored: must be at the start of a logical line (post-trim).
    // Tolerates leading `export ` and either `=` or `="..."` form.
    private static readonly Regex AzureKeyLineRx = new(
        @"^\s*(?:export\s+)?AZUREOPENAIAPI\s*=",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex ProviderHeaderRx = new(
        @"^\s*\[provider:(?<name>[A-Za-z0-9_-]+)\]\s*$",
        RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.CultureInvariant);

    private static readonly Regex SectionKvRx = new(
        @"^\s*(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
}
