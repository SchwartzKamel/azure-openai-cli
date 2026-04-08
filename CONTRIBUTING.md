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
