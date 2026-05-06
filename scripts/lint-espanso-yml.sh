#!/usr/bin/env bash
# lint-espanso-yml.sh -- structural lint for the Espanso/PowerShell/WSL config.
#
# Catches the bug class that broke ai-windows-to-wsl.yml in S02E37 and S03E01:
# trigger renames that left $trigger out of sync, drifted BACKSPACE counts,
# missing try/finally retype, or placeholders containing SendKeys metachars.
# Also catches the bash-stdin/heredoc regression class from S03E04 / v2.1
# audit (F-1, F-2) and -- as of 2026-05 -- cross-file trigger collisions
# between the prompt-templates kit and any platform-variant kit.
#
# Usage: bash scripts/lint-espanso-yml.sh [path-to-yml ...]
# Default path: examples/espanso-ahk-wsl/espanso/ai-windows-to-wsl.yml
#
# When given multiple files, the script also runs a cross-file collision
# check. Platform-variant files (ai.yml, ai-macos.yml, ai-windows-to-wsl.yml)
# are mutually exclusive at install time, so duplicate triggers among them
# are expected; duplicates that include any non-platform file (e.g. the
# prompt-templates kit ai-prompts.yml) are real collisions and fail the
# lint.

set -u

DEFAULT_PATH="examples/espanso-ahk-wsl/espanso/ai-windows-to-wsl.yml"
if [ "$#" -eq 0 ]; then
    set -- "$DEFAULT_PATH"
fi

python3 - "$@" <<'PY'
import os
import re
import sys

try:
    import yaml
except ImportError:
    print("[espanso-yml-lint] error: PyYAML not available", file=sys.stderr)
    sys.exit(1)

PREFIX = "[espanso-yml-lint]"
paths = sys.argv[1:]
failures = []

def fail(severity, where, msg):
    failures.append(f"{PREFIX} {severity}: {where}: {msg}")

# Platform-variant group: these files ship the same trigger set per OS
# and are mutually exclusive at install time -- a user loads exactly
# one. Duplicate triggers WITHIN this group are expected. Any other
# file (e.g., ai-prompts.yml, the prompt-templates kit) is loaded
# *alongside* whichever platform variant is active; cross-group
# duplicates are real collisions that the user will hit (S04 backlog
# item resolved 2026-05).
PLATFORM_VARIANTS = {"ai.yml", "ai-macos.yml", "ai-windows-to-wsl.yml"}
PLATFORM_GROUP = "<platform-variant>"

def file_group(path):
    base = os.path.basename(path)
    return PLATFORM_GROUP if base in PLATFORM_VARIANTS else base

# Regexes used by per-trigger structural checks.
SAFE_PH_RE = re.compile(r"^[A-Za-z0-9 .,:;!?\-]+$")
TRIGGER_ASSIGN_RE = re.compile(r"^\s*\$trigger\s*=\s*'([^']*)'\s*$", re.MULTILINE)
PH_ASSIGN_RE      = re.compile(r"^\s*\$ph\s*=\s*'([^']*)'\s*$",      re.MULTILINE)
BACKSPACE_RE      = re.compile(r"\{BACKSPACE\s*'\s*\+\s*\$(trigger|ph)\.Length\s*\+\s*'\}")
SENDWAIT_TRIG_RE  = re.compile(r"SendWait\(\s*\$trigger\s*\)")
# Form-input injection guards (S03E01 audit, round 2):
#   * forbid {{form1.X}} substituted directly into a single-quoted bash argument
#     value inside the @'...'@ here-string -- this was the bash-apostrophe bug
#     that broke :aitone/:aitr/:aireply when users picked a choice with `'`.
#   * forbid Invoke-Expression / iex composition with form values.
SYS_INLINE_FORM_RE = re.compile(r"--system\s+'[^']*\{\{\s*form1\.")
IEX_FORM_RE        = re.compile(r"(Invoke-Expression|iex)\s*[^\n]*\{\{\s*form1\.", re.IGNORECASE)

# Heredoc-body extractor for bash triggers (S03E04 / v2.1 audit F-1, F-2).
HEREDOC_OPEN_RE = re.compile(r"<<\s*'([A-Za-z_][A-Za-z0-9_]*)'")
PLACEHOLDER_RE  = re.compile(r"\{\{\s*[A-Za-z_][A-Za-z0-9_]*\s*\.")


def find_cmd(match):
    """Return (cmd, shell) from a shell-type var, or (None, None)."""
    for var in match.get("vars", []) or []:
        if not isinstance(var, dict):
            continue
        if var.get("type") != "shell":
            continue
        params = var.get("params") or {}
        cmd = params.get("cmd")
        shell = params.get("shell")
        if isinstance(cmd, str):
            return cmd, shell
    return None, None


def strip_quoted_heredocs(cmd_text):
    """Remove the body of every <<'TAG'...TAG block from cmd_text.

    Only single-quoted terminators count -- those are the ones that
    disable bash interpolation. Unquoted heredocs (<<TAG) still
    interpolate $(...) and ${VAR}, so we deliberately do NOT mask them.
    """
    out_lines = []
    in_heredoc = None
    for line in cmd_text.splitlines():
        if in_heredoc is None:
            m = HEREDOC_OPEN_RE.search(line)
            if m:
                out_lines.append(line)
                in_heredoc = m.group(1)
            else:
                out_lines.append(line)
        else:
            if line.strip() == in_heredoc:
                in_heredoc = None
    return "\n".join(out_lines)


def collect_triggers(match):
    """Return the list of trigger strings this match declares.

    Espanso supports both `trigger: ":foo"` and `triggers: [":foo", ":foo2"]`
    (synonym list). We collect every form so cross-file collision
    detection sees synonyms too.
    """
    out = []
    if "trigger" in match and isinstance(match["trigger"], str):
        out.append(match["trigger"])
    if "triggers" in match and isinstance(match["triggers"], list):
        for t in match["triggers"]:
            if isinstance(t, str):
                out.append(t)
    return out


def lint_one(path):
    """Parse + per-file structural checks. Return list of triggers found,
    or None on parse failure (which is also recorded in `failures`)."""
    try:
        with open(path, "r", encoding="utf-8") as f:
            doc = yaml.safe_load(f)
    except yaml.YAMLError as e:
        mark = getattr(e, "problem_mark", None)
        line = (mark.line + 1) if mark else "?"
        fail("error", path, f"YAML parse error at line {line}: {e}")
        return None
    except OSError as e:
        fail("error", path, f"cannot open: {e}")
        return None

    if not isinstance(doc, dict) or "matches" not in doc or not isinstance(doc["matches"], list):
        fail("error", path, "top-level 'matches:' list missing")
        return None

    matches = doc["matches"]

    # Per-file: collect triggers, flag intra-file duplicates.
    triggers = []
    for i, m in enumerate(matches):
        if not isinstance(m, dict):
            fail("error", f"{path}: matches[{i}]", "match block is not a mapping")
            continue
        ts = collect_triggers(m)
        if not ts:
            fail("error", f"{path}: matches[{i}]", "missing 'trigger:' / 'triggers:' key")
            continue
        triggers.extend(ts)

    seen = {}
    for t in triggers:
        seen[t] = seen.get(t, 0) + 1
    for d in sorted([t for t, c in seen.items() if c > 1]):
        fail("error", f"{path}: {d!r}", f"duplicate trigger appears {seen[d]} times in this file")

    # Per-trigger structural assertions.
    for m in matches:
        if not isinstance(m, dict) or "trigger" not in m:
            continue
        trigger = m["trigger"]
        where = f"{path}: {trigger!r}"
        cmd, shell = find_cmd(m)
        if cmd is None:
            continue

        if shell == "bash":
            outside = strip_quoted_heredocs(cmd)
            for ph_match in PLACEHOLDER_RE.finditer(outside):
                line_no = outside[:ph_match.start()].count("\n") + 1
                fail("error", where,
                     f"line {line_no}: form placeholder {{{{...}}}} appears "
                     "outside a single-quoted heredoc body in a bash cmd; "
                     "user input must reach az-ai via stdin (see "
                     "docs/exec-reports/s03e01-the-yada-yada-strikes-back.md "
                     "and the v2.1 audit F-1/F-2)")
            continue

        # PowerShell-side checks (S02E37 trigger drift, BACKSPACE counts).
        trig_matches = TRIGGER_ASSIGN_RE.findall(cmd)
        if len(trig_matches) != 1:
            fail("error", where, f"expected exactly one $trigger assignment, found {len(trig_matches)}")
        else:
            if trig_matches[0] != trigger:
                fail("error", where,
                     f"$trigger value {trig_matches[0]!r} does not match outer trigger {trigger!r} "
                     "(rename drift -- a trigger was changed without updating $trigger)")

        ph_matches = PH_ASSIGN_RE.findall(cmd)
        if len(ph_matches) != 1:
            fail("error", where, f"expected exactly one $ph assignment, found {len(ph_matches)}")
        else:
            ph_val = ph_matches[0]
            if not SAFE_PH_RE.match(ph_val):
                fail("error", where,
                     f"$ph value {ph_val!r} contains characters unsafe for SendKeys "
                     r"(metachars + ^ % ~ ( ) [ ] { } would be misinterpreted)")

        bs = BACKSPACE_RE.findall(cmd)
        if len(bs) != 2:
            fail("error", where, f"expected exactly two BACKSPACE substitutions, found {len(bs)}")
        else:
            if bs[0] != "trigger":
                fail("error", where, f"first BACKSPACE must reference $trigger.Length, got $${bs[0]}.Length")
            if bs[1] != "ph":
                fail("error", where, f"second BACKSPACE must reference $ph.Length, got $${bs[1]}.Length")

        if "try {" not in cmd:
            fail("error", where, "missing 'try {' block")
        if "} finally {" not in cmd:
            fail("error", where, "missing '} finally {' block")
        bs_iter = list(BACKSPACE_RE.finditer(cmd))
        if len(bs_iter) >= 2:
            second_bs_end = bs_iter[1].end()
            retype = SENDWAIT_TRIG_RE.search(cmd, second_bs_end)
            if retype is None:
                fail("error", where,
                     "missing SendWait($trigger) retype after second BACKSPACE in finally block")

        if SYS_INLINE_FORM_RE.search(cmd):
            fail("error", where,
                 "{{form1.X}} appears inside a single-quoted --system argument; "
                 "use a PS switch-mapped variable (choice fields) or env var via WSLENV "
                 "(free-form fields) instead -- raw substitution is the S03E01 bug class")
        if IEX_FORM_RE.search(cmd):
            fail("error", where,
                 "Invoke-Expression / iex composed with {{form1.X}} -- forbidden")

    return triggers


# Run per-file lint, collect triggers per file.
all_triggers_by_file = {}
for path in paths:
    triggers = lint_one(path)
    if triggers is not None:
        all_triggers_by_file[path] = triggers

# Cross-file collision check (only meaningful when more than one file is
# given). Platform variants are treated as a single "group" since users
# install exactly one.
if len(all_triggers_by_file) >= 2:
    # trigger -> list of (group, path)
    occurrences = {}
    for path, triggers in all_triggers_by_file.items():
        group = file_group(path)
        for t in triggers:
            occurrences.setdefault(t, []).append((group, path))

    for trig in sorted(occurrences):
        occ = occurrences[trig]
        groups = {g for g, _ in occ}
        # Collision iff the trigger appears in 2+ distinct groups (a
        # platform-variant kit + a non-platform kit, or two distinct
        # non-platform kits).
        if len(groups) >= 2:
            files = sorted({p for _, p in occ})
            fail("error", f"<cross-file>: {trig!r}",
                 "trigger collides across kits that load together: "
                 + ", ".join(files))

if failures:
    for line in failures:
        print(line)
    total = sum(len(v) for v in all_triggers_by_file.values())
    print(f"{PREFIX} summary: {len(failures)} failure(s) across "
          f"{len(all_triggers_by_file)} file(s), {total} trigger(s) checked")
    sys.exit(1)

total = sum(len(v) for v in all_triggers_by_file.values())
if len(all_triggers_by_file) == 1:
    only_path = next(iter(all_triggers_by_file))
    print(f"{PREFIX} ok: {only_path} ({total} triggers checked)")
else:
    print(f"{PREFIX} ok: {len(all_triggers_by_file)} files, {total} triggers checked, no cross-file collisions")
sys.exit(0)
PY
