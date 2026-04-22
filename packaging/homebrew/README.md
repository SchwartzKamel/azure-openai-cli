# Homebrew tap -- `az-ai-v2`

> "Homebrew? I know a guy. Done. Next problem." -- Bob Sacamano

This directory holds the Homebrew formulae pinned in-repo. The actual
**tap repo** is published out-of-tree at:

- **`SchwartzKamel/homebrew-az-ai`** (Homebrew resolves the short name
  `schwartzkamel/az-ai` → `github.com/SchwartzKamel/homebrew-az-ai`).

Formulae in `Formula/` are the canonical source; the tap repo mirrors
them. Never hand-edit the tap -- always round-trip through this repo.

## Layout

```text
packaging/homebrew/
├── README.md                      # this file
└── Formula/
    ├── az-ai.rb                   # tracks latest (currently pinned to v2.0.4; v2.0.5 bump queued)
    ├── az-ai-v2@2.0.0.rb          # frozen pin -- keg_only
    ├── az-ai-v2@2.0.1.rb
    ├── az-ai-v2@2.0.2.rb
    └── az-ai-v2@2.0.4.rb
```

v2.0.3 has no formula -- the tag was cancelled at cutover; see
`docs/launch/v2.0.2-publish-handoff.md`. v2.0.5 is the queued
fix-forward that rolls the `stage.sh` VERSION drift called out in
`Formula/az-ai-v2@2.0.4.rb`'s filename-drift comment.

## Install (end-user)

Once the tap repo is live:

```sh
brew tap schwartzkamel/az-ai      # maps to SchwartzKamel/homebrew-az-ai
brew install schwartzkamel/az-ai/az-ai-v2          # latest tracking formula
brew install schwartzkamel/az-ai/az-ai-v2@2.0.4    # pinned
```

Before the tap is live, install straight from the file:

```sh
brew install --formula ./packaging/homebrew/Formula/az-ai.rb
```

## Intel Macs (osx-x64 dropped in v2.0.4)

The v2.0.4 release matrix **does not** ship an osx-x64 tarball (see
`docs/runbooks/macos-runner-triage.md` §5). On Intel Macs, install the
Apple Silicon bottle under Rosetta 2:

```sh
softwareupdate --install-rosetta --agree-to-license
arch -x86_64 /usr/sbin/softwareupdate --install-rosetta  # no-op if already installed
arch -arm64e brew install schwartzkamel/az-ai/az-ai-v2   # uses osx-arm64 tarball
```

This path is a stop-gap. If and when osx-x64 tarballs return to the
release matrix, add an `on_intel` block back to `Formula/az-ai.rb` and
drop this note.

## Publish (ops action)

Full step-by-step runbook: **[`docs/runbooks/packaging-publish.md`](../../docs/runbooks/packaging-publish.md)**.

Short version:

1. Ensure `SchwartzKamel/homebrew-az-ai` exists (create via `gh repo
   create` if not).
2. Copy `packaging/homebrew/Formula/*.rb` into the tap repo's
   `Formula/` directory.
3. `brew audit --new --strict --online schwartzkamel/az-ai/az-ai-v2`
   before announcing.
4. `brew install --build-from-source schwartzkamel/az-ai/az-ai-v2`
   on a fresh machine (Puddy gate).

## Verify locally (pre-publish)

```sh
brew audit --strict ./packaging/homebrew/Formula/az-ai.rb
brew install --build-from-source --formula ./packaging/homebrew/Formula/az-ai.rb
az-ai-v2 --version --short
```

Expected `--version --short` output is whatever the filename-drift
comment in the matching formula says (v2.0.4 reports `2.0.2` until
v2.0.5 lands).

## Hash provenance

SHA256s embedded in each formula come from the GitHub Release's
`digests.txt` artifact (produced by the `Compute digests` step in
`.github/workflows/release.yml`). The tag-time ritual in
[`packaging/README.md`](../README.md) §Tag-time-ritual is the
single source of truth -- if a formula's digest disagrees with
`digests.txt` for its tag, **the release is broken, not the
formula**. Stop the publish and file a fix-forward.
