# Docs Audit — DevRel & Demos Segment — v2.0.4

**Auditor:** Keith Hernandez (DevRel / speaking / livestream)
**Date:** 2026-04-22
**Baseline:** `HEAD` at tag `v2.0.4` (`afa95fd`)
**Scope:** `docs/demos/`, `docs/announce/`, `docs/examples/`, `docs/diary/`,
  README quick-start / demo, launch-adjacent blurbs, Espanso/AHK walk-through,
  WSL flow.
**Non-goals:** No source edits. No doc rewrites. Findings + fixes only.

I'm Keith Hernandez. I read every demo script against v2.0.4 with the flag
reference open in the other hand. The hero GIF still hits — but the copy
around it has drifted a full major version behind the binary. Three talks
are CFP-ready; zero of them have a rehearsed, stage-worthy demo file that
matches the CLI we actually ship today. That's the headline.

---

## 1. Executive summary

- **Demos inventoried:** 3 scripts (`01-standard-prompt.sh`,
  `02-raw-espanso.sh`, `03-agent-tool-calling.sh`) + 1 hero-GIF runbook.
- **Demos runnable on v2.0.4 as written:** **0 of 3**. All three type
  `az-ai` (v1 binary) rather than `az-ai-v2` (what the v2 artifacts install
  as during the dual-tree window). On a fresh machine with only v2
  installed, every demo fails at the first prompt.
- **Talk-track / narration docs paired with demos:** **0 of 3**. Each
  script has an in-file prose preamble (Peterman-flavored, charming), but
  there is no scene-by-scene narration doc for a speaker to rehearse
  against.
- **Announce copy up to date:** **No.** `docs/announce/` holds exactly one
  file — `v1.8.0-launch.md` — and it claims 5.4 ms cold start, 9 MB
  binary, 7 RIDs (including the dropped `osx-x64`), and 538 tests.
  v2.0.4 is 12.91 MB, 4 release RIDs, and 1510 tests. New release
  collateral has all migrated to `docs/launch/` without ever backfilling
  the `docs/announce/` convention.
- **CFP packet:** `docs/launch/v2-conference-cfp.md` is solid —
  three abstracts, targeting matrix, cut-list. Needs a Gold-Glove polish
  before submission (see §6), but the bones are there.
- **Speaker bureau bio:** **Missing.** Per-abstract "Why me to give it"
  paragraphs exist inside the CFP; no central bio, headshot, or
  conflict-of-interest disclosure block.
- **Swag brief:** **Missing.** Zero vendor-ready spec files. One
  passing mention in `docs/cost-optimization.md` unrelated to physical
  goods.
- **Livestream show-run / episode archive:** **Missing.**
- **WSL walk-through:** **Strong on flow, stale on numbers.** The
  Espanso guide has a Path A / Path B breakdown that's the best in the
  repo, but cites 8.9 MB / 11 ms for the AOT binary — v1 numbers.
  v2.0.4 is 12.91 MB; startup is p95 1.12× v1. WSL-specific numbers
  were never re-measured for v2.

**Top DevRel gap:** there is a **three-abstract CFP packet with no
rehearsed, v2.0.4-accurate demo to back it**. We could submit tomorrow
and have nothing to show on stage. That's the gap. See §7 for the
prioritized opportunity list.

---

## 2. Severity rubric

| Severity | Meaning |
|---|---|
| **Critical** | Public-facing copy is factually wrong about the shipped binary, or the hero demo fails on a fresh install. Fix before any talk, livestream, or blog post. |
| **High** | Copy is stale in a way a reviewer at a CFP committee, conference organizer, or new user will catch in 30 seconds. Fix inside the next release window. |
| **Medium** | Asset is missing or thin in a way that limits a specific DevRel opportunity (conference, livestream, swag). Fix before the next event. |
| **Low** | Polish. Tone drift, naming inconsistency, minor staleness that does not mislead. |
| **Informational** | Observation for future planning; not a bug. |

---

## 3. Findings

### C-1 (Critical) — All three demo scripts call `az-ai`, not `az-ai-v2`

**Files:**
- `docs/demos/scripts/01-standard-prompt.sh:38,43`
- `docs/demos/scripts/02-raw-espanso.sh:46,51`
- `docs/demos/scripts/03-agent-tool-calling.sh:63`

**Problem:** v2 ships during the dual-tree window as `az-ai-v2`
(`docs/launch/v2.0.0-announcement.md`, `docs/launch/v2.0.0-social-posts.md`,
CHANGELOG 2.0.0–2.0.4). A fresh v2.0.4 install — brew, nix, scoop, or
`dotnet publish` — lays down `az-ai-v2` on PATH. The demo scripts hard-code
`az-ai`. On a machine that only has v2, every script dies with `command not
found` on its first `type_prompt` line. This is the hero demo. It does not run.

**Proposed fix:** Parameterize the binary name at the top of each script
(`: "${AZ_AI_BIN:=az-ai-v2}"`) and interpolate. Add a comment block
explaining the dual-tree window. When cutover collapses to single-binary
`az-ai`, the default flips and the scripts don't change.

**Severity:** Critical.

**DevRel impact:** The README hero GIF is regenerated from
`01-standard-prompt.sh` (see `hero-gif.md:14`). The next regen on a clean
machine fails. If we film a talk demo against this file as-is, we ship v1
on stage in a v2 release window.

---

### C-2 (Critical) — `docs/announce/v1.8.0-launch.md` is the only file in `docs/announce/` and is a major version behind

**File:** `docs/announce/v1.8.0-launch.md` (whole file; see specifically
lines 47–59, 62–78, 137–146)

**Problem:** The directory the audit scope calls out as "announce" holds
one document, from v1.8.0. It claims:
- 5.4 ms cold start (v2.0.0 is ~1.12× v1 on `--version --short` — needs
  re-measurement for v2.0.4 but is *not* 5.4 ms)
- 9 MB binary (v2.0.0 is **12.91 MB**; v2.0.4 is in the same neighborhood)
- 7 RIDs including `osx-x64` (v2.0.4 **dropped** `osx-x64`,
  CHANGELOG 2.0.4)
- 538 tests (v2.0.4 suite is **1510** total: 1025 v1 + 485 v2)
- Azure.AI.OpenAI **2.1.0 stable** as a flagship feature (still true, but
  stale as a hook)

All newer release collateral (v2.0.0, v2.0.1, v2.0.2, v2.0.4) lives in
`docs/launch/`, not `docs/announce/`. The two directories have never been
reconciled.

**Proposed fix:** Two options; pick one and commit to it.
1. **Archive `docs/announce/` → `docs/launch/archive/v1.8.0-launch.md`**
   and point the scope forward: `docs/launch/` is the canonical home for
   launch collateral.
2. **Resurrect `docs/announce/`** as the *published* announcement archive
   (post-launch, stable URLs) while `docs/launch/` is the *pre-publish*
   workshop. Move v2.0.0 announcement and v2.0.4 release notes into
   `docs/announce/` as `v2.0.0-launch.md` and `v2.0.4-launch.md`.

Either way: add a `README.md` at the top of the chosen directory
explaining the convention so the next auditor doesn't re-discover this.

**Severity:** Critical.

**DevRel impact:** Anyone linking `docs/announce/` from the outside
(blog cross-references, HN post, conference speaker page) lands on a
document pitching a version two majors behind. The v1.8.0 page's social
pull-quotes (lines 137–146) are still plausibly citable; a reader who
quotes "75× faster than the Dockerized equivalent" is quoting v1.8.0
numbers in a v2.0.4 world.

---

### H-1 (High) — Default model drift: `docs/examples/azureopenai-cli.sample.json`

**File:** `docs/examples/azureopenai-cli.sample.json:3-8`

**Problem:** Sample config ships:
```json
"models": { "fast": "gpt-4o-mini", "smart": "gpt-4o",
            "reasoning": "gpt-5", "cheap": "gpt-35-turbo" },
"default_model": "fast"
```
Per the session brief, v2.0.4's intended default is `gpt-5.4-nano`. The
sample config is also the example a reader copies verbatim into
`~/.azureopenai-cli.json`. `docs/cost-optimization.md:43,60–70` explicitly
argues for `gpt-4o-mini` over `gpt-5.4-nano` for Espanso — **which is a
valid position but contradicts the v2.0.4 default**. One of the two
sources is wrong. The cost doc is detailed and defensible; the sample is
terse and aspirational.

**Proposed fix:** Reconcile with product. Either:
- Update sample to `"default_model": "nano"`, with `"nano": "gpt-5.4-nano"`
  in `models`, and cross-link `docs/cost-optimization.md` as "override
  recipes" — OR —
- Revert the v2.0.4 default to `gpt-4o-mini` and leave the sample as the
  canonical example.

Until reconciled, the sample is misleading for *both* camps.

**Severity:** High.

**DevRel impact:** A blog post explaining "az-ai picks gpt-5.4-nano by
default; here's why" runs aground when the example file pins
`gpt-4o-mini`. Demo scripts that say `az-ai "…"` implicitly showcase
whatever the default is; if the default doesn't match our own docs, the
GIF looks like a liar.

---

### H-2 (High) — README performance table cites v1 numbers under a v2.0.4 tag

**File:** `README.md:64-74, 16-20`

**Problem:** `## Performance` says 5.4 ms cold start / ~9 MB / 538 tests
and attributes the win to a single-file AOT build. The repo is tagged
`v2.0.4`. v2.0.0 is **12.91 MB**; startup p95 is **1.12× v1** on
`--version --short`. Newman / Bania signed off on the regression budget —
but the README hasn't updated.

Line 18 (`5 execution modes`) and line 20 (`538 passing tests`) both need
the v2 numbers.

**Proposed fix:** Add a "v2.0.4" row to the perf table (measured on the
v2 binary) and retain v1 row as historical. Update the 538 count to
"1510 tests (1025 v1 + 485 v2)". Revise line 16 to something honest at
v2 scale, e.g. "Native AOT single-file binary, 1.12× v1 startup,
inside the ≤25% regression budget."

**Severity:** High.

**DevRel impact:** README is the first screen of every conference
evaluator. A claim of 5.4 ms that the binary can't reproduce is the
kind of thing a PyCon or .NET Conf reviewer will catch with `hyperfine`
in 60 seconds.

---

### H-3 (High) — Espanso/AHK guide WSL section cites v1 AOT numbers

**File:** `docs/espanso-ahk-integration.md:452, 475`

**Problem:** WSL Path A/B walkthrough (which is, on flow alone, the
strongest DevRel asset in the repo) opens with:
> "Both use the **Linux-native AOT binary** (`dist/aot/AzureOpenAI_CLI`,
> ~8.9 MB, ~11 ms cold start)"

v2.0.4's binary is ~12.9 MB; WSL cold-start was never re-measured for
v2. The `dist/aot/…` path is also a source-build path; v2.0.4 ships
pre-built tarballs (`azure-openai-cli-linux-x64.tar.gz`) that install
as `az-ai-v2`. A WSL user pulling the release tarball won't find
`dist/aot/` anywhere.

Line 475 quotes 20–80 ms for the WSL boundary crossing "on top of the
~11 ms AOT cold start" — a composite number that needs a v2 re-measure.

**Proposed fix:**
1. Update the intro to reference the v2.0.4 release asset
   (`azure-openai-cli-linux-x64.tar.gz`, ships `az-ai-v2`, ~12.9 MB).
2. Re-measure WSL boundary overhead against v2.0.4 and update line 475.
   If we don't have a v2 WSL number yet, flag it as "(v1 number;
   v2 re-measurement tracked in [issue])" — honest > stale.
3. Update the embedded YAML (lines 519–600+) to call `az-ai-v2` not
   `az-ai`, or parameterize.

**Severity:** High.

**DevRel impact:** The WSL walkthrough is the single strongest
user-acquisition funnel the docs have — it's the only place in the repo
that walks a Windows user end-to-end to a working trigger. A v2 user
following it hits "command not found" on the first `:aifix`. Silent
fail, exactly the kind the doc itself (line 500) warns against.

---

### H-4 (High) — Demo scripts have no paired narration / talk-track docs

**Files (absent):** `docs/demos/scripts/01-standard-prompt.talk.md` (etc.)

**Problem:** Each `.sh` file opens with a Peterman-voice preamble —
great flavor, unusable as a talk track. A speaker rehearsing for a
KubeCon lightning slot or a Twitch stream needs:
- Per-scene beats (what's on screen, what I say, what I *don't* say)
- Failure-recovery lines (what to say if the API stalls, if the Wi-Fi
  dies, if the hero GIF reveals a typo)
- Q&A plants ("you might be wondering why `--raw` instead of `--json`
  here — that's in the follow-up talk")

None exists. The CFP (`docs/launch/v2-conference-cfp.md`) includes
"demo script summaries" at the abstract level, but those are one
paragraph each, not scene-by-scene.

**Proposed fix:** For each `scripts/NN-*.sh`, add a
`scripts/NN-*.talk.md` with three sections:
- **Cold opener (≤10 s)** — the hook
- **Scene beats** — timestamp ranges, on-screen content, spoken line,
  fallback line
- **Landing** — the one sentence you want the audience to tweet

**Severity:** High.

**DevRel impact:** Three CFPs in flight, zero rehearsed talk tracks.
If the first abstract lands, we have 6–10 weeks to build the talk from
scratch — fine for one, punishing for three concurrent.

---

### H-5 (High) — No speaker bureau bio / headshot / COI disclosure

**Files (absent):** `docs/launch/speaker-bio.md`, `docs/launch/headshot.*`

**Problem:** The CFP packet has per-abstract "Why me" paragraphs but no
canonical bio the team can paste into a conference speaker-page form,
a podcast intro, or a livestream guest prompt. No headshot. No
conflict-of-interest disclosure (relevant for any Azure-sponsored track
— we ship an Azure OpenAI tool). No pronouns-on-file. No Mastodon /
Bluesky / X handle set.

**Proposed fix:** Create `docs/launch/speaker-bureau.md` with:
- 50-word bio
- 150-word bio
- 300-word bio (PyCon keynote length)
- Pronouns (editable per speaker)
- Social handles
- COI disclosure block: "Maintainer of `azure-openai-cli`, an OSS CLI
  for Azure OpenAI. No employment, consulting, or financial
  relationship with Microsoft or any Azure partner."
- Headshot path + licensing note

**Severity:** High.

**DevRel impact:** Every CFP form asks for these. Every podcast intro
asks for these. Without a canonical file, each request gets a bespoke
writeup — drift, inconsistency, and delay.

---

### M-1 (Medium) — No swag design brief anywhere in the repo

**Files (absent):** `docs/launch/swag/brief.md`, `docs/launch/swag/assets/`

**Problem:** Scope brief called this out explicitly. There is no
vendor-ready swag spec. No constraints document (PMS colors, min
resolution, trademark clearance — Jackie Chiles's remit). No sticker
design. No booth-in-a-box kit. The `img/` directory has `its_alive_too.gif`
and presumably the Kramer-designed logo mark, but no `logo-print.svg`,
no `sticker-3in-circle.ai`, no vendor checklist.

**Proposed fix:** Create `docs/launch/swag/`:
- `brief.md` — constraints, clearance checklist (cross-link Jackie's
  licensing audit), vendor contact etiquette, color specs
- `assets/` — logo variants (SVG + PNG @ 1x/2x/print), sticker cuts
  (3", 2", die-cut outline), laptop-cover 13"/14"/16" templates
- `kit-manifest.md` — what goes in a booth-in-a-box for a regional
  meetup sponsorship

Defer the actual art; the brief unblocks everything else.

**Severity:** Medium.

**DevRel impact:** Conference sponsorships, meetup partnerships, and
contributor gifts all block on "where's the logo file?" This is the
bike-shed that stops a talk from becoming a booth.

---

### M-2 (Medium) — No livestream show-run template, no episode archive

**Files (absent):** `docs/livestream/` (directory)

**Problem:** Scope brief calls out Twitch / YouTube / LinkedIn Live as a
surface. Nothing in the repo. No show-run template (open / segment
pacing / guest booking / chat moderation / lower-third copy). No
VOD-indexing convention. No guest brief template.

**Proposed fix:** Create `docs/livestream/`:
- `show-run-template.md` — 60-min episode structure, 30-min episode
  structure
- `guest-brief-template.md` — what we ask guests before going live
- `chat-moderation-brief.md` — rules, escalation path (Frank Costanza's
  on-call rotation if we need it)
- `episode-archive/` — one markdown file per recorded episode with
  VOD link, guest, topics covered, chapter timestamps

Light lift. Unblocks the first stream.

**Severity:** Medium.

**DevRel impact:** A streaming agent mode (promised in
`v1.8.0-launch.md` §VII and now live via MAF in v2.0.0) is *visually*
perfect for livestream. The asset that makes the stream happen is an
empty directory.

---

### M-3 (Medium) — Diary posts are meta-narrative, not publishable devlogs

**Files:**
- `docs/diary/2026-04-16-interactive-chat-repl-chat-flag-iter-2.md`
- `docs/diary/2026-04-16-untitled-iter-1.md`

**Problem:** Each file is ~9 lines of agent-session meta ("I spent 56s…
13 tool calls… 181,281 tokens… quality gate passed"). These are
agent-telemetry artifacts, not publishable devlog posts. Narrative-heavy
they are not. As DevRel surface they're net-negative — a reader landing
from an external link sees filler.

**Proposed fix:** Either:
1. Move them to `docs/diary/internal/` (or `.meta/` equivalent) and
   keep `docs/diary/` for publishable devlog; or
2. Expand each into a real post — "what we learned building `--chat`
   REPL" is an actual story that would read well with Peterman / Elaine
   polish.

**Severity:** Medium.

**DevRel impact:** Diary is a natural SEO + community-engagement
surface; right now it actively discourages the reader.

---

### L-1 (Low) — `docs/demos/README.md` claims `make install` puts `az-ai` on PATH; v2 puts `az-ai-v2`

**File:** `docs/demos/README.md:33`

**Problem:** "You also need a working `az-ai` on `PATH` (`make install`
from the repo root)". During the dual-tree window, `make install` for
v2 produces `az-ai-v2`. Same issue as C-1, but at the prerequisite
level rather than the script content level.

**Proposed fix:** "You need a working `az-ai` (v1) **or** `az-ai-v2`
(v2) on PATH. The demo scripts default to `az-ai-v2`; override with
`AZ_AI_BIN=az-ai` if you're demoing v1."

**Severity:** Low.

---

### L-2 (Low) — `v1.8.0-launch.md` §VII promises v1.9 streaming agent; v2.0.0 delivered it — no retroactive note

**File:** `docs/announce/v1.8.0-launch.md:109-117`

**Problem:** "We are working on a **streaming agent mode** for v1.9".
v2.0.0 shipped it via MAF. The v1.8.0 post has no "update:" trailer
linking forward.

**Proposed fix:** If we keep the file in place (see C-2), add a
`> **Update 2026-04-20:** Streaming agent landed in v2.0.0 on
> Microsoft Agent Framework — see [v2.0.0 announcement](...)` block at
> the top.

**Severity:** Low.

---

### L-3 (Low) — Social snippets have no v2.0.4 variant

**Files:**
- `docs/launch/social-snippets.md` (all — 2.0.0 only)
- `docs/launch/v2.0.0-social-posts.md` (all — 2.0.0 only)

**Problem:** v2.0.4 dropped `osx-x64` and landed FDR High fixes. These
are both tweet-able ("macOS Intel users: Rosetta 2 path is official
now; we dropped the dedicated `osx-x64` artifact because `macos-13`
runners kept blocking releases."). No v2.0.4 short-form drafts exist.

**Proposed fix:** Add `docs/launch/v2.0.4-social-posts.md` with three
variants (technical / user-benefit / contributor-thanks) paralleling
the v2.0.0 structure.

**Severity:** Low.

---

### L-4 (Low) — `hero-gif.md` references `monokai` / `github-dark` / `dracula` but the current `its_alive_too.gif` theme isn't documented

**File:** `docs/demos/hero-gif.md:31`

**Problem:** Recording notes list theme options; don't pin which one
the *current* hero was rendered with. Re-rendering the hero without
that pin risks brand-chrome drift.

**Proposed fix:** Add `**Current hero renders with `--theme monokai
--speed 1.25 --font-size 18 --cols 88 --rows 18`.**` at the top. One
line, big payoff.

**Severity:** Low.

---

### I-1 (Informational) — Espanso/AHK guide is the strongest long-form DevRel asset in the repo

**File:** `docs/espanso-ahk-integration.md` (1107 lines)

**Observation:** The shell-comparison table (`:485–487`), the
`bash -lc` vs `-e` explainer (`:496–504`), and the per-platform
security storage matrix (`:103–243`) are all textbook DevRel — opinion
plus receipts. With the v2 number refresh from H-3, this file is a
15-minute talk outline wholesale. Flagging it as an asset, not a bug.

**Severity:** Informational.

---

### I-2 (Informational) — `docs/launch/v2-conference-cfp.md` is 90% CFP-ready

**File:** `docs/launch/v2-conference-cfp.md`

**Observation:** Three abstracts, targeting matrix, cut-list,
per-abstract demo summaries, opening-line options. The gaps are the
ones §3 calls out: no paired talk-track docs (H-4), no speaker
bureau bio (H-5), no swag kit (M-1). With those filled, the packet
is ready to submit to .NET Conf, KubeCon + CloudNativeSecurityCon,
Strange Loop, and PyCon at the same time.

**Severity:** Informational.

---

## 4. Demo Readiness Dashboard

| Demo | Runnable on v2.0.4? | Talk-track doc? | Visuals (GIF/SVG)? | CFP-ready? | Notes |
|---|:-:|:-:|:-:|:-:|---|
| `01-standard-prompt.sh` | ❌ (C-1) | ❌ (H-4) | ✅ (`its_alive_too.gif` — v1 binary) | ⚠️ | Hero demo. Needs bin param + talk-track + v2 re-record. |
| `02-raw-espanso.sh` | ❌ (C-1) | ❌ (H-4) | ❌ (no GIF committed) | ✅ after fixes | Strongest narrative: clean pipe, Espanso, single `xxd` tail. 15-min talk outline as-is. |
| `03-agent-tool-calling.sh` | ❌ (C-1) | ❌ (H-4) | ❌ (no GIF committed) | ✅ after fixes | Safest live demo (mktemp sandbox, no network tools). Pair with Abstract C (fleet dispatch). |
| `hero-gif.md` runbook | ❌ (delegates to 01) | n/a | ✅ | n/a | Needs C-1 fix + L-4 theme pin. |

**Rollup:** 0/3 runnable. 0/3 talk-tracked. 1/3 has visuals. 0/3
CFP-ready as-is. All three become CFP-ready with C-1 + H-4 +
per-demo refresh.

---

## 5. Espanso / WSL Walk-through Scorecard

| Dimension | Status | Note |
|---|:-:|---|
| Linux / macOS Espanso config | ✅ | Clean, current structure. |
| Windows native Espanso | ✅ | Path+env explainer is thorough. |
| Secure storage per OS | ✅ | Best matrix in the repo. |
| WSL Path A (Espanso in WSL) | ⚠️ | Flow ✅, binary path + size stale (H-3). |
| WSL Path B (Espanso on Win → WSL) | ⚠️ | Flow ✅, binary size + latency numbers stale (H-3). |
| `shell: powershell` vs `wsl` vs `cmd` | ✅ | Canonical reference. Keep. |
| `bash -lc` vs `-e` explainer | ✅ | Canonical reference. Keep. |
| AHK v2 coverage | ✅ | Present, unchanged by v2. |
| Binary-name consistency (`az-ai` vs `az-ai-v2`) | ❌ | Entire doc calls `az-ai`; v2 ships `az-ai-v2`. Same root cause as C-1. |

---

## 6. CFP Packet Scorecard

| Abstract | Status | Gap |
|---|:-:|---|
| A — AOT trim | ✅ CFP-ready | Paired rehearsed demo (H-4) |
| B — Persona memory / chaos drill | ✅ CFP-ready | Reproducer scripts referenced but not bundled as a demo file; paired narration (H-4) |
| C — Seinfeld ensemble / fleet dispatch | ✅ CFP-ready | Best livestream candidate; no show-run template (M-2) |

**Speaker packet blockers:** speaker bio (H-5), headshot (H-5),
COI disclosure (H-5), swag kit (M-1).

---

## 7. Top 3 DevRel opportunities

### Opportunity 1 — **Ship the v2.0.4 `:aifix` WSL demo as a 90-second vertical video**

The WSL walkthrough already has the Path B `:aifix` config. Instrument
it on a clean Windows 11 + WSL2 box. Record the `wsl.exe bash -lc` cold
start, the 20–80 ms boundary crossing, the clean clipboard replacement
in Outlook or Edge. Caption with v2.0.4 numbers (re-measured per H-3).
This is the single highest-impact piece of content in the audit
surface — Windows users are underserved by every existing AI CLI
demo, and we have the only honest WSL-specific one.

**Blocks on:** C-1, H-3, a 90-minute recording session.
**Lands:** Product Hunt clip, LinkedIn Live teaser, YouTube Short,
`/r/bashonubuntuonwindows` post.

### Opportunity 2 — **Abstract A + demo → .NET Conf 2026 lightning**

CFP Abstract A (AOT trim to 12.91 MB without waiving the gate) is a
.NET Conf-shaped story with a visible number. The demo is two minutes,
never touches the network, and rehearses on any laptop. Needs C-1 +
H-4 + a rehearsal pass on a fresh Windows and Linux box (the "no Wi-Fi"
fallback path is free for this demo — `--estimate` and
`--version --short` don't hit the API).

**Blocks on:** C-1, H-4, speaker bureau packet (H-5).
**Lands:** .NET Conf submission by the CFP deadline. Community video
lives on the conf YouTube for three years after.

### Opportunity 3 — **Persona/Chaos drill → KubeCon + CloudNativeSecurityCon**

Abstract B is, on the merits, a KubeCon-caliber security talk: real
findings, real fixes, reproducible harness, honest scoping. The chaos
harness at `tests/chaos/` is the demo — we just need to package it as
a talk. H-4 (talk track) is the only real blocker on the doc side;
product-side the reproducers already work.

**Blocks on:** H-4, H-5.
**Lands:** KubeCon + CloudNativeSecurityCon submission, cross-posted
to BSides regional. The security-minded DevRel audience is an order
of magnitude more decision-maker-dense than the general-purpose AI
crowd.

---

## 8. Remediation priority (what to do Monday morning)

1. **C-1** — parameterize `AZ_AI_BIN` in all three demo scripts (30 min).
2. **C-2** — pick an `announce/` vs `launch/` convention and enforce it
   (1 hour).
3. **H-1** — reconcile default model (`gpt-5.4-nano` vs `gpt-4o-mini`)
   across sample config and cost doc (product call, then 30 min of
   edits).
4. **H-2** — README perf table v2 refresh (30 min + measurement time
   if not already in `docs/perf-baseline-v2.md`).
5. **H-3** — WSL section binary / size / latency refresh (1 hour).
6. **H-4** — write three `scripts/NN-*.talk.md` files (half a day).
7. **H-5** — speaker bureau bio + COI + headshot block (2 hours).
8. **M-1, M-2, M-3, L-1..L-4** — schedule behind H-series.

---

## 9. Non-goals reaffirmed

- No source edits landed as part of this audit.
- No doc rewrites; findings only.
- Demo `.sh` scripts not re-recorded against v2.0.4 — that's the work
  this audit unblocks, not the work it completes.

— *Keith Hernandez. I'm an athlete. The CFPs go out clean.*
