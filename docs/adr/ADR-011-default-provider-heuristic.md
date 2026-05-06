# ADR-011 -- Default Provider Heuristic

- **Status**: Accepted -- 2026-05-21
- **Deciders**: Costanza (PM, lead), Larry David (showrunner sign-off), Kramer (eng review), Newman (sec review), Frank Costanza (reliability)
- **Related**:
  - [ADR-009 -- Default Model Resolution](./ADR-009-default-model-resolution.md) -- the model-side ladder this ADR mirrors on the provider side
  - [ADR-007 -- Third-Party HTTP Provider Security](./ADR-007-third-party-http-provider-security.md) -- the six guardrails every non-Azure provider inherits
  - [ADR-010 -- First Non-Azure Cloud Provider](./ADR-010-first-non-azure-cloud.md) -- introduced compat presets that this heuristic now arbitrates between
  - [FR-014 -- Local Preferences and Multi-Provider](../proposals/FR-014-local-preferences-and-multi-provider.md) -- the umbrella feature
  - [`docs/exec-reports/s03e22-the-default.md`](../exec-reports/s03e22-the-default.md) -- the episode that landed this heuristic
  - finding `costanza-2026-05-S-2` -- the unspec'd-default grievance this ADR closes

## Context

S03E20 *The Switch* codified `Preferences.Resolve()` as the one-and-only
precedence chain for provider, profile, and model. Resolve() handles the
user-driven rails (CLI flag > env > profile) cleanly, but the final rung --
the *default* provider when none of the above pin a choice -- was still
ad-hoc. The S03E20 implementation walked four signals in an undocumented
order: AZUREOPENAIENDPOINT presence, OPENAI_API_KEY presence, then a hardcoded
`{openai, groq, together, cloudflare}` list of compat-preset key envs. The
order was stable in code but never written down. Finding `costanza-2026-05-S-2`
captured the user-visible smell: a contributor who exports four credential
keys in their shell (a perfectly legal thing to do for testing) gets a
"surprising default" with no documented basis for why.

The default is a contract. It runs on every cold-start where the operator
does not pin a provider explicitly -- Espanso text expansion, AHK shortcuts,
cron jobs, the doctor command, the wizard's pre-fill. A surprising default
shipped through any of those paths is a debugging session. The default has
to be deterministic, documented, and grep-able from `--config show`.

## Decision

Replace the ad-hoc default-provider order in `PreferencesResolver` with a
six-rung deterministic heuristic. Each rung produces a distinct `Source`
label so `--config show` and audit trails can answer "why this provider?"
without re-deriving the algorithm. The heuristic is a pure function of the
env snapshot: no socket probes, no DNS, no I/O. The "local-detected" rung
(below) reads endpoint URL strings only -- the live port probe stays in
`ProviderDoctor` where it has always lived.

### The ladder

The default heuristic runs only after CLI flags, env pins (`AZ_PROVIDER`),
and profile pins have all failed. It walks the rungs in order and returns
on the first match.

1. **`default:azure`** -- `AZUREOPENAIENDPOINT` and `AZUREOPENAIAPI` are
   both set. The user has Azure credentials wired up and no other signal;
   honour the historic default.

2. **`default:<preset>`** -- exactly one `AZ_AI_<PRESET>_ENDPOINT` env is
   set (any preset name; case-insensitive on the preset). Single-provider
   intent is unambiguous. The provider is `<preset>` (lowercased).

3. **`default:<preset>:local-detected`** -- two or more
   `AZ_AI_<PRESET>_ENDPOINT` envs are set, `AZ_AI_LOCAL_PROVIDERS=1` is
   exported (strict equality), and at least one endpoint URL parses to a
   loopback host (`localhost`, `127.0.0.1`, `::1`) on a known local-runtime
   port. The first matching preset wins, in alphabetical preset order.
   Known local ports (S03E22 baseline):

   | Preset    | Port  | Runtime    |
   |-----------|-------|------------|
   | ollama    | 11434 | Ollama     |
   | llamacpp  | 8080  | llama.cpp  |
   | lmstudio  | 1234  | LM Studio  |

   Port matching is *exact* and *string-shaped* -- the URL is parsed but
   no socket is opened. ProviderDoctor still owns the live probe.

4. **`default:openai`** -- `OPENAI_API_KEY` is set and no preset endpoint
   matched. OpenAI direct is the documented "I have a key, I have no
   endpoint" path (per ADR-010).

5. **Tie-break: `default:<preset>` + warning** -- two or more preset
   endpoints are set with no other signal (rungs 1-4 all missed). Pick the
   alphabetically first preset name and emit a non-fatal warning:
   `multiple-presets-no-cli-no-profile-no-env-pin`. The warning is the
   audit trail; the alphabetical pick is the determinism.

6. **`default:azure:fallback`** -- nothing matched. Return azure as the
   provider with the `:fallback` source suffix. The dispatch path will
   fail closed at `BuildChatClient` with the existing missing-credentials
   error, which is the correct UX -- the operator sees a credentials
   prompt, not an opaque "no provider" trace.

### Source-label contract

Every rung emits a stable, ordinal-lowercase, colon-separated string. The
five distinct labels are:

- `default:azure`
- `default:<preset>`              (used by rungs 2 and 5)
- `default:<preset>:local-detected`
- `default:openai`
- `default:azure:fallback`

`--config show` prints the label verbatim under "Switch resolution". A
tie-break match additionally prints the warning under the same block.

### Why a fallback at all?

Two reasons. First: backwards compatibility. The pre-S03E22 code returned
azure-as-default when everything else failed (via the AZUREOPENAIENDPOINT
rung that did not require a key); operators with environment files that
load slowly, or with a typo in their key env, would still see "az-ai is
trying to talk to azure" rather than a confusing "no provider could be
resolved". Second: error pathway clarity. A failed BuildChatClient gives
the operator an actionable "missing AZUREOPENAIAPI" message. A thrown
InvalidOperationException from the resolver gives them "no provider" --
strictly less useful. The `:fallback` suffix is the breadcrumb that says
"we picked azure because nothing else fit, not because you wanted azure".

### What we explicitly did NOT change

- `BuildChatClient` and `OpenAiCompatAdapter` were untouched. The resolver
  produces a `(provider, model)` pair; the dispatch path consumes it. The
  capability gate (S03E18) and the credential rotator (S03E25) own those
  files and were locked under their respective episodes.
- Resolve() remains a pure function. The env snapshot is built by
  `Program.SnapshotEnv()` once per invocation; the resolver never reads
  Environment.
- ProviderDoctor (S03E15) keeps the live socket probe. ADR-011 reads URL
  strings only.

## Consequences

### Positive

- The order is **specified, not folkloric**. Reviewers, future contributors,
  and the CHANGELOG can cite ADR-011 instead of "see Preferences.cs".
- Every default outcome has a **distinct, grep-able source label**. Support
  tickets that include `--config show` output now identify the exact rung
  in one line.
- The local-runtime path is **discoverable** via `AZ_AI_LOCAL_PROVIDERS=1`
  -- explicit opt-in matches the security posture established by S03E16
  *The Allowlist* and the local-providers tutorial (S03E19).
- The tie-break warning **surfaces ambiguity instead of hiding it**. An
  operator with four preset endpoints set sees the warning the first time
  they run `--config show`; the message names the action ("pin one with
  AZ_PROVIDER, --provider, or a profile").

### Negative / cost

- Six rungs is more code than three. Cold-path cost: a handful of dictionary
  reads and one optional Uri.TryCreate -- well below the 1ms threshold the
  Costanza latency budget cares about. AOT footprint: negligible.
- Operators who relied on the **old, unspec'd order** (e.g. "az-ai picked
  groq because GROQ_API_KEY was set") may see a default change to
  `default:azure:fallback` after the upgrade. This is the entire point of
  the ADR -- the old behaviour was the bug. A user who actually wants groq
  should set `AZ_AI_GROQ_ENDPOINT` (rung 2) or `AZ_PROVIDER=groq` (env pin,
  beats the default rung). README and CHANGELOG name this migration.
- The local-port table is a **2026-05 snapshot**. Ollama / llama.cpp /
  LM Studio default ports are stable in practice but a future runtime
  bump could invalidate the entry. Keep the table small; expand it through
  ADR amendment, not silent code edits.

### Migration

- Existing users who ran on AZUREOPENAIENDPOINT alone (no key) will see
  `default:azure:fallback` now. Behaviour at dispatch time is unchanged
  (BuildChatClient still throws on missing key). The label changed; the
  outcome did not.
- Users with one of {GROQ_API_KEY, TOGETHER_API_KEY, CLOUDFLARE_API_TOKEN}
  set as their *only* signal must now also export the matching
  `AZ_AI_<PRESET>_ENDPOINT` env, or set `AZ_PROVIDER=<preset>` explicitly.
  This is the documented price of moving the heuristic from key-based
  ("which key is set?") to endpoint-based ("which provider is wired?").

## Compliance

- **Determinism check**: the heuristic is a function of the env snapshot
  alone; no clocks, no random, no I/O. `DefaultProviderHeuristicTests`
  enforces by feeding identical envs and asserting identical outcomes,
  including label and warning text.
- **Source-label uniqueness**: each rung emits a label that does not
  collide with any other rung's label. The tie-break label
  `default:<preset>` shares its shape with rung 2 by design -- the
  Warning collection is the disambiguator.
- **Backward compat**: the resolver no longer throws when nothing matches;
  it returns `default:azure:fallback`. The S03E20 test that asserted the
  throw was updated in the same commit; the rationale is in this ADR's
  "Negative / cost" section.

## Status history

- 2026-05-21 -- Accepted (S03E22 *The Default*, Costanza lead).
