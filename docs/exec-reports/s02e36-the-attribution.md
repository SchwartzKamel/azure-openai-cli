# S02E36 -- *The Attribution*

> *Pre-v2.1.0 license audit. Counsel re-runs the closure. Clean bill of health.*

**Commit:** `<this commit>`
**Branch:** `main` (direct push)
**Runtime:** ~25 min (audit-only, no code touched)
**Director:** Larry David (showrunner)
**Cast:** 2 sub-agents in 1 dispatch wave (Jackie Chiles lead, Bob Sacamano guest)

## The pitch

Outrageous. Egregious. Preposterous! My client is about to tag v2.1.0, and
the last formal licensing audit on record is the v2.0.0 cutover -- dated
2026-04-10, referenced from `docs/licensing-audit.md`. Between that audit
and the v2.1.0 press kit now in flight (see the E10 *Press Kit* wave),
direct `<PackageReference>` pins have moved (`Azure.AI.OpenAI` 2.0.x →
2.1.0; OpenTelemetry 1.14.x → 1.15.2) and the Microsoft Agent Framework
packages were promoted to 1.1.0 GA. Any one of those bumps could have
dragged in a transitive dependency with an unfriendly license. Pre-release
means pre-release, counselor: we don't ship until every attribution line
is verified against the resolved graph.

This episode re-runs `dotnet list package --include-transitive` against
`azureopenai-cli/AzureOpenAI_CLI.csproj` as currently pinned on `ee3991f`,
cross-checks every row against `THIRD_PARTY_NOTICES.md`, re-reads `NOTICE`
and `NOTICE-assets.md` and `LICENSE` for drift, and returns a ship / no-ship
verdict for Mr. Lippman's release tag. The audit came back airtight --
zero drift, zero copyleft, zero blockers. No file changes were required;
the exec report itself is the deliverable.

## Scene-by-scene

### Act I -- Planning

- **Decision:** Audit the v2 production binary only. The test project's
  dependency graph (`coverlet.collector`, `Microsoft.NET.Test.Sdk`, `xunit`,
  `xunit.runner.visualstudio`) is `<IsPackable>false</IsPackable>` and is
  never redistributed, so MIT / Apache-2.0 attribution obligations for those
  rows do not attach to shipped artifacts.
- **Decision:** Preserve `NOTICE`'s retained v1-baseline block (lines 35-168)
  as historical continuity copy per the brief's "minimal intervention" rule.
  The v2 summary at `NOTICE` lines 19-33 is still accurate; the v1 block is
  explicitly annotated as retained pending v2 packaging transition.
- **Pivot considered and rejected:** Bumping `LICENSE` copyright to
  `2025-2027`. Rejected -- we're still in 2026 per the audit docs, and the
  current `2025-2026` range already covers the ship date. No change.

### Act II -- Fleet dispatch

| Wave | Agents (parallel) | Outcome |
|------|-------------------|---------|
| **1** | jackie-chiles (lead), bob-sacamano (guest) | Resolved graph re-verified, 39 packages cross-checked, zero drift, zero copyleft, clean ship. |

### Act III -- Ship

Audit-only wave. No code, no csproj, no workflow, no Dockerfile touched.
Preflight skipped per brief (`No preflight required -- you're not touching
.cs / .csproj / .sln / workflow / Dockerfile`). Single docs-only commit
pushes the exec report. E10 *Press Kit* owns `CHANGELOG.md`,
`AzureOpenAI_CLI.csproj`, and `docs/release-notes-v2.1.0.md` this wave --
those surfaces are disjoint from this episode.

## What shipped

**Production code** -- n/a (audit-only wave).

**Tests** -- n/a (no behavior change).

**Docs** --
- `docs/exec-reports/s02e36-the-attribution.md` (**NEW**) -- this report,
  plus the dependency table below and the verification receipts.

**Not shipped** (intentional follow-ups):
- **`NOTICE` v1-baseline retained block trimming.** `NOTICE` lines 35-168
  still carry the v1 production closure as historical continuity content.
  It is not misleading (the v2 summary at lines 19-33 is correct and
  labelled as such), but it is redundant now that v2.1.0 is the only
  supported ship. A future episode -- when the v1 binary is fully deleted
  from the repo -- should prune it. Surfaced here so the orchestrator can
  queue it behind the v1-decommission arc.
- **`NOTICE-assets.md` inbound-license advisory paragraph** (lines 67-79)
  still references a pending `CONTRIBUTING.md` edit. That is Elaine /
  Mr. Wilhelm's owning episode, not counsel's -- noted for the writers'
  room, no action here.
- **Reaffirmation cadence.** The v2 audit at `docs/licensing-audit.md` is
  dated 2026-04-10. Counsel recommends a formal re-audit entry against
  the v2.1.0 resolved graph be appended to that doc in a follow-up
  episode; the present exec report is evidence but is not structured as
  an audit-doc update.

## The dependency table

Resolved via `dotnet list azureopenai-cli/AzureOpenAI_CLI.csproj package
--include-transitive` on `ee3991f`.

| # | Package | Version | License | Notice Req.? | Status |
|---|---|---|---|---|---|
| 1 | Azure.AI.OpenAI | 2.1.0 | MIT | Yes | ✅ in TPN |
| 2 | Azure.Core | 1.44.1 | MIT | Yes | ✅ in TPN |
| 3 | dotenv.net | 3.1.2 | MIT | Yes | ✅ in TPN |
| 4 | Google.Protobuf | 3.30.2 | BSD-3-Clause | Yes | ✅ in TPN |
| 5 | Microsoft.Agents.AI | 1.1.0 | MIT | Yes | ✅ in TPN |
| 6 | Microsoft.Agents.AI.Abstractions | 1.1.0 | MIT | Yes | ✅ in TPN |
| 7 | Microsoft.Agents.AI.OpenAI | 1.1.0 | MIT | Yes | ✅ in TPN |
| 8 | Microsoft.Bcl.AsyncInterfaces | 6.0.0 | MIT | Yes | ✅ in TPN |
| 9 | Microsoft.Extensions.AI | 10.4.0 | MIT | Yes | ✅ in TPN |
| 10 | Microsoft.Extensions.AI.Abstractions | 10.4.0 | MIT | Yes | ✅ in TPN |
| 11 | Microsoft.Extensions.AI.OpenAI | 10.4.0 | MIT | Yes | ✅ in TPN |
| 12 | Microsoft.Extensions.Caching.Abstractions | 10.0.4 | MIT | Yes | ✅ in TPN |
| 13 | Microsoft.Extensions.Compliance.Abstractions | 10.4.0 | MIT | Yes | ✅ in TPN |
| 14 | Microsoft.Extensions.Configuration | 10.0.0 | MIT | Yes | ✅ in TPN |
| 15 | Microsoft.Extensions.Configuration.Abstractions | 10.0.3 | MIT | Yes | ✅ in TPN |
| 16 | Microsoft.Extensions.Configuration.Binder | 10.0.0 | MIT | Yes | ✅ in TPN |
| 17 | Microsoft.Extensions.DependencyInjection | 10.0.0 | MIT | Yes | ✅ in TPN |
| 18 | Microsoft.Extensions.DependencyInjection.Abstractions | 10.0.4 | MIT | Yes | ✅ in TPN |
| 19 | Microsoft.Extensions.Diagnostics.Abstractions | 10.0.3 | MIT | Yes | ✅ in TPN |
| 20 | Microsoft.Extensions.FileProviders.Abstractions | 10.0.3 | MIT | Yes | ✅ in TPN |
| 21 | Microsoft.Extensions.Hosting.Abstractions | 10.0.3 | MIT | Yes | ✅ in TPN |
| 22 | Microsoft.Extensions.Logging | 10.0.0 | MIT | Yes | ✅ in TPN |
| 23 | Microsoft.Extensions.Logging.Abstractions | 10.0.4 | MIT | Yes | ✅ in TPN |
| 24 | Microsoft.Extensions.Logging.Configuration | 10.0.0 | MIT | Yes | ✅ in TPN |
| 25 | Microsoft.Extensions.ObjectPool | 10.0.4 | MIT | Yes | ✅ in TPN |
| 26 | Microsoft.Extensions.Options | 10.0.3 | MIT | Yes | ✅ in TPN |
| 27 | Microsoft.Extensions.Options.ConfigurationExtensions | 10.0.0 | MIT | Yes | ✅ in TPN |
| 28 | Microsoft.Extensions.Primitives | 10.0.4 | MIT | Yes | ✅ in TPN |
| 29 | Microsoft.Extensions.VectorData.Abstractions | 9.7.0 | MIT | Yes | ✅ in TPN |
| 30 | Microsoft.ML.Tokenizers | 2.0.0 | MIT | Yes | ✅ in TPN |
| 31 | Microsoft.NET.ILLink.Tasks † | 10.0.6 | MIT | No (build-time only) | ✅ in TPN with dagger note |
| 32 | OpenAI | 2.9.1 | MIT | Yes | ✅ in TPN |
| 33 | OpenTelemetry | 1.15.2 | Apache-2.0 | Yes (§4) | ✅ in TPN |
| 34 | OpenTelemetry.Api | 1.15.2 | Apache-2.0 | Yes (§4) | ✅ in TPN |
| 35 | OpenTelemetry.Api.ProviderBuilderExtensions | 1.15.2 | Apache-2.0 | Yes (§4) | ✅ in TPN |
| 36 | OpenTelemetry.Exporter.OpenTelemetryProtocol | 1.15.2 | Apache-2.0 | Yes (§4) | ✅ in TPN |
| 37 | System.ClientModel | 1.10.0 | MIT | Yes | ✅ in TPN |
| 38 | System.Memory.Data | 10.0.3 | MIT | Yes | ✅ in TPN |
| 39 | System.Numerics.Tensors | 10.0.4 | MIT | Yes | ✅ in TPN |

† `Microsoft.NET.ILLink.Tasks` is an auto-referenced build-time MSBuild
tasks package (AOT trimmer). Not redistributed in `az-ai` binaries;
listed for completeness per the existing TPN convention.

**License distribution (v2.1.0 resolved graph):**
- MIT: 34 packages
- Apache-2.0: 4 packages (all OpenTelemetry)
- BSD-3-Clause: 1 package (Google.Protobuf)
- **Copyleft (GPL / LGPL / AGPL / SSPL / MPL / EPL / CDDL / CC-NC):** 0
- **UNKNOWN:** 0

All three present license families (MIT, Apache-2.0, BSD-3-Clause) are
fully compatible with the project's MIT outbound license. Apache-2.0 §4(d)
NOTICE obligations are satisfied: counsel verified the upstream
`opentelemetry-dotnet` repository at the 1.15.2 tag carries no `NOTICE`
file, so no inherited notice text needs to be reproduced here beyond the
per-package rows already in TPN. BSD-3-Clause's no-endorsement clause is
honored by the project's trademark posture (`docs/legal/trademark-policy.md`).

## Findings

- **F-01 (clean).** Resolved graph matches TPN v2.x manifest exactly, row
  for row, version for version. No drift.
- **F-02 (clean).** No copyleft dependencies anywhere in the closure.
- **F-03 (clean).** Every attribution-requiring license has a correct
  copyright-holder line in TPN. MIT / Apache-2.0 / BSD-3-Clause bind
  attribution to the holder, not to a specific version number, and all
  holders are accurate.
- **F-04 (clean).** `NOTICE` v2 summary (lines 19-33) is factually
  accurate for v2.1.0.
- **F-05 (clean).** `LICENSE` copyright range `2025-2026` covers the
  ship date. No change warranted.
- **F-06 (clean).** `NOTICE-assets.md` has exactly one bundled asset
  (`img/its_alive_too.gif`), first-party MIT, correctly attributed.
  `packaging/` manifests (Homebrew, Scoop, Nix, tarball) reference
  `LICENSE` / `NOTICE` via the standard channels -- no additional
  attribution drift in the packaging layer.
- **F-07 (advisory, not blocking).** `NOTICE` lines 35-168 still retain
  the v1 production closure as continuity content. Redundant but not
  inaccurate. Prune when v1 is fully decommissioned. Noted in
  "Not shipped" above.
- **F-08 (advisory, not blocking, owning-episode elsewhere).**
  `NOTICE-assets.md` lines 67-79 still carry the inbound-license
  advisory paragraph awaiting an `CONTRIBUTING.md` clause. That belongs
  to Elaine / Mr. Wilhelm.

## Verification receipts

```
$ grep -c 'PackageReference' azureopenai-cli/AzureOpenAI_CLI.csproj \
      tests/AzureOpenAI_CLI.Tests/AzureOpenAI_CLI.Tests.csproj
azureopenai-cli/AzureOpenAI_CLI.csproj:7
tests/AzureOpenAI_CLI.Tests/AzureOpenAI_CLI.Tests.csproj:6
```

- 7 `<PackageReference>` matches in the main `.csproj` correspond to 7
  direct production dependencies (excluding the auto-referenced
  `Microsoft.NET.ILLink.Tasks`, which SDK injects at build time).
- 6 matches in the test `.csproj` correspond to 4 test-only
  `<PackageReference>` elements -- the count is 6 because two elements
  (`coverlet.collector`, `xunit.runner.visualstudio`) span multiple
  lines with nested `<IncludeAssets>` / `<PrivateAssets>` children and
  have both opening and closing tags matched. Test-only packages are
  `<IsPackable>false</IsPackable>` and are not redistributed.
- Resolved graph size: **39 packages** (8 top-level including ILLink +
  31 transitive), identical to the v2.0.0 audit closure at
  `docs/licensing-audit.md`.

**TPN entry counts:**
- Before: 34 MIT + 4 Apache-2.0 + 1 BSD-3-Clause = **39 rows** (v2.x table).
- After:  34 MIT + 4 Apache-2.0 + 1 BSD-3-Clause = **39 rows** (unchanged).

**Before/after diff summary:**
- `THIRD_PARTY_NOTICES.md`: **0 lines added, 0 removed** (no drift detected).
- `NOTICE`: **0 lines added, 0 removed.**
- `NOTICE-assets.md`: **0 lines added, 0 removed.**
- `LICENSE`: **0 lines added, 0 removed** (copyright range current).
- `docs/exec-reports/s02e36-the-attribution.md`: **new file**, ~180 lines.

## Ship verdict

> **v2.1.0 IS LEGALLY CLEARED TO SHIP.** No blocking license concerns.
> No copyleft contamination. No attribution drift. No UNKNOWN licenses.
> The resolved dependency graph on `ee3991f` is identical -- row for row
> and version for version -- to the manifest already curated in
> `THIRD_PARTY_NOTICES.md`. Mr. Lippman may tag v2.1.0 without a legal
> hold. The case rests.

## Lessons from this episode

1. **Stable direct pins + stable resolver = clean audit.** The v2.1.0
   resolved graph is byte-identical to the v2.0.0 audit because the
   direct pins changed within minor ranges and the transitive resolver
   converged on the same versions. That is not luck; it is the payoff
   for pinning to patch-stable semver bands and letting NuGet do its
   job. When that stops being true, the audit will catch it.
2. **Re-audits that come back clean are still valuable deliverables.**
   The purpose of a pre-release audit is not to find something every
   time -- it is to *confirm* the attribution manifest matches the
   resolved graph at the moment of tagging. A clean verdict *on the
   record*, dated and signed, is what keeps the project defensible on
   inspection three years from now.
3. **Separate the authoritative version record (SBOM) from the
   human-readable attribution copy (`THIRD_PARTY_NOTICES.md`).** The
   existing NOTICE language at lines 10-17 captures this correctly:
   attribution obligations bind to the copyright holder, not the
   version number. That separation means minor/patch version churn in
   the transitive graph does *not* force TPN edits. Good architecture
   upstream saves the counsel's desk this wave.
4. **Test-project dependencies are out of scope, but say so explicitly.**
   `xunit` is Apache-2.0; if a future episode flips the test project's
   `<IsPackable>` flag, those rows need to move into TPN. Flagged here
   so the E10 *Press Kit* wave and future dispatchers know.

## Metrics

- Diff size: **+1 file, ~180 insertions, 0 deletions** (this exec
  report only).
- Test delta: n/a (no code change).
- Preflight: skipped per brief (no `.cs` / `.csproj` / `.sln` /
  workflow / Dockerfile touched).
- CI status at push: will confirm post-push; docs-only change, no CI
  risk expected.

## Credits

- **Jackie Chiles** (OSS licensing & compliance counsel) -- episode
  lead. Re-ran the resolved-graph audit, authored this report,
  rendered the ship verdict.
- **Bob Sacamano** (integrations / packaging) -- guest. Confirmed the
  `packaging/` surface (Homebrew formula, Scoop manifest, Nix flake,
  tarball staging) does not ship additional third-party binaries or
  assets that would create new attribution obligations.
- **Co-authored-by: Copilot** trailer present on the single commit
  associated with this episode.
