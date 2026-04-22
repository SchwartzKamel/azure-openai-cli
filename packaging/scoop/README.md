# Scoop bucket — `az-ai-v2`

> "Scoop bucket? I got a guy. It's handled." — Bob Sacamano

This directory holds the Scoop manifests pinned in-repo. The actual
**bucket repo** is published out-of-tree at:

- **`SchwartzKamel/scoop-az-ai`** (added by users as `schwartzkamel`).

Manifests in this directory are canonical; the bucket repo mirrors
them. Never hand-edit the bucket — always round-trip through this
repo.

## Layout

```
packaging/scoop/
├── README.md                      # this file
├── az-ai.json                     # tracks latest (currently v2.0.4; v2.0.5 bump queued)
└── versions/
    ├── az-ai-v2@2.0.0.json        # frozen pin
    ├── az-ai-v2@2.0.1.json
    ├── az-ai-v2@2.0.2.json
    └── az-ai-v2@2.0.4.json
```

`versions/` follows the
[`scoopinstaller/versions`](https://github.com/ScoopInstaller/Versions)
convention so users can pin with `@<version>` without a bespoke
bucket layout.

v2.0.3 has no manifest — the tag was cancelled at cutover. v2.0.5
fix-forward is queued; when it lands, copy `az-ai.json` to
`versions/az-ai-v2@2.0.4.json` (already present) and bump the
tracking manifest.

## Install (end-user)

Once the bucket is live:

```powershell
scoop bucket add schwartzkamel https://github.com/SchwartzKamel/scoop-az-ai
scoop install schwartzkamel/az-ai-v2            # latest
scoop install schwartzkamel/az-ai-v2@2.0.4      # pinned
```

Before the bucket is live, install straight from the manifest:

```powershell
scoop install ./packaging/scoop/az-ai.json
```

## Publish (ops action)

Full step-by-step runbook: **[`docs/runbooks/packaging-publish.md`](../../docs/runbooks/packaging-publish.md)**.

Short version:

1. Ensure `SchwartzKamel/scoop-az-ai` exists.
2. Copy `packaging/scoop/az-ai.json` to the bucket's `bucket/az-ai.json`
   (Scoop resolves `bucket/<name>.json` by default).
3. Mirror `packaging/scoop/versions/*.json` to `bucket/versions/*.json`.
4. `scoop install schwartzkamel/az-ai-v2` on a fresh Windows box
   before announcing (Puddy gate).

## Verify locally (pre-publish)

```powershell
scoop install ./packaging/scoop/az-ai.json
az-ai-v2 --version --short
```

The v2.0.4 binary reports `2.0.2` due to the filename-drift captured
in `_comment_filename_drift` at the top of `az-ai.json`. v2.0.5 rolls
the version strings back into lock-step.

## Auto-bump (post-cutover)

`az-ai.json` already carries a `checkver` + `autoupdate` block. Once
the bucket exists, run inside the bucket checkout:

```powershell
scoop checkver az-ai-v2 -u
```

That re-computes `version`, `url`, and `hash` from the GitHub Releases
RSS feed and the `.sbom.json` sidecar. Do **not** run `checkver`
against the pinned `versions/*.json` files — those are frozen by
design.

## Hash provenance

SHA256s embedded in each manifest come from the GitHub Release's
`digests.txt` artifact (produced by the `Compute digests` step in
`.github/workflows/release.yml`). `autoupdate.hash.url` pulls from
`$url.sbom.json`, which CycloneDX populates at release time — the
two must agree. If they disagree, **the release is broken, not the
manifest**.

## NOTICE bundling (Windows)

Scoop has no concept of a `doc/` directory, so the release zip must
place `NOTICE`, `THIRD_PARTY_NOTICES.md`, and `LICENSE` at the zip
root alongside `az-ai-v2.exe`. `post_install` in the manifest surfaces
the paths to the user so Mr. Lippman's "all distributed artifacts
include NOTICE" claim holds on Windows.
