#!/usr/bin/env bash
# lint-espanso-yml.sh — structural lint for the Espanso/PowerShell/WSL config.
#
# Catches the bug class that broke ai-windows-to-wsl.yml in S02E37 and S03E01:
# trigger renames that left $trigger out of sync, drifted BACKSPACE counts,
# missing try/finally retype, or placeholders containing SendKeys metachars.
#
# Usage: bash scripts/lint-espanso-yml.sh [path-to-yml]
# Default path: examples/espanso-ahk-wsl/espanso/ai-windows-to-wsl.yml

set -u

DEFAULT_PATH="examples/espanso-ahk-wsl/espanso/ai-windows-to-wsl.yml"
TARGET="${1:-$DEFAULT_PATH}"

python3 - "$TARGET" <<'PY'
import re
import sys

try:
    import yaml
except ImportError:
    print("[espanso-yml-lint] error: PyYAML not available", file=sys.stderr)
    sys.exit(1)

PREFIX = "[espanso-yml-lint]"
path = sys.argv[1]
failures = []

def fail(severity, where, msg):
    failures.append(f"{PREFIX} {severity}: {where}: {msg}")

# 1. Parse gate.
try:
    with open(path, "r", encoding="utf-8") as f:
        doc = yaml.safe_load(f)
except yaml.YAMLError as e:
    mark = getattr(e, "problem_mark", None)
    line = (mark.line + 1) if mark else "?"
    print(f"{PREFIX} error: {path}: YAML parse error at line {line}: {e}")
    sys.exit(1)
except OSError as e:
    print(f"{PREFIX} error: {path}: cannot open: {e}")
    sys.exit(1)

if not isinstance(doc, dict) or "matches" not in doc or not isinstance(doc["matches"], list):
    print(f"{PREFIX} error: {path}: top-level 'matches:' list missing")
    sys.exit(1)

matches = doc["matches"]

# 2. Unique triggers.
triggers = []
for i, m in enumerate(matches):
    if not isinstance(m, dict):
        fail("error", f"matches[{i}]", "match block is not a mapping")
        continue
    if "trigger" not in m:
        fail("error", f"matches[{i}]", "missing 'trigger:' key")
        continue
    triggers.append(m["trigger"])

seen = {}
for t in triggers:
    seen[t] = seen.get(t, 0) + 1
dups = sorted([t for t, c in seen.items() if c > 1])
for d in dups:
    fail("error", repr(d), f"duplicate trigger appears {seen[d]} times")

# 3. Per-trigger structural assertions.
SAFE_PH_RE = re.compile(r"^[A-Za-z0-9 .,:;!?\-]+$")
TRIGGER_ASSIGN_RE = re.compile(r"^\s*\$trigger\s*=\s*'([^']*)'\s*$", re.MULTILINE)
PH_ASSIGN_RE      = re.compile(r"^\s*\$ph\s*=\s*'([^']*)'\s*$",      re.MULTILINE)
BACKSPACE_RE      = re.compile(r"\{BACKSPACE\s*'\s*\+\s*\$(trigger|ph)\.Length\s*\+\s*'\}")
SENDWAIT_TRIG_RE  = re.compile(r"SendWait\(\s*\$trigger\s*\)")

def find_cmd(match):
    """Return the cmd string from a shell-type var, or None."""
    for var in match.get("vars", []) or []:
        if not isinstance(var, dict):
            continue
        if var.get("type") != "shell":
            continue
        params = var.get("params") or {}
        cmd = params.get("cmd")
        if isinstance(cmd, str):
            return cmd
    return None

for m in matches:
    if not isinstance(m, dict) or "trigger" not in m:
        continue
    trigger = m["trigger"]
    where = repr(trigger)
    cmd = find_cmd(m)
    if cmd is None:
        # Non-shell trigger: skip structural checks.
        continue

    # $trigger assignment.
    trig_matches = TRIGGER_ASSIGN_RE.findall(cmd)
    if len(trig_matches) != 1:
        fail("error", where, f"expected exactly one $trigger assignment, found {len(trig_matches)}")
    else:
        if trig_matches[0] != trigger:
            fail("error", where,
                 f"$trigger value {trig_matches[0]!r} does not match outer trigger {trigger!r} "
                 "(rename drift — a trigger was changed without updating $trigger)")

    # $ph assignment.
    ph_matches = PH_ASSIGN_RE.findall(cmd)
    if len(ph_matches) != 1:
        fail("error", where, f"expected exactly one $ph assignment, found {len(ph_matches)}")
    else:
        ph_val = ph_matches[0]
        if not SAFE_PH_RE.match(ph_val):
            fail("error", where,
                 f"$ph value {ph_val!r} contains characters unsafe for SendKeys "
                 r"(metachars + ^ % ~ ( ) [ ] { } would be misinterpreted)")

    # Two BACKSPACE substitutions, in order: trigger.Length then ph.Length.
    bs = BACKSPACE_RE.findall(cmd)
    if len(bs) != 2:
        fail("error", where, f"expected exactly two BACKSPACE substitutions, found {len(bs)}")
    else:
        if bs[0] != "trigger":
            fail("error", where, f"first BACKSPACE must reference $trigger.Length, got $${bs[0]}.Length")
        if bs[1] != "ph":
            fail("error", where, f"second BACKSPACE must reference $ph.Length, got $${bs[1]}.Length")

    # try / finally / retype.
    if "try {" not in cmd:
        fail("error", where, "missing 'try {' block")
    if "} finally {" not in cmd:
        fail("error", where, "missing '} finally {' block")
    # SendWait($trigger) must appear AFTER the second BACKSPACE substitution.
    bs_iter = list(BACKSPACE_RE.finditer(cmd))
    if len(bs_iter) >= 2:
        second_bs_end = bs_iter[1].end()
        retype = SENDWAIT_TRIG_RE.search(cmd, second_bs_end)
        if retype is None:
            fail("error", where,
                 "missing SendWait($trigger) retype after second BACKSPACE in finally block")

if failures:
    for line in failures:
        print(line)
    print(f"{PREFIX} summary: {len(failures)} failure(s) in {path}")
    sys.exit(1)

print(f"{PREFIX} ok: {path} ({len(triggers)} triggers checked)")
sys.exit(0)
PY
