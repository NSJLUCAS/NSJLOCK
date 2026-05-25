# Release Guide

This document describes the public release flow for NSJ Lock.

It is intended for maintainers. It should not include private notes,
credentials, tokens, or machine-specific paths beyond the repository-relative
commands shown below.

## Release Target

NSJ Lock is a Windows desktop application. Publish the downloadable `.exe` on
GitHub Releases.

Do not publish the application executable to GitHub Packages. GitHub Packages
is better suited for NuGet, npm, Maven, Ruby, Python, or container packages.

## Version Source

The application version is defined in:

```text
src\NSJLock.App\NSJLock.App.csproj
```

Update all version fields before creating a new release:

```xml
<Version>1.0.2</Version>
<AssemblyVersion>1.0.2.0</AssemblyVersion>
<FileVersion>1.0.2.0</FileVersion>
<InformationalVersion>1.0.2</InformationalVersion>
```

Use the same version number for the Git tag:

```text
v1.0.2
```

## Pre-Release Validation

From the repository root, use the .NET 8 SDK:

```powershell
dotnet restore NSJLock.sln
dotnet build NSJLock.sln
dotnet test NSJLock.sln
```

Do not create a release if build or tests fail.

## Build The Release Executable

Use the repository publish script:

```powershell
powershell.exe -ExecutionPolicy Bypass -NoProfile -File .\scripts\publish-single-file.ps1
```

The release output directory is:

```text
artifacts\publish\NSJLock-single-win-x64
```

The expected files are:

```text
artifacts\publish\NSJLock-single-win-x64\NSJLock.exe
artifacts\publish\NSJLock-single-win-x64\NSJLock-v<version>-win-x64.exe
```

`NSJLock.exe` is always the latest executable. The versioned executable is the
file to upload to GitHub Releases.

## Commit Public Source Changes

Commit only public source, tests, scripts, assets, and public documentation.

Do not commit:

```text
private maintainer notes
.dotnet/
.dotnet-cli-home/
.dotnet-temp/
.nuget/
.nuget-packages/
.tmp/
.worktrees/
artifacts/
bin/
obj/
tools/
```

Typical commit flow:

```powershell
git status --short
git add README.md docs\release.md src tests scripts .gitignore NSJLock.sln
git commit -m "Release v1.0.2"
```

Adjust the staged file list to match the actual release changes. Do not use
`git add -A` unless the working tree has been reviewed and only public files
are dirty.

For the first public release checkout, clone the public repository next to the
development repository:

```powershell
cd ..
git clone https://github.com/NSJLUCAS/NSJLOCK.git "NSJ Lock Public"
cd "NSJ Lock"
```

If the public GitHub repository is maintained from a separate public checkout,
export the public files first:

```powershell
powershell.exe -ExecutionPolicy Bypass -NoProfile -File .\scripts\export-public.ps1 -DestinationPath "..\NSJ Lock Public" -Clean
```

Preview the same export without writing files:

```powershell
powershell.exe -ExecutionPolicy Bypass -NoProfile -File .\scripts\export-public.ps1 -DestinationPath "..\NSJ Lock Public" -Clean -WhatIf
```

Then commit from the public checkout:

```powershell
cd "..\NSJ Lock Public"
git status --short
git add .gitignore README.md NSJLock.sln docs\release.md scripts src tests
git commit -m "Release v1.0.2"
```

## Push To GitHub

Push the main branch and the release tag:

```powershell
git tag v1.0.2
git push origin main
git push origin v1.0.2
```

If the public GitHub repository is maintained from a clean public export,
repeat the export process first, then commit and tag from that public export.
The public history should not contain private files.

## Create The GitHub Release

Open the GitHub release page:

```text
https://github.com/NSJLUCAS/NSJLOCK/releases/new?tag=v1.0.2
```

Use this title:

```text
NSJ Lock v1.0.2
```

Upload the versioned executable:

```text
artifacts\publish\NSJLock-single-win-x64\NSJLock-v1.0.2-win-x64.exe
```

Example release notes:

```markdown
NSJ Lock v1.0.2

Windows desktop volume protection tool.

- Locks system master volume to the configured value
- Restores volume when it is above or below the target
- Supports tray operation
- Stores settings locally
- Ships as a single-file win-x64 executable

Validation:
- build passed
- tests passed
```

## Post-Release Checks

After publishing the release, verify:

- The GitHub repository has the expected `main` commit.
- The GitHub repository has the expected `v<version>` tag.
- The GitHub Release exists.
- The versioned `.exe` is attached to the release.
- `NSJLock.exe` and the versioned `.exe` have the expected file version.
- Build output and release artifacts are not committed to the repository.
