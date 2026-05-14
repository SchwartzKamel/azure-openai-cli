# S05 *The Workflow* -- full-season plan

- **Season number:** 5
- **Theme:** The Workflow
- **Anchor release:** v2.5.0 at S05E12
- **Target episode count:** 12

## Theme

S04 taught the CLI to pick the right model. S05 teaches it to *do something
useful with more than one call*. The unifying arc is the pivot from
per-invocation selection toward multi-call orchestration: streaming,
templating, chaining, parallel dispatch, retries, cost caps, batch input,
structured output, audit trails, dry-run previews, and a file-watch loop.
By S05E12 the v2.5.0 binary should feel less like a one-shot CLI and more
like a small, scriptable orchestration engine that respects budgets,
fails predictably, and leaves a forensic trail.

## Episode roster

| #   | Code     | Title                  | Lead       | Co-lead   | Scope summary                                                                       |
|-----|----------|------------------------|------------|-----------|-------------------------------------------------------------------------------------|
| E01 | S05E01   | *The Stream*           | Kramer     | Russell   | Streaming output (token-by-token to stdout, `--stream` flag, raw-mode interactions) |
| E02 | S05E02   | *The Template*         | Elaine     | Maestro   | Prompt templates under `~/.config/az-ai/templates/`, `--template <name>` flag       |
| E03 | S05E03   | *The Chain*            | Costanza   | Kramer    | Multi-step tool-call orchestration (chained calls, intermediate state)              |
| E04 | S05E04   | *The Parallel*         | Jerry      | Bania     | Parallel-call dispatch with throttling and per-model concurrency budgets            |
| E05 | S05E05   | *The Retry*            | Newman     | Frank     | Retry/backoff policy, idempotency keys, jitter, partial-stream recovery             |
| E06 | S05E06   | *The Cap*              | Costanza   | Morty     | Cost-cap guardrails (`--max-tokens-spend`, `--max-cost-usd`) + Pitt mid-season audit |
| E07 | S05E07   | *The Batch*            | Kramer     | Puddy     | Batch input mode (`--batch file.jsonl`), per-line result streaming                  |
| E08 | S05E08   | *The Schema*           | Elaine     | Kramer    | Structured output via `--schema file.json` (JSON Schema validation + repair)        |
| E09 | S05E09   | *The Ledger*           | Newman     | Frank     | Append-only audit log of prompts/responses with redaction policy                    |
| E10 | S05E10   | *The Dry Run*          | Russell    | Lloyd     | `--dry-run` mode: show the plan and projected cost, make no API calls               |
| E11 | S05E11   | *The Watch*            | Jerry      | Costanza  | `--watch` mode: re-run prompt on file change with debounce + diff output            |
| E12 | S05E12   | *The Release*          | Lippman    | Pitt      | v2.5.0 cut, CHANGELOG sweep, release notes, season recap                            |

## Dependency graph

- E01 *Stream* -> E04 *Parallel* (parallel dispatch reuses the stream plumbing).
- E01 *Stream* -> E05 *Retry* (partial-stream recovery needs the stream surface).
- E02 *Template* -> E07 *Batch* (batch lines can reference templates).
- E02 *Template* -> E08 *Schema* (templates can declare their schema).
- E03 *Chain* -> E04 *Parallel* (parallel dispatch is a chain primitive).
- E03 *Chain* -> E09 *Ledger* (chains generate multi-step audit entries).
- E04 *Parallel* -> E06 *Cap* (cost cap must aggregate across parallel calls).
- E05 *Retry* -> E06 *Cap* (retries count against the cap).
- E06 *Cap* -> E10 *Dry Run* (dry-run uses the cap projection math).
- E07 *Batch* -> E10 *Dry Run* (dry-run is the natural batch preview).
- E08 *Schema* -> E11 *Watch* (watch mode benefits from schema-stable diffs).
- E09 *Ledger* -> E12 *Release* (release-notes draft pulls audit-log totals).
- E11 *Watch* -> E12 *Release* (last user-visible feature before cut).
- All -> E12 *Release* (release gate).

## Cast balance pre-flight

### Leads (12 slots)

- Costanza: 2 (E03, E06)
- Kramer: 2 (E01, E07)
- Elaine: 2 (E02, E08)
- Jerry: 2 (E04, E11)
- Newman: 2 (E05, E09)
- Russell: 1 (E10)
- Lippman: 1 (E12)

Every main-cast member clears the Rule 2 multi-lead floor. No back-to-back
leads in airing order (Rule 1 clean).

### Co-leads

- Pitt: E12 (plus E06 audit overlay)
- Maestro: E02
- Bania: E04
- Frank: E05, E09
- Morty: E06
- Puddy: E07
- Lloyd: E10
- Russell: E01

### Support / guest seats (Rule 3 floor: every supporting player gets at least one appearance)

- Mickey: E01 (a11y of streamed output for screen readers)
- Babu: E02 (locale-aware templates, RTL/CJK)
- FDR: E03 (adversarial chain / prompt-injection scenarios)
- Bookman: E04 (rate-limit messaging brevity)
- Wilhelm: E05 (failure-handling change process)
- Sue Ellen: E06 (competitive cost-cap comparison)
- Soup Nazi: E08 (JSON Schema strictness gate)
- Rabbi: E09 (ethics of logging user prompts)
- Jackie: E09 (data-retention legal review)
- Peterman: E10 (story copy for the dry-run "would-have-happened" output)
- Keith: E11 (devrel demo of watch mode)
- Bob: E11 (file-watcher packaging interactions)
- Uncle Leo: E12 (community release announcement)

Every one of the 22 supporting players appears at least once across the
season. Pairings honored: Kramer + Elaine (E08), Newman + FDR (E03 guest
seat overlaps Newman's E05/E09 leads via Wave-2 dispatch), Costanza +
Lloyd (E10 carries Costanza co-lead's product brief into Lloyd's onboarding
review), Maestro + Costanza (E02 -> E03 handoff).

## Stop conditions / season-finale criteria

- v2.5.0 cuts at E12 with: streaming, templates, chains, parallel, retry,
  cost-cap, batch, JSON-Schema output, audit log, dry-run, and watch all
  shipped and integration-tested.
- CHANGELOG `[Unreleased]` rolled to `2.5.0` block; release notes drafted.
- FDR-CRITICAL stop: any red-team CRITICAL on the orchestration layer
  halts forward dispatch until Newman lands a hotfix.
- 3+ consecutive same-lead: pause and rotate (Rule 1 enforcement).
- Cumulative AOT binary growth > 300 KB across the season: pause for Bania
  size audit before E12 cut.
- Mid-season Pitt audit at E06 must pass before E07 dispatches.

## Open questions

1. Should `--stream` be opt-in or default-on for interactive TTY contexts?
   Russell wants default-on for UX; Bookman flags noisier exec logs.
2. Cost-cap unit: tokens vs. USD vs. both? Morty leans both with USD as
   the human-facing primary; Costanza wants a single primary to keep the
   help text small.
3. Audit-log location and rotation policy: `~/.local/state/az-ai/audit/`
   per XDG, or `~/.config/az-ai/audit/`? Frank prefers state-dir; Newman
   wants encryption-at-rest as a follow-up special.
4. Should batch mode reuse the streaming plumbing or take a parallel
   path? Kramer wants reuse; Jerry warns about backpressure semantics.
5. Does v2.5.0 ship `--watch` or hold it for an S06 off-roster? Costanza
   wants it in v2.5.0 to round out the workflow story; Lippman flags
   scope risk if E11 slips.
