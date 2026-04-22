# ADR-006 -- NVIDIA NIM / NVFP4 Provider Integration

- **Status**: Proposed -- 2026-04-23
- **Deciders**: Kramer (eng), Costanza (PM), Morty (FinOps), Newman (sec), Jerry (DevOps), Jackie (legal), Bania (perf)
- **Related**:
  - [FR-018 -- Local-Model Provider (llama.cpp/Ollama)](../proposals/FR-018-local-model-provider-llamacpp.md) -- **hard dependency**
  - [FR-019 -- gemma.cpp Direct Adapter](../proposals/FR-019-gemma-cpp-direct-adapter.md) -- sibling, ship paired
  - Prospective **FR-020 -- NVIDIA NIM Provider** -- gated by this ADR
  - [ADR-007](./ADR-007-third-party-http-provider-security.md) -- security guardrails spun out of this decision
  - [ADR-008](./ADR-008-gpu-provider-bench-policy.md) -- benchmarking policy for non-CI-gated providers
  - [ADR-006 appendix](./ADR-006-appendix-roundtable.md) -- verbatim 7-agent roundtable memos

## Context

The user asked whether `azure-openai-cli` should integrate `nvidia/Gemma-4-31B-IT-NVFP4`. **NVFP4 is NVIDIA's 4-bit floating-point format designed for Blackwell FP4 tensor cores.** Neither `llama.cpp` (FR-018) nor `gemma.cpp` (FR-019) supports it, so NVFP4 is unreachable from az-ai unless we add a dedicated path.

Hard constraints:

- az-ai is a 40 MB AOT Alpine binary. It does **not** ship GPU drivers, CUDA, Triton, or TensorRT. Anything that requires those must live behind a socket.
- FR-018 introduces a provider abstraction that speaks OpenAI-compatible HTTP with a configurable base URL and bearer token. Any new provider must live inside that abstraction -- we will not introduce a second runtime concept.

**Hardware reality (dev rig):** RTX PRO 3000 Blackwell (mobile), **12 GB VRAM**, Intel Core Ultra 7 265H, 31 GB host RAM, CUDA 12.8. Docker + NVIDIA Container Toolkit not yet installed.

**The 31B NVFP4 variant does not fit on 12 GB.** Weights alone are ~18-20 GB before KV cache. Models that *do* fit on the dev rig:

- `nvidia/Gemma-4-9B-IT-NVFP4` -- ~5 GB weights, comfortable KV headroom. **Primary demo target.**
- `nvidia/Gemma-4-2B-IT-NVFP4` -- ~1.2 GB, smoke-test target.

The 31B variant is retained in docs as the "if you have a B200 / dual-H200 rack, point az-ai at it" example -- the cost-model exemplar, not the demo target. This reshapes §6 of the original roundtable: the primary ICP is now the **Blackwell-on-laptop developer**, not the datacenter tenant.

## Decision

**Adopt NVIDIA NIM as the single integration path for NVFP4 workloads. Layer it on FR-018's provider abstraction. Ship no runtime artifacts of our own.**

Five commitments, in order of importance:

**1. NIM (OCI container, HTTP, OpenAI-compatible) is the only supported path.**
NIM exposes `/v1/chat/completions` with streaming SSE, `tools`, and `tool_choice` -- the dialect az-ai already speaks. From az-ai's perspective, NIM is "another `IChatProvider` with a different base URL and a bearer token." Zero new C#, zero new JSON source-gen contexts, one new provider preset plus docs. `trtllm-serve` is acknowledged as a user-side fallback (same wire format) but is **not** a first-party target.

**2. No bundling, ever.** az-ai must not ship, mirror, vendor, or re-host the NIM image, Gemma weights (original or NVFP4 re-quant), TRT-LLM engine plans, or any NVIDIA artifact in any release channel (GitHub Releases, Docker Hub, Homebrew, Scoop, installers). We document `docker pull` / `docker run`; we do not distribute. This is a legal line (Jackie: redistribution = "copyright infringement, breach of contract, tortious interference") and an architectural one (Jerry: "we do not bundle a 20 GB house inside a 40 MB toolshed").

**3. `nvidia/Gemma-4-9B-IT-NVFP4` is the reference demo model; 31B is a documented datacenter example.**
Every quickstart, compose example, and CI fixture defaults to the 9B tag. The 31B tag lives in a separate `examples/compose/nim-gemma-31b/` with a "requires ≥24 GB VRAM" banner. All published latency and throughput claims for 31B must include the hardware row; no 31B number may be quoted without a B200/H200-class reproducer.

**4. Security guardrails are non-negotiable.**
The six guardrails from Newman's memo (digest-pinned images, bearer-token auth, SSRF allow-list with metadata endpoints always blocked, TLS on non-loopback, first-run acknowledgment file, hardened container docs) are promoted to cross-cutting policy in **[ADR-007](./ADR-007-third-party-http-provider-security.md)**. FR-020 implements that policy; it does not re-litigate it.

**5. Perf claims for this provider are not CI-gated.**
GitHub Actions has no Blackwell silicon. Plumbing regressions (HTTP, auth, stream parse, retry) are caught by a mock-NIM benchmark at the usual 5% / 10% thresholds. Real-GPU numbers are manual / nightly self-hosted with a warning badge. The full policy is in **[ADR-008](./ADR-008-gpu-provider-bench-policy.md)**.

## Sequencing

- **Release N** -- FR-018 lands. Provider abstraction becomes real.
- **Release N+1** -- FR-019 (gemma.cpp) and FR-020 (NIM) ship **paired**, under Costanza's unified "Gemma, everywhere you have silicon -- from a MacBook (gemma.cpp) to a laptop Blackwell (NIM) to a datacenter rack (NIM)" narrative.

FR-020 is **deferred behind FR-018 landing.** No FR-020 work starts before FR-018 merges.

## Consequences

### Positive

- az-ai becomes reachable from NVFP4 workloads without taking on any GPU-runtime responsibility. Zero cold-start regression, zero AOT impact, zero new top-level CLI surface.
- The "Gemma everywhere" narrative becomes coherent across FR-018, FR-019, FR-020.
- Total FR-020 scope (after FR-018): ~half a day -- docs, preset, integration-test stub, smoke test. No new C#.

### Negative

- **NVAIE is a de facto commercial dependency for production users** (~$4,500/GPU/yr list). Free dev tier exists but carries restrictions. We disclose this plainly in `docs/providers/nvidia.md` -- the "just works locally" pitch is materially qualified.
- Perf claims for this provider will always be community-reported rather than CI-gated. We say so on the tin (see ADR-008).
- We inherit a documentation liability around `docker run --gpus all` hardening even though we do not run the container ourselves.
- Tool-calling on Gemma-4 NVFP4 is **unverified** until a live endpoint can be smoke-tested. `--agent` mode on this provider is gated on that smoke test.

## Alternatives Considered

- **TensorRT-LLM native linking.** Rejected: AOT-unfriendly, CUDA P/Invoke surface, attack surface, build-farm cost. No upside over HTTP. (Jerry, Kramer, Newman concur.)
- **Raw Triton + custom backend.** Rejected: "a week of YAML and protobuf for zero user-visible win" (Kramer). Three container runtimes too many (Jerry).
- **Bundle NIM or weights in an az-ai artifact.** Rejected: catastrophic license exposure (Jackie) and absurd artifact size (Jerry). Non-negotiable.
- **Build a HuggingFace gated-model download helper.** Rejected: makes az-ai an intermediary in the Gemma ToU acceptance flow. Clean hands.
- **Ship FR-020 before FR-018.** Rejected: "You don't pour the second floor before the first" (Costanza).
- **Morty's DEFER position** (hold FR-020 until a user shows ≥10M tok/mo premium + Blackwell + NVAIE willingness). Overridden in favor of Costanza's flagship-demo framing, justified by the 9B-on-laptop ICP -- the audience *does* exist on workstation Blackwell, not only in the datacenter.

## Open Questions (flag before FR-020 drafting)

1. **NVAIE dev-tier EULA** -- does the current free dev tier carry conditions (non-commercial, eval-only, expiry) that constrain what we can demo publicly? Needs a fresh read of the live EULA, not a 2024 summary.
2. **Docker + NVIDIA Container Toolkit install instructions** -- FR-018, FR-019, and FR-020 all want the same prereqs page. Recommendation: one shared `docs/local-model-prereqs.md`, linked thrice.
3. **Ack-file UX under non-interactive callers** (Espanso, AHK). Inherits whatever FR-018 lands on (`AZ_AI_ACCEPT_THIRD_PARTY_TERMS=1`, `--yes`, silent-deny with clear stderr if neither is set).
4. **Is Gemma 4 actually released?** As of 2026-04-23 the `nvidia/Gemma-4-*-IT-NVFP4` cards have not been verified live on NGC from this environment. If Gemma 4 has not shipped by drafting time, FR-020 targets `nvidia/Gemma-3-9B-IT-NVFP4` (or the currently-available family) with the model id treated as a parameter.

## Acceptance criteria (for FR-020 when it is drafted)

- ✅ FR-018 has merged and its provider abstraction is live.
- ✅ One new provider preset (`providers.nim`) is added; no new C# types and no new JSON source-generator contexts.
- ✅ Every guardrail in [ADR-007](./ADR-007-third-party-http-provider-security.md) has a corresponding `ToolHardeningTests` case landing in the same PR.
- ✅ A mock-NIM benchmark lands under `scripts/bench_mock_nim.py` and is wired into the standard 5% / 10% CI gate per [ADR-008](./ADR-008-gpu-provider-bench-policy.md).
- ✅ `docker-compose.yml` examples exist at `examples/compose/nim-gemma-9b/` (primary) and `examples/compose/nim-gemma-31b/` (aspirational), both with digest-pinned images.
- ✅ `THIRD_PARTY_NOTICES.md` and `SECURITY.md` updates land with FR-020, including the NVAIE commercial-dependency disclosure and the export-control boilerplate.
- ✅ `docs/providers/nvidia.md` exists and carries the ADR-008 "Performance characterization" banner.
- ✅ Zero cold-start regression vs the current AOT binary. Kenny Bania gates.
- ✅ No artefact in any release channel contains a NIM image, Gemma weights, or a TRT-LLM engine plan. Jackie verifies.

## References

- [ADR-007 -- Security guardrails for third-party HTTP inference providers](./ADR-007-third-party-http-provider-security.md)
- [ADR-008 -- Benchmarking policy for GPU / non-CI-gated providers](./ADR-008-gpu-provider-bench-policy.md)
- [ADR-006 appendix -- verbatim roundtable memos](./ADR-006-appendix-roundtable.md)
- [FR-018 -- Local-Model Provider (llama.cpp/Ollama)](../proposals/FR-018-local-model-provider-llamacpp.md)
- [FR-019 -- gemma.cpp Direct Adapter](../proposals/FR-019-gemma-cpp-direct-adapter.md)
