# Observability (v2 Phase 5)

**Owners:** Frank Costanza (SRE / telemetry) + Morty Seinfeld (FinOps / cost schema)
**Status:** Phase 5 complete -- opt-in telemetry shipped with OTel spans + cost events.

> SERENITY NOW! -- and then, a clipboard.
> Telemetry is not a feeling. It's a number. And the number is OFF by default.

---

## The opt-in contract

**Nothing leaves the machine unless you say so.** No default-on, no phone-home, no surprise
egress. The v2 CLI emits zero telemetry on the hot path until one of the following is true:

| Trigger | What it enables | Where it goes |
|---|---|---|
| `--telemetry` (umbrella) | OTel spans + metrics + stderr cost events | OTLP endpoint + stderr |
| `AZ_TELEMETRY=1` (env) | Equivalent to `--telemetry` | OTLP endpoint + stderr |
| `--otel` | OTel spans only (tracing) | OTLP endpoint |
| `--metrics` | Meters + stderr cost events | OTLP endpoint + stderr |

Set `AZ_TELEMETRY` to `1`, `true`, or `yes` (case-insensitive) to enable via environment. Any
other value -- or an unset variable -- keeps telemetry OFF.

**Stdout is sacred.** No telemetry ever writes to stdout. `--raw` consumers (Espanso, AHK)
see exactly what they saw before -- the assistant's text and nothing else. Cost events go to
stderr. Spans and metrics go to the configured OTLP endpoint. Period.

---

## What gets emitted

### OpenTelemetry spans (`--otel` or `--telemetry`)

| Span | Kind | Tags |
|---|---|---|
| `az.chat.request` | Client | `az.model`, `az.mode=standard`, `az.raw` |
| `az.agent.request` | Client | `az.model`, `az.mode=agent`, `az.raw` |
| `az.ralph.iteration` | Internal | `ralph.iteration`, `ralph.max_iterations` |

Export target: `OTEL_EXPORTER_OTLP_ENDPOINT` (default `http://localhost:4317`).

### Metrics (`--metrics` or `--telemetry`)

| Instrument | Type | Unit | Tags |
|---|---|---|---|
| `azai.chat.duration` | Histogram | s | `model` |
| `azai.tokens.input` | Counter | tokens | `model`, `mode` |
| `azai.tokens.output` | Counter | tokens | `model`, `mode` |
| `azai.cost.usd` | Histogram | USD | `model`, `mode` |
| `azai.ralph.iterations` | Histogram | iterations | -- |
| `azai.tool.invocations` | Counter | invocations | -- |

### Stderr cost events (`--metrics` or `--telemetry`)

One JSON line per completed LLM request, written to stderr. Stable schema -- log shippers
and Morty's spreadsheet depend on this. See [`CostEvent.cs`](../azureopenai-cli-v2/Observability/CostEvent.cs).

```json
{"ts":"2026-04-20T12:34:56.789Z","kind":"cost","model":"gpt-4o-mini","input_tokens":1200,"output_tokens":340,"usd":0.000384,"mode":"standard"}
```

| Field | Type | Notes |
|---|---|---|
| `ts` | ISO-8601 UTC timestamp | round-trip format (`"O"`) |
| `kind` | string | always `"cost"` for this event |
| `model` | string | deployment name as reported to Azure OpenAI |
| `input_tokens` | int | prompt tokens consumed |
| `output_tokens` | int | completion tokens generated |
| `usd` | number \| null | `null` if the model is not in the price table -- never faked |
| `mode` | string | `standard`, `agent`, or `ralph` |

Price table lives in [`CostHook.cs`](../azureopenai-cli-v2/Observability/CostHook.cs).
Override with `AZAI_PRICE_TABLE=/path/to/prices.json` -- JSON must match
`{"model-name":{"InputPer1K":0.00015,"OutputPer1K":0.00060}}`.

---

## Disabling and auditing

- **Default state is off.** Omit all flags and leave `AZ_TELEMETRY` unset -- the ActivitySource
  and Meter have no listeners, so `StartActivity` / counter calls are no-op.
- **Audit what would be sent:** run `az-ai-v2 --telemetry --metrics "hi"` and inspect the
  single cost-event line on stderr before enabling an OTLP collector.
- **Purge:** nothing is persisted by the CLI itself. Telemetry is delivered to the OTLP
  collector (your responsibility to retain or purge) and to stderr (your terminal buffer).

---

## AOT / hot-path guarantees

- `Telemetry.IsEnabled` short-circuits all cost/metric work when off -- no JSON serialization,
  no string formatting on the `--raw` hot path.
- `CostEvent` serialization goes through `AppJsonContext` (source-gen, no reflection).
- OTel SDK is wired only when a flag is set; the `ActivitySource` itself is a no-op with
  no registered listener, which is the zero-overhead contract documented in the `Telemetry`
  class header.

---

## SLOs and error budgets

Phase 5 deliverables include the Frank Costanza reliability catalog in
[`docs/reliability.md`](reliability.md) (when it lands) -- SLOs for startup latency, chat
success rate, and cost-schema emission completeness live there.

If telemetry emission throws, the request path continues; the exception is swallowed inside
`Telemetry.RecordRequest`. **Telemetry must never break the user's request.**

---

## Related

- [v2-migration.md §Phase 5](v2-migration.md)
- [v2-cutover-checklist.md §1](v2-cutover-checklist.md)
- [cost-optimization.md §3](cost-optimization.md) -- Morty's rate card
- [`azureopenai-cli-v2/Observability/`](../azureopenai-cli-v2/Observability/) -- source
