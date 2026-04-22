# "The Docs Audit" — Episode Transcript

**Season 2, Episode 04 · Docs Audit Night · 2026-04-22**

> **Log line.** Jerry is put on the stand when the CHANGELOG and the
> binary disagree about what version they are. George tries to help.
> Everyone learns Babu was right all along.

**Consolidator:** Elaine Benes (Technical Writing)
**Cast audited:** Jerry (DevOps) · George Costanza (PM, helping) · Newman
(Security) · Babu Bhatt (i18n) · Morty Seinfeld (FinOps) · Kenny Bania
(Perf) · Mickey Abbott (a11y) · Jackie Chiles (Legal) · Keith Hernandez
(DevRel) · The Maestro (Prompt) · Puddy (QA) · Mr. Lippman (Release).
**Files reviewed across the fleet:** 147 markdown + 9 root-level +
`.github/agents/*.agent.md` + packaging manifests.
**Individual reports:** 12 (see §9, "Credits").

---

## 1. Cold open

> **JERRY** (on the stand, tie straight): "The docs are fine."
>
> **GEORGE** (leaning in, helpfully, with his destroying-the-case
> whisper): "Jerry. The binary says 2.0.2."
>
> **JERRY:** "We shipped 2.0.4."
>
> **GEORGE:** "I know."
>
> **JERRY:** "The tag is 2.0.4."
>
> **GEORGE:** "I know."
>
> **JERRY:** "So why does --version say 2.0.2?"
>
> **GEORGE** *(triumphantly)*: "THAT'S what I'm saying!"

Cut to title card.

---

## 2. The verdict

The fleet audited every markdown file in the repository and every adjacent
source-of-truth (csproj, workflow, packaging manifest, agent spec). The
diagnosis is unanimous — **twelve independent audits converged on the same
sentence:**

> **The code is hardened. The docs are nine-to-twelve months behind the
> code.**

This is a *good* kind of drift in one sense — it means engineering shipped
faster than documentation could keep pace — but it is a *dangerous* drift
in every other sense, because the repository now advertises itself as
something it isn't:

- README advertises a five-platform binary matrix; the matrix is four.
- README shows `azure-openai-cli-<rid>.tar.gz`; artifacts ship as
  `az-ai-v2-<tag>-<rid>.{tar.gz,zip}`.
- Security docs describe a v1 architecture (`Process.Start`,
  `RALPH_DEPTH` env var); v2 is in-process MAF with `AsyncLocal<int>`.
- Cost docs list `gpt-4o-mini` as default; `cost-optimization.md` §3.6
  explicitly decided against nano-as-default, yet the brief for this
  audit thought nano *was* the default. Team-wide confusion is the
  finding.
- Test docs cite 538 / 541 / 555+ tests. Reality is **1510**.
- Front-page performance numbers cite 5.4 ms cold start / 9 MB binary.
  Reality is **8.87 ms / 12.96 MB** — off by +64 % / +44 %.
- The `v2.0.4` binary **reports its version as `2.0.2`**.

The last bullet is a genuine shipped bug, not a doc bug. It is also the
most important thing this audit will surface. See §4.

---

## 3. Findings at a glance

| Cast member | Scope | Crit | High | Med | Low | Info | Total | Report |
|---|---|---:|---:|---:|---:|---:|---:|---|
| **Elaine** | Top-level prose (README, ARCH, CONTRIBUTING, IMPLEMENTATION_PLAN, AGENTS, CoC) | 3 | 7 | 6 | 4 | 1 | **21** | [elaine](docs-audit-2026-04-22-elaine.md) |
| **Jerry + Costanza** | DevOps · CI · runbooks · Dockerfile · Makefile | **2** | 3 | 4 | 3 | 5 | **17** | [jerry](docs-audit-2026-04-22-jerry.md) |
| **Newman** | `SECURITY.md`, `docs/security/`, `docs/audits/` | 0 | 5 | 4 | 3 | 5 | **17** | [newman](docs-audit-2026-04-22-newman.md) |
| **Babu Bhatt** | i18n / Unicode / RTL across 147 md files | 0 | 2 | 4 | 3 | 2 | **11** | [babu](docs-audit-2026-04-22-babu.md) |
| **Morty Seinfeld** | `docs/cost-optimization.md` + pricing refs | **2** | 5 | 9 | 4 | 4 | **24** | [morty](docs-audit-2026-04-22-morty.md) |
| **Kenny Bania** | `docs/benchmarks/`, perf claims everywhere | **2** | 4 | 5 | 3 | 4 | **18** | [bania](docs-audit-2026-04-22-bania.md) |
| **Mickey Abbott** | a11y / NO_COLOR / screen-reader / keyboard-only | 0 | 2 | 5 | 4 | 3 | **14** | [mickey](docs-audit-2026-04-22-mickey.md) |
| **Jackie Chiles** | `THIRD_PARTY_NOTICES.md`, `docs/legal/`, license attribution | 0 | 1 | 4 | 3 | 1 | **9** | [jackie](docs-audit-2026-04-22-jackie.md) |
| **Keith Hernandez** | `docs/demos/`, `docs/announce/`, `docs/examples/`, WSL guide | **2** | 5 | 3 | 4 | 0 | **14** | [keith](docs-audit-2026-04-22-keith.md) |
| **The Maestro** | Prompts · `.github/agents/` · squad · Ralph | 0 | 4 | 6 | 5 | 4 | **19** | [maestro](docs-audit-2026-04-22-maestro.md) |
| **Puddy** | `docs/testing/`, test-section prose, CI gates | **2** | 6 | 7 | 5 | 4 | **24** | [puddy](docs-audit-2026-04-22-puddy.md) |
| **Mr. Lippman** | `CHANGELOG.md`, `docs/launch/`, release runbook, hash-sync | **3** | 4 | 5 | 4 | 2 | **18** | [lippman](docs-audit-2026-04-22-lippman.md) |
| **Totals** |  | **16** | **48** | **62** | **45** | **35** | **206** | |

**Positive findings worth calling out:**

- **Babu (P-1):** 0 smart-quote contaminations, 0 BOMs, 0 CRLF, 0 mixed
  line endings across 147 md files. Plus
  `<InvariantGlobalization>true</InvariantGlobalization>` in the v2
  csproj — shipping-strong i18n *product*, just a missing i18n *doc*.
- **Jackie (P-2):** **Zero copyleft contamination.** No GPL / LGPL /
  AGPL / SSPL / CDDL / CC-BY-NC in either csproj graph.
  `scripts/license-audit.sh` hardfails on drift. Attribution gaps are
  two (a bundled gif + one stale version number).
- **Mickey (P-1):** The v2 binary has no spinner, no cursor-hide
  escapes, no persistent TUI. Silent-by-design is textbook a11y. The
  7-rule `NO_COLOR`/`FORCE_COLOR`/`CLICOLOR`/`TERM=dumb`/TTY
  precedence in `Theme.cs:108-142` is perfect — the only flaw is zero
  user-facing docs mention it.
- **Newman:** **No new CVE-worthy issues.** Code posture strong. SBOM,
  SLSA, digest pinning, secret redaction (`UnsafeReplaceSecrets` from
  FDR fix `4842b6a`) all present.
- **Maestro:** 25/25 agent archetype files pass the four-section
  layout check. Voice contracts are quotable and distinct. Persona
  *structure* is professional; persona *evaluation* isn't built yet.

---

## 4. The emergency (fix immediately)

There is exactly one finding in this audit that breaks the *user's*
workflow rather than the *maintainer's* trust. Everyone else can stay in
the priority queue. This one cannot.

### 🚨 **SHIPPED VERSION LIES — v2.0.5 fix-forward required**

**Source:** Lippman C-1 + C-2.

- `packaging/tarball/stage.sh:30` was never rolled past `2.0.2`. The
  `v2.0.4` tag was cut and published, but the release tarballs embed
  **`2.0.2`** in their filenames: `az-ai-v2-2.0.2-linux-x64.tar.gz`,
  etc. The file-level digest is of a v2.0.4 binary, but the URL and
  filename advertise v2.0.2.
- `azureopenai-cli-v2/Program.cs:1550-1551` (and `Telemetry.cs:31`)
  hardcode `"2.0.2"` in the `--version --short` output. The binary
  downloaded from the `v2.0.4` release reports itself as `2.0.2`.
- `brew test` asserts `2.0.4` via the formula Lippman hash-synced —
  the test will fail on install.

**Impact:** downstream users scripting against the release API get URL
mismatches; `brew install az-ai-v2@2.0.4` will install the binary but
fail its own test block; anyone inspecting `az-ai-v2 --version` thinks
they are on the old build. Not a security issue. It is an identity
issue: the tool does not know who it is.

**Plan of record:**

1. Open a `v2.0.5` track on `main` immediately.
2. Roll `stage.sh`, `Program.cs`, `Telemetry.cs` version strings to
   `2.0.5` (not `2.0.4` — we already shipped `2.0.4` with broken
   strings; no retag).
3. Add a smoke test: `./az-ai-v2 --version --short` must equal
   `$(cat VERSION)` (or equivalent), gated in CI so this never ships
   again.
4. Bania snapshots a fresh `v2.0.5` baseline on the same host.
5. Lippman re-hash-syncs formulas on publish.
6. CHANGELOG `[2.0.5]` includes a note that `v2.0.4` binaries exist
   and are byte-valid but advertise the wrong version; offer a
   migration line.

Kramer owns the code-side fix; Lippman owns the release flow; Bania
owns the re-baseline.

---

## 5. Acts (prioritized work)

The findings fall into four natural waves. Each wave is small enough to
fit into a named sprint; the dependency chain runs top-to-bottom.

### Act I — Truth-in-advertising (blocking user trust)

**Why first:** fix the lies the front page tells. Every hour we delay,
some new user gets a 404 on a download URL or a contradicting security
claim.

- **Elaine C1–C3:** Rewrite README Install table to match `release.yml`
  matrix (4 legs, v2 artifact naming, `v2.0.5` once shipped). Fix the
  Quickstart `.env.example` path (v2 tree). *(2h)*
- **Elaine H2+H3:** Resolve the v1/v2 security contradiction. README
  says ".env never baked"; ARCHITECTURE.md says ".env baked at build
  time." Pick the true one (README is correct per `Dockerfile`) and
  delete the false one. *(1h)*
- **Elaine H5:** CODE_OF_CONDUCT currently routes harassment to public
  GitHub Issues; CONTRIBUTING mandates a private channel. One-line
  fix. Reporter-safety bug. *(15m)*
- **Bania C1:** Purge the `5.4 ms / 9 MB` perf numbers from the
  front page. Cite the v2.0.4 baseline (`8.87 ms / 12.96 MB`) or the
  v2.0.5 re-baseline once Bania takes it. *(1h)*
- **Bania C2 / §5 of Bania report:** Delete references to
  `bench.py --cold --ttft --stream --compare --budget` and
  `bench.sh --aot` flags. They don't exist. *(30m)*
- **Morty C-1:** Pick a default model and state it in ONE place.
  Currently `gpt-4o-mini` in `Program.cs` conflicts with `gpt-5.4-nano`
  cited in 8 docs and conflicts with `cost-optimization.md §3.6` which
  explicitly ruled out nano-as-default. Make one choice; cascade
  everywhere. *(2h)*
- **Puddy H1:** Update test-count docs. `tests/README.md` says
  "555+ tests"; README says 538; release runbook says 541. Reality is
  **1510 (1025 v1 + 485 v2)**. *(30m)*
- **Lippman C-3:** Rewrite `docs/runbooks/release-runbook.md` for v2
  reality — correct csproj path, 4-leg matrix (no osx-x64), hash-sync
  step documented, `gh run rerun --failed` lever added. *(2h)*
- **Keith C1:** All three demo scripts call `az-ai` instead of
  `az-ai-v2`. Zero of three run on a fresh v2.0.4 install. *(1h)*
- **Keith C2:** Stale `docs/announce/v1.8.0-launch.md` remains on the
  announce index. Archive or redirect. *(15m)*

**Act I effort:** ~10 engineer-hours. **Ships with v2.0.5.**

### Act II — Missing docs of record (discoverability)

**Why second:** every finding below is the *first time somebody looks
for guidance and doesn't find it*. Ship the missing doc once, link it
from README and SECURITY.md, and reclaim a dozen smaller findings for
free.

- **Mickey top-rec:** `docs/accessibility.md` — five sections: color
  contract (`NO_COLOR`/`FORCE_COLOR`/`CLICOLOR`/`TERM=dumb`/TTY
  precedence from `Theme.cs:108-142`), `--raw` contract, exit-code
  table, keyboard-only workflows, known gaps. **This one page closes
  H-001 + M-001 + M-002 + M-003 + M-004 simultaneously.** *(3h)*
- **Babu H-02:** `docs/i18n.md` — declares
  `InvariantGlobalization=true` contract, USD-only / no-conversion
  cost statement, worked non-ASCII path/prompt example, RTL notes,
  future-`--locale` reservation. *(3h)*
- **Newman F-4:** `docs/security/index.md` — links SECURITY.md to every
  audit report in `docs/audits/` and every security-review in
  `docs/security/`. Orphaned reports are worthless reports. *(1h)*
- **Jerry Medium-6:** `docs/runbooks/macos-runner-triage.md` —
  consolidates the scattered postmortem knowledge on GitHub Actions'
  macos-13 backlog (and what to do when `osx-arm64` / `macos-14`
  inevitably has its own day). Jerry promised this across three commit
  messages and never shipped it. *(2h)*
- **Maestro H1+H2:** `docs/prompts/` + a minimal eval harness. Move the
  14 production prompts out of C# string literals; version them; make
  changes testable. *(8h — the biggest single item in the audit)*
- **Puddy C1:** `docs/testing/README.md` with TDD/flaky/contract-test
  playbooks. *(2h)*

**Act II effort:** ~19 engineer-hours. **Candidate for v2.1.0.**

### Act III — Postmortem hygiene + stale content (trust)

- **Newman F-1:** `SECURITY.md` last-updated 9 months ago; supported-
  versions still shows `1.8.x (current)`. Refresh. *(30m)*
- **Newman F-5:** `SECURITY.md §12 DelegateTaskTool` describes the v1
  `Process.Start` subprocess model. v2 is in-process MAF with
  `AsyncLocal<int>` depth tracking. Rewrite from `DelegateTaskTool.cs`
  ground truth. *(1h)*
- **Newman F-2:** Document `UnsafeReplaceSecrets` (the FDR-4842b6a
  fix — `Program.cs:1348`). Redaction of `AZUREOPENAIAPI` + endpoint
  hostname from every error surface is a **user-visible security
  commitment** that currently lives only in the patch. *(30m)*
- **Jerry postmortems:** Add **RESOLVED** banners + resolution-commit
  pointers to `v2.0.1-release-attempt-diagnostic.md`,
  `v2.0.2-release-attempt-diagnostic.md`,
  `v2.0.2-publish-handoff.md`, `v2-tag-rehearsal-report.md`.
  Currently read as live incidents. *(30m total for 4 files)*
- **Jerry High-2 / Low-1:** `release-v2-playbook.md` has a 25-line
  macos-13 troubleshooting block that is now historical. Move it
  to `docs/launch/archive/` with a note or delete. *(30m)*
- **Jerry table of 14 rows:** purge every other `macos-13` /
  `osx-x64` reference in forward-looking docs (historical
  postmortems stay). *(1h)*
- **Jackie M-*:** Source-attribute the `img/its_alive_too.gif`
  (likely 1931 *Frankenstein* derivative — either find the public-
  domain or generate a replacement). Fix stale `NOTICE:30-157` v1
  baseline block (lists `Azure.AI.OpenAI 2.1.0`; csproj declares
  `2.9.0-beta.1`). *(1h)*
- **Lippman `v2.0.3` limbo:** v2.0.3 was tagged and cancelled. Either
  delete the tag and note the supersession cleanly, or add a
  CHANGELOG `[2.0.3]` "never published" entry. *(30m)*
- **Elaine H1:** `IMPLEMENTATION_PLAN.md` is a stuck-in-amber v1.9.0
  roadmap. Archive or rewrite. *(2h if rewrite, 5m if archive)*
- **Maestro M5:** Default-model is contradicted in 8 places (the Morty
  C-1 finding from a prompt angle). Already in Act I. No-op here.

**Act III effort:** ~8 engineer-hours. **Rolling trust maintenance.**

### Act IV — Forward-looking lift (optimization)

- **Morty C-2 + §5 rewrite:** Document Azure native prompt caching,
  Batch API (50 % off), FR-008 cache with accurate ROI numbers (not
  the 10×-overstated claim currently there). *(3h)*
- **Bania baseline snapshot + archive:** Fresh v2.0.5 baseline on the
  same hardware; move older reports to `docs/benchmarks/archive/` with
  an index. *(2h — can parallel with the v2.0.5 Act I cut)*
- **Bania bench-harness promotion:** `scripts/bench.py` → real
  CLI-flagged bench (todo `bania-v2-03` already open). *(4h)*
- **Keith WSL talk-track:** the Espanso/AHK WSL walkthrough (once
  v2.0.5 numbers land) is a 15-minute conference talk wholesale.
  Promote it: abstract draft, speaker bureau bio, rehearsed demo.
  *(4h)*
- **Maestro safety clauses:** Bake the `SAFETY_CLAUSE` (currently
  invisible) into the 5 default Squad personas. Document it. Users
  overriding `--system` currently lose it silently. *(2h)*
- **Maestro temperature cookbook:** one table, large uplift. *(1h)*
- **Babu M-02:** 267 table cells across 72 files where a bare
  ✅/🟢/⚠ emoji is the only meaning. Add text labels for screen-
  reader pass-through. *(3h — shared work with Mickey a11y push)*
- **Puddy H-3:** Fix `make test` / `make preflight` to run *both* v1
  and v2 test projects. Local green can currently be CI red. *(30m)*
- **Jackie docs-lint CI check:** reject any md introducing a bare
  emoji with no text alt in a table cell. Runs fast, pays off
  forever. *(2h)*

**Act IV effort:** ~21 engineer-hours. **Roadmap.**

---

## 6. Jerry's mea culpa, for the record

Read the full report at
[`docs-audit-2026-04-22-jerry.md`](docs-audit-2026-04-22-jerry.md). The
operative sentences, unedited:

> 1. **Oversold macos-13 reliability** — wrote a 25-line "wait,
>    escalate, shelve" troubleshooting block in
>    `release-v2-playbook.md` as if the backlog were a managed risk.
>    It wasn't. Should have cut osx-x64 after v2.0.2; deferred three
>    tags too long.
> 2. **Left postmortem docs without "resolved" banners.** Four v2.0.x
>    diagnostic files still read as live incidents. A sticker takes
>    ten seconds. I didn't add forty seconds of clarity across four
>    files.
> 3. **Never consolidated the macOS-runner triage runbook.** Promised
>    across three commit messages, still doesn't exist.

And the George beat from the report's PM critique sidebar, which is
better than my summary:

> **COSTANZA:** "If the runbook describes a five-platform matrix we
> haven't had in three tags — Jerry, that's not a doc, that's a dream
> journal."

---

## 7. The one finding that was actually Babu

For the record, against the stereotype:

- Babu was included.
- Babu was early.
- Babu found **zero smart-quote contamination** across **147**
  markdown files.
- Babu found **zero BOMs, zero CRLF, zero mixed line endings.**
- Babu verified `InvariantGlobalization=true` is honestly shipped in
  the csproj and produces locale-stable output across `de_DE.UTF-8`,
  `ja_JP.UTF-8`, `ar_EG.UTF-8`, and `C`.
- Babu's only Highs are a *missing* `docs/i18n.md` and an *undeclared*
  `InvariantGlobalization` commitment.

The product is an internationalization good citizen. The documentation
just hasn't told anyone yet.

> **BABU:** "Jerry, next time — you call Babu first. Very good man
> today."

---

## 8. Action queue (SQL-ready)

The consolidation step will insert these as follow-up todos
(dependencies mirror the Act ordering). Abbreviated here; see individual
reports for detail.

| id | title | origin | est (hr) | blocks |
|---|---|---|---:|---|
| `v205-version-string-fix` | Roll stage.sh + Program.cs + Telemetry.cs `2.0.2` → `2.0.5` + CI gate | Lippman C-1/C-2 | 2 | v2.0.5 tag |
| `readme-matrix-truth` | Rewrite Install table + artifact filenames for v2.0.5 | Elaine C1+C2+C3 | 2 | |
| `arch-v2-rewrite` | Resolve v1/v2 security contradiction (ARCHITECTURE.md vs README) | Elaine H2+H3 | 1 | |
| `coc-private-channel` | CoC → private channel for harassment (1-line fix) | Elaine H5 | 0.25 | |
| `perf-numbers-refresh` | Purge 5.4 ms / 9 MB claims; cite 8.87 ms / 12.96 MB (or v2.0.5 re-baseline) | Bania C1 | 1 | v205-baseline |
| `bench-flag-vaporware` | Delete references to non-existent `bench.py`/`bench.sh` flags | Bania C2 | 0.5 | |
| `default-model-pick` | Pick default model, cascade everywhere, document reasoning | Morty C-1 + Maestro M5 | 2 | |
| `test-count-sync` | Correct 555+/538/541 → 1510 across README + tests/README + runbook + ADR-003 | Puddy H1 | 0.5 | |
| `release-runbook-v2` | Rewrite runbook for v2 reality (csproj path, 4-leg matrix, hash-sync, rerun lever) | Lippman C-3 | 2 | |
| `demo-scripts-v2` | Fix `az-ai` → `az-ai-v2` in all 3 demo scripts + hero GIF | Keith C1 | 1 | |
| `docs-accessibility` | `docs/accessibility.md` — ships 7-rule color contract + `--raw` + exit codes + keyboard-only | Mickey top-rec | 3 | closes 5 findings |
| `docs-i18n` | `docs/i18n.md` — InvariantGlobalization contract + USD-only statement + non-ASCII example | Babu H-02 | 3 | |
| `security-index` | `docs/security/index.md` linking every audit report + security review | Newman F-4 | 1 | |
| `macos-runner-triage` | `docs/runbooks/macos-runner-triage.md` (Jerry's overdue runbook) | Jerry M-3 | 2 | |
| `docs-prompts` | `docs/prompts/` + eval harness; move prompts out of C# string literals | Maestro H1+H2 | 8 | |
| `docs-testing-readme` | `docs/testing/README.md` + TDD/flaky/contract playbooks | Puddy C1 | 2 | |
| `security-refresh` | SECURITY.md last-updated, supported-versions, §12 DelegateTaskTool v2-rewrite, UnsafeReplaceSecrets docs | Newman F-1+F-2+F-5 | 2 | |
| `postmortem-stickers` | RESOLVED banners on 4 diagnostic files | Jerry L-1 | 0.5 | |
| `notice-v1-baseline-fix` | `NOTICE:30-157` stale version (2.1.0 → 2.9.0-beta.1) + gif attribution | Jackie M-*  | 1 | |
| `changelog-203-limbo` | Document v2.0.3 cancelled-release policy | Lippman M-* | 0.5 | |
| `implementation-plan-archive` | Archive or rewrite `IMPLEMENTATION_PLAN.md` (v1.9.0 stuck-in-amber) | Elaine H1 | 0.25 | |
| `cost-docs-patch` | Native cache + Batch API + FR-008 real ROI in `cost-optimization.md` §3+§5 | Morty H-* | 3 | |
| `v205-baseline` | Snapshot fresh v2.0.5 baseline, archive older benchmark reports | Bania | 2 | perf-numbers-refresh |
| `bench-harness-promote` | Promote `bench_harness.py` → `scripts/bench.py` (already bania-v2-03) | Bania | 4 | |
| `keith-wsl-talk` | Abstract + speaker bureau bio + rehearsed demo from WSL walkthrough | Keith top-rec | 4 | perf-numbers-refresh |
| `persona-safety-clause` | Bake SAFETY_CLAUSE into 5 Squad personas + document | Maestro H4 | 2 | |
| `temperature-cookbook` | One-table prompt/temperature guidance | Maestro H3 | 1 | |
| `make-test-both-trees` | `make test` / `make preflight` must run v1 + v2 projects | Puddy H-3 | 0.5 | |
| `emoji-alt-text` | 267 bare-emoji table cells need text labels (Babu + Mickey joint win) | Babu M-02 / Mickey M-* | 3 | |
| `docs-lint-ci` | Markdown lint in CI: bare-emoji rule + smart-quote rule + link-check | Jackie I- / Babu I-02 | 2 | |

**Total effort across the board:** ~58 engineer-hours, split 10 / 19 /
8 / 21 across Acts I–IV. Act I is a single sprint; Acts II–IV roll
through v2.1.0 / v2.2.0.

---

## 9. Credits

| # | Agent | Report | Commit |
|---|---|---|---|
| 1 | Elaine Benes | [docs-audit-2026-04-22-elaine.md](docs-audit-2026-04-22-elaine.md) | `50f8799` |
| 2 | Jerry (+ Costanza PM critique) | [docs-audit-2026-04-22-jerry.md](docs-audit-2026-04-22-jerry.md) | `fb6bec0` |
| 3 | Newman | [docs-audit-2026-04-22-newman.md](docs-audit-2026-04-22-newman.md) | `c6a01a0` |
| 4 | Babu Bhatt | [docs-audit-2026-04-22-babu.md](docs-audit-2026-04-22-babu.md) | `0aaa854` |
| 5 | Morty Seinfeld | [docs-audit-2026-04-22-morty.md](docs-audit-2026-04-22-morty.md) | `84e1807` |
| 6 | Kenny Bania | [docs-audit-2026-04-22-bania.md](docs-audit-2026-04-22-bania.md) | `532df2c` |
| 7 | Mickey Abbott | [docs-audit-2026-04-22-mickey.md](docs-audit-2026-04-22-mickey.md) | `83aba10` |
| 8 | Jackie Chiles | [docs-audit-2026-04-22-jackie.md](docs-audit-2026-04-22-jackie.md) | `0c347e9` |
| 9 | Keith Hernandez | [docs-audit-2026-04-22-keith.md](docs-audit-2026-04-22-keith.md) | `781efbb` |
| 10 | The Maestro | [docs-audit-2026-04-22-maestro.md](docs-audit-2026-04-22-maestro.md) | `e0aa770` |
| 11 | Puddy | [docs-audit-2026-04-22-puddy.md](docs-audit-2026-04-22-puddy.md) | `090608e` |
| 12 | Mr. Lippman | [docs-audit-2026-04-22-lippman.md](docs-audit-2026-04-22-lippman.md) | `68b3d92` |

**Bonus work, same pipeline:**

- Mr. Lippman also performed the `v2.0.4` hash-sync
  (`packaging/{homebrew,nix,scoop}` + CHANGELOG), commit `1884a8f`.
  This unblocks Bob Sacamano's tap/bucket publish.

---

## 10. Tag

> **JERRY** *(tying his tie, freshly audited)*: "So the docs were wrong
> the whole time."
>
> **GEORGE:** "See? I help."
>
> **JERRY:** "You pointed at the binary and said 2.0.2."
>
> **GEORGE:** "Yes."
>
> **JERRY:** "I could have read the binary."
>
> **GEORGE:** "But you didn't. That's the *value* of George."
>
> **BABU** *(off-camera):* "I was included. Very good man today."

*Roll credits.*

---

**Authored** 2026-04-22 by Elaine Benes (consolidation) with sworn
testimony from eleven other cast members. No findings were harmed in the
making of this episode.
