# Roadmap

This project does not maintain a static, long-horizon roadmap document.
The roadmap is **distributed** across four living sources, each with a
clear owner and cadence. Read them in this order.

## 1. What's shipping next -- `CHANGELOG.md`

[`CHANGELOG.md`](CHANGELOG.md) is the canonical source of truth for
what is about to ship. The top entries are, in order:

- **`[Unreleased]`** -- landed on `main`, tagged for the next patch or
  minor. Owned by **Mr. Lippman** (release management).
- **`[2.0.x] -- unreleased`** -- the current in-flight release candidate.
  See the [`## [Unreleased]`](CHANGELOG.md#unreleased) section for the
  specific items queued.

Current release: **v2.0.4** (2026-04-22). v2.0.5 is in flight.

## 2. Accepted feature work -- `docs/proposals/`

Substantial features (new flag surfaces, new providers, new subsystems)
land through the **FR proposal** process before code is written.

- **Index:** [`docs/proposals/README.md`](docs/proposals/README.md) --
  lists every proposal, status, owner, and target release.
- **Template:** copy the format of any existing `FR-NNN-*.md`.
- **Owner:** **Costanza** (PM) drafts; **Mr. Pitt** (program) sequences;
  **Kramer** implements.

A proposal in `Accepted` status with a target milestone is the closest
thing to a "planned feature" this project has.

## 3. In-flight work -- GitHub Issues

[Open issues](https://github.com/SchwartzKamel/azure-openai-cli/issues)
filtered by **milestone** and **label** are the most granular view of
what's being actively worked on this cycle. Relevant labels:

- `v2` -- touches the current v2 tree. Default for new work.
- `v1-maintenance` -- bounded backports to `azureopenai-cli/`. Security
  fixes, P0 regressions, the handful of v2.0.0 cutover blockers -- and
  nothing else.
- `enhancement` -- feature request; usually precedes an FR proposal.
- `good-first-issue` / `help-wanted` -- newcomers welcome.

## 4. Architectural direction -- `docs/adr/`

Architecturally significant decisions are recorded as ADRs in
[`docs/adr/`](docs/adr/). Read these to understand **why** the codebase
is the shape it is -- packaging, AOT, persona routing, MAF migration,
telemetry, and so on. Owned by **Elaine** (docs) and **Wilhelm** (process).

## Near-term horizon -- v2.x

The dual-tree window (v1 `azureopenai-cli/` + v2 `azureopenai-cli-v2/`
side-by-side) remains open through the v2.0.x line. v1.9.1 continues
to receive security patches only. The v2 tree is the default for every
feature, fix, doc change, and test addition.

Concrete near-term v2 themes -- each one is an FR, an ADR, or both:

- Persona routing and `.squad/` maturity (`--persona`, `SquadCoordinator`).
- `--estimate` cost prediction across providers.
- Packaging reach: Homebrew, Scoop, Nix, Docker -- coordinated by
  **Bob Sacamano** on the integrations side.
- Accessibility contract enforcement (monochrome-by-construction, man
  pages, screen-reader ergonomics) -- **Mickey Abbott**.
- Supply-chain hardening: SBOM, Scorecards, pinned actions -- **Newman**.

Anything outside these themes is on the backlog until it earns an FR.

## Historical plans

- [`docs/archive/IMPLEMENTATION_PLAN-v1.9.0.md`](docs/archive/IMPLEMENTATION_PLAN-v1.9.0.md) --
  the v1.9.0-era implementation plan, frozen on v2.0.0 ship. Read this
  only if you are researching the v1 → v2 migration decision.
- [`IMPLEMENTATION_PLAN.md`](IMPLEMENTATION_PLAN.md) -- the archival
  pointer file.

---

If you want a feature that isn't listed here and isn't an open
proposal: open a [feature request issue](.github/ISSUE_TEMPLATE/feature_request.yml).
If it grows beyond a single flag, it will become an FR. If it doesn't,
it will be scheduled against an issue milestone.

No Gantt charts. No quarterly themes. No dates we won't hit. If it's
not in the changelog or an open issue, it isn't scheduled.
