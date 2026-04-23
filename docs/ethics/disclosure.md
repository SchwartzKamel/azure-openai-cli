# AI Use Disclosure

A short, plain-language statement of what you should know about how
this CLI uses AI. If you want the full posture and the technical
controls, see [`responsible-use.md`](responsible-use.md).

## What model runs your prompts

The CLI talks to **Azure OpenAI**, a hosted service from Microsoft.
The model does not run on your machine. When you send a prompt,
the prompt text leaves your machine and is processed in Microsoft's
cloud.

The specific model is whatever is configured by the
`AZUREOPENAIMODEL` environment variable (or your saved configuration).
You can change it. The CLI ships defaults but does not lock you in.

## Where your prompts go

```text
your terminal -> CLI binary -> Azure OpenAI endpoint -> response -> your terminal
```

Your prompts and the model's responses pass through Microsoft's
infrastructure and are subject to Microsoft's data-handling terms
for Azure OpenAI. The authoritative description of that handling
lives at:

- <https://learn.microsoft.com/azure/ai-services/openai/how-to/data-privacy>

We link to the upstream page rather than paraphrase it, because
Microsoft updates the terms and we do not want a stale summary in
this repo to mislead you.

## What this project does and does not do with your data

- **We do not store your prompts.** The CLI runs locally, sends your
  prompt to Azure OpenAI, prints the response, and exits. There is
  no project-side server, database, or log file capturing your
  input or the output.
- **We do not transmit telemetry.** No usage analytics, no error
  reporting, no "phone home." See `docs/telemetry.md` for the audit
  recipe a contributor can run to verify this themselves.
- **We do not train any model.** This project is a client; it does
  not produce, fine-tune, or contribute training data to any model.
  Whether Microsoft uses your prompts for service improvement is
  governed by their terms (linked above), not by this project.
- **You own your prompts and outputs.** This project does not claim
  rights over what you type into it or what the model returns to
  you.

## What about your API key

Your Azure OpenAI API key stays on your machine. On Windows it is
stored using DPAPI; on Linux via libsecret; on macOS via Keychain.
The CLI never logs your key, never prints it, and never sends it
anywhere except to the Azure OpenAI endpoint as the authentication
header on each request.

If you suspect your key has leaked, rotate it in the Azure portal
immediately. The CLI will pick up the new value on the next run.

## What you should know about model output

The model can be wrong. The model can be biased. The model can
produce code that looks correct but is not. The CLI does not check
or validate model output for accuracy, safety, or correctness. You
are the final reviewer.

If you use this CLI in agent mode, the model can run tools on your
behalf -- read files, fetch URLs, execute shell commands. There are
guardrails (see `responsible-use.md`), but the guardrails are not
infallible. Treat agent-mode output the way you would treat a
script written by a stranger: read it before you trust it.

## Reporting concerns

- **Security issues** (e.g., a way to bypass the tool blocklists):
  see `SECURITY.md` for the disclosure policy.
- **Responsible-use concerns** (e.g., a use case the documentation
  does not address): open a GitHub Discussion or file an issue.
- **Microsoft-side concerns** (data handling, content policy,
  service availability): contact Microsoft directly via the Azure
  portal; this project does not control or speak for the hosted
  service.

## See also

- [`responsible-use.md`](responsible-use.md) -- the full ought / must
  matrix and per-surface technical controls.
- [`../telemetry.md`](../telemetry.md) -- zero-default-telemetry posture.
- [`../../SECURITY.md`](../../SECURITY.md) -- vulnerability disclosure.
