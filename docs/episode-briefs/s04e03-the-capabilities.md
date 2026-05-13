**Status:** GREENLIT (Larry David, 2026-05-13)

**Date:** 2026-05-21
**Lead:** Lt. Bookman -- error-message wording, brevity discipline on rejections
**Co-lead:** The Maestro -- gate logic and request-construction insertion point
**Dependencies:** S04E01 *The Registry* (GREENLIT, on `main`); S04E02 *Embedded Cards* (filming, MUST land first -- this episode reads `Capabilities` off every registry entry)

# S04E03 -- The Capabilities

> Log line: Two CLIs, one with a capability gate, one without. That is funny.
> The user picks `gpt-5.4-nano` and passes `--tools shell`. Today we accept the
> flag, build the request, ship it to the provider, and the provider says no.
> Truncated JSON. Confused user. Five minutes of "did I typo the tool name?"
> Tomorrow we say no at startup, in one sentence, and tell them which model
> to switch to.

---

You are filming **S04E03 *The Capabilities*** for `azure-openai-cli`. Working
directory: `/home/tweber/tools/azure-openai-cli`. Branch: `main`.

---

## The pitch

S04E01 planted the registry. Every model carries a `Capabilities` array. S04E02
makes that array load from embedded cards instead of a flat seed. So as of E02
landing, the CLI *knows* whether `gpt-5.4-nano` does `tool_calls`. It just does
not consult that knowledge before building requests.

Today four flags are accepted unconditionally: `--tools`, `--schema`,
`--stream`, `--system-prompt`. Each one requires a specific capability tag on
the resolved model. When the model lacks the capability, the failure surfaces
as a provider-side error: a 4xx wire response, an empty tool-call loop, a
streaming connection that never streams, a system message that the model
silently ignores. The user diagnoses upward through three layers of
abstraction before realizing the model was wrong for the prompt.

Move the failure forward. At argument-parse time, after model resolution,
before client construction, check each requested capability against the
registry entry. If absent, exit with rc=2 (CLI usage error) and a message
that names the unsupported capability, the resolved model, and the list of
configured models that *do* support it. One sentence. No preamble. No
markdown. Belt-and-suspenders enforcement: the gate is the hard ceiling, the
suggestion list is the soft handoff to the next attempt.

---

## Scope (in)

- Capability gate enforced at startup for: `--tools`, `--schema`, `--stream`,
  `--system-prompt`.
- Single rejection-message format with the suggestion list.
- `ModelRegistry.ModelsWithCapability(string tag) -> string[]` helper.
- Gate respects the `AZUREOPENAIMODEL` allowlist when generating suggestions
  (no point suggesting a model the user has not configured).
- xUnit suite covering each (flag, capability) pair, both directions
  (rejected / allowed), rc=2, and message contents.
- a11y pass on the rejection message: NO_COLOR honoured, no ANSI, screen
  reader friendly.
- ADR-013 records the gate design and the env-var-bypass rejection.

## Scope (out)

- Smart routing / model auto-selection from the prompt (E05 *The Picker*).
- Capability *inference* from a card prose section (cards are authoritative).
- Suggesting models not in `AZUREOPENAIMODEL` even if registered.
- Adding new capability tags to `ModelCapability.AllowedTags` (locked in E01;
  only E03 adds a tag if a flag-vs-capability mapping requires one, in which
  case ADR-012 gets a migration note).
- Runtime / mid-stream capability checks. The gate is startup-only.
- Re-architecting the prior `Capabilities/` directory from S03E18
  (`CapabilityDescriptor`, `ProviderCapabilities`); E03 reads the
  registry-card capability tags, not the legacy provider matrix. Reconciliation
  of the two surfaces is a deliberate non-goal of E03 and is captured as a
  finding for a later episode.

---

## Acceptance criteria

1. `make preflight` exits 0.
2. `az-ai --model gpt-5.4-nano --tools shell "ping"` exits with rc=2 and a
   single-line stderr message containing the strings `gpt-5.4-nano`,
   `tool_calls`, and at least one suggested model name.
3. `az-ai --model gpt-4o-mini --tools shell "ping"` is *not* rejected by the
   gate (proceeds to normal execution).
4. The four covered flags (`--tools`, `--schema`, `--stream`,
   `--system-prompt`) each have a passing
   `Gate_Rejects_<Flag>_When_<Capability>_Missing` test and a matching
   `Gate_Allows_<Flag>_When_<Capability>_Present` test.
5. Suggestion list is filtered to the intersection of (registered models with
   the capability) and (`AZUREOPENAIMODEL` allowlist); when the intersection
   is empty, the message ends with `no configured model supports this; see
   --doctor` instead of an empty list.
6. Rejection message is ASCII only, contains no ANSI escape sequences, and
   respects `NO_COLOR=1` when the rest of the CLI emits color elsewhere.
7. Rejection message is a single line, no leading prefix beyond the standard
   `[ERROR]` prefix from `ErrorAndExit`, and does not exceed 240 characters before
   the suggestion list.
8. `make publish-aot` binary grows by no more than 25 KB versus the
   pre-episode baseline.
9. `ADR-013-capability-gate.md` exists in the canonical ADR format and records
   the explicit rejection of an `AZ_AI_DISABLE_CAPABILITY_GATE` escape hatch.

---

## Dispatch plan (sub-agents and files)

| Wave | Agent | Files (file-disjoint) | Scope |
|------|-------|-----------------------|-------|
| 1 | **Bookman (lead)** | `azureopenai-cli/Cli/CapabilityRejection.cs` (NEW); ADR-013 skeleton | Rejection-message builder; one method `Build(string flag, string capability, string model, IReadOnlyList<string> suggestions)`. Brevity discipline applied to its own output: <=240 chars before the suggestion list. No `Sure, here is...`. No markdown. No preamble. |
| 1 | **Maestro (co-lead)** | `azureopenai-cli/Capabilities/CapabilityGate.cs` (NEW); single insertion call in `azureopenai-cli/Program.cs` (one call site, post-model-resolution, pre-client-construction) | Gate logic. Reads the resolved `ModelRegistryEntry`, checks the four flag-vs-capability pairs, calls Bookman's builder on rejection, hands the string to `ErrorAndExit(..., 2, ...)`. |
| 1 | **Kramer** | `azureopenai-cli/Registry/ModelRegistry.cs` -- add `ModelsWithCapability(string tag) -> string[]` helper only | One method, one signature, no other edits to Registry. File-disjoint from Maestro and Bookman. |
| 2 | **Puddy** | `tests/AzureOpenAI_CLI.Tests/CapabilityGateTests.cs` (NEW) | xUnit suite per Acceptance #4, plus rc=2 assertion, plus message-content assertions. Add an integration assertion in `tests/integration_tests.sh` only if the unit suite cannot reach a code path. |
| 2 | **Mickey** | Review only of `CapabilityRejection.cs` output; may file findings; may NOT edit code | a11y pass: NO_COLOR, ANSI-free, screen-reader friendly. If a change is required, Mickey files a finding; Bookman edits in Wave 3. |
| 3 | **FDR** | Append-only to `docs/adr/ADR-013-capability-gate.md` (Adversarial appendix) | Adversarial review post-impl. No code touches. Findings into the ADR appendix. |

Shared-file note: only Maestro touches `Program.cs` in Wave 1, and only the
single insertion call. No other agent opens `Program.cs` for the duration of
this episode. Bookman's message builder is a separate file precisely so that
re-tiering the rejection wording does not require reopening the gate logic.

---

## Risks and mitigations

- **Registry override produces surprising allow/deny shifts.** A user override
  card flips a capability tag and a flag silently starts working (or stops).
  *Mitigation:* `--doctor` already lists capability tags per model (E01); add
  one line to the doctor output stating which override file (if any) was
  loaded. Document in ADR-013.
- **A provider supports a capability we have no tag for.** User passes a flag
  that has no capability mapping; the gate has nothing to check; the request
  goes through. *Mitigation:* this is the correct behavior. The gate only
  checks the four mapped flags; everything else passes by design. Document
  the closed-set policy in ADR-013.
- **`AZ_AI_DISABLE_CAPABILITY_GATE` env-var temptation.** Recommended:
  **NO**. If you need to bypass, fix the registry. An env var that disables
  the gate is an env var that ships in someone's `~/.config/az-ai/env` and
  silently re-enables the confused-4xx era six months from now. ADR-013
  records the rejection.
- **Mismatch with the legacy `Capabilities/` matrix from S03E18.** Two surfaces
  now describe model capability: the registry tag set (E01/E02) and
  `ProviderCapabilities` (S03E18). The gate reads the registry, not the
  matrix. *Mitigation:* document the dual surface in ADR-013; file a finding
  to reconcile in a later episode. Do not reconcile here.
- **Suggestion list churn under partial allowlists.** A user with only one
  model in `AZUREOPENAIMODEL` will frequently see "no configured model
  supports this." *Mitigation:* the message ends with the actionable
  `see --doctor`, which lists every registered model. No additional code.

---

## AOT delta budget

**Target: <= 25 KB.** Kramer's lesson from E01: typed-record additions hit
~44.5 KB. This episode adds no records. Three small classes
(`CapabilityRejection`, `CapabilityGate`, one helper method on
`ModelRegistry`) and a test file. No new `AppJsonContext` registrations. No
new embedded resources. If the binary grows by more than 25 KB, something is
wrong; check for accidental reflection paths or a forgotten LINQ chain that
pulled in `System.Linq.Expressions`. Bania will baseline pre- and post-merge.

---

## Flag-vs-capability mapping (canonical for E03)

| Flag | Required capability tag | Notes |
|------|-------------------------|-------|
| `--tools <name>` | `tool_calls` | Also covers built-in tool-calling features that imply tool dispatch (agent mode, ralph mode). |
| `--schema <path>` | `json_mode` | JSON-schema-constrained output. |
| `--stream` | `streaming` | SSE / chunked streaming. |
| `--system-prompt <path>` | `system_prompt` | Some local models ignore system messages; tag is authoritative. |

Any flag not in this table is out of scope for E03. Adding a row to this
table after E03 ships requires an ADR-013 amendment, a new test pair, and a
re-tier of the rejection message if the new flag does not fit the existing
240-char budget.

---

## Rejection message format (Bookman owns the wording)

Single line, ASCII only, no preamble, no markdown:

```text
[ERROR] Model 'gpt-5.4-nano' does not support 'tool_calls' (required by --tools). Models that do: gpt-4o-mini, llama-local. Pass --model <name>.
```

If the suggestion intersection is empty:

```text
[ERROR] Model 'gpt-5.4-nano' does not support 'tool_calls' (required by --tools). No configured model supports this; see --doctor.
```

Brevity discipline: the message is the message. No `Sure, here is the
problem:`. No `I hope this helps`. No trailing newline beyond the one
`ErrorAndExit` already emits. The model talks when it has something to say;
otherwise it shuts up.

---

## References

- ADR-012 -- model registry seam (E01)
- ADR-013 -- capability gate (NEW; this episode authors it)
- `docs/exec-reports/s04e01-*.md` -- E01 execution report (registry shipped,
  AOT delta noted at 44.5 KB)
- `docs/episode-briefs/s04e02-*.md` -- E02 brief (embedded cards; reads
  `Capabilities` array off every entry)
- `azureopenai-cli/Registry/ModelCapability.cs` -- canonical capability tag
  enum (`AllowedTags`)
- `azureopenai-cli/Capabilities/CapabilityDescriptor.cs` -- legacy S03E18
  capability matrix; reconciliation deferred per ADR-013
- `.github/skills/episode-brief.md` -- canonical brief format (this file
  follows it)
- `.github/agents/bookman.agent.md` -- tier doctrine; Snap-tier discipline
  applied to the rejection-message wording

---

## Validation

```bash
# ASCII punctuation -- 0 matches required
grep -nP '[\x{2018}\x{2019}\x{201C}\x{201D}\x{2013}\x{2014}]' \
  docs/episode-briefs/s04e03-the-capabilities.md

# Markdownlint clean
NODE_OPTIONS="--max-old-space-size=4096" \
  npx markdownlint-cli2 docs/episode-briefs/s04e03-the-capabilities.md
```

---

## On completion

```sql
UPDATE todos SET status = 'done' WHERE id = 's04e03-brief';
```

Return to showrunner: commit SHA, line count, the paragraph cut for brevity
that future-Bookman may reinstate.
