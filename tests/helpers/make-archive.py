#!/usr/bin/env python3
"""Build a synthetic release archive for scripts/verify-archive.py's tests.

    make-archive.py <outdir> <kind>     prints:  <archive path> <rid>

Kinds are one good archive per format plus one deliberately broken archive per rule the verifier enforces.
The "binary" is a few bytes of junk, or (for the run-* kinds) a tiny shell script, so no AOT build is needed.
"""
import io
import os
import sys
import tarfile
import zipfile

VER = "2.4.0"
LEGAL = {"LICENSE": b"MIT\n", "NOTICE": b"notice\n", "THIRD_PARTY_NOTICES.md": b"third party\n", "README.md": b"readme\n"}
JUNK = b"\x7fELF-not-a-real-binary" * 64
SHIM = b'@echo off\r\n"%~dp0hopper.exe" %*\r\nexit /b %errorlevel%\r\n'


def script(out_text, code=0):
    return f"#!/bin/sh\necho '{out_text}'\nexit {code}\n".encode()


def tar(path, top, files, links=None, modes=None, raw=None):
    with tarfile.open(path, "w:gz") as t:
        d = tarfile.TarInfo(top)
        d.type, d.mode = tarfile.DIRTYPE, 0o755
        t.addfile(d)
        for rel, data in files.items():
            ti = tarfile.TarInfo(f"{top}/{rel}")
            ti.size = len(data)
            ti.mode = (modes or {}).get(rel, 0o755 if rel in ("hopper", "az-ai") else 0o644)
            t.addfile(ti, io.BytesIO(data))
        for rel, target in (links or {}).items():
            ti = tarfile.TarInfo(f"{top}/{rel}")
            ti.type, ti.linkname, ti.mode = tarfile.LNKTYPE, target, 0o755
            t.addfile(ti)
        for name, data in (raw or {}).items():
            ti = tarfile.TarInfo(name)
            ti.size = len(data)
            t.addfile(ti, io.BytesIO(data))


def zipf(path, top, files):
    with zipfile.ZipFile(path, "w", zipfile.ZIP_DEFLATED) as z:
        for rel, data in files.items():
            z.writestr(f"{top}/{rel}", data)


def main(outdir, kind):
    rid = "win-x64" if kind.startswith("zip-") else "linux-x64"
    ext = "zip" if rid.startswith("win-") else "tar.gz"
    top = f"hopper-{VER}-{rid}"
    path = os.path.join(outdir, f"{top}.{ext}")
    good_files = {"hopper": JUNK, **LEGAL}
    good_links = {"az-ai": f"{top}/hopper"}

    if kind == "good-tar":
        tar(path, top, good_files, good_links)
    elif kind == "bad-name":
        path = os.path.join(outdir, f"az-ai-{VER}-{rid}.{ext}")
        tar(path, top, good_files, good_links)
    elif kind == "bad-topdir":
        tar(path, f"az-ai-{VER}-{rid}", good_files, {"az-ai": f"az-ai-{VER}-{rid}/hopper"})
    elif kind == "no-alias":
        tar(path, top, good_files)
    elif kind == "no-license":
        tar(path, top, {k: v for k, v in good_files.items() if k != "LICENSE"}, good_links)
    elif kind == "extra-file":
        tar(path, top, {**good_files, "hopper.dbg": b"symbols"}, good_links)
    elif kind == "escape":
        tar(path, top, good_files, good_links, raw={f"{top}/../evil": b"x"})
    elif kind == "alias-differs":
        tar(path, top, {**good_files, "az-ai": b"some other program"})
    elif kind == "alias-links-elsewhere":
        tar(path, top, good_files, {"az-ai": f"{top}/LICENSE"})
    elif kind == "not-executable":
        tar(path, top, good_files, good_links, modes={"hopper": 0o644})
    elif kind == "empty-binary":
        tar(path, top, {**good_files, "hopper": b""}, good_links)
    elif kind == "two-tops":
        tar(path, top, good_files, good_links, raw={"extra-dir/file": b"x"})
    elif kind == "not-an-archive":
        open(path, "wb").write(b"this is not a tarball")
    elif kind == "run-ok":
        tar(path, top, {**good_files, "hopper": script(f"hopper {VER} (fake)")}, good_links)
    elif kind == "run-exit1":
        tar(path, top, {**good_files, "hopper": script(f"hopper {VER} (fake)", 1)}, good_links)
    elif kind == "run-wrong-version":
        tar(path, top, {**good_files, "hopper": script("hopper 1.0.0 (fake)")}, good_links)
    elif kind == "zip-good":
        zipf(path, top, {"hopper.exe": JUNK, "az-ai.cmd": SHIM, **LEGAL})
    elif kind == "zip-exe-copy":
        zipf(path, top, {"hopper.exe": JUNK, "az-ai.exe": JUNK, **LEGAL})
    elif kind == "zip-shim-no-forward":
        zipf(path, top, {"hopper.exe": JUNK, "az-ai.cmd": b"@echo off\r\necho nope\r\n", **LEGAL})
    elif kind == "zip-shim-huge":
        zipf(path, top, {"hopper.exe": JUNK, "az-ai.cmd": SHIM + b"rem " + b"x" * 4096 + b"\r\n", **LEGAL})
    else:
        sys.exit(f"unknown kind {kind}")
    print(path, rid)


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2])
