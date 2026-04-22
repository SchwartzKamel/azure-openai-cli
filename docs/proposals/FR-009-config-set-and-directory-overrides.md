# FR-009: `--config set` Commands & Per-Directory Config Overrides

> **Status: Superseded by FR-014.** The flag surface and `~/.azureopenai-cli.json`
> reader shipped in v2.0.0 as part of FR-014's Phase 1 (legacy backward-compat
> reader). Future work on preferences, aliases, and directory overrides tracks
> in [FR-014](FR-014-local-preferences-and-multi-provider.md).

**Priority:** P1 -- High  
**Impact:** Eliminates the need to hand-edit JSON for preferences; enables project-specific AI behavior  
**Effort:** Medium (1-2 days)  
**Category:** Configuration UX

---

## The Problem

### Problem 1: UserConfig Has Fields Nobody Can Set

FR-003 added preference fields to `UserConfig` (lines 22-27):

```csharp
// FR-003: User preference overrides (nullable = only override when explicitly set)
public float? Temperature { get; set; }
public int? MaxTokens { get; set; }
public int? TimeoutSeconds { get; set; }
public string? SystemPrompt { get; set; }
```

And `GetEffectiveConfig` (line 300-308) already implements the precedence chain:

```csharp
float temperature = opts.Temperature ?? config.Temperature ?? TryParseEnvFloat("AZURE_TEMPERATURE", DEFAULT_TEMPERATURE);
int maxTokens = opts.MaxTokens ?? config.MaxTokens ?? TryParseEnvInt("AZURE_MAX_TOKENS", DEFAULT_MAX_TOKENS);
```

**But there's no CLI command to set these values.** The only way to persist `Temperature: 0.2` is to manually edit `~/.azureopenai-cli.json`. There's `--set-model` for models (line 769-775), but nothing equivalent for temperature, max-tokens, timeout, or system prompt.

The `--config show` command exists (line 926-993) and shows where each value comes from (`cli flag`, `config`, `env`, `default`). But it's read-only. The data model is ready; the CLI surface is not.

### Problem 2: One Config for All Contexts

The config file lives at `~/.azureopenai-cli.json` -- a single global file. This means:

- Working on a Terraform project? Same system prompt as when writing Python.
- In a security-review repo? Same temperature as creative writing.
- Pair programming on someone else's machine? Your preferences don't travel with the project.

Every mature developer tool has solved this with per-directory config: `.editorconfig`, `.eslintrc`, `.prettierrc`, `pyproject.toml`, `.gitconfig` (with `includeIf`). The pattern is well-understood: check the current directory (and parents) for a local override, merge with the global config.

---

## The Solution

### Part 1: `--config set/get/reset` Commands

Extend the existing `--config` subcommand (currently only supports `show`) with `set`, `get`, and `reset`:

```bash
# Set persistent preferences (saved to ~/.azureopenai-cli.json)
az-ai --config set temperature 0.2
az-ai --config set max-tokens 4000
az-ai --config set timeout 60
az-ai --config set system-prompt "You are a Terraform expert. Output only HCL."

# Read a specific value with its source
az-ai --config get temperature
# Temperature: 0.2 (config) -- default is 0.55

# Reset a value to "not set" (falls through to env/default)
az-ai --config reset temperature
# Temperature reset. Effective value: 0.55 (default)

# Show everything (already implemented)
az-ai --config show
```

#### Implementation: Extend `HandleModelCommands` or New Config Router

The cleanest approach is to expand the `--config` handling in `ParseCliFlags` (line 128-139). Currently:

```csharp
else if (arg == "--config")
{
    if (i + 1 < args.Length && args[i + 1].ToLowerInvariant() == "show")
    {
        showConfig = true;
        i++;
    }
    else
    {
        return (null, new CliParseError("[ERROR] Unknown --config subcommand. Usage: --config show", 1));
    }
}
```

Extend to:

```csharp
else if (arg == "--config")
{
    if (i + 1 >= args.Length)
        return (null, new CliParseError("[ERROR] --config requires a subcommand: show, set, get, reset", 1));

    var subCmd = args[i + 1].ToLowerInvariant();
    i++;

    switch (subCmd)
    {
        case "show":
            showConfig = true;
            break;
        case "set":
            if (i + 2 >= args.Length)
                return (null, new CliParseError("[ERROR] --config set requires <key> <value>", 1));
            configSetKey = args[i + 1];
            configSetValue = args[i + 2];
            i += 2;
            break;
        case "get":
            if (i + 1 >= args.Length)
                return (null, new CliParseError("[ERROR] --config get requires <key>", 1));
            configGetKey = args[i + 1];
            i++;
            break;
        case "reset":
            if (i + 1 >= args.Length)
                return (null, new CliParseError("[ERROR] --config reset requires <key>", 1));
            configResetKey = args[i + 1];
            i++;
            break;
        default:
            return (null, new CliParseError($"[ERROR] Unknown --config subcommand '{subCmd}'. Options: show, set, get, reset", 1));
    }
}
```

The handler in `Main`:

```csharp
if (opts.ConfigSetKey != null)
{
    return HandleConfigSet(config, opts.ConfigSetKey, opts.ConfigSetValue!);
}

static int HandleConfigSet(UserConfig config, string key, string value)
{
    switch (key.ToLowerInvariant())
    {
        case "temperature":
            if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float temp) || temp < 0 || temp > 2)
                return ErrorAndExit("Temperature must be a number between 0.0 and 2.0", 1, false);
            config.Temperature = temp;
            break;
        case "max-tokens":
            if (!int.TryParse(value, out int tokens) || tokens <= 0 || tokens > 128000)
                return ErrorAndExit("Max tokens must be between 1 and 128000", 1, false);
            config.MaxTokens = tokens;
            break;
        case "timeout":
            if (!int.TryParse(value, out int timeout) || timeout <= 0)
                return ErrorAndExit("Timeout must be a positive integer (seconds)", 1, false);
            config.TimeoutSeconds = timeout;
            break;
        case "system-prompt":
            config.SystemPrompt = value;
            break;
        default:
            return ErrorAndExit($"Unknown config key '{key}'. Options: temperature, max-tokens, timeout, system-prompt", 1, false);
    }
    config.Save();
    Console.WriteLine($"✅ {key} set to: {value}");
    return 0;
}
```

### Part 2: Per-Directory Config Overrides

Support a `.azureopenai-cli.json` file in the current working directory (or any parent directory, walking up to `/`). This file merges on top of the global `~/.azureopenai-cli.json` -- local values override global, unset values fall through.

#### Lookup Order

```
1. CLI flags (--temperature 0.9)           ← highest precedence
2. .azureopenai-cli.json in CWD            ← project-level
3. .azureopenai-cli.json walking up to /   ← workspace-level
4. ~/.azureopenai-cli.json                 ← global user config
5. Environment variables (.env)            ← build-time defaults
6. Hardcoded defaults                      ← lowest precedence
```

#### Example: Project-Specific Config

```bash
# In a Terraform project:
cd ~/projects/infra
cat .azureopenai-cli.json
```

```json
{
  "Temperature": 0.1,
  "SystemPrompt": "You are an infrastructure-as-code expert. Output HCL only unless asked to explain. Follow HashiCorp best practices.",
  "MaxTokens": 4000
}
```

Now every `az-ai` invocation from within `~/projects/infra/` (or subdirectories) uses these settings automatically. No flags needed.

#### Implementation: `UserConfig.LoadWithOverrides()`

```csharp
/// <summary>
/// Loads configuration with per-directory override support.
/// Walks from CWD up to root looking for .azureopenai-cli.json,
/// then merges with the global config (~/.azureopenai-cli.json).
/// </summary>
public static UserConfig LoadWithOverrides()
{
    // Load global config first
    var global = Load();

    // Walk up from CWD looking for local overrides
    var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
    UserConfig? localConfig = null;

    while (dir != null)
    {
        var localPath = Path.Combine(dir.FullName, ConfigFileName);
        if (File.Exists(localPath) && localPath != ConfigFilePath) // Don't double-load the global
        {
            try
            {
                var json = File.ReadAllText(localPath);
                localConfig = JsonSerializer.Deserialize(json, AppJsonContext.Default.UserConfig);
            }
            catch { }
            break; // Use the nearest override
        }
        dir = dir.Parent;
    }

    if (localConfig == null) return global;

    // Merge: local overrides global (null = "not set, fall through")
    return new UserConfig
    {
        ActiveModel = localConfig.ActiveModel ?? global.ActiveModel,
        AvailableModels = localConfig.AvailableModels.Count > 0 ? localConfig.AvailableModels : global.AvailableModels,
        Temperature = localConfig.Temperature ?? global.Temperature,
        MaxTokens = localConfig.MaxTokens ?? global.MaxTokens,
        TimeoutSeconds = localConfig.TimeoutSeconds ?? global.TimeoutSeconds,
        SystemPrompt = localConfig.SystemPrompt ?? global.SystemPrompt,
    };
}
```

Replace `UserConfig.Load()` with `UserConfig.LoadWithOverrides()` on Program.cs line 345.

#### `--config show` Upgrade

Show which config file(s) are active:

```
Azure OpenAI CLI Configuration
===============================
  Endpoint:      https://my-resource.openai.azure.com  (env)
  Model:         gpt-4o                                 (config: global)
  Temperature:   0.1                                    (config: ./azureopenai-cli.json)
  Max Tokens:    4000                                   (config: ./azureopenai-cli.json)
  Timeout:       120s                                   (default)
  System Prompt: "You are an infrastructure-as-code..." (config: ./azureopenai-cli.json)

  Config Files:
    Global:  ~/.azureopenai-cli.json
    Local:   ~/projects/infra/.azureopenai-cli.json  ← active
```

#### `--config set --local` for Project Config

```bash
# Set in the global config (default)
az-ai --config set temperature 0.2

# Set in the current directory's config
az-ai --config set --local temperature 0.1
az-ai --config set --local system-prompt "You are a Terraform expert."
```

---

## Why Both Parts Together

`--config set` without per-directory overrides gives you one global config. Per-directory overrides without `--config set` forces hand-editing JSON. Together, they create a natural workflow:

```bash
# Global: set your defaults once
az-ai --config set temperature 0.55
az-ai --config set system-prompt "You are a concise CLI assistant."

# Per-project: override for specific contexts
cd ~/projects/security-audit
az-ai --config set --local temperature 0.0
az-ai --config set --local system-prompt "You are a security auditor. Flag all vulnerabilities."

# Per-invocation: one-off experiments (already implemented via --temperature flag)
az-ai --temperature 0.9 "Write a creative poem"
```

Three levels of preference. Zero JSON hand-editing. Full discoverability via `--config show`.

---

## Expected Impact

| Before | After |
|---|---|
| Changing temperature requires editing JSON | `az-ai --config set temperature 0.2` |
| One system prompt for all contexts | Project-specific system prompts auto-activate |
| Users don't know what's configurable | `--config show` + `--config set <TAB>` discovers everything |
| Shared machines = shared config | Per-directory overrides travel with the repo |
| Config precedence is invisible | `--config show` labels every value with its source file |

---

## Files Affected

| File | Change |
|---|---|
| `azureopenai-cli/UserConfig.cs` | Add `LoadWithOverrides()`, merge logic |
| `azureopenai-cli/Program.cs` (ParseCliFlags) | Extend `--config` with `set`, `get`, `reset`, `--local` flag |
| `azureopenai-cli/Program.cs` (Main) | Replace `UserConfig.Load()` with `UserConfig.LoadWithOverrides()` |
| `azureopenai-cli/Program.cs` (new methods) | `HandleConfigSet`, `HandleConfigGet`, `HandleConfigReset` |
| `azureopenai-cli/Program.cs` (ShowEffectiveConfig) | Show source file paths, indicate local vs global |

---

## Exit Criteria

- [ ] `az-ai --config set temperature 0.2` persists to `~/.azureopenai-cli.json`
- [ ] `az-ai --config set --local temperature 0.1` persists to `./.azureopenai-cli.json`
- [ ] `az-ai --config get temperature` shows value and source
- [ ] `az-ai --config reset temperature` clears the override
- [ ] Local `.azureopenai-cli.json` overrides global for all preference keys
- [ ] `--config show` shows source file for each value
- [ ] Validation: temperature range, max-tokens range, positive timeout
- [ ] `--config set` works inside Docker (volume-mounted config file)
- [ ] Backward compatible: no `.azureopenai-cli.json` in CWD = exact current behavior
