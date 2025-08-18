IMAGE_NAME := azureopenai-cli
IMAGE_TAG := sealed
DOCKERFILE := Dockerfile
BUILD_CTX := azureopenai-cli

DOCKER_CMD := docker run --rm $(IMAGE_NAME):$(IMAGE_TAG)

.PHONY: all build run clean alias scan

all: build

## Build: standard docker build
build:
	@echo ">> Building $(IMAGE_NAME):$(IMAGE_TAG)"
	@docker buildx build \
        -t $(IMAGE_NAME):$(IMAGE_TAG) \
		-f $(DOCKERFILE) .

## Run: make run ARGS="your prompt here"
run:
	@if [ -z "$(shell docker images -q $(IMAGE_NAME):$(IMAGE_TAG) 2>/dev/null)" ]; then \
		echo "Error: Container image $(IMAGE_NAME):$(IMAGE_TAG) not found. Run 'make build' first."; \
		exit 1; \
	fi
	@docker run --rm \
		$(IMAGE_NAME):$(IMAGE_TAG) $(ARGS)

## Clean dangling images
clean:
	@echo ">> Removing old build artifacts..."
	@find $(BUILD_CTX) -type d \( -name 'bin' -o -name 'obj' \) -exec rm -rf {} +
	@echo ">> Cleaning up old Docker images/containers except required .NET SDK/runtime-deps..."
	@docker container prune -f
	@docker image prune -a -f --filter "dangling=true"
	@docker images -q | xargs -r docker inspect --format '{{.Id}} {{.RepoTags}}' | \
		grep -v 'mcr.microsoft.com/dotnet/sdk:9.0-preview' | \
		grep -v 'mcr.microsoft.com/dotnet/runtime-deps:9.0-preview-alpine' | \
		awk '{print $$1}' | xargs -r docker rmi -f
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
grype $(IMAGE_NAME): $(IMAGE_TAG)
