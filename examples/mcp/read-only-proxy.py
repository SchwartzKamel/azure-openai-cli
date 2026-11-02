#!/usr/bin/env python3
"""read-only-proxy.py -- put an allowlist in front of any MCP stdio server.

Why: hopper's MCP *client* (--mcp-server) hands the model every tool the server lists. The --read-only flag
only governs hopper's own MCP server. Some servers mix safe and destructive tools and declare no
readOnlyHint (the agent-toolkit server lists janitor_run and updater_run beside its reports), so there is
nothing to filter on automatically. This proxy relays the real server and shows the model only the tools you
allow; a call to anything else is refused here, before it reaches the server.

Usage (the policy comes from the environment, and there is no default: with neither variable set it refuses
to start, so a missing policy cannot silently mean "everything"):

    MCP_ALLOW='janitor_report,janitor_health,updater_*' \\
    MCP_DENY='*_run' \\
        hopper --agent --mcp-server "toolkit=python3 read-only-proxy.py agent-toolkit-mcp" "..."

    MCP_ALLOW and MCP_DENY are comma-separated patterns where * matches any characters.
    A tool is visible only if it matches MCP_ALLOW (unset means no allowlist) and does not match MCP_DENY. Deny wins.
    Everything after the script name is the real server's command line.

Limits, stated plainly: this is a filter on tool names, not a sandbox. It trusts the server's tool names, does
not look inside arguments, and a server that exposes one tool which does many things stays that powerful.
For pi, prefer its own `toolExposure` setting in ~/.pi/agent/mcp.json, which does the same job natively.
"""
import fnmatch
import json
import os
import subprocess
import sys
import threading


def patterns(name):
    raw = os.environ.get(name, "")
    return [p.strip() for p in raw.split(",") if p.strip()]


ALLOW = patterns("MCP_ALLOW")
DENY = patterns("MCP_DENY")


def permitted(tool):
    if ALLOW and not any(fnmatch.fnmatchcase(tool, p) for p in ALLOW):
        return False
    return not any(fnmatch.fnmatchcase(tool, p) for p in DENY)


def main():
    if not ALLOW and not DENY:
        sys.stderr.write("read-only-proxy: set MCP_ALLOW and/or MCP_DENY; refusing to start with no policy.\n")
        return 2
    if len(sys.argv) < 2:
        sys.stderr.write("read-only-proxy: give the real server's command line after the script name.\n")
        return 2

    server = subprocess.Popen(sys.argv[1:], stdin=subprocess.PIPE, stdout=subprocess.PIPE,
                              stderr=subprocess.DEVNULL, text=True, bufsize=1)
    out_lock = threading.Lock()
    list_ids = set()  # ids of tools/list requests, so only their responses are rewritten

    def emit(message):
        with out_lock:
            sys.stdout.write(json.dumps(message) + "\n")
            sys.stdout.flush()

    def pump_server():
        for line in server.stdout:
            try:
                message = json.loads(line)
            except ValueError:
                continue
            result = message.get("result")
            if message.get("id") in list_ids and isinstance(result, dict) and "tools" in result:
                result["tools"] = [t for t in result["tools"] if permitted(t.get("name", ""))]
            emit(message)

    threading.Thread(target=pump_server, daemon=True).start()

    for line in sys.stdin:
        try:
            message = json.loads(line)
        except ValueError:
            continue
        method = message.get("method")
        if method == "tools/list":
            list_ids.add(message.get("id"))
        if method == "tools/call" and not permitted((message.get("params") or {}).get("name", "")):
            emit({"jsonrpc": "2.0", "id": message.get("id"),
                  "error": {"code": -32601, "message": "tool not permitted by read-only-proxy"}})
            continue
        server.stdin.write(json.dumps(message) + "\n")
        server.stdin.flush()

    server.terminate()
    return 0


if __name__ == "__main__":
    sys.exit(main())
