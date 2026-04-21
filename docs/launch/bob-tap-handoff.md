# Bob's tap + bucket hand-off

> "You need it on Homebrew? I know a guy. Scoop bucket? I know a guy.
> Repos are scaffolded and staged — one push when Lippman's hash-sync
> lands and we're live. Next problem." — Bob Sacamano

This document is the operational runbook for publishing the Homebrew
tap and Scoop bucket for **Azure OpenAI CLI v2** (`az-ai-v2`). It is
a **two-phase** workflow because the manifests cannot be published
until the release artifacts exist on GitHub Releases and their SHA256
digests have been back-filled into `packaging/` by Mr. Lippman.

- **Target repos (public, under SchwartzKamel):**
  - `https://github.com/SchwartzKamel/homebrew-tap`
  - `https://github.com/SchwartzKamel/scoop-bucket`
- **Local staging (phase 1 already committed):**
  - `/tmp/bob-tap-prep/homebrew-tap/` — scaffolded, 1 commit
  - `/tmp/bob-tap-prep/scoop-bucket/` — scaffolded, 1 commit
- **Owner:** Bob Sacamano (ecosystem/distribution)
- **Blocker:** awaiting upstream commit `release: v<x>.<y>.<z> post-publish hash sync` on `main`

## Current status

| Item | State |
| --- | --- |
| `gh` CLI available in sandbox | ❌ not installed |
| Phase 1 scaffolds committed locally | ✅ |
| Phase 2 (formula/manifest copy) | ⏳ blocked on hash-sync |
| Remote repos created | ❌ needs the person with a `gh` login |
| Initial push | ❌ (requires remote creation) |

Todo `bob-tap-standup` is **blocked** in the session DB with reason
"auth unavailable for gh repo create; handoff doc staged at
docs/launch/bob-tap-handoff.md".

## Why two phases

Homebrew and Scoop will both refuse (or worse, silently mis-install)
manifests that point at tarballs with placeholder or wrong digests.
The upstream formulae and JSON manifests in `packaging/` currently
carry the literal string `TODO_FILL_AT_RELEASE_TIME` for every
SHA256 field. Lippman's post-publish hash-sync commit replaces those
strings with real digests computed from the published tarballs. Only
after that commit lands is it safe to copy the files into the public
tap/bucket repos.

**Never** publish a manifest containing `TODO_FILL_AT_RELEASE_TIME`.

## Phase 1 — Scaffolding (done)

Already committed locally in `/tmp/bob-tap-prep/`:

- `homebrew-tap/` — `LICENSE` (MIT, copied from upstream), `README.md`
  (tap install instructions), empty `Formula/` with `.gitkeep`
- `scoop-bucket/` — `LICENSE` (MIT), `README.md` (bucket install
  instructions), empty `bucket/versions/` with `.gitkeep`

Both repos are `git init`'d on `main` with one commit each.

## Phase 2 — Fill + publish

### Step 2.0 — Wait for the hash-sync commit

```sh
cd /home/tweber/tools/azure-openai-cli
git fetch origin
git log origin/main -1 --format='%h %s'
# Look for: "release: v2.0.0 post-publish hash sync" (or equivalent)

# Sanity check: placeholders must be gone from all four files
grep -c TODO_FILL_AT_RELEASE_TIME \
  packaging/homebrew/Formula/az-ai.rb \
  packaging/homebrew/Formula/az-ai-v2@2.0.0.rb \
  packaging/scoop/az-ai.json \
  packaging/scoop/versions/az-ai-v2@2.0.0.json
# Expected output: all four lines end in ":0"
```

**Do not proceed** if any file still reports a non-zero placeholder
count.

### Step 2.1 — Copy filled manifests into the prep repos

Filename mapping (upstream → published):

| Upstream path | Published path |
| --- | --- |
| `packaging/homebrew/Formula/az-ai.rb` | `Formula/az-ai-v2.rb` |
| `packaging/homebrew/Formula/az-ai-v2@2.0.0.rb` | `Formula/az-ai-v2@2.0.0.rb` |
| `packaging/scoop/az-ai.json` | `bucket/az-ai-v2.json` |
| `packaging/scoop/versions/az-ai-v2@2.0.0.json` | `bucket/versions/az-ai-v2@2.0.0.json` |

The Homebrew main formula is renamed (`az-ai.rb` → `az-ai-v2.rb`) so
that `brew install az-ai-v2` resolves directly. The formula class
name is already `AzAiV2` (and `AzAiV2AT200` for the pinned sibling),
which is the required camel-case for `brew install az-ai-v2` /
`brew install az-ai-v2@2.0.0`. The Scoop main manifest is renamed
(`az-ai.json` → `az-ai-v2.json`) so that `scoop install az-ai-v2`
resolves directly.

```sh
SRC=/home/tweber/tools/azure-openai-cli
TAP=/tmp/bob-tap-prep/homebrew-tap
BUC=/tmp/bob-tap-prep/scoop-bucket

# Tap
rm -f "$TAP/Formula/.gitkeep"
cp "$SRC/packaging/homebrew/Formula/az-ai.rb"           "$TAP/Formula/az-ai-v2.rb"
cp "$SRC/packaging/homebrew/Formula/az-ai-v2@2.0.0.rb"  "$TAP/Formula/az-ai-v2@2.0.0.rb"

# Bucket
rm -f "$BUC/bucket/.gitkeep"
cp "$SRC/packaging/scoop/az-ai.json"                              "$BUC/bucket/az-ai-v2.json"
cp "$SRC/packaging/scoop/versions/az-ai-v2@2.0.0.json"            "$BUC/bucket/versions/az-ai-v2@2.0.0.json"

# Paranoia: make sure no placeholders snuck through
grep -R TODO_FILL_AT_RELEASE_TIME "$TAP" "$BUC" && echo "ABORT: placeholders present" || echo "clean"
```

### Step 2.2 — Commit phase 2

```sh
cd /tmp/bob-tap-prep/homebrew-tap
git add -A
git commit -m "feat: publish az-ai-v2 formula — phase 2

- Formula/az-ai-v2.rb           (latest, copied from packaging/homebrew/Formula/az-ai.rb)
- Formula/az-ai-v2@2.0.0.rb     (pinned, copied verbatim)
All SHA256s come from upstream hash-sync commit on SchwartzKamel/azure-openai-cli.
"

cd /tmp/bob-tap-prep/scoop-bucket
git add -A
git commit -m "feat: publish az-ai-v2 manifest — phase 2

- bucket/az-ai-v2.json                      (latest, copied from packaging/scoop/az-ai.json)
- bucket/versions/az-ai-v2@2.0.0.json       (pinned, copied verbatim)
All SHA256s come from upstream hash-sync commit on SchwartzKamel/azure-openai-cli.
"
```

### Step 2.3 — Create + push remote repos (needs `gh` auth as SchwartzKamel)

If `gh` is not installed, install it first
(<https://cli.github.com/>) and authenticate:

```sh
gh auth login
gh auth status   # verify account: SchwartzKamel
```

Then:

```sh
# Homebrew tap
gh repo create SchwartzKamel/homebrew-tap \
  --public \
  --description "Homebrew tap for az-ai-v2 — Azure OpenAI CLI v2" \
  --source /tmp/bob-tap-prep/homebrew-tap \
  --remote origin \
  --push

# Scoop bucket
gh repo create SchwartzKamel/scoop-bucket \
  --public \
  --description "Scoop bucket for az-ai-v2 — Azure OpenAI CLI v2" \
  --source /tmp/bob-tap-prep/scoop-bucket \
  --remote origin \
  --push
```

Expected output for each: `✓ Created repository SchwartzKamel/<name>
on GitHub` followed by `To https://github.com/SchwartzKamel/<name>`
and branch `main -> main`.

If the repos already exist (someone created them manually), swap the
`gh repo create` command for:

```sh
cd /tmp/bob-tap-prep/homebrew-tap
git remote add origin https://github.com/SchwartzKamel/homebrew-tap.git
git push -u origin main

cd /tmp/bob-tap-prep/scoop-bucket
git remote add origin https://github.com/SchwartzKamel/scoop-bucket.git
git push -u origin main
```

### Step 2.4 — Verify

```sh
# Homebrew (on a Mac or Linux workstation with Homebrew)
brew untap schwartzkamel/tap 2>/dev/null || true
brew tap schwartzkamel/tap
brew tap-info schwartzkamel/tap           # should list 2 formulae
brew info az-ai-v2                        # should print version + URLs + digests
brew install az-ai-v2                     # full install
az-ai-v2 --version                        # sanity
brew uninstall az-ai-v2

# Scoop (on Windows / PowerShell)
scoop bucket rm schwartzkamel 2>$null
scoop bucket add schwartzkamel https://github.com/SchwartzKamel/scoop-bucket
scoop bucket list                         # should include "schwartzkamel"
scoop search az-ai-v2                     # should find the manifest
scoop install az-ai-v2
az-ai-v2 --version
scoop uninstall az-ai-v2
```

Puddy owns the post-publish verification pass (fresh machine, no
stale caches). Nothing in the README advertises these install paths
until Puddy signs off.

### Step 2.5 — Close the loop upstream

Once both repos are live and verified, commit this note's
twin-entry:

```sh
cd /home/tweber/tools/azure-openai-cli
# Edit docs/launch/bob-tap-handoff.md to flip the "Current status"
# table rows to ✅ and record the commit SHAs of the phase-2 commits
# in tap/ and bucket/ plus the workflow run that unblocked us.
git add docs/launch/bob-tap-handoff.md
git commit -m "docs(packaging): tap + bucket live — az-ai-v2 brew/scoop install paths published

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
git push origin main
```

And in the session DB:

```sql
UPDATE todos SET status='done' WHERE id='bob-tap-standup';
```

## Notes for future releases

- The release pipeline should grow a job that opens PRs against
  `SchwartzKamel/homebrew-tap` and `SchwartzKamel/scoop-bucket` with
  the new-version manifests + digests, so this whole dance becomes
  "merge the bot PR". Until then: Bob's cadence is manual.
- When the `az-ai-v2` binary is eventually renamed to `az-ai` (see
  `_comment_bin` in `packaging/scoop/az-ai.json`), the bucket
  filename stays `az-ai-v2.json` for continuity. A new `az-ai.json`
  manifest is added alongside, not instead of.
- `homebrew-core` / `scoopinstaller/extras` upstreaming is a later
  play (per `AGENTS.md`: "until then, tap/bucket form"). Jackie
  clears trademark and attribution before any upstream PR lands.

---

*I know a guy.*
