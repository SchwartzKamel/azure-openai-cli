# FR-023 -- First-run interactive setup wizard with per-OS credential storage

**Status:** Proposed
**Target:** 2.2
**Author:** Copilot fleet (Elaine, Costanza, Newman, Rabbi Kirschbaum, Kramer)
**Owner (eng):** Kramer
**Date:** 2025-01-27
**Related:** FR-003 (local preferences), FR-009 (`--config set`), FR-022 (native `az-ai setup` -- superseded in part by this FR's storage design)

## Summary

When `az-ai` is run without credentials on an interactive terminal, drop the user into a friendly wizard that collects endpoint, API key, and model; validates with a cheap round-trip; and persists to an OS-appropriate store. Subsequent runs are silent. The wizard is skipped in every scripted context (pipes, `--raw`, `--json`, containers, CI), so no existing workflow breaks.

## Problem

The tool's promise is "type a prompt, get text." Today, first contact looks like this:

1. `az-ai "hi"` errors out with missing-env complaints.
2. User greps the repo, finds `.env.example`.
3. User copies it, edits three values, saves.
4. User runs `source .env` -- or figures out they need to, after another failure.
5. User retries. Maybe it works.

There is no in-product discovery path. `.env.example` is a repo artifact, not a CLI surface; a `brew install`'d or `scoop install`'d binary has no reference to it. The bootstrap scripts in FR-022 closed part of this gap for Linux/WSL, but they still require the user to know a script exists and to invoke it manually.

A first-run wizard reverses the flow: the tool asks, the user answers, and the thing works.

## Non-goals

The following are out of scope for this FR and are tracked as follow-ups:

- **Multi-profile support** (`--profile work` / `--profile personal`). Future FR.
- **OAuth / Entra ID authentication.** Larger scope, different threat model. Future FR.
- **TPM-bound secrets.** Hardware-attested storage. Future FR.
- **Linux `systemd-creds` / `secret-tool` / libsecret providers.** Well-scoped follow-up FR; the `ICredentialStore` abstraction in this FR is explicitly designed to accept them later without breaking changes.

## Design

### Precedence (highest to lowest)

1. CLI flags (`--model`, `--endpoint`, etc.) -- unchanged.
2. Environment variables (`AZUREOPENAIENDPOINT`, `AZUREOPENAIAPI`, `AZUREOPENAIMODEL`) -- unchanged.
3. Persisted values in `~/.azureopenai-cli.json` (via `ICredentialStore`). **New.**
4. Wizard prompt (interactive TTY only). **New.**

Environment variables still win over stored config. Docker, CI, Espanso, and AHK flows keep working byte-for-byte.

### Trigger conditions

The wizard runs when **all** of these hold:

1. `AZUREOPENAIENDPOINT` **and** `AZUREOPENAIAPI` are both unset in env **and** config.
2. `stdin` **and** `stderr` are TTYs.
3. User did not pass `--raw`, `--json`, or `--no-interactive`.
4. Not inside a container (`/.dockerenv` absent and `$container` unset).

If any check fails, the existing `ErrorAndExit` path runs. Scripts, pipes, and containers get the same error they get today.

`az-ai --init` (aliases: `--configure`, `--login`) bypasses the checks and always runs the wizard, even when credentials already exist. This is the re-configuration and rotation entry point.

### Wizard flow

```text
$ az-ai
Welcome to az-ai! Let's get you set up. (takes ~30 seconds)

Azure OpenAI endpoint URL
  e.g. https://my-resource.openai.azure.com/
> https://contoso.openai.azure.com/

API key (input hidden)
> ••••••••••••••••

Model deployment name (comma-separated for multiple)
  e.g. gpt-4o,gpt-4o-mini
> gpt-4o

Testing connection... ✓ authenticated (gpt-4o responded in 412ms)
Saved to /home/user/.azureopenai-cli.json (mode 0600)

Run 'az-ai --config show' anytime to inspect settings.
────────────────────────────────────────────────────────
```

Behavior details:

- API key input uses `Console.ReadKey(intercept: true)`, echoing `•` per character.
- Endpoint is validated with `Uri.TryCreate` plus an HTTPS scheme check.
- Validation is a minimal `chat.completions` "ping" with a 10 s timeout. On failure the wizard offers retry or skip; it never persists unvalidated credentials without explicit confirmation.
- SIGINT during the wizard exits cleanly with no partial write.

### Per-OS storage (LOLBin: Living Off The Land)

Use what is already on the machine. No new NuGet dependencies. Provider is selected at runtime.

| OS | Provider | Binary / API | Notes |
|---|---|---|---|
| Windows | `DpapiCredentialStore` | `CryptProtectData` / `CryptUnprotectData` via P/Invoke to `crypt32.dll` | Scope `CurrentUser`. `crypt32.dll` ships with every Windows since 2000. AOT-safe via `[LibraryImport]` source-gen. Ciphertext stored base64 in the JSON file. |
| macOS | `MacSecurityCredentialStore` | `/usr/bin/security add-generic-password` / `find-generic-password` | Always preinstalled. Service `az-ai`, account `$USER`. Decrypted key cached in process memory for the single run. |
| Linux | `PlaintextCredentialStore` | `~/.azureopenai-cli.json`, mode `0600` | Industry baseline (see Security trade-offs). Compensating control: documented key rotation. |
| Container | `EnvOnlyStore` | env vars only | Wizard never runs in containers; credentials come from env. Detection via `/.dockerenv` and `$container`. |

### `ICredentialStore` abstraction

```csharp
internal interface ICredentialStore
{
    string ProviderName { get; }
    void Store(string apiKey);
    string? Retrieve();
    void Delete();
}

public static ICredentialStore Create() =>
      SetupDetection.IsContainer() ? new EnvOnlyStore()
    : OperatingSystem.IsWindows()  ? new DpapiCredentialStore()
    : OperatingSystem.IsMacOS()    ? new MacSecurityCredentialStore()
    :                                new PlaintextCredentialStore();
```

Rationale: the interface is a clean seam. Adding `SystemdCredsStore`, `SecretToolStore`, or a future TPM-bound provider is a new class plus a branch in `Create()`. No caller changes. This is what unblocks the follow-up FR for Linux opportunistic upgrades.

### Config file layout

Same file (`~/.azureopenai-cli.json`, mode `0600`). New fields on `UserConfig`:

- `Endpoint` -- plaintext, low-sensitivity.
- `ApiKeyProvider` -- `"dpapi" | "macos-keychain" | "plaintext" | "env-only"`. Load code uses this to pick the retrieval path.
- `ApiKeyCiphertext` -- base64 DPAPI blob (Windows only).
- `ApiKey` -- plaintext (Linux only; null elsewhere).
- `ApiKeyFingerprint` -- `sha256(key)[0..12]`. Shown in `--config show` for safe display and tamper detection. See Security trade-offs.

On macOS the key lives in the Keychain; the JSON file stores only fingerprint and provider tag.

## Rejected alternatives

- **`systemd-creds`** -- not present on Ubuntu 22.04, Alpine, or minimal containers. Revisit behind the `ICredentialStore` seam in a follow-up.
- **`secret-tool` / libsecret** -- requires a running D-Bus session and a desktop keyring daemon; fails on headless SSH and WSL.
- **`gpg --symmetric`** -- needs `gpg-agent` and interactive unlock. Terrible UX for a keystroke-injection tool that must be fast and silent after first run.
- **`keyctl` kernel keyring** -- keys vanish on reboot. Wrong lifetime for a persisted credential.
- **`System.Security.Cryptography.ProtectedData` NuGet package** -- unnecessary dependency. Direct `[LibraryImport]` to `crypt32.dll` is zero-dep, AOT-clean, and roughly 40 lines of reviewable code.
- **AES-GCM with machine-id-derived key on Linux** -- obfuscation, not security. Any local process running as the same user can re-derive the key. Security theater is worse than honest plaintext because it invites false confidence.
- **Hashing the stored key** -- one-way. Azure needs the plaintext key to authenticate, so we cannot replay a hash. This is a frequent user question and the answer is in Security trade-offs below.

## Security trade-offs

This section is deliberately explicit. The Linux plaintext-at-rest decision is the single most scrutinized part of the design and we want reviewers to see the reasoning, not infer it.

### Industry baseline

On Linux, the API key is stored as plaintext at mode `0600`. This matches:

- **AWS CLI** -- `~/.aws/credentials`, plaintext, `0600`.
- **GitHub CLI** -- `~/.config/gh/hosts.yml`, plaintext, `0600` on Linux.
- **Azure CLI** -- `~/.azure/`, plaintext, `0600`.

We are not inventing a weaker posture. We are matching the established baseline for developer CLIs.

### Compensating control: documented 90-day rotation

Azure OpenAI resources expose two active keys and one-click regeneration. This is designed for zero-downtime rotation. The wizard, `--config show`, and the README all surface this guidance:

> Rotate your Azure OpenAI key roughly every 90 days. In the Azure portal, navigate
> to your resource, open **Keys and Endpoint**, and click **Regenerate Key 1** (or
> Key 2). Update `az-ai` with `az-ai --init`. The two-key model lets you flip between
> keys with no downtime.

For higher-assurance setups, environment variables remain the escape hatch: Docker secrets, CI/CD secret stores, HashiCorp Vault, Azure Key Vault via a launcher script. Env vars always override stored config.

### `ApiKeyFingerprint` -- the legitimate use of hashing

`sha256(key)[0..12]` is stored alongside the key (or in place of it on macOS). Two purposes:

1. **Safe display.** `az-ai --config show` prints `API Key: sha256:a4f2b8c1d9e0 (set)`. The raw key never appears in diagnostic output.
2. **Tamper detection.** On load, if the retrieved key's fingerprint does not match the stored fingerprint, the wizard is re-offered. This catches corrupted stores and partial writes.

Hashing is correct *here* because we are not trying to replay the hash; we only need to identify the key.

### Threat model

**We defend against:**

- Casual filesystem snooping. A bystander running `cat ~/.azureopenai-cli.json` on an unlocked laptop. Mode `0600` blocks other local users.
- Backup leakage. A `tar -czf backup.tgz $HOME` that gets shared or uploaded. Fingerprint-only display in diagnostics means logs and screenshots do not leak the key.
- Log and screenshot leakage. The key is never echoed to stdout or stderr; only the fingerprint is.

**We do not defend against:**

- A local attacker already running as the target user. They can read env vars, process memory, and keystrokes; a wrapped file does not help.
- Root on the local machine. Out of scope for a user-level CLI.
- A compromised machine. Full-disk encryption and host hardening are the user's responsibility.

This is the same threat model as AWS CLI, GitHub CLI, and Azure CLI. We are not solving a harder problem than they do, and we are honest about it.

## Signoffs

### Newman (security)

Reviewed: per-OS storage backends, plaintext-at-rest decision on Linux, threat model, logging redaction, `ApiKeyFingerprint` design.

- DPAPI scope is `CurrentUser`. `/usr/bin/security` is the Apple-shipped binary, not a third-party tool. Plaintext at `0600` matches AWS/GH/Azure CLI baseline and is documented, not hidden.
- `AZUREOPENAIAPI` stays in the `ShellExecTool` env blocklist. Fingerprint display path confirmed to never surface the raw key. Validation ping refuses to persist broken credentials silently.

No soup for you if this lands without the rotation guidance in the README and in `--config show`. Signing off conditional on those docs shipping in the same release.

### Rabbi Kirschbaum (ethics)

Reviewed: user autonomy, transparency, failure modes.

- The wizard is opt-out by construction (any non-TTY, any flag-indicated scripted context) and discloses exactly where the key is stored and at what mode. Users are not surprised later. The trade-offs are stated in the tool, not buried in a wiki.
- Validation before persistence means the tool does not lie to the user about success. The rotation guidance respects the user's ability to act on the risk rather than hiding it.

Signed off. The ethical posture here is honesty over theater; that is the right call.

## Implementation plan

Implementation is tracked in the session SQL todo tracker (16 todos, dependency-ordered). High-level groupings:

1. `ICredentialStore` interface and factory; per-OS stores (Windows/macOS/Linux/container).
2. `UserConfig` field additions and JSON source-gen regeneration.
3. `SetupDetection` (TTY, container, creds-missing).
4. `FirstRunWizard` (masked input, URL validation, validation ping, store delegation).
5. `Program.cs` wire-up: wizard invocation, `--init` flag, precedence preservation.
6. Tests: per-OS stores (guarded by `OperatingSystem.IsX()`), wizard detection matrix, UserConfig round-trip, integration skip paths.
7. Docs: this FR, README first-run section, CHANGELOG entry.
8. `make preflight` gate.

See the tracker for exact IDs, titles, and dependency edges.

## Open questions

None blocking. Two known follow-ups, both deliberately out of scope:

- **Linux opportunistic upgrades** (`systemd-creds`, `secret-tool`, TPM). Scoped as a separate FR; the `ICredentialStore` abstraction in this FR is the forward-compatible seam for them. No design decisions here foreclose that work.
- **Multi-profile and OAuth/Entra.** Larger scope, separate FRs.
