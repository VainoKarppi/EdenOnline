$functionsScript = Join-Path $PSScriptRoot "Functions.ps1"
try {
    . $functionsScript
    Write-Host "Functions loaded successfully." -ForegroundColor Green
} catch {
    Write-Host "Error loading functions: $_" -ForegroundColor Red
    exit 1
}

$projectPath = Get-ProjectPath
$buildInfo = Get-BuildInfo
if (-not $buildInfo) {
    exit 1
}

$modFolder = Join-Path $projectPath "@$($buildInfo.AssemblyName -replace '_x64$', '')"
if (-not (Test-Path -LiteralPath $modFolder -PathType Container)) {
    New-Item -Path $modFolder -ItemType Directory | Out-Null
    Write-Host "Created mod folder: $modFolder" -ForegroundColor Green
}

$srcFolder = Join-Path $projectPath "src"
$hashFilePath = Join-Path $modFolder "hash.txt"
$dllPath = Join-Path $modFolder "$($buildInfo.AssemblyName).dll"

if (-not (Test-Path -LiteralPath $srcFolder -PathType Container)) {
    throw "Source folder not found: $srcFolder"
}

function Get-SourceHash {
    param (
        [Parameter(Mandatory = $true)]
        [string]$SourcePath
    )

    $fileHashes = Get-ChildItem -LiteralPath $SourcePath -Recurse -File |
        Sort-Object -Property FullName |
        ForEach-Object {
            $relativePath = $_.FullName.Substring($SourcePath.Length).TrimStart([char]'\')
            "$relativePath`:$((Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash)"
        }

    $combinedHashes = $fileHashes -join "`n"
    $sha256 = [System.Security.Cryptography.SHA256]::Create()
    try {
        $hashBytes = $sha256.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($combinedHashes))
        return [BitConverter]::ToString($hashBytes).Replace("-", "")
    } finally {
        $sha256.Dispose()
    }
}

$currentHash = Get-SourceHash -SourcePath $srcFolder
$previousHash = $null
if (Test-Path -LiteralPath $hashFilePath -PathType Leaf) {
    $previousHash = (Get-Content -LiteralPath $hashFilePath -Raw).Trim()
}

Write-Host "Project Path: $projectPath" -ForegroundColor Blue
Write-Host "Mod Path: $modFolder" -ForegroundColor Blue
Write-Host ""

if ((Test-Path -LiteralPath $dllPath -PathType Leaf) -and $previousHash -and $previousHash -eq $currentHash) {
    Write-Host "No source changes detected and the DLL exists. Skipping build." -ForegroundColor Blue
    exit 0
}

if (-not (Build-Project -projectPath $projectPath -destinationPath $modFolder)) {
    exit 1
}

Get-SourceHash -SourcePath $srcFolder | Set-Content -LiteralPath $hashFilePath
Write-Host "DLL is ready in $modFolder. Arma was not started." -ForegroundColor Green
