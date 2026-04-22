# Licensing Audit -- v2.0.0 Cutover

**Audit date:** 2026-04-10
**Auditor:** Jackie Chiles (OSS compliance)
**Scope:** Every NuGet package in the `azureopenai-cli-v2` dependency graph at the
versions pinned for v2.0.0 cutover.
**Source of truth:** `dotnet list package --include-transitive` against
`azureopenai-cli-v2/AzureOpenAI_CLI_V2.csproj` (net10.0), cross-referenced with
`api.nuget.org` catalog entries and upstream `LICENSE` / `NOTICE` files.

---

## Summary

| Metric | Value |
|---|---|
| Total packages in v2 graph | **39** (8 top-level + 31 transitive) |
| MIT-licensed | 34 |
| Apache-2.0-licensed | 4 (all OpenTelemetry) |
| BSD-3-Clause-licensed | 1 (Google.Protobuf) |
| Copyleft (GPL / LGPL / AGPL / SSPL / MPL) | **0** |
| Build-time-only (not redistributed) | 1 (`Microsoft.NET.ILLink.Tasks`) |
| **BLOCKERS** | **None** |
| **Overall verdict** | ✅ **CLEAR for v2.0.0 distribution under MIT.** |

New in v2 vs. v1 (license-family deltas):

- v1 NOTICE claimed "all dependencies are MIT." That is **no longer true** in v2.
  v2 introduces Apache-2.0 (OpenTelemetry) and BSD-3-Clause (Google.Protobuf)
  via the observability and MAF transitive stacks. Both are MIT-compatible and
  permissive; attribution obligations are satisfied via `THIRD_PARTY_NOTICES.md`.
- All Microsoft Agent Framework packages (`Microsoft.Agents.AI*`) are MIT --
  verified via `api.nuget.org` catalog entry `licenseExpression: MIT` and
  cross-referenced against the upstream repo.

---

## Per-package table

Columns: **Package | Version | License (SPDX) | Attribution Required | Copyright Holder | Notes**

### Top-level (declared in `.csproj`)

| Package | Version | License | Attrib. | Copyright | Notes |
|---|---|---|---|---|---|
| [Azure.AI.OpenAI](https://github.com/Azure/azure-sdk-for-net) | 2.1.0 | MIT | Yes | Microsoft Corporation | Azure SDK for .NET |
| [dotenv.net](https://github.com/bolorundurowb/dotenv.net) | 3.1.2 | MIT | Yes | Bolorunduro Winner-Timothy B (2017) | `licenseExpression` field is empty on NuGet; verified against upstream `LICENSE` file (MIT) |
| [Microsoft.Agents.AI](https://learn.microsoft.com/agent-framework/) | 1.1.0 | MIT | Yes | Microsoft Corporation | MAF core -- new in v2 |
| [Microsoft.Agents.AI.OpenAI](https://learn.microsoft.com/agent-framework/) | 1.1.0 | MIT | Yes | Microsoft Corporation | MAF OpenAI adapter -- new in v2 |
| [Microsoft.NET.ILLink.Tasks](https://dot.net/) | 10.0.6 | MIT | No | .NET Foundation | **Build-time only** (trimmer MSBuild tasks). Not redistributed in AOT binaries. Auto-referenced by SDK. |
| [OpenTelemetry](https://opentelemetry.io/) | 1.15.2 | Apache-2.0 | Yes | The OpenTelemetry Authors | **New in v2.** Apache-2.0 NOTICE inheritance: upstream repo carries no `NOTICE` file (verified 2026-04-10), so no inherited notice text is required. |
| [OpenTelemetry.Api](https://opentelemetry.io/) | 1.15.2 | Apache-2.0 | Yes | The OpenTelemetry Authors | See above |
| [OpenTelemetry.Exporter.OpenTelemetryProtocol](https://opentelemetry.io/) | 1.15.2 | Apache-2.0 | Yes | The OpenTelemetry Authors | Pulls `Google.Protobuf` (BSD-3-Clause) and `OpenTelemetry.Api.ProviderBuilderExtensions` (Apache-2.0) |

### Transitive

| Package | Version | License | Attrib. | Copyright | Notes |
|---|---|---|---|---|---|
| [Azure.Core](https://github.com/Azure/azure-sdk-for-net) | 1.44.1 | MIT | Yes | Microsoft Corporation | |
| [Google.Protobuf](https://github.com/protocolbuffers/protobuf) | 3.30.2 | BSD-3-Clause | Yes | Google Inc. (2008) | **Only BSD package** in graph. Verified upstream `LICENSE` -- no-endorsement clause applies; nominative use only. No upstream `NOTICE` file. |
| [Microsoft.Agents.AI.Abstractions](https://learn.microsoft.com/agent-framework/) | 1.1.0 | MIT | Yes | Microsoft Corporation | MAF abstraction surface |
| [Microsoft.Bcl.AsyncInterfaces](https://dot.net/) | 6.0.0 | MIT | Yes | .NET Foundation and Contributors | Older pin than v1 (was 10.0.2); pulled via `Azure.Core` |
| [Microsoft.Extensions.AI](https://dot.net/) | 10.4.0 | MIT | Yes | .NET Foundation and Contributors | |
| [Microsoft.Extensions.AI.Abstractions](https://dot.net/) | 10.4.0 | MIT | Yes | .NET Foundation and Contributors | |
| [Microsoft.Extensions.AI.OpenAI](https://dot.net/) | 10.4.0 | MIT | Yes | .NET Foundation and Contributors | |
| [Microsoft.Extensions.Caching.Abstractions](https://dot.net/) | 10.0.4 | MIT | Yes | .NET Foundation and Contributors | |
| [Microsoft.Extensions.Compliance.Abstractions](https://dot.net/) | 10.4.0 | MIT | Yes | .NET Foundation and Contributors | |
| [Microsoft.Extensions.Configuration](https://dot.net/) | 10.0.0 | MIT | Yes | .NET Foundation and Contributors | |
| [Microsoft.Extensions.Configuration.Abstractions](https://dot.net/) | 10.0.3 | MIT | Yes | .NET Foundation and Contributors | |
| [Microsoft.Extensions.Configuration.Binder](https://dot.net/) | 10.0.0 | MIT | Yes | .NET Foundation and Contributors | |
| [Microsoft.Extensions.DependencyInjection](https://dot.net/) | 10.0.0 | MIT | Yes | .NET Foundation and Contributors | |
| [Microsoft.Extensions.DependencyInjection.Abstractions](https://dot.net/) | 10.0.4 | MIT | Yes | .NET Foundation and Contributors | |
| [Microsoft.Extensions.Diagnostics.Abstractions](https://dot.net/) | 10.0.3 | MIT | Yes | .NET Foundation and Contributors | |
| [Microsoft.Extensions.FileProviders.Abstractions](https://dot.net/) | 10.0.3 | MIT | Yes | .NET Foundation and Contributors | |
| [Microsoft.Extensions.Hosting.Abstractions](https://dot.net/) | 10.0.3 | MIT | Yes | .NET Foundation and Contributors | |
| [Microsoft.Extensions.Logging](https://dot.net/) | 10.0.0 | MIT | Yes | .NET Foundation and Contributors | |
| [Microsoft.Extensions.Logging.Abstractions](https://dot.net/) | 10.0.4 | MIT | Yes | .NET Foundation and Contributors | |
| [Microsoft.Extensions.Logging.Configuration](https://dot.net/) | 10.0.0 | MIT | Yes | .NET Foundation and Contributors | |
| [Microsoft.Extensions.ObjectPool](https://dot.net/) | 10.0.4 | MIT | Yes | .NET Foundation and Contributors | |
| [Microsoft.Extensions.Options](https://dot.net/) | 10.0.3 | MIT | Yes | .NET Foundation and Contributors | |
| [Microsoft.Extensions.Options.ConfigurationExtensions](https://dot.net/) | 10.0.0 | MIT | Yes | .NET Foundation and Contributors | |
| [Microsoft.Extensions.Primitives](https://dot.net/) | 10.0.4 | MIT | Yes | .NET Foundation and Contributors | |
| [Microsoft.Extensions.VectorData.Abstractions](https://dot.net/) | 9.7.0 | MIT | Yes | .NET Foundation and Contributors | Pulled by MAF -- Semantic Kernel lineage |
| [Microsoft.ML.Tokenizers](https://dot.net/ml) | 2.0.0 | MIT | Yes | .NET Foundation and Contributors | Pulled by `Microsoft.Extensions.AI` |
| [OpenAI](https://github.com/openai/openai-dotnet) | 2.9.1 | MIT | Yes | OpenAI | **Bumped** from v1's 2.1.0 → v2's 2.9.1. Still MIT. |
| [OpenTelemetry.Api.ProviderBuilderExtensions](https://opentelemetry.io/) | 1.15.2 | Apache-2.0 | Yes | The OpenTelemetry Authors | No upstream NOTICE |
| [System.ClientModel](https://github.com/Azure/azure-sdk-for-net) | 1.10.0 | MIT | Yes | .NET Foundation and Contributors | |
| [System.Memory.Data](https://dot.net/) | 10.0.3 | MIT | Yes | .NET Foundation and Contributors | |
| [System.Numerics.Tensors](https://dot.net/) | 10.0.4 | MIT | Yes | .NET Foundation and Contributors | Pulled by `Microsoft.ML.Tokenizers` |

---

## Transitively-pulled high-risk packages

**None.** No GPL, LGPL, AGPL, MPL, SSPL, EPL, CDDL, or CC-NC dependency appears
in the resolved v2 graph.

The only non-MIT licenses are:

1. **Apache-2.0** -- OpenTelemetry stack (4 packages). MIT-compatible, permissive,
   no source disclosure requirement. Attribution obligation discharged via
   `THIRD_PARTY_NOTICES.md`. Apache-2.0 §4(d) requires carrying forward any
   upstream `NOTICE` file; `opentelemetry-dotnet` has none (verified via
   `HTTP 404` on `raw.githubusercontent.com/open-telemetry/opentelemetry-dotnet/main/NOTICE`),
   so no inheritance is required.
2. **BSD-3-Clause** -- Google.Protobuf (1 package). MIT-compatible. The
   no-endorsement clause (§3) requires us to not use "Google" or contributor
   names to endorse or promote `az-ai`. We don't, and our trademark policy
   already forbids this.

---

## Distribution implications

`az-ai` (v2) is shipped as:

- Native AOT binaries (Linux/macOS/Windows) -- produced by `dotnet publish -p:PublishAot=true`.
- Container images -- `Dockerfile` multi-stage, publishes a self-contained binary.
- Homebrew formula, Scoop manifest, Nix flake -- each points at a release tarball
  containing the binary and adjacent license files.

Every distributed artifact MUST include:

| File | Required by | Content |
|---|---|---|
| `LICENSE` | MIT §1 (project) | Already present at repo root. |
| `NOTICE` | Apache-2.0 §4(d) (OTel), convention | Updated in this audit. |
| `THIRD_PARTY_NOTICES.md` | MIT attribution, Apache-2.0 §4(a), BSD-3-Clause §2 | **New -- created in this audit.** |

Shipment checklist (owned by Mr. Lippman's release process):

- [ ] Tarballs (`dist/az-ai-*.tar.gz`) contain `LICENSE`, `NOTICE`, `THIRD_PARTY_NOTICES.md` alongside the binary.
- [ ] Container image copies the three files into `/licenses/` (OCI label convention `org.opencontainers.image.licenses=MIT`).
- [ ] Homebrew / Scoop / Nix formulae declare `license "MIT"` and the release bundles include the three files.
- [ ] GitHub release notes link to `THIRD_PARTY_NOTICES.md` at the tagged commit.

---

## Action items

Packaging metadata (deferred -- not in scope of this audit; follow-up issues):

1. **`packaging/homebrew/az-ai.rb`** -- confirm `license "MIT"` is declared
   and that the install block copies `NOTICE` and `THIRD_PARTY_NOTICES.md`
   into `pkgshare` (or equivalent). Hand off to Bob Sacamano.
2. **`packaging/scoop/az-ai.json`** -- add `"license": "MIT"` (Scoop schema)
   and include `NOTICE` + `THIRD_PARTY_NOTICES.md` in the extracted payload.
3. **`packaging/nix/flake.nix`** -- set `meta.license = lib.licenses.mit;`
   and include all three files in the derivation output.
4. **`Dockerfile`** -- add `COPY LICENSE NOTICE THIRD_PARTY_NOTICES.md /licenses/`
   to the final stage and the OCI label
   `org.opencontainers.image.licenses=MIT`. Hand off to Kramer.
5. **`packaging/` tarball script** -- update the release tar staging to copy
   all three files alongside the binary. Hand off to Mr. Lippman.
6. **v1 `NOTICE` claim update** -- v1's NOTICE asserted "all deps are MIT."
   v2 introduces Apache-2.0 and BSD-3-Clause. The repo-root `NOTICE` has
   been updated by this audit to reflect that truthfully.

These are **packaging-configuration** changes and were deliberately not
made in this audit (per scope rules). Each should open a tracked issue
before v2.0.0 ships.

---

## Reaffirmation timeline

Re-run this audit:

- **Every MAF version bump** (`Microsoft.Agents.AI*`) -- these packages are
  pre-1.0-era in API stability and transitive closures can swing.
- **Every OpenTelemetry minor bump** -- Apache-2.0 NOTICE status can change
  between releases.
- **Every `Microsoft.Extensions.AI*` version bump** -- pulls a large transitive
  surface that can introduce new packages.
- **Quarterly baseline** -- even without version changes, re-verify the NuGet
  catalog entries (license expressions can be amended after publish).
- **Before every signed release** (Mr. Lippman gate) -- spot-check that no
  unexpected transitive has entered via NuGet resolver drift.

Audit method is reproducible; see the `curl` + `api.nuget.org` + `python3`
snippet in the session log. A scripted version should be added to
`scripts/license-audit.sh` as a follow-up.

---

## Verification performed

- ✅ Ran `dotnet list package --include-transitive` against the v2 csproj.
  All 39 packages appear in the per-package table above.
- ✅ Queried `api.nuget.org` catalog entries for all non-trivial packages
  (MAF, OpenTelemetry, Google.Protobuf, OpenAI, dotenv.net, Azure SDK,
  Microsoft.Extensions.AI, Microsoft.ML.Tokenizers, ILLink).
- ✅ Spot-checked three obscure packages against upstream `LICENSE` files:
  `dotenv.net` (MIT confirmed), `Google.Protobuf` (BSD-3-Clause confirmed),
  `Microsoft.Extensions.VectorData.Abstractions` (MIT via catalog).
- ✅ Verified OpenTelemetry-dotnet carries no upstream `NOTICE` file
  (HTTP 404) -- no Apache-2.0 §4(d) inheritance obligation.
- ✅ No GPL / LGPL / AGPL / MPL / SSPL / EPL / CDDL / CC-NC present.

**Verdict: CLEAR for v2.0.0 cutover.**

-- Jackie Chiles
