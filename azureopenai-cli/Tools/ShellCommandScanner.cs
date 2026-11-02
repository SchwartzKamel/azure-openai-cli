using System.Text;
using System.Text.RegularExpressions;

namespace AzureOpenAI_CLI.Tools;

/// <summary>
/// Finds the commands a shell line would actually run and refuses the ones <c>shell_exec</c> blocks.
///
/// <para>
/// The first-token check in <see cref="ShellExecTool"/> only sees a blocked name at the start of a
/// segment, written plainly. Measured against it (S04E15), 30 of 32 trivial variations ran:
/// <c>bash -c 'rm x'</c>, <c>\rm x</c>, <c>"rm" x</c>, <c>env rm x</c>, <c>(rm x)</c>,
/// <c>timeout 5 rm x</c>, <c>echo x | xargs rm</c>, <c>find x -delete</c>. This scanner reads the
/// line the way the shell does (quotes, escapes, separators, grouping) and checks every command
/// position, so those all resolve to the command they really run.
/// </para>
///
/// <para>
/// What it does: follows quoting and backslashes, so <c>\rm</c>, <c>r'm'</c> and <c>"rm"</c> are all
/// <c>rm</c>; looks through wrappers (<c>env</c>, <c>nohup</c>, <c>nice</c>, <c>timeout</c>,
/// <c>xargs</c>, <c>command</c>, <c>time</c>, <c>busybox</c> and friends); recurses into the payload of
/// <c>sh -c</c> and its relatives; treats an interpreter that reads commands from stdin as a hidden
/// command; checks <c>find -exec</c> and refuses <c>find -delete</c>; skips reserved words so
/// <c>then rm x</c> and <c>{ rm x; }</c> are seen; and refuses a command name it cannot see through
/// (a variable, a glob, a brace list, <c>$'..'</c>).
/// </para>
///
/// <para>
/// What it does not do, by design: this is defense in depth, not a sandbox. It does not inspect a
/// script file the agent ran (<c>sh build.sh</c>), a general-purpose language interpreter
/// (<c>python -c</c>, <c>perl -e</c>, <c>node -e</c>, <c>awk 'BEGIN{system(..)}'</c>), or a git alias.
/// A process that must be contained should run in a container or a restricted user, not behind a
/// denylist. Fail-closed on ambiguity: a false positive costs the agent one refused command.
/// </para>
/// </summary>
internal static class ShellCommandScanner
{
    private const int MaxDepth = 5;

    private const string EvalMessage = "Error: process substitution and eval/exec are blocked for safety.";
    private const string HiddenMessage =
        "Error: the command name is built from a variable, glob, brace list or $'...' string, so it cannot be checked; it is blocked for safety.";
    private const string ParseMessage =
        "Error: the command line could not be parsed safely (unbalanced quote?); it is blocked for safety.";
    private const string DepthMessage = "Error: shell commands nested too deeply to check; blocked for safety.";

    private static readonly HashSet<string> ShellInterpreters = new(StringComparer.OrdinalIgnoreCase)
    {
        "sh", "bash", "zsh", "dash", "ksh", "mksh", "ash", "csh", "tcsh", "fish", "rbash",
    };

    // Words that introduce or close a command without being one: `then rm x` runs rm.
    private static readonly HashSet<string> ReservedWords = new(StringComparer.Ordinal)
    {
        "{", "}", "!", "if", "then", "else", "elif", "do", "while", "until",
    };

    private sealed record Wrapper(string[] ValueOptions, int Positional);

    // Commands that run another command given as their arguments. ValueOptions take the next word as
    // a value (so it is not mistaken for the wrapped command); Positional is how many plain
    // arguments belong to the wrapper before the command (timeout DURATION, flock FILE).
    private static readonly Dictionary<string, Wrapper> Wrappers = new(StringComparer.OrdinalIgnoreCase)
    {
        ["env"] = new(new[] { "-u", "-C", "-S", "--unset", "--chdir", "--split-string" }, 0),
        ["nohup"] = new(Array.Empty<string>(), 0),
        ["nice"] = new(new[] { "-n", "--adjustment" }, 0),
        ["ionice"] = new(new[] { "-c", "-n", "-p", "-P", "-u", "--class", "--classdata", "--pid", "--pgid", "--uid" }, 0),
        ["timeout"] = new(new[] { "-s", "-k", "--signal", "--kill-after" }, 1),
        ["time"] = new(new[] { "-f", "-o", "--format", "--output" }, 0),
        ["command"] = new(Array.Empty<string>(), 0),
        ["builtin"] = new(Array.Empty<string>(), 0),
        ["setsid"] = new(Array.Empty<string>(), 0),
        ["stdbuf"] = new(new[] { "-i", "-o", "-e" }, 0),
        ["unbuffer"] = new(Array.Empty<string>(), 0),
        ["busybox"] = new(Array.Empty<string>(), 0),
        ["watch"] = new(new[] { "-n", "--interval" }, 0),
        ["flock"] = new(new[] { "-w", "-E", "--timeout", "--conflict-exit-code" }, 1),
        ["xargs"] = new(new[]
        {
            "-n", "-I", "-P", "-L", "-s", "-E", "-d", "-a",
            "--max-args", "--replace", "--max-procs", "--max-lines", "--max-chars", "--delimiter", "--arg-file", "--eof",
        }, 0),
    };

    private static readonly Regex Assignment = new(@"^[A-Za-z_][A-Za-z0-9_]*\+?=", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Returns the refusal message when the line would run a blocked command anywhere a command can
    /// appear, or <c>null</c> when nothing blocked was found.
    /// </summary>
    internal static string? Scan(string? command) => Scan(command, 0);

    private static string? Scan(string? command, int depth)
    {
        if (string.IsNullOrWhiteSpace(command)) return null;
        if (depth > MaxDepth) return DepthMessage;
        if (!TryParse(command, out var commands)) return ParseMessage;

        foreach (var simple in commands)
        {
            var i = 0;
            while (i < simple.Count && (Assignment.IsMatch(simple[i].Text) || ReservedWords.Contains(simple[i].Text))) i++;
            if (i >= simple.Count) continue;

            var refusal = CheckCommandAt(simple, i, depth);
            if (refusal != null) return refusal;
        }
        return null;
    }

    private static string? CheckCommandAt(List<Word> words, int i, int depth)
    {
        if (i >= words.Count) return null;
        var word = words[i];
        if (word.Dynamic) return HiddenMessage;

        var name = BaseName(word.Text);
        if (name.Length == 0) return null;

        if (ShellExecTool.IsBlockedName(name)) return $"Error: command '{name}' is blocked for safety.";
        if (name is "eval" or "exec") return EvalMessage;

        if (Wrappers.TryGetValue(name, out var wrapper))
        {
            var (next, splitPayload) = Unwrap(words, i, name, wrapper);
            if (splitPayload != null)
            {
                var inner = Scan(splitPayload, depth + 1);
                if (inner != null) return inner;
            }
            return next >= 0 ? CheckCommandAt(words, next, depth) : null;
        }

        if (ShellInterpreters.Contains(name)) return CheckInterpreter(words, i, name, depth);

        if (name.Equals("find", StringComparison.OrdinalIgnoreCase)) return CheckFind(words, i, depth);

        return null;
    }

    // Index of the command a wrapper runs, or -1 when it runs none (`env` alone prints the
    // environment; `command -v rm` only asks where rm is). `env -S 'cmd args'` carries a whole
    // command line as its value, returned so the caller can scan it.
    private static (int Next, string? SplitPayload) Unwrap(List<Word> w, int i, string name, Wrapper wrapper)
    {
        var isEnv = name.Equals("env", StringComparison.OrdinalIgnoreCase);
        var j = i + 1;
        string? split = null;
        while (j < w.Count)
        {
            var t = w[j].Text;
            if (t == "--") { j++; break; }
            if (isEnv && Assignment.IsMatch(t)) { j++; continue; }
            if (t.Length < 2 || t[0] != '-') break;
            if (name.Equals("command", StringComparison.OrdinalIgnoreCase) && (t == "-v" || t == "-V")) return (-1, null);
            if (isEnv && (t == "-S" || t == "--split-string") && j + 1 < w.Count) split = w[j + 1].Text;
            j += Array.IndexOf(wrapper.ValueOptions, t) >= 0 ? 2 : 1;
        }
        j += wrapper.Positional;
        return (j < w.Count ? j : -1, split);
    }

    // sh -c 'payload' runs the payload; a bare `sh` (or `bash -s`, `sh -`, `sh < file`) runs whatever
    // arrives on stdin, which the guard cannot read; `sh script.sh` runs a file, which it does not.
    private static string? CheckInterpreter(List<Word> w, int i, string name, int depth)
    {
        var j = i + 1;
        var info = false;
        var readsStdin = false;
        while (j < w.Count)
        {
            var t = w[j].Text;
            if (t == "--") { j++; break; }
            if (t == "-") { readsStdin = true; j++; continue; }
            if (t is "--version" or "--help") { info = true; j++; continue; }
            if (t.StartsWith("--", StringComparison.Ordinal))
            {
                j += t is "--rcfile" or "--init-file" ? 2 : 1;
                continue;
            }
            if (t.Length > 1 && (t[0] == '-' || t[0] == '+'))
            {
                if (t is "-o" or "+o" or "-O" or "+O") { j += 2; continue; }
                var letters = t.Skip(1).All(char.IsLetter);
                if (t[0] == '-' && letters && t.Contains('c'))
                {
                    // The next word is the command string.
                    return j + 1 < w.Count ? Scan(w[j + 1].Text, depth + 1) : null;
                }
                if (t[0] == '-' && letters && t.Contains('s')) readsStdin = true;
                j++;
                continue;
            }
            break; // first plain word: a script file (or its arguments)
        }

        if (info) return null;
        if (!readsStdin && j < w.Count) return null; // runs a file the guard cannot read
        return $"Error: command '{name}' reading commands from stdin is blocked for safety.";
    }

    private static string? CheckFind(List<Word> w, int i, int depth)
    {
        for (var k = i + 1; k < w.Count; k++)
        {
            var t = w[k].Text;
            if (t == "-delete") return "Error: 'find -delete' is blocked for safety.";
            if (t is "-exec" or "-execdir" or "-ok" or "-okdir")
            {
                var inner = CheckCommandAt(w, k + 1, depth);
                if (inner != null) return inner;
            }
        }
        return null;
    }

    private static string BaseName(string text)
    {
        var slash = text.LastIndexOf('/');
        return slash >= 0 ? text[(slash + 1)..] : text;
    }

    // ── Tokenizer ───────────────────────────────────────────────────────────────

    /// <summary>A word as the shell would see it after quoting, plus whether part of it is not literal.</summary>
    internal readonly record struct Word(string Text, bool Dynamic);

    /// <summary>
    /// Splits a shell line into simple commands (lists of words). Quotes and backslashes are removed
    /// the way the shell removes them, so the text of a word is what the command is really called. A
    /// word is <c>Dynamic</c> when a <c>$</c>, a glob character or a brace list is in it. There is no
    /// exception for "only the directory part is dynamic": an unquoted expansion can word-split
    /// (<c>X='rm '; $X/tmp/y</c> runs rm; so does <c>rm${IFS}/tmp/y</c>), so the final path component
    /// is not knowable either. The price is that <c>$HOME/bin/tool</c> is refused as a command.
    /// Returns false on an unbalanced quote.
    /// </summary>
    internal static bool TryParse(string s, out List<List<Word>> commands)
    {
        var result = new List<List<Word>>();
        commands = result; // an out parameter cannot be captured by the local functions below
        var cur = new List<Word>();
        var sb = new StringBuilder();
        bool inWord = false, dynamic = false, skipNextWord = false;

        void EndWord()
        {
            if (!inWord) return;
            var text = sb.ToString();
            var dyn = dynamic;
            // A brace list or a glob bracket anywhere in the word can hide the name ({rm,/tmp/x} has a slash inside).
            if (!dyn && text.Contains('{') && (text.Contains(',') || text.Contains("..", StringComparison.Ordinal))) dyn = true;
            if (!dyn && text.Length > 1 && text.Contains('[') && text.Contains(']') && text is not ("[[" or "]]")) dyn = true;
            if (skipNextWord) skipNextWord = false;
            else cur.Add(new Word(text, dyn));
            sb.Clear();
            inWord = false;
            dynamic = false;
        }

        void EndCommand()
        {
            EndWord();
            if (cur.Count > 0) result.Add(cur);
            cur = new List<Word>();
        }

        for (var i = 0; i < s.Length; i++)
        {
            var c = s[i];
            switch (c)
            {
                case '\\':
                    if (i + 1 >= s.Length) { sb.Append('\\'); inWord = true; }
                    else if (s[i + 1] == '\n') { i++; }
                    else { sb.Append(s[++i]); inWord = true; }
                    break;

                case '\'':
                    {
                        var end = s.IndexOf('\'', i + 1);
                        if (end < 0) return false;
                        sb.Append(s, i + 1, end - i - 1);
                        inWord = true;
                        i = end;
                        break;
                    }

                case '"':
                    {
                        var k = i + 1;
                        while (k < s.Length && s[k] != '"')
                        {
                            if (s[k] == '\\' && k + 1 < s.Length && s[k + 1] is '$' or '`' or '"' or '\\' or '\n')
                            {
                                if (s[k + 1] != '\n') sb.Append(s[k + 1]);
                                k += 2;
                                continue;
                            }
                            if (s[k] is '$' or '`') dynamic = true;
                            sb.Append(s[k]);
                            k++;
                        }
                        if (k >= s.Length) return false;
                        inWord = true;
                        i = k;
                        break;
                    }

                case '$':
                case '`':
                case '*':
                case '?':
                    dynamic = true;
                    sb.Append(c);
                    inWord = true;
                    break;

                case '#':
                    if (inWord) { sb.Append(c); break; }
                    while (i + 1 < s.Length && s[i + 1] != '\n') i++;
                    break;

                case ' ':
                case '\t':
                    EndWord();
                    break;

                case '\n':
                case ';':
                case '|':
                case '&':
                case '(':
                case ')':
                    EndCommand();
                    break;

                case '<':
                case '>':
                    {
                        // `2>&1`: a bare number before the operator is a file descriptor, not a word.
                        if (inWord && sb.ToString().All(char.IsDigit)) { sb.Clear(); inWord = false; dynamic = false; }
                        else EndWord();
                        var heredoc = c == '<' && i + 1 < s.Length && s[i + 1] == '<';
                        while (i + 1 < s.Length && s[i + 1] is '<' or '>' or '&' or '|') i++;
                        if (heredoc && i + 1 < s.Length && s[i + 1] == '-') i++; // <<-EOF
                        skipNextWord = true; // the redirect target (a file, a delimiter, a descriptor) is not a command word
                        break;
                    }

                default:
                    sb.Append(c);
                    inWord = true;
                    break;
            }
        }

        EndCommand();
        return true;
    }
}
