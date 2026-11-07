#!/usr/bin/env python3
"""Verify a release archive before it is published.

    verify-archive.py <archive> <rid> [--version X.Y.Z] [--run]

Checks, and fails (exit 1, one line per problem) on any of them:

  * the file name is hopper-<version>-<rid>.tar.gz (zip on win-*), and matches --version when given;
  * the archive has exactly ONE top-level directory, named hopper-<version>-<rid>, and every member is inside it
    (no absolute paths, no '..': an archive that writes outside its directory is a zip-slip, never publishable);
  * it holds the binary (hopper, hopper.exe on win-*), LICENSE, NOTICE, THIRD_PARTY_NOTICES.md and README.md, and
    the alias az-ai (a link or copy of hopper on unix; a small az-ai.cmd shim on Windows, not a second 7 MB binary);
  * nothing else (stray debug symbols or build leftovers would be published too);
  * on unix the binary and the alias are executable.

With --run it also extracts the archive and RUNS `hopper --version` and the alias, requiring exit 0 and the version in
the output. Use it only where the runner can execute the rid. ASCII output, so it reads in a CI log.
"""
import hashlib
import os
import re
import shutil
import subprocess
import sys
import tarfile
import tempfile
import zipfile

LEGAL = ["LICENSE", "NOTICE", "THIRD_PARTY_NOTICES.md", "README.md"]


def fail_exit(problems):
    for p in problems:
        print(f"verify-archive: FAIL: {p}")
    sys.exit(1)


def read_zip(path):
    """-> (members, data, links): members are (name, is_dir, mode); data maps name -> bytes; zips carry no links."""
    mem, data = [], {}
    with zipfile.ZipFile(path) as z:
        for i in z.infolist():
            mem.append((i.filename, i.is_dir(), (i.external_attr >> 16) & 0o7777))
            if not i.is_dir():
                data[i.filename] = z.read(i.filename)
    return mem, data, {}


def read_tar(path):
    """-> (members, data, links): links maps a member name to the member it is a hard or symbolic link to."""
    mem, data, links = [], {}, {}
    with tarfile.open(path, "r:gz") as t:
        for m in t.getmembers():
            mem.append((m.name, m.isdir(), m.mode))
            if m.isfile():
                data[m.name] = t.extractfile(m).read()
            elif m.islnk() or m.issym():
                links[m.name] = m.linkname
    return mem, data, links


def main(argv):
    args = [a for a in argv[1:] if not a.startswith("--")]
    want_run = "--run" in argv
    version = None
    if "--version" in argv:
        i = argv.index("--version")
        if i + 1 >= len(argv):
            fail_exit(["--version needs a value"])
        version = argv[i + 1]
        args = [a for a in args if a != version]
    if len(args) != 2:
        print(__doc__)
        return 2
    archive, rid = args
    windows = rid.startswith("win-")
    ext = "zip" if windows else "tar.gz"
    problems = []

    base = os.path.basename(archive)
    m = re.fullmatch(r"hopper-(\d+\.\d+\.\d+(?:-[0-9A-Za-z.]+)?)-" + re.escape(rid) + r"\." + re.escape(ext), base)
    if not m:
        fail_exit([f"file name '{base}' is not hopper-<version>-{rid}.{ext}"])
    found_version = m.group(1)
    if version and version != found_version:
        problems.append(f"file name carries version {found_version}, expected {version}")
    stem = f"hopper-{found_version}-{rid}"

    try:
        mem, data, links = read_zip(archive) if windows else read_tar(archive)
    except (zipfile.BadZipFile, tarfile.TarError, OSError) as e:
        fail_exit([f"cannot read the archive: {e}"])

    names = [n.rstrip("/") for n, _d, _m in mem]
    for n in names:
        if n.startswith("/") or re.match(r"^[A-Za-z]:", n) or ".." in n.replace("\\", "/").split("/"):
            problems.append(f"member '{n}' escapes the archive directory")
    tops = sorted({n.replace("\\", "/").split("/")[0] for n in names if n})
    if tops != [stem]:
        problems.append(f"expected exactly one top-level directory '{stem}', found {tops}")

    files = {n.replace("\\", "/")[len(stem) + 1:] for n, d, _m in mem if not d and n.replace("\\", "/").startswith(stem + "/")}
    files |= {n.replace("\\", "/")[len(stem) + 1:] for n in links if n.startswith(stem + "/")}  # a hard link is a file too
    binary = "hopper.exe" if windows else "hopper"
    alias = "az-ai.cmd" if windows else "az-ai"
    expected = {binary, alias, *LEGAL}
    for need in sorted(expected - files):
        problems.append(f"missing '{need}'")
    for extra in sorted(files - expected):
        problems.append(f"unexpected file '{extra}'")

    def content(rel):
        return data.get(f"{stem}/{rel}")

    bin_bytes = content(binary)
    if bin_bytes is not None and len(bin_bytes) == 0:
        problems.append(f"'{binary}' is empty")

    if not windows:
        modes = {n.replace("\\", "/")[len(stem) + 1:]: mode for n, _d, mode in mem if n.startswith(stem + "/")}
        for exe in (binary, alias):
            if exe in modes and not (modes[exe] & 0o111):
                problems.append(f"'{exe}' is not executable (mode {oct(modes[exe])})")
        # the alias must BE the binary: a hard link to it, or an identical copy
        link_target = links.get(f"{stem}/{alias}")
        if link_target is not None:
            if link_target != f"{stem}/{binary}":
                problems.append(f"'{alias}' links to '{link_target}', not to '{binary}'")
        elif content(alias) is not None and bin_bytes is not None:
            if hashlib.sha256(content(alias)).digest() != hashlib.sha256(bin_bytes).digest():
                problems.append(f"'{alias}' is not identical to '{binary}'")
    else:
        shim = content(alias)
        if shim is not None:
            text = shim.decode("utf-8", "replace")
            if "hopper.exe" not in text or "%*" not in text:
                problems.append("az-ai.cmd does not forward its arguments to hopper.exe")
            if len(shim) > 2048:
                problems.append("az-ai.cmd is not a small shim (a second copy of the binary would double the download)")

    if problems:
        fail_exit(problems)

    if want_run:
        tmp = tempfile.mkdtemp(prefix="verify-archive-")
        try:
            if windows:
                with zipfile.ZipFile(archive) as z:
                    z.extractall(tmp)
            else:
                with tarfile.open(archive, "r:gz") as t:
                    t.extractall(tmp)
            root = os.path.join(tmp, stem)
            runs = [[os.path.join(root, binary), "--version"]]
            runs.append(["cmd", "/c", os.path.join(root, alias), "--version"] if windows else [os.path.join(root, alias), "--version"])
            for cmd in runs:
                r = subprocess.run(cmd, capture_output=True, text=True, timeout=60)
                out = (r.stdout + r.stderr)
                label = os.path.basename(cmd[-2] if cmd[0] == "cmd" else cmd[0])
                if r.returncode != 0:
                    problems.append(f"'{label} --version' exited {r.returncode}")
                elif found_version not in out:
                    problems.append(f"'{label} --version' did not report {found_version}: {out.strip()[:80]!r}")
        finally:
            shutil.rmtree(tmp, ignore_errors=True)
        if problems:
            fail_exit(problems)

    print(f"verify-archive: OK {base} ({len(files)} files{', ran hopper and az-ai' if want_run else ''})")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
