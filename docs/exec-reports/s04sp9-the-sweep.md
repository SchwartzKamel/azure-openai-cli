# S04SP9 -- *The Sweep*

> *A rename of the command is a sentence in four hundred places. Most of them are not yours to change.*

**Commit:** `(this commit)` -- docs only
**Branch:** `docs/hopper-sweep` (PR, off `main`)
**Runtime:** ~1 hour wall-clock, in a separate jj workspace
**Director:** Larry David (showrunner seat, filled by a single orchestrator)
**Lead:** Elaine Benes (technical writer; owner of `elaine-2026-11-E-1`)
**Cast:** 0 sub-agents, inline passes by one orchestrator. Elaine (LEAD), Lloyd Braun (guest: reads as a newcomer would), Mr. Wilhelm (process: what is history and what is not), Babu Bhatt (the i18n quick-starts)

## The pitch

S04E14 renamed the command and the integrations, and left about 190 documentation files saying
`az-ai`. The alias keeps every one of them correct, so nothing was broken. But a newcomer who
follows `docs/use-cases-agent.md` should type the name the tool now goes by, and a README that
says `hopper` next to a guide that says `az-ai` looks unfinished.

The risk is the opposite one. A blanket replace across the docs would rewrite an April
measurement report, a rollback template and a v1-to-v2 cutover record to say a name those
documents never used.

## Scene-by-scene

### Act I -- Sort the files before touching them

Three piles. **Living, user-facing** docs convert: the use-case guides, the config reference,
the persona and prompt guides, the i18n quick-starts, the root `CONTRIBUTING` and `SECURITY`.
**History** stays: ADRs, audits, proposals, launch notes, episode briefs, recaps, and dated
reports. **Packaging** stays until Stage 2: the release and distribution docs and the
release runbook still describe `az-ai-<version>` archives and an `az-ai` formula.

### Act II -- A script with a dry run, and rules about what a line is

The replacement keeps stored and published names: `~/.config/az-ai`, `.az-ai/queue.json`,
archive names, package-channel lines. It changes binary locations, because those did move
(`~/.local/bin/az-ai`, `dist/aot/az-ai`, `/app/az-ai`). Lines that read as history (`v1`,
`legacy`, `stale`, `formerly`, `renamed`) are skipped and listed for a human.

The dry run came before the apply, and it was wrong twice, which is what a dry run is for:

- It swept in `.wgm/` (this project's own working notes, excluded by convention) and the
  packaging runbook.
- My path rule protected `bin/az-ai` along with `.config/az-ai`. The first is the installed
  binary and changed; the second is the user's settings and did not.
- Two skip keywords were too greedy: "migrat" matched "SQL migration" and "alias" matched
  *model* aliases in the config reference.

### Act III -- Read what it did

A regex check for the dangerous shapes (a moved config path, a package name that cannot have
changed, a sentence that says the tool was formerly itself) found nothing. That is not
enough, because a regex finds only what you thought to look for. A random sample of 28 changed
lines, and a review of every remaining `az-ai`, found the real problems:

- **Seven files were dated or cutover-era** and would have been falsified: the April
  dogfooding baseline, the SRE runbook with a "rolled back 2.0.0 to 1.9.1" template, three
  audits, a v2 review, and the release runbook. All reverted, and the exclusions written into
  the script so the run is reproducible.
- **A sentence the rebrand PR had made false.** The accessibility guide said no man page
  exists and that a `docs/man/az-ai.1` was future work. There is now `man/hopper.1`. Rewritten
  to say what is true: a stub exists, it is not installed or shipped.
- **Four command mentions** that had tripped a keyword were fixed by hand.

The leftovers were then sorted by kind: 44 stored locations (correct), 45 published package
or asset names (Stage 2), 3 compatibility notes, and 20 others, of which every one was a
stored name (systemd unit and container names, an Azure resource name, a config file name).

## What shipped

**Docs**

- 46 documentation files: the command is `hopper`, install locations are `hopper`, the wrapper
  and AutoHotkey names follow the earlier rename. 565 lines changed, one for one.
- `docs/findings-backlog.md`: `elaine-2026-11-E-1` resolved; a narrower follow-up opened.

**Not shipped**

- **Display-name prose.** "Azure OpenAI CLI" as a product name in prose is left alone: it is
  often ambiguous with the Azure service, and each sentence needs a person.
- **History documents**, by design.
- **Packaging docs and the release runbook**, until Stage 2.

## Lessons from this episode

1. **Sort the files before you sort the lines.** Whether a document is history decides more
   than any regex does.
2. **A dry run is for being wrong cheaply.** Mine was wrong three ways before anything changed.
3. **Regex finds what you expected; a sample finds what you did not.** The seven falsified
   documents showed up in the sample, not in the pattern search.
4. **Your last change makes some sentences false.** The man page line was only wrong because of
   the previous PR. Grep for claims about the thing you just changed.
5. **A converted file still has leftovers; classify them.** Every remaining `az-ai` was a stored
   name or a published one, and saying so is how you know you are finished.

## Metrics

- **Diff:** 46 documentation files, 566 insertions and 565 deletions, plus this report, the
  index row and two findings rows. No code.
- **Inventory** (`scripts/brand-inventory.sh`): `az-ai` 305 to 280 files and 2,648 to 2,100
  lines (328 and 3,156 before the rebrand).
- **Test delta:** n/a (docs-only).
- **Preflight:** docs-only path; `ascii-check`, `markdownlint` and `exec-report-check` green.
- **CI status at push time:** pending at commit time; link in the PR.

## Credits

Elaine led because the backlog item was hers. Wilhelm's question, "is this document a record
or an instruction?", is the sorting rule. Lloyd's lens is why the random sample was read as a
newcomer would. Babu is why the four i18n quick-starts were checked as a set. Commits carry
`Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>`.
