# Abstract -- Living Off the Land: Per-OS Credential Storage in a Single-Binary CLI

**Title:** Living Off the Land: Per-OS Credential Storage in a Single-Binary CLI

**Speaker:** `<Speaker Name>`
**Track suggestion:** Security / Developer Tools / .NET
**Format:** 25-30 minute conference talk, single track, with live demo.
**Audience:** Intermediate. Developers who have shipped a CLI, fought
secret storage at least once, and know what "single binary" means.

## Long form (~150 words)

Every CLI eventually has to answer the same awkward question: where do
we put the API key? Dotfiles leak. Environment variables leak louder.
Bundling a crypto library bloats your single-file binary and reinvents
a wheel the operating system already ships. This talk shows the third
option: have the CLI shell out to whatever credential vault the host OS
already trusts -- `secret-tool` on Linux, `security` on macOS, the
Credential Manager on Windows -- and keep your own binary blissfully
key-free. We will walk the design of a "living off the land" credential
layer in a NativeAOT C# CLI: how the first-run wizard detects the
platform, picks a backend, falls back gracefully when the vault is
absent, and never writes a key to disk in plaintext. You will leave
with a decision tree, the exact shell-outs, the failure modes that
bite, and a working pattern you can lift into any single-binary tool.

**Word count: 156.**

## Short form (~50 words)

Where should a single-binary CLI store its API key? Not in a dotfile,
not in an env var, and not by shipping its own crypto. This talk shows
how to lean on the OS-native credential vault on Linux, macOS, and
Windows -- with a graceful fallback when the vault is missing.

**Word count: 50.**

## Pitch to the program committee (one paragraph)

Credential storage is a topic every CLI author rediscovers the hard
way, usually after a key shows up in a screenshot. The "living off the
land" pattern -- delegating to the OS keychain via a tiny shell-out --
is well known to red teamers but underused by tool authors. This talk
gives the audience a concrete, copy-pasteable design for doing it
right in a NativeAOT single binary, including the boring parts
(detection, fallback, error UX) that decide whether the pattern
actually ships.
