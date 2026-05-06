# S03E17 -- *The Stream*

> *Kramer audits the compat streaming path, finds it sound, pins it with 15 deterministic facts, files one ledger gap.*

**Commit:** `<staged>` (no commit yet -- DO NOT COMMIT per dispatch brief)
**Branch:** `main` (direct push pending; this episode runs in parallel with E24 *The CVE Log* and E26 *The Offline Mode*)
**Runtime:** ~25 minutes wall-clock (audit + fake extension + 15 facts + ledger row + this report)
**Director:** Larry David (showrunner)
**Cast:** 1 lead (Kramer) -- single-wave verification episode; no parallel sub-agents dispatched

## The pitch

S03E09 *The Compat* shipped `OpenAiCompatAdapter` -- the single seam that
routes any OpenAI-wire-compatible endpoint (OpenAI direct, Groq, Together,
Cloudflare Workers AI, and the eventual local-runtime crowd) to the same
`IChatClient` the rest of the binary already speaks. That episode pinned
preset resolution, `AZ_AI_COMPAT_MODELS` parsing, env-var precedence, and
dispatch routing through `Program.BuildChatClient`. What it did NOT pin --
because the agent loop was the hot path and the brief was already long --
was streaming. Tool-call delta merging. Cancellation mid-stream. The
empty-stream graceful close. The `--json` interaction. All of these work
today, but "works on Azure" plus "the seam is identical" is a hand-wave,
not a regression test.

This episode is the regression test. It is also the answer to the question
Costanza asked at the E09 sign-off: *if a Groq stream lands a tool-call as
three deltas instead of one, do we reassemble it correctly?* The honest
answer at E09 was "MAF and the OpenAI SDK do that for us, we trust them" --
which is the right answer architecturally but not the right answer when
your audit log is one column wide and reads "TRUST." This episode pins
the trust with a deterministic in-memory fake.

The episode is also a writers-room reconciliation: the original blueprint
(`s03-blueprint.md`) named E13 *The Stream*. By the time the dispatch
queue thawed, slots E13-E16 + E19 were already occupied by exec-reports
for Telemetry / Screen Reader / Allowlist / Probe / First-Hour-Local. The
brief from Larry was unambiguous: write the report at the next-free
exec-report slot (E17), note the renumber, and let the future
"blueprint-renumber" episode reconcile the slate.

## Scene-by-scene

### Act I -- Planning

One clarifying read against the dispatch brief, no pivots.

Decisions locked:

1. **Verification, not invention.** The audit came first. If the streaming
   path were broken on compat, this episode would have shipped a fix +
   tests. The audit found it sound (the `IChatClient` seam is identical
   for Azure and compat; MAF's `agent.RunStreamingAsync` calls the same
   method shape; the OpenAI SDK aggregates raw `tool_calls` deltas before
   handing `FunctionCallContent` upward). So the deliverable is
   tests-only, with the production-code section explicitly empty.
2. **Test at the IChatClient seam, not at Program.RunAsync.** The seam is
   what `OpenAiCompatAdapter.Build()` returns. Anything above it (MAF
   wrapper, agent loop, console output) is shared with Azure and already
   has coverage in `StreamingAgentLoopTests` (FR-011 regression) and the
   v2 streaming corpus. New facts target the seam.
3. **No real network. No SDK construction.** `FakeChatClient` is the
   reproducibility layer (Bania's rule from E12). Extend it; do not stand
   up a recorder.
4. **15 facts, not 12.** The brief asked for >=12. The natural fact
   inventory came out to 15. Did not pad; did not trim.
5. **Ledger the HttpClient finding.** S03E09 surfaced it in the exec
   report but never put a row in `findings-backlog.md`. The brief
   referenced `kramer-2026-05-CR-09-F3` as if it existed -- it did not.
   Fix: add the row, leave it `open` with a clear note that this episode
   did not address it.

### Act II -- Fleet dispatch

| Wave | Agents (parallel) | Outcome |
|------|-------------------|---------|
| **1** | Kramer (audit + extend FakeChatClient + write 15 facts + ledger row + this report) | Single-wave verification; no sub-agents dispatched |

Concurrency notes (per brief): two background agents in flight when this
episode shipped --

- **E24 *The CVE Log* (Jerry, CI/SBOM)** -- touches `.github/workflows/`.
  Zero overlap with this episode's surface; no coordination needed.
- **E26 *The Offline Mode* (Newman, `--offline` flag + EndpointAllowlist
  gate)** -- touches `Net/EndpointAllowlist.cs`, `Tools/WebFetchTool.cs`,
  AND `Program.cs` flag parsing. This episode touched **none** of those
  three files. The only overlap risk vector was `Program.cs` flag
  parsing, and the streaming audit found no production-code change was
  required, so the staged hunk is empty on that file. Conflict surface:
  zero.

### Act III -- Ship

- `make preflight` passed end-to-end with the new tests (see Metrics).
- No commit produced (dispatch brief: DO NOT COMMIT).
- CI not invoked (no push).
- Findings backlog row added for `kramer-2026-05-CR-09-F3` (the ledger
  was missing this row; documenting in the same staged change so a
  future reader can trace the trail from E09's exec report -> backlog ->
  this episode -> the eventual recorded-fixture episode).

## What shipped

**Production code** -- none. The audit confirmed parity. Streaming over
the compat path goes through `chatClient.AsAIAgent(...).RunStreamingAsync`
in `Program.RunAsync` (line 688), with the chat client constructed via
`OpenAiCompatAdapter.Build()` -> `new ChatClient(...).AsIChatClient()`.
The MAF wrapper does not distinguish Azure from compat at this layer; the
OpenAI SDK's stream parser merges `tool_calls` deltas before MAF sees
them. No fix was warranted. If a real bug surfaces in the field, it would
land as a follow-up episode with the same test scaffolding ready to pin
the regression.

**Tests** -- one new file, one fake extension:

- `tests/AzureOpenAI_CLI.Tests/CompatStreamingTests.cs` (new, 15 facts,
  `[Collection("ConsoleCapture")]`):
  - **Text reassembly (4 facts):** five-chunk join matches; order
    preserved across updates; empty-string deltas appear verbatim (the
    dispatch path filters them, but the seam emits them); aggregate via
    `ToChatResponseAsync` round-trips into a single concatenated message.
  - **Tool-call reassembly (3 facts):** call id survives across deltas;
    function name surfaces from the first delta; argument keys are
    visible in the union across all deltas; mixed text + tool-call
    interleaving preserves both content kinds.
  - **Cancellation (2 facts):** mid-stream `OperationCanceledException`
    injected at chunk N is observed cleanly with N earlier chunks
    successfully consumed; pre-cancelled token produces zero updates and
    a clean OCE before the first yield.
  - **Empty stream (2 facts):** zero-chunk close completes without
    throwing and emits zero updates; aggregating an empty stream into a
    `ChatResponse` produces an empty-text response.
  - **MAF agent surface parity (2 facts):** `IChatClient.AsAIAgent`
    over the fake propagates every text chunk through `RunStreamingAsync`;
    `FunctionCallContent` reaches the agent-stream consumer with the
    seeded call id intact.
  - **`--json` invariant (1 fact):** the dispatch seam emits raw text
    deltas, never a JSON envelope. `--json` is a *Program.cs* output
    formatter, not a stream shaper. If a future change wraps deltas in
    JSON at the adapter layer, this fact fails and the contract gets
    revisited.
  - **Latency budget (1 fact):** 100-chunk stream completes well under
    1000ms wall-clock with `perTokenLatency=Zero`. Bania's perf-gate
    invariant. Catches "someone added a sleep that didn't belong."
- `tests/AzureOpenAI_CLI.Tests/Benchmarks/FakeChatClient.cs` -- new
  constructor `FakeChatClient(IReadOnlyList<ChatResponseUpdate>
  streamChunks, int? throwAfterChunk = null, ...)`. The S03E12
  token-repeat constructor is unchanged; tests that already use the
  latency-knob shape continue to compile and pass without edit. The
  cancellation injection is keyed on chunk index; setting it to 0
  yields zero chunks before throwing, mirroring the
  pre-cancelled-token shape that exists in real-world `Ctrl+C` flows
  before the first byte hits the wire.

**Docs** --

- `CHANGELOG.md` `[Unreleased]` Added entry naming the episode, the
  blueprint-vs-shipped-slot reconciliation, the 15-fact inventory, and
  the deferred F3 finding.
- `docs/exec-reports/s03-writers-room.md` -- new row for E17 with the
  renumber annotation.
- `docs/findings-backlog.md` -- new row for `kramer-2026-05-CR-09-F3`
  (LOW, open, owner Kramer); references this episode as the formal
  ledger-creation point and S03E09 as the original surfacing.
- This file (`docs/exec-reports/s03e17-the-stream.md`).

**Not shipped** (intentional follow-ups) --

- **F3 itself.** The HttpClient parameter on `OpenAiCompatAdapter.Build`
  is still ignored. Closing it requires a custom `PipelineTransport`
  shim, which is the deliverable of a future "recorded-fixture
  transport" episode. The brief explicitly allowed leaving it open with
  a note.
- **Real-network parity drill.** A live Groq / Together streaming
  smoke test against a recorded fixture would close the gap between
  "fake-IChatClient says it works" and "real wire says it works." Out
  of scope for E17; queued for the recorded-fixture episode.
- **README streaming flag note.** No public-surface flag changed. README
  was not edited. The brief said "tiny update IF the public surface
  changed" -- it did not.
- **Integration tests.** `tests/integration_tests.sh` already exercises
  the live binary against an Azure endpoint when env vars are set; it
  does not need a new assertion for this episode because nothing
  user-visible changed.

## Lessons from this episode

1. **The dispatch seam is the test boundary.** Testing at
   `Program.RunAsync` means owning Console.Out plumbing, env-var
   shenanigans, and the MAF agent loop. Testing at the `IChatClient`
   returned by `OpenAiCompatAdapter.Build()` lets the fake replay
   exactly what a real provider would emit, and lets MAF and the OpenAI
   SDK be black boxes the way they were designed to be. Future
   parity-verification episodes (Anthropic when FR-024 lands, MCP
   provider when Arc 4 lands) should target the same seam.
2. **Backlog hygiene fails silently when an exec report surfaces a
   finding without writing the ledger row.** S03E09 documented the
   HttpClient gap in prose; nobody filed it. Six episodes later the
   dispatch brief referenced an ID that did not exist. The fix in this
   episode is one row in `findings-backlog.md`, but the lesson is
   process: every "Findings filed" line in an exec report needs a
   parallel ledger row in the same push, or the row never lands.
   Mr. Wilhelm should consider a `make exec-report-check` extension
   that greps for finding IDs in exec reports and confirms each appears
   in the backlog.
3. **Verification episodes are real episodes.** The audit took longer
   than the implementation. Writing 15 facts that pin a contract that
   already holds is not "no work" -- it is the work that keeps the
   contract holding when the SDK rev'd next month. Costanza's
   "everything that is not pinned will eventually unpin itself" rule
   (S03E08) applies to streaming behavior just as much as to dispatch
   behavior.
4. **The blueprint slate diverges from the shipped slate, and that is
   normal.** The fix is a future blueprint-renumber episode, not a
   retro-edit. Both files (`s03-blueprint.md`, `s03-writers-room.md`)
   should remain canonical to their respective viewpoints --
   blueprint = original plan, writers-room = what aired -- with a clear
   bridge note for any reader following one to the other.
5. **`Skip-Exec-Report` was not used here.** The dispatch brief said
   DO NOT COMMIT, so the report and CHANGELOG entry are staged. When
   the eventual commit pushes, the exec-report gate will be satisfied
   by this file. No skip trailer needed.

## Metrics

- **Diff size:** 4 files changed, 1 file created in `tests/`, 1 file
  edited in `tests/Benchmarks/`, 3 files edited in `docs/`, 1 file
  edited in repo root (`CHANGELOG.md`), this exec-report file created
  in `docs/exec-reports/`. ~340 lines added in tests, ~30 lines added
  to the fake, ~6 ledger / changelog / writers-room rows.
- **Test delta:** +15 unit tests (CompatStreamingTests). Integration
  tests unchanged. Suite count moves from 989 -> 1004 unit and stays at
  66 integration. Nothing renamed; nothing removed.
- **Preflight result:** `DOTNET_ROOT=/usr/lib/dotnet make preflight`
  passed end-to-end. Format-check clean. Build clean (one CS8620
  warning resolved before final commit by switching the FunctionCall
  args dictionary value type to `object?`). Unit tests green. New
  tests run in 98ms total. Integration tests green. Exec-report-check
  satisfied by this file.
- **CI status at push time:** n/a (no push -- DO NOT COMMIT directive).

## Credits

- **Kramer** -- audit, fake extension, 15 facts, this report, ledger
  row.
- **Larry David** (showrunner) -- episode conception and the renumber
  call.
- **Kenny Bania** -- prior-episode `FakeChatClient` author; the
  S03E17 explicit-chunk constructor sits on top of his S03E12 latency
  knobs without breaking either path.
- **Mr. Wilhelm** (process) -- credited in absentia for the lesson #2
  follow-up.

`Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>`
trailer will be on the eventual commit per the `commit` skill.

## Tag scene

> Next episode preview -- S03E18 *The Capability Gate*. Costanza wants
> a single shared seam where every provider declares what it can do --
> streaming, tool-calls, vision, JSON-mode, structured output --
> before dispatch happens. Newman wants the gate to fail-closed.
> Kramer is already three commits deep on the negotiation matrix.
