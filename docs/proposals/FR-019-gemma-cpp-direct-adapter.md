# FR-019 -- gemma.cpp Direct Adapter for az-ai

**Status:** Draft (Costanza, 2026-04-21)
**Related:** [FR-018](FR-018-local-model-provider-llamacpp.md) (provider abstraction, llama.cpp/Ollama), [FR-014](FR-014-local-preferences-and-multi-provider.md) (preferences schema), [FR-010](FR-010-model-aliases-and-smart-defaults.md) (model aliases)
**Owner:** Costanza (PM) · Kramer (impl) · Newman (security) · Jackie (licensing) · Morty (cost)
**Depends on:** FR-018's provider abstraction -- gemma.cpp is a *downstream adapter* that plugs into the provider interface that FR-018 establishes. This FR assumes FR-018 has shipped the `IModelProvider` contract, the `providers.*` preferences block, and the `--provider` CLI flag.

---

## 1. Problem Statement

FR-018 introduces local-model support via OpenAI-compatible HTTP adapters -- principally `llama.cpp` (via `llama-server`) and Ollama. Ollama already bundles and serves Gemma models out of the box, so asking *why we need a second path dedicated to one model family* is the honest opening question of this FR.

The case for a direct [google/gemma.cpp](https://github.com/google/gemma.cpp) adapter rests on four narrow-but-real user segments:

1. **Edge / small-footprint deployments.** gemma.cpp is a pure C++17 library (~few MB compiled, Highway SIMD, no Go runtime, no Python, no network stack). On a Raspberry Pi 5, a kiosk, or a locked-down corp laptop where installing Ollama's Go daemon is a non-starter, gemma.cpp's static binary is the only viable path.
2. **Tokenizer / quantization fidelity.** gemma.cpp is Google's own reference implementation. It ships the first-party `.sbs` (single-batch-stream) weights with Google's exact tokenizer and quantization. Users doing eval work, reproducing paper results, or benchmarking against Google's published numbers want a "pure Google stack," not a GGUF re-quantization.
3. **Gemma-only focus.** llama.cpp and Ollama are general-purpose and carry the engineering overhead that implies (hundreds of ops, dozens of arches, large test matrices). gemma.cpp does one thing well. For users who only ever intend to run Gemma, the smaller surface area is a feature.
4. **Privacy/offline with zero daemon.** gemma.cpp as a CLI has no listening socket, no background process, no REST port to firewall. That is a hard requirement in some security-sensitive environments.

**Skeptic's counter:** for ~90% of users, Ollama's Gemma bundle is simpler, faster to install, and uses the same FR-018 adapter. This FR should only land if we can validate demand in the four segments above. See §13 (Open Questions).

## 2. Competitive & Strategic Framing

- **Not a competitive gap.** No competitor (fabric, aichat, llm, Claude Code, Codex CLI, Gemini CLI) ships a direct gemma.cpp adapter either. Shipping this is not about catching up -- it is about *over-serving* a specific audience who values Google-native fidelity. That makes the feature optional, not urgent.
- **Brand alignment.** "az-ai speaks Azure OpenAI fluently *and* Google's own reference runtime, on the same binary, offline." That is a clean one-liner for Peterman/Sue Ellen if we decide to lean into it.
- **Morty's objection on record.** Two local-model adapters doubles the test matrix, the doc surface, and the bug-report inbox for a feature that is already a minority use case. We should be able to answer "why not just Ollama?" at merge time.

---

## 3. Approach Options

Three credible implementations. Each is analyzed end-to-end; recommendation at the end.

### Option A -- Subprocess + stdin/stdout (RECOMMENDED as MVP)

Spawn the user-installed `gemma` CLI binary as a child process, write the prompt on `stdin`, parse streaming `stdout` line-by-line, detect EOT/`<end_of_turn>` token, forward to the `--raw` sink.

**Pros**

- Works from AOT C# today via `System.Diagnostics.Process` -- zero new dependencies, zero native-interop risk, no AOT breakage.
- Portable across Linux/macOS/Windows wherever the user can install gemma.cpp.
- Clean process isolation: prompt injection cannot escape into the az-ai process.
- Easy to vendor-drop: the gemma.cpp build is entirely the user's responsibility; az-ai just shells out.
- Composes with FR-018's provider abstraction trivially -- gemma.cpp becomes `IModelProvider.GemmaCpp` that internally does not go over HTTP.

**Cons**

- No native streaming protocol. We are parsing free-form stdout, which means we must be defensive about stray log lines, ANSI colour codes (`--nocolors` flag), and tokenizer artefacts (pad tokens, `<start_of_turn>` / `<end_of_turn>`).
- Process-per-request overhead. gemma.cpp cold-loads weights from disk on every invocation; for a 2B model that is ~1-2 s before first token. Mitigation: a **long-running worker process** with a simple length-framed JSON protocol on stdin/stdout (see Phase C).
- No function calling (see §6) -- though this is a Gemma limitation, not an Option A limitation.

**Verdict:** Best risk/reward for MVP.

### Option B -- Thin Rust/Go/C++ HTTP shim wrapping gemma.cpp

Build (or adopt) a small daemon that links gemma.cpp as a library and exposes `POST /v1/chat/completions` in OpenAI-compatible form. gemma.cpp then looks identical to llama-server from az-ai's perspective, and the FR-018 HTTP provider handles it with zero new code in az-ai.

**Pros**

- Architectural symmetry with FR-018. gemma.cpp becomes "just another OpenAI-compat endpoint," which is the simplest story for users.
- Reuses all streaming / error-handling / retry logic from FR-018.
- A daemon can batch requests, reuse loaded weights, and serve multiple az-ai invocations cheaply.

**Cons**

- **Ships a second binary we must maintain.** That is a significant commitment -- CI builds, release artefacts, CVE response, platform matrix. Effectively a new mini-project.
- Licensing & supply-chain complexity: the shim is *our* code wrapping someone else's code, which Jackie will want reviewed; cross-compile matrix balloons.
- It is not obvious this is better than telling users to run Ollama, which already *is* such a shim and is maintained by someone else.
- Newman: a new daemon expands the attack surface more than a subprocess does.

**Verdict:** Defensible, but only after Option A ships and real demand for a daemon materializes. Do not build speculatively.

### Option C -- Link gemma.cpp as a native library via P/Invoke

Build `libgemma.so` / `libgemma.dylib` / `gemma.dll`, load it from C# via `DllImport`, call inference functions directly.

**Pros**

- Best latency -- no process boundary, no stdio parsing, direct token stream.
- Most control over generation parameters.

**Cons**

- **AOT-hostile.** P/Invoke to a non-standard native library makes the single-file AOT binary story (FR-006) significantly more fragile -- now we ship `az-ai` *and* `libgemma.*` and keep them version-locked.
- **Cross-platform build pain.** Every supported platform now needs a native build pipeline. gemma.cpp's build depends on Highway SIMD + CMake; that is not a casual dependency to take on.
- **Newman's attack surface.** Any buffer-bug in gemma.cpp becomes a buffer-bug in az-ai. A subprocess crash is a degraded UX; a linked-library crash is a CVE.
- Breaks the "az-ai is a pure C# AOT binary" promise that underpins our startup-time story.

**Verdict:** Reject for v1. Revisit only if a specific perf requirement emerges that Option A+worker-reuse demonstrably cannot meet.

### Recommendation

**Phase B → Option A (subprocess).** **Phase C → keep Option A, add long-running worker.** **Phase D → evaluate Option B only if user demand is evidenced.** **Option C is rejected.**

---

## 4. Model Lifecycle & Weight Discovery

gemma.cpp uses Google's `.sbs` (single-batch-stream) weight format, *not* GGUF. This is a hard asymmetry with FR-018:

- **Storage path.** Default to `~/.cache/az-ai/models/gemma/` (XDG-compliant; configurable via `providers.gemmaCpp.weightsDir` in the FR-014 preferences schema).
- **Discovery.** Add `az-ai --list-models --provider gemma-cpp` that scans the weights dir and reports `name`, `size`, `quantization`, and `sha256`. Listing other providers' models is out of scope here; if cross-provider discovery is needed, spin it out as its own FR.
- **Download.** az-ai does **not** download weights automatically. Users acquire weights from Kaggle or the Gemma release channel per Google's instructions, accept the Gemma Terms of Use themselves, and drop the `.sbs` files into the weights dir. We surface a clear `az-ai --gemma-setup` helper that prints the canonical instructions and the destination path -- it does not fetch anything.
- **Integrity.** When Google publishes SHA256 manifests for a given weight release, az-ai verifies on first use and caches the result. Mismatch = loud error, refuse to run.

## 5. Tool-Calling Story (the honest one)

Gemma (through Gemma 3, as of this FR) does **not** natively support OpenAI-style structured tool/function calling. Three options:

1. **Fail-fast (recommended for v1):** when `--agent` is used with `--provider gemma-cpp`, emit a clear error: *"Agent/tool-calling mode is not supported with the gemma-cpp provider. Gemma models do not implement OpenAI function calling natively. Use `--provider azure` or `--provider ollama` (with a tool-capable model) for agent mode."*
2. **Prompt-template shim:** instruct Gemma via the system prompt to emit `<tool_call name="..." args="..."/>` XML, then parse it. This is brittle, frequently hallucinates malformed XML, and silently downgrades reliability. **Not recommended for v1.**
3. **Wait for upstream.** If/when Gemma adds native tool calling, revisit. Track as an open question (§13).

## 6. Streaming

gemma.cpp streams to stdout by default. The adapter:

1. Spawns `gemma --weights <path> --tokenizer <path> --model <variant> --verbosity 0`.
2. Writes the fully-templated prompt (using Gemma's `<start_of_turn>user ... <end_of_turn><start_of_turn>model` chat template) on stdin, closes stdin.
3. Reads stdout as a UTF-8 stream, emits bytes as they arrive.
4. Detects `<end_of_turn>` / EOT token → signals completion.
5. On `--raw`: strips all control markers, emits only the model's content. On non-`--raw`: preserves formatting, still strips control tokens.
6. Captures stderr separately for diagnostics; never mixes it into the response.

Timeout, cancel-on-Ctrl-C, and partial-response handling follow the same rules as FR-018's HTTP provider.

## 7. Security (Newman)

Local-binary spawning is not free. Required controls:

- **Pinned binary path.** `providers.gemmaCpp.binary` preference is **required** -- no `$PATH` search by default. Default suggestion in docs: `/usr/local/bin/gemma`. A `providers.gemmaCpp.allowPathLookup: true` opt-in exists for power users who know what they are doing.
- **No shell interpretation.** Use `ProcessStartInfo.ArgumentList` (never `Arguments`) -- this is the existing house rule from `ShellExecTool` and applies verbatim. Prompt content flows on stdin, never via command-line arguments.
- **Resource limits.** On Linux, wrap with `prlimit` (or configure rlimit via P/Invoke to `setrlimit` -- subprocess only) for CPU time and address space. Document the Windows/macOS gap.
- **No fd leakage.** `ProcessStartInfo.RedirectStandardInput/Output/Error = true`; everything else closed. No environment variable pass-through except an explicit allowlist.
- **Kill on cancel.** Ctrl-C in az-ai sends SIGTERM, then SIGKILL after a grace period. Orphaned gemma processes are a documented bug class -- test for it (Puddy).
- **Weight-file path validation.** Weights path must resolve inside `providers.gemmaCpp.weightsDir`; reject traversal.

## 8. Licensing (Jackie)

Two separate licenses, **do not conflate**:

- **gemma.cpp source code:** Apache-2.0. Safe to depend on. If we ever vendor or redistribute (Option B or C) we must carry NOTICE/LICENSE. For Option A we only tell users to install it themselves.
- **Gemma model weights:** distributed under the **Gemma Terms of Use** (not OSI-approved; includes prohibited-use clauses). az-ai **must not** bundle, redistribute, or download weights. We **must** link to Google's canonical installation/acceptance flow, and we **should** surface a one-time CLI acknowledgment (`az-ai --provider gemma-cpp` on first use prints the ToU URL and requires `az-ai config set providers.gemmaCpp.termsAcknowledged true` to proceed). This mirrors how other CLIs handle Llama-ToU gating.

Jackie owns the final wording.

## 9. Cost & Telemetry (Morty + Frank)

- **Cost:** local inference is $0 API cost. The existing Morty cost-estimator (FR-015) should treat `provider=gemma-cpp` as cost-zero and not emit spend warnings.
- **Telemetry (opt-in only, per Frank's standing rule):** record `tokens_in`, `tokens_out`, `wall_time_ms`, `tokens_per_second`, `model_variant`, `first_token_ms`. Useful for Bania's benchmark corpus and for validating perf claims in §10.
- **Tokenizer drift.** Gemma's tokenizer is not GPT-4's. For cost reporting in a mixed-provider session (some turns Azure, some gemma-cpp), we either (a) count tokens per-provider with provider-specific tokenizers, or (b) approximate everything with the Azure tokenizer and label it. Recommend (a); log a warning once per session if the counts disagree by >20%. Flag as open question.

## 10. Performance Expectations

Indicative, single-CPU, cold-start subprocess, modern mid-range laptop:

| Model | Quantization | Approx. tok/s | First-token latency |
|---|---|---|---|
| Gemma 3 2B-it | int8 | ~15 tok/s | 1-2 s |
| Gemma 3 7B-it | int8 | ~3 tok/s | 3-6 s |
| Gemma 3 2B-it | int4 | ~25 tok/s | ~1 s |

These numbers are **far** below Azure cloud performance (50-200+ tok/s for comparable workloads). This adapter exists for **privacy, offline, and fidelity** -- never for speed. Docs must lead with that framing (Peterman/Elaine).

## 11. AOT Implications

- Option A (recommended): **zero AOT impact.** `System.Diagnostics.Process`, `System.Text.Json` with source-generated contexts, standard `StreamReader` loops. All already AOT-safe.
- Option B: **zero AOT impact** inside az-ai (the shim is a separate binary).
- Option C: **breaks AOT story.** Rejected.

New types introduced for the adapter (request/response/config records, stream event DTOs) must be registered in `AppJsonContext` per the house rules.

## 12. Phased Rollout

- **Phase A -- Blocked on FR-018.** Cannot start until FR-018 lands the `IModelProvider` abstraction and the `providers.*` preferences block.
- **Phase B -- Option A MVP.** Subprocess adapter, `--list-models`, Gemma ToU gate, docs for user-supplied binary, fail-fast on `--agent`. Ship behind `--provider gemma-cpp`; not the default.
- **Phase C -- Long-running worker.** Daemon-mode subprocess that keeps weights loaded; JSON line protocol on stdin/stdout. Eliminates cold-load overhead for interactive/chat mode (FR-002). Only build if Phase B adoption justifies it.
- **Phase D -- Evaluate Option B.** If users ask for a hosted `/v1/chat/completions` shim (they probably won't -- Ollama fills that niche), revisit the HTTP shim path.

## 13. Open Questions

1. **Is this worth building at all vs. pointing users to Ollama's Gemma bundle?** Ollama handles weights, updates, quantization choices, and already implements an OpenAI-compat endpoint. What concrete user signal justifies a second path? (We should have names of interested users before Phase B.)
2. **Who maintains the gemma.cpp dependency surface?** We are not forking or vendoring for Option A, but we are still coupled to its CLI's argument grammar and stdout format. If upstream renames a flag, our adapter breaks. Is this a tolerable maintenance tax for Kramer/Jerry?
3. **Gemma ToU acceptance UX in a non-interactive CLI.** Espanso/AHK users don't see prompts. How do we gate without breaking automation? One-time interactive acknowledgment stored in config, failing loud in non-interactive mode? What about CI environments?
4. **Tokenizer drift between Gemma and OpenAI-style counters.** Does Morty's cost reporter need first-class multi-tokenizer support, or is an approximation with a labelled warning enough for v1?
5. **Gemma 4 / future releases.** If Gemma 4 ships with native tool calling, §5 changes fundamentally and this FR's v2 story looks very different. Do we wait for Gemma 4 before investing?
6. **Does gemma.cpp's roadmap add a built-in server mode?** If upstream adds `gemma-server --openai-compat`, this FR collapses entirely into FR-018 and we should not build Option A at all. Worth asking upstream before committing engineering time.
7. **SBS format stability.** `.sbs` is not as stable/ubiquitous as GGUF. How often will users need to re-download weights because the format revved?
8. **Windows support priority.** gemma.cpp builds on Windows but is Linux-first upstream. Do we commit to Windows parity in Phase B, or make it best-effort?

## 14. Success Criteria (Measurable)

Phase B is "done" when all of the following hold on a mid-range laptop (8-core x86-64, 16 GB RAM, no GPU):

1. `az-ai --provider gemma-cpp --model gemma-3-2b-it "hello world"` returns a streamed response with **TTFT ≤ 5 s** (cold), **≤ 1 s** (warm via Phase C worker reuse, when shipped).
2. `--raw` output is clean: no control tokens, no stderr bleed, no ANSI codes, no trailing whitespace.
3. First invocation prompts for Gemma ToU acknowledgment; subsequent invocations do not.
4. `--agent` with `--provider gemma-cpp` fails fast with a helpful error message (Mickey-approved wording).
5. No AOT regressions: `make publish-aot` produces a binary within ±5% of pre-FR-019 size.
6. Integration tests in `tests/integration_tests.sh` cover: happy path, missing binary, missing weights, ToU not accepted, binary crash mid-stream, Ctrl-C cancellation. Puddy signs off.
7. Newman's security checklist (§7) green across the board.
8. Docs shipped: `docs/providers/gemma-cpp.md` with install instructions, ToU pointer, troubleshooting, and perf expectations. Elaine approves.

## 15. Out of Scope (Explicit)

This FR does **not** cover, and will reject scope-creep on:

- **Bundling or redistributing Gemma weights.** Ever. Full stop.
- **Building gemma.cpp from source in az-ai's CI.** Users bring their own binary.
- **GPU support.** gemma.cpp has experimental GPU paths; defer to a future FR once upstream stabilizes.
- **Function / tool calling for Gemma.** Defer until upstream Gemma models support it natively.
- **Embeddings API.** Separate future FR.
- **Streaming multi-turn conversations.** FR-002 (chat mode) is the owner of multi-turn; this FR only needs single-turn completion that FR-002 can compose.
- **A hosted HTTP shim (Option B).** Deferred to Phase D and gated on demand signal.
- **Other Google runtimes** (JAX, TFLite, MediaPipe). If those become interesting, new FRs -- not this one.

---

## Appendix: Why Not Just Ollama?

The honest short answer: for most users, Ollama *is* the right answer and we should say so in docs. This FR exists for the narrow audience where Ollama is the wrong answer -- and that audience must be validated with real user signal before Phase B starts. If the signal doesn't materialize in 90 days post-FR-018, this FR should be closed as *Won't Fix -- superseded by FR-018's Ollama path*.

That is the measurement that determines whether this proposal ships or gets archived. No sentimentality about "pure Google stack" if nobody actually wants it.
