# Change management

> *"The gate is the gate. The gate is there for a reason. I think we
> talked about the reason. Did we?"* -- Mr. Wilhelm

Owned by **Mr. Wilhelm**. Style enforcement at merge time delegated to
**The Soup Nazi**. CI-side gate execution owned by **Jerry**.

This doc is the spec for *how a change moves through the project*: how
it is classified, what reviews it earns by class, what gates it must
pass, how an architecturally significant decision becomes an ADR vs. a
backlog entry, and what the PR flow looks like end to end.

If a change has no reasonable home in the table below, that is a
process gap. Surface it in your exec report so this doc gets a new row.

---

## 1. Change classification

Every PR (or direct push to `main` for solo-maintainer changes) falls
into exactly one of four classes. The class drives review, gates, and
release-note posture.

| Class | Examples | User-visible? | Reverts trivially? |
|-------|----------|---------------|--------------------|
| **Cosmetic** | Whitespace, comment edits, dead-code removal, typo fixes, internal renames not exposed in `--help` or JSON output. | No | Yes |
| **Additive** | New flag, new tool, new subcommand, new env var, new optional config key, new test, new doc, new ADR. Existing behavior unchanged. | Yes (new only) | Yes |
| **Breaking** | Removed flag, renamed flag, changed JSON shape, changed exit code, changed default behavior, removed tool, raised minimum runtime version, changed env-var name. | Yes (existing) | No (downstream coordination required) |
| **Security** | Patch for a CVE or self-disclosed vulnerability, secrets-handling change, sandbox tightening, supply-chain remediation (dependency pin, base image bump for a known CVE). | Sometimes | Sometimes |

**Tie-breakers:**

- A change that is *additive in code* but *removes a documented
  default* (e.g., flips an opt-in flag to opt-out) is **breaking**.
- A change that is *cosmetic in source* but alters a man page,
  `--help` string, or any user-readable byte is **additive** at minimum
  (because users may grep for the old wording).
- A change that *adds* a sandbox tightening (new shell-blocklist
  pattern, new path-blocklist entry) is **security**, even though it is
  technically additive, because it earns a different review path
  (Newman) and a `### Security` CHANGELOG bullet.

---

## 2. Review requirements per class

Single-maintainer reality: these are the *minimum* review surfaces a
change must satisfy. For solo direct-push to `main`, the maintainer is
both author and reviewer; the discipline is to mentally walk each row.

| Class | Required reviews | Optional but encouraged |
|-------|-----------------|--------------------------|
| **Cosmetic** | Self-review (read your own diff). [`ascii-validation`](../../.github/skills/ascii-validation.md) on any `.md` touched. | None. |
| **Additive** | Self-review + tests added or N/A justified. Docs updated where behavior changed. [`preflight`](../../.github/skills/preflight.md) on any code change. | Newman if the addition touches a tool, an external network call, or a credential path. Elaine if a new doc was added. |
| **Breaking** | Self-review + ADR (see [`adr-stewardship`](adr-stewardship.md) section "When a change earns an ADR"). [`preflight`](../../.github/skills/preflight.md). CHANGELOG `### Changed` or `### Removed` bullet. Migration note in `docs/migration-*.md` if user-facing. | [`cab-lite`](cab-lite.md) consult if the break crosses subsystem boundaries. |
| **Security** | Self-review + Newman pass (in single-maintainer mode: explicitly run through the [`newman.agent.md`](../../.github/agents/newman.agent.md) checklist). [`preflight`](../../.github/skills/preflight.md). CHANGELOG `### Security` bullet. **No public issue** for an as-yet-undisclosed vulnerability -- see [`SECURITY.md`](../../SECURITY.md). | Frank if the change affects observability or incident-response surface. Jackie if a dependency was added or removed for security reasons. |

---

## 3. Stage gates

Stage gates run in the order below. A gate either passes (proceed) or
blocks (stop and fix). There is no "gate skipped, will fix in
follow-up" -- that is how `180d64f` happened. See [`ci-triage`](../../.github/skills/ci-triage.md)
if a gate fails after push.

```text
   [classify]
       |
       v
   [preflight]      -- code-touching diffs only; .github/skills/preflight.md
       |
       v
   [ascii grep]     -- any .md outside the upstream exclusion list
       |
       v
   [self code review] -- read your own diff before staging
       |
       v
   [security audit]   -- Newman pass; mandatory for security class, advised
       |                 for additive changes touching tools/network/creds
       v
   [license check]    -- Jackie pass; mandatory if dependencies changed
       |
       v
   [docs sync]        -- README, CHANGELOG, --help, man pages, docs/* per
       |                 the matrix in section 2
       v
   [merge / push]     -- direct push to main, or PR + CI green
       |
       v
   [post-push CI watch] -- watch the run; fix-forward within the hour if red
```

**Per-gate ownership and authoritative skill / contract:**

| Gate | Owner | Authoritative reference |
|------|-------|--------------------------|
| Preflight | Kramer (author) + Jerry (CI mirror) | [`preflight.md`](../../.github/skills/preflight.md) |
| ASCII grep | Soup Nazi (style) | [`ascii-validation.md`](../../.github/skills/ascii-validation.md) |
| Self code review | The change author | This doc, section 2 |
| Security audit | Newman | [`newman.agent.md`](../../.github/agents/newman.agent.md) |
| License check | Jackie Chiles | [`jackie.agent.md`](../../.github/agents/jackie.agent.md) |
| Docs sync | Elaine (writing) + author | [`exec-report-format.md`](../../.github/skills/exec-report-format.md) for episode work |
| Commit format | Wilhelm + Soup Nazi | [`commit.md`](../../.github/skills/commit.md) |
| Post-push CI watch | Jerry + Frank | [`ci-triage.md`](../../.github/skills/ci-triage.md) |

**Docs-only fast path:** if the diff is docs-only per
[`docs-only-commit.md`](../../.github/skills/docs-only-commit.md), the
preflight gate is skipped and the ASCII grep becomes the primary block.
Everything else in the sequence still applies.

---

## 4. ADR vs. backlog -- the decision tree

Every change produces *some* artifact: a commit, an exec report, an
ADR, a backlog entry, or a combination. The question is which.

```text
                Does the change ...
                       |
        +--------------+--------------+
        |                             |
   ... lock in a               ... surface a defect,
   technology, runtime,        smell, gap, or
   convention, or              follow-up that the
   reverse a prior ADR?        episode did not fix?
        |                             |
       Yes                           Yes
        |                             |
        v                             v
   Write an ADR.               Append to findings
   See adr-                    backlog. See
   stewardship.md.             findings-backlog.md.
        |                             |
        +--------------+--------------+
                       |
                       v
        Is the change architecturally
        significant *and* surfaces follow-ups?
                       |
                      Yes
                       |
                       v
              Both. ADR for the lock-in,
              backlog entries for the
              loose threads. Cross-link
              from each to the other.
```

**Rules of thumb:**

- A change that someone will ask "why did we do it this way?" about
  six months from now -> ADR.
- A change that *reveals* something broken but does not fix it ->
  backlog (per [`findings-backlog.md`](../../.github/skills/findings-backlog.md)).
- A bug fix with a non-obvious tradeoff (e.g., perf cost for
  correctness) -> ADR + the fix in the same PR.
- A routine refactor or rename with no tradeoff -> commit only.

When in doubt, write the ADR. ADRs are cheap; lost context is not.

---

## 5. PR / direct-push flow

The end-to-end flow, single-maintainer or sub-agent dispatch:

1. **Classify.** Pick one of the four classes in section 1. Write it
   in the exec report's "What shipped" section if this is an episode.
2. **Plan the gates.** Walk section 3. If any are required and you
   cannot run them locally (constrained shell, missing tooling), push
   to a branch instead of `main` so CI runs them for you.
3. **Decide ADR or backlog.** Walk section 4. If ADR, draft it in the
   same PR / commit as the change.
4. **Run preflight** (code) or **ASCII grep** (docs-only).
5. **Self-review the diff.** `git diff --stat`, then `git diff` in
   full. Read every line. The cost of catching a regression now is
   one minute; in CI, an hour; in production, a fix-forward and an
   exec-report retro.
6. **Stage explicitly.** `git add <paths>`, never `git add -A`. The
   sub-agent fleet protocol; even solo maintainers benefit from the
   discipline because it prevents stray scratch files.
7. **Commit** per [`commit.md`](../../.github/skills/commit.md). Conventional
   Commits, lowercase type, Copilot trailer if AI-assisted.
8. **Push.** Direct to `main` for maintainer changes per
   [`commit.md`](../../.github/skills/commit.md); branch + PR otherwise.
9. **Watch CI.** `gh run list --branch main --limit 1 --json
   conclusion,status,displayTitle`. Red within the hour means
   [`ci-triage.md`](../../.github/skills/ci-triage.md).
10. **If the change taught a lesson**, update the exec report and (if
    applicable) the findings backlog before moving to the next change.

---

## 6. Branch protection posture (current state)

This is a solo-maintainer repo. Branch protection on `main` is
**advisory**, enforced by maintainer discipline and the gate sequence
above rather than by GitHub's required-status-check rule. The trade-off:

- **Pro:** sub-agent dispatches and human direct pushes are not blocked
  on a CI round-trip for low-risk changes (typos, doc tweaks, single-
  agent exec reports).
- **Con:** the `180d64f` failure mode is possible. The mitigation is
  that every code-touching commit goes through [`preflight`](../../.github/skills/preflight.md)
  *locally* before push.

If multi-maintainer arrives, this section flips: required status
checks become hard gates, direct push is disabled, and the PR flow
becomes mandatory. Until then, the gate sequence in section 3 is the
process; CI is the safety net, not the gate.

---

## 7. Anti-patterns

- **Misclassifying a breaking change as additive** because "the old
  flag still parses, it just no-ops now." A no-op of a documented flag
  is breaking. Bullet it under `### Changed`.
- **Skipping the security gate on an additive change that touches a
  tool.** Tools are the project's largest attack surface; new tools
  are always a Newman concern.
- **Writing an ADR after the fact.** ADRs are decision records, not
  decision rationalizations. If the decision is already merged and
  shipped, the ADR documents the fait accompli and loses half its
  value -- but write it anyway, with `Status: Accepted (post-hoc)` so
  the next reader knows.
- **"This is a docs-only PR"** when the diff also touches a workflow
  file. Workflows are not docs. See [`docs-only-commit.md`](../../.github/skills/docs-only-commit.md).
- **Using `git add -A` and discovering an `.env` or `bin/` in the
  staged set at commit time.** Stage explicit paths.

---

## 8. Cross-references

- Skills cited: [`preflight`](../../.github/skills/preflight.md),
  [`commit`](../../.github/skills/commit.md),
  [`ascii-validation`](../../.github/skills/ascii-validation.md),
  [`docs-only-commit`](../../.github/skills/docs-only-commit.md),
  [`ci-triage`](../../.github/skills/ci-triage.md),
  [`findings-backlog`](../../.github/skills/findings-backlog.md),
  [`shared-file-protocol`](../../.github/skills/shared-file-protocol.md),
  [`exec-report-format`](../../.github/skills/exec-report-format.md),
  [`changelog-append`](../../.github/skills/changelog-append.md).
- Companion process docs:
  [`adr-stewardship.md`](adr-stewardship.md),
  [`cab-lite.md`](cab-lite.md),
  [`retrospective-cadence.md`](retrospective-cadence.md).
- Agents cited: [`wilhelm`](../../.github/agents/wilhelm.agent.md),
  [`soup-nazi`](../../.github/agents/soup-nazi.agent.md),
  [`jerry`](../../.github/agents/jerry.agent.md),
  [`newman`](../../.github/agents/newman.agent.md),
  [`jackie`](../../.github/agents/jackie.agent.md),
  [`frank`](../../.github/agents/frank.agent.md).
- Templates: [`PULL_REQUEST_TEMPLATE.md`](../../.github/PULL_REQUEST_TEMPLATE.md),
  [`CONTRIBUTING.md`](../../CONTRIBUTING.md).
