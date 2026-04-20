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
  # Windows / unknown — attempt arm64 detection via PROCESSOR_ARCHITECTURE
  ifeq ($(PROCESSOR_ARCHITECTURE),ARM64)
    RID := win-arm64
  else
    RID := win-x64
  endif
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

.PHONY: all build dotnet-build run clean alias scan test integration-test docker-test smoke-test check help lint format format-check audit all-tests preflight publish publish-fast publish-aot publish-r2r setup \
	publish-linux-x64 publish-linux-musl-x64 publish-linux-arm64 \
	publish-osx-x64 publish-osx-arm64 \
	publish-win-x64 publish-win-arm64 \
	publish-all bench install uninstall \
	install-nim-gemma-2b uninstall-nim-gemma-2b nim-status nim-warmup

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
	@echo "  make publish-aot  - Publish Native AOT binary (RECOMMENDED, ~5ms startup, host RID)"
	@echo "  make publish      - Alias for publish-aot (the new default)"
	@echo ""
	@echo "Per-OS cross-builds (portable ReadyToRun, dist/<rid>/):"
	@echo "  make publish-linux-x64       - Linux glibc x64 (WSL/Ubuntu/Debian/Fedora)"
	@echo "  make publish-linux-musl-x64  - Linux musl x64 (Alpine)"
	@echo "  make publish-linux-arm64     - Linux ARM64 (Raspberry Pi, ARM servers)"
	@echo "  make publish-osx-x64         - macOS Intel"
	@echo "  make publish-osx-arm64       - macOS Apple Silicon (M1/M2/M3)"
	@echo "  make publish-win-x64         - Windows x64"
	@echo "  make publish-win-arm64       - Windows ARM64"
	@echo "  make publish-all             - Build all 7 cross-platform binaries"
	@echo ""
	@echo "Native-install & benchmark (drop Docker for speed — ideal for Espanso/AHK):"
	@echo "  make install      - Install host-AOT binary to ~/.local/bin/az-ai (Linux/macOS/WSL)"
	@echo "  make uninstall    - Remove ~/.local/bin/az-ai"
	@echo "  make bench        - Measure cold-start time of dist/aot/$(BIN_NAME) (10 runs)"
	@echo ""
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

## Lint: alias for format-check (single source of truth)
lint: format-check

## Dotnet-build: compile the solution in Release (no Docker, fast preflight gate)
dotnet-build:
	$(DOTNET) build azure-openai-cli.sln -c Release

## Check: compile and verify the project builds successfully (Docker image)
## .dockerignore already excludes .env, so no env-file dance is needed.
check: build

## Format: auto-format code
format:
	$(DOTNET) format azure-openai-cli.sln

## Format-check: check formatting without changing files
format-check:
	$(DOTNET) format --verify-no-changes azure-openai-cli.sln

## Preflight: format-check + dotnet-build + test + integration (skill: .github/skills/preflight.md)
## Uses `dotnet-build` (not `build`) — Docker rebuilds are too slow for a pre-commit gate.
preflight: format-check dotnet-build test integration-test
	@echo "[preflight] all gates green — safe to commit"

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

# ─────────────────────────────────────────────────────────────────────────────
# Per-OS cross-builds (portable ReadyToRun self-contained, dist/<rid>/)
#
# Why R2R, not AOT, for cross-builds?
#   Native AOT compilation is host-constrained:
#     - Linux host: can AOT-build linux-x64, linux-arm64 only
#     - macOS host: can AOT-build osx-x64, osx-arm64 only
#     - Windows host: can AOT-build win-x64, win-arm64 only
#   Cross-OS AOT (e.g. Linux → macOS) is not supported by .NET 10.
#   ReadyToRun is fully portable: build any RID from any host.
#
# Want maximum speed on YOUR OS? Use `make publish-aot` on each platform.
# Need ONE command to build everything (e.g. for a release)? Use `publish-all`.
# ─────────────────────────────────────────────────────────────────────────────

_publish_rid = \
	$(DOTNET) publish azureopenai-cli/AzureOpenAI_CLI.csproj \
		-c Release -r $(1) --self-contained \
		-p:PublishReadyToRun=true -p:PublishSingleFile=true \
		-p:IncludeNativeLibrariesForSelfExtract=true \
		-o dist/$(1)/

publish-linux-x64:
	@echo ">> Publishing linux-x64 (ReadyToRun, self-contained) to dist/linux-x64/"
	@$(call _publish_rid,linux-x64)
	@ls -lh dist/linux-x64/AzureOpenAI_CLI

publish-linux-musl-x64:
	@echo ">> Publishing linux-musl-x64 (Alpine) to dist/linux-musl-x64/"
	@$(call _publish_rid,linux-musl-x64)
	@ls -lh dist/linux-musl-x64/AzureOpenAI_CLI

publish-linux-arm64:
	@echo ">> Publishing linux-arm64 to dist/linux-arm64/"
	@$(call _publish_rid,linux-arm64)
	@ls -lh dist/linux-arm64/AzureOpenAI_CLI

publish-osx-x64:
	@echo ">> Publishing osx-x64 (macOS Intel) to dist/osx-x64/"
	@$(call _publish_rid,osx-x64)
	@ls -lh dist/osx-x64/AzureOpenAI_CLI

publish-osx-arm64:
	@echo ">> Publishing osx-arm64 (Apple Silicon) to dist/osx-arm64/"
	@$(call _publish_rid,osx-arm64)
	@ls -lh dist/osx-arm64/AzureOpenAI_CLI

publish-win-x64:
	@echo ">> Publishing win-x64 to dist/win-x64/"
	@$(call _publish_rid,win-x64)
	@ls -lh dist/win-x64/AzureOpenAI_CLI.exe

publish-win-arm64:
	@echo ">> Publishing win-arm64 to dist/win-arm64/"
	@$(call _publish_rid,win-arm64)
	@ls -lh dist/win-arm64/AzureOpenAI_CLI.exe

## Publish all 7 supported RIDs (~7× single-build time).
## Uses ReadyToRun because AOT is host-OS-constrained. Runs in parallel if
## invoked with `make -j7 publish-all`.
publish-all: publish-linux-x64 publish-linux-musl-x64 publish-linux-arm64 \
             publish-osx-x64 publish-osx-arm64 \
             publish-win-x64 publish-win-arm64
	@echo ""
	@echo ">> All 7 binaries built under dist/<rid>/"
	@du -sh dist/*/

# ─────────────────────────────────────────────────────────────────────────────
# Native install & benchmark (drop Docker — ideal for Espanso/AHK workflows)
# ─────────────────────────────────────────────────────────────────────────────

PREFIX ?= $(HOME)/.local
INSTALL_BIN := $(PREFIX)/bin/az-ai

## Install host-AOT binary to ~/.local/bin/az-ai (Linux/macOS/WSL).
## Requires `make publish-aot` to have been run first.
## Add ~/.local/bin to PATH if it isn't already (most distros do this).
install: dist/aot/$(BIN_NAME)
	@mkdir -p $(PREFIX)/bin
	@cp dist/aot/$(BIN_NAME) $(INSTALL_BIN)
	@chmod +x $(INSTALL_BIN)
	@echo ">> Installed to $(INSTALL_BIN)"
	@echo "   Invoke with: az-ai \"your prompt\""
	@echo "   Espanso:     command: [\"az-ai\", \"--raw\", \"{{prompt}}\"]"

## If the host-AOT binary doesn't exist, build it first.
dist/aot/$(BIN_NAME):
	@$(MAKE) publish-aot

uninstall:
	@rm -f $(INSTALL_BIN)
	@echo ">> Removed $(INSTALL_BIN)"

## Measure cold-start of the host-AOT binary (10 runs, after 2 warm-ups).
## Requires python3 on PATH (which it is on Linux, macOS, WSL, and Git Bash).
bench: dist/aot/$(BIN_NAME)
	@command -v python3 >/dev/null 2>&1 || { echo "Error: python3 required for bench"; exit 1; }
	@python3 scripts/bench.py dist/aot/$(BIN_NAME) --args --version

# ─────────────────────────────────────────────────────────────────────────────
# NVIDIA NIM (local Gemma-4-2B-NVFP4) — see docs/nim-setup.md, ADR-006
# ─────────────────────────────────────────────────────────────────────────────

install-nim-gemma-2b: ## Install and warm-start NIM with Gemma-4-2B-NVFP4
	@bash scripts/install-nim-gemma-2b.sh $(ARGS)

uninstall-nim-gemma-2b: ## Stop and remove the NIM daemon
	@bash scripts/uninstall-nim-gemma-2b.sh $(ARGS)

nim-status: ## Show NIM daemon status and health
	@systemctl --user status az-ai-nim.service --no-pager || true
	@echo ""
	@echo ">> /v1/health/ready:"
	@curl -sS -o /dev/stdout -w "\nHTTP %{http_code}\n" http://localhost:8000/v1/health/ready || echo "(unreachable)"

nim-warmup: ## Block until NIM is warm
	@deadline=$$(( $$(date +%s) + 120 )); \
	while [ $$(date +%s) -lt $$deadline ]; do \
	  code=$$(curl -s -o /dev/null -w '%{http_code}' http://localhost:8000/v1/health/ready || true); \
	  if [ "$$code" = "200" ]; then echo ">> NIM ready"; exit 0; fi; \
	  printf '.'; sleep 2; \
	done; \
	echo ""; echo ">> timeout waiting for NIM"; exit 1
