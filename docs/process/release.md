# Release procedure

> *"Are you saying you want to go to press? Because I'm ready to go to press."*
> -- Mr. Lippman

Owned by **Mr. Lippman** (release manager). Docs sign-off by **Elaine**.
CI-side execution owned by **Jerry**. Security spot-check by **Newman**
before any external publish.

Cutting a release is mechanical when the precheck list is clean. This
document is the checklist. Read it once before your first cut; after that,
follow it step by step without improvising.

---

## 1. Precheck -- before tagging anything

Run every item below before you create the git tag. A missed item can
produce a release with wrong version numbers, empty release notes, or a
broken artifact. `make release-precheck` (see step 1f) automates most
of these, but read each row at least once so you know what it is checking.

### 1a. CI is green on `main`

All five workflows must be passing on the exact commit you intend to tag:

| Workflow | File | What it guards |
|----------|------|----------------|
| CI | `.github/workflows/ci.yml` | format, build, unit tests, integration tests, vuln scan |
| docs-lint | `.github/workflows/docs-lint.yml` | markdown lint, ASCII-only checks |
| SBOM | `.github/workflows/sbom.yml` | dependency manifest |
| OpenSSF Scorecard | `.github/workflows/scorecards.yml` | supply-chain posture |
| Automatic Dependency Submission | (Dependabot / dependency graph) | dependency graph freshness |

Check the badge row in the README or run:

```bash
gh run list --branch main --limit 10
```

Do not tag until all five show `completed / success` on the same commit SHA.

### 1b. `[Unreleased]` block in CHANGELOG.md is non-empty and reviewed

Open `CHANGELOG.md`. The `## [Unreleased]` section must contain at least
one bullet under a `### Added`, `### Changed`, `### Fixed`, `### Removed`,
or `### Security` heading. Empty `[Unreleased]` sections ship nothing and
produce confusing release notes.

Read the bullets. Confirm:

- Every user-visible change is described in one sentence a user can act on.
- No internal jargon that leaks implementation details without context.
- Breaking changes have a `### Changed` or `### Removed` bullet (not just
  a `### Added` for the replacement).

### 1c. `<Version>` in csproj matches the intended release version

```bash
grep '<Version>' azureopenai-cli/AzureOpenAI_CLI.csproj
```

The value must equal the version number you are about to tag -- e.g. `2.3.0`
for a `v2.3.0` tag. The release workflow reads this field; a mismatch
produces artifact filenames and `--version` output that disagree with the
tag.

The csproj version must also be *greater than* the most recent existing tag.
`make release-precheck` verifies this automatically.

### 1d. README install/download table version references match

The README contains a download table near the `## Install` section that
includes hardcoded version strings (e.g. `v2.2.0 shown`). Verify the
prose either (a) cites the version you are about to cut, or (b) uses a
`latest`-style reference that resolves correctly after the release.

```bash
grep -n 'v[0-9]\+\.[0-9]\+\.[0-9]\+' README.md | grep -i 'shown\|download\|release'
```

### 1e. No open CRITICAL or HIGH findings in `docs/findings-backlog.md`

```bash
grep -E '^\|.*\| (CRITICAL|HIGH) \|.*\| open ' docs/findings-backlog.md
```

A CRITICAL or HIGH finding in `open` state is a release blocker. Either
resolve it before tagging, file a CAB-lite record explaining why it is
safe to defer, or change its state to `deferred` with a one-sentence
rationale. Do not tag over an unacknowledged blocker.

### 1f. Run `make release-precheck`

```bash
make release-precheck
```

The target checks items 1c, 1d (version alignment), `[Unreleased]`
non-empty (1b), and findings-backlog severity (1e) in a single pass. It
prints a PASS/FAIL summary to stdout. A single FAIL line is a hard stop.

CI green (1a) is not checked by the Makefile target -- it requires a
GitHub API call. Verify CI manually via `gh run list` before proceeding.

---

## 2. Promote `[Unreleased]` to a versioned section in CHANGELOG.md

Edit `CHANGELOG.md`. Replace the `## [Unreleased]` heading with a dated
versioned heading and add a new empty `## [Unreleased]` above it:

Before:

```markdown
## [Unreleased]

### Added
- ...
```

After:

```markdown
## [Unreleased]

## [X.Y.Z] -- YYYY-MM-DD

### Added
- ...
```

Use ISO-8601 date (`YYYY-MM-DD`). Use ` -- ` as the separator (two hyphens
with spaces), consistent with recent entries. Do not use an en-dash or
em-dash -- ASCII only.

Leave the new `## [Unreleased]` section completely empty for now. You will
add a placeholder in step 6.

Commit this CHANGELOG update on `main` *before* tagging:

```bash
git add CHANGELOG.md
git commit -m "chore(release): promote [Unreleased] to v<X.Y.Z>

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
git push origin main
```

Wait for CI to pass on this commit before continuing.

---

## 3. Tag and push

Create an annotated tag pointing at the CHANGELOG-promotion commit:

```bash
git tag -a v<X.Y.Z> -m "Release v<X.Y.Z>"
git push origin v<X.Y.Z>
```

The tag name must be `v` followed by the version string with no extra
characters (`v2.3.0`, not `2.3.0`, not `v2.3.0-release`). The `release.yml`
workflow fires on `push: tags: ['v*']`.

---

## 4. CI takes over

After the tag push, `release.yml` runs automatically:

1. **CI gate** -- re-runs `ci.yml` against the tagged commit. Build fails
   if this gate fails.
2. **Build matrix** -- produces one artifact per RID:
   - `linux-x64` on `ubuntu-latest`
   - `linux-musl-x64` on `ubuntu-latest`
   - `osx-arm64` on `macos-14`
   - `win-x64` on `windows-latest`
3. **SBOM generation** -- CycloneDX SBOM per artifact, uploaded alongside
   the binary tarball/zip.
4. **GitHub attestation** -- provenance attestation generated via
   `actions/attest-build-provenance`.
5. **Digest roll-up** -- `digests.txt` containing SHA256 checksums for all
   artifacts.
6. **GitHub Release creation** -- release notes extracted from CHANGELOG.md,
   all artifacts attached.

Watch progress:

```bash
gh run list --workflow release.yml --limit 5
gh run watch <run-id>
```

Expected duration: 10-20 minutes for all four legs plus the release job.

---

## 5. Post-release verification

Do not announce the release until all of these pass.

### 5a. GitHub Release page

```bash
gh release view v<X.Y.Z>
```

Expected attachments:

- `az-ai-<X.Y.Z>-linux-x64.tar.gz`
- `az-ai-<X.Y.Z>-linux-musl-x64.tar.gz`
- `az-ai-<X.Y.Z>-osx-arm64.tar.gz`
- `az-ai-<X.Y.Z>-win-x64.zip`
- `digests.txt`
- One `*.sbom.json` per RID

### 5b. Digest spot check (optional but recommended)

Download the artifact for your local platform and verify the SHA256:

```bash
# Example: Linux x64
curl -LO https://github.com/SchwartzKamel/azure-openai-cli/releases/download/v<X.Y.Z>/az-ai-<X.Y.Z>-linux-x64.tar.gz
curl -LO https://github.com/SchwartzKamel/azure-openai-cli/releases/download/v<X.Y.Z>/digests.txt
sha256sum az-ai-<X.Y.Z>-linux-x64.tar.gz
grep linux-x64 digests.txt
```

The two SHA256 values must match exactly.

### 5c. Release notes review

```bash
gh release view v<X.Y.Z> --json body --jq '.body' | head -50
```

Verify the notes reflect the CHANGELOG entries you promoted in step 2.
If the body is wrong, the release asset contents are still correct -- the
notes can be edited on GitHub without re-tagging.

### 5d. Smoke-install on at least one platform

```bash
# Linux x64 example
curl -LO https://github.com/SchwartzKamel/azure-openai-cli/releases/download/v<X.Y.Z>/az-ai-<X.Y.Z>-linux-x64.tar.gz
tar xzf az-ai-<X.Y.Z>-linux-x64.tar.gz
./az-ai --doctor
./az-ai --version --short
```

`--version --short` must print exactly `<X.Y.Z>` (no `v` prefix).
`--doctor` must exit 0 (even without credentials -- it reports missing
creds as a warning, not a failure).

---

## 6. Open `[Unreleased]` for the next cycle

After successful smoke-install, prepare `main` for the next iteration:

### 6a. Add placeholder under `[Unreleased]`

Edit `CHANGELOG.md`. The empty `## [Unreleased]` section left in step 2
should get a placeholder so the format stays parseable:

```markdown
## [Unreleased]

<!-- next release notes go here -->
```

### 6b. Bump `<Version>` in csproj

Decide the next planned version and update `azureopenai-cli/AzureOpenAI_CLI.csproj`:

```xml
<Version>X.Y.Z+1-pre</Version>
```

For a patch release after `2.3.0`, use `2.3.1-pre`. For a minor after
`2.3.0`, use `2.4.0-pre`. The `-pre` suffix prevents the precheck's
version-is-greater guard from firing prematurely on the next run.

### 6c. Commit and push

```bash
git add CHANGELOG.md azureopenai-cli/AzureOpenAI_CLI.csproj
git commit -m "chore(post-release): open [Unreleased] for v<NEXT>, bump csproj to <NEXT>-pre

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
git push origin main
```

---

## Quick reference

```text
1. make release-precheck          -- all checks must PASS
2. Edit CHANGELOG.md              -- promote [Unreleased] to [X.Y.Z] -- DATE
3. git commit + push main         -- wait for CI green
4. git tag -a vX.Y.Z + push tag   -- triggers release.yml
5. gh run watch <run-id>          -- wait for all legs
6. gh release view vX.Y.Z         -- verify artifacts + notes
7. smoke-install on one platform  -- ./az-ai --doctor + --version --short
8. Edit CHANGELOG.md + csproj     -- open next [Unreleased], bump version
9. git commit + push main         -- done
```

---

## Troubleshooting

**`release.yml` failed on the CI gate.**
The tagged commit had a CI failure that was not present on `main` before
tagging. Common cause: a flaky integration test. Re-run:
`gh workflow run release.yml --ref v<X.Y.Z>`. If it fails again, investigate
the failure -- do not delete and re-push the tag unless the commit itself
needs a fix (a tag re-push changes provenance).

**One build leg failed (e.g. `win-x64`).**
Re-dispatch the workflow: `gh workflow run release.yml --ref v<X.Y.Z>`.
The workflow is idempotent; it will re-upload artifacts and overwrite the
existing GitHub Release if one was created.

**`make release-precheck` reports `[Unreleased] is empty`.**
Add at least one bullet to the appropriate section in CHANGELOG.md before
tagging. If this is a trivial release (dependency bump, CI fix), add a
`### Changed` or `### Fixed` bullet with a one-line description.

**Wrong version in csproj.**
Edit `azureopenai-cli/AzureOpenAI_CLI.csproj`, push to `main`, wait for CI,
then re-run `make release-precheck` before tagging.

---

## Provenance

Introduced in the post-S03 housekeeping pass. Owned going forward by
**Mr. Lippman** (release protocol) and **Elaine** (prose accuracy).
Update this doc when the release pipeline changes -- specifically when
the artifact matrix in `release.yml` gains or loses a leg.
