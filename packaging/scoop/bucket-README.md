# scoop-bucket

Scoop bucket for [hopper](https://github.com/SchwartzKamel/hopper), the AOT command-line agent for Azure OpenAI.

```powershell
scoop bucket add schwartzkamel https://github.com/SchwartzKamel/scoop-bucket
scoop install schwartzkamel/hopper
```

That installs `hopper` and shims `az-ai` to the same binary (the old command name, so existing scripts keep working).

- `bucket/hopper.json` is the current release. `bucket/az-ai@<version>.json` are frozen pins of the releases that were
  still named `az-ai`.
- The manifest's URLs and hashes are checked against the release's published digests on every change. The install itself
  has not yet been exercised on Windows.

**This repository is a mirror.** Do not edit it. The manifests live in
[`packaging/scoop/`](https://github.com/SchwartzKamel/hopper/tree/main/packaging/scoop) in the main repository and are copied
here by `scripts/publish-taps.sh` after each release.
