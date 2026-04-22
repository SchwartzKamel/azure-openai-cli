# FR-012: Plugin/Tool Registry System

**Priority:** P2 -- Strategic
**Effort:** Medium-Large (phased)
**Category:** Extensibility / Platform
**Status:** 📋 PLANNED
**Depends on:** FR-006 (AOT), FR-009 (config subsystem)

---

## The Problem

`ToolRegistry.Create()` hard-codes six built-in tools (`shell_exec`,
`read_file`, `web_fetch`, `get_clipboard`, `get_datetime`, `delegate_task`).
Every user who wants `git_log`, `jira_search`, `k8s_describe`, or an internal
API fetcher must fork, rebuild the AOT binary, and maintain a private branch.

The `IBuiltInTool` interface is a clean seam begging to be extensible.
Agentic mode is the CLI's most differentiated feature; keeping the tool
surface closed caps its ceiling. We want: **users add custom tools without
forking, without runtime reflection, and without opening an RCE hole.**

---

## Design Options

Three viable paths, roughly in order of security/complexity tradeoff.

### Option A -- Manifest-declared shell-out plugins (RECOMMENDED)

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

The CLI loads these at startup via source-generated JSON, wraps each in a
`ManifestTool : IBuiltInTool` that argv-substitutes `{placeholders}` into a
`ProcessStartInfo`, captures stdout/stderr, and returns the result.

**Pros:** AOT-clean (no reflection, no dynamic assembly load); language-
agnostic (Bash, Python, Go, Rust); security surface is the same
`ShellExecTool` surface we already ship; easy to audit. Composes with
FR-009's per-directory override story.

**Cons:** Subprocess overhead (~1-5 ms per call). Placeholder substitution
needs careful quoting to avoid argv injection.

### Option B -- .NET Assembly Load (REJECTED for AOT mode)

Drop `.dll` files into a plugins dir; load via `AssemblyLoadContext`.
Max performance, same process. **Fundamentally incompatible with FR-006:**
AOT binaries cannot `Assembly.LoadFrom` arbitrary managed code. We'd need
a JIT'd fallback binary (doubles build matrix, regresses the sub-15 ms cold
start -- see [`docs/perf/v2.0.5-baseline.md`](../perf/v2.0.5-baseline.md))
or accept plugins don't work in the recommended publish mode.
Neither is acceptable.

### Option C -- WASI-sandboxed plugins (FUTURE)

`.wasm` modules invoked via an embedded WASI host (Wasmtime.NET). True
capability-scoped sandboxing, cross-language, deterministic. **Cons:**
+10-20 MB to the AOT binary; Wasmtime.NET's AOT story is immature.
Probably 2027 territory.

---

## Recommendation

**Ship Option A now. Document Option C as the future-proof target. Reject
Option B.** Option A preserves the FR-006 AOT win, is implementable in a
week, reuses security primitives we already defend, and delivers ~90% of
what power users want: "call my existing script with structured arguments."

---

## Architecture Sketch

```text
Tools/Manifest/
├── ToolManifest.cs          (record; registered in AppJsonContext)
├── ManifestTool.cs          (IBuiltInTool wrapping ProcessStartInfo)
├── ManifestLoader.cs        (discover + parse + validate)
└── ArgumentSubstitution.cs  (safe {placeholder} → argv expansion)
```

- Discovery: `$XDG_CONFIG_HOME/azureopenai-cli/tools/*.json` →
  `~/.config/azureopenai-cli/tools/*.json` → `./.azureopenai-cli/tools/*.json`.
  Later entries shadow earlier by `name`.
- `--list-tools` prints the merged registry with source annotation.
- `--no-plugins` disables manifest loading (CI / hostile envs).
- Built-in tools always win on name collision (prevents a malicious
  `shell_exec.json` from shadowing the real one).

---

## Security Tradeoffs (Newman review required)

Plugins execute arbitrary code on behalf of the LLM. Threat model:

1. **Placeholder injection.** Naive string substitution into a shell
   command is a textbook RCE. Implementation MUST pass `command` as argv
   (not shell string); `{placeholder}` tokens map one-to-one to argv
   entries. No `/bin/sh -c`. No shell interpolation.
2. **Manifest trust.** Any JSON in `./.azureopenai-cli/tools/` runs with
   full user privileges -- `git clone` + `az-ai --agent` would own the
   user. Mitigation: **project-local plugins OFF by default**; require
   `--allow-local-plugins` or explicit per-directory opt-in (VS Code
   workspace-trust model). Optional Ed25519 manifest signing in v2.
3. **Resource limits.** `timeout_seconds`, `stdout_limit_bytes`,
   `max_memory_mb` must be enforceable, not advisory. Use
   `Process.Kill(entireProcessTree: true)` on timeout.
4. **Environment leakage.** Default to minimal env (`PATH`, `HOME`,
   `LANG`) -- not the full parent env which contains `AZURE_OPENAI_KEY`.
   Manifests opt in via `env_passthrough: ["FOO", "BAR"]`.
5. **Network/FS scoping.** Option A cannot enforce these (subprocess is
   unconstrained). Document clearly; this is the motivating reason to
   pursue Option C later.
6. **Supply chain.** An "official plugin repo" becomes a phishing target.
   Defer; do not build a registry in v1.

---

## AOT Compatibility

- `ToolManifest` is a plain record; register in `AppJsonContext` with
  `[JsonSerializable(typeof(ToolManifest))]`. No reflection.
- `ManifestTool.ParametersSchema` built at load-time by re-serializing the
  manifest's `parameters` node to `BinaryData` -- same pattern existing
  tools use.
- `System.Diagnostics.Process` is fully AOT-supported.
- **Explicitly NOT supported in AOT mode:** loading `.dll` plugins. If
  Option B is ever revisited, it must ship as a separate non-AOT
  distribution, clearly labeled.

---

## Acceptance Criteria

- [ ] `ToolManifest` record + source-generator registration
- [ ] `ManifestLoader` discovers plugins from user + project dirs with
      documented precedence
- [ ] `ManifestTool` passes arguments as argv (never shell string)
- [ ] Built-in tool names cannot be shadowed by manifests
- [ ] `--list-tools` prints merged registry with source annotation
      (`[builtin]` vs `[plugin: /path/to.json]`)
- [ ] `--no-plugins` disables manifest loading
- [ ] Project-local plugins require `--allow-local-plugins` or config opt-in
- [ ] `timeout_seconds` enforced via process-tree kill; `stdout_limit_bytes`
      enforced with truncation message
- [ ] Default env is minimal; `env_passthrough` explicitly allow-lists
- [ ] AOT build passes with zero new trim warnings; cold-start regression
      < 1 ms with three plugins installed
- [ ] Integration test: hostile manifest with `{path}: "; rm -rf /"`
      produces a literal argv entry, never executes the injection

---

## Open Questions

1. **Manifest format -- JSON or TOML?** JSON is cheaper (source generator
   already in the binary). Start JSON; revisit if users complain.
2. **Per-project trust UX.** Store decision in
   `~/.config/azureopenai-cli/trusted-dirs.json` (VS Code model) or
   project-local (riskier)? Lean toward the former.
3. **Progressive stdout streaming to the model?** Compounds well with
   FR-011 but adds protocol complexity. Defer to v2.
4. **Plugin discovery in Docker.** Support `AZURE_OPENAI_CLI_PLUGINS_DIR`
   env var and document a volume mount. Both.
5. **One `plugin_exec` built-in vs. top-level tools?** Top-level gives
   better schema visibility to the model; keep it.
