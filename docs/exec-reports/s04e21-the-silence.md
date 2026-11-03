# S04E21 -- *The Silence*

> *The model had nothing to say, so it said nothing, and the exit code said that was fine.*

**Commit:** `(this commit)` -- one code commit plus the episode report
**Branch:** `fix/s04e21-the-silence` (PR, off `main`)
**Runtime:** one session, second of the gap-fixing batch
**Director:** Larry David (showrunner seat, filled by a single orchestrator)
**Lead:** Frank Costanza (reliability: a failure that looks like success)
**Cast:** 0 sub-agents, inline passes by one orchestrator. Frank (LEAD), Kramer (the check), Puddy (the cases that must not change), Mickey (guest: what a text expander does with an empty string)

## The pitch

S04SP11 found it by accident. `gpt-5-nano` with `--max-tokens 16` or `64`: hopper printed nothing and exited 0. A reasoning model spends
its token budget on hidden reasoning, and with a small budget it spends all of it before writing a word. The service says so
(`finish_reason: length`), and hopper threw that away. A text expander inserts an empty string. A pipeline passes on an empty
string. Exit 0 told every caller that everything went well.

## Scene-by-scene

### Act I -- The signal was already there

`AgentResponseUpdate` carries a `FinishReason`, and the streaming loop in `Program.cs` never read it. A throwaway test listed the
properties rather than guessing: the field exists on the agent's update directly, with the values `Stop`, `Length`, `ToolCalls`
and `ContentFilter`. Against the live service the streaming path does populate it.

### Act II -- Decide narrowly

The rule is easy to get wrong in both directions. An **empty reply that stopped at the limit** is an error: the caller asked for an
answer and got none, and the cause is something they can fix. An **empty reply the content filter withheld** is an error too, and the
message must not suggest raising `--max-tokens`, because that would not help. But a model that **stopped normally with nothing to
say** is not an error: hopper cannot know that nothing was the wrong answer. And a reply that **has text** is never this check's
business, even when it was cut off (see below).

The decision is a pure function, `DescribeEmptyReply`, so it can be tested without a model.

### Act III -- Prove it against the real thing

On the live `gpt-5-nano` deployment with `--temperature 1`:

| `--max-tokens` | Old binary | New build |
|---|---|---|
| 16 | empty, exit 0 | exit 1, names `--max-tokens 16` |
| 64 | empty, exit 0 | exit 1, names `--max-tokens 64` |
| 1500 | `pong`, exit 0 | `pong`, exit 0 |

`--json` gets a structured error. An empty reply is also never stored in the response cache, because the check returns before it.

With S04E20 merged underneath, the same deployments at **fully default settings** (no `--temperature`):

| Deployment | `--max-tokens 16` | `--max-tokens 1500` |
|---|---|---|
| `gpt-5-nano` | note about temperature, then exit 1 naming the limit | note, then `pong` |
| `gpt-5.2-chat` | note, then `pong` (no hidden reasoning to exhaust) | note, then `pong` |
| `gpt-5.4-mini` | `pong`, no note | `pong`, no note |

The two fixes compose: the first removes the setting the model refuses, the second reports the real problem behind it.

### Act IV -- Which layer sees which defect

A unit test of the helper cannot tell whether the loop recorded the right things, and an end-to-end test cannot cheaply cover every
branch. Both exist. The end-to-end test needed a fake model that **streams**, which the existing fake (plain JSON) could not do, so
the case has a small server-sent-events server whose answer is chosen by the prompt: empty and limit, empty and content filter,
empty and stop, partial and limit, and a normal answer.

The mutation check showed why both are needed. Breaking the loop's `anyText` tracking passes every unit test; only the
integration case "a truncated but non-empty answer is unchanged" catches it. Breaking the finish-reason tracking or the call site
likewise passes the unit tests, and the integration cases catch them.

## What shipped

- `DescribeEmptyReply` and the check in the one-shot and agent streaming path of `Program.cs`.
- `EmptyReplyTests.cs`: 10 unit cases (unit 2,235 to 2,245).
- Six integration cases against the streaming fake (183 to 189).
- **Mutation-checked:** nine mutations (five unit-level, four loop-level), each caught, each edit verified to have applied.
  One mutation was refused by the harness first because its anchor was wrong; it was fixed and rerun.
- The field guide, CHANGELOG, the findings backlog and this report.

**Not shipped (and why it matters)**

- **A truncated but non-empty answer is still silent** (`kramer-2026-11-K-9`). `--raw --max-tokens 50` on a long answer prints
  half of it and exits 0, which is the same class of failure for a text expander. It was not folded in because the right behaviour is
  a decision (a warning, or an error, or a flag), not an obvious fix. An integration case pins the current behaviour so changing it
  is a deliberate act.
- **Ralph and Commander** have their own streaming loops that never read a finish reason (`K-10`).
- An Azure content-filter rejection that arrives as an HTTP 400 is a different path (`maestro-2026-11-M-5`).

## Lessons from this episode

1. **A failure that exits 0 is the expensive kind.** Nothing downstream can tell.
2. **Read the field the service already sends.** The finish reason was on every update and was thrown away.
3. **State what you are not changing, and pin it.** The "truncated but non-empty" case exists to make that boundary visible.
4. **Test the layer under test.** Two of the mutants passed every unit test.
5. **A mutation harness that refuses is working.** A mutation with a wrong anchor was refused instead of counted.

## Metrics

- **Live result:** 2 of 3 small-budget calls were silent failures before, 0 after; the 1500 case unchanged.
- **Test delta:** unit 2,235 to 2,245 (+10); integration 183 to 189 (+6).
- **Preflight:** format, 0 warnings, unit, integration, `ascii-check`, `markdownlint`, `exec-report-check`.
- **CI status at push time:** pending at commit time; link in the PR.

## Credits

Frank led because the subject is the signal that separates a failure from a success. Kramer wrote the check, Puddy wrote the cases
that must not change, and Mickey's lens is the text expander that inserts an empty string. Commits carry
`Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>`.
