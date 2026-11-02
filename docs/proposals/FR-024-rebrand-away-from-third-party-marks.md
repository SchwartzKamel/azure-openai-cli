# FR-024 -- Rename the project away from third-party marks (staged), with the review

**Status:** Proposed. Three decision gates are open (see "Gates"). Nothing in this
document renames anything.
**Related:** [ADR-016](../adr/ADR-016-rebrand-away-from-third-party-marks.md),
[`docs/legal/trademark-policy.md`](../legal/trademark-policy.md),
[`scripts/brand-inventory.sh`](../../scripts/brand-inventory.sh)
**Date of evidence:** 2026-11-01 (collision data is point-in-time)

## How to read this document

The brief was to review the rebrand in many rounds of multi-persona conferral with
heavy devil's advocate, not in one pass. That is what is below. Two honest
caveats up front:

- The personas are **review lenses run in turn by one orchestrator**, not separate
  agents. Each lens is allowed to be wrong; the devil's advocate is allowed to be
  unfair. What matters is that every claim traces to something checked.
- **The legal lens is not counsel.** It identifies a question; it does not answer
  it. Gate G2 exists for that reason.

## Summary

The earlier verdict was **do not rename**. This review reverses it, because the
two assumptions that verdict rested on turned out to be false, and one fact it
did not know turned up in the repo's own policy:

1. It assumed external users with muscle memory. The repository has **0 stars,
   0 forks and 0 watchers**; the maintainer is effectively the only user.
2. It assumed the name was merely inconvenient. The project's own trademark
   policy says third-party marks are used "only nominatively ... only so much of
   the mark as is necessary", while the project's **name is "Azure OpenAI CLI"**,
   two third-party marks used as the product's identity.
3. The product is no longer Azure-only. It routes seven providers, speaks MCP,
   runs a work queue and an eval harness. The name describes the first feature.

**Recommendation:** rename in two reversible stages, display name and repository
first, binary second with `az-ai` kept as a working alias. Candidate name:
**`pith`**, runner-up **`usher`**. Do **not** touch environment variables or
internal C# identifiers. The name itself is the owner's decision (G1).

## Evidence

| # | Fact | How it was checked |
|---|------|--------------------|
| E1 | The project's short name is "Azure OpenAI CLI" / `az-ai`; policy section 3 limits third-party marks to nominative use (three-factor test: not otherwise identifiable, only as much as necessary, no implied endorsement) | `docs/legal/trademark-policy.md` lines 1-80 |
| E2 | The repo has 0 stars, 0 forks, 0 watchers, 1 open issue; releases run v1.9.0-alpha.1 to v2.3.0 | `gh api repos/...`, `gh release list` |
| E3 | Providers shipped: azure, openai, groq, together, cloudflare, foundry, local; plus MCP client and server, a work queue, an eval harness | README, ADR-010/011, S03 to S04 reports |
| E4 | Blast radius (tracked files, history excluded): `az-ai` 314 files / 3,072 lines; `azure-openai-cli` 140 / 504; `AzureOpenAI_CLI` 275 / 820; `azureopenai-cli` (dir) 191 / 850; `AZUREOPENAI*` env vars 174 / 1,071; `AZ_AI_*` 80 / 627 | `scripts/brand-inventory.sh` (reproducible) |
| E5 | Published surfaces: the GitHub repo and releases; the container image path is `ghcr.io/<repo>`, so it is tied to the repo name; Homebrew, Scoop and Nix manifests live in `packaging/` | `release.yml`, `packaging/` |
| E6 | No public Homebrew tap was found at `SchwartzKamel/homebrew-tap` (HTTP 404). Whether another tap exists is **unverified** | `gh api` |
| E7 | All 24 short candidate names are taken on npm and crates.io, and 23 of 24 on PyPI. "Free everywhere" cannot be satisfied | registry HTTP checks, 24 names |
| E8 | For the channels this tool ships through, many names are free: Homebrew formula and cask, Scoop main and extras, Docker library | HTTP checks, 8 finalists |

## The review

### Round 1 -- Should we rename at all?

| Lens | Position | Devil's advocate | Resolution |
|------|----------|------------------|------------|
| Larry David (showrunner) | The "no" verdict rested on users and muscle memory. Evidence E2 removes both. | FDR: "Zero stars means nobody will notice, which also means nobody is harmed by the name. A rename with no audience is vanity." | The harm argument is about the mark owners' view of the name, not audience size. The cost argument is symmetric: zero audience also means near-zero cost, and a rename only gets dearer as users arrive. Now is the cheap moment. |
| Costanza (product) | The name says Azure OpenAI; the product routes seven providers, MCP, a queue and an eval harness. The first-run wizard asks "which provider". | Kramer: "Users search for 'azure openai cli'. You are throwing away discoverability." | Keep the phrase in the README description and repo topics, and write "formerly Azure OpenAI CLI" for at least two releases. |
| Jackie Chiles (legal lens, **not counsel**) | E1: using "Azure OpenAI" as a *description* ("works with Azure OpenAI") is nominative. Using it as the product's *name* is source-identifying use, a different and riskier question. | FDR: "You do not know that it is a problem. Microsoft has not complained." | Correct, and this review does not claim it is a violation. It claims the project's own policy sits uneasily with its own name and that the fix is cheap. Whether it is actually a problem is gate G2, for counsel. |
| Sue Ellen Mischke (competitive) | `docs/competitive-analysis.md` lists provider-neutral peers, for example opencode ("BYOM router CLI; 75+ providers"). A neutral name is the norm in that set. | Peterman: "A neutral name is also forgettable." | Accepted as a risk for the naming round, not a reason to keep the current name. |
| Newman (security) | The `az-` prefix mimics the Azure CLI's `az` command. A user cannot tell `az-ai` from an Azure CLI extension from the name alone. | Kramer: "Nobody has been confused." | There is no evidence either way with 0 users, so this is recorded as a smell, not a finding. |
| Mr. Pitt (scope) | E4 is mechanical. The expensive part is not the count, it is the six surfaces in E5. | Kramer: "The env vars are the real public API. A rename that leaves `AZUREOPENAIENDPOINT` is half a rename; one that changes it breaks every script." | Do not rename the env vars. They name the Azure service they configure, which is exactly the nominative use the policy permits. Neutral aliases can be added later, additively. |
| FDR (devil's advocate) | "You will pick a name, find it is a problem, and rename twice." | | Phase 0 is reversible and gated (G2). No irreversible step runs before a clearance check. |

**Verdict: rename.** This reverses the earlier "do not rename". It is reversed
because the premises changed and a fact was found, not because it was asked for
twice.

### Round 2 -- What scope?

| Option | What changes | Verdict |
|--------|--------------|---------|
| A. Keep the name, add a disclaimer | Nothing | Rejected. The repo already has the disclaimer. The issue is the name, not the absence of a disclaimer. |
| B. Display name and repository only | README title, help banner, docs, trademark policy short name, GitHub repo name. Binary stays `az-ai` | **Stage 1.** Reversible: rename back, and GitHub documents redirects for git and web from the old name. |
| C. B plus the binary | New binary name; `az-ai` kept as a working alias | **Stage 2.** After B has settled. |
| D. New repository | Fresh repo, archive the old one | Rejected. Loses issue and PR history and the redirect, for no benefit B does not give. |

Challenges, and how each was answered:

- **Bob Sacamano (packaging):** the in-repo Homebrew formula is `az-ai.rb` plus
  versioned `az-ai@2.0.x.rb` files. A rename means a new formula and keeping the
  old ones. E6 says no public tap was found, so the install base to migrate is
  small, but this is unverified.
- **Jerry (CI):** the image path is `ghcr.io/<repo>`, taken from `github.repository`.
  After a repo rename, new pushes would use the new name and **old tags would stay
  under the old package name**. GitHub documents redirects for git and web URLs;
  none was found or tested here for the container registry, so assume there is
  none. The release workflow must be checked before the rename, not after.
- **Mr. Lippman (release):** a binary rename with the old name kept as an alias is
  not a breaking change, so a minor bump. Without the alias it is a major bump.
- **Newman:** after a rename the old name is an unowned identity that someone could
  take on a channel we do not control. Keep publishing the alias for a stated
  period, and register the old name only where we already publish.
- **FDR:** "An alias that lives forever means you never renamed." Fair. The alias
  gets a sunset review at the next major version, and success is measured by
  `scripts/brand-inventory.sh`, not by feeling.

**Verdict: B, then C.** And separate user-visible from internal: the C# namespace
(`AzureOpenAI_CLI`, 275 files) and the source directory (`azureopenai-cli`, 191
files) are not user-facing. Renaming them is risk with no user benefit. They are
out of scope unless a later decision says otherwise.

### Round 3 -- What makes a good name here?

Criteria, in the order they were applied:

1. **No third-party marks** (Azure, OpenAI, GPT, Microsoft, Copilot, and the other
   model vendors).
2. **Typeable:** at most six characters, lowercase, no shell-special characters.
3. **Free on the channels we ship through:** Homebrew formula and cask, Scoop main
   and extras, the Docker library namespace. The GHCR path follows the repo.
4. **Does not shadow a command people already have** (Debian package or a binary
   on PATH).
5. **Small exact-name collision on GitHub** (under about 200 stars), so a search
   still finds us.
6. **Fits the product:** a pipe-friendly, multi-provider prompt tool whose default
   system prompt asks for "factual, no fluff".

**Deliberately not a criterion:** npm, PyPI and crates.io. This tool is not
published there (E5) and E7 shows the criterion cannot be met by any short word.

First pass, 24 candidate words against the registries (T = taken, f = free):

| name | npm | PyPI | crates | brew | cask | PATH/apt | exact-name issue |
|------|-----|------|--------|------|------|----------|------------------|
| ask | T | T | T | f | f | no | too generic to measure |
| hey, muse, aide, sift | T | T | T | T or f | f | **apt package** | shadows a real command |
| quill, wren, tern | T | T | T | **T** | f | no | Homebrew name taken |
| relay, kite, lumen, ember | T | T | T | f | f or T | no | large exact-name-adjacent projects (7.5k to 22k stars: meta/relay, laravel/lumen, emberjs/ember.js, a 12k Docker project for kite) |
| ferry, tally, plume | T | T | T | f | f | no | 6.6k, 1.2k, 2.2k stars exact-name |
| aiq | T | f | T | f | f | no | cryptic; an exact-name repo at 250 stars (taylorai/aiq) |
| **pith, usher, herald** | T | T | T | **f** | **f** | no | 97, 141, 80 stars exact-name |

Exact-name GitHub results for the three that survived (top three by stars):
`pith` abhisekjha/pith 97, mdub/pith 38, pithtv/pith 36; `usher` yatisht/usher 141,
joshbuddy/usher 122, whitfin/usher 35; `herald` vbauer/herald 80, kOlapsis/herald
46, helpshift/herald 43. All three are 404 on Scoop main, Scoop extras and Docker
library. (A GitHub *user* of each name exists; that is irrelevant because the repo
owner does not change.)

One caution on the data: the first-pass GitHub column was a substring search
("pith" surfaced "pithos"), which overstates collisions, so the finalists were
re-checked by exact name. A "none" for `ask` in the exact-name pass is
**inconclusive**, not clean: the search returned only the top 30 substring hits.

### Round 4 -- Attack the finalists

| Name | For | Against (devil's advocate) | Verdict |
|------|-----|----------------------------|---------|
| **pith** | Four letters. "The essential part" matches "factual, no fluff". Smallest exact-name collisions of the three. Peterman: "get to the pith" is a usable line. | Autocorrect turns it into "pitch". A botanical word, so search results are noisy. FDR: "It is a slightly unlovely word." | Recommended. The typo risk is real but it fails safe (command not found), and the owner may simply dislike it. |
| usher | Routing and queue metaphor fits `--prefer` and `az-ai queue`. | UShER is a widely used bioinformatics tool (and a Ruby gem). Searches for the name will not find us. Five characters. | Runner-up. Fit is better than `pith`, discoverability is worse. |
| herald | Six letters, distinctive. | Means "announce", which does not describe routing, queues or scoring. A common newspaper name. | Not recommended. |

Naming is taste as well as evidence. The data excludes bad choices; it does not
choose among good ones. That is gate G1.

### Round 5 -- Stress the migration

| Surface | What could break | Mitigation |
|---------|------------------|------------|
| Espanso, AHK and WSL scripts that call `az-ai` | Every existing trigger | Stage 2 keeps `az-ai` as a working alias. Example configs keep working untouched. |
| Container image path | New tags move to the new repo path; old tags stay | Document both. Verify the release workflow before the repo rename, not after. |
| Homebrew, Scoop, Nix manifests | Formula and bucket names | New names added; old versioned formulas left in place. |
| Shell completions | Only one command name is completed | Emit completions for both names. |
| Documentation links | Deep links to the old repo name | GitHub documents redirects for git and web URLs (not tested here). Verify by following three real links after the rename. |
| Environment variables | Every script | **Unchanged.** Aliases only, later, additively. |
| History | Episode logs mention the old name | `docs/exec-reports`, `docs/diary`, `docs/img` and `.wgm` are append-only history and are not rewritten. |
| Telemetry or scripts that parse the banner | A changed banner string | Search the tests and examples for the banner text before changing it. |

### Round 6 -- What would change this recommendation

- A real user base appears. Then the cost side of Round 1 changes and the plan
  needs a longer alias period.
- Counsel says the current name is fine. Then Stage 1 is optional and the case
  rests on the product no longer being Azure-only, a weaker reason.
- Counsel flags the chosen name. Then back to Round 3, before any irreversible
  step, which is why G2 comes before Stage 1.

## Recommendation and plan

| Phase | Scope | Reversible? | Done when |
|-------|-------|-------------|-----------|
| **0** (this change) | FR, ADR, `scripts/brand-inventory.sh` baseline | Yes | Baseline recorded. |
| **1** display and repo | README title and description, help banner, docs titles, trademark policy short name; rename the GitHub repo; verify release workflow and image path first | Yes (rename back; GitHub documents redirects for git and web, not tested here) | Help banner test updated; `brand-inventory` "repo / package" is history only; three old deep links still resolve. |
| **2** binary | New `AssemblyName`; ship `az-ai` as an alias in tarballs, formulas and Scoop; completions for both | Yes while the alias exists | `az-ai --version` equals the new binary's; integration suite runs under both names. |
| **3** (optional) | Neutral env var aliases, additive | Yes | Old and new names resolve identically; old ones documented as supported. |
| **4** | Alias sunset review | n/a | At the next major version. |

Not planned: renaming the C# namespace, the source directory, or the existing
`AZUREOPENAI*` / `AZ_AI_*` variables.

## Gates

- **G1 -- the name.** The owner chooses. Recommendation `pith`, runner-up `usher`.
- **G2 -- clearance.** Counsel (or at minimum a trademark and common-law search)
  on the chosen name and on whether the current name needs changing. **Not done
  here:** no trademark database, domain or social-handle check was performed.
- **G3 -- go.** Approval to start Phase 1.

Until all three are cleared, no file outside the documentation and the inventory
script changes.

## Reproducing the evidence

```bash
scripts/brand-inventory.sh            # E4, per name form
gh api repos/SchwartzKamel/azure-openai-cli --jq '{stars:.stargazers_count,forks:.forks_count}'   # E2
curl -s -o /dev/null -w '%{http_code}' https://formulae.brew.sh/api/formula/pith.json   # 404 = free
gh api 'search/repositories?q=pith+in:name&sort=stars&per_page=30' \
  --jq '[.items[] | select(.name|ascii_downcase=="pith")] | .[0:3] | map(.full_name+":"+(.stargazers_count|tostring))'
```
