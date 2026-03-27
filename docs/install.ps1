# Task CLI installer for Windows (PowerShell)
# Usage: irm https://jmkelly.github.io/task/install.ps1 | iex

[CmdletBinding()]
param (
    [string]$InstallDir = "$env:LOCALAPPDATA\Programs\task"
)

$ErrorActionPreference = 'Stop'

$Repo   = "jmkelly/task"
$Asset  = "task-win-x64.exe"
$BinName = "task.exe"

$DownloadUrl = "https://github.com/$Repo/releases/latest/download/$Asset"

Write-Host "Task CLI Installer" -ForegroundColor Cyan
Write-Host "==================" -ForegroundColor Cyan
Write-Host "Platform:     Windows x64"
Write-Host "Asset:        $Asset"
Write-Host "Install path: $InstallDir\$BinName"
Write-Host ""

# Create install directory if it doesn't exist
if (-not (Test-Path $InstallDir)) {
    New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null
}

$DestPath = Join-Path $InstallDir $BinName

Write-Host "Downloading from: $DownloadUrl"
try {
    Invoke-WebRequest -Uri $DownloadUrl -OutFile $DestPath -UseBasicParsing
} catch {
    Write-Error "Failed to download Task CLI: $_"
    exit 1
}

# Add InstallDir to user PATH if not already present
$UserPath = [System.Environment]::GetEnvironmentVariable("PATH", "User")
if ($UserPath -notlike "*$InstallDir*") {
    [System.Environment]::SetEnvironmentVariable(
        "PATH",
        "$UserPath;$InstallDir",
        "User"
    )
    Write-Host "Added '$InstallDir' to your user PATH." -ForegroundColor Green
    Write-Host "Please restart your terminal for the PATH change to take effect."
} else {
    Write-Host "'$InstallDir' is already in your PATH." -ForegroundColor Green
}

Write-Host ""
Write-Host "Installation complete! Open a new terminal and run 'task --help' to get started." -ForegroundColor Green
