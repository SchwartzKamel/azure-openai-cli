#!/usr/bin/env bash
# 02 — stdin evil inputs. Parser must not crash; size caps should engage.
source "$(dirname "$0")/_lib.sh"

STUB_ENV=(env AZUREOPENAIENDPOINT=https://example.invalid AZUREOPENAIAPI=x)

# 10 MB prompt on stdin. --estimate keeps it local (no API call).
run_attack 02a "10MB stdin prompt (--estimate)" -- \
  bash -c 'head -c 10485760 /dev/urandom | base64 | head -c 10485760 | "$@" --estimate' _ "${STUB_ENV[@]}" "$BIN"

# Partial stdin — writer closes pipe mid-stream.
run_attack 02b "pipe closed mid-stream" -- \
  bash -c '{ printf "hello "; kill -PIPE $$; } | "$@" --estimate' _ "${STUB_ENV[@]}" "$BIN"

# CRLF-only stdin.
run_attack 02c "CRLF-only stdin" -- \
  bash -c 'printf "\r\n\r\n\r\n" | "$@" --estimate' _ "${STUB_ENV[@]}" "$BIN"

# BOM-only stdin.
run_attack 02d "UTF-8 BOM only stdin" -- \
  bash -c 'printf "\xef\xbb\xbf" | "$@" --estimate' _ "${STUB_ENV[@]}" "$BIN"

# ANSI escape bomb stdin.
run_attack 02e "ANSI escape bomb stdin" -- \
  bash -c 'printf "\x1b[2J\x1b[H\x1b[31mPWN\x1b[0m" | "$@" --estimate' _ "${STUB_ENV[@]}" "$BIN"

# 1M zero-width spaces.
run_attack 02f "1M zero-width spaces stdin" -- \
  bash -c 'python3 -c "import sys; sys.stdout.buffer.write(\"\u200b\".encode(\"utf-8\")*1_000_000)" | "$@" --estimate' _ "${STUB_ENV[@]}" "$BIN"

# Bidi override characters in prompt.
run_attack 02g "bidi-override prompt" -- \
  bash -c 'python3 -c "import sys; sys.stdout.buffer.write((\"\u202eevil\u202c\" * 64).encode())" | "$@" --estimate' _ "${STUB_ENV[@]}" "$BIN"
