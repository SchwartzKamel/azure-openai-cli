# S03E15 -- *The Probe*

> *Three providers, three probes, zero secrets emitted. Costanza ships the doctor.*

**Commit:** (pending -- DO NOT COMMIT per orchestrator brief)
**Branch:** main (working tree, alongside Frank Costanza's telemetry hooks and Mickey Abbott's `--plain` flag)
**Runtime:** roughly 35 minutes wall-clock for the Costanza dispatch leg
**Director:** Larry David (showrunner)
**Cast:** 1 sub-agent (Costanza) in 1 dispatch wave; orchestrator-coordinated alongside two concurrent agents (Frank Costanza, Mickey Abbott) operating on `Program.cs`

## The pitch

You ever set up a tool, type the first prompt, and it just sits there?
Spinner spinning. Maybe it's the endpoint. Maybe it's the key. Maybe the
allowlist. Maybe DNS. You don't know. You're not going to know. You'll
unset and reset variables for fifteen minutes and then go make a sandwich.
That's a *bad first day*, my friends. We don't ship bad first days.

S03E15 ships `az-ai --doctor` -- a non-authenticated diagnostic that
walks every configured provider in the user's environment and reports
three things per provider, in one pane, in under a second:

1. **DNS.** Can we resolve the endpoint host? (3-second cap, parallel.)
2. **Credentials.** Is the relevant env var set? (Boolean. Never the value.)
3. **Models.** How many entries are in the allowlist?

That's it. No POST. No `Authorization: Bearer`. No round-trip to a
remote service that might rate-limit, page someone, or charge a fraction
of a cent. The doctor is the thing you run *before* the first real call,
the thing you run *after* a colleague gives you their `~/.config/az-ai/env`,
the thing CI runs in a smoke-test stage. Latency target: sub-second on a
healthy box, sub-five on a fully unreachable one (cap = max-providers x
3s, but we parallelize via `Task.WhenAll`, so wall-clock is one timeout
window, not N).

The grievance was old and obvious. The proposal landed in the writers'
room as soon as Mickey's a11y pass made plain output a first-class
citizen and Frank's telemetry hooks gave us a reason to introspect what
was actually configured at run time. E15 was the proposal that turned
into a feature.

## Scene-by-scene

### Act I -- Planning

The orchestrator brief was tight. Two other agents were already on
`Program.cs`: Frank wiring telemetry on the dispatch hot path, Mickey
landing `--plain` and the ANSI gating helpers. The brief drew a clean
line in the dirt: Costanza touches `Program.cs` AT MOST ONCE, and only
to wire the new subcommand. Everything else lives in NEW files under
`azureopenai-cli/Cli/`.

That single constraint shaped the design more than anything else:

- The doctor had to be a self-contained class with a `Run(...)` entry
  point, not a smear of methods through `Program.cs`.
- The wire-up had to be a single `case "--doctor":` branch. No options
  record extension (Frank and Mickey are already mid-flight there).
  No early dispatch from `Main()`. One switch case, self-contained,
  early `Environment.Exit`.
- DNS had to be stubbable for tests, because no test in this repo gets
  to actually hit DNS -- not even `8.8.8.8`-style sentinels.
- The output had to round-trip through `SecretRedactor` on every line,
  not because the doctor *intends* to print secrets (it doesn't) but
  because defense in depth treats intent as a guess and structural
  scrubbing as a guarantee.

Pivots:

- Considered adding `Doctor` to the `CliOptions` record. Rejected.
  Frank is touching that record. Mickey is touching that record. Adding
  a field guarantees a rebase conflict. Self-contained switch case sidesteps it.
- Considered an HTTP HEAD or TLS handshake instead of just DNS. Rejected.
  HEAD-without-auth is a de-facto authenticated probe on Azure (gets a
  401 with rich response headers we'd then have to strip). Plain DNS is
  cheap, deterministic, and free of credential-shape exposure on the wire.
- Considered exposing the doctor at `providers doctor` *also* as a
  positional sub-command. Held off. The arg parser has `setup` and `help`
  as bare positional words but the existing scaffold is single-word; a
  two-word `providers doctor` requires a small parser refactor that the
  orchestrator's "one hunk" budget does not cover. Filed as a follow-up
  for E16 *The Allowlist*, where the same parser change earns its keep.

Decisions locked:

- DNS-only probe. No HTTP. No auth.
- 3-second per-host cap. Parallel dispatch via `Task.WhenAll`.
- Boolean creds-presence. Numeric model count. No values.
- Three output modes: default ASCII table, `--json`, `--plain` stanzas.
- Every print path through `SecretRedactor.Redact`.
- DNS resolver injectable via static property test seam.
- One `Program.cs` hunk, scoped to a single switch case.

### Act II -- Fleet dispatch

| Wave | Agents (parallel) | Outcome |
|------|-------------------|---------|
| **1** | Costanza | Shipped `Cli/ProviderDoctor.cs`, JSON context records, single `--doctor` switch case in `Program.cs`, 21 unit tests, 3 integration assertions, README "Diagnosing your setup" subsection, CHANGELOG `[Unreleased]` entry, this exec report, writers'-room E15 row. |

Concurrent (orchestrator-coordinated, not Costanza-dispatched):

- **Frank Costanza** -- telemetry hooks in dispatch hot path. Touches
  `Program.cs` for instrumentation. Costanza's wire-up is in a switch
  case `--doctor` between `--raw` and `--agent`; non-overlapping.
- **Mickey Abbott** -- `--plain` flag, `Plain.cs` helper, ANSI gating,
  banner audit, glyph cleanup. Costanza calls `Plain.IsActive()` from
  the wire-up site to honor whatever Mickey's ergonomics policy decides.

### Act III -- Ship

- Build: `DOTNET_ROOT=/usr/lib/dotnet dotnet build` -> 0 warnings, 0 errors.
- Unit tests: `dotnet test --filter ProviderDoctorTests` -> 21 passed, 0 failed.
- Preflight: `DOTNET_ROOT=/usr/lib/dotnet make preflight` -- run before
  the orchestrator's final commit. Doctor branch never invokes the API,
  so preflight integration mode (which does no auth) covers it.
- Per the orchestrator brief: DO NOT COMMIT. Costanza stages files for
  the orchestrator's merge wave; conflict notes are in the report
  delivered alongside this file.

## What shipped

**Production code:**

- `azureopenai-cli/Cli/ProviderDoctor.cs` (new, ~290 LOC). Public surface:
  `public static int Run(bool jsonMode, bool plain, TextWriter stdout, TextWriter stderr)`.
  Internal seams: `DnsResolver` (test stub), `DnsTimeout` (test override),
  `CollectProviders()` (test inspect). Three providers handled: `azure`
  (gated on `AZUREOPENAIENDPOINT`), `foundry` (gated on `AZURE_FOUNDRY_ENDPOINT`),
  and one row per OpenAI-compat preset referenced in `AZ_AI_COMPAT_MODELS`.
- `azureopenai-cli/JsonGenerationContext.cs` (modified). Added
  `ProviderDoctorReport` and `ProviderDoctorEntry` records with
  `JsonPropertyName` snake_case mapping; registered with
  `AppJsonContext` for AOT-safe serialization. Snake_case is explicit
  per-property because the context-level naming policy is camelCase
  for the rest of the surface and we don't break existing payloads
  for one new envelope.
- `azureopenai-cli/Program.cs` (one hunk: a single new
  `case "--doctor":` switch branch, 12 lines net). Pre-scans `args`
  for `--json`; reads plain via `Plain.IsActive()` (Mickey's
  helper); calls `ProviderDoctor.Run` and `Environment.Exit`s with
  the return code. No options-record changes. No dispatch-loop changes.
  No new fields anywhere else in `Program.cs`.

**Tests:**

- `tests/AzureOpenAI_CLI.Tests/ProviderDoctorTests.cs` (new). 21 facts
  in the `[Collection("ConsoleCapture")]` collection (env-var mutation).
  DNS stubbed via `ProviderDoctor.DnsResolver` -- production wires to
  `Dns.GetHostEntryAsync`, tests inject a `Func<string, CancellationToken, Task<bool>>`.
  Cases: no providers configured; all-healthy single Azure; missing
  creds; bad DNS; resolver throws; DNS timeout under cap; malformed
  endpoint URL; Azure + Foundry both healthy; model count from comma
  list; compat preset by name; malformed compat env; plain mode
  ASCII-only and key:value shape; JSON schema field presence; never
  emits a sentinel credential value; parallel probes complete inside
  the timeout cap; one-of-many unhealthy flips `all_healthy`; default
  mode uses ASCII box drawing; CollectProviders enumeration tests x 2.
- Test count delta: 829 unit -> 850 unit (+21). 51 integration ->
  54 integration (+3, all in the S03E15 doctor block).

**Docs:**

- `README.md` -- new "Diagnosing your setup" subsection inside the
  Security section. Sample output with redacted-shaped endpoint and
  no credential text. Five-flag matrix: default, `--json`, `--plain`,
  exit code semantics, secret-redactor citation.
- `CHANGELOG.md` -- `[Unreleased]` Added entry citing S03E15 *The Probe*.
- `docs/exec-reports/s03e15-the-probe.md` -- this file.
- `docs/exec-reports/s03-writers-room.md` -- E15 row added with GREEN
  verdict and lead = Costanza.

**Not shipped (intentional follow-ups):**

- Two-word positional sub-command (`az-ai providers doctor`). Held
  for E16 *The Allowlist*, which already needs a parser refresh.
- HTTP/TLS-level reachability probe. Held indefinitely. The cost of
  generating false negatives (corporate proxies, MITM TLS inspectors,
  rate-limit response codes that look like outages) outweighs the
  signal in a non-authenticated diagnostic.
- Latency-budget contribution. The doctor itself is sub-second on
  healthy hosts; the latency-budget table (Costanza FR follow-up)
  remains a separate roadmap item -- this episode is about
  *correctness of presence*, not *speed of dispatch*.
- Compat preset DNS short-circuit when `CLOUDFLARE_ACCOUNT_ID` is
  unset. The placeholder URL contains `{account_id}`; we currently
  let `Uri.TryCreate` accept it (the host segment resolves), but a
  future tightening could mark the preset as `error: account-id-missing`
  before the DNS attempt.

## Lessons from this episode

1. **One-hunk discipline beats record-field temptation.** The
   instinct was to add a `Doctor` boolean to `CliOptions` and route
   through `RunAsync`. With Frank and Mickey both touching the
   record, that would have produced a guaranteed three-way conflict
   on rebase. The self-contained switch case eliminated the conflict
   surface entirely. Cost: a fraction more local complexity inside
   the case branch (re-scanning `args` for `--json`). Worth it.
2. **Defense-in-depth on output paths is non-negotiable.** Even when
   the doctor *cannot* construct a string with a credential value
   (we never read the value, only its presence), every print
   statement still routes through `SecretRedactor.Redact`. Future-
   regression resistance: a careless interpolation in 18 months that
   *would* leak a value is caught at output time, not at first
   user-bug-report time.
3. **Stub DNS, always.** Real DNS in tests is a flake-source. The
   `Func<string, CancellationToken, Task<bool>>` seam is two lines
   of API surface, takes ten lines of doc comments, and saves an
   indefinite amount of CI flake debugging.
4. **Concurrent agents need orchestrator-mediated handoff.** Frank
   and Mickey were both in `Program.cs`. Costanza touched `Program.cs`
   exactly once, in a switch case visually adjacent to (but
   logically separate from) the regions Frank and Mickey are
   editing. The orchestrator's brief named that constraint up front;
   without it, the rebase would have been a coin flip.
5. **The "no API call" guarantee is a feature, not a limitation.**
   Tempted, in scope review, to bolt on a `--probe-api` flag that
   would issue an authenticated POST. Dropped. The doctor's value
   proposition is "free, fast, safe" -- adding an auth probe would
   make every doctor invocation suspect of side effects. If we want
   API smoke, that's `az-ai --estimate` or a dedicated `--smoke`
   subcommand, not a doctor mode.

## Metrics

- Diff size: 4 new files (`Cli/ProviderDoctor.cs`, `ProviderDoctorTests.cs`,
  `s03e15-the-probe.md`, plus the existing-files edits). 5 modified
  files (`Program.cs`, `JsonGenerationContext.cs`, `README.md`,
  `CHANGELOG.md`, `s03-writers-room.md`, `integration_tests.sh`).
  Net additions roughly 850 lines (production + tests + docs);
  net deletions trivial.
- Test delta: +21 unit (829 -> 850), +3 integration (51 -> 54).
- Build: 0 warnings, 0 errors. AOT-clean (BCL-only, no reflection;
  JSON via `AppJsonContext`).
- Preflight: orchestrator-run after merge wave; expected GREEN.
- CI status at push time: pending the orchestrator's merge wave.

## Conflict notes vs. concurrent agents

- **Frank Costanza (telemetry hooks):** Frank is editing `Program.cs`
  inside `BuildChatClient` / dispatch wrappers. Costanza's hunk is in
  the arg-parser switch statement (around the `--raw` case). No line
  overlap. If Frank renames or relocates the switch block, the
  Costanza hunk needs to follow; staged for orchestrator merge as a
  single-case insertion. **No conflict expected.**
- **Mickey Abbott (`--plain` + Ansi helper):** Mickey added the
  `--plain` case in the same switch block and added a `Plain.cs`
  helper. Costanza *uses* `Plain.IsActive()` and adds a switch case
  several entries below `--plain` (between `--raw` and `--agent`).
  Provided Mickey's `Plain.cs` lands in the same wave (it already
  exists in the dirty working tree at probe time), Costanza's
  reference resolves cleanly. **No conflict expected, hard
  dependency on Mickey's `Plain.cs`.**
- **Sidecar:** the single `--doctor` switch case has been kept in
  the working tree at exactly the location it would land post-merge.
  If the orchestrator detects rebase friction, the case body is
  five tokens (case label, jump target, three argument expressions)
  and can be re-applied at any equivalent location in the parser.
  No need for a sidecar staging file at this time.

## Findings filed

- **F-CLI-DOCTOR-15.1 (LOW):** `cloudflare` compat preset's
  placeholder URL contains literal `{account_id}` which `Uri.Host`
  resolves to a non-routable string. The doctor reports DNS
  `unreachable` for unconfigured Cloudflare. Acceptable behavior
  (the *real* config IS broken without `CLOUDFLARE_ACCOUNT_ID`),
  but worth a friendlier `error: account-id-missing` enrichment in
  E16 / E17.
- **F-CLI-DOCTOR-15.2 (INFO):** `--doctor` ignores `--config <path>`
  alternate config files (only reads process env). Intentional for
  v1: the doctor diagnoses the *shell session that will run az-ai*,
  not arbitrary config layers. Document in the help text in a
  follow-up patch.
- **F-CLI-DOCTOR-15.3 (INFO):** `--doctor` exits via
  `Environment.Exit` rather than returning to `Main`. Side-effect:
  any `using` declarations or `try/finally` blocks above the call
  do not run. Currently no such blocks exist on the path; if any
  are added in the future, switch to a return-path dispatch.

## Tag scene

> *Next episode preview -- S03E16* The Allowlist *: Costanza files an
> FR for first-class allowlist linting -- typo detection, dead-entry
> warnings, and the long-deferred two-word `providers doctor` parser
> refresh. The doctor was diagnosis. The Allowlist is the prescription.*

## Credits

- **Costanza** (Product Manager, sub-agent lead) -- proposal,
  implementation, tests, docs, exec report.
- **Mickey Abbott** (concurrent, S03E14) -- `Plain.IsActive()` helper
  consumed by the wire-up.
- **Frank Costanza** (concurrent, S03E13) -- telemetry hooks
  preserved by the one-hunk discipline; no overlap on the dispatch
  path with the diagnostic.
- **Newman** (security, advisory) -- "never auth, never value"
  invariants enforced via `SecretRedactor.Redact` on every output
  line and integration assertion against `Bearer`/`sk-...` shapes.
- **Larry David** (showrunner) -- episode conception, orchestrator
  brief that drew the "one hunk" line and mediated the three-agent
  `Program.cs` collision.

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>
