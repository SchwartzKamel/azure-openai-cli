# ADR-016: Rename the project away from third-party marks (staged)

- **Status**: Proposed
- **Deciders**: the maintainer (gates G1 to G3 below); review lenses run in
  [FR-024](../proposals/FR-024-rebrand-away-from-third-party-marks.md)
- **Related**: [FR-024](../proposals/FR-024-rebrand-away-from-third-party-marks.md),
  [`docs/legal/trademark-policy.md`](../legal/trademark-policy.md), ADR-010
  (compat seam), ADR-011 (default provider), `scripts/brand-inventory.sh`

## Context

The project is named "Azure OpenAI CLI" and its binary is `az-ai`. Its own
trademark policy (section 3) says the Azure and OpenAI marks are used "only
nominatively": only so much of a mark as is necessary, and nothing that implies
endorsement. A product *name* that embeds both marks is source-identifying use,
which sits uneasily with that sentence. Whether it is an actual problem is a
question for counsel; this ADR does not claim it is.

Separately, the product has outgrown the name. It routes seven providers, speaks
MCP as client and server, runs a work queue and an eval harness. An earlier
review recommended against renaming, on two premises: that there were external
users with scripts and muscle memory, and that the name was only inconvenient.
Neither holds. The repository has 0 stars, 0 forks and 0 watchers, and the
maintainer is effectively the only user.

The cost is measurable and mostly mechanical: `az-ai` appears in 314 tracked
files (3,072 lines). The user-visible surfaces are few: the repo and image path,
the binary, the packaging manifests, the docs, and the environment variables.

## Decision

We rename in two reversible stages and leave the rest alone.

1. **Stage 1:** the display name and the repository (README title, help banner,
   docs, trademark policy short name, GitHub repo name). The binary stays `az-ai`.
2. **Stage 2:** the binary, with `az-ai` shipped as a working alias so existing
   Espanso, AHK and shell scripts keep working. The alias gets a sunset review at
   the next major version.

We do **not** rename the `AZUREOPENAIENDPOINT`-style environment variables (they
name the Azure service they configure, which is nominative use), the `AZ_AI_*`
variables, the C# namespace, or the source directory. Neutral env aliases may be
added later, additively.

The recommended name is `dunlin` (runner-up `curlew`), selected in the FR-024
addendum after 719 candidates were screened against package channels, GitHub and the
US trademark register. An earlier shortlist (`pith`, `usher`, `herald`) was withdrawn
when that screen found live software-class marks. The choice is the maintainer's to
confirm. No irreversible step runs until three gates clear:

- **G1:** the maintainer confirms the name.
- **G2:** counsel, or at least a full trademark and common-law search, clears it and
  rules on whether the current name needs to change. A preliminary US-federal screen
  is done (`scripts/name-screen.py`); it is not clearance.
- **G3:** approval to start Stage 1.

## Consequences

- Removes a standing tension between the project's name and its own policy, and
  makes the name match what the product does.
- Costs one repo rename, one binary rename with an alias, and a documentation
  pass. `scripts/brand-inventory.sh` makes the leftovers countable.
- The container image path follows the repo name, so old tags stay under the old
  package name. The release workflow must be verified before the rename.
- Keeping `az-ai` as an alias means the rename is not a breaking change (a minor
  bump); dropping it would be a major bump.
- Leaving environment variables and internal identifiers alone means the code
  still says `AzureOpenAI_CLI` internally. That is accepted: it carries no user
  benefit to change and a real risk.
- Discoverability dips for people searching the old name. Mitigated by keeping the
  phrase in the README description and writing "formerly Azure OpenAI CLI".

## Alternatives Considered

- **Keep the name, add a disclaimer.** The disclaimer already exists. The issue is
  the name itself.
- **Rename everything at once, including env vars and internals.** Breaks every
  script and touches 500+ files for no user benefit.
- **A new repository.** Loses issue and PR history and the redirect, which a
  rename keeps.
- **Rename without an alias.** A major bump and a broken script for the one user
  who exists.
- **Do nothing and revisit later.** The cost only rises as users arrive.

## References

- [FR-024](../proposals/FR-024-rebrand-away-from-third-party-marks.md): the
  six-round review, the evidence, and the name shortlist.
- `docs/legal/trademark-policy.md`, sections 0 and 3.
- `scripts/brand-inventory.sh`: reproducible baseline.
