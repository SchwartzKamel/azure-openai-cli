#!/usr/bin/env bash
# 10 — Ralph/delegate depth fork-bomb probe.
# Cannot drive the live LLM loop in a chaos drill, so this is a static
# assertion: the V2 test project already includes DelegateTaskTool tests that
# exercise the AsyncLocal depth counter at MaxDepth=3. If those pass, the
# depth guard is in force. If they fail, this is a 🔴 finding.
source "$(dirname "$0")/_lib.sh"

run_attack 10a "delegate depth cap via test suite" -- \
  bash -c 'cd "$ROOT" && dotnet test tests/AzureOpenAI_CLI.V2.Tests --nologo -v q --filter "FullyQualifiedName~Delegate" 2>&1 | tail -30' "$ROOT"

# Probe that RALPH_DEPTH env var is NOT honored (v2 moved to AsyncLocal; env
# override would reintroduce the bypass vector).
run_attack 10b "RALPH_DEPTH env must not affect v2" -- \
  env AZUREOPENAIENDPOINT=https://example.invalid AZUREOPENAIAPI=x RALPH_DEPTH=99 \
  "$BIN" --estimate "hi"

# Also verify --max-iterations cap (1..50) is enforced at parse time.
run_attack 10c "--max-iterations=51 rejected at parse" -- \
  env AZUREOPENAIENDPOINT=https://example.invalid AZUREOPENAIAPI=x \
  "$BIN" --ralph --max-iterations 51 "hi"
