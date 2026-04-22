# ADR-006 Appendix -- Roundtable Memos (verbatim)

> **This document is not itself an ADR.** It preserves the seven verbatim
> agent memos that were collated into [ADR-006](./ADR-006-nvfp4-nim-integration.md),
> [ADR-007](./ADR-007-third-party-http-provider-security.md), and
> [ADR-008](./ADR-008-gpu-provider-bench-policy.md) during the 2026-04-23
> synthesis pass. The ADRs above are the decisions of record; the memos below
> are preserved for historical traceability only.

Original sources: `~/.copilot/session-state/<session>/files/roundtable/<agent>.md`.

## A.1 -- Kramer (Engineering)

> # Kramer -- `nvidia/Gemma-4-31B-IT-NVFP4` on az-ai
>
> *Giddyup.* I looked at the model, I looked at the runtimes, I looked at FR-018, and
> I'm telling you -- this is a **ninety-minute integration** if we pick the right door.
> Let's pick the right door.
>
> ## 1. The three paths
>
> **A. NVIDIA NIM (containerized microservice).** ✅ **This is the one.**
> NIM ships the model preloaded into a TRT-LLM engine, wraps it in an
> OpenAI-compatible HTTP server on `:8000/v1/chat/completions`, handles tokenizer,
> KV cache, batching, the works. `docker run` with an NGC API key and an
> `--gpus all` flag and you're serving. It speaks our dialect natively --
> `messages`, `stream: true`, `tools`, `tool_choice`. It's basically a localhost
> Azure OpenAI endpoint with a leather jacket.
>
> **B. TensorRT-LLM `trtllm-serve`.** 🟡 Works, but you own the zoo.
> You `trtllm-build` the engine from the NVFP4 checkpoint yourself (NVFP4 needs
> TRT-LLM ≥ 0.13 and Blackwell/Hopper w/ FP4 support -- check the GPU first!),
> then launch the OpenAI-compat server. Same wire protocol as NIM. More knobs,
> more ways to cut yourself. Good fallback if NIM licensing is a problem.
>
> **C. Raw `trtllm-build` + Triton + custom backend.** 🚫 Nope. Not our job.
> That's a week of YAML and protobuf for zero user-visible win. *These
> protobufs are making me thirsty.* Skip it.
>
> **Recommendation:** **NIM first, `trtllm-serve` as documented fallback.** Both
> terminate at the same OpenAI-compat endpoint, so the CLI doesn't care which
> one's behind the curtain.
>
> ## 2. Does it slot into FR-018?
>
> **Yes -- cleanly.** FR-018's provider abstraction is "OpenAI-compatible HTTP +
> configurable base URL + bearer token." NIM is *exactly* that shape. We don't
> need a new provider -- we need a **config recipe**.
>
> Gap check: NIM's `/v1/models` listing returns the NGC model id, so `--model`
> must match exactly. One-line doc note. No code.
>
> ## 3. Tool calling
>
> Gemma 4 IT **does** have a function-calling template upstream, and NIM exposes
> `tools` / `tool_choice` on models that support it -- **but coverage is per-model
> and the 31B NVFP4 card doesn't explicitly advertise it yet.** Until I can
> `curl` a live endpoint and see a `tool_calls` array come back, I'm calling
> `--agent` mode **gated on a smoke test**.
>
> ## 4. Streaming
>
> SSE, `data: {...}\n\n`, `data: [DONE]`. Identical to Azure OpenAI's stream
> shape. Our existing `--raw` sink drops straight in. Zero changes. *That's
> gold, Jerry. Gold.*
>
> ## 5. Effort (assuming FR-018 has landed)
>
> **T-shirt: XS -- maybe S.** Half a day, tops -- docs page, sample preset,
> integration test stub, smoke test, help copy. No new C#. No new HTTP client.
>
> ## 6. Deal-breakers
>
> 1. GPU requirement (Blackwell or late Hopper w/ FP4 path).
> 2. NGC API key + EULA -- Jackie eyeballs.
> 3. 31B VRAM: 16-18 GB weights + KV cache. 24 GB minimum, 48 GB comfortable.
> 4. Tool calling unverified on 31B NVFP4.
>
> None are showstoppers. Footnotes in the docs.
>
> ## Verdict
> **Build it.** NIM path, config-only change on top of FR-018. *Giddyup.*

## A.2 -- Morty Seinfeld (FinOps)

> # Morty's Cost Memo -- `nvidia/Gemma-4-31B-IT-NVFP4`
>
> You paid HOW much for a pair of sneakers?! Here's the math. With receipts.
>
> ## 1. TCO -- Four Cases
>
> Assumptions: 31B @ NVFP4 ≈ ~16 GB weights + KV, fits on 32 GB with room for
> context. Single-user throughput: ~60-100 tok/s on a 5090; ~300-500 tok/s on
> B200 (vendor-ish -- NIM hasn't published official Gemma-4 numbers yet).
>
> **Case A -- Owned RTX 5090:** CapEx $2,000 ÷ 36 mo = **$55.56/mo**. Load power
> $0.0675/hr. Idle 24/7 = $8.64/mo. Fixed floor **~$65/mo**. Marginal
> **~$0.065/M tok**.
>
> **Case B -- Cloud B200 rental:** ~$4.50/hr blended, ~$3.13/M tok at 100% util,
> $6-15/M tok effective with bursty traffic. Worse than Azure. Moving on.
>
> **Case C -- Azure `gpt-4o-mini`:** $0.15/M in, $0.60/M out. Blended ~$0.40/M.
> Can't beat with a stick.
>
> **Case D -- Azure `gpt-4.1` / `gpt-4o` (honest comparison):** $2/$8 → ~$5/M
> blended. This is where Gemma-4-31B-IT actually sits on quality.
>
> ## 2. Break-Even
> Fixed $65/mo, marginal $0.07/M, Azure 4.1 blended $5/M:
> - vs gpt-4.1: **13.2M tok/mo** (~440k/day)
> - vs gpt-4o-mini: **197M tok/mo** (~6.5M/day) -- LOL
>
> ## 3. Hidden Costs
> - **NVAIE: ~$4,500/GPU/yr list** for prod; free dev tier. Commercial alone
>   blows up Case A.
> - HF gated-model agreement -- onboarding tax.
> - TRT-LLM engine build: 20-60 min one-time.
> - Ops time: ~2 hr/mo. At $100/hr loaded = $200/mo, exceeds amortized HW alone.
> - Idle GPU waste: $8/mo blinking at you.
> - Blackwell-only native NVFP4. Ada/Hopper eats 2-4× perf penalty.
>
> ## 4. Who Is This For?
> - ✅ Power user with 5090/B200 already: marginal cost = electricity.
> - 🟡 Startup ≥15M tok/mo premium-tier: run the spreadsheet.
> - ❌ Casual <1M tok/mo: 400 months of gpt-4.1 for the price of the card.
> - ❌ Anyone pre-Blackwell.
>
> ## 5. Telemetry Schema
> Log api_cost_usd AND amortized_cost_usd per-call; monthly rollup surfaces
> local_effective_per_mtok side-by-side with Azure. No hiding CapEx in couch
> cushions.
>
> ## 6. Recommendation
> **DEFER.** Provider abstraction (FR-018) is all that needs to exist today.
> No dedicated NIM integration until a real user shows Blackwell + >10M tok/mo
> premium + NVAIE willingness. Document the pattern in
> `docs/cost-optimization.md` and move on.
>
> Top cost risk: somebody reads "local = free" and forgets NVAIE + ops + idle.
> Local is NOT free. Local is "prepaid at retail, non-refundable, batteries not
> included." -- Morty

## A.3 -- Newman (Security)

> # Hello. NIM.
>
> ## 1. Supply chain
> NIM pulls from `nvcr.io`. Unpinned tags are a silent-rollout vector.
> **Mitigation:** pin by digest `nvcr.io/nim/...@sha256:…`, ingest NGC SBOM,
> Trivy on every provider-CI run, HIGH gate blocking. Attack surface: HTTP
> server + model runtime + Triton + Python -- lot of sockets for "just a model."
>
> ## 2. Endpoint auth
> NIM's default is localhost HTTP with no API key. **Mitigation:** az-ai
> generates a random bearer token on first run, starts NIM with `NIM_API_KEY`
> set, sends `Authorization: Bearer …`, stores token in OS keychain -- never in
> config, never echoed, never logged. Auth header in an exception message = P1.
>
> ## 3. SSRF -- allow-list, not deny-list
> Default allow: `127.0.0.1/32`, `::1/128`, resolved `localhost`. `--allow-lan`
> opt-in permits RFC1918 with a yellow banner. Always blocked (even under
> `--allow-lan`): `169.254.169.254`, `fd00:ec2::254`, `100.100.100.200`,
> `metadata.google.internal`, `metadata.azure.com`, full `169.254/16`.
> Post-redirect IP re-validation. DNS-rebind pinning.
>
> ## 4. User-supplied endpoint trust
> Non-loopback endpoints → interactive confirmation on first use, cached
> per-host in a trust file. Structured logs record host+port+auth-scheme only --
> never prompt, never response, never bearer. TLS required for non-loopback;
> `--insecure-remote` loud.
>
> ## 5. Weights provenance
> Trust chain: Google → NVIDIA → us. Pin HF revision SHA (not branch), verify
> SHA256 of every shard, record in `MODEL_PROVENANCE.md`. Trust digests, not
> "latest," not "main."
>
> ## 6. Gemma ToU
> First `--provider nim --model gemma-*`: prompt, record attestation (user,
> timestamp, model id, ToU URL hash), refuse without it. No silent accept.
>
> ## 7. Container escape
> We don't *run* the container; we document it. Hardened `docker run` recipe:
> `--user` non-root, `--read-only` rootfs, `--cap-drop=ALL`,
> `--security-opt=no-new-privileges`, `--network` dedicated bridge bound to
> 127.0.0.1, never `--privileged`.
>
> ## 8. ToolHardeningTests additions
> New tool surface, new tests. Allow-list rejection, metadata block under
> `--allow-lan`, post-redirect rejection, auth-header redaction, remote-endpoint
> prompt non-logging, plain-HTTP refusal, weight-SHA verification, ToU
> attestation requirement.
>
> ## Non-negotiables
> 1. Digest-pinned NIM + Trivy gate HIGH blocking.
> 2. Bearer auth, keychain, redacted.
> 3. SSRF allow-list; metadata blocked even under `--allow-lan`.
> 4. SHA256 weight verification; failure fatal.
> 5. Gemma ToU attestation before first inference.
> 6. Non-localhost requires explicit logged confirmation; prompts never logged.
> 7. Every test in §8 lands with the PR, or the PR doesn't land.
>
> The postman always rings twice. The attacker only needs to ring *once*.
> -- Newman

## A.4 -- Jerry (DevOps)

> # Jerry -- DevOps memo: NVFP4 / NIM / TensorRT-LLM
>
> ## 1. CI impact -- can we test this in GHA?
> No. GHA has no GPU. Options: (a) don't test in CI, smoke-test locally;
> (b) self-hosted GPU runner -- who pays? who's on call? defer;
> (c) mock the NIM HTTP endpoint in tests -- **this is the actual answer.**
> Recommendation: (c) in CI + (a) for release smoke.
>
> ## 2. AOT impact
> NIM path is `System.Net.Http` all the way down. OpenAI-shaped JSON over
> HTTPS. **Zero AOT impact.** FR-018's abstraction holds. TRT-LLM direct is a
> nightmare to trim -- **massive AOT impact**, don't go there.
>
> **Verdict: NIM yes, TRT-LLM no.**
>
> ## 3. Docker story -- do we bundle?
> **No.** 20+ GB NIM container vs 40 MB Alpine static binary. We do not bundle
> a house inside a toolshed. Ship a compose example under
> `examples/compose/nim-gemma/docker-compose.yml` and done.
>
> ## 4. Base image compatibility
> Alpine/musl vs glibc -- **non-issue**. We're making HTTPS calls to a separate
> container. musl talks TCP.
>
> ## 5. Release plumbing
> No new artifact. No "az-ai-nim" SKU. Docs page + compose example + CHANGELOG
> line. Mr. Lippman owns the narrative, I own the compose file and the pin.
>
> ## 6. Dependency cadence
> **Pin.** `nvcr.io/nim/...:1.2.0`, not `:latest`. Monthly sweep -- Dependabot
> doesn't cover NVCR. Digest-pin would be ideal; tag-pin is the pragmatic floor.
>
> ## 7. Developer experience
> A contributor working on the NIM adapter needs nothing installed. No CUDA, no
> driver, no 31B weights. Mock the endpoint, write the adapter, `make
> preflight`, ship. GPU is someone else's problem.
>
> **Bottom line:** NIM over HTTP, pinned version, compose example, mocked in
> CI. TensorRT-LLM is a trap. *Shrug.*

## A.5 -- Costanza (PM)

> # Costanza -- PM Memo: FR-020
>
> ## 1. Roadmap thesis
> az-ai is a fast Azure OpenAI CLI with a provider abstraction that grows
> *outward from Azure*, not sideways into multi-cloud soup. NIM passes. NVIDIA
> is an Azure AI Foundry first-class partner; a NIM endpoint on an Azure
> NC-H100 / ND-GB200 VM is Azure-adjacent. That's the abstraction doing its
> job. We do NOT market "bare-metal box in Hoboken" even if the abstraction
> supports it.
>
> ## 2. User segmentation
> Narrow: Azure NC/ND Blackwell customers, on-prem ML platform teams piloting
> NIM on DGX/HGX Blackwell, a handful of researchers with RTX 50-series. A
> **power-user flagship feature**, not a mainline driver. Flagships sell the
> mainline.
>
> ## 3. Latency -- THIS is the angle
> Our brand is sub-15 ms cold start (10.7 ms p50 on v2.0.6 — see docs/perf/v2.0.5-baseline.md). Local NVFP4 on Blackwell can hit sub-100 ms
> TTFT for 31B. Combined: **sub-120 ms end-to-end first-token on the user's own
> iron.** Not a feature -- a different product. Chat loop feels like autocomplete,
> not a transaction.
>
> | Mode                   | TTFT target | TTFT measured |
> |------------------------|-------------|---------------|
> | Azure OpenAI (gpt-4o)  | < 800ms     | ~600-900ms    |
> | NIM local (NVFP4)      | **< 150ms** | TBD (pilot)   |
> | gemma.cpp (FR-019)     | < 300ms     | TBD           |
> | llama.cpp (FR-018)     | < 400ms     | TBD           |
>
> ## 4. Sequencing
> - Before FR-018? No. Don't pour the second floor before the first.
> - Before FR-019? No. FR-019 proves "Gemma on iron" on commodity HW.
> - **Paired with FR-019** in a single release: yes. *"Gemma, everywhere you
>   have silicon -- from a MacBook to a Blackwell rack."*
>
> Target: FR-018 release N. FR-019 + FR-020 together release N+1.
>
> ## 5. Success criteria
> - Latency: p50 TTFT ≤ 150 ms on reference Blackwell SKU; p95 ≤ 250 ms.
> - ≤ 1 config block (`providers.nim`), ≤ 1 CLI flag (`--provider nim`). No
>   new top-level command. Wilhelm audits.
> - **Zero cold-start regression.** Lazy-loaded. Kenny Bania gates.
> - Adoption: ≥ 25 distinct opt-in-telemetry users within 60 days.
> - One quickstart, one ADR, Elaine + Wilhelm sign off.
> - Newman signs off on credential handling (bearer, not disk-cached).
>
> ## 6. The pitch
> This is a prestige FR. Revenue is the NVIDIA + MSFT joint keynote slide, the
> Foundry docs page, the GTC demo booth. Pitch to Pitt (the fork bullet):
>
> > *"One release. Two Gemma stories. Sub-150ms first-token on the user's own
> > iron. Zero cold-start regression. Azure-adjacent, not multi-cloud drift.
> > NVIDIA co-marketing upside. Total scope: one provider adapter, reusing the
> > FR-018 abstraction."*
>
> -- Costanza

## A.6 -- Jackie Chiles (Legal)

> # Jackie Chiles -- Legal Memo: NIM + Gemma-4-31B-IT-NVFP4
>
> A four-layer licensing lasagna. Let me itemize. Itemize!
>
> ## 1. License matrix -- HTTP boundary is our friend
> - az-ai: MIT. Permissive, no copyleft contagion.
> - NIM: NVIDIA proprietary, governed by NVIDIA Software License + NVAIE for
>   prod. Redistribution forbidden.
> - TensorRT-LLM: Apache-2.0. Inbound fine.
> - Gemma weights: **Gemma Terms of Use** (Google). Not OSI-approved. AUP:
>   no weapons, no CSAM, no unlawful surveillance.
> - NVIDIA NVFP4 re-quant: derivative of Gemma, distributed under Gemma Terms
>   + NVIDIA attribution. No separate weight EULA overlay -- but the NIM
>   **container** has its own EULA. Don't conflate.
>
> **Holding:** az-ai (MIT) invoking NIM over HTTP is **not** a derivative
> work. The HTTP boundary is a process + network boundary -- well-established
> case law, well-established FSF guidance. Clean. *Confirmed.*
>
> ## 2. Redistribution -- non-negotiable
> az-ai **MUST NOT** bundle, mirror, vendor, re-host, or ship: the NIM image,
> Gemma weights (original or NVFP4), NIM binaries, TRT-LLM engine plans, or
> any NVIDIA artifact in release tarballs / Docker / Homebrew / installers.
> **MAY** document pull commands, endpoint URLs, config. Pointing ≠
> distribution. Violate = copyright infringement + breach + tortious
> interference.
>
> ## 3. Gemma ToU UX
> az-ai does not distribute Gemma; not contractually on the hook. But
> prudence: one-time interactive notice on first `nvidia` provider use, link
> to Gemma ToU + NVIDIA terms, `y` to proceed, persist hash + timestamp in
> `~/.config/az-ai/providers/nvidia.ack.json`. Support `--yes` and
> `AZ_AI_ACCEPT_THIRD_PARTY_TERMS=1`. Phrase it as a **notice** ("By
> proceeding you acknowledge…"), never "You agree to…" -- we are not their
> agent.
>
> ## 4. NVAIE -- de facto commercial dependency
> Production NIM requires NVAIE (paid). Dev/eval has a free tier with
> restrictions. Required disclosure in `docs/providers/nvidia.md`:
>
> > "Production use of NVIDIA NIM requires a NVIDIA AI Enterprise subscription
> > or equivalent license from NVIDIA. az-ai does not provide, resell, or
> > include this license."
>
> ## 5. Attribution -- NOTICE updates
> `THIRD_PARTY_NOTICES.md` entries for NVIDIA (NIM, TRT-LLM Apache-2.0) and
> Google (Gemma, ToU link). Trademark line. No TRT-LLM NOTICE passthrough
> unless we bundle it. We don't.
>
> ## 6. Export control
> Required boilerplate in `docs/providers/nvidia.md` + `SECURITY.md`: "Use of
> NVIDIA GPUs and Gemma weights may be subject to EAR and the Gemma AUP.
> Users are responsible for their own compliance. az-ai makes no
> representation…"
>
> ## 7. HF gated-model download helper -- DO NOT BUILD
> Automating `hf auth` + gated acceptance makes us an intermediary in the
> ToU flow. Users pull weights themselves. az-ai takes an endpoint URL and
> an API key. Period. *Clean hands.*
>
> ## 8. Risk ranking
> 1. Redistribution violation (bundling) -- catastrophic.
> 2. Gated-model download helper scope creep.
> 3. Gemma AUP misuse by downstream users -- reputational.
> 4. NVAIE hidden-cost surprise -- user-trust risk.
> 5. Export control exposure -- low for az-ai, user-side risk.
> 6. NOTICE omissions -- low legal, high optics.
> 7. HTTP-boundary contamination theory -- effectively zero.
>
> **Bottom line:** ship the provider, don't ship the weights, don't ship the
> container, notice in the terminal, disclaimers in the docs, do NOT write a
> download helper. -- Chiles

## A.7 -- Kenny Bania (Perf)

> # Bania -- Bench Angle on NIM + Gemma-4-31B-NVFP4
>
> ## 1. Benchable in CI? Honest answer.
> **No. Not the GPU part.** GHA has no Blackwell, no NVFP4, no NIM, no CUDA.
> Any "perf gate" we claim in GHA for *this provider* is theater. This
> provider gets a **best-effort perf badge**, not a merge-blocking gate.
>
> ## 2. Catching regressions without GPU in CI -- three tiers
> 1. Nightly self-hosted Blackwell runner (opt-in `gpu-nvfp4` label), if/when
>    org hardware exists. Until then: stubbed, clearly labeled.
> 2. Manual run logs versioned under `benchmarks/manual/<date>-<host>.json` --
>    sample size, variance, hardware mandatory. Means are for amateurs.
> 3. Warning badge in `docs/providers.md`: *"Perf: manual / community-reported.
>    Not CI-gated."*
>
> ## 3. Metrics (if we could bench)
> TTFT (cold, warm), steady-state throughput, TTFT-under-load at
> 1/4/16/64 concurrent, queue depth tolerance, container cold start, model
> load time, VRAM residency idle+peak, wall-clock vs az-ai overhead. All with
> p50/p95/p99, warm-up, variance published.
>
> ## 4. Baseline estimates (not my bench -- published numbers)
>
> | Metric     | Gemma-4-31B-NVFP4 (B200) | Azure GPT-4o-mini |
> |------------|--------------------------|-------------------|
> | TTFT p50   | ~40-80 ms (LAN)          | ~250-500 ms (RTT) |
> | Throughput | ~120-180 tok/s           | ~80-120 tok/s     |
> | Cold start | Minutes                  | Zero              |
>
> Faster TTFT? Yes. Fast enough to matter? Interactive chat: maybe. Batch:
> gold. One-shot CLI on a cold box: **disaster.**
>
> ## 5. Mock-NIM bench for CI -- this we CAN gate
> `scripts/bench_mock_nim.py` -- local HTTP server mimics NIM OpenAI-compat
> endpoint, fixed latency, scripted stream. Measure az-ai overhead: request
> build, auth, stream parse, callback dispatch. Gate at ≥5% flag / ≥10% block,
> same rules as everyone else.
>
> ## 6. "Gold" scenarios
> Long-context bulk summarization. Privacy / air-gapped. Offline / flaky
> networks. Sustained batch jobs.
>
> ## 7. "Not gold" scenarios
> One-shot CLI on cold box. Low-volume interactive. Multi-model tool-calling
> flows (Azure ecosystem more mature). Anyone pre-Blackwell.
>
> ## Bottom line
> Real-GPU perf: manual + nightly self-hosted, warning badge, no merge gate.
> Plumbing perf: mock-NIM CI-gated, 5%/10% rules. Every published number ships
> with sample size, variance, hardware. **It's gold when it's measured.
> Otherwise it's a rumor.** -- Bania

---

*End of appendix.*
