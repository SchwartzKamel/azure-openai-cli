# S04SP12 -- *The Bridge*

> *The server spoke HTTP. The client spoke stdio. Neither was wrong, and a forty-line script made them a pair.*

**Commit:** `(this commit)` -- docs, an example script and its tests
**Branch:** `docs/s04sp12-the-bridge` (PR, off `main`)
**Runtime:** one session, following S04SP11
**Director:** Larry David (showrunner seat, filled by a single orchestrator)
**Lead:** Bob Sacamano (integrations: making two things that were not built for each other work together)
**Cast:** 0 sub-agents, inline passes by one orchestrator. Bob (LEAD), Newman (guest: roles, tokens, least privilege), Puddy (guest: the stand-in server), Frank Costanza (guest: the session leak)

## The pitch

The owner pointed at scouter, a reverse-engineering workbench with an MCP host, and said it could benefit. The
question was in which direction, and whether hopper could reach it at all.

## Scene-by-scene

### Act I -- Read before running

Scouter is a Rust workspace that offers LLM-assisted reverse engineering of native programs, including memory
scanning and process injection on Windows. Before running anything, its own rules and the shape of its MCP surface
were read: a 256-tool registry in three tiers (SAFE, CONFIRM, DANGEROUS), but only a three-tool offline profile
and an eleven-tool shared-probe profile are exposed over MCP, each behind per-role bearer tokens. Only the
offline profile could run on this Linux server, and it analyses executable files without touching a process. That
is the only part used.

### Act II -- Two mismatches, one cheap and one not

The first was expected. Scouter's hosts are authenticated HTTP; hopper's MCP client launches a command, so a URL
fails with "error trying to start process 'http://...'". A bridge is the answer, and the bridge was the easy part.

The second was not. After the bridge worked, it stopped working on the ninth run. The cause was in hopper, not in the
bridge or in scouter (S04E18): every run had leaked a session.

### Act III -- My own misreadings

Three small ones, each corrected by reading the server rather than guessing:

- A raw hex buffer was refused ("unsupported binary format"). The tool wants an executable *image*, which is what its
  description says. A small ELF was compiled instead, so the ground truth was known.
- A second controller call returned HTTP 403. The docs say the host serves one controller, and my first session was
  never closed. The server was enforcing its documented limit.
- The first model run quoted an address that was wrong. Scouter's output contained the right one and never the wrong
  one, so the error was the model's decimal-to-hex conversion, which then became a measured result (2 of 5 against
  5 of 5) and a suggestion for scouter.

### Act IV -- Make the workaround survive its caller

The bridge's first design cleaned up its session when its input ended. Hopper killed it before that could run, so
it leaked. Until hopper's shutdown was fixed, the bridge needed to be safe against being killed, so its default became
one session per request, opened and closed within the same call. That meant three extra loopback round trips per
request and no leak: 24 of 24 runs against a cap of 8. The old behaviour remains as `sticky` mode for a client that
shuts down politely, which hopper now does.

### Act V -- A test that tested the wrong thing

The mutation run on the bridge caught four of five. The survivor was "leak the token into an error message": hopper's
own secret redactor masked the leaked `token=...` before it was printed, so the test saw nothing wrong. A test that
passes because *another layer* hid the defect proves nothing about this one. It now drives the bridge directly and
inspects its raw output, and the mutation is caught.

## What shipped

- `examples/mcp/http-bridge.py`: a stdio-to-HTTP MCP bridge with a token file, two session modes and no token in any output.
- Six integration tests (174 to 180) against a stand-in HTTP MCP server with a session cap of two: tools list, a call
  round trip, six runs with no leak, sticky mode releasing its session, a wrong token refused with no token printed,
  and a usage error. **Mutation-checked:** five mutations, all caught, each verified to have applied.
- A scouter recipe in the field guide, with the measured results and what was *not* tried.
- Findings, and the report.

**Not shipped**

- Any change to scouter. The suggestions are written down for its owner.
- The probe DLL, process injection, live processes, or anything Windows-only. None was run.
- An HTTP transport inside hopper (`kramer-2026-11-K-8`).

## Lessons from this episode

1. **Read a tool's rules before running it,** especially one with a DANGEROUS tier. Use only the part that fits.
2. **Build the ground truth yourself.** A binary you compiled lets you grade every claim.
3. **Least privilege made concrete:** one controller call, made outside hopper (by me, at the owner's direction, standing
   in for a person); every later call used a read-only token, with the write tool hidden from the model.
4. **A workaround should not depend on its caller behaving.** Per-request sessions survive being killed.
5. **When a test passes because another layer hid the bug, test that layer's output directly.**
6. **Report what you did not touch.** Everything here was offline analysis of a program I compiled.

## Metrics

- **Diff:** an example script, the integration tests, the guide, the report, the index row and the findings.
- **Graded runs:** 10 (5 per model). **Bridge leak test:** 24 of 24 runs against a session cap of 8, after 8 of 16
  with the first design.
- **Test delta:** integration 174 to 180 (+6), 2 skipped; unit unchanged.
- **Preflight:** `ascii-check`, `markdownlint`, `exec-report-check`, `scripts-ignore-check`, integration.
- **CI status at push time:** pending at commit time; link in the PR.

## Credits

Bob led because the subject is integration: two programs that were not designed for each other. Newman's lens is the
one-controller, many-observers split and the token file. Puddy's is the stand-in server with a cap of two, which
turns an invisible leak into a failing test. Frank's is the leak that fails on a schedule. Commits carry
`Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>`.
