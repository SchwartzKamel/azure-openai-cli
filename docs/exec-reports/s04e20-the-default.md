# S04E20 -- *The Default*

> *A setting nobody asked for, rejected by a model that had a good reason, failed every call until someone learned to type `--temperature 1`.*

**Commit:** `(this commit)` -- one code commit plus the episode report
**Branch:** `fix/s04e20-the-default` (PR, off `main`)
**Runtime:** one session, the first of the gap-fixing batch
**Director:** Larry David (showrunner seat, filled by a single orchestrator)
**Lead:** The Maestro (prompt engineering and model behaviour)
**Cast:** 0 sub-agents, inline passes by one orchestrator. Maestro (LEAD), Kramer (the decorator), Puddy (what must not be retried), Morty (guest: a failed round trip per agent round is money)

## The pitch

S04SP11 found that two of the three Azure deployments on the owner's server answered hopper with HTTP 400 on
every call: `gpt-5-nano` and `gpt-5.2-chat` accept only the default temperature, and hopper always sends one
(its default is 0.55). The workaround, `--temperature 1`, was known to exactly one person.

## Scene-by-scene

### Act I -- Why not a list

The obvious fix is a list of models that take no temperature. It would be wrong within a release: the names
(`gpt-5-nano`, `gpt-5.2-chat`) do not follow a pattern (a sibling, `gpt-5.4-mini`, accepts it), and every provider
changes this without notice. The registry could not know, so the rule is reactive: on that one error, retry once
without the setting.

### Act II -- One place to put it

Every path builds its client through `Program.BuildChatClient`: one-shot, agent, Ralph, the queue, eval and the
doctor probe. It has ten return points, so editing each would have been ten chances to miss one. The method was
split: the old body is `BuildChatClientCore`, and `BuildChatClient` wraps the result in a decorator.

### Act III -- Narrow on purpose

A retry rule that is too broad is worse than the bug. The decorator retries only when **all** of these hold: the
exception is an SDK request error, its message names `temperature` and says it is unsupported, there was a
temperature to drop, and it has not already retried. Any other 400 (a bad schema, a bad parameter) still surfaces
as the error it is. It never mutates the caller's options, because the next agent round reuses them, and it
remembers a model that has refused, so an agent loop pays for one failed round trip, not one per round.

Streaming needed one more rule. The rejection arrives with the first read, before anything has been yielded, so
retrying cannot repeat output. A failure after output has started is never retried, and a test uses the exact
rejection text to prove it.

### Act IV -- Proof against the real service

Against the same three live deployments, default settings:

| Deployment | Old binary | New build |
|---|---|---|
| `gpt-5.4-mini` | answers | answers, no note |
| `gpt-5.2-chat` | HTTP 400 | answers, one note |
| `gpt-5-nano` | HTTP 400 | answers, one note |

In a multi-round agent run on `gpt-5-nano` the note appeared once, which is the memory working.

### Act V -- A race I would otherwise have inherited

One of my tests builds a client through `BuildChatClient`, which means setting an environment variable. That is the exact
shape of the race suspected behind the unexplained CI failure in `ProviderDispatchTests` (S04E15's PR): classes that
mutate process-wide environment with no `[Collection]`, running in parallel with the ones that do. I should not add a
test with that flaw. The wiring test has its own class in the serial `ConsoleCapture` collection, and
`ProviderDispatchTests` and `EndpointAllowlistTests` joined it.

Two integration cases then failed once each on a machine at a load of 20 to 42 (completions output, `--help` output) and passed on
rerun, 0 of 130 times in isolation. Memory was not the cause. Their failure messages said only "missing", so they now carry the captured
length and its first characters (`puddy-2026-11-P-4`).

That closes the suspected race by construction. It is **not** claimed as the fix for the CI failure, which was never
reproduced; `puddy-2026-11-P-3` stays open until CI stays clean.

## What shipped

- `Resilience/TemperatureTolerantChatClient.cs` (new) and the wrapper in `Program.BuildChatClient`.
- `TemperatureTolerantChatClientTests.cs`: 13 tests (unit 2,235 to 2,248): the retry, the options kept, the options not
  mutated, the memory (and that it is per model), a model that accepts its temperature, a different 400, a non-SDK
  error, no temperature to drop, a second failure, streaming on the first read, streaming after output, and the
  wiring. **Mutation-checked:** nine mutations, each caught, each edit verified to have applied.
- Two existing test classes moved into the serial collection.
- The field guide, the config reference, CHANGELOG, the findings backlog and this report.

**Not shipped**

- A registry flag for "takes no temperature". The reactive rule needs no list.
- The empty-reply problem (`kramer-2026-11-K-6`): the next episode.

## Lessons from this episode

1. **Prefer a rule that reacts to the error over a list that predicts it.** The list is wrong by the next release.
2. **Wrap at the one place everything passes through.** Ten return points are ten chances to miss one.
3. **Say what you will not retry.** Most of the tests are about the cases that must still fail.
4. **Do not retry after output has started.** A retry that repeats output is a new bug.
5. **Check against the real service.** A unit test proves the logic and a live call proves the original failure is gone.
6. **Do not add a test that shares a known race.** Put it in the serial collection and say what that does and does not prove.

## Metrics

- **Diff:** 1 new production file, a wrapper in `Program.cs`, 1 new test file, 2 existing test classes gaining an
  attribute, the guide, the config reference, CHANGELOG, the findings backlog, the report and the index row.
- **Live result:** 2 of 3 deployments failed before, 0 of 3 after.
- **Test delta:** unit 2,235 to 2,248 (+13). Integration 183, unchanged.
- **Preflight:** format, 0 warnings, unit, integration, `ascii-check`, `markdownlint`, `exec-report-check`.
- **CI status at push time:** pending at commit time; link in the PR.

## Credits

Maestro led because the subject is how models actually behave, as opposed to how their documentation says they do.
Kramer wrote the decorator. Puddy's lens is the list of what must still fail. Morty's is the failed round trip per
agent round. Commits carry `Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>`.
