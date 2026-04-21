#!/usr/bin/env bash
# 11 — Persona memory live exercise.
# Program.cs resolves the persona (including ReadHistory) BEFORE constructing
# the Azure OpenAI client, so we can drive the memory code by providing a
# valid-looking HTTPS endpoint that will DNS-fail later. The history read
# happens before the failure — any crash/leak here is a finding.
source "$(dirname "$0")/_lib.sh"

LIVE_ENV=(env AZUREOPENAIENDPOINT='https://nothing.invalid.example/' AZUREOPENAIAPI=x)

# A) ReadHistory on a 100 MB file — System.IO.File.ReadAllText loads the entire
# file into memory BEFORE the 32 KB truncation check (see
# Squad/PersonaMemory.cs:36-39). Expected: transient ~100 MB alloc on every
# persona invocation.
run_big_read() {
  local d; d=$(mktemp -d -p "$WORK" liveAXXX)
  mkdir -p "$d/.squad/history"
  cat > "$d/.squad.json" <<'EOF'
{"team":{"name":"t","description":""},
 "personas":[{"name":"big","role":"r","description":"","system_prompt":"x","tools":[]}],
 "routing":[]}
EOF
  python3 -c "
import sys
chunk = b'line line line line line line line line line line line line line line\n'
with open(sys.argv[1], 'wb') as f:
    n = 0
    while n < 100*1024*1024:
        f.write(chunk); n += len(chunk)" "$d/.squad/history/big.md"
  # Time + peak RSS via /usr/bin/time.
  (cd "$d" && run_attack 11a "100MB ReadHistory peak RSS" -- \
    /usr/bin/time -v env AZUREOPENAIENDPOINT='https://nothing.invalid.example/' AZUREOPENAIAPI=x HOME="$d" \
      "$BIN" --persona big --timeout 2 "hi")
}
run_big_read

# B) History file symlinked to /dev/urandom — ReadHistory will stream unbounded.
# ReadAllText on /dev/urandom never returns EOF → hang. Expected: the drill
# timeout kicks in. A real user would see an indefinite wait.
run_urandom_read() {
  local d; d=$(mktemp -d -p "$WORK" liveBXXX)
  mkdir -p "$d/.squad/history"
  ln -sf /dev/urandom "$d/.squad/history/rogue.md"
  cat > "$d/.squad.json" <<'EOF'
{"team":{"name":"t","description":""},
 "personas":[{"name":"rogue","role":"r","description":"","system_prompt":"x","tools":[]}],
 "routing":[]}
EOF
  (cd "$d" && run_attack 11b "history symlink -> /dev/urandom (hang probe)" -- \
    env AZUREOPENAIENDPOINT='https://nothing.invalid.example/' AZUREOPENAIAPI=x HOME="$d" \
      "$BIN" --persona rogue --timeout 2 "hi")
}
run_urandom_read

# C) Persona-name path traversal on the WRITE side. AppendHistory is called
# AFTER a successful agent run, which we can't drive without creds. But we CAN
# prove GetHistoryPath composes a traversal path by exercising the read side
# (which uses the identical method). If ReadHistory returns the contents of a
# file we planted OUTSIDE .squad/history/, that is conclusive proof of traversal.
run_traversal_read() {
  local d; d=$(mktemp -d -p "$WORK" liveCXXX)
  mkdir -p "$d/.squad/history"
  # Plant canary two levels up from .squad/history/ — the path a persona named
  # '../../canary' would resolve to.
  echo "FDR-CANARY-PWNED $(date -u +%FT%TZ)" > "$d/canary.md"
  cat > "$d/.squad.json" <<'EOF'
{"team":{"name":"t","description":""},
 "personas":[{"name":"../../canary","role":"r","description":"","system_prompt":"x","tools":[]}],
 "routing":[]}
EOF
  # Read by --personas doesn't trigger ReadHistory. We need --persona X path.
  # With a valid-looking (but unreachable) HTTPS endpoint, persona resolution
  # runs before endpoint I/O. Any canary content appearing in stderr/stdout
  # confirms the traversal reads outside .squad/history/.
  (cd "$d" && run_attack 11c "persona '../../canary' reads outside history dir" -- \
    env AZUREOPENAIENDPOINT='https://nothing.invalid.example/' AZUREOPENAIAPI=x HOME="$d" \
      "$BIN" --persona '../../canary' --timeout 2 "probe")
  echo "--- post-run file tree for 11c ---"
  find "$d" -type f -o -type l | sort
}
run_traversal_read
