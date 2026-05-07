# S03E02 -- *The Library Cop's Word Limit*

> *Real user reports the snappy ~150-char replies are the best ones, asks for cap discipline. We swear in Lt. Bookman, ship a tier doctrine, tighten three triggers, and add a snap-tier and a YAML-authoring trigger so future triggers can be drafted by AI from inside Espanso itself.*

**Commit:** `pending` (ships at end of episode)
**Branch:** `main` (direct push)
**Runtime:** ~50 min real time
**Director:** Larry David (showrunner)
**Cast:** Lt. Bookman (new), Kramer (inline), Bookman tier-doctrine, lint script

## The pitch

S03E01 closed the bash injection surface and shipped 20 triggers. The user came back with a UX observation that mattered: the *snappy* replies (~150-200 chars) feel best in chat apps. Anything longer means the user has to wait, and then has to *re-read* before pasting. The network round-trip we cannot speed up -- Azure is in Texas; the user is not. But the *generation* time is a knob. `--max-tokens` is that knob, and we'd been spinning it freely.

So the episode has three concrete jobs:

1. **Establish a tier doctrine** so trigger length is not a per-author decision made in isolation. Snap (60 tok) / Chat (250) / Document (800) / Mirror (1500, length tracks input) / Free (4096, user-controlled).
2. **Tighten the existing triggers** that were over-budgeted. `:aiq` was at 200 tok for a "1-2 short sentences" reply -- 60 will do. `:aireply` at 800 was double what a chat-thread reply needs. `:aitldr` came in at 150 tok with a generic system prompt; 120 + a hard char cap fits the brief better.
3. **Make the system extensible from inside Espanso itself.** The user wanted "ask AI to add a trigger and just do it." We took the espanso-trigger path over an AutoHotkey hotkey -- single ecosystem, espanso reloads on file save, no new privilege surface. v1 puts the generated YAML on the clipboard; the user pastes into `ai-wsl.yml` themselves and keeps config ownership.

## Scene-by-scene

### Act I -- Planning

User question of the episode: *espanso trigger or AutoHotkey hotkey for the YAML-authoring tool?*

Decision: espanso trigger. Three reasons. (1) The user already lives in espanso for AI; AHK would split the workflow across two tools. (2) Espanso's file watcher reloads automatically on save, so we don't need an orchestrator to tell it the file changed. (3) Clipboard-write keeps us out of file-edit-privilege territory that Newman would object to. v1 ships clipboard-only; a v2 with file-write + post-write lint validation can come later if the workflow demands it.

Casting question: who owns this? The closest existing fits were Russell Dalrymple (UX/presentation), Mickey Abbott (CLI ergonomics), Maestro (prompt engineering), and Morty (FinOps/cost). None of them owned *response brevity as a contract*. So we hired one. Lt. Bookman, the library cop, who came after Jerry over Tropic of Cancer in 1971 and now comes after triggers that talk too much. Clipped, accusatory, no patience for preamble.

### Act II -- Fleet dispatch

| Wave | Cast | Outcome |
|------|------|---------|
| **1** | Kramer (inline) | Drafted Lt. Bookman as `.github/agents/bookman.agent.md`. Tier doctrine codified in the agent file as the canonical source. Voice tuned: clipped, accusatory, comes after long-windedness. |
| **2** | Kramer (inline) | Updated `AGENTS.md`: 27 -> 28 agents, 21 -> 22 supporting players, added "brevity" to the supporting-player narrative. New row in the supporting players table. |
| **3** | Kramer (inline) | Tightened three triggers per Bookman's doctrine: `:aiq` 200 -> 60 max-tokens with stricter `<=150 char, 1 sentence` system prompt; `:aireply` 800 -> 400 with `<=4 short sentences` language; `:aitldr` 150 -> 120 with `<=300 chars total` cap. Mirror-tier triggers (rewrite, translate, fix) intentionally NOT capped -- their output length must track input. Free-tier (`:ai`) untouched per user direction. |
| **4** | Kramer (inline) | Added `:aishort` -- snap-tier free-form trigger. 60 max-tokens, ~150 char target, 1 sentence. The fast lane for chat-app replies where you don't need a paragraph. |
| **5** | Kramer (inline) | Added `:aiyml` -- form-input trigger that generates a new Espanso YAML block from a natural-language description, places it on the clipboard for the user to paste into `ai-wsl.yml`. System prompt teaches the AI the unified S03 pattern (trigger/$trigger match, SendKeys-safe `$ph`, two BACKSPACE ops, try/finally retype, here-string bash, empty-stdout banner) and the tier table so it picks`--max-tokens` correctly. Output is `Set-Clipboard`'d, NOT typed -- typing a YAML block into a chat app would be a mess. |
| **6** | Kramer (inline) | Updated trust-model header: added `:aiyml` privacy callout (clipboard-write surface, design prompt egresses to Azure), added `:aishort` and `:aiyml` to the form-trigger roster, codified the tier doctrine table inline so the YAML is self-documenting. Header also points at `bookman.agent.md` as the canonical tier source. |
| **7** | Kramer (inline) | Re-ran `scripts/lint-espanso-yml.sh`: 22 triggers checked, all green. Mirrored to `%APPDATA%\espanso\match\ai-wsl.yml` so the user's live install picks up the changes immediately. |

### Act III -- Ship

Preflight clean. Format + build + 657 unit tests + 35 integration tests + espanso-yml-lint + ascii-validation all pass. Direct push to `main`. CI all 5 jobs green.

## What shipped

**Production config**

- `examples/espanso-ahk-wsl/espanso/ai-windows-to-wsl.yml` -- 20 -> 22 triggers; three tightened (`:aiq`, `:aireply`, `:aitldr`); two new (`:aishort`, `:aiyml`); trust-model header now codifies the tier doctrine and adds `:aiyml` privacy callout.
- Mirror at `%APPDATA%\espanso\match\ai-wsl.yml` updated.

**New cast member**

- `.github/agents/bookman.agent.md` -- Lt. Bookman, output economy and brevity discipline. Owns the response-length tier doctrine (canonical source), `--max-tokens` budgets, and the system-prompt brevity language ("Output ONLY the X. No preamble. No markdown.").

**Roster**

- `AGENTS.md` -- 27 -> 28 agents (1 showrunner + 5 main + **22** supporting). Bookman in the supporting players table.

## Lessons

- **Per-trigger thinking, not blanket caps.** The first instinct on "cap output length" is to set a global `--max-tokens` ceiling. Wrong -- some triggers (rewrite, translate, fix-grammar, anonymize) MUST mirror input length or they truncate the user's content. Tier U exists exactly so we don't make that mistake.
- **System prompt + token budget = belt and suspenders.** The token budget is the hard ceiling (model literally can't exceed it). The brevity language in the system prompt is the *soft* enforcement -- it makes the model write tighter sentences within the ceiling. Both layers needed; either one alone is leaky.
- **Espanso > AutoHotkey for tool-extension UX.** Single ecosystem, file-watcher auto-reload, clipboard write-out keeps privilege surface small. The AHK option is on the table for future episodes if we need GUI-driven flows (multi-step wizards, file-system pickers) -- but `:aiyml` proves you don't need it for "let AI scaffold a new trigger".
- **Self-extensible config is a force multiplier.** With `:aiyml` shipped, *the user can ask AI to author the next trigger from inside the trigger system itself.* That changes the cadence: previously a new trigger meant editing YAML by hand or asking Kramer in a session. Now it's a 3-second prompt and a paste.
- **Casting the right verb.** Bookman exists because no existing agent was a clean fit for "owns response brevity as a contract". Russell handles aesthetics; Mickey handles a11y; Maestro tunes prompts; Morty watches the bill. Brevity-as-discipline was an unowned seam. Hiring (file-creating) the right specialist is cheaper than overloading an existing one.

## Open threads / next episode hooks

- `:aiyml` v2: write to a tempfile, run `lint-espanso-yml.sh` against it, only `Set-Clipboard` if lint passes. Otherwise output the lint errors. Punted because the lint script lives in the repo (`~/tools/azure-openai-cli/scripts/`), not on the espanso path -- needs a discovery mechanism or a packaged copy.
- Bania benchmark for tier latencies. Bookman set the budgets from first principles (~30 ms/token at GPT-4o-mini). Bania should measure actuals and flag any triggers where the empirical p50 doesn't match the tier.
- Quarterly tier-audit on the calendar (Bookman's deliverable). First one due once we hit S03E10.
