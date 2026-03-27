#!/usr/bin/env bash
# Task CLI installer for Linux and macOS
# Usage: curl -fsSL https://jmkelly.github.io/task/install.sh | bash

set -euo pipefail

REPO="jmkelly/task"
INSTALL_DIR="/usr/local/bin"
BINARY_NAME="task"

# Detect OS and architecture
OS="$(uname -s)"
ARCH="$(uname -m)"

case "$OS" in
  Linux*)
    case "$ARCH" in
      x86_64) ASSET="task-linux-x64" ;;
      *)
        echo "Error: Unsupported Linux architecture: $ARCH" >&2
        echo "Only x86_64 is currently supported on Linux." >&2
        exit 1
        ;;
    esac
    ;;
  Darwin*)
    case "$ARCH" in
      x86_64) ASSET="task-osx-x64" ;;
      arm64)  ASSET="task-osx-arm64" ;;
      *)
        echo "Error: Unsupported macOS architecture: $ARCH" >&2
        exit 1
        ;;
    esac
    ;;
  *)
    echo "Error: Unsupported operating system: $OS" >&2
    echo "For Windows, use the PowerShell installer:" >&2
    echo "  irm https://jmkelly.github.io/task/install.ps1 | iex" >&2
    exit 1
    ;;
esac

DOWNLOAD_URL="https://github.com/${REPO}/releases/latest/download/${ASSET}"

echo "Task CLI Installer"
echo "=================="
echo "OS:           $OS ($ARCH)"
echo "Asset:        $ASSET"
echo "Install path: ${INSTALL_DIR}/${BINARY_NAME}"
echo ""

# Check for curl or wget
if command -v curl &>/dev/null; then
  DOWNLOADER="curl -fsSL"
elif command -v wget &>/dev/null; then
  DOWNLOADER="wget -qO-"
else
  echo "Error: Neither curl nor wget is available. Please install one and try again." >&2
  exit 1
fi

# Download to a temp file
TMP_FILE="$(mktemp)"
trap 'rm -f "$TMP_FILE"' EXIT

echo "Downloading from: $DOWNLOAD_URL"
if command -v curl &>/dev/null; then
  curl -fsSL "$DOWNLOAD_URL" -o "$TMP_FILE"
else
  wget -qO "$TMP_FILE" "$DOWNLOAD_URL"
fi

chmod +x "$TMP_FILE"

# Install: try /usr/local/bin first (may need sudo), fall back to ~/bin
install_binary() {
  local dest="${INSTALL_DIR}/${BINARY_NAME}"
  if [ -w "$INSTALL_DIR" ]; then
    mv "$TMP_FILE" "$dest"
    echo "Installed to: $dest"
  elif command -v sudo &>/dev/null; then
    echo "Installing to $INSTALL_DIR requires sudo access..."
    sudo mv "$TMP_FILE" "$dest"
    echo "Installed to: $dest"
  else
    # Fall back to ~/bin
    local user_bin="$HOME/bin"
    mkdir -p "$user_bin"
    mv "$TMP_FILE" "${user_bin}/${BINARY_NAME}"
    echo "Installed to: ${user_bin}/${BINARY_NAME}"
    # Remind user to add ~/bin to PATH if not already there
    if [[ ":$PATH:" != *":${user_bin}:"* ]]; then
      echo ""
      echo "NOTE: Add ~/bin to your PATH by adding the following to your shell profile"
      echo "      (~/.bashrc, ~/.zshrc, etc.):"
      echo ""
      echo '  export PATH="$HOME/bin:$PATH"'
      echo ""
      echo "Then reload your shell or run: source ~/.bashrc"
    fi
  fi
}

install_binary

echo ""
echo "Installation complete! Run 'task --help' to get started."
