# Contributing to Azure OpenAI CLI

Hello! Contributor! Hello!

You found the repo, you read this far -- that already puts you ahead. This
doc exists so you can go from `git clone` to a merged PR without guessing.
It is terse on purpose. If something is unclear, that's a bug in this
file; open an issue and we'll fix it.

---

## The 30-second orientation

- **Two source trees live in this repo.** v1 is in [`azureopenai-cli/`](azureopenai-cli/),
  v2 is in [`azureopenai-cli-v2/`](azureopenai-cli-v2/). **All new work goes in v2.**
  v1 is maintenance-only: security fixes, P0 regressions, and the handful of
  v2.0.0 cutover blockers tracked in [`docs/proposals/README.md`](docs/proposals/README.md).
  If you're not sure which tree to touch, assume v2.
- **v2 is the new default.** Background, scope, and behavior contracts are in
  [`docs/release-notes-v2.0.0.md`](docs/release-notes-v2.0.0.md). Migrating from
  v1? See [`docs/migration-v1-to-v2.md`](docs/migration-v1-to-v2.md).
- **Preflight is non-negotiable** on any change that touches `.cs`, `.csproj`,
  `.sln`, or `.github/workflows/`. Details below.
- **Every PR needs a Conventional Commit subject and the Copilot trailer**
  if the work was AI-assisted. Details below.

---

## Quickstart

```bash
# 1. Fork, clone, enter
git clone https://github.com/<your-username>/azure-openai-cli.git
cd azure-openai-cli

# 2. Install the .NET 10 SDK + local tooling (once)
make setup

# 3. Run the unit test suite -- this is your "is my tree sane?" check
dotnet test tests/AzureOpenAI_CLI.Tests/AzureOpenAI_CLI.Tests.csproj

# 4. Before you commit code: the preflight gate (see below)
#    This is what CI runs. If it's green locally, CI will be green.
dotnet format azure-openai-cli.sln --verify-no-changes
dotnet build azureopenai-cli-v2/AzureOpenAI_CLI_V2.csproj -c Release --nologo
dotnet test  tests/AzureOpenAI_CLI.Tests/AzureOpenAI_CLI.Tests.csproj --nologo
```

`make help` lists every target -- build, cross-platform publish, AOT,
benchmarks, Docker, scan, install. Most contributors only need `dotnet test`,
`make format`, and `make publish-aot`.

`.env` with Azure credentials is only needed if you run the CLI end-to-end
against a live endpoint. Unit tests don't need it.

---

## Your first PR

If you want a small, real change to start with, pick one of these. Each
one is bounded, reviewable in a single sitting, and references work an
earlier episode left as a B-plot.

- **Add a missing acronym to [`docs/glossary.md`](docs/glossary.md).**
  S02E08 stood the glossary up but only seeded the first eleven entries.
  If you tripped over a term in the README and had to look it up, that's
  a glossary gap. Add it; one paragraph, one PR.
- **Address a `Lloyd flags:` callout in [`docs/user-stories.md`](docs/user-stories.md).**
  S02E11 left explicit "Lloyd flags:" markers where the prose still reads
  like a spec. Pick one, rewrite the surrounding paragraph in plain
  English, and remove the marker.
- **Fill a docs gap referenced in S02E13's security audit follow-ups.**
  See the "Not shipped" section of `docs/exec-reports/s02e13-*.md` once
  it lands; pre-S02E13, the standing ask is a one-page "what data leaves
  your machine" summary that cross-links [`docs/telemetry.md`](docs/telemetry.md).
- **Improve a `--help` example.** Run `az-ai-v2 <subcommand> --help`,
  find a flag whose example is thin or missing, and add one. The strings
  live in `azureopenai-cli-v2/`; the test pattern lives in
  `tests/AzureOpenAI_CLI.Tests/`.
- **Fix a typo or broken link.** Boring, valued, always merged. Run
  `grep -rn "TODO\|FIXME" docs/` for hints.

If none of those fit, open a [Question issue](.github/ISSUE_TEMPLATE/question.yml)
describing what you'd like to work on. We will point you at something.

---

## The preflight gate

**Non-negotiable.** Read [`.github/skills/preflight.md`](.github/skills/preflight.md)
and run all four checks locally before `git commit` on any change that
touches:

- `*.cs`, `*.csproj`, `*.sln`, `.editorconfig`
- `.github/workflows/*.yml`
- `Dockerfile` or integration test scripts

Docs-only PRs (`*.md`) can skip it. If you skip it on a code change, CI will
catch you and the PR will sit red until you fix it -- the skill file exists
because we already paid for the lesson (`180d64f`, five red runs on `main`).

---

## Commits

Conventional Commits, lowercase type, imperative subject ≤ 72 chars. Full
rules and examples in [`.github/skills/commit.md`](.github/skills/commit.md).

Accepted types: `feat`, `fix`, `docs`, `style`, `refactor`, `perf`, `test`,
`build`, `ci`, `chore`, `bench`, `security`.

If Copilot, Claude, or any other assistant helped write the change, add the
trailer -- no exceptions, this is how we trace provenance:

```text
feat(v2): add --estimate cost preview for chat

Surfaces the FR-015 rate card before the request is sent so
Espanso users can bail out of expensive prompts.

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>
```

---

## Where to file what

| You have… | File it as… |
| --- | --- |
| A bug or regression | [Bug report issue](.github/ISSUE_TEMPLATE/bug_report.yml) |
| A small feature idea or usage question | [Feature request issue](.github/ISSUE_TEMPLATE/feature_request.yml) or [Question](.github/ISSUE_TEMPLATE/question.yml) |
| A substantial feature (new flag surface, new provider, new subsystem) | A proposal under [`docs/proposals/`](docs/proposals/) as `FR-XXX-short-slug.md` -- match the format of an existing entry (e.g. [FR-014](docs/proposals/FR-014-local-preferences-and-multi-provider.md)) and add your row to [`docs/proposals/README.md`](docs/proposals/README.md) |
| A new agent archetype | A markdown file in [`.github/agents/`](.github/agents/); see [`AGENTS.md`](AGENTS.md) for the fleet rationale and existing cast |
| A security vulnerability | **Do not open a public issue.** Use [GitHub Security Advisories](https://github.com/SchwartzKamel/azure-openai-cli/security/advisories/new); see [`SECURITY.md`](SECURITY.md) |

Big features go through a proposal first so Costanza (PM) and Mr. Pitt
(program) can slot them against the roadmap before you burn a weekend on
code. The live roadmap -- release queue, accepted proposals, open
milestones -- is enumerated in [`ROADMAP.md`](ROADMAP.md).

### DCO / sign-off

**No DCO is required.** We do not enforce `Signed-off-by:` trailers. The
only provenance trailer this project asks for is the AI-assist
`Co-authored-by: Copilot <…>` line shown above -- and only when an
assistant actually helped. If you prefer to also `git commit -s`, that's
fine; it won't block the PR. Nothing else is required or checked.

---

## Pull request expectations

Before you open the PR:

- [ ] Preflight is green locally (and will be green in CI)
- [ ] A test covers the new or changed behavior -- unit preferred, integration
      if the change is at the CLI surface
- [ ] User-visible docs updated: `README.md`, `docs/…`, `--help` text, man
      pages, whichever applies
- [ ] `CHANGELOG.md` gains an entry under `[Unreleased]` unless the change
      is docs-only
- [ ] No secrets, endpoints, or tokens in the diff
- [ ] Commit subject is a Conventional Commit; Copilot trailer present if
      AI-assisted

The PR template (see [`.github/PULL_REQUEST_TEMPLATE.md`](.github/PULL_REQUEST_TEMPLATE.md))
walks you through it. Keep the diff focused -- one concern per PR reviews
faster and reverts cleaner.

Reviewers usually respond within a few days. Nudge the PR after a week;
we don't mind.

---

## Getting help

- Stuck on setup? Open an issue with your OS, `.NET` version, and the
  exact error. That's already a useful bug report.
- Design question before you code? Open a
  [Question issue](.github/ISSUE_TEMPLATE/question.yml) or start a
  [Discussion](https://github.com/SchwartzKamel/azure-openai-cli/discussions).
- Maintainer AWOL on your PR? Tag them on the PR. Silence is never the
  intended signal.

---

## The fleet

You'll see names like **Costanza**, **Kramer**, **Elaine**, **Peterman**,
**Newman**, **Uncle Leo** turn up in commit bodies, PR comments, and
review threads. These aren't people -- they're
[agent archetypes](AGENTS.md) this project uses with GitHub Copilot custom
agents. Each archetype owns a domain (PM, engineering, docs, DevOps,
security, release, a11y, i18n, …) and the fleet collaborates to drive the
build.

When a PR comment says "Newman flagged this for supply-chain review" or
"let Peterman rewrite the marketing blurb," that's a real owner with a
real scope -- just one wearing a persona. The full cast of 25 is listed in
[`AGENTS.md`](AGENTS.md) and defined in [`.github/agents/`](.github/agents/).
Contributors are not required to use the archetypes. They're how the
maintainer team organizes attention.

---

## Color, ANSI, and accessibility

v2.0.0 ships **monochrome-by-construction** -- zero ANSI escapes, zero
`ConsoleColor` calls, no spinner. `NO_COLOR`, `TERM=dumb`, and piped
stdout are trivially honored because there is nothing to suppress. v2.1
and beyond may add color; when that happens the contract is **locked in
now, before the first color byte ships**.

**→ Canonical contract: [`.github/contracts/color-contract.md`](.github/contracts/color-contract.md)**

The short version -- if your PR emits ANSI or color anywhere, the same
PR must:

1. **`NO_COLOR` always wins.** If the env var is set and non-empty, no
   ANSI SGR escapes. Ever. See <https://no-color.org>.
2. **`FORCE_COLOR=1` / `CLICOLOR_FORCE=1`** force color on even when
   stdout is redirected (CI log collectors, `script(1)`, test harnesses).
   `NO_COLOR` still beats this.
3. **Auto-detect:** color off unless `Console.IsOutputRedirected == false`.
4. **`TERM=dumb`:** no ANSI.
5. **Precedence:** `NO_COLOR` > `TERM=dumb` > `CLICOLOR=0` >
   `FORCE_COLOR` / `CLICOLOR_FORCE` > auto-detect.
6. **`--raw` is silent-by-design:** no color, no spinner, no banner,
   no decoration. Stable machine-readable output contract; breaking it
   is a SemVer-breaking change.
7. **`[ERROR]` prefix is mandatory** on every stderr error line -- screen
   readers (Orca, NVDA, VoiceOver) key off the literal token. Not
   `Error:`, not `ERR:`.

All color output must be gated by a single `Theme.UseColor()` helper.
PRs that set `Console.ForegroundColor` or emit raw ANSI escapes outside
that helper will be blocked on review. Every new colorized path ships
with a `NO_COLOR=1` test asserting zero escape bytes and a
`FORCE_COLOR=1` test asserting at least one escape byte.

Full test checklist, anti-patterns, and enforcement rules live in the
contract file linked above. Background rationale is in
[`docs/accessibility-review-v2.md`](docs/accessibility-review-v2.md) §1.4
and §7.1.

---

## Code of Conduct

This project follows the [Contributor Covenant](CODE_OF_CONDUCT.md). By
participating, you agree to uphold it. Report unacceptable behavior
through [`SECURITY.md`](SECURITY.md)'s private channel or to a maintainer
directly -- we deal with it privately first, publicly only when necessary,
and consistently either way.

---

## Labels

A small, boring set. If you're browsing issues:

| Label | Meaning |
| --- | --- |
| `good-first-issue` | Open, well-scoped, ideal for newcomers. Start here. |
| `help-wanted` | We'd love help; assumes some project familiarity. |
| `needs-triage` | New, awaiting maintainer review. Auto-applied by issue forms. |
| `bug` | Confirmed defect or regression. |
| `enhancement` | Feature request or improvement. |
| `question` | Usage or design question; often migrates to Discussions. |
| `docs` | Documentation-only change. |
| `security` | Security-sensitive. Prefer Security Advisories for vulnerabilities. |
| `v1-maintenance` | Touches `azureopenai-cli/` only; bounded scope. |
| `v2` | Touches `azureopenai-cli-v2/` -- the default for new work. |

Maintainers curate these. If a label looks wrong, say so on the issue.

---

## Uncle Leo's contributor wall

Hello! Contributor! Hello! This project would not exist without the
people and personas who showed up. The human cast on the commit log so
far: **SchwartzKamel**. The agent fleet -- 25 named archetypes,
defined in [`.github/agents/`](.github/agents/) -- has shown up under
the names **Babu Bhatt**, **Bob Sacamano**, **Costanza**, **Elaine**,
**Jackie Chiles**, **Jerry**, **Kenny Bania**, **Kramer**, **Mr. Lippman**,
**Morty Seinfeld**, **Mr. Pitt**, **Newman**, **J. Peterman**,
**Puddy**, **Soup Nazi**, and the **Copilot SWE agent** itself. Your
name belongs on this wall too. Open the PR. We will hold the door.

---

Welcome aboard. Ship something small first -- a typo fix, a missing
`--help` example, a clearer error string. That first merged PR is the
hardest one. Everything after it is easier.

-- the maintainers (and the fleet)
