# Pre-release checklist

> "This isn't ready. Fix it or pull it from the release." -- Mr. Lippman

Audience: whoever is cutting the next tag on the v2 line. Copy this
table into the release PR description, tick each row as you go, and
do not cut the tag while a row is red. If a row does not apply
(e.g. no persona changes this cycle), write `n/a` and say why -- don't
leave it blank.

**Scope.** v2 line. v1 line uses a trimmed version -- see
[`../runbooks/release-runbook.md`](../runbooks/release-runbook.md).

Companion docs:

- [`semver-policy.md`](semver-policy.md) -- how to pick the bump.
- [`ghcr-tag-lifecycle.md`](ghcr-tag-lifecycle.md) -- what happens to
  container tags at publish time.
- [`artifact-inventory.md`](artifact-inventory.md) -- the assets this
  checklist is validating.
- [`../CHANGELOG-style-guide.md`](../CHANGELOG-style-guide.md) -- prose
  rules for the CHANGELOG entry this release lands.
- [`../runbooks/release-runbook.md`](../runbooks/release-runbook.md)
  -- end-to-end release flow (this checklist is §1 of that).
- [`../runbooks/packaging-publish.md`](../runbooks/packaging-publish.md)
  -- Bob's post-release tap/bucket publish flow.

---

## Variables

Fill these in at the top of the release PR. Every command on this
page references them.

```bash
export VERSION=2.0.6                      # example; no 'v' prefix
export TAG=v${VERSION}
export DATE=$(date -u +%Y-%m-%d)          # UTC, ISO 8601
export REPO=SchwartzKamel/azure-openai-cli
```

---

## The table

20 rows. Run top to bottom. Do not reorder -- later rows assume earlier
rows passed.

| #  | Gate                              | Command / check                                                                                                                                                       | Owner       | Pass criterion                                                                                              |
|----|-----------------------------------|-----------------------------------------------------------------------------------------------------------------------------------------------------------------------|-------------|-------------------------------------------------------------------------------------------------------------|
| 1  | Clean working tree on `main`      | `git -c commit.gpgsign=false pull --rebase origin main && git status`                                                                                                 | author      | "working tree clean", on `main`, up-to-date with `origin/main`.                                             |
| 2  | SemVer bump justified in PR       | Link in PR body to [`semver-policy.md`](semver-policy.md) §3, state which row applies.                                                                                | author      | Reviewer signs off on the bump class before anything else on this table runs.                               |
| 3  | Version bumped in csproj          | `grep -c "<Version>${VERSION}</Version>" azureopenai-cli-v2/AzureOpenAI_CLI_V2.csproj`                                                                                | author      | Exactly `1`.                                                                                                |
| 4  | Version strings single-sourced    | `./packaging/scripts/verify-version-strings.sh "$TAG"` (lands with v2.0.5; see audit H-1).                                                                            | author      | Exit 0. Guards `stage.sh`, `Program.cs`, `Telemetry.cs`, `integration_tests.sh` against the v2.0.2-era drift. |
| 5  | Format check                      | `dotnet format azure-openai-cli.sln --verify-no-changes`                                                                                                              | author      | Exit 0, no diff.                                                                                            |
| 6  | v1 test suite green               | `dotnet test tests/AzureOpenAI_CLI.Tests/AzureOpenAI_CLI.Tests.csproj --verbosity minimal`                                                                             | author      | 1025+ passed, 0 failed, 0 skipped without waiver.                                                           |
| 7  | v2 test suite green               | `dotnet test tests/AzureOpenAI_CLI.V2.Tests/AzureOpenAI_CLI.V2.Tests.csproj --verbosity minimal`                                                                       | author      | 490+ passed, 0 failed. Number grows every release -- do not ratchet down.                                    |
| 8  | Binary `--version` smoke          | `make publish-linux-x64 && ./artifacts/publish/linux-x64/az-ai-v2 --version --short`                                                                                  | author      | Prints exactly `${VERSION}\n`. ≤ 10 bytes, trailing LF.                                                     |
| 9  | Preflight integration gates       | `tests/integration_tests.sh` (Gate 2 asserts `--version`; Gate N asserts exit codes).                                                                                 | Puddy       | All gates green. Gate count = published baseline + any new gates landed this cycle.                         |
| 10 | CHANGELOG entry populated         | `[${VERSION}] -- ${DATE}` header present; `[Unreleased]` reset to skeleton (`### Added / Changed / Deprecated / Removed / Fixed / Security`).                          | author      | Entry follows [`CHANGELOG-style-guide.md`](../CHANGELOG-style-guide.md). Prose, not commit dump.            |
| 11 | Breaking changes called out       | If the bump is MAJOR or includes a `### Removed` / `### Deprecated`, a migration paragraph exists in the CHANGELOG banner.                                            | author      | Migration path is copy-paste-runnable or links to a doc that is.                                            |
| 12 | Persona / prompt diff reviewed    | `git diff ${LAST_TAG}..HEAD -- docs/prompts/ azureopenai-cli-v2/**/personas/`                                                                                         | Maestro     | Wording changes match SemVer §4. Tone changes noted in `### Changed`.                                       |
| 13 | Security sign-off                 | `grep -A2 "### Security" CHANGELOG.md` (current release block), threat-model delta reviewed.                                                                          | Newman      | Either `### Security` is populated and reviewed, or Newman confirms "no security-relevant change".          |
| 14 | Licensing / NOTICE up to date     | `NOTICE` reflects current direct-dependency set; new/dropped deps audited.                                                                                            | Jackie      | No GPL contagion; attribution current.                                                                      |
| 15 | Docs updated                      | README version refs, migration notes, persona docs, man page stub. `rg -n "${LAST_VERSION}" README.md docs/` returns only historical refs.                            | Elaine      | No stale version strings outside `CHANGELOG.md` and `docs/archive/`.                                        |
| 16 | CI green on release commit        | `gh run list --branch main --limit 1 --json conclusion,name` shows `ci.yml` success for the exact SHA you plan to tag.                                                | author      | `conclusion: success`. Not `in_progress`, not `failure`.                                                    |
| 17 | Tag dry-run                       | `git -c commit.gpgsign=false tag -a ${TAG} -m "${TAG}" --dry-run` (then real tag when the rest is green).                                                             | author      | Tag does not already exist on `origin`; no accidental retag.                                                |
| 18 | Release workflow monitored        | `gh workflow run release.yml -r ${TAG}` → follow `gh run watch`.                                                                                                      | author      | All 4 RIDs build, Docker publish-v2 succeeds, SBOM jobs succeed.                                            |
| 19 | Post-release artifact inventory   | Every row in [`artifact-inventory.md`](artifact-inventory.md) §2 shows up on the GitHub Release and/or GHCR. SBOM JSON attached per RID. Image digest recorded.       | author      | No missing assets. Sidecar `.sha256` per tarball / zip.                                                     |
| 20 | Hash-sync + announce drafted      | Hash-sync PR (Homebrew / Nix / Scoop) opened with SHA256s from the Release. Announce post draft in `docs/announce/` with demo-script verified (`make demo`).          | Lippman/Bob | PR up within T+2h per H-2. Announce held until Bob confirms tap/bucket publish per [`../runbooks/packaging-publish.md`](../runbooks/packaging-publish.md). |

---

## Sign-offs

Before the tag is pushed, the following roles must tick in the release
PR:

- [ ] **Puddy (QA)** -- gates 6, 7, 9 green.
- [ ] **Newman (security)** -- gate 13.
- [ ] **Jackie (licensing)** -- gate 14.
- [ ] **Elaine (docs)** -- gates 10, 11, 15.
- [ ] **Lippman (release)** -- SemVer call on gate 2, final go/no-go
      against the full table, tag push.

Missing a sign-off is a no-go. Lippman can override a no-go **only**
with a written note in the release PR explaining the risk accepted
and the follow-up ticket.

---

## No-go triggers (any one blocks the tag)

- A gate in the table above is red and has no recorded waiver.
- A test suite is skipped "just to ship" without a Puddy-signed waiver.
- CHANGELOG is a commit dump or missing a migration note for a
  breaking change.
- Version strings disagree with the tag (gates 3, 4, 8) -- this is
  C-1 / C-2 and it must never recur.
- A previous release's hash-sync is still open. Finish the tail of
  the last release before starting the next.
- Known regression vs. the last release's perf baseline with no
  waiver from Bania.

---

## Post-tag tail (not part of the gate, but not optional)

1. Reset `[Unreleased]` to a bare skeleton in the **next** commit on
   `main` (M-3 from the audit). This is the release commit's
   follow-up, not the release commit itself.
2. File the hash-sync PR within T+2h; merge within T+4h; tap/bucket
   open within T+6h (H-2 SLA). Bob owns the merge; Lippman owns the
   PR.
3. Publish the GitHub Release body with the image digest and SBOM
   links. Do not mutate the Release body after announce goes out
   except to fix typos.
4. If anything is wrong with the shipped artifact, **fix forward**.
   Never retag, never force-push, never delete a GHCR version tag.
   See [`ghcr-tag-lifecycle.md`](ghcr-tag-lifecycle.md) and the
   release-rollback runbook (H-3).

-- Mr. Lippman, release management
