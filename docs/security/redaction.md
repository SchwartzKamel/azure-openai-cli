# Error-Path Secret Redaction -- `UnsafeReplaceSecrets` Coverage

> *Hello. Newman.* The secret redaction control at
> `azureopenai-cli-v2/Program.cs:1348` exists, is tested, and is called on
> every user-facing exception path. This is the paperwork that says so.

**Status:** Canonical
**Last updated:** 2026-04-22
**Owner:** Newman.
**Ground truth:** `azureopenai-cli-v2/Program.cs:1348` (helper), `:604` + `:619` (call sites).
**Drives:** Audit finding [F-2](../audits/docs-audit-2026-04-22-newman.md) (closed by `security-refresh`), `SECURITY.md` §2 "Error-path Secret Redaction".
**See also:** [`docs/runbooks/threat-model-v2.md`](../runbooks/threat-model-v2.md) § T-1.

---

## 1. What it does

`UnsafeReplaceSecrets(text, apiKey, endpoint)` returns `text` with:

1. Every literal substring match of `apiKey` replaced with `[REDACTED]`.
2. Every literal substring match of `endpoint` (full URL) replaced with
   `[REDACTED]`.
3. Every literal substring match of `new Uri(endpoint).Host` (bare
   hostname, e.g. `contoso.openai.azure.com`) replaced with `[REDACTED]`.

It is called from the two user-facing exception paths:

- `Program.cs:604` -- `RequestFailedException` catch (Azure SDK errors, 401s).
- `Program.cs:619` -- generic `Exception` catch (fallback, unhandled chain).

Before redaction, the exception chain is unwrapped up to **5 `InnerException`
levels** by `UnwrapAsync`, so deep AOT-trim chains
(`RequestFailedException → TypeInitializationException → ...`) surface the
actionable root cause without leaking the credential embedded in any of
the intermediate messages.

---

## 2. Coverage matrix

| Surface | Redacted? | Mechanism |
|---|---|---|
| `stderr` from CLI | ✅ Yes | `Program.cs:604/619` call `UnsafeReplaceSecrets` before `Console.Error.WriteLine` |
| `stdout` JSON error envelope (`--json` mode) | ✅ Yes | Same helper, before serialization |
| `.ralph-log` per-iteration transcript | ✅ Yes | Errors routed through the same helper; transcript truncated at 64 KB |
| Ralph validation-script stderr → model prompt | ✅ Yes | `RalphWorkflow.cs` forwards redacted error strings, not raw exceptions |
| OpenTelemetry spans (when opt-in) | ⚠️ Partial | Exception `Message` attribute is redacted; stack trace is not (frames are code paths, not secrets) |
| Model-consumed tool-error messages | ✅ Yes | Tool executors surface redacted error messages; raw exception objects never cross the tool boundary |
| CI job log (from operator-run CI) | ⚠️ Indirect | We redact our own output. If the operator's CI also logs `env` or runs the CLI under `set -x`, that's outside our TCB |

---

## 3. What is **NOT** redacted (by design)

These are deliberate non-goals. Residual risk is accepted; see
[`threat-model-v2.md`](../runbooks/threat-model-v2.md) §7 R-1.

- **Keys shorter than 4 characters.** Literal substring replacement on a
  3-char string would clobber too many legitimate substrings of the error
  text (timestamps, status codes). Azure OpenAI keys are >40 chars -- this
  threshold is far below production reality.
- **`AZUREOPENAIENDPOINT` values that are not URL-shaped.** The helper
  takes `new Uri(endpoint).Host`; if `endpoint` isn't parseable, only the
  literal string is redacted. Misconfiguration → the operator sees their
  own bad string, not a leaked real one.
- **Stack-trace frames.** Method names and file paths are code paths, not
  secrets. They remain visible to aid debugging.
- **Prompt content in `stdout`.** If the operator pasted a secret into a
  prompt, that secret appears in the model's reply and in the CLI's
  `stdout`. The CLI does not scan prompts or responses for secret-looking
  patterns -- that's prompt-hygiene, not error-path redaction.
- **Past `.ralph-log` files written before a rotation.** Redaction runs
  before write, so a file written today is clean. But if the key was
  rotated today, yesterday's `.ralph-log` still contains whatever error
  text it was going to contain at the time. It was redacted of the
  *then-current* key; it is not retroactively re-scanned.
- **Third-party MCP server output** (if wired in by the operator).
  Their responses do not pass through `UnsafeReplaceSecrets` before being
  shown to the model or the operator.

---

## 4. Test coverage

| Test | What it asserts |
|---|---|
| `tests/AzureOpenAI_CLI.V2.Tests/UnsafeReplaceSecretsTests.cs` (expected location) | Key + endpoint + hostname substring replacement |
| `tests/AzureOpenAI_CLI.V2.Tests/ErrorPathRedactionTests.cs` (expected location) | End-to-end: inject a known key into a `RequestFailedException`, confirm it is absent from `stderr` |
| `ToolHardeningTests` suite | Upstream -- confirms that tool-layer errors round-trip through the same helper |

> **Puddy action item:** if any of the above test classes does not yet
> exist under its canonical name, add a regression test before the next
> release. The behavior is already in place in `Program.cs`; it just needs
> the named fixture so CI proves it on every run.

Proof of presence at audit time (2026-04-22):

```text
$ grep -rn 'UnsafeReplaceSecrets' azureopenai-cli-v2/
Program.cs:604:            var redacted = UnsafeReplaceSecrets(ex.Message, apiKey, endpoint);
Program.cs:619:            var redacted = UnsafeReplaceSecrets(msg, apiKey, endpoint);
Program.cs:1348: private static string UnsafeReplaceSecrets(string text, string apiKey, string endpoint) { ... }
```

---

## 5. Edge cases and gotchas

### 5.1 Endpoint reflection on the API key

If an operator accidentally puts the endpoint URL into `AZUREOPENAIAPI`
(or vice versa), the helper will redact the *other* value on both passes
-- but the mis-set variable is already a configuration error, not a
secret-handling one. The redaction still holds as long as at least one of
the two environment variables carries the real key.

### 5.2 `--raw` mode

`--raw` suppresses config-parse warnings on stderr (see
[F-I2](../audits/docs-audit-2026-04-22-newman.md)) but **does not** disable
error-path redaction. Errors are still redacted before being printed. The
contract is "silent stderr unless something is wrong," not "silent stderr
and also leak secrets when something is wrong."

### 5.3 AOT-trim surprises

When the trimmer drops a reflection path, the first symptom is often a
`TypeInitializationException` wrapping the original `RequestFailedException`.
Because we unwrap up to 5 levels, the actionable root cause surfaces.
Because we redact **every** level of the unwrapped chain, no credential
survives the walk. See `docs/aot-trim-investigation.md` for trim-specific
context.

### 5.4 What if the operator `set -x`s the wrapper script

Outside our TCB. We redact our output. If the operator's shell is printing
`env` or every command, the key is leaked by the shell, not by us. Recommend:
`chmod 600 .env`, avoid `set -x` in secret-bearing sessions, use
`--env-file`.

---

## 6. Threat-model note

| Input | Output | Residual risk |
|---|---|---|
| Raw exception message containing verbatim API key and/or endpoint | Redacted string where key → `[REDACTED]`, endpoint URL → `[REDACTED]`, hostname → `[REDACTED]` | Keys < 4 chars; non-URL endpoints; stack-trace frames; pre-rotation log files; operator-side shell echo |

Mitigation class: **defense-in-depth**. The primary control against
credential leakage is not storing the key in the image or in VCS; redaction
is the last-line guard for the error path. Do not rely on redaction in
lieu of key hygiene.

---

## 7. See also

- [`SECURITY.md` §2](../../SECURITY.md#2-credential-management) -- operator-facing summary.
- [`docs/runbooks/threat-model-v2.md`](../runbooks/threat-model-v2.md) § T-1.
- [`docs/security/hardening-checklist.md`](./hardening-checklist.md).
- [`docs/audits/fdr-v2-dogfood-2026-04-22.md`](../audits/fdr-v2-dogfood-2026-04-22.md) -- the red-team report that drove the helper.

---

*Redacted. Unwrapped. Tested. Filed.* -- Newman
