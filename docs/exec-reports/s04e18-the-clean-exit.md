# S04E18 -- *The Clean Exit*

> *A client that kills its server at once is a client that never lets the server say goodbye.*

**Commit:** `(this commit)` -- one code commit plus the episode report
**Branch:** `fix/s04e18-the-clean-exit` (PR, off `main`)
**Runtime:** ~1 hour wall-clock, found during the scouter investigation
**Director:** Larry David (showrunner seat, filled by a single orchestrator)
**Lead:** Jerry (DevOps: process lifecycle, what a program leaves behind)
**Cast:** 0 sub-agents, inline passes by one orchestrator. Jerry (LEAD), Kramer (the fix), Puddy (the stub servers), Frank Costanza (guest: reliability, the slow leak)

## The pitch

The owner asked whether scouter, a reverse-engineering workbench with an MCP host, could benefit from hopper. Its
MCP host speaks authenticated HTTP, and hopper's MCP client speaks only stdio, so a small bridge was needed. The
bridge worked. Then it stopped working, after exactly eight runs.

## Scene-by-scene

### Act I -- A failure that arrives on schedule

The bridge opened an HTTP session with scouter, relayed the traffic, and closed the session with an HTTP `DELETE`
when its input ended. Eight runs later the ninth failed with `HTTP 400: missing Mcp-Session-Id`, and the cause
underneath was `HTTP 429: session limit reached`. A control experiment made it exact: against a fresh server, 16
consecutive `hopper mcp tools` runs through the bridge succeeded 8 times and then failed. The server admits 8
observer sessions, so **every hopper run was leaking one**.

### Act II -- Why the cleanup never ran

The bridge's `DELETE` was reachable only when its stdin closed. Hopper never closed it.
`McpStdioClient.Dispose()` called `Kill(entireProcessTree: true)` on the server process at once: a SIGKILL, which
nothing can catch. The MCP stdio shutdown sequence is the opposite order: close the server's input, wait for it to
exit, and only then escalate. By skipping the first two steps hopper denied every server, not just the bridge, the
chance to release what it held: an HTTP session, a lock, a temp file, a child process.

Scouter did nothing wrong. A session cap with no idle expiry is a reasonable design; it simply made a latent
hopper bug visible on a schedule.

### Act III -- The fix, and what it costs

`Dispose` now closes the server's input and waits up to two seconds for it to exit on its own, then kills the whole
process tree. A well-behaved server exits in milliseconds, so the wait is not paid. A server that ignores its closed
input costs at most the grace period at exit, and is still killed.

One trade-off is accepted and written down: a server that exits cleanly but leaves its own grandchildren running (an
`npx` wrapper around a node server) is no longer followed into the tree, because the process tree is only killed when
the direct child hangs. The old behaviour tidied that case by force; the new one trusts the server, as the protocol
does.

### Act IV -- Tests, and a flaw in mine

Three hermetic tests use stub MCP servers written in a few lines of Python. Only one of them fails without the fix:
the server never sees its input close. The other two guard the new logic: a server that exits at once must not make
`Dispose` spend the grace period, and a server that ignores its input must still be killed.

Mutation-checking them found a flaw in my own test. The "never kill" mutation deliberately leaves the stub running,
and the test found its stub by script *name*, so the next run latched onto that orphan from the previous one and
failed. The clean run after the mutations was red for a reason that had nothing to do with the code under test. The
test now kills the stub in a `finally` if the fix ever regresses.

A second flaw was found by CI, not by me: the test still located its stub with `Process.GetProcessesByName("python3")`,
which is how the process is named on my machine and not on the runners (macOS calls it `Python`), so it found nothing and
both platforms failed. The stub now writes **its own PID** to a file, which is deterministic everywhere. The "is it gone"
check also treats a killed-but-not-yet-reaped zombie as gone, since `GetProcessById` can still return one.

## What shipped

- `McpStdioClient.cs`: a graceful shutdown (close the input, wait, then kill), with the grace period an internal
  constant tests can shorten.
- `McpClientShutdownTests.cs`: 3 tests (unit 2,232 to 2,235). **Mutation-checked:** three mutations (do not close the
  input; never kill; always spend the whole grace period), each caught, and each edit verified to have applied.
- CHANGELOG, the findings backlog and this report.

**Not shipped**

- HTTP transport for the MCP client (`kramer-2026-11-K-8`): the bridge is the workaround, and a URL given to
  `--mcp-server` still produces a misleading "error trying to start process".

## Lessons from this episode

1. **A leak that arrives on schedule is a gift.** Eight runs is a number you can reproduce, and a control
   experiment (8 of 16) turned a hunch into a measurement before any code changed.
2. **Cleanup that depends on the other side being polite is not cleanup.** The bridge did the right thing and the
   caller made it impossible.
3. **Follow the protocol's own sequence.** The shutdown order was already written down; the code skipped to its last step.
4. **State the trade-off.** Trusting a server to clean up its own children is a choice, so it is written in the report.
5. **A mutation can poison the next test run.** If a mutation leaves a process behind, an unscoped test finds it.
6. **Do not find a process by guessing.** Let the process report its own identity; names differ across platforms.

## Metrics

- **Diff:** 1 production file, 1 new test file, CHANGELOG, the findings backlog, the report and the index row.
- **Control experiment:** 8 of 16 runs succeeded before; 24 of 24 after the bridge stopped depending on cleanup (see
  S04SP12), and hopper itself now allows the polite path.
- **Test delta:** unit 2,232 to 2,235 (+3). Integration 174, unchanged.
- **Preflight:** format, 0 warnings, unit, integration, `ascii-check`, `markdownlint`, `exec-report-check`.
- **CI status at push time:** pending at commit time; link in the PR.

## Credits

Jerry led because the subject is what a process leaves behind. Frank's lens is the slow leak that fails on a schedule.
Puddy's is the stub servers and the lesson about scoping a test's search. Kramer owns the three lines that were wrong.
Commits carry `Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>`.
