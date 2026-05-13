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

## Accessibility review (Mickey Abbott, S04E03 Wave 2)

The rejection message reaches users through Espanso popups, screen readers,
AHK tooltips, pipe-to-grep workflows, and CI logs. Full a11y walkthrough --
JAWS/NVDA/VoiceOver/Orca read-aloud transcript, NO_COLOR confirmation,
colorblind-safe analysis, keyboard-only path, pipe contracts, truncation
behavior -- lives in
[docs/model-cards/REVIEW-capability-rejection.md](../model-cards/REVIEW-capability-rejection.md).

Headline: NO_COLOR is honored **by construction** (Bookman's `Scrub`
whitelists `0x20..0x7E`, which excludes every C0/C1 byte including ESC), no
color is used so colorblind-safety is N/A, the message is single-line plain
ASCII so the screen-reader / keyboard-only / piped paths are identical.

Open findings (both low severity, deferred):

- **A11Y-CG-01** -- the printable-ASCII whitelist permits `'` (0x27), so a
  hostile registry name containing an apostrophe would break shell-quoted
  propagation of the rejection string. Recommended fix: reject `'` at
  registry load time. Overlaps thematically with FDR's Wave 3 registry
  hostile-input findings; route to E04.
- **A11Y-CG-02** -- the suggestion tail is unbounded; word-boundary
  truncation deferred to S04E04 *Reading Room*. Overlaps with
  F-EE-AR-09 (Bookman's brevity budget covers prefix only).

Regression guards: 7 assertions in
`tests/AzureOpenAI_CLI.Tests/CapabilityGateAccessibilityTests.cs`.

## Adversarial review (FDR, S04E03 Wave 3)

**Date:** 2026-05-21
**Scope:** `CapabilityGate.Check` (`Capabilities/CapabilityGate.cs`),
`CapabilityRejection.Build` (`Cli/CapabilityRejection.cs`),
`ModelRegistry.ModelsWithCapability` (Wave 1 helper), and the Wave 1
insertion site in `Program.cs` lines 751-753 (post-pin-recheck,
pre-client-construction).
**Findings:** 11 (0 CRITICAL, 0 HIGH, 1 MEDIUM, 4 LOW, 2 INFO, 2 NIT, 2 PASS)

> **No episode-blocking findings.** The gate is correctness-shaped, not
> security-shaped, and its rejection wording is well-scrubbed. The one
> MEDIUM (F-EE-AR-09) is a contract-violation -- Bookman's 240-char
> brevity budget is enforced on the prefix only, so the suggestion tail
> can balloon arbitrarily when the registry override is hostile. Hand
> to Bookman + Newman for an E04 follow-up, not a hotfix lane.

### Top findings

**1. F-EE-AR-09 (MEDIUM, verified) -- the 240-char budget is enforced on
the prefix, not the assembled message.** `CapabilityRejection.Build`
checks `prefix.Length > PrefixBudgetChars` (line 84) but the suggestion
tail (`Try: a, b, c, ...`) is appended without any per-message ceiling.
An attacker (or an enthusiastic operator with a long registry override
at `~/.config/az-ai/registry.json`) who registers N models all carrying
the missing capability gets a single stderr line of length
`prefix + 5 + sum(len(name)+2 for name in suggestions)`. With 200
models at 24 chars each the line crosses 5 KB, blowing through both
Bookman's S-tier brevity contract and Mickey's screen-reader budget. No
crash, no RCE -- but a one-shot UX denial-of-attention and a documented
wording contract violation. PoC in the detailed findings.

**2. F-EE-AR-01 (LOW-MEDIUM, verified) -- capability-tag comparison is
ordinal-case-sensitive but model identity is case-insensitive.**
`caps.Contains(capability, StringComparer.Ordinal)` (CapabilityGate.cs
line 113) and `e.Capabilities.Contains(tag, StringComparer.Ordinal)`
(ModelRegistry.cs line 83) both treat `tool_calls` and `Tool_Calls` as
distinct tags. Today this is defensible -- `ModelCapability.AllowedTags`
is a closed lowercase set and `ModelCard` parsing enforces it, so a
mis-cased tag fails registration at startup. But the registry-override
file at `~/.config/az-ai/registry.json` is JSON-only and the
allowed-tag validator only runs for embedded YAML cards (per ADR-012
F-EE-07 the JSON override skips card-name reconciliation; the tag-set
validator has the same hole). A hostile or typo'd override that
declares `"Capabilities": ["Tool_Calls"]` produces a model that the
gate sees as carrying *no* tags -- false-positive rejection (user
confusion) -- or, in the suggestion-list path, *misses* eligible models
(false-negative on the helpful tail). Either way: the casing rule
should be canonicalised at one boundary (registry load) and asserted at
both consumers. Defer to the S03E18-vs-S04E03 reconciliation episode if
that ships before E04.

**3. F-EE-AR-02 (LOW, verified) -- DefaultSystemPromptSentinel is a
content-equality probe, not an "is-default" oracle.** The check at
CapabilityGate.cs line 106-107 compares `opts.SystemPrompt` against the
literal default string with `StringComparison.Ordinal`. `opts.SystemPrompt`
is the *content* (not a path -- args parsing at Program.cs:1324 takes
the raw arg value), so a user who deliberately passes
`--system-prompt "You are a secure, concise CLI assistant. Keep answers
factual, no fluff."` -- byte-identical to the default -- gets a silent
pass through the gate even though they did, in fact, request the
`system_prompt` capability. Likelihood is low (the default is 77 chars
and not what a thinking user would re-type) but the failure mode is a
*silent* false-negative, which is exactly what this episode exists to
prevent. The structural fix is a boolean "user supplied --system-prompt"
flag carried in `CliOptions`, not content equality; out of scope for
this review but worth a follow-up.

### Findings table

| ID | Severity | Title | Verified | Disposition |
|----|----------|-------|----------|-------------|
| F-EE-AR-01 | LOW-MEDIUM | Capability-tag Contains is Ordinal; model identity is OrdinalIgnoreCase -- inconsistent at the override boundary | verified | E04 backlog (reconciliation episode) |
| F-EE-AR-02 | LOW | DefaultSystemPromptSentinel collision -- intentional prompt equal to default bypasses gate | verified | E04 backlog (flag bit, not content compare) |
| F-EE-AR-03 | LOW | Null/empty allowlist returns full registered set as suggestions -- intentional, but undocumented in rejection message | verified | WONT-FIX (document) |
| F-EE-AR-04 | INFO | Unregistered-model pass-through downgrades capability-mismatch errors to provider-side rc=99 | verified | WONT-FIX (documented decision, decision 1) |
| F-EE-AR-05 | INFO | First-miss-wins forces two-round-trip UX when user combines --tools and --schema on a no-cap model | verified | WONT-FIX (documented decision, decision 2) |
| F-EE-AR-06 | PASS | Persona pin re-check happens at L739; gate at L752 observes the final pinned model | verified | n/a (verified safe) |
| F-EE-AR-07 | LOW | Suggestion list leaks all allowlisted deployment names supporting the capability -- mild info disclosure | verified | E04 backlog (consider truncating to top-K) |
| F-EE-AR-08 | NIT | `--stream` mapping row hardcoded `Requested = false` placeholder; E07 wiring debt | verified | E07 owner (Maestro's note already documents) |
| F-EE-AR-09 | MEDIUM | 240-char budget enforced on prefix only; suggestion tail unbounded -- Bookman brevity contract breach with hostile override | verified | E04 backlog (Bookman + Newman) |
| F-EE-AR-10 | PASS | Capability-tag injection (C0/C1, ANSI CSI) into rejection message blocked by Scrub(); --doctor output blocked by SanitizeForTerminal | verified | n/a (verified safe) |
| F-EE-AR-11 | NIT | `ModelsWithCapability` returns registry-cased names; allowlist intersection is case-insensitive but allowlist entries with whitespace silently drop | verified | WONT-FIX (operator typo, surfaces in --doctor) |

### Detailed findings

#### F-EE-AR-09 -- Suggestion tail unbounded; 240-char brevity contract breach -- [MEDIUM] [verified]

**Surface:** `CapabilityRejection.Build` lines 95-111. The
`PrefixBudgetChars = 240` invariant is asserted at line 84 against
`prefix.Length` only; lines 105-110 append `"Try: "` and a
comma-separated list of every suggestion without re-checking the
running length.

**Attack payload (registry-override path):**

```text
# Hostile or careless ~/.config/az-ai/registry.json -- 200 model
# entries, each name 24 chars, each carrying "tool_calls":
#   { "Name": "aaaaaaaaaaaaaaaaaaaaaaaa", "Capabilities": ["tool_calls"], ... }
#   ... 199 more ...
AZUREOPENAIMODEL="<comma-joined list of all 200>"
az-ai --model some-non-cap-model --tools shell "hi"
```

**Observed behaviour:** The gate rejects (correct), then assembles a
single-line stderr message of roughly
`120 (prefix) + 5 ("Try: ") + 200*(24+2) = 5325 chars`. Single line.
ASCII-only. No newline. Snap-tier wording contract requires
`<=240` chars before the suggestion tail -- which holds -- but the
*overall* message is 22x the targeted budget, defeating the Snap-tier
brevity goal and almost certainly tripping Mickey's screen-reader and
terminal-width budgets. Reproduced by inflating
`Program.RegistryEntries` in a debug build to 200 entries; the gate's
`SuggestionsFor` returns all 200, `Build` happily concatenates.

**Expected behaviour:** Either (a) cap the suggestion list at top-K
(K=5 fits the Snap tier; ordering by registry seed position is fine),
or (b) extend the `PrefixBudgetChars` invariant to a `TotalBudgetChars`
that re-checks `sb.Length` between appends and emits an
`(... and N more; see --doctor)` tail when exceeded. Option (b) is
strictly better -- it preserves the "see --doctor" handoff Bookman
already uses for the empty-intersection case.

**Blast radius:** UX-denial / observability noise. Not RCE. Not data
disclosure beyond F-EE-AR-07's deployment-name surface (which becomes
*more* exploitable here because the upper bound is gone). Requires
write access to `~/.config/az-ai/registry.json` *or* a 200-entry seed
ship from upstream -- the second is the real risk because nothing in
the registry pipeline today rejects oversize registry files. Cross-link
to ADR-012 F-EE-06 (notes-list size cap): same shape, different field.

**Disposition:** E04 backlog. Owner: Bookman (wording contract) +
Newman (input-size cap). Not a hotfix lane.

#### F-EE-AR-01 -- Capability-tag Contains is Ordinal; identity is OrdinalIgnoreCase -- [LOW-MEDIUM] [verified]

**Surface:** `CapabilityGate.cs:113`
(`caps.Contains(capability, StringComparer.Ordinal)`) and
`ModelRegistry.cs:83`
(`e.Capabilities.Contains(tag, StringComparer.Ordinal)`).
Compare against model-identity comparisons throughout the resolution
chain (Program.cs:529, CapabilityGate.cs:85), all of which use
`OrdinalIgnoreCase`.

**Attack payload:** Drop a `~/.config/az-ai/registry.json` entry with
`"Capabilities": ["Tool_Calls"]` (note the title-case). The JSON-based
override path in `ModelRegistry` (per ADR-012 F-EE-07) does not run the
`ModelCapability.IsValid` validator that embedded YAML cards run.

**Observed behaviour:**

- Gate sees `caps = ["Tool_Calls"]`, looks for `"tool_calls"` with
  Ordinal -- no match. User passes `--tools shell` and gets rejected
  even though their intent was to declare the capability.
- Conversely, in the suggestion-tail path, a model with the correct
  `tool_calls` tag will not be suggested if another model in the
  registry uses `Tool_Calls` -- it'll be filtered out as not carrying
  the canonical tag.

**Expected behaviour:** Tag comparison should be canonicalised once at
registry load (lowercase normalise + AllowedTags validation, on both
the YAML and JSON-override paths) and asserted consistently at every
consumer. Either canonical-case + Ordinal, or accept any case +
OrdinalIgnoreCase -- but not "identity is loose, vocabulary is strict".

**Blast radius:** Operator confusion. Not a security boundary -- the
override file is already trusted (read from `~/.config/az-ai/` with
chmod 600). Pairs with ADR-012 F-EE-07 (card-name not reconciled): the
JSON-override path is the weak boundary on the registry seam.

**Disposition:** E04 backlog (ideally bundled with the S03E18 dual-
surface reconciliation episode the ADR's Negative-Consequences section
forecasts).

#### F-EE-AR-02 -- DefaultSystemPromptSentinel content-equality false-negative -- [LOW] [verified]

**Surface:** `CapabilityGate.cs:106-107`.
`!string.Equals(opts.SystemPrompt, DefaultSystemPromptSentinel, StringComparison.Ordinal)`.

**Attack payload:**

```text
az-ai --model some-no-sysprompt-model \
  --system-prompt "You are a secure, concise CLI assistant. Keep answers factual, no fluff." \
  "hello"
```

**Observed behaviour:** `opts.SystemPrompt` is set by Program.cs:1324
from the raw arg value (no file read; the flag takes content, not a
path). The Ordinal compare against the in-file `DefaultSystemPromptSentinel`
literal succeeds, the gate decides "user did not request system_prompt
capability", and the model receives a system prompt it cannot honour
(the failure surfaces as the model silently ignoring the system
message -- exactly the confused-4xx-class failure the episode exists
to prevent).

**Expected behaviour:** Wire a `bool SystemPromptExplicit` (or
equivalent) into `CliOptions` at parse time and check that bit instead
of comparing content. The current shape -- "if it happens to equal the
default we assume the user didn't ask" -- is the kind of clever oracle
that ages badly the day the default sentinel changes.

**Blast radius:** Single-shot UX confusion. Not a security finding;
no privilege boundary crossed. Likelihood is low because the default
prompt is verbose and not a string a user would re-type by accident,
but the failure mode is silent, which is the disqualifier.

**Disposition:** E04 backlog. Owner: Maestro (the sentinel was their
Wave 1 call) + Costanza (CliOptions shape).

#### F-EE-AR-06 -- Gate observes post-pin model -- [PASS] [verified]

**Surface:** Program.cs lines 720-753. Order of operations confirmed
by direct read:

1. L622-720: persona resolution may overwrite `model` with a pinned value.
2. L739-746: allowlist re-validation after the persona pin -- exits
   with `rc=1` if the pin selected a non-allowlisted model.
3. L752: `CapabilityGate.Check(model, opts, allowedModels)` -- runs
   on the final post-pin, post-allowlist-revalidation model.

**Verification:** The `model` local is the same variable threaded
through both checks; there is no reassignment between L746 and L752.
The gate sees the model the dispatcher will actually call. PASS.

**Disposition:** Closed -- no action.

#### F-EE-AR-10 -- Capability-tag terminal-control injection -- [PASS] [verified]

**Surface:** Rejection-message output (CapabilityRejection.Scrub, lines
122-138) and `--doctor` capability-row output (Program.cs:3694 calls
`SanitizeForTerminal` on each capability tag).

**Attack payload attempted:** Registry-override entry with
`"Capabilities": ["tool_calls\u001b[2J\u001b[H"]` (CSI clear-screen +
home). Reaches the gate, fails the Ordinal `Contains("tool_calls")`
check (the embedded ESC makes it a distinct string -- a side-benefit
of F-EE-AR-01's strict comparison), and if it ever did reach
`CapabilityRejection.Build` via suggestion, would be reduced to `?`
characters by `Scrub` before stderr emission. `--doctor` separately
sanitises the same field at L3694. Both output paths neutralise CSI,
OSC, BEL, and the rest of the C0/C1 menagerie. PASS.

**Disposition:** Closed -- no action. Note for future-FDR: this is the
class of bug we lost to in S03E11; the dual-scrubber pattern (output
boundary + doctor boundary) is now load-bearing and should grow a
regression test in the Wave 2 functional pass.

### Hardening recommendations (E04 or beyond, not for this episode)

- **Cap the suggestion tail in `CapabilityRejection.Build`** at a Bookman-
  chosen top-K with a `(... and N more; see --doctor)` overflow tail.
  Closes F-EE-AR-09 and bounds F-EE-AR-07's disclosure surface in one
  edit. Top-K=5 fits Snap tier; ordering by registry seed position is
  deterministic and AOT-friendly.
- **Canonicalise capability tags at registry load** -- lowercase
  normalise + `ModelCapability.IsValid` on both the embedded-card and
  JSON-override paths, with an `rc=99` exit on a non-canonical tag.
  Closes F-EE-AR-01 and folds ADR-012 F-EE-07 (card-name reconciliation)
  into the same load-boundary invariant. Reconciliation episode owner.
- **Replace the SystemPrompt sentinel with an explicit `SystemPromptExplicit`
  bit on `CliOptions`.** Closes F-EE-AR-02 and removes a load-bearing
  string literal duplication between Program.cs and CapabilityGate.cs.
  Trivial diff; the only risk is the existing `--system-prompt` parsing
  must set the bit at exactly one site.
- **Emit a `[WARNING]` when the gate is asked about an unregistered model.**
  Decision 1 (pass-through) stays the default, but a single warning
  line ("model 'foo' is not in the registry; capability checks skipped")
  closes the silent-rc=99-downstream loop that F-EE-AR-04 documents,
  without forcing strict mode on operators who haven't updated their
  override file. Cross-link to ADR-012's `[INFO] using card anchor`
  recommendation for F-EE-04 -- same operator-visibility shape.
- **Audit the registry-load size cap.** F-EE-AR-09 only matters because
  nothing today rejects a 200-entry registry override. A simple
  `entries.Length > 64` rejection at load time would defang the tail-
  inflation surface entirely and pairs naturally with ADR-012 F-EE-06.

### Closing assessment

No CRITICAL. No HIGH-verified-exploitable. The gate is structurally
sound, the rejection wording is well-scrubbed, and the insertion site
in Program.cs sees the model the dispatcher will actually call. The
one MEDIUM (F-EE-AR-09) is a wording-contract breach with a hostile
registry override, not a security boundary, and is the right shape for
an E04 follow-up bundled with the suggestion-tail truncation work.
F-EE-AR-01 and F-EE-AR-02 are both latent false-result bugs (one
false-positive, one false-negative) that pair naturally with the
S03E18-vs-S04E03 reconciliation episode forecast in the ADR's
Consequences/Negative section.

**Recommendation:** Do **not** pause the S04E03 episode close. Ship the
gate, file the four E04 follow-ups (F-EE-AR-01, F-EE-AR-02, F-EE-AR-07,
F-EE-AR-09) into the findings backlog under Newman + Bookman + Maestro
joint ownership, and let the reconciliation episode pick them up
alongside the dual-surface fold.
