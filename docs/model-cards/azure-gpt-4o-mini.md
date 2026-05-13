---
model: gpt-4o-mini
provider: azure
description: "Cheap, fast workhorse for tool-calling and JSON mode"
status: active
version_noted: "2024-07-18"
capabilities: [tool_calls, json_mode, streaming, system_prompt]
context_window: 128000
cost_tier: low
---

## Strengths

GPT-4o mini is the workhorse of the `az-ai` default fleet. It handles structured
output reliably: JSON mode produces well-formed objects even on nested schemas, and
tool-call round-trips are stable across the Espanso and AHK text-expansion workflows
that are the primary `az-ai` use case. Streaming latency is low enough that
single-sentence expansions feel instantaneous. The 128 K context window means large
pasted documents (code files, email threads, logs) fit without chunking. Cost per
token is the lowest in the Azure OpenAI catalogue at time of writing, making it the
correct default for high-frequency expansion workflows where a user may fire dozens
of prompts per hour.

## Weaknesses

Complex multi-step reasoning chains degrade before GPT-4o does: chain-of-thought
tasks that require more than two or three reasoning hops frequently produce plausible
but incorrect conclusions without signalling uncertainty. Code generation on non-trivial
refactoring tasks is adequate for small functions but unreliable for cross-file
changes. The model does not support vision input or output; prompts that include image
data will be rejected or silently ignored depending on the SDK version.

## Default use case

Short-to-medium text expansion and transformation: rewriting a sentence, summarising
a paragraph, generating a structured JSON blob from a natural-language description,
or calling a single tool in the `az-ai` agent loop. This is the model to reach for
when throughput and cost matter more than depth of reasoning.

## Known limitations

- No vision input (`vision_in` is absent from `capabilities`); sending image content
  raises an API error.
- Function-call streaming (tool calls delivered as a stream rather than a complete
  message) has known gaps in the Azure OpenAI SDK at `2024-07-18`; `az-ai` works
  around this by buffering tool-call chunks before dispatch.
- At the far end of the 128 K context window (above ~100 K tokens in practice),
  instruction following degrades and the model may truncate or ignore the system
  prompt. Keep system prompts short and place critical instructions early.
- JSON mode requires an explicit instruction to produce JSON in the system or user
  prompt; the mode alone does not guarantee a particular schema.
