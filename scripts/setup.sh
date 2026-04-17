#!/usr/bin/env bash
# -------------------------------------------------------------------
# setup.sh — Prerequisite installer for Azure OpenAI CLI
#
# Installs .NET 10 SDK, Docker (optional), clipboard tools, and jq.
# Idempotent, non-destructive, and safe to run multiple times.
#
# Usage:
#   bash scripts/setup.sh              # interactive install
#   bash scripts/setup.sh --skip-docker  # skip Docker install
#   bash scripts/setup.sh --help         # show help
# -------------------------------------------------------------------
set -euo pipefail

# ── Colors ──────────────────────────────────────────────────────────
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
CYAN='\033[0;36m'
BOLD='\033[1m'
NC='\033[0m'

info()    { echo -e "${BLUE}[INFO]${NC} $*"; }
success() { echo -e "${GREEN}[✓]${NC} $*"; }
warn()    { echo -e "${YELLOW}[!]${NC} $*"; }
error()   { echo -e "${RED}[✗]${NC} $*"; }
header()  { echo -e "\n${CYAN}${BOLD}── $* ──${NC}"; }

# ── Flags ───────────────────────────────────────────────────────────
SKIP_DOCKER=false

show_help() {
    cat <<EOF
${BOLD}Azure OpenAI CLI — Setup Script${NC}

Usage: bash scripts/setup.sh [OPTIONS]

Options:
  --skip-docker   Skip Docker installation prompt
  --help          Show this help message

What it does:
  1. Detects your OS (Linux, macOS, WSL)
  2. Installs .NET 10 SDK via Microsoft's dotnet-install.sh
  3. Optionally installs Docker Engine
  4. Optionally installs clipboard tools (xclip/xsel) and jq
  5. Creates a template .env file if not present
  6. Verifies all installations
EOF
    exit 0
}

for arg in "$@"; do
    case "$arg" in
        --skip-docker) SKIP_DOCKER=true ;;
        --help|-h)     show_help ;;
        *)             error "Unknown option: $arg"; show_help ;;
    esac
done

# ── Globals ─────────────────────────────────────────────────────────
DOTNET_CHANNEL="10.0"
DOTNET_INSTALL_DIR="$HOME/.dotnet"
REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

# ── OS Detection ────────────────────────────────────────────────────
detect_os() {
    header "Detecting Operating System"

    OS_TYPE="unknown"
    DISTRO="unknown"
    IS_WSL=false
    PKG_MANAGER="unknown"

    if [[ "$OSTYPE" == "darwin"* ]]; then
        OS_TYPE="macos"
        DISTRO="macos"
        PKG_MANAGER="brew"
        success "Detected macOS"
        return
    fi

    if [[ "$OSTYPE" == "linux-gnu"* ]] || [[ "$OSTYPE" == "linux"* ]]; then
        OS_TYPE="linux"

        # Check for WSL
        if [ -f /proc/version ] && grep -qiE "(microsoft|wsl)" /proc/version 2>/dev/null; then
            IS_WSL=true
            info "Running inside WSL"
        fi

        # Detect distro
        if [ -f /etc/os-release ]; then
            # shellcheck disable=SC1091
            . /etc/os-release
            case "$ID" in
                ubuntu|debian|linuxmint|pop)
                    DISTRO="debian"
                    PKG_MANAGER="apt"
                    ;;
                fedora|rhel|centos|rocky|alma)
                    DISTRO="fedora"
                    if command -v dnf &>/dev/null; then
                        PKG_MANAGER="dnf"
                    else
                        PKG_MANAGER="yum"
                    fi
                    ;;
                arch|manjaro|endeavouros)
                    DISTRO="arch"
                    PKG_MANAGER="pacman"
                    ;;
                alpine)
                    DISTRO="alpine"
                    PKG_MANAGER="apk"
                    ;;
                *)
                    warn "Unrecognized distro: $ID — will attempt generic install"
                    DISTRO="$ID"
                    ;;
            esac
        fi

        success "Detected Linux ($DISTRO)"
        return
    fi

    error "Unsupported operating system: $OSTYPE"
    error "This script supports Linux (Ubuntu/Debian, Fedora/RHEL, Arch, Alpine) and macOS."
    exit 1
}

# ── Helper: prompt yes/no ───────────────────────────────────────────
confirm() {
    local prompt="$1"
    local default="${2:-n}"
    local reply

    if [[ "$default" == "y" ]]; then
        prompt="$prompt [Y/n] "
    else
        prompt="$prompt [y/N] "
    fi

    read -r -p "$prompt" reply
    reply="${reply:-$default}"
    [[ "$reply" =~ ^[Yy]$ ]]
}

# ── Helper: install a package via the detected package manager ──────
pkg_install() {
    local pkg="$1"
    info "Installing $pkg via $PKG_MANAGER..."
    case "$PKG_MANAGER" in
        apt)    sudo apt-get update -qq && sudo apt-get install -y -qq "$pkg" ;;
        dnf)    sudo dnf install -y -q "$pkg" ;;
        yum)    sudo yum install -y -q "$pkg" ;;
        pacman) sudo pacman -Sy --noconfirm "$pkg" ;;
        apk)    sudo apk add --quiet "$pkg" ;;
        brew)   brew install "$pkg" ;;
        *)      error "Cannot install $pkg — unknown package manager: $PKG_MANAGER"; return 1 ;;
    esac
}

# ── Install .NET 10 SDK ─────────────────────────────────────────────
install_dotnet() {
    header "Installing .NET $DOTNET_CHANNEL SDK"

    # Check if dotnet is already installed with correct version
    local dotnet_cmd=""
    if command -v dotnet &>/dev/null; then
        dotnet_cmd="dotnet"
    elif [ -x "$DOTNET_INSTALL_DIR/dotnet" ]; then
        dotnet_cmd="$DOTNET_INSTALL_DIR/dotnet"
    fi

    if [ -n "$dotnet_cmd" ]; then
        local current_version
        current_version=$("$dotnet_cmd" --version 2>/dev/null || echo "")
        if [[ "$current_version" == 10.* ]]; then
            success ".NET SDK $current_version is already installed"
            return 0
        else
            info "Found .NET $current_version — upgrading to $DOTNET_CHANNEL"
        fi
    fi

    # Download and run the official installer
    local install_script
    install_script=$(mktemp)
    info "Downloading dotnet-install.sh from Microsoft..."
    if ! curl -fsSL https://dot.net/v1/dotnet-install.sh -o "$install_script"; then
        error "Failed to download dotnet-install.sh"
        error "Check your internet connection and try again."
        rm -f "$install_script"
        exit 1
    fi
    chmod +x "$install_script"

    info "Installing .NET $DOTNET_CHANNEL SDK to $DOTNET_INSTALL_DIR..."
    if ! bash "$install_script" --channel "$DOTNET_CHANNEL" --install-dir "$DOTNET_INSTALL_DIR"; then
        error "dotnet-install.sh failed"
        error "Try running manually: bash $install_script --channel $DOTNET_CHANNEL --verbose"
        rm -f "$install_script"
        exit 1
    fi

    rm -f "$install_script"
    success ".NET SDK installed to $DOTNET_INSTALL_DIR"
}

# ── Configure PATH for .NET ─────────────────────────────────────────
configure_dotnet_path() {
    header "Configuring PATH"

    # Add to current session
    export PATH="$DOTNET_INSTALL_DIR:$DOTNET_INSTALL_DIR/tools:$PATH"
    export DOTNET_ROOT="$DOTNET_INSTALL_DIR"

    # Detect shell config file
    local shell_rc=""
    case "${SHELL:-/bin/bash}" in
        */zsh)  shell_rc="$HOME/.zshrc" ;;
        */bash) shell_rc="$HOME/.bashrc" ;;
        *)      shell_rc="$HOME/.profile" ;;
    esac

    local dotnet_path_line="export PATH=\"\$HOME/.dotnet:\$HOME/.dotnet/tools:\$PATH\""
    local dotnet_root_line="export DOTNET_ROOT=\"\$HOME/.dotnet\""

    if [ -f "$shell_rc" ] && grep -qF '.dotnet' "$shell_rc"; then
        success "PATH already configured in $shell_rc"
    else
        info "Adding .NET to PATH in $shell_rc"
        {
            echo ""
            echo "# .NET SDK (added by azure-openai-cli setup)"
            echo "$dotnet_root_line"
            echo "$dotnet_path_line"
        } >> "$shell_rc"
        success "Added .NET to PATH in $shell_rc"
        warn "Run 'source $shell_rc' or open a new terminal to apply"
    fi
}

# ── Install Docker ──────────────────────────────────────────────────
install_docker() {
    header "Docker"

    if command -v docker &>/dev/null; then
        local docker_ver
        docker_ver=$(docker --version 2>/dev/null || echo "unknown")
        success "Docker is already installed: $docker_ver"
        return 0
    fi

    if [ "$SKIP_DOCKER" = true ]; then
        info "Skipping Docker installation (--skip-docker)"
        return 0
    fi

    if ! confirm "Docker is not installed. Install Docker Engine?"; then
        info "Skipping Docker — you can install it later from https://docs.docker.com/engine/install/"
        return 0
    fi

    case "$OS_TYPE" in
        macos)
            if command -v brew &>/dev/null; then
                info "Installing Docker via Homebrew cask..."
                brew install --cask docker
                success "Docker Desktop installed — launch it from Applications"
            else
                warn "Install Docker Desktop from https://www.docker.com/products/docker-desktop/"
            fi
            ;;
        linux)
            if [ "$IS_WSL" = true ]; then
                warn "In WSL, install Docker Desktop on Windows and enable WSL integration:"
                warn "  https://docs.docker.com/desktop/wsl/"
                return 0
            fi

            case "$DISTRO" in
                debian)
                    info "Installing Docker via official apt repository..."
                    sudo apt-get update -qq
                    sudo apt-get install -y -qq ca-certificates curl gnupg
                    sudo install -m 0755 -d /etc/apt/keyrings

                    # Add Docker's official GPG key
                    local distro_id
                    distro_id=$(. /etc/os-release && echo "$ID")
                    curl -fsSL "https://download.docker.com/linux/$distro_id/gpg" | \
                        sudo gpg --dearmor -o /etc/apt/keyrings/docker.gpg 2>/dev/null || true
                    sudo chmod a+r /etc/apt/keyrings/docker.gpg

                    # Set up the repository
                    local arch
                    arch=$(dpkg --print-architecture)
                    local codename
                    codename=$(. /etc/os-release && echo "$VERSION_CODENAME")
                    echo "deb [arch=$arch signed-by=/etc/apt/keyrings/docker.gpg] \
https://download.docker.com/linux/$distro_id $codename stable" | \
                        sudo tee /etc/apt/sources.list.d/docker.list > /dev/null

                    sudo apt-get update -qq
                    sudo apt-get install -y -qq docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin

                    # Add user to docker group
                    if ! groups | grep -q docker; then
                        sudo usermod -aG docker "$USER"
                        warn "Added $USER to docker group — log out and back in to apply"
                    fi
                    ;;
                fedora)
                    info "Installing Docker via dnf..."
                    sudo dnf -y install dnf-plugins-core
                    sudo dnf config-manager --add-repo https://download.docker.com/linux/fedora/docker-ce.repo
                    sudo dnf install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin
                    sudo systemctl start docker
                    sudo systemctl enable docker
                    if ! groups | grep -q docker; then
                        sudo usermod -aG docker "$USER"
                        warn "Added $USER to docker group — log out and back in to apply"
                    fi
                    ;;
                arch)
                    info "Installing Docker via pacman..."
                    sudo pacman -Sy --noconfirm docker docker-buildx docker-compose
                    sudo systemctl start docker
                    sudo systemctl enable docker
                    if ! groups | grep -q docker; then
                        sudo usermod -aG docker "$USER"
                        warn "Added $USER to docker group — log out and back in to apply"
                    fi
                    ;;
                alpine)
                    info "Installing Docker via apk..."
                    sudo apk add docker docker-cli-buildx docker-cli-compose
                    sudo rc-update add docker default
                    sudo rc-service docker start
                    if ! groups | grep -q docker; then
                        sudo addgroup "$USER" docker
                        warn "Added $USER to docker group — log out and back in to apply"
                    fi
                    ;;
                *)
                    warn "Automatic Docker install not supported for $DISTRO"
                    warn "Install manually: https://docs.docker.com/engine/install/"
                    ;;
            esac
            ;;
    esac

    if command -v docker &>/dev/null; then
        success "Docker installed: $(docker --version)"
    fi
}

# ── Install optional tools ──────────────────────────────────────────
install_optional_tools() {
    header "Optional Tools"

    # ── jq ──
    if command -v jq &>/dev/null; then
        success "jq is already installed"
    else
        if confirm "Install jq (JSON parsing for --json output)?"; then
            pkg_install jq && success "jq installed" || warn "Failed to install jq"
        else
            info "Skipping jq"
        fi
    fi

    # ── Clipboard tools (Linux only) ──
    if [ "$OS_TYPE" = "linux" ]; then
        if command -v xclip &>/dev/null || command -v xsel &>/dev/null; then
            success "Clipboard tool already installed (xclip/xsel)"
        else
            if confirm "Install xclip (clipboard support for agentic mode)?"; then
                pkg_install xclip && success "xclip installed" || warn "Failed to install xclip"
            else
                info "Skipping clipboard tools"
            fi
        fi
    fi
}

# ── Create .env template ────────────────────────────────────────────
create_env_template() {
    header "Environment File"

    local env_file="$REPO_ROOT/.env"
    local env_example="$REPO_ROOT/azureopenai-cli/.env.example"

    if [ -f "$env_file" ]; then
        success ".env file already exists — not overwriting"
        return 0
    fi

    if [ -f "$env_example" ]; then
        if confirm "Create .env from template? (you'll need to add your Azure credentials)" "y"; then
            cp "$env_example" "$env_file"
            success "Created .env from template"
            warn "Edit .env and add your Azure OpenAI credentials before running the CLI"
        else
            info "Skipping .env creation"
        fi
    else
        warn "Template file not found at $env_example"
        info "Create .env manually — see README.md for required variables"
    fi
}

# ── Verify installation ─────────────────────────────────────────────
verify_installation() {
    header "Verification"

    local all_good=true

    # .NET SDK
    local dotnet_cmd=""
    if command -v dotnet &>/dev/null; then
        dotnet_cmd="dotnet"
    elif [ -x "$DOTNET_INSTALL_DIR/dotnet" ]; then
        dotnet_cmd="$DOTNET_INSTALL_DIR/dotnet"
    fi

    if [ -n "$dotnet_cmd" ]; then
        local ver
        ver=$("$dotnet_cmd" --version 2>/dev/null || echo "unknown")
        if [[ "$ver" == 10.* ]]; then
            success ".NET SDK $ver"
        else
            warn ".NET SDK $ver (expected 10.x)"
        fi
    else
        error ".NET SDK not found"
        all_good=false
    fi

    # Docker
    if command -v docker &>/dev/null; then
        success "Docker $(docker --version 2>/dev/null | sed -n 's/.*version \([^ ,]*\).*/\1/p' || echo 'installed')"
    else
        warn "Docker not installed (required for containerized mode)"
    fi

    # jq
    if command -v jq &>/dev/null; then
        success "jq $(jq --version 2>/dev/null || echo 'installed')"
    else
        info "jq not installed (optional, for --json output)"
    fi

    # Clipboard
    if command -v xclip &>/dev/null; then
        success "xclip installed"
    elif command -v xsel &>/dev/null; then
        success "xsel installed"
    elif [ "$OS_TYPE" = "linux" ]; then
        info "No clipboard tool (optional, for agentic clipboard)"
    fi

    # .env
    if [ -f "$REPO_ROOT/.env" ]; then
        success ".env file present"
    else
        warn ".env file not found — run 'cp azureopenai-cli/.env.example .env' and add credentials"
    fi

    echo ""
    if [ "$all_good" = true ]; then
        success "${BOLD}Setup complete!${NC} You're ready to build and run the CLI."
        echo ""
        info "Next steps:"
        info "  1. Edit .env with your Azure OpenAI credentials"
        info "  2. make build   — build the Docker image"
        info "  3. make run ARGS=\"Hello, world!\"   — test it out"
        info "  4. make test    — run unit tests"
    else
        error "Some required tools are missing. Check the errors above."
        exit 1
    fi
}

# ── Main ─────────────────────────────────────────────────────────────
main() {
    echo -e "${CYAN}${BOLD}"
    echo "╔══════════════════════════════════════════════╗"
    echo "║   Azure OpenAI CLI — Prerequisites Setup    ║"
    echo "╚══════════════════════════════════════════════╝"
    echo -e "${NC}"

    detect_os
    install_dotnet
    configure_dotnet_path
    install_docker
    install_optional_tools
    create_env_template
    verify_installation
}

main "$@"
