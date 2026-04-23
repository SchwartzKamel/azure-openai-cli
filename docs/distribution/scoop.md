# Scoop distribution

> Status: **DRAFT**. Bucket repository does not yet exist; SHA256 in the
> manifest is a placeholder. This document describes the approach.

## Approach: custom bucket first, scoop-extras later

[Scoop](https://scoop.sh/) ships software as JSON manifests grouped
into "buckets". Two paths:

1. **Custom bucket** (`SchwartzKamel/scoop-bucket`) -- a plain GitHub
   repo containing a `bucket/` directory of manifest JSON files. Users
   add it with `scoop bucket add`.
2. **scoopinstaller/extras** -- the second-most-trusted upstream
   bucket (after `main`). Acceptance is by PR; manifests must follow
   strict conventions and pass the bucket's CI checks.

**We start with the custom bucket** for the same reasons Homebrew gets a
custom tap: control cadence, iterate the manifest shape, prove
stability before asking for upstream trust.

## Draft manifest

Lives at
[`packaging/scoop/azure-openai-cli.json`](../../packaging/scoop/azure-openai-cli.json).
Key fields:

```json
{
  "version": "1.9.1",
  "architecture": {
    "64bit": {
      "url": "https://github.com/SchwartzKamel/azure-openai-cli/releases/download/v1.9.1/azure-openai-cli-win-x64.zip",
      "hash": "TODO_FILL_AT_RELEASE_TIME"
    }
  },
  "bin": [["AzureOpenAI_CLI.exe", "azure-openai-cli"]],
  "checkver": { "github": "https://github.com/SchwartzKamel/azure-openai-cli", "regex": "releases/tag/v(1\\.\\d+\\.\\d+)" },
  "autoupdate": { "architecture": { "64bit": { "url": "https://github.com/SchwartzKamel/azure-openai-cli/releases/download/v$version/azure-openai-cli-win-x64.zip" }, "hash": { "url": "$url.sha256" } } }
}
```

The `bin` rename publishes `azure-openai-cli` on PATH while the zip
ships `AzureOpenAI_CLI.exe` (the .NET assembly name).

## SemVer link to Mr. Lippman

The `checkver.regex` of `v(1\.\d+\.\d+)` deliberately constrains the
auto-bump to the v1 line. This is **load-bearing**:

- v2 ships as a separate package (`az-ai`) with its own manifest;
  Scoop must not auto-bump v1 users onto a v2 release.
- Per [`docs/release/semver-policy.md`](../release/semver-policy.md), a
  v2.0.0 tag is a MAJOR break -- it's never a silent update.
- When the v1 line eventually retires, Mr. Lippman flips the regex to
  `v(2\.\d+\.\d+)` in a deliberate manifest commit, not as a side
  effect of a tag.

The `autoupdate` block uses `$url.sha256`. The release pipeline must
publish a sibling `<artifact>.sha256` file alongside each zip; if it
doesn't, `scoop checkver -u` falls back to downloading the zip and
hashing it locally (still safe, just slower).

## Install / uninstall verification

Once the bucket exists:

```powershell
# Install
scoop bucket add schwartzkamel https://github.com/SchwartzKamel/scoop-bucket
scoop install azure-openai-cli
azure-openai-cli --version       # expect: 1.9.1

# Update
scoop update azure-openai-cli

# Uninstall
scoop uninstall azure-openai-cli
scoop bucket rm schwartzkamel
where.exe azure-openai-cli       # expect: nothing
```

Pre-bucket (today) install path:

```powershell
scoop install ./packaging/scoop/azure-openai-cli.json
```

## Validation

The manifest must parse as JSON:

```sh
python -m json.tool packaging/scoop/azure-openai-cli.json > $null
```

Once published, also:

```powershell
scoop install scoop-checkver
scoop-checkver azure-openai-cli
```

## Open work (deferred)

- Bucket repository creation (`SchwartzKamel/scoop-bucket`).
- Sibling `<artifact>.sha256` file emitted by `.github/workflows/release.yml`
  for the v1 win-x64 zip (so `autoupdate.hash.url` works without a
  fallback download).
- ARM64 Windows artifact when the v1 release matrix grows it.
- `scoopinstaller/extras` PR after twelve consecutive stable releases.

## Cross-refs

- [Distribution README](README.md) -- channel comparison.
- [Homebrew distribution](homebrew.md) -- mirror approach for macOS.
- [`docs/release/semver-policy.md`](../release/semver-policy.md) --
  why the `checkver` regex pins to the v1 line.
