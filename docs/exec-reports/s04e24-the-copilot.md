# S04E24 -- *The Copilot*

> *Hopper learns to talk to GitHub Copilot, and stops paying for the same mistake twice.*

**Commit:** `(this commit)` -- one code commit plus the episode report
**Branch:** `feat/s04e24-the-copilot` (PR, off `main`)
**Runtime:** one session, with live probes against the Copilot API
**Director:** Larry David (showrunner seat, filled by a single orchestrator)
**Lead:** Cosmo Kramer (providers: one more preset, no new adapter)
**Cast:** 0 sub-agents, inline passes by one orchestrator. Kramer (LEAD), Newman (guest: the token command and the headers), Morty (guest: per-model prices), Puddy (guest: the live runs)

## The pitch

The operator already pays for GitHub Copilot, which fronts GPT-6, Claude, Gemini and Grok behind one OpenAI-style API
billed per token. Hopper could not reach it. Copilot needs a GitHub token instead of an API key, a set of client
identification headers on every request, and -- the part that decides everything -- it serves the cheapest and best
GPT models (GPT-6 Luna at $0.10 per million input tokens, GPT-6.1 Terra at $1) only on the Responses API, never on
`/chat/completions`. A preset that only spoke chat would have reached Claude and missed the models worth using.

ADR-010 forbids a per-vendor adapter for anything that speaks the OpenAI wire protocol, so Copilot arrives as a
preset built from four new generic knobs: fallback key env vars, a key command, static headers, and a list of
Responses-only model patterns. Two things surfaced only in live runs. A multi-model `AZUREOPENAIMODEL` vetoed every
model routed through `AZ_AI_COMPAT_MODELS`, so no compat preset had ever been reachable in that setup. And the
temperature-refusal memory from S04E20 lasted one process, so every one-shot call to a reasoning model paid a
failed round trip first.

## Scene-by-scene

### Act I -- What Copilot actually serves

The Copilot `/models` catalog settled the design: 14 chat models list only `/responses` (every GPT-5.3-codex,
GPT-5.4, GPT-5.6, GPT-6.x and Grok model), 2 list both, and Claude, Gemini and older GPT models list only
`/chat/completions`. A throwaway probe against the real API confirmed that the OpenAI SDK's `ResponsesClient`,
wrapped by `Microsoft.Extensions.AI` as an `IChatClient`, works for GPT-6 Luna and GPT-6.1 Terra, and that the chat
client works for Claude Haiku 5.5, streaming included. That fixed the shape: one preset, two client types chosen per
model.

### Act II -- Fleet dispatch

| Wave | Agents (parallel) | Outcome |
|------|-------------------|---------|
| **1** | Kramer | Preset knobs, `copilot` preset, Responses-vs-Chat dispatch, capability profile, provider-name lists |
| **2** | Newman, Morty | Token command (no shell, 5 s timeout, never logged), header policy, per-model list prices |
| **3** | Puddy | Live runs: allowlist veto found and fixed; agent tool loop failed on `previous_response_id`, fixed with stateless Responses; temperature memory persisted |

Two live failures shaped wave 3. The first live call stopped at hopper's own allowlist: the operator's
`AZUREOPENAIMODEL` lists three Azure deployments, and the check ran before routing, so it rejected `gpt-6-luna`
although `AZ_AI_COMPAT_MODELS` named it. The README already promised the opposite. Models named in
`AZ_AI_COMPAT_MODELS` or `AZURE_FOUNDRY_MODELS` now pass; each is still an explicit operator opt-in.

The second came from agent mode. `Microsoft.Extensions.AI`'s Responses client is stateful by default: after the
first turn it sends only the new messages plus `previous_response_id`, and Copilot answers
`previous_response_id is not supported`. A probe reproduced it, then showed that `store: false` makes the response
carry no conversation id, so the tool loop resends the whole history the way it does on `/chat/completions`. The
Responses path now always runs that way.

### Act III -- Ship

Preflight ran in a clean worktree of the commit, because 487 untracked Smith diary files in the operator's checkout
(`docs/diary`, `docs/img`) fail docs-lint locally though CI never sees them. The native AOT build was checked live as
well, because AOT is what `make install` ships and the Responses client is new to this binary. Then the commit on
`feat/s04e24-the-copilot` and a PR.

## What shipped

- **Production code** --
  `OpenAiCompatAdapter.cs`: four optional preset knobs (`ApiKeyFallbackEnvVars`, `ApiKeyCommand`, `StaticHeaders`,
  `ResponsesModelPatterns`), the `copilot` preset, `ResolveApiKey` (env, fallback env, command), `UsesResponsesApi`,
  `StaticHeaderPolicy`, and `StatelessResponsesChatClient` (sets `store: false` on a copy of each call's options).
  `CopilotHeaders` is declared before `BuiltIn` because static initializers run in textual order; a test pins it.
  `Program.cs`: `IsModelAllowed` lets routed models through the `AZUREOPENAIMODEL` gate at both call sites; `copilot`
  added to the help text, the `--provider` hint, the zsh and fish completions and the known config sections.
  `ProviderCapabilities.cs`: Copilot profile (tools, streaming, vision, JSON). `CompatCostRates.cs` and
  `CostEstimator.cs`: per-model Copilot list prices, snapshot 2026-11-03. `Preferences.cs` and `FallbackPolicy.cs`:
  `copilot` in the known-provider sets. `TemperatureTolerantChatClient.cs`: an optional `memoryPath`; the CLI passes
  `temperature-rejecting-models` in the prompt-cache directory (created 0700), bounded to 256 names and 32 KiB, only
  plain identifier names written or read, best effort on I/O errors.
- **Tests** -- `CopilotPresetTests.cs`, 40 cases: preset shape, header initialization order, existing presets
  untouched, credential precedence (command stubbed, never a real `gh`), Responses-vs-Chat dispatch for 14 model
  ids, `GetService` proof of which client was built, default-model and endpoint overrides, stateless options without
  mutating the caller's, capability profile, list prices, cost-estimate label, the allowlist rule including a
  malformed compat list, dispatch telemetry. `TemperatureTolerantChatClientTests.cs`: 8 more cases for the
  cross-process memory (a fresh process skips the failed round trip; no file means nothing written; junk and unsafe
  lines ignored).
- **Docs** -- README "GitHub Copilot" subsection; CHANGELOG entries under Added and Fixed; this report and its index
  row; four backlog rows.
- **Not shipped** -- the Responses-only list is a static snapshot (`kramer-2026-11-K-11`); the client headers present
  hopper as VS Code Copilot Chat, and Copilot's API is not documented for third-party clients
  (`newman-2026-11-N-13`); the per-model prices are a dated snapshot (`morty-2026-11-M-2`); the temperature memory
  is keyed by model name only, across providers (`kramer-2026-11-K-12`).

## Lessons from this episode

1. The catalog decided the design before any code did. Reading Copilot's `/models` first showed that a chat-only
   preset would have reached the expensive models and missed the cheap ones.
2. Unit tests passed while the binary could not answer: the allowlist veto lived in `Program.Main`, above the seam
   the tests exercised. Only the live run found it, and the README example it contradicted had been wrong since
   S03E09.
3. "Stateful by default" is a library choice, not a protocol fact. The single-turn probe passed; only a tool loop
   exercised `previous_response_id`. Probe the multi-turn path before trusting a new client type.
4. A process-lifetime memory is no memory for a one-shot CLI. S04E20's design was right for agent loops and
   invisible for single calls; persisting it is what made the saving real.
5. Static field initializers run in textual order. `BuiltIn` referenced `CopilotHeaders` declared below it, which
   would have shipped a preset with null headers; it was caught in review and a test now pins it.

## Metrics

- Diff size: 15 files, about 1,000 insertions and 20 deletions (code, tests, docs).
- Test delta: +48 cases (40 in `CopilotPresetTests`, 8 in `TemperatureTolerantChatClientTests`).
- Live, AOT binary, loaded host (load average about 20 on 8 cores), single runs: GPT-6 Luna 1.46 s, Claude Haiku 5.5
  1.14 s for a one-word reply; agent mode with a `datetime` tool call: Luna 2.75 s, Terra 2.82 s, Haiku 1.75 s.
  Medians of 4 runs on the JIT build: Luna 1.94 s, Haiku 1.77 s; the failed temperature round trip it removes cost
  about 0.75 s per call. Startup: AOT 11 ms, `gh auth token` 67 ms when no token env var is set.
- Preflight: `make preflight` in a clean worktree of the commit passed format, color-contract, docs-lint, ascii-check,
  scripts-ignore-check, build and all 2,337 unit tests. Its integration step failed 8 cases, all `You must install
  .NET`: those cases run the binary under `env -i`, which drops `DOTNET_ROOT`, and this host's .NET is user-local.
  Rerun against the AOT binary (`BIN=... bash tests/integration_tests.sh`): 204 passed, 0 failed. `eval-lint` and
  `exec-report-check` passed.
- CI status at push time: pending at the time of writing.

## Credits

Kramer drove the preset and dispatch; Newman the token command and headers; Morty the price table; Puddy the live
runs that found both production-only failures. The commit carries the `Co-authored-by: Copilot` trailer.
