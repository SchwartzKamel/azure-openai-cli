# Git & Agent Automation Examples

Ready-to-use manifests and hook recipes for running `hopper` inside Git workflows and MCP agent hosts:

1. **Custom Git Plugin Manifest (`git_context.tool.json`):**

   ```bash
   hopper --plugin-dir examples/git-automation --list-tools --json
   ```

2. **Test `git_context` over MCP Stdio (`hopper mcp call`):**

   ```bash
   hopper mcp call "hopper --plugin-dir examples/git-automation mcp serve" git_context '{"count":3}' --json
   ```

3. **Full Guide & Ecosystem Comparison:**
   See [`docs/use-cases-git-and-agent-interop.md`](../../docs/use-cases-git-and-agent-interop.md).
