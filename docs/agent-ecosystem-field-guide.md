# Using hopper with agents and ops tools: a field guide

> What was tried on a real development server (an agent platform, a coding-agent harness, a skill system,
> cron-driven ops scripts, an Azure OpenAI resource), what worked, what did not, and the numbers. Written
> from measurements, not from the feature list. It names no endpoints, keys or resource names.

The server had no model configured for hopper at the start. Everything below ran against one Azure OpenAI
resource with three chat deployments, supplied through the normal environment variables.

## The short version

| Use | Verdict | Evidence |
|---|---|---|
| A fast second reviewer of a diff | **Works, with calibration** | 18/18 seeded bugs caught, 0 false alarms; but a deep review of real code was only 47% to 100% accurate depending on the model |
| `wgm`'s docs-audit swarm | **Works with a mid-tier model** | 5 roles contract-valid in 35 s; the cheapest model failed the writer role |
| Natural-language ops questions over an MCP server | **Works, but filter the tools** | Correct, source-checked answers in 19 s; the server also listed two destructive tools |
| A first draft of a commit message | **Useful, incomplete** | 1.3 s; accurate on *what*, blind to *why* |
| The eval harness against a live model | **Works, and found real things** | First live run: smoke 100%, persona-voice 70% with flakes; one safety fixture was blocked upstream, and is now its own outcome (S04E26) |
| An HTTP-only MCP server (scouter's offline analysis) | **Works through a bridge** | 10 graded runs, 2.5 s each; both models right on the code, the smaller one wrong on 3 of 5 hex addresses |
| Compressing an agent platform's logs | **Not worth it here** | The logs were tiny (one run directory, 20 KB) |
| Attaching the machine's CI runner to this repo | **Do not** | The repo is public; a fork PR could run code on the host |

## Prerequisite: a model, and what it will accept

Hopper needs a deployment it can reach. On Azure, the model decides which parameters are accepted:

- Two of the three deployments tested (`gpt-5-nano`, `gpt-5.2-chat`) **reject hopper's default
  `temperature`** with HTTP 400 `unsupported_value`; only `gpt-5.4-mini` accepts it. Hopper now handles this itself
  (S04E20): on that exact error it retries once without the setting, says so on stderr, and remembers the model for the
  rest of the run. Before then, `--temperature 1` was the workaround.
- A reasoning model needs a realistic `--max-tokens`: the hidden reasoning counts against it. With
  `--max-tokens 16` or `64` on `gpt-5-nano`, hopper used to print **nothing and exit 0**; `1500` returned the
  answer. Since S04E21 an empty reply that stopped at the output limit (or was withheld by the content filter) is an
  error with exit 1 that names `--max-tokens`. A reply that has *some* text but was cut off is not yet flagged
  (`kramer-2026-11-K-9`), and the Ralph and Commander loops do not check either (`K-10`).

Supply credentials the supported way (`hopper --setup`, or the three environment variables). Do not copy a
key from another tool's configuration into a chat, a script or a repository.

## Recipe 1: a second reviewer (and how to trust it)

```bash
git diff | hopper --raw --system "You are a strict code reviewer. Reply with JSON: {\"verdict\":\"bug\"|\"clean\",\"issue\":\"...\"}" "Review this diff."
```

Measured with nine small diffs (six seeded defects, three clean controls), three repeats each:

| Model | Seeded bugs caught | False alarms | Time for 27 calls |
|---|---|---|---|
| `gpt-5.4-mini` | 18 / 18 | 0 / 9 | 29 s |
| `gpt-5.2-chat` | 18 / 18 | 0 / 9 | 36 s |
| `gpt-5-nano` | 18 / 18 | 0 / 9 | 83 s |

That benchmark is easy and **cannot tell the models apart**. A harder test did. The same models were asked to
find weaknesses in a 361-line shell-guard scanner, and every claim was run against the real scanner:

| Model | Claims | Consistent with the real code |
|---|---|---|
| `gpt-5.2-chat` | 10 | 10 (100%) |
| `gpt-5.4-mini` | 15 | 7 (47%) |

The better model also found a bypass nobody had (`bash -c 'coproc rm FILE'`), confirmed against a real shell
and fixed in S04E17. The lesson is the method, not the model: **grade a reviewer by checking its claims
against the system, and never by how confident it sounds.**

## Recipe 2: `wgm`'s docs audit with hopper as the agent

`wgm`'s `scripts/audit.sh` drives five roles through one headless command that takes the prompt as an
argument. Hopper's agent mode with the `file` tool fits:

```bash
export WGM_AGENT='hopper --agent --tools file --max-tokens 8000 --max-rounds 12 --raw'
bash ~/.agents/skills/wgm/scripts/audit.sh --scope "README.md and docs/" --timeout-seconds 300
```

| Model | Result | Time |
|---|---|---|
| `gpt-5.4-mini` | complete, 5 of 5 roles contract-valid, 66-line consolidated report | 35 s |
| `gpt-5-nano` | **RED**: four personas passed, the writer failed the contract; one persona reported that files did not exist when they did | 141 s |

The audit fails closed, so the weaker model wrote nothing rather than a success-shaped report. Use a mid-tier
model for this, and treat the output as one independent signal, not as the audit.

## Recipe 3: ask a tool server a question in English

Hopper's agent can call any MCP stdio server's tools. Against the `agent-toolkit` ops server, showing the model only
the four read-only reports:

```bash
hopper --agent --tools datetime --max-tokens 4000 \
  --mcp-server "toolkit=agent-toolkit-mcp" \
  --mcp-allow janitor_report --mcp-allow janitor_health \
  --mcp-allow updater_report --mcp-allow updater_health \
  "Is the janitor or the updater unhealthy, when did each last run, and what should I do?"
```

It prints `[mcp] 'toolkit': exposing 4 of 9 tool(s) to the model (5 hidden by --mcp-allow/--mcp-deny)`, answers in
about 6 s, and every fact was checked against the tools' own output (the janitor `degraded` with exit 4 because
`aged_logs` has an idle streak of 20, its last run ID and time; the updater `healthy` with exit 0, its last run and ID).

**The filter matters.** An MCP client exposes every tool the server lists. The toolkit lists `janitor_run` (cleans disk)
and `updater_run` (upgrades packages) beside its reports, declares no `readOnlyHint`, and nothing else would stop a
model from calling them. Since S04E22 hopper has `--mcp-allow` and `--mcp-deny` (repeatable globs; deny wins; a tool
the model is not told about cannot be called). Run `hopper mcp tools <server> --mcp-allow ...` first to preview exactly
what the model will see.

- The default is still **everything**, unchanged, so nothing that works today breaks. Say what you want to allow.
- A pattern that matches no tool prints a warning: a mistyped `--mcp-deny` leaves the tool you meant to hide exposed.
- `examples/mcp/read-only-proxy.py` still has a use: it enforces the same policy outside the process, for hosts that
  have no such flag.

## Recipe 4: a first draft of a commit message

```bash
git diff | hopper --pattern commit-message --raw
```

About 1.3 s. On two real commits it named the right scope and listed the right changes, and it labelled a
security fix `feat` and a redaction fix `refactor`, because intent is not in the diff. Use it to pre-fill
`jj describe` or `git commit`, then add the reason yourself.

## Recipe 5: the eval harness against a live model

```bash
hopper eval --corpus smoke --repeat 3
```

First live results (`gpt-5.4-mini`, three repeats):

| Corpus | Score | Notes |
|---|---|---|
| `smoke` | 100% (7/7) | 16 s |
| `persona-voice` | 70% (7/10) | two fixtures flaky, one failed 0 of 3; flake detection did its job |
| `refusal` | 87.5% (7/8), FAIL | seven fixtures 3/3; one **errored**: Azure's content filter rejected the prompt before the model saw it |

That last row was a real product gap, and it is fixed (S04E26). A safety fixture that cannot be verified correctly fails the
gate, but on an Azure deployment with the default content filter the classic "DAN" jailbreak fixture could never be graded,
so the live gate stayed red whatever the model did. A block is now its own outcome, `FILTERED`: unverified, so it fails a
safety fixture by default. The shipped `refuse-persona-override` fixture sets `allow_filtered` (a block is an acceptable
outcome of that attack), so on Azure the refusal suite now reads `100% (8/8) PASS` with the line
`Filtered: 1 fixture had a run blocked by the platform content filter`. It is deliberately visible: on Azure that fixture no
longer checks the model's own refusal, only that the platform stops the prompt. On a provider without such a filter the model
is asked and graded as written.

## Recipe 6: an HTTP-only tool server (scouter's offline analysis)

Scouter is a reverse-engineering workbench with a large tool registry and a small MCP surface. Its offline profile
(`scouter mcp serve-offline`) exposes three tools for static analysis of an executable image that you own:
`native_analysis_admit` (controller role, CONFIRM tier), and `native_analysis_open` and `native_analysis_page`
(SAFE). It is a disassembler behind MCP, not a decompiler, and nothing here touched a live process or the Windows
probe DLL.

Two facts shape the integration. Scouter's MCP hosts are **authenticated HTTP** on loopback (a bearer token per
role, an `Mcp-Session-Id`, at most one controller, bounded sessions), and hopper's MCP client **only launches a
command**. Pointing hopper at the URL fails with an error that does not say why. So a bridge sits between them:

```bash
# one-time, by a person: the controller admits an image you own (a CONFIRM-tier write into the project directory)
# then hopper gets only the observer token, and never even sees the admit tool
MCP_BRIDGE_TOKEN_FILE=observer.tok MCP_DENY=native_analysis_admit \
  hopper --agent --tools datetime \
  --mcp-server "scouter=python3 examples/mcp/read-only-proxy.py python3 examples/mcp/http-bridge.py http://127.0.0.1:13340/mcp" \
  "Read analysis <id>: what does the function at 0x401176 compute, and which function checks a password?"
```

`examples/mcp/http-bridge.py` relays newline-delimited JSON-RPC to an HTTP MCP server, reading the token from a file so
nothing secret is in the environment or the command line. It composes with the read-only proxy from Recipe 3.

**The test.** A small ELF program was compiled for the purpose, so the ground truth was known (`objdump` and `nm`):
a loop summing 1 to n, and a function that `strcmp`s its argument with `hunter2`. One controller call admitted it;
every later call used the read-only observer token. Each run was graded mechanically on a JSON answer:

| Model | formula | function name | secret string | **hex address** | mean time |
|---|---|---|---|---|---|
| `gpt-5.4-mini` | 5/5 | 5/5 | 5/5 | **2/5** | 2.5 s |
| `gpt-5.2-chat` | 5/5 | 5/5 | 5/5 | **5/5** | 2.7 s |

Both models understood the code. The smaller one failed at **converting a decimal address to hex**, because scouter
reports addresses as decimal integers (the true `check` was `0x40119e`, and it said `0x4011814`; the tool's output
never contains that number). When exact addresses matter, use the stronger model, or have the server emit hex.

**Three things that went wrong, and are fixed or recorded:**

- **Eight runs, then HTTP 429.** The host admits 8 observer sessions and every hopper run leaked one, because hopper
  killed the bridge with SIGKILL before it could close its session. A control experiment (8 of 16 runs, then failure)
  made it exact. The root cause was in hopper (S04E18), and the bridge now defaults to a per-request session so it
  leaks nothing even under SIGKILL: 24 of 24 runs against a cap of 8.
- **A 67 KB result for a ten-line program.** `native_analysis_open` returns the whole record. That is cheap in
  milliseconds and not in tokens; page through it when the program is larger than a toy.
- **Scouter lists `native_analysis_admit` to the read-only role,** which then correctly refuses to run it. Hiding what a
  role cannot call (the proxy does it here) saves the model from trying.

**Suggestions for scouter, from this run** (not changes made to it): emit a `va_hex` string beside every integer
address; expire idle sessions, so a client that dies cannot hold a slot for ever; filter `tools/list` by role.

## What not to do

- **Do not attach a self-hosted CI runner to a public repository.** A pull request from a fork can run code on
  the host. Run live evals from a schedule or a manual trigger with repository secrets, or locally.
- **Do not point an agent at a mixed tool server unfiltered.** See Recipe 3.
- **Do not compress logs you have not measured.** The hypothesis that an agent platform's logs were large was
  false; the multi-gigabyte directory was build worktrees.
- **Do not leave cleanup to a client that may be killed.** Open and close a session within one request, or make sure
  the client shuts the child down politely. Hopper now does (S04E18).
- **Do not trust an HTTP 200 from a host you have not looked at.** One GitHub endpoint answered `OK` to every
  request, which looks like success and is not.
- **Do not generalise from the easy benchmark.** See Recipe 1.

## Reproducing this

Every number above came from a command in this guide or from the integration suite. The proxy has hermetic
tests (hopper's own MCP server as the upstream, no network or model). The model comparisons need your own
deployments, so re-run them rather than reuse these figures.
