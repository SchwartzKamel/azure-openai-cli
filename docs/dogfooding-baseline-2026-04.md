# Dogfooding Baseline -- 2026-04

> Snapshot of where we use `az-ai` today vs where we use other tools
> or do things manually. Frame for the upcoming S06 Dogfooding season.

## Snapshot date

2026-04-22

## Methodology

Inventory built from four sources, all read-only:

1. **`Makefile`** -- every `.PHONY` target enumerated (43 targets across
   build, test, publish, install, bench, scan, secrets, demo).
2. **`.github/workflows/*.yml`** -- four workflows (`ci.yml`,
   `docs-lint.yml`, `release.yml`, `scorecards.yml`) read end-to-end
   for any `az-ai` invocation in pipeline scripts.
3. **`git log --oneline -100`** sampled on `main`, with the
   `Co-authored-by` trailer extracted via
   `--pretty=format:'%h|%s|%(trailers:key=Co-authored-by,...)'` to
   estimate Copilot-coding-agent participation per commit.
4. **`docs/exec-reports/*.md`** -- 23 episode files plus three season
   blueprints (S03, S04, S05) and the seasons-roadmap pad, scanned for
   author signal ("drafted by az-ai" vs Copilot-sub-agent prose).

No code was changed. No tests were run. PRs and issues were not
queried (see *What this baseline does NOT cover*).

## Tooling inventory: az-ai usage today

### Where az-ai IS the tool

1. **AHK / Espanso text expansion** -- `az-ai --raw` is the engine
   behind `:ai`, `:aifix`, `:aisum`, `:aiexp` Espanso triggers
   (`examples/espanso-ahk-wsl/espanso/ai.yml`) and the `Ctrl+Shift+A`
   / `+E` / `+G` / `+S` AutoHotkey v2 hotkeys
   (`examples/espanso-ahk-wsl/ahk/az-ai.ahk`). Invoked by developers
   from any Windows/WSL editor; ad-hoc cadence; this is the canonical
   "primary use case" the README opens with.
2. **CLI standard mode (one-shot prompts)** -- developers who have
   `make install`-ed the AOT binary at `~/.local/bin/az-ai` invoke it
   directly from a terminal for fix-this-paragraph, explain-this-error,
   and quick-summarize tasks. Ad-hoc; no telemetry to confirm
   frequency (E07 deferred).
3. **Squad personas (manual invocation)** -- `--persona <name>` and
   `--squad-init` exist (`azureopenai-cli/Squad/`) and are documented
   in `AGENTS.md`; usage in our own day-to-day workflow on this repo
   is anecdotal at best (see *honest gaps*).
4. **Ralph mode (autonomous Wiggum loop)** -- shipped, documented,
   and benchmarked (`scripts/bench.py --flag-matrix`); cited in
   release notes; not part of any committed workflow on this repo.
5. **Six built-in tools** (`shell_exec`, `read_file`, `web_fetch`,
   `get_clipboard`, `get_datetime`, `delegate_task`) -- available in
   agent mode. They get exercised in `tests/` and the integration
   suite, but invocation from a developer's own terminal during
   normal repo work is rare-to-zero.

### Where az-ai is NOT the tool (but could be)

1. **Commit message authoring** -- 100-commit sample on `main` shows
   ~60 of the last 100 commits carry the
   `Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>`
   trailer (i.e., drafted via the GitHub Copilot coding agent), not
   `az-ai`. The trailer-less commits are human-typed. Zero commits in
   the sample are attributed to `az-ai` itself.
2. **Exec-report drafting** -- all 23 S02 episode files plus the S03,
   S04, S05 blueprints carry the Copilot trailer. The drafting tool
   is the GitHub Copilot coding agent (cloud), not `az-ai` (the CLI
   we ship). This is the largest documentation surface in the repo
   and `az-ai` does not touch it.
3. **CHANGELOG curation** -- `CHANGELOG.md` is appended by hand at
   commit time; entries are written in a single house style (see the
   `## [Unreleased]` block). No tool consolidation, no LLM-assisted
   reduction; Mr. Lippman's release-runbook describes a manual review.
4. **CI failure triage** -- `.github/workflows/ci.yml` and
   `release.yml` produce logs that humans read in the Actions UI.
   `delegate_task` exists but is not wired into any `gh run rerun`
   or post-failure pipeline. The `ci-triage` skill
   (`.github/skills/ci-triage.md`) is a human-followed checklist.
5. **Release notes synthesis** -- `release.yml` writes a static
   markdown body (`Binaries / Container image / Verification /
   Install`); the human-curated narrative ships in CHANGELOG and the
   per-version `docs/launch/*.md` files. No `az-ai` in the loop.
6. **PR descriptions** -- not directly observable from the local
   clone (PRs not queried), but the commit-message pattern strongly
   implies the same pattern: Copilot-coding-agent drafts, human
   polish; not `az-ai`.
7. **Issue triage** -- no automation; documented as a manual step
   in the maintainer playbook.
8. **ADR drafting** -- ADR-009 et al. are human-authored; no
   `az-ai`-drafted ADRs in the tree.
9. **Spec / proposal drafting** (`docs/proposals/FR-NNN-*.md`) --
   PM cast members (Costanza, Mr. Pitt) author these via the
   coding agent; `az-ai` is not invoked.
10. **Glossary / docs maintenance** -- `docs/onboarding.md`
    glossary, `docs/i18n/`, `docs/security/`, `docs/perf/`
    directories are all maintained by Copilot sub-agents through the
    coding agent, not by `az-ai` calls.
11. **Pre-commit format / build / test** -- `make preflight` is a
    human-run gate. `az-ai` is not consulted to summarise failures
    or propose fixes.
12. **Code-review pre-pass** -- the `code-review` sub-agent exists in
    the agent fleet and is human-invoked; `az-ai` itself is not used
    as a local pre-review step before pushing.

## Friction-cost matrix

Cost is a **rough order-of-magnitude per-week estimate** on this
team's cadence (one primary maintainer plus the agent fleet). It is
not measured. Feasibility "post-S05" assumes S03 multi-provider, S04
model intelligence, and S05 MCP client+server land as blueprinted.

| Workflow | Current tool | Cost / week (estimate) | az-ai feasibility today | az-ai feasibility post-S05 |
|----------|--------------|------------------------|-------------------------|----------------------------|
| Espanso / AHK text expansion | `az-ai --raw` (shipped) | < 5 min | DONE | DONE |
| CLI quick-prompt (fix / explain) | `az-ai` standard mode | ~20 min | DONE | DONE |
| Commit message drafting | Copilot coding agent | 30-60 min | HIGH (one-shot stdin -> message) | HIGH + MCP-fed diff context |
| Exec-report drafting | Copilot coding agent | 2-4 hrs | MEDIUM (stitch with `read_file` + persona) | HIGH (MCP repo-context server) |
| CHANGELOG curation | Manual append | 15-30 min | MEDIUM (squad `writer` persona) | HIGH (MCP + model intelligence) |
| Release notes synthesis | Manual + static template | ~30 min / release | MEDIUM (writer persona + tag-to-tag log) | HIGH |
| CI failure triage | Human reads Actions UI | 30-90 min on red runs | MEDIUM (`web_fetch` log URL + summarise) | HIGH (MCP `gh` server) |
| PR description drafting | Copilot coding agent | 20-40 min | HIGH | HIGH |
| Issue triage / labelling | Manual | 15-30 min | LOW today (no GH integration) | HIGH (MCP `gh` server) |
| ADR drafting | Human | 1-2 hrs / ADR | MEDIUM (architect persona) | HIGH |
| Code-review pre-pass | `code-review` sub-agent (cloud) | 30 min / PR | MEDIUM (`reviewer` persona on diff via stdin) | HIGH |
| Glossary / docs maintenance | Copilot sub-agents | 1-2 hrs | MEDIUM (writer persona) | HIGH |

## The honest gaps

*(Lloyd Braun, junior lens. Read these as questions a new contributor
asked out loud after one week on the repo.)*

We ship a `delegate_task` tool and document it in `AGENTS.md`, but
nothing in my onboarding (`docs/onboarding.md`) tells me when I am
supposed to invoke it from my own terminal during normal repo work.
The `RALPH_DEPTH` cap is documented; the trigger is not. If the only
caller is the agent fleet on the cloud side, the local CLI surface is
effectively dark for the developer reading the README.

The squad persona system is beautifully designed -- five default
personas, per-name memory under `.squad/history/`, deterministic
keyword routing -- and I can find exactly zero artefacts on `main`
that look like they were produced by a developer running
`az-ai --persona reviewer` against a real diff on this repo. The
memory files that would prove it (`.squad/history/*.md`) are
gitignored by design, but no exec report cites a persona session
either. The feature exists; the workflow does not.

We have a CHANGELOG append-on-commit convention and Mr. Lippman owns
the release-runbook, but I cannot find a step that says *"diff the
CHANGELOG against the tag-to-tag commit log and assert nothing user-
facing was forgotten."* `az-ai` could do this in one stdin pipe;
nobody does. The result is that CHANGELOG quality depends on the
commit author remembering to write the entry in the same PR.

CI failure triage right now is "open the Actions tab, read the log,
re-run, hope." We have `web_fetch` and we have `delegate_task` and we
have `Frank Costanza`'s incident-runbook docs, but the three are not
wired together. A single `az-ai --agent "summarize this run"` against
a `gh run view --log` URL would beat the human round-trip on every
red run, and we have not built it.

The exec reports -- the largest writing surface on this repo by an
order of magnitude -- are 100% drafted by the Copilot coding agent.
That is not a bug (the coding agent is the right tool for multi-file
PRs), but it does mean the binary we ship has effectively never
written one of our own season scripts. If we cannot dogfood `az-ai`
into the writers' room, S06 is named wrong.

## What the S06 finale should be able to claim

Five measurable claims, each verifiable against this baseline:

1. **Commit messages.** At least 50% of non-orchestrator commits
   landed during S06 carry an `az-ai`-attributable trailer (e.g., a
   distinct `X-Drafted-By: az-ai/<version>` trailer or equivalent
   attribution mechanism), measured against the current 0% in the
   100-commit sample.
2. **CI failure triage.** Every red CI run on `main` during S06 has
   an `az-ai`-generated summary attached (issue comment, run
   annotation, or dedicated artefact) **before** a human posts the
   first investigation comment. Baseline: 0 such summaries today.
3. **Exec report polish pass.** At least one exec report per S06
   wave is drafted (or substantially revised) via `az-ai --persona
   writer` with a documented human polish pass. Baseline: 0 of 23
   S02 episode files were touched by `az-ai`.
4. **CHANGELOG sweep.** Every S06 release tag is preceded by an
   `az-ai`-driven CHANGELOG-vs-commit-log diff that surfaces
   missing user-facing entries; the diff is attached to the release
   PR. Baseline: no such sweep exists.
5. **Persona session in the wild.** At least one merged PR during
   S06 cites a real `.squad/history/<persona>.md` session that
   informed the change (excerpt or summary in the PR body).
   Baseline: zero such citations.

## What this baseline does NOT cover

- Production user telemetry (we have none -- E07 deferred opt-in
  metrics; see `docs/exec-reports/s02e07-the-observability.md`).
- Other contributors' workflows (sample size = this team).
- Performance / latency comparisons of az-ai-vs-other-tool (Bania
  territory; out of scope here).
- Cost comparisons of az-ai-vs-Copilot-coding-agent on the same
  drafting task (Morty territory; out of scope here).
- PR / issue archaeology (no `gh` API queries were run; only the
  local clone was inspected).

## Cross-references

- S06 blueprint: `docs/exec-reports/s06-blueprint.md` (in flight,
  Jerry lead).
- `docs/exec-reports/s03-blueprint.md` -- Local & Multi-Provider
  (frames "post-S03" feasibility column).
- `docs/exec-reports/s04-blueprint.md` -- Model Intelligence
  (frames smart-defaults / cost-aware routing).
- `docs/exec-reports/s05-blueprint.md` -- Protocols & Plugins
  (frames the "post-S05" MCP-server feasibility column).
- `docs/exec-reports/s02e07-the-observability.md` -- telemetry
  posture this baseline builds on (no opt-in metrics yet).
- `docs/exec-reports/s02e12-the-apprentice.md` -- friction log;
  several Lloyd items above overlap.

## Sign-off

Frank Costanza (SRE / baselining), Lloyd Braun (junior lens).
SERENITY NOW.
