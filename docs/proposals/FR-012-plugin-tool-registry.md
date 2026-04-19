# FR-012: Plugin/Tool Registry System

**Priority:** P2 — Strategic
**Effort:** Medium-Large (phased)
**Category:** Extensibility / Platform
**Status:** 📋 PLANNED
**Depends on:** FR-006 (AOT), FR-009 (config subsystem)

---

## The Problem

`ToolRegistry.Create()` in `azureopenai-cli/Tools/ToolRegistry.cs` hard-codes
six built-in tools: `shell_exec`, `read_file`, `web_fetch`, `get_clipboard`,
`get_datetime`, `delegate_task`. Every user who wants an additional tool —
`git_log`, `jira_search`, `k8s_describe`, an internal API fetcher — must fork
the repo, rebuild the AOT binary, and maintain a private branch.

This doesn't scale. The `IBuiltInTool` interface is a clean seam
(`Name` / `Description` / `ParametersSchema` / `ExecuteAsync`) that is
begging to be extensible. Agentic mode is the CLI's most differentiated
feature; keeping the tool surface closed caps its ceiling.

We want: **users add custom tools without forking, without runtime reflection,
and without opening an arbitrary-code-execution hole.**

---

## Design Options

Three viable paths, roughly in order of security/complexity tradeoff.

### Option A — Manifest-declared shell-out plugins (RECOMMENDED)

Plugins are **external executables** described by a JSON/TOML manifest
discovered at a well-known location
(`~/.config/azureopenai-cli/tools/*.json` and
`./.azureopenai-cli/tools/*.json` for project-local).

```json
{
  "name": "git_log",
  "description": "Show recent git commits in the current repository.",
  "parameters": {
    "type": "object",
    "properties": {
      "count": { "type": "integer", "default": 10 },
      "path":  { "type": "string" }
    }
  },
  "command": ["git", "log", "--oneline", "-n", "{count}"],
  "cwd": "{path}",
  "timeout_seconds": 10,
  "stdout_limit_bytes": 65536
}
```

The CLI loads these at startup via System.Text.Json source generators,
wraps each in a `ManifestTool : IBuiltInTool` implementation that argument-
substitutes `{placeholders}` into a `ProcessStartInfo` invocation, captures
stdout/stderr, and returns the result to the model.

**Pros:** AOT-clean (no reflection, no dynamic assembly load); language-
agnostic (plugin can be Bash, Python, Go, Rust); security surface is the
same `ShellExecTool` surface we already ship; easy to audit — a plugin is
one JSON file and one binary. Composes with FR-009's per-directory
override story for free.

**Cons:** Requires a subprocess per call (~1–5 ms overhead). Placeholder
substitution needs careful quoting to avoid argv injection.

### Option B — .NET Assembly Load (REJECTED for AOT mode)

Ship a second `IBuiltInToolPlugin` interface and let users drop
`.dll` files into `~/.config/azureopenai-cli/plugins/`. Load via
`AssemblyLoadContext` and reflection.

**Pros:** Maximum performance and expressiveness; same process, same
memory. **Cons:** Fundamentally incompatible with FR-006. Native AOT
binaries cannot `Assembly.LoadFrom` arbitrary managed code — the JIT is
gone. We'd either ship a JIT'd fallback binary (doubles our build matrix
and regresses our ~5.4 ms cold start) or accept that plugins don't work
in the recommended publish mode. Neither is acceptable.

### Option C — WASI-sandboxed plugins (FUTURE)

Plugins are WebAssembly modules (`.wasm`) invoked via a WASI host
embedded in the CLI (Wasmtime.NET or a managed runtime). The WASI module
gets a capability-scoped virtual filesystem and no network by default.

**Pros:** Best-in-class security model — true sandboxing, not
just "we trust the subprocess." Cross-language. Deterministic.
**Cons:** +10–20 MB to the AOT binary for the Wasmtime runtime; non-
trivial API design for passing JSON arguments; Wasmtime.NET's AOT story
is immature. Probably 2027 territory.

---

## Recommendation

**Ship Option A now. Document Option C as the future-proof target. Explicitly
reject Option B.**

Option A preserves the AOT win from FR-006, is implementable in a week,
reuses security primitives we already defend (`ShellExecTool`'s allow-list
pattern), and delivers ~90% of what power users actually want: "call my
existing script with structured arguments."

---

## Architecture Sketch

```
Tools/
├── IBuiltInTool.cs              (unchanged)
├── ToolRegistry.cs              (+ LoadManifestPlugins method)
├── Manifest/
│   ├── ToolManifest.cs          (record; registered in AppJsonContext)
│   ├── ManifestTool.cs          (IBuiltInTool wrapping ProcessStartInfo)
│   ├── ManifestLoader.cs        (discover + parse + validate)
│   └── ArgumentSubstitution.cs  (safe {placeholder} → argv expansion)
```

- Discovery order: `$XDG_CONFIG_HOME/azureopenai-cli/tools/*.json` →
  `~/.config/azureopenai-cli/tools/*.json` → `./.azureopenai-cli/tools/*.json`.
  Later entries shadow earlier entries by `name`.
- A new CLI flag `--list-tools` prints the merged registry (built-in +
  manifest) so users can confirm what the agent sees.
- A new CLI flag `--no-plugins` disables all manifest plugins for a single
  invocation (escape hatch for CI / hostile environments).
- Built-in tools always win on name collision (prevents a malicious
  `shell_exec.json` from shadowing the real one).

---

## Security Tradeoffs (Newman review required)

This is the section that matters. Plugins execute arbitrary code on behalf
of the LLM. The threat model must be explicit.

1. **Placeholder injection.** Naive string substitution into a shell
   command is a textbook RCE. The implementation MUST pass the `command`
   array as argv (not shell string), and `{placeholder}` tokens must map
   one-token-to-one-argv-entry. No shell interpolation. No `/bin/sh -c`.
2. **Manifest trust.** Any JSON in `./.azureopenai-cli/tools/` runs with
   full user privileges. `git clone`-ing a hostile repo and running
   `az-ai --agent` would own the user. Mitigations:
   - **Project-local plugins OFF by default.** Require
     `--allow-local-plugins` or explicit opt-in per directory (mirrors
     how VS Code handles workspace trust).
   - **Manifest signing (v2).** Optional Ed25519 signature field; users
     can pin trusted publisher keys in config.
3. **Resource limits.** `timeout_seconds`, `stdout_limit_bytes`, and a
   `max_memory_mb` field must be enforceable, not advisory. Use
   `Process.Kill(entireProcessTree: true)` on timeout.
4. **Environment leakage.** By default, plugins should receive a
   **minimal env** (`PATH`, `HOME`, `LANG`) — not the full parent env
   which may contain `AZURE_OPENAI_KEY`. Manifests can opt into passing
   specific env vars via an `env_passthrough: ["FOO", "BAR"]` field.
5. **Network/FS scoping.** Option A cannot enforce these (subprocess is
   unconstrained). Document clearly; treat as the motivating reason to
   pursue Option C later.
6. **Supply chain.** If we ever publish an "official plugin repo," it
   becomes a phishing target. Defer; do not build a registry in v1.

---

## AOT Compatibility

- `ToolManifest` is a plain record; register in `AppJsonContext` with
  `[JsonSerializable(typeof(ToolManifest))]`. No reflection.
- `ManifestTool.ParametersSchema` is built at load-time by re-serializing
  the manifest's `parameters` node back to `BinaryData` — same pattern
  the existing tools use.
- `System.Diagnostics.Process` is fully AOT-supported.
- **Explicitly NOT supported in AOT mode:** loading `.dll` plugins. If
  Option B is ever revisited, it must ship as a separate non-AOT
  distribution, clearly labeled.

---

## Acceptance Criteria

- [ ] `ToolManifest` record + source-generator registration added
- [ ] `ManifestLoader` discovers plugins from user + project dirs with
      documented precedence
- [ ] `ManifestTool` implements `IBuiltInTool` and passes arguments as
      argv (never shell string)
- [ ] Built-in tool names cannot be shadowed by manifests
- [ ] `--list-tools` flag prints merged registry with source annotation
      (`[builtin]` vs `[plugin: /path/to.json]`)
- [ ] `--no-plugins` flag disables manifest loading
- [ ] Project-local plugins require `--allow-local-plugins` or config opt-in
- [ ] `timeout_seconds` enforced via process-tree kill
- [ ] `stdout_limit_bytes` enforced; truncation message appended
- [ ] Default env is minimal; `env_passthrough` explicitly allow-lists
- [ ] AOT build (`make publish-aot`) passes with zero new trim warnings
- [ ] Cold start regression < 1 ms with three plugins installed
- [ ] Integration test: hostile manifest with `{path}: "; rm -rf /"`
      produces a literal argv entry, never executes the injection

---

## Open Questions

1. **Manifest format — JSON or TOML?** JSON is cheaper (source generator
   already in the binary). TOML is more human-pleasant. Start with JSON;
   revisit if users complain.
2. **Per-project plugin policy UX.** Do we store the trust decision in
   `~/.config/azureopenai-cli/trusted-dirs.json` (VS Code model) or in a
   project-local `.azureopenai-cli/trust.json` (riskier)? Lean toward the
   former.
3. **Should plugins be able to stream stdout back to the model progressively,**
   or is a single final string sufficient? Streaming compounds well with
   FR-011 (agent streaming) but adds protocol complexity. Defer to v2.
4. **Plugin discovery in Docker.** The container image has no `~/.config`.
   Do we support a `AZURE_OPENAI_CLI_PLUGINS_DIR` env var override, or
   mount a volume? Both; document the volume mount in the Docker README.
5. **Do we want a `plugin_exec` built-in tool** that the agent calls with
   a plugin name + args, instead of each plugin being a top-level tool?
   The top-level approach gives better schema visibility to the model;
   keep it.
