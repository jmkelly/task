#!/usr/bin/env bash

set -euo pipefail

PROJECT="Task"
JOB_ID="job-installers-release-20260328"
REPOSITORY="jmkelly/task"
DEFAULT_INSTALL_DIR="${HOME}/.local/bin"
RELEASE_API_URL="https://api.github.com/repos/${REPOSITORY}/releases/latest"

log() {
    local level="$1"
    local step="$2"
    local message="$3"

    printf '[task-installer] level=%s project=%s job_id=%s step=%s message=%s\n' "$level" "$PROJECT" "$JOB_ID" "$step" "$message" >&2
}

fail() {
    local step="$1"
    local message="$2"

    log "error" "$step" "$message"
    exit 1
}

need_command() {
    local command_name="$1"

    if ! command -v "$command_name" >/dev/null 2>&1; then
        fail "prerequisites" "Missing required command: ${command_name}."
    fi
}

download_to_file() {
    local url="$1"
    local output_path="$2"

    if command -v curl >/dev/null 2>&1; then
        curl -fsSL "$url" -o "$output_path"
        return
    fi

    if command -v wget >/dev/null 2>&1; then
        wget -qO "$output_path" "$url"
        return
    fi

    fail "prerequisites" "Install curl or wget before running this installer."
}

extract_zip() {
    local archive_path="$1"
    local destination_dir="$2"

    if command -v unzip >/dev/null 2>&1; then
        unzip -q "$archive_path" -d "$destination_dir"
        return
    fi

    python3 - "$archive_path" "$destination_dir" <<'PY'
import pathlib
import sys
import zipfile

archive_path = pathlib.Path(sys.argv[1])
destination_dir = pathlib.Path(sys.argv[2])

with zipfile.ZipFile(archive_path) as archive:
    archive.extractall(destination_dir)
PY
}

choose_asset() {
    local release_json_path="$1"
    local runtime_identifier="$2"

    python3 - "$release_json_path" "$runtime_identifier" <<'PY'
import json
import sys

release_json_path, runtime_identifier = sys.argv[1:3]

with open(release_json_path, "r", encoding="utf-8") as handle:
    payload = json.load(handle)

releases = payload if isinstance(payload, list) else [payload]

for release in releases:
    if release.get("draft") or release.get("prerelease"):
        continue

    assets = release.get("assets", [])
    candidates = []

    for asset in assets:
        name = str(asset.get("name", ""))
        lower_name = name.lower()

        if runtime_identifier not in lower_name:
            continue

        if lower_name.endswith((".tar.gz", ".tgz")):
            score = 0
        elif lower_name.endswith(".zip"):
            score = 1
        else:
            score = 2

        candidates.append((score, name, str(asset.get("browser_download_url", "")), str(release.get("tag_name", ""))))

    if candidates:
        candidates.sort(key=lambda item: (item[0], item[1]))
        best = candidates[0]
        print(best[1])
        print(best[2])
        print(best[3])
        sys.exit(0)

print("__NO_MATCH__")
for release in releases:
    if release.get("draft") or release.get("prerelease"):
        continue

    release_tag = str(release.get("tag_name", ""))
    for asset in release.get("assets", []):
        asset_name = str(asset.get("name", ""))
        if release_tag and asset_name:
            print(f"{release_tag}:{asset_name}")
        elif asset_name:
            print(asset_name)
PY
}

resolve_binary_path() {
    local downloaded_path="$1"
    local extracted_dir="$2"

    case "$downloaded_path" in
        *.tar.gz|*.tgz|*.zip)
            ;;
        *)
            if [ -f "$downloaded_path" ]; then
                printf '%s\n' "$downloaded_path"
                return
            fi
            ;;
    esac

    local candidate=""
    for candidate in \
        "$extracted_dir/task" \
        "$extracted_dir/Task.Cli" \
        "$extracted_dir/Task"
    do
        if [ -f "$candidate" ]; then
            printf '%s\n' "$candidate"
            return
        fi
    done

    candidate=$(find "$extracted_dir" -type f \( -name 'task' -o -name 'Task.Cli' -o -name 'Task' \) -print | python3 -c 'import sys; print(sys.stdin.readline().strip())')
    if [ -n "$candidate" ]; then
        printf '%s\n' "$candidate"
        return
    fi

    fail "extract" "Downloaded asset did not contain a Linux Task CLI binary."
}

main() {
    need_command "uname"
    need_command "mktemp"
    need_command "python3"

    if [ "$(uname -s)" != "Linux" ]; then
        fail "platform" "This installer only supports Linux."
    fi

    local machine_architecture
    machine_architecture="$(uname -m)"

    local runtime_identifier=""
    case "$machine_architecture" in
        x86_64|amd64)
            runtime_identifier="linux-x64"
            ;;
        *)
            fail "platform" "Linux installer currently supports x64 release assets only. Detected architecture: ${machine_architecture}."
            ;;
    esac

    local temporary_dir
    temporary_dir="$(mktemp -d)"
    trap "rm -rf -- \"$temporary_dir\"" EXIT

    local release_json_path="$temporary_dir/release.json"
    log "info" "release_lookup" "Fetching latest release metadata from ${RELEASE_API_URL}."
    download_to_file "$RELEASE_API_URL" "$release_json_path"

    local asset_selection
    asset_selection="$(choose_asset "$release_json_path" "$runtime_identifier")"

    local asset_name
    asset_name="$(printf '%s\n' "$asset_selection" | python3 -c 'import sys; print(sys.stdin.readline().strip())')"
    if [ "$asset_name" = "__NO_MATCH__" ]; then
        local available_assets
        available_assets="$(printf '%s\n' "$asset_selection" | python3 -c 'import sys; lines=sys.stdin.read().splitlines(); available=[line for line in lines if line and line != "__NO_MATCH__"]; print(", ".join(available) if available else "none")')"
        fail "release_lookup" "No Linux release asset matched runtime ${runtime_identifier}. Available assets: ${available_assets}."
    fi

    local asset_url
    asset_url="$(printf '%s\n' "$asset_selection" | python3 -c 'import sys; lines=sys.stdin.read().splitlines(); print(lines[1] if len(lines) > 1 else "")')"
    local release_tag
    release_tag="$(printf '%s\n' "$asset_selection" | python3 -c 'import sys; lines=sys.stdin.read().splitlines(); print(lines[2] if len(lines) > 2 else "")')"

    local downloaded_path="$temporary_dir/$asset_name"
    local extracted_dir="$temporary_dir/extracted"
    mkdir -p "$extracted_dir"

    log "info" "download" "Downloading ${asset_name} from ${release_tag}."
    download_to_file "$asset_url" "$downloaded_path"

    case "$asset_name" in
        *.tar.gz|*.tgz)
            need_command "tar"
            tar -xzf "$downloaded_path" -C "$extracted_dir"
            ;;
        *.zip)
            extract_zip "$downloaded_path" "$extracted_dir"
            ;;
    esac

    local binary_path
    binary_path="$(resolve_binary_path "$downloaded_path" "$extracted_dir")"

    local install_dir="${TASK_INSTALL_DIR:-$DEFAULT_INSTALL_DIR}"
    local install_path="$install_dir/task"
    mkdir -p "$install_dir"

    chmod +x "$binary_path"
    cp "$binary_path" "$install_path"
    chmod 755 "$install_path"

    log "info" "install" "Installed Task CLI to ${install_path}."

    if ! printf ':%s:' "$PATH" | grep -Fq ":${install_dir}:"; then
        log "warn" "path" "${install_dir} is not on PATH for this shell. Add it to your shell profile before using 'task' globally."
    fi

    printf 'Task CLI installed to %s\n' "$install_path"
    printf 'Run: %s --help\n' "$install_path"
}

main "$@"
