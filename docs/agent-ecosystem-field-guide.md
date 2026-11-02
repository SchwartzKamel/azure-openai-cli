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
| The eval harness against a live model | **Works, and found real things** | First live run: smoke 100%, persona-voice 70% with flakes, one safety fixture blocked upstream |
| Compressing an agent platform's logs | **Not worth it here** | The logs were tiny (one run directory, 20 KB) |
| Attaching the machine's CI runner to this repo | **Do not** | The repo is public; a fork PR could run code on the host |

## Prerequisite: a model, and one trap

Hopper needs a deployment it can reach. On Azure, the model decides which parameters are accepted:

- Two of the three deployments tested (`gpt-5-nano`, `gpt-5.2-chat`) **reject hopper's default
  `temperature`** with HTTP 400 `unsupported_value`. `--temperature 1` works. `gpt-5.4-mini` accepts the default.
- The registry does not know these models, so hopper cannot omit the parameter for you. This is tracked
  as `kramer-2026-11-K-5`.
- A reasoning model needs a realistic `--max-tokens`: the hidden reasoning counts against it. With
  `--max-tokens 16` or `64` on `gpt-5-nano`, hopper printed **nothing and exited 0**; `1500` returned the
  answer. An empty reply with a success exit code is a silent failure, tracked as `kramer-2026-11-K-6`.

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

Hopper's agent can call any MCP stdio server's tools. Against the `agent-toolkit` ops server:

```bash
MCP_ALLOW='janitor_report,janitor_health,updater_report,updater_health' \
  hopper --agent --tools datetime --temperature 1 --max-tokens 4000 \
  --mcp-server "toolkit=python3 examples/mcp/read-only-proxy.py agent-toolkit-mcp" \
  "Is the janitor or the updater unhealthy, when did each last run, and what should I do?"
```

It answered in 19 s and every fact was checked against the tools' own output (status, an idle streak of 15,
integrity, `dpkg --audit` clean, both timestamps).

**The filter matters.** Hopper's MCP client exposes every tool the server lists, and `--read-only` only
governs hopper's own server. The toolkit lists `janitor_run` (cleans disk) and `updater_run` (upgrades
packages) beside its reports, declares no `readOnlyHint`, and nothing else would stop a model from calling
them. `examples/mcp/read-only-proxy.py` puts an allow and a deny pattern in front of any server and refuses to
start with no policy. Tracked as `newman-2026-11-N-11`.

For **pi**, skip the proxy: its `toolExposure` setting in `~/.pi/agent/mcp.json` does this natively
(`"*_run": "hidden"`).

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

That last row is a real product gap. A safety fixture that cannot be verified correctly fails the gate, but on
an Azure deployment with the default content filter this fixture can never be graded, so the live gate would
stay red. A filtered prompt should be its own outcome, not an error. Tracked as `maestro-2026-11-M-5`.

## What not to do

- **Do not attach a self-hosted CI runner to a public repository.** A pull request from a fork can run code on
  the host. Run live evals from a schedule or a manual trigger with repository secrets, or locally.
- **Do not point an agent at a mixed tool server unfiltered.** See Recipe 3.
- **Do not compress logs you have not measured.** The hypothesis that an agent platform's logs were large was
  false; the multi-gigabyte directory was build worktrees.
- **Do not trust an HTTP 200 from a host you have not looked at.** One GitHub endpoint answered `OK` to every
  request, which looks like success and is not.
- **Do not generalise from the easy benchmark.** See Recipe 1.

## Reproducing this

Every number above came from a command in this guide or from the integration suite. The proxy has hermetic
tests (hopper's own MCP server as the upstream, no network or model). The model comparisons need your own
deployments, so re-run them rather than reuse these figures.
