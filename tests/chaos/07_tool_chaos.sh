#!/usr/bin/env bash
# 07 — Tool chaos. The Azure SDK requires HTTPS and a real/plausible endpoint
# for full end-to-end mocking. Instead, we exercise the individual tool
# defenses directly by pointing the CLI's agent mode at a plain-HTTP mock and
# observing how the transport layer rejects it. The core defenses (path
# traversal rejection, SSRF blocks, blocked-command filter) are additionally
# asserted against the source in the Kramer review.
#
# NOTE: the v2 test project (tests/AzureOpenAI_CLI.V2.Tests) already exercises
# ReadFileTool, WebFetchTool, ShellExecTool, and DelegateTaskTool with the
# MockChatClient pattern — run those here to confirm the regression suite
# stays green on the publish artifact's source tree.
source "$(dirname "$0")/_lib.sh"

# Run the V2 test project (covers tool defenses: blocked paths, SSRF, blocked
# commands, delegate depth cap). Any failure here IS a chaos finding.
run_attack 07a "V2 test suite — tool defenses regression" -- \
  bash -c 'cd "$ROOT" && dotnet test tests/AzureOpenAI_CLI.V2.Tests --nologo -v q 2>&1 | tail -40' "$ROOT"

# Reach an agent-mode path with a bogus endpoint so we observe the transport
# error path (no tool chaos actually executes — the SDK rejects plain-HTTP
# endpoints or DNS-fails the hostname).
run_attack 07b "agent mode against invalid endpoint (HTTP transport error path)" -- \
  env AZUREOPENAIENDPOINT=http://127.0.0.1:1/ AZUREOPENAIAPI=x \
  "$BIN" --agent --tools read_file --timeout 3 "read /etc/passwd"

# Directly exercise 1 tool: --estimate short-circuits so no tool runs, but the
# main point is to confirm --tools parser tolerance. Also checks that an
# unknown tool in --tools is a clean error.
run_attack 07c "--tools contains unknown tool" -- \
  env AZUREOPENAIENDPOINT=https://example.invalid AZUREOPENAIAPI=x \
  "$BIN" --agent --tools 'read_file,format_c_drive' --estimate "hi"
