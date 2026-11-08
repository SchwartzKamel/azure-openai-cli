#!/usr/bin/env python3
"""Build synthetic package-manager manifests (and a matching digests.txt) for scripts/check-manifests.py's tests.

    make-manifests.py <outdir> <kind>      writes hopper.rb, hopper.json, flake.nix and digests.txt into <outdir>

Version 9.9.9 with made-up digests. 'good' is correct; every other kind breaks exactly one rule.
"""
import base64
import hashlib
import json
import os
import sys

V = "9.9.9"
REPO = "SchwartzKamel/azure-openai-cli"
RIDS = ["linux-x64", "linux-musl-x64", "linux-arm64", "osx-arm64", "win-x64", "win-arm64"]


def ext(rid):
    return "zip" if rid.startswith("win-") else "tar.gz"


def asset(rid, v=V, prefix="hopper"):
    return f"{prefix}-{v}-{rid}.{ext(rid)}"


def sha(rid):
    return hashlib.sha256(f"archive:{rid}".encode()).hexdigest()


def url(rid, v=V, tag=None, prefix="hopper"):
    return f"https://github.com/{REPO}/releases/download/v{tag or v}/{asset(rid, v, prefix)}"


def sri(h):
    return "sha256-" + base64.b64encode(bytes.fromhex(h)).decode()


def brew(kind):
    if kind == "brew-empty":
        return 'class Hopper < Formula\n  desc "x"\nend\n'
    h = {r: sha(r) for r in RIDS}
    u = {r: url(r) for r in RIDS}
    if kind == "brew-bad-hash":
        h["linux-x64"] = "0" * 64
    if kind == "brew-missing-asset":
        u["linux-arm64"] = url("linux-arm64", v="9.9.8")
    if kind == "brew-az-ai-prefix":
        u["linux-x64"] = url("linux-x64", prefix="az-ai")
    if kind == "brew-mixed-versions":
        u["osx-arm64"] = url("osx-arm64", v="9.9.8")
    if kind == "brew-wrong-tag":
        u["linux-x64"] = url("linux-x64", tag="9.9.8")
    if kind == "brew-wrong-ext":
        u["linux-x64"] = u["linux-x64"].replace(".tar.gz", ".zip")
    return f'''class Hopper < Formula
  on_macos do
    on_arm do
      url "{u["osx-arm64"]}"
      sha256 "{h["osx-arm64"]}"
    end
  end
  on_linux do
    on_intel do
      url "{u["linux-x64"]}"
      sha256 "{h["linux-x64"]}"
    end
    on_arm do
      url "{u["linux-arm64"]}"
      sha256 "{h["linux-arm64"]}"
    end
  end
end
'''


def scoop(kind):
    d = {
        "version": "9.9.8" if kind == "scoop-bad-version" else V,
        "architecture": {
            "64bit": {"url": url("win-x64"), "hash": "0" * 64 if kind == "scoop-bad-hash" else sha("win-x64"),
                      "extract_dir": "hopper-9.9.9-win-arm64" if kind == "scoop-bad-extract-dir" else f"hopper-{V}-win-x64"},
            "arm64": {"url": url("win-arm64"), "hash": sha("win-arm64"), "extract_dir": f"hopper-{V}-win-arm64"},
        },
    }
    return json.dumps(d, indent=4) + "\n"


def nix(kind):
    if kind == "nix-no-block":
        return 'let\n  version = "9.9.9";\nin {}\n'
    lx, os_ = sri(sha("linux-x64")), sri(sha("osx-arm64"))
    if kind == "nix-bad-hash":
        lx = sri("0" * 64)
    if kind == "nix-bad-sri":
        lx = "sha256-not-base64"
    return f'''{{
  outputs = {{ self }}:
    let
      version = "{V}";
      baseUrlFor = v: "https://github.com/{REPO}/releases/download/v${{v}}";
      latestHashes = {{
        linux-x64 = "{lx}";
        osx-arm64 = "{os_}";
      }};
    in {{}};
}}
'''


def main(out, kind):
    os.makedirs(out, exist_ok=True)
    files = {"hopper.rb": brew(kind), "hopper.json": scoop(kind), "flake.nix": nix(kind)}
    for name, text in files.items():
        open(os.path.join(out, name), "w", encoding="utf-8").write(text)
    drop = "hopper-9.9.9-linux-arm64.tar.gz" if kind == "digests-missing-asset" else None
    with open(os.path.join(out, "digests.txt"), "w", encoding="utf-8") as f:
        for rid in RIDS:
            if asset(rid) != drop:
                f.write(f"{sha(rid)}  {asset(rid)}\n")
        # Give the "wrong" asset a real, matching digest, so ONLY the rule under test can object to it (otherwise the
        # missing-asset rule fires first and hides it).
        if kind == "brew-mixed-versions":
            f.write(f"{sha('osx-arm64')}  {asset('osx-arm64', v='9.9.8')}\n")
        if kind == "brew-wrong-ext":
            f.write(f"{sha('linux-x64')}  hopper-{V}-linux-x64.zip\n")


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2])
