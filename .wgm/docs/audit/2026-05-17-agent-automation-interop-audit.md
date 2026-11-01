# Consolidated Documentation Audit Report -- Agent, Tool & Automation Interoperability

- **Date:** 2026-05-17
- **Scope:** `README.md`, `azureopenai-cli/Program.cs` (`--help` & shell completions), `.wgm/specs/agent-automation-interop.md`
- **Independence record:** Consolidated five-persona review (Junior Dev · Senior Dev · Principal Dev · PM → Technical Writer consolidator) run in-session with distinct persona checklists.

## Persona Reviews Summary

1. **Junior Developer Lens (Onboarding & Clarity):**
   - Verified that `az-ai --help` and `README.md` clearly explain `--json`, `--code`, `--schema`, `--list-tools`, `--plugin-dir`, `--allow-local-plugins`, `--no-plugins`, and `az-ai mcp serve`.
   - Verified that `az-ai mcp` without `serve` prints an actionable usage message: `'mcp' subcommand requires 'serve' (usage: az-ai mcp serve [--allow-shell] [--read-only])`.
2. **Senior Developer Lens (Accuracy & Contract):**
   - Verified that `README.md` accurately documents the `ChatJsonResponse` happy-path stdout contract (`response`, `model`, `mode`, `input_tokens`, `output_tokens`, `total_tokens`, `duration_ms`, `cached`) vs `ErrorJsonResponse` on stderr.
   - Verified that `--schema <json>` in `README.md` reflects active wire enforcement (`ChatResponseFormat.ForJsonSchema`) rather than the outdated "lands in 2.1.x" note.
3. **Principal Developer Lens (Architecture & Security):**
   - Verified security documentation for `az-ai mcp serve` (`shell_exec` disabled by default unless `--allow-shell` is passed) and custom manifest plugins (repo-local `.azureopenai-cli/tools/` disabled by default unless `--allow-local-plugins` or `AZ_ALLOW_LOCAL_PLUGINS=1` is set).
4. **Product Manager Lens (Discoverability & JTBD):**
   - Confirmed all new flags appear in `--help` and bash/zsh/fish completion scripts.

## Action Items

| ID | Classification | Item | Status |
|---|---|---|---|
| DA-1 | **Agent action** | Update `README.md` flag table to reflect `--json` happy-path envelope, `--code`, enforced `--schema`, enforced `--max-rounds`, `--list-tools`, plugin flags, and `az-ai mcp serve`. | Completed in T4 |
| DA-2 | **Agent action** | Update `Program.ShowHelp()` and shell completion scripts with `--code`, `--list-tools`, `--plugin-dir`, `--allow-local-plugins`, `--no-plugins`, and `mcp serve` flags. | Completed in T1/T3/T4 |
| DA-3 | **Agent action** | Document Wave 2 additions (`--jsonl`, `--pattern` / `-p`, `--patterns [--json]`, `--personas --json`, `--models --json`, `--mcp-server`, `az-ai mcp tools`, `az-ai mcp call`, and MCP Prompts/Resources) in `README.md` and `Program.ShowHelp()`. | Completed in T5/T6/T7 |
| DA-4 | **Operator action** | When ready to wire `az-ai` into Claude Code, GitHub Copilot CLI, or Cursor, add `{"command": "az-ai", "args": ["mcp", "serve"]}` to the host's MCP config file, or pass `--mcp-server "<cmd>"` to consume external MCP servers inside `az-ai`. | Ready for operator |
