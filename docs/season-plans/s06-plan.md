# S06 *The Trust Layer* -- full-season plan

- **Season number:** 6
- **Theme:** The Trust Layer
- **Anchor release:** v3.0.0 at S06E12
- **Target episode count:** 12

## Theme

S05 made the CLI orchestrate. S06 makes the CLI *trustworthy enough to
run in production and packaged ecosystems*. The unifying arc covers
identity (managed identity, key vault, OIDC), supply chain (SBOM, signed
artifacts, reproducible builds), execution boundaries (plugin sandbox,
prompt-injection defense), distribution (Homebrew, Scoop, Nix), and the
governance scaffolding (telemetry consent, deprecation policy,
breaking-change inventory) that a 3.0.0 release demands. The cut at
S06E12 is the first major-version bump since v2.0.0; the season is
deliberately heavy on Newman, Jerry, Bob, and the legal/ethics bench.

## Episode roster

| #   | Code     | Title                  | Lead       | Co-lead   | Scope summary                                                                          |
|-----|----------|------------------------|------------|-----------|----------------------------------------------------------------------------------------|
| E01 | S06E01   | *The Identity*         | Newman     | Kramer    | Managed-identity auth path via `DefaultAzureCredential`, no static key fallback        |
| E02 | S06E02   | *The Vault*            | Kramer     | Newman    | Azure Key Vault integration for `AZUREOPENAIAPI` and Foundry keys                      |
| E03 | S06E03   | *The OIDC*             | Jerry      | Newman    | OIDC federation for GitHub Actions; no long-lived secrets in CI                        |
| E04 | S06E04   | *The Bill of Materials* | Newman    | Jackie    | SBOM publication + cosign/sigstore signing of release artifacts                        |
| E05 | S06E05   | *The Reproducible*     | Jerry      | Bania     | Reproducible Native AOT builds + a public verifier script                              |
| E06 | S06E06   | *The Sandbox*          | Kramer     | FDR       | Plugin sandbox + capability tokens for external tools (Pitt mid-season audit overlay) |
| E07 | S06E07   | *The Injection*        | Costanza   | FDR       | Prompt-injection-defense kit (input classifier, output guardrails, eval suite)         |
| E08 | S06E08   | *The Tap*              | Elaine     | Bob       | Homebrew formula + Scoop manifest + tap-repo onboarding docs                           |
| E09 | S06E09   | *The Flake*            | Bob        | Jerry     | Nix flake packaging + reproducibility cross-check                                      |
| E10 | S06E10   | *The Consent*          | Elaine     | Frank     | Opt-in telemetry consent UI, redaction policy, docs                                    |
| E11 | S06E11   | *The Inventory*        | Costanza   | Lippman   | Breaking-change inventory + deprecation policy + migration guide                       |
| E12 | S06E12   | *The Three-Oh*         | Lippman    | Pitt      | v3.0.0 cut + season finale + roadmap recap                                             |

## Dependency graph

- E01 *Identity* -> E02 *Vault* (Vault retrieval uses MI as the primary credential).
- E01 *Identity* -> E03 *OIDC* (OIDC is the CI-shaped specialization of MI).
- E02 *Vault* -> E03 *OIDC* (OIDC flow stores nothing locally; relies on Vault for fallback).
- E04 *Bill of Materials* -> E05 *Reproducible* (signed reproducible builds are the trust pair).
- E05 *Reproducible* -> E09 *Flake* (Nix flake is the reproducibility proof point).
- E06 *Sandbox* -> E07 *Injection* (defense kit runs inside the sandbox boundary).
- E07 *Injection* -> E11 *Inventory* (any behavioral change in injection handling lands in the breaking-change inventory).
- E08 *Tap* -> E09 *Flake* (packaging story compounds; Homebrew/Scoop first, Nix second).
- E08 *Tap* -> E12 *Three-Oh* (release ships through three+ package managers).
- E09 *Flake* -> E12 *Three-Oh* (same).
- E10 *Consent* -> E11 *Inventory* (consent default change is a breaking change).
- E11 *Inventory* -> E12 *Three-Oh* (the inventory is the release notes' spine).
- All -> E12 *Three-Oh* (release gate).

## Cast balance pre-flight

### Leads (12 slots)

- Costanza: 2 (E07, E11)
- Kramer: 2 (E02, E06)
- Elaine: 2 (E08, E10)
- Jerry: 2 (E03, E05)
- Newman: 2 (E01, E04)
- Bob: 1 (E09)
- Lippman: 1 (E12)

Every main-cast member clears the Rule 2 multi-lead floor. No back-to-back
leads in airing order (Rule 1 clean). Cross-season check: S05E12 closes on
Lippman, S06E01 opens on Newman -- no back-to-back across the boundary.

### Co-leads

- Kramer: E01
- Newman: E02, E03
- Jackie: E04
- Bania: E05
- FDR: E06, E07
- Bob: E08
- Jerry: E09
- Frank: E10
- Lippman: E11
- Pitt: E12 (plus E06 audit overlay)

### Support / guest seats (Rule 3 floor)

- Lloyd: E01 (junior lens on credential-prompt copy), E10 (reads the consent dialog)
- Wilhelm: E01 (credential-rotation process), E03 (CI change-management), E11 (deprecation governance)
- Morty: E02 (cost of Key Vault calls per session)
- Soup Nazi: E02 (style of secret references in config), E11 (deprecation message format)
- Bookman: E03 (CI script brevity), E12 (release-notes brevity)
- Russell: E04 (presentation of supply-chain badges in README), E08 (install-experience polish)
- Puddy: E05 (reproducibility regression suite), E11 (deprecation regression suite)
- Mickey: E05 (CLI ergonomics of the verifier script), E10 (a11y of consent UI)
- Maestro: E06 (sandbox semantics for prompt fragments), E07 (prompt engineering for defense), E12 (prompt-library v3 notes)
- Babu: E07 (i18n injection vectors and locale-aware filters)
- Keith: E08 (devrel demo of Homebrew install), E12 (launch talk)
- Uncle Leo: E08 (community welcome for new install paths), E12 (announcement)
- Sue Ellen: E09 (competitive Nix-vs-Homebrew vs. peers), E12 (positioning brief)
- Peterman: E09 (story copy for Nix flake), E12 (launch copy)
- Rabbi: E07 (responsible-use review of defense kit), E10 (consent ethics)
- Frank: E04 (artifact-signing reliability), E10 (telemetry SLOs)

Every one of the 22 supporting players appears at least once. Pairings
honored: Newman + FDR (E04 -> E06/E07 cluster), Kramer + Elaine
(E02 + E08 sequencing, plus E10 Elaine carries forward Kramer's E06
sandbox surface), Costanza + Lloyd (E11 inventory is read by Lloyd),
Mr. Wilhelm + Soup Nazi (E02 + E11), Jackie + Newman (E04), Frank
Costanza + Newman (E04 + E10), The Maestro + Costanza (E06 -> E07).

## Stop conditions / season-finale criteria

- v3.0.0 cuts at E12 with: MI auth, Key Vault, OIDC, signed SBOM,
  reproducible builds verifier, plugin sandbox, injection-defense kit,
  Homebrew + Scoop + Nix install paths, opt-in telemetry, deprecation
  policy, and a migration guide for v2.x users.
- CHANGELOG `[Unreleased]` rolled to a `3.0.0` block with explicit
  BREAKING CHANGE entries; v2-to-v3 migration doc shipped under
  `docs/migration-v2-to-v3.md`.
- All release artifacts cosign-verified end-to-end; a public verifier
  script reproduces the binary byte-for-byte (or documents the exact
  non-determinism budget).
- FDR-CRITICAL stop on the sandbox or injection-defense layers halts
  forward dispatch until Newman + FDR clear the finding.
- Any Rabbi-flagged responsible-AI red line on telemetry or injection
  defense pauses E10/E07 for re-design.
- Mid-season Pitt audit at E06 must pass before E07 dispatches.
- Bob's packaging episodes (E08, E09) must produce *working* install
  paths verified on a clean machine; doc-only PRs do not satisfy the
  E08/E09 close criteria.

## Open questions

1. Does v3.0.0 *remove* the static-key auth path or just deprecate it
   loudly? Newman wants removal; Costanza wants a one-major-version
   deprecation window for downstream Espanso/AHK users.
2. Plugin sandbox boundary: OS-level (process isolation, seccomp on
   Linux) or in-process (capability tokens only)? Kramer favors
   in-process for AOT/binary-size; FDR insists OS-level for a real
   trust claim.
3. Telemetry consent default: hard opt-in (no signal until explicit
   yes) vs. opt-in-on-first-run prompt with a 30-day grace period?
   Rabbi wants hard opt-in; Frank wants the prompt for SLO data.
4. Public package-manager onboarding order: Homebrew + Scoop first
   then Nix (E08 -> E09 as planned), or Nix first because flakes prove
   reproducibility? Bob has a preference; Sue Ellen has competitive
   data.
5. Does the prompt-injection-defense kit ship as a built-in mode or as
   an opt-in module loaded through the new sandbox? The latter dogfoods
   E06 but risks E07 slipping if E06 wobbles.
