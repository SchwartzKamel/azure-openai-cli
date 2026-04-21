#!/usr/bin/env bash
# 04 — .azureopenai-cli.json config chaos.
# Each sub-test runs in an isolated tmp cwd with a HOME that cannot be read.
source "$(dirname "$0")/_lib.sh"

STUB_ENV=(env AZUREOPENAIENDPOINT=https://example.invalid AZUREOPENAIAPI=x)

make_sandbox() {
  local d
  d=$(mktemp -d -p "$WORK" cfgXXXX)
  echo "$d"
}

# 50 MB config file.
run_config_50mb() {
  local d; d=$(make_sandbox)
  # Balanced JSON but huge: pad a long key.
  python3 -c "
import sys
sys.stdout.write('{\"models\":{\"fast\":{\"deployment\":\"gpt-4o-mini\"},\"_pad\":\"')
sys.stdout.write('A' * (50*1024*1024))
sys.stdout.write('\"}}')" > "$d/.azureopenai-cli.json"
  (cd "$d" && run_attack 04a "50MB .azureopenai-cli.json" -- \
    "${STUB_ENV[@]}" HOME="$d" "$BIN" --estimate "hi")
}
run_config_50mb

# Null models.
run_config_null() {
  local d; d=$(make_sandbox)
  echo '{"models": null}' > "$d/.azureopenai-cli.json"
  (cd "$d" && run_attack 04b '{"models":null}' -- \
    "${STUB_ENV[@]}" HOME="$d" "$BIN" --estimate "hi")
}
run_config_null

# Deeply nested / self-referential-ish object (JSON can't self-reference but we
# exercise deep nesting).
run_config_deep() {
  local d; d=$(make_sandbox)
  python3 -c "
depth = 2000
print('{' + '\"x\":{' * depth + '\"v\":1' + '}' * depth + '}')" > "$d/.azureopenai-cli.json"
  (cd "$d" && run_attack 04c "2000-deep nested JSON" -- \
    "${STUB_ENV[@]}" HOME="$d" "$BIN" --estimate "hi")
}
run_config_deep

# Symlink to /etc/passwd — must not be read as config.
run_config_symlink_passwd() {
  local d; d=$(make_sandbox)
  ln -s /etc/passwd "$d/.azureopenai-cli.json"
  (cd "$d" && run_attack 04d "config symlink -> /etc/passwd" -- \
    "${STUB_ENV[@]}" HOME="$d" "$BIN" --estimate "hi")
}
run_config_symlink_passwd

# Symlink to self — must not infinite-loop.
run_config_symlink_self() {
  local d; d=$(make_sandbox)
  ln -s "$d/.azureopenai-cli.json" "$d/.azureopenai-cli.json" 2>/dev/null || true
  (cd "$d" && run_attack 04e "config symlink -> self" -- \
    "${STUB_ENV[@]}" HOME="$d" "$BIN" --estimate "hi")
}
run_config_symlink_self

# World-writable config file (mode 0666). Should we warn? (info only).
run_config_worldwritable() {
  local d; d=$(make_sandbox)
  echo '{"defaults":{"temperature":0.3}}' > "$d/.azureopenai-cli.json"
  chmod 0666 "$d/.azureopenai-cli.json"
  (cd "$d" && run_attack 04f "world-writable config (mode 0666)" -- \
    "${STUB_ENV[@]}" HOME="$d" "$BIN" --config show)
}
run_config_worldwritable

# BOM in config.
run_config_bom() {
  local d; d=$(make_sandbox)
  printf '\xef\xbb\xbf{"defaults":{"temperature":0.2}}' > "$d/.azureopenai-cli.json"
  (cd "$d" && run_attack 04g "UTF-8 BOM config" -- \
    "${STUB_ENV[@]}" HOME="$d" "$BIN" --config show)
}
run_config_bom

# UTF-16 LE config.
run_config_utf16() {
  local d; d=$(make_sandbox)
  python3 -c "
import io, sys
data = '{\"defaults\":{\"temperature\":0.2}}'
sys.stdout.buffer.write('\ufeff'.encode('utf-16-le') + data.encode('utf-16-le'))" > "$d/.azureopenai-cli.json"
  (cd "$d" && run_attack 04h "UTF-16 LE config" -- \
    "${STUB_ENV[@]}" HOME="$d" "$BIN" --config show)
}
run_config_utf16

# --config <path> pointing at /etc/passwd.
run_config_path_etcpasswd() {
  run_attack 04i "--config /etc/passwd" -- \
    "${STUB_ENV[@]}" "$BIN" --config show --config /etc/passwd
}
run_config_path_etcpasswd
