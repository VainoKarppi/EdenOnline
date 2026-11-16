# Publish.ps1
#
# Builds the extension, rewrites the addon paths / PBO prefixes so the packed
# addons are standalone, packs the addons into .pbo files and produces:
#   Publish\EdenOnline_v<version>.zip
#     addons\functions.pbo
#     mod.cpp
#     logo.paa
#     EdenOnline_x64.dll
#     include\...

param(
    [string]$Version,
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"

# ============================================================
# Load Functions.ps1
# ============================================================

$functionsScript = Join-Path $PSScriptRoot "Functions.ps1"

try {
    . $functionsScript
    Write-Host "Functions loaded successfully." -ForegroundColor Green
}
catch {
    Write-Host "Error loading functions: $_" -ForegroundColor Red
    exit 1
}

$utf8NoBom = New-Object System.Text.UTF8Encoding($false)

# ============================================================
# Rewrite addon prefixes and all references to them
# ============================================================
#
#   z\eoex\addons\functions  ->  functions
#
# The prefix is read from every addon's $PBOPREFIX$ file. Its last path
# segment becomes the new prefix and every occurrence of the old prefix in
# the addon's .cpp / .hpp files (for example a CfgFunctions "file = ..."
# entry) is replaced with it, so the packed PBO resolves its own paths.

function Convert-AddonPrefixes {
    param (
        [Parameter(Mandatory = $true)]
        [string]$ModFolder
    )

    $addonsFolder = Join-Path $ModFolder "addons"

    if (-not (Test-Path -LiteralPath $addonsFolder -PathType Container)) {
        Write-Host "Addons folder not found: $addonsFolder" -ForegroundColor Red
        return $false
    }

    $addonFolders = Get-ChildItem -LiteralPath $addonsFolder -Directory

    foreach ($addonFolder in $addonFolders) {

        $prefixFilePath = Join-Path $addonFolder.FullName '$PBOPREFIX$'

        if (-not (Test-Path -LiteralPath $prefixFilePath -PathType Leaf)) {
            Write-Host "No `$PBOPREFIX`$ found in $($addonFolder.Name). Skipping." -ForegroundColor DarkYellow
            continue
        }

        $oldPrefix = ([System.IO.File]::ReadAllText($prefixFilePath)).Trim()

        if (-not $oldPrefix) {
            Write-Host "Empty `$PBOPREFIX`$ in $($addonFolder.Name). Skipping." -ForegroundColor DarkYellow
            continue
        }

        $newPrefix = ($oldPrefix -split '\\')[-1]

        if ($newPrefix -eq $oldPrefix) {
            Write-Host "Prefix '$oldPrefix' is already standalone in $($addonFolder.Name)." -ForegroundColor DarkGray
            continue
        }

        Write-Host "Rewriting prefix '$oldPrefix' -> '$newPrefix' in $($addonFolder.Name)..." -ForegroundColor Yellow

        [System.IO.File]::WriteAllText($prefixFilePath, $newPrefix, $utf8NoBom)

        $prefixPattern = '(?i)' + [regex]::Escape($oldPrefix)

        $sourceFiles = Get-ChildItem -LiteralPath $addonFolder.FullName -Recurse -File |
            Where-Object { $_.Extension -eq '.cpp' -or $_.Extension -eq '.hpp' }

        foreach ($sourceFile in $sourceFiles) {

            $content = [System.IO.File]::ReadAllText($sourceFile.FullName)

            if ($content -notmatch $prefixPattern) {
                continue
            }

            $updatedContent = [regex]::Replace($content, $prefixPattern, $newPrefix)

            [System.IO.File]::WriteAllText($sourceFile.FullName, $updatedContent, $utf8NoBom)

            $relativePath = $sourceFile.FullName.Substring($ModFolder.Length).TrimStart([char]'\')
            Write-Host "  Updated: $relativePath" -ForegroundColor DarkGray
        }
    }

    return $true
}

# ============================================================
# Locate the Arma 3 Tools directory (Binarize, CfgConvert, ...)
# ============================================================
# AddonBuilder resolves these from the registry when available. Fall back to
# the well-known locations so packing works on machines where the
# "Arma 3 Tools" registry key is only registered for the current user.

function Get-ArmaToolsDirectory {
    $candidates = @()

    try {
        $candidates += (Get-ItemProperty -Path "HKLM:\SOFTWARE\WOW6432Node\Bohemia Interactive\Arma 3 Tools" -ErrorAction Stop).path
    }
    catch { }

    try {
        $candidates += (Get-ItemProperty -Path "HKCU:\SOFTWARE\Bohemia Interactive\Arma 3 Tools" -ErrorAction Stop).path
    }
    catch { }

    try {
        $addonBuilderPath = (Get-ItemProperty -Path "HKLM:\SOFTWARE\WOW6432Node\Bohemia Interactive\AddonBuilder" -ErrorAction Stop).path
        $candidates += (Split-Path -Path $addonBuilderPath -Parent)
    }
    catch { }

    foreach ($candidate in $candidates) {
        if (-not $candidate) { continue }
        if (Test-Path -LiteralPath (Join-Path $candidate "CfgConvert\CfgConvert.exe")) {
            return $candidate
        }
    }

    return $null
}

# ============================================================
# Paths
# ============================================================

$projectPath = Get-ProjectPath
$buildInfo = Get-BuildInfo

$assemblyName = $buildInfo.AssemblyName -replace '_x64$', ''
$dllName = "$($buildInfo.AssemblyName).dll"

$modFolder = Join-Path $projectPath "@$assemblyName"
$publishFolder = Join-Path $projectPath "Publish"

# ============================================================
# Version
# ============================================================

if (-not $Version) {
    $Version = $buildInfo.Version
}

if ($Version -notmatch '^\d+\.\d+\.\d+$') {
    Write-Host "Invalid version '$Version'. Use the format x.x.x, for example 1.2.0." -ForegroundColor Red
    exit 1
}

$zipPath = Join-Path $publishFolder "EdenOnline_v$Version.zip"
$stagingFolder = Join-Path $publishFolder ".staging-$Version"
$stagedModFolder = Join-Path $stagingFolder "@$assemblyName"
$releaseFolder = Join-Path $stagingFolder "release"

Write-Host ""
Write-Host "Publishing: EdenOnline_v$Version" -ForegroundColor Cyan
Write-Host ""
Write-Host "Project Path : $projectPath" -ForegroundColor Blue
Write-Host "Mod Path     : $modFolder" -ForegroundColor Blue
Write-Host "Publish Path : $zipPath" -ForegroundColor Blue
Write-Host ""

try {
    # ========================================================
    # Build the extension
    # ========================================================

    if ($SkipBuild) {
        Write-Host "Skipping build (-SkipBuild)." -ForegroundColor Yellow
    }
    else {
        Write-Host "Building project..." -ForegroundColor Cyan
        Write-Host ""

        if (-not (Build-Project -projectPath $projectPath -destinationPath $modFolder)) {
            Write-Host "Build failed. Publish was not created." -ForegroundColor Red
            exit 1
        }

        Write-Host "Build completed successfully." -ForegroundColor Green
    }

    $sourceDll = Join-Path $modFolder $dllName

    if (-not (Test-Path -LiteralPath $sourceDll -PathType Leaf)) {
        Write-Host "Extension DLL not found: $sourceDll" -ForegroundColor Red
        exit 1
    }

    # ========================================================
    # Stage a copy of the mod
    # ========================================================

    Write-Host ""
    Write-Host "Staging mod copy..." -ForegroundColor Cyan

    if (Test-Path -LiteralPath $stagingFolder) {
        Remove-Item -LiteralPath $stagingFolder -Recurse -Force
    }

    New-Item -Path $stagedModFolder -ItemType Directory -Force | Out-Null

    $stagedItems = Get-ChildItem -LiteralPath $modFolder -Force | Where-Object {
        $_.Name -notin @('.hemtt', '.hemttout')
    }

    foreach ($item in $stagedItems) {
        Copy-Item -LiteralPath $item.FullName -Destination $stagedModFolder -Recurse -Force
    }

    Write-Host "Staged mod copy: $stagedModFolder" -ForegroundColor Green

    # ========================================================
    # Rewrite paths / prefixes
    # ========================================================

    Write-Host ""
    Write-Host "Updating addon prefixes..." -ForegroundColor Cyan
    Write-Host ""

    if (-not (Convert-AddonPrefixes -ModFolder $stagedModFolder)) {
        Write-Host "Prefix update failed. Publish was not created." -ForegroundColor Red
        exit 1
    }

    # ========================================================
    # Pack addons
    # ========================================================

    Write-Host ""
    Write-Host "Packing addons..." -ForegroundColor Cyan
    Write-Host ""

    $toolsDirectory = Get-ArmaToolsDirectory

    if ($toolsDirectory) {
        Write-Host "Arma 3 Tools: $toolsDirectory" -ForegroundColor Blue
    }
    else {
        Write-Host "Arma 3 Tools directory not found. AddonBuilder will use its default." -ForegroundColor Yellow
    }

    if (-not (Pack-Addons -modFolder $stagedModFolder -ToolsDirectory $toolsDirectory)) {
        Write-Host "Addon packing failed. Publish was not created." -ForegroundColor Red
        exit 1
    }

    # ========================================================
    # Assemble the release content
    # ========================================================

    Write-Host ""
    Write-Host "Assembling release content..." -ForegroundColor Cyan

    New-Item -Path $releaseFolder -ItemType Directory -Force | Out-Null

    $releaseAddonsFolder = Join-Path $releaseFolder "addons"
    New-Item -Path $releaseAddonsFolder -ItemType Directory -Force | Out-Null

    $packedPbos = Get-ChildItem -LiteralPath (Join-Path $stagedModFolder "addons") -Filter *.pbo -File

    if (-not $packedPbos) {
        Write-Host "No packed PBO files were produced." -ForegroundColor Red
        exit 1
    }

    foreach ($pbo in $packedPbos) {
        Copy-Item -LiteralPath $pbo.FullName -Destination $releaseAddonsFolder -Force
        Write-Host "Added: addons\$($pbo.Name)" -ForegroundColor DarkGray
    }

    $requiredFiles = @("mod.cpp", "logo.paa", $dllName)

    foreach ($fileName in $requiredFiles) {
        $sourcePath = Join-Path $stagedModFolder $fileName

        if (-not (Test-Path -LiteralPath $sourcePath -PathType Leaf)) {
            Write-Host "Required file not found: $sourcePath" -ForegroundColor Red
            exit 1
        }

        Copy-Item -LiteralPath $sourcePath -Destination $releaseFolder -Force
        Write-Host "Added: $fileName" -ForegroundColor DarkGray
    }

    $stagedIncludeFolder = Join-Path $stagedModFolder "include"

    if (Test-Path -LiteralPath $stagedIncludeFolder -PathType Container) {
        Copy-Item -LiteralPath $stagedIncludeFolder -Destination $releaseFolder -Recurse -Force
        Write-Host "Added: include\" -ForegroundColor DarkGray
    }
    else {
        Write-Host "No include folder found. Skipping." -ForegroundColor DarkYellow
    }

    # ========================================================
    # Create ZIP
    # ========================================================

    Write-Host ""
    Write-Host "Creating ZIP archive..." -ForegroundColor Cyan

    if (-not (Test-Path -LiteralPath $publishFolder -PathType Container)) {
        New-Item -Path $publishFolder -ItemType Directory -Force | Out-Null
        Write-Host "Created Publish folder." -ForegroundColor Green
    }

    if (Test-Path -LiteralPath $zipPath) {
        Write-Host "Removing existing ZIP: $zipPath" -ForegroundColor Yellow
        Remove-Item -LiteralPath $zipPath -Force
    }

    Compress-Archive `
        -Path "$releaseFolder\*" `
        -DestinationPath $zipPath `
        -CompressionLevel Optimal `
        -Force

    if (-not (Test-Path -LiteralPath $zipPath -PathType Leaf)) {
        Write-Host "Failed to create ZIP archive." -ForegroundColor Red
        exit 1
    }

    # ========================================================
    # Finished
    # ========================================================

    $zipInfo = Get-Item -LiteralPath $zipPath
    $zipSizeMB = [Math]::Round($zipInfo.Length / 1MB, 2)

    Write-Host ""
    Write-Host "========================================" -ForegroundColor Green
    Write-Host " Publish created successfully!" -ForegroundColor Green
    Write-Host "========================================" -ForegroundColor Green
    Write-Host ""
    Write-Host "Version : $Version" -ForegroundColor Cyan
    Write-Host "ZIP     : $zipPath" -ForegroundColor Cyan
    Write-Host "Size    : $zipSizeMB MB" -ForegroundColor Cyan
    Write-Host ""

    Get-ChildItem -LiteralPath $releaseFolder -Recurse -File |
        ForEach-Object { $_.FullName.Substring($releaseFolder.Length).TrimStart([char]'\') } |
        Sort-Object |
        ForEach-Object { Write-Host "  $_" -ForegroundColor DarkGray }

    Write-Host ""
}
finally {
    # ========================================================
    # Clean up staging
    # ========================================================

    if (Test-Path -LiteralPath $stagingFolder) {
        Remove-Item -LiteralPath $stagingFolder -Recurse -Force -ErrorAction SilentlyContinue
    }
}
