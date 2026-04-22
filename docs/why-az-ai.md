# Why az-ai?

**A one-page pitch for the only Azure-native, AOT-fast, text-expander-grade AI CLI.**

---

In April 2026, the terminal-AI category has matured into two tiers: **vendor-flagship coding agents** (Claude Code, Codex CLI, Gemini CLI, GitHub Copilot CLI) that bundle a first-party model at $10-$100/seat/month, and a **long tail of Python/Node CLIs** (sgpt, llm, chatblade-archived) that pay an interpreter tax of 300-1,000 ms on every invocation.

**az-ai sits in neither camp -- and that is the point.**

## Three differentiators. All defensible. All measurable.

### 1. 10.7 ms p50 cold start. AOT-compiled. Zero runtime.

.NET 10 Native AOT produces a **~13 MiB single binary** with a measured **10.73 ms p50 cold start** on linux-x64 (`--help`, N=50, v2.0.6 on the `malachor` laptop reference rig -- see [`docs/perf/v2.0.5-baseline.md`](./perf/v2.0.5-baseline.md)). The nearest competitors -- Rust tools like `aichat`, Go tools like `crush`/`fabric` -- clock in at 30-150 ms. Python tools are 10-200× slower. ([Competitive matrix §2](./competitive-analysis.md#2-competitive-matrix))

**Why it matters:** inside Espanso, AutoHotkey, or a shell pipe, cold start is perceived latency. Below ~15 ms, AI text expansion *feels synchronous*. Above 100 ms, users notice the lag and stop using it. We are the only CLI that ships on the right side of that threshold.

### 2. Azure OpenAI -- and Azure Government -- as the primary target.

Every Azure-capable competitor treats Azure OpenAI as an "OpenAI-compatible base URL" hack. az-ai is designed around Azure's resource/deployment/api-version model from the ground up, which means:

- **FedRAMP High / DoD IL5 reachable today** via `AzureUSGovernment` endpoints (Azure OpenAI in Azure Gov authorized since Aug 2024; reference: [fedscoop coverage](https://fedscoop.com/microsoft-azure-openai-service-fedramp/), [fedramp.gov/ai](https://www.fedramp.gov/ai/)).
- **No Python, Node, or pip required** on locked-down enterprise/regulated workstations. A single ~13 MiB static binary ships and runs.
- **Control plane (`az cognitiveservices`) and data plane are disjoint in Microsoft's official tools.** az-ai is the data-plane chat/agent client Microsoft does not ship ([docs ref](https://learn.microsoft.com/en-us/cli/azure/cognitiveservices)).

### 3. Persona + Squad memory, and Espanso-grade text injection -- both unique.

- **`--persona <name>`** binds a named teammate to a scoped system prompt, tool allowlist, and persistent memory in `.squad/`. llm has stateful toolboxes and fabric has sessions, but neither ships a named-persona dispatch pattern with per-persona memory as a first-class primitive.
- **`--raw` mode** emits only clean stdout -- spinner, ANSI, and framing stripped -- specifically so text-expanders can inject it back into any field. No competitor treats in-field replacement as a designed use case ([integration guide](./espanso-ahk-integration.md)).

## Cost story (Morty-approved)

At 10,000 requests/month (3k in + 500 out), az-ai on Azure GPT-5 is **$87.50/month all-in**. Claude Code Max is **$275/month tokens + $100 seat = $375/month** -- **4.3× more expensive** per seat. ([FinOps annex](./competitive-analysis.md#4-mortys-finops-annex--cost-per-task))

**Free CLI + Azure PAYG + Microsoft volume discounts = the cheapest defensible story in the category.**

## What we are not

We are not a vendor-flagship coding agent. We do not bundle a model, we do not have a TUI, we will not win SWE-bench. For autonomous repo surgery, use Claude Code. **For every interactive, latency-sensitive, regulated, or shell-native workflow in between -- use az-ai.**

---

**Evidence index:** Benchmarks in `README.md#performance`. Sources in `docs/competitive-analysis.md` footnotes. All claims are reproducible and timestamped 2026-04-20.

*~490 words.*
