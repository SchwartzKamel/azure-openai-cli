namespace AzureOpenAI_CLI.Mcp;

/// <summary>
/// Decides which tools of an MCP server the model is allowed to see (<c>--mcp-allow</c>, <c>--mcp-deny</c>).
/// <para>
/// Without it the client wraps every tool a server lists and hands them all to the model, including ones that delete or
/// deploy. The model can only call a tool it was told about, so hiding a tool here is enforcement, not a request.
/// </para>
/// <para>
/// Rules: a name matching any <c>deny</c> pattern is hidden (deny always wins); when any <c>allow</c> pattern is given,
/// only names matching one of them are kept; with neither, everything is kept (the historical behaviour) unless
/// <b>strict</b> mode is on, which keeps only tools the server declares <c>readOnlyHint: true</c> (and not also
/// <c>destructiveHint: true</c>). An explicit allow list always wins over hints: you named the tools. Patterns are
/// globs over the tool name: <c>*</c> matches any run of characters, <c>?</c> exactly one; matching is case-sensitive.
/// There is no regex, so no pattern can be slow.
/// </para>
/// </summary>
internal sealed class McpToolFilter
{
    private readonly string[] _allow;
    private readonly string[] _deny;

    public McpToolFilter(IEnumerable<string>? allow, IEnumerable<string>? deny, bool strict = false)
    {
        _allow = Clean(allow);
        _deny = Clean(deny);
        Strict = strict;
    }

    /// <summary>
    /// Strict mode: with no allow list, expose only tools that declare <c>readOnlyHint: true</c> instead of everything, and
    /// treat a pattern that matches no tool as an error rather than a warning. The hints are the server's own claims.
    /// </summary>
    public bool Strict { get; }

    /// <summary>True when the filter changes what is exposed (a pattern was given, or strict mode is on).</summary>
    public bool IsActive => Strict || _allow.Length > 0 || _deny.Length > 0;

    /// <summary>True when an allow list was given: the user named the tools, so hints are not consulted.</summary>
    public bool HasAllowList => _allow.Length > 0;

    public IReadOnlyList<string> AllowPatterns => _allow;
    public IReadOnlyList<string> DenyPatterns => _deny;

    /// <summary>Whether the model may be told about <paramref name="toolName"/>, ignoring any hints the server declared.</summary>
    public bool Allows(string toolName) => Allows(toolName, null, null);

    /// <summary>As above, with the hints the server declared for the tool (only consulted in strict mode with no allow list).</summary>
    public bool Allows(string toolName, bool? readOnlyHint, bool? destructiveHint)
    {
        foreach (var d in _deny)
            if (GlobMatch(d, toolName)) return false;
        if (_allow.Length > 0)
        {
            foreach (var a in _allow)
                if (GlobMatch(a, toolName)) return true;
            return false;
        }
        if (!Strict) return true;
        // Strict, no allow list: only what the server itself says is read-only. A tool that claims both read-only and
        // destructive contradicts itself, and the safe reading of a contradiction is to hide it.
        return readOnlyHint == true && destructiveHint != true;
    }

    /// <summary>
    /// Patterns that match none of <paramref name="seenToolNames"/>. A typo in a deny pattern leaves the tool it was
    /// meant to hide exposed, so the caller reports these loudly instead of letting them be silently inert.
    /// </summary>
    public IReadOnlyList<(string Kind, string Pattern)> UnmatchedPatterns(IEnumerable<string> seenToolNames)
    {
        var names = seenToolNames.ToList();
        var result = new List<(string, string)>();
        foreach (var p in _deny)
            if (!names.Any(n => GlobMatch(p, n))) result.Add(("--mcp-deny", p));
        foreach (var p in _allow)
            if (!names.Any(n => GlobMatch(p, n))) result.Add(("--mcp-allow", p));
        return result;
    }

    private static string[] Clean(IEnumerable<string>? patterns)
        => patterns is null
            ? Array.Empty<string>()
            : patterns.Select(p => p.Trim()).Where(p => p.Length > 0).Distinct(StringComparer.Ordinal).ToArray();

    /// <summary>Iterative glob match (<c>*</c>, <c>?</c>), linear in the inputs: no recursion, no backtracking blow-up.</summary>
    internal static bool GlobMatch(string pattern, string text)
    {
        int p = 0, t = 0, star = -1, mark = 0;
        while (t < text.Length)
        {
            if (p < pattern.Length && (pattern[p] == '?' || pattern[p] == text[t]) && pattern[p] != '*')
            {
                p++; t++;
            }
            else if (p < pattern.Length && pattern[p] == '*')
            {
                star = p++; mark = t;
            }
            else if (star >= 0)
            {
                p = star + 1; t = ++mark;
            }
            else
            {
                return false;
            }
        }
        while (p < pattern.Length && pattern[p] == '*') p++;
        return p == pattern.Length;
    }
}
