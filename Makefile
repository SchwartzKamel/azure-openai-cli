IMAGE_NAME := azureopenai-cli
IMAGE_TAG := gpt-5-chat
AGENTIC_TAG := 4.1-mini
FULL_IMAGE := $(IMAGE_NAME):$(IMAGE_TAG)
DOCKERFILE := Dockerfile
BUILD_CTX := azureopenai-cli

DOCKER_CMD := docker run --rm --env-file .env $(FULL_IMAGE)

.PHONY: all build run clean alias scan test smoke-test check help lint

## Help: list available make targets (default target)
help:
	@echo "Available targets:"
	@echo "  make build       - Build the Docker image"
	@echo "  make run         - Run the CLI (requires .env file). Use ARGS=\"your prompt\""
	@echo "  make clean       - Remove build artifacts and dangling images"
	@echo "  make alias       - Install 'az-ai' shell alias"
	@echo "  make scan        - Run vulnerability scan with grype"
	@echo "  make test        - Run unit tests (xUnit)"
	@echo "  make lint        - Check code formatting (for CI)"
	@echo "  make smoke-test  - Clean, build, and run a test prompt via Docker"
	@echo "  make check       - Verify the project builds successfully"
	@echo "  make help        - Show this help message"

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
		grep -v 'mcr.microsoft.com/dotnet/sdk:9.0' | \
		grep -v 'mcr.microsoft.com/dotnet/runtime-deps:9.0-alpine' | \
		grep -Ev "$(IMAGE_NAME):(gpt-5-chat|4.1-mini)" | \
		awk '{print $$1}' | xargs -r docker rmi -f || true
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
	grype $(FULL_IMAGE)

## Run unit tests
test: ## Run unit tests
	dotnet test tests/AzureOpenAI_CLI.Tests/AzureOpenAI_CLI.Tests.csproj --verbosity minimal

## Smoke test: clean, build, and run a test prompt via Docker
smoke-test: clean build
	make run ARGS="Tell me some unusual facts about cats"

## Lint: check code formatting (for CI)
lint:
	dotnet format --verify-no-changes azure-openai-cli.sln

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
