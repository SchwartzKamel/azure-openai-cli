# Git & Agent Automation Examples

Ready-to-use manifests and hook recipes for running `az-ai` inside Git workflows and MCP agent hosts:

1. **Custom Git Plugin Manifest (`git_context.tool.json`):**
   ```bash
   az-ai --plugin-dir examples/git-automation --list-tools --json
   ```
2. **Test `git_context` over MCP Stdio (`az-ai mcp call`):**
   ```bash
   az-ai mcp call "az-ai --plugin-dir examples/git-automation mcp serve" git_context '{"count":3}' --json
   ```
3. **Full Guide & Ecosystem Comparison:**
   See [`docs/use-cases-git-and-agent-interop.md`](../../docs/use-cases-git-and-agent-interop.md).
