# BuildRelease.ps1

$ErrorActionPreference = "Stop"

# ============================================================
# Load Functions.ps1
# ============================================================

$functionsScript = "$PSScriptRoot\Functions.ps1"

try {
    . $functionsScript
    Write-Host "Functions loaded successfully." -ForegroundColor Green
}
catch {
    Write-Host "Error loading functions: $_" -ForegroundColor Red
    exit 1
}

# ============================================================
# Get version
# ============================================================

do {
    $version = Read-Host "Enter release version (x.x.x)"

    if ($version -notmatch '^\d+\.\d+\.\d+$') {
        Write-Host "Invalid version. Please use the format x.x.x, for example 1.2.0." -ForegroundColor Yellow
    }
}
while ($version -notmatch '^\d+\.\d+\.\d+$')

$releaseName = "EdenOnline_v$version"

Write-Host ""
Write-Host "Preparing release: $releaseName" -ForegroundColor Cyan
Write-Host ""

# ============================================================
# Project paths
# ============================================================

$projectPath = Get-ProjectPath

$buildInfo = Get-BuildInfo
$assemblyName = $buildInfo.AssemblyName -replace '_x64$', ''

$modFolder = Join-Path $projectPath "@$assemblyName"
$releaseFolder = Join-Path $projectPath $releaseName
$publishFolder = Join-Path $projectPath "Publish"
$zipPath = Join-Path $projectPath "$releaseName.zip"
$publishZipPath = Join-Path $publishFolder "$releaseName.zip"

Write-Host "Project Path : $projectPath" -ForegroundColor Blue
Write-Host "Mod Path     : $modFolder" -ForegroundColor Blue
Write-Host "Release Path : $releaseFolder" -ForegroundColor Blue
Write-Host "Publish Path : $publishFolder" -ForegroundColor Blue
Write-Host ""

# ============================================================
# Find main .csproj
# ============================================================

$csproj = Join-Path $projectPath "EdenOnline.csproj"

if (-not (Test-Path -Path $csproj)) {
    Write-Host "Main project file not found: $csproj" -ForegroundColor Red
    exit 1
}

Write-Host "Project file: $csproj" -ForegroundColor Blue
Write-Host ""

# ============================================================
# Update <Version>
# ============================================================

Write-Host "Updating project version..." -ForegroundColor Cyan

$csprojContent = Get-Content -Path $csproj -Raw

$versionPattern = '<Version>\s*[^<]*\s*</Version>'

if ($csprojContent -notmatch $versionPattern) {
    Write-Host "No <Version> element found in the .csproj." -ForegroundColor Red
    exit 1
}

$csprojContent = [regex]::Replace(
    $csprojContent,
    $versionPattern,
    "<Version>$version</Version>"
)

Set-Content `
    -Path $csproj `
    -Value $csprojContent `
    -Encoding UTF8

Write-Host "Updated project version to $version." -ForegroundColor Green
Write-Host ""

# ============================================================
# Create mod folder if necessary
# ============================================================

if (-not (Test-Path -Path $modFolder)) {

    New-Item `
        -Path $modFolder `
        -ItemType Directory `
        -Force | Out-Null

    Write-Host "Created mod folder: $modFolder" -ForegroundColor Green
    Write-Host "Please ensure the mod folder is set up correctly before proceeding!" -ForegroundColor Yellow

    Read-Host "Press Enter to continue..."
}

# ============================================================
# Remove previous temporary release / ZIP
# ============================================================

if (Test-Path -Path $releaseFolder) {

    Write-Host "Removing existing release folder..." -ForegroundColor Yellow

    Remove-Item `
        -Path $releaseFolder `
        -Recurse `
        -Force
}

if (Test-Path -Path $zipPath) {

    Write-Host "Removing existing ZIP..." -ForegroundColor Yellow

    Remove-Item `
        -Path $zipPath `
        -Force
}

# ============================================================
# Build project
# ============================================================

Write-Host ""
Write-Host "Building project..." -ForegroundColor Cyan
Write-Host ""

if (-not (Build-Project `
        -projectPath $projectPath `
        -destinationPath $modFolder)) {

    Write-Host ""
    Write-Host "Build failed. Release was not created." -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "Build completed successfully." -ForegroundColor Green

# ============================================================
# Pack addons
# ============================================================

Write-Host ""
Write-Host "Packing addons..." -ForegroundColor Cyan
Write-Host ""

if (-not (Pack-Addons -modFolder $modFolder)) {

    Write-Host ""
    Write-Host "Addon packing failed. Release was not created." -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "Addon packing completed successfully." -ForegroundColor Green

# ============================================================
# Copy @EdenOnline to temporary release folder
# ============================================================

Write-Host ""
Write-Host "Creating release copy..." -ForegroundColor Cyan

New-Item `
    -Path $releaseFolder `
    -ItemType Directory `
    -Force | Out-Null

Copy-Item `
    -Path $modFolder `
    -Destination $releaseFolder `
    -Recurse `
    -Force

if (-not (Test-Path -Path $releaseFolder)) {
    Write-Host "Failed to create release folder." -ForegroundColor Red
    exit 1
}

Write-Host "Created: $releaseFolder" -ForegroundColor Green

# ============================================================
# Remove unbinarized addon folders from release copy
# ============================================================

Write-Host ""
Write-Host "Cleaning unbinarized addon folders from release..." -ForegroundColor Cyan
Write-Host ""

$releaseAddonsFolder = Join-Path $releaseFolder "@$assemblyName\addons"

if (Test-Path -Path $releaseAddonsFolder) {

    $addonDirectories = Get-ChildItem `
        -Path $releaseAddonsFolder `
        -Directory

    foreach ($addonDirectory in $addonDirectories) {

        Write-Host "Removing: $($addonDirectory.Name)" -ForegroundColor Yellow

        Remove-Item `
            -Path $addonDirectory.FullName `
            -Recurse `
            -Force
    }

    Write-Host "Release addon folders cleaned." -ForegroundColor Green
}
else {
    Write-Host "No release addons folder found. Skipping cleanup." -ForegroundColor Yellow
}

# ============================================================
# Create ZIP
# ============================================================

Write-Host ""
Write-Host "Creating ZIP archive..." -ForegroundColor Cyan
Write-Host ""

Compress-Archive `
    -Path "$releaseFolder\*" `
    -DestinationPath $zipPath `
    -CompressionLevel Optimal `
    -Force

if (-not (Test-Path -Path $zipPath)) {
    Write-Host "Failed to create ZIP archive." -ForegroundColor Red
    exit 1
}

Write-Host "ZIP created: $zipPath" -ForegroundColor Green

# ============================================================
# Remove temporary release folder
# ============================================================

Write-Host ""
Write-Host "Removing temporary release folder..." -ForegroundColor Cyan

Remove-Item `
    -Path $releaseFolder `
    -Recurse `
    -Force

if (Test-Path -Path $releaseFolder) {
    Write-Host "Failed to remove temporary release folder." -ForegroundColor Red
    exit 1
}

Write-Host "Temporary release folder removed." -ForegroundColor Green

# ============================================================
# Create Publish folder
# ============================================================

if (-not (Test-Path -Path $publishFolder)) {

    New-Item `
        -Path $publishFolder `
        -ItemType Directory `
        -Force | Out-Null

    Write-Host "Created Publish folder." -ForegroundColor Green
}

# ============================================================
# Move ZIP to Publish
# ============================================================

if (Test-Path -Path $publishZipPath) {

    Write-Host "Removing existing published ZIP..." -ForegroundColor Yellow

    Remove-Item `
        -Path $publishZipPath `
        -Force
}

Move-Item `
    -Path $zipPath `
    -Destination $publishZipPath `
    -Force

if (-not (Test-Path -Path $publishZipPath)) {
    Write-Host "Failed to move ZIP to Publish folder." -ForegroundColor Red
    exit 1
}

Write-Host "ZIP moved to Publish folder." -ForegroundColor Green

# ============================================================
# Finished
# ============================================================

$zipInfo = Get-Item $publishZipPath
$zipSizeMB = [Math]::Round($zipInfo.Length / 1MB, 2)

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host " Release created successfully!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "Version : $version" -ForegroundColor Cyan
Write-Host "ZIP     : $publishZipPath" -ForegroundColor Cyan
Write-Host "Size    : $zipSizeMB MB" -ForegroundColor Cyan
Write-Host ""