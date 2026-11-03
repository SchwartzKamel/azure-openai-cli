#!/usr/bin/env python3
"""http-bridge.py -- let a stdio-only MCP client talk to an HTTP MCP server.

Hopper's MCP client launches a command and speaks newline-delimited JSON-RPC over its stdin and stdout. Many MCP
servers (scouter's loopback hosts, GitHub's remote server) speak HTTP instead. This relays one to the other.

    MCP_BRIDGE_TOKEN_FILE=obs.tok python3 http-bridge.py http://127.0.0.1:13340/mcp

The bearer token comes from MCP_BRIDGE_TOKEN_FILE (a path, preferred: nothing secret in the environment or argv)
or MCP_BRIDGE_TOKEN. With neither, requests go unauthenticated. The token is never written to stdout or stderr.

Two session modes, chosen with MCP_BRIDGE_SESSION:

  per-request (default)  Every client request opens an upstream session, sends the request, and closes the session
                         before returning. Nothing is left open, so nothing leaks when this process is killed.
                         Hopper's MCP client ends a server with SIGKILL and gives it no chance to clean up (see
                         findings), and servers that cap sessions (scouter admits 8 observers) then fill up and
                         refuse everyone with HTTP 429. Costs three extra loopback round trips per request.
  sticky                 One upstream session for the whole run, closed with HTTP DELETE when stdin ends or on
                         SIGTERM. Cheaper, and right for a client that shuts a server down politely.

Limits: server-to-client notifications and requests are not relayed, and per-request mode does not preserve any
state the server keeps inside a session.
"""
import json, os, signal, sys, urllib.request, urllib.error

TIMEOUT = float(os.environ.get("MCP_BRIDGE_TIMEOUT", "120"))
MODE = os.environ.get("MCP_BRIDGE_SESSION", "per-request")


def token():
    path = os.environ.get("MCP_BRIDGE_TOKEN_FILE")
    if path:
        with open(path, encoding="utf-8") as f:
            return f.read().strip()
    return os.environ.get("MCP_BRIDGE_TOKEN", "").strip()


class Upstream:
    def __init__(self, url, tok):
        self.url, self.tok, self.sid = url, tok, None

    def _headers(self):
        h = {"Content-Type": "application/json", "Accept": "application/json, text/event-stream"}
        if self.tok:
            h["Authorization"] = "Bearer " + self.tok
        if self.sid:
            h["Mcp-Session-Id"] = self.sid
        return h

    def post(self, message):
        """POST one message. Returns (http_status, error_text_or_None, [json replies])."""
        req = urllib.request.Request(self.url, data=json.dumps(message).encode("utf-8"), headers=self._headers())
        try:
            resp = urllib.request.urlopen(req, timeout=TIMEOUT)
        except urllib.error.HTTPError as e:
            return e.code, e.read().decode("utf-8", "replace"), []
        got = resp.headers.get("Mcp-Session-Id")
        if got:
            self.sid = got
        raw = resp.read().decode("utf-8", "replace")
        if "text/event-stream" in (resp.headers.get("Content-Type") or ""):
            return resp.status, None, [json.loads(l[5:].strip()) for l in raw.splitlines() if l.startswith("data:") and l[5:].strip()]
        return resp.status, None, ([json.loads(raw)] if raw.strip() else [])

    def close(self):
        if not self.sid:
            return
        try:
            urllib.request.urlopen(urllib.request.Request(self.url, method="DELETE", headers=self._headers()), timeout=15)
        except Exception:
            pass
        self.sid = None

    def handshake(self, init_params):
        status, err, replies = self.post({"jsonrpc": "2.0", "id": "bridge-init", "method": "initialize", "params": init_params})
        if err is not None or not replies:
            return status, err, None
        self.post({"jsonrpc": "2.0", "method": "notifications/initialized"})
        return status, None, replies[0]


def write(message):
    sys.stdout.write(json.dumps(message) + "\n")
    sys.stdout.flush()


def rpc_error(message, text):
    if "id" in message:
        write({"jsonrpc": "2.0", "id": message["id"], "error": {"code": -32000, "message": text[:300]}})


def main():
    if len(sys.argv) != 2 or not sys.argv[1].startswith(("http://", "https://")) or MODE not in ("per-request", "sticky"):
        sys.stderr.write("http-bridge: usage: http-bridge.py http(s)://host:port/path  (MCP_BRIDGE_SESSION=per-request|sticky)\n")
        return 2
    url, tok = sys.argv[1], token()
    up = Upstream(url, tok)
    init_params = {"protocolVersion": "2025-06-18", "capabilities": {}, "clientInfo": {"name": "http-bridge", "version": "1"}}

    if MODE == "sticky":
        signal.signal(signal.SIGTERM, lambda *_: (up.close(), sys.exit(0)))

    for line in sys.stdin:
        line = line.strip()
        if not line:
            continue
        try:
            message = json.loads(line)
        except ValueError:
            continue
        method, is_request = message.get("method"), "id" in message

        if MODE == "sticky":
            status, err, replies = up.post(message)
            if err is not None:
                rpc_error(message, f"HTTP {status}: {err}")
            for r in replies:
                write(r)
            continue

        # per-request mode: the client's initialize is answered from a real upstream handshake, which is then closed
        if method == "initialize":
            init_params = message.get("params") or init_params
            status, err, reply = up.handshake(init_params)
            up.close()
            if reply is None:
                rpc_error(message, f"HTTP {status}: {err}")
            else:
                reply["id"] = message["id"]
                write(reply)
            continue
        if not is_request:
            continue  # client notifications (initialized, cancelled) have no meaning without a long-lived session
        status, err, reply = up.handshake(init_params)
        if reply is None:
            rpc_error(message, f"HTTP {status}: {err}")
            up.close()
            continue
        try:
            status, err, replies = up.post(message)
            if err is not None:
                rpc_error(message, f"HTTP {status}: {err}")
            for r in replies:
                write(r)
        finally:
            up.close()

    up.close()
    return 0


if __name__ == "__main__":
    sys.exit(main())
