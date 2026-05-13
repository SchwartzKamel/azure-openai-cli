---
model: gpt-5.4-nano
provider: azure
version_noted: "2025-01-01"
capabilities: [tool_calls, json_mode, streaming, system_prompt]
context_window: 128000
cost_tier: low
---

## Strengths

GPT-5.4 nano is a next-generation compact model positioned as the successor to the
GPT-4o mini tier. Where GPT-4o mini excels at throughput, GPT-5.4 nano adds improved
instruction adherence and more reliable tool-call formatting under adversarial prompt
conditions. Structured output fidelity is higher: JSON mode produces schema-conformant
objects more consistently even without an explicit JSON instruction in the prompt. The
128 K context window matches GPT-4o mini, and the cost tier remains low, making it a
drop-in upgrade for Espanso and AHK expansion workflows that have hit GPT-4o mini's
reasoning ceiling.

## Weaknesses

As a newer model, production behaviour may differ from benchmark results; the
`version_noted` date should be treated as the snapshot against which this card was
written. Deep multi-hop reasoning remains limited relative to GPT-5 full; use a
larger model for tasks requiring more than three chained inferences. Tool-call
streaming behaviour at the Azure endpoint may lag behind the OpenAI endpoint by one
SDK release cycle.

## Default use case

A step up from `gpt-4o-mini` for workflows where JSON schema compliance and
tool-call reliability matter, without paying the cost or latency premium of a larger
model. Suitable as the new default once the deployment is confirmed available in the
user's Azure region and the model is listed in `AZUREOPENAIMODEL`.

## Known limitations

- Context window is documented as 128 K tokens; actual usable window under
  instruction-following constraints may be lower. This card will be updated when
  empirical data from `az-ai` dogfooding is available. Until then, treat the ceiling
  as approximately 100 K tokens for reliable behaviour.
- No vision input or output at time of writing; `vision_in` and `vision_out` are
  absent from `capabilities`.
- Availability varies by Azure region and subscription tier. The deployment name
  `gpt-5.4-nano` is a placeholder; confirm the exact deployment name in the Azure
  portal before adding it to `AZUREOPENAIMODEL`.
- Model versioning on Azure OpenAI does not map one-to-one to OpenAI versioning.
  The `version_noted` field reflects the date this card was authored, not a
  guaranteed API version string. Verify the deployed API version in the portal.
