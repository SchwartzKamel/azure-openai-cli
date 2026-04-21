#!/usr/bin/env bash
# 05 — .squad.json / persona chaos.
source "$(dirname "$0")/_lib.sh"

STUB_ENV=(env AZUREOPENAIENDPOINT=https://example.invalid AZUREOPENAIAPI=x)

mk() { mktemp -d -p "$WORK" sqdXXXX; }

# Persona name with ../ — does GetHistoryPath path-traverse when ReadHistory is called?
run_persona_traversal() {
  local d; d=$(mk)
  # The persona lookup is name-based (case-insensitive equality) so the name
  # must match exactly as declared. If we declare a persona literally named
  # "../../pwned", the loader accepts it and PersonaMemory will Path.Combine it.
  cat > "$d/.squad.json" <<'EOF'
{
  "team": {"name": "t", "description": ""},
  "personas": [
    {"name": "../../pwned", "role": "r", "description": "",
     "system_prompt": "you are pwned", "tools": []}
  ],
  "routing": []
}
EOF
  # Also ensure .squad/ exists so Initialize isn't needed.
  mkdir -p "$d/.squad/history"
  # Drop a canary file that a traversal write would land near.
  (cd "$d" && run_attack 05a "persona name with ../ (flag)" -- \
    "${STUB_ENV[@]}" HOME="$d" "$BIN" --persona '../../pwned' --estimate "hi")
  (cd "$d" && run_attack 05a2 "list personas shows ../ name" -- \
    "${STUB_ENV[@]}" HOME="$d" "$BIN" --personas)
  # Did any file get written outside .squad/history/ ?
  echo "--- traversal footprint for 05a ---"
  ls -la "$d/.." 2>/dev/null | head -20
}
run_persona_traversal

# Persona name with 10 000 chars.
run_persona_big_name() {
  local d; d=$(mk)
  python3 -c "
import json, sys
name = 'a' * 10000
cfg = {'team':{'name':'t','description':''},
       'personas':[{'name':name,'role':'','description':'','system_prompt':'x','tools':[]}],
       'routing':[]}
print(json.dumps(cfg))" > "$d/.squad.json"
  (cd "$d" && run_attack 05b "10K-char persona name" -- \
    "${STUB_ENV[@]}" HOME="$d" "$BIN" --personas)
}
run_persona_big_name

# 10 000 entries in tools list.
run_persona_huge_tools() {
  local d; d=$(mk)
  python3 -c "
import json, sys
cfg = {'team':{'name':'t','description':''},
       'personas':[{'name':'p','role':'','description':'','system_prompt':'x',
                    'tools':['read_file']*10000}],
       'routing':[]}
print(json.dumps(cfg))" > "$d/.squad.json"
  (cd "$d" && run_attack 05c "10K entries in tools list" -- \
    "${STUB_ENV[@]}" HOME="$d" "$BIN" --personas)
}
run_persona_huge_tools

# Routing pattern that would be a catastrophic regex in a regex engine.
# (v2 SquadCoordinator uses comma-split keywords, NOT regex — documenting that.)
run_persona_redos() {
  local d; d=$(mk)
  cat > "$d/.squad.json" <<'EOF'
{
  "team":{"name":"t","description":""},
  "personas":[{"name":"p","role":"","description":"","system_prompt":"x","tools":[]}],
  "routing":[{"pattern":"(a+)+$", "persona":"p", "description":""}]
}
EOF
  # Feed it a matcher-friendly prompt.
  (cd "$d" && run_attack 05d "ReDoS-shaped routing pattern" -- \
    "${STUB_ENV[@]}" HOME="$d" "$BIN" --persona auto --estimate "$(python3 -c 'print("a"*50000)')")
}
run_persona_redos

# History file symlink to /dev/urandom — ReadHistory would stream garbage.
run_persona_urandom_link() {
  local d; d=$(mk)
  mkdir -p "$d/.squad/history"
  ln -sf /dev/urandom "$d/.squad/history/rogue.md"
  cat > "$d/.squad.json" <<'EOF'
{"team":{"name":"t","description":""},
 "personas":[{"name":"rogue","role":"r","description":"d","system_prompt":"x","tools":[]}],
 "routing":[]}
EOF
  (cd "$d" && run_attack 05e "history file symlink -> /dev/urandom" -- \
    "${STUB_ENV[@]}" HOME="$d" "$BIN" --persona rogue --estimate "hi")
}
run_persona_urandom_link
