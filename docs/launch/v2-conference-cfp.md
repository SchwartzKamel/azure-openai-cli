# azure-openai-cli v2.0.0 — Conference CFP Pitch

**Author:** Keith Hernandez (DevRel / speaking)
**Status:** draft for CFP submission
**Baseline:** `HEAD` on `main`, v2.0.0 release window 2026-04-20

I'm Keith Hernandez. This is the speaker packet for v2.0.0. Three talks, one
demo, honest scope. We're not selling a SaaS — we're sharing patterns from an
indie OSS CLI that rebuilt itself on Microsoft Agent Framework without
breaking a single v1 flag. Every number in here has a file behind it.

---

## Table of contents

1. [Abstract A — AOT trim / deep-dev](#abstract-a--shipping-an-agentic-cli-in-aot)
2. [Abstract B — Chaos-drill hardening / security-SRE](#abstract-b--persona-memory-that-cant-read-devurandom)
3. [Abstract C — Fleet dispatch / DevRel](#abstract-c--a-seinfeld-ensemble-of-ai-agents-shipped-a-v2)
4. [Conference targeting matrix](#4-conference-targeting-matrix)
5. [Demo script — Abstract A](#5-demo-script--abstract-a)
6. [Opening-line options](#6-opening-line-options)
7. [Cut-list if the talk overruns](#7-cut-list-if-the-talk-overruns)

---

## Abstract A — Shipping an agentic CLI in AOT

**Title:** *Shipping an agentic CLI in AOT: trimming Microsoft Agent Framework to 12.91 MB*

**Track:** Deep-dev / .NET internals (.NET Conf, dotnetos, NDC)

**Elevator (≤140 char):** MAF + OpenTelemetry + Azure SDK on NativeAOT, single-file, 12.91 MB. Two csproj lines took us from 1.625× to 1.456× v1.

**Target audience:** .NET engineers shipping AOT CLIs and anyone who has stared at an `IL3053` warning from a Microsoft package and wondered whether to ignore it or panic.

**Why me to give it:** I maintain `azure-openai-cli`, an indie OSS project. We shipped v2 on MAF without waiving the 1.5× size gate. I'm not selling a product — I'm showing the exact levers, the measurements behind them, and the warnings we *couldn't* silence.

**Outline (5 bullets):**
- The gate: why we chose ≤1.50× v1 AOT size and what it cost us to honor it.
- First build: **15,105,904 bytes — 1.625×**. Over the gate. No waiver on the table.
- Measured lever sweep: `IlcGenerateStackTraceData` (no-op on net10), `DebuggerSupport` (+16 bytes of noise), `HttpActivityPropagationSupport` (−41 KB but breaks OTLP). What worked: `OptimizationPreference=Size` (−495 KB) + `StackTraceSupport=false` (−1.05 MB) = **−1.50 MB, 1.456×.** Commit `056920f`.
- The warnings we couldn't kill: `IL2104`/`IL3053` from `Azure.AI.OpenAI 2.1.0`, two methods ILC rewrites to "always throw" (`PostfixSwapMaxTokens`, `PostfixClearStreamOptions`). They're real, they're cold, we don't call them — we leave the warnings visible on purpose.
- What we didn't do: drop `Azure.AI.OpenAI` entirely (est. 0.3–0.9 MB more, tracked for 2.1). This is a ship-vs-polish story, not a heroics story.

**Demo script summary:** Two-minute AOT size A/B — show the csproj diff, show `ls -lh az-ai-v2` at 12.91 MB, run `--version --short` and `--estimate` to prove the binary still works with zero Azure creds. The live section never touches the network.

**Takeaways (3):**
- On net10, `StackTraceSupport=false` is the real stack-trace knob; the older `Ilc…` property is a no-op.
- Upstream AOT warnings you can't fix are still signal — suppress them and you lose the canary for the *next* regression.
- "No waiver" is a cultural artifact: if your gate has an escape hatch, you'll use it. Sources: [`docs/aot-trim-investigation.md`](../aot-trim-investigation.md), [`docs/perf-baseline-v2.md`](../perf-baseline-v2.md) §3.4, [`docs/release-notes-v2.0.0.md`](../release-notes-v2.0.0.md).

---

## Abstract B — Persona memory that can't read /dev/urandom

**Title:** *Persona memory that can't read /dev/urandom: hardening a Markdown file store against a real chaos drill*

**Track:** Security / SRE (KubeCon + CloudNativeSecurityCon, OSS Summit Security, BSides)

**Elevator (≤140 char):** A Markdown file store met an adversary who brought a 5 GB log, a `/dev/urandom` symlink, and a persona named `../../canary`. Three findings, one commit, green.

**Target audience:** security engineers, SREs, and library authors who think "it's just a file per user, how bad can it be" — and anyone writing a chaos harness for a CLI.

**Why me to give it:** I own the code that got exploited. FDR (our red-team persona) wrote the reproducers; Kramer shipped the fix in `a0ca066`. I'll walk through the three live-reproducible findings against real `azureopenai-cli-v2` bits and the exact patch that closed them. This is a post-mortem, not a marketing deck.

**Outline (5 bullets):**
- **F1 — RSS amplification.** `File.ReadAllText` on a 100 MB persona history peaked RSS at **431 MB** (`/usr/bin/time -v`), before any network I/O. Fix: tail-seek the last 32 KB via `FileStream.Seek(len − 32 KB, Begin)` + UTF-8 continuation-byte skip. `PersonaMemory.ReadHistory` / `ReadSeekableTail`.
- **F2 — `/dev/urandom` hang.** `.squad/history/rogue.md` symlinked to `/dev/urandom` never hits EOF; the CLI hung until SIGTERM. Fix: **dual device-guard** — `FileAttributes.Device` check *plus* symlink-target canonicalization (`FileInfo.ResolveLinkTarget(returnFinalTarget: true)`) against the expected `_baseDir/history/` root. Belt + 5-second `CancellationTokenSource` if both fail.
- **F3 — path traversal via persona name.** `Path.Combine(_baseDir, "history", $"{name}.md")` with `name = "../../canary"` produced `.squad/history/../../canary.md` — fully outside the sandbox. `ToLowerInvariant()` doesn't strip `/`. Fix: `SanitizePersonaName` rejects anything outside `^[a-z0-9_-]{1,64}$`, called on every entry point (read, append, log-decision, public `GetHistoryPath`).
- The drill harness: 11 scripted attack categories under `tests/chaos/`, reproducible on any Linux box with a snapshotted AOT binary. Commit `835b95e` put it in the repo; `a0ca066` flipped F1/F2/F3 from 🔴 to 🟢 with 33 new unit tests.
- What's still 🟡 and why we shipped anyway: F4–F8 (config parse amplification, `--max-tokens` range checks, world-writable config, the `Azure.AI.OpenAI` trim warnings). Honest scoping — "CLEAR" from Newman and FDR, not "perfect."

**Demo script summary:** Three reproducers, under 90 seconds each. `dd if=/dev/zero of=.squad/history/bloat.md bs=1M count=100` then `/usr/bin/time -v az-ai-v2 --persona bloat --estimate hi` shows the tail-seek (flat RSS). `ln -s /dev/urandom .squad/history/rogue.md` then the same invocation shows the device-guard log line. `az-ai-v2 --persona '../../canary' --estimate hi` shows the sanitizer rejecting the name with exit 1.

**Takeaways (3):**
- "It's a user's own directory" is not a trust boundary when `.squad.json` is committed and team-shared.
- Dual-guard matters: `FileAttributes.Device` is not reliable on all Unix filesystems; symlink canonicalization is the belt to its suspenders.
- A chaos drill without reproducible scripts is a story, not a test. Sources: [`docs/chaos-drill-v2.md`](../chaos-drill-v2.md), [`azureopenai-cli-v2/Squad/PersonaMemory.cs`](../../azureopenai-cli-v2/Squad/PersonaMemory.cs), [`docs/security-review-v2.md`](../security-review-v2.md) (Newman CLEAR verdict, 0 🔴 / 8 🟡).

---

## Abstract C — A Seinfeld ensemble of AI agents shipped a v2

**Title:** *A Seinfeld ensemble of AI agents shipped a v2: the fleet-dispatch pattern for OSS maintenance*

**Track:** Community / DevRel / Practice (All Things Open, OSS Summit, FOSDEM Community devroom, Open Source 101)

**Elevator (≤140 char):** 25 archetype agents — Costanza, Kramer, Newman, FDR — drove v2 of an OSS CLI to green. Here's what worked and what collided.

**Target audience:** OSS maintainers, DevRel leads, and any solo-ish developer who uses Copilot / Claude / agent frameworks and wants a pattern beyond "one chat window, one task."

**Why me to give it:** I'm a cast member, not the cast itself. I can walk through the pipeline honestly — including the places where two agents editing the same file in parallel had to be serialized. This is an indie project's workflow report, not a vendor pattern.

**Outline (5 bullets):**
- The roster: **25 archetype agents** under [`.github/agents/`](../../.github/agents/) — PM (Costanza), implementer (Kramer), security (Newman), chaos (FDR), SRE (Frank Costanza), perf (Bania), release (Lippman), docs (Elaine), a11y (Mickey), licensing (Jackie), change mgmt (Wilhelm), DevRel (me), and more. Each has a `.agent.md` skill file defining voice, scope, deliverables, and standards.
- The dispatch pattern: PM writes the gate matrix → specialist agents fan out in parallel → captain serializes merges. Concrete example from v2 cutover: Bania on AOT trim (`056920f`), Newman on security review (`3c35ecf`), FDR on chaos drill (`835b95e`) — all ran in parallel; `a0ca066` (PersonaMemory fixes) merged after FDR filed F1/F2/F3.
- What worked: specialized voices force specialized reviews. Newman's clipboard is not Bania's clipboard. The FR-008 cache shipped with three documented deviations from the proposal (opt-in not opt-out, 7-day TTL not 24h, mtime eviction not strict LRU) because Costanza ruled on each one in writing — [`docs/proposals/FR-008-prompt-response-cache.md`](../proposals/FR-008-prompt-response-cache.md), commit `632068a`.
- What didn't: parallel dispatch on the **same file** bit us. Dispatching Kramer to harden `PersonaMemory.cs` while another thread was already touching it produced a merge collision that cost more time than a serial dispatch would have. Lesson: partition by *file path*, not just by *persona*.
- The diagram, honestly drawn: proposal → gate matrix → parallel specialist dispatch → serialized merge → release captain (Lippman) → tag. 15 commits between `9e74961..488aebd` on `main`, plus G1–G10 go-time checklist in [`docs/v2-cutover-decision.md`](../v2-cutover-decision.md).

**Demo script summary:** Open three `.agent.md` skill files side-by-side (Newman, Kramer, Costanza) — show the voice / scope contrast. Walk through the v2 cutover decision doc's precondition matrix live. End with the release notes and the commit graph — no API calls, no slideware charts the audience can't verify.

**Takeaways (3):**
- Archetype-per-concern is cheaper than role-per-task; the voice enforces the scope.
- Serialize on file paths, parallelize on archetypes. Two agents editing the same `.cs` is a merge conflict waiting to happen.
- A proposal that disagrees with the shipped code is a culture cost. FR-008's "Shipped deviations" block is the pattern to copy. Sources: [`.github/agents/`](../../.github/agents/), [`docs/v2-cutover-decision.md`](../v2-cutover-decision.md), commits `4f1acdd`, `a0ca066`, `632068a`, `2716be0`, `cb251d1`.

---

## 4. Conference targeting matrix

| Conference | Track | Abstract fit | CFP window (approx) | Notes |
|---|---|---|---|---|
| **.NET Conf** (Microsoft, virtual, Nov) | Deep-dev / AOT | **A** (strong) | CFP ~Jul–Aug | Natural home: Microsoft Agent Framework + NativeAOT is the dead-center audience. Lead here. |
| **dotnetos Conference** (Warsaw, Oct) | .NET internals | **A** (strong) | CFP ~May–Jun | dotnetos crowd lives for trim knobs and ILC diagnostics. Perfect deep-dive venue. |
| **NDC Oslo / London / Copenhagen** | .NET / platform | **A** (good), **C** (maybe) | rolling, check per event | NDC takes practitioner stories; A fits the "build + ship" track, C could land on a "dev productivity / AI" track. |
| **KubeCon + CloudNativeSecurityCon** | SecurityCon | **B** (strong) | CFP ~Feb (NA) / Jul (EU) | Framed as "supply-chain / runtime hardening of an OSS CLI" — F1/F2/F3 are concrete, reproducible, CNCF-tone-appropriate. |
| **OSS Summit (Linux Foundation)** | Security / Community | **B** (strong), **C** (strong) | CFP ~Feb (NA) / May (EU) / Jun (Japan) | Two separate submissions — B to the security track, C to the community track. |
| **FOSDEM** (Brussels, Feb) | Community devroom | **C** (strong) | devroom CFPs ~Oct–Nov | Community devroom or the relevant language devroom. Low-ceremony, high-signal audience for the fleet-dispatch story. |
| **All Things Open** (Raleigh, Oct) | OSS practice | **C** (strong) | CFP ~Apr–May | ATO loves indie-OSS pattern talks. C is a natural fit. |
| **BSides** (regional, year-round) | Defensive security | **B** (good) | rolling per chapter | B is a 30-minute BSides talk all day long — reproducers, no marketing. |
| **Strange Loop successors / local meetups** | General | **A** or **C** | rolling | Use for dry-runs before the big stages. Rehearse on Triangle .NET, PDX .NET, etc. |

**Submission priority:** B → KubeCon SecurityCon first (highest bar, best forcing function). A → .NET Conf + dotnetos. C → FOSDEM community devroom + ATO.

---

## 5. Demo script — Abstract A

Runs on a laptop. No Azure creds needed for anything below except the clearly banner-marked section. Copy-pasteable. Every step is idempotent on the demo machine.

```bash
# ──────────────────────────────────────────────────────────────────
# Demo: Shipping an agentic CLI in AOT — 12.91 MB, 1.456x v1
# Pre-req: the v2 repo is cloned; .NET SDK 10 is on PATH.
# ──────────────────────────────────────────────────────────────────

# Step 1 — Set up. Show where we are, what version of .NET.
cd ~/demos/azure-openai-cli
dotnet --version                                   # expect 10.0.x

# Step 2 — The two csproj lines. This is the whole story.
grep -n 'OptimizationPreference\|StackTraceSupport' \
  azureopenai-cli-v2/AzureOpenAI_CLI_V2.csproj

# Step 3 — Clean publish v1 (baseline, 8.86 MB).
dotnet publish azureopenai-cli   -c Release -r linux-x64 -p:PublishAot=true -v q
ls -lh azureopenai-cli/bin/Release/net10.0/linux-x64/publish/az-ai

# Step 4 — Clean publish v2 (shipped form, 12.91 MB).
dotnet publish azureopenai-cli-v2 -c Release -r linux-x64 -p:PublishAot=true -v q
ls -lh azureopenai-cli-v2/bin/Release/net10.0/linux-x64/publish/az-ai-v2

# Step 5 — The ratio, out loud.
V1=$(stat -c %s azureopenai-cli/bin/Release/net10.0/linux-x64/publish/az-ai)
V2=$(stat -c %s azureopenai-cli-v2/bin/Release/net10.0/linux-x64/publish/az-ai-v2)
awk -v a="$V2" -v b="$V1" 'BEGIN{printf "v2/v1 = %.3fx (gate: 1.500x)\n", a/b}'

# Step 6 — The IL3053 / IL2104 warnings we leave visible. Show, don't hide.
dotnet publish azureopenai-cli-v2 -c Release -r linux-x64 -p:PublishAot=true 2>&1 \
  | grep -E 'IL2104|IL3053|will always throw' | head -n 5

# Step 7 — Prove the shipped binary still works offline.
V2BIN=azureopenai-cli-v2/bin/Release/net10.0/linux-x64/publish/az-ai-v2
$V2BIN --version --short                           # ~12 ms p95 on AOT

# Step 8 — Gate-2 command timed, 10 runs. No creds needed.
for i in $(seq 1 10); do /usr/bin/time -f '%e' $V2BIN --version --short 2>&1 >/dev/null; done

# Step 9 — Offline estimator. No network, no AZUREOPENAIAPI read.
$V2BIN --estimate "Summarize the AOT trim investigation."

# Step 10 — --estimate short-circuits before credentials; prove it with an empty env.
env -i PATH=$PATH $V2BIN --estimate "hello world"

# Step 11 — Help path: verify p95 under budget on the audience's machine.
for i in $(seq 1 10); do /usr/bin/time -f '%e' $V2BIN --help >/dev/null; done

# ──────────────────────────────────────────────────────────────────
# ⚠️ REQUIRES AZUREOPENAIENDPOINT + AZUREOPENAIAPI
# If you want to show a real call, do it from a pre-loaded shell
# with env vars already set offstage. Never paste secrets on camera.
# ──────────────────────────────────────────────────────────────────

# Step 12 — (OPTIONAL, live API) prewarm + one real call.
#   $V2BIN --prewarm "Name three AOT trim levers that ILC respects on net10."

# Step 13 — Confirm trim warnings did not become runtime crashes.
$V2BIN --help >/dev/null && echo "exit 0 — cold paths not hit"

# Step 14 — Show the investigation doc that sourced these numbers.
sed -n '10,20p' docs/aot-trim-investigation.md       # Outcome table

# Step 15 — Close: the file that made it ship. Two lines.
awk '/OptimizationPreference|StackTraceSupport/' \
  azureopenai-cli-v2/AzureOpenAI_CLI_V2.csproj
```

**Safety rails:**
- Demo machine has `AZUREOPENAIAPI` unset for all non-step-12 work. Step 12 is pre-loaded offstage.
- Every `ls -lh` and `stat` runs against files produced in-session; the audience sees the bytes.
- If WiFi dies, steps 1–11 + 13–15 still run. The only network-dependent step is the optional step 12 — cut it.

---

## 6. Opening-line options

Pick one. Each ≤ 25 words. Rehearsed three times before stage.

1. *"I'm Keith Hernandez. Microsoft Agent Framework plus OpenTelemetry plus the Azure SDK, on NativeAOT, single file — twelve point nine one megabytes. Two lines in a csproj got us there."*

2. *"A 100 megabyte Markdown file took our CLI's RSS to 431 megabytes before we hit the network. Tonight I'll show you the three commits that fixed it."*

3. *"Twenty-five archetype agents shipped a v2. Not one of them is a founder. Let me walk you through the dispatch graph that actually got us to green."*

---

## 7. Cut-list if the talk overruns

Ordered. At the 25-minute mark on a 30-minute slot, start cutting from the top.

**Abstract A cut-list:**
1. Cut the framework-dependent benchmark section (Perf §3.1). AOT is the story; JIT is an appendix. (Saves ~2 min.)
2. Cut the `Azure.AI.OpenAI` direct-SDK rewrite exploration (future work). Point at the doc and move on. (Saves ~1.5 min.)
3. Cut the `HttpActivityPropagationSupport` anecdote. Interesting, not essential. (Saves ~1 min.)
4. Cut the RSS comparison entirely — it's a footnote next to binary size. (Saves ~1 min.)
5. **Do not cut:** the two-line csproj diff, the 1.625×→1.456× before/after, the "why we kept IL3053 visible."

**Abstract B cut-list:**
1. Cut F4–F8 (the 🟡 findings). They're honest scoping but not the narrative. (Saves ~2 min.)
2. Cut the harness architecture diagram — just show `tests/chaos/` in a terminal. (Saves ~1.5 min.)
3. Cut the Windows/macOS symlink-semantics footnote. Linux is enough. (Saves ~1 min.)
4. Collapse F1 and F2 into one reproducer (they share a fix path). (Saves ~2 min.)
5. **Do not cut:** the live 5 GB log repro, the `../../canary` name, the dual device-guard explanation.

**Abstract C cut-list:**
1. Cut the full 25-archetype roster read-out. Show four on screen, name-check the rest. (Saves ~2 min.)
2. Cut the FR-008 three-deviation walkthrough — reference it, don't narrate it. (Saves ~2 min.)
3. Cut the "release captain" commit-graph flyover. Audience can scan the repo. (Saves ~1.5 min.)
4. Cut the go-time G1–G10 checklist tour. One screenshot suffices. (Saves ~1 min.)
5. **Do not cut:** the parallel-agent file-collision anecdote — it's the one part no vendor talk will ever cover.

---

*— Keith Hernandez. I play hard, I play fair. Every number has a file behind it. Tip the crew, tip the A/V tech, and rehearse the demo on a cold machine 48 hours before the talk.*
