namespace AzureOpenAI_CLI.Mcp;

/// <summary>
/// Decides which tools of an MCP server the model is allowed to see (<c>--mcp-allow</c>, <c>--mcp-deny</c>).
/// <para>
/// Without it the client wraps every tool a server lists and hands them all to the model, including ones that delete or
/// deploy. The model can only call a tool it was told about, so hiding a tool here is enforcement, not a request.
/// </para>
/// <para>
/// Rules: a name matching any <c>deny</c> pattern is hidden (deny always wins); when any <c>allow</c> pattern is given,
/// only names matching one of them are kept; with neither, everything is kept (the historical behaviour). Patterns are
/// globs over the tool name: <c>*</c> matches any run of characters, <c>?</c> exactly one; matching is case-sensitive.
/// There is no regex, so no pattern can be slow.
/// </para>
/// </summary>
internal sealed class McpToolFilter
{
    private readonly string[] _allow;
    private readonly string[] _deny;

    public McpToolFilter(IEnumerable<string>? allow, IEnumerable<string>? deny)
    {
        _allow = Clean(allow);
        _deny = Clean(deny);
    }

    /// <summary>True when at least one pattern was given, so the filter changes what is exposed.</summary>
    public bool IsActive => _allow.Length > 0 || _deny.Length > 0;

    public IReadOnlyList<string> AllowPatterns => _allow;
    public IReadOnlyList<string> DenyPatterns => _deny;

    public bool Allows(string toolName)
    {
        foreach (var d in _deny)
            if (GlobMatch(d, toolName)) return false;
        if (_allow.Length == 0) return true;
        foreach (var a in _allow)
            if (GlobMatch(a, toolName)) return true;
        return false;
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
