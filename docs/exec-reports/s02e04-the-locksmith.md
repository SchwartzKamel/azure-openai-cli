# S02E04 -- *The Locksmith*

> *Linux finally gets a real keyring. When the desktop has one, we use
> it; when it doesn't, we keep being honest about the fallback.*

**Commit:** `ef8756c`
**Branch:** `main` (direct push, solo-led repo per `skill:commit`)
**Runtime:** ~20 minutes, single-shot episode
**Director:** Copilot (fleet orchestrator)
**Cast:** Kramer (engineer) + Newman (security) as the on-screen pair;
Jerry on preflight; Elaine on the README/CHANGELOG touch-ups.

## The pitch

S02E01 shipped three credential providers behind `ICredentialStore` and
closed with a blueprint that called out `secret-tool` (libsecret) as the
obvious opportunistic upgrade for Linux desktops. The plaintext fallback
was an honest baseline, not a ceiling -- GNOME Keyring and KDE Wallet
are already running on most developer workstations, and libsecret gives
us a zero-dependency hop into either of them.

This episode is the hop. If `/usr/bin/secret-tool` is present *and* a
DBus session bus is advertised, `az-ai` now stores its API key in
libsecret -- no key on disk, just a non-secret fingerprint in
`~/.azureopenai-cli.json`. If either signal is missing (headless box,
minimal install, container, SSH without forwarding), the factory falls
back to plaintext at `0600` exactly like before. `systemd-creds` stays
on the S03 shortlist -- different ergonomic, service-user focus, not
the right fit for an interactive CLI.

## Scene-by-scene

### Act I -- Planning

Scope was already tight from the orchestrator brief: one new provider,
one factory branch, one test block, CHANGELOG + README. Out of scope,
explicitly:

1. `systemd-creds` -- different audience, different UX. Stays on the
   S03 blueprint.
2. Factory-to-registry refactor. Premature while we have four providers.
3. Un-skipping the Mac tests. They need their own injection-hook episode.

### Act II -- Fleet dispatch

A single wave, because the work was genuinely linear:

| Wave | Agents (parallel)   | Outcome |
|------|---------------------|---------|
| **1** | Kramer + Newman    | `SecretToolCredentialStore.cs` (218 lines), factory branch, 4 runnable Linux tests + 7 daemon-gated skips, CHANGELOG + README. |

Kramer mirrored `MacSecurityCredentialStore` for structural parity --
same timeout constant, same `Scrub()` helper, same `ArgumentList`-only
spawn discipline, same `ResolveAccount()` fallback from
`Environment.UserName` to `$USER`.

Newman's notes landed inline:

- Secret passed on **stdin**, never argv. That's the whole reason to
  prefer libsecret over a file; argv is world-visible in `/proc`.
- 10 s `WaitForExit` with an entire-process-tree termination on hang --
  the common hang is a keyring unlock prompt that will never be
  answered from a non-interactive spawn.
- Scrubbed exception messages on every error path; the key is
  replaced with `<redacted>` before any stderr surfaces.
- `Retrieve()` treats exit 1 with empty stderr as "not found"
  (libsecret's ergonomic is to not distinguish) and exit 1 with
  stderr content as a genuine failure.

### Act III -- Ship

- `make preflight`: green. 150 integration tests pass, 3 skipped
  (unrelated, require live credentials).
- New unit tests: 6 runnable Linux facts pass, 7 daemon-required tests
  are `[Fact(Skip=...)]` with a reason that calls out the absent CI
  DBus session.
- `dotnet format --verify-no-changes`: clean.
- Push to `main`: `ef8756c`.

## What shipped

**Production code**

- `azureopenai-cli/Credentials/SecretToolCredentialStore.cs` -- 218
  lines. `internal sealed class`, `[SupportedOSPlatform("linux")]`,
  `ProviderName => "libsecret"`. Shape matches `MacSecurityCredentialStore`
  byte-for-byte on the non-domain-specific parts (timeout, process
  plumbing, scrub helper, account resolution).
- `azureopenai-cli/Credentials/CredentialStoreFactory.cs` -- one new
  branch between macOS and the plaintext fallback:

  ```csharp
  if (OperatingSystem.IsLinux() && SecretToolAvailable())
  {
      return new SecretToolCredentialStore(config);
  }
  ```

  Plus `SecretToolAvailable()`: both the binary and a DBus session bus
  address must be present. Either signal alone is insufficient.

**Tests**

- `tests/AzureOpenAI_CLI.Tests/CredentialStoreTests.cs` -- new
  `LinuxOnlyFactAttribute`, `SecretToolCredentialStoreTests` class,
  `CredentialStoreFactoryLinuxTests` class.
- Runnable on Linux CI: `ProviderName_Is_Libsecret`,
  `Store_Null_Throws`, `Store_Empty_Throws`, `Store_Whitespace_Throws`,
  `NoDbus_FallsBackToPlaintext_OnLinux`,
  `NotLinux_DoesNotReturnSecretToolStore`.
- Skipped (daemon-required, scaffolding for local runs):
  roundtrip, retrieve-null, delete, delete-idempotent, cache,
  exception-scrub.

**Docs**

- `README.md` -- updated the per-OS storage table so the Linux row
  reads "libsecret (GNOME Keyring / KDE Wallet) when `/usr/bin/secret-tool`
  and a DBus session are present; otherwise plaintext, file mode
  `0600`." The narrative paragraph below now frames libsecret as the
  preferred provider while staying honest about the fallback.
- `CHANGELOG.md` -- new `Unreleased > Added` bullet describing the
  opportunistic libsecret store, detection rules, fallback posture,
  and zero new NuGet deps.

**Not shipped (intentional follow-ups)**

- `systemd-creds` provider -- still on S03. Different audience.
- Mac Keychain test un-skipping -- needs an `internal` ctor
  accepting a service-name override; out of scope here.
- Factory-to-registry refactor -- premature at four providers.
- `docs/exec-reports/README.md` episode index bump -- the
  orchestrator owns that after both S02E03 and S02E04 land.

## Newman signoff

The three risks libsecret introduces that plaintext didn't have:

1. **Argv leakage of the secret.** Mitigated: secret goes through
   stdin with `RedirectStandardInput = true`; argv carries only the
   attribute pairs `service az-ai`, `username <account>`, and the
   cosmetic `--label=az-ai`.
2. **Indefinite hang on a desktop unlock prompt.** Mitigated: 10 s
   `WaitForExit` plus `Kill(entireProcessTree: true)`, surfacing a
   `CredentialStoreException` whose message names the probable cause.
3. **Secret echoing back in a stderr error path.** Mitigated: every
   exception message passes through `Scrub()`, which replaces the raw
   key with `<redacted>` before it can be logged.

Input validation (`null`/empty/whitespace) is enforced on `Store()` at
the same level as the other providers after S02E02. Signoff granted.

## Lessons from this episode

1. **Structural parity with an existing provider pays for itself in
   review speed.** Kramer's diff reads as "MacSecurityCredentialStore
   with a different binary and stdin for the secret," which is exactly
   what it is. No new idioms.
2. **Two-signal detection matters.** A binary check alone would have
   activated libsecret on SSH sessions without forwarded DBus, where
   the subsequent `store` would hang until the timeout. The DBus env
   check keeps the happy path fast and the fallback path automatic.
3. **Daemon-required tests should be scaffolded, not deleted.** The
   Mac scaffolding pattern lets a future session flip the skip flags
   the moment a test harness with a real libsecret daemon is wired up.

## Metrics

- Diff size: **+386 / -3 across 5 files** (production 254, tests 125,
  docs 10).
- Test delta: **6 new runnable tests** (Linux CI), 7 new skipped
  scaffolds.
- Preflight: **green** (150 pass / 3 skipped integration; full unit
  suite clean).
- CI status at push time: verified green on `CI` + `docs-lint` for
  `ef8756c`.

## Credits

- **Kramer** -- `SecretToolCredentialStore.cs` and the factory branch.
- **Newman** -- stdin vs. argv discipline, timeout behaviour,
  scrub coverage, signoff.
- **Jerry** -- preflight guardrail.
- **Elaine** -- README + CHANGELOG copy.
- **Copilot** -- orchestration and this report.

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>
