# S03E16 -- The Allowlist

> *Showrunner: Larry David. Lead: FDR (Adversarial Red Team).
> Episode arc: Arc 3 setup, the SSRF gate that has to land before
> S03E14 The Daemon ships an Ollama preset.*

## Cold open

Costanza is at the whiteboard. He has drawn a box marked "compat
adapter" with four arrows pointing out: openai, groq, together,
cloudflare. He has drawn a fifth arrow with a question mark.

  COSTANZA: We are about to point this thing at localhost.
  KRAMER:   Ollama, yeah. Eleven thousand four thirty four.
  COSTANZA: And what is on localhost.
  KRAMER:   ...Ollama.
  COSTANZA: What ELSE is on localhost.
  KRAMER:   ...the metadata service.
  COSTANZA: 169.254.169.254. Not strictly localhost. Close enough.
  KRAMER:   Right. Not on localhost. On the LAN.
  COSTANZA: WHAT IS ON THE LAN, KRAMER.
  KRAMER:   ...everything.

FDR enters carrying a manila folder.

  FDR:     Good. You are scared. Let me make it worse.

Cut to title card.

## Goal

Tighten the SSRF posture on the new compat dispatch path so localhost-
only providers (Ollama upcoming in S03E14, llama-server in S03E17) work
without opening RFC1918 / link-local / loopback wide. Single allowlist
seam, two callers (the agent tool surface and the provider build
surface), one opt-in env-var.

## What shipped

### New file: `azureopenai-cli/Net/EndpointAllowlist.cs`

  - `enum AllowlistVerdict` with eight states: Allow, BlockPrivate,
    BlockLoopback, BlockLinkLocal, BlockUla, BlockMulticast,
    BlockMalformed, BlockBroadcast.
  - `static AllowlistVerdict Check(Uri uri, bool localProvidersOptIn)`
    is the public entry point. Resolves the hostname via DNS (3-second
    timeout), checks every A/AAAA record, returns the first failing
    verdict or Allow.
  - `static AllowlistVerdict Check(Uri uri, bool optIn, IPAddress[] preResolved)`
    is the test/injection seam. Skips DNS and uses the supplied array
    directly. The DNS-rebinding test uses this to pin the rule "if any
    address in the answer set is in a blocked range and opt-in is off,
    reject the whole hostname."
  - `static bool LocalProvidersOptInFromEnv()` reads
    `AZ_AI_LOCAL_PROVIDERS` with strict-equality "1". Mirrors the
    `AZ_AI_TELEMETRY` contract from S03E13: "true", "yes", "1 " (with
    trailing space), and "" all keep the gate closed.
  - `static string Describe(AllowlistVerdict)` returns operator-
    friendly text that names the rule that fired and the env-var to
    flip if the operator intended the local target.

### Edited: `azureopenai-cli/Tools/WebFetchTool.cs`

The old ad-hoc `IsPrivateAddress` survives as a back-compat shim that
delegates to the seam. Two call sites (initial fetch, post-redirect
re-validation) now go through `EndpointAllowlist.Check(uri, optIn:
false)`. WebFetchTool is a TOOL, never a provider connection -- the
opt-in is hard-coded false. Localhost on the tool surface stays
blocked even when the operator runs Ollama.

### Edited: `azureopenai-cli/OpenAiCompatAdapter.cs`

`Build()` now calls the allowlist after the cloudflare account-id
substitution and before the `OpenAIClient` is constructed. Verdict
other than Allow throws `ArgumentException` with the friendly text:

    compat preset 'stub-private' resolves to private RFC-1918 address
    (set AZ_AI_LOCAL_PROVIDERS=1 to allow local providers);
    endpoint='https://10.0.0.1/v1'. Refusing to dispatch.

The pre-existing `IsLoopback(Uri)` helper stays in place because the
HTTPS-only-unless-loopback rule is still narrower than the allowlist
itself -- the allowlist refuses HTTP regardless of host shape unless
opt-in is on, which subsumes the old rule but does not replace it.

### New tests: `tests/AzureOpenAI_CLI.Tests/EndpointAllowlistTests.cs`

37 named facts; 57 individual cases when xUnit unrolls the Theories.
Every fact has an inline comment naming the attack class it defends
against. See the per-vector table in
`docs/audits/security-v2.1.3-allowlist.md` for the CWE mapping.

### Audit doc: `docs/audits/security-v2.1.3-allowlist.md`

Threat model first, then per-vector coverage table, then three
forward-hardening notes (`fdr-2026-05-A-1` MEDIUM TOCTOU,
`fdr-2026-05-A-2` LOW parser drift, `fdr-2026-05-A-3` LOW env-var
naming).

### CHANGELOG / README

CHANGELOG `[Unreleased]` Added entry. README Security paragraph gained
one sentence linking to the allowlist audit and naming the
`AZ_AI_LOCAL_PROVIDERS=1` opt-in.

## What did NOT ship

- **Per-redirect re-resolution at the socket layer.** The current
  defense pre-resolves the hostname before HttpClient connects, but
  the actual TCP connect is opaque to us mid-redirect. Filed as
  `fdr-2026-05-A-1` and queued for S03E17 *The Server*.
- **Foundry adapter wiring.** The Foundry seam is on a separate
  adapter; same allowlist will land there in a follow-up. Outside
  scope per the dispatch brief.
- **Setup-wizard endpoint validation.** The wizard writes endpoint
  strings to `~/.config/az-ai/env` but does not run them through the
  allowlist before write. Filed for S03E14 where Ollama lands and
  the wizard surface will need a "validate me" pass anyway.

## Concurrency notes

The dispatch warned of four agents possibly mid-flight: Frank
telemetry, Mickey a11y, Costanza E15 probe, Lloyd local-providers
docs. Touched files were narrow (one new file, two edits). No
collisions observed:

- `WebFetchTool.cs` -- not in any other agent's stated scope.
- `OpenAiCompatAdapter.cs` -- Costanza's E15 was read-only on this
  file. No edit conflict.
- No Mickey a11y helper observed at file edges; if one shows up in
  her sweep the diff is additive and harmless.
- No Program.cs edit (per dispatch instruction).

## Test deltas

  Before: 829 unit + 51 integration (per dispatch).
  After:  886 unit (+57) + 51 integration.

Full suite: `Failed: 0, Passed: 989, Skipped: 0, Total: 989` after
the two ToolHardeningTests substring expectations were satisfied by
keeping the word "private" in the loopback Describe() string.

## Friendly error format

Pin the format here so future agents do not drift:

    compat preset '<name>' resolves to <Describe(verdict)>;
    endpoint='<scheme>://<host>[:<port>]<path>'. Refusing to dispatch.

The Describe text always ends with the env-var to flip when the rule
permits opt-in (loopback, private, link-local, ULA). Multicast,
broadcast, malformed do NOT name an env-var because they are
always-blocked.

## Lessons

- **Strict-equality opt-in is cheap and worth it.** Six lines of test
  coverage (the OptInEnv Theory) plus one StringComparison.Ordinal
  call. Newman has been pushing this pattern since E07; it is the
  right pattern for any "are you sure?" gate.
- **The bare-IP fast path closes the obfuscation lane.** Octal,
  decimal-integer, and IPv6-mapped-IPv4 all parse correctly through
  `IPAddress.TryParse`. We do not have to write a custom parser;
  we have to remember to CALL the parser before the DNS resolver.
- **Test-as-documentation works when every fact has a comment.** The
  audit doc's per-vector table reads almost word-for-word from the
  inline comments in EndpointAllowlistTests.cs. Future regressions
  will surface as a named test going red, and the comment tells the
  next reader what attack class they just unbroke.
- **The IsPrivateAddress shim is an honest delegation, not a
  back-compat lie.** It builds a synthetic Uri from the address and
  re-runs the seam. Same range catalog, single source of truth.

## Findings to log

- `fdr-2026-05-A-1` (MEDIUM, open) -- TOCTOU between DNS pre-flight
  and TCP connect. Owner FDR + Newman. Queued for S03E17.
- `fdr-2026-05-A-2` (LOW, open) -- IPv4 short-form parser drift
  coverage gap. Owner FDR. One-line-fix.
- `fdr-2026-05-A-3` (LOW, open) -- env-var name `AZ_AI_LOCAL_PROVIDERS`
  overstates scope. Owner Elaine + Lloyd Braun. Doc/UX, deferred to
  S03E17.

All three appended to the active findings table in
`docs/findings-backlog.md` per the findings-backlog skill.

## Cast credits

  - **Lead:** FDR (red team).
  - **Co-conspirators:** Newman (security review on Describe wording),
    Costanza (UX of the friendly error), Kramer (Build() patch).
  - **Off-screen:** Frank Costanza (telemetry left alone), Mickey
    Abbott (a11y left alone), Lloyd Braun (his local-providers docs
    will inherit the env-var name conversation).
  - **Showrunner:** Larry David.

## Preflight

  $ DOTNET_ROOT=/usr/lib/dotnet make preflight
  format check     -- clean
  build            -- 0 warnings, 0 errors
  unit tests       -- Passed: 989 / Failed: 0
  integration      -- Passed: 51 / Failed: 0
  exec-report      -- this file satisfies the gate

## Next episode preview -- S03E17 *The Server*

  Cold open: KRAMER returns from a coffee run carrying a laptop that
  now has llama-server bound to 0.0.0.0:8080. He is delighted.

  KRAMER:   I am running it on every interface!
  COSTANZA: Every interface.
  KRAMER:   Every interface!
  FDR:      (entering, smiling) Including the metadata one?

  Cut to title card. The TOCTOU finding from this episode comes due.
  Newman wires the SocketsHttpHandler.ConnectCallback. FDR fuzzes
  the bind. Costanza finally explains what 0.0.0.0 means. Larry David
  signs off on the cut.

That's the show.
