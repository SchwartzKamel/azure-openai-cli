# Persona & Squad Guide (v2)

> Target audience: anyone using `--persona`, `--squad-init`, or the `.squad/`
> directory in v2.0.0. For internal design notes, see
> [`v2-migration.md`](v2-migration.md) Phase 4. For migration specifics, see
> [`migration-v1-to-v2.md`](migration-v1-to-v2.md).

Personas are named AI team members, each with a distinct system prompt, an
explicit tool allow-list, and a persistent memory file. The Squad system lets
you keep those definitions in version control next to your project, so every
contributor gets the same team with the same accumulated context.

---

## Overview

A **persona** is three things glued together:

1. A **system prompt** (what the model is told to be).
2. A **tool allow-list** (which built-in tools the persona may call).
3. A **memory file** at `.squad/history/<name>.md` (what happened in past
   sessions, prepended to the system prompt on each run).

A **Squad** is a collection of personas plus optional **routing rules** --
keyword patterns that let `--persona auto` pick the best persona for a task.

All configuration lives in two places:

- `.squad.json` -- the Squad definition (personas + routing rules). Commit it.
- `.squad/` -- runtime state (memory files + decision log). Also commit it;
  that is how knowledge compounds across contributors.

Persona mode **implies agent mode**. If a persona declares any tools, its
allow-list overrides `--tools` on the command line.

---

## Quickstart

```bash
# 1. Scaffold a Squad in the current directory.
az-ai --squad-init
# Writes:
#   .squad.json           -- 5 default personas + routing rules
#   .squad/history/       -- empty; memory files are created on first use
#   .squad/decisions.md   -- shared decision log
#   .squad/README.md      -- inline reference

# 2. Run a task under a named persona.
az-ai --persona coder "Refactor BUG-142 out of src/auth.py"

# 3. Let the coordinator pick the persona for you.
az-ai --persona auto "Review the new token-refresh flow for vulnerabilities"
# → 🎭 Auto-routed to: security (Security Auditor)

# 4. See who's on the team.
az-ai --personas
```

Interactive runs print a one-line banner to **stderr**:

```text
🎭 Persona: coder (Software Engineer)
```

The banner is silent under `--raw` and `--json`. Stdout is never contaminated.

---

## Concepts

### Persona

A `PersonaConfig` entry in `.squad.json`. Fields:

| Field           | Required | Description                                                        |
|-----------------|:--------:|--------------------------------------------------------------------|
| `name`          | ✅ Yes | Unique identifier, case-insensitive when matched by `--persona`    |
| `role`          |          | Short human-readable title; printed in the 🎭 banner                |
| `description`   |          | One-liner shown by `--personas`                                    |
| `system_prompt` | ✅ Yes | Full system prompt. Overrides `--system` on the command line.      |
| `tools`         |          | Tool names: any of `shell`, `file`, `web`, `clipboard`, `datetime`, `delegate`. Empty = no tool restriction; falls back to `--tools` or the default allow-list. |
| `model`         |          | Optional per-persona model override. Reserved -- not yet wired in 2.0.0. |

### Memory

For every persona run, `PersonaMemory` does two things:

1. **On entry** -- reads `.squad/history/<name>.md` (if present) and prepends
   the contents to the persona's system prompt under the header
   `## Your Memory (from previous sessions)`. The file is capped at **32 KB at
   read time**; older content is truncated from the front with a
   `...(earlier history truncated)...` marker. Writes are not capped.
2. **On exit** -- appends a session summary to the same file. Format:

   ```markdown
   ## Session -- 2026-04-20 14:32 UTC
   **Task:** <first 200 chars of prompt>...
   **Result:** <first 500 chars of model response>...
   ```

   On cancel (Ctrl-C) the summary reads `**Result:** [cancelled]` so you can
   see at a glance which sessions were interrupted.

Memory files are plain Markdown. Edit them by hand to prune noise, correct
bad learnings, or seed a new persona with institutional knowledge.

### Routing

`--persona auto` runs the `SquadCoordinator`:

1. Lowercase the prompt.
2. For each routing rule, split `pattern` by comma, count how many keywords
   are `Contains`-substring matches against the lowercased prompt.
3. Pick the **highest-scoring** rule. Ties break by **array order in
   `.squad.json`**. No match → fall back to the **first persona** in the
   array.
4. Look up the winning rule's `persona` name in `Personas`.

This is deterministic, offline, and cheap. No model is called to pick the
persona -- that would defeat the purpose.

Routing rules are a list of:

```json
{
  "pattern": "security,vulnerability,cve,owasp,harden,credential,secret",
  "persona": "security",
  "description": "Security tasks"
}
```

Tune them by editing `.squad.json`. Rules are additive; reorder them to
change tie-breaking.

### Decisions log

`.squad/decisions.md` is a shared file personas can append to. It is capped
at 32 KB on read (same rules as memory). Use it for cross-persona context
that does not belong in any single persona's history -- for example, an
architecture trade-off the `architect` recorded that the `coder` should
honour on the next session.

---

## Reference

### Flags

| Flag                 | Purpose                                                          |
|----------------------|------------------------------------------------------------------|
| `--squad-init`       | Scaffold `.squad.json` + `.squad/`. Idempotent -- refuses if `.squad.json` already exists. |
| `--persona <name>`   | Run under the named persona. Case-insensitive. Unknown name exits 1 with the list of valid names. |
| `--persona auto`     | Let the coordinator pick the persona. Falls back to the first persona on no match. |
| `--personas`         | List configured personas with descriptions.                       |

### Files

| Path                           | Role                                                 |
|--------------------------------|------------------------------------------------------|
| `.squad.json`                  | Squad definition (team + personas + routing).        |
| `.squad/history/<name>.md`     | Per-persona memory. One file per persona.            |
| `.squad/decisions.md`          | Shared decision log.                                 |
| `.squad/README.md`             | Reference copy of the structure, written at init.    |

### `.squad.json` schema

Verified against `azureopenai-cli-v2/Squad/SquadConfig.cs`:

```json
{
  "team": {
    "name": "Default Squad",
    "description": "AI team for your project."
  },
  "personas": [
    {
      "name": "coder",
      "role": "Software Engineer",
      "description": "Writes clean, tested, production-ready code.",
      "system_prompt": "You are an expert software engineer. …",
      "tools": ["shell", "file", "web", "datetime"],
      "model": null
    }
  ],
  "routing": [
    {
      "pattern": "code,implement,build,fix,refactor,feature,bug",
      "persona": "coder",
      "description": "Implementation tasks"
    }
  ]
}
```

### Default personas (after `--squad-init`)

| Name        | Role                 | Tools                            | Routing keywords                                                      |
|-------------|----------------------|----------------------------------|------------------------------------------------------------------------|
| `coder`     | Software Engineer    | shell, file, web, datetime       | `code, implement, build, fix, refactor, feature, bug`                  |
| `reviewer`  | Code Reviewer        | file, shell                      | `review, audit, check, inspect, quality`                               |
| `architect` | System Architect     | file, web, datetime              | `design, architecture, system, scale, pattern, migration`              |
| `writer`    | Technical Writer     | file, shell                      | `document, readme, docs, guide, tutorial, changelog`                   |
| `security`  | Security Auditor     | file, shell, web                 | `security, vulnerability, cve, owasp, harden, credential, secret`      |

Verified against `azureopenai-cli-v2/Squad/SquadInitializer.cs`.

### Precedence rules

- **System prompt** -- persona's `system_prompt` **overrides** `--system` and
  `SYSTEMPROMPT`.
- **Tools** -- a non-empty `tools` array on the persona **overrides**
  `--tools` and forces `--agent` on. Empty `tools` falls back to `--tools`
  or the default allow-list.
- **Model** -- persona-level `model` is reserved; for now resolution is the
  same as a bare run: CLI `--model` > `AZUREOPENAIMODEL` > UserConfig smart
  default > `gpt-4o-mini`.

---

## Cast personas (the show lives on)

Starting with the S02E30 *The Cast* release, `--squad-init` seeds **17**
personas, not 5: the original generics (`coder`, `reviewer`, `architect`,
`writer`, `security`) plus **12 Seinfeld-themed cast members** compressed
from the project's [`AGENTS.md`](../AGENTS.md) archetypes into runnable
system prompts. The cast lived in process documentation for two seasons;
now it ships in the binary.

The 12 cast personas, by domain:

| Persona          | Role                                | When to reach for them                                      |
|------------------|-------------------------------------|--------------------------------------------------------------|
| `costanza`       | Product Manager                     | Latency obsession, FR proposals, preference schema          |
| `kramer`         | Engineer (C#, Docker, Azure OpenAI) | Hands-on implementation, AOT-clean code, hardening tests    |
| `elaine`         | Technical Writer                    | README, ADRs, prose that earns its place                    |
| `jerry`          | DevOps / Modernization              | Dockerfile, Makefile, CI hygiene, dependency sweeps         |
| `newman`         | Security Inspector                  | Threat models, blocklists, SSRF, supply chain               |
| `larry-david`    | Showrunner / Orchestrator           | Multi-step planning, fleet dispatch, episode framing        |
| `lloyd-braun`    | Junior Developer / Onboarding lens  | First-hour audits, glossary, jargon-free explainers         |
| `maestro`        | Prompt Engineer                     | Prompt library, model A/B, temperature cookbook             |
| `mickey-abbott`  | Accessibility / CLI Ergonomics      | NO_COLOR, screen-reader output, --raw discipline            |
| `frank-costanza` | SRE / Observability                 | SLOs, opt-in telemetry, runbooks, ralph-mode safety review  |
| `soup-nazi`      | Code Style / Merge Gatekeeper       | Conventional Commits, dotnet format, docs-lint              |
| `mr-wilhelm`     | Process / Change Management         | PR gates, retros, ADR stewardship, release flow             |

Each cast persona is the runtime compression of its `.github/agents/<name>.agent.md`
archetype: voice, focus areas, standards, and the explicit "things you do NOT
do" boundary that keeps personas in their lane.

### Direct-name routing wins over keyword routing

`--persona auto` (or any call to `SquadCoordinator.RouteByKeyword`) now
prefers direct cast-name matches over generic keyword scoring:

```bash
az-ai --persona auto "kramer review this csproj"
# -> 🎭 Auto-routed to: kramer (Engineer)
# (NOT 'reviewer', even though "review" is in reviewer's keyword list.)

az-ai --persona auto "ask larry-david to greenlight the next episode"
# -> 🎭 Auto-routed to: larry-david (Showrunner)
```

The 5 generic names (`coder`, `reviewer`, `architect`, `writer`, `security`)
are deliberately excluded from direct-name precedence -- they collide with
their own routing keywords ("security audit" should still route to the
`security` persona), so they go through the standard keyword-score path.

### Existing `.squad.json` files are untouched

Only `--squad-init` (a fresh seed) gets the cast. If your repo already has a
`.squad.json`, nothing changes -- the file is the source of truth and will
not be rewritten. To pull in the cast on an existing repo:

```bash
mv .squad.json .squad.json.bak
az-ai --squad-init                # writes the 17-persona default
# Then merge any custom personas from .squad.json.bak into .squad.json by hand.
```

### Relationship to the archetype files

The `.github/agents/<name>.agent.md` files remain the **canonical voice**
for each cast member -- they are what GitHub Copilot's custom-agent system
loads, and they're also where Larry David (the showrunner) reads to cast
episodes. The runtime persona is a compressed, in-character system prompt
derived from that archetype. If voice drifts, the archetype wins; the
runtime prompt is regenerated to match.

See [`AGENTS.md`](../AGENTS.md) for the full 25-agent roster, the dispatch
pipeline diagram, and the supporting players who did NOT make the runtime
cut (those archetypes still operate as Copilot custom agents during
development; they just are not seeded into your local `.squad.json`).

---



```bash
# One-shot coding task.
az-ai --persona coder "Implement a Redis-backed rate limiter in src/ratelimit/"

# Security review of a specific file.
az-ai --persona security "Audit src/api/tokens.py for injection and SSRF bugs"

# Documentation from scratch -- note the 'docs' keyword triggers --persona auto.
az-ai --persona auto "Write docs for FR-014 (multi-provider support)"
# → 🎭 Auto-routed to: writer (Technical Writer)

# Architecture trade-off, piped prompt.
git diff main..feature/payments | az-ai --persona architect \
  "Evaluate this diff for scaling risks and log your decision"

# Script-friendly: silent banner, JSON errors.
az-ai --persona coder --json "$(cat task.md)"

# Espanso / AHK safe: --raw suppresses banners and telemetry.
echo "Rephrase this politely: $CLIPBOARD" | az-ai --raw --persona writer
```

Combine with Ralph for autonomous review loops:

```bash
az-ai --ralph --validate "pytest -x" --persona coder \
  "Fix the failing auth tests until the suite is green"
```

The persona's tool allow-list applies inside the Ralph loop, so a `reviewer`
persona configured without `shell` cannot execute commands even if the model
really wants to.

---

## FAQ

**Does persona mode require a network call to pick the persona?**
No. `--persona auto` is deterministic keyword matching. No API call is made
until you have a winning persona and a prompt to run under it.

**Can I have more than 5 personas?**
Yes. Edit `.squad.json`. The defaults are a starting point; add, remove, or
rename freely. The 32 KB memory cap is per persona, not per Squad.

**What happens if two routing rules tie?**
The one that appears **earlier in the `routing` array** wins. Reorder to
change the tie-break.

**Where is memory stored?**
`./.squad/history/<name>.md` relative to the current working directory. If
you run `az-ai --persona coder` from two different project roots, the
`coder` persona has two independent memories. This is intentional -- each
project gets its own Squad.

**Can I disable memory for a run?**
Delete (or rename) `.squad/history/<name>.md` before the run. The file will
be recreated with just the new session's entry.

**Does `--raw` suppress the memory read?**
No. `--raw` suppresses the 🎭 banner and other stderr output. Memory is
still read into the system prompt; only the visible output is silenced.

**How do I share a persona with my team?**
Commit `.squad.json` and the relevant `.squad/history/*.md` files.
Contributors who clone the repo pick up the team and its accumulated
context automatically.

**Can personas call other personas?**
Not directly. Use `delegate_task` in agent mode for sub-agent recursion.
`delegate_task` spawns a bounded child agent, not a named persona.

---

## Troubleshooting

### `No .squad.json found. Run --squad-init first.`

You invoked `--persona` without a Squad config. Fix:

```bash
az-ai --squad-init
```

If `.squad.json` exists but is in a parent directory, `cd` into the project
root first -- `SquadConfig.Load()` reads from the **current working directory**.

### `Unknown persona '<name>'. Available: coder, reviewer, …`

Typo in `--persona <name>`, or the persona was removed from `.squad.json`.
List the available names:

```bash
az-ai --personas
```

Names are case-insensitive.

### `--persona auto` keeps picking the wrong persona

The routing rules are keyword substring matches. Two common causes:

1. Your prompt contains a keyword that belongs to another rule. Example:
   `"refactor the security audit script"` contains both `refactor` (coder)
   and `security` (security). The one with more keyword hits wins; on ties,
   the one earlier in the `routing` array wins.
2. No keywords match at all, so the coordinator falls back to the first
   persona in the array.

Fix by editing `.squad.json` -- reorder rules, remove overlapping keywords,
or make the patterns more specific.

### Memory file seems to "forget" older sessions

Expected. `ReadHistory` truncates to 32 KB, keeping the tail. If you want
the full history, open the raw file -- it is not truncated on disk, only on
read. To compact, edit the file by hand and keep the summary you want.

### Persona-mode tools don't match what I passed with `--tools`

Expected. A persona with a non-empty `tools` array **overrides** `--tools`.
Inspect `az-ai --personas` to see each persona's configured tools, or edit
`.squad.json` to widen the allow-list.

### I see the 🎭 banner in my Espanso output

You forgot `--raw`. The banner is emitted on stderr, but some text expanders
capture combined output. Either add `--raw` or redirect stderr:

```bash
az-ai --raw --persona writer "Rephrase: $INPUT"
# or
az-ai --persona writer "Rephrase: $INPUT" 2>/dev/null
```

### Decisions or memory don't survive across machines

Commit `.squad/` to your repo. It is designed to be version-controlled --
that is how the team's accumulated knowledge travels.

---

**Maintained by**: Elaine (docs).
**See also**: [`use-cases-ralph-squad.md`](use-cases-ralph-squad.md) for the
Ralph + Squad interaction recipes, [`AGENTS.md`](../AGENTS.md) for the
meta-agent roster (distinct from user-defined personas).
