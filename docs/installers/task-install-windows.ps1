[CmdletBinding()]
param(
    [string]$InstallDir = "$env:LOCALAPPDATA\Task\bin"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$Project = 'Task'
$JobId = 'job-installers-release-20260328'
$Repository = 'jmkelly/task'
$SourceVersion = '1.0.0.52'
$ReleaseApiUrl = "https://api.github.com/repos/$Repository/releases?per_page=10"

function Write-InstallerLog {
    param(
        [Parameter(Mandatory = $true)][string]$Level,
        [Parameter(Mandatory = $true)][string]$Step,
        [Parameter(Mandatory = $true)][string]$Message
    )

    Write-Host "[task-installer] level=$Level project=$Project job_id=$JobId step=$Step message=$Message"
}

function Fail-Installer {
    param(
        [Parameter(Mandatory = $true)][string]$Step,
        [Parameter(Mandatory = $true)][string]$Message
    )

    Write-InstallerLog -Level 'error' -Step $Step -Message $Message
    throw $Message
}

function Get-WindowsRuntimeIdentifier {
    $architectures = @($env:PROCESSOR_ARCHITEW6432, $env:PROCESSOR_ARCHITECTURE) | Where-Object { $_ }
    foreach ($architecture in $architectures) {
        switch ($architecture.ToUpperInvariant()) {
            'AMD64' { return 'win-x64' }
            'X86_64' { return 'win-x64' }
        }
    }

    Fail-Installer -Step 'platform' -Message "Windows installer currently supports x64 release assets only. Detected architecture values: $($architectures -join ', ')."
}

function Get-BestWindowsAsset {
    param(
        [Parameter(Mandatory = $true)]$Releases,
        [Parameter(Mandatory = $true)][string]$RuntimeIdentifier
    )

    foreach ($release in @($Releases)) {
        if ($release.draft -or $release.prerelease) {
            continue
        }

        $candidates = foreach ($asset in @($release.assets)) {
            $name = [string]$asset.name
            $lowerName = $name.ToLowerInvariant()

            if (-not $lowerName.Contains($RuntimeIdentifier.ToLowerInvariant())) {
                continue
            }

            $score = if ($lowerName.EndsWith('.exe')) {
                0
            }
            elseif ($lowerName.EndsWith('.zip')) {
                1
            }
            else {
                2
            }

            [pscustomobject]@{
                Score = $score
                Name = $name
                Url = [string]$asset.browser_download_url
                ReleaseTag = [string]$release.tag_name
            }
        }

        $best = $candidates | Sort-Object Score, Name | Select-Object -First 1
        if ($null -ne $best) {
            return $best
        }
    }

    return $null
}

function Get-TaskExecutablePath {
    param(
        [Parameter(Mandatory = $true)][string]$DownloadedPath,
        [Parameter(Mandatory = $true)][string]$ExtractedDirectory
    )

    if ($DownloadedPath.ToLowerInvariant().EndsWith('.exe')) {
        return $DownloadedPath
    }

    $candidate = Get-ChildItem -Path $ExtractedDirectory -Recurse -File |
        Where-Object { $_.Name -in @('task.exe', 'Task.Cli.exe', 'Task.exe') } |
        Select-Object -First 1

    if ($null -eq $candidate) {
        Fail-Installer -Step 'extract' -Message 'Downloaded asset did not contain a Windows Task CLI executable.'
    }

    return $candidate.FullName
}

$runtimeIdentifier = Get-WindowsRuntimeIdentifier
$temporaryDirectory = Join-Path ([System.IO.Path]::GetTempPath()) ([System.Guid]::NewGuid().ToString('N'))
$downloadPath = $null

try {
    New-Item -ItemType Directory -Path $temporaryDirectory -Force | Out-Null

    Write-InstallerLog -Level 'info' -Step 'release_lookup' -Message "Fetching release metadata from $ReleaseApiUrl. Source version reference: $SourceVersion."
    $releases = Invoke-RestMethod -Uri $ReleaseApiUrl -Headers @{ 'User-Agent' = 'task-installer' }

    $asset = Get-BestWindowsAsset -Releases $releases -RuntimeIdentifier $runtimeIdentifier
    if ($null -eq $asset) {
        $availableAssets = (@(
                foreach ($release in @($releases)) {
                    if ($release.draft -or $release.prerelease) {
                        continue
                    }

                    foreach ($releaseAsset in @($release.assets)) {
                        if ($release.tag_name) {
                            "$($release.tag_name):$($releaseAsset.name)"
                        }
                        else {
                            [string]$releaseAsset.name
                        }
                    }
                }
            ) -join ', ')
        if (-not $availableAssets) {
            $availableAssets = 'none'
        }
        Fail-Installer -Step 'release_lookup' -Message "No Windows release asset matched runtime $runtimeIdentifier. Available assets: $availableAssets."
    }

    $downloadPath = Join-Path $temporaryDirectory $asset.Name
    $extractPath = Join-Path $temporaryDirectory 'extracted'
    New-Item -ItemType Directory -Path $extractPath -Force | Out-Null

    Write-InstallerLog -Level 'info' -Step 'download' -Message "Downloading $($asset.Name) from $($asset.ReleaseTag)."
    Invoke-WebRequest -Uri $asset.Url -OutFile $downloadPath -Headers @{ 'User-Agent' = 'task-installer' }

    if ($asset.Name.ToLowerInvariant().EndsWith('.zip')) {
        Expand-Archive -Path $downloadPath -DestinationPath $extractPath -Force
    }

    $binaryPath = Get-TaskExecutablePath -DownloadedPath $downloadPath -ExtractedDirectory $extractPath

    New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null
    $installPath = Join-Path $InstallDir 'task.exe'
    Copy-Item -Path $binaryPath -Destination $installPath -Force

    $currentUserPath = [Environment]::GetEnvironmentVariable('Path', 'User')
    $userEntries = @()
    if ($currentUserPath) {
        $userEntries = $currentUserPath -split ';' | Where-Object { $_ }
    }

    if ($userEntries -notcontains $InstallDir) {
        $newUserPath = if ($currentUserPath) {
            "$currentUserPath;$InstallDir"
        }
        else {
            $InstallDir
        }

        [Environment]::SetEnvironmentVariable('Path', $newUserPath, 'User')
        Write-InstallerLog -Level 'info' -Step 'path' -Message "Added $InstallDir to the current user's PATH. Open a new terminal before using 'task' globally."
    }

    if (($env:Path -split ';') -notcontains $InstallDir) {
        $env:Path = if ($env:Path) {
            "$env:Path;$InstallDir"
        }
        else {
            $InstallDir
        }
    }

    Write-InstallerLog -Level 'info' -Step 'install' -Message "Installed Task CLI to $installPath."
    Write-Host "Task CLI installed to $installPath"
    Write-Host "Run: $installPath --help"
}
finally {
    if ($temporaryDirectory -and (Test-Path -LiteralPath $temporaryDirectory)) {
        Remove-Item -LiteralPath $temporaryDirectory -Recurse -Force
    }
}
