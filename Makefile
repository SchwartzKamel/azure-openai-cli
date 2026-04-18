SHELL := /bin/bash

# Auto-detect Runtime Identifier (RID) for cross-platform publish
UNAME_S := $(shell uname -s 2>/dev/null || echo Windows)
UNAME_M := $(shell uname -m)
ifeq ($(UNAME_S),Linux)
  ifeq ($(UNAME_M),aarch64)
    RID := linux-arm64
  else
    RID := linux-x64
  endif
else ifeq ($(UNAME_S),Darwin)
  ifeq ($(UNAME_M),arm64)
    RID := osx-arm64
  else
    RID := osx-x64
  endif
else
  RID := win-x64
endif

# Binary name varies by platform (.exe on Windows)
ifeq ($(findstring win,$(RID)),win)
  BIN_EXT := .exe
else
  BIN_EXT :=
endif
BIN_NAME := AzureOpenAI_CLI$(BIN_EXT)

IMAGE_NAME := azureopenai-cli
IMAGE_TAG := gpt-5-chat
AGENTIC_TAG := 4.1-mini
FULL_IMAGE := $(IMAGE_NAME):$(IMAGE_TAG)
DOCKERFILE := Dockerfile
BUILD_CTX := azureopenai-cli

DOCKER_CMD := docker run --rm --env-file .env $(FULL_IMAGE)

# Resolve dotnet: prefer PATH, fall back to ~/.dotnet/dotnet (installed by setup.sh)
DOTNET := $(shell command -v dotnet 2>/dev/null || echo "$$HOME/.dotnet/dotnet")

.DEFAULT_GOAL := help

.PHONY: all build run clean alias scan test integration-test docker-test smoke-test check help lint format format-check audit all-tests publish publish-fast publish-aot publish-r2r setup

## Help: list available make targets (default target)
help:
	@echo "Available targets:"
	@echo "  make setup       - Install prerequisites (.NET 10 SDK, Docker, tools)"
	@echo "  make build       - Build the Docker image"
	@echo "  make run         - Run the CLI (requires .env file). Use ARGS=\"your prompt\""
	@echo "  make clean       - Remove build artifacts and dangling images"
	@echo "  make alias       - Install 'az-ai' shell alias"
	@echo "  make scan        - Run vulnerability scan with grype"
	@echo "  make test        - Run unit tests (xUnit)"
	@echo "  make integration-test - Run end-to-end integration tests"
	@echo "  make docker-test - Validate Dockerfile best practices"
	@echo "  make lint        - Check code formatting (for CI)"
	@echo "  make smoke-test  - Clean, build, and run a test prompt via Docker"
	@echo "  make format      - Auto-format code"
	@echo "  make format-check - Check formatting without changes"
	@echo "  make audit       - Check for vulnerable NuGet packages"
	@echo "  make all-tests   - Run unit + integration + docker tests"
	@echo "  make publish-fast - Publish self-contained ReadyToRun binary (~100ms startup)"
	@echo "  make publish-r2r  - Alias for publish-fast"
	@echo "  make publish-aot  - Publish Native AOT binary (RECOMMENDED, ~11ms startup)"
	@echo "  make publish      - Alias for publish-aot (the new default)"
	@echo "  make check       - Verify the project builds successfully"
	@echo "  make help        - Show this help message"
	@echo ""
	@echo "  Detected platform: $(RID)"

all: build

## Build: standard docker build
build:
	@echo ">> Building $(FULL_IMAGE)"
	@docker buildx build \
		-t $(FULL_IMAGE) \
		--label preserve=true \
		-f $(DOCKERFILE) .

## Run: make run ARGS="your prompt here" (requires .env file for credentials)
run:
	@if [ ! -f .env ]; then \
		echo "Error: .env file not found. Create one from .env.example with your Azure credentials."; \
		exit 1; \
	fi
	@if [ -z "$(shell docker images -q $(FULL_IMAGE) 2>/dev/null)" ]; then \
		echo "Error: Container image $(FULL_IMAGE) not found. Run 'make build' first."; \
		exit 1; \
	fi
	@docker run --rm --env-file .env \
		$(FULL_IMAGE) $(ARGS)

## Clean dangling images
clean:
	@echo ">> Removing old build artifacts..."
	@find $(BUILD_CTX) -type d \( -name 'bin' -o -name 'obj' \) -exec rm -rf {} +
	@docker container prune -f
	@docker image prune -f --filter "dangling=true" --filter "label!=preserve=true"
	@docker images --format '{{.ID}} {{.Repository}}:{{.Tag}}' | \
		grep -v 'mcr.microsoft.com/dotnet/sdk:10.0' | \
		grep -v 'mcr.microsoft.com/dotnet/runtime-deps:10.0-alpine' | \
		grep -Ev "$(IMAGE_NAME):(gpt-5-chat|4.1-mini)" | \
		awk '{print $$1}' | xargs docker rmi -f 2>/dev/null || true
	@docker builder prune -f

## Install alias permanently in shell profile
alias:
	@case "$$SHELL" in \
		*/zsh) RCFILE=$$HOME/.zshrc ;; \
		*/bash) RCFILE=$$HOME/.bashrc ;; \
		*) RCFILE=$$HOME/.profile ;; \
	esac; \
	echo "alias az-ai='$(DOCKER_CMD)'" >> $$RCFILE; \
	echo "Alias 'az-ai' added to $$RCFILE"

## Run a vulnerability assessment of the compiled image
scan:
	@command -v grype >/dev/null 2>&1 || { echo "Error: grype not found. Install: https://github.com/anchore/grype"; exit 1; }
	grype $(FULL_IMAGE)

## Setup: install prerequisites (.NET 10, Docker, tools)
setup:
	@bash scripts/setup.sh

## Run unit tests
test: ## Run unit tests
	$(DOTNET) test tests/AzureOpenAI_CLI.Tests/AzureOpenAI_CLI.Tests.csproj --verbosity minimal

## Run integration tests (end-to-end, uses dotnet run + Docker)
integration-test: ## Run integration tests
	bash tests/integration_tests.sh

docker-test: ## Validate Dockerfile best practices
	bash tests/docker-image-optimization.sh

## Smoke test: clean, build, and run a test prompt via Docker
smoke-test: clean build
	make run ARGS="Tell me some unusual facts about cats"

## Lint: check code formatting (for CI)
lint:
	$(DOTNET) format --verify-no-changes azure-openai-cli.sln

## Check: compile and verify the project builds successfully
check:
	@CLEANUP_ENV=0; \
	if [ ! -f azureopenai-cli/.env ]; then \
		echo ">> Creating placeholder .env for build verification..."; \
		cp azureopenai-cli/.env.example azureopenai-cli/.env; \
		CLEANUP_ENV=1; \
	fi; \
	$(MAKE) build; \
	BUILD_RC=$$?; \
	if [ "$$CLEANUP_ENV" = "1" ] && [ -f azureopenai-cli/.env ]; then \
		rm azureopenai-cli/.env; \
	fi; \
	exit $$BUILD_RC

## Format: auto-format code
format:
	$(DOTNET) format azure-openai-cli.sln

## Format-check: check formatting without changing files
format-check:
	$(DOTNET) format --verify-no-changes azure-openai-cli.sln

## Audit: check for vulnerable NuGet packages
audit:
	$(DOTNET) list package --vulnerable --include-transitive

## All-tests: run unit tests + integration tests + docker tests sequentially
all-tests: test integration-test docker-test

## Publish self-contained binary with ReadyToRun (pre-JIT, ~100ms startup).
## Kept for compatibility; prefer `publish-aot` for new installs — Native AOT is
## ~9× faster to start (critical for Espanso / AutoHotkey text-injection
## workflows where each invocation pays the startup cost).
publish-fast:
	$(DOTNET) publish azureopenai-cli/AzureOpenAI_CLI.csproj -c Release -r $(RID) --self-contained -p:PublishReadyToRun=true -o dist/
	@echo "Published ReadyToRun binary to dist/$(BIN_NAME)"
	@ls -lh dist/$(BIN_NAME)

## Alias for publish-fast (ReadyToRun)
publish-r2r: publish-fast

## Publish Native AOT binary — RECOMMENDED.
## Single-file, ~9 MB, ~11 ms cold-start on Linux x64 (vs ~100 ms for JIT).
## That 9× win matters a lot for Espanso/AutoHotkey text-injection triggers,
## where each key sequence spawns a fresh process. All app-level IL2026/IL3050
## trim/AOT warnings have been fixed via System.Text.Json source generation
## (see azureopenai-cli/JsonGenerationContext.cs). The only remaining warnings
## come from third-party assemblies (Azure.AI.OpenAI, OpenAI) and do not affect
## runtime behavior.
## See: https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/
publish-aot:
	$(DOTNET) publish azureopenai-cli/AzureOpenAI_CLI.csproj -c Release -r $(RID) -p:PublishAot=true -o dist/aot/
	@echo "Published Native AOT binary to dist/aot/$(BIN_NAME) (~11ms startup)"
	@ls -lh dist/aot/$(BIN_NAME)

## Default `make publish` now builds the AOT binary.
publish: publish-aot
