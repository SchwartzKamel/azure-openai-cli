# User Stories -- `azure-openai-cli`

> One paragraph per shipped feature, written so a non-engineer (or a
> first-time contributor) can tell you what we built and, more
> importantly, why anyone should care.

## Costanza notes

Engineers ship features. Users encounter products. The translation is
the job. Look, I read the S02 exec reports back-to-back -- six episodes,
gorgeous prose, every one of them written for the person who already
knows what a credential store is. That is half the audience. The other
half opens the README, sees "opportunistic libsecret provider behind
`ICredentialStore`," and closes the tab. We do not get that tab back.

So this document is the other voice. Same features, told as the user
would tell them. Each story names the role that benefits, the thing
the user actually does, and the outcome they get to brag about. If a
story still reads like a spec, Lloyd flags it and we know the feature
itself is murky -- that is a product smell, not a writing problem.

Was that wrong? Should I not have done that? No. This was right.

---

## US-001: First run does not punish you

**As a** first-time contributor,
**I want** `az-ai` to walk me through endpoint, key, and model on the
very first run,
**so that** I can send my first prompt within a minute of cloning the
repo, without hunting for a sample env file.

**Why it matters.** The pre-wizard experience was a scavenger hunt:
copy `.env.example`, edit it, source it, retry. Half of new
contributors never make it past that step. The wizard meets them where
they are -- an interactive terminal -- and gets out of the way the
moment it detects a script, a pipe, a CI job, or a container.

**Where it lives.** `azureopenai-cli/Program.cs` (`--init` /
`--configure` / `--login` flags and the auto-trigger when creds are
missing on a TTY); see also `docs/exec-reports/s02e01-the-wizard.md`.

---

## US-002: Your key is stored the way your OS already stores keys

**As a** security-conscious operator,
**I want** my Azure key kept in the credential vault that ships with
my operating system,
**so that** I do not add a new attack surface or a new dependency just
to use this CLI.

**Why it matters.** Every desktop OS already solves "where does a
local app keep a secret." Windows has DPAPI, macOS has Keychain, Linux
desktops have libsecret. We use what is already there ("Living Off The
Land") and fall back to a `0600` plaintext file only when no vault is
reachable -- and we say so out loud rather than pretending the
fallback is something it is not. Key rotation is the documented
compensating control on the plaintext path.

**Where it lives.** `azureopenai-cli/Security/` (the
`ICredentialStore` implementations: DPAPI, Keychain, libsecret,
plaintext); see `docs/exec-reports/s02e01-the-wizard.md` and
`docs/exec-reports/s02e04-the-locksmith.md`.

---

## US-003: Pasting a key with a stray space does not silently brick you

**As a** first-time contributor,
**I want** the wizard to refuse a key that is only whitespace,
**so that** I do not save a "valid-looking" empty key, hit a confusing
auth failure on my first prompt, and rage-quit the project.

**Why it matters.** The original guard only checked for empty strings.
A trailing newline from a clipboard paste, or one accidental space,
would pass the check, get persisted, and then fail with a generic
authentication error on the next call -- the exact moment the user has
the least context to debug it. The fix is one line per credential
store; the value is one fewer "az-ai is broken" issue per week.

**Where it lives.** `MacSecurityCredentialStore`,
`DpapiCredentialStore`, `PlaintextCredentialStore` in
`azureopenai-cli/Security/`; see
`docs/exec-reports/s02e02-the-cleanup.md`.

---

## US-004: When CI says a check is required, it actually is

**As a** release engineer,
**I want** the docs-lint job to label its checks honestly,
**so that** I know which violations will block a merge and which are
only advisory, without having to read the workflow YAML to find out.

**Why it matters.** The docs-lint summary table used to mark
`markdownlint` as "warn-only" while the underlying step was, in fact,
a hard-fail gate. Three pushes to `main` went red before anyone
noticed the label was lying. The episode rewrote the summary so the
label matches the behavior. Honest CI labels are a load-bearing piece
of contributor trust.

**Where it lives.** `.github/workflows/docs-lint.yml` (header comment +
Summary step); see `docs/exec-reports/s02e03-the-warn-only-lie.md`.

---

## US-005: Linux desktops get a real keyring, not a lecture

**As a** security-conscious operator on a Linux workstation,
**I want** my key in GNOME Keyring or KDE Wallet when those are
running,
**so that** I get desktop-class secret storage without installing
anything new.

**Why it matters.** Most Linux developers already run a session with
libsecret behind it. Detecting `/usr/bin/secret-tool` plus a DBus
session bus is enough to opportunistically upgrade from the
`0600`-plaintext baseline to the real keyring -- no extra packages, no
NuGet additions, no behavior change for headless boxes, containers, or
SSH sessions where the bus is not present. Those still get the honest
plaintext fallback.

**Where it lives.** `azureopenai-cli/Security/LibsecretCredentialStore.cs`
and the factory branch in `CredentialStoreFactory`; see
`docs/exec-reports/s02e04-the-locksmith.md`.

---

## US-006: A 5-second perf check you will actually run

**As a** first-time contributor,
**I want** `make bench-quick` to give me cold-start numbers in seconds,
not minutes,
**so that** I can sanity-check that my change did not tank performance
before I push.

**Why it matters.** The full bench (`make bench-full`) takes 5 to 10
minutes and nobody runs it pre-commit. `make bench` (N=100) is faster
but still feels like a wait. `bench-quick` (N=50, no warmup, stdout
only) finishes in roughly 5 seconds -- short enough to be part of the
dev loop, long enough to catch a 2x regression. It does not replace
the full bench; it lowers the floor.

**Where it lives.** `Makefile` (`bench-quick` target),
`scripts/bench.py`; see
`docs/exec-reports/s02e05-the-marathon.md` and
`docs/perf/bench-workflow.md`.

---

## US-007: Every push posts perf numbers, but does not block on them

**As a** release engineer,
**I want** CI to publish cold-start numbers to the run summary on
every push,
**so that** I can eyeball directional perf trends across PRs without
trusting noisy shared-runner numbers as a hard gate.

**Why it matters.** Shared GitHub runners jitter by roughly 30 percent
between runs. Treating that as a regression gate would block honest
PRs and erode trust in CI. Treating it as a directional smoke -- a
table in the step summary, `continue-on-error: true`, a disclaimer
that says "pinned-rig numbers are authoritative" -- gets the value
(visibility) without the cost (false reds).

**Where it lives.** `.github/workflows/ci.yml` (`bench-canary` job);
see `docs/exec-reports/s02e05-the-marathon.md`.

---

## US-008: Color obeys NO_COLOR and FORCE_COLOR

**As an** accessibility user,
**I want** `NO_COLOR=1` to suppress every color in the v1 binary, and
`FORCE_COLOR=1` to keep color when I am piping output into a tool that
re-renders it,
**so that** my screen reader, my colorblind-safe terminal theme, and
my piped workflows all get the output they expect.

**Why it matters.** The v2 binary already honored the seven-rule color
precedence. The v1 binary was riding on TTY auto-detection alone, with
no explicit `NO_COLOR` audit. The new `AnsiPolicy` helper is the one
chokepoint any future color must pass through, and its rules match v2
exactly so users get one mental model across both binaries.

**Where it lives.** `azureopenai-cli/ConsoleIO/AnsiPolicy.cs`;
contract documented in `docs/accessibility.md`; see
`docs/exec-reports/s02e06-the-screen-reader.md`.

---

## US-009: The masked-key prompt warns your screen reader first

**As an** accessibility user,
**I want** the wizard to tell me, in one sentence, that my key will be
masked as I type,
**so that** my text-to-speech engine does not announce "BULLET BULLET
BULLET" forty times in a row and leak my key length while it is at it.

**Why it matters.** A masked-input prompt that emits one bullet glyph
per keystroke is hostile to a screen reader by default. A single
announcement before the prompt -- "Your key will be masked as you
type. Press Enter when done." -- makes the experience humane and is
also a small side-channel win, since the announcement replaces a
glyph-stream that telegraphed key length.

**Where it lives.** `azureopenai-cli/Program.cs` wizard prompt path
plus the v1 section of `docs/accessibility.md`; see
`docs/exec-reports/s02e06-the-screen-reader.md`.

---

## US-010: One command, one prompt, one answer

**As a** terminal power user,
**I want** to run `az-ai "summarize this paragraph"` and get a clean
answer on stdout,
**so that** I can pipe AI into any shell workflow without writing
glue code or learning a new client library.

**Why it matters.** This is the load-bearing feature. Everything else
in the repo exists to make this one invocation fast, safe, scriptable,
and humane. A sub-15 ms cold start on a single AOT binary means it
feels synchronous inside Espanso and AHK -- the moment a tool feels
synchronous, people start using it like a calculator.

**Where it lives.** `azureopenai-cli/Program.cs` (the default
single-shot path); see `README.md` "First run" and the v2.0.6 perf
baseline in `docs/perf/`.

**Lloyd flags:** the relationship between the two binaries (v1 in
`azureopenai-cli/`, v2 elsewhere) is assumed knowledge here. A new
contributor reading this story will not know which binary they get
when they type `az-ai`, or why there are two.

---

## US-011: Tell me what you think my settings are

**As a** first-time contributor,
**I want** `az-ai --config show` to print the endpoint, model, and
masked key that the CLI is actually using,
**so that** I can debug "why is it hitting the wrong resource" without
opening the JSON file by hand.

**Why it matters.** Configuration drifts. People set env vars in one
shell, edit the JSON in another, and lose track of which one wins.
`--config show` is the canonical answer to "what does the tool
believe right now," and it masks the key so the output is safe to
paste into an issue.

**Where it lives.** `azureopenai-cli/Program.cs` (`--config show`
handler) and `azureopenai-cli/UserConfig.cs`.

**Lloyd flags:** the precedence rules ("env vars beat stored config")
are documented in the README but not surfaced in `--config show`
output -- a new user reading the printout cannot tell which value
came from where.

---

## US-012: Raw mode for text expanders

**As a** terminal power user,
**I want** `--raw` to suppress every spinner, prefix, and trailing
newline,
**so that** Espanso and AHK can paste the model output verbatim into
whatever app I am typing in.

**Why it matters.** Text-expander integrations are the sharpest use
case for this CLI. They paste exactly what the tool prints. A spinner
character, an `[INFO]` prefix, or a stray newline becomes a visible
artifact in the user's email, chat, or code editor. `--raw` is the
contract that says: nothing on stderr, nothing decorative on stdout,
just the model output.

**Where it lives.** `azureopenai-cli/Program.cs` (the `isRaw` guard
threaded through every output path); contract enforced by integration
tests in `tests/integration_tests.sh`.

---

## If you are a ..., read these stories first

| Role | Start here |
|------|------------|
| first-time contributor | US-001, US-003, US-006, US-010, US-011 |
| terminal power user | US-010, US-012, US-006 |
| security-conscious operator | US-002, US-005, US-003, US-011 |
| accessibility user | US-008, US-009, US-001 |
| release engineer | US-004, US-007, US-006 |

---

*Maintained by the product desk (Costanza). Refinement passes by
Lloyd Braun (gap-flagging) and Elaine Benes (structural consistency).
New features should land here as part of the same PR that ships the
code -- if you cannot write the user story, the feature is not done.*
