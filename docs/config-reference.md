# `.azureopenai-cli.json` -- Config Reference

A local JSON file that lets you define model aliases, a default model, and
preference defaults so you don't have to pass `--model`, `--temperature`,
`--max-tokens`, etc. on every invocation.

> Scope: **v2.0.0+** (`az-ai`). v1 reads a compatible subset (see
> [v1→v2 migration guide](migration-v1-to-v2.md) once published).

---

## TL;DR

```bash
# Copy the sample into your home directory
curl -sSL https://raw.githubusercontent.com/SchwartzKamel/azure-openai-cli/main/docs/examples/azureopenai-cli.sample.json \
  -o ~/.azureopenai-cli.json

# Or use the CLI
az-ai --set-model fast=gpt-4o-mini
az-ai --set-model smart=gpt-4o
az-ai --config set default_model=fast
az-ai --config set defaults.temperature=0.7

# Verify
az-ai --config list
az-ai --models
az-ai --current-model
```

Now `az-ai --model smart "…"` resolves `smart` → `gpt-4o` automatically,
and omitting `--model` uses `fast`.

---

## Sample file

See [`examples/azureopenai-cli.sample.json`](examples/azureopenai-cli.sample.json)
for a drop-in starter. The full schema:

```json
{
  "models": {
    "fast": "gpt-4o-mini",
    "smart": "gpt-4o",
    "reasoning": "gpt-5",
    "cheap": "gpt-35-turbo"
  },
  "default_model": "fast",
  "defaults": {
    "temperature": 0.7,
    "max_tokens": 4000,
    "timeout_seconds": 90,
    "system_prompt": "You are a terse, accurate assistant. No preamble, no sign-off."
  }
}
```

### Field reference

| Field | Type | Default | Notes |
|---|---|---|---|
| `models` | `{ alias: deployment }` | `{}` | Alias → Azure deployment name. Used by `--model <alias>`. Alias is arbitrary; deployment must match the name in your Azure OpenAI resource. |
| `default_model` | string | `null` | Which alias in `models` to use when `--model` and `AZUREOPENAIMODEL` are both unset. |
| `defaults.temperature` | float | model default | `0.0`-`2.0`. |
| `defaults.max_tokens` | int | model default | Caps completion length. gpt-5/o1/o3 enforce this as `max_completion_tokens` on the wire. |
| `defaults.timeout_seconds` | int | `90` | Request timeout. |
| `defaults.system_prompt` | string | none | Prepended to every request. Override per-call with `--system-prompt`. |

All `defaults.*` fields are **nullable** -- omit a field to use the built-in
default instead of forcing `null`.

---

## File locations

Looked up in this order (first match wins):

1. `--config <path>` -- explicit override
2. `./.azureopenai-cli.json` -- project-local (e.g., a repo's own config)
3. `~/.azureopenai-cli.json` -- user-global
4. Built-in defaults (no file)

Project-local files override the user-global file, which is handy for
team-shared prompts per repo without clobbering your personal setup.

**Permissions.** On Unix-likes, `--set-model` and `--config set` chmod the
file to `0600` (read-write owner only) so the file doesn't leak to other
local users on shared machines.

---

## Precedence

When resolving a setting, the CLI checks, in order (first wins):

1. **CLI flag** -- e.g., `--model`, `--temperature`, `--max-tokens`
2. **Environment variable** -- e.g., `AZUREOPENAIMODEL` for the model name
3. **Project-local config** -- `./.azureopenai-cli.json`
4. **User-global config** -- `~/.azureopenai-cli.json`
5. **Built-in default** -- `gpt-4o-mini` for model, provider defaults for rest

Example:

```bash
# Config has default_model=fast → gpt-4o-mini
az-ai "hi"                            # uses gpt-4o-mini (config)
AZUREOPENAIMODEL=gpt-4o az-ai "hi"    # uses gpt-4o (env beats config)
az-ai --model smart "hi"              # uses gpt-4o (smart alias, CLI beats env)
```

---

## CLI cheatsheet

```bash
# Models
az-ai --set-model <alias>=<deployment>    # add an alias; first alias is auto-default
az-ai --models                            # list all aliases + mark default
az-ai --current-model                     # print default alias
az-ai --model <alias> "…"                 # use a specific alias
az-ai --model <deployment> "…"            # literal deployment also works

# Config CRUD
az-ai --config show                       # dump as JSON
az-ai --config list                       # one "key=value" line per field
az-ai --config get <dotted.key>           # e.g., defaults.temperature
az-ai --config set <dotted.key>=<value>   # e.g., default_model=smart
az-ai --config reset                      # empty the file
az-ai --config <path>                     # use a specific file for this invocation
```

Dotted keys: `default_model`, `models.<alias>`, `defaults.<temperature|max_tokens|timeout_seconds|system_prompt>`.

---

## WSL gotchas

Running the CLI from Linux on WSL2 while keeping the Windows host's
Azure credentials / espanso / AHK configs is the common setup. A few
ways this bites people:

### 1. `~` resolves to the Linux home, not Windows

When you run `az-ai` inside WSL, `~/.azureopenai-cli.json` means
`/home/<you>/.azureopenai-cli.json` (Linux), **not**
`C:\Users\<you>\.azureopenai-cli.json` (Windows).

If you already have a config on the Windows side, symlink it:

```bash
# From inside WSL
ln -s /mnt/c/Users/<YourName>/.azureopenai-cli.json ~/.azureopenai-cli.json
```

Or copy it:

```bash
cp /mnt/c/Users/<YourName>/.azureopenai-cli.json ~/
```

If you run **both** `az-ai` (Windows binary via espanso) and `az-ai`
(Linux binary from WSL) from the same keyboard, keep the two files in
sync -- a symlink is the zero-maintenance option.

### 2. File permissions surprise on `/mnt/c/...`

Windows drives mounted in WSL (`/mnt/c/…`) don't honor Unix `chmod`.
`--set-model` will succeed but the 0600 permission is a no-op. Your
Azure API key sitting in an Azure shell wrapper around the config is
still only as safe as Windows NTFS permissions make it. If that
matters for your threat model, keep the config inside your WSL home
(`~/…`) on the ext4 filesystem, where `0600` is enforced.

### 3. Line endings

If you edit the config with Notepad or a Windows editor, it may
write CRLF line endings. The parser tolerates it (JSON doesn't care
about newline style within whitespace), but if you ever `cat` the
file from Windows into a here-doc on Linux, the stray `\r` can
corrupt the last value. Stick to a Unix-aware editor (VS Code with
"LF" in the status bar, `vim`, `nano`) when editing from WSL.

### 4. Path precedence when `az-ai` runs from `cmd.exe` inside WSL

If you wrap `wsl az-ai …` inside a Windows espanso trigger, the
current working directory on the WSL side is **your WSL home**, not
the Windows directory you launched from. So the project-local
precedence (`./.azureopenai-cli.json`) points at your WSL `~/` by
default, not at a Windows project folder. To pin a project config
across the boundary, use `--config /path/to/cfg.json` explicitly.

### 5. Shared espanso triggers across Linux and Windows

Your Linux and Windows espanso configs may both call `az-ai`. The
two hosts have independent config files. If you want identical
behavior, either:

- Keep one config on Windows (`C:\Users\<you>\.azureopenai-cli.json`)
  and symlink it into WSL (case 1 above), **and** sync that same
  file path into your Windows `%USERPROFILE%\.azureopenai-cli.json`
  if the Windows `az-ai.exe` looks there too, or
- Keep separate configs per host and accept the drift.

### 6. Environment variables cross the boundary, config files don't

If your espanso config on Windows sets `AZUREOPENAIMODEL=…` and
shells out to `wsl az-ai …`, WSL inherits that env via
`/etc/wsl.conf` or `WSLENV`. But the config **file** does not
travel; only the symlink or copy trick above does. Because env
beats config in the precedence chain (see above), a stray env
var from the Windows side can silently override your WSL config.
Confirm with `az-ai --config show` and `env | grep AZURE`.

See also the [Espanso + AHK integration guide](espanso-ahk-integration.md)
for full Path A (Windows-native) and Path B (WSL) setups that use
this config file consistently across both hosts.

---

## Troubleshooting

**`[WARNING] Config file '…' has invalid JSON: …`**
> Your file has a syntax error. Run `jq . ~/.azureopenai-cli.json` to
> find the offending position. Common culprits: trailing commas
> (not allowed in JSON), smart quotes pasted from a doc, unescaped
> backslashes in Windows paths inside `system_prompt`.

**`[WARNING] Permission denied reading config '…'`**
> The file exists but your user can't read it. On WSL this often
> happens with configs placed on `/mnt/c` that were created by the
> Windows side under a different ACL. Copy into `~/` (ext4) or run
> `sudo chown $USER ~/.azureopenai-cli.json` if it's a Linux file.

**`--current-model` prints nothing**
> `default_model` isn't set. Fix with:
> `az-ai --config set default_model=<one-of-your-aliases>`.

**Alias resolves to itself (literal deployment)**
> If you pass `--model smart` but `smart` isn't in the `models`
> map, the CLI falls through to using `smart` as a literal Azure
> deployment name. Azure will respond `DeploymentNotFound`. Fix
> with `--set-model smart=<actual-deployment>` or `--models` to
> see what you've got.

**Config writes silently disappear on Windows**
> Check that `%USERPROFILE%\.azureopenai-cli.json` isn't being
> created in a OneDrive-synced folder that renames on sync, or in a
> roaming profile that gets overwritten on logout. Point the CLI
> at a stable path via `--config <path>` if so.

---

## Security notes

- **Never put your API key or endpoint in this file.** Use the
  `AZUREOPENAIENDPOINT` / `AZUREOPENAIAPI` environment variables.
  The CLI intentionally provides no config-file slot for credentials
  -- config files get committed, shared, or synced; env vars don't.
- **`system_prompt` is not secret.** It's echoed by `--config show`
  and included in request bodies that can be captured by Fiddler /
  Wireshark / network telemetry. Treat it as public.
- **Team-shared project configs** (a `./.azureopenai-cli.json`
  checked in with a repo) should contain only model aliases and
  `defaults.system_prompt` for that project -- never anything user-
  specific.

---

## See also

- [Prerequisites](prerequisites.md) -- environment variables
- [Use cases: config integration](use-cases-config-integration.md)
- [Espanso + AHK integration](espanso-ahk-integration.md) -- Path A/B,
  full WSL wiring
- [Observability](observability.md) -- `--telemetry`, cost events
