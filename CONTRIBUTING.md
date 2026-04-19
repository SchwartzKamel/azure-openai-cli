# Contributing to Azure OpenAI CLI

Welcome! We're glad you're interested in contributing to the Azure OpenAI CLI. Whether you're fixing a bug, adding a feature, improving documentation, or reporting an issue — every contribution matters.

## Prerequisites

Before you begin, make sure you have the following installed:

- [Docker](https://www.docker.com/) (for containerized builds and running)
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (for local development without Docker)
- [Make](https://www.gnu.org/software/make/) (for build automation)

Verify your setup:

```bash
docker --version
dotnet --version
make --version
```

## Getting Started

```bash
# 1. Fork and clone the repository
git clone https://github.com/<your-username>/azure-openai-cli.git
cd azure-openai-cli

# 2. Create your .env file from the template
cp azureopenai-cli/.env.example .env
nano .env  # Add your Azure credentials

# 3. Build the Docker image
make build

# 4. Run the CLI
make run ARGS="Hello world!"
```

## Development Workflow

### Local build (without Docker)

For faster iteration during development, you can build and run directly with the .NET SDK:

```bash
dotnet run --project azureopenai-cli/ -- "your prompt"
```

### Docker build

The primary build and distribution mechanism uses Docker:

```bash
make build
make run ARGS="your prompt"
```

### Useful Make targets

| Target | Description |
|--------|-------------|
| `make build` | Build the Docker image |
| `make run ARGS="..."` | Run the CLI inside a container |
| `make clean` | Remove build artifacts and prune Docker cache |
| `make test` | Clean, build, and run a sample prompt |
| `make scan` | Run Grype vulnerability scanner on the image |
| `make check` | Build verification (CI-friendly) |

## Running Tests

```bash
# Via Make (builds and runs a sample prompt)
make test

# Via .NET directly
dotnet test
```

## Code Style

This project follows standard C# conventions:

- **Target framework:** `net10.0`
- **Nullable reference types:** enabled
- **Implicit usings:** enabled
- Use meaningful, descriptive names for variables, methods, and classes
- Keep methods focused and short
- Handle errors explicitly — see the [Architecture](ARCHITECTURE.md) doc for error handling patterns
- Use `System.Text.Json` for serialization (no Newtonsoft)

## Submitting Changes

### Branch naming

Use descriptive branch names with a prefix:

- `feature/add-streaming-timeout`
- `fix/model-selection-crash`
- `docs/update-readme-examples`

### Pull request process

1. **Fork** the repository and create your branch from `main`
2. **Make your changes** — keep commits focused and atomic
3. **Test your changes** — run `make test` or `dotnet test`
4. **Push** your branch to your fork
5. **Open a Pull Request** against `main` with a clear description of your changes
6. Fill out the PR template completely

```bash
git checkout -b feature/my-feature
# make your changes
git add .
git commit -m "Add my feature"
git push origin feature/my-feature
```

### What we look for in PRs

- Clear description of what changed and why
- No breaking changes without discussion
- Tests pass (`make test`)
- Documentation updated if behavior changes
- No credentials or secrets committed

## Reporting Issues

Found a bug or have a feature idea? Please use our issue templates:

- [Bug Report](https://github.com/SchwartzKamel/azure-openai-cli/issues/new?template=bug_report.md)
- [Feature Request](https://github.com/SchwartzKamel/azure-openai-cli/issues/new?template=feature_request.md)

If your issue doesn't fit a template, feel free to [open a blank issue](https://github.com/SchwartzKamel/azure-openai-cli/issues/new).

## Code of Conduct

This project follows the [Contributor Covenant Code of Conduct](CODE_OF_CONDUCT.md). By participating, you are expected to uphold this code. Please report unacceptable behavior by opening an issue.

## Questions?

Not sure where to start? Open an issue with your question — we're happy to help!

## First Contribution

New here? Welcome. Here's the fastest path from clone to merged PR.

### 1. Set up your environment

```bash
git clone https://github.com/SchwartzKamel/azure-openai-cli.git
cd azure-openai-cli
make setup   # restores dependencies and preps local tooling
make test    # runs the full test suite
```

If `make test` is green, you're good. If it isn't, that's already a valuable
bug report — open an issue with your OS and the output.

### 2. Find something small

Look for issues labeled [`good-first-issue`](https://github.com/SchwartzKamel/azure-openai-cli/labels/good-first-issue).
These are scoped deliberately small: a docs fix, a single-function change, a
clear test to add. Comment on the issue saying you're picking it up so no one
duplicates your work.

Nothing on the board catches your eye? Small PRs we always welcome:

- Typo and grammar fixes in docs
- A missing `--help` example
- An extra test for an existing behavior
- A clearer error message

### 3. Ship the small PR

```bash
git checkout -b fix/short-description
# ...make your change...
make test
git commit -m "docs: clarify chat --stream flag behavior"
git push origin fix/short-description
```

Open the PR. Fill in the template. `make test` should pass in CI. A
maintainer will review — we try to respond within a few days. Ping the PR
if a week goes by; we don't mind the nudge.

### A note on AI assistance

Using Copilot, Claude, or similar to help write code is fine and
encouraged — just disclose it. Add a `Co-authored-by:` trailer to your
commit message so the assistant shows up as a co-author:

```
Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>
```

Don't be shy. We'd rather have a rough first PR from you than a polished
one that never leaves your laptop.

## Labels

We use a small, boring set of labels. If you're browsing issues, these are
the ones that matter:

| Label | Meaning |
| --- | --- |
| `good-first-issue` | Open, well-scoped, ideal for newcomers. Start here. |
| `help-wanted` | We'd love help, but the scope assumes some project familiarity. |
| `needs-triage` | New, awaiting maintainer review. Auto-applied by issue forms. |
| `bug` | Confirmed defect or regression. |
| `enhancement` | Feature request or improvement. |
| `question` | A usage or design question; often ends up in Discussions. |
| `docs` | Documentation-only change. |
| `security` | Security-sensitive. Prefer [Security Advisories](https://github.com/SchwartzKamel/azure-openai-cli/security/advisories/new) for vulnerabilities. |

Maintainers apply labels during triage. If you think a label is wrong,
say so on the issue — we're not precious about it.
