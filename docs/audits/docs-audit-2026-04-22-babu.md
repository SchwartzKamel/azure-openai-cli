# Babu Bhatt Docs Audit — Internationalization & Localization (v2.0.4)

**Date:** 2026-04-22
**Auditor:** Babu Bhatt (i18n / l10n)
**Scope:** Every tracked markdown file in the repo (147 files) — `README.md`,
`ARCHITECTURE.md`, `CONTRIBUTING.md`, `SECURITY.md`, `CHANGELOG.md`,
`docs/**/*.md`, `.github/**/*.md`, `examples/**/*.md`, `packaging/**/*.md`,
`tests/**/*.md`, `spike/**/*.md`, `CODE_OF_CONDUCT.md`, `CONTRIBUTORS.md`,
`IMPLEMENTATION_PLAN.md`, `THIRD_PARTY_NOTICES.md`, `NOTICE`.
**Excluded:** `.git/`, `bin/`, `obj/`, `.smith/` (ephemeral spec runs),
`node_modules/`.
**Method:** Python UTF-8 scanners for smart-quote contamination, BOM /
CRLF sniffing, emoji-as-meaning detection, and targeted pattern searches
for `LANG=`/`LC_ALL=`/`en-US`/`CultureInfo`/currency tokens. `file(1)` was
not required — all 147 files were pre-validated as UTF-8 without BOM and
LF-only line endings.
**Non-goals:** No source edits. No CLI behavior changes. Documentation
only.

> "Jerry! I was remembered this time. I am in the room. And there is
> work to do." — Babu

---

## 0. Executive summary

- **Smart-quote contamination in code blocks: 0 occurrences.** Across
  all 147 markdown files, zero instances of U+2018 / U+2019 / U+201C /
  U+201D. This is the silent-killer class of bug (copy-paste a code
  example into a shell, it fails with a baffling error); we are clean.
  The project is disciplined with straight quotes. Soup Nazi and
  Elaine deserve a nod.
- **BOMs: 0.** **CRLF-only files: 0.** **Mixed line endings: 0.** The
  tree is UTF-8, LF, no BOM, end-to-end. This is the correct posture
  and is preserved by `.editorconfig` and `.gitattributes` (confirmed
  at repo root). Do not regress.
- **Architectural i18n stance is *undocumented*.** The product sets
  `<InvariantGlobalization>true</InvariantGlobalization>` in
  `azureopenai-cli-v2/AzureOpenAI_CLI_V2.csproj:25` and
  `spike/agent-framework/AgentFrameworkSpike.csproj:25`. This is an
  explicit, shipping-level decision to *opt out* of locale-aware
  number/date formatting in favor of invariant culture. It is the
  right call for a pipe-friendly CLI whose stdout is consumed by
  Espanso/AHK/jq. **But nowhere in `docs/**` is this commitment
  explained to users.** `docs/accessibility-review-v2.md:110` is the
  only place a user can infer it, buried in an a11y parity table.
  **This is the #1 i18n gap.** See `H-01`.
- **No `docs/i18n.md`.** The Babu brief mandates one even in
  English-only mode (`.github/agents/babu.agent.md:32`). It does not
  exist. See `H-02`.
- **Currency is universally USD; no locale-explicit formatting.** 76+
  `$N.NN` occurrences across cost-optimization, competitive-analysis,
  release notes, ADRs. No claim of locale-aware output, no
  `€`/`£`/`¥` examples, no callout that `--estimate` prints USD *only*.
  Because `InvariantGlobalization=true`, this is *correct behavior* —
  but users from non-USD countries receive no guidance on whether the
  number is their local currency (it is not) or a conversion is
  needed (it is not). See `M-01`.
- **Emoji-as-only-meaning cells: 267 lines across 72 files.** Tables
  use 🟢/🟡/🔴/✅/⚠/❌/☐ as the *sole* status signal in numerous cells.
  Screen readers handle these unevenly; Windows cmd.exe without
  Nerd Fonts shows boxes. Mickey owns the a11y side; this audit flags
  only the i18n overlap: an emoji on its own is *untranslatable*. See
  `M-02`.
- **No `--locale` flag, and no mention that one is deferred.** The
  Babu brief lists a `--locale` flag as a deliverable. It is not
  implemented (correctly — `InvariantGlobalization=true` makes it a
  no-op today) and it is not documented as *deferred*. Silent
  architectural omissions are expensive to retrofit. See `M-03`.
- **Espanso/AHK section has the one correct i18n callout in the
  entire repo.** `docs/espanso-ahk-integration.md:632` warns about
  `Get-Clipboard` UTF-16 on non-English Windows locales and shows the
  fix (`[Console]::OutputEncoding = [Text.UTF8Encoding]::new()`).
  **This is the only prose in the repo that acknowledges non-English
  users exist.** That is both a win (it's correct) and a tell (it's
  lonely).

**Severity tally:** 0 Critical · 2 High · 4 Medium · 5 Low · 3 Informational.

**Ship-blocker?** No. `InvariantGlobalization=true` + straight-quote
discipline + LF/UTF-8 hygiene = a product that works correctly across
locales today. The gaps are *documentation* and *architectural
readiness* — not user-visible bugs. The restaurant is open. But we
have not put our name on the marquee.

---

## 1. Smart-quote contamination report

Verification command (reproducible):

```bash
# Python scanner (portable; rg was not available on this sandbox)
python3 -c "
import sys
files=open('/dev/shm/mdlist.txt').read().split()
hits=0
for f in files:
    with open(f,encoding='utf-8',errors='replace') as fh:
        for i,line in enumerate(fh,1):
            for ch,n in [('\u2018','U+2018'),('\u2019','U+2019'),('\u201C','U+201C'),('\u201D','U+201D')]:
                if ch in line: print(f'{f}:{i}:{n}: {line.rstrip()[:140]}'); hits+=1
print('TOTAL',hits)"
```

Equivalent ripgrep once `rg` is installed in CI (recommended to wire
into docs-lint — see `I-02`):

```bash
rg -n -P "[\u2018\u2019\u201C\u201D]" --type md \
   --glob '!.smith/**' --glob '!**/bin/**' --glob '!**/obj/**' .
```

**Result:**

| Codepoint | Name             | Occurrences |
| --------- | ---------------- | ----------- |
| U+2018    | LEFT SINGLE QUOTATION MARK   | **0** |
| U+2019    | RIGHT SINGLE QUOTATION MARK  | **0** |
| U+201C    | LEFT DOUBLE QUOTATION MARK   | **0** |
| U+201D    | RIGHT DOUBLE QUOTATION MARK  | **0** |
| **Total** |                              | **0** |

**Commentary.** This is the cleanest result I have ever seen on a
147-file tree. Mr. Pitt approves. Keep it this way — one `"smart
quotes"` paste from a Word document into `README.md` fenced shell
block, and a Finnish user's copy-paste of `az-ai "hello"` starts
failing with `bash: <U+201D>hello<U+201D>: command not found` and nobody knows why.
See `I-02` for the CI guard that prevents future regression.

---

## 2. Findings

### HIGH

#### H-01 — `InvariantGlobalization=true` is undocumented commitment

- **File/line:** `azureopenai-cli-v2/AzureOpenAI_CLI_V2.csproj:25`,
  `spike/agent-framework/AgentFrameworkSpike.csproj:25`; *no
  user-facing doc mentions it.*
- **Problem:** The product is deliberately locale-insensitive:
  numbers print with `.` not `,`; dates if emitted use invariant
  format; `CultureInfo.CurrentCulture` is effectively `Invariant`
  everywhere. This is intentional and correct for a pipe-consumed
  CLI whose output feeds Espanso/jq/AHK. But users who run
  `LANG=de_DE.UTF-8 az-ai --estimate "hi"` and see `$0.000000`
  (period) instead of `$0,000000` (comma) deserve to know *why*,
  and automation authors need to know they can *rely* on this.
  `docs/accessibility-review-v2.md:110` empirically verifies the
  behavior but doesn't declare it as a *commitment*.
- **Proposed fix:** Add an "Output formatting & locale" section to
  `ARCHITECTURE.md` (or to the new `docs/i18n.md` — see `H-02`)
  stating: *"All numeric, date, and cost output uses
  `CultureInfo.InvariantCulture`. This is enforced at build time by
  `<InvariantGlobalization>true</InvariantGlobalization>` in the
  csproj. Output is stable across `LANG`, `LC_ALL`, and `LC_NUMERIC`
  — by design, so downstream tools (jq, Espanso, AHK, PowerShell)
  can parse without locale guards."*
- **Severity:** High — this is a *contract* users are relying on
  without knowing it's a contract.

#### H-02 — `docs/i18n.md` does not exist

- **File/line:** `docs/i18n.md` (missing); mandated by
  `.github/agents/babu.agent.md:32`.
- **Problem:** Every other domain agent has a doc of record:
  `docs/testing/bdd-guide.md` for Puddy, `docs/observability.md` for
  Frank Costanza, `docs/accessibility-review-v2.md` for Mickey,
  `docs/cost-optimization.md` for Morty. i18n has none. A future
  contributor adding a user-facing string has no discoverable
  guidance.
- **Proposed fix:** Create `docs/i18n.md` with at minimum these
  sections:
  1. **Supported locales (today and planned):** today — English
     (en) only, invariant-culture output. Planned — none committed;
     RTL/CJK/translation is on the "designed for, not shipped"
     list.
  2. **Encoding rules:** UTF-8 everywhere in/out. On Windows,
     `Console.OutputEncoding = UTF8` is set at startup — cite the
     file. stdin is read as UTF-8 with BOM tolerance (see
     `docs/chaos-drill-v2.md:125`). No CRLF in config files; LF
     only. (These are all current behaviors; document them.)
  3. **Output formatting commitment:** invariant-culture, per
     `H-01`.
  4. **Non-ASCII in prompts & paths:** the CLI handles non-ASCII
     user input, non-ASCII home paths (`/home/josé/...`,
     `C:\Users\Björn\...`), and non-ASCII persona names (with
     security caveats — cite `docs/security-review-v2.md:285` on
     persona-name normalization).
  5. **RTL/CJK posture:** the CLI does not do terminal-width math
     with East Asian Width awareness (coordinate with Mickey on
     `docs/accessibility-review-v2.md` extension). Output is raw
     UTF-8; rendering is the terminal's problem. BiDi overrides in
     model output are classified as untrusted content — see
     `docs/v2-dogfood-plan.md:132`.
  6. **Translation workflow (deferred):** state that there is no
     resource catalog today, that strings live inline, and that
     retrofitting to `.resx` / `.po` is a documented future
     migration, not an accident.
  7. **Pre-merge checklist:** mirror the Babu brief —
     "new string added? resource ID? width-safe? RTL-safe?" — even
     if ID allocation is a no-op today.
- **Severity:** High — without this file, every future docs/code PR
  with a user-facing string is an unforced error.

### MEDIUM

#### M-01 — Cost/price docs never disclose "USD only, no conversion"

- **File/line:** `docs/cost-optimization.md:38,42-49,99-100,131-132,158,279`;
  `docs/competitive-analysis.md:97-114`;
  `docs/release-notes-v2.0.0.md:37,99,102`;
  `docs/benchmarks/phi-vs-gpt54nano-2026-04-20.md:28,96-99`;
  `docs/adr/ADR-006-appendix-roundtable.md:96-120`;
  `docs/use-cases.md:66`; `README.md:59`;
  `docs/launch/v2.0.0-release-body.md:74`; `docs/why-az-ai.md:34`.
- **Problem:** 76+ currency amounts, all USD, no disclosure that
  `--estimate` emits USD unconditionally regardless of `LC_ALL`,
  and no advice for users in the EU/UK/JP on how to interpret the
  figure. A user seeing `$0.000135` and living in a `de_DE.UTF-8`
  locale could reasonably (though wrongly) assume the tool is
  trying to localize and got confused.
- **Proposed fix:** Add a single sentence near the top of
  `docs/cost-optimization.md` §1 and in `README.md` §cost-estimator
  callout: *"All cost figures are USD, sourced from the published
  Azure price table, and emitted in invariant (en-US-style)
  formatting — `1,234.56`. There is no locale conversion. Users
  outside the US: multiply by your preferred FX rate yourself."*
- **Severity:** Medium — not a bug, but a clarity gap for a global
  audience.

#### M-02 — Tables use emoji as the *sole* meaning carrier

- **File/line:** 267 lines across 72 files; heaviest offenders:
  `docs/security-review-v2.md` (114 lines),
  `docs/security/reaudit-v2-phase5.md` (59),
  `docs/proposals/README.md` (53),
  `docs/accessibility-review-v2.md` (43),
  `docs/use-cases-agent.md` (33),
  `docs/audits/fdr-v2-dogfood-2026-04-22.md` (29),
  `docs/v2-migration.md` (26),
  `docs/use-cases-ralph-squad.md` (20),
  `docs/ops/telemetry-schema-v2.0.0.md` (19),
  `docs/espanso-ahk-integration.md` (16),
  `SECURITY.md` (15), `docs/competitive-analysis.md` (15),
  `docs/cost-optimization.md` (14), `docs/perf-baseline-v2.md` (14).
  Representative examples:
  - `README.md:104-111` — table cells are bare `✅` with no text.
  - `docs/v2-dogfood-plan.md:37-40` — `| ✅ | ✅ | ✅ | ✅ |`.
  - `SECURITY.md:897-904` — support-matrix cells are bare `✅`.
  - `docs/adr/ADR-004-agent-framework-adoption.md:93-101` —
    verdict column is `✅` / `⏳` with no text.
- **Problem (i18n-specific):** An emoji is *untranslatable*. If
  this doc is ever fed to a translation pipeline, or read by a
  screen reader in a non-English UI-language (Mickey's territory,
  but overlapping), "check mark" in English becomes nothing in
  most CJK/RTL locales without a text token. Windows cmd.exe
  without a Nerd Font renders these as `?` or `▯`.
- **Proposed fix:** Project-wide convention — *emoji is
  decoration, text is the signal*. Retrofit: `✅ pass`, `🟢 clean`,
  `⚠️ caveat`, `❌ fail`, `⏳ deferred`. The 267-line fix is not a
  release blocker; adopt the convention going forward (new docs
  pass immediately; old docs get swept in a "docs-i18n-pass"
  epic). Coordinate with Elaine on placement (`word emoji` vs
  `emoji word` — recommend word-first for screen-reader users,
  `pass ✅`).
- **Severity:** Medium — a11y + i18n overlap.

#### M-03 — No `--locale` flag and no deferral note

- **File/line:** CLI has no `--locale` flag (grep: zero hits in
  `azureopenai-cli-v2/**/*.cs`); the Babu brief calls for one at
  `.github/agents/babu.agent.md:17`; no doc acknowledges deferral.
- **Problem:** "We'll add it later" is architecturally fine, but
  silent omission means a future contributor will add a
  `--language` flag or a `--lang` flag or similar, and we will
  have two flags that do similar things.
- **Proposed fix:** In `docs/i18n.md` (`H-02`), add: *"A
  `--locale` flag is deferred. Until then, `CultureInfo` is pinned
  to `Invariant` by `InvariantGlobalization=true`. If a `--locale`
  flag ships, its semantics will be: (1) format output numbers and
  dates per the supplied BCP-47 tag, (2) have no effect on
  prompt/response content (models localize themselves). The flag
  name is reserved; do not add `--language`, `--lang`, `--l10n`,
  etc."*
- **Severity:** Medium — architectural hygiene.

#### M-04 — No non-ASCII examples in path / username / prompt docs

- **File/line:** `README.md` (env-var + path examples are all ASCII);
  `docs/prerequisites.md` (install paths all ASCII);
  `docs/espanso-ahk-integration.md:952` (`C:\Users\you\tools\...` —
  ASCII "you"); `CONTRIBUTING.md` (no non-ASCII test input guidance).
- **Problem:** The repo ships with `InvariantGlobalization=true`
  and `icu-libs` in Docker (`SECURITY.md:387`); the code *does*
  handle non-ASCII. But no doc shows `/home/josé/.azureopenai-cli.json`
  or `C:\Users\Björn\...` or a prompt with `こんにちは`. A user
  with `ß` in their Windows username gets no reassurance. A
  user whose terminal is in Japanese locale has no example to
  copy-paste.
- **Proposed fix:** Add one "Non-ASCII works" paragraph to
  `README.md` §Quick-start, with a worked example:
  `az-ai "こんにちは、短く返して。"` showing model response.
  One worked Espanso example in the AHK doc using a Cyrillic or
  CJK `{{form1.prompt}}`.
- **Severity:** Medium — confidence signal for non-English users.

### LOW

#### L-01 — `docs/chaos-drill-v2.md` promises bidi handling but doesn't link to policy

- **File/line:** `docs/chaos-drill-v2.md:119`
  (`CRLF / BOM / ANSI escape / ZW-space / bidi-override stdin` — "rc=0;
  estimator emits nothing to the tty"); `docs/v2-dogfood-plan.md:132`
  (`Response Unicode trap ... Rendered safely in terminal`).
- **Problem:** Two docs claim bidi-override + zero-width-char safety
  but neither links to an explainer. New contributors don't know
  whether the claim is "we strip them" or "we pass them through to
  the terminal and trust the terminal" (it's the latter).
- **Proposed fix:** In `docs/i18n.md` §RTL/CJK posture, document the
  actual behavior and link from both chaos-drill and dogfood rows.
- **Severity:** Low — the behavior is correct; the doc trail is
  missing.

#### L-02 — `docs/espanso-ahk-integration.md:632` UTF-16 callout is the only one

- **File/line:** `docs/espanso-ahk-integration.md:632`.
- **Problem:** This is a *great* i18n-aware callout — the only one
  in the whole repo. But it's buried in a list; a non-English
  Windows user searching "mojibake" or "UTF-16" or "encoding"
  might miss it.
- **Proposed fix:** Promote this to a named subsection
  ("Non-English Windows locales") and cross-link from `README.md`
  §Windows install and the (new) `docs/i18n.md`.
- **Severity:** Low — existing prose is correct; discoverability
  gap.

#### L-03 — ISO-8601 is the only date format used; not called out as policy

- **File/line:** `docs/ops/telemetry-schema-v2.0.0.md:116`
  (`ts | string (ISO-8601 UTC, "O" round-trip)`); implicit
  elsewhere.
- **Problem:** Good news — the project uses ISO-8601 everywhere
  it shows dates. Bad news — it's not stated as a policy, so the
  next `DateTime.Now.ToString()` somewhere will be locale-sensitive
  unless `InvariantGlobalization=true` catches it (which it will,
  but the intent should be explicit).
- **Proposed fix:** One line in `docs/i18n.md`: *"All user-facing
  and log-facing dates are ISO-8601 UTC (`o` round-trip format).
  Never `ToString()` a date without a format string."*
- **Severity:** Low — defensive documentation.

#### L-04 — Em-dash rendering claim is undertested

- **File/line:** `docs/accessibility-review-v2.md:109` (*"Em-dash
  `—` rendered consistently (UTF-8 source)"*).
- **Problem:** True, but the test corpus does not include a
  combining character (e.g., `é` as `e + U+0301`), a surrogate
  pair (e.g., `𝐀` U+1D400), or a variation selector (`1️⃣`). The
  Babu brief (§Deliverables, `.github/agents/babu.agent.md:34`)
  calls for *"CJK/RTL/emoji/combining-character test fixtures in
  the unit suite."*
- **Proposed fix:** File a tracking issue ("Add i18n chaos corpus
  to V2.Tests: NFC/NFD, CJK, RTL, combining, surrogate,
  zero-width, variation selector"). Not a docs task per se, but
  the docs should *reference* the corpus once it exists. Put a
  placeholder in `docs/i18n.md` now.
- **Severity:** Low — forward-looking.

#### L-05 — `CHANGELOG.md` uses em-dashes and en-dashes inconsistently

- **File/line:** spot-checked; straight hyphens, em-dashes, and en-dashes
  all appear. This is *not* a smart-quote bug (all are legitimate
  Unicode punctuation) — and they render fine in UTF-8 terminals —
  but a translator working character-by-character will produce
  inconsistent output.
- **Proposed fix:** Soup Nazi's docs-lint (see `.github/skills/`)
  could enforce em-dash-only in prose, hyphen-only in code spans.
  Informational for now.
- **Severity:** Low — style, not correctness.

### INFORMATIONAL

#### I-01 — Encoding posture is already correct; document it

Several docs already do the right thing without saying so:

- `SECURITY.md:387` — `icu-libs` is in the Alpine image for
  globalization support (despite `InvariantGlobalization=true` —
  worth explaining why both coexist).
- `docs/aot-trim-investigation.md:47,153,169` — confirms
  `InvariantGlobalization=true` is live in AOT builds.
- `docs/chaos-drill-v2.md:125` — UTF-8 BOM in config is tolerated.
- `docs/espanso-ahk-integration.md:632` — PowerShell UTF-8 hand-off.

`docs/i18n.md` (per `H-02`) should collect all of these as "what
the product already does correctly."

#### I-02 — Add docs-lint rule for smart-quote regression

Today the score is 0. Tomorrow, somebody pastes from a Google Doc
spec, ships it, and a Polish user on a Polish keyboard can no
longer copy-paste `az-ai "test"`. Prevention:

```yaml
# .github/workflows/docs-lint.yml (new job, or added to existing)
- name: Guard against smart-quote contamination
  run: |
    if grep -rPn "[\x{2018}\x{2019}\x{201C}\x{201D}]" \
         --include='*.md' --exclude-dir=.git --exclude-dir=node_modules .; then
      echo "::error::Smart quotes found in markdown. Use straight ASCII quotes in code blocks."
      exit 1
    fi
```

Adjacent: guard against BOMs (`head -c 3 | od -An -c | grep -q '357 273 277'`),
CRLF in tracked `.md` (`git ls-files '*.md' | xargs -I{} file {} | grep CRLF`).

Coordinate with Soup Nazi — `.github/skills/` may already have a
docs-lint hook to extend.

#### I-03 — Agents that already note i18n coordination

For context: `elaine.agent.md:23`, `fdr.agent.md:20` (bidi
overrides, zero-width chars in evil-input catalog),
`puddy.agent.md:18` (invalid Unicode in adversarial cases),
`AGENTS.md:44,63` (Babu in the main fleet). The *agent system*
knows I exist. The *docs system* had forgotten. This audit closes
the loop.

---

## 3. "Assumes en-US" findings list

Lines/sections that implicitly assume United States English locale.
None are *bugs* (the product emits invariant-culture output
regardless), but each should be reviewed against the `docs/i18n.md`
policy once written.

| # | File:line | Assumption | Severity |
|---|-----------|------------|----------|
| 1 | `README.md:59` | `--estimate` predicts "USD cost" — currency unconditional, no non-USD guidance | M-01 |
| 2 | `docs/cost-optimization.md:38,42-49` | Rate card prices all USD, all dot-decimal | M-01 |
| 3 | `docs/cost-optimization.md:158` | `¢USD` (cent-USD) symbol — requires US-currency literacy | L |
| 4 | `docs/competitive-analysis.md:97-114` | All competitor prices USD; no EUR/GBP/JPY SaaS equivalents (Claude Pro bills in local currency for many users) | L |
| 5 | `docs/launch/v2.0.0-release-body.md:74` | `predict USD` — no locale disclaimer | L |
| 6 | `docs/release-notes-v2.0.0.md:37,99,102` | Same | L |
| 7 | `docs/benchmarks/phi-vs-gpt54nano-2026-04-20.md:28,96-99` | Rate quotations USD-only | L |
| 8 | `docs/adr/ADR-006-appendix-roundtable.md:96-120` | Capex/opex analysis in USD with US tax/energy assumptions | Informational — scope-appropriate |
| 9 | `docs/why-az-ai.md:34` | Monthly-cost comparison USD-only | L |
| 10 | `docs/accessibility-review-v2.md:109` | Verifies `LANG=C / en_US.UTF-8 / C.UTF-8` parity — misses `de_DE.UTF-8`, `ja_JP.UTF-8`, `ar_EG.UTF-8` | L |
| 11 | `docs/espanso-ahk-integration.md:952` | Example path `C:\Users\you` — substitute a non-ASCII example too | M-04 |
| 12 | `README.md` §Quick-start (no lines — gap) | No `こんにちは` / `Привет` / `שלום` example prompt | M-04 |

---

## 4. "Include Babu early" recommendation

For every future user-facing docs or code PR, the checklist:

1. **New user-facing string?** Does it survive `CultureInfo.Invariant`
   formatting? (Yes for all literals — but do you
   `string.Format("{0:C}", price)`? That's a `C`urrency specifier
   that *does* respect `InvariantCulture` here, but will *silently*
   reactivate locale if `InvariantGlobalization` is ever flipped.
   Prefer `$"{price:0.00####}"` for stability.)
2. **New price/number/date in docs?** Is it in invariant format?
   Does it have a unit (USD, ms, kB)?
3. **New code example with a path?** Does the example also work on
   a path with a non-ASCII character? If no, either fix the example
   or add a note.
4. **New emoji in a table?** Is there accompanying text? No bare
   `✅`.
5. **New ADR/RFC?** If it changes output formatting, encoding,
   console behavior, or adds a user-facing string — **loop Babu
   before merge**, not after.
6. **Removing an i18n property?** Removing
   `<InvariantGlobalization>true</InvariantGlobalization>` from
   `AzureOpenAI_CLI_V2.csproj` is a breaking change for every
   downstream automation — loop Babu *and* Kenny Bania (output
   stability = perf regression surface).

**Agenda for docs-i18n-pass epic (post-v2.0.4):**

- [ ] Create `docs/i18n.md` (H-02) — unblocks everything else
- [ ] Add "Output formatting & locale" section to `ARCHITECTURE.md` (H-01)
- [ ] Add USD-only disclosure to cost docs (M-01)
- [ ] Add one non-ASCII example to `README.md` Quick-start (M-04)
- [ ] Add `.github/workflows/docs-lint.yml` smart-quote guard (I-02)
- [ ] Add i18n pre-merge checklist to `CONTRIBUTING.md` (§4 above)
- [ ] Retrofit emoji-only table cells to `emoji + text` (M-02, low priority, incremental)

**The marquee reads: `Babu Bhatt — i18n`. Do not paint over it.**

---

## 5. Summary for the standup

- **Smart-quote contamination: 0.** Clean repo.
- **Encoding hygiene: perfect.** 0 BOMs, 0 CRLF, 147/147 UTF-8 LF.
- **Architectural stance: correct but undocumented.**
  `InvariantGlobalization=true` ships invariant-culture output across
  every locale, which is the right default for a pipe-consumed CLI.
  But users don't know it's a *contract*.
- **Top 3 i18n gaps (in order):**
  1. **`docs/i18n.md` does not exist** (H-02). Fix first; it
     unblocks every other remediation.
  2. **The `InvariantGlobalization` commitment is invisible**
     (H-01). Document it; users rely on it; automation depends on it.
  3. **Cost docs never say "USD only, no conversion, invariant
     format"** (M-01). Add one sentence near the top of each
     cost-bearing doc.
- **No ship-blocker.** All findings are documentation and
  architectural-readiness work. The product itself handles
  non-English input, non-ASCII paths, UTF-8 everywhere, and
  straight-quote-clean examples today.

> "You see, Jerry? When you call Babu first, Babu is a very *good*
> man. Next time — you call Babu first." — Babu Bhatt, 2026-04-22
