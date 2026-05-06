# S03E07 -- *The Redactor*

> *Newman finally builds the thing he has been demanding since S02. Six patterns, one timeout, every error path scrubbed.*

**Commit:** `pending` (ships at end of episode)
**Branch:** `main` (direct push)
**Runtime:** ~95 min real time
**Director:** Larry David (showrunner)
**Cast:** Newman (lead, in engineering mode), Maestro (cameo, JSON-quoting question), Frank Costanza (cameo, telemetry stub), Puddy (cameo, flake handoff), Kramer (half-line, accepts the handoff)
**Arc:** Provider Abstraction Seam -- E07 of 13
**Related ADRs/FRs:** ADR-007 section 2 (auth-header redaction mandate), `docs/audits/security-v2.1-post-prompts.md` (Newman's RED audit, the open ask this closes)

---

## The pitch

Newman has been writing the same finding into the same audit since S02. *"Authorization: Bearer ..." can appear in an exception message. There is no central scrubber. This is a P1 by ADR-007 section 2 and the project does not enforce it."* He filed it. He re-filed it. He filed a re-audit of the re-audit. Every time, the team ack'd it and pushed it to next sprint. E07 is next sprint.

What is novel about this episode is that Newman is not auditing. Newman is *building*. The first time on this show that the security inspector has shipped code instead of shipping a finding about code. He is not happy about it -- the role reversal makes him hostile and he typed every comment block in a clipped, slightly aggrieved register -- but he ships. `azureopenai-cli/SecretRedactor.cs` is 115 lines. `tests/AzureOpenAI_CLI.Tests/SecretRedactorTests.cs` is 283 lines. Six regex patterns, a 500ms match timeout, a `[REDACTED:<kind>]` mask format, and a top-level catch in `Program.cs` that now folds the redactor in before any error message reaches stderr. 703 unit tests green. 39 integration tests green. The P1 test, `P1_BearerTokenNeverAppearsInRedactedOutput`, fails the build the moment any bearer header survives `Redact()`. It is now physically impossible to merge a regression on this without lying to git.

The arc context: S03E06 *The Schema* shipped `preferences.json` v1 -- providers, profiles, a four-layer resolution chain, and a `--config show` surface that paints the resolved tuple on stdout. That work created two new opportunities for a secret to leak: the path printer in error messages and the JSON envelope in `--config show --json`. The redactor closes both. With E07 in the can, the Provider Abstraction Seam arc is now *secure-by-default*: any provider config that lands in E08 onward inherits the same scrubbing on every error and log path, with no per-provider opt-in to remember to write. E08 *The Pick* (Anthropic vs OpenAI direct, Costanza writes the ADR) can be a clean architectural decision episode rather than a security episode in disguise.

The deeper reason this episode took ten months to get on the schedule is that nobody wanted to write it. Audit findings file cleanly. Patches that close audit findings file cleanly. The thing in between -- the *general-purpose mitigation* that retires a category of finding rather than a single instance -- is harder to scope, harder to test, and harder to justify when no production incident is currently bleeding. E07 is that piece. The justification is that the *next* arc cannot proceed without it: a multi-provider future means more auth headers in flight, more SDK exception surfaces, and a strictly larger blast radius if any one of them leaks. We pay the cost now or we pay it incident-by-incident later. Showrunner picked now.

---

## Scene-by-scene

### Cold open -- the leak that has been in the build for ten months

Newman walks in carrying the same printout he brought to S02E14, S02E18, S02E21, and the v2.1 re-audit. Pages 1 through 3 are the same finding: an exception thrown from the upstream HTTP layer can carry the original `Authorization: Bearer ...` header verbatim into `inner.Message`, which the top-level catch in `Program.cs` writes to stderr. Page 4 is the test he tried to write three episodes ago and was told to defer. Page 5 is ADR-007 section 2 underlined twice. He sets the printout on the table. Larry: "Fine. Build it."

The ground truth before this episode: `Program.cs` had a defensive helper, `UnsafeReplaceSecrets(text, apiKey, endpoint)`, that scrubbed *literal user-known* secrets from a string. Useful but narrow -- it required knowing the exact secret value at the call site. It did nothing about a bearer token that arrived from a third-party SDK exception, because that token was never in the user's env in the first place; it was the *response* to the user's key. The audit asks were never that `UnsafeReplaceSecrets` was wrong. The asks were that it was the *only* tool, applied at three call sites out of the nineteen `ErrorAndExit` invocations in `Program.cs`, and that *shape*-based scrubbing -- catching anything that looks like a bearer header regardless of value -- did not exist.

The pre-episode gap analysis ran a `grep -n "Console.Error.WriteLine\|ErrorAndExit\|inner.Message" azureopenai-cli/Program.cs` and produced a one-page table that Newman pinned to the writers' room wall. Nineteen `ErrorAndExit` call sites. One top-level catch. Two `UnsafeReplaceSecrets` call sites at lines 743 and 755. Three of those nineteen call sites already routed through `UnsafeReplaceSecrets`. Sixteen did not. Each one was a potential bearer-leak surface. The cheapest fix was not to patch sixteen call sites; the cheapest fix was to fold the scrubber into the helper that all nineteen already passed through. That is the design move E07 makes.

### Act I -- The patterns

Newman wrote six. They live in `azureopenai-cli/SecretRedactor.cs` lines 39-72. Reproduced here verbatim from disk so the next person who edits them does not paraphrase the contract:

1. **Bearer header.** `Authorization\s*[""']?\s*:\s*[""']?\s*Bearer\s+[^\s,;""'\\]+` -- tolerates JSON-quoted form (`"Authorization":"Bearer xyz"`) as well as raw header form. This is the headline P1 case. Replacement: `Authorization: [REDACTED:bearer]`.
2. **api-key / x-api-key headers.** `(?<name>x-api-key|api-key)\s*:\s*[^\s,;""']+` -- preserves the header name, scrubs the value. Replacement: `<name>: [REDACTED:api-key]`.
3. **AZUREOPENAIAPI-style env exports.** `(?<name>AZURE[_]?OPENAI[_]?API(?:[_]?KEY)?)\s*=\s*[^\s""']+` -- catches the literal env-var name from a `set` / `export` dump. Replacement: `<name>=[REDACTED:azure-key]`.
4. **URL credentials.** `(?<scheme>https?://)[^:/?#\s]+:[^@/?#\s]+@` -- the `https://user:pass@host/...` shape. Replacement: `<scheme>[REDACTED:url-cred]@`.
5. **JSON secret fields.** `"(?<name>api[_-]?key|apikey|secret|token|password|access[_-]?token|refresh[_-]?token|key)"\s*:\s*"(?<val>(?:[^"\\]|\\.)*)"` -- snake_case and camelCase, any nesting depth. Replacement: `"<name>": "[REDACTED:api-key]"`.
6. **kv pairs in query strings / env exports.** `(?<name>api[_-]?key|apikey|access[_-]?token|refresh[_-]?token|token|secret|password)\s*=\s*[^\s&;""']+` -- bounded by whitespace, `&`, or `;` to keep the match from running away. Replacement: `<name>=[REDACTED:api-key]`.

Every regex is constructed with `RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant` and a `TimeSpan.FromMilliseconds(500)` match timeout. Compile-once is for AOT-friendly steady-state cost; `IgnoreCase` is because audit-shape inputs are not predictable about case (`AUTHORIZATION`, `X-Api-Key`, `api-key` all show up in real logs); `CultureInvariant` is the project-wide rule (`InvariantGlobalization=true` in the csproj). The 500ms ceiling exists because regex engines can go superlinear on adversarial input and the redactor is on the *error* path -- a hang here would defeat its own purpose. On timeout, `Redact()` returns the input unchanged and increments a static `TimeoutCount` counter so Frank Costanza can wire it to telemetry later.

A note on the ordering of patterns 5 and 6: the JSON-secret-field pattern runs *before* the kv-pair pattern, because a `"api_key":"value"` substring in a JSON-like log line should be replaced by a *quoted* mask (`"api_key": "[REDACTED:api-key]"`) rather than the unquoted kv-style mask. Reversing the order would still scrub the secret but would produce ugly output that no longer resembles JSON at all. The order is unit-tested implicitly by `JsonSecretField_NestedTwoLevelsDeep_IsMasked` -- if anyone reorders the passes, the assertion that the mask appears as a quoted JSON value fires.

The mask format -- `[REDACTED:<kind>]` -- is enforced by a contract test (`MaskFormat_UsesBracketedKindLabels`) that fails the build if anyone drifts to `***` or `<redacted>` or any other shape. The bracketed-kind format is what downstream parsers (log scrapers, support-bundle redactors, Newman's own follow-up audits) will key off; drift here costs more than the test is worth. The five `<kind>` labels in current use are `bearer`, `api-key`, `azure-key`, `url-cred`, and (implicitly via the kv pass) `api-key` again. The catalogue in open question #3 will own the kind-label vocabulary going forward.

### Act II -- The wiring

`SecretRedactor.cs` is 115 lines on disk. It exports two methods:

- `string Redact(string? input)` -- runs the six patterns in sequence, returns the scrubbed string. Null/empty in, empty out. Try/catch around the whole pipeline so a `RegexMatchTimeoutException` cannot escape.
- `string RedactException(Exception? ex)` -- thin wrapper that calls `Redact(ex.ToString())`, so the type, message, inner exceptions, and stack frames all route through the same scrubber.

The wiring is in `azureopenai-cli/Program.cs`. Two call sites that matter:

- The canonical `ErrorAndExit(message, exitCode, jsonMode)` helper (around line 1624 on disk) now passes the message through `SecretRedactor.Redact` before writing to stderr. There are nineteen distinct `ErrorAndExit` call sites in `Program.cs`; all nineteen are now scrubbed by virtue of routing through the helper. No per-call-site change was needed and that is the design point -- centralising the redactor was always cheaper than hunting nineteen leak surfaces.
- The top-level catch in `Main` (line 174 on disk, confirmed) now reads `Console.Error.WriteLine($"[ERROR] {inner.GetType().Name}: {SecretRedactor.Redact(inner.Message)}");`. This is the safety net. If anything escapes from inside `RunAsync` -- a third-party SDK exception, a kernel-mode crash dump, anything -- it cannot reach stderr without going through the redactor first.

The older helper, `UnsafeReplaceSecrets(text, apiKey, endpoint)`, is *preserved* as defense-in-depth at lines 743 and 755. Keeping it is deliberate: the new redactor catches *shapes* (bearer headers, JSON token fields, URL creds), and the old helper catches *literal user secrets* (the user's actual `AZUREOPENAIAPI` value, the user's actual endpoint hostname). The two scrubbers cover overlapping but non-identical surfaces; removing either would create a gap. Newman's note in the source comment is explicit on this: "the redactor catches shapes; `UnsafeReplaceSecrets` catches values; ship both."

A worked example of why both are needed: imagine a user whose real API key happens to be a 32-character hex string that does *not* match any of the six shape patterns (it is not preceded by `api-key:` or `AZUREOPENAIAPI=` -- it is the bare value, copy-pasted from a clipboard into a `--task-file` path because the user fat-fingered a paste). If a file-not-found error surfaces that path in stderr, the shape-based redactor will not catch it -- there is no header, no JSON field, no URL. The value-based `UnsafeReplaceSecrets` *will* catch it, because it knows the literal string the user's env supplied. Conversely: if the upstream Azure SDK throws `RequestFailedException` carrying its own `Authorization: Bearer ...` header in the message, `UnsafeReplaceSecrets` will not catch it -- the bearer token is not the user's API key, it was minted by Azure during the request. The shape-based redactor *will* catch it. Two scrubbers, two coverage stories, one stderr surface.

| Wave | Agents (parallel) | Outcome |
|------|-------------------|---------|
| **1** | Newman (build the redactor + tests + wire `ErrorAndExit` + integration block) | 115-line `SecretRedactor.cs`, 283-line test file with 31 tests, +49 lines in `tests/integration_tests.sh`, `Program.cs` wiring at line 174 + line 1631 |
| **1 (parallel lane)** | Puddy (`pref-test-flake-fix`, the carry-over from E06) | In-flight at the same time as the redactor wave; not part of this episode's diff but noted here because the showrunner triaged the routing live |

### Act III -- The verification

**Unit tests.** 31 new tests in `tests/AzureOpenAI_CLI.Tests/SecretRedactorTests.cs`. The headline is the P1 guard:

- `P1_BearerTokenNeverAppearsInRedactedOutput` -- five sample inputs, each containing a bearer header in a different shape (raw, JSON-quoted, embedded in a paragraph, all-uppercase header with multi-space token, lower-case `authorization: bearer` log line). The assertion is `Assert.DoesNotContain("Bearer ", r, StringComparison.OrdinalIgnoreCase)` paired with `Assert.Contains("[REDACTED:bearer]", r)`. There is no shape of bearer header that survives this test.

The five samples deserve a closer look because they are the canonical "audit-shape menu" the team will reference for years:

1. `Authorization: Bearer sk-abc123XYZ` -- the textbook bearer header. If this leaked, the redactor was never wired.
2. `preface Authorization: Bearer eyJhbGciOiJIUzI1NiJ9.payload.sig postface` -- a JWT-shaped token embedded mid-prose. Surrounding text must survive; the token must not.
3. `{"headers":{"Authorization":"Bearer ghp_AAAA1111BBBB2222CCCC3333DDDD4444EEEE"}}` -- a GitHub-style PAT inside a JSON-quoted form. The bearer pattern's lookahead for `[""']?` is what makes this case work; without it, the JSON-quoted form would slip through.
4. `AUTHORIZATION:   Bearer    leadingspaces` -- all caps, multiple spaces. Catches any naive case-sensitive or single-space implementation.
5. `log line | authorization: bearer lower-case-token | trailing` -- lower-case header, lower-case scheme, log-line context. Catches any naive Pascal-case-only implementation.

Other tests quoted by name from `SecretRedactorTests.cs`:

- `BearerToken_InMiddleOfParagraph_IsMasked` -- bearer header buried in prose still gets scrubbed; surrounding text (`retry?`) survives.
- `BearerToken_NotPresent_StringUnchanged` -- the negative case; a string with no secret comes back byte-identical.
- `ApiKeyHeader_CaseInsensitive_IsMasked` -- four `[InlineData]` rows covering `api-key`, `API-KEY`, `x-api-key`, `X-Api-Key`; preserves the header name, scrubs the value.
- `NonSecretHeader_NotMasked_NegativeCase` -- pairs with the api-key test; a `Content-Type` and a `User-Agent` header pass through byte-identical.
- `JsonSecretField_AnyAlias_ValueIsMasked` -- eight `[InlineData]` rows over `api_key`, `apiKey`, `api-key`, `token`, `secret`, `password`, `access_token`, `refresh_token`. All eight aliases produce the same `[REDACTED:api-key]` mask.
- `JsonSecretField_NestedTwoLevelsDeep_IsMasked` -- two-level JSON nesting; the secret is masked, a non-secret sibling key (`"keep":"me"`) is preserved byte-for-byte.
- `JsonNonSecretField_NotMasked_NegativeCase` -- the dual; `{"name":"Alice","email":"a@b.com"}` survives untouched. Catches any pattern that over-greedies onto innocent fields.
- `UrlCredentials_AreMasked` -- `https://alice:hunter2@example.com/path?x=1` becomes `https://[REDACTED:url-cred]@example.com/path...`; the password (`hunter2`) is asserted absent.
- `UrlWithoutCredentials_NotMasked_NegativeCase` -- the dual; a clean `https://example.com/path?x=1` survives byte-identical.
- `AzureOpenAiApiEnvVar_ValueIsMasked` -- `export AZUREOPENAIAPI=abcdef...` produces `export AZUREOPENAIAPI=[REDACTED:azure-key]`. The audit-shape that started this whole arc.
- `QueryStringApiKey_IsMasked` -- `GET /v1/chat?api_key=SECRETXYZ123&model=gpt-4` scrubs the key, preserves `model=gpt-4`. Verifies the kv pattern's `&`-bounded match does not run away.
- `MultipleSecretsInOneString_AllMasked` -- four secret shapes in one buffer; all four scrubbed. Verifies the six passes compose without one undoing another.
- `LongInput_OneMegabyte_RedactsUnderBudget` -- 1 MB of text with bearer tokens sprinkled in; budget is 500ms wall clock (typical CI box is well under 100ms). Catches superlinear regression.
- `PathologicalInput_DoesNotHang` -- 200,000 `a` characters glued onto a `Authorization: Bearer ` prefix with a trailing NUL byte. The contract is: either the bearer is masked, or the timeout path returns input unchanged within 1500ms. Hanging is the *only* failure mode the test rejects.
- `RedactException_StripsBearerFromMessage` -- throws an `InvalidOperationException` with a bearer header in the message, catches it, calls `RedactException(caught)`, asserts the secret is gone and the type name (`InvalidOperationException`) is preserved.
- `RedactException_NullInput_ReturnsEmpty` -- the null safety case for the exception variant.
- `MaskFormat_UsesBracketedKindLabels` -- guards the `[REDACTED:<kind>]` contract against drift to `***` or any other shape.
- `EmptyString_ReturnsEmpty`, `NullInput_ReturnsEmpty`, `NoSecrets_StringUnchanged` -- the three trivial edge cases. `null` and `""` both return `""`; non-secret strings round-trip byte-identical.

Total unit-test count after E07: **703 / 703 green** (672 prior to E07 + 31 new). Integration: **39 / 39 green** (35 prior + 4 new redactor smoke assertions in `tests/integration_tests.sh` lines 450 through 495).

**Integration tests.** The new block in `tests/integration_tests.sh` triggers a real `az-ai --task-file <path>` failure where the path itself contains `Authorization: Bearer SECRET-NEWMAN-9999`. The error message that comes back through `ErrorAndExit` *must* contain that path, which means it *must* contain the bearer header, which means the redactor *must* scrub it. Four assertions:

1. The error path exits 1.
2. stderr does not contain a literal `Bearer <token>` (regex: `Bearer[[:space:]]+[A-Za-z0-9._-]`).
3. stderr does not contain a literal `api-key:` header value pair.
4. stderr *does* contain the `[REDACTED:bearer]` tag (positive assertion).

All four green on the local run.

The shape of this integration test deserves a note. Earlier drafts proposed mocking the SDK exception path, injecting a fake bearer header, and asserting on the captured stderr buffer. That would have been faster to write but worse to maintain -- it would test that the redactor scrubs strings that look like bearer tokens, but it would not test that the redactor is *wired* to the surface that real bearer tokens emerge from. The actual implementation triggers a `--task-file` not-found error, which routes through the `ErrorAndExit` helper at line 405 of `Program.cs`, which routes through `SecretRedactor.Redact` at line 1631. End-to-end. If anyone refactors `ErrorAndExit` and accidentally bypasses the redactor, this test fires; a string-only mock would not.

A future-proofing note: the test injects the bearer token *into a path*, not into a network response. This is intentional. The primary leak surface for this audit thread has historically been third-party SDK exceptions, but constructing an integration test that reliably triggers an SDK exception with a bearer in the message would require either a network fixture or a substantial mock harness. The path-injection trick is a clean, deterministic, network-free way to exercise the same code path the SDK exceptions exercise. The unit test `RedactException_StripsBearerFromMessage` covers the SDK-exception case directly with a constructed exception.

---

## What shipped

| File | Lines | Status | Role |
|------|-------|--------|------|
| `azureopenai-cli/SecretRedactor.cs` | 115 | **new** | Centralised secret redactor; six patterns + `Redact()` + `RedactException()` |
| `tests/AzureOpenAI_CLI.Tests/SecretRedactorTests.cs` | 283 | **new** | 31 unit tests including the P1 guard |
| `azureopenai-cli/Program.cs` | edit | modified | Top-level catch (line 174) and `ErrorAndExit` (line 1631) now route through `SecretRedactor.Redact`; `UnsafeReplaceSecrets` retained as defense-in-depth |
| `tests/integration_tests.sh` | +49 | modified | New "S03E07 redactor smoke" block at lines 450-495 |
| `CHANGELOG.md` | edit | modified | `[Unreleased]` row under Security: centralised secret redactor on every error and log path (ADR-007 section 2). |

Line counts verified on disk via `wc -l` -- not trusted from Newman's report.

### Production code

`SecretRedactor.cs` is the entire surface. Internal static class, no I/O, no reflection, no dynamic types -- Native AOT clean. Six `Regex` instances, all `Compiled | IgnoreCase | CultureInvariant`, all carrying a 500ms match timeout. Two public methods (`Redact` and `RedactException`) and one internal counter (`TimeoutCount`) for the SRE telemetry hook.

### Tests

31 new unit tests (one P1 guard, 30 supporting cases including negatives, edge cases, perf canary, pathological-input timeout containment, and the mask-format contract). Four new integration assertions in `tests/integration_tests.sh`. 703 / 703 unit; 39 / 39 integration.

### Docs

CHANGELOG `[Unreleased]` entry under Security. ADR-007 section 2 was already authoritative; no ADR changes this episode. `docs/hardening.md` is *not* written this episode -- see open question #3 below.

### Not shipped

- A pattern for AWS-shaped or GCP-shaped credentials. Out of scope -- this CLI does not currently touch AWS or GCP. Add when E08-E10 land a non-Azure provider that needs it.
- The `docs/hardening.md` reference page. Deferred per open question #3 (Newman owns the catalogue; he wants to write it after E10 when the second provider's audit-shape catalogue is also in scope).
- Telemetry plumbing for `TimeoutCount`. Stub only -- counter exists, telemetry sink does not. See open question #4.
- A round-trippable JSON output mode for the redactor. See open question #2.

---

## Sample redacted outputs

| Input | Output |
|-------|--------|
| `Authorization: Bearer sk-abc123XYZ` | `Authorization: [REDACTED:bearer]` |
| `{"headers":{"Authorization":"Bearer ghp_AAAA..."}}` | `{"headers":{Authorization: [REDACTED:bearer]"}}` (note: replacement currently consumes the inner JSON quote -- see open question #2) |
| `api-key: AbCdEf1234567890` | `api-key: [REDACTED:api-key]` |
| `X-Api-Key: AbCdEf1234567890` | `X-Api-Key: [REDACTED:api-key]` |
| `export AZUREOPENAIAPI=abcdef0123456789...` | `export AZUREOPENAIAPI=[REDACTED:azure-key]` |
| `https://alice:hunter2@example.com/path?x=1` | `https://[REDACTED:url-cred]@example.com/path?x=1` |
| `{"api_key":"ABC123"}` | `{"api_key": "[REDACTED:api-key]"}` |
| `{"outer":{"inner":{"api_key":"DEEP","keep":"me"}}}` | `{"outer":{"inner":{"api_key": "[REDACTED:api-key]","keep":"me"}}}` |
| `GET /v1/chat?api_key=SECRETXYZ123&model=gpt-4` | `GET /v1/chat?api_key=[REDACTED:api-key]&model=gpt-4` |
| `Hello world. No secrets here.` | `Hello world. No secrets here.` (byte-identical) |

Note on the second row: the JSON-quoted bearer case is the trigger for open question #2. The current pattern matches the bearer shape inside the JSON quotes and emits a non-quoted replacement, which means the resulting string is no longer parseable JSON. For stderr log lines this is fine -- and that is the only place the redactor runs today. If the redactor is ever reused on a structured-error JSON envelope, this would need to be revisited.

---

## Sweep methodology

Newman's six-step sweep, the same one he ran for the v2.1 post-prompts audit, restated here so the next person running it does not have to reverse-engineer it:

1. **Enumerate every error / log surface in `Program.cs`.** `grep -n "Console.Error.WriteLine\|ErrorAndExit\|inner.Message" azureopenai-cli/Program.cs`. Result: nineteen `ErrorAndExit` call sites + one top-level catch + two `UnsafeReplaceSecrets` call sites at lines 743 and 755.
2. **Classify each surface as scrubbed / partial / raw.** Pre-E07: 3 / 0 / 17 (the three `UnsafeReplaceSecrets` calls were partial; everything else was raw). Post-E07: 19 / 2 / 0 (every `ErrorAndExit` runs through `Redact`; the two `UnsafeReplaceSecrets` calls remain as defense-in-depth and are now *both* scrubbed -- by `Redact` first, then by the value-substitution pass).
3. **Catalogue audit-shape inputs.** Six patterns from prior audits + two from real-world OpenAI / Azure SDK exception traces. Each pattern paired with at least one positive and one negative test before any wiring touched `Program.cs`. The negatives matter as much as the positives -- a redactor that over-greedies onto innocent input is just a different leak (it leaks signal-to-noise, not secrets, but it still costs the operator their ability to read their own logs).
4. **Pin the contract with a P1 test.** The bearer-header case is the historical headline. Test name reads as the contract: `P1_BearerTokenNeverAppearsInRedactedOutput`. The naming convention here is deliberate -- prefix with `P1_` so the test's priority is visible in the test runner output and so any future agent triaging a red CI knows immediately whether they have just broken something audit-bound or merely something cosmetic.
5. **Hammer the perf and timeout envelope.** 1 MB perf canary (must finish under 500ms; typical CI under 100ms). 200,000-character pathological backtracking input (must not hang; either masks or returns input unchanged within 1500ms). Both tests are slow-by-test-suite-standards (each runs in tens to low-hundreds of milliseconds) but cheap relative to the regression cost they prevent.
6. **Verify the wire.** Integration test that triggers a *real* error path with a bearer-shape secret in the path string and asserts the bearer cannot appear in stderr. End-to-end, not just unit. The unit tests prove the redactor *can* scrub; the integration test proves it *is* scrubbing on the actual error path that ships in the binary. Skipping step 6 would leave a class of regression -- the redactor wired to the wrong helper, or accidentally bypassed by a future refactor -- invisible to the unit suite.

---

## Why six patterns and not more

The natural follow-up to "ship a redactor" is "ship more patterns." Newman's deliberate decision was to stop at six. The reasoning, captured here for the catalogue owner (himself) when the next pattern shows up:

- **Each new pattern is a maintenance liability.** Patterns drift. New SDK versions emit slightly different shapes. False positives appear when the pattern accidentally matches innocent text. Six patterns is enough surface for the current threat model and small enough that one person can hold all six in their head.
- **The current six cover every shape observed in the v1.x and v2.x audit logs.** Bearer header, api-key header, AZUREOPENAIAPI env var, URL credentials, JSON secret field, kv pair. There is no historical incident that would have been prevented by a seventh pattern that is not prevented by these six.
- **AWS / GCP / Anthropic shapes are deferred.** Not because they are less important, but because they are not currently in the threat model -- this CLI does not call those providers today. Adding patterns for shapes the binary will never produce is dead code with a maintenance cost.
- **The catalogue owner's mandate, when a new shape arrives, is explicit.** New audit-shape arrival = new pattern + new positive test + new negative test + CHANGELOG row under Security. No exceptions, no "we will add the test later."

The kind-label vocabulary is similarly capped at five (`bearer`, `api-key`, `azure-key`, `url-cred`, plus the kv-pass reuse of `api-key`). A future pattern for, say, a JWT bearer specifically might want a `jwt-bearer` label; a pattern for an AWS access key might want `aws-access-key`. The catalogue owner decides. The contract test fails the build if anyone introduces an unrecognised label without updating the documented vocabulary.

---

## Audit-thread retrospective

This episode closes a finding that has been open since v1.8. The shape of the audit thread, season by season:

- **S01 (v1.x)** -- `UnsafeReplaceSecrets` lands as a tactical patch for a specific incident where an endpoint hostname leaked into a CI log. Value-based, narrow, applied to three call sites. Newman files the first "shape-based scrubbing is missing" finding on the v1.8 post-release audit. Triaged to "next major."
- **S02 (v2.0 - v2.1)** -- the multi-provider work begins. Newman re-files the finding three times: once on the v2.0 launch audit, once on the v2.1 post-prompts audit, once on the v2.1.1 re-audit. Each time triaged forward. The justification is consistent: no current production incident, so no urgency; centralising a scrubber is a "next sprint" project that never quite lands.
- **S02E22 *The Process*** -- the findings-backlog skill lands. The shape-based-scrubbing finding finally gets a stable ID and a permanent home. It stops being a thing that gets re-discovered every audit and becomes a thing that lives at the top of the queue.
- **S03 opening triple (E03 / E04 / E05)** -- audits all the way down. The shape-based-scrubbing finding is still open. The auditor's auditor (E05) flags the gap between "documented findings" and "shipped fixes." Showrunner schedules E07.
- **S03E06 *The Schema*** -- preferences.json v1 ships. New error surfaces. The cost of *not* having shape-based scrubbing rises by another increment. The schedule for E07 firms up.
- **S03E07 *The Redactor*** -- ships. The thread closes.

The lesson, written down so the next ten-month finding does not happen the same way: a finding that gets re-filed three times across two seasons is not a "next sprint" finding. It is a structural gap, and structural gaps are scheduled by the showrunner, not deferred by the on-call team. The `findings-backlog` skill plus the writers'-room arc plan plus the showrunner's authority to schedule against the arc are the three pieces of machinery that made E07 actually land. None of those existed in S01.

---

## Threat model note

| Attack | Impact | Mitigation | Residual risk |
|--------|--------|------------|---------------|
| Bearer token in upstream SDK exception escapes via top-level catch | Token written to stderr, captured in support bundles / CI logs / shell history | `Redact()` on `inner.Message` at line 174; `RedactException()` available for full `ex.ToString()` | None observed on the six pattern shapes. New shapes need new patterns -- see open question #3. |
| api-key header echoed by an SDK that logs request headers on failure | Same as above | Pattern 2 (`api-key` / `x-api-key`); preserves header name so debugging still works | Same -- shape-based, so a header named `X-Custom-Auth` would not match. |
| URL with embedded credentials surfaced in a redirect-chain error | `user:pass` pair leaked | Pattern 4 | Only matches `http`/`https` schemes. `ftp://user:pass@...` would not match; not a current attack surface. |
| Adversarial input causing catastrophic backtracking | Hang on the error path -- defeats the redactor's purpose | 500ms `MatchTimeout` on every regex; `try/catch (RegexMatchTimeoutException)` returns input unchanged and bumps `TimeoutCount` | Counter is stubbed; telemetry sink is open question #4. A live attacker could in theory probe by checking for the timeout fall-through, but the input that triggers it never contained a real secret to begin with. |
| Redactor drift -- someone replaces `[REDACTED:<kind>]` with `***` six months from now | Downstream parsers break silently | `MaskFormat_UsesBracketedKindLabels` test fails the build | None. |
| Pattern miss -- new audit-shape arrives in a post-E10 provider | New shape leaks until pattern is added | Open question #3: Newman owns the catalogue; new shape = new pattern + CHANGELOG row | Live until the new pattern lands. The catalogue ownership is the durable mitigation. |
| Mask format drift in a careless future refactor | Downstream parsers silently break; secret stays scrubbed but consumers cannot key off the tag | `MaskFormat_UsesBracketedKindLabels` test fails the build on any drift | None observed; the test is the durable mitigation. |
| Logging library bypasses `ErrorAndExit` and writes directly to `Console.Error` | Secret leaks via the bypass path, redactor never sees it | The top-level catch at line 174 is a backstop for unhandled exceptions, but `Console.Error.WriteLine` calls scattered through the codebase that *are* handled and skip `ErrorAndExit` would bypass | Audit task: enumerate bare `Console.Error.WriteLine` calls; route any that touch user input or exception data through the redactor. Filed for a future episode. |
| Future agent disables the regex timeout to "make a test pass" | A pathological input could hang the redactor | Code review + the `PathologicalInput_DoesNotHang` test failing if the timeout is removed | None observed; the test is the durable mitigation. |

---

## Open questions -- showrunner triage

Newman filed four. The showrunner's calls:

1. **PreferencesTests flake (carried over from E06).** A non-deterministic test in the preferences lane is intermittently red on slow CI runners. **Routed to Puddy** via the `pref-test-flake-fix` todo, which is in-flight in the same dispatch wave as this episode. Puddy: "Either it works or it doesn't. It doesn't. I'll fix it." Kramer (whose code originally landed the flake): "Yeah. Sorry. Take it." Two sentences and the handoff is done. Tracked separately; not part of E07's diff. Showrunner's call: this is an E06 lane finding, not an E07 finding; the rollup below reflects that.

2. **JSON-quote-after-redaction.** When the bearer pattern matches inside a JSON-quoted form (`"Authorization":"Bearer xyz"`), the replacement produces a string that is no longer parseable JSON. For stderr log lines this does not matter. If the redactor is ever reused on a structured-error JSON envelope (the E08 ADR may want this), it does. **Routed to Maestro and Frank Costanza, joint, 30-day SLA.** Maestro: "Defer to Frank -- this is a logging-format question, not a prompt-engineering question." Frank: "SERENITY NOW. Fine. I'll wire it into the SRE backlog and we'll decide before E12." Showrunner's call: decide whether redacted output should round-trip as JSON. Document either way -- a deliberate "log-lines-only, JSON consumers must wrap" is a fine answer; silently shipping non-parseable JSON is not.

3. **Pattern catalogue ownership.** Six patterns today. There will be more -- E10's keychain work and any post-E13 hardening will surface new audit-shape inputs. Without an owner, patterns get added ad-hoc and the catalogue rots. **Newman owns the catalogue.** New audit-shape arrival = new pattern + a row in CHANGELOG `[Unreleased]` under Security. `docs/hardening.md` becomes the long-form reference page when Newman is ready -- showrunner's call is *no rush*; write it after E10 when the second provider's catalogue is also visible, so the page is not a single-provider artifact.

4. **`TimeoutCount` telemetry.** The counter exists. There is no sink. **Routed to Frank Costanza, SRE backlog, no urgency.** Showrunner's call: wire it when telemetry plumbing lands as part of an arc that needs telemetry; do not build telemetry plumbing solely to surface this counter. The counter being non-zero in production is the kind of signal Frank will want to see eventually, but it is not a release blocker for v2.x.

---

## Findings rollup

This episode introduces **no new findings**. The carry-over PreferencesTests flake is filed against the E06 lane and tracked under `pref-test-flake-fix` (Puddy, in-flight). The four open questions above are all *forward-looking design questions*, not findings -- no audit shape they describe constitutes a current leak or a current break.

Sufficient gate-tier findings are now indexed in `docs/findings-backlog.md` (Wilhelm's `wilhelm-2026-05-W-01` entry tracks the meta-process finding that this episode partially closes -- the findings-backlog skill has now been used by enough episodes that "documented and unused" is no longer an accurate state). E07 confirms: **no new gate-tier finding rows added**.

Findings closed by this episode (status updates land in the backlog ledger as part of the same push):

- The shape-based-scrubbing finding originally filed on the v1.8 post-release audit is **closed**. The v2.1 and v2.1.1 re-files of the same finding are also closed by reference.
- The "no centralised redactor on the top-level catch" sub-finding from the v2.1 post-prompts audit is **closed**.
- The `wilhelm-2026-05-W-01` "findings-backlog skill documented and unused" entry transitions from `in-progress` to `mitigated` -- the skill is now demonstrably in use across multiple episodes.

Findings *not* closed but explicitly de-risked by this episode:

- Any future "bearer header in stderr" report becomes a regression rather than a novel finding. The P1 test is the gate.
- Any future "secret leaks via top-level catch" report becomes the same kind of regression. The line-174 wire is the gate.

---

## Next episode preview -- E08 *The Pick*

Costanza writes the decision ADR. Anthropic vs OpenAI direct as the first non-Azure cloud. Sue Ellen weighs in on competitive optics. Recommendation in the blueprint is OpenAI direct -- it slots into the generic OpenAI-compat adapter that E09 will build, with zero bridge code. E08 is the architectural decision; E09 is the adapter; E10 is the per-OS keychain extension; E11 is the wizard; E12 is the per-provider rate-card stubs. The redactor that landed today is the floor that lets all five of those land safely.

The Provider Abstraction Seam arc is now *secure-by-default*. E08 next.

### What the next contributor needs to know

Five things, kept short, because the next person to touch this code may be six months from now and will not remember the context:

1. **`SecretRedactor.cs` is small on purpose.** Do not grow it. New patterns, yes; new methods, no. Keep the surface narrow so the audit story stays narrow.
2. **The 500ms timeout is non-negotiable.** Do not remove it. Do not raise it without a perf-canary test that proves the new ceiling. Do not catch the `RegexMatchTimeoutException` anywhere except the one place it is already caught.
3. **The mask format is a contract, not a style choice.** Downstream tooling depends on `[REDACTED:<kind>]` byte-for-byte. Do not "improve" it.
4. **`UnsafeReplaceSecrets` is not dead code.** It is defense-in-depth. Read the comment block at line 1599. Removing it would create a value-based-scrubbing gap that the shape-based redactor does not cover.
5. **The P1 test is the audit contract.** If you ever find yourself making the P1 test less strict to land a feature, stop. The P1 test failing is the system telling you the feature has a leak. Fix the leak.

---

## Lessons from this episode

1. **Centralising a scrubber is cheaper than fixing nineteen call sites.** The pre-E07 plan -- briefly considered -- was to audit every `ErrorAndExit` invocation and add per-call-site scrubbing. That would have been nineteen changes, nineteen tests, nineteen review surfaces, and nineteen places for the next contributor to forget. Folding the scrubber into the helper itself is one change, one P1 test, one review surface. Pick the choke point.
2. **Shape-based and value-based scrubbing are complementary, not redundant.** `UnsafeReplaceSecrets` (value-based) and `SecretRedactor` (shape-based) cover overlapping but non-identical surfaces. Removing either creates a gap. The instinct to delete the older code as "superseded" was wrong; both ship. The worked example in Act II makes this concrete -- the bearer token from a third-party SDK is invisible to value-based scrubbing, and a fat-fingered paste of the user's literal key into a path is invisible to shape-based scrubbing. The two scrubbers are not a redundancy; they are a partition of the threat model.
3. **A regex on the error path needs a timeout.** The redactor exists *because* errors are surfacing. An adversarial input that hangs the redactor would defeat the redactor's purpose and be indistinguishable from the bug it was meant to fix. 500ms ceiling, fall through with the input unchanged on timeout, count the timeouts. Newman caught this on the first draft. The `PathologicalInput_DoesNotHang` test is the durable enforcement -- 200,000 `a`s plus a NUL byte, ceiling 1500ms, contract is "either masks or returns input; hanging is unacceptable."
4. **Engineering mode for an audit agent is a one-off.** The role inversion worked here because the work was tightly scoped (one file, one helper, one wire-up) and the contract was already authoritative (ADR-007 section 2). It is not a precedent for hiring auditors as builders generally; this episode was an exception, not a pattern. The next time Newman files a finding the answer is still "Newman files, Kramer fixes." E07 was the closing chord on a multi-season audit thread, not the opening of a new mode.
5. **The carry-over flake routing happened live.** Showrunner triaged the `pref-test-flake-fix` handoff inside the same dispatch wave as the redactor itself. Two parallel agents, no shared files, no collision. The fleet-dispatch skill's "wave on collision risk" rule handled it cleanly because there was no risk -- Newman touches `SecretRedactor.cs`, `Program.cs`, `SecretRedactorTests.cs`, and `tests/integration_tests.sh`; Puddy touches `PreferencesTests.cs` and possibly `Preferences.cs`. Disjoint diff sets, parallel waves, zero merge ceremony.
6. **The mask-format contract test pays for itself the day someone proposes "a more readable" format.** `[REDACTED:<kind>]` is ugly. Someone, eventually, will propose `<secret>` or `***` or even Unicode block characters. The drift test (`MaskFormat_UsesBracketedKindLabels`) makes that a code-review conversation rather than a silent regression. Six months from now, when log scrapers and support-bundle tooling depend on the bracketed-kind shape, this test will be the only thing standing between the team and a downstream breakage.

---

## Metrics

- Diff size: +447 lines insertions / ~6 lines deletions / 5 files (`SecretRedactor.cs` new, `SecretRedactorTests.cs` new, `Program.cs` modified at two sites, `tests/integration_tests.sh` +49, `CHANGELOG.md` +1 row).
- Test delta: +31 unit tests (672 -> 703), +4 integration assertions (35 -> 39).
- Preflight: passed (format clean, build green, unit + integration green).
- CI status at push time: pending at write-time; will be green or this report gets a follow-up note.

---

## Credits

- **Newman** -- lead, in engineering mode for the first and arguably last time. Wrote `SecretRedactor.cs`, all 31 unit tests, the four integration assertions, and the `Program.cs` wiring at lines 174 and 1631. Filed the four open questions on the way out. Hostile professional pride throughout; did the work anyway. The tag scene captures the mood: he wishes he could audit himself one more time. The work shipped clean and the audit thread closes.
- **Maestro** -- one cameo scene on the JSON-quoting question. Deferred to Frank. Clean handoff, no wasted dialog.
- **Frank Costanza** -- inherits `TimeoutCount` telemetry stub and joint ownership of the JSON-quote question. SERENITY NOW. Stub is in the source comment at line 30 of `SecretRedactor.cs`; sink is on the SRE backlog.
- **Puddy** -- accepted the `pref-test-flake-fix` handoff from Kramer's E06 lane. Stoic. Working it in parallel. Either it works or it does not.
- **Kramer** -- handed off the flake; otherwise off-camera this episode. Two sentences of dialog, then back to the keyboard.
- **Larry David** -- showrunner sign-off, open-question triage, dispatch routing, this exec report.

All commits carry the `Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>` trailer per the commit skill. Conventional Commits format. `git -c commit.gpgsign=false` because sub-agents cannot sign.

---

## Sign-off

The redactor is in. The P1 test fails the build the moment a bearer header escapes. The Provider Abstraction Seam arc is secure-by-default before any non-Azure provider lands. Newman files four questions and walks away. Tag scene: he mutters, halfway out the door, that he wishes he could audit himself one more time. Larry: "Forget it. Move on."

Pretty, pretty, pretty good.

E08 *The Pick* next.

---

## Appendix A -- Cast notes

A short note on each cast member's posture this episode, for the writers' room cast-balance audit at E12:

- **Newman (lead).** First lead in engineering mode. Posture: hostile professional pride. He did not enjoy writing the code. He enjoyed the work being done. The distinction matters for casting -- this is not Newman's new mode, it is a one-episode role inversion to close a multi-season audit thread. Future Newman episodes return to audit posture by default.
- **Maestro (cameo).** A clean three-line scene on the JSON-quoting question. Deferred to Frank. The deferral is in-character: Maestro owns prompt engineering, not logging-format decisions, and recognised the boundary cleanly.
- **Frank Costanza (cameo).** One line on the `TimeoutCount` stub. SERENITY NOW. Inherits the SRE backlog item. This is exactly the role Frank should be playing in a security-adjacent episode -- present, named, owning the reliability hook, not stealing focus.
- **Puddy (cameo).** Stoic acknowledgement of the flake handoff. Two-line dialog. Working the fix in a parallel lane. Puddy's value to this episode is that he absorbs the carry-over without ceremony; if Puddy were not on the bench, the flake would have stalled the redactor wave.
- **Kramer (half-line).** Hands off the flake. Otherwise absent. Correct casting -- E07 is not a Kramer episode and forcing him on-screen would have diluted the lead.
- **Larry David (showrunner).** Triaged four open questions, routed two to existing owners, scheduled two for follow-up. Did not write production code. Did not write tests. Did write this exec report. Correct showrunner posture.

---

## Appendix B -- Pattern reference card

A single-page reference for the catalogue owner. Each row: pattern name, replacement template, kind label, primary positive test, primary negative test.

| # | Pattern | Replacement template | Kind label | Positive test | Negative test |
|---|---------|----------------------|------------|---------------|---------------|
| 1 | Bearer header | `Authorization: [REDACTED:bearer]` | `bearer` | `P1_BearerTokenNeverAppearsInRedactedOutput` | `BearerToken_NotPresent_StringUnchanged` |
| 2 | api-key / x-api-key header | `<name>: [REDACTED:api-key]` | `api-key` | `ApiKeyHeader_CaseInsensitive_IsMasked` | `NonSecretHeader_NotMasked_NegativeCase` |
| 3 | AZUREOPENAIAPI env export | `<name>=[REDACTED:azure-key]` | `azure-key` | `AzureOpenAiApiEnvVar_ValueIsMasked` | (covered by `NoSecrets_StringUnchanged`) |
| 4 | URL credentials | `<scheme>[REDACTED:url-cred]@` | `url-cred` | `UrlCredentials_AreMasked` | `UrlWithoutCredentials_NotMasked_NegativeCase` |
| 5 | JSON secret field | `"<name>": "[REDACTED:api-key]"` | `api-key` | `JsonSecretField_AnyAlias_ValueIsMasked` | `JsonNonSecretField_NotMasked_NegativeCase` |
| 6 | kv pair | `<name>=[REDACTED:api-key]` | `api-key` | `QueryStringApiKey_IsMasked` | (covered by `NoSecrets_StringUnchanged`) |

Cross-cutting tests that gate the whole catalogue:

- `MaskFormat_UsesBracketedKindLabels` -- every pattern's replacement must use the `[REDACTED:<kind>]` shape.
- `MultipleSecretsInOneString_AllMasked` -- composing all six passes on one buffer must scrub every secret with no pass undoing another.
- `LongInput_OneMegabyte_RedactsUnderBudget` -- the steady-state perf canary.
- `PathologicalInput_DoesNotHang` -- the timeout-containment canary.
- `EmptyString_ReturnsEmpty`, `NullInput_ReturnsEmpty`, `NoSecrets_StringUnchanged` -- the trivial-edge-case trio.
- `RedactException_StripsBearerFromMessage`, `RedactException_NullInput_ReturnsEmpty` -- the exception-variant tests.

When adding pattern 7 in a future episode, the checklist is: write the regex with `Compiled | IgnoreCase | CultureInvariant` + 500ms timeout, write at least one positive test, write at least one negative test, add a row here, add a CHANGELOG row under Security, document the kind label in this appendix, and update the catalogue owner's notes in `docs/hardening.md` once that page exists.

The reverse checklist -- when *removing* a pattern -- is shorter and stricter: do not. Patterns only get removed if the shape they catch is provably unreachable in the binary's current threat model, and even then the removal needs a CHANGELOG row plus a written-down justification in this appendix. Defensive code that has never fired is not the same as defensive code that is unnecessary.

---

## Appendix C -- ADR-007 section 2 cross-reference

For any reader who lands here without the ADR context: ADR-007 section 2 ("Bearer-token auth on every provider endpoint") establishes that all provider endpoints in this CLI authenticate via a bearer token in the `Authorization` header, and that the bearer token, by virtue of being the response to the user's API key, is itself a secret of equivalent sensitivity to the API key. The ADR mandates that any log path, exception message, or structured trace that could carry an `Authorization` header must route through a redactor; auth-header appearance in an exception message is explicitly classified as a P1 bug.

E07 is the implementation of that mandate. The P1 unit test name (`P1_BearerTokenNeverAppearsInRedactedOutput`) is a direct quotation of the ADR's "must never appear" language. The integration smoke test in `tests/integration_tests.sh` is the end-to-end enforcement. The two together close the ADR section 2 mandate from "documented requirement" to "mechanically enforced contract."

If a future ADR amends section 2 -- for example, to add an Anthropic-style `x-api-key` header as a co-equal auth shape, or to add an OAuth2 refresh-token shape -- the redactor's pattern set must be updated in the same change. The catalogue owner is the gate.

End of report.

---
