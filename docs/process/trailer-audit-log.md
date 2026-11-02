# Commit trailer audit log

> *Forward-only. Wilhelm keeps the ledger; nobody rewrites `main`.*

The [`commit`](../../.github/skills/commit.md) skill requires every commit
authored with Copilot assistance to carry:

```text
Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>
```

`main` is protected and its commits are shared history, so a commit that
landed without the trailer is **recorded here and left alone**. Rewriting it
would change every SHA after it for everyone who has pulled. This file is
append-only: add an entry, never edit an old one.

## How to re-audit

List commits in a range that lack the trailer (`--invert-grep` keeps the ones
that do not match):

```bash
T='Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>'
git log --format='%h %an %s' --invert-grep --grep="$T" <base>..origin/main
```

Run it on a PR's commits **before** merging, not after:

```bash
gh pr view <n> --json commits \
  --jq '[.commits[] | select((.messageBody | contains("Co-authored-by: Copilot")) | not)] | length'
```

A rebase-merge keeps each commit's message exactly, so it cannot add a trailer
that the commit did not already have. The check has to happen on the branch.

## Entries

### 2026-11-01 -- five commits merged via PR 19 without the trailer

| SHA | Author (per git) | Subject |
|-----|------------------|---------|
| `dae00b8` | Smith | obs: add simple log rotation cap for .ralph-log |
| `da1035a` | Smith | test: cover SSRF address classification boundaries |
| `12403b4` | Smith | test: cover user config load precedence and permissions |
| `dbdd4b4` | Smith | test: cover explicit config and save destination edges |
| `239ce82` | Smith | Expand cost estimator boundary tests |

**What happened.** These came from old local branches
(`smith/temp-observability-changes` and the `smith/swarm/*` branches), were
cherry-picked onto current `main` as PR 19, and merged by rebase. The trailer
audit for that PR ran after the merge instead of before it, so the gap was
found only once the commits were published. Every PR merged since was audited
first and passed (PRs 20, 21, 22 and 23).

**What was verified.** The content of the five commits was reviewed and tested
before merge: the unit suite was green on the combined tree (1,611 of 1,611)
and the integration suite passed (111 of 111). The gap is provenance metadata,
not code quality.

**What this entry does not claim.** It does not say who wrote these commits
beyond what git records (author: Smith), and it does not say whether Copilot
assisted. The rule applies to Copilot-assisted work; whether it applied here
cannot be established after the fact, so the missing trailer is recorded as a
gap and not waived.

**Disposition.** No history rewrite. No retroactive trailer. Tracked in the
[findings backlog](../findings-backlog.md) as `wilhelm-2026-11-W-1`, state
`wontfix`, because the only fix available is the one this log refuses.

### Scope of the audit

The audit window for the entry above is `b98945a..origin/main` (from the close
of S04E07), where exactly five commits lack the trailer. The wider history was
**not** audited for this entry: as of this date 92 of 537 commits on `main`
lack the trailer, most of them human-authored or authored before the rule
existed, where it does not clearly apply. A full historical audit is a
separate task and is not implied by this log.
