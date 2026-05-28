[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [string]$DestinationPath = "",
    [switch]$Clean
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..")

if ([string]::IsNullOrWhiteSpace($DestinationPath)) {
    $DestinationPath = Join-Path (Split-Path -Parent $repoRoot) "NSJ Lock Public"
}

$destinationFullPath = [System.IO.Path]::GetFullPath($DestinationPath)
$repoFullPath = [System.IO.Path]::GetFullPath($repoRoot)

if ($destinationFullPath.TrimEnd("\") -eq $repoFullPath.TrimEnd("\")) {
    throw "DestinationPath must not be the source repository."
}

$publicFiles = @(
    ".gitignore",
    "NSJLock.sln",
    "README.md",
    "docs\release.md"
)

$publicDirectories = @(
    "scripts",
    "src",
    "tests"
)

$privateRootFiles = @(
    "AGENTS.md",
    "PROJECT.md",
    "ROADMAP.md"
)

$excludedDirectoryNames = @(
    ".git",
    ".dotnet",
    ".dotnet-cli-home",
    ".dotnet-home",
    ".dotnet-temp",
    ".dotnet_home",
    ".nuget",
    ".nuget-packages",
    ".tmp",
    ".worktrees",
    "artifacts",
    "bin",
    "obj",
    "tools"
)

function Test-IsExcludedPath {
    param([string]$RelativePath)

    $parts = $RelativePath -split '[\\/]'
    foreach ($part in $parts) {
        if ($excludedDirectoryNames -contains $part) {
            return $true
        }
    }

    return $false
}

function Get-RepoRelativePath {
    param([string]$FullPath)

    $root = $repoFullPath.TrimEnd("\") + "\"
    $normalizedFullPath = [System.IO.Path]::GetFullPath($FullPath)

    if (-not $normalizedFullPath.StartsWith($root, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Path is outside the source repository: $FullPath"
    }

    return $normalizedFullPath.Substring($root.Length)
}

function Copy-PublicFile {
    param(
        [string]$RelativePath
    )

    $source = Join-Path $repoRoot $RelativePath
    $destination = Join-Path $destinationFullPath $RelativePath

    if (-not (Test-Path -LiteralPath $source -PathType Leaf)) {
        Write-Host "Skipping missing file: $RelativePath"
        return
    }

    if ($PSCmdlet.ShouldProcess($destination, "Copy public file $RelativePath")) {
        $destinationDirectory = Split-Path -Parent $destination
        New-Item -ItemType Directory -Path $destinationDirectory -Force | Out-Null
        Copy-Item -LiteralPath $source -Destination $destination -Force
        Write-Host "Copied file: $RelativePath"
    }
}

function Copy-PublicDirectory {
    param(
        [string]$RelativeDirectory
    )

    $sourceDirectory = Join-Path $repoRoot $RelativeDirectory

    if (-not (Test-Path -LiteralPath $sourceDirectory -PathType Container)) {
        Write-Host "Skipping missing directory: $RelativeDirectory"
        return
    }

    Get-ChildItem -LiteralPath $sourceDirectory -File -Recurse |
        ForEach-Object {
            $relativePath = Get-RepoRelativePath -FullPath $_.FullName

            if (-not (Test-IsExcludedPath -RelativePath $relativePath)) {
                Copy-PublicFile -RelativePath $relativePath
            }
        }
}

function Clear-PublicPath {
    param([string]$RelativePath)

    $target = Join-Path $destinationFullPath $RelativePath

    if ((Test-Path -LiteralPath $target) -and $PSCmdlet.ShouldProcess($target, "Clean public path $RelativePath")) {
        Remove-Item -LiteralPath $target -Recurse -Force
        Write-Host "Cleaned: $RelativePath"
    }
}

if ($PSCmdlet.ShouldProcess($destinationFullPath, "Ensure public export directory exists")) {
    New-Item -ItemType Directory -Path $destinationFullPath -Force | Out-Null
}

if ($Clean) {
    foreach ($file in $publicFiles) {
        Clear-PublicPath -RelativePath $file
    }

    foreach ($file in $privateRootFiles) {
        Clear-PublicPath -RelativePath $file
    }

    foreach ($directory in $publicDirectories) {
        Clear-PublicPath -RelativePath $directory
    }
}

foreach ($file in $publicFiles) {
    Copy-PublicFile -RelativePath $file
}

foreach ($directory in $publicDirectories) {
    Copy-PublicDirectory -RelativeDirectory $directory
}

if (-not (Test-Path -LiteralPath (Join-Path $destinationFullPath ".git") -PathType Container)) {
    Write-Warning "Destination is not a Git repository. Clone https://github.com/NSJLUCAS/NSJLOCK.git there before committing."
}

Write-Host "Public export complete: $destinationFullPath"
