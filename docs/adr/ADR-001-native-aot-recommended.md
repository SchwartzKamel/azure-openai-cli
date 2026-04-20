# ADR-001: Native AOT as the Recommended Publish Mode

- **Status**: Accepted — 2026-04-19
- **Deciders**: Core maintainers
- **Related**: `FR-006-unblock-native-aot-compilation.md`, `docs/espanso-ahk-integration.md`, CHANGELOG v1.8.0

## Context

The primary real-world use case for `azure-openai-cli` is text-injection
automation via [Espanso](https://espanso.org/) and
[AutoHotkey](https://www.autohotkey.com/). In that workflow, **every key
expansion spawns a fresh CLI process**, which means cold-start latency — not
throughput — dominates user-perceived UX. A 50 ms delay on every expansion is
noticeable; a 5 ms delay is not.

.NET 10 offers three publish modes, each with different startup profiles:

| Mode                   | Cold start (linux-x64, `make bench`) | Binary size      | Runtime required |
| ---------------------- | ------------------------------------ | ---------------- | ---------------- |
| Framework-dependent    | *not measured*                       | ~1 MB + shared   | Yes              |
| ReadyToRun (R2R)       | ~54 ms                               | ~80 MB           | Bundled          |
| **Native AOT**         | **~5.4 ms**                          | **~9 MB**        | No               |
| Docker (for reference) | ~400 ms                              | image-dependent  | Container host   |

Native AOT is roughly **10× faster than R2R and ~75× faster than Docker** for
cold start, while producing the smallest self-contained artifact. These
measurements were taken on WSL2 (linux-x64) via the repository `Makefile`
target `make bench`; they are directionally consistent across hosts but
absolute numbers vary with disk and CPU.

## Decision

**Native AOT is the recommended default publish mode for end-users.**

- `make publish` now aliases to `publish-aot`.
- `make publish-fast` is retained as the R2R fallback for developers who need
  faster iteration or who are on a host that cannot cross-compile AOT.
- Docker remains supported but is explicitly **not** the primary distribution
  channel for interactive/injection use cases.

## Consequences

### Positive

- **Sub-10 ms cold start** makes Espanso/AutoHotkey integration feel
  instantaneous.
- **Single-file distribution** (~9 MB) — users download one binary, no SDK or
  runtime install required.
- **Smaller attack surface**: no shared .NET runtime on the host, no JIT at
  runtime, trimmed unused assemblies.
- **Simpler user support**: "download, `chmod +x`, run" replaces multi-step
  runtime/SDK installation instructions.

### Negative

- **Slower builds**: AOT publish is ~30 s vs ~2 s for a framework-dependent
  build. Contributors should use `dotnet run` or `publish-fast` during
  development.
- **Host-only cross-compilation**: the AOT toolchain cannot currently
  cross-compile between OS families. Linux binaries must be built on Linux,
  macOS on macOS, Windows on Windows. This has downstream consequences for
  CI (see below).
- **Reflection-sensitive code paths** require explicit support via
  `System.Text.Json` source generators (`AppJsonContext`). New JSON
  serialization must be registered there or AOT publish will warn/fail.
- **Third-party trim/AOT warnings** are emitted by `Azure.AI.OpenAI` and
  `OpenAI`. These are documented as known, non-blocking, and tracked
  upstream. They do not affect runtime behavior for current code paths.
- **CI gap**: the release workflow (`.github/workflows/release.yml`)
  currently publishes R2R for the cross-platform matrix because AOT requires
  a runner per target OS. Moving the release matrix to per-OS runners for
  AOT artifacts is tracked as future work.

## Alternatives Considered

### Self-contained, framework-dependent (rejected)

Bundles the .NET runtime with the app. Produces 100+ MB artifacts and retains
the full managed startup cost — cold start is *worse* than R2R because the
runtime still JITs. No advantage over AOT for our use case.

### ReadyToRun (R2R) only (rejected as default)

R2R pre-compiles IL to native stubs but still requires the JIT and full
managed startup. At ~54 ms cold start it is a meaningful improvement over
pure framework-dependent but **still perceptibly laggy** in Espanso/AHK
injection. Retained as `publish-fast` for developer workflows.

### Alternative native compilers — Bflat, NativeAOT-LLVM (rejected)

Bflat and similar tools produce smaller binaries but have a substantially
smaller ecosystem, no first-party Microsoft support, and limited
compatibility with `Azure.AI.OpenAI`. The risk/reward does not justify
leaving the official toolchain.

## References

- [`FR-006-unblock-native-aot-compilation.md`](../proposals/FR-006-unblock-native-aot-compilation.md) — original feature
  proposal and measurement methodology.
- [`docs/espanso-ahk-integration.md`](../espanso-ahk-integration.md) — latency
  measurements and integration recipes.
- [`CHANGELOG.md`](../../CHANGELOG.md) v1.8.0 — promotion of AOT from
  experimental to default.
