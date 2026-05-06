# S03E06 -- *The Schema*

> *Kramer ships `preferences.json` v1. Providers, profiles, a four-layer resolution chain, and `--config show` painting the table on stdout. Costanza approves the shape with three nitpicks. Newman drops in long enough to confirm the file never serializes a secret. 657 prior tests stay green. 15 new tests land. The seam exists.*

**Commit:** `pending` (ships at end of episode)
**Branch:** `main` (direct push)
**Runtime:** ~70 min real time
**Director:** Larry David (showrunner)
**Cast:** Kramer (lead), Costanza (reviewer), Newman (cameo, secret invariant), Elaine (cameo, docs gap), Mickey Abbott (one line, cosmetics)
**Arc:** Provider Abstraction Seam -- E06 of 13
**Related ADRs/FRs:** FR-014 (provider abstraction), FR-003 / FR-009 (absorbed by FR-014), ADR-009 (default-model-resolution, generalised in this episode)

---

## The pitch

S03 opened by closing three audits in series (E03 docs, E04 secrets, E05 the auditor's auditor). The work was real but the work was *meta* -- audits of the pipeline, not bricks in the wall. E06 is the first brick. The Provider Abstraction Seam arc resumes here, and the first thing the arc needs is a *place to put providers*. Not the providers themselves. The schema. The drawer. Empty drawers, labeled, with a lock.

That is what `preferences.json` v1 is. Two dictionaries -- `providers{}` and `profiles{}` -- a schema-version pin, an XDG-on-Unix-`%APPDATA%`-on-Windows default path, mode 0600 on save, and an explicit no-secrets invariant. v1 carries only what `--config show` needs to paint. The richer fields the arc will require -- `apiKeyEnv`, `apiVersion`, `deployments[]`, `capabilities{}` -- are scheduled for E08 through E11 and are explicitly *not* in this commit. Curb your enthusiasm.

The other half of the episode is the resolution chain. ADR-009 already documented the model-resolution order (CLI flag > env > user config > hardcoded fallback). E06 generalises that to four layers across four fields -- provider, endpoint, model, profile -- and exposes the resolved tuple via `az-ai --config show`, with each layer carrying a stable `source` label callers can switch on. Text output keeps the legacy `# Effective configuration` header so the chaos suite's regression assertion does not light up. JSON output is the new canonical surface: a `ConfigShowJson` envelope with a `resolved{}` map, a `preferences_path`, a `preferences_loaded` boolean, and the lists of known providers and profiles. No secrets. Newman audited that.

This was a clean episode. 672 unit tests green, 35 integration tests green, format clean, no findings carried forward. The four open questions Kramer flagged are all *deferral* questions -- "should this go in E07 or E11?" -- not unresolved bugs. They are triaged at the bottom of this report.

---

## Scene-by-scene

### Cold open -- arc resume

The audit triple closed at the top of last episode. The Wilhelm meta-audit's headline 50% closure rate landed; three forward-looking proposals were greenlit; the showrunner's note was simply that E06 starts the actual season. Today is that day. Kramer is at the keyboard, hawaiian shirt, second cup of coffee, giddyup. The drawer needs to exist before the providers can move in.

The arc, restated for the writers' room: thirteen episodes (E06 through E18) build an abstraction seam such that adding a new chat provider becomes a config change plus an adapter file, not a Program.cs surgery. E06 is the schema. E07 is the redactor that makes the schema safe to surface in errors. E08 picks the first non-Azure cloud. E09 builds the generic OpenAI-compat adapter. E10 wires a real second provider. E11 onward is hardening, retry, telemetry, and capability negotiation. The schema is the floor of all of it. If the floor is wrong, the rest sinks.

### Act I -- The schema

`azureopenai-cli/Preferences.cs` (212 lines, new file) lays out the v1 shape:

```text
Preferences
  schema:    string (pinned to "1")
  providers: Dictionary<string, ProviderEntry>
  profiles:  Dictionary<string, ProfileEntry>
  LoadedFrom (JsonIgnore)

ProviderEntry
  endpoint:    string?
  modelAlias:  string?
  notes:       string?

ProfileEntry
  provider:  string  (must match a key in Providers)
  model:     string? (optional override)
  notes:     string?
```

Three load/save invariants Kramer wrote to and tested:

1. **Missing file is not an error.** `Preferences.Load(path)` on a non-existent path returns a default-constructed instance with `LoadedFrom = null`. The file is optional. Operators who never touch it never see it.
2. **Empty dictionaries serialize as `{}`, never `null`.** A null here would surface as a `KeyNotFoundException` somewhere downstream the first time anyone called `prefs.Profiles["default"]`. The `Save_EmptyDictionaries_SerializeAsObjectsNotNull` test asserts both directions: `"providers": {}` is present, `"providers": null` is not.
3. **Mode 0600 on Unix.** `SetUnixFileMode(path, UserRead | UserWrite)` after every save. Best-effort, swallows on platforms where it cannot apply, matches the pattern `UserConfig` already uses for `~/.azureopenai-cli.json`.

Default path lookup follows platform convention: `%APPDATA%\az-ai\preferences.json` on Windows, `$XDG_CONFIG_HOME/az-ai/preferences.json` on Unix when `XDG_CONFIG_HOME` is set, `~/.config/az-ai/preferences.json` otherwise. The XDG-respecting branch is what makes the test suite tractable -- pinning `XDG_CONFIG_HOME` inside a `TempHome` lets every integration test run against an isolated config tree without touching the developer's actual `~/.config`.

The schema header file-comment lays out the resolution order in plain prose and points at ADR-009 as the canonical source. The ADR existed already; the episode does not amend it. The ADR's "Compliance" section already documented the four layers in the abstract; E06's contribution is to *implement* that order across all four fields, not to redefine it.

### Act II -- The resolution chain

ADR-009 generalised. Per the ADR's compliance language:

| # | Layer              | Examples                                              |
|---|--------------------|-------------------------------------------------------|
| 1 | CLI flag           | `--model gpt-4o`, future `--provider`, `--profile`    |
| 2 | Environment        | `AZUREOPENAIENDPOINT`, `AZUREOPENAIMODEL`, `AZ_PROFILE`, `AZ_PROVIDER` |
| 3 | Active profile     | `preferences.profiles[<active>]`                      |
| 4 | Provider default   | `preferences.providers[<provider>]`                   |
|   | (terminal fallback)| Hardcoded `DefaultModelFallback` (ADR-009)            |

`RunConfigShow(opts, config)` in `Program.cs` (~170 added lines, anchored at the existing config sub-dispatcher around line 2610) walks the chain once per field and records both the resolved value *and* the layer that produced it. The `source` strings are stable -- `"env AZUREOPENAIENDPOINT"`, `"profile 'work'"`, `"preferences provider 'azure'"`, `"hardcoded fallback (ADR-009)"`, etc. -- so consumers (jq pipelines, the chaos suite, future `az-ai doctor`) can switch on them without parsing prose.

The JSON envelope is canonical. `ConfigShowJson` (declared in `JsonGenerationContext.cs` alongside the existing CLI response records) carries:

```text
{
  "resolved": {
    "provider": { "value": "...", "source": "..." },
    "endpoint": { "value": "...", "source": "..." },
    "model":    { "value": "...", "source": "..." },
    "profile":  { "value": "...", "source": "..." }
  },
  "preferences_path":   "/home/.../preferences.json",
  "preferences_loaded": true,
  "providers":          ["azure", "openai"],
  "profiles":           ["default", "work", "personal"]
}
```

Text output keeps the legacy `# Effective configuration` header verbatim because the chaos regression suite asserts on it. Below the legacy block, the new `Resolved configuration:` section paints the four layers with their source labels, followed by a `Preferences file:` line and the `Providers known:` / `Profiles known:` summaries. Mickey Abbott walked by the column alignment, did the eyebrow thing, said "padding looks tight", and moved on. We tabled it -- see open question Q2.

The new AOT registrations land in `JsonGenerationContext.cs` (+11 lines): `Preferences`, `ProviderEntry`, `ProfileEntry`, the two typed `Dictionary<string, ...>` shapes, plus `ConfigShowJson` and `ConfigShowResolvedField`. Every type that crosses the JSON boundary is wired through `AppJsonContext` -- no reflection paths, no AOT trim warnings. The `Json_RoundTripsThroughSourceGenerator` test exercises this explicitly so a future contributor who adds reflection-mode serialization to the file by accident trips the assertion.

### Act III -- The verification

Test pass: **672/672 unit, 35/35 integration.** Format clean. Build clean. Native AOT publish not exercised in this episode (defer to E07 preflight).

#### Sample text output

With `AZUREOPENAIENDPOINT=https://test.openai.azure.com/` and `AZUREOPENAIMODEL=gpt-4o-mini,gpt-4o` set in the environment, and a seeded `preferences.json` carrying one provider (`azure`) and one profile (`default`), `az-ai --config show` paints (paraphrased from Kramer's run):

```text
# Effective configuration
# source: ~/.azureopenai-cli.json
... legacy KV lines ...

Resolved configuration:
  provider:    azure                    (source: profile 'default')
  endpoint:    https://test.openai...   (source: env AZUREOPENAIENDPOINT)
  model:       gpt-4o-mini              (source: env AZUREOPENAIMODEL[0])
  profile:     default                  (source: preferences.json)

Preferences file: /home/.../.config/az-ai/preferences.json (loaded)
Providers known: azure
Profiles known:  default
```

The legacy `# Effective configuration` header is byte-for-byte the prior format; chaos regression assertion happy. The `Resolved configuration:` block below it is the new surface, with each field carrying its source label in parentheses.

#### Sample JSON envelope

Same inputs, with `--json` appended:

```json
{
  "resolved": {
    "provider": { "value": "azure",       "source": "profile 'default'" },
    "endpoint": { "value": "https://...", "source": "env AZUREOPENAIENDPOINT" },
    "model":    { "value": "gpt-4o-mini", "source": "env AZUREOPENAIMODEL[0]" },
    "profile":  { "value": "default",     "source": "preferences.json" }
  },
  "preferences_path":   "/home/.../.config/az-ai/preferences.json",
  "preferences_loaded": true,
  "providers":          ["azure"],
  "profiles":           ["default"]
}
```

The envelope is the canonical surface for tooling. `jq -r '.resolved.endpoint.source'` is the contract; the prose-formatted text block is for humans.

#### Tests

15 new xUnit tests in `tests/AzureOpenAI_CLI.Tests/PreferencesTests.cs` (412 lines including the integration trio Kramer added late in the episode). Every positive is paired with a negative -- the test file's leading comment is explicit on the discipline: *"Pass the pass, FAIL the fail."*

The 15 tests, by name:

1. `Load_MissingFile_ReturnsDefaultsNoThrow` -- non-existent path returns defaults; never throws.
2. `Save_ThenLoad_RoundTripsSchemaVersion` -- schema version pin survives a save/load cycle.
3. `Save_EmptyDictionaries_SerializeAsObjectsNotNull` -- `{}`, never `null`. Negative path on both sides.
4. `Save_ThenLoad_RoundTripsPopulatedPreferences` -- 2 providers, 3 profiles, all fields including notes round-trip.
5. `Save_NeverContainsApiKeyField` -- **the Newman invariant.** No `apiKey`, no `api_key`, no `secret` in the serialized bytes (case-insensitive). Trips on any future contributor who adds a credential field.
6. `Load_MalformedJson_ThrowsInvalidPreferencesException` -- explicit typed exception with the offending path attached.
7. `Load_EmptyFile_ReturnsDefaults` -- zero-byte file is treated like a missing file.
8. `Save_OnUnix_SetsMode0600` -- positive (UserRead+UserWrite present) and negative (Group/Other read+write absent) in one assertion block. Skipped on Windows.
9. `Save_CreatesMissingParentDirectories` -- save into a deep path that does not exist; parents created.
10. `DefaultPath_HasExpectedShape` -- ends with `preferences.json`, contains `az-ai`, contains `config` on Unix.
11. `DefaultPath_RespectsXdgConfigHome_OnUnix` -- XDG override produces a path under the override prefix.
12. `Json_RoundTripsThroughSourceGenerator` -- AOT invariant: serialization goes through `AppJsonContext`, not reflection.
13. `ConfigShow_Json_EmitsResolvedLayersAndPaths` -- `--config show --json` envelope contains `resolved.endpoint.source == "env AZUREOPENAIENDPOINT"`, `resolved.model.source == "env AZUREOPENAIMODEL[0]"`, profile resolves from `preferences.json`, and `"api"` does not appear anywhere in the output (case-insensitive).
14. `ConfigShow_NoPreferencesFile_StillSucceedsWithDefaults` -- absent file, exit 0, model source contains `"ADR-009"`, provider source contains `"hardcoded default"`.
15. `ConfigShow_TextOutput_KeepsLegacyEffectiveBlock` -- regression: text output starts with `"# Effective configuration"`, contains `"Resolved configuration:"`, contains `"Preferences file:"`, never contains an `api_key=sk-` literal.

**Newman drops in.** One line in the writers' room, hostile professional approval: "If `Save_NeverContainsApiKeyField` flips to red, that is a P1 and you stop the train. The negative test is the contract." It went in the test file's docstring. He left.

The Newman invariant is worth stating in plain language because it is the load-bearing security claim of the entire episode: **`preferences.json` is a registry, not a vault.** Credentials live in environment variables and the OS credential store -- never in the file. The schema enforces this by *omission*: there is no `apiKey` field on `ProviderEntry`, no `secret` field anywhere, no `password` field anywhere. The `Save_NeverContainsApiKeyField` test enforces it by *negative assertion*: serialize a populated preferences object, read the bytes back, assert no string matching `apiKey`/`api_key`/`secret` (case-insensitive) appears. A future contributor adding a credential field to the schema either deletes the test (visible in code review) or adds a credential and watches the test go red (visible in CI). Both paths are loud. That is the design.

**Costanza drops in.** Two lines, three nitpicks: (1) `notes` field everywhere as `string?` is fine but it should be excluded from `--raw` mode in any future trigger that displays it; (2) `Schema = "1"` as a string rather than an int makes future `1.1` / `2.0` upgrades cheaper; (3) the active-profile fallback rule (`AZ_PROFILE` > `default` key > first key alphabetically) should be documented in the ADR before E08. All three are accepted. (3) is logged as a docs item for Elaine, not a blocker.

**Elaine drops in.** The `--help` text for `--config show` does not yet mention the new resolved section. She filed it. See open question Q4.

---

## What shipped

**New files**

- `azureopenai-cli/Preferences.cs` -- 212 lines. Schema document (`Preferences`, `ProviderEntry`, `ProfileEntry`), `Load` / `Save` / `DefaultPath` static methods, `InvalidPreferencesException`. AOT-friendly. No reflection paths.
- `tests/AzureOpenAI_CLI.Tests/PreferencesTests.cs` -- 412 lines, 15 xUnit facts. Brief said 264; the integration trio (`ConfigShow_Json_*`, `ConfigShow_NoPreferencesFile_*`, `ConfigShow_TextOutput_*`) was added during the episode after the schema tests stabilised, which is the line-count delta.

**Modified files**

- `azureopenai-cli/JsonGenerationContext.cs` -- +11 lines. New `ConfigShowJson` and `ConfigShowResolvedField` records; `[JsonSerializable]` registrations for `Preferences`, `ProviderEntry`, `ProfileEntry`, `Dictionary<string, ProviderEntry>`, `Dictionary<string, ProfileEntry>`, `ConfigShowJson`, `ConfigShowResolvedField`.
- `azureopenai-cli/Program.cs` -- +170 lines. New `RunConfigShow(opts, config)` method around line 2627. Wires the `--config show` subcommand to the new resolution chain. Legacy `# Effective configuration` block preserved verbatim.

**Untouched (deliberate)**

- ADR-009. The episode operates *under* the ADR; it does not amend it. Costanza's nitpick (3) is queued for an Elaine docs episode, not for this commit.
- `CHANGELOG.md`. Updated by the showrunner during the push, not by Kramer in the episode.
- `docs/exec-reports/README.md` and `s03-blueprint.md`. Orchestrator-owned per `shared-file-protocol`; updated outside this writeup.

---

## Test results

```text
Build:           clean
Format:          clean (dotnet format --verify-no-changes)
Unit tests:      672 / 672 passed
Integration:     35 / 35 passed (tests/integration_tests.sh)
ASCII guard:     clean (no smart quotes, no em-dashes, no ellipses)
AOT publish:     not exercised this episode (carried to E07 preflight)
```

Of the 672, 15 are new -- listed by name in Act III. The other 657 were the prior baseline; all green. The integration suite (`bash tests/integration_tests.sh`) covers the `--config show` text-output regression (`# Effective configuration` header) and the `--config show --json` envelope shape; both unchanged from the test trio's assertions because the integration script asserts on the same surfaces from the binary side.

No test was deleted. No test was disabled. No test was marked `[Skip]` outside the documented Windows-vs-Unix mode 0600 branch (which is a platform skip, not a flake skip).

---

## Open questions

Kramer flagged four. Showrunner triage:

**Q1 -- `--provider <name>` CLI flag.**
Should `--provider` land in this episode as a CLI override layer, or wait for the adapter work in E08-E11?
*Triage:* **Defer to E08-E11.** The CLI flag without an adapter to dispatch to is a stub that prints into the void. The schema is what E06 owes; the dispatch is what the adapter arc owes. Adding `--provider` here would be a half-implemented seam and would tempt a future contributor to wire it to nothing. E08 ("The Pick") is the decision episode for the first non-Azure cloud; E09 ("The Compat") implements the generic `OpenAiCompatAdapter`. `--provider` lands with E09.

A subsidiary point on Q1 worth recording: `AZ_PROVIDER` *as an env var* already works in E06. The resolution chain reads it at layer 2. The deferral is specifically about the *CLI flag*. Operators who want to override the resolved provider today can do so with `AZ_PROVIDER=openai az-ai --config show` and watch the source label flip to `"env AZ_PROVIDER"`. The flag is the deferred piece because the dispatch downstream of the flag is the deferred piece -- the env-var path is plumbed end-to-end and tested.

**Q2 -- column padding cosmetics in text output.**
Mickey Abbott noted the `Pad(value, 24)` width is tight when an endpoint URL is long; values overflow into the source-label column.
*Triage:* **Defer.** Cosmetic only. Not a correctness issue. The `--json` surface is the canonical one; the text surface is for humans, and humans will read past the alignment. Logged on the writers' room "off-roster cosmetics" backlog. Mickey gets a future episode if the backlog grows past three items.

**Q3 -- legacy `# Effective configuration` block retirement.**
The legacy text block is preserved for the chaos regression suite. When does it actually retire?
*Triage:* **Defer to a chaos-regen episode.** The block is cheap to keep; the test that asserts on it is one line; retiring the block requires regenerating the chaos baseline, which is itself a discrete piece of work. Logged on the writers' room "chaos-regen" backlog. Candidate episode: anywhere in the second half of S03 that already touches the chaos suite for another reason. Don't do it standalone.

**Q4 -- `--help` text for `--config show`.**
The help text does not yet mention the resolved-layers section or the JSON envelope.
*Triage:* **Hers (Elaine).** Pure docs. Costanza's nitpick (3) -- documenting the active-profile fallback in ADR-009 -- attaches to the same docs episode. Greenlit for an Elaine-led docs episode in the E07-E08 gap or, if the gap closes too tight, at the head of the E08 brief.

All four are *deferrals*, not blockers. None of them gate the E06 commit.

---

## Casting notes

This is Kramer's third lead of the season (after E01 and the implementation half of E02). The cast distribution table after this episode:

| Lead    | S03 leads to date           |
|---------|------------------------------|
| Kramer  | E01, E02, **E06**            |
| Elaine  | E03                          |
| Newman  | E04, (E05 cameo, E07 lead)   |
| Wilhelm | E05                          |
| Costanza| (none yet -- E08 incoming)   |

Kramer led three of the first six because the work itself was *implementation-first* (triggers, ergonomics, schema). The mid-season balance test at E12 will check that Costanza, Frank, Mickey, and the supporting bench have all had at least one lead by then. E08 (Costanza, "The Pick") and E09 (Kramer, "The Compat") are already on the board; if a third Kramer lead in three episodes starts to feel narrow, the showrunner has license to swap in Puddy or FDR as guest leads on E11's hardening work. We are not there yet. Pretty, pretty, pretty good.

The Newman cameo in E06 is deliberate Newman-warmup for E07 -- he is the lead next episode, and having him drop in here to bless the no-secret invariant is the seam between the two. The Costanza appearance is the standing PM check: any new schema in this codebase passes through him for shape approval before it commits, even when the engineer leading the episode is a senior cast member.

---

## Findings

None this episode.

This was a clean cut. Format clean, build clean, all 672 unit tests green, all 35 integration tests green. No flake retries. No carried-over warnings. No "we'll fix it in the next pass" items. The four open questions above are triage decisions, not findings -- they have triage outcomes and disposition (defer / hers), not investigation tasks.

The findings backlog (`docs/findings/`) is unchanged from the close of E05. It will reopen in E07.

For the writers' room record: clean episodes are not common. Of the five S03 episodes shipped before this one, three landed at least one finding -- E03 (the docs audit re-prise), E04 (the secrets audit), and E05 (the meta-audit, which closed with a 50% headline number). E06 closing with zero findings is partly because the scope was narrower than an audit and partly because the lead (Kramer) treated the negative-test discipline as load-bearing rather than optional. Future implementation episodes are expected to clear the same bar. The bar is: *every positive assertion paired with a negative*, including the no-secret invariant. If a future episode ships without a negative test for the security claim it makes, that is itself a finding.

---

## Next episode preview

**S03E07 -- *The Redactor*. Lead: Newman.**

The schema landed clean. Every other log path still leaks. ADR-007 §2 mandates a centralised secret redactor on every log and exception path; today, that path is implemented in some places and skipped in others, and the test coverage for it is uneven. E07 is the security episode that closes that gap.

Newman's brief, as drafted in the writers' room and pending detail:

- Single `SecretRedactor` utility, applied at every `Console.Error.WriteLine` and every exception-message-formatting boundary.
- `Authorization: Bearer ...` patterns, `AZUREOPENAIAPI` values, anything that looks like an Azure key (regex), anything that looks like an OpenAI key (`sk-...` regex) -- all redacted to `[REDACTED]` before the bytes leave the process.
- Negative invariant tests: any new exception message added in any future episode that fails to round-trip through the redactor is a P1. Mirrors the `Save_NeverContainsApiKeyField` discipline E06 just established.
- Newman owns; FDR cameos for the adversarial test cases ("can I smuggle a key through a stack trace, through a tool error message, through a child-process stderr?"); Kramer and Elaine on standby for the implementation polish and the security guide update.

E07 also picks up Q4 from this episode if Elaine's docs episode has not already shipped by then.

---

## Sign-off

The seam exists. The schema is the schema. 672 green, 15 new. Newman approved the no-secret invariant. Costanza approved the shape with three line-edits, two accepted at-commit and one routed to docs. The four open questions are triaged. The chain is documented; the JSON envelope is canonical; the text block keeps the chaos suite happy.

What this episode does not do, restated for the record so a future episode does not have to litigate it: it does not implement `--provider` as a CLI override; it does not add `apiKeyEnv` or `apiVersion` or `deployments[]` to `ProviderEntry`; it does not amend ADR-009; it does not retire the legacy text block; it does not update `--help`; it does not touch the chaos baseline; it does not ship a migration tool for the old `~/.azureopenai-cli.json` user-config file (that file continues to exist and contributes to endpoint resolution at layer 2.5, between env and profile, by virtue of `UserConfig` being threaded through `RunConfigShow` unchanged). All of those are deliberate omissions, scoped to later episodes, listed above.

What it does do: it gives the rest of the arc a place to put providers and profiles, with a stable on-disk shape, a stable resolution chain, a stable JSON surface, and a negative test that prevents the schema from accidentally becoming a vault. That is the brick. The next twelve episodes lay bricks on top of it.

Pretty, pretty, pretty good.

The arc resumes. E07 is Newman's. Action.

-- Larry David
   Showrunner / Director / Executive Producer

---

## Credits

- **Kramer** -- lead. `Preferences.cs` (212 lines, new), `RunConfigShow` in `Program.cs` (+170), AOT registrations in `JsonGenerationContext.cs` (+11), and the 15-test suite in `PreferencesTests.cs` (412 lines). Ran preflight clean. Mirrored the new bits into the chaos regression baseline implicitly via the `ConfigShow_TextOutput_KeepsLegacyEffectiveBlock` assertion.
- **Costanza** -- reviewer. Two lines, three nitpicks. `notes`-field guidance, `Schema = "1"` as string, ADR-009 active-profile fallback documentation. Two accepted at commit, one routed to Elaine.
- **Newman** -- cameo. Audited the no-secret invariant. Confirmed `Save_NeverContainsApiKeyField` is the contract. P1 if it ever flips. Lead next episode.
- **Elaine** -- cameo. Filed the `--config show --help` text gap as Q4. Owns the docs follow-up.
- **Mickey Abbott** -- one line. Column padding cosmetics tabled as Q2.
- **Larry David** -- showrunner. Conceived, cast, signed off, owns the orchestrator-only files (episode index, blueprint, writers' room) which update outside this writeup per `shared-file-protocol`.

`Co-authored-by: Copilot` trailer affirmed for the E06 commit when it ships.

---

> *"The drawer exists. Empty drawer. Locked. With a label. The providers can move in next episode."* -- Kramer, sign-off, writers' room, end of day.

> *"Don't make the schema a vault. Vaults are vaults; registries are registries. The day someone confuses the two is the day we are on the front of Hacker News for the wrong reason."* -- Newman, on the no-secret invariant.

> *"E07 starts tomorrow. Action."* -- Showrunner.
