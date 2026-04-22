# v2 Cutover Checklist (Phase 6)

**Owners:** Mr. Wilhelm (process) + Mr. Lippman (release)
**Status:** Pending -- do not execute until all preconditions in §1 are green
**Companion docs:** [`v2-migration.md`](v2-migration.md) · [`v2-dogfood-plan.md`](v2-dogfood-plan.md) · [`adr/ADR-004-agent-framework-adoption.md`](adr/ADR-004-agent-framework-adoption.md)

> The gate is the gate. The gate is *there* for a reason. We're going to press -- but not before every item below is checked, signed, and dated. No back-channel merges. No "just this once." -- Wilhelm & Lippman

---

## 1. Preconditions

The cutover **must not start** until all of the following are true. Owner initials go in the release PR checklist.

- [ ] Phase 5 (Observability) shipped to `main` with OTel + Morty's cost schema behind opt-in flag. Owner: Frank + Morty.
- [ ] Phase 7 dogfood window complete per [`v2-dogfood-plan.md`](v2-dogfood-plan.md) exit criteria -- **this is non-negotiable**. Owner: FDR + Bania.
- [ ] All `azureopenai-cli-v2/` unit + integration tests green on latest `main` (CI shows green `Test v2` step).
- [ ] Bania perf deltas vs 1.9.1 within budget (cold start ≤ +10%, TTFT ≤ +5ms, stream throughput ≤ −5%, AOT binary ≤ 15 MB).
- [ ] FDR chaos drill: zero P1 findings outstanding, any P2 triaged with owners.
- [ ] Newman security sign-off on the v2 tool hardening adapter layer.
- [ ] Jackie licensing scan across the MAF dependency graph -- attribution file updated, no GPL contagion.
- [ ] Elaine: migration guide draft ready for the GitHub Release body.
- [ ] Costanza go/no-go signed in release PR.

---

## 2. Branching reality

### The situation (as of 2026-04-20)

The original [`v2-migration.md`](v2-migration.md) describes a "`v2` branch forked from `main`" that will be renamed into `main` at cutover. **That is not what actually happened.** Both v1 and v2 live side by side on `main`:

```
azureopenai-cli/            ← v1 project (AzureOpenAI_CLI.csproj, 1.9.1)
azureopenai-cli-v2/         ← v2 project (AzureOpenAI_CLI_V2.csproj, 2.0.0-alpha.1)
tests/AzureOpenAI_CLI.Tests/
tests/AzureOpenAI_CLI.V2.Tests/
```

Both projects are registered in [`azure-openai-cli.sln`](../azure-openai-cli.sln). CI builds and tests both. `Dockerfile`, `Makefile`, and `.github/workflows/release.yml` publish **only the v1 project** today.

The cutover is therefore a **project rename/swap on `main`**, not a branch rename.

### Options considered

| Option | Summary | Verdict |
|--------|---------|---------|
| **(a)** Delete `azureopenai-cli/`, rename `azureopenai-cli-v2/` → `azureopenai-cli/`. Snapshot v1 to a `v1-legacy` branch for hotfix window. | One published binary, one source tree, clean mental model. Rollback = restore from `v1-legacy`. | **Recommended.** |
| (b) Keep both trees on `main`, flip the default published artifact to v2, mark v1 deprecated. | Lower-risk day-of, but doubles CI time forever, forces every contributor to reason about two projects, and invites drift. Also leaves the packaging story ambiguous. | Rejected. |

**Rationale for (a):** v2 is a superset of v1 by design (hot-path contract preserved, same flags, same `.squad/` format). The only reason to keep v1 in-tree is for hotfix velocity during the maintenance window, and a dedicated branch serves that purpose more cleanly than parallel sibling directories. Single-binary releases also protect Morty's cost budgets (no double-SBOM, no double-Trivy, no double-signing).

**Rollback posture:** `v1-legacy` branch + retagged `1.9.1` container image are the two anchors. See §5.

---

## 3. Step-by-step cutover checklist

Every step has an owner and a validation command. Do not mark done without running the validation.

### 3.1 Pre-cutover -- establish rollback anchors

| # | Action | Owner | Validation |
|---|--------|-------|------------|
| 1 | Cut `v1-legacy` branch from current `main` HEAD: `git switch -c v1-legacy && git push -u origin v1-legacy` | Jerry | `git ls-remote origin v1-legacy` |
| 2 | Apply branch protection to `v1-legacy` (require PR, require `build-and-test`, restrict force-push) | Wilhelm | GitHub UI -- branch settings screenshot in release PR |
| 3 | Retag current `ghcr.io/schwartzkamel/azure-openai-cli:latest` as `:1.9.1` (explicit lockdown of the last v1 image) | Jerry | `docker buildx imagetools inspect ghcr.io/schwartzkamel/azure-openai-cli:1.9.1` |
| 4 | Snapshot current Homebrew/Scoop/Nix files into `packaging/legacy/v1.9.1/` for trivial rollback | Bob Sacamano | `git log packaging/legacy/v1.9.1/` |
| 5 | Open the cutover tracking PR in draft; enable merge queue gating | Wilhelm | PR link posted to Discussions |

### 3.2 Project swap

All work happens on branch `release/2.0.0` cut from `main`.

| # | Action | Owner | Validation |
|---|--------|-------|------------|
| 6 | `git rm -r azureopenai-cli/ tests/AzureOpenAI_CLI.Tests/` | Kramer | `test ! -d azureopenai-cli` |
| 7 | `git mv azureopenai-cli-v2 azureopenai-cli` and `git mv tests/AzureOpenAI_CLI.V2.Tests tests/AzureOpenAI_CLI.Tests` | Kramer | File exists at new path with old name (`test -f azureopenai-cli/AzureOpenAI_CLI_V2.csproj`), ready for §3.3 step 8 rename |
| 8 | Rename `azureopenai-cli/AzureOpenAI_CLI_V2.csproj` → `azureopenai-cli/AzureOpenAI_CLI.csproj`; rename test csproj analogously; rename `AssemblyName` / `RootNamespace` inside the csproj if set | Kramer | `grep -l AzureOpenAI_CLI_V2` must return no csproj hits |
| 9 | Update [`azure-openai-cli.sln`](../azure-openai-cli.sln): delete the two V2 entries (`{B2C3...}`, `{C3D4...}`), delete the two v1 entries, re-add a single `AzureOpenAI_CLI` + `AzureOpenAI_CLI.Tests` pair pointing at the new paths. Use `dotnet sln` not hand-editing. | Kramer | `dotnet sln list` shows exactly two projects |
| 10 | Update [`Dockerfile`](../Dockerfile) -- path is already `azureopenai-cli/AzureOpenAI_CLI.csproj`, so no change needed after the rename. Confirm: | Jerry | `docker build -t az-ai:cutover-smoke .` succeeds |
| 11 | Update [`Makefile`](../Makefile) -- all `azureopenai-cli/AzureOpenAI_CLI.csproj` references already match; sweep for `V2`/`v2` stragglers (test targets, bench scripts) | Jerry | `grep -nE '_V2|azureopenai-cli-v2' Makefile` returns nothing |
| 12 | Update `.github/workflows/ci.yml` -- collapse `Test v1` + `Test v2` into a single `Test` step pointing at `tests/AzureOpenAI_CLI.Tests/AzureOpenAI_CLI.Tests.csproj` | Jerry | workflow dry-run via `act` or push-to-branch CI green |
| 13 | Update `.github/workflows/release.yml` -- already references `azureopenai-cli/AzureOpenAI_CLI.csproj`; confirm SBOM + publish matrix still resolve | Jerry | `grep -nE '_V2|azureopenai-cli-v2' .github/workflows/` returns nothing |
| 14 | Sweep repo for stale references: `grep -rnE 'azureopenai-cli-v2\|AzureOpenAI_CLI_V2\|V2\.Tests' -- ':!docs/v2-*.md' ':!CHANGELOG.md'` -- each hit either updated or explicitly whitelisted in the PR description | Kramer + Elaine | Clean grep output |

### 3.3 Package identity

| # | Action | Owner | Validation |
|---|--------|-------|------------|
| 15 | Keep binary name `az-ai` (install target, Homebrew alias, Scoop `bin` mapping -- all unchanged) | Mr. Lippman | `bin.install "AzureOpenAI_CLI" => "az-ai"` unchanged in formula |
| 16 | Bump `<Version>` in `azureopenai-cli/AzureOpenAI_CLI.csproj` from `2.0.0-alpha.1` to `2.0.0` | Mr. Lippman | `grep '<Version>2.0.0</Version>' azureopenai-cli/AzureOpenAI_CLI.csproj` |
| 17 | Update Docker image `org.opencontainers.image.version` label (if present) to `2.0.0` | Jerry | `docker inspect --format '{{index .Config.Labels "org.opencontainers.image.version"}}' az-ai:cutover-smoke` |

### 3.4 CHANGELOG

| # | Action | Owner | Validation |
|---|--------|-------|------------|
| 18 | Add `## [2.0.0] - <date>` section. **Headline** under **Changed**: "Adopted Microsoft Agent Framework (MAF) -- `ChatClientAgent`, `Workflow`+`CheckpointManager`, `AgentSession`+`AIContextProvider` replace ~2200 LOC of hand-rolled harness. Zero CLI surface changes. See [ADR-004](docs/adr/ADR-004-agent-framework-adoption.md)." Include Added (opt-in telemetry flag, OTel spans, per-request cost hook), Changed (dependency graph now includes `Microsoft.Agents.AI.*`), Removed (dual-runtime `--agent-runtime` if not shipped; legacy squad session loader). Link the migration guide. | Mr. Lippman | Preview render on the PR |
| 19 | Call out breaking changes loudly -- even if user-invisible, library consumers may see API surface drift | Mr. Lippman | Section starts "### ⚠️ Breaking changes" |
| 20 | Date the section on the day the tag is cut, not the day the PR is opened | Mr. Lippman | Visual inspection |

### 3.5 Docker

| # | Action | Owner | Validation |
|---|--------|-------|------------|
| 21 | On tag push `v2.0.0`, release workflow publishes `ghcr.io/schwartzkamel/azure-openai-cli:2.0.0` and moves `:latest` to point at the new digest | Jerry | `docker buildx imagetools inspect ghcr.io/schwartzkamel/azure-openai-cli:2.0.0` |
| 22 | Confirm `:1.9.1` tag (set in §3.1 step 3) still exists and resolves to the prior digest | Jerry | `docker buildx imagetools inspect ghcr.io/schwartzkamel/azure-openai-cli:1.9.1` |
| 23 | Trivy HIGH/CRITICAL scan on `:2.0.0` returns no new findings vs `:1.9.1` | Newman | Scan report attached to release PR |

### 3.6 Packaging updates (Bob Sacamano owns upstream PRs)

| # | Action | Owner | Validation |
|---|--------|-------|------------|
| 24 | [`packaging/homebrew/Formula/az-ai.rb`](../packaging/homebrew/Formula/az-ai.rb): bump `version "2.0.0"`, update all three `url` + `sha256` entries, update `test do assert_match "2.0.0"` | Bob | `brew audit --new-formula packaging/homebrew/Formula/az-ai.rb` locally |
| 25 | [`packaging/scoop/az-ai.json`](../packaging/scoop/az-ai.json): bump `version`, update `url` + `hash` for win-x64 zip | Bob | Scoop manifest JSON schema validation |
| 26 | [`packaging/nix/flake.nix`](../packaging/nix/flake.nix): bump `version`, update all `sha256` entries; add `linux-aarch64` if the release pipeline emits it this cycle | Bob | `nix flake check` |
| 27 | Open upstream PRs to homebrew-core / scoop-extras *after* the GitHub Release is published and tarball SHAs are pinned to the signed attestation | Bob | PR links posted to release PR |

### 3.7 Release notes

| # | Action | Owner | Validation |
|---|--------|-------|------------|
| 28 | Draft GitHub Release body from the CHANGELOG 2.0.0 section. Lead with the MAF headline. Include an "Upgrade notes" callout (none for Espanso/AHK; library consumers review ADR-004) and a "Rollback" pointer to §5 | Mr. Lippman + Elaine | Draft preview URL in release PR |
| 29 | Link [ADR-004](adr/ADR-004-agent-framework-adoption.md) and [`v2-migration.md`](v2-migration.md) from the release notes | Elaine | Links render |
| 30 | SBOM + attestations attached to the release (inherited from `release.yml`) | Jerry | Release assets list includes `.sbom.json` for each RID |

### 3.8 Announcement (coordinated)

| # | Action | Owner | Validation |
|---|--------|-------|------------|
| 31 | Hero copy + launch post drafted | Peterman | Draft linked in release PR |
| 32 | Community notice -- Discussions post, welcome thread for early v2 reports | Uncle Leo | Post URL |
| 33 | Conference-talk delta (if a CFP references a version) | Keith Hernandez | Slide delta or "N/A" |
| 34 | Announcement fires *after* tag is published and binaries are signed/verified | Mr. Lippman | Timestamped in the release PR |

---

## 4. Validation gates -- must all pass before Phase 6 is marked done

Run in order. Every command below must exit 0 or its artifact must match the stated criterion.

```bash
# Gate 1: preflight (format + build + unit + integration)
make preflight

# Gate 2: container
docker build -t azure-openai-cli:2.0.0 .
docker run --rm azure-openai-cli:2.0.0 --version --short    # prints 2.0.0

# Gate 3: integration against a real Azure endpoint
AZUREOPENAIENDPOINT=... AZUREOPENAIAPI=... AZUREOPENAIMODEL=gpt-4o-mini \
  bash tests/integration_tests.sh

# Gate 4: FDR chaos drill
bash tests/adversarial/run-chaos-drill.sh --baseline 1.9.1   # zero P1, P2 triaged

# Gate 5: Bania perf regression (⚠️ PLANNED -- see note below)
python scripts/bench.py --compare 1.9.1 --budget cold=10% --budget ttft=5ms \
       --budget stream=5% --budget binsize=15MB
```

> ⚠️ **Gate 5 is planned, not shipped.** The `--compare` / `--budget`
> surface above is the target CLI once [`bania-v2-03`] promotes the
> harness. Today's `scripts/bench.py` is a positional cold-start timer
> (`bench.py <binary> [-n RUNS] [-w WARMUP] [--args ...]`) with no
> compare or budget mode. Until `bania-v2-03` lands, Gate 5 is
> satisfied manually by re-running `python3 scripts/bench.py
> dist/aot/<bin>` against a 1.9.1 AOT build and eyeballing the deltas
> against [`docs/perf-baseline-v2.md`](perf-baseline-v2.md). Track:
> [`docs/audits/docs-audit-2026-04-22-bania.md`](audits/docs-audit-2026-04-22-bania.md) C2.

| Gate | Pass criterion | Signoff |
|------|----------------|---------|
| 1 | `make preflight` exit 0 | Soup Nazi |
| 2 | `docker build` exit 0; `--version` prints `2.0.0` | Jerry |
| 3 | Integration tests green against real endpoint | Puddy |
| 4 | FDR chaos: zero P1 | FDR |
| 5 | Bania: all budgets green vs 1.9.1 baseline | Bania |

**Any amber = no-go.** Lippman blocks the tag.

---

## 5. Rollback plan

Rollback is a sequence of inverse steps against the anchors set in §3.1. It is designed to take < 30 minutes from decision to restored `main`.

### 5.1 Rollback triggers (any one)

- P0/P1 regression discovered post-tag that affects the hot path (`--raw`, streaming, Espanso/AHK).
- Security finding in MAF or a transitive MAF dependency that cannot be mitigated in ≤ 24h.
- Data-corruption report against `.squad/` files caused by v2 `AIContextProvider`.

### 5.2 Rollback procedure

| # | Action | Owner | Validation |
|---|--------|-------|------------|
| R1 | `git switch v1-legacy && git switch -c rollback/1.9.2 && git merge --ff-only v1-legacy` | Jerry | `git log --oneline -1` matches pre-cutover SHA |
| R2 | Apply the single hotfix that forced the rollback (if any) as a separate commit | Kramer | Hotfix PR linked |
| R3 | Bump csproj to `1.9.2`, add CHANGELOG entry under `## [1.9.2]` with "Rollback of 2.0.0 -- see §5 of v2-cutover-checklist" | Mr. Lippman | `grep '<Version>1.9.2</Version>'` |
| R4 | Reset `main` to the rollback branch: merge via PR with "rollback" label; squash disabled | Wilhelm | `main` HEAD = rollback SHA |
| R5 | Retag `ghcr.io/...:latest` back to the `:1.9.1` digest (set aside in §3.1 step 3); publish `:1.9.2` after hotfix merges | Jerry | `docker buildx imagetools inspect ...:latest` |
| R6 | Cut GitHub Release `v1.9.2` with a "Rollback Notice" section citing trigger + ADR-004 status change to "Rejected post-implementation" | Mr. Lippman | Release page URL |
| R7 | Packaging: restore from `packaging/legacy/v1.9.1/` snapshot, bump to 1.9.2, re-open upstream PRs | Bob | Upstream PR links |
| R8 | Communicate: Peterman drafts a short, factual rollback note; Uncle Leo pins it in Discussions; no marketing spin | Peterman + Uncle Leo | Post URLs |
| R9 | Schedule post-incident retro per Wilhelm's cadence; Frank owns the incident report | Wilhelm + Frank | Retro invite sent |

### 5.3 Point of no return

Once `v1-legacy` has been deleted *or* 30 days have passed since the 2.0.0 tag, rollback is no longer fast. After that window, any revert is treated as a new major-version down-migration and requires its own ADR.

---

## 6. Communications plan

| Channel | Message | Timing | Owner |
|---------|---------|--------|-------|
| GitHub Release (v2.0.0) | Full release notes + migration guide + ADR link | T+0 (tag publish) | Mr. Lippman |
| README.md | Version badge bump + "What's new in 2.0" callout | T+0 | Elaine |
| Discussions -- Announcements | "v2.0.0 is out" thread; invite early reports | T+0 | Uncle Leo |
| Discussions -- Migration | FAQ stub -- "Do I need to change anything?" (answer: no for CLI users) | T+0 | Elaine + Uncle Leo |
| Issue tracker | Pin "v2.0 known issues" meta-issue; triage P1s in ≤ 4h during first week | T+0 through T+7d | Puddy |
| Blog / launch copy (if any) | Peterman's hero post | T+1 (next business day) | Peterman |
| Conference / talk decks | Version bump slide | T+7 or next rehearsal | Keith Hernandez |

Do **not** pre-announce. The announcement fires after tag publish and binary attestation verification.

---

## 7. Post-cutover cleanup

Execute within two weeks of the 2.0.0 tag.

- [ ] Remove the "v1 vs v2" test-matrix split from `ci.yml` -- one test job only.
- [ ] Archive `.github/workflows/` references to `V2.Tests` if any remain.
- [ ] Sweep docs for `azureopenai-cli-v2` / "v2 branch" language -- update to historical tense ("v2 was merged in 2.0.0"). Elaine owns the sweep.
- [ ] Move [`docs/v2-migration.md`](v2-migration.md), this checklist, and [`docs/v2-dogfood-plan.md`](v2-dogfood-plan.md) into `docs/history/` -- they are artifacts now, not planning docs.
- [ ] Freeze `v1-legacy`: retain branch for 180 days for hotfixes, then archive (not delete) via GitHub branch archive.
- [ ] Update [`docs/v2-migration.md`](v2-migration.md) phase table: Phase 6 and 7 → ✅ Done with commit SHAs.
- [ ] Retire the `--agent-runtime native|af` flag (if it shipped) at v2.1 per the open question in [`v2-migration.md`](v2-migration.md).
- [ ] Bania publishes the post-cutover "state of the bench" report comparing 2.0.0 vs 1.9.1.
- [ ] Frank publishes the first post-2.0 SLO snapshot.
- [ ] Wilhelm schedules the post-cutover retro (mandatory, not optional).

---

**Maintained by:** Wilhelm (process) + Mr. Lippman (release)
**Review cadence:** Before every cutover rehearsal; after cutover execution
**Predecessor:** [`v2-migration.md`](v2-migration.md) · [`v2-dogfood-plan.md`](v2-dogfood-plan.md)
