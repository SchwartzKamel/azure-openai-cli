#!/usr/bin/env python3
"""FDR mock Azure OpenAI server — scripted misbehavior.

Usage:
    ./mock_server.py --mode <mode> --port <port>

Modes:
    slowloris        accept but never respond
    gibberish        200 OK with non-JSON body
    big_blob         10 MB JSON response
    midstream_close  start SSE, close socket mid-stream
    no_content_len   HTTP/1.0 response without Content-Length
    tool_call_loop   emit N tool_call deltas to exercise round limits
    tool_huge_args   single tool_call with 10 MB argument string
    tool_bad_path    single read_file tool_call with /etc/shadow
    tool_bad_url     single web_fetch tool_call with IMDS URL
    tool_bad_cmd     single shell_exec tool_call with rm -rf /tmp/foo
    tool_bad_json    tool_call where arguments isn't valid JSON
    tool_unknown     tool_call referencing a tool the CLI did not register
"""
import argparse, json, socket, sys, threading, time

def send_sse(conn, events):
    conn.sendall(b"HTTP/1.1 200 OK\r\nContent-Type: text/event-stream\r\n"
                 b"Cache-Control: no-cache\r\nConnection: close\r\n\r\n")
    for e in events:
        conn.sendall(b"data: " + json.dumps(e).encode() + b"\n\n")
        time.sleep(0.01)
    conn.sendall(b"data: [DONE]\n\n")

def tool_call_chunk(name, args, idx=0, call_id="call_fdr_1"):
    return {
        "id":"cmpl-x","object":"chat.completion.chunk","model":"gpt-4o-mini",
        "choices":[{"index":0,"delta":{"tool_calls":[{
            "index":idx,"id":call_id,"type":"function",
            "function":{"name":name,"arguments":args}}]},"finish_reason":None}]
    }

def handler(conn, mode):
    # Drain request
    buf = b""
    conn.settimeout(2.0)
    try:
        while b"\r\n\r\n" not in buf:
            chunk = conn.recv(4096)
            if not chunk: break
            buf += chunk
    except Exception: pass

    if mode == "slowloris":
        # Accept, drip one byte every 10s forever (inside chaos, drill times out).
        try:
            conn.sendall(b"HTTP/1.1 200 OK\r\nContent-Type: text/event-stream\r\n\r\n")
            while True:
                conn.sendall(b"d"); time.sleep(10)
        except Exception: pass
    elif mode == "gibberish":
        body = b"\x00\xff\xfeNOT JSON AT ALL \x1b[31m"
        conn.sendall(b"HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: "
                     + str(len(body)).encode() + b"\r\n\r\n" + body)
    elif mode == "big_blob":
        body = b'{"junk":"' + b"A"*(10*1024*1024) + b'"}'
        conn.sendall(b"HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: "
                     + str(len(body)).encode() + b"\r\n\r\n" + body)
    elif mode == "midstream_close":
        conn.sendall(b"HTTP/1.1 200 OK\r\nContent-Type: text/event-stream\r\n\r\n")
        conn.sendall(b"data: {\"choices\":[{\"delta\":{\"content\":\"partial\"}}]}\n\n")
        conn.close()
    elif mode == "no_content_len":
        conn.sendall(b"HTTP/1.0 200 OK\r\nContent-Type: application/json\r\n\r\n"
                     b'{"choices":[{"message":{"content":"hi"},"finish_reason":"stop"}]}')
        conn.close()
    elif mode == "tool_call_loop":
        events = []
        for i in range(100):
            events.append(tool_call_chunk("get_datetime","{}", idx=i, call_id=f"c{i}"))
            events.append({"id":"x","object":"chat.completion.chunk","model":"m",
                           "choices":[{"index":0,"delta":{},"finish_reason":"tool_calls"}]})
        send_sse(conn, events)
    elif mode == "tool_huge_args":
        big = "A"*(10*1024*1024)
        send_sse(conn, [tool_call_chunk("read_file", json.dumps({"path": big}))])
    elif mode == "tool_bad_path":
        send_sse(conn, [tool_call_chunk("read_file", json.dumps({"path":"/etc/shadow"})),
                        tool_call_chunk("read_file", json.dumps({"path":"../../../etc/passwd"}), idx=1, call_id="c2")])
    elif mode == "tool_bad_url":
        send_sse(conn, [tool_call_chunk("web_fetch", json.dumps({"url":"http://169.254.169.254/"}))])
    elif mode == "tool_bad_cmd":
        send_sse(conn, [tool_call_chunk("shell_exec", json.dumps({"command":"rm -rf /tmp/foo"}))])
    elif mode == "tool_bad_json":
        send_sse(conn, [tool_call_chunk("read_file", "{not valid json")])
    elif mode == "tool_unknown":
        send_sse(conn, [tool_call_chunk("nuke_production", "{}")])
    else:
        conn.sendall(b"HTTP/1.1 404 Not Found\r\n\r\n")
    try: conn.close()
    except: pass

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--mode", required=True)
    ap.add_argument("--port", type=int, default=0)
    ap.add_argument("--once", action="store_true")
    args = ap.parse_args()

    s = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    s.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
    s.bind(("127.0.0.1", args.port))
    s.listen(16)
    addr = s.getsockname()
    print(f"MOCK_PORT={addr[1]}", flush=True)
    while True:
        conn, _ = s.accept()
        t = threading.Thread(target=handler, args=(conn, args.mode), daemon=True)
        t.start()
        if args.once:
            t.join(timeout=25)
            break

if __name__ == "__main__":
    main()
