# S02E16 -- *The Catalog*

> *Bob Sacamano stands up Homebrew, Scoop, and Nix drafts so the v1 binary stops shipping only as Docker + curl.*

**Commit:** `ba96f6d`
**Branch:** `main` (direct push)
**Runtime:** ~25 min
**Director:** Larry David (showrunner)
**Cast:** 2 sub-agents (Bob Sacamano lead, Mr. Lippman guest) across 1 dispatch wave

## The pitch

Today the v1 line ships as a Docker image and a `make publish-aot`
binary download. Anyone on macOS who reaches for `brew install`,
anyone on Windows who reaches for `scoop install`, and anyone on
NixOS who reaches for a flake input gets nothing. The v2 line
already has a packaging story under `/packaging/{homebrew,scoop,nix}/`
for the `az-ai-v2` binary; the v1 line never got one.

This episode catches the v1 line up to where v2 sits: three draft
manifests + three approach docs + a comparison index. No registries
were created, no PRs were filed, no checksums were filled -- this is
the manifest-and-docs scaffolding that makes the publish step in a
future episode (or Bob's S03/S06 work) a one-day job instead of a
one-week scoping exercise.

The Lippman cameo earns its keep on the Scoop manifest's
`checkver.regex`: `v(1\.\d+\.\d+)` deliberately constrains the
auto-updater to the v1 line so a future v2.0.0 tag never silently
upgrades a v1 user across a documented MAJOR break.

## Scene-by-scene

### Act I -- Planning

- **Pivot 1.** Brief said "Today the only ship surface is Docker +
  manual binary." That is true for v1 (`AzureOpenAI_CLI`) but **not**
  for v2 (`az-ai-v2`), which already has a complete packaging
  directory. Decision: deliver v1 drafts as parallel siblings, name
  them `azure-openai-cli` (the project name) to disambiguate from the
  `az-ai-v2` siblings, and document the dual-binary intent for the
  future collapse episode.
- **Pivot 2.** Brief specified `packaging/nix/flake.nix` (NEW). That
  path is occupied by the v2 flake (200+ lines, version 2.0.6, with
  pinned siblings). Two flakes cannot share one path. Decision: place
  the v1 flake at `packaging/nix/azure-openai-cli/flake.nix` (a
  subdirectory) and document the deviation in this report and in
  `docs/distribution/nix.md`.
- **Lippman input adopted.** Constrain Scoop's `checkver.regex` to
  `v(1\.\d+\.\d+)` so v2 tags never silently upgrade v1 users.
  Mirrored in the Nix flake by hard-coding `version = "1.9.1"` rather
  than reading from a tag template.
- **NOTICE bundling preserved.** Every manifest stages `LICENSE`,
  `NOTICE`, and `THIRD_PARTY_NOTICES.md` alongside the binary -- the
  rule from the v2-line packaging README carries over verbatim.

### Act II -- Fleet dispatch

| Wave | Agents (parallel) | Outcome |
|------|-------------------|---------|
| **1** | Bob Sacamano (lead), Mr. Lippman (guest review) | Three manifest drafts + three approach docs + index README + CHANGELOG bullet. Lippman's SemVer constraint applied to Scoop checkver regex. |

### Act III -- Ship

- ASCII grep on every new `.md` -- clean.
- Scoop manifest validated with `python -m json.tool` -- parses.
- Homebrew formula visually two-space-indented; Ruby not installed in
  this environment so `brew style` could not run -- gap noted.
- Nix flake skeleton hand-checked for shape; `nix flake check` could
  not run because `nix` is **not installed** in this filming
  environment -- gap noted, closed at tag-time on a Nix-equipped
  workstation.
- `git status` confirms only docs + manifest paths are staged. No
  C# touched. Per `docs-only-commit`, preflight skipped.
- Commit `docs(distribution): Homebrew/Scoop/Nix packaging drafts -- S02E16 The Catalog`,
  no-sign, Copilot trailer.
- Push to `origin main`.

## What shipped

**Production code** -- none. Manifests are configuration:

- `packaging/homebrew/azure-openai-cli.rb` -- DRAFT formula. Class
  `AzureOpenaiCli`. Two `sha256 "TODO_FILL_AT_RELEASE_TIME"`
  placeholders (osx-arm64, linux-x64). Symlinks `AzureOpenAI_CLI` to
  `azure-openai-cli` on PATH. NOTICE-bundling guard preserved from
  the v2 formula.
- `packaging/scoop/azure-openai-cli.json` -- DRAFT bucket manifest.
  Single `TODO_FILL_AT_RELEASE_TIME` SHA256. `bin` rename publishes
  `azure-openai-cli` from `AzureOpenAI_CLI.exe`. `checkver.regex`
  pinned to v1 line per Lippman.
- `packaging/nix/azure-openai-cli/flake.nix` -- DRAFT flake skeleton.
  Two `nixpkgs.lib.fakeHash` placeholders. `flake-utils.eachSystem`
  over `x86_64-linux` and `aarch64-darwin`. `autoPatchelfHook` on
  Linux to make the AOT binary find its libc dependencies.

**Tests** -- n/a (manifest drafts; no runtime behavior changed).

**Docs**:

- `docs/distribution/README.md` (NEW) -- index + comparison table
  (audience / cadence / signing / maintainer burden / publish path) +
  the explicit list of channels we *skipped* and why.
- `docs/distribution/homebrew.md` (NEW) -- custom-tap-first approach,
  inline formula preview, checksum-pinning rules, install/uninstall
  verification commands, deferred-work list.
- `docs/distribution/scoop.md` (NEW) -- bucket-first approach, JSON
  preview, the SemVer link to Lippman's policy spelled out, install
  verification, validation snippet.
- `docs/distribution/nix.md` (NEW) -- in-repo flake -> dedicated repo
  -> nixpkgs PR escalation, why the v1 flake lives in a subdirectory,
  SRI-hash strategy, validation gap (nix not installed).
- `CHANGELOG.md` -- one bullet under `[Unreleased] > Added` calling
  out the three packaging drafts as a user-visible deliverable
  (packaging IS user-visible: a user can now read the doc, build the
  formula locally, and install pre-tap).

**Not shipped** (intentional follow-ups):

- Tap repository creation (`SchwartzKamel/homebrew-tap`). Requires
  maintainer account; out of scope for a manifest-drafting episode.
- Bucket repository creation (`SchwartzKamel/scoop-bucket`). Same.
- Dedicated Nix flake repo or nixpkgs PR. Bob's S03/S06 work.
- Filling `TODO_FILL_AT_RELEASE_TIME` and `lib.fakeHash` placeholders.
  These are tag-time tasks per `packaging/README.md` ritual; doing
  them in this episode would invent digests for an unpublished v1.9.1
  release.
- `<artifact>.sha256` sibling files in the v1 release workflow (Scoop
  `autoupdate.hash.url` consumes them). Workflow change defers to a
  release-engineering episode.
- Linux ARM64 builds in the v1 release matrix. Touching the matrix is
  out of scope; doc'd as deferred in `homebrew.md` and `nix.md`.
- README install section update -- explicitly out of scope per brief
  (touch later in dedicated episode).
- Collapse of v1 + v2 packaging into single per-channel manifest after
  v2 GA. Separate episode.
- AGENTS.md / TV guide / writers' room updates. Per `shared-file-protocol`,
  orchestrator-owned -- Larry's batch picks them up.

## Lessons from this episode

1. **Reading the existing packaging tree before writing the new one
   prevented two collisions.** The brief described Bob's territory as
   greenfield; it is not. The v2 line already taught the project what
   tag-time hash filling looks like. The v1 drafts inherit the
   ritual rather than reinvent it. *Caught by Bob (lead) during Act I.*
2. **Path-collision deviation belongs in the report, not silently in
   the file tree.** `packaging/nix/flake.nix` was specified by the
   brief but already exists. Placing the v1 flake at
   `packaging/nix/azure-openai-cli/flake.nix` is the right call;
   silently dropping the brief-specified path is not. Documented
   here and in `docs/distribution/nix.md`.
3. **Lippman's cameo earned a concrete artifact.** "Constrain
   `checkver.regex` to the v1 line" is a one-line manifest change
   that prevents a SemVer-MAJOR silent upgrade. The kind of detail a
   guest agent should reliably contribute.
4. **Validation gaps must be named, not hidden.** `nix` is not
   installed in the filming environment, so `nix flake check` could
   not run. Recording the gap and pointing at the tag-time
   workstation closure is honest; pretending the flake is verified
   would be unsafe.

## Metrics

- Diff size: 9 files added, 0 modified outside CHANGELOG. ~430 lines
  inserted across docs + manifests + this report. 1 line into
  CHANGELOG.
- Test delta: n/a (docs + manifests).
- Preflight: **skipped** -- `git status` confirmed docs + manifests
  only, no `.cs` / `.csproj` / `.sln` / workflow files touched. Per
  `docs-only-commit`.
- CI status at push time: pending (filled after push completes).

## Credits

- **Bob Sacamano** -- lead. Authored every manifest draft, both
  packaging-readable approach docs, the comparison index, the
  validation calls, and the deferred-work lists.
- **Mr. Lippman** -- guest. SemVer regex constraint on the Scoop
  `checkver` block; reaffirmed the NOTICE-bundling rule across all
  three channels; aligned the artifact-naming contract with
  `docs/release/semver-policy.md`.
- **Copilot** -- co-author trailer applied to the commit per
  `.github/skills/commit.md`.
