---
title: Docs Audit — Top-Level Narrative & Contributor Prose
auditor: Elaine (Technical Writer)
date: 2026-04-22
release_under_review: v2.0.4
scope:
  - README.md
  - ARCHITECTURE.md
  - CONTRIBUTING.md
  - IMPLEMENTATION_PLAN.md
  - AGENTS.md
  - CODE_OF_CONDUCT.md
  - CONTRIBUTORS.md
  - cross-check against .github/workflows/release.yml (current v2 matrix)
excluded_from_scope:
  - CHANGELOG.md (Mr. Lippman)
  - SECURITY.md (Newman)
  - THIRD_PARTY_NOTICES.md (Jackie Chiles)
  - NOTICE (Jackie Chiles)
files_reviewed: 7 (plus release.yml reference read)
severity_scale:
  Critical: User follows the doc → immediate failure (404, missing file, wrong artifact name). Ship-blocker in doc terms.
  High: User follows the doc → significant confusion or wrong mental model of the current release.
  Medium: Doc is technically reachable but stale, internally contradictory, or missing a signpost readers will predictably look for.
  Low: Rough edge. A careful reader notices; a casual reader doesn't.
  Nit: Style, voice, or wording. No semantic harm.
---

# Docs Audit — 2026-04-22 — Elaine

Project cut v2.0.4 this release. The binary matrix shrank (macOS x64 dropped,
macos-13 runner retired, only four RIDs ship). The top-level prose has not
kept up. The worst of it: the README tells readers to download artifacts
under names the release workflow no longer produces. The roadmap doc stops
at v1.8.0-Unreleased and plans a v1.9.0 that never happened. ARCHITECTURE.md
is a v1 document with a v2 badge on the README that links to it.

Clarity is queen. Two of our three most-visited docs lie to the reader on
first read. Fix the Criticals first; the rest is stewardship.

---

## Summary

| Severity      | Count | Where the damage lands                              |
|---------------|------:|------------------------------------------------------|
| Critical      |     3 | README Install table, README Quickstart, README artifact names |
| High          |     5 | IMPLEMENTATION_PLAN wholesale stale; ARCHITECTURE v1-only; credential-model contradiction; CoC/CONTRIBUTING reporting channel conflict; "v1.9.x upgrade" nonsense |
| Medium        |     5 | Stale test count, orphaned v2 docs, heading hierarchy, `--help` flag gaps in ARCHITECTURE, "New in v2.0.0" heading |
| Low           |     4 | CONTRIBUTORS roster, directory tree gaps, misleading "arm64" platform blurb, AGENTS diagram lint |
| Informational |     2 | Orphaned `docs/` set; CONTRIBUTING voice ownership |
| Nits          |     2 | Prose tightening opportunities                     |
| **Total**     | **21**|                                                      |

---

## Critical

### C1 — README `Install → Pre-built binaries` table ships platforms the release workflow does not produce

**File:** `README.md:129-135`
**Evidence:** `.github/workflows/release.yml:221-236` — v2 matrix is exactly:

| RID            | Runner          |
|----------------|-----------------|
| `linux-x64`    | `ubuntu-latest` |
| `linux-musl-x64` | `ubuntu-latest` |
| `osx-arm64`    | `macos-14`      |
| `win-x64`      | `windows-latest`|

README currently advertises **Linux arm64**, **macOS x64**, **Windows arm64**.
None of those are produced by the v2 release pipeline. A reader clicking
through to [Releases](https://github.com/SchwartzKamel/azure-openai-cli/releases)
will not find the files the table promises.

**Problem:** The user follows the doc, lands on a 404, and abandons.
**Proposed fix:** Trim the table to the four RIDs the workflow actually
emits. If arm64 Linux and Windows, or x64 macOS, are genuinely coming back,
move them into a "Planned" footnote with a tracking issue link — not into
the install table.
**Severity justification:** This is the install path. If the install path
is wrong, nothing else in the README matters.

---

### C2 — README Pre-built artifact filenames use the v1 naming scheme

**File:** `README.md:131-135`
**Evidence:** `.github/workflows/release.yml:207-212, 267-297, 395-398` — v2
artifacts are `az-ai-v2-<version>-<rid>.tar.gz|zip`
(e.g., `az-ai-v2-v2.0.4-linux-x64.tar.gz`). README lists
`azure-openai-cli-linux-x64.tar.gz` etc., which was the v1 (pre-EOL)
naming.

**Problem:** The filenames in the README do not exist as v2.x release
assets. A user running `curl -LO https://.../azure-openai-cli-linux-x64.tar.gz`
gets a 404.
**Proposed fix:** Replace the filename column with the v2 pattern
(`az-ai-v2-<tag>-<rid>.{tar.gz,zip}`) or link each row to the specific
release asset. Cross-reference `docs/verifying-releases.md` which already
uses the correct name.
**Severity justification:** Identical to C1 — installs fail silently with
404s when users script the download.

---

### C3 — README Quickstart points at the v1 source tree for `.env.example`

**File:** `README.md:27`
**Evidence:** `cp azureopenai-cli/.env.example .env && $EDITOR .env`. File
`azureopenai-cli-v2/.env.example` does not exist (`ls` confirms). The
CONTRIBUTING guide says "All new work goes in v2" and "v2 is the new
default" (`CONTRIBUTING.md:15, 19`), yet the 5-minute Quickstart instructs
the reader to rummage in the v1 tree.

**Problem:** Contradicts the repo's stated v2-first posture at the exact
moment a new user forms their mental model of where code lives. Also means
`azureopenai-cli-v2/` has no template file for the reader to copy from the
"correct" tree.
**Proposed fix:** Either (a) add `azureopenai-cli-v2/.env.example` and
update Quickstart to reference it (preferred; keeps v1 and v2 self-contained),
or (b) hoist a canonical `.env.example` to repo root and point both trees +
Quickstart at it. Coordinate with Kramer on the source-tree choice.
**Severity justification:** The Quickstart is the single most-executed code
block in the repo. It should not hand-wave the reader toward a tree the
project has formally deprecated.

---

## High

### H1 — `IMPLEMENTATION_PLAN.md` is a v1.9.0 planning document; the project is at v2.0.4

**File:** `IMPLEMENTATION_PLAN.md:70, 85, 139-208, 281-332` (whole doc)
**Evidence:**
- `v1.8.0 — Native AOT GA & Cross-Platform Publish (Unreleased)` (L70) — 1.8.0 has long since shipped, and two major versions later.
- `## v1.9.0 Roadmap (planned)` (L139) — a release that was skipped; the next major was v2.0.0.
- "Today's Fleet Manifest (v1.9 kickoff)" (L147) — names dated deliverables.
- `Golden Run — v1.9.0 polish pass` (L281) — describes a v1.9.0-alpha.1
  tagging exercise that never occurred.
- "Non-goals for v1.9.0" explicitly excludes "Plugin system for custom tools
  (too large; revisit v2.0)" — v2.0 has already happened.

**Problem:** Anyone who opens this file to understand what the project is
doing next gets a snapshot of a roadmap that ran aground a year ago. The
document claims authority ("Each milestone has an owner … If it doesn't
have all three, it isn't on the roadmap") while its own milestones are
ghosts.
**Proposed fix:** Either (a) archive it wholesale to
`docs/archive/IMPLEMENTATION_PLAN-v1.9-era.md` and replace the root file
with a current v2.x roadmap authored by Mr. Pitt/Costanza, or (b) rewrite
in place. Option (a) preserves the historical artifact and is less error-prone.
**Severity justification:** Not install-blocking, but the second-most-cited
"where is this project going?" doc after README. Wrong answers here
mislead PR authors and issue triagers.

---

### H2 — `ARCHITECTURE.md` describes v1 architecture and source layout

**File:** `ARCHITECTURE.md` (entire file; worst offenders below)
**Evidence:**
- L5: "packaged as a self-contained single-file binary, and distributed
  through Docker or native `dotnet run`." v2.0.4 primary distribution is
  NativeAOT tarballs + package managers (see `packaging/`).
- L22-35: system diagram scopes the binary inside a Docker container as the
  primary runtime.
- L187-201: Build Pipeline diagram is Docker-stage-based, target
  `runtime-deps:9.0-preview-alpine` (note: .NET 9 preview; project is .NET 10).
- L215-224: Makefile targets table lists `make build`, `make run`, `make alias`,
  `make scan`, `make test`, `make check` — the v2 Makefile canonical targets
  are `make publish-aot`, `make install`, `make bench`, `make format`,
  `make publish-fast`, etc. (`grep -n '^(install|setup|publish-aot|...)' Makefile`).
- L660-717: Directory Structure section shows only `azureopenai-cli/`. No
  `azureopenai-cli-v2/`, no `docs/adr/`, no `docs/proposals/`, no
  `docs/launch/`, no `packaging/`, no `.github/workflows/scorecards.yml`.
- L744-748 "Modifying the Docker build" — explains how to edit the v1
  `Dockerfile`; does not mention `Dockerfile.v2`.
- L752-772 "Design Decisions" table anchors on Docker-first distribution and
  `--agent` being the clear v1.0 boundary.

**Problem:** The architecture document describes a project that existed
18 months ago. README links to it as the authoritative system-design
reference (`README.md:149`). A new contributor reading it builds the wrong
mental model and writes PRs against conventions that no longer apply.
**Proposed fix:** Re-scope in place. Target minimum:
1. Rewrite §1 (Overview) + §2 (System Architecture) for the v2 binary-first
   reality; Docker becomes a secondary distribution.
2. Split the Directory Structure (§8) into v1 (maintenance) and v2 (primary)
   subtrees.
3. Update Build Pipeline (§4) to the NativeAOT `stage.sh` path used by
   `release.yml`'s `build-binaries-v2` job.
4. Renumber §8.5 (see M3).
5. Update Design Decisions (§10) — "Docker-first distribution" is no longer
   the headline rationale.
Coordinate with Kramer on technical accuracy and with Mr. Wilhelm on
versioning the old file vs. superseding it.
**Severity justification:** The doc is foundational and currently misleads
on every major v2 architectural choice. High, not Critical, only because no
install fails on it.

---

### H3 — Credential handling model contradicts itself between README and ARCHITECTURE

**File:** `README.md:119` vs. `ARCHITECTURE.md:603`
**Evidence:**
- README (`Security` section): "Credentials are never baked into the binary
  or Docker image — always injected at runtime."
- ARCHITECTURE (§7 Security Architecture → Credential handling): "API keys
  and endpoints live in the `.env` file, which is **baked into the image**
  at build time." (emphasis in source).

These two statements are the opposite of each other on a subject where
"opposite" is a security-relevant distinction.
**Problem:** A reader reconciling the two cannot tell which is correct.
(Reality: v2 ships NativeAOT binaries with no image baking; the v1 Docker
historically used `COPY .env` in its build.) The README claim is the
current-state truth; ARCHITECTURE.md preserves the v1 build behavior
without labeling it as v1-specific.
**Proposed fix:** Rewrite `ARCHITECTURE.md:601-606` to describe runtime
injection via env vars, `.env` loaded at process start, and flag the v1
Docker baking behavior as a deprecated historical path. Confirm with
Newman for Security narrative consistency.
**Severity justification:** Security-adjacent contradictions destroy
reader trust faster than any other documentation error class.

---

### H4 — README says "Upgrading from v1.9.x?" — no such version exists

**File:** `README.md:62`
**Evidence:** CHANGELOG.md releases go v1.0.0 → v1.0.1 → v1.1.0 → … →
v1.8.0 → v1.8.1 → **v2.0.0** (no v1.9.x was ever cut; IMPLEMENTATION_PLAN
still plans it, H1).

**Problem:** Readers who pattern-match on version numbers see "1.9.x" and
wonder what they missed.
**Proposed fix:** Change to "Upgrading from v1.x?" or the precise last
v1 line: "Upgrading from v1.8.x?" Coordinate with Mr. Lippman on the
canonical minimum-supported-upgrade-source.
**Severity justification:** Semantic error in a high-visibility migration
signpost. Not install-blocking; reputationally embarrassing.

---

### H5 — `CODE_OF_CONDUCT.md` reporting channel conflicts with `CONTRIBUTING.md` reporting channel

**File:** `CODE_OF_CONDUCT.md:39` vs. `CONTRIBUTING.md:211-215`
**Evidence:**
- CoC: "Instances of abusive, harassing, or otherwise unacceptable behavior
  may be reported to the project maintainers via
  [GitHub Issues](https://github.com/SchwartzKamel/azure-openai-cli/issues)."
  GitHub Issues is a **public** channel.
- CONTRIBUTING: "Report unacceptable behavior through [`SECURITY.md`](SECURITY.md)'s
  private channel or to a maintainer directly — we deal with it privately
  first…"

**Problem:** A reporter reading CoC literally files their harassment
complaint in a public issue, exposing themselves and the accused
simultaneously. CONTRIBUTING's private-first posture is the correct one;
CoC points at the wrong channel.
**Proposed fix:** Replace `CODE_OF_CONDUCT.md:39` with a sentence that
mirrors CONTRIBUTING: private channel via SECURITY.md's disclosure path
or direct maintainer contact. Keep the "All community leaders are
obligated to respect the privacy and security of the reporter" line
(L41) — it's good, just toothless when the channel is public.
**Severity justification:** Reporter-safety is not a style preference.
High because it's a procedural harm pattern, not merely stale prose.

---

## Medium

### M1 — README "538 passing tests" number is stale

**File:** `README.md:20`
**Evidence:** IMPLEMENTATION_PLAN.md:78 claims a 538 baseline for v1.8.0.
The Golden Run section (L304) raised the floor to ≥546. v2.0.4 has shipped
multiple test-bearing features since. The exact current count lives in
`tests/AzureOpenAI_CLI.Tests/` and is not verified here — but 538 is
demonstrably a v1.8.0 artifact.
**Problem:** Specific numbers in marketing prose get fact-checked. A stale
one tells the reader the rest of the page may also be stale.
**Proposed fix:** Either (a) drop the exact number and say "extensive test
suite" (Peterman won't love it), or (b) wire the number to CI output and
regenerate on release. Lippman's territory for the release-note pass.
**Severity justification:** A single number, but load-bearing for the "this
is a serious project" claim in the hero paragraph.

---

### M2 — README "New in v2.0.0" heading, 4 patch releases later

**File:** `README.md:47`
**Evidence:** Heading text `### New in v2.0.0`. Project at v2.0.4; FDR
High fixes, macos-13 runner retirement, packaging iteration all shipped
in 2.0.1 → 2.0.4.
**Problem:** The heading dates itself. Readers don't know whether the table
below captures only v2.0.0 additions or the 2.0.x line cumulatively.
**Proposed fix:** Rename to `### New in v2` or `### New in v2.x` and, if
needed, link to CHANGELOG for per-patch detail.
**Severity justification:** Minor but will compound as v2.1 approaches.

---

### M3 — `ARCHITECTURE.md` section ordering: §8.5 appears before §8

**File:** `ARCHITECTURE.md:622-717`
**Evidence:** `## 8.5. Source-Generated JSON (AOT Strategy)` (L622) followed
by `## 8. Directory Structure` (L660).
**Problem:** Section numbers imply order; here they don't. Readers using the
doc as a reference (TOC-scan then jump) see §8.5 in the body before they've
seen §8.
**Proposed fix:** Renumber the source-gen section as §7.5 (it sits inside
the Security Architecture concern) or move it to its own top-level §9, and
push Directory Structure → Extension Points → Design Decisions down
accordingly. A minor heading re-sort; do it during the H2 rewrite.
**Severity justification:** A markdown-lint-visible structural bug. Harmless
until a TOC generator runs over it.

---

### M4 — `ARCHITECTURE.md` CLI flag table missing v2 surface area

**File:** `ARCHITECTURE.md:95-116`
**Evidence:** Flag table enumerates v1.x flags. Missing everything the
README "New in v2.0.0" table announces: `--json` (present but without v2
error-contract changes), `--schema`, `--max-rounds` (present), `--config`,
`--completions`, `--telemetry`, `--estimate` / `--dry-run-cost` /
`--estimate-with-output`, `--set-model` subcommand semantics, `--version
--short`.
**Problem:** ARCHITECTURE is supposed to be the reference; it's missing
eight surface flags that ship in v2. A reader writing a wrapper script
looks here and under-builds their integration.
**Proposed fix:** Regenerate the table from the current CliParser. Consider
moving the "canonical flag list" out of ARCHITECTURE and into a single
source of truth (man page / `--help` output captured verbatim) that both
README and ARCHITECTURE link to, to prevent drift next cycle.
**Severity justification:** Not wrong, but incomplete in a way that will
bite the advanced user first — the exact cohort we want to retain.

---

### M5 — README "Documentation" index omits several actively maintained v2 docs

**File:** `README.md:146-171`
**Evidence:** The Documentation section links ~12 docs. Unlinked from any
root-level narrative prose, yet clearly user-facing:
- `docs/config-reference.md`
- `docs/observability.md`
- `docs/why-az-ai.md`
- `docs/competitive-analysis.md`
- `docs/opportunity-analysis.md`
- `docs/nim-setup.md`
- `docs/release-notes-v2.0.0.md` (linked from CONTRIBUTING, not README)
- `docs/use-cases-agent.md`, `docs/use-cases-standard.md`,
  `docs/use-cases-config-integration.md` (parent `docs/use-cases.md` is
  linked as an index, but individual mode pages aren't surfaced elsewhere —
  verify the index actually lists them)

Legitimately internal ephemera (left out on purpose): `docs/v2-cutover-*.md`,
`docs/v2-dogfood-plan.md`, `docs/chaos-drill-v2.md`,
`docs/security-review-v2.md`, `docs/launch/*`.
**Problem:** "Orphaned from the landing page" for user-facing docs is a
retention bug — readers don't know these exist.
**Proposed fix:** Add a row or two to the README Documentation index for
`config-reference.md`, `observability.md`, `why-az-ai.md`. Verify
`docs/use-cases.md` actually indexes the per-mode children; if not, add
the index (Puddy/Kramer can confirm).
**Severity justification:** Medium because it affects discoverability, not
correctness.

---

## Low

### L1 — `CONTRIBUTORS.md` hasn't tracked actual contribution activity

**File:** `CONTRIBUTORS.md:37`
**Evidence:** The file states "Your name lands here automatically the first
time one of your contributions is merged" (L10-18) and claims "A maintainer
will add you during the next release pass" (L26-28). At v2.0.4, after the
Golden Run + v2 cutover + FDR High fixes, the "First contributors" list
has one entry: `@SchwartzKamel`.
**Problem:** Either the policy is not being enforced at release passes, or
the policy is aspirational and the doc over-promises. Either way the file
is out of date.
**Proposed fix:** Lippman's release-pass territory — flag it for the v2.0.5
pass. Minimum: either populate honestly or soften the wording to match the
cadence we actually keep.
**Severity justification:** Low because nothing breaks; high-emotional-cost
if a first-time contributor notices they weren't added after a merge.

---

### L2 — `ARCHITECTURE.md` Directory Structure missing `scorecards.yml`

**File:** `ARCHITECTURE.md:671-672`
**Evidence:** Tree shows `ci.yml` and `release.yml` under `.github/workflows/`.
Repo ships a third: `.github/workflows/scorecards.yml` (OSSF Scorecards).
**Problem:** Reference tree out of date; a reader counts workflows from the
doc and misses one (common during CI audits).
**Proposed fix:** Add the file to the tree with a one-line comment. Rolls
into H2's broader rewrite.
**Severity justification:** Low. Single line.

---

### L3 — README "arm64" platform blurb overstates Linux coverage

**File:** `README.md:19`
**Evidence:** "Pre-built AOT binaries for Linux (glibc/musl/arm64) …" —
`release.yml:225-232` ships `linux-x64` (glibc) and `linux-musl-x64`. No
`linux-arm64` RID in the v2 matrix.
**Problem:** Same shape as C1 but in the marketing blurb rather than the
install table. A reader scanning only the hero bullet forms the wrong
impression.
**Proposed fix:** "Pre-built AOT binaries for Linux (glibc, musl), macOS
(Apple Silicon), and Windows (x64)." Straight list; no phantom arm64.
**Severity justification:** Lower than C1 because install table is the
authoritative source; this is a supporting claim.

---

### L4 — `AGENTS.md` pipeline diagram + table have subtle role-ownership mismatches

**File:** `AGENTS.md:54-94`
**Evidence:** Pipeline diagram and table are generally consistent, but:
- "Sue Ellen Mischke" appears in the PLANNING lane of the diagram but is
  listed under Supporting Players in the table (fine, the lane includes
  Mr. Pitt + Costanza who are "main" + "supporting"; just noting for
  consistency).
- Elaine appears in COMMUNITY & ADVOCACY despite being Main Cast (that's
  fine — Main Cast agents can appear in supporting-lane phases). No action
  required on this one unless a future reader raises an issue.
**Problem:** Mild cognitive friction; a careful reader questions whether
the lanes are authoritative or illustrative.
**Proposed fix:** Add a one-line caption: "Main Cast agents may appear in
multiple phases; the diagram shows primary owners per phase, not exclusive
membership."
**Severity justification:** Cosmetic clarity improvement.

---

## Informational

### I1 — Orphaned-from-landing `docs/` files: inventory

**Scope:** Same as M5; recording the full enumeration so future audits
can diff against it.

Files in `docs/*.md` (root of `docs/`) not referenced from README.md,
ARCHITECTURE.md, IMPLEMENTATION_PLAN.md, AGENTS.md, SECURITY.md, or
CHANGELOG.md (CONTRIBUTING.md does reference
`release-notes-v2.0.0.md` and `migration-v1-to-v2.md`, which I re-count
as linked):

```
docs/chaos-drill-v2.md              # FDR ephemera; internal — fine
docs/competitive-analysis.md        # Sue Ellen; should we surface?
docs/config-reference.md            # USER-FACING — should be indexed
docs/nim-setup.md                   # Foundry/NIM — niche; link from use-cases?
docs/observability.md               # USER-FACING — should be indexed
docs/opportunity-analysis.md        # internal strategy; keep unlinked
docs/release-notes-v2.0.0.md        # linked from CONTRIBUTING only
docs/security-review-v2.md          # Newman's; internal
docs/use-cases-agent.md             # verify indexed by use-cases.md
docs/use-cases-config-integration.md # verify indexed by use-cases.md
docs/use-cases-standard.md          # verify indexed by use-cases.md
docs/v2-cutover-checklist.md        # internal launch — fine
docs/v2-cutover-decision.md         # internal launch — fine
docs/v2-dogfood-plan.md             # internal — fine
docs/why-az-ai.md                   # USER-FACING — should be indexed
```

No fix required from the audit itself; feeds M5.

---

### I2 — Voice audit: `CONTRIBUTORS.md` opens in Uncle Leo's voice

**File:** `CONTRIBUTORS.md:3`
**Evidence:** "Hello! Contributor! Hello! We're glad you're here." — that's
Uncle Leo's signature opener (see `AGENTS.md:36` — Uncle Leo / DevRel /
Community). CONTRIBUTING.md:3 also opens with it, and CONTRIBUTING is
Uncle Leo/Elaine co-owned prose by the fleet's convention.
**Problem:** None, technically. Informational only: we have a voice
convention in practice (Uncle Leo for community-welcome docs, Elaine for
reference docs) and it's worth naming it in `docs/style.md` if/when that
file materializes.
**Proposed fix:** No change to the file. Capture the voice-ownership rule
in a future `docs/style.md` pass.
**Severity justification:** Informational.

---

## Nits

### N1 — `README.md:31` orphan paragraph between Quickstart block and "Execution Modes"

**File:** `README.md:31`
**Evidence:** The "You need an Azure OpenAI resource — grab the endpoint,
a deployed model, and the API key" paragraph sits between the Quickstart
code block and the next heading. It reads as an afterthought.
**Problem:** Prerequisite info lives below the code block that consumes
it. Readers who copy-paste the Quickstart first and then notice the
"oh, you also need …" beat below, backtrack.
**Proposed fix:** Move this paragraph above the Quickstart code block, or
fold a single inline prerequisite comment into the block. Maintains the
Quickstart-first ordering the doc otherwise honors.
**Severity justification:** Aesthetic; doesn't block anything.

---

### N2 — `ARCHITECTURE.md:5` "distributed through Docker or native `dotnet run`"

**File:** `ARCHITECTURE.md:5`
**Evidence:** Neither is the primary v2 distribution story. Primary is
pre-built NativeAOT binaries + `brew`/`scoop`/`nix` (see `packaging/`).
`dotnet run` is a dev-loop command, not a distribution channel.
**Problem:** One sentence, overtaken by events; folds into H2.
**Proposed fix:** Rolls into H2's Overview rewrite.
**Severity justification:** Nit-level only because H2 already captures it
in scope.

---

## Out of scope — noted for the relevant auditor

- `SECURITY.md` — contradictions with this file set called out in H3 (for
  Newman).
- `CHANGELOG.md` — staleness floor on version/test numbers called out in
  M1, M2 (for Mr. Lippman).
- `NOTICE`, `THIRD_PARTY_NOTICES.md` — not read (Jackie Chiles).

---

## Recommended execution order

1. **C1, C2, C3** — same PR. Ten-minute fix; unblocks every new install.
2. **H4** — five seconds; part of the C1/C2 PR.
3. **H5** — one-line CoC edit. Critical for reporter safety despite
   Medium-sized diff.
4. **H3** — fix the contradiction; tighten security-adjacent prose.
5. **M1, M2, M5, L3** — README second pass.
6. **H1** — archive IMPLEMENTATION_PLAN; replace with v2.x roadmap
   authored by Mr. Pitt.
7. **H2** + **M3, M4, L2, N2** — ARCHITECTURE rewrite cycle. Largest
   single effort; do last so the Critical/High fixes aren't blocked on it.

— Elaine
