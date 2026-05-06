---
title: Security Audit -- v2.1.2 (S03E10 / The Keychain)
auditor: Newman (Security Inspector)
date: 2026-05-07
release_under_review: post-S03E10 (per-provider env-section loader)
scope:
  - azureopenai-cli/Program.cs (LoadConfigEnvFrom only -- top-of-file loader, NOT BuildChatClient or provider dispatch)
  - azureopenai-cli/SecretRedactor.cs (new ProviderKeyEnvRx pattern)
  - tests/AzureOpenAI_CLI.Tests/EnvLoaderSectionTests.cs (new)
  - tests/AzureOpenAI_CLI.Tests/SecretRedactorTests.cs (extended)
  - tests/integration_tests.sh (S03E10 block)
  - README.md (Configuration / per-provider sections)
excluded_from_scope:
  - BuildChatClient and provider-dispatch hot path -- E09 territory, not touched this episode
  - OpenAiCompatAdapter.cs -- under concurrent authoring by E09
  - All other Program.cs middle and tail
supersedes: n/a
related_audits:
  - docs/audits/security-v2.1.1-reaudit.md
  - docs/audits/security-v2.1-post-prompts.md
related_exec_reports:
  - docs/exec-reports/s03e07-the-redactor.md
  - docs/exec-reports/s03e08-the-pick.md
  - docs/exec-reports/s03e10-the-keychain.md
predecessor_audit_id: security-v2.1.1-reaudit
audit_id: security-v2.1.2-keychain
severity_scale:
  CRITICAL: Code execution as the local user via routine trigger.
  HIGH: Likely accidental breakage / lower-likelihood RCE under realistic input.
  MEDIUM: Structural drift hazard, no live exposure.
  LOW: Defense-in-depth only.
  INFO: Note for the next auditor.
---

# Security Audit -- 2026-05-07 -- Newman (S03E10)

> *Hello. Newman. The drawer has compartments now. Each compartment has its
> own lock. The lock list grew by one label and the redactor caught up the
> same afternoon.*

---

## Executive Summary

**Verdict: GREEN -- per-provider credential isolation lands clean.
Two LOW-severity residual findings filed (`newman-2026-05-K-1` documentation
gap, `newman-2026-05-K-2` keyring-vs-file precedent). One INFO note
(`newman-2026-05-K-3`) on raw-mode coverage. No CRITICAL or HIGH.**

ADR-010 picked OpenAI direct as the first non-Azure cloud. The compat
adapter (E09) is being authored in parallel. This episode (E10) closes
the credential-storage half of the same hand-off: the env loader now
recognises `[provider:NAME]` section headers and namespaces bare keys
(`API_KEY`, `API_TOKEN`) into provider-prefixed env vars
(`OPENAI_API_KEY`, `GROQ_API_KEY`, `TOGETHER_API_KEY`,
`CLOUDFLARE_API_TOKEN`). The default (unsectioned) content keeps every
existing file working unchanged. Unknown sections warn to stderr (silent
under `--raw` / `--json`) and skip without aborting.

I audited four invariants. All four hold:

1. **No cross-contamination.** A value under `[provider:openai]` cannot
   land in `AZUREOPENAIAPI`, and vice versa. Verified by unit test
   (`OpenAiSection_DoesNotLeakIntoAzureSlot`,
   `AzureDefault_DoesNotLeakIntoOpenAiSlot`) and by the integration
   smoke that asserts the loaded secret value never appears in
   `--config show` output.
2. **Shell precedence preserved.** A pre-set env var wins over file
   content -- the section path obeys the same `IsNullOrEmpty` guard
   as the default path. Verified by `ExistingEnvVar_NotOverwritten_BySection`.
3. **No new redactor blind spot.** Every namespaced env var name
   appears in `SecretRedactor.ProviderKeyEnvRx`, replaced as
   `[REDACTED:provider-key]`, and is unit-tested by name in
   `SecretRedactorTests` for both export-syntax and exception-message
   shapes. The Azure label (`[REDACTED:azure-key]`) and the new
   provider label (`[REDACTED:provider-key]`) collapse cleanly when
   both appear in the same string -- the cross-contamination guard
   `AzureKey_AndOpenAiKey_BothMasked_DistinctLabels` is the regression
   pin.
4. **No abort-on-unknown.** A section header for a provider we have
   never heard of (S04 candidates, vendor renames, typos) warns once
   to stderr and the rest of the file keeps loading. The binary does
   not exit non-zero. Forward-compat is documented and tested.

| Severity | Count (this pass) |
|----------|-------------------|
| CRITICAL | 0 |
| HIGH     | 0 |
| MAJOR    | 0 |
| MEDIUM   | 0 |
| LOW      | 2 |
| INFO     | 1 |

---

## Threat Model

### Attack: Cross-contamination via shared secret slot

Before E10 the loader had one slot for an API key and one provider
that wrote to it (`AZUREOPENAIAPI`). When E09 lands the OpenAI-compat
adapter, a user with both Azure and OpenAI credentials in the same
file could (a) overwrite their Azure key with an OpenAI key, or (b)
have a stale Azure key route a request to OpenAI's endpoint. Either
is a credential-confusion bug; (b) is also a billing-leak bug.

**Mitigation:** section headers carve disjoint namespaces. The
loader resolves each line's effective env-var name based on the
current section; the default section keeps writing verbatim keys, the
named section uppercases-and-prefixes. The two namespaces never share
a key.

**Residual risk:** the user can still write
`export OPENAI_API_KEY=...` in the default section and it will land
in the OpenAI slot. That is intentional -- back-compat with files
that pre-date sections. Newman's position: the namespacing is a
*convenience*, not a *gate*. The gate is "different env-var names",
which has held since Azure-only.

### Attack: Stderr leak of section content under raw mode

Espanso, AHK, and cron pipe stderr-clean. A loader that prints
`[WARNING] unknown section [provider:foo]` under `--raw` would
contaminate the consumer's text-expansion output. Worse, if the
warning ever included the *value* of a misplaced key, it would be
a live secret leak.

**Mitigation:** the loader takes an `isRaw` flag (set by
pre-detection in `Main` before `ParseArgs` runs) and silences the
warning under `--raw` / `--json`. The warning text *never* includes
key values -- only the section header name and the path. Verified
by `UnknownProviderSection_RawMode_Silent` and the integration
`--raw silences unknown-section warning` assertion.

**Residual risk:** none above the existing baseline. The pre-detect
scan reads the same `args[]` `ParseArgs` later parses; the only
divergence is a future flag that aliases `--raw` (e.g. an env var).
Not on the roadmap. Tracked as `newman-2026-05-K-3` (INFO).

### Attack: Section-header injection via crafted env file

The env file is read from `~/.config/az-ai/env`. Write access to
that file is already a full local-credential compromise (the
pre-existing F-8 finding tracks the missing `chmod 600` enforcement
warning). Section headers do not widen this surface -- the parser
accepts only the bracket form `[name]`, and unknown names skip
without dispatching arbitrary keys into arbitrary slots.

**Mitigation:** allow-list of six provider names
(`KnownProviderSections`); anything else lands in the warn-and-skip
branch. No reflection, no dynamic env-var name construction outside
the `prefix + upperKey` join. Native AOT safe.

**Residual risk:** none. The file-mode enforcement gap is the
existing F-8 finding (open against Kramer + Newman); it predates this
episode and is not in scope here.

### Attack: Double-prefix on already-namespaced keys

A user copies an example that uses `OPENAI_API_KEY` as the bare key
inside a `[provider:openai]` section. Naive prefixing would produce
`OPENAI_OPENAI_API_KEY`, leaving the actual `OPENAI_API_KEY` slot
empty and the credential effectively dropped. Silent failure mode.

**Mitigation:** the loader checks whether the upper-cased key
already starts with `<PROVIDER>_`; if so, it passes through
unchanged. Pinned by `OpenAiSection_AlreadyNamespacedKey_NotDoublePrefixed`.

**Residual risk:** none.

---

## Findings

### `newman-2026-05-K-1` -- LOW -- README does not document the env-file mode 600 expectation alongside the new sections

The Configuration section (README.md ~L153) shows the new
`[provider:openai]` example but does not restate the `chmod 600`
hygiene that the prior `setup-secrets` flow established. A user
hand-authoring the file post-E10 may not know the file permission
matters. Pre-existing finding `newman-2026-05-F-13` covers this for
the auto-load section; K-1 is the same gap re-surfaced by the new
example block.

**Mitigation proposed:** one sentence under the per-provider example
calling out the `chmod 600` hygiene, owned by Elaine in a docs-only
follow-up. Not a release blocker.

**State:** open. Owner: Elaine.

### `newman-2026-05-K-2` -- LOW -- No precedent decision documented for OS keyring vs file storage

ADR-010 Decision says the bearer is "resolved from `OPENAI_API_KEY`
(env) or the per-OS keychain (S03E10)." This episode delivered the
env half. The OS-keychain half (libsecret on Linux, Keychain on
macOS, Credential Manager on Windows) is *not* in this episode and
does not have an ADR yet. Without that ADR, the project has a
half-implemented promise on disk. A future contributor reading
ADR-010 will expect to find keychain code and find only the file
loader.

**Mitigation proposed:** Costanza opens an FR (FR-025 candidate) or
amends ADR-010 with an explicit "file-store now, keychain deferred
to S04 / FR-NNN, here is the placeholder" note. Tracked here for
the writers' room and the season finale retrospective.

**State:** open. Owner: Costanza.

### `newman-2026-05-K-3` -- INFO -- Raw-mode pre-detection scans args, not env

The `LoadConfigEnv(preRaw)` call site detects `--raw` / `--json` by
linear-scanning `args[]` before `ParseArgs` runs. Future flags that
alias raw mode (an env var like `AZ_RAW=1`, a config-file toggle, a
new short alias `-q`) would not be honoured by the pre-detect scan
and would leak the unknown-section warning under those modes. No
such alias exists today.

**Mitigation proposed:** if a raw-mode alias is added, extend the
pre-detect block in `Program.Main` (the comment block already flags
the contract). For now this is documented, not coded.

**State:** open. Owner: future-Newman.

---

## Verification matrix

| Invariant | Test | Layer |
|-----------|------|-------|
| OpenAI section -> OPENAI_API_KEY | `OpenAiSection_ApiKey_NamespacedToOpenAiApiKey` | unit |
| Groq section -> GROQ_API_KEY | `GroqSection_ApiKey_NamespacedToGroqApiKey` | unit |
| Together section -> TOGETHER_API_KEY | `TogetherSection_NamespacesCorrectly` | unit |
| Cloudflare section -> CLOUDFLARE_API_TOKEN | `CloudflareSection_NamespacesCorrectly` | unit |
| No double-prefix on namespaced key | `OpenAiSection_AlreadyNamespacedKey_NotDoublePrefixed` | unit |
| No leak OpenAI -> Azure | `OpenAiSection_DoesNotLeakIntoAzureSlot` | unit |
| No leak Azure -> OpenAI | `AzureDefault_DoesNotLeakIntoOpenAiSlot` | unit |
| Shell env wins over file | `ExistingEnvVar_NotOverwritten_BySection` | unit |
| Unknown section warns | `UnknownProviderSection_WarnsToStderr_AndSkipsContents` | unit |
| Raw mode silences warn | `UnknownProviderSection_RawMode_Silent` | unit |
| BOM tolerated | `Utf8Bom_AtStartOfFile_NotTreatedAsKeyChar` | unit |
| CRLF tolerated | `CrlfLineEndings_ParseCorrectly` | unit |
| Mixed default+section | `MixedDefaultAndOpenAiSection_BothLoadIntoCorrectSlots` | unit |
| All 4 new env vars redact | `ProviderApiKeyEnvVar_InErrorMessage_IsMasked` (Theory x4) | unit |
| Provider-key vs azure-key labels distinct | `AzureKey_AndOpenAiKey_BothMasked_DistinctLabels` | unit |
| End-to-end: secret never reaches stdout | "OPENAI_API_KEY value not leaked" | integration |
| End-to-end: unknown section warns | "unknown section warns to stderr" | integration |
| End-to-end: --raw silences | "--raw silences unknown-section warning" | integration |

---

## Verdict

**GREEN.** Per-provider credential isolation is real, tested at unit
and integration layers, and the redactor coverage tracks the new
namespaces by name. No CRITICAL / HIGH / MAJOR findings opened. Two
LOW + one INFO recorded above and propagated to
`docs/findings-backlog.md`.

E11 may proceed. The compat-adapter (E09) hand-off is unblocked on
the credential-loading side.

> *Hello. Newman. Six provider compartments. One drawer. The lock list
> grew. Nothing leaked. Show me the next episode.*
