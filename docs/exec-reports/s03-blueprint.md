# Season 3 -- Blueprint *(unaired; awaiting showrunner greenlight)*

> *Pre-season treatment. No episodes filmed yet -- when the
> showrunner (the human maintainer) picks a theme, the first
> episode gets a number and this file gets archived under that
> season's header in the main README.*

S02 wrapped with the pilot (*The Wizard*) and a tight cleanup episode
(*The Cleanup*). The v2 era is production-stable: setup UX is
friendly, credentials live off the land, CI is green, preflight is
enforced, and the docs lint suite is fully armed.

S03 needs a theme. Three credible candidates, each tied to existing
feature requests in `docs/proposals/`. They are mutually exclusive as
*season themes* but any of them can be picked up feature-by-feature
in later seasons.

## Candidate themes

### A -- *Protocols & Plugins*

**Pitch.** Turn `az-ai` from a single-binary tool into a platform:
talk MCP (Model Context Protocol) both as client and server, and
expose a plugin registry so third-party tool authors can ship
`.dll` or script-based tools without recompiling.

**Tied FRs:** FR-013 (MCP client/server), FR-012 (plugin tool
registry).

**Pros.** Enormous reach. MCP is the Copilot / Claude / OpenAI
interoperability standard; shipping first-class support makes
`az-ai` a citizen of the broader agent ecosystem.

**Cons.** Architectural lift. Requires a formal extension contract,
versioning, and trust boundary design (plugins can see prompts and
tool call payloads). Newman + Rabbi signoff mandatory.

**Likely episode arc.**

1. *The Protocol* -- MCP client: consume external MCP servers.
2. *The Reciprocation* -- MCP server: expose our built-in tools.
3. *The Registry* -- plugin discovery, signing, sandboxing.
4. *The Stack* -- user-facing configuration story + docs.

### B -- *Local & Multi-Provider*

**Pitch.** Break the Azure-only assumption. Add first-class local
inference (llama.cpp, gemma.cpp) and at least one commercial
non-Azure provider (NVIDIA NIM). Keep the zero-dep / AOT ethos.

**Tied FRs:** FR-018 (llama.cpp), FR-019 (gemma.cpp), FR-020
(NVIDIA NIM + per-trigger routing), FR-014 (local preferences &
multi-provider).

**Pros.** Huge user unlock -- offline use, privacy, cost control.
Aligns with the LOLBin ethos from S02.

**Cons.** Provider abstraction work is invasive. Streaming,
tool-calling, and token accounting differ across backends. Need
a crisp `IModelProvider` interface that doesn't paper over
capabilities.

**Likely episode arc.**

1. *The Provider* -- `IModelProvider` abstraction + Azure
   passthrough.
2. *The Local* -- llama.cpp adapter.
3. *The Gemma* -- gemma.cpp adapter.
4. *The NIM* -- NVIDIA NIM + per-trigger routing.
5. *The Switch* -- preferences, auto-fallback, docs.

### C -- *Model Intelligence*

**Pitch.** Make `az-ai` smarter about model selection and token
spend without asking the user. Model aliases (`gpt-4o` -> whichever
deployment name the user chose), prompt / response cache (keyed by
stable prompt hash + model), pattern library with cost estimation.

**Tied FRs:** FR-010 (model aliases & smart defaults), FR-008
(prompt/response cache), FR-015 (pattern library & cost estimator).

**Pros.** Each feature is self-contained and low-risk. Clear UX
wins. Lower-velocity season in architectural terms, higher
velocity in user-visible improvements. Morty Seinfeld will
actually like us.

**Cons.** Less headline-grabbing than A or B. Risk of scope creep
into "we invented a framework" if the pattern library isn't
bounded.

**Likely episode arc.**

1. *The Alias* -- canonical model names + config migration.
2. *The Cache* -- prompt/response cache with safe invalidation.
3. *The Library* -- pattern library + cost estimator.
4. *The Receipt* -- spend reporting + budget warnings.

## Recommendation

**No recommendation without a showrunner.** Each theme is defensible;
the right choice depends on where the maintainer wants the tool to
grow. A + B are ecosystem plays; C is a UX / cost play.

If I had to choose autonomously, I'd pick **C -- Model Intelligence**:
lowest architectural risk, every episode ships standalone value,
clean fit with the existing codebase, and it buys time to design A or
B properly for S04. But that's a call the human should make.

## Outstanding season-independent items

These can ship any time, between seasons or as an opening bridge:

- Mac Keychain test-body rewrite (needs a Mac owner).
- `docs-lint.yml` "warn-only" label vs. actual exit code mismatch --
  flagged in *The Cleanup*.
- Linux `systemd-creds` / `secret-tool` opportunistic providers
  (seam exists; purely additive).

## How this file retires

When the showrunner picks a theme:

1. Season 3's header goes into `docs/exec-reports/README.md`.
2. The chosen arc above becomes the rough episode plan.
3. This file gets moved to `docs/exec-reports/archive/` for
   historical reference (showed which themes were considered).
4. The first episode (`s03e01-the-<noun>.md`) ships as usual.

*-- Mr. Pitt (program management), with notes from Costanza
(product), Elaine (structure), and a nudge from Morty Seinfeld (who
is quietly rooting for Theme C).*
