**Status:** DRAFT (Costanza, 2026-05-21)

**Date:** 2026-05-21
**Lead:** Costanza -- product design, resolver spec, reason-chain UX
**Co-lead:** The Maestro -- algorithm correctness, edge cases, scoring rules
**Support:** David Puddy -- test corpus design (seeds E11 *The Corpus*)
**Dependencies:** S04E01 *The Registry* (GREENLIT, on `main`); S04E02 *Embedded Cards* (filming); S04E03 *The Capabilities* (GREENLIT) -- the gate runs AFTER this resolver; resolver is gate-blind by design. **Prologue dependency on E04**: card metadata needs `LatencyTier` and `QualityTier` fields alongside the existing `CostTier`; see Open Questions.

# S04E05 -- The Picker

> Log line: The user types `az-ai "hello"`. Forty milliseconds later a model
> is chosen. Today we choose the first comma-separated entry in an env-var.
> Tomorrow we choose the same model, but we can also explain in one sentence
> *why*, and when the user adds `--prefer latency` next season the same code
> picks the fast one. The picker is small. The picker is pure. The picker is
> *deterministic*. Hello.

---

You are filming **S04E05 *The Picker*** for `azure-openai-cli`. Working
directory: `/home/tweber/tools/azure-openai-cli`. Branch: `main`.

---

## The pitch

Today, when the user runs `az-ai "hello"` with no `--model` flag, the CLI
silently picks the first entry of `AZUREOPENAIMODEL`. That is the whole
algorithm. It is one line of code and it is brittle:

- Typo in position 1 of the env-var -> every default invocation fails.
- Vendor deprecates the model at position 1 -> every default invocation
  starts returning provider-side 4xx, and the user has no signal that the
  *resolver* picked it.
- User wants the cheap model as the default but the fast one for ralph mode
  -> no axis to express that.

E03 landed the capability *gate*. E05 lands the capability-*aware* default
*picker*. They are two different concerns. The gate enforces correctness
post-hoc (`this flag does not work with this model`). The picker chooses
proactively when the user has expressed no preference. The gate runs after
the picker. The picker may pick a model the gate then rejects -- that is
the correct ordering, because the rejection message names the resolved
model, and the user can fix the wrong choice with `--model`.

This episode ships the function and its tests. It does **not** ship the
`--prefer` flag yet -- that is E09. The function is built today so that E09
is a wiring exercise tomorrow.

---

## Scope (in)

- `Resolution/ResolveSmartDefault.cs` (NEW) -- pure function, no I/O.
- Signature:
  `ResolveSmartDefault.Pick(IModelRegistry registry, string[] allowlist, ResolverInputs inputs) -> ResolutionResult`
- `ResolutionResult` record: `(string Model, string ReasonCode, string HumanReason)`.
- Four reason codes locked: `EXPLICIT`, `PREFER_AXIS`, `ALLOWLIST_HEAD`, `FALLBACK`.
- Ordered preference walk:
  1. `inputs.ExplicitModel` set -> return it with `EXPLICIT`.
  2. `inputs.PreferAxis` set (`cost` | `latency` | `quality`) -> rank
     allowlist members by the corresponding card metadata field; return
     the head of the ranked list with `PREFER_AXIS`. **Note:** the
     `--prefer` flag itself ships in E09; E05 lands the code path so it is
     exercised by tests via `ResolverInputs.PreferAxis`.
  3. No explicit model, no axis -> return `allowlist[0]` with
     `ALLOWLIST_HEAD` (today's behavior, now explainable).
  4. Allowlist empty or head not in registry -> return `FALLBACK` with a
     human-readable reason naming `AZUREOPENAIMODEL`.
- Single insertion in `Program.cs`: the existing "first entry of
  `AZUREOPENAIMODEL`" line is replaced with a call to `Pick(...)`; the
  resolved `ResolutionResult.Model` continues into the existing flow.
- TRACE-level log line emitted with the full reason chain (Frank's
  observability surface; structured fields).
- `--doctor` shows the would-be default selection and the reason chain.
- `models list` (planned E04) is told it can read this same function to
  preview the default; if E04 has not shipped that surface yet, the brief
  is a no-op for `models list` and we revisit in E04.

## Scope (out)

- The `--prefer` flag itself (E09 *The Preference*).
- Benchmark-driven cost/latency/quality data sourcing (E10 *The Score*).
  E05 reads whatever tier values the cards expose; E10 makes those tiers
  defensible.
- Multi-model fanout, A/B routing, shadow-traffic (S05 territory).
- Model pinning to subscription tiers / SKU-level routing.
- The capability gate (E03 owns that; it runs after the resolver).

---

## Acceptance criteria

1. `make preflight` exits 0.
2. `Pick(...)` is **pure**: no file I/O, no env-var reads, no clock reads,
   no allocations outside the returned `ResolutionResult` and the
   intermediate score array. All inputs are parameters.
3. `Pick(...)` is **deterministic**: same registry snapshot + same
   allowlist + same `ResolverInputs` -> bit-identical `ResolutionResult`.
   A test asserts this over 100 iterations with shuffled iteration of
   registry dictionaries.
4. `ResolutionResult` is an `internal sealed record` with exactly three
   fields: `string Model`, `string ReasonCode`, `string HumanReason`.
   Registered in `AppJsonContext` if and only if `--doctor` serializes it
   (otherwise no JSON registration -- AOT budget).
5. `ReasonCode` is one of the string constants `EXPLICIT`, `PREFER_AXIS`,
   `ALLOWLIST_HEAD`, `FALLBACK`. Defined as `public const string` on a
   static `ResolutionReason` class -- no enum (AOT JSON cost) and no
   string literals scattered through the codebase.
6. `inputs.ExplicitModel = "gpt-4o-mini"` -> result is
   `("gpt-4o-mini", "EXPLICIT", "user supplied --model gpt-4o-mini")`.
   No registry consultation required; the gate (E03) will validate
   afterwards.
7. `inputs.PreferAxis = "cost"` with two models in the allowlist whose
   cards expose `CostTier` values `low` and `high` -> `low` wins; reason
   `PREFER_AXIS`; `HumanReason` names the axis and the runner-up.
8. `inputs.PreferAxis = "latency"` and `inputs.PreferAxis = "quality"`
   each route to the corresponding tier field. If the tier field is
   missing on a card, the model sorts last (stable), not first.
9. Empty allowlist -> result is
   `("", "FALLBACK", "AZUREOPENAIMODEL is empty; set the env-var to a comma-separated list of deployment names")`
   and `Program.cs` translates this into an `ErrorAndExit(..., 2, ...)`
   call. The resolver itself does **not** exit; it returns. Separation of
   concerns.
10. Allowlist head names a model not in the registry -> `FALLBACK` with
    `HumanReason` naming the missing model and pointing at `--doctor`.
11. `HumanReason` is ASCII only and capped at 120 characters. A test
    asserts the cap on every reason code.
12. Compatible with E03 `CapabilityGate`: the gate runs after the
    resolver returns. The picker never consults capability tags. A test
    asserts that a `PREFER_AXIS` selection of an axis-winner lacking a
    required capability still returns that winner (the gate will reject;
    that is the correct user signal).
13. TRACE log line is emitted via the existing telemetry surface
    (Frank's `TelemetryEmitter`) with structured fields: `model`,
    `reason_code`, `human_reason`, `allowlist_size`. Off by default; no
    behavior change for users who do not opt into telemetry.
14. `--doctor` output gains one section: *Default model selection (would
    pick)* showing `Model`, `ReasonCode`, `HumanReason`. One block, three
    lines, no color codes. Renders identically under `NO_COLOR=1`.
15. AOT delta budget: <= 15 KB. No new records beyond `ResolutionResult`
    and `ResolverInputs`. No `Linq.Expressions`. Bania baselines pre-
    and post-merge.

---

## Dispatch plan (sub-agents and files)

| Wave | Agent | Files (file-disjoint) | Scope |
|------|-------|-----------------------|-------|
| 1 | **Costanza (lead)** | `azureopenai-cli/Resolution/ResolveSmartDefault.cs` (NEW) -- co-authored with Maestro on the shared file via the orchestrator's shared-file protocol (Costanza drafts skeleton + records + reason-string wording; Maestro fills the algorithm body) | Records (`ResolutionResult`, `ResolverInputs`), `ResolutionReason` constants, `HumanReason` strings (<=120 chars each), public `Pick(...)` entrypoint signature. |
| 1 | **The Maestro (co-lead)** | Same file as above (shared-file handoff after Costanza's skeleton lands) + single insertion call in `azureopenai-cli/Program.cs` (one call site, replacing the existing `AZUREOPENAIMODEL[0]` line, post-arg-parse, pre-`CapabilityGate`) | Ordered preference walk, axis-scoring (stable sort with missing-tier-last), empty-allowlist and missing-registry-head branches, TRACE log emission. |
| 2 | **Puddy** | `tests/AzureOpenAI_CLI.Tests/ResolveSmartDefaultTests.cs` (NEW) | xUnit suite covering all acceptance criteria; determinism test (100 iterations); axis-tie-break test; missing-tier-last test; capability-gate-blindness assertion. **Seeds the E11 corpus**: test inputs are factored into a `ResolverTestCorpus` static class so E11 can reuse them. |
| 2 | **Frank Costanza** | Review-only of the TRACE log emission; may file findings; no code edits | Confirm structured fields are observable through the existing telemetry surface and that off-by-default semantics hold. |
| 3 | **FDR** | Append-only to a new `docs/findings/F-PICKER-*.md` if adversarial review surfaces issues; no code | Adversarial corpus contribution to E11. Empty allowlist with a single whitespace entry. Allowlist where head matches a registry entry but its card is malformed. PreferAxis with an unknown axis string. |

Shared-file note: `ResolveSmartDefault.cs` is the only file with two
authors in Wave 1. The protocol: Costanza commits the skeleton (records,
constants, `Pick` signature, `throw new NotImplementedException()` body),
Maestro replaces the body in a follow-up commit. No concurrent edits. The
showrunner (Larry David) signs off on the handoff timestamp. `Program.cs`
has exactly one author (Maestro) for the duration of this episode.

---

## Risks and mitigations

- **The `cost` / `latency` / `quality` tier fields do not yet exist on
  the card.** Today `ModelRegistryEntry` has `CostTier`; there is no
  `LatencyTier` or `QualityTier`. *Mitigation:* add the two missing tier
  fields as an E05 prologue (or as a corollary commit in E04 *The
  Catalog* if that ships first). The fields are nullable strings with
  values in `{low, medium, high}`; missing means "unknown" and sorts
  last. ADR-014 records the tier vocabulary. Without this prologue,
  acceptance criteria 7 and 8 cannot pass.
- **PREFER_AXIS picks a model the gate rejects.** A user sets
  `--prefer cost` and the cheapest model lacks `tool_calls` while the
  user also passes `--tools`. The picker returns the cheap model; the
  gate (E03) rejects it; the user sees a rejection message naming the
  cheap model and is confused about why a cheap model was picked when
  they asked for tools. *Mitigation:* this is acceptable in E05 (the
  gate's rejection message tells them what to do). E09 will revisit:
  `--prefer` could optionally pre-filter by required capabilities. That
  is an E09 design decision, not an E05 one. Document the deferral.
- **Determinism under dictionary iteration.** .NET dictionary
  enumeration order is documented as undefined. *Mitigation:* `Pick`
  reads the allowlist (an ordered array) and looks up registry entries
  by name. It never enumerates the registry dictionary directly. The
  determinism test asserts this by passing a deliberately
  perturbed registry on each iteration.
- **`HumanReason` drift.** Once a string ships, users grep for it. A
  later edit breaks scripts. *Mitigation:* `HumanReason` strings are
  defined as `public const string` formatting templates with at most one
  `{0}` interpolation slot, all in one place at the top of
  `ResolveSmartDefault.cs`. ADR-014 marks them as a stable surface
  starting in v1.0 (not before).
- **Scope creep into E09.** It is tempting to land the `--prefer` flag
  this episode since the resolver supports it. *Mitigation:*
  `ResolverInputs.PreferAxis` is exercised only by tests in E05. No
  command-line parsing change. Mr. Pitt holds the line.

---

## AOT delta budget

**Target: <= 15 KB.** Two new records (`ResolutionResult`,
`ResolverInputs`), one static class of `const string` reason codes and
template strings, one pure function. No new `AppJsonContext`
registrations unless `--doctor` requires serializing the result (decide
in Wave 2; if yes, +~3 KB acceptable within the 15 KB envelope). No new
embedded resources. No reflection paths. Bania baselines.

---

## Reason-chain canonical examples (Costanza owns the wording)

```text
EXPLICIT       -- model 'gpt-4o-mini' chosen because user passed --model
PREFER_AXIS    -- model 'gpt-4o-mini' chosen for axis 'cost' (tier low; runner-up gpt-5.4)
ALLOWLIST_HEAD -- model 'gpt-4o-mini' chosen as head of AZUREOPENAIMODEL
FALLBACK       -- AZUREOPENAIMODEL is empty; set the env-var to a comma-separated list of deployment names
```

Each line is one `HumanReason`. Each is <= 120 characters. No preamble,
no markdown, no trailing period required. The product surface is the
reason code; the human string is for the human in the loop.

---

## Open questions

- **Where does the resolver live?** Proposed: top-level `Resolution/`
  namespace, peer to `Registry/` and `Capabilities/`. Cross-cuts both.
  Larry David signs off before Wave 1 lands.
- **Does the resolver consume seed-only data or also embedded cards?**
  Proposed: both. The registry presents a unified `IModelRegistry`
  surface where overrides are already merged (E01 contract). The
  resolver does not care about provenance; it reads `ModelRegistryEntry`
  fields and is done. Pin in ADR-014.
- **`HumanReason` length cap.** Proposed: 120 characters. Rationale:
  fits in a single terminal line at 80 columns with a `--doctor` prefix
  and still leaves margin. Reject 240 (too long for inline doctor
  output) and 80 (too tight for runner-up annotation in `PREFER_AXIS`).
- **Card-metadata tier fields.** `CostTier` exists today on
  `ModelRegistryEntry`. `LatencyTier` and `QualityTier` do **not**.
  E05 cannot complete without them. Decision: add the two fields as an
  E05 prologue commit (small, ADR-014 covers the vocabulary), OR fold
  the addition into E04 *The Catalog* if E04 ships first. Mr. Pitt
  arbitrates the sequencing.
- **Should `--doctor` serialize the result as JSON?** Affects
  `AppJsonContext` and AOT delta. Proposed: no, render as three plain
  lines; defer JSON until a consumer asks for it.

---

## References

- ADR-012 -- model registry seam (E01)
- ADR-013 -- capability gate (E03)
- ADR-014 -- smart-default resolver and tier vocabulary (NEW; this
  episode authors it)
- `azureopenai-cli/Registry/ModelRegistryEntry.cs` -- source of
  `CostTier`; target for `LatencyTier` and `QualityTier` additions
- `azureopenai-cli/Registry/ModelRegistry.cs` -- `IModelRegistry`
  surface the resolver consumes
- `azureopenai-cli/Capabilities/CapabilityGate.cs` (E03) -- runs after
  the resolver; gate-blindness asserted in tests
- `docs/episode-briefs/s04e03-the-capabilities.md` -- gate brief,
  ordering contract
- `.github/skills/episode-brief.md` -- canonical brief format
- `.github/agents/costanza.agent.md` -- product PM voice; this brief
  follows the standards listed there

---

## Validation

```bash
# ASCII punctuation -- 0 matches required
grep -nP '[\x{2018}\x{2019}\x{201C}\x{201D}\x{2013}\x{2014}]' \
  docs/episode-briefs/s04e05-the-picker.md

# Markdownlint clean
NODE_OPTIONS="--max-old-space-size=4096" \
  npx markdownlint-cli2 docs/episode-briefs/s04e05-the-picker.md
```

---

## On completion

```sql
UPDATE todos SET status = 'done' WHERE id = 's04e05-brief';
```

Return to showrunner: commit SHA, line count, the four locked reason
codes (`EXPLICIT`, `PREFER_AXIS`, `ALLOWLIST_HEAD`, `FALLBACK`), and a
yes/no on whether `LatencyTier` / `QualityTier` ship as an E05 prologue
or get folded into E04.
