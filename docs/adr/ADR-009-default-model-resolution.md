# ADR-009 -- Default Model Resolution: Env-Var-First, Conservative Fallback

- **Status**: Accepted -- 2026-04-23
- **Deciders**: Costanza (PM), Morty (FinOps), Maestro (prompts), Kramer (eng)
- **Related**:
  - [ADR-005 -- Foundry Routing](./ADR-005-foundry-routing.md) -- how non-OpenAI deployments resolve
  - [docs/cost-optimization.md §3.5](../cost-optimization.md) -- Morty's cost analysis of `gpt-5.4-nano` vs `gpt-4o-mini`
  - [docs/audits/docs-audit-2026-04-22-morty.md](../audits/docs-audit-2026-04-22-morty.md) -- finding: "the doc lies" about the operational default
  - [docs/audits/docs-audit-2026-04-22-maestro.md §M5](../audits/docs-audit-2026-04-22-maestro.md) -- finding: no single source of truth for the default model

## Context

Look, I'll tell you what happened. I opened the audits and the *left hand doesn't know what the right hand is doing*.

- `azureopenai-cli-v2/Program.cs:257,1222` has the literal `"gpt-4o-mini"` wired in as the hardcoded fallback.
- **Eight-ish documentation locations** (CHANGELOG, README passthroughs, FR-017, ADR-005 commentary, spike reports, Morty's own audit, Maestro's M5, the Bania benchmark preamble, assorted ops runbooks) cite `gpt-5.4-nano` as "the default."
- `docs/cost-optimization.md §3.5` explicitly decided against `gpt-5.4-nano` as the default on cost grounds (4.3× more expensive on input for reasoning most Espanso-style calls don't need).
- The **user/operator** has -- per Morty's audit and recent operational guidance -- *flipped their own deployment's default to `gpt-5.4-nano`* via environment variable.

So we have three "defaults":

1. **Code fallback:** `gpt-4o-mini` (what a fresh install gets).
2. **Ops default:** `gpt-5.4-nano` (what the current operator has in their env).
3. **Recommended default:** `gpt-4o-mini` per Morty's §3.5 cost analysis.

Three defaults is two defaults too many. The team has no single source of truth for "what is our default?" and audit reviewers fall into the gap.

*The sea was angry that day, my friends -- like an old man trying to return soup at a deli.* That's what reading the contradictory docs felt like. Fix it.

## Decision

**Option C -- env-var-first resolution chain with a conservative hardcoded fallback.**

The default model is a **resolution chain**, not a single value. The canonical ordering is:

1. **CLI flag** -- `--model` / `-m` (alias-resolved via `UserConfig.ResolveModel`).
2. **Environment variable** -- `AZUREOPENAIMODEL`.
3. **UserConfig** -- `default_model` and `ResolveSmartDefault()` in `~/.azureopenai-cli.json`.
4. **Hardcoded fallback** -- `Program.DefaultModelFallback = "gpt-4o-mini"`.

Each step is consulted in order; the first non-null value wins. No step can silently be skipped.

### Why Option C over Option A or B

| Option | What it does | Why we rejected |
|---|---|---|
| **A -- Keep `gpt-4o-mini` everywhere, roll back docs** | Honors §3.5 purely, makes docs consistent with code. | Contradicts the operator's explicit directive to run nano in their own deployment. Also pretends the env-var override doesn't exist -- but it does, and it's how the *operator* already behaves. |
| **B -- Flip code to `gpt-5.4-nano`, rewrite §3.5** | Honors operator directive in code. Single default everywhere. | Punishes every *fresh install* with a 4.3× input-cost bump the new user didn't opt into. Shipping a cost regression as a default is, to use a technical term, *disturbed*. |
| **C -- Env-var-first, conservative fallback** ✅ | Operator gets what they want via env. Fresh installs get the cheap, well-behaved SKU. Docs stop lying because they describe a **chain**, not a value. | Slightly more doc surface (one precedence paragraph instead of one name). Worth it. |

Costanza's architecture reasoning, for the record: **defaults are a UX contract with the user who just installed the thing**, not with the power user who knows their way around `export`. The fresh-install default must be the conservative, low-variance, low-cost choice. Power users who want reasoning go one `export AZUREOPENAIMODEL=gpt-5.4-nano` away from it. That's not friction -- that's consent.

## Consequences

### What changes in code

- A single named constant -- `Program.DefaultModelFallback` -- replaces the two duplicated `"gpt-4o-mini"` literals at `Program.cs:259` and `Program.cs:1223`. Future model-default changes are one-line diffs plus an ADR amendment.
- `RunEstimate` explicitly documents that it short-circuits the UserConfig / alias step (estimate must work pre-config). Operators who want the smart default from `--estimate` pass `--model` explicitly.
- No default value change. `gpt-4o-mini` stays the fallback.

### What changes in docs

- `docs/cost-optimization.md §3.5` updated: the **recommendation** still says `gpt-4o-mini`, but the section now acknowledges that operators may (and per current ops posture, *do*) set `AZUREOPENAIMODEL=gpt-5.4-nano` for reasoning-forward workloads. The decision is reframed as "keep the *fallback* conservative," not "nobody may ever run nano."
- `README.md` default-model sentence verified: already describes the precedence chain correctly. Good. No edit.
- `CHANGELOG.md` gets an Unreleased entry describing the canonicalization.

### Cost ceiling per session (fresh install)

At `gpt-4o-mini` fallback pricing (~$0.15 input / ~$0.60 output per 1M tokens) and Morty's baseline workload (1K in / 200 out per call, ~3K calls/mo/seat), a fresh-install seat burns about **~$0.50/seat/month**. That's the ceiling for someone who never touches config. If they flip to nano via env, it rises to roughly **~$0.80-$1.00/seat/month** -- still unambitious by any reasonable bar, but up 50-67%, hence the conservative fallback.

### Quality expectations

`gpt-4o-mini` clears the bar for every primary Espanso-style use case (rewrites, commit messages, summaries, yes/no classifiers). Agent-mode and `--schema` workflows also pass on it (ref: `docs/benchmarks/phi-vs-gpt54nano-2026-04-20.md`). Users who need reasoning explicitly opt in -- via `--model`, env, or config.

### How to change the default (as a user)

Pick the scope that matches your intent:

```bash
# One-shot:
az-ai --model gpt-5.4-nano "explain this diff" < diff.patch

# Session / shell:
export AZUREOPENAIMODEL=gpt-5.4-nano

# Persistent (machine-wide):
az-ai --config set default_model=gpt-5.4-nano
```

### How to change the default (as the project)

Changing `Program.DefaultModelFallback` requires:

1. An ADR amendment (this file) citing the new cost/quality trade-off.
2. A `cost-optimization.md §3.x` rewrite showing the new baseline math.
3. Morty's sign-off on the cost ceiling.
4. A CHANGELOG entry under the relevant release.

No drive-by edits. The fallback is a contract.

## Compliance checklist for future docs / audits

- [ ] Any doc that says "the default model is X" must either say `gpt-4o-mini` (the fallback) **or** describe the full precedence chain.
- [ ] Audit findings that assert "the default is Y" must cite which *tier* of the chain they mean (fallback? env? operator?).
- [ ] Benchmark reports that baseline against "the default" must name the concrete model they tested, not the word "default."

*It's not a lie… if you believe it. And I believe this is the last time we have this conversation.*
