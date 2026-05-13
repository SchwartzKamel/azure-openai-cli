# ADR-013: Capability Gate

## Status

GREENLIT (S04E03) -- 2026-05-21

## Context

S04E01 *The Registry* shipped a typed `ModelRegistry` whose every entry
carries a `Capabilities` array drawn from the closed set in
`ModelCapability.AllowedTags` (`tool_calls`, `vision_in`, `vision_out`,
`json_mode`, `streaming`, `system_prompt`). S04E02 *Embedded Cards* made
that array load from per-model card resources rather than a flat seed.
As of E02 landing, the CLI knows -- at startup, before any network call --
whether the resolved model carries each capability tag.

It does not, today, consult that knowledge before building requests.
Four flags are accepted unconditionally and dispatched to the provider:
`--tools`, `--schema`, `--stream`, `--system-prompt`. Each one requires
a specific capability tag on the resolved model. When the model lacks
the capability, the failure surfaces as a provider-side artifact: a 4xx
wire response, an empty tool-call loop, a streaming connection that
returns one chunk and closes, a system message the model silently
ignores. The user diagnoses upward through three layers of abstraction
(provider error -> request body -> flag combination) before discovering
that the model was the wrong choice for the prompt.

The right cut point for that diagnosis is *startup*. The registry is
fully loaded by then. The resolved model name is known. The flag set is
parsed. Client construction has not begun. Everything required to
answer "does this model support what this invocation needs?" is in
hand, and nothing of consequence has been committed -- no HTTP socket,
no streaming response, no partial tool-call state. A startup-time gate
turns a five-minute confused-4xx debugging session into a single
sentence on stderr and an `rc=2`.

A runtime gate -- one that checks capabilities just before each provider
call inside the agent or ralph loop -- was considered and rejected. It
would catch the same cases but at higher cost: more code paths, more
test surface, partial state to unwind, and a worse user experience
because the failure now arrives N seconds into a session rather than
before it begins. The capability surface does not change mid-session;
moving the check later buys nothing and pays a complexity tax.

An `AZ_AI_DISABLE_CAPABILITY_GATE` escape hatch was proposed during
brief review and rejected outright. The episode brief risk register
captures the reasoning: an env-var that disables a correctness gate is
an env-var that ends up in someone's `~/.config/az-ai/env` six months
from now and silently re-enables exactly the confused-4xx era this
episode exists to end. If the gate is wrong about a specific model,
the fix is in the registry card (a one-line capability-tag edit), not
in a runtime opt-out. Acceptance criterion #9 forces this rejection
into this ADR so a future maintainer cannot quietly add the env-var
without reopening this file.

## Decision

- The capability gate is **startup-only**. It runs after model
  resolution and before client construction, at a single call site in
  `Program.cs`. There is no runtime check inside the agent loop, the
  ralph loop, or the streaming receiver.
- The gate reads capability tags off the **registry card**
  (`ModelRegistryEntry.Capabilities`), not off the legacy
  `Capabilities/CapabilityDescriptor` matrix introduced in S03E18.
  The two surfaces are documented here as a known dual-surface
  condition; reconciliation is a deliberate non-goal of E03 and is
  tracked as a finding for a later episode.
- Four flag-vs-capability pairs are enforced in E03:

  | Flag | Required capability |
  |------|---------------------|
  | `--tools <name>` | `tool_calls` |
  | `--schema <path>` | `json_mode` |
  | `--stream` | `streaming` |
  | `--system-prompt <path>` | `system_prompt` |

  Flags not in this table pass the gate by design (closed-set policy:
  the gate enforces what it knows about and is silent on the rest).
- On rejection, the gate calls `CapabilityRejection.Build` and hands
  the resulting single-line string to `ErrorAndExit(..., rc=2, ...)`.
  Exit code `2` is the standard CLI usage-error code; provider errors
  remain `rc=1`. The distinction matters for shell scripting around
  `az-ai`.
- Suggestion list is filtered to the **intersection** of
  (registered models advertising the capability) and the
  `AZUREOPENAIMODEL` allowlist. Models the user has not configured
  are not suggested. When the intersection is empty, the message ends
  with `no configured model supports this; see --doctor` (canonical
  string defined in `CapabilityRejection`).
- The rejection wording is owned by `CapabilityRejection.cs` (Bookman)
  as a separate file so re-tiering the wording does not require
  reopening the gate logic. The wording fits the Snap tier: single
  line, no preamble, no markdown, ASCII only, `<=240` chars before the
  suggestion tail.
- **No `AZ_AI_DISABLE_CAPABILITY_GATE` env-var.** No `--no-capability-gate`
  flag. No opt-out surface of any kind. If the gate is wrong for a
  specific model, the fix is a one-line capability-tag edit in the
  registry override file at `~/.config/az-ai/registry.json`.

## Consequences

### Positive

- Wrong-model invocations fail in one sentence at startup instead of
  three layers of confused provider output minutes later.
- `rc=2` makes capability rejections programmatically distinguishable
  from provider errors in shell pipelines, Espanso wrappers, and CI.
- The gate is the *only* place that maps a flag to a capability tag;
  future episodes that add a flag (or a tag) reopen one file rather
  than auditing a scattered set of check points.
- The rejection message names the specific switch the user should pass
  next (`--model <name>` with one of the suggestions), shortening the
  retry loop.

### Negative

- Two capability surfaces coexist in the codebase until the
  S03E18-vs-S04E03 reconciliation episode lands: the registry tags
  and the legacy `ProviderCapabilities` matrix. New contributors must
  be told which one is authoritative for which feature. The dual
  surface is the cost of shipping E03 without blocking on a wider
  refactor.
- The closed-set policy means a new flag with a new capability
  requirement is a code change, not a config change. Adding a row to
  the flag-vs-capability table requires an ADR-013 amendment, a new
  test pair, and a re-tier of the rejection wording if the new flag
  pushes the 240-char prefix budget.
- Users who keep `AZUREOPENAIMODEL` set to a single deployment will
  often see the empty-intersection tail (`no configured model supports
  this; see --doctor`). The trade-off is intentional: suggesting a
  model the user has not configured is worse than admitting we have
  nothing to suggest.

### Neutral

- AOT delta is expected to be small (three new types, one new helper
  method on `ModelRegistry`, no new `AppJsonContext` registrations,
  no new embedded resources). Bania baselines pre- and post-merge.

## Alternatives considered

### (a) Runtime gate inside the agent / ralph / streaming loops

Check capabilities just before each provider call. Rejected: same
information available at startup, but the failure now arrives N
seconds into a session, leaves partial state to unwind, and multiplies
the test surface across every loop. The capability surface is
immutable for the lifetime of the process; the earliest valid check
is the correct check.

### (b) `AZ_AI_DISABLE_CAPABILITY_GATE` env-var bypass

Allow operators to opt out of the gate per invocation or per
environment. Rejected: an opt-out for a correctness gate becomes
load-bearing the moment someone ships a wrong registry tag. The fix
for a wrong tag is to fix the tag, in the registry-override file. An
env-var bypass migrates the bug from the registry into someone's shell
profile, where it is invisible to `--doctor` and outlives the
maintainer who set it. The brief's risk register made this rejection
explicit and acceptance criterion #9 pins it here.

### (c) Capability inference from the prompt or the flag pattern

Infer the required capability set from prompt content (`"call this
tool..."` -> `tool_calls`) or from heuristic flag combinations.
Rejected: the registry is the single source of truth for capability
declarations; inferring capability from prose introduces a second
surface that disagrees with the registry on edge cases, and "the
prompt mentions a tool" is not the same proposition as "the user
passed `--tools`". The flag set is unambiguous; the prompt is not.

### (d) Read capability from the legacy `Capabilities/CapabilityDescriptor` matrix

Reuse the S03E18 capability matrix as the authoritative source.
Rejected: the registry tags (E01) are the surface the rest of S04
builds on (`--doctor`, smart defaults, routing rules, cost
visibility). Anchoring E03 to the legacy matrix would force every
downstream episode to either query both surfaces or pick the wrong
one. Documenting the dual surface and deferring reconciliation is
cheaper than building E03 on the surface we plan to retire.

## Adversarial review (FDR, S04E03 Wave 3)

<!-- FDR appends adversarial appendix here in Wave 3. Do not pre-fill. -->
