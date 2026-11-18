<#
.SYNOPSIS
    Hot-reloads EdenOnline SQF functions into a running Arma 3 session.

.DESCRIPTION
    The Arma 3 SQF Reloader VS Code extension recompiles a function when the file is saved
    from the editor. File writes made by other tools (scripts, coding agents) do not raise
    that save event, so this script performs the same compile through the SqfReloader REST API.

    Each function is compiled from an immutable snapshot under the Arma installation's
    "z\SqfReloader\Snapshots" folder. Compiling the workspace file directly would make Arma
    lock the source file until it is restarted, preventing further edits.

.PARAMETER Function
    Function names to reload, for example "receiveObjectCreate" or "EOEX_fnc_receiveObjectCreate".
    Defaults to every fn_*.sqf file in the addon (except extension/init).

.PARAMETER ArmaDirectory
    Arma 3 installation folder. Detected from the registry when omitted.

.EXAMPLE
    .\ReloadArmaFunctions.ps1 -Function receiveObjectCreate

.EXAMPLE
    .\ReloadArmaFunctions.ps1
#>
param(
    [string[]]$Function = @(),
    [string]$ArmaDirectory,
    [int]$ApiPort = 8764,
    [int]$TimeoutMs = 30000
)

$ErrorActionPreference = "Stop"

$projectPath = Split-Path -Parent $PSScriptRoot
$sourceRoot = Join-Path $projectPath "@EdenOnline\addons\functions"
$functionPrefix = "EOEX_fnc_"

if (-not $ArmaDirectory) {
    $registryPaths = @(
        "HKLM:\SOFTWARE\WOW6432Node\bohemia interactive\arma 3",
        "HKLM:\SOFTWARE\bohemia interactive\arma 3"
    )
    foreach ($registryPath in $registryPaths) {
        if (Test-Path -LiteralPath $registryPath) {
            $ArmaDirectory = (Get-ItemProperty -LiteralPath $registryPath -ErrorAction SilentlyContinue).main
            if ($ArmaDirectory) { break }
        }
    }
}

if (-not $ArmaDirectory -or -not (Test-Path -LiteralPath $ArmaDirectory -PathType Container)) {
    throw "Arma 3 installation not found. Pass -ArmaDirectory <path to Arma 3>."
}

if (-not (Test-Path -LiteralPath $sourceRoot -PathType Container)) {
    throw "Function source folder not found: $sourceRoot"
}

# Accept both "receiveObjectCreate" and "EOEX_fnc_receiveObjectCreate".
# -File passes "a,b" as a single argument, so split comma separated lists as well.
$requested = @($Function | ForEach-Object { $_ -split "," } | ForEach-Object { $_.Trim() } | Where-Object { $_ } |
    ForEach-Object { $_ -replace "^$([regex]::Escape($functionPrefix))", "" })

$snapshotId = "agent\$((Get-Date).ToString('yyyyMMdd-HHmmss-fff'))"
$snapshotRoot = Join-Path $ArmaDirectory ("z\SqfReloader\Snapshots\$snapshotId\functions")
$snapshotArmaRoot = "z\SqfReloader\Snapshots\$snapshotId\functions"

$compiles = @()
$reloaded = @()

Get-ChildItem -LiteralPath $sourceRoot -Recurse -File -Filter "fn_*.sqf" | ForEach-Object {
    $relativePath = $_.FullName.Substring($sourceRoot.Length).TrimStart([char]'\')

    # Initialization functions must keep running the code they were loaded with.
    if ($relativePath -like "extension\init\*") { return }

    $name = $_.BaseName.Substring(3)
    if ($requested.Count -gt 0 -and $requested -notcontains $name) { return }

    $destination = Join-Path $snapshotRoot $relativePath
    New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
    Copy-Item -LiteralPath $_.FullName -Destination $destination -Force

    $armaPath = "$snapshotArmaRoot\$relativePath"
    $functionVariable = $functionPrefix + $name

    $compiles += "missionNamespace setVariable [""$functionVariable"", compile preprocessFileLineNumbers ""$armaPath""];"
    $reloaded += $functionVariable
}

if ($reloaded.Count -eq 0) {
    throw "No matching fn_*.sqf files were found for: $($Function -join ', ')"
}

$payload = @{
    code      = ($compiles -join "`n")
    timeoutMs = $TimeoutMs
} | ConvertTo-Json -Compress

$uri = "http://127.0.0.1:$ApiPort/api/eval"
$response = Invoke-RestMethod -Method Post -Uri $uri -ContentType "application/json" -Body $payload -TimeoutSec ([math]::Ceiling($TimeoutMs / 1000) + 10)

if ($response.success -eq $false) {
    Write-Host "Reload failed: $($response.error)" -ForegroundColor Red
    exit 1
}

Write-Host "Reloaded $($reloaded.Count) function(s):" -ForegroundColor Green
$reloaded | ForEach-Object { Write-Host "  $_" }
