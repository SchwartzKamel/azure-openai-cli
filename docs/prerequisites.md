# Prerequisites — Environment Variables

Every `az-ai` example in the use-case guides assumes the following three
environment variables are set. Without them the CLI will refuse to start.

| Variable              | Purpose                                         | Example                                         |
|-----------------------|-------------------------------------------------|-------------------------------------------------|
| `AZUREOPENAIENDPOINT` | Your Azure OpenAI resource URL                  | `https://my-resource.openai.azure.com/`         |
| `AZUREOPENAIAPI`      | API key for authentication (note: **not** `KEY`) | `ab12cd34ef56...`                               |
| `AZUREOPENAIMODEL`    | Deployment name(s); comma-separated for multi-model | `gpt-4o` or `gpt-4o,gpt-4o-mini`             |

## Quick setup

```bash
export AZUREOPENAIENDPOINT="https://your-resource.openai.azure.com/"
export AZUREOPENAIAPI="your-api-key-here"
export AZUREOPENAIMODEL="gpt-4o"
```

Persist them in your shell profile (`~/.bashrc`, `~/.zshrc`) or a project-local
`.env` file loaded by your shell or tool of choice.

## Multi-model deployments

`AZUREOPENAIMODEL` accepts a comma-separated list. The first entry is the
default; use `--set-model <name>` or `--config set model <name>` to pick a
different one at runtime. See
[use-cases-config-integration.md](use-cases-config-integration.md) for the full
configuration workflow.

## Troubleshooting

- **"Missing AZUREOPENAIENDPOINT"** — the variable is unset or exported in a
  different shell. Verify with `env | grep AZUREOPENAI`.
- **HTTP 401 Unauthorized** — `AZUREOPENAIAPI` is wrong or rotated. Regenerate
  in the Azure portal under *Keys and Endpoint*.
- **HTTP 404 DeploymentNotFound** — `AZUREOPENAIMODEL` does not match a
  deployment on the endpoint. List deployments in Azure AI Foundry / portal.
