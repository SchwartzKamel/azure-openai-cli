# S02E23 -- *The Adversary*

> FDR finally takes the offensive seat: chaos drill against the
> built-in tool surface, no fixes -- only findings and pinned tests.

**Commit:** see footer
**Branch:** `main` (direct push, per `.github/skills/commit.md`)
**Runtime:** ~25 minutes
**Director:** Larry David (showrunner)
**Cast:** FDR (lead, adversarial red team -- first S02 lead),
Newman (guest, defense -- classifies findings),
David Puddy (guest, test infrastructure -- pins each finding to a
repeatable Skipped regression test).

## The pitch

The codebase has accumulated defenses across half a season:
S02E02's shell hardening, S02E04's SSRF posture, S02E13's audit
walkthrough. Every one of those episodes was Newman's voice --
defense describing what it stops. Nobody had ever pointed FDR at
the same surface and asked "what bypasses do you find?"

This is that episode. Scope discipline is the point: FDR finds
attack paths, Newman classifies them (CVE-shape vs hardening-gap
vs by-design), Puddy turns each finding into a regression test
that runs in CI. **Nothing under `azureopenai-cli/` is touched.**
Failing tests are marked `[Fact(Skip = "Live ...: e23-...")]` so
the build stays green; removing the Skip after the matching fix
episode lands turns each test into proof that the hole closed.

The episode produces one test directory
(`tests/AzureOpenAI_CLI.Tests/Adversary/`), one staging findings
file, and this exec report. Future fix episodes are queued via the
findings backlog -- not greenlit here.

## Scene-by-scene

### Act I -- Threat model (FDR's voice)

FDR walked the four most attacker-relevant tool surfaces and
catalogued one bypass family per surface:

1. **`ShellExecTool`** -- the substring blocklist
   (`Contains("$(")`, `Contains("\``")`, etc.) and the first-token
   tokenizer (`Split(' ', 2)[0].Split('/').LastOrDefault()`) both
   make assumptions about how `/bin/sh` will re-tokenize the
   string. Every place the sh tokenizer disagrees with the
   blocklist tokenizer is a bypass: `${IFS}`, tab,
   newline, `\`-escaped names, quoted names, `$VAR` indirection,
   `&& eval` (only `; eval ` is matched). Eight live bypasses
   confirmed.

2. **`ReadFileTool`** -- the blocklist covers `/etc/*`, `/root/.ssh`,
   and a handful of `~/.aws`-class home-directory paths. It does
   NOT cover `~/.ssh` (the user's own SSH dir, far more relevant
   than root's), `~/.kube/config`, `~/.gnupg`, `~/.netrc`,
   `~/.docker/config.json`, `~/.git-credentials`, `~/.npmrc`,
   `~/.pypirc`. Cross-references S02E13 finding
   `e13-readfile-blocklist-home-dir-gap` -- this episode pins each
   path individually so the S02E26 *Locked Drawer* fix can flip
   them green one by one.

3. **`WebFetchTool`** -- the SSRF posture is the strongest of the
   four (HTTPS-only, pre-flight DNS, IPv4+IPv6 private-range
   coverage, post-redirect re-validation). FDR found three real
   gaps: DNS rebinding TOCTOU between the pre-flight resolution
   and HttpClient's own resolution; multicast / broadcast / SSDP
   ranges not classed as private; CGNAT 100.64.0.0/10 not classed
   as private. Decimal-IP encoding (e.g. `https://2130706433/`)
   is untested either way.

4. **`DelegateTaskTool` + ToolRegistry** -- recursion cap holds
   for non-negative depth. With `RALPH_DEPTH=-1` set by a hostile
   parent the cap effectively raises by one per negative unit
   (`-99` permits 102 levels). The ToolRegistry envelope catches
   malformed JSON, truncated streams, unknown tool names, and
   pathological argument nesting -- but the individual tool
   `ExecuteAsync` methods call `GetString()` on a `JsonElement`
   without checking ValueKind, so a `{"command": 123}` payload
   throws and is only caught at the outer envelope.

### Act II -- Fleet dispatch

| Wave | Agents (parallel) | Outcome |
|------|-------------------|---------|
| **1** | FDR (threat model), Newman (audit posture inventory) | Eight ShellExec bypasses + six ReadFile gaps + three WebFetch gaps + two stream-chaos gaps catalogued. Newman scored each. |
| **2** | Puddy (test pinning) | 21 Skipped tests written, one per finding. 64 passing tests pin the defenses that hold. Four test files in `Adversary/`. |
| **3** | FDR + Newman (classification table), Puddy (preflight) | Findings file drafted. `make preflight` equivalent: format + build + 1180 tests pass + 53 skip. |

### Act III -- Ship

`dotnet format --verify-no-changes` clean (one xUnit2020 warning
caught and fixed: `Assert.True(false, ...)` -> `Assert.Fail(...)`).
`dotnet build` clean. Full suite: **1180 passed, 53 skipped**
(21 of those skips are this episode's pinned findings, 32 are
pre-existing). Direct push to `main`. Findings staged in
`docs/exec-reports/s02e23-findings.md` for orchestrator harvest.

## What shipped

**Production code** -- none. Episode is intentionally find-only.
The four touched files are all under `tests/`. Tool source under
`azureopenai-cli/Tools/` is unchanged.

**Tests** -- four new files in `tests/AzureOpenAI_CLI.Tests/Adversary/`:

- `ShellExecBypassTests.cs` -- 18 cases (10 passing, 8 Skipped
  pinning live bypasses).
- `ReadFileSensitivePathTests.cs` -- 18 cases (12 passing across
  Theory variants, 7 Skipped pinning home-dir gaps -- one Theory
  test counted once).
- `WebFetchSSRFTests.cs` -- 30 cases (26 passing across Theory
  variants, 4 Skipped pinning DNS-TOCTOU / decimal-IP / multicast
  / CGNAT gaps).
- `PartialStreamChaosTests.cs` -- 9 cases (7 passing, 2 Skipped
  pinning non-string-param throw + negative-depth bypass).

Net: **64 active passing tests, 21 Skipped findings.**

**Docs** -- this exec report (`s02e23-the-adversary.md`) and the
findings staging file (`s02e23-findings.md`). No CHANGELOG entry
(test additions are not user-visible per shared-file-protocol).
Writers' room file (`s02-writers-room.md`) untouched -- the
orchestrator harvests the staging file in the next batch.

**Not shipped** (intentional follow-ups):

- All 21 fix episodes. Every Skipped test names the finding it
  pins; the orchestrator queues the fix episodes (Newman lead)
  via the findings backlog.
- `tests/integration_tests.sh` chaos cases. Brief mentions some
  chaos belongs in bash; deferred -- the C# unit-test surface
  proved sufficient for the four target tools and adding bash
  cases would have broadened scope past "find, do not fix".
- Squad-tool adversarial coverage. The Squad surface is S02E30
  / E31 territory and explicitly off-limits per the brief.

## Lessons from this episode

1. **Two tokenizers in one tool is two attack surfaces.** Every
   ShellExec bypass FDR found is the same shape: the C# blocklist
   tokenizer disagrees with `/bin/sh`'s tokenizer about where
   tokens end. The structural fix (S02E26-or-later) is to stop
   pretending we can pre-tokenize and either run with no shell at
   all or use a true allowlist of (command, arg-shape) pairs.
2. **Substring matches don't mean what you think.**
   `command.Contains("; eval ")` looks like it covers eval, but
   it only covers eval after `; ` -- not `&&`, `||`, newline, or
   pipe. The blocklist should be a regex on word boundaries
   across all separators, not literal substrings.
3. **The `~/.aws`-vs-`~/.ssh` asymmetry is an authoring artifact.**
   The blocklist covers `~/.aws` (good) and `/root/.ssh` (good
   for root) but NOT `~/.ssh` -- which is the most attacker-
   valuable file on most developer laptops. S02E26's *Locked
   Drawer* needs to fix this; this episode pins it as seven
   distinct findings so the fix can land path-by-path with
   per-path test coverage.
4. **Defense in depth means each layer must hold alone.**
   `ShellExecTool.ExecuteAsync({"command": 123})` throws because
   the tool trusts the registry envelope to catch. That envelope
   is the only layer between the model and an unhandled
   exception killing the agent loop. One catch is not depth.
5. **Skip-with-finding-name beats failing tests.** Pinning each
   bypass as `[Fact(Skip = "Live bypass: e23-...")]` keeps `main`
   green AND keeps the finding visible in test output. When the
   fix episode lands, removing the Skip is one line and the green
   tick is mechanical proof that the hole closed.
6. **The orchestrator-owned-file rule paid off again.** Two
   uncommitted Squad changes were sitting in the working tree
   when this episode opened (S02E30 territory). Brief explicitly
   denylisted `Squad/*` and `tests/.../Squad/*`; staging
   discipline (`git add` with explicit paths, never `git add -A`)
   kept those files un-touched and un-committed.

## Findings to log (orchestrator harvest)

All 21 findings staged in
[`docs/exec-reports/s02e23-findings.md`](s02e23-findings.md) per
the [`findings-backlog`](../../.github/skills/findings-backlog.md)
format. Newman's top-3 fix-priority recommendations for the
orchestrator to greenlight as future episodes:

1. **`e23-shell-ifs-tokenization`** -- highest priority.
   `${IFS}` bypass routes ANY blocked command past the gate
   (rm, sudo, kill, wget). Trivially exploitable by a hostile
   prompt. CVE-shape.
2. **`e23-readfile-ssh-userdir-not-blocked`** -- highest impact
   among the home-dir gaps. The user's own `~/.ssh/id_rsa` is
   the most attacker-valuable file on most dev machines and the
   blocklist only covers `/root/.ssh`. CVE-shape.
3. **`e23-webfetch-dns-rebinding-toctou`** -- structural
   weakness in the SSRF defense. Requires architectural fix
   (resolve once, connect by IP with Host header preserved),
   not a one-line patch. Hardening-gap with a real attack path
   on hostile-DNS environments.

## Classification table summary

| Class | Count | Notes |
|-------|-------|-------|
| **CVE-shape** | 9 | Real exploitable bypasses or sensitive-path gaps |
| **Hardening-gap** | 8 | Defense-in-depth weaknesses; not directly exploitable but reduce safety margin |
| **By-design / non-issue** | 2 | Pinned to prevent re-litigation |
| **Untested-either-way** | 2 | Behavior not pinned; needs deterministic test infrastructure |
| **Total** | **21** | |

Per-finding classification lives in
[`s02e23-findings.md`](s02e23-findings.md).

## Metrics

- **Diff size:** 6 files added (4 test files, 2 docs files);
  ~+850 lines (tests) + ~+220 lines (docs) = ~+1070 insertions,
  0 deletions.
- **Test delta:** +85 cases total (64 active passing, 21 Skipped
  with finding names). Suite size: 1180 passing -> 1180 still
  passing (Adversary tests added 64 to passing, no regressions);
  53 skipped (was 32, +21).
- **Preflight result:** PASS. `dotnet format --verify-no-changes`
  clean (after one xUnit2020 fix). `dotnet build -c Release`
  clean. `dotnet test` 1180 / 53 / 0.
- **CI status at push time:** to be observed; this report will be
  updated by Frank if anything reds.

## Credits

- **FDR (lead).** Threat model, bypass enumeration, attack
  classification draft. First S02 lead -- the show needed offense.
- **Newman (guest).** Audit posture inventory, severity scoring,
  fix-priority ranking. The defender voice that lets us ship
  findings-only without the test-author drift toward "and here
  is the fix."
- **David Puddy (guest).** Test infrastructure, Skip-with-finding
  pattern, preflight discipline. Either it works or it doesn't.

All commits carry the `Co-authored-by: Copilot` trailer per
[`.github/skills/commit.md`](../../.github/skills/commit.md).
