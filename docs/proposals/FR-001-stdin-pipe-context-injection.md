# FR-001: Stdin Pipe & Context Injection

> **✅ Shipped in v1.1.0.** `az-ai` reads from stdin when piped and combines
> piped content with positional prompt arguments. The 32 KB prompt cap and
> related input validation landed in the same release. The `--file <path>`
> flag described below was deferred.

---
**Priority:** P0 — Critical  
**Impact:** Transforms the tool from a toy into a workflow weapon  
**Effort:** Small (< 1 day)  
**Category:** Core UX

---

## The Problem

Right now, the CLI only accepts prompts as command-line arguments:

```bash
az-ai "Explain quantum computing"
```

This means the tool is **isolated from the developer's actual workflow**. You can't feed it code, diffs, logs, or files. Every other serious CLI tool — `jq`, `ripgrep`, `sed`, even `curl` — reads from stdin. This tool doesn't. That's a dealbreaker for power users who compose tools with pipes.

A developer looking at a failing test or a messy diff should be able to say:

```bash
git diff --staged | az-ai "Review this diff for bugs"
cat error.log | az-ai "What went wrong here?"
kubectl logs pod/api-server | az-ai "Summarize these errors"
```

Today, none of that works. The user has to manually copy-paste content into a quoted argument — which hits the 32KB prompt limit fast and breaks on special characters.

---

## The Proposal

### 1. Detect and read stdin when it's piped

When stdin is not a TTY (i.e., something is being piped in), read it and prepend it to the user prompt as context:

```csharp
string? stdinContent = null;
if (!Console.IsInputRedirected)
{
    // Interactive mode — stdin is the terminal, no pipe
}
else
{
    stdinContent = Console.In.ReadToEnd();
}
```

Then compose the prompt:

```csharp
string fullPrompt = stdinContent != null
    ? $"Context:\n```\n{stdinContent}\n```\n\nUser request: {userPrompt}"
    : userPrompt;
```

### 2. Support an explicit `--file` flag for direct file injection

```bash
az-ai --file src/Program.cs "Find the bug in this code"
az-ai --file crash.log --file config.yaml "Why is this service crashing?"
```

This is more discoverable than pipes and works on systems where piping is awkward (Windows PowerShell).

### 3. Truncation safety

Piped content can be enormous. Apply a content-aware truncation:

- If stdin + prompt exceeds `MAX_PROMPT_LENGTH`, truncate stdin with a `[... truncated N chars ...]` marker.
- Log a warning to stderr so the user knows content was cut.
- Never silently drop content.

---

## User Journeys This Unlocks

| Before (Painful) | After (Glorious) |
|---|---|
| Copy code → paste into quotes → pray special chars don't break it | `cat file.py \| az-ai "explain this"` |
| Can't review diffs with AI | `git diff \| az-ai "review for bugs"` |
| Can't analyze logs | `tail -100 app.log \| az-ai "what's failing?"` |
| Can't chain with other tools | `curl api/health \| az-ai "is this healthy?"` |

---

## Why This Is P0

**This is the #1 feature that separates "CLI toy" from "CLI tool."** Every developer who evaluates this will try to pipe something into it within the first 5 minutes. When it doesn't work, they close the tab and go back to ChatGPT.

Charmbracelet's `mods` made pipe support their headline feature. `sgpt` supports it. This is table stakes.

The implementation is trivial — `Console.IsInputRedirected` + `Console.In.ReadToEnd()` — but the impact on adoption is enormous. This is the feature that makes someone tweet: "just reviewed my entire PR with one command."

---

## Exit Criteria

- [ ] `echo "hello" | az-ai "translate to Spanish"` works
- [ ] `cat file.py | az-ai "add error handling"` works  
- [ ] `az-ai "hello"` still works (no regression when stdin is a TTY)
- [ ] Content exceeding `MAX_PROMPT_LENGTH` is truncated with a warning
- [ ] `--file <path>` reads one or more files into context
- [ ] Update `--help` output and README with pipe examples
