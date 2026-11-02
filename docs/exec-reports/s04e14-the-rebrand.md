# S04E14 -- *The Rebrand*

> *A name is a promise about a command. The old command had to keep every promise it ever made.*

**Commit:** `(this commit)` -- one code commit plus the episode report
**Branch:** `feat/s04e14-the-rebrand` (PR, off `main`)
**Runtime:** ~3 hours wall-clock, in a separate jj workspace
**Director:** Larry David (showrunner seat, filled by a single orchestrator)
**Lead:** Bob Sacamano (integrations and packaging; the cast-balance audit in S04E06 listed him as owed a slot)
**Cast:** 0 sub-agents, inline passes by one orchestrator. Bob (LEAD), Jerry (co-lead: build, Docker, release), Jackie Chiles (guest: the policy text; not counsel), Mickey Abbott (completions), FDR (devil's advocate)

## The pitch

The owner's reply to the name workshop was four words: "hopper is good". That clears gate
G1 of FR-024. The plan in ADR-016 said what to do next, but the plan had a stage order that
no longer fit what the code allowed, so this episode starts by measuring.

`scripts/brand-inventory.sh` before: `az-ai` in **328 files, 3,156 lines**. That number is
the honest size of the rename, and almost none of it is the same kind of mention.

## Scene-by-scene

### Act I -- Two kinds of `az-ai`

A mention of `az-ai` is one of two things. Either it is **a command a person reads or
types** (help text, error hints, completions, docs), or it is **a name that already exists
on a disk or on a server** (`~/.config/az-ai/`, `.az-ai/queue.json`, release archive names,
the Homebrew formula). The first kind should change. The second kind cannot change
without breaking something that already exists, and a renamed config directory orphans every
saved API key with no error at all.

So the source change was a **protected replacement**: every `az-ai` becomes `hopper` except
path segments, the working-directory queue folder and the temp-file prefix. 203 replacements
in 22 files, then the whole diff read by eye.

### Act II -- Build plumbing

`AssemblyName` is now `hopper`. The Makefile, Dockerfile, `stage.sh`, the eval scripts, one
workflow and the integration harness follow. `make install` installs `hopper` and links
`az-ai` to it; `make uninstall` removes both. The Docker image keeps `/app/az-ai` as a link.

### Act III -- What cannot be tested here, and the decision that follows

`release.yml` cannot run locally, and the manifests are bound to it in two ways that are easy
to miss. The Homebrew formula and the Scoop manifest download **published** assets by their
current names, and Scoop's `autoupdate` builds the next URL from the old pattern
(`az-ai-$version-win-x64.zip`) and expects `az-ai.exe` inside. An archive without `az-ai`
would break an automatic Scoop update on the first release after this change.

Decision: keep the archive names, and put **both** binaries in each archive. On tar the
alias is a hard link, so the archive is 7.1 MB, not the 14.2 MB two real copies would be
(measured). The rename of archive names and manifests is Stage 2, to happen together with a
release.

`stage.sh` runs locally, so it was run: a real AOT publish, then the archive inspected. The
extracted `az-ai` and `hopper` share an inode and both print `hopper 2.3.0`.

### Act IV -- FDR and the objections

- *"Scripts parse `az-ai --version`."* The full line now begins `hopper`. `--version --short`
  is the documented machine form and is unchanged. The CHANGELOG says so.
- *"The alias doubles the download."* Not on tar (hard link). On the Windows zip it does,
  for one release, and the finding below tracks removing it.
- *"Existing Espanso and AHK configs call `az-ai`."* They keep working; that is what the
  alias is for. Their examples are not rewritten in this episode.
- *"A `make install` over an old install."* Tested: an old `az-ai` regular file is replaced
  by the link, and the new binary runs under both names.

### Act V -- The defects, mine and inherited

1. **My first Makefile pass was wrong.** It rewrote `migrate-check` and `migrate-clean`,
   which hunt for stale v1 `az-ai` leftovers and must keep saying `az-ai`. Caught by reading
   the diff, reverted, redone as targeted edits.
2. **My protection list missed `%APPDATA%/az-ai/env`** in a doc comment, which then claimed
   the Windows path had moved. Caught by searching the diff for directory-shaped paths.
3. **An inherited double registration.** An earlier mechanical rename left
   `compdef _az-ai az-ai az-ai` and a fish line that told a command to wrap itself. Fixed;
   fish now declares `az-ai` as a wrapper of `hopper`, which is what `--wraps` is for.
4. **An inherited stale version.** The help banner hard-coded `v2.0.0` while the binary is
   2.3.0. The banner now prints the real version, pinned by a test.
5. **An inherited broken form.** `.github/ISSUE_TEMPLATE/bug_report.yml` does not parse as
   YAML (two dropdown options start with a backtick), so the bug-report form was likely
   broken. Quoted; not part of the rename.
6. **The same ordering mistake, twice.** In the man page, a blanket `az-ai` to `hopper` replace
   ran *after* I had written "formerly az-ai", and turned it into "formerly hopper". A sentence
   that names the old name has to be written after the replacement, not before.
7. **A local-only quirk.** Some integration cases run the binary under `env -i` with a
   throwaway `HOME`, which loses `DOTNET_ROOT` for a framework-dependent build. A wrapper that
   hard-codes the path fixes it locally. CI is unaffected.

### Act VI -- The aliases and the integrations (the second commit)

The owner's next instruction was to update the aliases and the rest, and keep them working.
The rule that sorted every case: **what is installed on a machine needs a fallback; what is
copied from a checkout does not.**

- **Installed helpers get real fallbacks.** The WSL wrapper (`hopper-wrap`) prefers `hopper`
  and falls back to `az-ai`; the AutoHotkey scripts resolve `hopper-wrap` or `az-ai-wrap` at
  run time through one line; `make espanso-test` and `setup-secrets` accept either name. The
  install step links `az-ai-wrap` to `hopper-wrap`, mirroring the binary's alias.
- **Templates just call `hopper`.** The Espanso templates are copied from a current checkout,
  and a fallback in each of 22 triggers would clutter them and risk the linter. A compatibility
  note says what to do on an older install.
- **Stored identifiers do not move.** `setup-secrets.sh` writes a marker comment into the
  user's `~/.profile` and finds it again by its text; renaming it would leave the old hook
  and add a second. A comment now says why. The container name `az-ai-nim` stays too.
- **The man page** is `hopper.1`, and `az-ai.1` is a one-line `.so` stub, so `man az-ai`
  still works (checked with groff).
- **Left alone on purpose:** the Season 3 demo script calls `az-ai` from PATH, which works on
  every install, and rewriting 25 call sites in a script that asserts on output buys nothing.

Every claim above was run, not read: the wrapper in four install situations (including an
already-installed **old** `az-ai-wrap` after upgrading), the AHK one-liner, and the Makefile's
command pick against a throwaway login shell. Then it was pinned: 8 new integration cases.
**Mutation-checked:** five mutations caught. The first run caught only three, because a
failing assertion inside a command substitution aborted the whole suite under `set -e`
instead of reporting a FAIL. That was a flaw in my tests, now fixed.

## What shipped

**Production code**

- `AzureOpenAI_CLI.csproj`: `AssemblyName` is `hopper`.
- 22 source files: help, hints and usage text name `hopper`; the MCP `serverInfo` name is
  `hopper`; the banner prints the real version; completions register both names.

**Build and packaging**

- `Makefile`, `Dockerfile`, `packaging/tarball/stage.sh`, `scripts/eval-*.sh`,
  `scripts/pre-push.sh`, `scripts/bench.*`, `.github/workflows/eval-gate.yml`.

**Tests**

- `RebrandTests.cs`: 11 tests, half guarding what must change (the banner, both completion
  names, no hint that tells a user to run the old command) and half guarding what must not
  (the env file, preferences file, queue folder and the file-tool blocklist all keep the old
  directory name). **Mutation-checked:** five mutations, each turned exactly one test red,
  and each revert was clean.
- Six existing assertions updated for the new command text.
- Integration: 8 new cases (wrapper fallback in four situations, `make install` and `uninstall`
  including replacing an older `az-ai` file, and both completion names from the real binary).
  Mutation-checked.

**Docs**

- README (title, lead, a naming note, commands), `docs/legal/trademark-policy.md`, ADR-016
  (accepted; the stages restated to match what is built), FR-024 status, CHANGELOG, issue
  and PR templates.
- The Espanso/AutoHotkey kit (README, templates, `hopper-wrap.sh`, `hopper.ahk`,
  `hopper-prompts.ahk`), `docs/espanso-ahk-integration.md` and the man page.

**Not shipped**

- **Stage 2:** archive names, the Homebrew formula, the Scoop manifest, the Nix flake,
  `release.yml` globs. They move together at the release.
- **Stage 3:** the GitHub repository name. The image path follows it.
- **The rest of the docs migration:** about 190 documentation files still say `az-ai` as the
  command. The alias keeps them correct, and history-style documents should keep the name they
  were written under. The user-facing ones are a follow-up.

## Lessons from this episode

1. **Measure the rename before doing it.** 3,156 lines sounded like one job and was two.
2. **Classify every mention.** Whether a name is *read* or *stored* decides everything.
3. **Do not rename a thing that is stored without a migration.** The config directory is the
   whole risk, so a test guards it.
4. **A broad edit needs a diff review, not a passing build.** Two of my own mistakes compiled
   and passed tests; reading the diff found them.
5. **Ship the transition, not the destination.** Both binaries in the archive costs a hard
   link and saves a broken autoupdate.
6. **What you cannot run, you can still reason about.** `release.yml` is untested here; the
   decision rests on reading what consumes its output.
7. **Installed means fallback; copied means plain.** A script that lives on a user's machine
   must cope with either name. A template they copy from a current checkout may assume one.
8. **A test that aborts is worse than one that fails.** Under `set -e` a failing assertion in
   a command substitution ended the whole suite without a word. Mutation-checking found it.

## Metrics

- **Diff:** about 70 files changed across two commits (see the PR). The inventory moved from
  328 to 305 files and from 3,156 to 2,648 lines: the command and its integrations are
  renamed, most of the documentation is not.
- **Test delta:** unit 2,006 to 2,017 (+11). Integration 155 to 163 (+8), 2 skipped (live Azure
  credentials).
- **Verified locally:** unit, integration, a real AOT publish through `stage.sh` with the
  archive inspected, and `make install` / `make uninstall` including the upgrade path.
- **Not verified locally:** the Docker image build and `release.yml`; CI runs the former.
- **CI status at push time:** pending at commit time; link in the PR.

## Credits

Bob led because this is a packaging problem first: the real work was reading what consumes
the archive. Jerry's lens is why the Docker image and the build scripts were not forgotten.
Jackie's is why the trademark policy now names the project it covers (a policy text, not
legal advice). Mickey's is why a screen-reader user's `hopper` and `az-ai` both complete.
FDR's objections are the section above. Cast-balance ledger after this episode: Bob has his
slot, and Jerry's co-lead is recorded. Commits carry
`Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>`.
