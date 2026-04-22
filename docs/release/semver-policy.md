# SemVer policy -- Azure OpenAI CLI

<!-- markdownlint-disable-file MD051 -->
<!-- §-anchors follow GitHub's slug algorithm; markdownlint's heuristic differs. Links verified against github.com render. -->

> "We ship with discipline. The version number is a contract. Break it,
> and we hear about it for the next three releases." -- Mr. Lippman

Audience: anyone cutting a tag or reviewing a release PR. This page
makes [SemVer](https://semver.org/spec/v2.0.0.html) concrete for
**this** project. If a change isn't covered here, default to the more
conservative bump and note it in the release PR.

**Scope.** Applies to the v2 line (`azureopenai-cli-v2/`, binary
`az-ai-v2`, GHCR image `…/az-ai-v2`). The v1 line is in patch-only
maintenance mode; see [§7](#7-v1-line-maintenance-mode).

Companion docs:

- [`pre-release-checklist.md`](pre-release-checklist.md) -- the gate
  every release runs through.
- [`ghcr-tag-lifecycle.md`](ghcr-tag-lifecycle.md) -- OCI tag policy.
- [`artifact-inventory.md`](artifact-inventory.md) -- what ships per RID.
- [`../CHANGELOG-style-guide.md`](../CHANGELOG-style-guide.md) -- prose
  rules for the CHANGELOG entries that document each bump.
- [`../runbooks/release-runbook.md`](../runbooks/release-runbook.md) --
  how to actually cut the tag.
- [`../runbooks/packaging-publish.md`](../runbooks/packaging-publish.md)
  -- Bob's tap/bucket publish flow (runs after the tag).

---

## 1. The rule in one paragraph

Given `MAJOR.MINOR.PATCH`:

- **MAJOR** -- we broke a user-visible contract on purpose. Users must
  read the migration note before upgrading.
- **MINOR** -- we added a user-visible capability. Existing invocations,
  configs, and scripts keep working unchanged.
- **PATCH** -- we fixed a bug, rolled a dependency, or shipped docs.
  No new capability, no contract change.

When in doubt, bump higher. A too-high bump costs us a line in the
CHANGELOG; a too-low bump costs us a user's trust.

---

## 2. What counts as a "user-visible contract"

The following surfaces are **load-bearing**. A breaking change to any
of them is a MAJOR bump.

1. **CLI flag set and semantics** -- flag names, short forms, required
   vs. optional, default values, accepted value grammar.
2. **Exit codes** -- `0` success, `1` generic failure, `2` usage error,
   `130` SIGINT, and any documented per-command code (e.g. Ralph's
   iteration-exhausted `1`).
3. **Output format on stdout** when a machine-oriented flag is in
   effect -- `--raw`, `--json`, `--short`. Output when no such flag is
   set is best-effort prose; we can tighten it at MINOR.
4. **Persisted config schema** -- `~/.azureopenai-cli.json`
   (`UserConfig`). Field renames, type changes, required-vs-optional
   flips, and removal are all MAJOR.
5. **Environment variables we read** -- `AZUREOPENAIAPI`,
   `AZUREOPENAIENDPOINT`, `AZUREOPENAIMODEL`, and any documented
   `AZUREOPENAI_*` / `AZ_AI_*` knob. Removing or renaming is MAJOR.
6. **Persona / squad / prompt YAML schema** under
   `docs/prompts/` and friends -- field names, required keys, resolution
   order. Personality wording changes are NOT schema changes (see §4).
7. **Telemetry schema** -- OTel attribute names and semantics
   (`service.name`, `service.version`, Ralph span names, cost
   attributes). Renames are MAJOR; new additive attributes are MINOR.
8. **OCI image tag scheme and image entrypoint** -- see
   [`ghcr-tag-lifecycle.md`](ghcr-tag-lifecycle.md). Changing
   `ENTRYPOINT` in a way that breaks `docker run …/az-ai-v2 --help` is
   MAJOR.
9. **Release-artifact naming** -- the
   `az-ai-v2-<version>-<rid>.{tar.gz,zip}` pattern. Downstream
   consumers (Homebrew, Nix, Scoop) derive URLs from this. Renaming is
   MAJOR. (See also audit C-1.)
10. **Binary name(s)** -- `az-ai-v2`, `az-ai` (v1). Renaming the binary
    is MAJOR.

Anything **not** on this list is internal and can change at PATCH or
MINOR at the author's discretion, subject to review.

---

## 3. Decision table

| Change type                                                       | Bump  | Notes                                                                        |
|-------------------------------------------------------------------|:-----:|------------------------------------------------------------------------------|
| Remove a CLI flag                                                 | MAJOR | Even if deprecated first -- the removal is the MAJOR.                         |
| Rename a CLI flag (alias the old name for ≥1 MINOR)               | MINOR | Removal later is MAJOR. Deprecation notice in CHANGELOG `### Deprecated`.    |
| Add a CLI flag (additive, default preserves old behavior)         | MINOR |                                                                              |
| Change a flag's default value in a user-observable way            | MAJOR | Call out loudly. Migration note required.                                    |
| Change exit code for an existing failure mode                     | MAJOR | e.g. Ralph `fdr-v2-ralph-exit-code` would have been MAJOR if it had shipped. |
| Add a new exit code for a new failure mode                        | MINOR | Documented in `--help` or persona docs.                                      |
| Change `--raw` / `--json` / `--short` output format               | MAJOR | Stdout contract on machine flags is frozen.                                  |
| Tighten prose output wording (default mode, no machine flag)      | PATCH | Users should not be parsing default prose.                                   |
| Add a field to `--json` output                                    | MINOR | Additive only. Readers must tolerate unknown fields.                         |
| Add a field to `UserConfig` (optional, default preserves old)     | MINOR | Document the default. PATCH if purely internal and never serialized.         |
| Rename/retype a `UserConfig` field                                | MAJOR | Provide a migration path -- upgrade-on-load or a migration command.           |
| Read a new env var (additive)                                     | MINOR |                                                                              |
| Stop reading an env var / change its semantics                    | MAJOR |                                                                              |
| Add a persona / squad / prompt                                    | MINOR | New YAML file, schema unchanged.                                             |
| Change persona **prompt text** (wording, tone, examples)          | MINOR | See §4. Users pin via `--persona-version` if they care.                      |
| Change persona / squad **YAML schema**                            | MAJOR | Field renames, required-vs-optional flips.                                   |
| Add a tool to a persona's tool-surface                            | MINOR | Additive capability.                                                         |
| Remove a tool from a persona's tool-surface                       | MAJOR | User scripts that invoked the tool break silently otherwise.                 |
| Add an OTel attribute                                             | MINOR |                                                                              |
| Rename / remove an OTel attribute                                 | MAJOR | Dashboards break.                                                            |
| Bump a dependency, patch range (`X.Y.Z` → `X.Y.Z+1`)              | PATCH | Default. Exception: if the bump ships a visible behavior change, treat as the visible change demands.  |
| Bump a dependency, minor range                                    | PATCH | Same as above.                                                               |
| Bump a dependency, major range                                    | MINOR | Unless the dep change cascades into our own contract -- then MAJOR.           |
| Bump .NET runtime (e.g. `net10.0` → `net11.0`)                    | MAJOR | Affects AOT binary, image base, RID matrix. Always MAJOR.                    |
| Bump Docker base image tag within the same distro/libc            | PATCH |                                                                              |
| Switch Docker base image distro or libc                           | MINOR | e.g. Debian → Alpine musl. User-visible ABI shift for volume-mounted tools.  |
| Drop a RID from the published artifact matrix                     | MAJOR | See v2.0.4 dropping `osx-x64`. Document fallbacks (Rosetta, Docker, source). |
| Add a RID to the published artifact matrix                        | MINOR |                                                                              |
| Rename the release artifact filename pattern                      | MAJOR | Downstream URL consumers 404. (This is what C-1 almost did.)                 |
| Change GHCR image tag scheme                                      | MAJOR | See [`ghcr-tag-lifecycle.md`](ghcr-tag-lifecycle.md).                        |
| Fix a bug with no behavior change beyond "it now works"           | PATCH |                                                                              |
| Fix a bug where the old (broken) behavior was load-bearing        | MINOR | If anyone could have relied on the bug, flag the fix in `### Changed`.       |
| Security fix with no contract change                              | PATCH | Entry under `### Security`.                                                  |
| Security fix that removes a CLI flag or endpoint                  | MAJOR | Contract break is the contract break; severity doesn't downgrade the bump.   |
| Docs-only change                                                  | PATCH | Or no release at all if it can ride the next PATCH.                          |
| Packaging-only change (Homebrew/Nix/Scoop manifest hash-sync)     | none  | Post-release hash-sync lands on the same tag; does not bump.                 |

---

## 4. Persona / prompt-text changes -- the squishy case

Persona prompts live in `docs/prompts/` and are loaded at runtime.
Wording changes behave, from the outside, like a model change: the
same flags produce subtly different output. Formal rule:

- **Tone, examples, and clarifications** -- MINOR. Document the diff
  in `### Changed` with a one-line summary; do not paste the full
  prompt diff into the CHANGELOG.
- **Adding a new tool callout or workflow step** -- MINOR.
- **Removing a workflow step that users might script against**
  (e.g. "persona always emits a `--- SUMMARY ---` block") -- MAJOR.
- **Renaming a persona** -- MAJOR. Alias the old name at MINOR first
  if you have the runway.

A prompt edit that only changes punctuation or fixes a typo is PATCH.

---

## 5. Worked examples from the CHANGELOG

These are real entries you can cross-reference to calibrate.

### 5.1 MAJOR -- v2.0.0

The v2 cutover bumped MAJOR because it:

- Introduced a second binary namespace (`az-ai-v2`) with a different
  image and different tarball-filename pattern.
- Redrew the RID matrix (new `linux-musl-x64`, eventually dropped
  `osx-x64`).
- Introduced the v2 OTel schema.
- Changed the persisted-config contract in non-backwards-compatible
  ways.

Anything one of those on its own would have justified MAJOR; all four
together was obvious.

### 5.2 MAJOR -- v2.0.4 dropping `osx-x64`

Per the [2.0.4] entry, we dropped `osx-x64` from the official artifact
matrix. That is a RID-matrix removal -- per §3, MAJOR. We shipped it
at PATCH (`2.0.3 → 2.0.4`) because:

1. The functional fallback is excellent (Rosetta 2, Docker,
   build-from-source).
2. v2.0.3 never published a GitHub Release, so no `osx-x64` v2.x
   binary was ever downloadable from a Release page.
3. The release notes call the drop out loudly, in the top banner,
   with three documented fallback paths.

**Lesson:** we chose to bend the rule with eyes open and a migration
story. Do not use this as a precedent; the bar for bending is
"no user of the removed thing can possibly have shipped yet."

### 5.3 MINOR -- additive CLI flags and personas

Any release that added a new persona (e.g. the Ralph squad additions
across 1.x) or a new additive flag should have been MINOR. The v1
line batched a lot of these into 1.5 / 1.6 / 1.7 MINORs -- that is the
correct cadence.

### 5.4 PATCH -- v2.0.2 Dockerfile AOT fix

[2.0.2] fixed the Dockerfile `--no-restore` AOT asset-graph bug. No
flag changed, no config changed, no OTel attribute moved. The only
user-visible effect was "the GHCR image now exists." PATCH.

### 5.5 PATCH -- v2.0.5 version-string drift fix

[2.0.5] rolls `Program.VersionSemver`, `Telemetry.ServiceVersion`,
and `stage.sh:VERSION` to match the tag, and adds a contract test.
The **fix** is a behavior change (`--version` reports the right
string), but the **contract** is "`--version` reports what the tag
says" -- which was always the intent. PATCH.

### 5.6 PATCH -- dependency bumps

`.NET` SDK patch bumps, Azure SDK patch bumps, CycloneDX tool bumps
-- all PATCH unless they cascade into a visible change in our own
surface.

---

## 6. Pre-1.0 and pre-release identifiers

We are past 1.0. This project does not ship `0.x` tags; any future
"experimental" work happens on a branch, not with a `0.` version.

Pre-release identifiers (`-rc.1`, `-beta.1`) are permitted for staging
a MAJOR. Rules:

- Pre-release tags are cut from the release branch, signed, and
  never retagged.
- Pre-release tags do **not** move the `latest` GHCR tag (see
  [`ghcr-tag-lifecycle.md`](ghcr-tag-lifecycle.md)).
- Pre-release CHANGELOG entries live under `[X.Y.Z-rc.N]` and are
  collapsed into the final `[X.Y.Z]` entry at GA -- do not duplicate
  line items.
- The pre-release tag's GitHub Release is marked "Pre-release" in
  the UI.

We have not actually used a pre-release tag yet. Don't be the first
without reading this section twice.

---

## 7. v1 line -- maintenance mode

The v1 line (`azureopenai-cli/`, `az-ai`, image `…/azure-openai-cli`)
is **PATCH-only** as of v2.0.0. Policy:

- Security fixes, runtime fixes, and docs -- PATCH (`1.9.1 → 1.9.2`).
- No new features, no new flags, no new personas on v1.
- Anything that would be a MINOR on v2 gets declined on v1 with a
  pointer to the v2 migration guide (`docs/migration-v1-to-v2.md`).
- If a v1 change would be MAJOR on v2's rules, we do not ship it on
  v1 at all -- we end-of-life v1 first.

---

## 8. How a SemVer decision gets made

1. Author proposes a bump in the release PR description with a
   one-paragraph justification citing this page.
2. Reviewer validates against §2 and the §3 table.
3. If the two disagree, Lippman casts the tie-break and writes a note
   in the release PR explaining which rule applied.
4. The chosen bump, the justification, and any bent rule land in the
   CHANGELOG entry -- either inline in the release-note banner
   (for MAJOR / notable MINOR) or in the commit message for PATCH.

There is no SemVer bureaucracy beyond this. The table is the law;
the tie-break is documented; we move on.

-- Mr. Lippman, release management
