# FR-021 — PersonaMemory `ArgumentException` UX wrap

**Status:** Accepted — scheduled for 2.0.1
**Owner:** Kramer (impl) · Puddy (regression test) · Lippman (release)
**Related:** commit `a0ca066` (PersonaMemory F1/F2/F3 hardening), FDR chaos drill `docs/chaos-drill-v2.md`, Costanza go/no-go `docs/v2-cutover-decision.md` §Judgment calls

## Problem

A malformed `.squad.json` persona name (anything outside `[a-z0-9_-]{1,64}` — empty string, whitespace, unicode, path-traversal attempt, over-long) surfaces to the user as an **uncaught `System.ArgumentException`** with a stack trace, exiting **134** instead of cleanly **exiting 1 with an `[ERROR]` line**.

The offending call site is `Program.cs` ~line 321:

```csharp
string history = PersonaMemory.ReadHistory(activePersona.Name);
```

`SanitizePersonaName` (public static, introduced in `a0ca066`) is the correct gate, but it is called *inside* `ReadHistory` / `AppendEntry` / `Prune` rather than *before* the persona path is selected, and the caller does not wrap.

## What is NOT the bug

- **Security is intact.** `SanitizePersonaName` still rejects the malicious name. No file is read, no traversal succeeds, no write lands outside the sandbox.
- **The chaos drill F3 finding is closed** by `a0ca066`. The remaining issue is cosmetic: wrong exit code, stack trace instead of clean message.

Costanza's judgment (`docs/v2-cutover-decision.md`): **ship 2.0.0 as-is**, patch in 2.0.1. The UX gap is tracked here plus a Known-Limitations bullet in the release notes.

## Fix

Three-line wrap at the persona-activation site:

```csharp
string history;
try
{
    history = PersonaMemory.ReadHistory(activePersona.Name);
}
catch (ArgumentException ex)
{
    return ErrorAndExit(
        $"Invalid persona name in .squad.json: {ex.Message}. " +
        "Persona names must match [a-z0-9_-]{1,64}.",
        exitCode: 1, isJson: options.Json, isRaw: options.Raw);
}
```

Apply the same wrap to any other direct call into `PersonaMemory.*(name, ...)` at the top of `Program.cs`'s persona code path. Audit with `grep -n 'PersonaMemory\.' azureopenai-cli-v2/Program.cs`.

### Alternative considered — and rejected

**Pre-validate at `.squad.json` load time.** Would require `SquadConfig` to fail its JSON-load step on a bad name, which changes the semantics of "config with one bad persona": today every other persona still works. Wrap at the call site preserves that behavior; pre-validate would regress it.

## Acceptance

- [ ] Malformed persona name → exit 1 (not 134), single `[ERROR]` line to stderr, no stack trace.
- [ ] With `--json`, error emits structured `ErrorJsonResponse` to stderr (consistent with other `ErrorAndExit` paths).
- [ ] Regression test in `PersonaMemoryHardeningTests` that feeds a bad `.squad.json` persona name through the Program entry point and asserts exit 1 + `[ERROR]` prefix.
  - **Test prewritten (Puddy, pre-2.0.1):** `tests/integration_tests.sh` lines 698–770 (skipped by default; un-skip with `FR021_FIXED=1`). Forced run against 2.0.0 FAILS as expected (exit 134 + stack trace), proving the test exercises the real failure mode, not a mock of it. The 2.0.1 PR should flip the sentinel default (or drop the `FR021_FIXED` guard) once the wrap lands — no new test code needed.
- [ ] No change to `PersonaMemory.SanitizePersonaName` behavior — this is a caller-side wrap only.

## Out of scope

- Reworking `SanitizePersonaName` rules (stay at `[a-z0-9_-]{1,64}`).
- Any change to the security posture of `PersonaMemory` — `a0ca066` is final for 2.0.0.
- Pre-load validation in `SquadConfig` (explicit design choice — see Alternative).
