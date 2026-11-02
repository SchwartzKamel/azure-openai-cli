using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace AzureOpenAI_CLI.Tools;

/// <summary>
/// Execute a shell command and return stdout. Sandboxed with timeout and output cap.
/// MAF version: uses [Description] attributes for AIFunctionFactory.Create.
/// </summary>
internal static class ShellExecTool
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
        "AZUREOPENAIAPI",      // this repo's convention -- see project memory
        "AZUREOPENAIENDPOINT",
        "AZUREOPENAIMODEL",
        "AZURE_FOUNDRY_ENDPOINT",
        "AZURE_FOUNDRY_KEY",
        "AZURE_FOUNDRY_MODELS",
        "AZURE_IMAGE_MODEL",
        "GITHUB_TOKEN",
        "GH_TOKEN",
        "OPENAI_API_KEY",
        "ANTHROPIC_API_KEY",
    };

    // git global options that consume the NEXT token as their value.
    private static readonly HashSet<string> GitGlobalOptionsWithValue = new(StringComparer.Ordinal)
    {
        "-C", "-c", "--git-dir", "--work-tree", "--namespace", "--exec-path", "--super-prefix",
    };

    /// <summary>
    /// True when <paramref name="command"/> would run <c>git push</c> in a destructive
    /// mode: a force flag (<c>--force</c>, <c>--force-with-lease</c>, <c>--force-if-includes</c>,
    /// or any short cluster containing <c>f</c>), a <c>+refspec</c>, <c>--mirror</c>, or remote
    /// ref deletion (<c>--delete</c>, <c>-d</c>, <c>:ref</c>).
    ///
    /// Design notes:
    /// <list type="bullet">
    ///   <item>Quotes are treated as separators, so a wrapper such as
    ///     <c>sh -c 'git push -f'</c> is seen. (<c>sh</c>/<c>bash</c> are not in
    ///     <see cref="BlockedCommands"/>, so a per-segment first-token check alone misses it.)</item>
    ///   <item><c>push</c> must be the git <em>subcommand</em> (after any global options such as
    ///     <c>-C dir</c> or <c>--no-pager</c>), so a commit message that merely contains the
    ///     words "git push -f" is not blocked.</item>
    ///   <item>Known limits, by design: a literal mention such as <c>echo git push --force</c> is
    ///     refused (indistinguishable from <c>env git push --force</c> without a full shell
    ///     parser), and indirection such as a git alias (<c>git p</c> where
    ///     <c>alias.p = push -f</c>) or a script is not seen. This is defense in depth, not a
    ///     sandbox.</item>
    ///   <item>Fail-closed on ambiguity: this is a guard, not a parser. A false positive costs the
    ///     agent one refused command; a false negative costs a collaborator their history.</item>
    /// </list>
    /// </summary>
    internal static bool ContainsDestructiveGitOperation(string? command)
    {
        if (string.IsNullOrWhiteSpace(command)) return false;

        // Quotes become separators; shell grouping characters split commands.
        var flat = command.Replace('\'', ' ').Replace('"', ' ').Replace('`', ' ');
        foreach (var segment in flat.Split(new[] { '|', ';', '&', '\n', '(', ')', '{', '}' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var tokens = segment.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);

            for (var i = 0; i < tokens.Length; i++)
            {
                var exe = tokens[i].Split('/').LastOrDefault() ?? string.Empty;
                if (!exe.Equals("git", StringComparison.OrdinalIgnoreCase)) continue;

                // Find the subcommand: first token after git that is not a global option or its value.
                var j = i + 1;
                while (j < tokens.Length && tokens[j].StartsWith('-'))
                {
                    j += GitGlobalOptionsWithValue.Contains(tokens[j]) ? 2 : 1;
                }
                // Everything after this git is its own arguments, never a second git
                // command (separators were already split above), so inspect it once
                // and move on. This is what keeps `git commit -m 'never git push -f'` allowed.
                if (j >= tokens.Length || !tokens[j].Equals("push", StringComparison.OrdinalIgnoreCase)) break;

                for (var k = j + 1; k < tokens.Length; k++)
                {
                    var t = tokens[k];
                    if (t.StartsWith("--"))
                    {
                        if (t.StartsWith("--force", StringComparison.OrdinalIgnoreCase)   // --force, --force-with-lease[=], --force-if-includes
                            || t.Equals("--mirror", StringComparison.OrdinalIgnoreCase)
                            || t.Equals("--delete", StringComparison.OrdinalIgnoreCase))
                            return true;
                    }
                    else if (t.StartsWith('-'))
                    {
                        // short cluster, e.g. -f, -fu, -d
                        if (t.Skip(1).Any(c => c == 'f' || c == 'd')) return true;
                    }
                    else if (t.Length > 1 && (t[0] == '+' || t[0] == ':'))
                    {
                        // +refspec forces the update; :ref deletes the remote ref
                        return true;
                    }
                }
                break;
            }
        }
        return false;
    }

    [Description("Execute a shell command and return its stdout. Use for running git, ls, cat, grep, curl, etc. Commands that delete or modify system state are blocked.")]
    public static async Task<string> ExecuteAsync(
        [Description("The shell command to execute")] string command,
        CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(command))
            return "Error: parameter 'command' must not be empty.";

        // Block shell substitution patterns that could bypass command filtering
        if (command.Contains("$(") || command.Contains("`"))
            return "Error: shell substitution ($() and backticks) is blocked for safety.";

        // Block process substitution and eval-like constructs
        if (command.Contains("<(") || command.Contains(">(") ||
            command.TrimStart().StartsWith("eval ") || command.Contains("; eval ") ||
            command.TrimStart().StartsWith("exec ") || command.Contains("; exec "))
            return "Error: process substitution and eval/exec are blocked for safety.";

        // Block dangerous commands.
        // K-1 sibling (2.0.2): split on space/tab/newline with RemoveEmptyEntries
        // so a whitespace-leading invocation (e.g. "\trm -rf /", "\n sudo ...",
        // " \trm") still reaches the first-token blocklist on the fast path,
        // not just via the segment rescan below. Defense-in-depth — TrimStart()
        // already strips leading whitespace, but belt-and-suspenders across
        // future refactors that might touch it.
        var firstTokens = command.Split(new[] { ' ', '\t', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        var firstToken = (firstTokens.Length > 0 ? firstTokens[0] : "").Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? "";
        if (BlockedCommands.Contains(firstToken))
            return $"Error: command '{firstToken}' is blocked for safety.";

        // Block pipe chains to dangerous commands.
        // K-1 (2.0.1): include '\t' so `ls\trm -rf /` is rejected the same way
        // `ls ; rm -rf /` is — tab is a shell-word separator too and the
        // first-token check above won't catch a tab-delimited second command.
        foreach (var segment in command.Split('|', ';', '&', '\t', '\n'))
        {
            var token = segment.Trim().Split(new[] { ' ', '\t' }, 2)[0].Split('/').LastOrDefault() ?? "";
            if (BlockedCommands.Contains(token))
                return $"Error: command '{token}' is blocked for safety.";
        }

        // Destructive remote git operations (force push, '+' refspec, mirror, remote
        // ref deletion) are irreversible for other collaborators and are never needed
        // to inspect or validate a repository, so text in a repo cannot talk the
        // agent into running them.
        if (ContainsDestructiveGitOperation(command))
            return "Error: destructive git operations (force push, mirror, remote ref delete) are blocked for safety.";

        // Block curl/wget body + upload forms (write-side HTTP). Read-only GETs
        // stay allowed so the model can fetch public metadata, but any flag that
        // would send data or upload a file is rejected — those belong in
        // web_fetch (GET only) or a proper HTTP client, not a shell tool.
        if (ContainsHttpWriteForms(command, out var offending))
            return $"Error: curl with body/upload options is not permitted in shell_exec — use web_fetch (GET only) or a proper HTTP client (offending token: {offending}).";

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
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
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
