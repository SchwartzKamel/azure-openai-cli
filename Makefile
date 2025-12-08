IMAGE_NAME := azureopenai-cli
# Use the current git branch as the default image tag (sanitized)
BUILD_TAG := $(shell git rev-parse --abbrev-ref HEAD 2>/dev/null | sed 's/[^a-zA-Z0-9._-]/_/g')
# Allow overriding IMAGE_TAG externally but default to current branch name
IMAGE_TAG ?= $(BUILD_TAG)
FULL_IMAGE := $(IMAGE_NAME):$(IMAGE_TAG)
DOCKERFILE := Dockerfile
BUILD_CTX := azureopenai-cli

DOCKER_CMD := docker run --rm $(FULL_IMAGE)

.PHONY: all build run clean alias scan test test-docker-optimization

all: build

## Build: standard docker build
## Build: standard docker build
build:
	@echo ">> Building $(FULL_IMAGE)"
	@docker buildx build -t $(FULL_IMAGE) -f $(DOCKERFILE) $(BUILD_CTX)

## Run: make run ARGS="your prompt here"
run:
	@if [ -z "$(shell docker images -q $(FULL_IMAGE) 2>/dev/null)" ]; then \
		echo "Error: Container image $(FULL_IMAGE) not found. Run 'make build' first."; \
		exit 1; \
	fi
	@ENVFILE="$(PWD)/$(BUILD_CTX)/.env"; \
	if [ -f "$$ENVFILE" ]; then \
		DOCKENV="--env-file=$$ENVFILE"; \
	else \
		DOCKENV=""; \
	fi; \
	@docker run --rm $$DOCKENV $(FULL_IMAGE) $(ARGS)

## Run natively (local dev)
run-local:
	@dotnet run --project $(BUILD_CTX) -- $(ARGS)

## Publish locally (single-file)
build-local:
	@dotnet publish $(BUILD_CTX) -c Release -r linux-x64 --self-contained true /p:PublishSingleFile=true /p:PublishTrimmed=true -o ./publish

## Clean dangling images
clean:
	@echo ">> Removing old build artifacts..."
	@find $(BUILD_CTX) -type d \( -name 'bin' -o -name 'obj' \) -exec rm -rf {} +
	@echo ">> Cleaning up old Docker images/containers except required .NET SDK/runtime-deps..."
	@docker container prune -f
	@docker image prune -f --filter "dangling=true"
	@docker images --format '{{.ID}} {{.Repository}}:{{.Tag}}' | \
		grep -v 'mcr.microsoft.com/dotnet/sdk:9.0-preview' | \
		grep -v 'mcr.microsoft.com/dotnet/runtime-deps:9.0-preview-alpine' | \
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


## Test with a question about cats
test: clean build
	make run ARGS="Tell me some unusual facts about cats"

## Run Docker image optimization tests
test-docker-optimization:
	@echo ">> Running Docker image optimization tests..."
	@if command -v bats >/dev/null 2>&1; then \
		bats tests/docker-image-optimization.bats; \
	else \
		./tests/docker-image-optimization.sh; \
	fi

help:
	@echo "Targets: build, run, run-local, build-local, test, clean, alias, scan, test-docker-optimization"