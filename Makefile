IMAGE_NAME := azureopenai-cli
IMAGE_TAG := sealed
FULL_IMAGE := $(IMAGE_NAME):$(IMAGE_TAG)
DOCKERFILE := Dockerfile
BUILD_CTX := azureopenai-cli

DOCKER_CMD := docker run --rm $(FULL_IMAGE)

.PHONY: all build run clean alias scan test

all: build

## Build: standard docker build
build: clean
	@echo ">> Building $(FULL_IMAGE)"
	@docker buildx build \
-t $(FULL_IMAGE) \
		-f $(DOCKERFILE) .

## Run: make run ARGS="your prompt here"
run:
	@if [ -z "$(shell docker images -q $(FULL_IMAGE) 2>/dev/null)" ]; then \
		echo "Error: Container image $(FULL_IMAGE) not found. Run 'make build' first."; \
		exit 1; \
	fi
	@docker run --rm \
		$(FULL_IMAGE) $(ARGS)

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
test: build
	make run ARGS="Tell me some unusual facts about cats"