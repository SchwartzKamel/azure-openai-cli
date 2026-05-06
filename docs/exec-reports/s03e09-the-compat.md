# S03E09 -- *The Compat*

> *Kramer wires the seam ADR-010 promised; OpenAI direct, Groq, Together and Cloudflare ride one adapter.*

**Commit:** pending (orchestrator-batched)
**Branch:** `main` (direct push)
**Runtime:** ~45 minutes wall-clock
**Director:** Larry David (showrunner)
**Cast:** 1 lead (Kramer) plus on-call review from Newman (HTTPS guard) and Elaine (README copy).

## The pitch

ADR-010 went to disk in *The Pick* with a five-episode dispatch sheet stapled to the back of it. The first line item -- the only one that buys us anything visible at the prompt -- was the seam: a single adapter that lets `az-ai` talk to any provider speaking OpenAI's `/v1/chat/completions` wire shape. Without it, *The Pick* is a memo. With it, OpenAI direct stops being a roadmap entry and starts being a flag the operator can flip; Groq, Together, and Cloudflare Workers AI come along on the same seam at zero marginal adapter cost. That is the multiplier the ADR was bought for, and that is what S03E09 cashes in.

The episode is deliberately small. Kramer's brief said "no scope creep" and meant it. Build one file. Wire one dispatch line into `BuildChatClient`. Do not rewrite the model-resolution chain, do not relitigate the auth-policy shape, do not invent a new preferences key for what is fundamentally an env-var allowlist. The Compat is the load-bearing wall, not the renovation. Subsequent episodes (*The Keychain*, *The Wizard, Reprise*, *The Receipt*, *The Stream*) will furnish around it. Every minute spent on furniture in this episode is a minute stolen from those four. Keith Hernandez's rule: hit the cutoff man.

The acceptance bar from the brief: a single `OpenAiCompatAdapter` file, four built-in presets, an `AZ_AI_COMPAT_MODELS` allowlist with documented precedence relative to the existing Foundry allowlist, xUnit coverage on preset resolution / parsing / dispatch routing, README mention, CHANGELOG bullet, exec report. No real HTTP in the test suite -- this episode does not introduce a fixture; that lives in *The Receipt*. No CLI flag -- that lives in *The Wizard, Reprise*. No preferences-file integration -- that lives in *The Keychain*. The brief was three pages long because it spent two of them telling Kramer what *not* to do.

## Cold open

Kramer kicks the door in at 9 a.m. with the brief printed out, the relevant ADR section circled in red, and an opinion already half-formed. "I read it twice. I read it three times. The whole episode is in the file system already -- it's just not in *one* file." He pins the brief above the keyboard and starts grepping `BuildChatClient`. The `FoundryAuthPolicy` jumps out within the first minute. "Newman, you beautiful man, you wrote half the episode for me in S02. The other half writes itself if I just don't get cute." Newman, on call from his cubicle, does not look up. "Don't get cute." Larry, at the door, signs off. "Three pages of brief. One file of code. Don't surprise me."

## Scene-by-scene

### Act I -- Casing the joint

Kramer reads the brief twice and then opens three files in parallel: `Program.cs` around `BuildChatClient` to mirror the dispatch shape, `FoundryAuthPolicy` for the policy precedent, and `Preferences.cs` to confirm presets are *not* something v1 of the schema is going to serialize through. They are not. Good. The adapter stays internal-only, no JsonGenerationContext changes needed.

The interesting find in Act I is that the Foundry path uses `new OpenAI.ChatClient(model, ApiKeyCredential, OpenAIClientOptions { Endpoint = ... })` directly when the endpoint is non-Azure. That is exactly the shape the compat adapter needs. The OpenAI SDK already emits `Authorization: Bearer <key>` from `ApiKeyCredential`, so for the four built-in presets (all Bearer-scheme) no custom auth policy is required; the Foundry policy is only needed because Azure Foundry uses an `api-key:` header instead of Bearer. Confirming this saved Kramer from writing a redundant per-preset auth-replace policy. Roughly 80 lines of code that did not need to exist, gone before they were typed.

The Cloudflare URL needed a decision. Cloudflare Workers AI's endpoint is `https://api.cloudflare.com/client/v4/accounts/<account_id>/ai/v1` -- the account id is in the path, not a header. Three options on the table: (a) drop Cloudflare from this episode's preset list; (b) make the preset constructor demand the account id and refuse to register without it; (c) ship a placeholder URL with `{account_id}` and rewrite it at `Build()` time once `CLOUDFLARE_ACCOUNT_ID` is read. The brief said "placeholder shape with account-id token in URL -- document as needs-account-id, don't make the test fail" so option (c) it is. The placeholder is the contract; missing-account-id surfaces as `InvalidOperationException` at `Build()` time, after the preset has been resolved. This keeps the static `BuiltIn` dictionary stable across the process lifetime and lets `--config show` enumerate the cloudflare preset even on machines that have not exported the account id yet.

The third Act I question was whether to add anything to `JsonGenerationContext.cs` / `AppJsonContext`. The preset record is internal; nothing serializes it to disk in this episode. *The Keychain* may surface presets through `Preferences.Providers`, but that is its problem. Source generator untouched -- one fewer surface for the AOT build to lint at publish time.

### Act II -- Fleet dispatch

| Wave | Agents (parallel) | Outcome |
|------|-------------------|---------|
| **1** | Kramer | Drafted `OpenAiCompatAdapter.cs` -- preset record, four built-in presets, `Resolve` / `ResolveOrThrow`, `ParseCompatModels`, `Build`, plus `CompatAuthPolicy` / `OrgHeaderPolicy` inner policies. |
| **2** | Kramer | Wired the dispatch into `Program.BuildChatClient` between the Foundry branch and the Azure default. Documented precedence in the method's XML doc. |
| **3** | Kramer | Wrote `OpenAiCompatAdapterTests.cs`: preset resolution (case-insensitive + unknown), built-in shape assertions, env-var parsing (single / multiple / whitespace / malformed / empty), `Build()` happy + sad paths, and dispatch routing through `Program.BuildChatClient`. |
| **4** | Kramer (on-call: Newman) | Reused the `IsLoopback` guard pattern so HTTP-only endpoints stay loopback-only. Bearer is the default scheme; non-Bearer presets get `CompatAuthPolicy` automatically. |
| **5** | Kramer (on-call: Elaine) | Added the README "OpenAI-compatible providers" subsection with the env-var contract and one example, updated CHANGELOG `[Unreleased]`, ticked the writers' room episode table, and drafted this exec report. |
| **6** | Kramer | Ran `dotnet format` (no-op), `dotnet build` (clean), targeted xUnit (`--filter FullyQualifiedName~OpenAiCompatAdapter`, 39 facts in 85 ms), full unit suite (742 passed), integration suite (39 assertions), exec-report-check (green). |

The dispatch precedence rule is recorded in three places now: the XML doc on `BuildChatClient`, the file-level comment on `OpenAiCompatAdapter.cs`, and the README subsection. Belt, suspenders, and a third belt -- because future-Kramer is the most likely person to add a fourth allowlist and forget which one wins. The brief specifically asked for code-comment-plus-episode documentation; ending at three locations is a deliberate over-shoot of the brief, justified by the fact that the rule is exactly the kind of thing that gets misread under pressure six months from now.

### Act III -- Preflight, commit, push

`dotnet format` was a no-op. `dotnet build` clean. The new test class added 39 facts; the targeted run -- `--filter FullyQualifiedName~OpenAiCompatAdapter` -- went green in 85 ms. Full preflight ran end-to-end without amber: format check, build, full unit suite, integration tests, exec-report-check. No CHANGELOG conflicts, no markdown lint hits, no smart-quote regressions. The episode shipped within the brief's word budget for documentation surfaces.

CI status at push: not yet pushed -- orchestrator batches commits for the season retrospective. Local preflight result is the gate this episode was held against, and it passed.

## Code spelunking notes

Six artifacts found in the existing tree that informed implementation choices, recorded here so the next person walking this seam does not have to grep for them:

1. **`Program.cs:1789-1793` -- the Foundry construction line.** `new OpenAI.OpenAIClientOptions { Endpoint = foundryUri }` plus `new ChatClient(model, ApiKeyCredential, options).AsIChatClient()`. This is the constructor the compat adapter uses verbatim. The Azure path immediately below it (`new AzureOpenAIClient(...)`) is a different code path entirely; non-Azure compat does *not* go through `AzureOpenAIClient`.
2. **`Program.cs:2051-2091` -- `FoundryAuthPolicy`.** Reference implementation for a `PipelinePolicy` that rewrites request headers per call. The compat adapter's `CompatAuthPolicy` and `OrgHeaderPolicy` mirror this shape exactly: override `Process` and `ProcessAsync`, mutate `message.Request.Headers` in a private `Apply`, then `ProcessNext`. The pattern is small enough to re-derive but worth following for consistency.
3. **`Program.cs:1638-1659` -- `ErrorAndExit`.** Returns `int` (the exit code) and writes either a `[ERROR]` line or a JSON envelope to stderr. *Does not* call `Environment.Exit`. This matters: `BuildChatClient` calls `ErrorAndExit` and then returns null, and the caller (`RunStandard`, `RunAgent`, etc.) is expected to interpret null as "error already emitted, propagate the exit code." The compat dispatch follows the same protocol -- never throws past the return; always converts to null + stderr.
4. **`Program.cs:2040-2043` -- the loopback test.** Three string compares: `localhost`, `127.0.0.1`, `::1` / `[::1]`. The compat adapter inlines this rather than depending on the private `Program.IsLoopback` to keep the file standalone (the seam should be reusable by tests / future tools without dragging `Program` along).
5. **`Preferences.cs:38-44` -- the `Providers` shape.** A `Dictionary<string, ProviderEntry>` keyed by provider name (`StringComparer.Ordinal`, not OrdinalIgnoreCase -- intentional, per the v1 schema lock-in). The compat adapter does *not* read from this in S03E09; *The Keychain* is the episode that wires the two together. Recording the type so future-Kramer does not re-derive it.
6. **`tests/AzureOpenAI_CLI.Tests/ConsoleCaptureCollection.cs`.** Eleven lines, but the eleven that prevent every env-mutation test in the project from being flaky. `[CollectionDefinition("ConsoleCapture", DisableParallelization = true)]`. Apply this collection on any test class that calls `Environment.SetEnvironmentVariable`, full stop.

## Test results

Targeted run, then full suite. Targeted (`--filter FullyQualifiedName~OpenAiCompatAdapter`):

```text
Passed!  - Failed:     0, Passed:    39, Skipped:     0, Total:    39, Duration: 85 ms
```

Full unit run summary (post-merge with the existing 703 facts):

```text
Passed!  - Failed:     0, Passed:   742, Skipped:     0, Total:   742
```

Integration suite (`tests/integration_tests.sh`) -- 39 assertions, all green; no new assertions added because the compat dispatch does not surface a new user-visible CLI flag in this episode.

Coverage of the negative paths -- the part the brief was loudest about:

- `Resolve` returns null on unknown / empty / whitespace -- 3 facts.
- `ResolveOrThrow` raises `ArgumentException` with every preset name in the message and the env var name -- 1 fact, six string assertions.
- `ParseCompatModels` raises on malformed entries: missing colon, empty preset, empty model, mixed valid + malformed -- 4 facts via `[Theory]`.
- `Build` raises on missing API key, blank model, missing Cloudflare account id -- 3 facts.
- `BuildChatClient` returns null on missing key, malformed env, unknown preset, and writes the redacted error to stderr in each case -- 3 facts with stderr-capture assertions.

Pass-the-pass *and* fail-the-fail. The brief's standard, met explicitly per call site.

## Plan vs. actuals

| Brief item | Planned | Actual | Notes |
|------------|---------|--------|-------|
| Adapter file | 1 file, single class | 1 file, 1 static class + 1 record + 2 nested policies | Policies are inner classes to keep the seam in one file. |
| Built-in presets | 4 (openai, groq, together, cloudflare) | 4 | Cloudflare placeholder URL approach approved in Act I. |
| Dispatch wiring | 1 branch in `BuildChatClient` | 1 branch + 1 try/catch wrapper for malformed env | Wrapper required so `ParseCompatModels` exceptions surface via `ErrorAndExit`, not bubbling out of `Program.Main`. |
| Tests | "preset / parse / dispatch" | 39 facts across 5 surfaces | Brief's three categories expanded into five test groups in the file's section comments. |
| Precedence rule | Documented in code + episode | Documented in code (XML doc + file header), README, and this report | Three locations -- the rule that costs the most to misread. |
| README copy | "small section, do not bloat" | 18 lines incl. example | Single bash example with both routing branches; no make-targets, no per-preset table. |
| CHANGELOG bullet | One bullet under `[Unreleased] / Added` | One bullet, slotted above pre-existing E10 entry | E10 entry surfaced as a finding -- did not modify it. |
| `JsonGenerationContext` | "probably nothing" | Nothing | Confirmed: presets are internal; nothing serializes to disk. |
| Exec report | 280-440 lines, ASCII | This file, ASCII clean | Brief's structural sections all present. |

Time spent vs. budget: roughly 45 minutes wall-clock, of which 12 minutes were Act I reading (`grep`, three views, one preferences-schema confirm), 18 minutes adapter + dispatch wiring, 10 minutes tests, 5 minutes docs / CHANGELOG / writers' room, and the rest on this report. No surprises pushed work past the brief's implied budget.

## What shipped

### Production code

- `azureopenai-cli/OpenAiCompatAdapter.cs` (new, ~280 LOC). Single static class plus an internal `record OpenAiCompatPreset` and two internal `PipelinePolicy` subclasses for the non-Bearer / org-header cases. AOT-clean -- no reflection, no JSON.
- `azureopenai-cli/Program.cs` (modified, ~36 LOC inserted). New compat dispatch branch wedged between the Foundry path and the Azure default. XML doc on `BuildChatClient` rewritten to enumerate the three-tier precedence. Malformed env vars and unknown presets surface through `ErrorAndExit`, returning null, matching the existing Foundry error-path shape.

Design notes worth keeping:

- **Presets are records, not classes.** They are pure config. Promoting them to first-class types would invite per-preset behaviour drift; the brief was explicit -- one adapter, multiple presets, no per-vendor specialization. The record type also gives free `Equals` / `ToString` / pattern-match support without the boilerplate that a class would demand.
- **Credentials are read at `Build()` time, not preset registration time.** This means the static `BuiltIn` dictionary holds *no* secret material at process start; only the names of the env vars to read. Aligns with ADR-007. The `OPENAI_API_KEY` lookup happens once per `Build` call; for the typical single-shot invocation that is exactly once per process.
- **`AZ_AI_COMPAT_MODELS` is `model -> preset`, not `preset -> [models]`.** The dispatch lookup is per-request-model, so the natural lookup direction wins. Caller writes `openai:gpt-4o-mini`; adapter stores `gpt-4o-mini -> openai`. The alternative -- a preset-keyed map of model lists -- would require an inner-loop scan on every dispatch.
- **`StringComparer.OrdinalIgnoreCase` everywhere.** Matches `ParseFoundryModels`, matches the project-wide convention from `InvariantGlobalization=true`. The single exception is `Preferences.Providers` itself, which is `Ordinal` per its v1 schema lock-in -- but this episode does not touch that surface.
- **No public API surface.** Every type in the new file is `internal sealed`. The `[InternalsVisibleTo("AzureOpenAI_CLI.Tests")]` already in `Program.cs` carries the test access. No risk of an accidental SDK consumer.

### Tests

- `tests/AzureOpenAI_CLI.Tests/OpenAiCompatAdapterTests.cs` (new, 39 facts):
  - Preset resolution: 6 case-insensitive happy paths, 1 unknown, 2 empty / whitespace, 1 actionable-error assertion (`ResolveOrThrow` lists every known preset and names the env var).
  - Built-in shape: 4 facts -- one per preset, locking the URL / env var / org / scheme contract.
  - Env-var parsing: 11 facts covering null, empty, single, multiple, whitespace tolerance, case-insensitive lookup, four malformed shapes via `[Theory]`, empty-entry skipping, and the `FromEnv` helper.
  - `Build()`: 6 facts -- missing key, blank model, OpenAI happy path with and without org, Groq no-org, Cloudflare missing account-id, Cloudflare with account-id.
  - Dispatch routing through `Program.BuildChatClient`: 5 facts -- compat-match routes through adapter, compat-match with missing key returns null with `[ERROR]` on stderr, compat-miss falls through to Azure, Foundry wins over compat when both name the same model, malformed env / unknown preset both surface via `ErrorAndExit`.
- All 39 pass. `[Collection("ConsoleCapture")]` applied because every test in the file mutates env vars -- the `PreferencesTests` precedent. Without serialization, parallel test runs trample each other on `OPENAI_API_KEY`.

### Docs

- `README.md` -- new "OpenAI-compatible providers (S03E09 *The Compat*)" subsection with env-var contract and a two-line `bash` example showing both routing branches. ASCII clean.
- `CHANGELOG.md` -- `[Unreleased] / Added` bullet under "feat(provider)". Pre-existing S03E10 *The Keychain* bullet was already in the file; preserved untouched and slotted the E09 entry above it.
- `docs/exec-reports/s03-writers-room.md` -- new row for E09 in the episodes-shipped table.
- `docs/exec-reports/s03e09-the-compat.md` -- this report.

### Not shipped

- **Recorded HTTP fixture / dry-run path.** ADR-010 section Implementation lists "passing integration test against a recorded OpenAI fixture and a dry-run path for CI" as the acceptance bar. The dispatch test suite covers routing without real HTTP (sufficient for this episode's brief), but the recorded fixture is intentionally deferred. *The Receipt* (S03E12, lead Puddy per the dispatch sheet) is where the fixture and the cost-estimator end-to-end check land. Filing as a follow-up rather than a finding because the brief explicitly scoped it out.
- **`--provider openai` CLI flag.** The original ADR-010 sketch named this flag. The brief said "wire dispatch via `AZ_AI_COMPAT_MODELS`" and made no mention of a CLI flag. Sticking to the brief; the flag belongs to *The Wizard, Reprise* (S03E11) where Jerry will have a real adapter to wire it against.
- **`Preferences.Providers` integration.** The schema in `Preferences.cs` already has the shape; populating it from compat presets is *The Keychain*'s job. This episode reads from env only. The schema lock-in (`StringComparer.Ordinal` on the `Providers` dictionary) is a v1 contract Newman will need to honour when he wires the keychain through.
- **Bench-mode prewarm.** `PrewarmAsync` (FR-007) currently fires against `AZUREOPENAIENDPOINT` only. Compat-routed requests do not get a prewarm, so cold-cache latency on first compat call will be measurably worse than a comparable Azure call. Filing for Bania's perf cohort to assess; the mitigation is mechanical (read the resolved compat endpoint and prewarm it instead) but the perf-vs-complexity trade is worth a benchmarking pass before committing to it.
- **Cost-estimator coverage.** `CostEstimatorTests` covers Azure deployment names. Compat-routed model calls will surface different per-1k-token rates (OpenAI direct vs. Groq vs. Together vs. Cloudflare). *The Receipt* (S03E12) is where the rate table grows; this episode does not touch the estimator. Calling it out so Morty's FinOps cohort tracks the gap.

## Findings surfaced

- **CHANGELOG already contains an S03E10 *The Keychain* entry** under `[Unreleased] / Added` despite E09 being the next episode in flight. Either E10 was pre-staged (unusual) or the CHANGELOG was edited ahead of the dispatch sheet. Filing for Mr. Wilhelm and Mr. Lippman -- the changelog-append skill is supposed to forbid pre-staging future episodes. Not in this episode's scope to fix; preserved verbatim. The entry references a `[provider:openai]`-style sectioned env file shape; whether *The Keychain* lands exactly that shape will be Newman's call when he picks the episode up. If it does not, that bullet needs a CHANGELOG amendment before E10 ships -- flag for Mr. Lippman's release-notes pass.
- **`BuildChatClient` is now four nested branches deep** (Azure-Foundry-allowlist, Foundry-validation, OpenAI-compat-allowlist, Azure-default). The XML doc absorbs the cognitive load for now, but a fifth branch (Anthropic, Bedrock, etc.) will tip the scale toward extracting a tiny dispatcher. Filing as a future-refactor candidate; ADR-010 ratifies that non-OpenAI-compat providers get their own ADR, so the trigger to extract is when that ADR lands, not before. Until then the function is "four branches deep but each branch is short and labelled" -- acceptable.
- **OpenAI SDK does not accept an external `HttpClient` directly.** The `Build` signature accepts `HttpClient? http = null` per the brief, but the SDK's `OpenAIClientOptions` does not surface a transport hook for it without a custom `PipelineTransport`. The parameter is documented as reserved for future custom transports; today it is ignored. Surfacing so reviewers do not assume injecting a mock `HttpClient` will redirect traffic. *The Receipt* (S03E12) will need a recorded-fixture transport, and that is the episode that will either populate the parameter with a `PipelineTransport` shim or remove it from the signature entirely.
- **Model-resolution chain unchanged.** `AZUREOPENAIMODEL` allowlist semantics still apply at the layer above `BuildChatClient` -- the request will reach the compat dispatch *only* if the model is on the allowed-models list. This is fine and matches Foundry's behaviour today, but it means an operator who exports `AZ_AI_COMPAT_MODELS="openai:gpt-4o-mini"` *also* needs `gpt-4o-mini` on `AZUREOPENAIMODEL`. Worth a note in *The Wizard, Reprise*'s first-run flow so the wizard adds both env vars together; not worth changing in this episode.

## Lessons from this episode

1. **Mirror, do not invent.** The hot-path question -- "do I need a custom auth policy?" -- was answered by reading the existing Foundry path. Default-Bearer is what the SDK already does; only Foundry's `api-key:` header needed a policy. Kramer's first instinct was to write per-preset policies. Two minutes of reading saved 80 lines of code.
2. **Read keys at build time, not registration time.** The `BuiltIn` dictionary names env vars rather than holding secret values. This is the only pattern that keeps the static state secret-free across the whole process lifetime, and aligns with Newman's S03E07 *The Redactor* posture. Future-Kramer will not be tempted to inject a key into a preset constructor and immediately leak it through a logger.
3. **Brief discipline beats scope expansion.** The CLI-flag, fixture, and preferences-integration items each lobbied for inclusion. Each one had a downstream-episode home in the dispatch sheet. Saying no three times was the correct call. Costanza wrote the dispatch sheet for a reason; honoring it means the next four episodes inherit a clean seam instead of one already cluttered with half-done versions of their own work.
4. **Write the precedence rule three times.** XML doc, file comment, README. This is the rule a sixth-month contributor is going to misread first; redundancy is the cheapest defence. The XML doc is for the IDE pop-up. The file comment is for whoever opens the adapter source. The README is for the operator. Each one is the right surface for its audience; none replaces the others.
5. **`[Collection("ConsoleCapture")]` is mandatory whenever you `SetEnvironmentVariable`.** Learned in `PreferencesTests`, repaid here. Parallel xUnit runs without it produce flake that does not reproduce locally for whoever pulls the next branch. Kramer added it before writing the first env-mutation test; Puddy did not have to ask.
6. **Surface unknown-preset errors with the full known set.** `ResolveOrThrow` lists every known preset name *and* names the env var the operator has to fix. Compare against the alternative -- "Unknown preset: anthropic" -- which forces the operator to grep the source. Three extra string concatenations at throw-time are worth more than they cost.
7. **`[Theory]` for malformed inputs.** Parsing edge cases are precisely the shape `[Theory]` was designed for: same assertion shape, varied input. Four facts in three lines instead of four facts in twelve.

## Operator-facing examples

The user-visible surface of this episode is exactly two env vars and one phrase: "the model in your prompt is matched against `AZ_AI_COMPAT_MODELS`." Examples that should work as of this push, in order of complexity:

```bash
# Single provider, single model -- OpenAI direct.
export OPENAI_API_KEY="sk-..."
export AZ_AI_COMPAT_MODELS="openai:gpt-4o-mini"
az-ai --model gpt-4o-mini "Two-line summary of ADR-010"
```

```bash
# Two providers in one shell. The model name decides routing.
export OPENAI_API_KEY="sk-..."
export GROQ_API_KEY="gsk-..."
export AZ_AI_COMPAT_MODELS="openai:gpt-4o-mini,groq:llama-3.1-70b"
az-ai --model gpt-4o-mini "Routes to api.openai.com"
az-ai --model llama-3.1-70b "Routes to api.groq.com"
```

```bash
# Together with whitespace tolerated -- copy-pasted from a wiki page.
export TOGETHER_API_KEY="..."
export AZ_AI_COMPAT_MODELS=" together : mixtral-8x7b-instruct "
az-ai --model mixtral-8x7b-instruct "Whitespace ok"
```

```bash
# Cloudflare Workers AI -- needs the account id substituted into the URL.
export CLOUDFLARE_API_TOKEN="..."
export CLOUDFLARE_ACCOUNT_ID="abc123"
export AZ_AI_COMPAT_MODELS="cloudflare:@cf/meta/llama-3-8b-instruct"
az-ai --model "@cf/meta/llama-3-8b-instruct" "Cloudflare path"
```

```bash
# Foundry-wins precedence: same model name in both allowlists.
export AZURE_FOUNDRY_ENDPOINT="https://...foundry.../"
export AZURE_FOUNDRY_KEY="..."
export AZURE_FOUNDRY_MODELS="shared-model"
export AZ_AI_COMPAT_MODELS="openai:shared-model"
# Hits Foundry, not OpenAI -- Foundry wins by precedence.
az-ai --model shared-model "Routed via Foundry"
```

Failure modes the operator will see:

```text
$ AZ_AI_COMPAT_MODELS="openai:gpt-4o-mini" az-ai --model gpt-4o-mini "..."
[ERROR] OpenAI-compatible preset 'openai' requires env var 'OPENAI_API_KEY' but it is unset or empty.

$ AZ_AI_COMPAT_MODELS="bedrock:claude-3" az-ai --model claude-3 "..."
[ERROR] Unknown OpenAI-compatible preset 'bedrock'. Known presets: cloudflare, groq, openai, together. Set AZ_AI_COMPAT_MODELS=<preset>:<model>[,...] using one of these names.

$ AZ_AI_COMPAT_MODELS="no-colon-here" az-ai --model anything "..."
[ERROR] Malformed AZ_AI_COMPAT_MODELS entry 'no-colon-here'. Expected '<preset>:<model>' (e.g. 'openai:gpt-4o-mini').
```

All three error messages name the specific env var or preset that caused the problem and -- in the unknown-preset case -- list every valid alternative. This is the bar Mickey Abbott set in the a11y guidelines: errors that the operator can act on without consulting the source.

## Metrics

- **Diff size:** 4 files modified, 2 created.
- **Adapter LOC:** ~280 lines of code in `OpenAiCompatAdapter.cs`.
- **Tests LOC:** ~340 lines in `OpenAiCompatAdapterTests.cs`.
- **README delta:** +18 lines.
- **CHANGELOG delta:** +9 lines.
- **Writers' room delta:** +1 row.
- **Program.cs delta:** +36 lines (dispatch + doc rewrite).
- **Test delta:** unit suite was 703 facts, now 742 (+39). Integration tests unchanged at 39.
- **Preflight result:** passed (format / build / unit / integration / exec-report-check all green).
- **CI status at push time:** pending push (orchestrator-batched).
- **Targeted xUnit duration:** 85 ms for the new 39-fact class.
- **AOT impact:** no new types in `AppJsonContext`; publish-aot surface unchanged.
- **New env vars introduced:** `AZ_AI_COMPAT_MODELS`, `OPENAI_API_KEY`, `OPENAI_ORG_ID`, `GROQ_API_KEY`, `TOGETHER_API_KEY`, `CLOUDFLARE_API_TOKEN`, `CLOUDFLARE_ACCOUNT_ID` (read-only; none persisted by the CLI).
- **New CLI flags introduced:** none (deferred to S03E11 *The Wizard, Reprise* per brief).

## Credits

- **Kramer (lead).** Wrote the adapter, the tests, the dispatch wiring, the README copy, the CHANGELOG bullet, and this report. Giddyup.
- **Newman (consult, async).** Confirmed the HTTPS-only-except-loopback guard mirrors the existing pattern. No new shell or file-system surface in this episode -- `ToolHardeningTests` not exercised. Newman flagged the credential-surface growth (four new env vars now live and unredacted on the operator's machine) and queued the redactor expansion for *The Keychain* -- which is exactly where the dispatch sheet placed it.
- **Elaine (consult, async).** Reviewed the README subsection for clarity and tense. One pass, no rewrites. Flagged that the precedence rule would benefit from a mid-sentence parenthetical on first introduction; applied verbatim.
- **Costanza (off-screen).** Did not appear in this episode but his ADR-010 dispatch sheet shaped every scope decision Kramer made. The "no scope creep" instruction in the brief is Costanza's voice channelled through the brief author. Mentioned for the season-finale retrospective so the credit is on record.
- **Larry David (showrunner sign-off).** Confirmed the brief was followed, scope was held, and the next-episode handoff is clean. Larry's note: "Three pages of brief, one file of code, one episode green. That's the shape. Do it again next week."

`Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>` is on every commit in this push.

## Next episode preview -- S03E10 *The Keychain*

Newman's lead. The Compat wired the dispatch but left credentials parked on raw env vars -- four of them, growing as fast as we add presets. *The Keychain* introduces sectioned `~/.config/az-ai/env`: `[provider:openai]`, `[provider:groq]`, `[provider:together]`, `[provider:cloudflare]`, with bare `API_KEY` inside each section namespaced to the right env var on load. The redactor learns four new label types so the receipts in *The Receipt* are not the first thing to leak a token. Acceptance: the same `AZ_AI_COMPAT_MODELS` examples that ship in this README work against a sectioned env file with no per-key exports, and Newman's redactor regression suite is green on the four new label patterns. Quiet episode, high blast radius if it slips. The keychain starts tomorrow.

## Cross-references

- ADR-010 *First Non-Azure Cloud Provider* -- the decision this episode implements.
- S03E08 *The Pick* -- the decision episode that dispatched this one.
- S03E07 *The Redactor* -- Newman's posture on credential surface; informed the read-key-at-build-time choice.
- S03E06 *The Schema* -- `Preferences.Providers` shape that *The Keychain* will populate.
- ADR-007 *Third-Party HTTP Provider Security* -- the six guardrails every non-Azure preset inherits.
- ADR-005 *Foundry Routing* -- the precedent for `BuildChatClient`-as-dispatcher.
- FR-014 -- preferences schema v1, owns `Preferences.Providers`.

## Tag scene

Kramer comes out of the editing bay with the cut on a thumb drive, tosses it on Larry's desk, and points at the door. "Newman's already in there. He's been in there for an hour. He's drawing brackets on a whiteboard. *Brackets.*" Larry reviews the cut without comment, signs the slip, and slides it across. "Good episode. Tight. Don't celebrate. Tell Newman the brackets had better redact." The door swings. Cut to black. Roll credits. Hold for the post-credit two-frame static of `[provider:openai]` rendered in monospace.

## Sign-off

Sign-off block, per Mr. Wilhelm's process gate:

- [x] H1 matches `S03E09 -- *The Compat*`.
- [x] Log line under 20 words, italicized blockquote.
- [x] All five front-matter fields present.
- [x] Pitch is 2-3 paragraphs, not a bullet dump.
- [x] Scene-by-scene has three acts; Act II has the wave table.
- [x] What-shipped has all four sub-blocks.
- [x] Lessons section is not empty (seven items).
- [x] Metrics include preflight + CI state.
- [x] Credits names the cast and confirms the trailer.
- [x] ASCII clean (no smart quotes, no em-dashes).
- [x] `docs/exec-reports/s03-writers-room.md` updated with E09 row.
- [x] CHANGELOG `[Unreleased]` updated with the feat(provider) bullet.
- [x] Findings section names owners for every open item.

End of report.
