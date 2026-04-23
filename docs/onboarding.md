# Onboarding -- the first sixty minutes

> Hi, I'm Lloyd. I joined this project today. The README is good, the
> `CONTRIBUTING.md` is good, the wizard is good -- but I still spent an
> hour bouncing between five files trying to figure out which directory
> to build, which env var is named what, and where my key actually
> lands on disk. This doc is the page I wish had existed when I ran
> `git clone` for the first time. If something here surprises you, that
> is the bug; open an issue and we will fix it.
>
> Maintained by Lloyd Braun. New entries on every onboarding pass.
> If a term is unfamiliar, see [`docs/glossary.md`](glossary.md).

---

## The first 60 minutes

A literal walkthrough. Run these in order. If a step fails, jump to the
matching entry in [`docs/incident-runbooks.md`](incident-runbooks.md)
or to "Things I had to ask" below.

### 0. Before you clone (5 min)

You need three things on the box:

1. **.NET 10 SDK** -- not just the runtime. The SDK ships `dotnet build`,
   `dotnet test`, `dotnet format`. The runtime alone cannot build the
   project. See the SDK / runtime entry in the glossary.
2. **`git`** -- any recent version.
3. **An Azure OpenAI resource** with a deployed model and an API key.
   You only need this for end-to-end runs against the live API; the
   unit test suite does not need credentials.

Optional but useful: **Docker** (for the GHCR image and the docker job
in CI), **`jq`** (helpful when reading `--json` output), and a
clipboard tool (`xclip` on Linux, built-in on macOS / Windows).

`make setup` will install most of this for you on a fresh box. It is
idempotent and safe to re-run. Pass `--skip-docker` if you do not want
the Docker prompt.

### 1. Clone and orient (2 min)

```bash
git clone https://github.com/SchwartzKamel/azure-openai-cli.git
cd azure-openai-cli
ls
```

Two source trees live side-by-side and that confused me for ten
minutes. `azureopenai-cli/` is **v1** (maintenance only). `azureopenai-cli-v2/`
is **v2** and is where all new work goes. The README's quickstart and
the v2 release notes are both written for v2; if you are reading a
guide and a path feels wrong, check whether it is v1-shaped.

Quick tour of what is where -- see "Where to find things" below for
the full map.

### 2. Install prerequisites (5 min)

```bash
make setup
```

This installs the .NET 10 SDK if missing, prompts about Docker, and
checks for `jq` and clipboard tools. Re-run any time. If you already
have the SDK, the script just confirms and moves on.

Verify:

```bash
dotnet --list-sdks    # should list a 10.x SDK
```

### 3. Sanity check the tree (3 min)

Before you change anything, prove the build is clean as-shipped. This
is the "is my checkout sane?" smoke test.

```bash
dotnet test tests/AzureOpenAI_CLI.Tests/AzureOpenAI_CLI.Tests.csproj --verbosity minimal
```

Expected: a few thousand tests, all green, in roughly a minute on a
laptop. If they are red on a fresh clone, that is a bug or an
environment mismatch -- open an issue and paste the exact failure plus
your `dotnet --info` output.

### 4. Build the binary (3 min)

```bash
make publish-aot     # native AOT build, no .NET runtime needed at runtime
make install         # drops the binary at ~/.local/bin/az-ai
```

`~/.local/bin` needs to be on `$PATH`. If `az-ai` is not found after
`make install`, that is your culprit. Add this to your shell rc:

```bash
export PATH="$HOME/.local/bin:$PATH"
```

Verify:

```bash
az-ai --version --short
```

Expected: bare semver, e.g. `2.0.6`.

### 5. Wire credentials (5 min)

You have two paths. Pick one.

**Path A -- the wizard (recommended for humans).** Run the binary with
no creds configured and it drops into the first-run wizard:

```bash
az-ai
```

The wizard asks for endpoint, API key (input is masked), and model
deployment name(s). It pings the service to validate, then persists
to `~/.azureopenai-cli.json` (mode `0600`). On Windows the key is
encrypted with DPAPI; on macOS it lives in the login Keychain; on
Linux it goes into libsecret if available, otherwise plaintext at
`0600`. The README's "Where is my key stored?" table is the
canonical reference.

Re-run the wizard any time with `az-ai --init`.

**Path B -- environment variables (recommended for CI, Docker, scripts).**
Export the three required vars. Env vars always win over stored config.

```bash
export AZUREOPENAIENDPOINT="https://your-resource.openai.azure.com/"
export AZUREOPENAIAPI="your-api-key-here"   # NOTE: AZUREOPENAIAPI, not _KEY
export AZUREOPENAIMODEL="gpt-4o"
```

I tripped on `AZUREOPENAIAPI` versus `AZUREOPENAIKEY` for ten minutes.
The variable is named **`AZUREOPENAIAPI`**. Full env-var reference is
in [`docs/prerequisites.md`](prerequisites.md).

### 6. First successful invocation (2 min)

```bash
az-ai --raw "Say hello in five words."
```

Expected: a five-ish word reply on stdout, no spinner, no formatting.
That is `--raw` doing its job -- it is the mode Espanso and AutoHotkey
use, so it stays clean.

If you get an HTTP error, jump to the 401 / 429 entries in
[`docs/incident-runbooks.md`](incident-runbooks.md).

### 7. (Optional) Try agent mode (5 min)

```bash
az-ai --agent "Read README.md and summarize the install section in three bullets."
```

The model can now call the built-in tools: `shell_exec`, `read_file`,
`web_fetch`, `get_clipboard`, `get_datetime`, `delegate_task`. Each
has a security blocklist; see [`SECURITY.md`](../SECURITY.md).

### 8. Make a tiny change and run preflight (10 min)

Edit anything trivial -- fix a typo in this doc, for example. Then
run the local validation gate before you commit. This is what CI runs;
if it is green locally, CI will be green.

For docs-only changes, you can skip preflight. For any C# / project
file / workflow change, **preflight is non-negotiable** and the full
procedure is in [`.github/skills/preflight.md`](../.github/skills/preflight.md).
The short form:

```bash
make preflight
```

That target chains: `dotnet format --verify-no-changes`, the color
contract lint, build, unit tests, integration tests. All four must be
green or do not commit.

### 9. Commit (3 min)

Commit messages follow Conventional Commits. Lowercase type, imperative
subject, 72 char ceiling. Full rules in
[`.github/skills/commit.md`](../.github/skills/commit.md). If an AI
assistant helped, add the `Co-authored-by: Copilot` trailer.

```bash
git -c commit.gpgsign=false commit -m "docs(onboarding): fix typo

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

### 10. Open the PR (5 min)

Push to your branch, open the PR, and let the template walk you
through the checklist. Reviewers usually respond in a few days; nudge
the PR after a week.

You are done. Total wall-clock for me on a fresh Linux laptop with the
SDK already installed: 47 minutes. The bottleneck was step 5 (figuring
out the env-var spelling).

---

## Things I had to ask

The friction log. Each item is a question I actually had, the answer
I eventually found, and where I should have looked first.

### Q1. Which directory do I build -- `azureopenai-cli/` or `azureopenai-cli-v2/`?

**Answer:** v2. v1 is maintenance-only. The README's quickstart is
v2-shaped already, but the two sibling directories are not labeled in
the file listing and the project name is the same. CONTRIBUTING.md
says this in its "30-second orientation" -- read that first.
**Pointer:** [`CONTRIBUTING.md`](../CONTRIBUTING.md) -> "30-second orientation".

### Q2. The README says `make setup && make install`. Doesn't `make install` need a built binary first?

**Answer:** Yes, and the Makefile handles it. `install` depends on
`dist/aot/$(BIN_NAME)`, which transitively triggers the AOT publish.
You do not need to run `make publish-aot` separately. I only learned
this by reading the Makefile.
**Pointer:** [`Makefile`](../Makefile), `install:` target.

### Q3. What is the env var actually called -- `AZUREOPENAIKEY` or `AZUREOPENAIAPI`?

**Answer:** **`AZUREOPENAIAPI`**. The README has it right, and
[`docs/prerequisites.md`](prerequisites.md) calls out the gotcha
explicitly ("note: not `KEY`"). I still got it wrong twice because
"API" feels like a noun, not a credential.
**Pointer:** [`docs/prerequisites.md`](prerequisites.md).

### Q4. Where does my API key actually land on disk, and is that safe?

**Answer:** OS-dependent. Windows: DPAPI-encrypted (user-scoped).
macOS: login Keychain. Linux with `secret-tool` + DBus available:
libsecret. Linux otherwise: plaintext at mode `0600`. Containers / CI:
env vars only, no on-disk storage. The README's "Where is my key
stored?" table is the canonical answer; I missed it on the first read
because it sits below the wizard transcript.
**Pointer:** [`README.md`](../README.md) -> "Where is my key stored?".

### Q5. `make preflight` -- when do I have to run it?

**Answer:** Any change that touches `*.cs`, `*.csproj`, `*.sln`,
`*.editorconfig`, `.github/workflows/*.yml`, `Dockerfile`, or the
integration test scripts. Docs-only PRs can skip it. Skipping it on a
code change is what caused the `180d64f` incident -- five red runs on
`main` in a row -- and the skill file exists because of that.
"Serenity now -- insanity later" applies. Run the gate.
**Pointer:** [`.github/skills/preflight.md`](../.github/skills/preflight.md).

### Q6. What is the difference between the SDK and the runtime?

**Answer:** The SDK includes the runtime plus the build tooling
(`dotnet build`, `dotnet test`, `dotnet format`, AOT compiler). The
runtime alone can execute pre-built `.dll` / `.exe` files but cannot
build them. You need the **SDK** for this project. See the glossary
entry.
**Pointer:** [`docs/glossary.md`](glossary.md) -> SDK / runtime.

### Q7. What is "Trivy" and why does CI run it?

**Answer:** A container vulnerability scanner. The CI's docker job
builds the image and runs Trivy against it; CVEs above a severity
threshold fail the build. See the glossary.
**Pointer:** [`docs/glossary.md`](glossary.md) -> Trivy.

### Q8. What is an SBOM and why do releases ship one?

**Answer:** Software Bill of Materials. Each release artifact has a
CycloneDX 1.7 JSON SBOM listing every dependency and version. This
lets downstream consumers feed it to their own scanners and verify
supply-chain provenance against the build attestations. See the
glossary and `docs/verifying-releases.md`.
**Pointer:** [`docs/verifying-releases.md`](verifying-releases.md).

### Q9. Where is `make help`? It seems sparse.

**Answer:** `make help` echoes a curated subset, not every target.
The full target list is in the [`Makefile`](../Makefile) itself.
Skim it once -- there are useful `bench-quick`, `bench`, `bench-full`,
`scan`, and `audit` targets that the curated help does not surface.
**Pointer:** [`Makefile`](../Makefile).

### Q10. Conventional Commits -- where is the actual rule sheet?

**Answer:** [`.github/skills/commit.md`](../.github/skills/commit.md).
Accepted types: `feat`, `fix`, `docs`, `style`, `refactor`, `perf`,
`test`, `build`, `ci`, `chore`, `bench`, `security`. Lowercase type,
imperative subject, 72-char ceiling. Add the Copilot trailer if an
assistant helped.

### Q11. The README mentions Ralph mode. What is that?

**Answer:** Autonomous self-correcting loop. The agent runs a task,
hands the output to a validator (`--validate "dotnet test"`), and
retries until the validator passes (or `--max-rounds` is hit). Named
after the Wiggum loop pattern. See `docs/use-cases-ralph-squad.md`.
**Pointer:** [`docs/use-cases-ralph-squad.md`](use-cases-ralph-squad.md).

### Q12. Squad / persona -- what is `.squad.json` doing in the repo root?

**Answer:** Persona system config. `.squad.json` defines named AI
team members with their own system prompts, tool allow-lists, and
persistent memory under `.squad/`. The `--persona` flag activates a
member; `--persona auto` lets the SquadCoordinator route by keyword.
**Pointer:** [`docs/persona-guide.md`](persona-guide.md).

---

## Where to find things

A small file-system map so you stop bouncing.

```text
azure-openai-cli/
├── README.md                  -- start here, top-level pitch + install
├── CONTRIBUTING.md            -- dev workflow, preflight rules, first PR ideas
├── ARCHITECTURE.md            -- system design, tool registry, squad internals
├── AGENTS.md                  -- the 25-agent fleet roster
├── CHANGELOG.md               -- release history; new entries under [Unreleased]
├── SECURITY.md                -- threat model, vulnerability reporting
├── Makefile                   -- build/test/publish/bench targets (full list here)
│
├── azureopenai-cli/           -- v1 source (maintenance only)
│   ├── Program.cs             -- v1 CLI entry point
│   ├── Setup/FirstRunWizard.cs-- the wizard you hit on first run
│   ├── Tools/                 -- built-in tools (shell, file, web, ...)
│   ├── Squad/                 -- persona system
│   └── .env.example           -- credential template (works for v1 + v2)
│
├── azureopenai-cli-v2/        -- v2 source (default for new work)
│
├── tests/
│   └── AzureOpenAI_CLI.Tests/ -- xUnit suite (v1 + v2)
│   └── integration_tests.sh   -- bash assertion suite
│
├── docs/
│   ├── onboarding.md          -- you are here
│   ├── glossary.md            -- acronyms and jargon, single source of truth
│   ├── prerequisites.md       -- env-var reference (single source of truth)
│   ├── incident-runbooks.md   -- failure modes and recovery (401, 429, DNS/TLS)
│   ├── persona-guide.md       -- --persona flag and squad mechanics
│   ├── espanso-ahk-integration.md -- text-expander wiring
│   ├── cost-optimization.md   -- token budgets and per-persona profiles
│   ├── adr/                   -- architecture decision records
│   ├── proposals/             -- FR-NNN-*.md feature proposals
│   ├── exec-reports/          -- per-episode reports (this is one)
│   ├── perf/                  -- benchmark baselines
│   └── runbooks/              -- ops runbooks
│
├── scripts/
│   ├── setup.sh               -- prereq installer (called by `make setup`)
│   ├── setup-secrets.sh       -- credential walkthrough (Linux/macOS)
│   └── setup-secrets.ps1      -- credential walkthrough (Windows / git-bash)
│
├── .github/
│   ├── agents/                -- the 25 agent archetypes (.agent.md files)
│   ├── skills/                -- preflight.md, commit.md, ci-triage.md
│   ├── contracts/             -- locked behavioral contracts (color, etc.)
│   ├── workflows/             -- CI definitions (ci.yml, release.yml, ...)
│   └── ISSUE_TEMPLATE/        -- bug, feature, question forms
│
└── dist/                      -- build output (gitignored)
```

---

## Your first PR

Five concrete starter ideas, each rated by difficulty and pointing at
the file that would change. Pick one. None of these requires touching
production code, and each is reviewable in a single sitting.

1. **(S) Add a missing acronym to [`docs/glossary.md`](glossary.md).**
   Anything you tripped over in the README that is not yet defined.
   Append in alphabetical order, follow the H3-per-term shape, ASCII
   only. *File:* `docs/glossary.md`.

2. **(S) Address a `Lloyd flags:` callout in [`docs/user-stories.md`](user-stories.md).**
   S02E11 left explicit `Lloyd flags:` markers where the prose still
   reads like a spec (lines 19, 233, 257, ...). Pick one, rewrite the
   paragraph in plain English, remove the marker. *File:*
   `docs/user-stories.md`.

3. **(M) Write a test for the `:F0` culture bug noted in S02E08.**
   The translation episode flagged that `:F0` formatting is tied to
   current culture (e.g. `de-DE` swaps decimal separators). Add an
   xUnit test that asserts `CultureInfo.InvariantCulture` is used for
   numeric formatting on hot paths. *File:*
   `tests/AzureOpenAI_CLI.Tests/`. *Reference:*
   [`docs/exec-reports/s02e08-the-translation.md`](exec-reports/s02e08-the-translation.md).

4. **(S) Improve a `--help` example.** Run `az-ai --help`, find a
   flag whose example is thin or missing, add one. *Files:* the
   strings live in `azureopenai-cli-v2/`; the assertion patterns live
   in `tests/AzureOpenAI_CLI.Tests/`.

5. **(S) Fix a typo or broken link.** Boring, valued, always merged.
   `grep -rn "TODO\|FIXME" docs/` is a good starting hint.

6. **(M) Add a missing failure-mode entry to [`docs/incident-runbooks.md`](incident-runbooks.md).**
   If you hit an HTTP code or DNS error during your walkthrough that
   is not yet covered, document the diagnosis and recovery. *File:*
   `docs/incident-runbooks.md`. *Note:* this is a runbooks-owned file;
   coordinate with Frank Costanza in your PR description.

7. **(M) Document an undocumented `make` target.** `make help`
   surfaces a curated list; the full target list in `Makefile` has
   useful entries (`bench-quick`, `scan`, `audit`) that are
   under-explained. Add a docstring comment per the `## ` convention
   already present in the file. *File:* `Makefile`.

8. **(L) Pair with someone on a `FR-NNN-*.md` proposal under
   [`docs/proposals/`](proposals/).** If you have a substantial idea
   (new flag, new provider, new subsystem), write the proposal first.
   Match the format of an existing entry. *Reference:*
   [`docs/proposals/README.md`](proposals/README.md).

If none fit, open a [Question issue](../.github/ISSUE_TEMPLATE/question.yml)
and we will pair you with something.

---

## Glossary cross-link

If a term is unfamiliar, see [`docs/glossary.md`](glossary.md). It is
the single source of truth for acronyms and project jargon. New
entries appended every time someone trips on something.
