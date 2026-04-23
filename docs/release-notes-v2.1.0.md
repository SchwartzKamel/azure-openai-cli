# Azure OpenAI CLI v2.1.0 -- Release Notes

> A minor bump with no breaking changes. If you are on any v2.0.x release,
> upgrade in place -- no migration step required. v1.x users should read
> [`docs/migration-v1-to-v2.md`](migration-v1-to-v2.md) first, then consider
> the new `make migrate-check` target below.

🚀

## Headline

v2.1.0 is the first minor bump on the v2 line. It collects thirty-plus
episodes of accumulated work -- a cost receipt, cast personas, home-dir
credential blocklists, a v1 uninstaller contract, and a wave of docs /
process skills -- into one shipped version. No breaking changes, no new
required flags, no changes to existing exit codes, stdout bytes, or
config-file formats.

The bump is SemVer §7 territory: multiple new additive features
(`--show-cost`, `--squad-init` cast, `make migrate-check` /
`migrate-clean`, expanded `ReadFileTool` blocklist), none of which break
a script written against v2.0.x.

## Why you'll care

- **`--show-cost` tells you what the round actually cost.** Opt-in,
  stderr-only, one-line receipt per invocation (and an accumulated
  rollup across Agent / Ralph iterations). Token counts always print;
  dollar estimates only when the deployment is in the hard-coded price
  table. Raw stdout pipelines stay clean.
- **The cast is in the persona roster.** `--squad-init` now seeds 12
  Seinfeld-themed personas on top of the 5 generics -- direct name
  routing wins over keyword scoring, existing `.squad.json` files
  untouched.
- **v1 uninstall path is now a contract, not tribal knowledge.**
  `make migrate-check` audits your environment for leftover v1 bits;
  `make migrate-clean` removes them behind an explicit prompt. Safe to
  re-run. Read-only by default.
- **Seven more home-dir credential paths are off-limits to
  `read_file`.** `~/.ssh/`, `~/.kube/`, `~/.gnupg/`, `~/.netrc`,
  `~/.docker/config.json`, `~/.git-credentials` (plus the XDG
  variant), and `~/.npmrc` / `~/.pypirc` now return refusals from the
  tool surface, with 53 adversary-test facts guarding symlink, case,
  and `$HOME`-override bypass paths.
- **`shell_exec` blocklist is hardened** against IFS, Unicode, and
  shell-tokenization bypass. 8 previously-Skipped bypass tests are
  now gating.
- **Docker image is PSA-`restricted` compatible** -- explicit numeric
  `USER 10001:10001`, `--chown --chmod` on COPY, `HEALTHCHECK NONE`,
  telemetry opt-out baked in.

Full list in [`CHANGELOG.md`](../CHANGELOG.md#210--2026-04-23).

## What's new (grouped by theme)

### Security hardening

- **Home-dir credential blocklist extension** (E26 *The Locked Drawer*).
- **`shell_exec` tokenization hardening** (E32 *The Bypass*) --
  defense-in-depth pipeline: reject shell-substitution metachars up
  front, NFKC-normalize, per-segment tokenize, strip quotes, basename,
  exact-match.
- **Docker image hardening** (E14 *The Container*) -- Alpine
  multi-stage, non-root, read-only friendly.
- **Adversarial test surface** (E23 *The Adversary*, E31 *The
  Audition*) -- chaos drill against `read_file` / `shell_exec` /
  `web_fetch`, plus adversarial coverage of the 5 pre-cast generic
  personas. 9 findings filed, 1 routing-scorer bug fixed.

### Cost observability

- **`--show-cost` opt-in receipt** (E09 *The Receipt*) -- token + USD
  rollup, stderr-only, Agent / Ralph accumulation.
- **Lazy-init OTLP exporters** -- `--otel` / `--metrics` /
  `--telemetry` no longer pay the cold-start tax when no collector is
  configured. Measured drops: −20 % to −34 % on the reference rig.

### Migration ergonomics

- **`make migrate-check` / `migrate-clean`** (E33 *The Uninstaller*) --
  the first codified v1 → v2 clean-up contract. Previously the closest
  equivalent was a paragraph in the migration doc; now it is a
  checkable Makefile target.
- **Ralph `--validate` temperature default** (`0.15`) -- pass/fail
  verdicts no longer oscillate across iterations when the operator has
  not explicitly pinned temperature.

### Persona and squad

- **12-persona cast baked into `--squad-init`** (E30 *The Cast*) --
  Seinfeld-themed default roster on top of the 5 generics, additive;
  direct name routing beats keyword scoring.

### Accessibility

- **`NO_COLOR` / `FORCE_COLOR` gates** land in v1 via the new
  `AnsiPolicy` helper (E06 *The Screen Reader*). Precedence matches
  v2's `Theme.UseColor()`. First-run wizard prints a masking
  announcement before the key prompt.

### Tooling polish (docs / process)

Docs and process work that did not change a single `.cs` file but
materially changed how the project is maintained:

- **Entry-point docs map** (E25 *The Story Editor*) -- `docs/README.md`
  indexes every user-facing doc by task, with cross-link footers.
- **Orphan doc cleanup + launch index** (E34 *The Index*) -- 11
  previously orphaned docs re-linked or retired;
  `docs/launch/README.md` added.
- **Change-management contract** (E22 *The Process*) -- ADR
  stewardship, CAB-lite checklist, retrospective cadence.
- **Writers' bible + hygiene skills + cohesion skills** (E27 *The
  Bible*, E28 *The Style Guide*, E29 *The Casting Call*) -- seven new
  `.github/skills/` files consolidate procedures that had been
  re-derived per episode with minor drift.

## Customer story -- who is this release for, what problem does it solve, why now (Costanza's cut)

OK so, look. You're a developer. You've got four terminal windows open,
you're billing three clients, and somewhere in the back of your head
you're thinking -- *did that last prompt cost me a dollar? Two dollars?
A sandwich?* You don't know. Nobody knows. That's the problem. You're
flying blind on money in a tool that talks to a meter.

**That's who this release is for.** The person who needs a receipt.
Not a dashboard, not a Grafana panel, not a monthly PDF from finance --
a receipt. One line. Stderr. Out of the way. `--show-cost` gives you
that. You run the thing, you see what it cost, you move on. Nobody
asks. Nobody has to ask. It's beautiful.

And the migration thing? Here's the deal. For three months we've been
telling v1 users "yeah just uninstall it, just pull the old binary,
you'll figure it out." Meanwhile every single one of them is sitting on
a stale shim in `~/.local/bin`, a config file nobody's touched since
2025, a credential in a keystore that has the wrong schema. THAT'S not
a migration. That's an *archaeological dig*. So: `make migrate-check`.
Read-only. Tells you exactly what's still hiding on your machine.
`make migrate-clean` -- with a prompt, because I'm not Newman, I'm not
deleting anything without asking -- takes care of the rest. You can run
it twice. Three times. It's idempotent. That's the word, right? *It's
idempotent.* Anyway.

Why now? Because we just spent thirty episodes adding things and
zero episodes cutting a version. That's how you ship bugs. That's how
your users find out about a feature six months after it landed, on
Stack Overflow, from a stranger. Not on my watch. We cut the release,
we write the notes, we tell people what's in the box. Everybody sleeps
better.

Look, it's a minor bump. Nothing's broken. Your scripts still work.
Your env vars still work. Your `.squad/` memory transfers. You upgrade,
you read this page, you go back to what you were doing. That's the
deal.

## Breaking changes

None. v2.0.x → v2.1.0 is a drop-in. Every v2 flag still works, every
env var reads the same, exit codes unchanged, `--raw`/`--json` output
byte-identical, `.squad.json` format unchanged.

## Migration / upgrade notes

### From v2.0.x

Drop in. No action required.

### From v1.x

The v1 → v2 migration guide is
[`docs/migration-v1-to-v2.md`](migration-v1-to-v2.md). New in v2.1.0:

```bash
# From inside a clone, or from any install where the Makefile ships:
make migrate-check     # read-only; reports leftovers
make migrate-clean     # prompts per item before removing
```

These targets do not move any data or keys. They report stale v1 shims
on `$PATH`, stray `~/.config/az-ai` fragments, and pre-2.0 keystore
entries. The v2 keystore (DPAPI on Windows, Keychain on macOS,
libsecret on Linux with plaintext fallback at mode `0600`) is
untouched.

The v1.x branch continues to receive critical security fixes only;
feature work is v2-only.

## Known limitations at 2.1.0

- **Homebrew / Scoop / Nix manifests remain DRAFT.** The packaging
  surface under `packaging/{homebrew,scoop,nix}/` is code-complete but
  not published to a tap, bucket, or flake registry. Pre-publish
  install paths are documented in `docs/distribution/README.md`.
- **v1.x persona memory.** `.squad/history/` files written by v1 are
  compatible with v2's reader; the v2 writer adds fields the v1
  reader will silently skip. One-way upgrade, as documented.
- **Eval harness.** The prompt library and temperature cookbook under
  `docs/prompts/` define the seam for a future small eval runner; no
  runner ships in 2.1.0.

## Upgrading / rolling back

### Upgrade

- **Tarball:** download from the v2.1.0 GitHub Release and extract in
  place of your v2.0.x binary.
- **Container:** `docker pull ghcr.io/schwartzkamel/azure-openai-cli:2.1.0`.
- **Homebrew / Scoop / Nix:** see `docs/distribution/README.md` for the
  pre-publish install paths. Published taps are still pending.

### Roll back

Tags are immutable and re-installable. If 2.1.0 surfaces an issue, drop
to the v2.0.6 artifact -- tarball, GHCR `:2.0.6`, or the per-version
packaging pin -- and file a GitHub issue with the `regression` label.
No data migration is required in either direction.

## Acknowledgments

v2.1.0 carries work from thirty-plus episodes. Cast credits, in
episode-airing order:

- **E06** Mickey Abbott (`NO_COLOR` / `FORCE_COLOR` + wizard announcement)
- **E07** Frank Costanza (telemetry posture + incident runbooks)
- **E08** Babu Bhatt (i18n audit + glossary)
- **E09** The Maestro + Morty Seinfeld (`--show-cost`)
- **E11** Costanza (user-stories translation)
- **E12** Lloyd Braun (onboarding walkthrough)
- **E13** Newman (v2 security audit)
- **E14** Kramer (Alpine Docker hardening)
- **E15** Jackie Chiles (third-party license audit)
- **E16** Bob Sacamano (Homebrew / Scoop / Nix drafts)
- **E17** Uncle Leo (contributor onboarding refresh)
- **E18** The Maestro (prompt library + temperature cookbook)
- **E19** Sue Ellen Mischke (competitive landscape)
- **E20** Keith Hernandez (LOLBin credentials talk package)
- **E21** Rabbi Kirschbaum (responsible-use posture)
- **E22** Mr. Wilhelm (change-management contract)
- **E23** FDR (adversarial tool-surface chaos drill)
- **E25** Elaine (docs entry-point map)
- **E26** Newman (home-dir credential blocklist)
- **E27** Elaine + Mr. Pitt (writers' bible)
- **E28** The Soup Nazi (hygiene skills)
- **E29** Mr. Pitt (cohesion skills)
- **E30** Larry David (cast personas)
- **E31** Puddy (persona adversary coverage)
- **E32** Newman (shell_exec tokenization hardening)
- **E33** Jerry (v1 uninstaller contract)
- **E34** Elaine (orphan docs + launch index)

Hello! Contributor! Hello! Thanks to everyone on the bench --
showrunner Larry David, Costanza on PM, and Jerry on the pipes -- for
getting us to the tag.

---

**Release manager:** Mr. Lippman.
**Questions?** File an issue or see
[`CONTRIBUTING.md`](../CONTRIBUTING.md).
