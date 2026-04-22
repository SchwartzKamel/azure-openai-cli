# Docs Audit — Benchmarks Segment (v2.0.4)

**Date:** 2026-04-22
**Auditor:** Kenny Bania (pre-merge perf / bench harness)
**Release under review:** v2.0.4 (tag `afa95fd`)
**Reference baseline:** `docs/benchmarks/bania-v2-dogfood-2026-04-22.md` (taken against v2.0.2)
**Scope:** `docs/benchmarks/**`, perf claims in prose (`README.md`, `ARCHITECTURE.md`, `CHANGELOG.md`, `docs/**`), `scripts/bench*.py|sh` headers, `tests/chaos/**` perf assertions.
**Non-goals:** no source edits; a spot-check re-measurement was run to sanity-check the 8.87 ms claim (see §4).

> *It's gold, Jerry. Mostly gold. But the front-page numbers on the README are v1 numbers pretending to be v2 numbers, and we've been advertising three bench flags that don't exist. Roll with me.*

---

## 1. Executive summary

| Severity | Count |
|---|---:|
| Critical | 2 |
| High | 4 |
| Medium | 5 |
| Low | 3 |
| Informational | 4 |

**Is the benchmark suite audit-ready?** **No — conditional.** The methodology in `bania-v2-dogfood-2026-04-22.md` is the strongest artifact in the tree and is, in isolation, reproducible. But four things disqualify the wider suite:

1. **README/ARCHITECTURE still advertise v1.8 numbers** (`5.4 ms`, `9 MB`) as if they were current. They are off by ~+64 % (startup) and ~+44 % (size) on v2.0.4.
2. **Multiple planning docs reference a `scripts/bench.py` CLI surface that does not exist** (`--cold --iterations`, `--ttft`, `--stream`, `--compare`, `--budget`). The real script is a 75-line `--version` timer with a positional binary arg. This is vaporware-by-citation.
3. **No v2.0.4 baseline has been snapshotted** — the "current" reference is a v2.0.2 measurement and the AOT binary used for it (`/tmp/az-ai-v2-aot/az-ai-v2`, mtime `Apr 22 07:12`) was never rebuilt post-FDR-fix.
4. **`scripts/bench.sh` benches `dotnet <dll>` — framework-dependent, not the shipping AOT form.** It cannot exercise the cold-start budget it claims to gate.

**Recommendation on v2.0.4 baseline snapshot:** **YES, snapshot now**, *before* any CI perf-bench job is wired. v2.0.4 shipped two FDR High fixes (err-unwrap, raw-config, ralph-exit) and dropped a matrix leg — small blast radius but nonzero perf surface, and cutting a stable reference now ends the "is the baseline v2.0.2 or v2.0.4?" drift question. Template: re-run the existing harness on the same `malachor` host, commit as `bania-v2.0.4-baseline-2026-04-22.md`, move the v2.0.2 doc under `docs/benchmarks/archive/`.

---

## 2. Findings

### CRITICAL

#### C1. README advertises stale v1.8 perf numbers as current
- **Files:** `README.md:3`, `README.md:16`, `README.md:70`
- **Problem:** The tagline says *"A **5ms-startup, 9 MB single-binary** Azure OpenAI agent…"* and the features table lists `Native AOT | 5.4 ms | ~9 MB`. On v2.0.4 the numbers are **8.87 ms p50 / 12.96 MB** (per `bania-v2-dogfood-2026-04-22.md`). These are the first two perf numbers any new user sees. They are wrong by +64 % / +44 %.
- **Fix:** Update to `~9 ms cold start, ~13 MB` with a footnote citing `docs/benchmarks/bania-v2-dogfood-2026-04-22.md` and the `malachor` reference hardware. Or frame as a band: *"sub-10 ms cold start, ~13 MB single binary"* and retire the precise v1 number.
- **Severity:** Critical — Peterman/marketing traceability is broken; these numbers appear in `docs/launch/*` social copy and will go on websites.

#### C2. Planning docs reference a `scripts/bench.py` CLI that doesn't exist
- **Files:**
  - `docs/v2-dogfood-plan.md:87-89` — cites `scripts/bench.py --cold --iterations 100`, `--ttft --endpoint $AZUREOPENAIENDPOINT`, `--stream --duration 30s`.
  - `docs/v2-cutover-checklist.md:159` — cites `python scripts/bench.py --compare 1.9.1 --budget cold=10% --budget ttft=5ms --budget stream=5% --budget binsize=15MB`.
  - `docs/ops/slos-v2.md:33` — cites `scripts/bench.sh --aot`.
- **Problem:** `scripts/bench.py` accepts only `<binary> [-n RUNS] [-w WARMUP] [--args …]`. It has no `--cold`, `--ttft`, `--stream`, `--compare`, `--budget`, `--endpoint`, `--duration`, or `--iterations` flags. `scripts/bench.sh` has `--binary` and `--compare` but no `--aot` mode and measures `dotnet <dll>`, not the AOT binary. These citations are fabricated interfaces. Any reader following the cutover checklist will hit "unrecognized argument" and lose trust in the doc.
- **Fix:** Either (a) downgrade the citations to *"planned — see `BANIA-V2-03`"* with a visible TODO, or (b) implement the flags before v2.1. Given `BANIA-V2-03` is the explicit "promote harness to `scripts/bench.py`" todo, (a) is correct today; (b) is the deliverable that closes it.
- **Severity:** Critical — makes the SLO doc and the cutover checklist non-executable.

### HIGH

#### H1. `ARCHITECTURE.md` perpetuates the 5.4 ms / 9 MB claim
- **File:** `ARCHITECTURE.md:651` (and implicit in the AOT publish narrative elsewhere)
- **Problem:** *"`make publish-aot` now produces a fully AOT-compatible ~9 MB single-file binary with **~5.4 ms cold start** (Linux x64)"*. Same staleness as C1; Architecture is the doc reviewers cite.
- **Fix:** Replace with current numbers and link `docs/benchmarks/bania-v2-dogfood-2026-04-22.md` (soon: v2.0.4 baseline). Note explicitly that the 5.4 ms figure was a v1.8 measurement.
- **Severity:** High.

#### H2. `scripts/bench.sh` measures framework-dependent startup, not AOT
- **File:** `scripts/bench.sh:82-89` (`time_one` hard-codes `"$DOTNET" "$dll"`)
- **Problem:** The header advertises *"pre-merge startup benchmark harness"* and the file is referenced by `docs/perf-baseline-v2.md:28-49` as the canonical tool. But every scenario runs `dotnet <path>.dll`, which exercises the JIT path, not the shipping AOT binary. `docs/perf-baseline-v2.md:49` admits this openly (*"AOT numbers were collected via a small companion loop… will be folded into `bench.sh` in a follow-up"*) — that follow-up never landed. As a result, `make bench` (Python, ✅ AOT) and `scripts/bench.sh` (bash, ❌ JIT DLL) disagree on what "cold start" means, and the doc trail hides the disagreement.
- **Fix:** Add a `--aot` mode (or accept a native binary path directly) so `time_one` can exec the binary without `dotnet` prefix. Until then, add a prominent banner in the header comment: *"NOTE: this harness measures the framework-dependent (`dotnet <dll>`) path. For AOT shipping-form numbers use `scripts/bench.py` or rebuild with `make publish-aot` and time the binary directly."*
- **Severity:** High.

#### H3. `CHANGELOG.md` misstates `bench.py`'s reported percentiles
- **File:** `CHANGELOG.md:528`
- **Problem:** *"Portable Python startup benchmark that captures wall-clock invocation time with statistical summaries (min/median/p95/max)."* Actual code reports **p90**, not p95 (`scripts/bench.py:68-69`). Small thing, large implication — any reader comparing the CHANGELOG's p95 claim against the script's output will see a different number and reasonably conclude the script is broken.
- **Fix:** Either change the script to report p95 (nearest-rank on `samples[int(0.95*n)-1]`) or correct the CHANGELOG to say p90. Prefer the former — p95 is what every other doc in this tree gates against.
- **Severity:** High (data-contract correctness).

#### H4. `scripts/bench.py` lacks p99, stdev, hardware capture — can't feed the dogfood schema
- **File:** `scripts/bench.py` (entire file)
- **Problem:** The dogfood report table columns are `mean / p50 / p95 / p99 / stdev / min / max`. The shipped script produces `min / median / mean / p90 / max`. A future CI job wired to this script cannot emit the doc's own reported schema. Plus no CPU/kernel/RID capture means results aren't self-describing.
- **Fix:** Extend `bench.py` to emit JSON with `{p50, p95, p99, mean, stdev, min, max, n, warmup, binary_sha256, size_bytes, uname, cpu_model, dotnet_version, timestamp}`. This is `BANIA-V2-03` territory — until it lands, mark bench.py as **experimental** in its docstring.
- **Severity:** High.

### MEDIUM

#### M1. `docs/espanso-ahk-integration.md` has two contradictory perf claims
- **File:** `docs/espanso-ahk-integration.md:452` (*"~8.9 MB, ~11 ms cold start"*) vs `:875` (*"~9 MB, ~5 ms startup"*).
- **Problem:** Same doc, different numbers, neither matches v2.0.4 reality (13 MB / 9 ms).
- **Fix:** Pick one — *"~13 MB single binary, sub-10 ms cold start on linux-x64 (ref: malachor)"* — and use it consistently. Cite the baseline doc.
- **Severity:** Medium.

#### M2. `docs/benchmarks/bania-v2-dogfood-2026-04-22.md` title says v2.0.2, repo is at v2.0.4
- **File:** `docs/benchmarks/bania-v2-dogfood-2026-04-22.md:1`
- **Problem:** Title: *"Bania v2.0.2 dogfood bench — 2026-04-22"*. Repo HEAD: `v2.0.4`. No perf re-run after FDR High fixes (commits `4842b6a`, `afa95fd`). Small surface (error-message unwrap, raw-config redaction, ralph exit) so regression unlikely — but unverified.
- **Fix:** Either (a) snapshot a v2.0.4 baseline (recommended — see §5) or (b) add a note at the top: *"Applies to v2.0.2 through v2.0.4 — FDR fixes between these tags touched error surfaces, not hot paths; no measurable perf delta expected. Verified spot-check 2026-MM-DD."*
- **Severity:** Medium.

#### M3. `maf-spike-pt2-2026-04-20.md` draws conclusions from N=2 and N=3
- **File:** `docs/benchmarks/maf-spike-pt2-2026-04-20.md:43-66`
- **Problem:** Conclusions like *"Phi-4 model appears slower on completion vs gpt-4o-mini baseline"* rest on 2–3 iterations with one declared outlier. Methodology tax: no warm-up count, no stdev, no p50/p95 despite the doc making quantitative comparisons.
- **Fix:** Add a banner: *"Spike-grade numbers (N=2–3). Not a baseline. Reproduce with `bench-foundry.sh --quick` for signal, `--full` (N=10) for a defensible comparison."* Link to the later `phi-vs-gpt54nano-2026-04-20.md` (N=10) which supersedes it.
- **Severity:** Medium.

#### M4. OTel/metrics overhead is on the watchlist but has no home in prose docs
- **Files:** `docs/benchmarks/bania-v2-dogfood-2026-04-22.md:83-98` (the data); nowhere in `README.md`, `ARCHITECTURE.md §Observability`, `docs/ops/slos-v2.md`.
- **Problem:** `--otel +2.7 ms`, `--metrics +4.2 ms`, combined +5.1 ms (at the 5 ms trigger). This is user-facing behaviour — Espanso users enabling telemetry flags will pay ~5 ms — but no user-facing doc mentions it. Bania's watchlist lives only in the benchmark report.
- **Fix:** Add a one-liner to `ARCHITECTURE.md §Observability` and `docs/ops/slos-v2.md` citing the measured overhead and the watchlist status.
- **Severity:** Medium.

#### M5. `tests/chaos/` has zero perf assertions
- **Dir:** `tests/chaos/**` (11 scripts)
- **Problem:** Prompt explicitly asks *"tests/chaos/ perf assertions"*. None exist. Chaos suite validates correctness/security but doesn't assert on cold-start, RSS, or stream cadence even though `10_ralph_depth.sh` and `04_config_chaos.sh` would be natural homes for smoke-grade perf guards (e.g., "400 ralph rounds complete in < 2 s"; "1k config reloads stay under 50 MB RSS").
- **Fix:** Add a lightweight perf assertion to one or two chaos scripts (or a new `12_cold_start_guard.sh`) that runs the AOT binary N times and fails if p95 > 20 ms. Low-fi CI canary, not a replacement for the full harness.
- **Severity:** Medium.

### LOW

#### L1. `docs/benchmarks/raw/20260420-0749/` is 80 files of JSON/TSV checked into main
- **Dir:** `docs/benchmarks/raw/20260420-0749/` (80 files)
- **Problem:** Raw request/response bodies for the Phi-vs-gpt-5.4-nano bench are committed. No secrets present (verified via grep for `api-key`, `subscription-key`, `Authorization`). But this pattern scales badly — the next bench-foundry run doubles the tree. Not critical, not even clearly wrong (signal is reproducibility), but there's no retention policy.
- **Fix:** Document retention in `docs/benchmarks/README.md` (which does not yet exist — create it). Recommend: keep raw for the most recent baseline per benchmark type; older runs move to `archive/` or get git-LFS'd.
- **Severity:** Low.

#### L2. No `docs/benchmarks/README.md`
- **Problem:** The directory has four artefacts of three different formats (dogfood report, model-vs-model, spike notes) and an 80-file `raw/` subdir, with no index. A stranger cannot tell which file is the current baseline.
- **Fix:** Add `docs/benchmarks/README.md` listing each artifact, its type (baseline / comparison / spike), the commit/tag it reflects, and the reference hardware. Mark the current baseline prominently.
- **Severity:** Low.

#### L3. `spike/agent-framework/bench.sh` and `docs/spikes/af-benchmarks.md` still referenced from ADR-004
- **File:** `docs/adr/ADR-004-agent-framework-adoption.md:53`
- **Problem:** ADR cites a bench path in `spike/` — fine historically, but these are pre-v2 artefacts and the ADR reads as if they're current. Minor freshness hit.
- **Fix:** Add *"(archived — superseded by `docs/benchmarks/bania-v2-dogfood-2026-04-22.md`)"* annotation.
- **Severity:** Low.

### INFORMATIONAL

#### I1. `bania-v2-dogfood-2026-04-22.md:23-27` is exemplary methodology
- Hardware, kernel, SDK version, thermal caveat, binary SHA-256s, build command, warm-up policy, sample size per scenario, endpoint, and a limitations note — all in one place. This is the template every future bench report should follow. Promote the methodology section into `docs/benchmarks/README.md` as the canonical schema.

#### I2. `bania-v2-dogfood-2026-04-22.md:128` names `BANIA-V2-03` as the "pin CI hardware" todo
- The report already flags its own single-host limitation and names the remediation. Audit confirms: this is the gating dependency before any PR-diff bench comment is meaningful. No merge gate should reference a measured number until `BANIA-V2-03` closes.

#### I3. `phi-vs-gpt54nano-2026-04-20.md` is well-sampled (N=10) and cost-cited
- Unlike its predecessor spike doc (M3), this report declares N, stdev, p50/p95, cost source, and links a raw dir. Good model for model-vs-model comparisons going forward.

#### I4. `make bench` target exists and works
- `Makefile:352-354` wires `python3 scripts/bench.py dist/aot/$(BIN_NAME) --args --version`. Verified executable. This is the one bench path that is both current-form (AOT) and discoverable. Document it more prominently in the README's Performance section.

---

## 3. Baseline freshness table

Every externally-citable perf number in the docs, mapped to its measurement origin:

| Number | Where cited (file:line) | When measured | On what hardware | Still the SLO? | Verdict |
|---|---|---|---|---|---|
| **5.4 ms cold start** | `README.md:16,70`, `ARCHITECTURE.md:651`, 7× in `docs/proposals/*`, 2× in `docs/adr/*`, `docs/launch/v2.0.0-release-body.md` | v1.8.0 (pre-MAF) | unstated (likely WSL2 / dev box) | **No** — v2.0.4 is 8.87 ms p50 | **STALE — fix globally** |
| **~9 MB binary** | `README.md:3,70`, `ARCHITECTURE.md:651,758`, ADR-001, ADR-003, ADR-004 | v1.8.0–v1.9.1 | as above | **No** — v2.0.4 is 12.96 MB | **STALE — fix globally** |
| **~11 ms cold start** | `docs/espanso-ahk-integration.md:452` | unstated | unstated | No — current 8.87 ms | **STALE + wrong direction** |
| **~5 ms startup** | `docs/espanso-ahk-integration.md:875` | unstated | unstated | No | **STALE** |
| **8.87 ms p50 cold** | `docs/benchmarks/bania-v2-dogfood-2026-04-22.md:57,72,106` | 2026-04-22 (v2.0.2) | `malachor` i7-10710U / Ubuntu / Linux 6.8 / .NET 10.0.201 | Yes (under 10 ms gate) | **CURRENT — refresh for v2.0.4** |
| **12.96 MB AOT** | `docs/benchmarks/bania-v2-dogfood-2026-04-22.md:63`, `CHANGELOG.md:202` | 2026-04-22 | as above | Yes (under 20 MB gate, under 1.5× gate) | **CURRENT** |
| **12.91 MB AOT (1.456×)** | `docs/perf-baseline-v2.md:5,124`, `CHANGELOG.md:203,375`, `docs/launch/*` (×5) | v2.0.0 cutover | `AB-DM4MFJ4` WSL2 Ultra 7 265H | Approximately — 12.91 → 12.96 MB is within build-noise | **CURRENT (slightly drifted)** |
| **12.58 ms mean `--version --short`** | `CHANGELOG.md:372` | v2.0.0 | WSL2 Ultra 7 | Approximate — overlaps v2.0.2 9.00 mean; hardware different | **HOST-DEPENDENT** (flag clearly) |
| **+2.7 ms `--otel` / +4.2 ms `--metrics`** | `bania-v2-dogfood-2026-04-22.md:86-89` only | 2026-04-22 | `malachor` | Yes (watchlist) | **CURRENT, not promoted to prose** |
| **−36 % tool round-trip v1→v2** | `bania-v2-dogfood-2026-04-22.md:14,62`; Peterman social copy | 2026-04-22, N=10 | `malachor` | Yes | **CURRENT — reproducible** |
| **+15 % stream bytes/sec** | `bania-v2-dogfood-2026-04-22.md:61` | 2026-04-22, N=10 | `malachor` | Yes | **CURRENT** |
| **Startup p95 ≤ 1.25× v1** | `CHANGELOG.md:372`, `docs/launch/v2.0.0-announcement.md:23` | v2.0.0 cutover (WSL2) | Ultra 7 265H | Yes directionally | **CURRENT (different host)** |
| **ADR-004: cold start ≤ +10 % (5.4 → 5.9 ms)** | `docs/adr/ADR-004:38` | Targets, not measurements | — | **No** — overshot (5.4 → 8.87 ms, +64 %). Decision was made with a waiver downstream but ADR still shows the original gate | **STALE TARGET — annotate** |
| **Cutover: cold start ≤ +10 %, TTFT ≤ +5 ms, binary ≤ 15 MB** | `docs/v2-cutover-checklist.md:18` | Targets | — | Mixed (binary passes, cold start missed, TTFT in noise) | **Needs post-cutover reconciliation note** |
| **SLO: p50 cold-start ≤ 20 ms (grounded)** | `docs/ops/slos-v2.md:33` | Target | CI ref runner (doesn't exist yet) | N/A — SLO is target, no runner | **BLOCKED ON `BANIA-V2-03`** |

---

## 4. Spot-check re-measurement (v2.0.4 sanity check)

Ran `python3 scripts/bench.py /tmp/az-ai-v2-aot/az-ai-v2 -n 30 -w 5` on this host (see bench.py caveats, H4). Binary on disk: **13 591 712 B, mtime `Apr 22 07:12`** — identical byte-for-byte to the v2.0.2 dogfood artifact. **No v2.0.4 rebuild occurred** — the shipped baseline was never re-measured on v2.0.4.

Result: `min 9.19 / median 9.33 / mean 10.11 / p90 13.73 / max 14.54 ms` (n=30, w=5).

Interpretation: **baseline holds within host-noise tolerance.** Median 9.33 ms vs dogfood 8.87 ms p50 = +5 % — well under any regression trigger, and this session's system load is unknown. Does not falsify the claim. **Does** confirm that the binary under test is v2.0.2 byte-identical (size match + mtime), so v2.0.4 proper has not been benched.

---

## 5. Recommendation on v2.0.4 baseline snapshot

**YES — snapshot now.** Rationale:

1. v2.0.4 shipped two `FDR High` fixes (`err-unwrap`, `raw-config`, `ralph-exit` — commits `4842b6a`, `afa95fd`) plus a matrix change. Expected perf impact is near-zero (error paths, not hot paths) but unverified.
2. v2.0.0 and v2.0.2 both have baselines; skipping v2.0.4 creates a gap right before the next feature work lands, making the next regression harder to localize.
3. Snapshot is cheap: same harness, same host, fresh `make publish-aot`, commit as `docs/benchmarks/bania-v2.0.4-baseline-2026-04-22.md`.
4. Simultaneously move `bania-v2-dogfood-2026-04-22.md` and both `2026-04-20` docs under `docs/benchmarks/archive/` — the tree root should only contain the *current* baseline + a `README.md` index (L2).
5. **Do not** snapshot until after `make publish-aot` has actually been re-run on v2.0.4. The current `/tmp/az-ai-v2-aot/` is the v2.0.2 artifact.

---

## 6. Suggested remediation order

1. **Fix C1 + H1** in one PR (README + ARCHITECTURE numbers) — one hour of work, unblocks Peterman and everyone quoting these numbers downstream.
2. **Fix C2** (either delete the fabricated CLI citations or hide them behind *planned* language) — one hour.
3. **Snapshot v2.0.4 baseline** (§5) and add `docs/benchmarks/README.md` (L2) — half a day including a fresh `make publish-aot`.
4. **Fix H3** (p90 vs p95 in bench.py — pick one and align CHANGELOG) — 15 minutes.
5. **Land `BANIA-V2-03`**: extend `bench.py` to the full schema (H4), add `--aot` mode to `bench.sh` (H2), pin CI runner class, wire PR-diff bench comment. This is the multi-week item; everything above can ship without waiting on it.
6. **M3 / M4 / L1–L3 / I1**: doc hygiene, can batch as a single "bench docs cleanup" PR.

---

## 7. Methodology (this audit)

- **Source commit:** `afa95fd` (v2.0.4, `HEAD`)
- **Files reviewed:** 9 direct (`docs/benchmarks/*`, 3 bench scripts, `Makefile`, `README.md`, `ARCHITECTURE.md`, `CHANGELOG.md`) + 15 cross-referenced (`docs/proposals/*`, `docs/adr/*`, `docs/launch/*`, `docs/ops/slos-v2.md`, `docs/v2-*.md`, `docs/perf-baseline-v2.md`, `docs/espanso-ahk-integration.md`)
- **Dynamic check:** one 30-run cold-start measurement on the v2.0.2-identical AOT binary on this host (§4). Not a re-baseline — a sanity check only.
- **Out of scope:** Frank's production SLO telemetry (separate charter), Newman's security review of the `raw/` payloads beyond a grep for auth material (no hits).

---

*— Kenny Bania. So I'm going through the docs, Jerry, and get this — we're still telling people it's 5.4 milliseconds. Five-point-four! On a binary that hasn't existed in four minor versions! Who's reviewing this with me? Jerry? …Jerry?*
