# Speaker Bureau

> I'm Keith Hernandez. If a CFP form, podcast producer, meetup organizer, or conference speaker-page asks for copy, they get what's on this page -- not a bespoke writeup. One file, one source of truth, no title inflation.

**Owner:** Keith Hernandez (DevRel)
**Last reviewed:** 2026-04-22 (v2.0.4)
**Pairs with:** [`docs/launch/v2-conference-cfp.md`](launch/v2-conference-cfp.md), [`docs/devrel/livestream-checklist.md`](devrel/livestream-checklist.md), [`docs/devrel/swag-brief.md`](devrel/swag-brief.md)

---

## Table of contents

1. [About the project](#1-about-the-project)
2. [About the speaker](#2-about-the-speaker-template)
3. [Bios -- 50 / 150 / 300 word](#3-bios)
4. [Pronouns, handles, headshot](#4-pronouns-handles-headshot)
5. [Conflict-of-interest disclosure](#5-conflict-of-interest-disclosure)
6. [Talk abstracts on offer](#6-talk-abstracts-on-offer)
7. [CFP boilerplate](#7-cfp-boilerplate)
8. [What we say yes / no to](#8-what-we-say-yes--no-to)

---

## 1. About the project

**One-liner (≤140 char):** `azure-openai-cli` is an OSS single-binary CLI for Azure OpenAI, built on Microsoft Agent Framework, shipped NativeAOT at 12.91 MB.

**Short paragraph (≤75 words):** `azure-openai-cli` (aka `az-ai` / `az-ai-v2`) is an indie OSS command-line tool for Azure OpenAI. It streams, it runs tool-calling agents, it starts in under 6 ms, and it ships as a single NativeAOT binary with no runtime dependency. v2 rebuilt the internals on Microsoft Agent Framework without breaking a single v1 flag. Source: [github.com/SchwartzKamel/azure-openai-cli](https://github.com/SchwartzKamel/azure-openai-cli).

**Long blurb (≤150 words):** `azure-openai-cli` is an OSS CLI for Azure OpenAI that treats the terminal as a first-class surface. v1 was a Python script wrapping the OpenAI SDK; v2 is a NativeAOT-compiled .NET single binary built on Microsoft Agent Framework, with OpenTelemetry, structured logging, and a persona memory store. It runs on Linux, macOS (arm64), and Windows, on metal or under WSL, and integrates with Espanso and AHK for text-expander-driven workflows. The v2 rewrite honored a hard 1.5× size gate (shipped at 1.456× v1), kept every v1 flag alive through a dual-tree window, and passes 1510 tests across the combined suite. It is maintained by a small team; no commercial backer, no VC, no SaaS. The project is available under the terms in [`LICENSE`](../LICENSE).

---

## 2. About the speaker (template)

> **Instructions to the speaker:** copy this block into the event form, replace bracketed fields, and flip the checkboxes that apply. Do not paste the template verbatim.

```
Name: [Legal name as it should appear on the badge]
Preferred on-stage name: [if different]
Pronouns: [they/them | she/her | he/him | …]
Title / affiliation: [Maintainer, azure-openai-cli -- OSS] or [Role, Employer]
Location / timezone: [City, TZ offset]
Primary handle: [@handle on the platform they care about]
Secondary handles: [mastodon / bluesky / linkedin]
Email for logistics: [address]
Food restrictions: [for speaker dinner]
A/V needs: [HDMI-C, 16:9, own clicker, own laptop, mono lapel mic, …]
Accessibility notes: [quiet green room, lights-up during demo, …]
```

---

## 3. Bios

> Pick the length that fits the form. All three are interchangeable -- same person, same project, same tone. Update the *version number* sentence when a new release ships.

### 50-word bio

> [Speaker] maintains `azure-openai-cli`, an OSS NativeAOT CLI for Azure OpenAI built on Microsoft Agent Framework. They ship in single binaries, rehearse their demos on fresh machines, and believe terminal output is a UX surface. v2.0.4 shipped April 2026.

### 150-word bio

> [Speaker] maintains `azure-openai-cli`, an indie OSS command-line tool for Azure OpenAI. They work at the intersection of terminal ergonomics, AOT-compiled .NET, and agent frameworks -- shipping a single NativeAOT binary under 13 MB that streams tokens, runs tool-calling agents, and starts in milliseconds. In a past life they [one-line career hook -- keep it honest; no title inflation]. They talk about shipping polish-over-hype OSS, AOT trim warnings you *shouldn't* silence, and what happens when a Markdown persona file meets a real chaos drill. They live in [city] with [spouse / dog / houseplant]. Find the project at [github.com/SchwartzKamel/azure-openai-cli](https://github.com/SchwartzKamel/azure-openai-cli).

### 300-word bio (keynote length)

> [Speaker] is the maintainer of `azure-openai-cli`, an OSS NativeAOT CLI for Azure OpenAI. The project started as a personal text-expander workflow -- `;;fix` triggering a Python script that called the OpenAI SDK at 2 AM in Istanbul -- and grew into a cross-platform single-binary CLI that v2 rebuilt on Microsoft Agent Framework without breaking a single v1 flag.
>
> Their technical focus is the unglamorous middle of the stack: AOT trim warnings the Azure SDK emits that you *shouldn't* silence; a 1.5× binary-size gate honored without waiver; persona memory that survives a chaos drill involving `/dev/urandom` symlinks and a persona named `../../canary`; and OpenTelemetry wired into a CLI small enough to install with `curl`.
>
> [Speaker] has spoken at [conferences] and written for [outlets]. They are not an Azure employee, not a consultant, and hold no financial relationship with Microsoft or any Azure partner -- see the COI disclosure in this bureau. They prefer small, rehearsed demos on fresh machines over live-coding adventures, and they believe the terminal is a product surface that deserves the same care as a web UI.
>
> Outside the project they [one or two lines -- hobbies, family, city -- keep it human]. The project is at [github.com/SchwartzKamel/azure-openai-cli](https://github.com/SchwartzKamel/azure-openai-cli), under the license in the repo.

---

## 4. Pronouns, handles, headshot

| Field | Value |
|---|---|
| Pronouns | *[set per speaker -- do not default]* |
| Mastodon | *[@handle@instance]* |
| Bluesky | *[@handle.bsky.social]* |
| LinkedIn | *[full URL]* |
| X / Twitter | *[@handle if still active]* |
| GitHub | *[@handle]* |
| Headshot (web) | *placeholder -- add `docs/devrel/headshots/<speaker>-web-1024.jpg` (sRGB, 1024 px square, < 300 KB)* |
| Headshot (print) | *placeholder -- add `docs/devrel/headshots/<speaker>-print-3000.jpg` (sRGB, 3000 px square, 300 dpi)* |
| Headshot license | *"Free to use by [event name] for speaker-page and program use. Not licensed for merchandise or model-training datasets."* |

Headshots are **not** committed yet. Cut a PR against `docs/devrel/headshots/` once a speaker is confirmed for an event; don't bulk-upload the whole team ahead of demand.

---

## 5. Conflict-of-interest disclosure

> Paste this block verbatim into any CFP form or sponsor-track submission.

```
I maintain `azure-openai-cli`, an OSS CLI for Azure OpenAI. I have no
employment, consulting, or financial relationship with Microsoft, OpenAI,
or any Azure partner. I do not receive compensation or in-kind support
from any vendor whose product is named in this talk. I accept standard
conference travel reimbursement where offered; I do not accept paid
speaking fees from vendor-sponsored tracks for this material.
```

If any of the above becomes untrue for a specific event (e.g., a
vendor-sponsored keynote with a fee), amend the disclosure *for that
event* and note the amendment in the talk's slide colophon.

---

## 6. Talk abstracts on offer

These three are drawn from the CFP packet at [`docs/launch/v2-conference-cfp.md`](launch/v2-conference-cfp.md). The full packet has opening-line options, cut-lists, and demo scripts; this page is the short menu a producer can scan.

### Abstract A -- *Shipping an agentic CLI in AOT*

- **Length:** 30-45 min (25-30 min also works; see cut-list in CFP packet)
- **Track:** Deep-dev / .NET internals
- **Target events:** .NET Conf, NDC, dotnetos, Monitorama (if framed around OTel trim)
- **Elevator (≤140 char):** MAF + OpenTelemetry + Azure SDK on NativeAOT, single-file, 12.91 MB. Two csproj lines took us from 1.625× to 1.456× v1.
- **Status:** CFP-ready. Demo is rehearsed as the hero script.

### Abstract B -- *Persona memory that can't read /dev/urandom*

- **Length:** 30-40 min
- **Track:** Security / SRE
- **Target events:** KubeCon + CloudNativeSecurityCon, OSS Summit Security, BSides, Monitorama
- **Elevator (≤140 char):** A Markdown file store met an adversary who brought a 5 GB log, a `/dev/urandom` symlink, and a persona named `../../canary`. Three findings, one commit, green.
- **Status:** CFP-ready. Demo is the chaos-drill reproducer set.

### Abstract C -- *Text expansion at the speed of a keystroke: az-ai-v2 under WSL with Espanso*

- **Length:** 15 min (hard cap) + Q&A slot; scales to 30 min with extra demo (AHK path, fleet-dispatch teaser)
- **Track:** DevRel / developer tools / WSL
- **Target events:** Microsoft Reactor (WSL track), All Things Open lightning, Strange Loop meetups, PyCon hallway demo, .NET Conf community room, DevOps Days regionals
- **Elevator (≤140 char):** `:aifix` fires in an Outlook draft. 10.73 ms AOT cold-start under WSL + Espanso. 12.97 MiB binary. `--raw` keeps the cursor from jumping.
- **Status:** CFP-ready as of v2.0.6. Full packet -- abstract, bio, outline, demo script, rehearsal notes, Q&A prep -- at [`docs/talks/wsl-espanso.md`](talks/wsl-espanso.md). Fallback cast recording pending first booking.

---

## 7. CFP boilerplate

> Blocks below are for producers who need *just copy* -- no decisions. Paste, tweak the bracketed fields, submit.

### Short-form CFP answer -- "Why this talk, why now?"

> `azure-openai-cli` v2.0.4 ships in April 2026 as a single 12.91 MB NativeAOT binary with Microsoft Agent Framework inside. The lessons the rebuild surfaced -- AOT trim warnings you shouldn't silence, a 1.5× size gate honored without waiver, OpenTelemetry wired through a CLI small enough to `curl`-install -- are not in a vendor deck yet. This talk puts them in one.

### Short-form CFP answer -- "Who is this for?"

> Engineers shipping AOT CLIs, SRE/security teams deploying AI-adjacent tooling, and OSS maintainers deciding whether to take on an agent framework. No background in Azure OpenAI required; every flag is demonstrated.

### Short-form CFP answer -- "What will attendees take away?"

> Three things: (1) which AOT trim warnings from Microsoft packages are signal vs. noise; (2) how a Markdown-backed persona store survives a real chaos drill; (3) one honest, reproducible measurement harness you can lift into your own project. Slides cite every number.

### Travel / logistics answer

> Willing to travel internationally with 6 weeks notice; prefer 8. Standard conference reimbursement accepted. No paid speaking fees from vendor-sponsored tracks. Will bring own laptop and HDMI-C adapter; will ask about stage A/V specs in advance.

### Recording / publishing answer

> Consent to recording and publication on the conference's usual channels, CC-BY or equivalent. Request a copy of the raw recording for our own archive at `docs/talks/` (once that directory exists -- see the speaker-bureau README).

---

## 8. What we say yes / no to

**Yes:**
- OSS-track conferences with published CFP rubrics
- Community meetups (virtual or in person, travel permitting)
- Technical podcasts with published episode lists
- Livestream guest slots where the host runs the run-of-show ([checklist](devrel/livestream-checklist.md))
- Workshop slots up to half a day, when a co-maintainer is available

**No (default; amend per-event if justified):**
- Vendor-sponsored keynotes with a fee attached to *our* material
- "Thought leadership" panels with no published agenda
- Pre-recorded talks presented as live
- Events that won't confirm code-of-conduct enforcement in writing

**Ask first:**
- Anything that asks for exclusive publishing rights to the recording
- Anything that asks for the speaker to demo a *different* vendor's product as part of the talk
- Anything happening within 6 weeks -- we rehearse demos on fresh machines and the calendar is load-bearing

---

-- *Keith Hernandez. One file. Honest bio. The CFP goes out clean.*
