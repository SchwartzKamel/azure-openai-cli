#!/usr/bin/env bash
# 03 — env var chaos.
source "$(dirname "$0")/_lib.sh"

# Empty API key — must bail with a clean error.
run_attack 03a 'AZUREOPENAIAPI=""' -- \
  env AZUREOPENAIENDPOINT=https://example.invalid AZUREOPENAIAPI= "$BIN" "hi"

# NUL-prefixed API key. Shell can't embed NUL in env; pass as prefix that would
# otherwise corrupt logging. We simulate by using a control-char-prefixed key.
run_attack 03b 'AZUREOPENAIAPI control-char prefix' -- \
  env AZUREOPENAIENDPOINT=https://example.invalid AZUREOPENAIAPI=$'\x01padding' "$BIN" --estimate "hi"

# SSRF probe — IMDS hostname in endpoint. With --estimate we short-circuit before any network call.
run_attack 03c 'endpoint=IMDS (estimate short-circuits)' -- \
  env AZUREOPENAIENDPOINT='http://169.254.169.254/' AZUREOPENAIAPI=x "$BIN" --estimate "hi"

# SSRF probe — IMDS hostname, non-estimate path (should fail at auth/connection stage).
run_attack 03d 'endpoint=IMDS (real path, blocked by network)' -- \
  env AZUREOPENAIENDPOINT='http://169.254.169.254/' AZUREOPENAIAPI=x "$BIN" --timeout 2 "hi"

# Shell metachars in model name — must be treated as a literal.
run_attack 03e 'model name with metachars' -- \
  env AZUREOPENAIENDPOINT=https://example.invalid AZUREOPENAIAPI=x AZUREOPENAIMODEL='; rm -rf /' \
  "$BIN" --estimate "hi"

# Ralph depth via env (v2 no longer uses RALPH_DEPTH, but test it doesn't
# cause surprising behaviour).
run_attack 03f 'RALPH_DEPTH=999 (legacy env should be ignored in v2)' -- \
  env AZUREOPENAIENDPOINT=https://example.invalid AZUREOPENAIAPI=x RALPH_DEPTH=999 \
  "$BIN" --estimate "hi"

# AZ_TELEMETRY with control bytes.
run_attack 03g 'AZ_TELEMETRY=control-char' -- \
  env AZUREOPENAIENDPOINT=https://example.invalid AZUREOPENAIAPI=x AZ_TELEMETRY=$'\x01' \
  "$BIN" --estimate "hi"

# AZ_PREWARM=yes vs =1.
run_attack 03h 'AZ_PREWARM=yes' -- \
  env AZUREOPENAIENDPOINT=https://example.invalid AZUREOPENAIAPI=x AZ_PREWARM=yes \
  "$BIN" --estimate "hi"
run_attack 03i 'AZ_PREWARM=1' -- \
  env AZUREOPENAIENDPOINT=https://example.invalid AZUREOPENAIAPI=x AZ_PREWARM=1 \
  "$BIN" --estimate "hi"

# Malformed numeric env overrides.
run_attack 03j 'AZURE_MAX_TOKENS=not-a-number' -- \
  env AZUREOPENAIENDPOINT=https://example.invalid AZUREOPENAIAPI=x AZURE_MAX_TOKENS=NaN \
  "$BIN" --estimate "hi"
run_attack 03k 'AZURE_TEMPERATURE=9e99' -- \
  env AZUREOPENAIENDPOINT=https://example.invalid AZUREOPENAIAPI=x AZURE_TEMPERATURE=9e99 \
  "$BIN" --estimate "hi"
