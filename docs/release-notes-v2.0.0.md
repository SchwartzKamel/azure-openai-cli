# Azure OpenAI CLI v2.0.0 — Release Notes

🚀 🎉

> **Release window opens 2026-04-20.** 2026-04-20 is the commit-cutoff date
> for the 2.0.0 line, not a publication-date commitment. Availability on
> Homebrew / Scoop / Nix / GHCR follows once Costanza signs the go/no-go.

## Headline

v2.0.0 rebuilds `az-ai` on top of **Microsoft Agent Framework (MAF)** —
swapping ~2,200 lines of hand-rolled chat, tool, and Ralph orchestration
for first-party primitives — while preserving every v1 flag, env var,
exit code, and `--raw`/`--json` output byte-for-byte.

## Why you'll care

- **Persona routing actually works.** `--persona coder` / `--persona auto`
  now overlay the persona's system prompt, tool allow-list, and `.squad/`
  memory on every run. The flag was parsed-but-ignored in earlier v2
  previews; it is fully wired in 2.0.0.
- **`--estimate` tells you the bill before you pay it.** Offline,
  zero-network, safe to gate CI pipelines on. Reads the local price
  table (or `AZAI_PRICE_TABLE`) and prints a predicted USD figure.
- **Opt-in OpenTelemetry.** `--telemetry` (or `AZ_TELEMETRY=1`) emits
  spans and per-call cost events. Off by default, zero overhead when
  disabled, silent under `--raw`.
- **Connection prewarming.** `--prewarm` opens the Azure OpenAI channel
  in parallel with prompt assembly. Buys back most of the MAF cold-start
  cost for interactive use.
- **Same security posture as v1.** Every hardening Newman landed in
  v1.0.x–v1.9.x — shell blocklist, HTTPS-only fetch, DNS-rebinding
  guards, symlink traversal blocks, exact tool-name matching, SIGINT
  exit 130 — carries forward byte-for-byte. No new attack surface beyond
  MAF/OTel themselves, which are reviewed in the licensing audit.

## Breaking changes

**For end users invoking `az-ai` from the command line, Espanso, or
AutoHotkey: none.** Every v1 flag still works. Every env var reads the
same. Every exit code is unchanged. `--raw` output is byte-identical.
`.squad/` on-disk format is unchanged; accumulated persona memory
transfers across the upgrade untouched.

The 2.0.0 bump is driven by the **public transitive dependency surface**
(MAF, OpenTelemetry, Azure SDK) and the **packaging rename to `az-ai-v2`
during the dual-tree window**, not by behavioral contract changes.

Subtle deltas that are _not_ breaking but worth calling out:

- **`--max-rounds` loop accounting** now runs through MAF tool-call
  tracking. The cap itself is unchanged; logged round counts may differ
  by ±1 on edge cases.
- **Ralph retry prompt** is shorter — the task is carried by
  `AgentThread`, and only accumulated error context is re-injected per
  iteration.
- **Precedence chain** is unchanged from v1: persona `system_prompt`
  overrides `--system` overrides `SYSTEMPROMPT`; a non-empty persona
  `tools` array overrides `--tools` and forces `--agent` on.
- **`--estimate` short-circuits before credential resolution** — it will
  not read `AZUREOPENAIAPI`, will not hit the network, and works offline.

See [`docs/migration-v1-to-v2.md`](migration-v1-to-v2.md) §3 for the
full table.

## New features

### Persona mode

```bash
az-ai-v2 --persona coder "Refactor BUG-142 out of src/auth.py"
az-ai-v2 --persona auto "Audit tokens.py for SSRF and injection"
# → 🎭 Auto-routed to: security (Security Auditor)
```

Personas live in `.squad.json` plus `.squad/history/<name>.md` per-persona
memory. `--persona auto` picks by deterministic keyword match — no model
call to decide the persona. Full reference in
[`docs/persona-guide.md`](persona-guide.md).

### `--estimate`

```bash
az-ai-v2 --estimate "Summarize the attached RFC: $(cat rfc.md)"
# → Estimated input: 4,812 tokens
# → Estimated cost (gpt-4o-mini): $0.00072 USD

az-ai-v2 --estimate-with-output 2000 "Draft a design doc for FR-014"
# → Estimated cost (worst case, 2000 output tokens): $0.00143 USD
```

Zero-network. Safe in CI budget gates. Reads the hardcoded price table
or `AZAI_PRICE_TABLE`.

### `--telemetry`

```bash
AZ_TELEMETRY=1 az-ai-v2 --agent "Find the failing test and explain"
# → stderr carries OTel spans + per-call cost events as JSON
az-ai-v2 --raw --telemetry ...    # telemetry silenced by --raw
```

Opt-in. Off by default. Scope with `--otel` (traces only) or `--metrics`
(meters only).

### `--prewarm`

```bash
az-ai-v2 --prewarm "Quick question"
```

Opens the Azure OpenAI connection in parallel with prompt assembly.
Recovers most of the MAF cold-start cost on the first interactive call.
See [FR-007](proposals/FR-007-parallel-startup-and-connection-prewarming.md).

### Cost hook + pricing table

The estimator is backed by a pluggable price table. Set
`AZAI_PRICE_TABLE=/path/to/prices.json` to override per deployment. The
same hook feeds `--telemetry`'s per-call cost events, so estimator and
observed-cost outputs agree on the pricing model.

## Behavioral deltas (from the migration guide)

| Area | v1.9.x | v2.0.0 |
|------|--------|--------|
| Ralph retry prompt | Re-sent full task every iteration | Task carried by `AgentThread`; only error context re-injected |
| Telemetry | N/A | Opt-in via `--telemetry` / `AZ_TELEMETRY=1`; zero overhead when off |
| `--estimate` | N/A | Short-circuits before credentials / network; works offline |
| `--persona` | Parsed but not applied in early v2 | Fully wired: system prompt + tools + memory |
| FR-017 | Runtime crash on gpt-5.x/o1/o3 in late v1.9.x (fixed in 1.9.1) | Baked in from day one |

None of these change CLI output format or exit codes.

## Performance & size

Measured on `linux-x64` AOT (shipping form, 50 runs, 2 warmup) —
[`docs/perf-baseline-v2.md`](perf-baseline-v2.md) has the full table.

**Startup p95 is inside the ≤25% regression budget.** `--version --short`
lands at 1.12× v1 (12.58 ms mean p95 18.25 ms), `--help` at 1.23× v1
(14.64 ms mean p95 18.78 ms), `parse-heavy` is actually _faster_ than v1
(0.93× mean). RSS is at or below v1 on every scenario.

**Binary size is bigger.** The AOT single-file binary grew from 9.29 MB
(v1.9.1) to 15.10 MB (v2.0.0) — a 1.62× increase, or about +5.8 MB.
That is the cost of the MAF host + OpenTelemetry + the Azure SDK bump.
The proposed 1.5× ratio gate does not pass; we are shipping with a
documented waiver and an AOT trim pass scheduled for 2.0.1.

If your use case is latency-critical (Espanso triggers, AHK hotkeys),
the hot path is safe. If you deploy over metered bandwidth, the extra
5.8 MB is the honest tradeoff for agentic orchestration.

## Security

- **v1 hardening preserved byte-for-byte.** Shell blocklist,
  `ArgumentList`-based spawning, HTTPS-only fetch, DNS-rebinding guards,
  redirect-final-URL validation, symlink traversal blocks, exact-alias
  tool matching, SIGINT exit 130. No regressions; no relaxations.
- **Licensing audit: clear.** 39 packages reviewed across the v2 graph —
  34 MIT, 4 Apache-2.0 (OpenTelemetry), 1 BSD-3-Clause (Google.Protobuf).
  Zero GPL / LGPL / AGPL / MPL / SSPL. Attribution obligations are
  discharged via `NOTICE` and `THIRD_PARTY_NOTICES.md` at the repo root;
  every distributed artifact (tarball, container, Homebrew/Scoop/Nix
  bundle) includes all three files. See
  [`docs/licensing-audit.md`](licensing-audit.md).

## Known limitations at 2.0.0

Tracked in the issue tracker; not blockers for cutover.

- **`--schema <json>` wire enforcement is deferred to 2.1.x.** The flag
  is parsed and captured, but not yet sent as a `response_format` strict
  schema. Use `--json` + post-validation for now.
- **AOT binary is 1.62× v1 — over the 1.5× ratio gate.** Trim pass
  scheduled for 2.0.1. Stripping OTel exporters, investigating the
  `Azure.AI.OpenAI` trim warnings, and tuning `DebuggerSupport` /
  `EventSourceSupport` are the top candidates.
- **Windows CI is not yet enabled.** Linux (glibc, musl, arm64) and
  macOS (x64, arm64) run per release. Windows binaries are produced and
  manually validated pre-release.
- **arm64 release matrix rolls forward to 2.0.1.** If arm64 is your
  primary platform, pin `v1.9.1` until 2.0.1 ships.
- **Multi-provider (FR-014) is a spike, not a feature.** v2.0.0 remains
  Azure-OpenAI-only.

## Upgrading / rolling back

See [`docs/migration-v1-to-v2.md`](migration-v1-to-v2.md) for the full
story. The short version:

**During the dual-tree window** (pre-cutover) both binaries install side
by side:

```bash
az-ai    --version --short   # → 1.9.1
az-ai-v2 --version --short   # → 2.0.0
```

Both read the same env vars and config files. No migration step is
required; accumulated `.squad/` memory transfers untouched.

**Post-cutover** the v2 binary is installed as `az-ai`. To pin v1:

```bash
# Homebrew:
brew install schwartzkamel/tap/azure-openai-cli@1.9.1

# Scoop:
scoop install azure-openai-cli@1.9.1

# Manual: download from the v1.9.1 release page.
```

The v1.x branch continues to receive critical security fixes. No new
features will land there.

## Thanks

v2.0.0 development commits between `9e74961..488aebd`:

```
15  SchwartzKamel
```

Co-authored by GitHub Copilot across the v2 migration per project
convention. Thanks also to the cross-agent roster — Costanza (PM),
Wilhelm (change mgmt), Elaine (docs), Kenny Bania (perf), Jackie Chiles
(licensing), Newman (security), Kramer (Docker/AOT), Jerry (DevOps),
Puddy (QA), and everyone else on the bench — for the review passes that
got us to the gate.

---

**Release manager:** Mr. Lippman.
**Questions?** File an issue or see
[`CONTRIBUTING.md`](../CONTRIBUTING.md).
