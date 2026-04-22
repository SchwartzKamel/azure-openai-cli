# Hardening Checklist — v2 one-page rollup

> *Hello. Newman.* One page. Copy it into a PR-review comment. Tick the
> boxes. If a box is empty, the PR is not ready.

**Status:** Canonical
**Last updated:** 2026-04-22
**Audience:** reviewers, release managers, operators doing a pre-deploy
sanity check.
**Scope:** v2.0.x. Some rows apply to v1 as well; column notes where.

---

## 1. Build + binary

- [ ] **AOT publish flags verified.** `Dockerfile.v2:65` passes
  `-p:PublishAot=true` and csproj declares AOT. No `PublishSingleFile` /
  `PublishTrimmed` drift in v2 build output.
- [ ] **No credentials baked into image.** Search `COPY`/`ADD` in
  `Dockerfile.v2` — only the compiled binary + minimum runtime deps.
- [ ] **Base image digest-pinned.** `Dockerfile.v2:80` has
  `@sha256:...`, not `:latest` or a floating tag.
- [ ] **Non-root execution.** `USER appuser` before `ENTRYPOINT`.
- [ ] **Build provenance attestation wired.** `.github/workflows/release.yml:103, 287`
  includes `attest-build-provenance@e8998f9`.
- [ ] **SBOM emitted per RID.** `release.yml:95, 277` runs
  `dotnet dotnet-CycloneDX`. See [`sbom.md`](./sbom.md).

## 2. Tool surface

- [ ] **ShellExecTool blocklists reviewed against source.**
  - Destructive list: `ShellExecTool.cs:17-18`.
  - Privilege/interactive list: `ShellExecTool.cs:19-20`.
  - `$()`, backticks, `<()`, `>()` substitution block: `:53, 57`.
  - `eval`/`exec` prefix block: `:58-59`.
  - `ContainsHttpWriteForms` curl/wget upload block: `:79-80, 131-186`.
  - Tab/newline first-token rescan: `:69`.
  - Env scrub (`SensitiveEnvVars`): `:32-42`.
- [ ] **Every blocklist entry has a `ToolHardeningTests` case.**
  `tests/AzureOpenAI_CLI.V2.Tests/ToolHardeningTests.cs`.
- [ ] **WebFetchTool SSRF guard re-checked post-redirect.** Final URL (not
  request URL) validated against RFC1918 / loopback / link-local / ULA.
- [ ] **ReadFileTool path blocklist + canonicalization active.**
  `/etc/shadow`, `~/.ssh`, credential stores. Canonical path checked
  **after** symlink resolution.
- [ ] **GetClipboardTool caps enforced.** 32 KB + 5 s timeout + PATH-based
  command detection.

## 3. Subagent containment

- [ ] **`DelegateTaskTool.MaxDepth = 3`** (`DelegateTaskTool.cs:34`).
  Depth propagated via `AsyncLocal<int>`, not env.
- [ ] **`ToolRegistry.DefaultChildAgentTools` allowlist reviewed.**
  Excludes `delegate_task` + `get_clipboard` by default.
- [ ] **Child agent cannot re-escalate.** Verified via recursion test.

## 4. Secret handling

- [ ] **`UnsafeReplaceSecrets` called on all user-facing error paths.**
  `Program.cs:604, 619`. See [`redaction.md`](./redaction.md).
- [ ] **Exception chain unwrapped ≤ 5 levels before redaction.**
- [ ] **No API key echoed under debug flag.** `grep -n 'AZUREOPENAIAPI' azureopenai-cli-v2/`
  — no `Console.WriteLine`, no log emission, no attribute tagging.
- [ ] **`--raw` + `--json` suppress config-parse stderr noise.**
  `UserConfig.Load(quiet:)` respected.

## 5. Exit codes (script contract)

- [ ] `0` — success.
- [ ] `1` — validation / usage error, or Ralph `--max-iterations` exhaustion.
- [ ] `2` — CLI parse error.
- [ ] `99` — unhandled error.
- [ ] `130` — SIGINT / user interrupt, preserved end-to-end.

Any CI script consuming CLI output MUST treat all non-zero as failure and
MUST NOT retry on `130` (operator interrupt is intentional).

## 6. Supply chain

- [ ] **Direct deps pinned exact** in csproj (no `*` or range specifiers).
  See [`supply-chain.md`](./supply-chain.md).
- [ ] **Actions pinned by SHA.** `grep -rn 'uses:' .github/workflows/`
  — every line has `@<sha>` with `# v<tag>` comment.
- [ ] **Trivy gate green on PR.** `.github/workflows/ci.yml:119`.
- [ ] **No new alternate NuGet feed added** without Newman + Jerry sign-off.
- [ ] **SBOM attached to release.** See [`sbom.md`](./sbom.md) §3.

## 7. Disclosure + paperwork

- [ ] **`SECURITY.md` last-updated date within the last 90 days** or
  carries a note explaining why the older date is still current.
- [ ] **Prior audits linked from [`docs/security/index.md`](./index.md).**
- [ ] **[`cve-log.md`](./cve-log.md) up to date.** Any advisory filed in
  the last 90 days recorded; even "no activity" is a dated statement.
- [ ] **Threat model refreshed** when a new tool / flag / subagent landed.
  See [`docs/runbooks/threat-model-v2.md`](../runbooks/threat-model-v2.md).

## 8. Operator-side (add to deploy runbooks)

- [ ] `.env` is `chmod 600` and gitignored.
- [ ] Service principal scoped to `Cognitive Services OpenAI User` on
  the specific resource, not subscription-wide.
- [ ] Container launched with `--rm` (default in `make run`).
- [ ] Production: add `--read-only --cap-drop=ALL`.
- [ ] Key rotation cadence set (≤ 90 days recommended).

---

## 9. Use in review

Paste this checklist into the PR description (collapse if long). Any
unchecked box needs a link to the follow-up issue or a note in
[`cve-log.md`](./cve-log.md) under "accepted residual."

Missing-checklist PRs that touch `azureopenai-cli-v2/Tools/**` or any
file referenced above are **release-blockers**. Mr. Wilhelm + Newman both
veto.

---

*The paperwork is the discipline. Fill in the boxes.* — Newman
