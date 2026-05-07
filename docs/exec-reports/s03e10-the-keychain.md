# S03E10 -- *The Keychain*

> *Newman walks in with a clipboard and a label-maker. The drawer gets
> compartments. Each compartment gets its own lock. The label list grows
> by one. Nothing leaks.*

**Commit:** `pending` (ships at end of episode)
**Branch:** `main`
**Runtime:** ~75 min real time
**Director:** Larry David (showrunner)
**Cast:** Newman (lead, security inspector), Kramer (E09, concurrent and untouched), Elaine (docs follow-up named), Costanza (ADR-010 author, cited only), Puddy (test peer review, off-screen), Larry David (sign-off)
**Arc:** Provider Abstraction Seam -- E10 of 13 (Arc 2 mid-act)
**Related ADRs/FRs:** ADR-010 (the pick that named this episode), ADR-007 (redactor mandate the new patterns inherit), FR-014 (the umbrella)

---

## The pitch

Two episodes ago Costanza picked the first non-Azure cloud. One
episode ago Kramer started writing the adapter. This episode is the
half of the same hand-off that the adapter cannot land without: where
do the new credentials *live* on disk, and how do we make sure an
OpenAI key cannot wander into the Azure slot, or the other way around,
when both are sitting in the same file written by the same `make
setup-secrets` flow?

The answer is sections. The env file at `~/.config/az-ai/env` -- the
one the binary auto-loads at startup so Espanso, AHK, and cron all
find credentials without touching a shell profile -- learns to
recognise INI-style `[provider:NAME]` headers. Bare keys inside a
section get namespaced by the provider name on the way into the
process environment. `[provider:openai] / API_KEY=sk-...` lands in
`OPENAI_API_KEY`. `[provider:azure] / API_KEY=...` lands in
`AZURE_API_KEY` (a back-compat alias the loader still respects), and
the canonical Azure slot `AZUREOPENAIAPI` keeps working from the
default section verbatim. Six providers known by name today: azure,
openai, foundry, groq, together, cloudflare. Anything else warns to
stderr and skips, because forward-compat with future providers should
not be an abort condition and a stray section header should not take
the binary down.

The default section -- the one with no header at all, the one every
existing file starts with -- keeps working unchanged. That is the
back-compat contract. The `make setup-secrets` flow that wrote
`export AZUREOPENAIAPI="..."` in v2.0 still writes the same line and
the loader still parses it into the same env var. No migration
required. No "you must edit your file" footnote in the release notes.
The new format is *additive*, and the old format is the default-section
behaviour by definition.

The redactor catches up on the same afternoon. Four new env-var names
become first-class redactor patterns -- `OPENAI_API_KEY`,
`GROQ_API_KEY`, `TOGETHER_API_KEY`, `CLOUDFLARE_API_TOKEN` -- with a
dedicated `[REDACTED:provider-key]` label that is distinct from the
existing `[REDACTED:azure-key]` label. The cross-contamination guard
test asserts the labels never collapse: an exception message that
mentions both surfaces ends up with both labels in the redacted
output, each pinned to the right variable name.

This is a Newman episode. He drives. He files findings. He writes the
audit. He signs off. Costanza is on the credits because ADR-010 is
why this episode exists; he does not appear on screen. Kramer is in
the next room writing E09 and is not touched. Elaine gets one
follow-up assignment for the README example block. Puddy reads the
test list and approves it without speaking. Larry signs the cut.

---

## Scene-by-scene

### Cold open -- the drawer has compartments now

The Provider Abstraction Seam was a metaphor through E05. By E06 it
was a schema (`Preferences.cs`). By E07 it was a lock on that schema
(`SecretRedactor.cs`). E08 picked the first thing to put through it
(OpenAI direct, ADR-010). E09 is currently the wire-protocol adapter
for that pick. None of this works on a real machine if the
credentials for the new provider land in the *same env-var slot* as
the existing provider, because every code path downstream of
`LoadConfigEnvFrom` reads
`Environment.GetEnvironmentVariable("AZUREOPENAIAPI")` and there is
exactly one such slot. Two providers, one slot, one machine. That is
not a seam; that is a collision.

Newman walks in. He has a clipboard. He has a label-maker. He has a
list of six provider names and a regex pattern in his pocket. He
opens the drawer.

The drawer needs compartments. Each compartment needs its own lock.
The lock list needs to know which compartment it is locking. *Hello.
Newman. Let us proceed.*

### Act I -- The loader

The change to `LoadConfigEnvFrom` is forty lines of careful parsing
in a method that was, until this morning, twenty lines of careful
parsing. The shape:

1. Track a current-section state across the line loop. `null` means
   "default section", a string means "named section". Initialise to
   `null` so an unsectioned file behaves exactly as it did before.
2. On a line that starts with `[` and ends with `]`, treat it as a
   section header. Recognise `provider:NAME` and check `NAME` against
   the allow-list `KnownProviderSections = { azure, openai, foundry,
   groq, together, cloudflare }`. Known names update the section
   state. Unknown names warn to stderr and *also* update the section
   state -- the warning fires once per header, the contents skip
   silently.
3. On a key=value line, resolve the effective env-var name based on
   the section state. Default section: verbatim key. Named section:
   uppercase the key, prefix with `<PROVIDER>_`, but skip the prefix
   if the upper-cased key already starts with that prefix. The
   skip-if-already-prefixed branch is the difference between
   `OPENAI_API_KEY` (correct) and `OPENAI_OPENAI_API_KEY` (silent
   credential drop). It is one `StartsWith` call and one regression
   test.
4. The `IsNullOrEmpty(GetEnvironmentVariable(effectiveKey))` guard
   that already existed for the default path is preserved on the
   named path. Pre-set env vars still win. Shell profile precedence
   is unchanged.
5. The `try { ... } catch { /* silent */ }` envelope is unchanged.
   File-not-found is unchanged. The DotEnv-style "no exceptions
   reach the user" contract is unchanged.

BOM tolerance is added because some Windows-authored env files start
with a UTF-8 BOM and the old parser would treat the BOM as part of
the first key name. CRLF tolerance was already there via
`ReadAllLines` plus `Trim()`; the new parser preserves it.

The raw-mode pre-detection is the only change to `Main`. The loader
runs *before* `ParseArgs` (because `ParseArgs` reads env vars the
loader writes), so the loader does not yet know whether `--raw` or
`--json` was passed. A linear scan of `args[]` for the two flag
names sets a `preRaw` bit which is forwarded to
`LoadConfigEnvFrom(path, isRaw)`. The scan is O(N) on argv length,
which is negligible. The bit is the only thing the loader needs to
silence the unknown-section warning under raw mode.

### Act II -- The redactor catches up

The new env-var names get a dedicated `ProviderKeyEnvRx` pattern in
`SecretRedactor.cs`:

```text
OPENAI_API_KEY | GROQ_API_KEY | TOGETHER_API_KEY | CLOUDFLARE_API_TOKEN
```

Replaced with `<NAME>=[REDACTED:provider-key]`. Distinct label from
the Azure pattern (`[REDACTED:azure-key]`) so a redacted line that
contains both stays parseable for an operator who needs to know
*which* provider's credential blew up.

The pattern runs *after* `KvSecretRx` in the chain, intentionally.
`KvSecretRx`'s tail-match would otherwise catch `OPENAI_API_KEY=...`
as a generic `api_key=...` and label it `[REDACTED:api-key]`. We
want the specific label to win. Running `ProviderKeyEnvRx` last
means it overwrites the generic label with the provider-specific
one when both patterns would have fired. The cross-contamination
guard test (`AzureKey_AndOpenAiKey_BothMasked_DistinctLabels`) is
the regression pin.

500ms timeout, `Interlocked.Increment` on the timeout counter,
ASCII-only patterns, `RegexOptions.Compiled |
RegexOptions.IgnoreCase | RegexOptions.CultureInvariant` -- all
inherited from the existing patterns. Native AOT safe by
construction. No reflection. No dynamic types. Newman files no
findings against the redactor change because the change *is* the
finding being closed.

### Act III -- The tests

Two test files. One new, one extended.

`EnvLoaderSectionTests.cs` is new and lives under the
`[Collection("ConsoleCapture")]` attribute because three of its
cases swap `Console.Error` to a `StringWriter` to assert the
warning text. The cases:

- Default section back-compat (shell-export and bare KV).
- Each known provider section (`openai`, `groq`, `together`,
  `cloudflare`) namespaces correctly.
- Already-namespaced keys do not double-prefix.
- Mixed default + named section in the same file.
- Multiple named sections in the same file, each isolated.
- Comments and blank lines tolerated inside sections.
- BOM tolerated at start of file.
- CRLF line endings parsed correctly.
- Malformed header (missing closing bracket) skipped without abort.
- Unknown provider section warns to stderr and skips contents.
- Unknown provider section under `--raw` is silent.
- Unknown non-provider section also warns.
- OpenAI section does not leak into Azure slot.
- Azure default does not leak into OpenAI slot.
- Existing env var is not overwritten by named section content.

Sixteen tests. Every one has a one-line rationale comment in the
file. Every one runs in under 50ms. The whole class clears in well
under a second.

`SecretRedactorTests.cs` is extended with five new cases at the end
of the file:

- `[Theory]` over the four new env-var names, each asserting the
  value is masked and the variable name is preserved.
- Export-syntax case (`export OPENAI_API_KEY=sk-...`).
- In-exception case (the Newman P1 contract for ADR-007 section 2 applied
  to the new namespace).
- Cross-label distinctness (`[REDACTED:azure-key]` and
  `[REDACTED:provider-key]` both present, neither collapsed).

Total test delta this episode: +21 unit tests. All green on first
run after compilation cleared.

### Act IV -- The integration smoke

The `tests/integration_tests.sh` block lands as
"S03E10 -- per-provider credential sections" with seven assertions.
Hermetic `HOME` via `mktemp -d`, env file written via heredoc,
`chmod 600` applied because Newman files findings on himself if he
forgets. The smoke runs the binary against `--config show` (which
exercises the loader without real credentials) and asserts:

1. Exit 0 with sectioned env file present.
2. Default-section endpoint flows through to resolved config.
3. `OPENAI_API_KEY` value never appears in `--config show` output.
4. `GROQ_API_KEY` value never appears in `--config show` output.
5. Unknown section does not abort startup.
6. Unknown section warns to stderr.
7. `--raw` silences the unknown-section warning.

The third and fourth assertions are the headline. `--config show`
does not print credentials by design (it prints sources, not
values), but the assertion is a belt-and-suspenders pin against a
future regression where someone adds a value-printing branch and
forgets the secret-stripping. Newman's clipboard is a clipboard, not
a memo pad.

### Act V -- The hand-off

E09 is being authored in parallel by Kramer in a separate stash. The
shared-file-protocol invariant is that this episode does *not* touch
`BuildChatClient` or anything in Program.cs's provider-dispatch
middle. The loader sits at the top of `Program.cs` (above all the
mode dispatchers) and the redactor sits in its own file. Both are
module-edge changes. Kramer's middle-of-file E09 work is undisturbed
on inspection. No conflict reported by Newman's audit.

The follow-up assignments named in the audit (`newman-2026-05-K-1`
docs gap on chmod 600, `newman-2026-05-K-2` keyring-vs-file
precedent ADR, `newman-2026-05-K-3` raw-mode-alias coverage note)
are written to `docs/findings-backlog.md` and propagate to the
writers'-room file. Two LOW + one INFO. Verdict GREEN.

Larry signs off. The drawer has compartments. The compartments have
locks. The label list grew. Nothing leaked.

---

## Files changed

- `azureopenai-cli/Program.cs` -- `LoadConfigEnv` and
  `LoadConfigEnvFrom` rewrite for section-aware parsing; `Main`
  pre-detect for raw mode. ~95 lines net add.
- `azureopenai-cli/SecretRedactor.cs` -- new `ProviderKeyEnvRx`
  pattern, replace step appended at end of redact chain. ~12 lines
  net add.
- `tests/AzureOpenAI_CLI.Tests/EnvLoaderSectionTests.cs` -- new
  file, 16 unit tests, ConsoleCapture collection. 240 lines.
- `tests/AzureOpenAI_CLI.Tests/SecretRedactorTests.cs` -- extended
  with 5 cases (1 Theory of 4 + 4 standalone). ~70 lines net add.
- `tests/integration_tests.sh` -- new "S03E10 -- per-provider
  credential sections" block. ~95 lines net add.
- `README.md` -- per-provider sections subsection added under the
  Configuration heading; old + new shown side by side; back-compat
  callout in plain prose.
- `CHANGELOG.md` -- `[Unreleased] / Added` entry per the spec line
  in the episode brief.
- `docs/audits/security-v2.1.2-keychain.md` -- new audit, GREEN
  verdict, three findings filed (K-1 LOW, K-2 LOW, K-3 INFO).
- `docs/findings-backlog.md` -- three new K-* rows under Active.
- `docs/exec-reports/s03-writers-room.md` -- E10 row added to the
  episodes-shipped table.
- `docs/exec-reports/s03e10-the-keychain.md` -- this file.

---

## Preflight

`DOTNET_ROOT=/usr/lib/dotnet make preflight`: green.

- format-check: clean
- build: 0 warnings, 0 errors
- unit tests: +21 new, all pass
- integration tests: +7 new, all pass; full suite 46/46 (2 skipped
  for absent live API creds, expected)
- exec-report-check: this file satisfies the gate

---

## Findings opened (this episode)

- `newman-2026-05-K-1` (LOW) -- README does not document chmod 600
  alongside the new per-provider example block. Owner: Elaine.
- `newman-2026-05-K-2` (LOW) -- ADR-010 references "per-OS keychain"
  but no ADR exists for it; episode delivered the file half only.
  Owner: Costanza.
- `newman-2026-05-K-3` (INFO) -- raw-mode pre-detection scans argv,
  not env or config-file aliases. Owner: future-Newman.

## Findings closed (this episode)

None directly. The episode is additive scope.

---

## Next episode preview

> *Jerry is in the next room with a setup wizard, a stopwatch, and a
> bottle of grievances about the first-run experience. The wizard is
> about to learn the word "provider".* **S03E11 -- *The Wizard,
> Reprise*. Lead: Jerry.**
