---
model: llama-local
provider: local
version_noted: "2025-01-01"
capabilities: [tool_calls, streaming]
context_window: 8192
cost_tier: unknown
---

## Strengths

The local provider entry represents a llama.cpp-served model running on the user's
own hardware via `LLAMACPP_ENDPOINT`. Running locally means zero per-token cost,
no data leaving the machine, and no dependency on Azure availability. For users with
capable hardware, throughput can match or exceed cloud endpoints for short prompts.
The local provider is the right choice for privacy-sensitive inputs that must not
transit external networks.

## Weaknesses

Model identity is entirely determined by the GGUF file the user loads into llama.cpp.
This card describes the **default expectation** for a capable instruction-tuned model
(e.g., Llama 3 8B or equivalent); actual behaviour depends on the specific GGUF and
quantisation level. Tool-call support requires a model that was fine-tuned for
function calling; a base model loaded without the correct chat template will produce
malformed or absent tool-call payloads. Quality, context window, and capability set
are all user-controlled and not guaranteed by `az-ai`.

## Default use case

Privacy-sensitive text transformation and summarisation where cloud API access is
unavailable or undesirable. Also useful as a fallback during Azure outages, provided
the user has a local server running and `LLAMACPP_ENDPOINT` is set. Not recommended
as the default for tool-calling workflows unless the loaded model has been validated
against the `az-ai` agent loop.

## Known limitations

- The `context_window` value of 8192 is a conservative default. The actual window
  depends on the loaded GGUF and the llama.cpp server `--ctx-size` flag. Users who
  need a larger window must set it explicitly in their server configuration; `az-ai`
  does not override server-side context limits.
- `json_mode` and `system_prompt` are absent from `capabilities` because their
  availability depends on the loaded model and chat template. Many GGUF models support
  system prompts via the chat template; `az-ai` cannot verify this at runtime without
  a probe request.
- `cost_tier` is `unknown` because the marginal cost of local inference depends on
  electricity, hardware amortisation, and opportunity cost -- factors `az-ai` cannot
  model.
- The local provider env-var `LLAMACPP_ENDPOINT` is a placeholder name; the final
  name will be confirmed when the local-provider adapter lands. See the TODO comment
  in `ModelRegistry.cs` for the current status.
- This card will be substantially revised when the local-provider adapter (S03) is
  fully specified. Treat it as a best-effort snapshot.
