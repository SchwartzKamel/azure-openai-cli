# FR-022 — Native `az-ai setup` credential wizard

**Status:** Proposed
**Target:** 2.1
**Author:** Costanza (PM) + Kramer (eng) + Newman (security)
**Owner (eng):** Kramer
**Related:** FR-010 (UserConfig), [`scripts/setup-secrets.sh`](../../scripts/setup-secrets.sh) (2.0 bootstrap), [`docs/espanso-ahk-integration.md`](../espanso-ahk-integration.md) §*Secure env-var storage*

## Motivation

`az-ai` today expects `AZUREOPENAIENDPOINT` / `AZUREOPENAIAPI` / `AZUREOPENAIMODEL` in the process environment. On a fresh install — especially on WSL invoked by espanso/AHK — getting those three variables into the right rc file, with the right permissions, at the right shell-invocation scope (login vs. non-login vs. interactive vs. `bash -lc`), is nontrivial. The 2.0.0 silent-fail of `:aifix` we fixed in [`46e8652`](https://github.com/SchwartzKamel/azure-openai-cli/commit/46e8652) came straight from this class of error.

2.0 ships `scripts/setup-secrets.sh` as a bootstrap — a shell wizard that picks a storage tier (chmod 600 plaintext or GPG-symmetric) and installs an auto-source hook into `~/.profile` + `~/.zshenv`. That's fine for Linux/WSL, but:

- **Not cross-platform.** No Windows-native story, no macOS Keychain story.
- **Not self-discoverable.** User has to know the script exists and run it from the repo. A freshly `brew install`'d binary has no reference to it.
- **Not integrated with `UserConfig`.** The config system (`~/.azureopenai-cli.json`) stores model aliases but not credentials; users have two places to look for "how is this configured?".

A native `az-ai setup` subcommand closes all three gaps and makes the credential flow a first-class CLI citizen, on par with `--config`, `--squad-init`, and `--personas`.

## Proposal

Add an interactive `az-ai setup` subcommand (no positional args) that:

1. **Detects platform and shell** — WSL / Linux / macOS / Windows-native; zsh / bash / fish / PowerShell / cmd.
2. **Prompts for credentials** with hidden input on the API key, sensible defaults from any existing env/config.
3. **Offers a platform-appropriate storage backend**, picked from a short list:

   | Platform | Default | Alternatives |
   |---|---|---|
   | Linux / WSL | `~/.config/az-ai/env` (chmod 600) | GPG-symmetric, `libsecret` via `secret-tool` |
   | macOS | Keychain via `security add-generic-password` | chmod 600 fallback |
   | Windows | DPAPI via `cmdkey` / Credential Manager | chmod 600 fallback |

4. **Writes the shell hook**, platform-correct, idempotent. Same `# >>> az-ai creds hook >>>` / `# <<< az-ai creds hook <<<` markers as the 2.0 bootstrap script for compatibility and migration.
5. **Verifies** — replays the 3 probes the bootstrap script runs (env reaches `bash -lc`, binary on PATH, clipboard non-empty) with GREEN/RED output.
6. **Shows next steps** — platform-appropriate reload command, link to espanso docs.

Also: `az-ai setup --rotate` (rotate the key in-place, reuse endpoint/model), `az-ai setup --verify` (run probes only, no prompts), `az-ai setup --uninstall` (remove hook + secret file + optionally keychain entry).

## Non-goals

- **Not** a secrets manager for arbitrary credentials. Only the three known vars.
- **Not** a replacement for org-scale credential delivery (Azure Managed Identity, Key Vault, etc.). Those should be documented as alternatives; `setup` is for local-dev personal use.
- **Not** an AOT-trim-hazardous dependency. The `SecretStorage` abstraction must work with existing `System.Text.Json` + `System.Diagnostics.Process` shell-outs. No new NuGet packages.

## Design sketch

```csharp
// azureopenai-cli-v2/Setup/ISecretStorage.cs
internal interface ISecretStorage {
    string BackendName { get; }
    bool IsAvailable();                          // can we write here on this host?
    Task WriteAsync(Credentials creds, CancellationToken ct);
    Task<Credentials?> ReadAsync(CancellationToken ct); // null = not stored
    Task RemoveAsync(CancellationToken ct);
    string? PostWriteInstructions();             // e.g. "source ~/.zshenv"
}

// implementations
internal sealed class ChmodPlainFileStorage : ISecretStorage { ... }  // all Unix
internal sealed class GpgSymmetricStorage  : ISecretStorage { ... }  // all Unix + gpg
internal sealed class LibsecretStorage     : ISecretStorage { ... }  // Linux + secret-tool
internal sealed class MacosKeychainStorage : ISecretStorage { ... }  // macOS
internal sealed class DpapiCredManStorage  : ISecretStorage { ... }  // Windows + cmdkey
```

The shell-hook writer is its own abstraction (`IShellHookWriter`) with implementations for POSIX shells (appends to `~/.profile` + `~/.zshenv`, mirroring `setup-secrets.sh`) and PowerShell (appends to `$PROFILE`).

`az-ai setup` wires them together: pick storage based on platform+availability, prompt, write secrets, write hook, verify.

## Acceptance

1. `az-ai setup` on a fresh Linux/WSL box writes `~/.config/az-ai/env` with chmod 600 (default) and hooks into `~/.profile` + `~/.zshenv`. `bash -lc 'echo $AZUREOPENAIENDPOINT'` prints the endpoint. Probe all green.
2. `az-ai setup` on a fresh macOS box writes to Keychain via `security add-generic-password` and hooks into `~/.zshenv` (macOS default shell). `zsh -lc 'echo $AZUREOPENAIENDPOINT'` prints the endpoint.
3. `az-ai setup` on Windows writes to Credential Manager via `cmdkey` and appends to `$PROFILE`. `pwsh -NoProfile -Command "$env:AZUREOPENAIENDPOINT"` prints the endpoint *after* re-sourcing `$PROFILE`.
4. `az-ai setup --verify` runs without prompts and exits 0 green / nonzero red.
5. `az-ai setup --rotate` prompts only for the API key, reuses endpoint/model from the stored value.
6. `az-ai setup --uninstall` removes the hook markers, the secret file, and the keychain/credman entry.
7. Migration: if `~/.config/az-ai/env` (from `setup-secrets.sh`) exists, `az-ai setup` offers to migrate it and drops the plain file after successful migration.
8. No new NuGet dependencies. `dotnet publish -p:PublishAot=true` builds clean.
9. Integration tests: new section in `tests/integration_tests.sh` covering (a) setup writes + probes pass, (b) `--verify` idempotent, (c) `--rotate` keeps endpoint, (d) `--uninstall` cleans up.
10. Backwards compat: if env vars are already set in the parent process, `az-ai` ignores stored secrets. Env wins. (This is the rule today and doesn't change.)

## Security review (Newman)

- **At-rest encryption tier.** Default backend MUST be OS-native encrypted store on macOS and Windows. Linux/WSL default stays chmod 600 because `libsecret` isn't always available (headless WSL has no keyring daemon); users who want encryption there pick GPG or libsecret explicitly at prompt time.
- **No plaintext in logs.** `[az-ai] setup:` logs must never echo the API key. Not on success, not on failure. Test case 4 in `tests/integration_tests.sh` greps logs for `sk-` / first 10 chars of a test key.
- **Secure temp files.** GPG and Keychain paths that stage plaintext to a temp file must use `mktemp` + `trap rm`, same as `setup-secrets.sh` already does.
- **`--uninstall` leaves no residue.** Including zero-length leftover files, stale hook markers, orphaned keyring entries.

## Rollout

- **2.0.x:** `setup-secrets.sh` + `unlock-secrets.sh` ship in `scripts/`. README mentions them. No native subcommand yet.
- **2.1.0:** `az-ai setup` lands with Linux + macOS + Windows backends. `setup-secrets.sh` stays in `scripts/` with a deprecation banner pointing to `az-ai setup`; removed in 3.0.
- **2.1.1:** polish based on user reports (common failure modes, better diagnostics).

## Fleet sign-off

- **Costanza (PM):** go — user-facing friction, clear UX win.
- **Kramer (eng):** scoped — 2 days of work, no deps, fits AOT.
- **Newman (security):** go, conditional on Keychain/DPAPI backends being default on their platforms.
- **Mickey (a11y):** respects NO_COLOR, hidden input uses termios not ANSI tricks. Follows `.github/contracts/color-contract.md`.
- **Elaine (docs):** deprecation banner + README walkthrough.
- **Mr. Lippman (release):** 2.1.0, not 2.0.x — new subcommand is not a patch-level change.
