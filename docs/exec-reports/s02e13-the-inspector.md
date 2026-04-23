# S02E13 -- *The Inspector*

> Newman finally takes the mic. He audits the v2 security surface
> end to end and pairs every protection with the attack it stops.

**Commit:** see footer (two commits this episode)
**Branch:** `main` (direct push, per `.github/skills/commit.md`)
**Runtime:** ~30 minutes
**Director:** Larry David (showrunner)
**Cast:** Newman (lead, security inspector), FDR (guest, adversarial
red team and chaos -- describes one attack per defense surface)

## The pitch

The v2 surface has accreted defenses across half a dozen episodes
-- credential stores in S02E04, shell hardening in S02E02, SSRF
posture in S02E04, depth caps long before that. Nobody had ever
walked the whole thing top to bottom in one document with the
question "for each protection, what attack does it actually stop?"
This episode is that walk.

The framing is deliberate: each section pairs a Protection block
(file pointer + summary) with an Attack block in FDR's voice
(the adversary the protection defeats), then a one-line Status
verdict. That structure means a security-conscious user can read
one document and trust the project, instead of grepping the
codebase for `Blocked` and inferring intent.

Scope discipline is also the point. The audit names a
NEEDS-FOLLOW-UP and does not fix it. The fix is a future episode.

## Scene-by-scene

### Act I -- Inventory (Newman's voice)

Newman walked the six surfaces in order:

1. `azureopenai-cli/Credentials/` -- four store implementations
   plus the factory. Confirmed whitespace guard on every `Store`,
   `0600` enforcement via `UserConfig.SetRestrictivePermissions`,
   user-scoped DPAPI, `ArgumentList` everywhere, libsecret-via-stdin.
2. `azureopenai-cli/Tools/ShellExecTool.cs` -- substitution / eval
   blocklist, command blocklist applied to every pipe segment,
   HTTP write-form rejection, `ArgumentList` on `/bin/sh -c`,
   sensitive env scrubbing.
3. `azureopenai-cli/Tools/ReadFileTool.cs` -- `BlockedPathPrefixes`,
   tilde expansion, `.env` family special-case with
   `.env.example` allowlist, symlink resolution and re-check.
4. `azureopenai-cli/Tools/WebFetchTool.cs` -- HTTPS-only,
   pre-flight DNS check, `IsPrivateAddress` covering RFC1918 +
   loopback + link-local + IPv6 ULA + IPv6 link-local +
   IPv4-mapped IPv6, post-redirect re-validation with named test
   coverage.
5. Dependency vuln scan -- `dotnet list package --vulnerable
   --include-transitive` and `--deprecated`. Both clean.
6. `azureopenai-cli/Tools/DelegateTaskTool.cs` -- `MaxDepth = 3`,
   parent-set `RALPH_DEPTH = currentDepth + 1` propagation,
   restricted env passthrough.

### Act II -- Adversarial walkthrough (FDR's voice)

For each surface, FDR proposed one concrete attack and the audit
documented the defense path that stops it. Highlights:

- **Whitespace key.** A copy-paste that captures only newlines.
  All four credential stores reject at the entry point.
- **DNS-rebinding-precursor.** A hostname resolving to BOTH a
  public IP and `192.168.1.1`. `web_fetch` refuses if ANY
  resolved address is private.
- **AWS IMDS via redirect.** Public host returns `302 Location:
  http://169.254.169.254/...`. Caught twice: scheme check (HTTPS
  only) and range check (`169.254.0.0/16`).
- **`bash -c 'rm -rf ~'` as a chained command.** The blocklist
  check runs against every segment after `;`, `|`, `&`.
- **Recursion fork-bomb via `delegate_task`.** Depth cap holds at
  3 because depth is parent-set, not child-read.

### Act III -- Findings

- **5 PASS:** credential stores, shell_exec, web_fetch SSRF,
  dependency vulns, subagent depth cap.
- **1 NEEDS-FOLLOW-UP:** `read_file` blocklist names
  `/root/.ssh` but does NOT name the regular user's `~/.ssh`,
  `~/.kube`, `~/.gnupg`, `~/.netrc`, `~/.docker/config.json`,
  `~/.git-credentials`, `~/.config/git/credentials`, or
  `~/.config/gh/hosts.yml`. Cloud-credential paths
  (`~/.aws`, `~/.azure`) ARE covered. The fix is one PR's worth
  of additions to `BlockedPathPrefixes` plus matching xUnit
  cases in `tests/AzureOpenAI_CLI.Tests/SecurityToolTests.cs`.
- **0 GAP** in the strict sense (nothing that this audit would
  block release on). The NEEDS-FOLLOW-UP is real but bounded:
  cloud creds, system files, and the project's own config are
  protected; a user's SSH and Kubernetes secrets are not.

## What shipped

**Production code** -- none. Audit-only episode by design.

**Tests** -- none added. Existing coverage in
`tests/AzureOpenAI_CLI.Tests/ToolHardeningTests.cs` and
`SecurityToolTests.cs` was consumed as evidence (the redirect
test cluster at lines 208-265 is what proves the post-redirect
gate actually fires).

**Docs** -- two new files:

- `docs/security/v2-audit.md` -- the full audit. One section per
  surface, each with Protection / Attack (FDR) / Status, plus a
  Newman-finds executive summary at the top, a summary table, an
  out-of-scope appendix, and a file-pointer witness table.
- `docs/exec-reports/s02e13-the-inspector.md` -- this report.

**CHANGELOG** -- one bullet under `[Unreleased] > Added`.

**Not shipped (intentional follow-ups)**

- Did NOT fix the `~/.ssh` / `~/.kube` / `~/.gnupg` / `~/.netrc` /
  `~/.docker` / `~/.git-credentials` / `~/.config/gh` blocklist
  gap. Named in the audit; future episode (target: "S02E?? The
  Locked Drawer") owns the fix.
- Did NOT add new tests. Existing coverage is what it is for this
  episode.
- Did NOT touch any production code path.
- Did NOT review container / Docker security. That's S02E14
  ("The Container"), Jerry's lead.
- Did NOT formalize a STRIDE threat model.
- Did NOT touch glossary, user-stories, telemetry, runbooks, or
  i18n docs (orchestrator-owned).
- Did NOT touch `AGENTS.md`, top-level `README.md`,
  `.github/copilot-instructions.md`, `.github/agents/*`,
  or the exec-reports `README.md` / `s02-writers-room.md`.

## Lessons from this episode

1. **The audit framing works.** Pairing each protection with the
   specific attack it stops makes the document useful to a
   non-author reader. Without the FDR voice, the audit reads like
   a checklist; with it, it reads like a defense.
2. **`read_file` has a real gap and naming it loudly is the
   point.** Cloud-credential paths got covered when the cloud-credential
   episode shipped; SSH / Kubernetes / GPG / netrc never had their
   episode. Now they do, in the form of a named follow-up with a
   concrete prefix list and a test recipe.
3. **`--include-transitive` matters and the CI gate is the
   durable defense.** A clean run today binds nothing about
   tomorrow. The witness command and date are in the audit so
   future re-audits can compare.
4. **Test coverage as audit evidence.** Citing
   `ToolHardeningTests.cs:212-263` by name turned "we have a
   redirect check" into "and here is the test that proves it
   fires." Future audits should keep doing this.

## Metrics

- Diff: two new docs (`docs/security/v2-audit.md`,
  `docs/exec-reports/s02e13-the-inspector.md`) plus a one-bullet
  CHANGELOG edit.
- Test delta: zero.
- Production code delta: zero.
- Preflight: not required (docs-only; no `.cs` / `.csproj` /
  `.sln` / workflow files touched).
- Vuln scan run on 2026-04-23: 0 vulnerable, 0 deprecated.
- Audit verdict: 5 PASS / 1 NEEDS-FOLLOW-UP / 0 GAP.

## Credits

- **Newman** -- lead. Walked all six surfaces, wrote the
  Protection blocks, named the NEEDS-FOLLOW-UP without flinching.
- **FDR** -- guest. One adversarial scenario per surface; gave
  the document its teeth.
- **Larry David** -- showrunner / director. Cast Newman in the
  lead and kept the scope tight.

Co-authored-by trailer is on every commit per
`.github/skills/commit.md`.
