# Phase 0 spike -- Microsoft Agent Framework

**Status**: scaffolding only. Awaiting real Azure endpoint via `.env` to run benchmarks.

**Purpose**: prove (or disprove) that adopting [Microsoft Agent Framework](https://learn.microsoft.com/en-us/agent-framework/overview/) (`Microsoft.Agents.AI`) keeps `az-ai` within the speed budget defined in the v2.0 plan:

- Cold start regression ≤ 10% (5.4 ms → 5.9 ms max)
- TTFT regression ≤ 5 ms
- Streaming throughput regression ≤ 5%
- Single tool round-trip regression ≤ 5 ms
- Native AOT publish stays green

If MAF passes, we adopt it on the warm/cold paths per the plan's hot-vs-cold matrix. If it fails on the hot path, the hot path stays hand-rolled.

**This is throwaway code.** Not shipped. Not in the main test suite. Lives under `spike/` until decision is made and ADR-004 is written.

## Layout

- `AgentFrameworkSpike.csproj` -- references `Microsoft.Agents.AI` 1.1.0, `Microsoft.Agents.AI.OpenAI` 1.1.0, `Microsoft.Agents.AI.AzureAI` 1.0.0-rc5, `Azure.Identity` 1.13.1
- `Program.cs` -- minimal CLI: `--auth {apikey|aad|foundry} --prompt <text>` with monotonic `[mark]` stderr timestamps for the bench harness
- `bench.sh` -- Linux benchmark runner; compares handrolled vs spike across cold-start, TTFT, end-to-end latency, binary size
- `README.md` -- this file

## Build

```bash
export PATH="$HOME/.dotnet:$PATH"
dotnet build spike/agent-framework
dotnet publish spike/agent-framework -c Release -r linux-x64 \
  -p:PublishAot=true -p:StripSymbols=true
```

## Run

```bash
# stdin
echo "say pong" | ./af-spike --auth apikey

# explicit
./af-spike --auth apikey --prompt "say pong"
./af-spike --auth aad    --prompt "say pong"
./af-spike --auth foundry --prompt "say pong"   # currently throws NotImplementedException
```

## Bench

Requires real `.env` in repo root:

```text
AZUREOPENAIENDPOINT=https://...
AZUREOPENAIAPI=...
AZUREOPENAIMODEL=gpt-4o-mini
AZURE_FOUNDRY_PROJECT_ENDPOINT=https://...   # optional, foundry only
```

Then:

```bash
bash spike/agent-framework/bench.sh         # default RUNS=10
RUNS=20 PROMPT="hi" bash spike/agent-framework/bench.sh
```

Output:

- stdout: per-probe averages (cold-start, TTFT, end-to-end)
- `docs/spikes/af-benchmarks.md`: appended run section with timestamp + binary sizes

## Known stubs (Phase 0 follow-up)

- **Foundry path**: `BuildFoundryAgent` throws `NotImplementedException` -- requires verifying `Microsoft.Agents.AI.AzureAI` 1.0.0-rc5's exact public surface (`PersistentAgentsClient` factory shape) against an actual Foundry project endpoint
- **Tool round-trip benchmark**: not yet wired -- Phase 0 part 2 will register a single dummy AF function tool and re-time
- **AAD warm path**: token cache lifetime not yet measured across runs

## Decision criteria

Results land in `docs/spikes/af-benchmarks.md`. ADR-004 ("Speed-gated hybrid adoption") will record the verdict component-by-component using the Keep/Adopt/Defer matrix in `plan.md`.
