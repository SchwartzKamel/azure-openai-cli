# FR-010: Model Aliases & Smart Defaults

**Priority:** P2 — Medium  
**Impact:** Reduces daily friction of model selection; makes multi-model workflows ergonomic  
**Effort:** Small (4–6 hours)  
**Category:** Configuration UX / Developer Experience

---

## The Problem

### Problem 1: Azure Deployment Names Are Verbose and Arbitrary

Azure OpenAI deployment names are set by the administrator, not the user. They can be anything: `gpt-4o-2024-08-06`, `my-team-gpt4`, `prod-turbo-v2`. Users must type or remember these exact strings:

```bash
az-ai --set-model gpt-4o-2024-08-06-global-standard
```

The current model management commands (Program.cs lines 756–863) require exact matches against `AvailableModels`:

```csharp
// Line 126–128 in UserConfig.cs
if (AvailableModels.Contains(modelName, StringComparer.OrdinalIgnoreCase))
{
    ActiveModel = AvailableModels.First(m => m.Equals(modelName, StringComparison.OrdinalIgnoreCase));
```

There's no aliasing, no fuzzy matching, no shorthand. If your deployment is named `gpt-4o-2024-08-06-global-standard`, you type all 38 characters every time you switch.

### Problem 2: No Per-Task Model Selection Without Persistence

The `--set-model` command persists the selection to disk (line 849: `config.Save()`). There's no way to use a different model for a single invocation without permanently switching:

```bash
# I want to ask a quick question with the cheap model, but I don't want to change my default
az-ai --set-model gpt-4.1-mini    # Persists
az-ai "Quick question"
az-ai --set-model gpt-4o           # Have to switch back
```

Compare this to every other multi-model CLI tool:
```bash
# What it should look like:
az-ai --model mini "Quick question"    # One-off, doesn't change default
az-ai "Normal question"                 # Still uses gpt-4o
```

### Problem 3: Environment Variable Naming Is Inconsistent

The `.env.example` (line 1–11) shows two naming conventions:

```env
AZUREOPENAIENDPOINT=...    # No separators (legacy)
AZUREOPENAIMODEL=...       # No separators (legacy)
AZUREOPENAIAPI=...         # No separators (legacy)
AZURE_MAX_TOKENS=10000     # Underscored (newer)
AZURE_TEMPERATURE=0.55     # Underscored (newer)
AZURE_TIMEOUT=120          # Underscored (newer)
```

A user seeing `AZURE_TEMPERATURE` would reasonably guess the endpoint variable is `AZURE_ENDPOINT` — but it's `AZUREOPENAIENDPOINT`. This causes silent misconfiguration. The code in `Program.cs` (lines 384–387) hardcodes the legacy names:

```csharp
string? azureOpenAiEndpoint = Environment.GetEnvironmentVariable("AZUREOPENAIENDPOINT");
string? azureOpenAiModel = Environment.GetEnvironmentVariable("AZUREOPENAIMODEL");
string? azureOpenAiApiKey = Environment.GetEnvironmentVariable("AZUREOPENAIAPI");
```

---

## The Solution

### Part 1: Model Aliases in UserConfig

Add an `Aliases` dictionary to `UserConfig`:

```json
{
  "ActiveModel": "gpt-4o-2024-08-06",
  "AvailableModels": ["gpt-4o-2024-08-06", "gpt-4.1-mini", "gpt-4-turbo"],
  "ModelAliases": {
    "4o": "gpt-4o-2024-08-06",
    "mini": "gpt-4.1-mini",
    "turbo": "gpt-4-turbo",
    "fast": "gpt-4.1-mini",
    "smart": "gpt-4o-2024-08-06"
  }
}
```

#### Usage

```bash
# Set model by alias
az-ai --set-model mini
# Active model set to: gpt-4.1-mini (via alias 'mini')

# List models shows aliases
az-ai --models
# Available models:
# → gpt-4o-2024-08-06 * (aliases: 4o, smart)
#   gpt-4.1-mini         (aliases: mini, fast)
#   gpt-4-turbo          (aliases: turbo)

# Create new alias
az-ai --config set alias quick gpt-4.1-mini
```

#### Implementation

In `UserConfig.cs`:

```csharp
public Dictionary<string, string> ModelAliases { get; set; } = new(StringComparer.OrdinalIgnoreCase);

/// <summary>
/// Resolves a model name or alias to a deployment name.
/// Returns null if neither a direct match nor an alias match is found.
/// </summary>
public string? ResolveModel(string nameOrAlias)
{
    // Direct match first
    var direct = AvailableModels.FirstOrDefault(m => m.Equals(nameOrAlias, StringComparison.OrdinalIgnoreCase));
    if (direct != null) return direct;

    // Alias lookup
    if (ModelAliases.TryGetValue(nameOrAlias, out var aliasTarget))
    {
        return AvailableModels.FirstOrDefault(m => m.Equals(aliasTarget, StringComparison.OrdinalIgnoreCase))
            ?? aliasTarget; // Return alias target even if not in AvailableModels (user knows best)
    }

    // Prefix/substring match as last resort (e.g., "4o" matches "gpt-4o-2024-08-06")
    var prefixMatch = AvailableModels.FirstOrDefault(m => m.Contains(nameOrAlias, StringComparison.OrdinalIgnoreCase));
    return prefixMatch;
}
```

Update `SetActiveModel` (line 124) and `HandleModelCommands` (line 756) to use `ResolveModel`:

```csharp
public bool SetActiveModel(string nameOrAlias)
{
    var resolved = ResolveModel(nameOrAlias);
    if (resolved != null)
    {
        ActiveModel = resolved;
        return true;
    }
    return false;
}
```

### Part 2: `--model` Flag for Per-Invocation Model Override

Add a `--model` flag that overrides the active model for a single invocation without persisting:

```bash
# Use mini for this one question (doesn't change default)
az-ai --model mini "What's 2+2?"

# Use full model name
az-ai --model gpt-4-turbo "Explain quantum entanglement in detail"
```

In `ParseCliFlags`, add:

```csharp
else if (arg == "--model" || arg == "-m")
{
    if (i + 1 < args.Length)
    {
        modelOverride = args[++i];
    }
    else
    {
        return (null, new CliParseError("[ERROR] --model requires a model name or alias", 1));
    }
}
```

In `Main`, after `ValidateConfiguration`:

```csharp
// Apply per-invocation model override (does not persist)
if (opts.ModelOverride != null)
{
    var resolved = config.ResolveModel(opts.ModelOverride);
    if (resolved == null)
        return ErrorAndExit($"Unknown model or alias '{opts.ModelOverride}'. Run --models to see available.", 1, jsonMode);
    deploymentName = resolved;
    chatClient = azureClient.GetChatClient(deploymentName);
}
```

### Part 3: Auto-Generate Default Aliases

When `InitializeFromEnvironment` loads models, auto-generate sensible aliases for common model patterns:

```csharp
public void InitializeFromEnvironment(string? modelsEnvVar)
{
    // ... existing parsing logic ...

    // Auto-generate aliases for common model name patterns
    foreach (var model in AvailableModels)
    {
        var lower = model.ToLowerInvariant();

        // "gpt-4o-2024-..." → alias "4o"
        if (lower.Contains("4o") && !ModelAliases.ContainsKey("4o"))
            ModelAliases["4o"] = model;

        // "gpt-4.1-mini" → alias "mini"
        if (lower.Contains("mini") && !ModelAliases.ContainsKey("mini"))
            ModelAliases["mini"] = model;

        // "gpt-4-turbo" → alias "turbo"
        if (lower.Contains("turbo") && !ModelAliases.ContainsKey("turbo"))
            ModelAliases["turbo"] = model;

        // "o3" or "o4-mini" → alias "o3", "o4"
        if (System.Text.RegularExpressions.Regex.IsMatch(lower, @"^o\d"))
        {
            var prefix = lower.Split('-')[0]; // "o3", "o4"
            if (!ModelAliases.ContainsKey(prefix))
                ModelAliases[prefix] = model;
        }
    }
}
```

User-defined aliases always take precedence over auto-generated ones.

### Part 4: Normalize Environment Variable Names

Accept both old and new naming conventions, with the new names documented as canonical:

```csharp
// Accept both legacy and normalized names (normalized takes precedence)
string? azureOpenAiEndpoint =
    Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT")    // new canonical
    ?? Environment.GetEnvironmentVariable("AZUREOPENAIENDPOINT");  // legacy

string? azureOpenAiModel =
    Environment.GetEnvironmentVariable("AZURE_OPENAI_MODEL")
    ?? Environment.GetEnvironmentVariable("AZUREOPENAIMODEL");

string? azureOpenAiApiKey =
    Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY")
    ?? Environment.GetEnvironmentVariable("AZUREOPENAIAPI");
```

Update `.env.example` to show the canonical names while documenting the legacy ones:

```env
# Canonical names (recommended):
AZURE_OPENAI_ENDPOINT=https://<REPLACE_ME>.openai.azure.com/
AZURE_OPENAI_MODEL=<REPLACE_ME>
AZURE_OPENAI_API_KEY=<REPLACE_ME>

# Legacy names (still supported):
# AZUREOPENAIENDPOINT, AZUREOPENAIMODEL, AZUREOPENAIAPI
```

---

## Combined Example: A Day in the Life

```bash
# Morning: set up my aliases once
az-ai --config set alias fast gpt-4.1-mini
az-ai --config set alias deep gpt-4o-2024-08-06

# Quick question (use fast model, don't change default)
az-ai --model fast "What's the AWS CLI command to list S3 buckets?"

# Deep analysis (use full model, don't change default)
git diff | az-ai --model deep "Review this PR for security issues"

# Switch default for the afternoon
az-ai --set-model mini

# Check what's active
az-ai --models
# Available models:
# → gpt-4.1-mini *          (aliases: mini, fast)
#   gpt-4o-2024-08-06       (aliases: 4o, deep)
```

---

## Expected Impact

| Before | After |
|---|---|
| `--set-model gpt-4o-2024-08-06-global-standard` | `--set-model 4o` |
| Switching models requires two commands | `--model mini "question"` — one-off, no persistence |
| Guessing env var names | Consistent `AZURE_OPENAI_*` + legacy fallback |
| No aliases | Auto-generated + user-defined aliases |
| `--models` shows raw deployment names | Shows aliases alongside deployment names |

---

## Files Affected

| File | Change |
|---|---|
| `azureopenai-cli/UserConfig.cs` | Add `ModelAliases` dict, `ResolveModel()`, auto-alias generation |
| `azureopenai-cli/JsonGenerationContext.cs` | Add `[JsonSerializable(typeof(Dictionary<string, string>))]` |
| `azureopenai-cli/Program.cs` (ParseCliFlags) | Add `--model`/`-m` flag |
| `azureopenai-cli/Program.cs` (Main, line 468–478) | Apply model override via `ResolveModel` |
| `azureopenai-cli/Program.cs` (ListModels, line 797–818) | Display aliases next to model names |
| `azureopenai-cli/Program.cs` (lines 384–387) | Accept normalized env var names with legacy fallback |
| `azureopenai-cli/.env.example` | Update to canonical names |

---

## Exit Criteria

- [ ] `--set-model mini` resolves alias to full deployment name
- [ ] `--model fast "question"` uses alias for one invocation, doesn't persist
- [ ] `--models` displays aliases next to each model
- [ ] `--config set alias <short> <deployment>` creates user-defined alias
- [ ] Auto-aliases generated for common patterns (4o, mini, turbo)
- [ ] User-defined aliases override auto-generated ones
- [ ] `AZURE_OPENAI_ENDPOINT` accepted alongside legacy `AZUREOPENAIENDPOINT`
- [ ] Existing `.env` files with legacy names continue to work (backward compatible)
- [ ] Substring matching works as fallback: `--model turbo` resolves `gpt-4-turbo`
