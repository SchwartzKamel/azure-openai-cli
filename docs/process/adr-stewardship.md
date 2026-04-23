# ADR stewardship

> *"I thought we agreed in the last retro... didn't we? Let me check
> the ADR. Let me check the ADR."* -- Mr. Wilhelm

Owned by **Mr. Wilhelm** (process), with prose-polish from **Elaine**
(technical writing) and decision-quality review from **Costanza**
(product / architecture).

The [`docs/adr/`](../adr/) directory is where the project's
non-obvious decisions live. This doc is the operating manual for that
directory: when to write an ADR, what shape it takes, how it is
indexed, when one ADR supersedes another, and how stewardship is
shared across the cast.

The standalone [`docs/adr/README.md`](../adr/README.md) covers the
*format* (Nygard, three-digit index, immutability, target length).
This doc covers the *process* around the format: classification,
review, supersession, and the audit cadence that keeps the index
honest.

---

## 1. When a change earns an ADR

Use the test from [`change-management.md`](change-management.md)
section 4 first. The expanded ADR-side criteria:

**Write an ADR when the change:**

- **Locks in a technology, runtime, or dependency** that future
  contributors must build on (e.g., Native AOT, .NET 10, .NET version
  bump, container base image change, framework adoption).
  Worked example: [`ADR-001-native-aot-recommended.md`](../adr/ADR-001-native-aot-recommended.md)
  -- the publish-mode decision is referenced by every subsequent
  performance-sensitive change.
- **Establishes a cross-cutting convention** that PRs must follow
  going forward (JSON source generators for AOT, error-handling
  posture, behavior-driven test style).
  Worked example: [`ADR-003-behavior-driven-development.md`](../adr/ADR-003-behavior-driven-development.md).
- **Introduces a new architectural surface** with non-obvious
  tradeoffs (a new persona system, a new routing layer, a new
  provider integration).
  Worked example: [`ADR-002-squad-persona-memory.md`](../adr/ADR-002-squad-persona-memory.md)
  -- documents why we did not adopt the upstream `bradygaster/squad`
  npm tool wholesale and what we kept vs. replaced.
- **Reverses or supersedes a prior ADR.** The new ADR cites the old
  one in its `Related` field and the old one's `Status` flips to
  `Superseded by ADR-XXX`.
- **Has a tradeoff someone will question in six months.** "Why is the
  default model resolved this way?" "Why do we route Foundry traffic
  through a separate auth policy?" The ADR is the answer.

**Skip the ADR (write a backlog entry, an exec-report note, or just
the commit) when the change:**

- Is a routine refactor with no behavioral change.
- Is a bug fix where the root cause is obvious from the diff.
- Reverses cleanly (the kind of decision you can take back next week
  without a downstream ripple).
- Adds a flag, tool, or subcommand without changing a convention.
  (The change earns release notes, not an ADR.)
- Surfaces a defect or smell that the episode did not fix -- that is
  a [`findings-backlog`](../../.github/skills/findings-backlog.md)
  entry, not a decision.

The asymmetry is intentional: writing one too many ADRs costs
~150 lines of markdown; writing one too few costs the next
contributor an hour of code archaeology and a question to a
maintainer who may not remember.

---

## 2. The template

ADRs follow Michael Nygard's format. The skeleton:

```markdown
# ADR-NNN: <Short Title>

- **Status**: Proposed | Accepted -- YYYY-MM-DD | Deprecated | Superseded by ADR-XXX
- **Deciders**: <names or roles>
- **Related**: <ADR-XXX>, <CHANGELOG entry>, <FR-NNN proposal>, <external link>

## Context

What forces are in play? What constraints? What was tried before?
2-4 paragraphs. The reader should be able to feel the pressure that
made a decision necessary.

## Decision

The choice, in plain language. State it as a present-tense fact
("We use Native AOT as the default publish mode"). One paragraph.

## Consequences

The good and the bad. Bullet list. What does this make easier?
What does this make harder? What new constraints does it impose?

## Alternatives Considered

Each alternative as a sub-heading or bullet. Why was it not chosen?
Be honest -- "rejected because it adds an npm runtime" is more
useful than "did not fit."

## References

External links, prior art, related ADRs, FR proposals, CHANGELOG
entries.
```

**Style notes:**

- Filename: `ADR-NNN-short-kebab-title.md`, three-digit zero-padded
  index. The next available number is one more than the highest
  existing ADR in [`docs/adr/README.md`](../adr/README.md).
- Length: target 100-200 lines. Longer means it is probably a design
  doc -- promote that to `docs/proposals/FR-NNN-*.md` and reference
  it from a short ADR.
- Tense: present in the Decision section ("We use ..."), past in the
  Context section ("We tried ..."), conditional in the Alternatives
  section ("If we had picked X, we would ...").
- ASCII only per [`ascii-validation`](../../.github/skills/ascii-validation.md).

---

## 3. Status lifecycle

Every ADR moves through this lifecycle:

```text
   Proposed
       |
       v
   Accepted -- YYYY-MM-DD
       |
       +---------------+
       |               |
       v               v
   Deprecated     Superseded by ADR-XXX
```

- **Proposed.** The ADR is in review. Status remains `Proposed` until
  the change it covers merges. The decision can still be reshaped.
- **Accepted -- YYYY-MM-DD.** The change merged; the decision is
  binding. The Context and Decision sections become **immutable**.
  Typo fixes and broken-link repairs in those sections are allowed;
  rewriting rationale is not.
- **Deprecated.** The decision no longer applies (the technology
  retired, the convention dropped). Add a one-paragraph "Deprecated"
  block at the top explaining what replaced it (or what made it
  irrelevant). Do not delete.
- **Superseded by ADR-XXX.** A newer ADR overturns this one. The new
  ADR's Context section explains why; the old one's Status field
  links forward.

**Immutability rule:** once `Accepted`, the Context and Decision
sections are frozen. The Consequences section may gain a
"Consequences observed in practice" sub-block (timestamped) when
later episodes reveal something the original decision missed -- that
is a feature, not a violation. The Alternatives section is frozen.

---

## 4. Indexing discipline

The index lives in [`docs/adr/README.md`](../adr/README.md) as a
table. Two rules:

1. **Every ADR has a row.** When you add `ADR-NNN-*.md`, you also
   append the row to the index in the same commit. A new ADR with no
   index row is invisible to the next contributor's `grep`.
2. **Status changes are reflected in the index in the same commit
   that changes the ADR's Status field.** If `ADR-005` flips from
   `Proposed` to `Accepted -- 2026-04-25`, the index row updates in
   the same commit.

Index column order is fixed: `ID | Title | Status`. Add columns only
if every existing row gets backfilled in the same commit.

---

## 5. Audit cadence

ADRs rot when nobody re-reads them. The cadence:

- **Per-episode.** Any episode that names an ADR in its exec report
  must re-read that ADR before sign-off and confirm the Status is
  still accurate. If the episode invalidated a Consequence, log a
  finding and queue a follow-up ADR.
- **Mid-season (E12).** Mr. Pitt + Mr. Wilhelm walk the index
  together. Every `Proposed` ADR older than three episodes is either
  flipped to `Accepted -- <date>` or escalated to a decision meeting.
- **Season finale (E24).** Full ADR audit as part of the finale exec
  report. Every ADR's Status is confirmed; deprecations are entered;
  any ADR that should have been written and was not gets queued for
  the S03 blueprint.

---

## 6. Anti-patterns

- **Editing an Accepted ADR's Decision.** That is a supersession, not
  an edit. Write a new ADR.
- **Writing an ADR with no Alternatives Considered section.** The
  alternatives are half the value -- they tell the next reader what
  *not* to revisit. "None considered" is rarely true and always
  suspicious.
- **Index row added without the ADR file** (or vice versa). The
  pre-merge self-review must include `ls docs/adr/` against the
  index table.
- **Writing a 600-line ADR.** That is a design doc dressed as an
  ADR. Move the long-form content to `docs/proposals/FR-NNN-*.md`
  and let the ADR cite it.
- **Using an ADR to record a backlog item.** "We should fix the
  CJK padding bug eventually" is a backlog entry, not a decision.
  See [`findings-backlog`](../../.github/skills/findings-backlog.md).

---

## 7. Cross-references

- Format spec: [`docs/adr/README.md`](../adr/README.md).
- Worked examples cited above:
  [`ADR-001`](../adr/ADR-001-native-aot-recommended.md),
  [`ADR-002`](../adr/ADR-002-squad-persona-memory.md),
  [`ADR-003`](../adr/ADR-003-behavior-driven-development.md).
- Companion process docs:
  [`change-management.md`](change-management.md),
  [`cab-lite.md`](cab-lite.md),
  [`retrospective-cadence.md`](retrospective-cadence.md).
- Skills:
  [`ascii-validation`](../../.github/skills/ascii-validation.md),
  [`findings-backlog`](../../.github/skills/findings-backlog.md),
  [`commit`](../../.github/skills/commit.md).
- Agents:
  [`wilhelm`](../../.github/agents/wilhelm.agent.md),
  [`elaine`](../../.github/agents/elaine.agent.md),
  [`costanza`](../../.github/agents/costanza.agent.md).
