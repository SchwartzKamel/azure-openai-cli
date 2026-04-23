# S02E07 -- *The Observability*

> *"SERENITY NOW! The default is silence. Anything else is a flag the
> user flipped on, on purpose."* Frank Costanza writes the honesty doc
> and three runbooks; Lloyd asks what an SLO is; George asks what we
> actually owe the user.

**Commits:**
`87d190d` docs(observability) -- telemetry posture + runbooks ·
`(this commit)` docs(exec-reports) -- this report
**Branch:** `main` (direct push, solo-led repo per `.github/skills/commit.md`)
**Runtime:** ~25 minutes, audit + writing
**Director:** Larry David (showrunner)
**Lead:** Frank Costanza (SRE / observability / incident response)
**Guests:** Lloyd Braun (junior dev), George Costanza (PM)

## The pitch

The S02E04 audit (`The Locksmith`) reported zero default telemetry in
the tree. Since then, v2 Phase 5 shipped an opt-in OpenTelemetry
pipeline (`azureopenai-cli-v2/Observability/Telemetry.cs`) -- gated
behind `--telemetry`, `--otel`, `--metrics`, and `AZ_TELEMETRY=1`,
with a documented schema in `docs/observability.md`. The user-facing
posture didn't change (still off by default), but the project never
wrote a contributor-readable, one-page "what we collect and why we
don't" doc that a new user could find from the README without reading
the 118-line observability spec. Equally, when users hit a 401, a
429, or a TLS failure, the only triage trail was source comments and
chaos-test scripts -- no dedicated runbook.

This episode ships that one-page honesty doc plus three short
incident runbooks for the failures we see most often. Zero production
code touched.

## Scene-by-scene

### Act I -- Audit (Frank)

`grep -rniE 'telemetry|analytics|posthog|mixpanel|appinsights|sentry'`
across the tree. Findings:

- `azureopenai-cli/` (v1): zero telemetry surface. Confirmed.
- `azureopenai-cli-v2/Observability/Telemetry.cs`: opt-in OTel
  pipeline, off by default, lazy-init gated on
  `OTEL_EXPORTER_OTLP_ENDPOINT`. `RecordRequest` short-circuits when
  `_enabled == false`. Schema in `CostEvent.cs` carries timestamp,
  model, token counts, USD estimate, mode -- no prompt text, no
  endpoint URL, no key fingerprint, no file paths.
- `tests/AzureOpenAI_CLI.V2.Tests/ObservabilityTests.cs` and
  `TelemetryLazyInitTests.cs` pin the off-by-default contract.
- `docs/observability.md` exists (118 lines) and documents the
  schema. It does not, however, restate the *posture* in TL;DR form
  for a user who lands on the repo and asks "does this thing call
  home?"

Decision: don't restate the schema; cross-link to it. Write the
posture page. Add audit greps so a contributor can verify
independently in 60 seconds.

### Act II -- `docs/telemetry.md` (Frank + George)

One page. Sections:

1. TL;DR -- "zero default telemetry."
2. What is collected by default -- "nothing," with the four real
   output channels (stdout / stderr / `~/.azureopenai-cli.json` /
   HTTPS to Azure) enumerated so the answer is concrete.
3. **Costanza notes** callout -- the product trade. George's input:
   "say out loud that the cost of zero telemetry is hearing about
   regressions only when users file issues. Don't pretend it's free."
4. What can be enabled (the v2 opt-in table, cross-linked to
   `docs/observability.md` for the full schema).
5. What is NEVER collected, even when telemetry is on -- the
   PII-leak negative list (prompt text, endpoint URL, key
   fingerprint, file paths, clipboard, shell commands, env vars,
   hostname).
6. **Verifying for yourself** -- five `grep` commands a contributor
   can paste to audit the tree. This is the load-bearing section.
7. Disabling -- short, since the answer is "already disabled."

### Act III -- `docs/incident-runbooks.md` (Frank + Lloyd)

Three runbooks, each ~150 words, identical structure: symptoms ->
likely causes (ranked) -> recovery steps (ordered).

- **401 auth** -- leads with the `AZUREOPENAIAPI` vs `AZUREOPENAIKEY`
  env-var-name trap, because that's the single most common cause we
  see in support traffic. Then key rotation, then access revocation,
  then endpoint/key mismatch.
- **429 rate limit** -- TPM vs RPM ranked, with `--model` fallback
  for multi-deployment users and the "request quota increase"
  pointer.
- **DNS / TLS** -- captive portals + corporate TLS-intercept proxies
  ranked above stale CA bundles. Includes the
  `curl -sS -o /dev/null -w '%{http_code}'` reproducer for
  isolating the network from the CLI.

**Lloyd's callout** ("what's an SLO?") sits at the top of the page,
after the intro and before the runbooks, so a junior reader meets the
concept before the runbook prose. Two sentences, plain English,
honest about the fact that the project has aspirational targets only
-- not formal SLOs -- because we don't have a shared production
deployment to measure against. Pointer to `docs/glossary.md` (Babu's
S02E08) for the canonical entry.

### Act IV -- CHANGELOG + ship

Two surgical bullets at the top of `[Unreleased] > Added`. No code,
no tests, no preflight required (docs-only diff, all `.md`).
Pre-validation: ASCII-quote/dash grep clean across the three new
files. Two commits, both signed off with the Copilot co-author
trailer per `.github/skills/commit.md`.

## What shipped

**Production code** -- none.

**Tests** -- none.

**Docs**

- `docs/telemetry.md` (new, ~140 lines) -- posture, opt-in table,
  PII-leak negative list, audit greps, Costanza callout.
- `docs/incident-runbooks.md` (new, ~155 lines) -- three runbooks,
  Lloyd SLO callout near the top.
- `CHANGELOG.md` -- two bullets at the top of `[Unreleased] > Added`,
  one per new doc.

**Not shipped** (intentional follow-ups, scope discipline):

- Did NOT add telemetry. Posture remains zero-default.
- Did NOT add `--verbose` or `--config show` flags.
- Did NOT define formal SLOs (Frank's writers' room note: SLOs need
  real users; we have aspirational targets only). Recorded
  explicitly in Lloyd's callout.
- Did NOT add structured logging, metrics export, or tracing.
- Did NOT touch `Program.cs` or any production code path.
- Did NOT create or edit `docs/glossary.md` (Babu's S02E08) or
  `docs/user-stories.md` (S02E11). Cross-linked to `glossary.md`
  for the canonical SLO entry, no new file.
- Did NOT touch `docs/observability.md` (already canonical for the
  schema; we cross-link to it).

## Lessons from this episode

1. **Audit the audit.** S02E04 said "zero telemetry." That was true
   then and is *still* true for the default code path, but v2 grew an
   opt-in pipeline in between. The honesty doc has to capture both
   facts at once or it's lying by omission.
2. **The PII negative list is the load-bearing claim.** Saying
   "telemetry is opt-in" reassures no one without the explicit list
   of what is NEVER collected even when the flag is on. The list
   doubles as a contract for whoever extends `RecordRequest` next.
3. **Runbooks lead with the typo.** The most common 401 cause is the
   env-var name confusion (`AZUREOPENAIAPI` vs `AZUREOPENAIKEY`).
   That goes at the top of the causes list, not buried in step 4.
4. **Don't define SLOs without users.** Lloyd's callout makes the
   honest distinction between aspirational targets and contractual
   ones. Better to say "we don't have these yet" than to publish a
   number we can't defend.

## Metrics

- Diff size: +3 files (`docs/telemetry.md`, `docs/incident-runbooks.md`,
  this report) + 1 surgical CHANGELOG edit. ~+450 lines added,
  0 deleted, 0 production code paths touched.
- Test delta: 0 new tests, 0 changed (docs-only).
- Preflight: not required per `.github/skills/preflight.md` (no
  `.cs` / `.csproj` / `.sln` / workflow changes). Smart-quote /
  em-dash grep run manually on all three new files; clean.
- CI status at push time: see commit footers.

## Credits

- **Frank Costanza** -- lead. Owned the audit, the telemetry posture
  doc, the three runbooks, and the "what is NEVER collected" list.
- **Lloyd Braun** -- guest. Asked what an SLO is; got a plain-English
  two-sentence answer placed near the top of the runbooks doc, with
  honest framing on why we don't have formal ones.
- **George Costanza** -- guest. PM input on the Costanza callout in
  `docs/telemetry.md`: name the trade-off out loud, don't pretend
  zero-telemetry is free.
- **Larry David** -- showrunner. Cast the episode, scoped it to
  docs-only, kept the "did not do" list honest.
- **Copilot** -- co-author trailer on every commit per
  `.github/skills/commit.md`.
