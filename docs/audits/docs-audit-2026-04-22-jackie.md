# Docs Audit — Licensing & Compliance (Jackie Chiles)

**Date:** 2026-04-22
**Auditor:** Jackie Chiles (OSS compliance, legal posture)
**Release under review:** v2.0.4 (tag shipped 2026-04-22, commit `afa95fd`)
**Scope:** `LICENSE`, `NOTICE`, `THIRD_PARTY_NOTICES.md`, `docs/licensing-audit.md`,
`docs/legal/trademark-policy.md`, `CONTRIBUTORS.md`, `CONTRIBUTING.md`, `README.md`,
`SECURITY.md`, `azureopenai-cli/AzureOpenAI_CLI.csproj`,
`azureopenai-cli-v2/AzureOpenAI_CLI_V2.csproj`, `Dockerfile{,.v2}`,
`packaging/{homebrew,scoop,nix,tarball}/*`, `img/*`, `scripts/license-audit.sh`,
`scripts/license-allowlist.txt`.

> This is a **documentation audit**. No source or config files were modified.
> It's outrageous what can hide in a stale NOTICE file — egregious, even — so
> we are going to catalog every one of them, rank them, and hand the remediation
> to the right owner. Not legal advice.

---

## Executive summary

**Overall verdict:** ✅ **No distribution blocker.** MIT posture is intact,
no copyleft contamination was found, and the attribution plumbing
(`LICENSE` / `NOTICE` / `THIRD_PARTY_NOTICES.md`) is propagated correctly through
every packaging channel (Homebrew formula, Scoop manifest, Nix flake,
tarball staging, and both Dockerfiles). The `docs/licensing-audit.md`
action items filed on 2026-04-10 have **all** been executed in packaging
artifacts — verified file-by-file below.

**But we have work to do.** Nine findings, zero Critical, **one High**
(unattributed bundled GIF of unclear provenance), four Medium, three Low,
and one Informational. The High is the only thing with plausible
litigation surface area; the rest are hygiene and clarity work.

**Attribution-gap count:** 2 real gaps (v1 NOTICE block is stale;
`img/its_alive_too.gif` carries no source-or-license line).

**Copyleft contamination risk:** **None.** No GPL / LGPL / AGPL / MPL /
SSPL / EPL / CDDL / CC-BY-NC dependency appears in either csproj's direct
or transitive graph, and `scripts/license-allowlist.txt` + `license-audit.sh`
gate future drift with a `HARDFAIL_PATTERNS='^(GPL|LGPL|AGPL|MPL|SSPL|EPL|CDDL|CC-BY-NC)'`
rule.

---

## Severity ladder

| Severity | Meaning in this audit |
|---|---|
| **Critical** | Active license violation or GPL contagion. Block release, remediate immediately. |
| **High** | Plausible copyright / trademark exposure or a dependency unattributed in a distributed artifact. Fix before next release. |
| **Medium** | Policy clarity gap or stale attribution that will become a problem on the next dependency bump. Fix in the next milestone. |
| **Low** | Hygiene, freshness, or cross-reference cleanup. Fix when adjacent. |
| **Informational** | Not a finding; a note for future audits. |

---

## Findings

### F-01 — `img/its_alive_too.gif` has no source, license, or fair-use basis in the repo (High)

**File:** `README.md:12` — `![It's alive!](img/its_alive_too.gif)`; asset at
`img/its_alive_too.gif` (GIF89a, 1543×821, committed in `5e7c15c` "Added gif
of testing").

**Problem.** The README's hero image is an unattributed GIF. There is no
source URL, no copyright line, no license grant, and no fair-use rationale
recorded anywhere in the repo. The file name — `its_alive_too` — strongly
implies the 1931 Universal Pictures *Frankenstein* clip ("It's alive!
It's alive!"). That film entered the U.S. public domain at the end of 2026
for the *screenplay*, but Universal still asserts rights in the *audiovisual
work* through 2027, and in the makeup/costume design (Jack Pierce) separately.
Even if the underlying clip is clear, **we cannot prove it from this repo**,
and a redistributable GIF without an attribution line is exactly the kind
of asset that draws a takedown notice.

**Risk.** Moderate copyright exposure. The project is non-commercial-ish but
distributed through Homebrew, Scoop, Nix, GHCR, and GitHub Releases — all of
which copy the README. A Universal / Getty / studio-adjacent rights-holder
DMCA would force a forced-push history rewrite across every mirror. That is
the worst kind of Tuesday.

**Proposed fix:**
1. Replace with a first-party asset (a screenshot of the CLI running, an
   SVG logo from `img/`) **or** a clearly-labeled public-domain / CC0 GIF
   (e.g., from the Library of Congress public-domain film collection).
2. Add `img/README.md` (or an `ATTRIBUTIONS` block in `NOTICE`) recording:
   source URL, creator, license (e.g., `CC0-1.0`, `Public domain (US, pre-1929)`),
   and a one-line rationale if fair use is being claimed.
3. If we keep any third-party image, add it to `THIRD_PARTY_NOTICES.md`
   under a new "Bundled assets" section alongside its license text.

**Legal rationale.** Fair use is a defense, not a license; pleading it
after the fact is expensive. Clearing assets upstream of distribution costs
a PR.

**Owner:** Russell Dalrymple (UX / presentation) to pick the replacement;
Jackie to sign off on the attribution line.

---

### F-02 — v1 block in `NOTICE` (lines 30–157) is stale and inconsistent with `azureopenai-cli/AzureOpenAI_CLI.csproj` (High)

**File:** `NOTICE:30-157` vs `azureopenai-cli/AzureOpenAI_CLI.csproj:42-48`.

**Problem.** The bottom half of `NOTICE` is a per-package block labeled
"v1 baseline" and "retained for continuity." It lists:

| Package | NOTICE claims | Actual in `AzureOpenAI_CLI.csproj` |
|---|---|---|
| `Azure.AI.OpenAI` | 2.1.0 | **2.9.0-beta.1** |
| `Azure.Core` | 1.51.1 | 1.51.1 ✅ |
| `dotenv.net` | 3.1.2 | 3.1.2 ✅ |
| `OpenAI` (transitive) | 2.1.0 | **unknown** — transitive pinned by 2.9.0-beta.1 |
| `System.ClientModel` | 1.9.0 | transitive — version drifts with 2.9.0-beta.1 |
| `System.Memory.Data` | 10.0.1 | transitive |
| `Microsoft.Bcl.AsyncInterfaces` | 10.0.2 | transitive |
| (and 6 more `Microsoft.Extensions.*` at `10.0.2`) | pinned version numbers | transitive — resolver-dependent |

If v1 binaries are still being distributed (the `azureopenai-cli/` tree is
"maintenance-only" per `CONTRIBUTING.md`), the NOTICE file accompanying
those artifacts is advertising versions the csproj does not resolve. For
MIT attribution, copyright-holder accuracy matters more than version
accuracy, and those are intact. But an auditor reading `NOTICE` top-to-bottom
will note the contradiction with `THIRD_PARTY_NOTICES.md` and the csproj.

**Risk.** Low litigation risk (attribution still names the right parties),
but high embarrassment risk during any customer or enterprise procurement
review. Also actively misleading to downstream redistributors who rely on
our manifest.

**Proposed fix.** One of:
- **Preferred:** Delete `NOTICE:26-181` (the entire "v1 baseline" block)
  and have `NOTICE` point exclusively at `THIRD_PARTY_NOTICES.md` for
  the per-package manifest — `NOTICE` becomes a short Apache-2.0 §4(d)
  compliance file + trademark block.
- **Alternative:** Re-run `dotnet list azureopenai-cli/AzureOpenAI_CLI.csproj package --include-transitive`
  and refresh the v1 block verbatim. Only worth doing if v1 binaries are
  still produced — confirm with Mr. Lippman.

**Legal rationale.** Apache-2.0 §4(d) requires propagating the upstream
NOTICE; it doesn't require a particular version number, but an inaccurate
version record undermines the attribution's credibility.

**Owner:** Mr. Lippman (release) decides whether v1 is still shipped; Jackie
rewrites `NOTICE` accordingly.

---

### F-03 — `LICENSE` copyright line is a single-year 2025 and the repo now has 2026 commits (Medium)

**File:** `LICENSE:3` — `Copyright (c) 2025 SchwartzKamel`.

**Problem.** The MIT template carries a single-year copyright on a repo that
now has substantial 2026 contributions (v2 migration, v2.0.0–2.0.4).
MIT does not strictly require a year update, but the single-year form is
a common procurement-audit red flag ("is this project still maintained?
is copyright still asserted?").

**Proposed fix.** Update to `Copyright (c) 2025-2026 SchwartzKamel` (or
`2025, 2026` — both are acceptable; prefer the hyphen form for range). Add
a note to the release-runbook to bump on January 1 each year.

**Risk.** Very low. Cosmetic / procurement-audit optics.

**Owner:** Mr. Lippman, pre-release hygiene.

---

### F-04 — No DCO / CLA / inbound-license clause in `CONTRIBUTING.md` (Medium)

**File:** `CONTRIBUTING.md` (entirety — no mention of DCO, sign-off,
`Signed-off-by:`, CLA, Developer Certificate of Origin, or inbound license).

**Problem.** The project accepts PRs and adds `Co-authored-by:` trailers for
Copilot attribution, but nowhere does it state the inbound license. By
GitHub's ToS §D.6, public-repo contributions are licensed back under the
repo's license (MIT) by default — **but** that default only applies to
PRs submitted through the GitHub web UI; email patches, PR submissions
from forks hosted elsewhere, and offline contributions have no such
implicit grant. A one-line DCO-style statement or CLA closes that gap
and removes ambiguity.

**Proposed fix.** Add a short "License of contributions" section to
`CONTRIBUTING.md`:

> By submitting a contribution to this repository, you agree that your
> contribution is licensed to the project under the [MIT License](LICENSE)
> (inbound = outbound). If the contribution includes AI-assisted work,
> preserve the `Co-authored-by: Copilot <...>` trailer as described above.

Optional-but-recommended upgrade: require a **DCO sign-off** (`git commit
-s`) — lightweight, no CLA bureaucracy, and gates provenance cleanly. If
adopted, add a `DCO.txt` at repo root (copy the standard DCO 1.1 text) and
a GitHub Action (e.g., `dcoapp/app`) to enforce on PRs.

**Risk.** Low under GitHub-web-UI PRs; medium if the project ever accepts
patches outside GitHub (mailing list, mirror). Ambiguity becomes a problem
only if a contributor later asserts their contribution was not intended
as MIT.

**Owner:** Mr. Wilhelm (process), Jackie drafts the clause.

---

### F-05 — `README.md` contains no non-affiliation disclaimer for "Azure", "OpenAI", or "Microsoft" marks (Medium)

**File:** `README.md` — no disclaimer anywhere; only `NOTICE` and
`docs/legal/trademark-policy.md` carry the non-affiliation language.

**Problem.** The README is the project's public face. It says
"Azure OpenAI CLI" in the title, "Azure OpenAI agent" in the tagline,
and uses "Azure" / "OpenAI" throughout without a single line stating that
this project is not affiliated with, endorsed by, or sponsored by
Microsoft or OpenAI. Readers who don't drill into `NOTICE` never see
the disclaimer. Nominative-use doctrine does most of the heavy lifting
here, but the belt-and-suspenders posture is a one-liner.

**Proposed fix.** Add a short trademark footer to `README.md` (above the
"License" section), linking to the trademark policy:

> **Trademark notice.** "Microsoft", "Azure", ".NET", and "OpenAI" are
> trademarks of their respective owners and are used nominatively to
> identify the upstream products this project interoperates with. This
> project is an independent open-source tool and is not affiliated with,
> endorsed by, or sponsored by Microsoft Corporation, the .NET Foundation,
> or OpenAI, L.L.C. See [`docs/legal/trademark-policy.md`](docs/legal/trademark-policy.md).

**Risk.** Low. Current usage already satisfies the three-factor nominative-use
test. This is a clarity-and-hygiene improvement.

**Owner:** Elaine (docs), co-review with Jackie.

---

### F-06 — `README.md` carries no user-facing disclaimer about Azure OpenAI / OpenAI service terms (Medium)

**File:** `README.md` (entirety — no disclaimer about API costs, data
handling, ToS, or service-provider responsibility).

**Problem.** The CLI transmits user prompts to a third-party API (Azure
OpenAI or OpenAI). Users bring their own keys and are billed directly by
Microsoft / OpenAI. The README says "add Azure creds" and moves on. It
does not tell users:
- API usage is governed by Microsoft's Azure OpenAI Service terms and/or
  OpenAI's usage policies — users are responsible for their own compliance
  (rate limits, content policy, data-handling obligations, PII, etc.).
- Costs accrue to the user's Azure subscription / OpenAI billing account;
  this tool has no spend cap beyond what the user configures.
- No warranty on generated content; see MIT's §§15–16 disclaimers.

**Proposed fix.** Add a short "Responsibility" or "Terms & costs" section
near the "Configuration" section:

> **Using the Azure OpenAI / OpenAI API.** `az-ai` calls your configured
> provider with credentials you supply. Your use of the API is governed by
> your provider's terms — see the
> [Azure OpenAI Service terms](https://learn.microsoft.com/legal/cognitive-services/openai/)
> and [OpenAI usage policies](https://openai.com/policies/usage-policies).
> API usage is billed to your account; this tool does not cap spend.
> Generated content is provided as-is under the MIT LICENSE's
> no-warranty clauses.

**Risk.** Low direct liability risk (MIT disclaimer already covers
warranty); medium for first-time user surprise-bill and
terms-violation scenarios. Documenting the hand-off is cheap insurance.

**Owner:** Elaine drafts, Morty Seinfeld (FinOps) reviews spend language,
Jackie signs off.

---

### F-07 — `CONTRIBUTORS.md` is stale and self-describes a process that isn't being executed (Low)

**File:** `CONTRIBUTORS.md:37-39`.

**Problem.** The file lists only `@SchwartzKamel` and says "a maintainer
will add you during the next release pass." Three releases (v2.0.0,
v2.0.1, v2.0.2, v2.0.3, v2.0.4) have shipped since the v2 cutover
without any contributor additions. Either (a) no external contributions
were merged (plausible for a v2 sprint), or (b) the process is not
being executed.

**Proposed fix.** Either:
- **(Recommended)** Add a release-runbook step: `git shortlog -sne v2.0.3..v2.0.4`
  → propose new entries to `CONTRIBUTORS.md`. Makes the "process" real.
- **(Alternative)** Remove the "first contributors" section and point
  contributors at GitHub's auto-generated contributor graph
  (`https://github.com/SchwartzKamel/azure-openai-cli/graphs/contributors`),
  which is always current.

**Risk.** None legal. Hygiene.

**Owner:** Mr. Lippman (release runbook) or Uncle Leo (community).

---

### F-08 — No SBOM is produced or published alongside releases (Low)

**File:** search result — `docs/verifying-releases.md` references cosign
attestations but no SBOM; `scripts/` contains no `sbom.sh` / CycloneDX /
SPDX generator. No build step emits an SBOM artifact.

**Problem.** v2.0.4 ships binaries through Releases, GHCR, Homebrew,
Scoop, and Nix. Supply-chain reviewers (and increasingly, enterprise
procurement) expect a machine-readable SBOM (CycloneDX JSON or SPDX 2.3)
per release. `scripts/license-audit.sh` produces a text/JSON license
table — that's adjacent, not a substitute.

**Proposed fix.** Add `scripts/sbom.sh` that emits CycloneDX 1.5 JSON
using a standard tool (`dotnet-CycloneDX` is the canonical choice for
NuGet). Publish the SBOM as a release asset alongside the tarballs and
attach it to the cosign attestation. Link from `docs/verifying-releases.md`.

**Risk.** Not a legal exposure today. Becomes one when a large downstream
adopts an SBOM-mandatory procurement rule (EU CRA, US EO 14028 downstream,
various Fortune-500 vendor questionnaires).

**Owner:** Kramer (build / release tooling) with Newman sign-off
(supply-chain).

---

### F-09 — `docs/licensing-audit.md` "Action items" (§ "Action items", lines 138-161) are all done, but the doc still lists them as deferred (Informational)

**File:** `docs/licensing-audit.md:138-161`.

**Problem.** The 2026-04-10 audit listed six packaging follow-ups as
"deferred — not in scope of this audit." Cross-check confirms all six
have been executed:

| Action item | Status | Evidence |
|---|---|---|
| Homebrew `license "MIT"` + install NOTICE/THIRD_PARTY_NOTICES.md | ✅ | `packaging/homebrew/Formula/az-ai.rb:5,43` and three archived version formulae |
| Scoop `license` + bundle NOTICE/THIRD_PARTY_NOTICES.md | ✅ | `packaging/scoop/az-ai.json:4,24-26` (post-install surfaces paths) |
| Nix `meta.license = lib.licenses.mit` + install licenses | ✅ | `packaging/nix/flake.nix:112-120` |
| Dockerfile(s) COPY licenses + OCI label | ✅ | `Dockerfile:45,77` and `Dockerfile.v2:83,111` |
| Tarball staging script copies all three files | ✅ | `packaging/tarball/stage.sh:40,67` |
| Repo-root `NOTICE` updated to reflect Apache-2.0/BSD deps | ✅ (top section); see F-02 for the stale v1 bottom section |

**Proposed fix.** Either update `docs/licensing-audit.md` §"Action items"
to a "Status: closed, verified 2026-04-22" block, or delete the section
and note the closure in `CHANGELOG.md` under v2.0.4.

**Risk.** None. Documentation freshness.

**Owner:** Jackie (this is our own housekeeping).

---

## Dependency attribution coverage table

Source of truth: `azureopenai-cli-v2/AzureOpenAI_CLI_V2.csproj`
(v2, net10.0, 8 top-level PackageReference items) cross-referenced against
`THIRD_PARTY_NOTICES.md` and `docs/licensing-audit.md`. Transitive closure
is not re-enumerated here — that is the job of `scripts/license-audit.sh`
and is covered fully in the 2026-04-10 audit's per-package table (39
packages, verified).

Column legend: **Pkg** = package ID, **SPDX** = resolved license,
**NOTICE-req?** = Apache-2.0 §4(d) or BSD-3-Clause §2 obligation beyond
the manifest entry, **In THIRD_PARTY_NOTICES.md?** / **In NOTICE?** =
presence.

| Pkg (top-level) | Version | SPDX | NOTICE-req? | In THIRD_PARTY_NOTICES.md? | In NOTICE (top)? |
|---|---|---|---|---|---|
| Microsoft.Agents.AI | 1.1.0 | MIT | No | ✅ | implicit via license-family summary |
| Microsoft.Agents.AI.OpenAI | 1.1.0 | MIT | No | ✅ | implicit |
| Azure.AI.OpenAI | 2.1.0 | MIT | No | ✅ | implicit |
| dotenv.net | 3.1.2 | MIT | No | ✅ | implicit |
| OpenTelemetry | 1.15.2 | Apache-2.0 | Yes (§4(d); upstream has no NOTICE file — verified 2026-04-10 and re-affirmed here) | ✅ | ✅ |
| OpenTelemetry.Api | 1.15.2 | Apache-2.0 | Yes (same) | ✅ | ✅ |
| OpenTelemetry.Exporter.OpenTelemetryProtocol | 1.15.2 | Apache-2.0 | Yes (same) | ✅ | ✅ |
| (transitive) OpenTelemetry.Api.ProviderBuilderExtensions | 1.15.2 | Apache-2.0 | Yes (same) | ✅ | ✅ |
| (transitive) Google.Protobuf | 3.30.2 | BSD-3-Clause | Yes (§2 — no-endorsement clause honored by `docs/legal/trademark-policy.md`) | ✅ | ✅ |
| Microsoft.NET.ILLink.Tasks | 10.0.6 | MIT | No (build-time only, not redistributed) | ✅ (flagged as build-time) | n/a |

**v2 coverage verdict:** ✅ Every top-level and non-trivial transitive is
attributed; every Apache-2.0 and BSD-3-Clause package has the correct
notice/clause text reproduced.

**v1 coverage verdict:** ⚠️ `azureopenai-cli/AzureOpenAI_CLI.csproj`
declares `Azure.AI.OpenAI 2.9.0-beta.1` and `Azure.Core 1.51.1`; the v1
block in `NOTICE` claims `2.1.0` / does not list `Azure.Core` in the
top-level block. See F-02.

---

## Litigation-risk summary (likelihood × impact)

| # | Finding | Likelihood | Impact if triggered | Risk score |
|---|---|---|---|---|
| F-01 | Unattributed GIF of likely studio-owned origin in README | **Medium** — automated DMCA scanners hit GitHub-hosted media regularly; Universal / Getty-adjacent catalogs are well-policed | **Medium** — forced takedown, history rewrite across all mirrors (Homebrew, Scoop, Nix, GHCR), brand embarrassment | **Medium-High** |
| F-02 | Stale v1 block in NOTICE | Low | Low-Medium — attribution still names correct holders; principally embarrassment during procurement review | Low |
| F-03 | Single-year copyright in LICENSE | Very low | Very low — cosmetic only | Very low |
| F-04 | No DCO/CLA clause | Low (GitHub-web-UI PRs only today) | Medium if ever challenged — ambiguity requires individual contributor re-clearance | Low-Medium |
| F-05 | No non-affiliation line in README | Very low — nominative use is well-established | Low — trademark owner unlikely to sue; may send a polite letter | Very low |
| F-06 | No user-facing API-ToS disclaimer | Very low | Low-Medium for first-time surprise-bill user complaints (not the project's liability, but a support burden) | Low |
| F-07 | Stale CONTRIBUTORS.md | None | None | — |
| F-08 | No SBOM | Rising — EU CRA, enterprise procurement, SLSA L3 expectations | Low today, medium by 2027 | Low |
| F-09 | Stale closed-action-items section | None | None | — |

**Ranked overall:** F-01 → F-04 → F-02 → F-08 → F-06 → F-05 → F-03 → F-07/F-09.

---

## What's *good* (credit where credit is due)

- `scripts/license-audit.sh` + `scripts/license-allowlist.txt` +
  `HARDFAIL_PATTERNS='^(GPL|LGPL|AGPL|MPL|SSPL|EPL|CDDL|CC-BY-NC)'`: a real,
  executable, gateable license policy. This is the single most impactful
  legal-hygiene artifact in the tree. Wire it into CI on every `.csproj`
  or lockfile change if it isn't already.
- `docs/legal/trademark-policy.md`: a thorough, self-aware, well-drafted
  trademark posture — nominative-use three-factor test called out by
  name, third-party-mark handling, fork naming guidance, "Not legal
  advice" preamble. This is the model.
- `THIRD_PARTY_NOTICES.md`: license-text-deduplicated, SPDX-tagged,
  per-package copyright-holder attribution. Textbook.
- Apache-2.0 §4(d) NOTICE-inheritance check: **actually performed**
  (`HTTP 404` check against `opentelemetry-dotnet`'s `NOTICE` path).
  Auditors love this kind of evidence.
- All six packaging action items from the 2026-04-10 audit are **executed
  and verifiable** (Homebrew, Scoop, Nix, Docker×2, tarball). The release
  pipeline is legally defensible today.

---

## Reaffirmation cadence

Re-run **this** (docs) audit:

- On every dependency bump that changes `.csproj` PackageReference entries
  (Jerry / Kramer should ping Jackie).
- Before every minor release (`x.y.0`), as a Mr. Lippman pre-gate.
- After any new asset (image / font / icon / example data) lands in
  `img/`, `examples/`, or `docs/`.
- Quarterly, as a baseline — even with no dep changes, upstream
  NOTICE files and NuGet catalog license expressions can be amended.

Re-run the **dependency** audit (`scripts/license-audit.sh`) on every PR
that touches any `.csproj` (ideally as a GitHub Action).

---

## Sign-off

No Critical findings. No copyleft contamination. No distribution blocker
for v2.0.4.

High finding (F-01) should be remediated before the next README-touching
release. Medium findings should be queued for the v2.1.0 milestone.
Low / Informational findings are housekeeping — close them when adjacent
work comes through.

— Jackie Chiles
*Not legal advice. Outrage supplied at no extra cost.*
