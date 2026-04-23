using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace AzureOpenAI_CLI.Tools;

/// <summary>
/// Execute a shell command and return stdout. Sandboxed with timeout and output cap.
/// </summary>
internal sealed class ShellExecTool : IBuiltInTool
{
    private const int MaxOutputBytes = 65_536; // 64 KB
    private const int DefaultTimeoutMs = 10_000; // 10s

    private static readonly HashSet<string> BlockedCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        "rm", "rmdir", "mkfs", "dd", "shutdown", "reboot", "halt", "poweroff",
        "kill", "killall", "pkill", "format", "del", "fdisk", "passwd",
        "sudo", "su", "crontab", "vi", "vim", "nano",
        "nc", "ncat", "netcat", "wget",
    };

    /// <summary>
    /// Environment variables scrubbed from the child process before execution.
    /// Hardening rationale: the shell_exec tool is callable by the LLM with arbitrary
    /// commands. Even though BlockedCommands prevents obvious exfiltration vectors,
    /// a well-crafted `printenv` / `env` / `$VAR` echo could leak credentials into
    /// the tool output and then into chat history. Remove known secret-bearing
    /// variables from the child process environment so even a successful leak
    /// attempt yields nothing. The parent process retains them for its own use.
    /// </summary>
    private static readonly string[] SensitiveEnvVars = new[]
    {
        "AZURE_OPENAI_API_KEY",
        "AZUREOPENAIAPI",      // this repo's convention — see project memory
        "AZUREOPENAIENDPOINT",
        "AZUREOPENAIMODEL",
        "GITHUB_TOKEN",
        "GH_TOKEN",
        "OPENAI_API_KEY",
        "ANTHROPIC_API_KEY",
    };

    public string Name => "shell_exec";
    public string Description => "Execute a shell command and return its stdout. Use for running git, ls, cat, grep, curl, etc. Commands that delete or modify system state are blocked.";
    public BinaryData ParametersSchema => BinaryData.FromString("""
        {
            "type": "object",
            "properties": {
                "command": { "type": "string", "description": "The shell command to execute" }
            },
            "required": ["command"]
        }
        """);

    public async Task<string> ExecuteAsync(JsonElement arguments, CancellationToken ct)
    {
        if (arguments.ValueKind != JsonValueKind.Object ||
            !arguments.TryGetProperty("command", out var commandProp))
            return "Error: missing required parameter 'command'.";

        var command = commandProp.GetString();
        if (string.IsNullOrEmpty(command))
            return "Error: parameter 'command' must not be empty.";

        // Defense-in-depth blocklist (S02E32 *The Bypass*). Substring-on-raw-input
        // was bypassable via shell tokenization tricks (${IFS}, tab/newline,
        // quoted/escaped names, env-indirected commands, fullwidth Unicode
        // lookalikes, && chains). The pipeline below rejects shell metacharacters
        // up front, then NFKC-normalizes and tokenizes per shell-statement segment
        // before exact-matching each command head against the blocklist.
        var validation = Validate(command);
        if (validation != null)
            return validation;

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(DefaultTimeoutMs);

        var psi = new ProcessStartInfo
        {
            FileName = "/bin/sh",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        psi.ArgumentList.Add("-c");
        psi.ArgumentList.Add(command);

        // Scrub sensitive env vars from child process (defence in depth — see
        // SensitiveEnvVars XML doc above).
        foreach (var name in SensitiveEnvVars)
            psi.Environment.Remove(name);

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start shell process");

        // Close stdin immediately to prevent interactive commands from hanging
        process.StandardInput.Close();

        var stdout = await ReadCappedAsync(process.StandardOutput, MaxOutputBytes, cts.Token);
        var stderr = await ReadCappedAsync(process.StandardError, MaxOutputBytes / 4, cts.Token);

        try { await process.WaitForExitAsync(cts.Token); }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
            throw;
        }

        var result = stdout;
        if (process.ExitCode != 0 && !string.IsNullOrEmpty(stderr))
            result += $"\n[exit code {process.ExitCode}]\n[stderr] {stderr}";

        return string.IsNullOrEmpty(result) ? "(no output)" : result;
    }

    /// <summary>
    /// Static validation pipeline for an LLM-supplied shell command. Returns
    /// an error string ("Error: ...") if the command is rejected, or null if
    /// it should be allowed to execute. Stages:
    ///   1. Reject shell-substitution metacharacters (<c>$()</c>, backticks,
    ///      <c>&lt;()</c>, <c>&gt;()</c>, <c>${...}</c>) -- these defeat any
    ///      static blocklist by deferring command resolution to <c>/bin/sh</c>.
    ///   2. Reject newline / tab metacharacters used to break the tokenizer
    ///      (a newline is a statement terminator; tab is an IFS character).
    ///   3. Reject I/O redirection (<c>&lt;</c> / <c>&gt;</c>) which can leak
    ///      stdout to arbitrary files or smuggle a here-string.
    ///   4. NFKC-normalize the command so fullwidth Unicode lookalikes
    ///      (e.g. <c>\uFF52\uFF4D</c> for "rm") collapse to ASCII before
    ///      tokenization.
    ///   5. Split on shell-statement separators (<c>;</c>, <c>|</c>, <c>&amp;</c>)
    ///      so chained commands (<c>true &amp;&amp; rm ...</c>) are inspected
    ///      per segment.
    ///   6. Extract each segment's command head, strip surrounding quotes /
    ///      backslashes (<c>"rm"</c>, <c>\rm</c>), take the basename
    ///      (<c>/usr/bin/rm</c> -&gt; <c>rm</c>), lowercase, and exact-match
    ///      against the blocklist + the eval/exec sentinels.
    ///   7. Apply the curl/wget HTTP-write-form check.
    /// </summary>
    internal static string? Validate(string command)
    {
        // 1. Shell substitution / parameter expansion.
        if (command.Contains("$(") || command.Contains("`"))
            return "Error: shell substitution ($() and backticks) is blocked for safety.";
        if (command.Contains("<(") || command.Contains(">("))
            return "Error: process substitution is blocked for safety.";
        if (command.Contains("${"))
            return "Error: shell parameter expansion (${...}) is blocked for safety.";

        // 2. Whitespace metacharacters used to break tokenization.
        if (command.Contains('\n') || command.Contains('\r'))
            return "Error: newline characters are blocked for safety.";
        if (command.Contains('\t'))
            return "Error: tab characters are blocked for safety.";

        // 3. I/O redirection (after the <(/>( process-substitution check).
        if (command.Contains('<') || command.Contains('>'))
            return "Error: I/O redirection (< and >) is blocked for safety.";

        // 4. NFKC-normalize so fullwidth Unicode lookalikes collapse to ASCII
        //    BEFORE we tokenize and blocklist-match.
        var normalized = command.Normalize(NormalizationForm.FormKC);

        // 5/6. Per-segment command-head extraction + exact-match blocklist.
        foreach (var segment in normalized.Split(
                     new[] { ';', '|', '&' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var head = ExtractCommandHead(segment);
            if (head is null)
                continue;

            if (head == "eval" || head == "exec")
                return $"Error: command '{head}' (eval/exec) is blocked for safety.";

            if (BlockedCommands.Contains(head))
                return $"Error: command '{head}' is blocked for safety.";
        }

        // 7. Curl/wget write-side HTTP forms. Read-only GETs stay allowed so
        //    the model can fetch public metadata, but any flag that would
        //    send data or upload a file is rejected -- those belong in
        //    web_fetch (GET only) or a proper HTTP client, not a shell tool.
        if (ContainsHttpWriteForms(normalized, out var offending))
            return $"Error: curl with body/upload options is not permitted in shell_exec -- use web_fetch (GET only) or a proper HTTP client (offending token: {offending}).";

        return null;
    }

    /// <summary>
    /// Extract the command head from a single shell-statement segment.
    /// Strips leading whitespace, takes the first whitespace-delimited word,
    /// removes surrounding quotes and leading backslashes (which <c>/bin/sh</c>
    /// strips before resolving the command name), takes the basename of any
    /// path-qualified form (<c>/usr/bin/rm</c> -&gt; <c>rm</c>), and
    /// lowercases. Returns null for empty segments.
    /// </summary>
    internal static string? ExtractCommandHead(string segment)
    {
        var trimmed = segment.TrimStart();
        if (trimmed.Length == 0)
            return null;

        // First whitespace-delimited word (space or tab; tab is rejected upstream
        // but kept here for defense in depth if Validate is bypassed in tests).
        var firstWord = trimmed.Split(new[] { ' ', '\t' }, 2, StringSplitOptions.RemoveEmptyEntries)
                               .FirstOrDefault();
        if (string.IsNullOrEmpty(firstWord))
            return null;

        // Strip every quote and backslash. /bin/sh treats `"rm"`, `'rm'`, and
        // `\rm` as resolving to `rm`; the blocklist must do the same.
        var sb = new StringBuilder(firstWord.Length);
        foreach (var c in firstWord)
        {
            if (c == '"' || c == '\'' || c == '\\')
                continue;
            sb.Append(c);
        }
        var stripped = sb.ToString();
        if (stripped.Length == 0)
            return null;

        // Path-qualified form: take the basename. Empty after the last '/' means
        // the input was a bare slash/path-only token; treat as no-command.
        var basename = stripped.Split('/').LastOrDefault();
        if (string.IsNullOrEmpty(basename))
            return null;

        return basename.ToLowerInvariant();
    }


    /// <summary>
    /// Detect curl/wget invocations carrying request-body or file-upload flags.
    /// Returns true if the command contains <c>curl</c> or <c>wget</c> and any
    /// of the HTTP write-side options (body, form, upload, explicit POST/PUT/DELETE).
    /// </summary>
    internal static bool ContainsHttpWriteForms(string command, out string offending)
    {
        offending = "";
        // Tokenize on whitespace and common shell separators so we match tokens,
        // not substrings (avoids false positives like `ls -data_dir`).
        var tokens = command.Split(
            new[] { ' ', '\t', '\n', '|', ';', '&', '(', ')' },
            StringSplitOptions.RemoveEmptyEntries);

        bool hasHttpClient = false;
        foreach (var raw in tokens)
        {
            var t = raw.Split('/').LastOrDefault() ?? "";
            if (t.Equals("curl", StringComparison.OrdinalIgnoreCase) ||
                t.Equals("wget", StringComparison.OrdinalIgnoreCase))
            {
                hasHttpClient = true;
                break;
            }
        }
        if (!hasHttpClient) return false;

        string[] bodyFlags =
        {
            "-d", "--data", "--data-raw", "--data-binary", "--data-urlencode", "--data-ascii",
            "-F", "--form", "--form-string",
            "-T", "--upload-file",
        };
        foreach (var tok in tokens)
        {
            foreach (var f in bodyFlags)
            {
                if (tok.Equals(f, StringComparison.Ordinal) ||
                    tok.StartsWith(f + "=", StringComparison.Ordinal))
                {
                    offending = f;
                    return true;
                }
            }
        }

        // -X / --request <METHOD> — reject non-GET/HEAD methods.
        for (int i = 0; i < tokens.Length - 1; i++)
        {
            if (tokens[i] == "-X" || tokens[i] == "--request")
            {
                var method = tokens[i + 1].Trim('"', '\'').ToUpperInvariant();
                if (method is "POST" or "PUT" or "DELETE" or "PATCH")
                {
                    offending = $"{tokens[i]} {method}";
                    return true;
                }
            }
        }
        return false;
    }

    private static async Task<string> ReadCappedAsync(StreamReader reader, int maxBytes, CancellationToken ct)
    {
        var buffer = new char[maxBytes];
        int read = await reader.ReadBlockAsync(buffer.AsMemory(0, maxBytes), ct);
        var text = new string(buffer, 0, read);
        if (read == maxBytes)
            text += "\n... (output truncated)";
        return text;
    }
}
