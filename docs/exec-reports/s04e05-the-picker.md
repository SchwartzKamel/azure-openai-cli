# S04E05 -- *The Picker*

> *Capability-aware default model picker lands as a pure function with four locked reason codes.*

**Commit:** `3bdac79..c4c47e9` (four E05 commits across three waves; three side-note commits parallel)
**Branch:** `main` (direct push throughout)
**Runtime:** ~2 days wall-clock
**Director:** Larry David (showrunner)
**Cast:** Costanza (LEAD), Maestro (CO-LEAD W1 + W3), Puddy (LEAD W2), Frank (review)

## The pitch

S04E03 *The Capabilities* installed the capability gate. S04E04 *Reading
Room* gave users a way to inspect the registry. E05 is the third leg of
the registry-aware-CLI arc: when the user does not pass `--model`, the
CLI must *pick* a default that is capability-aware rather than just
"slot zero of `AZUREOPENAIMODEL`". That picker now exists as
`ResolveSmartDefault.Pick`, a pure function with no I/O, four locked
reason codes, and a deterministic ordering over the allowlist.

The episode shipped in three small waves on purpose. W1 (Costanza LEAD,
Maestro co-lead) landed the pure function plus the single insertion
point in `Program.cs`. W2 (Puddy LEAD) factored the test corpus into its
own file for E11 reuse and bolted on the FDR adversarial fold-in. W3
(Maestro) absorbed Frank's F-PICKER-TRACE-01 review by replacing the
plaintext `AZ_AI_TRACE`-gated stderr line with a structured NDJSON sibling
on `TelemetryEmitter.EmitResolverDecision(...)` gated by the existing
`AZ_AI_TELEMETRY` env var. No user-visible flag (the `--prefer`
companion flag lands in E09); the picker is internal infrastructure
that takes over the default-resolution line.

The four reason codes are the public contract of the picker and will
not be renumbered without an ADR.

## Cast and waves

| Wave | Lead / Co-lead | Commit | Suite | Outcome |
|------|----------------|--------|-------|---------|
| **0** brief | Costanza | `3bdac79` | 1408 | Episode greenlit; design pinned to four reason codes |
| **1** picker | Costanza LEAD + Maestro co-lead | `0d7d303` | 1408 -> 1426 (+18) | `ResolveSmartDefault.cs` (NEW, pure function) + `Program.cs` insertion replacing the `AZUREOPENAIMODEL[0]` default line; nullable `LatencyTier`/`QualityTier` added to `ModelRegistryEntry` |
| **2** corpus + adversarial | Puddy LEAD | `314f16e` | 1426 -> 1459 (+33) | `ResolverTestCorpus.cs` factored out for E11 reuse; FDR adversarial cases folded in |
| **W2 review** | Frank | `1f0ed9f` | 1459 | F-PICKER-TRACE-01 filed (LOW): TRACE surface uses ad-hoc `AZ_AI_TRACE` env gate + plaintext, not `TelemetryEmitter` + NDJSON + `AZ_AI_TELEMETRY` |
| **3** TRACE wrap | Maestro | `66e8cf8` | 1459 -> 1462 (+3) | TRACE moved to `TelemetryEmitter.EmitResolverDecision(...)`; honors `AZ_AI_TELEMETRY`; structured NDJSON |
| **Close** | Maestro | `c4c47e9` | 1462 | F-PICKER-TRACE-01 marked CLOSED in finding doc |

Side-note commits that landed during E05 but are not E05 work and are
attributed to their owning episodes:

- `97fa95a` -- Pitt's S05 / S06 season plans (parallel planning)
- `63b6bb6` -- Kramer's F-S04E04-04 security scrub (closes E04 backlog)
- `b6df80f` -- Pitt's S04E06 *The Audit* brief draft (next-episode prep)

## The four locked reason codes

The picker returns exactly one of these on every call. They are the
public contract for `ResolveSmartDefault.Pick` and for the
`reason_code` field of the resolver NDJSON event:

- **`EXPLICIT`** -- the caller passed `--model <name>` and the name is
  on the allowlist. The picker echoes the request through unchanged.
- **`PREFER_AXIS`** -- (reserved for E09 `--prefer` wiring) the
  allowlist was filtered by a latency or quality axis and a non-head
  entry was selected.
- **`ALLOWLIST_HEAD`** -- the picker walked the allowlist in order and
  the head entry passed capability gating. This is the steady-state
  hot-path outcome.
- **`FALLBACK`** -- the head entry was capability-gated out and the
  picker walked to the next eligible entry. Distinct from
  `ALLOWLIST_HEAD` so the telemetry stream can flag silent
  capability-driven re-routing.

Total: four. The brief mentioned a fifth slot; design review at W1
locked the surface at four and reserved the fifth slot for a future
episode under ADR.

## What shipped

- **Production code** -- `azureopenai-cli/Resolution/ResolveSmartDefault.cs`
  (NEW, pure function); single-line insertion in `Program.cs`
  replacing the `AZUREOPENAIMODEL[0]` default-resolution site;
  `ModelRegistryEntry` gains nullable `LatencyTier` and `QualityTier`
  (null defaults, non-breaking, positional callers untouched);
  `TelemetryEmitter.EmitResolverDecision(...)` sibling emitter (W3).
- **Tests** -- `ResolveSmartDefaultTests.cs` (+18 W1 facts), corpus
  expansion + FDR adversarial cases (+33 W2 facts),
  `TelemetryEmitterTests.cs` resolver-event facts (+3 W3 facts).
  Total **1408 -> 1462 (+54)**. `ResolverTestCorpus.cs` extracted to
  its own file so E11 *The Corpus* can consume the same fixtures.
- **Docs** -- `docs/findings/F-PICKER-TRACE-01.md` filed (Frank) and
  closed (Maestro); this exec report; CHANGELOG `[Unreleased]` entry.
- **Not shipped** -- the `--prefer <axis>` CLI flag (deliberately
  deferred to E09; W1 PR keeps the `PREFER_AXIS` reason-code slot
  warm); user-facing `az-ai resolve --explain` surface (E10);
  registry schema for `LatencyTier`/`QualityTier` defaults (the
  fields are nullable for E05; populated by registry pass in E07).

## Findings

| ID | Severity | Filed by | Filed in | Status | Closed by |
|----|----------|----------|----------|--------|-----------|
| **F-PICKER-TRACE-01** | LOW | Frank | `1f0ed9f` | **CLOSED** | Maestro in `66e8cf8` (TRACE wrap via `TelemetryEmitter`) |

No other findings filed during E05. Episode-internal review surface
held; nothing rolls forward from E05 itself.

## Backlog rolling forward

E05 itself rolls nothing forward (single finding, closed in-episode).
Carried in from prior episodes and still open at the close of E05:

- **F-P-S04E04-02** -- capabilities `unknown` row rendering. UX call,
  Russell + Mickey.
- **A11Y-MR-02** -- `az-ai models` no-subcommand rc=2. Test contract
  blocking. Coordinated test+behavior change required.
- **A11Y-MR-04** -- `capabilities --raw` zero-hit consistency.
- **A11Y-MR-06** -- `show` renders `Family`/`Modalities` as `unknown`
  pending registry-schema extension.
- **F-EE-SP-001** -- `CapabilityGate.cs:106` `IsNullOrEmpty` vs
  `IsNullOrWhiteSpace` cosmetic. Still open since E03.
- **F-SP4-01** -- deterministic-clock seam for `TelemetryEmitter`
  bucket assignment. Frank, MEDIUM. Still open.

E04 backlog item **F-S04E04-04** (registry-reject raw-name echo) was
**closed mid-episode** by Kramer in `63b6bb6` (side-note, not E05
scope).

## Validation

- `dotnet build` -- 0 warnings, 0 errors on every commit in the range
- `dotnet test` -- **1462 / 1462 passed** at HEAD (`c4c47e9`)
  - W1: 1408 -> 1426 (+18 in `ResolveSmartDefaultTests.cs`)
  - W2: 1426 -> 1459 (+33 corpus + adversarial)
  - W3: 1459 -> 1462 (+3 in `TelemetryEmitterTests.cs`)
- `dotnet format --verify-no-changes` -- clean
- `make ascii-check` -- clean
- `make docs-lint` -- clean
- `make exec-report-check` -- passes (this report satisfies the gate)
- `make preflight` -- **clean at HEAD**
- Pre-push hook -- clean on every push in the range
- CI on `main` -- green at HEAD

## Releases

**None mid-episode.** v2.3.0 was published at S04E04 close
(`s04e04-reading-room.md`). v2.4.0 is anchored at the S04 finale
(E12); E05 contributes to that anchor but does not cut a tag of its
own. CHANGELOG `[Unreleased]` carries the entry.

## Lessons from this episode

1. **Surface-locking under W1 saves W3 from drift.** Maestro's W1
   review pinned the reason-code set at four with the fifth slot
   reserved by ADR rather than soft-reserved by comment. W3 did not
   need to renegotiate any contract.
2. **A "no I/O" pure function is the cheapest possible review.**
   Frank's W2 review found exactly one defect (`F-PICKER-TRACE-01`)
   and it was in the *call-site wiring*, not the picker itself. The
   picker has no I/O so the review was bounded to inputs-outputs.
   Worth repeating in E07 *The Registry Pass*.
3. **Factor the test corpus on the way in, not on the way to E11.**
   Puddy's W2 split `ResolverTestCorpus.cs` out of
   `ResolveSmartDefaultTests.cs` proactively so E11 *The Corpus* has
   a ready import target. Cheaper now than a rename PR later.
4. **TRACE wrap is a reflex now.** Any new stderr-emitting code path
   gets a Frank review and -- by precedent -- ends up on
   `TelemetryEmitter` with `AZ_AI_TELEMETRY` as the gate. Worth
   pulling into the `episode-brief` skill as a default acceptance
   criterion for any wave that emits diagnostics.

## Metrics

- Diff: 15 files changed, 2102 insertions(+), 15 deletions(-) across
  the E05 commit range (excludes side-note commits and this report)
- Test delta: **+54** (1408 -> 1462; W1 +18, W2 +33, W3 +3)
- Preflight: **passed** at HEAD
- CI status at push time: **green** on `main` at `c4c47e9`

## Credits

- **Costanza** -- LEAD W1, picker design, four-reason-code lock-in
- **The Maestro** -- co-lead W1 (review), LEAD W3 (TRACE wrap),
  finding-close in `c4c47e9`
- **Puddy** -- LEAD W2 (corpus extraction + adversarial fold-in)
- **Frank Costanza** -- W2 review, filed `F-PICKER-TRACE-01`
- **FDR** -- adversarial inputs absorbed into W2 corpus
- **Larry David** -- close, this exec report
- **Mr. Pitt** (side-note) -- S04E06 brief draft at `b6df80f`
- **Kramer** (side-note) -- F-S04E04-04 close in `63b6bb6`

All commits in the range carry the
`Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>`
trailer, including this one.

## On completion

S04E06 *The Audit* (Pitt-drafted brief at `b6df80f`) is **greenlit**.
That episode is the mid-season cast-balance checkpoint per the
`writers-room-cast-balance` skill -- Pitt audits screen-time
distribution across the fleet through E06 and reports findings at
the writers' room. No code is in scope for E06 by default; if the
audit surfaces a hot-path concern it gets its own follow-on episode.

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>
