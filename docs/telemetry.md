# Telemetry posture

**Owner:** Frank Costanza (SRE / observability / incident response)
**Status:** Opt-in only. Off by default. Zero default egress.

> SERENITY NOW! The default is silence. Anything else is a feature flag
> the user flipped on, on purpose, in their own shell, on their own
> machine.

---

## TL;DR

The CLI ships **zero default telemetry**. Nothing is collected, nothing
is transmitted, nothing is written to disk for analytics purposes when
you run the binary out of the box. There is no "anonymous usage ping,"
no first-run survey, no crash uploader, no error reporter. The process
talks to exactly one network endpoint: the Azure OpenAI deployment URL
the user configured.

The v2 binary (`azureopenai-cli-v2`) ships an **opt-in OpenTelemetry
pipeline** for users who *want* observability on their own
infrastructure. It is gated behind explicit CLI flags and an environment
variable. None of those flags are set by default. The schema, fields,
and destinations are documented in
[`docs/observability.md`](observability.md); this page covers the
*posture* and how to verify it for yourself.

---

## What is collected by default

Nothing.

The default code path emits:

- Stdout: the assistant's text (or `--raw` payload).
- Stderr: human-readable status lines (`[INFO]`, `[ERROR]`), spinner
  glyphs, and the `[ERROR]`-prefixed exit messages from
  `ErrorAndExit()`.
- Disk: only what the user explicitly asks for via `--output`,
  `~/.azureopenai-cli.json` (config, written by the first-run wizard),
  and the `.squad/` persona memory directory when squad mode is in use.
- Network: HTTPS to the configured `AZUREOPENAIENDPOINT` only.

There is no second destination. There is no aggregation server, no
"opt out" flag because there is nothing to opt out of.

> **Costanza notes:** The cost of zero default telemetry is that we
> hear about regressions only when users file issues. We trade
> automated signal for total user trust, and on a CLI that handles
> prompts, clipboards, and API keys for AHK/Espanso pipelines, that
> trade is the right one. The `--telemetry` opt-in is the escape hatch
> for users running this in environments where they own the collector.

---

## What can be enabled (and where it goes)

The v2 binary exposes an opt-in observability pipeline. **All of these
are off unless the user explicitly turns them on.**

| Trigger                | Enables                         | Destination                                |
|------------------------|---------------------------------|--------------------------------------------|
| `--telemetry`          | OTel spans + metrics + cost JSON | OTLP endpoint + stderr                     |
| `AZ_TELEMETRY=1` (env) | Equivalent to `--telemetry`     | OTLP endpoint + stderr                     |
| `--otel`               | OTel spans only                 | OTLP endpoint                              |
| `--metrics`            | Meters + stderr cost events     | OTLP endpoint + stderr                     |

The OTLP endpoint is **user-controlled** via
`OTEL_EXPORTER_OTLP_ENDPOINT` (default `http://localhost:4317`). The
project itself operates no collector and receives no data. If the env
var is unset, the OTLP SDK pipeline is not even constructed (lazy-init
gate); the cost event JSON still goes to the user's own stderr.

### What the opt-in pipeline collects

When (and only when) the user enables one of the flags above:

- **Span tags:** `az.model` (deployment name), `az.mode`
  (`standard`/`agent`/`ralph`), `az.raw` (boolean), `ralph.iteration`,
  `ralph.max_iterations`.
- **Metric tags:** `model`, `mode`.
- **Counters / histograms:** input tokens, output tokens, chat
  duration (seconds), USD cost estimate, ralph iteration count, tool
  invocation count.
- **Stderr cost event JSON:** timestamp, model, input/output tokens,
  USD estimate, mode.

### What is NEVER collected, even when telemetry is on

- Prompt text or assistant response text.
- The endpoint URL, key, or any fingerprint of the key.
- File paths read by `read_file`.
- URLs fetched by `web_fetch`.
- Clipboard contents.
- Shell commands run by `shell_exec`.
- Environment variables (other than the explicit OTel env vars the
  user set).
- Hostname, username, IP address, or machine identifiers.

The schema is enumerated in
[`docs/observability.md`](observability.md) and pinned by the v2 test
suite (`tests/AzureOpenAI_CLI.V2.Tests/ObservabilityTests.cs`,
`TelemetryLazyInitTests.cs`).

---

## Verifying for yourself

Don't take this doc's word for it. Audit the tree:

```bash
# 1. No analytics SDKs anywhere in production code.
grep -rniE 'posthog|mixpanel|segment\.io|appinsights|app-insights|sentry|amplitude|datadog|newrelic|appcenter' \
  azureopenai-cli/ azureopenai-cli-v2/ --include='*.cs' --include='*.csproj'
```

```bash
# 2. The only telemetry surface is the opt-in OpenTelemetry pipeline in v2.
grep -rniE 'telemetry|opentelemetry' azureopenai-cli-v2/ --include='*.cs'
```

```bash
# 3. Telemetry is off unless explicitly initialized. The default code
#    path never calls Telemetry.Initialize() with a truthy argument.
grep -rn 'Telemetry.Initialize' azureopenai-cli-v2/ --include='*.cs'
```

```bash
# 4. Confirm zero default network egress beyond AZUREOPENAIENDPOINT.
#    Look for hardcoded URLs in production code.
grep -rnE 'https?://' azureopenai-cli/ azureopenai-cli-v2/ \
  --include='*.cs' | grep -vE '(example\.|localhost|127\.0\.0\.1|schemas\.|w3\.org|opentelemetry\.io|docs|//\s)'
```

```bash
# 5. Confirm no PII leaks in the cost-event schema.
cat azureopenai-cli-v2/Observability/CostEvent.cs
```

If any of those greps surface a third-party analytics endpoint or a
prompt-text field on a telemetry record, **that is a bug**. File it and
ping Frank Costanza or Newman.

---

## Disabling the opt-in pipeline

Already off by default. To make sure it stays off in a shared shell:

```bash
unset AZ_TELEMETRY
unset OTEL_EXPORTER_OTLP_ENDPOINT
```

Do not pass `--telemetry`, `--otel`, or `--metrics`. There is no global
config switch for these; they are per-invocation flags.

---

## See also

- [`docs/observability.md`](observability.md) -- full schema, CLI flag
  reference, OTLP exporter behaviour, and the lazy-init gate.
- [`SECURITY.md`](../SECURITY.md) -- threat model and reporting path.
- [`NOTICE`](../NOTICE) -- third-party attribution.
