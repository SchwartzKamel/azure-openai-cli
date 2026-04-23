# Skill: findings-backlog

**Run every time an episode surfaces a bug, smell, gap, or follow-up.** A finding mentioned in an exec report's "Lessons" section but NOT logged to the backlog is a finding that will get lost. Belt + suspenders: every exec report's lessons section that names a defect must also append a backlog entry.

This is the cohesion spine for cross-episode quality. Without it, S02E08 catches a CJK padding bug, the lesson lands in the exec report, and four episodes later nobody remembers it existed.

## Where findings go

The canonical backlog lives in `docs/exec-reports/s02-writers-room.md` under the **"Off-roster, season-independent"** section in the **"Findings backlog"** subsection. (For S03+, the same convention applies in that season's writers' room file.)

The writers' room file is orchestrator-owned; sub-agents append to the findings backlog by raising it in their exec report and asking the showrunner to log it, OR by appending directly when the dispatch brief explicitly grants edit permission to the writers' room file.

## Entry format

Each finding is one bullet. Required fields:

- **Name.** Kebab-case identifier, prefixed with the episode that surfaced it. Example: `e08-padding-spec-cjk`.
- **Discovering episode.** "Surfaced by S02E13 audit" or equivalent.
- **One-sentence diagnosis.** What is the defect, in one sentence. No theorizing about fixes.
- **File path + line.** Where applicable. Example: `Program.cs:1445`.
- **Severity tag.** One of:
  - `bug` -- a real defect (incorrect behavior, broken code path).
  - `smell` -- a UX or product issue (works as designed, design is wrong).
  - `gap` -- a missing capability (table-stakes feature absent).
  - `lint` -- cosmetic or stylistic only.
- **Disposition.** One of:
  - `queued-as-episode` -- with episode ID if greenlit (e.g., `queued-as-episode: S02E26`).
  - `b-plot` -- folds into a future episode as a secondary beat.
  - `one-line-fix` -- trivial, no episode warranted; can be picked up opportunistically.
  - `wontfix` -- with rationale appended.

## Example entry

```markdown
- **`e13-readfile-blocklist-home-dir-gap`** [gap, queued-as-episode: S02E26]
  Surfaced by S02E13 audit. `ReadFileTool.BlockedPathPrefixes` does not
  cover sensitive home-directory paths (`~/.ssh`, `~/.kube`, `~/.gnupg`,
  `~/.netrc`, `~/.docker/config.json`, `~/.git-credentials`).
  File: `azureopenai-cli/Tools/ReadFileTool.cs`. Queued as S02E26
  *The Locked Drawer* (Newman lead).
```

## The current S02 backlog (worked example)

The following entries are already in the writers' room (do not edit them here -- they live in `docs/exec-reports/s02-writers-room.md`). Cited verbatim as a worked example of the format in practice:

- `e07-dual-telemetry-reality` [smell] -- v2 has opt-in OTel pipeline at `azureopenai-cli/Observability/Telemetry.cs`; need to reconcile the two telemetry stories before users hit the seam.
- `e08-f0-current-culture-bug` [bug, one-line-fix] -- `:F0` formatted against current culture in `Program.cs:1445`; latent de-DE bug.
- `e08-iteration-plural-shortcut` [smell] -- Plural shortcut `iteration(s)` in Ralph mode output.
- `e08-padding-spec-cjk` [bug] -- `,-N` padding-spec column alignment broken in 3 sites; CJK-blocker.
- `e08-arabic-list-separator` [bug] -- Arabic list-separator U+060C silently rejected on input.
- `e08-lone-surrogate` [bug] -- Lone-surrogate masked-input edge case; crashes on input.
- `e11-binary-split-confusion` [smell] -- Two-binary product surface confuses users about which CLI to invoke.
- `e11-config-show-precedence` [smell] -- `--config show` does not make precedence (env > file > default) visible.
- `e12-azureopenaiapi-noun-reading` [smell] -- Env var name `AZUREOPENAIAPI` reads as a noun, not a key; onboarding trips on it.
- `e12-two-source-trees-discoverability` [gap] -- Two source trees (`azureopenai-cli/`, `azureopenai-cli/`) with no top-level discovery surface.
- `e13-readfile-blocklist-home-dir-gap` [gap, queued-as-episode: S02E26 *The Locked Drawer*] -- Sensitive home-dir paths not in the blocklist.
- `e18-ralph-mode-temperature-inheritance` [bug] -- Real bug in Ralph mode temperature inheritance; candidate for a code-fix episode.
- `e19-mcp-table-stakes` [gap] -- MCP support is table-stakes among premium CLIs; already tracked as FR-013.

## Lifecycle

A finding moves through five states. The disposition field reflects the current state.

1. **Surfaced.** The episode catches it. The exec report names it in the "Lessons" or equivalent section.
2. **Logged.** The entry is appended to the writers' room findings backlog with all required fields. **This step is what the skill exists to enforce.** A surfaced-but-unlogged finding will be forgotten.
3. **Triaged.** A reviewer (typically Mr. Pitt or the showrunner) sets the severity and proposes a disposition. Triage cadence: at least once per dispatch wave, ideally during the writers' room update that follows the episode.
4. **Dispositioned.** The disposition is locked: an episode is greenlit, the finding is folded into a planned episode as a b-plot, it's marked one-line-fix for opportunistic pickup, or it's wontfix with rationale.
5. **Closed.** The finding is resolved (episode shipped, fix landed, or wontfix accepted). Move the entry out of the active backlog into a "Closed findings" subsection (or delete with a closing reference in the exec report that fixed it).

## Anti-pattern -- the cardinal sin

> A finding mentioned in an exec report's "Lessons" section but NOT logged to the backlog is a finding that will get lost.

This is the failure mode the skill exists to prevent. Belt + suspenders:

- **Belt.** Every exec report that names a defect in its lessons section must also append a backlog entry in the same dispatch.
- **Suspenders.** The showrunner's writers' room update after each wave cross-references the wave's exec reports against the backlog and flags any unlogged finding for retroactive logging.

If you discover a finding mid-episode and the dispatch brief did not grant edit permission to the writers' room file, surface it in your exec report's lessons section AND in the report's "Findings to log" subsection so the showrunner can append it on next writers' room update.

## Other anti-patterns

- **Dispositioning before triaging.** "I'll just call it wontfix" without the severity tag means the next reader can't tell if you killed a bug or a lint. Triage first, disposition second.
- **Logging without a file path.** "There's a Unicode bug somewhere in masked input" is not a finding; it's a vibe. The next agent can't find it. Path + line, or it's not logged.
- **Stuffing the lessons section instead of the backlog.** Lessons are for humans reading the exec report linearly. The backlog is for the orchestrator scanning for the next thing to fix. They serve different audiences; do both.
- **Letting `wontfix` pile up without rationale.** A `wontfix` without a one-sentence reason is indistinguishable from "I forgot." Always include the why.

## Enforcement

- The showrunner owns the backlog file (writers' room is orchestrator-owned).
- Mr. Pitt audits the backlog at mid-season checkpoints (E06, E12, E18) and at season-end for the finale exec report.
- Every episode's exec report is checked against this skill at sign-off: did any named defect skip the backlog? If yes, the showrunner appends it before greenlighting the next episode in the wave.
