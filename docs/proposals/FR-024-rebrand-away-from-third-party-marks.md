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
first, binary second with `az-ai` kept as a working alias. Name: **`anymux`**
(runner-ups `plainly` and `askany`), chosen in the second
[Addendum](#addendum-2-those-names-do-not-make-sense-rounds-13-to-15). Two earlier
picks were withdrawn: `pith` and `usher` on trademark evidence, `dunlin` and `curlew`
because the owner found them meaningless. Do **not** touch environment variables or
internal C# identifiers.

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

### Round 4 -- Attack the finalists (superseded: see the Addendum)

> **Superseded.** These three finalists were later eliminated by a US trademark
> screen. `pith` has a live application by Pith Games LLC in classes 9 and 42, `usher`
> has two live software-class marks, and `herald` has a live class 9 mark. The round
> is kept as the record of what registries and GitHub alone could and could not show.

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

- **G1 -- the name.** The owner confirms. Recommendation `anymux`, runner-ups `plainly` and `askany` (see the second Addendum).
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

## Addendum: finding the name (rounds 7 to 12)

*Added 2026-11-02. The first review's shortlist used registries and GitHub only. This
addendum records why that was not enough, and how a name was found once it was
replaced by a proper screen.*

### Round 7 -- Why the first shortlist failed

The USPTO's public trademark search backend answers programmatic queries (the same
data as `tmsearch.uspto.gov`). Run against the three finalists it eliminated all of
them:

| Name | Live US mark found | Classes |
|------|--------------------|---------|
| `pith` | PITH, Pith Games LLC, application filed 2026-10-19 (non-downloadable and downloadable game software) | 9, 42 |
| `usher` | USHER, Usher Incorporated (registered and renewed), plus a second USHER mark | 9, 42 |
| `herald` | HERALD, Seabourn Cruise Line Limited | 9, 16 |

Classes 9 and 42 are where a CLI sits. The earlier recommendation of `pith` is
withdrawn, and the evidence table's gap is the lesson: **a package registry and a
GitHub search are not a trademark screen.**

### Round 8 -- Generate, widely and in rounds

Six screening rounds, **719 unique names screened**. Each lens proposed families,
and each round fed the next:

1. Themed words plus 330 seeded random pronounceable coinages (435 names).
2. Curated by lens: flowing water, nautical guidance, messengers, concision, word
   roots, old tools, obscure birds, short nature words (83).
3. Constructive coinages from product-meaning roots with vowel endings, plus short
   words for "word" and "ask" from other languages (142).
4. Hand-designed names (27).
5. A taste-curated finalist set, re-run so every name has identical data (28).
6. Short names from waders, textiles, stone and weather (40).

### Round 9 -- The screen, and how it was wrong first

Hard filters, cheapest first, so rate-limited APIs only see survivors: a local
command or Debian package; a third-party mark or provider name; Homebrew formula or
cask, Scoop main or extras, Docker library; and a **live US mark with software goods
in classes 9 or 42**. Survivors then got a near-miss check (live software marks one
edit away), GitHub exact-name repositories, command typo-neighbours and domains.

| Round | Names | Out (local) | Out (channel) | Out (USPTO) | Passed |
|-------|-------|-------------|---------------|-------------|--------|
| 1 (after re-screen; 2 lookups never resolved) | 435 | 15 | 27 | 75 | 316 |
| 2 | 83 | 2 | 6 | 40 | 35 |
| 3 | 142 | 1 | 3 | 27 | 111 |
| 4 | 27 | 3 | 1 | 9 | 14 |
| 5 | 28 | 0 | 0 | 0 | 28 |
| 6 | 40 | 2 | 3 | 16 | 19 |

Four things went wrong in the method, and each changed the result:

- **187 lookups errored and I first treated them as eliminations.** Eight parallel
  threads against the USPTO endpoint were rejected. Those names were not eliminated
  by any finding. They were re-screened serially with retries before any conclusion
  was drawn; two failed persistently and were left unresolved.
- **The first GitHub stage was rate-limited.** An unauthenticated client allows
  about ten searches a minute. It was redone with the authenticated `gh` client.
- **Wikipedia presence is not a filter.** 163 of 222 short candidates had a page
  (names, places, acronyms). It was kept as information only.
- **Counting near-misses hid severity.** `pipit` looked promising (a real bird, five
  letters, sounds like "pipe it") with one unrelated clothing mark. Reading its 11
  near-miss marks showed **PIPPIT**, a live software mark one letter away. The register
  lists its owner as Lemon Inc. I believe the product is an AI video tool, which is
  background knowledge and **unverified**; the live mark itself, in software classes
  and one letter from the candidate, is enough to rule the name out.

The deeper finding about the method: **automation clears names but cannot give them
taste.** Mechanical coinages (`sluiex`, `quillle`, `kofpu`) pass every screen and are
unusable. Obvious dictionary words mostly fail on live marks. The names worth
considering were obscure real words chosen by hand and then screened.

### Round 10 -- Five finalists on identical data

| Name | Letters | Live exact US marks | Near-miss software marks (class and goods) | Exact-name GitHub repos | Notes |
|------|---------|---------------------|---------------------------------------------|-------------------------|-------|
| **dunlin** | 6 | 2, unrelated goods (plastic bottle holder, dental supplies) | 0 (0 more at edit distance 2) | 19, 5, 4 stars; crypto notes, clinical-trial data, a panorama editor | A small wader that flies in tight flocks |
| **curlew** | 6 | 1, clothing | 0 | 64, 52, 6; a media converter, geological models, a job pool | Starts with `curl` |
| **uncial** | 6 | 0 | 0 | 6, 4, 2 | A rounded manuscript script; pronunciation ambiguous |
| **ouzel** | 5 | 0 | 0 | 981 (a C++ game engine) | Same software class as us, by use rather than registration |
| **adage** | 5 | several, agrochemicals, household and cleaning goods | 0 | 61, 55; DAG-workflow tools, a Node fortune package | A well-known word and a well-known publication |

None of the five has an AI or LLM project of the same name: searches for each name
beside llm, openai and chatgpt returned nothing.

### Round 11 -- The taste round, with devil's advocate

| Lens | On the finalists |
|------|------------------|
| Costanza (product) | Only `adage` says what the tool does (short, no fluff). The rest are arbitrary, which is normal for CLI tools. Fit is not worth a collision. |
| Sue Ellen (competitive) | Peers use arbitrary short names, so `dunlin`, `uncial` and `ouzel` fit the set. A common word such as `adage` gets lost in search. |
| Peterman (copy) | `dunlin`: "a flock that turns as one", which is multi-provider routing. Small, fast, crosses oceans. `curlew`: haunting, evocative, but the story is about a lone bird. |
| Mickey (typing, speech) | `dunlin` is spoken and typed the way it reads. `curlew` is spelled unlike it sounds ("KER-loo"). `uncial` and `ouzel` each have two plausible pronunciations. |
| Babu (meaning across languages) | No offensive meaning found in the languages I can reason about for `dunlin`; English "dun" (to press for payment) is the only echo. This is a reasoned check, not a native-speaker review. |
| Frank (support) | `dunlin` searches cleanly. `curlew` invites "is this curl?" questions and a tab-completion neighbour of `curl`. `adage` collides in search with a major advertising publication, which I believe is Ad Age; unverified. |
| Bob (packaging) | `dunlin` is free as a Homebrew formula and cask, Scoop and the Docker library; the image path would be `ghcr.io/<owner>/dunlin`. |
| Jackie and Newman (legal lens, **not counsel**) | `dunlin` has two live marks in unrelated goods and nothing in software within two edits. Open question: DUNLOP, which I know as a well-known tyre brand (background knowledge, not checked here), is two edits away and sounds alike in the first syllable. Whether that matters is for counsel. |
| FDR (devil's advocate) | "It is a boring bird, it sounds like 'dull', 'dun' means debt collector, and you will spend a year explaining what a dunlin is." |

The devil's advocate was answered, not dismissed. *Dull and debt collector:* real,
but both are English echoes of a name that is otherwise unambiguous to say and type,
and arbitrary names are the norm (Python and Ruby are not descriptive either).
*Explaining it:* the flock story does that work in one sentence, and an unusual word
is findable where a common one is not. *Boring bird:* accepted as a matter of taste;
it is exactly the kind of objection that gate G1 exists to let the owner overrule.

### Round 12 -- Decision

**`dunlin`**, with **`curlew`** as runner-up. Ranking of the five: `dunlin`,
`curlew`, `uncial`, `adage`, `ouzel`. `uncial` is clean but has no connection to the
product and an ambiguous pronunciation. `adage` is the best fit by meaning and the
worst by search and mind-share. `ouzel` has no registered marks but is a live
software product by use, in the same class of goods.

### What is and is not established

This is a **preliminary screen, not clearance, and not legal advice.** It covers US
federal registrations and applications as of 2026-11-02. It does **not** cover:
state or common-law marks and unregistered use; the EU, UK or WIPO registers (the
EUIPO site was unreachable and the WIPO database needs a browser); translations and
sound-alikes beyond two edits; or a human review of goods and services. The
"software goods" test is a keyword heuristic and can miss or over-flag.

Specific questions for counsel on `dunlin`: whether DUNLOP (tyres) is a concern
given the shared first syllable; whether the two live DUNLIN marks (plastic bottle
holders, dental supplies) matter; and a full common-law and international search.

### Reproducing this

`scripts/name-screen.py <name> [<name> ...]` runs the whole screen for any name and
prints what it found, including every live mark it relied on. `--json` gives the
raw record. It is read-only and states its own limits in its output.

## Addendum 2: "those names do not make sense" (rounds 13 to 15)

*Added 2026-11-02. The first addendum picked `dunlin` (runner-up `curlew`). The owner's
response was that those names do not make sense. This records why that objection is
right and what replaced them.*

### Round 13 -- The objection

`dunlin` and `curlew` are arbitrary birds. I had ranked "clears the screen" above
"means something" and treated taste as a tiebreaker, which is how two names with a clean
trademark profile and no connection to the product ended up recommended. The owner's
point is correct, and it changes the ranking rule: **a name has to pass a one-sentence
sense test first** ("it is called X because it does Y"), and only then is it screened.
`dunlin` and `curlew` are withdrawn on that ground, not on any evidence.

### Round 14 -- Meaning-first generation

109 names were generated by what the tool actually does, each with its one-line sense,
and screened with `scripts/name-screen.py`:

- routing one prompt to many providers (conduit, manifold, patchbay, crossbar, junction,
  gateway, courier, dispatch, router, mux, prism, nexus);
- concise, plain answers (tersely, plainly, succinct, brisk, crisp, nutshell, distill);
- conversation (parley, palaver, confab, colloquy, converse);
- asking and carrying messages (askany, askall, tellany, courier, envoy, pigeon);
- piping (askpipe, pipeask, llmpipe, pipellm, promptpipe);
- "multiplexer" blends (llmux, anymux, askmux, promptmux, modelmux, chatmux);
- adverb commands that carry a voice (frankly, bluntly, candidly, briefly, kindly).

The pattern is stark: **almost every obvious word is already a live mark in the software
classes.** Out on live marks: `gateway` (Acer), `courier`, `dispatch`, `crossbar`,
`intercom`, `mux` (Mux Inc.), `oracle`, `switchboard`, `junction`, `nutshell`, `distill`,
and every adverb voice but one (`frankly`, `bluntly`, `candidly`, `briefly`, `kindly`,
`simply`). Out on channels or commands: `conduit`, `manifold`, `nexus`, `envoy`,
`babel` (Homebrew), `parley`, `polyglot`, `psst`, `seer` (Debian packages), `bridge` (a
command on PATH).

A process note: the first run of this batch failed on most lookups because three
parallel USPTO queries ran alongside a full build and test gate. Errored lookups were
**not** counted as eliminations; the names were re-run one at a time.

### Round 15 -- Crowding: what the existing projects are

The trademark screen cannot see a same-named tool doing the same job, so each survivor's
exact-name GitHub, PyPI and npm projects were read:

| Name | What already exists | Verdict |
|------|---------------------|---------|
| `llmsh` | A PyPI package that is a command-line tool for using LLMs in your shell, including OpenAI and Anthropic. The same job. | Out |
| `pipellm` | An npm CLI for composing LLM pipelines. | Out |
| `llmux` | Projects described as an "LLM multiplexer for vLLM" and a tool to manage vLLM and llama.cpp servers. A neighbouring idea under the same word. | Crowded |
| `llmcat`, `llmpipe` | File-preparation and pipeline libraries. Different jobs, same neighbourhood. | Crowded |
| `askany` | Three tiny projects (6, 2 and 1 stars), two of them document question-and-answer tools. | Usable |
| `plainly` | A Ghost theme and an unrelated shopping tool. Nothing in AI or CLI. | Usable |
| `anymux` | **Nothing**: no GitHub repository, no PyPI package, no npm package. | Clean |

### The shortlist, on the sense test

| Name | "It is called X because..." | Live US marks | Typing and saying |
|------|------------------------------|---------------|-------------------|
| **anymux** | it multiplexes **any** provider behind one command, the way `tmux` multiplexes terminals | exact 0, whole-word 0, 1 near-miss (not software) | six letters, "any-mux", no ambiguous spelling |
| **plainly** | it answers **plainly**: the default system prompt is "factual, no fluff" | exact 0, 3 whole-word, 2 near-miss (none software) | seven letters; `plainly "why is the sky blue"` reads as a sentence |
| **askany** | you **ask any** model | exact 0, near-miss 4 (none software) | six letters, spelled as said |

Domains and channels: `anymux` and `askany` have `.dev` free; `plainly` does not. All three
are free on Homebrew, Scoop and the Docker library, are not commands on PATH, and have no
command typo-neighbours.

### The taste round, with devil's advocate

| Lens | On `anymux`, `plainly`, `askany` |
|------|----------------------------------|
| Costanza (product) | `anymux` is the only one that names the product's actual differentiator, many providers behind one command. `plainly` names a style, which half the tools could claim. |
| FDR (devil's advocate) | "`mux` is jargon. Anyone who has not used `tmux` will think it is a router appliance, and 'any' promises more than you deliver: you do not call every provider at once." |
| Peterman (copy) | `plainly` is the better voice and the better sentence. `anymux` is the better label. A tagline can carry the voice: *anymux: plain answers from any model.* |
| Mickey (typing, speech) | Both are spoken and typed as spelled, with no ambiguous pronunciation. `anymux` is a letter shorter and has no word ending to drop when typing fast. |
| Babu (meaning across languages) | `plainly` leans on an English adverb and loses its sense when translated; `anymux` is mostly Latin-script jargon and travels better. No offensive reading found for either, as a reasoned check, not a native-speaker review. |
| Frank (support) | `anymux` has no search results to compete with, so every result is ours. |

FDR's objection was answered, not dismissed. *Jargon:* the audience is people who already
use a command-line tool with provider flags, and `tmux`, `mux` and `muxer` are standard
vocabulary there. *"Any" over-promises:* it means any provider the tool supports, and the tagline
keeps the promise honest.
Neither objection survives a reader who has used a terminal multiplexer, which is the
reader this tool has.

### Decision

**`anymux`**, with `plainly` as the runner-up if the owner prefers a voice to a label, and
`askany` third. This is a preliminary US-federal screen plus a crowding check, not
clearance; the limits stated in the first addendum still apply.

Reproduce any of it with `scripts/name-screen.py <name>`.

## Addendum 3: open source only, and the competitor landscape (rounds 16 to 19)

### Round 16 -- The owner drops the trademark stage

The owner's instruction: this is an open-source command-line tool with no legal entity, so
do not worry about trademarks. That removes the US-register stage from the ranking and,
with it, the only reason `pith`, `usher`, `herald`, `courier`, `dispatch`, `gateway` and
`mux` were ever rejected. They were re-screened on open-source grounds in this round.

What stays is what an open-source user actually hits: a name that is already a command on
their machine, a Homebrew or Scoop package, or a well-known project in the same space, so
that `brew install <name>` or a web search lands on somebody else's tool. Counsel questions
(`jackie-2026-11-J-1`, `-J-2`) are unchanged for the day the project has an entity.

### Round 17 -- What competitors are called

A 314-repository keyword sweep of GitHub was too noisy to name peers from (coding agents
dominate it), so the table below was pulled by exact repository, as of 2026-11-02.

| Repository | Stars | Language | Created | Kind |
|---|---|---|---|---|
| `opencode` | 223,936 | TypeScript | 2025 | coding agent |
| `ollama` | 185,639 | Go | 2023 | local model runtime |
| `claude-code` | 154,212 | TypeScript | 2025 | coding agent |
| `codex` | 132,800 | Rust | 2025 | coding agent |
| `gemini-cli` | 108,661 | TypeScript | 2025 | coding agent |
| `openinterpreter` | 68,775 | Rust | 2023 | coding agent |
| `goose` | 56,111 | Rust | 2024 | general agent |
| `aider` | 49,891 | Python | 2023 | pair-programming agent |
| `Fabric` | 44,526 | Go | 2024 | prompt patterns framework |
| `crush` | 28,909 | Go | 2025 | coding agent |
| `plandex` | 15,885 | Go | 2023 | coding agent |
| `llm` | 12,689 | Python | 2023 | prompt CLI (closest peer) |
| `shell_gpt` | 12,299 | Python | 2023 | prompt CLI (closest peer) |
| `aichat` | 10,534 | Rust | 2023 | prompt CLI (closest peer) |
| `aicommits` | 9,121 | TypeScript | 2023 | commit-message writer |
| `code2prompt` | 7,745 | Rust | 2024 | codebase to prompt |
| `ai-shell` | 5,306 | TypeScript | 2023 | natural language to shell |
| `mods` | 4,521 | Go | 2023 | prompt CLI (closest peer), archived |
| `gptme` | 4,466 | Python | 2023 | terminal agent |
| `tgpt` | 3,287 | Go | 2023 | terminal chat |
| `chatblade` | 2,583 | Python | 2023 | prompt CLI, archived |
| `elia` | 2,481 | Python | 2023 | terminal chat UI |
| `gorilla-cli` | 1,374 | Python | 2023 | natural language to shell |
| `ai-cli` | 1,165 | TypeScript | 2022 | natural language to shell |
| `chatgpt-cli` | 965 | Go | 2023 | multi-provider prompt CLI (closest peer) |
| `tmuxai` | 1 | Go | 2026 | terminal assistant (new, 1 star) |

What the names do:

| Strategy | Examples | Reading |
|---|---|---|
| A literal "ai / gpt / chat / shell / cli" in the name | `shell_gpt`, `aichat`, `aicommits`, `ai-shell`, `tgpt`, `chatblade`, `ai-cli`, `chatgpt-cli`, `gptme`, `tmuxai` | Descriptive, weak as a brand. 12 of the 14 tools in the 1k to 13k star tier are like this. |
| A short brandable word or agent-noun | `aider`, `crush`, `goose`, `plandex`, `fabric`, `mods`, `elia`, `gorilla` | Of the ten tools above 28k stars, none has a literal "ai", "gpt", "llm" or "chat" in its name; they are words or vendor names. |
| A vendor product name | `codex`, `gemini-cli`, `claude-code`, `opencode`, `ollama` | The brand carries the name, not the function. |
| A generic noun that was first | `llm` | Works once, for whoever got there first. |

The median name is 6 characters. The correlation between brandable names and star count is
real but confounded: the top tier is mostly vendor-backed products. It is a reason not to
choose another `ai-something`, not proof that a word name wins.

What nobody has claimed, and this tool actually has: **speed** (a 10.7 ms p50 cold start
per the README, on its reference rig), **Unix-pipe use** (every peer's examples are interactive), **provider-agnosticism
as a signal** (only `chatgpt-cli` says "multi-provider"), and **text injection** (the
answer typed at your cursor by a text expander). A name can point at one of these.

One more criterion, new here: **does it read as a command?** `git diff | NAME "review"`
is the line a user types hundreds of times. `llm`, `aichat` and `mods` read as commands.

### Round 18 -- Generate by angle, then screen on open-source grounds

`scripts/name-screen.py --oss <name>` is the new mode. It skips the trademark stage and
checks, in order: a command on PATH, a shell builtin, a Debian package, a taken Homebrew,
Scoop or Docker name (all of these rule a name out); then GitHub repositories of the exact
name and whether they sit in the AI space, PyPI, npm and crates.io packages of that name
and a one-edit neighbour check (these are flags, not verdicts).

186 names were generated from six angles (speed, pipe, any-provider, text injection,
asking and summoning, helper spirits and voice) plus the team-of-personas and fallback-chain
features, and the earlier mark-only rejections.

| Stage | Names |
|---|---|
| Screened | 186 |
| Out: command, Debian package or shell builtin | 25 |
| Out: taken on Homebrew, a cask, Scoop or Docker | 35 |
| Survived distribution | 126 |
| Survived and has no AI-space collision on GitHub or any registry | 62 |

Two defects in the screen were found and fixed during the round, and they matter more than
any single name:

- **Shell builtins.** The local check looked for executables, so `type` passed. `type` is
  a builtin in bash, zsh and fish. The check now asks the shell.
- **A failed GitHub lookup was reported as "no repositories".** `tempo` showed no flags
  although `grafana/tempo` has 5,552 stars. A failed lookup is now an error, and the 13
  names that had shown zero repositories were re-checked: 7 are genuinely zero (`any`,
  `anymux`, `imp`, `quest`, `seek`, `speak`, `tell`), 5 had repositories after all
  (`hopper`, `tempo`, `troupe`, `usher`, `utter`) and 1 was a builtin (`type`).

A limit of "zero": GitHub search returns the top 100 substring matches by stars, so zero
means no exact-name repository notable enough to rank, not no repository at all.

### Round 19 -- Finalists on identical data

| Name | Letters | Why it is called that | Top same-named repo (stars) | AI-space collision | How it reads |
|---|---|---|---|---|---|
| `hopper` | 6 | hops between providers; stdin goes into the hopper | 274 (fuzzing tool) | none | `git diff \| hopper "review"` |
| `anymux` | 6 | any provider, multiplexed | none notable | none | `git diff \| anymux "review"` |
| `tell` | 4 | you tell the model | none notable | none | `git diff \| tell "review"` |
| `seek` | 4 | you seek an answer | none notable | none | `git diff \| seek "review"` |
| `jiffy` | 5 | in a jiffy (speed) | 880 (Erlang JSON) | none | `git diff \| jiffy "review"` |
| `trice` | 5 | in a trice (speed) | 1,012 (C tracing library) | none | `git diff \| trice "review"` |
| `typist` | 6 | types the answer where your cursor is | 1,056 (Swift keyboard) | none | `typist --raw "fix:" < note` |
| `usher` | 5 | ushers a prompt along | 141 (bioinformatics) | none | `git diff \| usher "review"` |
| `plainly` | 7 | plain answers | 30 (a theme) | 1 repo, 1 star | `git diff \| plainly "review"` |
| `summon` | 6 | summon an answer | **762, `cyberark/summon`, a CLI** | none | out: same-named CLI |

Devil's advocate, by name:

- **`hopper`:** a hopper is also a bin you pour things into, which is the right image for
  stdin and has nothing to do with models. *Answer:* that is the second meaning, and
  it fits. The first meaning is in the code: the project's own telemetry already calls a
  provider switch a `fallback_hop` (`TelemetryEmitter.EmitFallbackHop`). Collisions are a
  274-star fuzzing tool and a 132-star one, neither in the AI space. A macOS disassembler
  is also called Hopper (background knowledge, unverified here).
- **`anymux`:** jargon, and "any" over-promises. *Answer:* as in round 15, the reader is
  a terminal user, and the tagline keeps the promise honest. It is the descriptive choice
  and has no collisions at all.
- **`tell` and `seek`:** no collisions, and they read best as commands, but they are
  ordinary words, so a web search for them finds nothing, and the next project to take
  the name takes the search results with it.
- **`jiffy` and `trice`:** speed only, and speed is a feature a peer can match. `trice` is
  also an "ultra-fast" tracing library.
- **`typist`:** one use case of several.
- **`summon`:** `cyberark/summon` is a command-line tool of the same name. Out.

### Decision

**`hopper`** is the recommendation, with **`anymux`** as the descriptive runner-up for an
owner who prefers a label to a metaphor, and `tell` as a minimalist third. `hopper` has the
shape of the top-tier names (a friendly word, with `-er` like `aider`), passes the one-sentence
sense test twice, and is the only candidate whose meaning is already in the codebase.

This is the owner's decision (gate G1) and nothing is renamed. Caveat as before: it is a
screen of open-source collisions, not a legal clearance.

Reproduce with `scripts/name-screen.py --oss <name>`.

## Status: stage 1 implemented (S04E14)

The owner confirmed `hopper` (gate G1) and waived the legal gate (G2) for this open-source
project. Stage 1 is built: the command is `hopper`, `az-ai` stays as an alias, and the
config directory and environment variables are unchanged. See ADR-016 for the stages as
built, and S04E14 for what was verified and what was not.

What remains, in order:

1. **Stage 2, with a release:** move the archive names to `hopper-<ver>-<rid>`, the Homebrew
   formula (`bin.install "hopper"` plus an `az-ai` symlink), the Scoop manifest (`bin` entries
   for both names, and the `autoupdate` URL), the Nix flake (`pname`, `mainProgram`,
   `tarballPrefixFor`), the `release.yml` globs and release body, and `packaging/README.md`.
2. **The docs migration** (`elaine-2026-11-E-1`).
3. **Stage 3, a separate decision:** the GitHub repository name.
