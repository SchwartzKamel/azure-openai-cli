#!/usr/bin/env python3
"""Check package-manager manifests against a release's PUBLISHED digests.

    check-manifests.py [--digests FILE] [MANIFEST ...]

Default manifests: packaging/homebrew/Formula/hopper.rb, packaging/scoop/hopper.json, packaging/nix/flake.nix.

For every archive a manifest pins, this requires that:
  * the download URL is  https://github.com/<owner>/<repo>/releases/download/v<ver>/hopper-<ver>-<rid>.(tar.gz|zip),
    all pins in a manifest share one version, and (Scoop) the manifest's version and extract_dir agree with the URL;
  * the file named by the URL is in the release's digests.txt (a manifest must never point at an asset that does not exist);
  * the pinned sha256 equals the published one. Nix's SRI form (sha256-<base64>) is converted before comparing.

The digests come from --digests FILE, or are fetched from the release itself. A manifest that yields no pins fails: a check
that finds nothing to check is not a pass. Exit 0 all good, 1 any failure, 2 usage. ASCII output.
"""
import base64
import json
import os
import re
import sys
import urllib.request

DEFAULTS = ["packaging/homebrew/Formula/hopper.rb", "packaging/scoop/hopper.json", "packaging/nix/flake.nix"]
URL_RE = re.compile(r"^https://github\.com/([^/]+/[^/]+)/releases/download/v(\d+\.\d+\.\d+)/(hopper-(\d+\.\d+\.\d+)-([a-z0-9-]+)\.(tar\.gz|zip))$")
_cache = {}


def ext_for(rid):
    return "zip" if rid.startswith("win-") else "tar.gz"


def fetch_digests(repo, ver):
    key = (repo, ver)
    if key not in _cache:
        url = f"https://github.com/{repo}/releases/download/v{ver}/digests.txt"
        last = None
        for _ in range(3):
            try:
                with urllib.request.urlopen(url, timeout=30) as r:
                    _cache[key] = parse_digests(r.read().decode("utf-8", "replace"))
                break
            except Exception as e:  # network errors are failures, not passes
                last = e
        else:
            raise SystemExit(f"check-manifests: FAIL: cannot fetch {url}: {last}")
    return _cache[key]


def parse_digests(text):
    out = {}
    for line in text.splitlines():
        parts = line.split()
        if len(parts) == 2 and re.fullmatch(r"[0-9a-f]{64}", parts[0]):
            out[parts[1].lstrip("*")] = parts[0]
    return out


def sri_to_hex(sri):
    m = re.fullmatch(r"sha256-([A-Za-z0-9+/]{43}=)", sri)
    return base64.b64decode(m.group(1)).hex() if m else None


def pins_brew(text):
    return [{"url": u, "hash": h} for u, h in re.findall(r'url "([^"]+)"\s*\n\s*sha256 "([0-9a-f]{64})"', text)], {}


def pins_scoop(text):
    d = json.loads(text)
    pins, meta = [], {"version": d.get("version")}
    for arch, a in (d.get("architecture") or {}).items():
        pins.append({"url": a.get("url", ""), "hash": a.get("hash", ""), "extract_dir": a.get("extract_dir"), "arch": arch})
    return pins, meta


def pins_nix(text):
    ver = re.search(r'^\s*version = "(\d+\.\d+\.\d+)";', text, re.M)
    repo = re.search(r"https://github\.com/([^/]+/[^/]+)/releases/download", text)
    block = re.search(r"latestHashes = \{(.*?)\};", text, re.S)
    pins = []
    if ver and repo and block:
        for rid, sri in re.findall(r'([a-z0-9-]+)\s*=\s*"(sha256-[^"]+)"', block.group(1)):
            pins.append({"url": f"https://github.com/{repo.group(1)}/releases/download/v{ver.group(1)}/hopper-{ver.group(1)}-{rid}.{ext_for(rid)}",
                         "hash": sri_to_hex(sri) or "invalid-sri:" + sri})
    return pins, {}


def check(path, digests_file):
    problems, checked = [], 0
    try:
        text = open(path, encoding="utf-8").read()
    except OSError as e:
        print(f"check-manifests: cannot read {path}: {e}", file=sys.stderr)
        sys.exit(2)
    kind = pins_scoop if path.endswith(".json") else pins_nix if path.endswith(".nix") else pins_brew
    try:
        pins, meta = kind(text)
    except (ValueError, KeyError) as e:
        return [f"{path}: cannot parse: {e}"], 0
    if not pins:
        return [f"{path}: no pinned archives found, so there is nothing to check"], 0
    versions = set()
    for p in pins:
        m = URL_RE.match(p["url"])
        if not m:
            problems.append(f"{path}: url is not .../download/v<ver>/hopper-<ver>-<rid>.(tar.gz|zip): {p['url']}")
            continue
        repo, ver, asset, ver2, rid, _ext = m.groups()
        versions.add(ver)
        if ver != ver2:
            problems.append(f"{path}: {asset} sits under tag v{ver}, not v{ver2}")
        if ext_for(rid) != _ext:
            problems.append(f"{path}: {asset} should be .{ext_for(rid)} for {rid}")
        if meta.get("version") and meta["version"] != ver:
            problems.append(f"{path}: manifest version {meta['version']} but the url says {ver}")
        if p.get("extract_dir") is not None and p["extract_dir"] != f"hopper-{ver}-{rid}":
            problems.append(f"{path}: extract_dir '{p['extract_dir']}' should be 'hopper-{ver}-{rid}'")
        digests = parse_digests(open(digests_file, encoding="utf-8").read()) if digests_file else fetch_digests(repo, ver)
        if asset not in digests:
            problems.append(f"{path}: {asset} is not in the release's digests (the manifest points at an asset that does not exist)")
            continue
        if p["hash"] != digests[asset]:
            problems.append(f"{path}: {asset} sha256 {p['hash'][:16]}... does not match the published {digests[asset][:16]}...")
            continue
        checked += 1
        print(f"check-manifests: OK {os.path.basename(path)} {rid} {asset}")
    if len(versions) > 1:
        problems.append(f"{path}: pins mix versions {sorted(versions)}")
    return problems, checked


def main(argv):
    args, digests = [], None
    i = 1
    while i < len(argv):
        if argv[i] == "--digests":
            if i + 1 >= len(argv):
                print(__doc__)
                return 2
            digests = argv[i + 1]; i += 2
        elif argv[i] in ("-h", "--help"):
            print(__doc__); return 2
        else:
            args.append(argv[i]); i += 1
    files = args or DEFAULTS
    all_problems, total = [], 0
    for f in files:
        probs, n = check(f, digests)
        all_problems += probs; total += n
    for p in all_problems:
        print(f"check-manifests: FAIL: {p}")
    if all_problems or total == 0:
        if total == 0 and not all_problems:
            print("check-manifests: FAIL: nothing was checked")
        return 1
    print(f"check-manifests: OK {total} pin(s) in {len(files)} manifest(s) match the published digests")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
