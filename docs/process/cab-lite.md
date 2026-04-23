# CAB-lite -- the lightweight Change Advisory Board

> *"Change Advisory Board" is the enterprise IT term. Ours is lighter,
> faster, and named after the Penske file Wilhelm forgot to brief us
> on.*

Owned by **Mr. Wilhelm**. Convened by the showrunner or the change
author. Standing seats vary by topic; the standing *role*-set does
not.

A traditional CAB is a weekly meeting where every change waits for
sign-off before merge. That model breaks at solo-maintainer scale
and at sub-agent dispatch latencies. CAB-lite is the same idea,
sized down: **a named consult triggered by change class or topic, run
async, recorded in the exec report or a comment on the PR, never a
meeting.**

---

## 1. When CAB-lite is required

You owe a CAB-lite consult when a change crosses **two or more** of
these surfaces, or hits **any one** of the security / supply-chain
triggers:

| Trigger | Required consult |
|---------|-----------------|
| New external network call (provider, CDN, telemetry endpoint) | Newman (security) + Frank (reliability / SLO) |
| New dependency (NuGet, npm, OS package) | Jackie (license) + Newman (supply chain) |
| Base image change in `Dockerfile` or any container | Newman + Jerry |
| New environment variable that holds a secret | Newman + Frank (telemetry leak risk) |
| Removed or renamed CLI flag, env var, or JSON key | Costanza (product) + Mr. Lippman (release notes / migration) |
| Changes to `Program.cs` startup path or argv parsing | Kramer (engineering) + Mickey (a11y / `--help` surface) |
| New persona, new tool, new agent archetype | Costanza + Maestro (prompt engineering) + Newman (tool surface) |
| Changes to the gate sequence in [`change-management.md`](change-management.md) section 3 | Wilhelm + Soup Nazi + Jerry |
| Anything affecting the `--raw` contract or stderr `[ERROR]` prefix | Mickey (a11y contract owner) + Russell (UX) |
| Anything that ships a CHANGELOG `### Security` bullet | Newman + Wilhelm (release coordination) |
| Adoption / removal of a runtime, framework, or compile mode | Costanza + Kramer + Bania (perf budget) |

If a single trigger applies in isolation -- new doc, new test, new
flag with no migration cost -- you do not need CAB-lite. Run it
through the standard gates in [`change-management.md`](change-management.md)
section 3.

---

## 2. Standing role-set (always invitable)

These roles are always valid CAB-lite seats. The change author picks
the subset that matches the triggers; the showrunner can add others.

| Role | Agent | Bring them in for ... |
|------|-------|------------------------|
| Security | Newman | Tools, network, secrets, supply chain, CVE response |
| Reliability | Frank Costanza | SLOs, incident risk, telemetry, retries, timeouts |
| Licensing | Jackie Chiles | New dependency, license change, attribution |
| Performance | Kenny Bania | Hot-path changes, AOT size, startup-latency budget |
| Accessibility | Mickey Abbott | `--help`, stderr formatting, color, screen-reader output |
| UX | Russell Dalrymple | Output shape, banner, spinner, presentation |
| Product | Costanza | Default behavior changes, breaking flag changes |
| Release | Mr. Lippman | SemVer impact, release-note posture, migration notes |
| CI / DevOps | Jerry | Workflow changes, base image bumps, gate plumbing |
| Style / merge gate | The Soup Nazi | Format, conventional-commits, ASCII grep enforcement |
| i18n | Babu Bhatt | Strings, locale-aware formatting, RTL / CJK |
| Ethics | Rabbi Kirschbaum | Responsible-use surfaces, prompt-injection guardrails |
| FinOps | Morty Seinfeld | Token spend, model economics, CI cost |
| Process | Mr. Wilhelm | Anything that changes the process docs themselves |

The showrunner (Larry David) is *implicitly* on every CAB-lite as
the dispatcher. Mr. Pitt is the standing observer for cross-episode
coordination.

---

## 3. How a consult is requested

Async, never blocking, always recorded.

1. **In the exec report**, under "What shipped" or in a new
   "CAB-lite consults" subsection, name the consult and the
   triggering surface. Example:
   > *CAB-lite: Newman + Jackie consulted on the new `Octokit`
   > NuGet pin (CVE-2025-XXXX). Pin to >= 9.1.4 accepted; license
   > unchanged (MIT).*
2. **In a PR comment** (if a PR exists), `@`-reference the
   archetype names so a future reader can grep. The names need not
   be GitHub users; they are personas the maintainer or sub-agent
   wears.
3. **In the commit body** (rarely; only when the consult shaped the
   commit itself), add a short paragraph:
   > *"Newman flagged the path traversal risk on `read_file` --
   > addressed by adding the home-dir blocklist (see
   > `BlockedPathPrefixes`). Jackie cleared the new
   > `System.CommandLine` pre-release pin."*

The consult need not be a real round-trip with another human or
agent. In single-maintainer mode, the maintainer mentally walks the
named role's checklist (e.g., the [`newman.agent.md`](../../.github/agents/newman.agent.md)
focus areas) and records the result. The discipline is *naming the
consult and recording the outcome*, not synchronizing with another
calendar.

---

## 4. How the decision is recorded

The minimum recording shape:

```text
Consult: <role(s) and agent name(s)>
Trigger: <what surface the change hit>
Outcome: <approved | approved-with-changes | blocked-pending-fix>
Notes: <one-line rationale; link the file path or commit if applicable>
```

For an episode, this lives in the exec report. For a one-off direct
push to `main`, it lives in the commit body.

If the outcome is `blocked-pending-fix`, the change does not merge
until the fix lands. There is no "merge now, fix in follow-up" path
for a CAB-lite block -- that is the exact failure mode CAB-lite
exists to prevent.

If the outcome is `approved-with-changes`, the changes land in the
same PR / commit. They are not a follow-up.

---

## 5. Escalation

If two seats disagree, the showrunner calls it. If the showrunner is
the change author, the call goes to **Mr. Pitt** (program
management) for a tie-break. Pitt's call is final for that change;
disagreement with the call goes into the next retrospective per
[`retrospective-cadence.md`](retrospective-cadence.md).

---

## 6. Worked examples (pulled from prior episodes)

These are illustrative; the actual exec reports are the source of
truth.

- **S02E13 *The Inspector*** -- Newman lead, FDR + Jackie guests.
  CAB-lite was implicit: the security audit triggered consults on
  supply chain (Jackie) and adversarial inputs (FDR). The exec
  report recorded the outcomes and the findings backlog absorbed
  the deferred items.
- **S02E07 *The Observability*** -- Frank lead, Newman guest. The
  opt-in telemetry path required a Newman pass on PII leakage; the
  consult outcome ("zero PII in the documented payload") is in the
  exec report.
- **A hypothetical breaking flag change** -- Costanza + Mr. Lippman
  consult would land in the PR body under "CAB-lite consults" with
  the migration note path and the SemVer call (likely a major
  bump).

---

## 7. Anti-patterns

- **CAB-lite as a meeting.** It is not. Async, recorded,
  unblocking-by-default. If you find yourself scheduling, you have
  drifted into a heavyweight CAB and the latency will swallow the
  next two episodes.
- **Naming the consult without recording the outcome.** "Consulted
  Newman" with no result is indistinguishable from "did not
  consult." Always record the outcome line.
- **Skipping the consult because "I am Newman this week."** In
  single-maintainer mode you wear all the hats. The discipline of
  *naming* the hat (and walking that hat's checklist) is what the
  consult buys you.
- **Letting a `blocked-pending-fix` outcome convert to a
  follow-up.** It does not. The fix lands in the same change or the
  change does not land.
- **Adding a new trigger row to section 1 without flagging it in
  the exec report.** This doc is process; changing it is a process
  change; process changes are themselves reviewed (see Wilhelm's
  archetype: *"process changes are themselves reviewed as PRs --
  meta, but enforced"*).

---

## 8. Cross-references

- Companion process docs:
  [`change-management.md`](change-management.md),
  [`adr-stewardship.md`](adr-stewardship.md),
  [`retrospective-cadence.md`](retrospective-cadence.md).
- Skills:
  [`shared-file-protocol`](../../.github/skills/shared-file-protocol.md),
  [`exec-report-format`](../../.github/skills/exec-report-format.md),
  [`commit`](../../.github/skills/commit.md).
- Agents (standing role-set):
  [`newman`](../../.github/agents/newman.agent.md),
  [`frank`](../../.github/agents/frank.agent.md),
  [`jackie`](../../.github/agents/jackie.agent.md),
  [`bania`](../../.github/agents/bania.agent.md),
  [`mickey`](../../.github/agents/mickey.agent.md),
  [`russell`](../../.github/agents/russell.agent.md),
  [`costanza`](../../.github/agents/costanza.agent.md),
  [`mr-lippman`](../../.github/agents/mr-lippman.agent.md),
  [`jerry`](../../.github/agents/jerry.agent.md),
  [`soup-nazi`](../../.github/agents/soup-nazi.agent.md),
  [`babu`](../../.github/agents/babu.agent.md),
  [`rabbi`](../../.github/agents/rabbi.agent.md),
  [`morty`](../../.github/agents/morty.agent.md),
  [`wilhelm`](../../.github/agents/wilhelm.agent.md),
  [`mr-pitt`](../../.github/agents/mr-pitt.agent.md).

## Sibling process docs

- [`change-management.md`](change-management.md) -- change classes and stage gates.
- [`adr-stewardship.md`](adr-stewardship.md) -- when a change earns an ADR.
- [`retrospective-cadence.md`](retrospective-cadence.md) -- post-episode and post-incident retros.
- Index: [`README.md`](README.md).
