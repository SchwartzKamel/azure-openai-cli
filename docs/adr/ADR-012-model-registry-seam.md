# ADR-012: Model Registry Seam

## Status

Accepted -- 2026-05-13 (S04E01)

## What is a "seam"?

A **seam** is a deliberate boundary in the code where one part of the system
can be changed -- or a new part plugged in -- without reopening the parts on
the other side. In this ADR the seam is the `ModelRegistry` loader and its
typed `ModelRegistryEntry` record: later episodes (smart defaults, the
`--capabilities` query, routing rules) read from the registry through that
boundary, so they do not have to know how registry data is stored, validated,
or overridden. The term is borrowed from Michael Feathers, *Working
Effectively with Legacy Code* (2004), and is used in the same sense by Mark
Seemann, *Dependency Injection in .NET* (2011): a place where behaviour can
vary without editing the call site.

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

## Adversarial review (FDR, S04E02 Wave 2)

**Date:** 2026-05-13
**Scope:** ModelCard reader (`ReadCard`, `ReadCardOrThrow`, `LoadCards`,
front-matter parser, `IsRegularFile` libc shim) and the Wave 2 wiring of
`LoadCards` into `WriteRegistrySection` / `ResolveRegistryDir`.
**Findings:** 10 (1 CRITICAL, 1 HIGH, 4 MEDIUM, 2 LOW, 2 NIT)

> **FIX-NOW (block episode close):** F-EE-01 -- a parent-directory symlink
> inside `registryDir` defeats the F-01 prefix guard. Verified end-to-end
> with a 30-line PoC. Read-arbitrary-file primitive on any path the
> `az-ai` process can open. Newman owns the fix.

### Top findings

**1. F-EE-01 (CRITICAL, verified) -- the prefix guard only canonicalises
`..`, it does not resolve symlinks.** `Path.GetFullPath` collapses
`./` and `..` segments lexically; it never calls `realpath(3)`. And
`File.ResolveLinkTarget(resolved, returnFinalTarget: false)` only
inspects the leaf -- it returns null when the file itself is a regular
file even if a parent directory in `resolved` is a symlink. Ship a
symlink at `<registryDir>/sub -> /etc` and a card entry with
`cardPath: "sub/passwd"` and the read goes through. Reproduced in 5
seconds outside the test harness; PoC noted in detailed findings.

**2. F-EE-04 (HIGH, partial) -- `AZ_AI_REGISTRY_DIR` is honoured with
no canonicalisation, no allowlist, and no symlink rejection.** Combined
with F-EE-01 the env-var becomes a one-step "anchor anywhere" knob:
attacker exports `AZ_AI_REGISTRY_DIR=/some/dir/they/wrote`, drops a
`docs/model-cards/azure-gpt-4o-mini.md` symlink under it, and `--doctor`
reads whatever the symlink targets. Even without F-EE-01, the env-var
re-anchors all card lookups silently -- there is no `[INFO] using card
anchor /some/path` line, so a user invoked through Espanso with a poisoned
environment cannot tell their cards came from a foreign tree. Operator
escape hatch should at minimum log when it fires.

**3. F-EE-02 / F-EE-03 (MEDIUM, partial) -- two TOCTOU windows between
the guards and the read.** `info.Length` is captured at FileInfo
construction; `ReadAllText` opens the file later and reads to EOF. A
file that passed at 100 KB can grow to 4 GB before `open(2)` and the
256 KB cap is bypassed. Same shape for `IsRegularFile` -- stat happens
in the syscall shim, then `ReadAllText` opens the path again, so a
swap to a FIFO between the two calls hangs the process indefinitely
(exactly the failure mode F-04 was meant to prevent). Both require
write access to `registryDir`, which limits practical exploitability,
but the FIFO swap is the one Russell will hit first when his
`AZ_AI_REGISTRY_DIR` lands in user hands.

### Findings table

| ID | Severity | Title | Verified | Disposition |
|----|----------|-------|----------|-------------|
| F-EE-01 | CRITICAL | Parent-dir symlink defeats prefix guard | verified | **CLOSED** (S04E02 hotfix) |
| F-EE-02 | MEDIUM | Size-cap TOCTOU between FileInfo.Length and ReadAllText | partial | S04E03 backlog |
| F-EE-03 | MEDIUM | Type-check TOCTOU: stat then open allows FIFO swap | partial | S04E03 backlog |
| F-EE-04 | HIGH | AZ_AI_REGISTRY_DIR re-anchor with no canonicalisation/log | partial | FIX-NOW (lite) |
| F-EE-05 | MEDIUM | IsRegularFile returns true unconditionally on macOS | speculative | S04E03 backlog |
| F-EE-06 | LOW | Notes list allocates N strings, only bounded by 256 KB cap | verified | S04E03 backlog |
| F-EE-07 | LOW | Card front-matter `name` never compared to registry entry name | verified | S04E03 backlog |
| F-EE-08 | LOW | Duplicate front-matter keys silently last-wins | verified | WONT-FIX (document) |
| F-EE-09 | NIT | Lone-CR (classic Mac) line endings not normalised | verified | WONT-FIX |
| F-EE-10 | NIT | StripQuotes does not validate paired quote types | verified | WONT-FIX |

### Detailed findings

#### F-EE-01 -- Parent-directory symlink defeats prefix guard -- [CRITICAL] [verified]

**Surface:** `ReadCardOrThrow` lines 200-210 (the F-01 prefix guard) and
lines 225-228 (the symlink rejection).

**Attack payload:**

```text
mkdir -p /tmp/reg /tmp/victim
echo -e '---\nname: pwned\nprovider: evil\n---' > /tmp/victim/secret.md
ln -s /tmp/victim /tmp/reg/sub
# Then any caller:
ModelRegistry.ReadCard("sub/secret.md", "/tmp/reg")
```

**Observed behaviour:** `Path.GetFullPath(Path.Combine("/tmp/reg", "sub/secret.md"))`
returns `/tmp/reg/sub/secret.md`, which lexically starts with the
canonical registry directory plus separator; the prefix guard passes.
`File.ResolveLinkTarget("/tmp/reg/sub/secret.md", returnFinalTarget: false)`
returns null because the leaf is a regular file -- only `/tmp/reg/sub`
is a symlink, and the .NET API only inspects the leaf. `IsRegularFile`
calls `stat(2)` (which follows symlinks) and reports the eventual
`/tmp/victim/secret.md` is regular. ReadAllText then reads from
`/tmp/victim/secret.md`, which is outside `registryDir`. End-to-end
PoC confirmed:

```text
regFull   = /tmp/reg
resolved  = /tmp/reg/sub/secret.md
prefix-ok? True
ResolveLinkTarget(file)=(null)
-> bypasses guard: True
```

**Expected behaviour:** Any path component being a symlink that points
outside `registryDir` should be rejected with rc=99.

**Recommended fix:** Resolve symlinks before the prefix check. The
.NET-portable form is `File.ResolveLinkTarget(resolved, returnFinalTarget: true)`
applied to each parent directory and the leaf, then re-running the
`StartsWith(registryWithSep, Ordinal)` test against the realpath. On
Linux/macOS the simpler call is `realpath(3)` via the existing libc
P/Invoke seam (`LibcStat` already P/Invokes libc -- add a
`LibcRealpath` sibling). Either approach closes both directly-symlinked
files (already rejected by the leaf check) and parent-dir symlinks
(currently bypassed) under one rule.

**Blast radius:** Read-arbitrary-file as the `az-ai` process user.
Triggered by anyone who can drop a symlinked subdirectory inside
`registryDir`. The default `registryDir` is `~/.config/az-ai/` (when
the user override exists) or the repo root (when running from the
repo) -- neither is normally writable by a non-owner. But:
`AZ_AI_REGISTRY_DIR` (F-EE-04) lets an attacker who controls the
environment point at any directory; combined with this finding, that
is a full read primitive without ever needing to write to `~/.config`.

**Disposition:** **FIX-NOW**. Block S04E02 episode close on this. The
fix is one P/Invoke or one `ResolveLinkTarget(returnFinalTarget: true)`
loop -- well within Kramer's wave-3 scope.

**Status: CLOSED in S04E02 hotfix** (Newman). Mitigation: canonicalise
both `registryFull` and `resolved` through `realpath(3)` on Linux
(new `LibcRealpath` P/Invoke alongside the existing `LibcStat` seam),
with a per-ancestor `Directory.ResolveLinkTarget(returnFinalTarget:
true)` walk as the cross-platform fallback, before re-running the
`StartsWith` prefix check. macOS via the ancestor walk is best-effort
and tracked as F-EE-05. Regression tests:
`ReadCard_ParentDirectorySymlink_ExitsRc99` and
`ReadCard_LeafSymlinkOutsideDir_ExitsRc99` in `RegistryTests.cs`.

---

#### F-EE-02 -- Size-cap TOCTOU between FileInfo.Length and ReadAllText -- [MEDIUM] [partial]

**Surface:** `ReadCardOrThrow` lines 212-248. `info.Length` is read at
FileInfo construction; `File.ReadAllText` reopens the path on line 253.

**Attack scenario:** Concurrent writer keeps a file at 100 KB while the
guard runs, then `truncate(2)` or appends to make it 4 GB before the
`open(2)` inside ReadAllText. ReadAllText reads the file to EOF with no
further size check. The 256 KB cap (F-03) is bypassed.

**Observed/expected:** Cap should hold against a racing writer. Today
it does not.

**Recommended fix:** Open the file once with a `FileStream`, run
`fstat(2)` on the open fd (avoids TOCTOU because fstat consults the
inode the fd already holds), then read at most `MaxCardBytes` bytes via
a length-bounded loop. The same fd also services the type check (use
`fstat`'s `st_mode`) -- folds F-EE-03 into the same fix.

**Blast radius:** Memory exhaustion / OOM on the host running `az-ai`.
Requires a race-window writer with write access to `registryDir`.

**Disposition:** S04E03 backlog. The fd-then-fstat refactor is the
right shape for both F-EE-02 and F-EE-03 and deserves its own commit.

---

#### F-EE-03 -- Type-check TOCTOU: stat then open allows FIFO swap -- [MEDIUM] [partial]

**Surface:** `ReadCardOrThrow` lines 230-260. `IsRegularFile` calls
libc `stat(2)` on the path; ReadAllText calls `open(2)` on the same
path moments later.

**Attack scenario:** Attacker watches `registryDir` with `inotify`.
On the read of the card path's stat, the file is a regular file and
passes. Before `open(2)` runs, the attacker swaps the path: `unlink`
the regular file and `mkfifo` in its place. ReadAllText opens the
FIFO; with no writer attached, the read blocks indefinitely. Identical
failure mode to the FIFO-hang the F-04 guard was meant to prevent.

**Observed/expected:** Type guard should bind to the file actually read.

**Recommended fix:** Same as F-EE-02 -- open once, `fstat` on the open
fd, then read. Eliminates the swap window entirely.

**Blast radius:** Process hang during `--doctor` (and any future
caller of `ReadCard` / `LoadCards`). Requires write access to
`registryDir`. Compounds with F-EE-04: an attacker who controls
`AZ_AI_REGISTRY_DIR` and the directory it points at can hang every
non-raw `--doctor` invocation Espanso makes.

**Disposition:** S04E03 backlog (paired with F-EE-02).

---

#### F-EE-04 -- AZ_AI_REGISTRY_DIR re-anchor with no canonicalisation/log -- [HIGH] [partial]

**Surface:** `ResolveRegistryDir` in Program.cs lines 3749-3784.

**Attack scenario:** Espanso / AHK / cron contexts inherit the user's
environment. A second-tier attacker who can set environment variables
in those contexts (e.g., a malicious `~/.profile` snippet, a poisoned
Espanso package, a Windows registry shell-environment write) sets
`AZ_AI_REGISTRY_DIR=/path/they/control`. Every subsequent `--doctor`
call resolves cards under that path. There is no banner, no `[INFO]`,
nothing in stdout or stderr telling the user the anchor moved. Combined
with F-EE-01 (parent-dir symlink) the attacker reads any file the
process can open. Even without F-EE-01, the silent re-anchor breaks
the principle of least surprise documented in `ResolveRegistryDir`'s
own doc-comment ("operator escape hatch") -- escape hatches for
operators should be loud, not quiet.

The `Directory.Exists(envDir)` test is not a guard -- it returns true
for any directory the process can stat, including symlinked ones, and
performs no canonicalisation against an allowlist.

**Observed/expected:** Either reject the env-var unless it is itself
a real directory under `$HOME` or a small allowlist, or emit a single
`[INFO] registry anchor overridden via AZ_AI_REGISTRY_DIR=...` line
on stderr the first time it fires per process.

**Recommended fix:** Two-line patch: canonicalise via
`Path.GetFullPath(envDir)` and reject if the canonical path is not
prefixed by `$HOME` or `/etc/az-ai`; AND emit a single `[INFO]` line
unless `isRaw`.

**Blast radius:** As above -- multiplier on F-EE-01 for read-anywhere,
multiplier on F-EE-03 for hang-anywhere. On its own: surprise/UX gap
with security-relevant consequences in shared-env contexts.

**Disposition:** **FIX-NOW (lite)**. The `[INFO]` log is one line, no
behaviour change. Allowlist can wait for S04E03. Ship the log now so
operators discover misconfigurations.

---

#### F-EE-05 -- IsRegularFile returns true unconditionally on macOS -- [MEDIUM] [speculative]

**Surface:** `IsRegularFile` lines 424-431. The early-return short-circuits
all non-Linux paths to `true`.

**Attack scenario:** macOS host with a card path that resolves to a
FIFO: `mkfifo ~/.config/az-ai/card.fifo` and a registry entry pointing
at it. The Linux-only stat shim is skipped, so only the .NET
`ReparsePoint` check runs. FIFOs are not reparse points and Linux/macOS
`FileAttributes` does not flag them as `Device` reliably -- the same
gap that motivated the libc shim on Linux exists on macOS but is
unguarded. ReadAllText hangs.

**Observed/expected:** macOS users should get the same FIFO/device
guard as Linux users.

**Recommended fix:** Add a macOS branch: `struct stat` on Darwin places
`st_mode` at offset 4 (per the comment block in ModelRegistry.cs:412).
Repeat the existing Linux pattern with `LinuxStatModeOffset = 4`
when `OperatingSystem.IsMacOS()`. Or call `lstat` via P/Invoke with
the `__DARWIN_64_BIT_INO_T` variant -- one shim per platform.

**Blast radius:** Process hang on macOS. Same exposure model as F-EE-03
without needing a TOCTOU swap.

**Disposition:** S04E03 backlog. CI does not run macOS today; defer
behind a tracked issue so we do not ship a guard we cannot test.

---

#### F-EE-06 -- Notes list allocates N strings, only bounded by 256 KB cap -- [LOW] [verified]

**Surface:** `ParseBracketedList` lines 357-376.

**Attack payload:** A `notes:` value of `[a,a,a,...]` repeated until the
file is just under 256 KB -- approximately 80,000 entries at 3 bytes
each. `ParseBracketedList` allocates a `List<string>` plus N pinned
`string` instances and returns them as a `string[]` on the heap.

**Observed/expected:** Per-card cost should be bounded by something
tighter than file size -- a card with 80,000 notes is not a card.

**Recommended fix:** Cap at e.g. 64 entries; truncate with a `[WARN]`.
One-line change in `ParseBracketedList` after `parts.Length` is known.

**Blast radius:** ~5-10 MB heap pressure per malicious card during
`LoadCards`. Negligible at one card; non-trivial if every card in a
poisoned override registry pulls the trick.

**Disposition:** S04E03 backlog. Pair with the dedup work in F-05.

---

#### F-EE-07 -- Card front-matter `name` never compared to registry entry name -- [LOW] [verified]

**Surface:** `LoadCards` lines 384-398. Result is keyed by
`entry.Name`; `card.Name` is stored verbatim and never validated
against the registry entry that pointed at the card file.

**Attack scenario:** Registry entry says `name: "gpt-4o-mini"`,
its card front matter says `name: "../../etc"`. `WriteRegistrySection`
today uses `e.Name` (registry side) for display, so the bogus card name
is unused -- safe in S04E02. But any future caller that switches to
`card.Name` (smart-defaults engine, capability lookup, persona
manifest) inherits a confused-deputy primitive: the registry promised
a model named X, the card claims to be Y. Russell explicitly aliases
`model` to `name` in the front matter (ModelRegistry.cs:319-322), so
seed cards already exercise the alias path -- a subtle drift where the
two diverge would land without a test.

**Observed/expected:** When `card.Name` is non-empty and unequal
(ordinal) to `entry.Name`, emit `[WARN] card '<path>' declares
name='<X>' but registry entry is '<Y>'`.

**Recommended fix:** One block in `LoadCards` after `ReadCard`
returns, comparing `card?.Name` to `entry.Name`. WARN-only, no
behavioural change.

**Blast radius:** Currently zero (display uses registry side). Latent
once any consumer trusts `card.Name`.

**Disposition:** S04E03 backlog. The fix is small but the fix-without-a-consumer
is hard to test meaningfully; defer until E05 (smart-defaults) needs it.

---

#### F-EE-08 -- Duplicate front-matter keys silently last-wins -- [LOW] [verified]

**Surface:** `ParseFrontMatter` line 309: `keys[key] = value;` overwrites
without checking for prior insertion.

**Attack scenario:** A card with two `provider:` lines (one `azure`,
one `evil`). Last-wins means the second value clobbers the first
silently. A user copy-pasting two front-matter blocks together gets
no signal that the first block's keys were dropped.

**Observed/expected:** Either reject the card (rc=99 is too harsh for
a doc fidelity issue; null+WARN is right) or merge with `[WARN] card
'<path>' has duplicate key '<k>'`.

**Recommended fix:** `if (keys.ContainsKey(key)) WARN; else keys[key] = value;`.

**Blast radius:** Confusion only -- last-wins is a defensible default
and there is no privilege boundary crossed.

**Disposition:** WONT-FIX (document the behaviour in ADR-012's card
contract section). Adding the WARN is fine if Russell wants it; not
worth a fix-forward.

---

#### F-EE-09 -- Lone-CR (classic Mac) line endings not normalised -- [NIT] [verified]

**Surface:** `ParseFrontMatter` line 275 only normalises `\r\n` to `\n`.

**Attack scenario:** A card saved with classic-Mac line endings
(`\r` only) is treated as one giant line. `lines[0]` is the entire
file; `Trim() == "---"` is false; the parser returns null with a
"missing opening fence" WARN. Author is confused, file looks fine in
their editor.

**Recommended fix:** Replace lone `\r` with `\n` before the existing
`\r\n -> \n` normalisation. Two extra lines.

**Disposition:** WONT-FIX. Classic Mac line endings have not been
emitted by default by any editor in twenty years. Document and move on.

---

#### F-EE-10 -- StripQuotes does not validate paired quote types -- [NIT] [verified]

**Surface:** `StripQuotes` lines 347-355.

**Attack scenario:** A value of `"abc'` (open double, close single) is
NOT stripped (the type check is `(s[0]=='"' && s[^1]=='"') || (s[0]=='\'' && s[^1]=='\'')`,
which correctly rejects mismatched). But `"\""` (a single escaped
quote inside double quotes) is stripped to `\` -- there is no escape
handling, by design. This is the documented YAML-ish contract; the
NIT is that the contract is undocumented in the public XML doc on
ModelRegistry.

**Recommended fix:** Add a sentence to the front-matter contract
comment in `ModelCard.cs` explicitly stating that backslash escapes
are not honoured inside quoted values.

**Disposition:** WONT-FIX (Elaine doc tweak only).

---

### Closing assessment

One CRITICAL (F-EE-01) and one HIGH (F-EE-04) demand fixes before the
S04E02 episode close -- the parent-dir symlink bypass turns Russell's
clean `--doctor` row formatter into an arbitrary-file primitive when
`AZ_AI_REGISTRY_DIR` is set, which the env-var's "operator escape
hatch" framing actively encourages people to do.

The two TOCTOU findings (F-EE-02, F-EE-03) are right-shaped to fix
together with one open-once-then-fstat refactor; defer to S04E03
unless Newman wants them rolled in with the F-EE-01 patch.

Everything else is backlog or doc-only. The hand-rolled YAML parser is
genuinely small and surprisingly resilient -- BOM handling works
(verified), CRLF works (verified), the F-01/F-03/F-04 guards do their
intended jobs against the attacks they were specified for. The
findings here are the gaps between what the guards specify and what
attackers can actually do once a parent symlink or an env-var enters
the picture.

**Newman's triage queue (priority order):** F-EE-01 (FIX-NOW),
F-EE-04 (FIX-NOW lite), F-EE-02, F-EE-03, F-EE-05, F-EE-06, F-EE-07,
F-EE-08, F-EE-09, F-EE-10.
