# S04SP11 -- *The Field Notes*

> *We asked what hopper is for on a real server, and answered it by using it. Most of the ideas did not survive contact.*

**Commit:** `(this commit)` -- docs, an example script and its tests
**Branch:** `docs/s04sp11-field-notes` (PR, off `main`)
**Runtime:** one long session, interleaved with S04E17
**Director:** Larry David (showrunner seat, filled by a single orchestrator)
**Lead:** Sue Ellen Mischke (competitive and positioning: what is the tool actually good for)
**Cast:** 0 sub-agents, inline passes by one orchestrator. Sue Ellen (LEAD), Costanza (guest: product), Newman (guest: the MCP gap), Maestro (guest: the eval results)

## The pitch

The owner asked how hopper could be used for real with `wgm`, `smith` and the other utilities on the server.
A list of plausible integrations would have taken ten minutes and would have been mostly fiction. This special
did the slower thing: survey the machine, find a model, and try each candidate, keeping only what produced a
checkable result.

## Scene-by-scene

### Act I -- The machine, and a correction

The server runs an agent-orchestration platform (a plan/build/test/review loop, a deliberation engine, a
Discord bot, a dashboard), a GitHub Actions runner, Docker and Podman, a coding-agent harness, a skill
system and cron-driven ops scripts with an MCP server.

I had told the owner there were "no real credentials on this machine". That was wrong. I had checked
hopper's own configuration and the environment, and had not looked at the agent platform's. Its configuration
held Azure OpenAI deployments. Correcting it was the first result. The credential was read into a single process's
environment for small calls and was never printed, copied or saved. The first attempt used it against the
wrong one of the owner's two Azure resources (HTTP 401) before the right one answered, so it was sent to both. The guide tells readers to
supply credentials the supported way instead.

### Act II -- Ideas that did not survive

- **GitHub Models.** Two endpoints: one returned HTTP 200 with the body `OK` to every request, including
  unauthenticated ones, and the other did not connect. A 200 from a host you have not looked at is not success.
- **Compressing the agent platform's logs.** The directory was 3.2 GB, so it looked like a fit. The run
  directory was 20 KB; the bulk was build worktrees and binaries. Nothing to compress.
- **Attaching the machine's CI runner to this repository.** It serves a different repository, and this one is
  public.
- **A backlog triage over the platform's plan.** Ten items. A toy, so left out.

### Act III -- Ideas that did

Each was run against the real thing and checked against the source:

- an MCP ops question, answered correctly in 19 s (every fact verified against the tools' own output);
- `wgm`'s docs audit with hopper as the agent: 35 s with a mid-tier model, RED with the cheapest;
- a second reviewer: 18 of 18 seeded bugs on an easy benchmark, and on a hard one a spread from 47% to 100%;
- the eval harness against a live model for the first time.

### Act IV -- What using it found in hopper

Real use found four gaps that no test had: two of three deployments reject the default `temperature`; the MCP
client has no way to limit which of a server's tools the model sees; a safety fixture blocked by the platform's
content filter is reported as an error, so the gate can never pass there; and a reasoning model with a small
`--max-tokens` returns nothing and exits 0. Separately, the reviewer experiment found a real bypass in the
S04E15 shell guard (S04E17).

### Act V -- The stopgap, made real

The read-only MCP proxy was scaffolding in `/tmp`. Because it is the mitigation for the MCP gap, it became an
example with a policy that must be set (it refuses to start with none), allow and deny patterns where deny
wins, and five hermetic integration tests whose upstream is hopper's own MCP server. Five mutations were each
caught, and the harness checked that every edit applied.

## What shipped

- `docs/agent-ecosystem-field-guide.md`: the recipes, the numbers and the "what not to do".
- `examples/mcp/read-only-proxy.py` and five integration tests (169 to 174).
- Four findings opened and one updated (below), and `docs/README.md` links the guide.

**Not shipped**

- Fixes for the four hopper gaps. They are findings with a proposed shape; each is a PR of its own.
- Any change to the owner's `wgm`, `smith` or `pi` configuration. Everything was run from the command line.

## Findings opened

`kramer-2026-11-K-5` (default temperature rejected), `kramer-2026-11-K-6` (empty reply, exit 0),
`newman-2026-11-N-11` (MCP client exposes every tool), `maestro-2026-11-M-5` (content filter is an error),
and `jerry-2026-11-JE-2` updated (the live path has now run once, by hand).

## Lessons from this episode

1. **Check where credentials already are before saying there are none.** A claim of absence is only as good as
   the search.
2. **Measure the premise before building on it.** Two of the four candidate ideas died on a one-line `du`.
3. **A 200 is not success.** Read the body.
4. **Real use finds what tests do not.** Four gaps, none of which any test could have named.
5. **A mitigation should be tested like a feature.** The proxy has a policy-required start-up and five tests.
6. **Say what you did with a credential.** In memory, small, never persisted, and told to the owner.

## Metrics

- **Diff:** a guide, one example script, the integration tests, the report, the index row, the findings, and
  a docs index link.
- **Test delta:** integration 169 to 174 (+5), 2 skipped; unit unchanged at 2,232.
- **Model calls:** a few hundred, all small, on one Azure resource.
- **Preflight:** `ascii-check`, `markdownlint`, `exec-report-check`, `scripts-ignore-check`, integration.
- **CI status at push time:** pending at commit time; link in the PR.

## Credits

Sue Ellen led because the question was positioning: what is this for. Costanza's lens is the "what not to do"
list. Newman's is the MCP finding and the refusal to start without a policy. Maestro's is the eval numbers.
Commits carry `Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>`.
