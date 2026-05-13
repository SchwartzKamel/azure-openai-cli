# ADR-012: Model Registry Seam

## Status

Accepted -- 2026-05-13 (S04E01)

## Context

S03 (*Local & Multi-Provider*) broke the implicit assumption that `az-ai` talks to
exactly one Azure OpenAI endpoint. After S03, a single binary can route prompts to
Azure OpenAI, Azure AI Foundry, and (in the local-provider track) a llama.cpp
server. The instant a user has more than one model visible, one question displaces all
others: *which model for this prompt, right now?*

Three concrete gaps make a structured registry urgent at the start of S04:

1. `--doctor` knows nothing about models. It reports env-var presence but cannot
   tell the user which models are registered, what they support, or whether the
   necessary env-vars for each provider are set. A new user whose `--doctor` output
   shows "configured" still has no idea what the model can do.
2. The smart-defaults engine planned for E05 needs somewhere to anchor. Without a
   typed data structure it has no capability surface to query, no cost tier to prefer,
   and no context window to respect. Building E05 on free strings or a hardcoded
   array is a dead end.
3. The capability-tag vocabulary must be locked before E03 (*The Capabilities*)
   builds the `--capabilities` subcommand. Free strings in E01 force E03 to reopen
   this file and risk a flag-day rename.

`docs/exec-reports/s04-blueprint.md` Act I frames this as "plant the seam first":
a typed data structure backed by an embedded JSON resource, with a stable schema that
Acts II and III can consume without reopening the implementation.

## Decision

Introduce a `ModelRegistry` loader backed by an embedded JSON resource
(`azureopenai-cli/Registry/registry.json`) and an optional user-override file at
`~/.config/az-ai/registry.json`.

Key design points:

- **Typed record:** `ModelRegistryEntry` is a C# `record` with fields `name`,
  `provider`, `capabilities`, `contextWindow`, `costTier`, and optional `cardPath`.
  All fields are AOT-serializable via `AppJsonContext`.
- **Capability tags closed set:** `ModelCapability.AllowedTags` is the single source
  of truth for valid capability strings. The E01 baseline set is:
  `tool_calls`, `vision_in`, `vision_out`, `json_mode`, `streaming`, `system_prompt`.
  Any tag not in `AllowedTags` causes the registry load to exit with `rc=99`. This
  is intentionally strict. E03 (*The Capabilities*) adds `--strict-registry` if a
  graceful-degradation path is needed; strict is correct for E01.
- **User override replaces, does not merge:** if `~/.config/az-ai/registry.json`
  exists, it replaces the embedded seed list entirely. There is no deep-merge
  semantics. This is the simplest model and the one least likely to produce
  surprising results when a user experiments with a local model. See Risk row 4 in
  the episode brief.
- **Card paths resolve relative to `AppContext.BaseDirectory`:** the binary may run
  from an arbitrary working directory. Resolving card paths from the binary's own
  directory (typically `~/.local/bin/`) rather than `CWD` ensures `--doctor` can
  show a consistent resolved path. A missing card path is a warning, not a fatal
  error; the registry entry is still returned.
- **Load happens after `LoadConfigEnvFrom()` at startup:** provider env-var status
  in `--doctor` is evaluated against the loaded environment, so auto-config must run
  first.

## Alternatives considered

### (a) Hardcoded `static readonly` C# array

A `static readonly ModelRegistryEntry[]` in `Program.cs` gives AOT-safe access with
zero file I/O overhead. Rejected because the user cannot override it without
recompiling the binary. Any user who needs a local model or a Foundry deployment not
in the seed list is stuck. The registry's most important property is that it is
user-extensible without a rebuild.

### (b) External JSON file only (no embedded seed)

Ship no embedded data; require the user to create `~/.config/az-ai/registry.json`
manually. Rejected on two grounds: (1) zero-config UX is a first-class `az-ai`
requirement -- a fresh install must work without manual file creation; (2) the
offline-first property (binary works without network access) depends on embedded
seed data being present. An absent file at startup would either degrade gracefully
(empty registry, useless `--doctor`) or fail fatally (rc=99, broken UX).

### (c) Free-string capability tags

Store capability tags as arbitrary strings with no validation. Rejected because E03
(*The Capabilities*) builds a query subcommand on top of this vocabulary. Without a
closed set, E03 must defensively handle any string a user or future agent writes into
a registry file, and the flag-day cost of renaming a tag after E03 ships is high.
Locking the vocabulary now costs one enum amendment per new tag, which is cheap
compared to a migration.

## Consequences

### Positive

- E03 (*The Capabilities*), E05 (*Smart Defaults*), E17 (*Cost Visibility*), and
  E21 (*Routing Rules*) all have a typed, structured data source to anchor against.
  None of those episodes need to solve the "where does model metadata live?" question.
- `--doctor` becomes genuinely useful for new users: it lists every registered model,
  its capability surface, and its provider configuration status in a single command.
- The user-override file enables local-model experimentation without recompiling or
  modifying the source tree.

### Negative

- The capability set is closed. Adding a new tag (`vision_in`, a future
  `audio_in`, etc.) requires amending `ModelCapability.AllowedTags` and appending a
  migration note to this ADR. Each amendment is cheap in isolation but accumulates if
  the tag set grows rapidly. This ADR will be annotated, not superseded, on each
  amendment.
- The override-replaces-seed semantic means a user who wants to extend the seed list
  must copy all seed entries into their override file. There is no partial override.
  This may be revisited in a later episode if the friction proves meaningful.

### Neutral

- AOT serialization burden falls on `AppJsonContext` in
  `azureopenai-cli/JsonGenerationContext.cs`. Kramer must register
  `ModelRegistryEntry` and `ModelRegistryEntry[]` there; otherwise the AOT linker
  will trim the type. This is consistent with every other serializable type in the
  codebase and adds no new pattern.

## Deferred to later episodes

| Item | Episode |
|------|---------|
| Smart-defaults engine consuming registry capability tags | E05 |
| `--capabilities` query subcommand | E03 |
| In-binary card embedding (card prose as embedded resource) | E02 |
| Rate-card pricing metadata in registry entries | E17/E18 |
| Routing rules driven by capability tags | E21 |
| `--strict-registry` flag to downgrade rc=99 to a warning | E03 |

## Adversarial review (FDR, S04E01 Wave 2)

> Red team pass on the registry seam as shipped in Wave 1. Newman owns
> defense; FDR owns attack. Findings below are sorted by severity then
> verified-status. Speculative findings are flagged.

### Findings

#### F-01 -- cardPath stored verbatim, path traversal latent before E02 -- [HIGH] [speculative]

**Surface:** `cardPath` field in user override or seed registry.json

**Attack:** The loader stores whatever string appears in `cardPath` without any
validation -- no canonicalization, no prefix check, no rejection of relative
path traversal segments. An override file containing
`"cardPath": "../../../../../../etc/passwd"` loads cleanly (WARN only if the
path is missing, not if it is present). E02 plans to machine-read card files
from `cardPath`; if that read is implemented with `Path.Combine(AppContext.BaseDirectory, entry.CardPath)`
without a subsequent prefix check, it becomes a read-arbitrary-file vulnerability
on any file the `az-ai` process can open. The blast radius is currently zero
because E01 never reads `cardPath`, but the footgun is loaded and the E02
implementer may not know to look for it.

**Fix sketch:** Add path validation in `ValidateEntries` -- reject entries
where `Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, cardPath))`
does not start with `AppContext.BaseDirectory`.

---

#### F-02 -- Terminal injection via entry.Name in --doctor output -- [MEDIUM] [partial]

**Surface:** `WriteRegistrySection` in Program.cs (~line 3662-3671)

**Attack:** `e.Name`, `e.Provider`, and `e.Capabilities` array values are
concatenated into `stdout.WriteLine(line)` without any sanitization. A user
registry override with `"name": "\x1b[2J\x1b[H\x1b[31mPWNED\x1b[0m"` (ANSI
clear-screen + red banner) or an OSC 8 hyperlink sequence will be printed
verbatim when the user runs `az-ai --doctor`. Any terminal that interprets
ANSI/OSC sequences (xterm, Windows Terminal, iTerm2) will execute the
injection. The attacker must plant the file at `~/.config/az-ai/registry.json`,
which requires write access to the user's home directory -- a prerequisite that
keeps severity below HIGH, but in shared-workstation or CI environments where
home directories are writable by a build script, this is plausible.

**Fix sketch:** Strip or reject C0/C1 control characters (bytes 0x00-0x1F and
0x7F-0x9F) from Name, Provider, and capability tags before printing; one-liner
regex replace before `Pad()` calls.

---

#### F-03 -- Unbounded file read on user override (memory exhaustion) -- [MEDIUM] [plausible]

**Surface:** `ApplyUserOverride`, line 87: `var json = File.ReadAllText(userPath);`

**Attack:** `File.ReadAllText` reads the entire file into a single `string`
before parsing. There is no size cap. A file at `~/.config/az-ai/registry.json`
that is 2 GB (trivially created with `dd` or `fallocate`) will cause the
runtime to attempt a 2 GB string allocation. On a machine with limited RAM
this triggers OOM; on a machine with swap it causes extreme latency and may
bring `az-ai` startup to a halt. Because startup is synchronous and the load
happens before `ParseArgs`, there is no `--timeout` escape hatch. The catch
block rescues the seed list only after the allocation attempt, so the damage
is done before recovery. Low attacker-surface in single-user setups; higher in
containers where the home directory is volume-mounted.

**Fix sketch:** Check `new FileInfo(userPath).Length` before reading; reject
with WARN and fall back to seed if size exceeds a reasonable cap (e.g., 1 MB).

---

#### F-04 -- FIFO or device-file hang on user override -- [MEDIUM] [plausible]

**Surface:** `ApplyUserOverride`, lines 82-87: `File.Exists` then `File.ReadAllText`

**Attack:** On Linux, `File.Exists` follows symlinks and returns `true` for a
symlink pointing to a named pipe (FIFO) or a block device such as `/dev/zero`.
`File.ReadAllText` on a FIFO with no writer blocks indefinitely; on `/dev/zero`
it reads forever, combining with F-03 for OOM. The surrounding `catch (Exception ex)`
block does not help because neither call ever throws -- they simply block. An
attacker who can write a symlink at `~/.config/az-ai/registry.json` pointing to
a FIFO they control can hang `az-ai` at startup on every invocation (including
Espanso and AHK expansion calls, where the user has no visible process to kill).
Remediation for F-03 (size cap) partially mitigates this because `FileInfo.Length`
on a FIFO may return 0 or error, but a dedicated FIFO guard is safer.

**Fix sketch:** Open the file with a short `ReadTimeout` or check
`File.GetAttributes(userPath)` against `FileAttributes.Normal | FileAttributes.Archive`
and skip non-regular files before reading.

---

#### F-05 -- Duplicate name entries cause undefined routing for E05 -- [MEDIUM] [speculative]

**Surface:** `ApplyUserOverride` / `ValidateEntries` -- no deduplication

**Attack:** The loader deserializes the override file as a flat array with no
uniqueness constraint on the `name` field. An override file with two entries
both named `"gpt-4o-mini"` passes validation and populates `RegistryEntries`
with both. `WriteRegistrySection` prints both rows, confusing the user. More
critically, E05's smart-defaults engine and E03's `--capabilities` query will
query the registry by name; if the lookup uses `FirstOrDefault` it silently
ignores the second entry, and if it uses `Where` it processes both, potentially
applying contradictory capability sets to the same model. The embedded seed
cannot produce duplicates, but the user override mechanism makes this easy to
produce by accident (e.g., copy-paste a block and forget to rename it).

**Fix sketch:** In `ValidateEntries`, detect duplicate `name` values and treat
them as rc=99 (or at least a WARN) so the ambiguity is surfaced at load time
rather than at routing time.

---

#### F-06 -- NullReferenceException in --doctor when name or provider is omitted -- [LOW] [partial]

**Surface:** `WriteRegistrySection` -> `Pad(string s, int width)` at line 3634-3638

**Attack:** `Pad` is declared with a non-nullable `string s` parameter and
immediately calls `s.Length` with no null guard. If a user override entry omits
the `name` field, `System.Text.Json` source-gen deserialization sets `Name` to
`null` (no `[JsonRequired]` attribute on the record constructor). `ValidateEntries`
does not check for null `Name` -- it only checks capabilities and `CardPath`. When
`WriteRegistrySection` calls `Pad(e.Name, 16)`, the NullReferenceException is
unhandled in the `--doctor` branch (no enclosing try-catch at lines 1356-1379)
and crashes the process with an ugly unhandled-exception exit rather than a clean
rc=99. The `--doctor --json` path is immune because it skips `WriteRegistrySection`.

**Fix sketch:** In `ValidateEntries`, add a null/empty check on `entry.Name` and
`entry.Provider` and exit rc=99 with a clear message; or null-coalesce in `Pad`
call sites: `Pad(e.Name ?? "(null)", 16)`.

---

#### F-07 -- LoadEmbedded has no try/catch around JSON parse -- [LOW] [speculative]

**Surface:** `LoadEmbedded` in ModelRegistry.cs, line 71

**Attack:** `JsonSerializer.Deserialize(json, AppJsonContext.Default.ModelRegistryEntryArray)`
is not wrapped in a try/catch. If a developer merges a malformed `registry.json`
(e.g., a trailing comma, an incorrect type for `contextWindow`) the embedded
resource will be present (so the null-stream guard does not fire) but
deserialization will throw `JsonException`. The exception propagates unhandled
through `Load()` all the way to the top-level catch in `Main`, producing a
stack trace on stderr rather than the `[ERROR] ... rc=99` convention the rest
of the codebase follows. This is a developer-time risk rather than a runtime
attack, but CI does not currently run a dedicated test that deserializes the
embedded resource through the AOT path, so a malformed seed could ship.

**Fix sketch:** Wrap the `Deserialize` call in a try/catch and call
`ErrorAndExit` on `JsonException`, consistent with the convention used in
`ApplyUserOverride`.

---

#### F-08 -- JSON literal null in override file silently falls back to seed -- [LOW] [plausible]

**Surface:** `ApplyUserOverride`, line 90: `return overrideEntries ?? seed;`

**Attack:** A file at `~/.config/az-ai/registry.json` containing the single
JSON value `null` is syntactically valid JSON; `JsonSerializer.Deserialize`
returns `null` (not an exception). The expression `overrideEntries ?? seed`
silently returns the embedded seed list with no WARN message. A user who
creates a null-content file expecting to "clear" the registry (reasonable
interpretation) will be puzzled when `--doctor` still lists all three seed
models. This is not a security issue but is a fidelity gap: the override is
supposed to REPLACE, not fall through. The same silent fallback also occurs
for a 0-byte file (which throws `JsonException`, caught, returns seed with a
WARN) -- so the two zero-content states behave differently.

**Fix sketch:** After deserialization, if `overrideEntries` is null, emit a
WARN ("user registry parsed to null; falling back to embedded seed") and
return seed, making the fallback visible.

---

#### F-09 -- Capability tag error message omits case-sensitivity hint -- [NIT] [verified]

**Surface:** `ValidateEntries` in ModelRegistry.cs, line 110-116

**Attack:** `AllowedTags` uses `StringComparer.Ordinal`, so `"Streaming"`,
`"STREAMING"`, and `"streaming "` (trailing space) are all rejected with
rc=99. The error message reads: `"Unknown capability tag 'Streaming' in
registry entry 'my-model'. Allowed tags: tool_calls, ..."`. It does not
state that tags are case-sensitive or that whitespace is not trimmed. A
user hand-authoring an override file and typing `"Streaming"` or copying
a tag from documentation that used title case will receive an opaque error.
This is verified: `ModelCapability.AllowedTags` is a `HashSet<string>` with
`StringComparer.Ordinal` and `IsValid` does a direct `Contains` with no
normalization.

**Fix sketch:** Append to the error message: "(tags are case-sensitive and
must match exactly)" -- one-line change in `ValidateEntries`.

---

### Closing assessment

No CRITICAL findings. The seam is small and the embedded-resource path is
well-hardened. The most actionable pre-release finding is F-01: the path
traversal is latent today but will become a real vulnerability the moment
E02 adds file reads from `cardPath`. The E02 implementer must add a prefix
check before that read lands. F-02 (terminal injection) and F-06 (NullReferenceException
in `--doctor`) are both low-effort fixes with clear payoffs and should be
addressed before v2.3.0 is announced publicly. F-03 and F-04 are worth a
one-liner guard each. F-05 through F-09 can be deferred to E02 or the
findings backlog.

**Newman's triage queue (priority order):** F-01 (E02 blocker note), F-02,
F-06, F-04, F-03, F-05, F-07, F-08, F-09.
