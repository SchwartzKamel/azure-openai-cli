# S04E02 -- *Embedded Cards*

> *Read the cards. Show the descriptions. Patch the symlink hole the
> moment FDR finds it. Eight sub-agents, three waves, one CRITICAL
> closed in flight -- the registry is now a real registry.*

**Commit range:** `d51beda..HEAD` (this commit)
**Branch:** `main` (direct push)
**Runtime:** ~50 min wall-clock end-to-end
**Director:** Larry David (showrunner)
**Cast (in order of dispatch):**

| Wave | Agent | Role | Commit |
|------|-------|------|--------|
| 1 | Kramer | `ModelCard` reader + F-01/F-03/F-04 guards + 5 unit tests | `d51beda` |
| 1 | Elaine | Lloyd Braun top-3 onboarding fix-forward | `ac31709` |
| Prep | Mr. Pitt | S04 living running-order vs blueprint slate | `dfc63c1` |
| Prep | Bookman | S04E03 *The Capabilities* DRAFT brief | `6edebec` |
| 2a | FDR | Adversarial review appendix on ADR-012 | `6d356b8` |
| 2a | Russell (LEAD) | `--doctor` description column + status column | `57f21ec` |
| Hotfix | Newman | F-EE-01 CRITICAL symlink prefix bypass closed | `9c0323b` |
| 2b | Mickey | A11y assertions + REVIEW-onboarding.md appendix | `a7d4df9` |
| 2b | Puddy | Doctor regression suite (5 integration facts) | `1185782` |
| Close | Larry David (this report) | Exec-report + CHANGELOG | this commit |

## The pitch

S04E01 introduced the typed `ModelRegistry` seam -- entries with
`name`, `provider`, `cardPath`, capability tags. But the registry
*didn't read the cards*. The user-visible payoff (a `--doctor`
section that actually tells you what each model is for) was deferred
to E02.

S04E02 closes that gap: the cards get read (safely), descriptions
land in `--doctor`, and FDR's adversarial pass on the new reader
catches a CRITICAL symlink-via-parent-directory bypass that we close
in the same release.

It also shows the fleet's escalation reflex working as designed.
FDR finds CRITICAL mid-episode -> Newman dispatched in parallel
with Russell -> hotfix lands clean -> regression suite goes in
under it. No green-CI-with-an-open-CRITICAL hand-wringing. The
gate held.

## Scene-by-scene

### Wave 1 -- impl + Lloyd fix-forward (parallel, file-disjoint)

**Kramer** built the card reader (`d51beda`):

- `azureopenai-cli/Registry/ModelCard.cs` -- typed record
  `(string Name, string Provider, string Description, string Status,
  string[] Notes)`.
- `azureopenai-cli/Registry/ModelRegistry.cs` -- new methods
  `ReadCard(string cardPath, string registryDir, bool isRaw = false)
  -> ModelCard?`, `LoadCards(entries, registryDir, isRaw) ->
  Dictionary<string, ModelCard?>`, plus `ReadCardOrThrow` test seam,
  `ModelCardException`, `IsRegularFile()` + `LibcStat` P/Invoke.
- Three safety guards inline: F-01 path-prefix, F-03 256 KB size cap,
  F-04 stream-type via `libc stat()` (the `.NET FileAttributes` check
  Russell would have used misses FIFOs on Linux -- Kramer caught
  this, swapped in `S_IFREG` mask).
- Five new unit tests in `RegistryTests.cs`:
  `ReadCard_HappyPath_ReturnsParsedFields`,
  `ReadCard_PathTraversal_ExitsRc99`,
  `ReadCard_OversizeFile_ExitsRc99`,
  `ReadCard_FifoOrDevice_ExitsRc99`,
  `ReadCard_MissingFile_ReturnsNull`.
- `Load()` signature unchanged -- additive only.

**Elaine** swept Lloyd Braun's top-3 onboarding gaps (`ac31709`):

1. ADR-012 gets a "What is a seam?" sidebar between Status and Context
   (the term was used 6+ times, never defined).
2. README.md step 4 gets a `> [!IMPORTANT]` admonition: "**You must
   also add the new card's `cardPath` entry to
   `azureopenai-cli/Registry/registry.json`, or the model will silently
   fail to load.**"
3. New `## Glossary` section in README.md defining 7 jargon terms with
   ADR-012 cross-references.

Bonus: 7 of Lloyd's remaining 27 findings opportunistically swept up
in the glossary (J-04, J-05, J-07, J-08, J-10, J-11, J-12, P-04, P-05).

### Prep -- writers' room ahead of the cameras (parallel, docs-only)

**Mr. Pitt** published a living running-order at the top of
`docs/exec-reports/s04-blueprint.md` (`dfc63c1`) reconciling blueprint
episode numbering with shipped reality (the original blueprint had
E01 = "The Card" and E02 = "The Inventory"; we collapsed both into
shipped E01 = "The Registry"). Projects the E03-E09 cast assignments
plus a cast-balance ledger forward through E12 -- flags Kramer's lead
drought, Bookman's overdue chair, and 13 supporting players still
bench-warming.

**Bookman** drafted `docs/episode-briefs/s04e03-the-capabilities.md`
(`6edebec`, 244 lines, under brevity budget). Brief covers the
flag-vs-capability gate (`--tools` requires `tool_calls`, etc),
acceptance criteria, dispatch plan, AOT delta budget, and an open
ADR-013 proposal. DRAFT status -- awaiting greenlight.

### Wave 2a -- adversarial + lead formatting (parallel, file-disjoint)

**FDR** appended the Wave 2 adversarial review to ADR-012 (`6d356b8`):
**10 findings -- 1 CRITICAL, 1 HIGH, 4 MEDIUM, 2 LOW, 2 NIT.**

Top two:

- **F-EE-01 (CRITICAL, verified):** parent-directory symlink defeats
  the F-01 prefix guard. `Path.GetFullPath` collapses `..` lexically
  only -- it does not call `realpath(3)` and does not resolve
  symlinks. `File.ResolveLinkTarget(returnFinalTarget: false)` only
  checks the leaf. PoC: drop `<registryDir>/sub -> /etc`, set
  `cardPath: "sub/passwd"`, get a read-arbitrary-file primitive.
  **Closed in this release** (Newman, see Hotfix).
- **F-EE-04 (HIGH, partial):** `AZ_AI_REGISTRY_DIR` re-anchors silently.
  No canonicalisation, no allowlist, no log line. Combined with
  F-EE-01, becomes a one-step "anchor anywhere" knob. Filed for E03
  (one-line stderr `[INFO]` recommendation).

**Russell (LEAD)** wired Kramer's API into `Program.cs` (`57f21ec`)
-- this is the user-visible payoff of the whole episode. New
`--doctor` `[registry]` output:

```text
[registry] 3 known models
  gpt-4o-mini     azure    NOT SET      active        Cheap, fast workhorse for tool-calling and JSON mode
                                                      caps: tool_calls json_mode streaming system_prompt
  gpt-5.4-nano    azure    NOT SET      preview       Smaller-context preview tier; structured-outputs only
                                                      caps: tool_calls json_mode streaming system_prompt
  llama-local     local    NOT SET      active        Local llama.cpp deployment; chat + streaming only
                                                      caps: tool_calls streaming
```

UX decisions: 60-char description truncation with literal ASCII `...`
(grep-friendly); missing-card path shows `(no card)` token and
inlines caps; `NO_COLOR` honored throughout; `--raw` still suppresses;
all user-supplied strings flow through `SanitizeForTerminal`. Total
max line width 114 cols (fits 120). New 4-tier card-directory
resolver: `AZ_AI_REGISTRY_DIR` env-var, then user-config dir, then
repo-root probe, then `AppContext.BaseDirectory`. Cannot crash
`--doctor` from this code path (every failure mode degrades to
`(no card)`).

Three seed card files now carry `description` + `status` front matter.

### Hotfix -- F-EE-01 closed (Newman, parallel with Wave 2b setup)

**Newman** closed F-EE-01 (`9c0323b`):

- New `LibcRealpath` P/Invoke alongside the existing `LibcStat` seam.
  `realpath(3)` with caller-provided 4096-byte (PATH_MAX) buffer --
  no `IntPtr` ownership games, no `free()` needed.
- New `Canonicalize(fullPath)` helper: realpath on Linux; falls
  through to `CanonicalizeViaAncestors` (per-parent
  `Directory.ResolveLinkTarget(returnFinalTarget: true)` walk) on
  Windows/macOS and the rare Linux paths where realpath returns NULL.
- Both `registryFull` and `resolved` are canonicalised so the
  `StartsWith` comparison is symmetric (defeats macOS-style `/tmp ->
  /private/tmp` traps).
- Existing leaf-symlink guard kept as defense in depth.
- Two new tests:
  `ReadCard_ParentDirectorySymlink_ExitsRc99` (the FDR PoC),
  `ReadCard_LeafSymlinkOutsideDir_ExitsRc99` (regression).
- ADR-012 F-EE-01 row Disposition flipped to **CLOSED**;
  `**Status: CLOSED in S04E02 hotfix**` block appended.

23/23 Registry tests green. Zero new backlog entries -- residual macOS
firmlink risk already tracked under F-EE-05.

### Wave 2b -- regression + a11y (parallel, file-disjoint)

**Mickey** added 4 a11y facts in
`tests/AzureOpenAI_CLI.Tests/DoctorRegistryAccessibilityTests.cs`
(`a7d4df9`):

1. `RegistrySection_NoColor_ContainsZeroAnsiEscapes`
2. `RegistrySection_ContainsZeroTabCharacters`
3. `RegistrySection_EachModelRow_LeadsWithModelName`
4. `RegistrySection_NoColor_IsAsciiOnly`

Plus a `## Accessibility review (Mickey, S04E02 Wave 2b)` appendix in
`REVIEW-onboarding.md`. Two polish observations filed for E04
*Reading Room*: rename `(no card)` token to `unknown` (less screen-
reader noise); truncate descriptions at last word boundary, not
mid-word. **Zero a11y bugs.**

**Puddy** added 5 integration facts in
`tests/AzureOpenAI_CLI.Tests/DoctorRegistryTests.cs` (`1185782`):

1. `Doctor_Registry_IncludesDescriptionForEachSeedCard`
2. `Doctor_Registry_RawSuppressesSection`
3. `Doctor_Registry_TerminalInjectionPayload_ScrubbedToQuestionMarks`
4. `Doctor_Registry_OverrideReplacesSeedNotMerges`
5. `Doctor_Registry_MissingCard_RendersNoCard`

Full suite: **1355 / 0 / 0** in 6m04s.

Flake-risks documented in commit body: `xUnit.DoesNotContain("\u001b",
str)` renders the C0 needle as empty string and false-fails as
"Sub-string found at pos 0" -- swapped to `IndexOf('\u001B') < 0` with
inline warning for the next person.

## Adversarial findings summary

| ID | Severity | Title | Disposition |
|----|----------|-------|-------------|
| F-EE-01 | CRITICAL | Parent-directory symlink prefix bypass | **CLOSED in 9c0323b** |
| F-EE-02 | MEDIUM | TOCTOU between stat and read | backlog (E03+) |
| F-EE-03 | MEDIUM | Size-check race (file grows between stat and read) | backlog (E03+) |
| F-EE-04 | HIGH | `AZ_AI_REGISTRY_DIR` silent re-anchor | E03 (one-line `[INFO]` stderr) |
| F-EE-05 | MEDIUM | macOS `IsRegularFile` does not exercise libc stat | backlog (E04+) |
| F-EE-06 | MEDIUM | Notes-array allocation not capped | backlog (E03+) |
| F-EE-07 | LOW | `card.Name` vs `entry.Name` drift not validated | backlog (E03+) |
| F-EE-08, 09, 10 | LOW/NIT | Doc-only / WONT-FIX | n/a |

## Onboarding observations summary

| Source | Top action | Disposition |
|--------|------------|-------------|
| Lloyd Braun (S04E01) #1: "seam" undefined | ADR-012 sidebar | **CLOSED in ac31709** |
| Lloyd #2: silent registry.json registration | README.md `[!IMPORTANT]` admonition | **CLOSED in ac31709** |
| Lloyd #3: glossary missing | README.md `## Glossary` section | **CLOSED in ac31709** |
| Mickey (S04E02): `(no card)` token noisy | Rename to `unknown` | E04 *Reading Room* |
| Mickey: 60-char truncation cuts mid-word | Truncate at last word boundary | E04 *Reading Room* |

## Cast balance ledger

S04E02 corrects course further from the S03 `5/5/5/0/0/0...`
distribution:

- **Russell:** 0 -> 1 lead (S04E02 LEAD, the user-visible payoff)
- **Mickey:** 1 -> 2 leads (S04E02 a11y co-lead)
- **Bookman:** 0 -> "1 brief" (drafted E03; will lead E03 proper)
- **Mr. Pitt:** 0 -> 1 (program-management refresh)
- **Newman:** 5 -> 6 (CRITICAL hotfix; not a planned lead, but a
  high-impact unplanned screentime)
- **Puddy:** 1 -> 2 leads (regression suite owner this episode)
- **FDR:** 2 -> 3 leads (adversarial pass owner this episode)
- **Elaine:** 2 -> 3 leads (Lloyd fix-forward)

Rule-5 pairings firing: Russell+Mickey (UX+a11y), Kramer+Elaine
(impl+docs), FDR+Newman (red-team+remediation), FDR+Puddy
(red-team+regression).

Bench-warmers flagged by Mr. Pitt: Jerry, Lippman, Peterman, Sue
Ellen, Wilhelm, Soup Nazi, Bania, Bob, Morty, Keith, Rabbi, Jackie,
Uncle Leo. Recommend reservations for Jerry (eval-as-CI in E10)
and Lippman (release dress rehearsal E11) before E12 mid-season
audit.

## Risks & follow-up

- **F-EE-04 stays open.** `AZ_AI_REGISTRY_DIR` silent re-anchor is
  one-line stderr fix in E03 prologue. Russell suggested implementer.
- **Notes-array DoS (F-EE-06)** is a one-line `count` cap in the
  front-matter parser. Pair with E03+ refactor.
- **macOS firmlinks (F-EE-05)** not exercised on CI. Open question
  whether to file as accepted-residual or invest in a macOS-specific
  test runner.
- **Kramer's lead drought.** S03 workhorse, zero S04 leads through
  E09. Per Mr. Pitt's note, may trip cast-balance floor at E12 audit
  if E10+ also pass him over. Recommend slotting Kramer for E10 (eval
  harness implementation) or E11 (cache hash promotion).
- **AOT binary delta unmeasured this episode.** Pure managed code
  added (no source-gen JSON for `ModelCard`), estimated `<` 10 KB ILC
  overhead. Bania can verify pre-merge if budget audit is desired
  before v2.4.0 cut.

## Next steps

- **S04E03 *The Capabilities*** (Bookman LEAD, Maestro co-lead).
  Brief drafted at `docs/episode-briefs/s04e03-the-capabilities.md`;
  awaiting greenlight. Includes F-EE-04 prologue fix.
- **S04E04 *Reading Room*** (Elaine LEAD). Promote `docs/model-cards/`
  to a real reading room linked from `--help`. Folds Mickey's polish
  observations.
- **S04E05 *The Picker*** (Costanza + Maestro). `ResolveSmartDefault`.
- **S04E06 -- Mr. Pitt cast-balance audit** (mandatory).
- **S04off2 candidate** -- Babu Bhatt could be slotted for an i18n
  follow-up (hi/ar/de/pt for v2.4.0 cut?). User-driven.

## References

- `docs/episode-briefs/s04e01-the-registry.md` (predecessor, GREENLIT)
- `docs/episode-briefs/s04e03-the-capabilities.md` (successor, DRAFT)
- `docs/exec-reports/s04-blueprint.md` (live running-order section)
- `docs/exec-reports/s04e01-the-registry.md` (predecessor exec-report)
- `docs/adr/ADR-012-model-registry-seam.md` (Wave 2 FDR appendix
  plus F-EE-01 CLOSED block)
- `docs/model-cards/REVIEW-onboarding.md` (Mickey a11y appendix)
- `docs/model-cards/README.md` (Lloyd top-3 fix-forward + Glossary)
- `azureopenai-cli/Registry/ModelCard.cs` (NEW, Kramer)
- `tests/AzureOpenAI_CLI.Tests/DoctorRegistryTests.cs` (NEW, Puddy)
- `tests/AzureOpenAI_CLI.Tests/DoctorRegistryAccessibilityTests.cs` (NEW, Mickey)
- CHANGELOG `[Unreleased]` -- this episode's entries
