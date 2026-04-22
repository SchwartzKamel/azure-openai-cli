# FR-011: Agent Mode Streaming Output

**Priority:** P0 -- Top v1.9.0 item
**Impact:** Restores the perceived-latency win of Native AOT inside `--agent` mode
**Effort:** Small-Medium (1-2 days)
**Category:** Latency / UX

---

## Status

📋 **PLANNED** -- Targeted for v1.9.0. Assigned by Mr. Pitt as the top priority
following the v1.8.0 AOT ship. Depends on FR-006 (AOT) shipping first -- already done.

---

## Summary

Stream assistant text tokens to stdout *as they arrive* during every round of
`RunAgentLoop` -- including rounds that ultimately resolve to a tool call --
instead of buffering the full turn. Preserve correct conversation-history
semantics (tool-call fragments still accumulate) and `--raw` / `--json`
compatibility. Emit incremental round status on stderr so the user sees motion
between tool invocations.

---

## Motivation

> **Mr. Pitt:** `--agent` buffers the full turn before printing. At 40+ tokens
> per turn with multi-round tool-calling, perceived latency eats the 5.4 ms
> AOT win (v1.8 figure; v2.0.6 is 10.7 ms p50 -- see
> [`docs/perf/v2.0.5-baseline.md`](../perf/v2.0.5-baseline.md)). Top-priority
> v1.9 item precisely because it's the last big
> perceived-latency miss after AOT.

Concretely, today's `RunAgentLoop` (`Program.cs:1229-1274`):

```csharp
foreach (var part in update.ContentUpdate)
{
    if (!isToolCallRound)                  // ← gate
    {
        textBuilder.Append(part.Text);
        if (!jsonMode) Console.Write(part.Text);
    }
}
```

Two failure modes:

1. **Preamble suppression.** If a round emits text tokens *then* a tool-call
   fragment, `isToolCallRound` flips to `true` mid-stream and any subsequent
   text in that round is silently dropped. With chatty models this is where
   reasoning ("Let me check the docs…") lives.
2. **Inter-round silence.** Rounds 1..N-1 are tool-call rounds and print
   nothing to stdout. On a 3-round turn with 400 ms/round network + exec,
   the user stares at a blank terminal for >1 s before the first token of
   the final answer. AOT's sub-15 ms cold start is invisible against that.

---

## Goals

- Stream every assistant text token to stdout within ~1 frame of arrival,
  in *all* rounds, not just the final one.
- Preserve existing tool-call accumulation semantics -- `toolCallsById` must
  still rebuild complete `ChatToolCall` objects for conversation history.
- Keep `--raw` byte-identical to a cleanly concatenated final response
  (no ANSI, no status lines, no separators on stdout).
- Keep `--json` semantics: no incremental stdout; final JSON object only.
- Emit per-round tool-call progress on **stderr** (not stdout) so pipes stay
  clean and Espanso/AHK consumers are unaffected.
- Cancel-safe: CTRL+C flushes whatever has been streamed and exits cleanly
  (see FR-004 / existing `GlobalCancellation`).

## Non-goals

- No reordering of tool execution (still parallel via `Task.WhenAll`).
- No new streaming for `--json` or Ralph mode's captured `StringWriter`
  path -- those explicitly need buffered output.
- No markdown rendering during streaming (FR-005 territory).
- No change to the `CompleteChatStreamingAsync` call itself; this is a
  consumer-side fix.

---

## Design Sketch

### 1. Remove the `!isToolCallRound` gate on text output

Tool-call fragments and content fragments are *independent* on the same
`StreamingChatCompletionUpdate`. The model can (and does) emit reasoning
text before, between, or after tool-call fragments in a single round.
Stream text unconditionally; the presence of tool-call updates only affects
what we do at round-end, not whether we print tokens.

```csharp
foreach (var part in update.ContentUpdate)
{
    if (firstTextToken) { firstTextToken = false; ClearSpinner(); }
    textBuilder.Append(part.Text);
    if (!jsonMode) Console.Write(part.Text);   // unconditional
}
```

`isToolCallRound` stays -- it still governs round-end branching (execute tools
vs. finalize) -- but it no longer gates stdout.

### 2. Round separators on stderr

When a tool-call round completes and we're about to invoke tools, emit a
short delimiter so the streamed preamble is visually anchored:

```text
<streamed preamble text on stdout>
[stderr] 🔧 round 2: fs_read web_search
<streamed text from next round on stdout>
```

Delimiter goes to stderr only. Suppressed entirely when `--raw`, `--json`,
or stdout is non-TTY.

### 3. Trailing-newline normalization

Today the non-streaming path ensures a trailing `\n`. With streaming across
multiple rounds, we must:

- Track whether the last stdout byte was `\n`.
- On final round end, emit one `\n` if needed.
- On CTRL+C mid-stream, emit one `\n` to stderr *only* so the partial
  stdout stays byte-exact for pipe consumers.

### 4. `--json` path is unchanged

Guard the `Console.Write(part.Text)` with `!jsonMode` (already present).
The `textBuilder` still collects the full response for the final
`AgentJsonResponse` serialization.

### 5. Ralph mode is unchanged

Ralph calls `RunAgentLoop` with `Console.SetOut(agentOutput)` redirected
to a `StringWriter` (`Program.cs:1436-1443`). Streaming writes land in
that buffer exactly as before -- Ralph sees a fully-formed string at the
end. No code change needed; just verify the redirect still captures
everything.

---

## Risks

1. **Interleaved tool-call fragments and visible text.**
   If a round emits text → tool-call → more text, the user sees a preamble,
   then a stderr round marker, then (in the *next* round's streaming) more
   text. Mitigation: the stderr marker is cheap and clearly scoped; any
   post-tool-call text in the *same* round is rare in practice (the API
   tends to terminate the round at tool-call boundary).

2. **`--raw` contamination.**
   Any accidental stdout write of status/markers breaks Espanso/AHK
   consumers. Mitigation: funnel *all* status through a single
   `WriteStatus(string)` helper that checks `showStatus` (already gated by
   `--raw` and non-TTY), and add a regression test:
   `echo "" | az-ai --agent --raw "…" | diff - expected.txt`.

3. **Truncation on cancel.**
   CTRL+C mid-token could leave a partial UTF-8 sequence on stdout.
   Mitigation: `Console.Write` already flushes per-call; the Azure SDK
   delivers whole graphemes per `ContentUpdate.Text`. Document that
   cancelled output is "best-effort; may end mid-sentence".

4. **Token count drift in JSON mode.**
   `update.Usage` arrives on the final chunk. Since we no longer skip
   content in tool-call rounds, verify `promptTokens`/`completionTokens`
   still reflect the *final* round only (they should -- each round is its
   own streaming call).

5. **ANSI / Unicode on Windows console.**
   AOT builds on Windows may have codepage quirks. Mitigation: the existing
   streaming (non-agent) path at `Program.cs:676` already does this safely;
   reuse the same `Console.Write` semantics.

---

## Acceptance Criteria

- [ ] `az-ai --agent "<multi-tool prompt>"` prints text tokens within
      100 ms of their arrival from the API, in every round.
- [ ] Preamble text before a tool call appears on stdout (currently dropped).
- [ ] `--raw --agent` output is byte-identical to the concatenation of all
      streamed text tokens -- no status, no ANSI, no separators.
- [ ] `--json --agent` output is unchanged: single JSON object on stdout,
      no partial writes.
- [ ] CTRL+C during streaming flushes current line, writes a single
      newline to stderr, and exits non-zero per existing cancellation
      contract.
- [ ] Ralph mode (`--ralph`) captures complete output into its
      `StringWriter`; validation loop behavior unchanged.
- [ ] Tool-call execution order, parallelism, and conversation history
      semantics unchanged (existing agent integration tests pass).
- [ ] New regression test: snapshot of streamed token arrival timing
      shows no multi-hundred-ms gaps while the API is sending content.

---

## Open Questions

1. **Round-marker format.** `🔧 round 2: fs_read web_search` vs a subtler
   `· round 2 ·`? Emoji matches the existing style in `RunAgentLoop`
   (line 1287) but eats 4 bytes on stderr. Default: keep the emoji; it's
   stderr-only.
2. **Should we flush between tokens?** `Console.Out.Flush()` after each
   `Write` adds overhead but guarantees no OS-level buffering. Likely
   unnecessary on Linux TTY (line-buffered by default when TTY), but
   required when stdout is a pipe and consumer wants live tokens.
   Suggest: flush only when `!Console.IsOutputRedirected` (TTY) -- pipes
   typically batch anyway, and the consumer is usually fine waiting for
   the terminator.
3. **Inter-round blank line.** Do we emit `\n` on stdout between rounds'
   streamed text, or run them together? Running together preserves
   `--raw` purity; a blank line helps humans. Suggest: no inter-round
   newline on stdout; the stderr marker provides the visual break.
4. **Token-level telemetry.** Worth exposing a `--verbose-stream` flag
   that prints `[tok N t+12ms]` markers to stderr for perf debugging, or
   is that out of scope? Defer to FR-004 Phase 3.

---

## References

- **FR-004** -- Latency & Startup Optimization (spinner contract,
  showStatus rules, stderr-only status convention).
- **FR-006** -- Native AOT Compilation (the single-digit-ms cold-start win
  this proposal is defending -- v1.8 shipped at 5.4 ms; v2.0.6 at 10.7 ms p50).
- **FR-005** -- Shell Integration & Output Intelligence (`--raw` contract).
- `azureopenai-cli/Program.cs:1229-1314` -- current `RunAgentLoop`
  streaming block.
- `azureopenai-cli/Program.cs:676` -- reference implementation of
  clean streaming in non-agent mode.
- CTRL+C handling -- `GlobalCancellation` / `cts` linked at
  `Program.cs:35, 378, 591`.
