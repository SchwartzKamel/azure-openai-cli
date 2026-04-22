# Livestream Checklist

> I'm Keith Hernandez. The stream starts when the chat sees a clean terminal, not when OBS says "live." This is the checklist we run before every episode -- Twitch, YouTube Live, LinkedIn Live, doesn't matter. Two copies: one on paper next to the keyboard, one pinned in the producer's tab.

**Owner:** Keith Hernandez (DevRel)
**Pairs with:** [`../speaker-bureau.md`](../speaker-bureau.md), [`../demos/README.md`](../demos/README.md), Frank Costanza's on-call rotation (chat moderation escalation)
**Last reviewed:** 2026-04-22 (v2.0.4)

---

## Table of contents

1. [T-24h -- booking + rehearsal](#1-t-24h--booking--rehearsal)
2. [T-60m -- fresh machine setup](#2-t-60m--fresh-machine-setup)
3. [T-15m -- env + terminal + audio + video](#3-t-15m--env--terminal--audio--video)
4. [T-2m -- go-live pre-flight](#4-t-2m--go-live-pre-flight)
5. [Live -- demo risk-reduce](#5-live--demo-risk-reduce)
6. [Asciinema fallback](#6-asciinema-fallback)
7. [Q&A template](#7-qa-template)
8. [T+0 -- post-show](#8-t0--post-show)

---

## 1. T-24h -- booking + rehearsal

- [ ] Episode outline written, linked in the show-run doc
- [ ] Guest (if any) has signed the [guest release -- template pending] and received the guest brief
- [ ] Every command in the run-sheet executed **end-to-end on a fresh machine** (container, VM, or borrowed laptop). No "trust me, this works."
- [ ] Every command also pasted into the show-run doc as a copy-pasteable snippet -- if the live demo fails, the block falls back to read-aloud
- [ ] `az-ai-v2 --version --short` recorded in the show-run so chat can sanity-check what we're on
- [ ] Backup network identified (phone hotspot, tethered laptop) -- tested, not theoretical
- [ ] Asciinema recording of the hero demo captured as the dead-WiFi fallback (see §6)
- [ ] Chat-moderation brief reviewed with whoever is on mod duty; Frank's escalation path in the pinned note

## 2. T-60m -- fresh machine setup

Run on the streaming machine *before* you open OBS. A cold environment catches drift that a warm one hides.

- [ ] OS updates paused for the next 2 hours (no surprise reboots)
- [ ] Screen saver off, auto-lock off, Do Not Disturb **on**
- [ ] Browser profile: stream-only. No tabs with secrets, email, Slack, or internal docs. A *fresh profile* is safer than "I'll be careful."
- [ ] Window notifications silenced (Slack, Discord, email, calendar, IDE)
- [ ] Desktop clean -- no screenshots, no stray downloads with embarrassing names
- [ ] `~/.bash_history` / `~/.zsh_history` either (a) cleared for the stream shell, or (b) the stream shell is a fresh one with `HISTFILE=/dev/null`
- [ ] `env | grep -iE 'key|token|secret|password'` returns **nothing** in the shell you're about to share. Rotate anything that appears.
- [ ] `az-ai-v2` binary present at the path the demo assumes; `AZ_AI_BIN` exported if the demo uses it
- [ ] Azure OpenAI test credentials loaded from a **scoped** key (low-rate, billing-capped), not the production one
- [ ] Persona memory, history, and scratch directories reset to the demo-ready state

## 3. T-15m -- env + terminal + audio + video

### Terminal

- [ ] Shell: the one the audience expects (default: `bash` or `zsh`; match the OS)
- [ ] Terminal dimensions: **120 × 40** (columns × rows). Confirm with `tput cols && tput lines` or `stty size`.
- [ ] Theme: dark background, high-contrast foreground. Monokai / Dracula / One Dark are safe defaults. Match `docs/demos/hero-gif.md` if we want brand continuity.
- [ ] Font: a ligature-free monospace at **18 pt** minimum. Bigger if the stream target is mobile-heavy.
- [ ] Prompt: short, no username, no hostname, no git branch. One glyph + space. Long prompts eat screen real estate and leak info.
- [ ] Line wrapping tested: the longest command in the run-sheet fits in 120 cols without wrap

### Audio

- [ ] Microphone: lapel or cardioid desk mic, **not** laptop built-in
- [ ] Input gain set so normal speech peaks at ~−12 dBFS, never clips
- [ ] Monitor (headphones) on so you hear yourself; avoid speaker echo
- [ ] Room: HVAC aware, keyboard away from the mic, water within reach, phone silent
- [ ] Recorded test clip played back -- check for hum, hiss, clipping, room echo

### Video

- [ ] Camera at eye level, not laptop-low
- [ ] Lighting in front of you, not behind
- [ ] Background clean -- no whiteboard with yesterday's roadmap, no Post-its with anyone's handle
- [ ] Screen share scoped to a **single window** when possible -- never share the whole desktop unless the run-sheet specifically requires it

### OBS / stream software

- [ ] Scenes: `Intro`, `Camera + Terminal`, `Terminal full`, `Camera full`, `BRB`, `Outro` -- all tested with transitions
- [ ] Lower-third copy correct and spelled right (episode title, handle, project URL)
- [ ] Recording-to-local enabled as a backup even if the platform records server-side
- [ ] Bitrate reasonable for the uplink -- test at 80% of available bandwidth, not 100%
- [ ] Chat window visible on a secondary monitor, *not* in the streamed scene

## 4. T-2m -- go-live pre-flight

- [ ] `clear` run in the stream terminal; last visible line is a clean prompt
- [ ] `env | grep -iE 'key|token|secret|password'` -- one last time. One last time.
- [ ] Water, not coffee, within arm's reach
- [ ] Phone on airplane mode (but reachable off-scene in case the hotspot is the backup)
- [ ] Timer set for the episode length; warning ping at `T-10m` and `T-2m`
- [ ] Pinned chat message ready (episode title, project URL, code of conduct link)
- [ ] Guest (if any) audio-checked; their microphone doesn't double with yours
- [ ] Exhale. Roll the intro.

## 5. Live -- demo risk-reduce

Rules of the stage, in order:

1. **Read the next command aloud before pressing Enter.** Audience processes faster when they hear it framed.
2. **If a command produces more than a screenful, redirect to a file and `head` it.** Scrollback is not a demo.
3. **Never paste from a hidden buffer the audience can't see.** If it's not in the show-run doc, it's not in the demo.
4. **If a command fails, announce it.** "That didn't do what I expected. The fallback is in my notes -- one moment." No pretending.
5. **If the network drops, go to the asciinema fallback (§6). Do not wait out the outage live.**
6. **Keep one hand off the keyboard when speaking.** It slows you down. Slow is watchable.
7. **Time-box the demo.** If the run-sheet says 8 minutes and you're at 12, cut to the next segment. Audience forgives a skipped cool-down; they don't forgive a 20-minute debugging side quest.
8. **No live edits to secrets, `~/.ssh/`, or `/etc/` from the stream shell. Ever.**
9. **If chat surfaces a real bug, thank them and file it off-stream.** Don't fix a bug live unless the episode is *explicitly* a bug-fixing stream.
10. **Tip your guest and your mod on the way out.** Literally thank them by name on camera.

## 6. Asciinema fallback

When the WiFi dies -- and it will -- switch to a pre-recorded asciinema and narrate over it.

### Record the fallback (T-24h or earlier)

```bash
# record the hero demo on a known-good machine, known-good network
asciinema rec docs/demos/recordings/livestream-fallback-$(date +%Y%m%d).cast \
  --title "azure-openai-cli livestream fallback" \
  --idle-time-limit 2

# play it back to sanity-check before the stream
asciinema play docs/demos/recordings/livestream-fallback-*.cast
```

Keep the `.cast` file committed (they're small). Pair it with a notes block in the show-run doc so the narration still lands without a live terminal.

### Play during the stream

```bash
# during the outage -- full-screen the terminal, then:
asciinema play /path/to/livestream-fallback-YYYYMMDD.cast --speed 1.25
```

Narrate as if you were running it. Audience would rather hear a confident walk-through of a recording than watch a loading spinner for four minutes.

### When to invoke

- Streaming software reconnects more than once in 60 seconds
- A command hits the network twice and times out both times
- Platform status page shows a red dot
- You feel the room temperature change -- trust it

## 7. Q&A template

Last 10-15 minutes of the episode.

**Producer / mod preps:** 3-5 pre-selected questions from chat + the project backlog, ranked by what the audience will learn from the answer.

**Host script:**

> "Alright -- last stretch is Q&A. [Mod name] has been keeping a list. I'm going to start with the ones that teach the room something, and if we've got time at the end I'll grab fresh ones from chat. Keep them coming -- one question per message, and if you want to stay anonymous just say so."

**Handling rules:**

- **Don't know the answer?** "I don't know. Here's where I'd look: [file / doc / person]. I'll follow up in the episode notes." Honest beats improvised.
- **Out-of-scope question?** "That's a great one -- out of scope for today. Drop it as a GitHub issue and we'll address it there, or save it for the follow-up episode."
- **Hostile question?** Mod's call. The host stays on the technical merits, doesn't engage tone. If it escalates, Frank Costanza's escalation path kicks in (mute / ban / report, in that order).
- **Vendor pitch in chat?** Ignore it. Keep moving. We're not a lead-gen channel.

**Close-out (30 seconds):**

> "That's the show. Project URL is pinned; the CHANGELOG for what we just demoed is at the top of the repo; if you want the stickers, there's a link in the description. Thanks to [guest], thanks to [mod], thanks for hanging out. See you next time."

Cut to outro. Leave the stream running the outro card for 60 seconds so slow-loaders catch it.

## 8. T+0 -- post-show

- [ ] Local recording saved and named `YYYY-MM-DD-<slug>.mkv` (or similar)
- [ ] VOD URL captured; chapter timestamps drafted before memory fades
- [ ] Chat log exported (platform permitting) for question-follow-up
- [ ] Follow-ups list filed as issues or under `docs/talks/<slug>/followups.md`
- [ ] Episode entry added to the livestream archive (create `docs/talks/livestream/` on the first one)
- [ ] Debrief memo written within 48 hours: what landed, what didn't, what to cut next time. Keep it short. Be honest. Tip your mods.

---

-- *Keith Hernandez. Lights up. Mic hot. The demo's rehearsed.*
