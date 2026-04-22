# Contract Tests

> Contract tests gate **user-visible promises**. They fail loudly when a
> public interface drifts — exit codes, stdout/stderr discipline, version
> strings, tool-call JSON shapes, doc claims that encode constants.
> They exist so downstream consumers (Espanso, AHK, shell pipelines,
> Homebrew formula tests, Scoop manifests, CI scripts) don't silently break.

## Canonical example — `VersionContractTests`

`tests/AzureOpenAI_CLI.V2.Tests/VersionContractTests.cs` is the reference
implementation for "this is what a contract test looks like."

**What it pins:**

- `Program.VersionSemver` matches the bare semver regex `^\d+\.\d+\.\d+$`.
- `Program.VersionSemver`, `Program.VersionFull`, and
  `Telemetry.ServiceVersion` all agree with the csproj `<Version>` element
  resolved via `Assembly.GetName().Version`.
- A negative assertion rejects the specific stale literal (`"2.0.2"`) that
  caused audit finding C-1.

**What it caught in production:**

The v2.0.3 and v2.0.4 binaries shipped with `Program.VersionSemver` and
`Telemetry.ServiceVersion` hardcoded to `"2.0.2"`. `az-ai-v2 --version
--short` reported the wrong string on the v2.0.4 tag; `brew test az-ai-v2`
would have failed against the published tarball. This test would have
caught it at PR time. It now does.

**What makes it a contract test (not a unit test):**

- It asserts against a **public, user-observable surface** (`--version`
  output, telemetry service.version attribute).
- It fails on **behaviour drift**, not implementation detail — if the field
  moves from `Program` to `Versioning.Current`, the test follows; if the
  *value* regresses, the test fires.
- It is intentionally cheap so it runs on every PR.

## What counts as a contract test

A test is a contract test if **all three** of the following hold:

1. **User-observable output.** Exit code, stdout/stderr bytes, a JSON shape
   a downstream tool parses, a documented flag name, an env-var semantics,
   a file path the CLI writes.
2. **External consumer.** Someone outside this repo (Espanso, AHK, Homebrew
   formula, Scoop manifest, shell pipeline, CI script, another service's
   tool call) will break if the promise silently drifts.
3. **The test asserts the promise directly.** Not "the code internally does
   X" — instead "running the binary / calling the public method produces
   observable Y."

If only #1 and #3 hold (public surface, no known external consumer), it's
still a useful test — just file it as a regression or unit test. Contract
tests earn the label by having a named external victim of drift.

## Current contract suite

| Contract | File | Promise | Victim of drift |
|---|---|---|---|
| `--version` strings | `tests/AzureOpenAI_CLI.V2.Tests/VersionContractTests.cs` | `VersionSemver` / `VersionFull` / `ServiceVersion` stay locked to csproj `<Version>`; no hardcoded literals. | Homebrew `brew test`, Scoop manifest checksum, `az-ai-v2 --version --short` users, OpenTelemetry consumers. |
| Ralph exit codes | `tests/AzureOpenAI_CLI.V2.Tests/RalphExitCodeTests.cs` | `az-ai-v2 --ralph` exits 0/1/2/… per the documented spec. | Espanso snippet `:ralph`, `ci.yml` ralph step, any shell wrapper conditionally branching on `$?`. |
| `--raw` stderr discipline | `tests/AzureOpenAI_CLI.V2.Tests/RawModeTests.cs` | `--raw` writes ONLY model content to stdout; banners/prompts/warnings go to stderr or are suppressed. | Espanso / AHK / `<<<` shell pipes reading stdout verbatim. |
| Tool hardening | `tests/AzureOpenAI_CLI.Tests/ToolHardeningTests.cs` | `TryGetProperty` parse pattern (no throws on missing fields); SSRF redirects blocked; missing params → structured error, not crash. | Agent loop tool-call JSON shape; any consumer parsing tool error output. |
| Security doc claims | `tests/AzureOpenAI_CLI.Tests/SecurityDocValidationTests.cs` | Constants in `SECURITY.md` match constants in code (blocklist size, recursion depth, timeout bounds). | Readers of SECURITY.md; the doc can't lie. |

## Naming convention

Contract test classes end in `ContractTests.cs`:

- ✅ `VersionContractTests`
- ✅ `RalphExitCodeTests` *(legacy name — acceptable; would be
  `RalphExitCodeContractTests` under today's convention)*
- ✅ `RawModeContractTests` *(if added new)*
- ❌ `TestVersion`, `VersionTest1`, `CheckVersion`

**Within the class**, method names follow the BDD pattern from
`bdd-guide.md`:

- ✅ `VersionSemver_matches_csproj_version` — subject, verb, expected.
- ✅ `VersionSemver_is_not_the_stale_v2_0_2_literal` — negative contract
  naming the specific drift it guards against.
- ❌ `Test1`, `ItWorks`, `ShouldPass`.

Each negative-contract test should name the **specific regression** it
prevents in the method name or doc comment. If the comment says "added
after v2.0.4 C-1", future readers know what it's for.

## When to add a contract test

Add one when the PR introduces any of the following:

- A new exit code, or a change to an existing one.
- A new CLI flag that downstream scripts will depend on.
- A new output format (JSON shape, log line format, stderr banner).
- A new env-var the CLI reads (`AZ_CACHE_DIR`, `NO_COLOR`,
  `AZUREOPENAIMODEL`, …).
- A new file path the CLI writes or a new directory layout
  (`.squad/history/`, cache dirs).
- A new tool-call contract (JSON shape an agent emits).

Also add one **defensively** after a bug fix if the bug's root cause was
drift in a user-visible promise — that's the `v2.0.4` lesson. The
regression test for the specific literal belongs in the contract suite.

## How contract tests differ from regular unit tests

| | Unit test | Contract test |
|---|---|---|
| **Scope** | Pure function / class internal. | Public surface the world sees. |
| **Changes when…** | Implementation changes. | Promise changes. |
| **Breaks when…** | Implementation breaks. | User-observable behaviour drifts. |
| **Review lens** | "Does the code do the right thing internally?" | "Does this PR intentionally change a promise a consumer depends on? Is the change called out in the commit message?" |
| **Allowed to mock** | Yes, heavily. | Rarely — the point is to exercise the real surface. Version contract reads `Assembly.GetName()`, not a test double. |
| **Update cadence** | Every refactor. | Only when the promise deliberately changes — and then the CHANGELOG and release notes change too. |

A failing contract test is never "flaky" — it's either a real regression
or an intentional change. Reviewer's job: which one is it, and is the
commit message honest about it?

## Adding a new contract test — checklist

- [ ] Named with the `ContractTests` suffix (or a close historical variant
      — don't rename legacy files just for aesthetics).
- [ ] Placed in the right project (v1 → `AzureOpenAI_CLI.Tests`; v2 →
      `AzureOpenAI_CLI.V2.Tests`).
- [ ] Asserts against the **public surface**, not an internal helper.
- [ ] Has at least one **negative contract** naming the specific drift it
      prevents (`IsNotTheStale_X_Literal`).
- [ ] Doc comment on the class explains **which audit / bug / release**
      motivated the contract. Future readers should be able to unwind why.
- [ ] Cheap enough to run on every PR (< 100 ms). If the contract is
      expensive to verify, split: cheap pin on main, expensive replay
      behind a chaos or integration job.

## Cross-references

- [`README.md §5`](./README.md) — the contract-test table in the testing
  playbook (authoritative source).
- [`bdd-guide.md`](./bdd-guide.md) — Given/When/Then naming.
- [`../adr/ADR-003-behavior-driven-development.md`](../adr/ADR-003-behavior-driven-development.md)
  — decision record.
- [`../audits/docs-audit-2026-04-22-puddy.md`](../audits/docs-audit-2026-04-22-puddy.md)
  — audit finding H4 / M4 that called out the missing contract-test doc.

*Either the promise holds or it doesn't. Contract tests make that question
a green/red, not a judgement call. High-five.*
