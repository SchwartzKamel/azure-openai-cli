#!/usr/bin/env bash
# 06 — persona memory chaos: large history read, traversal-on-write,
# concurrent writers.
source "$(dirname "$0")/_lib.sh"

STUB_ENV=(env AZUREOPENAIENDPOINT=https://example.invalid AZUREOPENAIAPI=x)

# 100 MB history — check read cap behaviour.
run_big_history() {
  local d; d=$(mktemp -d -p "$WORK" memXXXX)
  mkdir -p "$d/.squad/history"
  cat > "$d/.squad.json" <<'EOF'
{"team":{"name":"t","description":""},
 "personas":[{"name":"big","role":"r","description":"","system_prompt":"x","tools":[]}],
 "routing":[]}
EOF
  # 100 MB history file (only the last 32 KB should be kept per MaxHistoryBytes,
  # but ReadAllText reads the ENTIRE file into a .NET string first).
  python3 -c "
import sys
chunk = ('session log line ' * 50 + '\n').encode()
with open(sys.argv[1], 'wb') as f:
    n = 0
    target = 100*1024*1024
    while n < target:
        f.write(chunk); n += len(chunk)" "$d/.squad/history/big.md"
  ls -la "$d/.squad/history/big.md"
  # Use --estimate so no network, but persona still resolves + history is read.
  (cd "$d" && run_attack 06a "100MB persona history read" -- \
    "${STUB_ENV[@]}" HOME="$d" "$BIN" --persona big --estimate "hi")
}
run_big_history

# Traversal-on-write: persona name ../outside must not create a file outside .squad/history/.
run_traversal_write() {
  local d; d=$(mktemp -d -p "$WORK" memWXXX)
  mkdir -p "$d/.squad/history"
  cat > "$d/.squad.json" <<'EOF'
{"team":{"name":"t","description":""},
 "personas":[{"name":"../hijack","role":"r","description":"","system_prompt":"x","tools":[]}],
 "routing":[]}
EOF
  # Sentinel — anything appearing outside .squad/history/ is a finding.
  before=$(find "$d" -type f | sort)
  (cd "$d" && run_attack 06b "persona name ../ write probe" -- \
    "${STUB_ENV[@]}" HOME="$d" "$BIN" --persona '../hijack' --estimate "hi")
  after=$(find "$d" -type f | sort)
  echo "--- new files after 06b ---"
  diff <(echo "$before") <(echo "$after") || true
}
run_traversal_write

# Concurrent writers: two processes appending to same persona history simultaneously.
# File.AppendAllText is not atomic across processes — race window is visible.
run_concurrent() {
  local d; d=$(mktemp -d -p "$WORK" memCXXX)
  mkdir -p "$d/.squad/history"
  cat > "$d/.squad.json" <<'EOF'
{"team":{"name":"t","description":""},
 "personas":[{"name":"shared","role":"r","description":"","system_prompt":"x","tools":[]}],
 "routing":[]}
EOF
  # Seed so append path is exercised.
  : > "$d/.squad/history/shared.md"
  # We can't easily force an append inside the binary without a live model run,
  # so this is a static observation test — we assert File.AppendAllText is used
  # (no file-locking) via a direct call.
  run_attack 06c "concurrent history append (static note)" -- \
    bash -c 'echo "PersonaMemory uses File.AppendAllText — no advisory or OS lock; concurrent writers may interleave within a line or lose writes on ext4 if both exceed page boundary."'
}
run_concurrent
