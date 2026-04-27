# Telemetry Schema Freeze -- v2.0.0

**Schema version:** `v2.0.0`
**Owner:** Frank Costanza (SRE/telemetry), with Morty Seinfeld on cost-event schema and Newman on privacy guardrails.
**Status:** **Frozen at 2.0.0**. Any change to a span name, attribute name, attribute type, meter name, cost-event field, or unit requires a formal schema bump and review -- see §7.
**Authoritative code:**

- [`azureopenai-cli/Observability/Telemetry.cs`](../../azureopenai-cli/Observability/Telemetry.cs) -- `ActivitySource`, `Meter`, flag plumbing.
- [`azureopenai-cli/Observability/CostEvent.cs`](../../azureopenai-cli/Observability/CostEvent.cs) -- stderr JSON schema.
- [`azureopenai-cli/Observability/CostHook.cs`](../../azureopenai-cli/Observability/CostHook.cs) -- pricing + cost-event emission.
- [`azureopenai-cli/Program.cs`](../../azureopenai-cli/Program.cs) (around line 494) -- `az.chat.request` span.
- [`azureopenai-cli/Ralph/RalphWorkflow.cs`](../../azureopenai-cli/Ralph/RalphWorkflow.cs) (around line 66) -- `az.ralph.iteration` span.
**Cross-links:**
- [`docs/observability.md`](../observability.md) -- user-facing telemetry guide (same schema, different audience).
- [`docs/ops/v2-sre-runbook.md`](v2-sre-runbook.md) §3 -- operator decoder ring.

> This document is the **schema contract**. `docs/observability.md` is the **user-facing tour**. They must agree. If they don't, this document wins for the schema definition and the user-facing doc gets a PR.

---

## 1. Opt-in mechanism -- the first and last rule

**Default: nothing leaves the machine.** Nothing. No spans, no metrics, no cost events, no phone-home, no "anonymous" anything. The `ActivitySource` and `Meter` are unlistened until the user opts in, so the hot path is no-op when telemetry is off.

Telemetry turns on only when exactly one of these triggers is set per invocation:

| Trigger | Form | Enables |
|---|---|---|
| `--telemetry` | CLI flag | spans + metrics + stderr cost events |
| `AZ_TELEMETRY=1` | env var (`1`, `true`, `yes`, case-insensitive) | equivalent to `--telemetry` |
| `--otel` | CLI flag | spans only |
| `--metrics` | CLI flag | meters + stderr cost events |

**There is no other toggle.** Any other env var name, any other flag spelling -- not real. If a user asks "how do I disable telemetry", the answer is "you don't have to, it's off by default". If they ask "how do I confirm nothing is leaving my machine", the answer is in §6.

### 1.1 What the flags do *not* do

- They do **not** enable an OTLP collector. Users configure their own collector address with `OTEL_EXPORTER_OTLP_ENDPOINT` (default `http://localhost:4317` -- localhost, because the CLI assumes the user is running their own collector).
- They do **not** change stdout. `--raw` consumers (Espanso, AHK) see byte-identical output whether telemetry is on or off.
- They do **not** persist anything to disk in the CLI process. Retention is the user's collector's problem.

---

## 2. Spans (OpenTelemetry `ActivitySource`)

**`ActivitySource` name:** `azureopenai-cli-v2`
**`ActivitySource` version:** set from `Telemetry.ServiceVersion` constant.

> ⚠️ **Drift observed at freeze time.** `Telemetry.ServiceVersion` in `azureopenai-cli/Observability/Telemetry.cs:31` reads `"2.0.0-alpha.1"` at the v2.0.0 release commit, while the git tag and csproj `<Version>` are `2.0.0`. This is reported as-shipped and consumers should treat a `service.version=2.0.0-alpha.1` OTel resource attribute as equivalent to v2.0.0 for this release. Tracked for fix-forward in a future patch -- no schema change, just a string correction. (Constraint: this doc does not modify code.)

### 2.1 Span catalogue (exhaustive)

| Span name | Kind | Emitter | Attributes |
|---|---|---|---|
| `az.chat.request` | Client | `Program.cs:494` (standard + agent chat path) | `az.model` (string), `az.mode` (string: `standard` \| `agent`), `az.raw` (bool) |
| `az.agent.request` | Client | agent persona path | `az.model` (string), `az.mode=agent`, `az.raw` (bool) |
| `az.ralph.iteration` | Internal | `RalphWorkflow.cs:66` | `ralph.iteration` (int), `ralph.max_iterations` (int) |

**No other spans.** If a future code path adds a `StartActivity` call, it is a schema-bump event (§7). Reviewers should grep for `ActivitySource.StartActivity` in PRs under `azureopenai-cli-v2/` and block merges that add unlisted spans without a schema bump.

### 2.2 Attribute contract

- **Names are stable.** `az.model` stays `az.model` forever in v2.x. Never rename, only add.
- **Types are stable.** `az.raw` is a bool; do not emit as string `"true"`.
- **Cardinality:** `az.model` is bounded by the Azure deployment names the user has configured. `az.mode` is a closed set of `{standard, agent, ralph}`.

### 2.3 OTel resource attributes (auto-populated by SDK)

`Telemetry.cs:178` wires `AddService(ServiceName, serviceVersion: ServiceVersion)`. Resource attributes emitted:

- `service.name = azureopenai-cli-v2`
- `service.version = 2.0.0-alpha.1` (see drift note in §2)
- Plus whatever the OTel SDK defaults add (`telemetry.sdk.*`, `host.*` -- these come from the SDK, not from this CLI).

---

## 3. Meters (OpenTelemetry `Meter`)

**`Meter` name:** `azureopenai-cli-v2`
**`Meter` version:** same as `ActivitySource` version (see drift note).

### 3.1 Instrument catalogue (exhaustive)

| Instrument | Type | Unit | Tags | Emitter |
|---|---|---|---|---|
| `azai.chat.duration` | Histogram | `s` (seconds) | `model` | `Telemetry.RecordRequest` |
| `azai.tokens.input` | Counter | `tokens` | `model`, `mode` | `Telemetry.RecordRequest` |
| `azai.tokens.output` | Counter | `tokens` | `model`, `mode` | `Telemetry.RecordRequest` |
| `azai.cost.usd` | Histogram | `USD` | `model`, `mode` | `CostHook` |
| `azai.ralph.iterations` | Histogram | `iterations` | -- | Ralph path |
| `azai.tool.invocations` | Counter | `invocations` | -- | agent tool-dispatch path |

**No other meters.** Same rule as §2.1: new instruments require a schema bump.

### 3.2 Tag values

- `model`: Azure deployment name, string. User-configured.
- `mode`: closed set `{standard, agent, ralph}`.

Do not add high-cardinality tags (`request_id`, user-supplied prompts, tenant IDs) -- they balloon aggregation costs for no operator value. See §4 for the explicit "never" list.

---

## 4. Cost event (stderr JSON, one line per completed LLM request)

Emitted when `--metrics` or `--telemetry` or `AZ_TELEMETRY=1` is set and a real LLM call completes. Goes to stderr -- **never stdout**.

### 4.1 Schema

Source: [`CostEvent.cs`](../../azureopenai-cli/Observability/CostEvent.cs), priced by [`CostHook.cs`](../../azureopenai-cli/Observability/CostHook.cs). Serialized through `AppJsonContext` source-gen -- reflection-free under AOT.

```json
{"ts":"2026-04-20T12:34:56.789Z","kind":"cost","model":"gpt-4o-mini","input_tokens":1200,"output_tokens":340,"usd":0.000384,"mode":"standard"}
```

| Field | Type | Required | Notes |
|---|---|:---:|---|
| `ts` | string (ISO-8601 UTC, `"O"` round-trip) | ✅ Yes | Lexicographically sortable. |
| `kind` | string, always `"cost"` | ✅ Yes | Reserved namespace. Future event kinds may ship; consumers must ignore unknown `kind`. |
| `model` | string | ✅ Yes | Deployment name as configured. |
| `input_tokens` | integer | ✅ Yes | Prompt tokens. |
| `output_tokens` | integer | ✅ Yes | Completion tokens. |
| `usd` | number \| **null** | ✅ Yes | `null` when the model is missing from the price table. **Never faked.** |
| `mode` | string (`standard` \| `agent` \| `ralph`) | ✅ Yes | |

### 4.2 Consumer contract

- Lines are newline-delimited JSON (NDJSON) to stderr.
- Order: emitted immediately after the LLM call returns.
- **Consumers must ignore unknown fields** added in future minor bumps (v2.x).
- **Consumers must ignore unknown `kind` values** so we can extend the namespace without breaking log shippers.
- Field removals or renames require a major schema bump (§7).

---

## 5. Never emitted (hard list)

The CLI **never** emits the following, under any flag combination, under any condition:

- ❌ API keys. `AZUREOPENAIAPI`, bearer tokens, Entra client secrets -- never tagged, never attributed, never in events, never in spans.
- ❌ Endpoint URLs in full. `AZUREOPENAIENDPOINT` is not a span attribute. If we ever wanted host-level diagnostics we would add a `net.peer.name` attribute scrubbed to hostname only -- that is a schema-bump event.
- ❌ Prompts. The user's input text is never a span attribute, metric dimension, or cost-event field.
- ❌ Completions. The model's output text is never a span attribute, metric dimension, or cost-event field.
- ❌ Tool-call arguments or results. Ralph/agent tool invocations emit a count, not a payload.
- ❌ Filesystem paths. `--file`/`--outfile` arguments are never emitted.
- ❌ Process environment. No `ProcessStartInfo.EnvironmentVariables` dumping, no `/proc/self/environ` equivalent.
- ❌ User identity. No `USER`, no `HOSTNAME`, no MAC address, no machine GUID, no stable install-level identifier. (OTel SDK *may* add `host.*` resource attributes -- see §2.3. That is a known SDK behavior and users opting in accept it.)
- ❌ Crash core dumps. Uncaught exceptions do not ship stack frames via telemetry.
- ❌ Azure region / subscription IDs. Not inferred, not derived, not emitted.
- ❌ Session IDs or conversation histories (ralph/agent).

This is Newman's list. He cares. Violating any entry here is a **hard rollback trigger** per [`v2.0.0-rollback-playbook.md`](v2.0.0-rollback-playbook.md) §0.

---

## 6. Auditing -- how a user proves the opt-in contract

A user who wants to verify exactly what would leave their machine can do so without enabling an OTLP collector:

```bash
# Dry-run: emit to stderr only, no OTLP, no network.
az-ai-v2 --metrics "hello" 2> /tmp/audit.log
cat /tmp/audit.log
# Expect: one `{"kind":"cost",...}` line, matching §4.1 schema. Nothing else.
```

To inspect spans without a real collector, point OTLP at a local dump:

```bash
OTEL_EXPORTER_OTLP_ENDPOINT=http://127.0.0.1:4317 \
  az-ai-v2 --telemetry "hello"
# Span payload arrives at whatever listener is bound to 4317. If nothing is
# bound, the SDK drops silently -- no network egress beyond loopback.
```

To confirm nothing egresses when telemetry is off:

```bash
# No flags, no env var. Run under strace or equivalent and grep connect() calls.
# Expect: only the Azure OpenAI endpoint for the actual chat request, nothing else.
az-ai-v2 "hello"
```

---

## 7. Schema bump policy

The string `"v2.0.0"` at the top of this document is load-bearing. Any of the following is a schema bump:

- Add / remove / rename a **span name**.
- Add / remove / rename an **attribute name** on an existing span.
- Change the **type** of an existing attribute.
- Add / remove / rename a **meter instrument**.
- Change an instrument's **unit** or **type** (Counter ↔ Histogram).
- Add / remove / rename a **cost-event field**.
- Change a **cost-event field's type** or serialization.
- Add a new **event `kind`** to the stderr NDJSON stream.
- Change the **opt-in mechanism** (new flag, new env var, any change to existing flags' semantics).

### 7.1 Bump rules

- **Additive, backward-compatible changes** (new optional attribute, new optional cost-event field consumers can ignore, new `kind` consumers are told to ignore unknowns): **minor bump** to `v2.1.0`-level schema. Documented in a new schema-freeze doc `telemetry-schema-v2.1.0.md`, linked from this one.
- **Breaking changes** (rename, remove, type change, opt-in-mechanism change): **major bump** to `v3.0.0`-level schema. Requires a deprecation period with both old and new emitted in parallel for at least one minor release of the CLI.

### 7.2 Review checklist for a schema bump PR

- [ ] New schema-freeze doc written under `docs/ops/telemetry-schema-vN.N.N.md`.
- [ ] `docs/observability.md` updated.
- [ ] `docs/ops/v2-sre-runbook.md` §3 updated.
- [ ] Newman signs off on the §5 "never" list remaining intact.
- [ ] Morty signs off on cost-event field changes.
- [ ] Frank signs off on span/meter changes and the opt-in contract.
- [ ] CHANGELOG entry under `### Telemetry` with the schema version.
- [ ] At least one downstream consumer (if any known) is pinged on the PR.

### 7.3 Non-bump changes (do not require this process)

- Bug fixes that bring behavior in line with this doc (e.g., correcting `ServiceVersion` from `2.0.0-alpha.1` → `2.0.0`).
- Documentation clarifications.
- Implementation refactors that preserve all emitted names, types, and units.

---

## 8. Emission guarantees (non-schema, for completeness)

These are behavioral guarantees the runtime makes. Not schema, but tightly related:

- **Zero-overhead when off.** No listeners on the `ActivitySource`/`Meter` means `StartActivity`/counter calls are no-op, no JSON serialization, no string formatting on the `--raw` hot path.
- **Telemetry must never break the user's request.** `Telemetry.RecordRequest` swallows its own exceptions. If export fails, the user's CLI invocation still succeeds.
- **Stdout is sacred.** No telemetry ever writes to stdout. Cost events → stderr. Spans / metrics → OTLP only.
- **No background threads outside the OTel SDK's own exporter threads.** The CLI does not spin up its own telemetry threads.

---

**SERENITY NOW. Opt-in is a promise. The schema is a contract. The "never" list is the constitution.**
