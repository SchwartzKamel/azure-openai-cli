#!/usr/bin/env bash
# 09 — network chaos. Point AZUREOPENAIENDPOINT at local misbehaving servers.
# We do NOT probe any real Azure resource. All endpoints resolve to 127.0.0.1.
source "$(dirname "$0")/_lib.sh"

launch_mock() {
  local mode="$1"
  python3 "$(dirname "$0")/mock_server.py" --mode "$mode" --once >"$WORK/${mode}.port" 2>"$WORK/${mode}.srv.err" &
  local pid=$!
  # Wait for port to be reported.
  for _ in $(seq 1 40); do
    if grep -q '^MOCK_PORT=' "$WORK/${mode}.port"; then break; fi
    sleep 0.05
  done
  local port; port=$(awk -F= '/MOCK_PORT/ {print $2}' "$WORK/${mode}.port")
  echo "$pid $port"
}

# A) Connection refused — nothing listens on port 1.
run_attack 09a "connection refused (127.0.0.1:1)" -- \
  env AZUREOPENAIENDPOINT='http://127.0.0.1:1/' AZUREOPENAIAPI=x \
  "$BIN" --timeout 3 "hi"

# B) Slowloris (accepts, never responds) — CLI must honor --timeout.
slowinfo=($(launch_mock slowloris)); slowpid=${slowinfo[0]}; slowport=${slowinfo[1]}
run_attack 09b "slowloris — never-responding server (--timeout 3s)" -- \
  env AZUREOPENAIENDPOINT="http://127.0.0.1:${slowport}/" AZUREOPENAIAPI=x \
  "$BIN" --timeout 3 "hi"
kill "$slowpid" 2>/dev/null; wait "$slowpid" 2>/dev/null

# C) 200 OK with gibberish body — must not crash the JSON parser.
gbinfo=($(launch_mock gibberish)); gbpid=${gbinfo[0]}; gbport=${gbinfo[1]}
run_attack 09c "200 OK gibberish body" -- \
  env AZUREOPENAIENDPOINT="http://127.0.0.1:${gbport}/" AZUREOPENAIAPI=x \
  "$BIN" --timeout 5 "hi"
kill "$gbpid" 2>/dev/null; wait "$gbpid" 2>/dev/null

# D) 10 MB JSON blob.
bginfo=($(launch_mock big_blob)); bgpid=${bginfo[0]}; bgport=${bginfo[1]}
run_attack 09d "10MB JSON response body" -- \
  env AZUREOPENAIENDPOINT="http://127.0.0.1:${bgport}/" AZUREOPENAIAPI=x \
  "$BIN" --timeout 10 "hi"
kill "$bgpid" 2>/dev/null; wait "$bgpid" 2>/dev/null

# E) Mid-stream socket close.
msinfo=($(launch_mock midstream_close)); mspid=${msinfo[0]}; msport=${msinfo[1]}
run_attack 09e "SSE socket closed mid-stream" -- \
  env AZUREOPENAIENDPOINT="http://127.0.0.1:${msport}/" AZUREOPENAIAPI=x \
  "$BIN" --timeout 5 "hi"
kill "$mspid" 2>/dev/null; wait "$mspid" 2>/dev/null

# F) HTTP/1.0 no Content-Length.
nlinfo=($(launch_mock no_content_len)); nlpid=${nlinfo[0]}; nlport=${nlinfo[1]}
run_attack 09f "HTTP/1.0 without Content-Length" -- \
  env AZUREOPENAIENDPOINT="http://127.0.0.1:${nlport}/" AZUREOPENAIAPI=x \
  "$BIN" --timeout 5 "hi"
kill "$nlpid" 2>/dev/null; wait "$nlpid" 2>/dev/null
