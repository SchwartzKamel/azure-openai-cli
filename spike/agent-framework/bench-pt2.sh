#!/usr/bin/env bash
# Phase 0 pt2 benchmark: measure Foundry auth path + tool round-trip latency.
# Runs 3 iterations each for: apikey (baseline), foundry (no tool), foundry (with tool).
# Outputs timing data (TTFT, complete) in a parseable format.

set -euo pipefail

export PATH="$HOME/.dotnet:$PATH"
cd "$(dirname "$0")"

# Load env vars from repo root .env
set -a
source ../../.env
set +a

PROMPT_NOTOOL="ping, reply with just: pong"
PROMPT_TOOL="What time is it? Call get_datetime and tell me the result."
ITERATIONS=3

echo "=== Phase 0 pt2 Benchmark: Foundry auth + tool round-trip ==="
echo

run_bench() {
    local auth="$1"
    local label="$2"
    local prompt="$3"
    local tool_flag="$4"
    
    echo "[$label]"
    for i in $(seq 1 $ITERATIONS); do
        local start=$(date +%s%N)
        
        # Capture stderr to parse timing markers
        local output
        if [ -n "$tool_flag" ]; then
            output=$(dotnet run --project . -- --auth "$auth" --prompt "$prompt" --tool 2>&1)
        else
            output=$(dotnet run --project . -- --auth "$auth" --prompt "$prompt" 2>&1)
        fi
        
        local end=$(date +%s%N)
        local wall_ms=$(( (end - start) / 1000000 ))
        
        # Parse mark timestamps from stderr portion
        local ttft=$(echo "$output" | grep '\[mark\] first-token' | awk '{print $3}' | sed 's/ns//')
        local complete=$(echo "$output" | grep '\[mark\] complete' | awk '{print $3}' | sed 's/ns//')
        
        if [ -n "$ttft" ] && [ -n "$complete" ]; then
            local ttft_ms=$(( ttft / 1000000 ))
            local complete_ms=$(( complete / 1000000 ))
            echo "  iter $i: TTFT=${ttft_ms}ms, complete=${complete_ms}ms, wall=${wall_ms}ms"
        else
            echo "  iter $i: FAILED"
            echo "$output" | head -10 >&2
        fi
    done
    echo
}

# Baseline: apikey path (no tool)
run_bench "apikey" "apikey (no tool)" "$PROMPT_NOTOOL" ""

# Foundry path: no tool
run_bench "foundry" "foundry (no tool)" "$PROMPT_NOTOOL" ""

# Foundry path: with tool
run_bench "foundry" "foundry (tool round-trip)" "$PROMPT_TOOL" "--tool"

echo "=== Benchmark complete ==="
