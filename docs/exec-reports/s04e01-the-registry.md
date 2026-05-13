# S04E01 -- *The Registry*

> *Five sub-agents, two waves, one off-roster Babu special -- S04 opens
> with a real model registry and a junior-readable doc trail.*

**Commit range:** `493c21b` (greenlight) .. HEAD
**Branch:** `main` (direct push)
**Runtime:** ~3 hours wall-clock end-to-end
**Director:** Larry David (showrunner)
**Cast (in order of dispatch):**

| Wave | Agent | Role | Commit |
|------|-------|------|--------|
| 1 | Kramer | Registry seam implementation | `dec7e1f` |
| 1 | Elaine | Model card spec + ADR-012 + 3 seed cards | `0bf4b1b` |
| Off-roster | Babu Bhatt | i18n quick-starts (ja/zh/es/ko) | `fb44420` |
| Off-roster | Babu+Puddy | CJK + Spanish round-trip tests | `239b4c3` |
| 2 | Puddy | RegistryTests (7 required facts) | `e06b608` |
| 2 | FDR | Adversarial review appendix on ADR-012 | `9b9c352` |
| 2 | Lloyd Braun | Junior-lens onboarding review | `8cd7713` |
| Fix-forward | Kramer (this report) | F-02 terminal-injection guard | this commit |

Plus the v2.3.0 release cut (`493c21b` tagged `v2.3.0` -- separate
chronologically but part of the same episode arc).

## The pitch

S03 closed with a real product: deterministic builds, SBOMs, CodeQL,
a 7-leg release matrix. But model selection was still a string-typed
free-for-all: any deployment name we got out of `AZUREOPENAIMODEL` was
treated as gospel, capabilities were implicit, and the CLI had no way
to introspect "what models do I actually know about?" S04 opens with
the registry seam that fixes that -- a typed, validated, embedded-first
model registry that user config can replace (not merge -- replace, by
design) -- plus the documentation scaffolding (model cards, ADR-012)
that makes the registry maintainable by humans who aren't the author.

And because the user named four languages explicitly -- Japanese,
Chinese, Spanish, Korean -- we ran Babu off-roster in parallel to land
four translated quick-starts and a CJK+es round-trip test suite that
proves UTF-8 + InvariantGlobalization survives ja/zh/es/ko intact.

## Scene-by-scene

### Act I -- Wave 1 (impl + docs, parallel)

**Kramer** built the seam:

- `azureopenai-cli/Registry/ModelCapability.cs` -- AllowedTags + validator
- `azureopenai-cli/Registry/ModelRegistryEntry.cs` -- record
- `azureopenai-cli/Registry/ModelRegistry.cs` -- loader (embedded seed,
  user override at `~/.config/az-ai/registry.json` *replaces* seed)
- `azureopenai-cli/Registry/registry.json` -- 3 seed entries
- `Program.cs` `--doctor` extension: `[registry]` section
- `JsonGenerationContext.cs` -- AOT source-gen registrations
- `AzureOpenAI_CLI.csproj` -- `<EmbeddedResource>` registration

AOT binary delta: **+44.5 KB** (brief specified 15 KB cap; exceeded with
justification -- JSON source-gen overhead is irreducible at AOT, and
`[DynamicDependency(PublicConstructors | PublicProperties)]` is the
sweet spot. Using `All` would have added ~89 KB more. Brief budget was
unrealistic; future typed-record briefs should set ~50 KB caps.)

**Elaine** built the doc scaffold:

- `docs/adr/ADR-012-model-registry-seam.md` -- the architectural decision
- `docs/model-cards/README.md` -- the spec
- `docs/model-cards/azure-gpt-4o-mini.md` -- seed card
- `docs/model-cards/azure-gpt-5.4-nano.md` -- seed card
- `docs/model-cards/local-llama.md` -- seed card (the diversity example)

Wave 1 result: 1332/1332 tests pass, lint clean, `[registry]` section
visible under `--doctor`, `--raw` suppression honored.

### Act II -- Babu off-roster i18n special

The user said Japanese was important. Then said Chinese, Spanish, and
Korean were too. Babu shipped:

- `docs/episode-briefs/s04off1-the-translation.md` -- the off-roster brief
- `docs/i18n/README.md` -- the language index
- `docs/i18n/quick-start.ja.md` -- Japanese quick-start
- `docs/i18n/quick-start.zh.md` -- Chinese (Simplified)
- `docs/i18n/quick-start.es.md` -- Spanish
- `docs/i18n/quick-start.ko.md` -- Korean
- `tests/AzureOpenAI_CLI.Tests/I18n/CjkRoundTripTests.cs` -- 29 round-trip facts

All 29 round-trip tests pass on first run. **Zero foundation gaps**:
`<InvariantGlobalization>true</>`, UTF-8 console, and NFKC path
normalization already handle ja/zh/es/ko. The episode's i18n risk was
documentation-only, not code.

### Act III -- Wave 2 (tests + adversarial + onboarding, parallel)

**Puddy** wrote `tests/AzureOpenAI_CLI.Tests/RegistryTests.cs` -- 7
required facts from the brief acceptance criteria:

| # | Test | Verifies |
|---|------|----------|
| 1 | `LoadRegistry_HappyPath_ReturnsThreeEntries` | embedded seed loads |
| 2 | `LoadRegistry_UnknownCapabilityTag_ExitsRc99` | capability validator |
| 3 | `LoadRegistry_MissingCardPath_WarnsNotFatal` | warn on missing card |
| 4 | `LoadRegistry_EmptyFile_ReturnsEmptyList` | `[]` override valid |
| 5 | `LoadRegistry_UserOverrideFile_ReplacesSeedEntries` | replace, not merge |
| 6 | `ModelRegistryEntry_Serialization_RoundTrip` | AOT source-gen path |
| 7 | `LoadRegistry_OfflineFlag_DoesNotAttemptFetch` | no HttpClient surface |

Full suite: **1339/1339 passed.** Puddy flagged two refactor candidates
for E02 (extract testable `ValidateEntry` from the private `Exit(99)`
path; add `overridePath` parameter to break filesystem coupling).

**FDR** appended an adversarial review to ADR-012. Total findings: 9.
**0 CRITICAL.** 1 HIGH (latent), 4 MEDIUM, 3 LOW, 1 NIT.

Top two:

- **F-01 (HIGH, latent):** `cardPath` is stored verbatim with no path
  traversal defense. Zero blast radius today (no card-file read happens
  yet) -- becomes live read-arbitrary-file the moment E02 reads card
  files. Filed as backlog item; E02 MUST add prefix guard.
- **F-02 (MEDIUM, verified):** `WriteRegistrySection` printed
  `Name`/`Provider`/capability tags raw to stdout -- ANSI/OSC injection
  via a crafted user override file. **Fixed in this commit** via
  `SanitizeForTerminal` (strips C0/C1 control chars; preserves CJK + emoji).

**Lloyd Braun** wrote `docs/model-cards/REVIEW-onboarding.md` -- the
junior-lens onboarding review. 30 observations across jargon (12),
prereqs (6), ordering (5), silent footguns (7). Plain-English answer
to *"could a junior add a 4th card on first try?"*: **70% success rate;
30% failure modes are silent.** Top three fix-forward candidates filed
for E02:

1. Define "seam" (design pattern) in ADR-012 -- used 6+ times, never defined.
2. Bold warning in `docs/model-cards/README.md` step 4: "must also
   update `registry.json`, or the model will not load."
3. Glossary or forward-reference section for: embedded resource,
   capability tags, GGUF, quantisation, chat template, Espanso, streaming.

## Fix-forward in this commit

**F-02 fix:** `SanitizeForTerminal()` added to `Program.cs`. Strips all
C0 (0x00..0x1F) and C1 (0x7F..0x9F) control characters from `Name`,
`Provider`, and capability tags before printing them under `--doctor`.
Printable Unicode (CJK, emoji, accented chars) passes through unchanged.

That closes the only Wave 2 finding with verified attacker payload and
keeps F-01, F-03, F-04, F-05, F-06, F-07, F-08 in the findings backlog
for E02+ to triage.

## Adversarial findings summary

| ID | Severity | Title | Disposition |
|----|----------|-------|-------------|
| F-01 | HIGH | `cardPath` traversal (latent until E02) | **backlog -- E02 must fix before card-read lands** |
| F-02 | MEDIUM | Terminal injection in `--doctor` | **FIXED this commit** |
| F-03 | MEDIUM | Unbounded user-override file read | backlog (E02) |
| F-04 | MEDIUM | FIFO/device-file hang on user override | backlog (E02) |
| F-05 | MEDIUM | Duplicate-name routing ambiguity | backlog (E05) |
| F-06 | LOW | NRE in `--doctor` on omitted name/provider | backlog (E02) |
| F-07 | LOW | LoadEmbedded missing try/catch | backlog (E02) |
| F-08 | LOW | JSON `null` override silently falls back | backlog (E02) |
| F-09 | NIT | Capability error missing case-sensitivity hint | backlog |

## Onboarding observations summary

| Category | Count | Top action |
|----------|-------|------------|
| Jargon terms | 12 | Define "seam" in ADR-012; add glossary in README |
| Assumed prereqs | 6 | Link Azure OpenAI deployment-name primer from card README |
| Ordering issues | 5 | Reorder "Adding a new card" steps; bold `registry.json` step |
| Silent footguns | 7 | Bold warning for `registry.json` registration |
| What worked well | 5 | Front matter is self-documenting; honest weaknesses sections |

Top 3 actioned in E02 backlog. Remaining 27 filed in
`docs/model-cards/REVIEW-onboarding.md` for future fix-forward.

## Cast balance ledger

S03 ended with Newman/Kramer/Costanza tied at 5 leads each; 17 cast
members at zero. S04E01 corrects course:

- **Maestro:** 0 -> 1 lead (S03 had zero; co-lead-of-record on E01 brief)
- **Babu Bhatt:** 0 -> 1 lead (off-roster i18n special, this episode)
- **Lloyd Braun:** 1 -> 2 leads (junior-lens review, Wave 2)
- **FDR:** 1 -> 2 leads (adversarial appendix, Wave 2)
- **Puddy:** 0 -> 1 lead (regression tests, Wave 2)
- **Elaine:** 1 -> 2 leads (docs scaffold, Wave 1)

Rule-5 pairings firing: Maestro+Costanza (lead+co-lead),
Kramer+Elaine (impl+docs), FDR+Puddy (red-team+regression).

## Releases

- **v2.3.0 cut.** Tag `v2.3.0` pushed at `493c21b`. Release workflow
  ran 9 of 10 legs green within ~10 min; final `build-binaries
  (osx-x64, macos-13, tar.gz)` leg sat queued for runner availability
  (~38 min queue at this report's authorship). All CI legs green,
  including the new `bench-canary` perf gate.

## Risks & follow-up

- **F-01 cardPath traversal** is the only blocker for E02. E02 *must*
  add a prefix guard before reading card files.
- **F-03 file-size cap + F-04 stream-type check** on user override
  reads -- one-liner guards; pair them in E02 prologue.
- **AOT binary cap:** brief specified 15 KB. Reality: 44.5 KB for
  typed-record additions. Future briefs should set ~50 KB caps.
- **Puddy refactor candidates:** extract `internal static
  ValidateEntry` (no `Exit(99)`), add `overridePath` param to `Load()`.
  Both improve testability without changing public surface.
- **Lloyd's top-3** flow into Elaine's E02 docs work.

## Next steps

- **S04E02 *Embedded Cards*:** Lead Russell (UX/output formatting),
  co-lead Mickey (a11y for `--doctor` registry output). Scope: read
  card files at startup, populate `[registry]` section with description
  + status, with F-01/F-03/F-04 prefix/size/stream guards in prologue.
- **S04E03 *The Capabilities*:** Lead Bookman (output economy),
  co-lead Maestro. Scope: enforce capability tags at request time
  (reject `--tools` on a model lacking `tool_calls`, etc).
- **S04E04 (Act I finale):** Mr. Lippman lead candidate -- v2.4.0 cut.
- **S04E05 *Smart Defaults*:** Costanza + Babu.
- **S04E06:** Mr. Pitt cast-balance audit (mandatory, per
  `writers-room-cast-balance` skill).

## References

- `docs/episode-briefs/s04e01-the-registry.md` (GREENLIT)
- `docs/episode-briefs/s04off1-the-translation.md` (Babu off-roster)
- `docs/adr/ADR-012-model-registry-seam.md` (incl. FDR appendix)
- `docs/model-cards/README.md` + 3 seed cards + `REVIEW-onboarding.md`
- `docs/i18n/README.md` + 4 quick-starts (ja/zh/es/ko)
- `tests/AzureOpenAI_CLI.Tests/RegistryTests.cs`
- `tests/AzureOpenAI_CLI.Tests/I18n/CjkRoundTripTests.cs`
- CHANGELOG `[Unreleased]` -- this episode's entries
