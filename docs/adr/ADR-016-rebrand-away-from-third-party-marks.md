# ADR-016: Rename the project away from third-party marks (staged)

- **Status**: Accepted (stage 1 implemented; stages 2 and 3 open)
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

1. **Stage 1 (done, S04E14):** the name and the command. The binary is `hopper` and
   `az-ai` ships beside it as a working alias, so existing Espanso, AHK and shell
   scripts keep working. README title, help banner, completions, trademark policy
   short name, the Docker image's binary, `make install` and the archive contents all
   follow. The alias gets a sunset review at the next major version.
2. **Stage 2 (with the release that carries Stage 1):** the release archive names
   (`az-ai-<ver>-<rid>` to `hopper-<ver>-<rid>`), the Homebrew formula, the Scoop
   manifest and the Nix flake. These are held back on purpose: the manifests download
   published assets by their current names, and Scoop's autoupdate builds the next
   URL from the old pattern. Until they move, archives keep the old names and
   contain both binaries.
3. **Stage 3 (a separate decision):** the GitHub repository name. The container image
   path follows it, so old tags stay under the old package name.

We do **not** rename the `AZUREOPENAIENDPOINT`-style environment variables (they
name the Azure service they configure, which is nominative use), the `AZ_AI_*`
variables, the C# namespace, or the source directory. Neutral env aliases may be
added later, additively.

The recommended name is `hopper`, with `anymux` as the descriptive runner-up (FR-024,
third addendum). The owner dropped the trademark stage, so 186 names were screened on
open-source grounds (commands, builtins, Homebrew and Scoop, GitHub and package registries)
against a table of 26 competitor tools. `anymux` was the pick of the second addendum
(109 candidates, with a US-register stage that no longer applies).
Earlier picks were withdrawn: `pith` and `usher` on trademark evidence, and `dunlin` and
`curlew` because the owner found them meaningless. No irreversible step runs until three
gates clear:

- **G1:** the maintainer confirms the name. Done: `hopper`, 2026-11-02.
- **G2:** counsel, or at least a full trademark and common-law search, clears it and
  rules on whether the current name needs to change. Waived by the maintainer for this
  open-source project; the open-source screen is `scripts/name-screen.py --oss` and is
  not clearance.
- **G3:** approval to start. Stage 1: given. Stage 3 (the repository): not yet given.

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

## Update 2026-11-05: sequencing Stages 2 and 3, and G3

G3 is given for both remaining stages (the maintainer, 2026-11-05). The order is **Stage 2, then the v2.4.0 release,
then the manifest hash-sync, then Stage 3 (the repository rename)**.

I first proposed renaming the repository *before* the release, so that the whole v2.4.0 release, including its
container image, would live under the new name. A deliberation by smith's legion (planner, reviewer and oracle, one
round) recommended the opposite order and made the better argument, and it is adopted:

- Renaming alongside a release combines two migrations (archive and manifest names, and the container path) in one
  event, which complicates recovery if either goes wrong. The release is the riskier, less reversible step, so it should
  not carry a second variable.
- The image path is derived from the repository name at publish time, so v2.4.0's image stays under the old path and
  later releases move to the new one. With one user the cost is small; the old path's support window should be stated
  when Stage 3 happens.
- Manifests must keep pointing at known-good assets until the v2.4.0 assets are published and verified, so they change
  in the hash-sync, never before.

The council was shallow on this repository's specifics (it told us to audit a `go.mod` that a .NET project does not
have, and read G3 as still pending), so it was treated as one signal and the rest was checked directly. Stage 3 also
needs smith's own configuration updated: its project entry names this repository by its GitHub slug.

## Update 2026-11-08: Stages 2 and 3 are done

- **Stage 2:** v2.4.0 was published with `hopper-<version>-<rid>` archives (each verified before upload, then audited after: checksums, provenance, a
  real run, the container image), and the Homebrew, Scoop and Nix manifests were hash-synced to it and are checked against the release's own
  digests.
- **Stage 3:** the repository is now `SchwartzKamel/hopper`, renamed last, as decided. The old address redirects (web, API, release assets and git
  were each checked), and GitHub Actions runs under the new name.
- **Not moved by the rename:** the container image path (`ghcr.io/schwartzkamel/azure-openai-cli` through v2.4.0; the next release publishes
  under `ghcr.io/schwartzkamel/hopper`) and existing attestations, which name the old repository (verify with `--owner`).
- **Outstanding:** smith's configuration still names the old slug (a file in smith's own configuration, updated by its owner), and the `az-ai` alias
  keeps its sunset review at the next major version.
