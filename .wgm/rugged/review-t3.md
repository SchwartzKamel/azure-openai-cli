# Review Ruggedness Record -- T3
date: 2026-05-17
phase: Review (T3)
scope: Custom Manifest Tool Plugins (FR-012) & Tool Introspection (--list-tools [--json])
produced_by: inline rubric (companion /rugged protocol applied inline)

1. Actual operator & environment:
   Developers and agent orchestrators adding custom CLI tools via `~/.config/azureopenai-cli/tools/*.json` or `--plugin-dir`, or cloning repositories that may contain `.azureopenai-cli/tools/*.json`.
2. Bottleneck decomposition:
   - Intrinsic design constraint: Native AOT (`TrimMode=full`) requires `ToolManifest` and `ToolDescriptor` to be source-generated in `AppJsonContext` rather than loading managed `.dll` plugins.
   - User capacity: Users want simple `{placeholder}` substitution in JSON manifests without writing C# wrappers.
   - Operational stress (dominant): Untrusted repo-local manifests executing without opt-in, shell command injection via `{placeholder}`, plugins shadowing built-in tools (`read_file`, `shell_exec`), or child processes leaking `AZUREOPENAIAPI`.
3. Simplify:
   Execute manifest tools strictly via `ProcessStartInfo.ArgumentList` (zero `/bin/sh -c`), scrub secret env vars unless listed in `env_passthrough`, enforce hard timeout (`Kill(entireProcessTree: true)`) and stdout byte cap, and gate repo-local plugins behind `--allow-local-plugins` / `AZ_ALLOW_LOCAL_PLUGINS=1`.
4. Exact check:
   `dotnet test tests/AzureOpenAI_CLI.Tests/AzureOpenAI_CLI.Tests.csproj -c Release --filter "FullyQualifiedName~ManifestPluginAndIntrospectionTests" --nologo` (4/4 passed).
5. Verdict:
   RUGGED
