param(
    [string]$Runtime = "win-x64",
    [string]$Version = ""
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..")
$localDotnet = Join-Path $repoRoot ".dotnet\dotnet.exe"
$dotnet = if (Test-Path -LiteralPath $localDotnet) { $localDotnet } else { "dotnet" }
$projectPath = Join-Path $repoRoot "src\NSJLock.App\NSJLock.App.csproj"

if ([string]::IsNullOrWhiteSpace($Version)) {
    [xml]$projectFile = Get-Content -LiteralPath $projectPath
    $Version = $projectFile.Project.PropertyGroup.Version
}

$outputPath = Join-Path $repoRoot "artifacts\publish\NSJLock-single-$Runtime"
$archivePath = Join-Path $repoRoot "artifacts\publish\archive\NSJLock-single-$Runtime"
$versionedExeName = "NSJLock-v$Version-$Runtime.exe"
$defaultExePath = Join-Path $outputPath "NSJLock.exe"
$versionedExePath = Join-Path $outputPath $versionedExeName

$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "1"

& $dotnet publish $projectPath `
    -c Release `
    -r $Runtime `
    --self-contained true `
    -o $outputPath `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:DebugType=none `
    -p:DebugSymbols=false

if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

New-Item -ItemType Directory -Path $archivePath -Force | Out-Null

Get-ChildItem -LiteralPath $outputPath -Filter "NSJLock-v*-$Runtime.exe" -File -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -ne $versionedExePath } |
    ForEach-Object {
        $destination = Join-Path $archivePath $_.Name
        if (Test-Path -LiteralPath $destination) {
            $baseName = [System.IO.Path]::GetFileNameWithoutExtension($_.Name)
            $extension = [System.IO.Path]::GetExtension($_.Name)
            $timestamp = $_.LastWriteTime.ToString("yyyyMMdd-HHmmss")
            $destination = Join-Path $archivePath "$baseName-$timestamp$extension"
        }

        Move-Item -LiteralPath $_.FullName -Destination $destination
        Write-Host "Archived previous package: $destination"
    }

if (Test-Path -LiteralPath $defaultExePath) {
    Copy-Item -LiteralPath $defaultExePath -Destination $versionedExePath -Force
}

Write-Host "Single-file package written to: $outputPath"
Write-Host "Versioned package written to: $versionedExePath"
