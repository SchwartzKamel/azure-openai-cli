# DevOps Docs Audit — 2026-04-22 (Jerry, under oath)

> Auditor: Jerry (DevOps / modernization)
> Co-auditor: Costanza (PM, critical-friend lens, per-finding)
> Tag of record: **v2.0.4** (release run 24789065975 — SUCCESS)
> Scope: `docs/runbooks/**`, `docs/ops/**`, DevOps-adjacent `docs/launch/**`,
>        `Dockerfile`, `.dockerignore`, `.github/workflows/*.yml`,
>        `scripts/setup.sh`, `Makefile`, CI section of `CONTRIBUTING.md`,
>        `docs/verifying-releases.md`
> Non-goals: no source doc edits in this pass — findings only.

## Frame

v2.0.4 dropped `osx-x64` / `macos-13` from the release matrix and shipped
FDR's High-severity fixes. The release went green on the first try. That
is the happy part. The less happy part is that a pile of DevOps docs still
treat `osx-x64 / macos-13` as a first-class ship target, and a few of them
give recovery advice that is now either obsolete or actively wrong. This
audit catalogs every DevOps-surface doc that either (a) misleads a reader
about what the pipeline actually builds and ships today, or (b) asks a
reader to copy a command that won't do what the comment claims.

Severity legend: **Critical** = factually wrong in a way that will cause
a new maintainer to misfire a release or on-call action. **High** = stale
enough to mis-set expectations on the current matrix. **Medium** = likely
to confuse or burn time. **Low** = cosmetic/historical framing. **Info**
= fine today, noted for a later sweep.

---

## Critical

### C1 — `docs/runbooks/release-runbook.md:76-77, 91` — matrix description is wrong on both v1 and v2

**Problem.** The one-and-only runbook describes the release workflow's
binary matrix as:

> "2. `build-binaries` — 5-way matrix (linux-x64, linux-musl-x64, win-x64,
>    osx-x64, osx-arm64)."

and later: "Release object exists and has all 5 platforms + 5 SBOMs."

Cross-check against `.github/workflows/release.yml`:

- v1 matrix (`build-binaries`, lines 51–58): **4 legs** — `linux-x64`,
  `linux-musl-x64`, `win-x64`, `osx-arm64`. v1 has **never** shipped
  `osx-x64` in the attested matrix.
- v2 matrix (`build-binaries-v2`, lines 225–236): **4 legs** — `linux-x64`,
  `linux-musl-x64`, `osx-arm64`, `win-x64`. `osx-x64` was cut in v2.0.4.

So the runbook over-counts (5 vs. 4) and mis-lists (`osx-x64` has never
been in v1, is no longer in v2). A maintainer copy-pasting the
verification checklist will wait for a 5th asset that will never land.

**Proposed fix.** Replace the "5-way matrix" paragraph with a version-aware
statement that points at the workflow file as the source of truth, enumerate
today's 4-leg matrix explicitly, and drop `osx-x64` everywhere. Add a
short note: *"matrix membership changes; always cross-check against
`.github/workflows/release.yml` before a release."*

**Severity.** Critical — this is the runbook a new maintainer opens first.

**Costanza's angle.** *"Jerry. Jerry. The runbook is telling a story about
a matrix that doesn't exist anymore. A new contributor lands here on day
one, they wait for the fifth asset, release looks broken, they page
somebody at 2am. This doc is doing product management through ritual
avoidance — it describes the *wish* of five platforms, not the *reality*
of four. Fix the shape, then write the *why* for the cut. Otherwise you
are just running the old play and calling it the new play."*

---

### C2 — `docs/ops/v2.0.0-day-one-baseline.md:71-103` — asset inventory inflates count and lists both phantom and dropped platforms

**Problem.** The "Expected asset inventory (10 files)" table for the
Frank-Costanza day-one baseline lists:

- `az-ai-v2-2.0.0-linux-arm64.tar.gz` (line 76) — **never in the v2
  matrix**. `release.yml` `build-binaries-v2` has no `linux-arm64` leg.
- `az-ai-v2-2.0.0-osx-x64.tar.gz` (line 77) — **dropped in v2.0.4**.
- Count = 10 = "5 tarballs + 5 SBOMs". Today's reality: 4 tarballs +
  4 SBOMs = 8. (v2.0.0 actually shipped 5 + 5 because `osx-x64` was in
  the matrix at the time, but `linux-arm64` was never a v2 asset.)
- Line 92: the per-platform sanity check ordering
  `linux-x64 ≥ osx-arm64 ≈ osx-x64 ≥ win-x64 ≥ linux-arm64` — references
  two platforms we do not ship. Frank will have nothing to chart.

**Proposed fix.** Two choices, and they are different choices:
(1) if the doc is meant as a **historical v2.0.0 baseline**, add a
"[HISTORICAL — v2.0.0 inventory, superseded by v2.0.4]" banner at the top,
strike `linux-arm64` (never shipped), and add a footnote on `osx-x64`
pointing at the v2.0.4 matrix cut; or (2) if the doc is meant as a
**living telemetry template**, re-derive the inventory from current
`release.yml` (4 tarballs + 4 SBOMs = 8 files), and delete the
`linux-arm64` and `osx-x64` rows. Frank should choose; do not split the
difference.

**Severity.** Critical — a 10-file expected inventory makes a successful
8-file release look broken.

**Costanza's angle.** *"Baseline? What baseline? You can't baseline against
a number that was never real on one platform and stopped being real on
another. This is a doc that *feels* like it's doing rigour — it has a
table! it has thresholds! — but the denominator is fiction. Pick
historical-snapshot or living-template. You cannot be both. Pick."*

---

## High

### H1 — `docs/launch/release-v2-playbook.md:73, 167-192` — matrix table + macos-13 troubleshooting section

**Problem.** Lippman's playbook still lists `osx-x64` on `macos-13` as a
first-class matrix row (line 73) and dedicates ~25 lines to
"`build-binaries-v2 / osx-x64` stuck in `queued` for 30+ min" as an expected
failure mode (lines 167–192). Post-v2.0.4 this is obsolete — the row and
the failure mode cannot happen because the leg is not scheduled. Reading
this section now, a new release manager will believe the macos-13 backlog
is still a live operational risk and may either (a) wait 120 min before
escalating a different problem, or (b) cargo-cult the `gh workflow run
release.yml --ref <tag>` recipe onto a tag that does not have that trigger.

**Proposed fix.** (1) Update the matrix table to 4 rows, drop `osx-x64`.
(2) Collapse the "stuck in queued" troubleshooting block to a one-line
historical note: *"Prior v2.0.1–v2.0.3 runs hit a macos-13 runner backlog.
Resolved by dropping `osx-x64` from the matrix in v2.0.4. See
`docs/launch/v2.0.2-release-attempt-diagnostic.md` for the postmortem."*
Keep the postmortem as history; do not keep the operational advice.

**Severity.** High — a playbook that describes a ghost failure mode is
worse than no playbook.

**Costanza's angle.** *"This section is doing product management through
ritual avoidance. 'Wait, escalate, shelve' — this is a checklist for a
problem we *solved by making a product decision*. The decision was: we
don't ship osx-x64. That's the bold line. The troubleshooting should
have evaporated the same day the matrix shrank. It didn't. Why? Because
nobody owned the cleanup. That's a PM failure too. Noted."*

---

### H2 — `docs/launch/v2.0.2-publish-handoff.md` — recovery recipe contradicts itself; no "resolved" banner

**Problem.** The handoff is entirely about a stuck v2.0.2 release. It
documents the `workflow_dispatch` HTTP 422 trap (good — that is a real
gotcha) and the `gh run rerun --failed` workaround (also good). But:

- No banner at the top saying "HISTORICAL — superseded by v2.0.4."
- The fallback section (lines 44–51) plans the fix-forward to v2.0.3.
  That happened. The later matrix cut (v2.0.4) happened too. This doc
  does not reflect either.
- A contributor searching for "how do I rerun a stuck release?" will land
  here, copy the recipe, and apply it to a tag whose workflow no longer
  has an `osx-x64` leg to re-queue.

**Proposed fix.** Add a "Status: RESOLVED in v2.0.4 (matrix cut)" banner
and a pointer to the current (future) macos-triage section of
`release-v2-playbook.md` or to a new short runbook (see M3).

**Severity.** High — this is the kind of stale op-doc that gets paged
against at 3am.

**Costanza's angle.** *"Every postmortem needs a sticker on the front
that says 'closed' or 'open'. If I have to read three paragraphs before I
know whether this is a live incident or a history book, you've already
lost me. Sticker."*

---

### H3 — `.github/workflows/release.yml:16-21` — inline comment cites macos-13 backlog as the canonical reason to keep `workflow_dispatch`

**Problem.** The comment on lines 16–21 explains `workflow_dispatch` with
the example *"when a prior run is blocked on transient infra (e.g.,
macos-13 runner backlog)."* That example no longer applies to this
repository — we don't have a macos-13 leg. The comment also points at
`docs/launch/release-v2-playbook.md §Troubleshooting`, which is itself
stale (see H1).

**Proposed fix.** Generalize the example: *"…blocked on transient infra
(flaky runner, registry outage, actions/artifacts hiccup)."* Remove the
macos-13 specificity. Keep the pointer, but update the target section in
the playbook first (H1).

**Severity.** High — inline YAML comments drift silently because nobody
reviews them outside release prep.

**Costanza's angle.** *"A comment in a YAML file is still product copy.
If it names a thing that doesn't exist anymore, it's misinformation with
a checkmark next to it."*

---

## Medium

### M1 — `Makefile:84, 294-297, 317-319` — `publish-osx-x64` advertised in `make help` with no "local-dev only" framing

**Problem.** `make help` prints `make publish-osx-x64 — macOS Intel` right
next to the other release-parity cross-build targets, and `make
publish-all` still builds 7 RIDs including `osx-x64`. Post-v2.0.4 this is
fine *for local development* (the target still works) but a contributor
who trusts `make help` as a release-matrix reference will think
`osx-x64` is a shipped platform.

**Proposed fix.** Add a one-line note under the "Per-OS cross-builds"
block in `make help`: *"These mirror local-dev convenience only. Shipped
matrix is enumerated in `.github/workflows/release.yml`."* Consider
splitting `publish-all` into `publish-all` (4 shipped RIDs) and
`publish-all-local` (7 RIDs including macOS Intel, Windows ARM64, Linux
ARM64). Do not remove `publish-osx-x64` — contributors on Intel Macs
still want it for local smoke.

**Severity.** Medium — misleads the "what do we ship?" reader, not the
"what can I build?" reader.

**Costanza's angle.** *"`make help` is the API of the Makefile. Every
line in it is a promise to the reader. If one of those promises doesn't
match what leaves the factory, that's a product bug, even if the code is
fine."*

---

### M2 — `docs/verifying-releases.md:206` — multi-platform verify loop still iterates `osx-x64`

**Problem.** The §5 "Verify every platform in one go" shell loop:

```bash
for rid in linux-x64 linux-musl-x64 osx-x64 osx-arm64; do
  gh release download v1.8.1 ...
```

v1.8.1 *did* have `osx-x64` in its (historic 4-leg) matrix — wait, no:
v1 matrix was `linux-x64`, `linux-musl-x64`, `win-x64`, `osx-arm64`
(release.yml:51–58). So v1.8.1 never shipped `osx-x64` either; the loop
was wrong at authoring time and will fail with a 404 today if a user
copies it. The loop also omits `win-x64`, which the prose below it then
hand-rolls as a special case. Inconsistent.

**Proposed fix.** Rewrite the loop to iterate today's actual v1 matrix
(or flip the example to v2, since v2 is the default), and fold `win-x64`
into the loop with a per-platform `ext` variable (`tar.gz` vs `.zip`).
Same mechanics, less copy-paste error surface.

**Severity.** Medium — copy-paste target for supply-chain-conscious users.
Failure here looks like "attestation is broken" when really it's just
"the file doesn't exist."

**Costanza's angle.** *"A verification doc that hands the user a broken
loop is worse than no doc. They'll assume the signing infrastructure is
flaky, not that the doc is wrong. You're outsourcing your bug to the
user's trust in Sigstore. Don't do that."*

---

### M3 — Missing: `docs/runbooks/macos-13-backlog-triage.md` (promised by accretion, never created)

**Problem.** Across four launch diagnostics (`v2.0.1-…`, `v2.0.2-…`,
`v2.0.2-publish-handoff.md`, `release-v2-playbook.md §Troubleshooting`)
there is a de-facto macOS-runner triage procedure, but it is not
consolidated anywhere stable. The matrix cut in v2.0.4 makes this moot
for `osx-x64`, but **the `osx-arm64 / macos-14` leg remains** and can
backlog for the same reasons. When (not if) `macos-14` backlogs, we are
right back to reading four different postmortems to figure out what to do.

**Proposed fix.** Create a short `docs/runbooks/macos-runner-triage.md`
(note: not `macos-13-` — the new doc should be runner-generation-agnostic)
containing: (1) current macOS legs in the release matrix, (2) symptoms
of runner backlog, (3) the `gh run rerun --failed` recipe with the
`workflow_dispatch` 422 caveat spelled out, (4) escalation thresholds,
(5) link back to the historical v2.0.x diagnostics as prior art. Keep
it under one screen.

**Severity.** Medium — we have the content; we lack the index.

**Costanza's angle.** *"This is a doc I've been asking for since v2.0.1
and you kept promising it was 'coming with the next postmortem.' Four
postmortems later, no consolidation. That is exactly what I mean by
ritual avoidance — you are writing *about* the problem instead of
writing *a fix for future me*. One page. One screen. One link. Ship it."*

---

### M4 — `docs/runbooks/release-runbook.md` — whole doc is v1-centric; no v2 path

**Problem.** Every example (`VERSION=1.8.1`, `TAG=v1.8.1`), every
artifact name (`azure-openai-cli-linux-x64.tar.gz`), and every ghcr path
(`ghcr.io/schwartzkamel/azure-openai-cli:${TAG#v}`) is v1-shaped. v2
has different artifact names (`az-ai-v2-<version>-<rid>.tar.gz`), a
different image (`ghcr.io/schwartzkamel/azure-openai-cli/az-ai-v2:<tag>`),
a different producer (`stage.sh`), and a different post-publish hash-sync
step (Homebrew/Scoop/Nix manifests). None of that appears in the
runbook. A reader cutting v2.0.5 from this runbook alone will generate
a broken checklist.

**Proposed fix.** Add a "§2a — v2 pipeline" subsection that mirrors §2–§4
with v2 artifact names, the v2 image path, and a pointer to
`release-v2-playbook.md` for the full hash-sync ritual. Or collapse the
runbook to a short index and delegate to `release-v2-playbook.md` (v2)
and a parallel `release-v1-playbook.md` (v1, maintenance-only).

**Severity.** Medium — the "one runbook to rule them all" is now a
one-runbook-that-knows-only-v1 situation.

**Costanza's angle.** *"Two source trees, two image names, two matrix
shapes — one runbook, v1-only. Guess which one gets consulted under
pressure. The one that's wrong. Split it or update it."*

---

## Low

### L1 — Diagnostic docs missing "resolved" framing

**Files.** `docs/launch/v2-release-attempt-1-diagnostic.md`,
`docs/launch/v2.0.1-release-attempt-diagnostic.md`,
`docs/launch/v2.0.2-release-attempt-diagnostic.md`.

**Problem.** These are valuable postmortems and should be preserved as
history. None of them carry a top-of-file banner noting that the root
causes were fixed (Dockerfile.v2 `--no-restore` removal in v2.0.2; the
macos-13 backlog resolved by matrix cut in v2.0.4). Without the banner,
a reader one year from now will re-diagnose a solved problem.

**Proposed fix.** One-line banner at the top of each:
*"Status: RESOLVED. Root cause fixed in <vX.Y.Z>. Kept for historical
context; do not use operationally."*

**Severity.** Low — content is right, framing is missing.

**Costanza's angle.** *"Sticker. Every postmortem. Sticker."*

---

### L2 — `CONTRIBUTING.md:42-46` — "If it's green locally, CI will be green" is slightly more optimistic than the mapping supports

**Problem.** Quickstart §4 lists three commands and promises CI parity:

```bash
dotnet format azure-openai-cli.sln --verify-no-changes
dotnet build azureopenai-cli-v2/AzureOpenAI_CLI_V2.csproj -c Release --nologo
dotnet test  tests/AzureOpenAI_CLI.Tests/AzureOpenAI_CLI.Tests.csproj --nologo
```

`.github/workflows/ci.yml` additionally runs: v2 test project
(`AzureOpenAI_CLI.V2.Tests`), `dotnet list package --vulnerable`, the
integration-test job (bash `tests/integration_tests.sh`), and the Docker
build + Trivy scan. Locally this is what `make preflight` covers
(format-check + dotnet-build + test + integration-test — but not the
docker leg, and not the vuln scan). The Quickstart doesn't mention
`make preflight` until §"The preflight gate."

**Proposed fix.** Change Quickstart §4 to `make preflight` with one
sentence: *"`make preflight` runs what CI runs except the Docker build
and vulnerability scan — those run in CI only."* Keeps the promise
honest.

**Severity.** Low — contributors will still ship, but the claim is tighter.

**Costanza's angle.** *"'Green locally, green in CI' is a marketing
claim. Either deliver it or downgrade it. Engineering docs should make
promises they can keep."*

---

### L3 — `docs/runbooks/release-runbook.md:96-102` — verification examples use v1 image path only

**Problem.** Post-release verify block:

```bash
docker pull "ghcr.io/schwartzkamel/azure-openai-cli:${TAG#v}"
```

v2 publishes to `ghcr.io/schwartzkamel/azure-openai-cli/az-ai-v2:<tag>`
(release.yml:320). A maintainer running this on v2.0.5 will pull an
image that may not exist at that tag (v1 line may be frozen).

**Proposed fix.** Add a conditional: v1 → canonical image name; v2 →
`/az-ai-v2` suffix. Same fix as M4; folded here for completeness.

**Severity.** Low — caught quickly at verification, but burns a minute.

**Costanza's angle.** *"See M4. Same bug. Same root cause. Same fix."*

---

## Informational

### I1 — `Dockerfile` — no macos-relevant content; layers and digests are current

`Dockerfile` pins `mcr.microsoft.com/dotnet/sdk:10.0@sha256:adc0…` and
`runtime-deps:10.0-alpine@sha256:f8a0…` with refresh instructions in
the comments. Nothing to flag. Newman-hardening already applied.

### I2 — `.dockerignore` — consistent with current tree

Excludes `tests/`, `docs/`, `.github/`, keeps `LICENSE`/`NOTICE`/
`THIRD_PARTY_NOTICES.md` whitelisted. Good.

### I3 — `scripts/setup.sh` — .NET 10 SDK only, idempotent, no macos-13 assumptions

`DOTNET_CHANNEL="10.0"`, detection covers Linux / macOS (Darwin) / WSL,
no hard-coded macOS version. Fine post-v2.0.4.

### I4 — `.github/workflows/ci.yml:24-28` — "Tracked for v1.9" comment is dated

The `windows-latest` exclusion comment references a v1.9 follow-up. We
are on v2.0.4. Low priority to update, but a future pass should
re-anchor the "tracked for" version or drop the forward reference.

### I5 — `Makefile` preflight target is accurate

`make preflight` = `format-check color-contract-lint dotnet-build test
integration-test` — this is the correct local mirror of CI's cheap-path
(excluding docker + vuln scan). No change needed.

---

## Table: outdated `macos-13` / `osx-x64` references to purge

| File | Lines | What it says | Required action |
|---|---|---|---|
| `docs/runbooks/release-runbook.md` | 76–77 | `5-way matrix (… osx-x64, osx-arm64)` | Drop `osx-x64`; correct count to 4 |
| `docs/runbooks/release-runbook.md` | 91 | `all 5 platforms + 5 SBOMs` | Correct to 4 + 4 = 8 assets |
| `docs/ops/v2.0.0-day-one-baseline.md` | 77 | `az-ai-v2-2.0.0-osx-x64.tar.gz` row | Strike (historical) or delete (living) |
| `docs/ops/v2.0.0-day-one-baseline.md` | 92 | share ordering includes `osx-x64` | Rewrite without `osx-x64` |
| `docs/ops/v2.0.0-day-one-baseline.md` | 100 | `osx-x64.tar.gz` blank row | Strike |
| `docs/launch/release-v2-playbook.md` | 73 | matrix row `osx-x64 \| macos-13` | Delete row |
| `docs/launch/release-v2-playbook.md` | 167–192 | macos-13 backlog troubleshooting block | Collapse to one-line historical pointer |
| `docs/launch/v2.0.2-publish-handoff.md` | 5, 7, 46, 60 | macos-13 backlog framing | Add RESOLVED banner; archive |
| `docs/launch/v2.0.2-release-attempt-diagnostic.md` | 3–5, 27–35, 52, 107–156, 194–200 | entire diagnostic is about macos-13 backlog | Add RESOLVED banner (keep as history) |
| `docs/launch/v2.0.1-release-attempt-diagnostic.md` | 47, 240 | `osx-x64 queued on macos-13 runner` | Add RESOLVED banner (keep as history) |
| `docs/verifying-releases.md` | 206 | `for rid in … osx-x64 …` loop | Rewrite loop to current matrix |
| `.github/workflows/release.yml` | 18 | comment example `macos-13 runner backlog` | Generalize to "transient infra" |
| `Makefile` | 84 | `make help` advertises `publish-osx-x64` | Add "local-dev only" framing |
| `Makefile` | 317–319 | `publish-all` includes `publish-osx-x64` | Split into `publish-all` (4 shipped) vs `publish-all-local` (7) |

Untouched (correct as local-dev only, no action required):
`Makefile:16` (RID auto-detect on Darwin x86_64), `Makefile:263`
(AOT host-constraint comment naming `osx-x64` as a valid AOT target),
`Makefile:294-297` (the `publish-osx-x64` target itself),
`docs/launch/v2-tag-rehearsal-report.md:74,119,197,226,239,271` (historical
rehearsal record — already historical in framing),
`docs/announce/v1.8.0-launch.md:51` (v1.8.0 announcement — historical).

---

## Mea culpa — top three things I got wrong in past doc iterations

Since I'm under oath here:

1. **I oversold `macos-13` reliability in `release-v2-playbook.md`
   §Troubleshooting.** I wrote a ~25-line "wait, escalate, shelve"
   recipe as if the runner-pool backlog was a *managed* operational
   risk. It wasn't. It was a recurring 30–120 min outage on every v2
   release. The right call was to cut `osx-x64` from the matrix after
   v2.0.2 — I deferred that decision three tags too long, and the docs
   wear the cost.

2. **I left postmortem docs without "resolved" banners.** Every
   `v2.0.x-release-attempt-diagnostic.md` still reads as a live
   incident. I told myself "postmortems are obviously historical" and
   moved on. They are not obviously historical to a contributor opening
   the file cold. A sticker takes ten seconds. I didn't add ten seconds
   × four files = forty seconds of clarity. That's embarrassing.

3. **I never consolidated the macOS-runner triage into a runbook.**
   The content is spread across four diagnostics and one playbook
   section. Every time a new incident hit, I wrote a new postmortem
   instead of updating the index. The matrix cut in v2.0.4 makes this
   moot for `osx-x64`, but `osx-arm64 / macos-14` is still in the
   matrix and will backlog someday. I promised a triage runbook across
   three separate commit messages. It still doesn't exist.

— Jerry, auditor of record, 2026-04-22

*Not that there's anything wrong with admitting it. Except there is, and
I'll fix it in the next sweep.*
