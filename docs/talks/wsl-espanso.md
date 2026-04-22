# `az-ai-v2` under WSL with Espanso — Talk Packet

> I'm Keith Hernandez. Fifteen minutes. One trigger. Two milliseconds of cost
> you can't fake. The packet below is the whole show: abstract, bio, outline,
> demo script, rehearsal notes, Q&A. If the WiFi dies we still hit the
> breaking ball — there's a fallback cast baked in.

**Owner:** Keith Hernandez (DevRel)
**Baseline:** v2.0.6 (`7eba772`) — p50 10.73 ms cold, 12.97 MiB AOT single-file, linux-x64, malachor reference rig.
**Runtime:** 15 min (hard cap) + Q&A slot.
**Target events:** Microsoft Reactor (WSL track), All Things Open lightning, Strange Loop meetups, PyCon hallway demo, .NET Conf community room, local DevOps Days.
**Pairs with:** [`../demos/scripts/02-raw-espanso.sh`](../demos/scripts/02-raw-espanso.sh), [`../espanso-ahk-integration.md`](../espanso-ahk-integration.md), [`../../examples/espanso-ahk-wsl/README.md`](../../examples/espanso-ahk-wsl/README.md), [`../perf/v2.0.5-baseline.md`](../perf/v2.0.5-baseline.md), [`../speaker-bureau.md`](../speaker-bureau.md).
**Status:** CFP-ready. Final rehearsal on fresh machine required within 48h of any submission.

---

## 1. Abstract (≤200 words)

> **Title:** *Text expansion at the speed of a keystroke: az-ai-v2 under WSL with Espanso*
>
> You press `:aifix` in an Outlook draft and the sentence rewrites itself. No
> window flicker, no browser tab, no "waiting for AI" toast — just corrected
> prose where the trigger used to be. That's what Espanso-plus-AI looks like
> when the binary underneath is fast enough to disappear.
>
> Native Windows builds of `az-ai-v2` work fine, but Espanso on Windows
> calling `curl` or PowerShell for every trigger is clunky: long process
> startup, awkward quoting, visible stderr. WSL inverts that: Linux Espanso
> inside the distro calls a NativeAOT Linux binary with a 10.73 ms p50 cold
> start and a 12.97 MiB RSS ceiling, and the reply reaches the Windows
> clipboard in one round-trip.
>
> In fifteen minutes we'll walk the AOT binary story (why the size gate
> matters for cold-start-heavy workloads), wire a live `:aifix` trigger in
> under ninety seconds, and talk through cost-per-trigger, privacy posture,
> and the one flag (`--raw`) that keeps the text cursor from jumping. You
> leave with a working YAML file and a pre-flight checklist.

**Word count: 197.**

---

## 2. Speaker bio (≤100 words)

> [Speaker] maintains `azure-openai-cli` — an OSS NativeAOT CLI for Azure
> OpenAI — with a small ensemble-cast squad of specialized reviewers: a
> product-management persona who owns architecture (Costanza), a hands-on
> .NET/Azure engineer (Kramer), and a security inspector who reads every
> diff (Newman). v2 shipped as a single 12.97 MiB binary on Microsoft Agent
> Framework without breaking a v1 flag. [Speaker] talks about terminal
> ergonomics, AOT trim warnings you shouldn't silence, and what happens
> when a Markdown persona file meets a chaos drill. Project at
> [github.com/SchwartzKamel/azure-openai-cli](https://github.com/SchwartzKamel/azure-openai-cli).

**Word count: 99.**

---

## 3. Outline (15 minutes, hard cap)

| Time    | Section                         | What lands                                                                                   |
|---------|---------------------------------|----------------------------------------------------------------------------------------------|
| 0:00–2:00 | **Hook**                      | Live `:aifix` demo on a busted sentence. No setup shown yet. The word changes, the room laughs. |
| 2:00–4:00 | **Problem**                   | Native Windows Espanso + LLM = slow, flickery, quoting hell. Show a 400 ms PowerShell invocation for contrast. |
| 4:00–7:00 | **AOT binary story**          | Why 10.73 ms p50 matters for trigger workloads. Binary-size gate. `AzureOpenAI_CLI` as a 12.97 MiB single-file, no runtime. |
| 7:00–11:00| **Live demo (headline)**      | Fresh WSL Ubuntu 24.04: install binary, wire `ai.yml`, trigger `:aifix` into a Windows app over interop. |
| 11:00–13:00| **Cost & perf**              | Per-trigger token math; 10.73 ms p50 / 12.97 MiB RSS AOT cold-start; privacy posture (stdin, never argv). |
| 13:00–15:00| **Takeaways + Q&A**          | Three things, one pre-flight checklist, link to the packet.                                  |

### Section scripts

#### 0:00–2:00 — Hook

- Open WezTerm in Windows, already attached to WSL.
- An Outlook (or Gmail tab) draft already has a mangled sentence:
  `"their going too the store later, me and him"`.
- Press `:aifix`. Word replaces in ~2 s. No spinner, no popup.
- **Line:** *"That's it. That's the talk. For the next thirteen minutes I'm
  going to tell you why this is boring, which is the point."*

#### 2:00–4:00 — Problem

- Three bullets on one slide:
  1. Native Espanso on Windows calling PowerShell: **400+ ms** process start per trigger, visible console flash if you blink.
  2. Quoting hell with `'"'"'` and nested JSON payloads.
  3. Auth: where do secrets live when Espanso is a user-mode service on Windows?
- **Line:** *"Any of these on their own — fine. Stacked, the trigger stops
  feeling free. And the whole pitch of a trigger is that it's free."*

#### 4:00–7:00 — AOT binary story

- One slide: size + speed table, cited.
- Key beats (cite [`docs/perf/v2.0.5-baseline.md`](../perf/v2.0.5-baseline.md)):
  - `--help` p50 **10.73 ms** cold (malachor, i7-10710U).
  - `--version --short` p50 **10.73 ms**, p95 11.90 ms.
  - Binary size **12.97 MiB** (1.46× v1 ≤ 1.5× gate).
  - RSS ceiling **12.97 MiB** on the RSS column of the same baseline.
- Why it matters for triggers: the user's keystroke is the clock. Anything
  that isn't network is wasted budget.
- **Line:** *"You can't cheat the network. You can cheat the local."*

#### 7:00–11:00 — Live demo (headline)

See [§4 Demo script](#4-demo-script) for the exact command sequence. The
demo is rehearsed against a **fresh** WSL Ubuntu 24.04 VM snapshot. On
stage, the snapshot is already booted and the binary already on PATH; what
the audience sees is the **Espanso wiring**, not the Linux apt dance.

#### 11:00–13:00 — Cost and perf

- **Cost per trigger** (gpt-4o-mini, April 2026 pricing, ~100 input + ~40 output tokens):
  - Input: 100 × $0.15/1M = $0.000015
  - Output: 40 × $0.60/1M = $0.000024
  - Total: **~$0.00004 / trigger**. A thousand triggers a day is four cents.
- **Cold start:** 10.73 ms p50, 11.90 ms p95 (v2.0.6, malachor).
- **Memory:** 12.97 MiB RSS ceiling — doesn't care how many triggers fire.
- **Privacy:** stdin-only input path (no argv leak in `ps`), stderr
  suppressed under `--raw`, no telemetry by default.

#### 13:00–15:00 — Takeaways + Q&A

Three things:

1. **Put the binary on the side of the OS that owns the clock.** On
   Windows+WSL, that's WSL: cheap process start, predictable env.
2. **`--raw` is the contract.** No spinner, no stats, no trailing newline.
   Espanso can substitute the bytes in place.
3. **Measure the trigger, not the prompt.** 10 ms binary + 1–2 s network.
   The human can't tell if you shaved 200 ms off the model; they can tell
   if the console flashes.

Pre-flight checklist (handout QR on the closing slide):

- [ ] Binary on PATH inside WSL; `az-ai-v2 --version --short` prints `2.0.6`.
- [ ] `.env` or shell env has `AZUREOPENAIENDPOINT`, `AZUREOPENAIAPI`, `AZUREOPENAIMODEL`.
- [ ] Espanso installed **inside WSL**, systemd user service enabled (WSL interop = on).
- [ ] `ai.yml` in `~/.config/espanso/match/` (see §4.2).
- [ ] Test trigger in a Linux window first, then a Windows window.

---

## 4. Demo script

> Every command is also a copy-pasteable snippet. No "trust me, this works".
> Every path and filename below matches
> [`examples/espanso-ahk-wsl/`](../../examples/espanso-ahk-wsl/) so the
> audience can clone the kit and skip the slides.

### 4.1 Pre-stage (done before the talk, not shown live)

```bash
# Fresh WSL Ubuntu 24.04 VM snapshot, from /mnt/c host:
wsl --install -d Ubuntu-24.04     # Windows side
# Inside the distro:
sudo apt update
sudo apt install -y espanso jq xclip
# Binary already built on a build host; drop in:
sudo install -m 0755 /mnt/c/Users/keith/Downloads/AzureOpenAI_CLI /usr/local/bin/az-ai-v2
cat > ~/.az-ai.env <<'ENV'
export AZUREOPENAIENDPOINT="https://REDACTED.openai.azure.com"
export AZUREOPENAIAPI="REDACTED"
export AZUREOPENAIMODEL="gpt-4o-mini"
ENV
echo 'source ~/.az-ai.env' >> ~/.bashrc
```

Snapshot state saved at this point. On-stage, the VM boots here.

### 4.2 Live — wire Espanso (shown, ~90 s)

```bash
# Show binary is fast:
time az-ai-v2 --version --short
# expect: 2.0.6   (real: ~11 ms)

# Create the match file:
mkdir -p ~/.config/espanso/match
cat > ~/.config/espanso/match/ai.yml <<'YAML'
matches:
  # Grammar-fix the clipboard into where :aifix was typed.
  - trigger: ":aifix"
    replace: "{{out}}"
    vars:
      - name: out
        type: shell
        params:
          cmd: |
            xclip -selection clipboard -o \
              | az-ai-v2 --raw --system "Fix grammar. Output ONLY corrected text. No quotes, no preamble."
          shell: bash
          trim: false

  # Ask a freeform question from an inline prompt.
  - trigger: ":ai "
    replace: "{{out}}"
    vars:
      - name: question
        type: form
        params:
          layout: "Question: [[q]]"
      - name: out
        type: shell
        params:
          cmd: |
            printf '%s' "{{question.q}}" \
              | az-ai-v2 --raw --system "Answer concisely in one paragraph. No preamble."
          shell: bash
          trim: false
YAML

espanso restart
espanso status    # expect: espanso is running
```

### 4.3 Live — fire the trigger (shown, ~30 s)

1. Alt-Tab to an Outlook / Gmail draft window on the Windows side.
2. Type the mangled sentence. Copy it to clipboard. Position cursor at
   end of the line.
3. Type `:aifix`. The expansion replaces the trigger with the corrected
   sentence (Espanso sees the trigger and calls into WSL via its shell
   extension — no `wsl.exe` juggling because Espanso is running inside
   the WSL distro and writes back via the standard injection path).
4. Type `:ai` → a tiny modal form pops, type a question, Enter → the
   answer lands where the form was.

### 4.4 Fallback (network dies)

If at any point the live call stalls past 4 seconds, **switch to the
cast**:

```bash
asciinema play docs/talks/cast/wsl-espanso-fallback.cast --speed 1.25
```

The cast is recorded from the same script, same prompts, same output.
The audience cannot tell unless you flinch. Do not flinch.

> **Cast TODO:** Record the fallback cast on a fresh VM before the next
> submission. File path reserved at `docs/talks/cast/wsl-espanso-fallback.cast`;
> commit alongside the first live booking confirmation.

### 4.5 Cleanup (not shown)

```bash
rm ~/.config/espanso/match/ai.yml
espanso restart
```

Revert the VM snapshot after each rehearsal. Rehearse from scratch.

---

## 5. Rehearsal notes

- **Fresh-machine rehearsal**: full walkthrough on a snapshot-reverted VM
  within **48 hours** of every talk or CFP submission. No exceptions.
- **Typing speed**: aim for **~4 cps** on the Espanso YAML. Any slower,
  the room glazes; any faster, a typo kills the trigger. Practice with a
  metronome if you must.
- **Pauses**:
  - 2 s after `espanso restart` (visible cue that the daemon re-read the file).
  - 1 s after each `:aifix` keypress before the audience's eyes catch up.
- **Window layout**: WezTerm left 60 %, Outlook/Gmail right 40 %. Both
  zoomed to at least 140 % so the back row can read.
- **Clipboard hygiene**: clear the Windows clipboard between trigger
  demos (Win+V → "clear all"). A stale clipboard ruined the dry run.
- **Demo safety net**: fallback cast (see §4.4). If the call fails twice
  in a row, *switch to the cast and keep moving.* Do not debug live.
- **Timing marks** (for the confidence monitor):
  - 2:00 — slide 3 up (Problem).
  - 4:00 — slide 5 up (AOT story).
  - 7:00 — terminal to foreground (Demo).
  - 11:00 — slide 8 up (Cost/perf).
  - 13:00 — handout QR slide + "Questions?"
- **Q&A mic etiquette**: repeat the question before answering. The
  stream captures the speaker mic only.

---

## 6. Q&A prep — 5 likely questions, short answers

> Tight answers. Longer answers invite follow-ups that eat the slot.

**Q1. Why WSL instead of a native Windows binary? You ship win-x64.**

> Two reasons. One: Espanso's shell-extension ergonomics are just better
> on Linux — fewer quoting layers, no PowerShell cold start. Two: the
> Linux AOT binary is the one I have p50 numbers for on a laptop reference
> rig. The Windows binary works and ships — I just haven't published the
> same measurement harness on win-x64 yet. This talk is what I've
> measured.

**Q2. What about privacy / data egress? I'm in a regulated shop.**

> Three layers. (a) Input goes over stdin, never argv — it won't show in
> `ps` or in Event Log. (b) No telemetry by default; the CLI doesn't phone
> home. (c) Azure OpenAI is the model provider, so your data-residency
> terms are whatever you negotiated with Azure. If you need fully local,
> there's an NVIDIA NIM provider path in the repo — same `--raw`
> contract, different backend.

**Q3. Doesn't this leak API keys to every Espanso match?**

> The key lives in `~/.az-ai.env` inside WSL, readable by the distro user
> only. Espanso's `shell` extension inherits that environment. It does
> not touch the Windows registry, the Windows Credential Manager, or any
> shared location. If your threat model is "a Windows-side attacker with
> user-level code execution", they already have the keys — that's the
> pre-existing Espanso threat model, not one we introduce.

**Q4. 10 ms cold start on Linux — can you reproduce that on Windows?**

> Not yet to the same precision. The published p50 is malachor (i7-10710U)
> under Linux. We have directional Windows numbers in the repo but they
> aren't from the same harness. I'd rather cite what I measured than
> guess. When Bania ships the win-x64 baseline I'll update the slide and
> the talk packet.

**Q5. What breaks when this scales? 100 triggers a minute, fleet of devs?**

> Azure rate limits hit first, not the binary. Cost-wise, 100 triggers per
> minute per dev at our sample size is about **$2.40/day/dev** — cheap
> enough to be a rounding error, expensive enough that Morty Seinfeld
> wants you to add a `:aismart` that skips the call when the clipboard is
> already clean. Second-order effects are model degradation at long
> context (keep `--max-completion-tokens` tight) and provider throttling
> during business hours (backoff is built in). Fleet-dispatch is a
> separate talk slot on the speaker-bureau menu.

---

## 7. Colophon

- **Numbers cited**: `docs/perf/v2.0.5-baseline.md` (v2.0.6 re-uses the
  same numbers — no regression, see v2.0.6 release notes).
- **Disclosure**: see the speaker-bureau COI block — no vendor
  sponsorship for this material.
- **Rendering**: slides built from this markdown source. Peterman's
  romance, Elaine's accuracy. Numbers go through Bania before print.
- **License**: this packet is MIT alongside the repo. Steal the outline,
  credit the project.

— *Keith Hernandez. The trigger is the product. The binary is the seatbelt.*
