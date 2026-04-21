#!/usr/bin/env bash
# 01 — argv injection: path traversal in flag values, shell-metachar values,
# NUL bytes, invalid UTF-8 in prompt. Parser must reject or safely pass through.
source "$(dirname "$0")/_lib.sh"

# Persona that looks like a path-traversal payload — we expect "Unknown persona" (rc=1).
run_attack 01a "persona=../../etc/passwd (no squad config)" -- \
  env -u AZUREOPENAIENDPOINT -u AZUREOPENAIAPI "$BIN" --persona=../../etc/passwd "hi"

# With creds+endpoint stubbed so we reach persona resolution:
run_attack 01b "persona=../../etc/passwd (creds set)" -- \
  env AZUREOPENAIENDPOINT=https://example.invalid AZUREOPENAIAPI=x \
  "$BIN" --persona=../../etc/passwd "hi"

# Model name carrying shell metacharacters — must be treated as a literal string.
run_attack 01c 'model="$(whoami)"' -- \
  env AZUREOPENAIENDPOINT=https://example.invalid AZUREOPENAIAPI=x \
  "$BIN" --model '$(whoami)' --estimate "hi"

# NUL byte inside a flag value. argv can carry it in exec() but Bash does not
# forward NULs past the first \0 in an argv slot; use printf+xargs-0 so the
# child sees a real embedded NUL in its argv.
# (Skipped with an explanatory note — POSIX argv cannot carry embedded NULs.)
run_attack 01d "NUL byte in --persona (POSIX-skipped)" -- \
  env AZUREOPENAIENDPOINT=https://example.invalid AZUREOPENAIAPI=x \
  "$BIN" --persona $'a\x00b' "hi"

# Invalid UTF-8 bytes in prompt.
run_attack 01e "invalid UTF-8 in prompt" -- \
  bash -c 'env AZUREOPENAIENDPOINT=https://example.invalid AZUREOPENAIAPI=x "$0" --estimate "$(printf '\''\xff\xfe\x80bad\xc3\x28'\'')"' "$BIN"

# Integer overflow candidates in numeric flags.
run_attack 01f "--max-tokens=1e999" -- \
  env AZUREOPENAIENDPOINT=https://example.invalid AZUREOPENAIAPI=x \
  "$BIN" --max-tokens 1e999 --estimate "hi"

run_attack 01g "--max-tokens=-1" -- \
  env AZUREOPENAIENDPOINT=https://example.invalid AZUREOPENAIAPI=x \
  "$BIN" --max-tokens -1 --estimate "hi"

run_attack 01h "--max-rounds=99999" -- \
  env AZUREOPENAIENDPOINT=https://example.invalid AZUREOPENAIAPI=x \
  "$BIN" --max-rounds 99999 --agent "hi"

run_attack 01i "--max-iterations=-5" -- \
  env AZUREOPENAIENDPOINT=https://example.invalid AZUREOPENAIAPI=x \
  "$BIN" --ralph --max-iterations -5 "hi"

run_attack 01j "unknown flag --pwn" -- \
  env AZUREOPENAIENDPOINT=https://example.invalid AZUREOPENAIAPI=x \
  "$BIN" --pwn "hi"

# Extremely long argv (1 MB flag value).
run_attack 01k "1MB --system prompt" -- \
  bash -c 'big=$(head -c 1048576 </dev/urandom | base64 | head -c 1048576); env AZUREOPENAIENDPOINT=https://example.invalid AZUREOPENAIAPI=x "$0" --system "$big" --estimate "hi"' "$BIN"
