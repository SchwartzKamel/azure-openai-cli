# OSS License Audit -- v1 Production CLI

> "It's outrageous, egregious, preposterous! You ship a binary, you pull
> down fifteen packages, and not one of you stops to ask whether the
> attribution paperwork is in order? That is the kind of oversight that
> ends careers, ends companies, ends *episodes*. Today we fix it. Today
> every package in this dependency closure has a name, a version, a
> copyright holder, and a license on file. No surprises. No GPL
> contagion. No UNKNOWNs hiding in a transitive ten levels deep. The
> case rests." -- Jackie Chiles, OSS counsel

This audit covers the runtime dependency closure of the v1 production CLI
(`azureopenai-cli/AzureOpenAI_CLI.csproj`, version 1.9.1) as resolved at
audit time. The v2 binary has its own (older) audit at
[`docs/licensing-audit.md`](../licensing-audit.md); the v2 manifest in
[`THIRD_PARTY_NOTICES.md`](../../THIRD_PARTY_NOTICES.md) is unchanged by this
episode.

## Summary

| Metric | Value |
|---|---|
| Total packages | 15 |
| Direct dependencies | 3 |
| Transitive dependencies | 12 |
| MIT-licensed | 15 |
| Apache-2.0-licensed | 0 |
| BSD / ISC / MPL / MS-PL | 0 |
| GPL / AGPL / LGPL family | 0 |
| Public domain / Unlicense / CC0 | 0 |
| UNKNOWN | 0 |

**Headline findings.** Every package in the v1 closure ships under MIT. No
copyleft contamination, no Apache-2.0 NOTICE obligations to satisfy upstream,
no UNKNOWN licenses to chase, and no commercial-use restrictions. The only
attribution obligation is the canonical MIT notice block, which is now
satisfied by [`THIRD_PARTY_NOTICES.md`](../../THIRD_PARTY_NOTICES.md) at the
repo root.

## Per-package classification

Direct dependencies are declared in `azureopenai-cli/AzureOpenAI_CLI.csproj`.
Transitive dependencies were captured via
`dotnet list package --include-transitive`. Each license was confirmed by
inspecting the `<license>` element in the cached `.nuspec` for the resolved
version under `~/.nuget/packages/<id>/<version>/<id>.nuspec`. The single
package without a `<license>` SPDX expression in its nuspec (`dotenv.net`)
declares its license via `<licenseUrl>` pointing at its repository LICENSE
file, which was fetched and confirmed as MIT.

| # | Package | Version | Direct? | License | Source |
|---|---|---|---|---|---|
| 1 | Azure.AI.OpenAI | 2.9.0-beta.1 | direct | MIT | Azure/azure-sdk-for-net |
| 2 | Azure.Core | 1.51.1 | direct | MIT | Azure/azure-sdk-for-net |
| 3 | dotenv.net | 3.1.2 | direct | MIT | bolorundurowb/dotenv.net |
| 4 | Microsoft.Bcl.AsyncInterfaces | 10.0.2 | transitive | MIT | dotnet/runtime |
| 5 | Microsoft.Extensions.Configuration.Abstractions | 10.0.2 | transitive | MIT | dotnet/runtime |
| 6 | Microsoft.Extensions.DependencyInjection.Abstractions | 10.0.2 | transitive | MIT | dotnet/runtime |
| 7 | Microsoft.Extensions.Diagnostics.Abstractions | 10.0.2 | transitive | MIT | dotnet/runtime |
| 8 | Microsoft.Extensions.FileProviders.Abstractions | 10.0.2 | transitive | MIT | dotnet/runtime |
| 9 | Microsoft.Extensions.Hosting.Abstractions | 10.0.2 | transitive | MIT | dotnet/runtime |
| 10 | Microsoft.Extensions.Logging.Abstractions | 10.0.2 | transitive | MIT | dotnet/runtime |
| 11 | Microsoft.Extensions.Options | 10.0.2 | transitive | MIT | dotnet/runtime |
| 12 | Microsoft.Extensions.Primitives | 10.0.2 | transitive | MIT | dotnet/runtime |
| 13 | OpenAI | 2.9.1 | transitive | MIT | openai/openai-dotnet |
| 14 | System.ClientModel | 1.9.0 | transitive | MIT | Azure/azure-sdk-for-net |
| 15 | System.Memory.Data | 10.0.1 | transitive | MIT | dotnet/runtime |

## License obligations and our compliance posture

### MIT (15 of 15 packages)

What MIT requires:

- The copyright notice and the MIT permission notice must be reproduced in
  "all copies or substantial portions of the Software."
- No advertising clause, no patent clause, no NOTICE file mandate, no
  source-disclosure obligation.

How we comply: [`THIRD_PARTY_NOTICES.md`](../../THIRD_PARTY_NOTICES.md) at
the repo root reproduces the MIT license text once and lists every package's
copyright holder in a manifest table. This file is shipped in the source
tree, included in source archives, and accompanies binary releases via the
GitHub Release notes that point to the tagged source. That satisfies the
"included in all copies or substantial portions" obligation for source and
binary redistribution alike.

### Apache 2.0 (0 of 15)

Not present in the v1 closure. The v2 closure does pull in OpenTelemetry
(Apache-2.0); see the v2 manifest in `THIRD_PARTY_NOTICES.md`. If we ever
add an Apache-2.0 dep to v1, we must:

- Reproduce the Apache-2.0 license text once in `THIRD_PARTY_NOTICES.md`.
- Include a `NOTICE` file in the distribution if the upstream project ships
  one (the patent-grant clause and notice-file requirement are the two
  things that distinguish Apache-2.0 from MIT).
- Preserve any required attribution strings the upstream NOTICE specifies.

### BSD-2 / BSD-3 / ISC (0 of 15)

Not present. Same posture as MIT if added: list the holder, reproduce the
license text once, satisfied.

### MPL 2.0 (0 of 15)

Not present. MPL 2.0 is weak file-level copyleft -- compatible with our
permissive distribution model so long as MPL-licensed source files retain
their headers and any modifications to those files are made available.
Allowed if needed; document the boundary if it ever arrives.

### MS-PL / MS-RL (0 of 15)

Not present. Microsoft-specific permissive licenses. Effectively
attribution-only; would slot into the manifest with their own license-text
section.

### GPL / AGPL / LGPL (0 of 15)

**No findings.** This is the headline you want from this audit. Any
appearance of a GPL-family license in the runtime closure of a permissive
CLI is a license-incompatibility incident, not a paperwork item -- AGPL in
particular would force network-service-style source disclosure that we
explicitly do not want to take on. Re-run the inventory step before every
release; if a transitive bump introduces a GPL-family package, escalate
immediately and either pin the previous transitive version or remove the
direct package that pulled it in.

### Public domain / Unlicense / CC0 (0 of 15)

Not present. No obligation if added.

### UNKNOWN (0 of 15)

**No findings.** Every package in this closure has either an SPDX
`<license>` expression in its nuspec or a `<licenseUrl>` pointing at a
project LICENSE file we have read and confirmed.

## Lloyd asks

> **Lloyd asks:** What's the difference between MIT and Apache 2.0?
>
> Both are permissive, attribution-only licenses, and from the user's
> perspective they let you do basically the same things: use the code, ship
> it, modify it, sell it, all without paying anyone or releasing your own
> source. Apache 2.0 adds two things MIT does not have. First, an explicit
> patent grant -- contributors grant you a license to any patents that read
> on their contribution, and that grant terminates if you sue them over
> those patents. Second, a `NOTICE` file convention -- if the upstream ships
> a `NOTICE`, downstream redistributors must preserve it alongside the
> license text. MIT has neither, which is why it fits in one paragraph and
> Apache 2.0 fits on four pages.
>
> **Lloyd asks:** What does GPL contagion mean?
>
> The GPL family of licenses (GPL, AGPL, LGPL with caveats) requires that
> derivative works be released under the same license. If you statically
> link or otherwise bake GPL-licensed code into your binary, your binary as
> a whole must be offered under the GPL too -- which means publishing your
> source under terms that let downstream users do the same thing. The
> "contagion" metaphor describes how a single GPL dependency can force the
> licensing terms of an entire downstream codebase. AGPL goes further and
> triggers on network use, not just distribution. For a permissive CLI like
> ours, allowing a GPL-family package into the runtime closure would force
> us to either relicense or remove the dep -- which is why the audit table
> tracks it as a release-blocking finding rather than a paperwork item.

These two callouts should also migrate to `docs/glossary.md` in the next
glossary update so that "MIT vs Apache 2.0" and "GPL contagion" become
project-wide terms of art rather than buried in a legal doc.

## Process: how to refresh this audit

Re-run before every release that bumps a direct dependency, or quarterly
otherwise. Steps:

1. Restore the project so the local NuGet cache is populated for the
   resolved versions:

   ```bash
   dotnet restore azureopenai-cli/AzureOpenAI_CLI.csproj
   ```

2. List all direct and transitive packages:

   ```bash
   dotnet list azureopenai-cli/AzureOpenAI_CLI.csproj package --include-transitive
   ```

3. For each unique `(id, version)` pair, inspect the cached nuspec for the
   SPDX license expression:

   ```bash
   for nuspec in ~/.nuget/packages/*/*/*.nuspec; do
     name=$(basename "$nuspec" .nuspec)
     ver=$(basename "$(dirname "$nuspec")")
     lic=$(grep -oP '<license[^>]*>[^<]*</license>' "$nuspec" | head -1)
     echo "$name $ver  $lic"
   done
   ```

4. For any package whose nuspec has no `<license>` expression, fall back
   to the `<licenseUrl>` element and fetch the upstream LICENSE file
   directly. Record the SPDX identifier in this audit.

5. For any package that cannot be classified from nuspec or licenseUrl,
   mark it as **UNKNOWN** in the table above and open a follow-up issue
   before the release ships. UNKNOWNs are release-blocking.

6. Update the manifest in `THIRD_PARTY_NOTICES.md` with any added or
   removed packages, refresh the version numbers, and confirm copyright
   holders are unchanged. Holders rarely change across minor or patch
   bumps; when they do, that is the supply-chain signal worth pausing on.

7. If any package switched into the GPL / AGPL / LGPL family, do **not**
   ship -- escalate and either pin the prior transitive version or remove
   the direct dep that introduced it.

## Scope and follow-ups

- The v2 production binary (`azureopenai-cli-v2`) and its 30+ deps are
  covered by the existing v2 manifest in `THIRD_PARTY_NOTICES.md` and the
  v2 audit at `docs/licensing-audit.md`. This episode did not re-verify
  that closure.
- The test projects (`tests/AzureOpenAI_CLI.Tests`,
  `tests/AzureOpenAI_CLI.V2.Tests`) and the spike (`spike/agent-framework`)
  were not audited. Test-only and spike-only packages are not redistributed
  with the shipped binary, so they do not carry attribution obligations
  for end-user redistribution -- but a future episode might still want to
  inventory them for completeness.
- Mr. Wilhelm's process episode owns whether to wire a license-checking
  step into CI (e.g., `dotnet-project-licenses` or a GitHub Action). This
  audit deliberately did not add one.
- The two Lloyd callouts above should migrate into `docs/glossary.md`.
