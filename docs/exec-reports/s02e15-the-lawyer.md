# S02E15 -- *The Lawyer*

> Jackie Chiles audits every dependency in the v1 production binary for
> license obligations and ships a refreshed `THIRD_PARTY_NOTICES.md` so
> nobody redistributing this CLI gets surprised by paperwork.

**Commit:** `fc58fb4` (parent) -- see Credits for episode SHAs
**Branch:** `main` (direct push, solo-led project per `.github/skills/commit.md`)
**Runtime:** ~10 minutes
**Director:** Larry David (showrunner)
**Cast:** Jackie Chiles (lead, OSS counsel), Lloyd Braun (guest, junior dev asks)

## The pitch

The v1 production CLI has grown a real dependency closure: 3 direct NuGet
packages, 12 more pulled in transitively. Every one of them carries some
license obligation -- usually attribution, sometimes a NOTICE file,
occasionally something worse. Anyone who downloads a binary release and
wants to redistribute it (Homebrew tap, Scoop bucket, internal mirror) needs
a single source of truth for what they are agreeing to. Today's episode
generates that source of truth.

The constraint: this is a docs-only audit. We classify, we attribute, we
flag findings; we do not yank dependencies, we do not change the project's
own LICENSE, we do not bolt a license-checking step onto CI. Mr. Wilhelm's
process episode owns CI gating; this episode owns the inventory.

## Scene-by-scene

### Act I -- Inventory

Restored `azureopenai-cli/AzureOpenAI_CLI.csproj` and ran
`dotnet list package --include-transitive`. Closure: 15 packages total. 3
direct (`Azure.AI.OpenAI` 2.9.0-beta.1, `Azure.Core` 1.51.1,
`dotenv.net` 3.1.2), 12 transitive (`Microsoft.Bcl.AsyncInterfaces`, eight
`Microsoft.Extensions.*` abstractions, `OpenAI` .NET SDK,
`System.ClientModel`, `System.Memory.Data`).

### Act II -- Classify

Inspected the cached `.nuspec` for each `(id, version)` pair under
`~/.nuget/packages/`. 14 of 15 declare an SPDX `<license>` expression: all
MIT. The lone exception (`dotenv.net`) declares only a `<licenseUrl>`
pointing at its repo LICENSE; fetched it -- MIT, dated 2017, holder
"Bolorunduro Winner-Timothy B". Zero Apache-2.0, zero BSD, zero MPL, zero
MS-PL, zero GPL family, zero UNKNOWN. Cleanest possible verdict.

### Act III -- Notices file

The repo already had a `THIRD_PARTY_NOTICES.md` covering the v2 closure
(33 packages across MIT / Apache-2.0 / BSD-3-Clause). Rather than
overwrite a working artifact, extended it with a new "Manifest -- v1.x
dependency graph" section listing all 15 v1 packages with versions,
copyright holders, and upstream LICENSE URLs. Renamed the existing
"Manifest" section to "Manifest -- v2.x dependency graph" for symmetry,
and updated the intro paragraph to point readers at the new v1 audit doc.

### Act IV -- Audit doc

Created `docs/legal/license-audit.md`. Opens with a Jackie Chiles voice
paragraph, then a summary table, then the per-package classification, then
a per-license-type section explaining what each license requires and where
we stand. The MIT section confirms compliance via `THIRD_PARTY_NOTICES.md`;
every other section documents the posture for if/when a new license type
shows up (Apache 2.0 needs NOTICE handling, GPL family is release-blocking,
etc.).

### Act V -- Lloyd asks

Two callouts in the audit doc, then promoted to `docs/glossary.md` under a
new "Added in S02E15" subsection: "MIT vs Apache 2.0" (patent grant +
NOTICE convention is the difference) and "GPL contagion" (derivative-work
relicense obligation, AGPL extends to network use, release-blocking for a
permissive CLI). Glossary already existed (E08 shipped it earlier today),
so the brief's fallback path -- inline-only callouts -- did not apply.

### Act VI -- CHANGELOG + exec report

Two surgical bullets at the top of `[Unreleased] > Added` for the notices
file extension and the new audit doc. Exec report (this file) follows the
template.

## What shipped

**Production code** -- none. Docs-only episode.

**Tests** -- none added. No code changed.

**Docs**

- `docs/legal/license-audit.md` (new): v1 audit, ~210 lines. Per-package
  table, per-license posture, Lloyd callouts, refresh process with exact
  `dotnet` commands.
- `THIRD_PARTY_NOTICES.md` (extended): added v1.x manifest section,
  renamed v2 section, updated intro. Net delta ~45 lines.
- `docs/glossary.md` (appended): "MIT vs Apache 2.0" and "GPL contagion"
  entries under a new "Added in S02E15" subsection. ~30 lines.
- `CHANGELOG.md`: two bullets at top of `[Unreleased] > Added`.

**Not shipped** (intentional follow-ups):

- Did NOT change any dependency. Closure is clean; nothing to remove. If a
  future bump introduces a non-MIT package, the audit doc tells the next
  contributor exactly what to do.
- Did NOT add a license-checking CI step. Belongs to Mr. Wilhelm's process
  episode (`dotnet-project-licenses` or a GitHub Action are the obvious
  candidates).
- Did NOT change the project's own `LICENSE` file. Out of scope.
- Did NOT audit the v2 production binary. The existing v2 manifest in
  `THIRD_PARTY_NOTICES.md` and `docs/licensing-audit.md` are unchanged;
  the v2 audit is a separate episode.
- Did NOT audit the test projects or `spike/agent-framework`. Test-only and
  spike-only packages are not redistributed in the user-facing binary, so
  they carry no end-user attribution obligation. A future "completeness"
  pass could still inventory them.
- Did NOT touch the v2 portion of `THIRD_PARTY_NOTICES.md` -- only added
  the new v1 section and reframed the intro.

## Lessons from this episode

1. **No GPL findings, no UNKNOWNs.** The headline outcome. Every package
   resolved cleanly with an SPDX expression in its nuspec or a fetchable
   LICENSE URL. This is the result you want from a license audit, but it
   is not the result you should assume; the audit-doc Process section
   exists so the next refresh actually re-checks rather than rubber-stamps.
2. **Two manifests, one notices file.** v1 and v2 binaries have different
   closures (v1 is 15 packages MIT-only, v2 is 33 packages across three
   licenses). Keeping both manifests in one `THIRD_PARTY_NOTICES.md` --
   each clearly labelled by binary version -- is less error-prone than
   forking the file or asking redistributors to chase two attribution
   documents.
3. **Glossary first, audit second.** Two terms ("MIT vs Apache 2.0", "GPL
   contagion") that started life as inline callouts in a legal doc are
   now project-wide vocabulary. That conversion is cheap and pays off the
   first time anyone else has to write or read a license discussion.
4. **Beta package in production closure.** `Azure.AI.OpenAI 2.9.0-beta.1`
   is a beta SDK on the v1 critical path. License is fine (MIT), but the
   stability flag belongs in a different episode -- supply-chain or
   release-management. Noted here so it does not get lost.

## Metrics

- Diff size: 4 files touched. Net +260 lines across notices, audit, glossary,
  changelog (no deletions of substance).
- Test delta: none.
- Preflight: not required (docs-only, no `.cs` / `.csproj` / `.sln` /
  workflow changes).
- Pre-validation: smart-quote / em-dash scan on `docs/legal/license-audit.md`,
  `docs/glossary.md` additions, and `CHANGELOG.md` additions returned clean.
  (Pre-existing CHANGELOG entries from prior episodes contain em-dashes;
  out of scope for this episode.)
- Inventory: 15 packages audited, 100% MIT, 0 UNKNOWN, 0 GPL findings.
- CI status at push: see Credits SHAs; verify via the `ci.yml` and
  `docs-lint.yml` runs on the merge commit.

## Credits

- **Jackie Chiles** -- lead. License inventory, classification, notices
  file extension, audit doc, refresh process documentation.
- **Lloyd Braun** -- guest. Asked the two questions that became the
  glossary entries. Earned his SAG card.
- **Babu Bhatt** (uncredited) -- shipped `docs/glossary.md` in S02E08
  earlier the same day, so the inline-only fallback was not needed.

Co-author trailer (`Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>`)
applied to all commits associated with this episode.
