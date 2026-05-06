---
title: Docs Audit -- Cross-Cutting Drift Refresh
auditor: Elaine (Technical Writer)
date: 2026-05-06
release_under_review: v2.2.0 (HEAD: 905515e, untagged prompt-templates feat)
last_full_audit: 2026-04-22 (docs-audit-2026-04-22-elaine.md and 12 supporting reports)
scope:
  - Root narrative -- README.md, AGENTS.md, ARCHITECTURE.md, ROADMAP.md, CONTRIBUTING.md, SECURITY.md, CHANGELOG.md
  - Cross-cutting docs -- docs/persona-guide.md, docs/use-cases*.md, docs/config-reference.md,
    docs/prompts/README.md, docs/glossary.md, docs/espanso-ahk-integration.md,
    docs/README.md
  - Examples kit -- examples/espanso-ahk-wsl/README.md, espanso/*.yml, ahk/*.ahk,
    PROMPT-TEMPLATES-INTEGRATION.md
  - Agent fleet -- AGENTS.md table vs .github/agents/*.agent.md inventory
  - Skill files -- .github/skills/*.md vs references in copilot-instructions.md and AGENTS.md
  - Cross-check against current code state -- csproj Version (2.2.0), recent commits,
    espanso trigger inventories
excluded_from_scope:
  - SECURITY.md deep review (Newman owns the next pass)
  - THIRD_PARTY_NOTICES.md / NOTICE-assets.md (Jackie Chiles)
  - Per-mode use-case body content (only top-level structure / mode coverage checked)
files_reviewed: 22 plus inventory grep across .github/agents and .github/skills
severity_scale:
  CRITICAL: User-facing wrong info -- following the doc fails (404, missing flag, broken trigger).
  MAJOR:    Drift that misleads -- doc still works but mental model is wrong for current release.
  MINOR:    Stale phrasing, version-date rot, internal inconsistency a careful reader notices.
  NIT:      Style / voice / wording only.
---

# Docs Audit -- 2026-05-06 -- Elaine

Two weeks since the last full pass, three episodes of work (v2.1, v2.2.0, plus
the prompt-templates feat sitting on `main` untagged), and the docs are
already drifting. The pattern is the one we keep paying for: **shipped code
moves; the prose that points at it does not**. The README still calls
AGENTS.md a "25-agent roster". The ROADMAP still announces v2.0.4 as the
current release. The use-cases index still lists five execution modes when
the README banner promises six. And the install table on the front page
has not been touched since v2.0.5 even though we are tagging v2.2.0.

The good news: the new prompt-templates work (commit `905515e`) is well
integrated into `examples/espanso-ahk-wsl/README.md` and `docs/prompts/README.md`.
The bad news: it is invisible from the root README and from
`docs/espanso-ahk-integration.md`. A reader who never opens the examples
folder cannot find it.

Two findings reach **CRITICAL**: the install table promises filenames the
release pipeline does not produce at the current tag, and `:aidata` is
defined twice with two different meanings, so a user installing both
match files gets undefined behavior.

Fix the Criticals first. The Majors are stewardship debt -- pay them down
before S03E03 ships another layer on top.

---

## Summary

| Severity   | Count | Where the damage lands                                                      |
|------------|------:|-----------------------------------------------------------------------------|
| CRITICAL   |     2 | README install table version mismatch; `:aidata` trigger collision           |
| MAJOR      |    11 | Agent count drift (3 sites); ROADMAP current-release; CHANGELOG `[Unreleased]` empty; use-cases missing Image mode; espanso-ahk-integration missing prompt-templates work; README docs-section missing prompts links; copilot-instructions agent count; persona-guide v2.0.0 staleness; AHK hotkey table incomplete |
| MINOR      |     7 | "v1.9.x upgrade" pointer; "New in v2.0.0" heading rot; perf-baseline cross-link mismatch; "1,510+ tests" plausibly stale; ARCHITECTURE GHCR path inconsistency; trigger sample reads as exhaustive; v2.0.0 scope phrasing in config-reference |
| NIT        |     2 | README em-dash / smart-arrow chrome; `:aiprompts` help-trigger undocumented in kit README inventory |
| **Total**  | **22** |                                                                             |

---

## CRITICAL

### C1 -- README `Install -> Pre-built binaries` table is pinned to v2.0.5; the repo is at v2.2.0

**File:** `README.md:248-255`
**Evidence:**

- `azureopenai-cli/AzureOpenAI_CLI.csproj` -- `<Version>2.2.0</Version>`.
- `git log --oneline` -- `0cf3330 (tag: v2.2.0) chore(release): cut v2.2.0`.
- README table body lists `az-ai-2.0.5-linux-x64.tar.gz`,
  `az-ai-2.0.5-linux-musl-x64.tar.gz`, `az-ai-2.0.5-osx-arm64.tar.gz`,
  `az-ai-2.0.5-win-x64.zip` and the lead-in says "(v2.0.5 shown)".

**Problem:** The current Releases page does not have v2.0.5-named assets at
the latest tag. A reader copy-pasting these filenames into a `curl -LO`
gets a 404, and the version they end up downloading from the linked
Releases listing will not match the table they were just told to follow.
This is the same class of bug as C1 in the 2026-04-22 audit, regenerated
two patch releases later.

**Proposed fix:** Either (a) change the lead-in to "(latest version shown
below; substitute the tag from [Releases](...))" and change all four rows
to use `<version>` placeholder; or (b) bump the four rows to `2.2.0` and
add a CI step / pre-push check that validates the table version matches
`<Version>` in the csproj. Option (a) is cheaper to maintain.

**Severity:** CRITICAL. The install path is still the most-followed
section of the README and it currently lies to the reader on first read.

---

### C2 -- `:aidata` trigger is defined in two espanso match files with two different meanings

**Files:**

- `examples/espanso-ahk-wsl/espanso/ai-windows-to-wsl.yml` (the v2.2.0
  trigger set, S03E01 unification)
- `examples/espanso-ahk-wsl/espanso/ai-prompts.yml` (the new
  prompt-templates feat from `905515e`)

**Evidence:** `grep '":aidata"'` returns one match in each file; both use
`replace: "{{output}}"` but with different `cmd` bodies (one is a Data
Workflow template prompt, the other is the original "Convert clipboard to
structured data" trigger from the unification work).

**Problem:** The kit README (`examples/espanso-ahk-wsl/README.md:30, 41`)
tells operators they may install both `ai-windows-to-wsl.yml` and
`ai-prompts.yml` ("Pick one Espanso option ... Prompt templates are
optional"). If they do, espanso will load whichever match it sees last
and silently shadow the other. There is no warning, no error, and the
behavior of `:aidata` from a user's perspective becomes a coin flip.

**Proposed fix:** Rename one of the triggers. The prompt-templates set is
the newer arrival and is clearly namespaced under the
`:aiquestion/:aiarch/:aicode/:aicost` family -- rename its data trigger
to `:aidataflow` (to match its task name in `task-templates.md`) and
update `ahk/az-ai-prompts.ahk` (Ctrl+Shift+D), the kit README, and
`PROMPT-TEMPLATES-INTEGRATION.md`. Add a trigger-name uniqueness check to
the espanso lint target (Puddy can own the test, Soup Nazi can own the
gate).

**Severity:** CRITICAL. Two triggers with the same name and different
behavior is the textbook footgun for an end-user-installable kit. The
fix is mechanical and small; the cost of leaving it is a class of
support tickets we cannot diagnose remotely.

---

## MAJOR

### M1 -- README still calls AGENTS.md a "25-agent roster"

**File:** `README.md:275`
**Evidence:** `AGENTS.md:9` says "**28 agents total** (1 showrunner + 5 main +
22 supporting)". Inventory of `.github/agents/*.agent.md` returns 28 files
matching the AGENTS.md table 1:1.

**Proposed fix:** Replace "the 25-agent roster" with "the 28-agent fleet"
or, better, drop the count and write "the fleet roster (currently 28)" --
the count rolls every season and pinning it in the README guarantees this
finding reappears.

---

### M2 -- `docs/persona-guide.md` also calls it a "25-agent roster"

**File:** `docs/persona-guide.md:291`
**Evidence:** Same. Drifted in lockstep with M1.

**Proposed fix:** Same. Cross-fix both in one PR.

---

### M3 -- `.github/copilot-instructions.md` says "27 Copilot custom agent personas" and "21 supporting players"

**File:** `.github/copilot-instructions.md` (the `## Agent Fleet` section
and the `## File Structure` line listing `.github/agents/`)
**Evidence:** Bookman addition was tracked in CHANGELOG `[2.2.0]` as
"Supporting players: 21 -> 22; total fleet: 27 -> 28." The
copilot-instructions doc still reads from the pre-Bookman state.

**Proposed fix:** Two string substitutions -- "27 Copilot custom agent
personas" -> "28 Copilot custom agent personas", "21 supporting players"
-> "22 supporting players". Same de-rot as M1: prefer "the cast roster"
over a hard count.

---

### M4 -- ROADMAP.md announces v2.0.4 as the current release

**File:** `ROADMAP.md:18`
**Evidence:** "Current release: **v2.0.4** (2026-04-22). v2.0.5 is in
flight." -- but the current tag is v2.2.0 (2026-04-30) and HEAD is
`905515e` (untagged prompt-templates feat).

**Problem:** A reader who lands here gets a wildly wrong sense of where
the project is. v2.1 cutover (foundry routing, image-gen, persona memory
caps) and v2.2.0 (Bookman, brevity tier doctrine, S03E01-E02 work) are
both invisible.

**Proposed fix:** Update the line to "Current release: **v2.2.0**
(2026-04-30). HEAD has the prompt-templates feature staged for the next
patch." Better: replace the sentence with a one-liner that defers to
`CHANGELOG.md` (it already does this above; line 18 is the only hardcoded
version pointer). Lippman should own a changelog-amend skill that
auto-bumps this line on release cut.

---

### M5 -- CHANGELOG `[Unreleased]` is empty; commit `905515e` is unrecorded

**File:** `CHANGELOG.md:9-23` (the empty `[Unreleased]` block)
**Evidence:** `git log --oneline` shows `905515e (HEAD -> main) feat(prompts):
Add five canonical task templates with Espanso/AHK integration` -- not in
v2.2.0, not in `[Unreleased]`. The CHANGELOG `[Unreleased]` Added/Changed/
etc. sub-sections are all empty.

**Problem:** Per the `changelog-append` skill, code that lands on `main`
between releases should appear under `[Unreleased]` before the final
push. This commit shipped without that step. If the next reader runs the
exec-report-check or any release tooling that reads `[Unreleased]`, they
will see "nothing changed" and either generate an empty release-notes
draft or skip the bump entirely.

**Proposed fix:** Add an `### Added` entry under `[Unreleased]`:

```markdown
- **feat(prompts):** Five canonical task templates landed in
  `docs/prompts/{system-prompt-master,task-templates}.md`, plus
  Espanso triggers (`:aiquestion`, `:aiarch`, `:aicode`, `:aidata`,
  `:aicost`, `:aiprompts`) and AHK hotkeys (Ctrl+Shift+Q/R/C/D/L,
  Ctrl+Shift+T) under `examples/espanso-ahk-wsl/`. See
  `examples/espanso-ahk-wsl/PROMPT-TEMPLATES-INTEGRATION.md` for the
  setup guide. (commit 905515e)
```

This is also a process finding: the exec-report-check gate did not stop
this push, which suggests either the gate has a hole or the push was
opted out. Worth a Wilhelm follow-up.

---

### M6 -- `docs/use-cases.md` only lists five execution modes; the README banner promises six

**File:** `docs/use-cases.md:33-37`
**Evidence:** README.md:15 (`6 execution modes`) and README.md:94 (the
mode table including `--image`). `docs/use-cases.md` table contains
Standard, Raw, Agent, Ralph, Persona/Squad -- no Image row, no link to
an image-mode use-case file.

**Problem:** The use-cases index is the canonical "what can this thing do"
reference linked from the README and from `docs/README.md`. A reader who
follows the link cannot find image generation at all, even though it has
been a first-class mode since v2.1 cutover.

**Proposed fix:** Add an `Image` row:

| **Image** | `--image` | (none) | (none) | (none) | new file `use-cases-image.md` or section in `use-cases-standard.md` |

If we are not ready to write `use-cases-image.md` yet, link the Image row
straight to the README's Image Generation section (`README.md#image-generation`)
as a stop-gap, and file an FR for the dedicated guide.

---

### M7 -- `docs/espanso-ahk-integration.md` is silent on prompt-templates and on the v2.2.0 trigger additions

**File:** `docs/espanso-ahk-integration.md` (1359 lines, last touched
before the prompt-templates feat and arguably before S03E02 too)
**Evidence:** `grep` for `Bookman | system-prompt-master | task-templates |
ai-prompts.yml | PROMPT-TEMPLATES | aiprompts | aiquestion | aiyml | aicost`
-- zero matches except a single one-off `:aiarch` mention on line 1274
that pre-dates the prompt-template family. Line 522 enumerates the Linux
trigger set as 16 names; the kit at `examples/espanso-ahk-wsl/espanso/
ai-windows-to-wsl.yml` actually ships 22 triggers and `ai-prompts.yml`
adds another six. The integration guide is two episodes behind the kit it
points at.

**Problem:** This doc is the long-form integration guide referenced from
the README ("Full integration guide ... `docs/espanso-ahk-integration.md`",
README.md:143). A reader following the README to its end will land here
and see a stale catalogue.

**Proposed fix:** This is a non-trivial rewrite, not a one-line patch.
Recommend filing as findings-backlog item with Babu/Maestro paired
co-owners, scope:

1. Add a top-of-file "Sources of truth" callout pointing at
   `examples/espanso-ahk-wsl/README.md` and `docs/prompts/README.md` for
   the trigger and prompt inventories respectively.
2. Replace the hand-listed trigger enumerations with a generated table or
   a one-line "see kit README for the current 22-trigger set + 6
   prompt-template triggers".
3. Add a section on the brevity tier doctrine (Bookman) and how
   `:aishort` / `:aiyml` interact with `--max-tokens`.

Carve this off as its own episode (S03E03 candidate) rather than trying to
hand-edit the file in this audit's fix wave.

---

### M8 -- README "Documentation" section does not link to the prompt library

**File:** `README.md:268-309` (the Documentation block)
**Evidence:** No mention of `docs/prompts/README.md`,
`docs/prompts/system-prompt-master.md`, or `docs/prompts/task-templates.md`.
The kit README links them; the root README does not. `docs/README.md:146`
does have the prompts link, but a reader who doesn't go through
`docs/README.md` first will never find them.

**Proposed fix:** Add a sub-section under Documentation:

```markdown
### Prompts

- [docs/prompts/README.md](docs/prompts/README.md) -- prompt library map
- [docs/prompts/system-prompt-master.md](docs/prompts/system-prompt-master.md)
  -- master system prompt
- [docs/prompts/task-templates.md](docs/prompts/task-templates.md) -- five
  canonical task templates (Q&A, Architecture, Code Gen, Data, Cost/ROI)
```

Place it between "Operating the CLI" and "Security" (it is operator-facing,
not architectural).

---

### M9 -- AHK hotkey table in the kit README does not cover the new prompt-template hotkeys

**File:** `examples/espanso-ahk-wsl/README.md:271-279`
**Evidence:** Table lists Ctrl+Shift+A / E / G / S only.
`examples/espanso-ahk-wsl/ahk/az-ai-prompts.ahk` ships Ctrl+Shift+Q / R / C
/ D / L plus Ctrl+Shift+T. `PROMPT-TEMPLATES-INTEGRATION.md` documents them
but the kit README's "§5 AutoHotkey v2 on Windows -> WSL" section -- the
one a reader uses to decide which hotkeys to memorize -- does not.

**Proposed fix:** Add a second table immediately after the existing one:

```markdown
### Prompt-template hotkeys (optional, requires `az-ai-prompts.ahk`)

| Hotkey         | Template                                      |
|----------------|-----------------------------------------------|
| `Ctrl+Shift+Q` | Knowledge Q&A                                 |
| `Ctrl+Shift+R` | Architecture Design                           |
| `Ctrl+Shift+C` | Code Generation                               |
| `Ctrl+Shift+D` | Data Workflows                                |
| `Ctrl+Shift+L` | Cost / ROI                                    |
| `Ctrl+Shift+T` | Prompt-template reference card (open docs)   |
```

Cross-link to `PROMPT-TEMPLATES-INTEGRATION.md` for the full setup steps.

---

### M10 -- `docs/persona-guide.md` still scopes itself to v2.0.0

**Files:** `docs/persona-guide.md:4, 83`
**Evidence:**

- L4: "directory in v2.0.0. For internal design notes, see..."
- L83: "Reserved -- not yet wired in 2.0.0."

**Problem:** Two version-pin landmines. The first reads as "this doc is for
v2.0.0 specifically, ignore on later versions" -- not the intent. The
second tells a 2.2.0 user that a feature "is not yet wired in 2.0.0",
which is technically true but useless and probably wrong (we should
either confirm the per-persona model override is now wired, or update the
pin).

**Proposed fix:** Reword L4 to "directory introduced in v2.0.0 and current
through v2.2.0". For L83, ask Kramer whether per-persona `model` is now
wired; if yes, document the field; if no, change the pin to "Reserved as
of v2.2.0; tracked in <FR-link>".

---

### M11 -- README "Why" section claims "Measured on v2.0.6, laptop reference rig" but links to a v2.0.5 baseline doc

**File:** `README.md:14`
**Evidence:** "Measured on v2.0.6, laptop reference rig -- see
[docs/perf/v2.0.5-baseline.md](docs/perf/v2.0.5-baseline.md)." Two
versions in one sentence, neither matches the current release (v2.2.0).

**Problem:** The reader cannot tell which number is authoritative -- the
v2.0.6 measurement claim or the v2.0.5 baseline doc. README.md:119 then
says baselines are "being re-measured" with no completion date. Three
release tags later, still being re-measured.

**Proposed fix:** Either ship the v2.2.0 baseline (Bania's call, exec-
reportable) or pick one canonical pin and use it consistently in both
the bullet and the deferred-refresh paragraph. Ideally: rename
`docs/perf/v2.0.5-baseline.md` to `docs/perf/baseline.md` and let the
file's own front-matter pin the version. Eliminates the entire class of
finding.

---

## MINOR

### m1 -- README "Upgrading from v1.9.x?" pointer is two minors stale

**File:** `README.md:115`
**Evidence:** "Upgrading from v1.9.x? See ..." -- written for v2.0.0
release. v2.2.0 readers are unlikely to be coming straight from v1.9.x.

**Proposed fix:** "Upgrading from v1.x? See ..." -- broader and stays
correct longer.

---

### m2 -- README "New in v2.0.0" heading has not been refreshed since v2.0.0

**File:** `README.md:100`
**Evidence:** Heading is `### New in v2.0.0`. We have shipped v2.1 and
v2.2 since. Flags listed (`--json`, `--schema`, `--max-rounds`,
`--config`, `--completions`, `--models`, `--telemetry`, `--estimate`,
`--persona`) are all still v2.0.0-era; v2.1+ additions
(`--image`, `--init`/`--configure`/`--login`, `--config export-env`)
are documented elsewhere in the README but not in this table.

**Proposed fix:** Rename to `### Flag reference (v2.x)` and add the
v2.1/v2.2 rows: `--image`, `--output`, `--size`, `--init`,
`--config export-env`, `--setup`/`--init-wizard`. The "New in v2.0.0"
framing was useful at cutover; it is now archaeology.

---

### m3 -- README claims "1,510+ passing tests" -- ground truth uncertain

**File:** `README.md:18`
**Evidence:** copilot-instructions.md describes the suite as "~7 min, 600+
xUnit tests". The 1,510 number is the v1+v2 cumulative figure from the
v2.0.0 cutover. Three patch releases later, the per-tree numbers may have
shifted; nobody has rerun the count for the README.

**Proposed fix:** Re-run `dotnet test --list-tests | wc -l` against
both trees and update, or drop the precise count for "1,500+ unit tests
plus integration assertions". Puddy owns the source of truth here; refer
out and stop pinning a number we don't gate on.

---

### m4 -- ARCHITECTURE.md GHCR path differs from README

**Files:** `ARCHITECTURE.md:33` vs `README.md:264`
**Evidence:**

- ARCHITECTURE: `ghcr.io/schwartzkamel/azure-openai-cli/az-ai`
- README: `ghcr.io/schwartzkamel/azure-openai-cli`

**Problem:** Only one of these is the actual published image path; the
README example is the one users will copy-paste, so the ARCHITECTURE
mention is the suspected typo. Verify against the latest release workflow
and align.

**Proposed fix:** Edit ARCHITECTURE.md:33 to drop the trailing `/az-ai`
once Jerry confirms the image path.

---

### m5 -- `examples/espanso-ahk-wsl/README.md:46-49` lists 8 triggers as if exhaustive

**File:** `examples/espanso-ahk-wsl/README.md:46-49`
**Evidence:** "Every trigger (`:aifix`, `:airw`, `:aitldr`, `:aiexp`,
`:aic`, `:ai`, `:aishort`, `:aiyml`) routes to Azure ..." -- only 8 of 22
triggers. Reads as a complete enumeration; is not.

**Proposed fix:** Change "Every trigger (`:aifix`, ..., `:aiyml`)" to
"Every trigger in the kit (the full 22-trigger set plus 6 prompt-template
triggers; see §0 for inventory)". Sample-by-name is fine if you say it's
a sample.

---

### m6 -- `docs/config-reference.md` scopes itself to "v2.0.0+"

**File:** `docs/config-reference.md:7`
**Evidence:** "Scope: **v2.0.0+** (`az-ai`)."

**Problem:** Same class as M10 -- v2.0.0 was the floor at writing time;
two patch releases later "v2.x" is the more useful pin.

**Proposed fix:** "Scope: **v2.x** (`az-ai`)."

---

### m7 -- `:aiprompts` self-help trigger isn't called out in the kit README inventory line

**File:** `examples/espanso-ahk-wsl/README.md:30`
**Evidence:** Inventory table row reads
"`:aiquestion`, `:aiarch`, `:aicode`, `:aidata`, `:aicost`" -- five
templates. The actual file has six triggers, the sixth being `:aiprompts`
(the help / discoverability trigger). New users miss the
self-discoverability path.

**Proposed fix:** Append `:aiprompts` (the help trigger that prints the
template list) to that table cell, or add a one-line note: "Plus
`:aiprompts` for an in-line cheat sheet."

---

## NIT

### n1 -- README chrome contains em-dash (U+2014) and right-arrow (U+2192) glyphs

**File:** `README.md`
**Evidence:** `grep -P "[\u2014\u2192]" README.md` matches in lines
14, 18, 45, 47, 79, 80, 119, 141, 143, 257, 270, 281-298 ...

**Problem:** Project ASCII discipline says `--` for em-dash and `->` for
arrows. The README has historically been allowed marketing chrome (smart
characters survive in the existing audit too), but the Soup Nazi gate
catches them in any other Markdown.

**Proposed fix:** Either (a) accept the carve-out and document it
explicitly in `.github/skills/ascii-validation.md` (README is exempt), or
(b) rewrite to ASCII. Recommend (a) -- the README is read on the GitHub
landing page and rendered glyphs help legibility; the rule should still
hold elsewhere. Ask the Soup Nazi to bless the carve-out so we stop
re-litigating it.

---

### n2 -- `README.md:106` (`--config <path>`) and `README.md:107` (`--config set/get/list/reset/show`) are split into two table rows but describe the same flag

**File:** `README.md:106-107`
**Evidence:** Two rows, one flag, two different sub-syntaxes.

**Proposed fix:** Merge into one row with both syntaxes in the cell.
Cosmetic.

---

## Cross-cutting integrity checks (non-findings, recorded for completeness)

The following checks **passed**:

- **Agent fleet inventory.** `ls .github/agents/*.agent.md | wc -l` returns 28.
  AGENTS.md table lists 28 (1 + 5 + 22). Every file in the directory is
  represented in the table; every table row has a corresponding file.
  Roster integrity is sound -- the only drift is the count copy in
  README.md / persona-guide.md / copilot-instructions.md (M1, M2, M3).

- **Skill inventory.** `ls .github/skills/*.md` returns 12 skill files
  plus `README.md`. AGENTS.md "Skills" table lists 12, all matching by
  filename. `.github/copilot-instructions.md` references the same 12 by
  unquoted name. No orphaned references, no orphaned files.

- **CHANGELOG -> exec-reports cross-references.** S03E01 and S03E02 are
  both linked from the v2.2.0 entry. The linked exec-report files exist.
  Other references (S02E37 / S02E38) also resolve.

- **`docs/prompts/README.md`.** Lists `system-prompt-master.md` and
  `task-templates.md` with correct relative paths and updated roadmap
  checklist. New files exist on disk. No drift here -- this index is
  current.

- **`PROMPT-TEMPLATES-INTEGRATION.md`** exists at
  `examples/espanso-ahk-wsl/PROMPT-TEMPLATES-INTEGRATION.md` and is linked
  from the kit README. Not deep-reviewed in this pass; sample-checked
  for existence and link integrity only.

- **`examples/espanso-ahk-wsl/README.md` -- §0 trigger inventory.** Lists
  the new `ai-prompts.yml` and `az-ai-prompts.ahk` files in the right
  table; lists the master system prompt and task-templates docs in the
  Documentation sub-table. The kit-level integration is good; the gap is
  purely upward (root README) and laterally (espanso-ahk-integration).

---

## Recommendations (prioritized)

The fix wave for this audit should sequence as follows. Each item is
sized for a single small commit. Total estimated cost: half a day,
mostly for whoever owns C2.

1. **Fix the install-table version (C1).** README.md:248-255 -- five-minute
   change. Owner: Elaine.
2. **Resolve the `:aidata` collision (C2).** Rename in `ai-prompts.yml`,
   `ahk/az-ai-prompts.ahk`, kit README, `PROMPT-TEMPLATES-INTEGRATION.md`,
   and CHANGELOG `[Unreleased]` Changed entry. Owner: Maestro
   (prompt-template stewardship), Puddy reviews.
3. **Cross-fix the agent-count drift (M1, M2, M3).** Three string edits or
   one structural edit (drop the count entirely). Owner: Elaine.
4. **Refresh ROADMAP.md current-release line (M4).** One-line edit + an
   amendment to the `changelog-append` skill so it auto-bumps on release
   cut. Owner: Mr. Lippman.
5. **Backfill CHANGELOG `[Unreleased]` for commit 905515e (M5).** Append
   the `feat(prompts)` Added entry. Wilhelm follows up on whether the
   exec-report-check gate has a hole here. Owner: Mr. Lippman + Wilhelm.
6. **Add Image-mode row to use-cases.md (M6).** One row, link to README
   anchor as stop-gap; file FR for `use-cases-image.md`. Owner: Elaine.
7. **Add the prompts sub-section to README's Documentation block (M8).**
   Three new bullet links. Owner: Elaine.
8. **Add prompt-template hotkey table to kit README (M9).** Pasteable
   table from the existing `az-ai-prompts.ahk` source. Owner: Elaine.
9. **Update persona-guide and config-reference v2.0.0 pins (M10, m6).**
   Verify with Kramer whether the per-persona `model` override is now
   wired; update accordingly. Owner: Elaine + Kramer.
10. **Reconcile perf claims in README (M11).** Either ship the v2.2.0
    baseline or rename the file to drop the version pin. Owner: Bania.
11. **Carve docs/espanso-ahk-integration.md refresh into its own episode
    (M7).** Too large for this fix wave. File as findings-backlog,
    candidate for S03E03. Owner: Babu / Maestro pair.
12. **Sweep the minors and nits.** One-pass commit. Owner: Elaine.

---

## Process observations

Three patterns showed up in this audit that are worth naming:

- **Hardcoded version strings rot in lockstep.** Every release we pay the
  same tax in the same files (README install table, ROADMAP current
  release, ARCHITECTURE scope statement, persona-guide v2.0.0 pin). A
  release-cut hook that templates these from the csproj `<Version>` would
  zero the recurring debt. Worth a Lippman + Jerry collaboration --
  candidate for an FR.
- **The exec-report-check gate did not stop commit 905515e from landing
  without a CHANGELOG `[Unreleased]` entry.** Either the gate does not
  inspect the CHANGELOG, or it was opted out via `Skip-Exec-Report:`
  trailer. Either way, the changelog-append skill was not enforced.
  Wilhelm should triage this; if the exec-report and the changelog
  append are conceptually one push-time obligation, the gate should
  enforce both.
- **New work tends to integrate sideways into the kit but not upward into
  the root narrative.** The prompt-templates feat is exemplary in
  `examples/espanso-ahk-wsl/` and in `docs/prompts/`. It is invisible
  from `README.md` and from `docs/espanso-ahk-integration.md`. We need a
  "did you remember to update the front door?" item on the
  exec-report-format checklist. Lloyd Braun is the right voice for that
  catch.

---

## Sign-off

Audited by **Elaine Benes**, Technical Writer, 2026-05-06.

Status: **22 findings (2 CRITICAL, 11 MAJOR, 7 MINOR, 2 NIT)**.
Recommendation: ship a fix-wave PR addressing C1, C2, M1-M5, M8, M9, m1
(the cheap ones) before S03E03 cuts. Carry M7 and M11 as their own
episodes.

S03E03 unblock status: **clear**. None of the open findings here block
new docs work; they are stewardship debt against existing prose. The
prompt-templates integration is good enough at the kit and prompt-library
layers to be built on top of. Just close C1 and C2 before the next
release tag, please.

Next audit recommended: **2026-06-15** (six-week cadence) or immediately
after the next minor cut (v2.3.x), whichever is sooner.
