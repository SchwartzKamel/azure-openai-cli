# v2 Chaos Drill -- FDR Red Team Report

**Author:** FDR (adversarial / chaos engineering)
**Baseline:** commit `488aebd` on `main`
**Artifact under test:** `azureopenai-cli-v2` → `dotnet publish -c Release -r linux-x64 -p:PublishAot=true` →
  `azureopenai-cli-v2/bin/Release/net10.0/linux-x64/publish/az-ai-v2` (15 MB AOT native, snapshotted to `tests/chaos/artifacts/az-ai-v2`)
**Harness:** `tests/chaos/*.sh` + `tests/chaos/mock_server.py` (every attack is a reproducible script)
**Scope:** 10 attack categories from the brief plus an 11th live persona-memory exercise added mid-drill once F1/F2/F3 surfaced.

> FDR does not patch; FDR files. Severity + owner for each 🔴/🟡 is listed below -- Kramer/Newman/Frank own the fixes.

---

## Verdict

**🔴 NOT READY for v2.0.0 cutover.**

Three live-reproducible 🔴 findings in `Squad/PersonaMemory.cs` alone (F1/F2/F3), plus a standing 🟡 for two failing tests at baseline HEAD (F8). Ship blockers below. Drop the mic; hold the release.

| Class  | Count |
|--------|------:|
| 🔴 crash / leak / wrong-behavior | **3** |
| 🟡 noisy / accepted but unsafe  | **5** |
| 🟢 defended                      | ~45 (see per-attack table) |

---

## 🔴 Findings (ship blockers)

### F1 -- `PersonaMemory.ReadHistory` amplifies arbitrary file size into process RSS
**File:** `azureopenai-cli-v2/Squad/PersonaMemory.cs:30-43`
**Severity:** 🔴 High (reliability / DoS; local privilege not required, just a writable `.squad/history/<persona>.md`)
**Owner:** Kramer (author) / Frank Costanza (SRE)
**Reproducer:** `tests/chaos/11_persona_live.sh` case `11a`

```csharp
// PersonaMemory.cs:34-39
var content = File.ReadAllText(path);              // reads entire file
if (content.Length > MaxHistoryBytes)              // then truncates
    content = "...(earlier history truncated)...\n" + content[^MaxHistoryBytes..];
```

Measured: 100 MB history → peak RSS **431 MB** (`/usr/bin/time -v`: `Maximum resident set size (kbytes): 431480`). Runs on every invocation that resolves a persona, before any network I/O.

**Fix direction:** stream the last 32 KB via `FileStream.Seek(-MaxHistoryBytes, SeekOrigin.End)` + `StreamReader`. Never load the whole file.

### F2 -- `PersonaMemory.ReadHistory` hangs on unbounded/device files
**File:** same as F1
**Severity:** 🔴 High (reliability; CLI becomes unresponsive, killed only by SIGTERM)
**Owner:** Kramer / Frank
**Reproducer:** `tests/chaos/11_persona_live.sh` case `11b` (symlink `.squad/history/rogue.md -> /dev/urandom`)

`File.ReadAllText` on `/dev/urandom` never returns EOF. With a persona bound to such a history file the CLI hangs; our drill reaped it with SIGTERM after 20 s. Respects neither `--timeout` nor SIGINT (Ctrl-C passes through but the read is in a finalizer-hostile path). Same root cause as F1 -- stream, don't slurp, and also stat the path up front and refuse symlinks outside `.squad/history/`.

### F3 -- Persona-name path traversal in `GetHistoryPath`
**File:** `azureopenai-cli-v2/Squad/PersonaMemory.cs:108-109`
**Severity:** 🔴 High (integrity; attacker-controlled `.squad.json` ⇒ history reads/writes escape `.squad/history/`)
**Owner:** Kramer / Newman (security triage)
**Reproducer:** `tests/chaos/11_persona_live.sh` case `11c` + static proof

```csharp
private string GetHistoryPath(string personaName) =>
    Path.Combine(_baseDir, HistoryDir, $"{personaName.ToLowerInvariant()}.md");
```

`Path.Combine` does **not** resolve `..`; it concatenates. A `.squad.json` declaring a persona named `../../canary` produces a history path of `.squad/history/../../canary.md`, which the OS normalises to `./canary.md` -- one level above `.squad/`, fully outside the intended sandbox.

Observed: `--persona '../../canary'` accepted, banner prints `🎭 Persona: ../../canary (r)`, lookup reaches `GetHistoryPath`. The read side was confirmed to traverse; the write side (`AppendHistory`) uses the identical method and will clobber attacker-chosen paths on the next successful agent session. `ToLowerInvariant()` does not strip `/` or `\`.

**Trust model note:** yes, `.squad.json` is in-repo and user-authored. It is also (a) committed and shared across a team, (b) trivially mutable by any process in the cwd, (c) the file `--squad-init` invites the user to "edit to customize." Treating it as a trust boundary is a one-line hardening (`if (name.Contains('/') || name.Contains('\\') || name.Contains("..")) throw ...`).

**Fix direction:** validate persona name at load (`SquadConfig.Load`) against `^[A-Za-z0-9_\-]{1,64}$`. Also reject at `--persona` flag parse time. Also call `Path.GetFullPath` and verify the result starts with `Path.GetFullPath(_baseDir + "/history/")`.

---

## 🟡 Findings (accepted / noisy, not crashes)

### F4 -- 50 MB config parse amplification
`UserConfig.Load` reads the entire `.azureopenai-cli.json` before JSON-parsing it. A 50 MB malformed file produces warnings only, but transient memory spikes to ≥50 MB on every invocation in that cwd. Cap at 1 MB with a stat() check before read.
Reproducer: `tests/chaos/04_config_chaos.sh:04a`. Owner: Kramer.

### F5 -- `--max-tokens` / `AZURE_MAX_TOKENS` / `AZURE_TEMPERATURE` accept nonsense values
- `--max-tokens -1` → rc=0 (only estimate path exercises it; real calls will bounce off the API with a 400). Parser (`Program.cs:630`) is `int.TryParse` with no range.
- `AZURE_TEMPERATURE=9e99` → `float.TryParse` returns `+Infinity`; passed through to the SDK, which will then explode. Cleanly-explode-via-API ≠ cleanly validated locally.
- `--max-iterations` / `--max-rounds` DO have range checks (verified 10c/01h/01i); `--max-tokens`, `AZURE_MAX_TOKENS`, `AZURE_TEMPERATURE` do not.
Reproducers: `01g`, `03j`, `03k`. Owner: Kramer.

### F6 -- World-writable config loaded without warning
`.azureopenai-cli.json` chmod 0666 is loaded silently. In multi-user boxes or sloppy dev containers, another user can steer `default_model` / `defaults.temperature`. Either warn on `S_IWOTH`/`S_IWGRP` (Linux) or match the hardening `ssh` applies to its config. Reproducer: `04f`. Owner: Newman.

### F7 -- AOT "will always throw" warnings in `Azure.AI.OpenAI`
`ILC: Method '[Azure.AI.OpenAI]Azure.AI.OpenAI.Chat.AzureChatClient.PostfixSwapMaxTokens' will always throw` (plus `PostfixClearStreamOptions`) -- latent NotSupportedException on any code path that invokes them. Caused by a missing accessor (`get_SerializedAdditionalRawData`) between the bound `Azure.AI.OpenAI 2.1.0` and its `OpenAI` dependency. Hand to Kramer to pin a matched version, or to Bob Sacamano to confirm the v2 dependency closure.
Reproducer: stderr of `dotnet publish …` (captured in drill; full trace reproducible by re-running the publish command). Owner: Kramer / Bob Sacamano.

### F8 -- Baseline test failures at `488aebd`
```
AzureOpenAI_CLI.V2.Tests.ErrorAndExitTests.ErrorAndExit_JsonMode_WritesValidJson        [FAIL]
AzureOpenAI_CLI.V2.Tests.V2FlagParityTests.ErrorAndExit_HonorsJsonMode(jsonMode: True…) [FAIL]
Failed! -- Failed: 2, Passed: 299, Skipped: 0, Total: 301
```
Both concern `--json` error output shape. Not introduced by this drill -- reproduced on a clean checkout. A v2.0.0 tag with 2 red tests is a Mr. Wilhelm change-advisory failure by itself.
Reproducer: `dotnet test tests/AzureOpenAI_CLI.V2.Tests` on `488aebd`. Owner: Kramer (author) / Mr. Lippman (release gate).

---

## 🟢 Defended (spot-checked and held)

| # | Attack                                                              | Evidence (rc / stderr prefix)                                            |
|---|---------------------------------------------------------------------|--------------------------------------------------------------------------|
| 01a/b | `--persona=../../etc/passwd` (no persona of that name defined)  | rc=1 -- "Unknown persona '…'. Available: …"                               |
| 01c | `--model '$(whoami)'`                                             | rc=1 in estimate path; no shell expansion at any stage (string-literal)  |
| 01f | `--max-tokens 1e999`                                              | rc=1 -- `int.TryParse` rejects                                            |
| 01h | `--max-rounds 99999`                                              | rc=1 -- "must be an integer 1-20"                                         |
| 01i | `--max-iterations -5`                                             | rc=1 -- "must be between 1 and 50"                                        |
| 01j | Unknown flag `--pwn`                                              | rc=1 -- help + clean error                                                |
| 01k | 1 MB `--system` value                                             | rc=126 -- kernel E2BIG on exec; expected OS-level bound                   |
| 02a | 10 MB stdin prompt (--estimate)                                   | rc=1 (downstream endpoint bogus); no crash; no OOM                       |
| 02b | Pipe closed mid-stream                                            | rc=141 -- clean SIGPIPE                                                   |
| 02c-g | CRLF / BOM / ANSI escape / ZW-space / bidi-override stdin       | rc=0; estimator emits nothing to the tty (stdout is ASCII only)           |
| 03a | `AZUREOPENAIAPI=""`                                               | rc=1 -- "AZUREOPENAIAPI environment variable not set"                     |
| 03d | `AZUREOPENAIENDPOINT=http://169.254.169.254/` (real path)         | rc=1 -- "Invalid endpoint URL: … Must be a valid HTTPS URL." SSRF never reached |
| 04b/c | `{"models":null}` / 2000-deep nested JSON                       | rc=0 -- warnings + fall back to defaults; `System.Text.Json` MaxDepth=64 catches depth bomb |
| 04d | Config symlink -> `/etc/passwd`                                   | rc=0 -- JSON parse error; no contents leaked                              |
| 04e | Config symlink -> self                                            | rc=0 -- "Too many levels of symbolic links"; no hang                      |
| 04g/h | UTF-8 BOM / UTF-16 LE config                                    | rc=0 -- BOM tolerated; UTF-16 silently falls back                         |
| 04i | `--config /etc/passwd`                                            | rc=0 -- treated as filename; warned as invalid JSON; no leak              |
| 05a2 | `--personas` with name `../../pwned`                             | rc=0; **but** this is F3's vector -- recorded as defended for LIST only   |
| 05d  | "ReDoS"-shaped routing pattern with 50k-char prompt              | rc=0; `SquadCoordinator.Route` is comma-split keywords, not regex -- ReDoS not applicable |
| 07b / 09a-f | Plain-HTTP mock endpoints (connection-refused, slowloris, gibberish body, 10 MB blob, mid-stream close, HTTP/1.0 no Content-Length) | rc=1 -- all rejected at the HTTPS-scheme guard (`Program.cs:350`). **Note:** consequence is we could **not** exercise the streaming/SSE parser with adversarial frames through this CLI. See "Not exercised" below. |
| 07a | V2 test suite                                                     | 299/301 pass (F8 noted)                                                  |
| 08a-d | SIGINT / SIGTERM / SIGPIPE / double SIGINT                      | rc=0 -- no crash, no zombie                                               |
| 10a | Delegate depth cap via `AsyncLocal<int>`                          | covered by V2 test suite                                                 |
| 10b | `RALPH_DEPTH=99` env not honored in v2                            | rc=0 -- env ignored; v2 uses `AsyncLocal` per code review                 |
| 10c | `--max-iterations 51`                                             | rc=1 -- "must be between 1 and 50"                                        |

---

## Not exercised (documented gaps)

- **#9 Network chaos / SSE parser adversarial frames.** The HTTPS-only guard rejected every plain-HTTP mock upfront. Fully exercising `midstream_close`, `tool_call_loop`, `tool_huge_args`, malformed-JSON-in-tool-args, and "10 MB frame with a missing delimiter" requires a local TLS terminator. Deferred to the next drill with a `mitmproxy --mode reverse` front-end; mock payloads are already scripted in `tests/chaos/mock_server.py` and ready to attach.
- **#7 Tool chaos end-to-end.** Same root cause -- cannot drive the model-to-tool path without either real Azure creds or a TLS-terminated mock. Individual tool defenses (path blocklist, SSRF block, blocked-commands, delegate depth) ARE covered by the V2 test suite (299 passing). The adversarial cases called out in the brief (tool call with args `{"path":"/etc/shadow"}`, `{"url":"http://169.254.169.254/"}`, `{"command":"rm -rf /tmp/foo"}`, unknown tool name, non-JSON args, 10 MB arg value, 100 tool calls) remain fuzz candidates for the next drill.
- **#6c Concurrent persona history writes.** `File.AppendAllText` is used with no advisory/OS lock (`Squad/PersonaMemory.cs:58`). Two concurrent `az-ai-v2 --persona X` sessions that both reach AppendHistory can interleave within a session record (POSIX append is atomic only up to `PIPE_BUF`/page boundaries -- a ~500-char session record exceeds no boundary on Linux ext4 but will on NFS). Static note only; no reproducible race observed in this drill.

---

## How to replay

```bash
# 1. Build
dotnet publish azureopenai-cli-v2 -c Release -r linux-x64 -p:PublishAot=true
# 2. Snapshot (dotnet test later will rebuild the tree; snapshot keeps attacks deterministic)
cp azureopenai-cli-v2/bin/Release/net10.0/linux-x64/publish/az-ai-v2 tests/chaos/artifacts/
# 3. Run every attack
bash tests/chaos/run_all.sh
# 4. Inspect per-attack stdout/stderr
ls tests/chaos/artifacts/*.out tests/chaos/artifacts/*.err
# 5. Aggregate TSV
column -t -s $'\t' tests/chaos/artifacts/results.tsv | less -S
```

Individual scripts (all live under `tests/chaos/`):

| Script                       | Category                                |
|------------------------------|-----------------------------------------|
| `01_argv_injection.sh`       | Argv / flag-value injection             |
| `02_stdin_evil.sh`           | Stdin evil inputs                       |
| `03_env_chaos.sh`            | Environment variable chaos              |
| `04_config_chaos.sh`         | `.azureopenai-cli.json` chaos           |
| `05_squad_chaos.sh`          | `.squad.json` chaos                     |
| `06_persona_memory.sh`       | Persona memory sizing & traversal (initial) |
| `07_tool_chaos.sh`           | Tool chaos (V2 test suite + shape probes) |
| `08_signal_chaos.sh`         | Signal handling                         |
| `09_network_chaos.sh`        | Malformed local endpoints               |
| `10_ralph_depth.sh`          | Delegate/Ralph depth cap                |
| `11_persona_live.sh`         | Live persona-memory exercise (added mid-drill; produced F1/F2/F3) |
| `mock_server.py`             | Scripted-misbehavior HTTP server        |

---

## Cutover recommendation

**Hold v2.0.0.** Minimum fix set before re-drilling:

1. **Block cutover** -- F1 + F2 + F3 (all in `Squad/PersonaMemory.cs`) must be closed. One file, one owner (Kramer), estimated < 1 day; regression tests are a natural fit for the existing `tests/AzureOpenAI_CLI.V2.Tests` suite -- Puddy can write them red, Kramer turns them green.
2. **Block cutover** -- F8 (two failing tests at HEAD). Either fix or document + triage by Mr. Lippman before the release tag.
3. **Fix before v2.0.1 / soak window** -- F4, F5, F6, F7.

Redrill gate: all 🔴 closed, 0 failing unit tests, `tests/chaos/run_all.sh` delivers zero new 🔴 verdicts.

-- FDR

> "Happy birthday to your release. I hope you like the three reproducible path-traversals in `PersonaMemory.cs` I filed before lunch."
