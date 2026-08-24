# Import the function script.
$functionsScript = Join-Path $PSScriptRoot "Functions.ps1"

try {
    . $functionsScript
    Write-Host "Functions loaded successfully." -ForegroundColor Green
}
catch {
    Write-Host "Error loading functions: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# Resolve project and mod paths.
$projectPath = Get-ProjectPath
$buildInfo = Get-BuildInfo

$assemblyName = $buildInfo.AssemblyName -replace '_x64$', ''
$modFolder = Join-Path $projectPath "@$assemblyName"

if (-not (Test-Path -LiteralPath $modFolder -PathType Container)) {
    New-Item `
        -Path $modFolder `
        -ItemType Directory `
        -Force | Out-Null

    Write-Host "Created new mod folder: $modFolder" -ForegroundColor Green
    Write-Host "Please ensure the mod folder is set up correctly before proceeding!" -ForegroundColor Yellow

    Read-Host "Press Enter to continue..."
}

Write-Host "Project Path: $projectPath" -ForegroundColor Blue
Write-Host "Mod Path: $modFolder" -ForegroundColor Blue
Write-Host ""

# Add function names to SQF files if they do not already exist.
Add-SQFFunctionTags -ModFolder $modFolder

# Build the project.
if (-not (Build-Project `
    -projectPath $projectPath `
    -destinationPath $modFolder)) {

    Write-Host "Project build failed." -ForegroundColor Red
    exit 1
}

# Pack all addons.
if (-not (Pack-Addons -modFolder $modFolder)) {
    Write-Host "Addon packing failed." -ForegroundColor Red
    exit 1
}

function Write-LogLine {
    param (
        [Parameter(Mandatory = $true)]
        [string]$Type,

        [AllowEmptyString()]
        [string]$Message
    )

    $prefix = "[$Type] "

    # Log-level colors take priority.
    if ($Message -match "ERROR:\s*") {
        $color = "Red"
    }
    elseif ($Message -match "DEBUG:\s*") {
        $color = "DarkGray"
    }
    elseif ($Message -match "INFO:\s*") {
        $color = "Cyan"
    }
    elseif ($Type -eq "EXT") {
        $color = "Yellow"
    }
    elseif ($Type -eq "RPT") {
        $color = "Green"
    }
    else {
        $color = "White"
    }

    Write-Host $prefix -NoNewline -ForegroundColor $color
    Write-Host $Message -ForegroundColor $color
}

# Start Arma 3.
if (-not (Start-Arma)) {
    Write-Host "Failed to start Arma 3." -ForegroundColor Red
    exit 1
}

# Start log watchers.
$extensionJob = $null
$rptJob = $null

try {
    $extensionJob = Start-Job -ScriptBlock {
        param ($functionsScript)

        . $functionsScript
        Watch-ExtensionLog
    } -ArgumentList $functionsScript

    $rptJob = Start-Job -ScriptBlock {
        param ($functionsScript)

        . $functionsScript
        Watch-RPTLog
    } -ArgumentList $functionsScript

    Write-Host "Log watchers started." -ForegroundColor Green

    # Monitor both log watchers while Arma 3 is running.
    while (Get-Process -Name "arma3_x64" -ErrorAction SilentlyContinue) {

        Receive-Job `
            -Job $extensionJob `
            -ErrorAction SilentlyContinue |
            ForEach-Object {
                Write-LogLine -Type "EXT" -Message $_.ToString()
            }

        Receive-Job `
            -Job $rptJob `
            -ErrorAction SilentlyContinue |
            ForEach-Object {
                Write-LogLine -Type "RPT" -Message $_.ToString()
            }

        Start-Sleep -Milliseconds 100
    }

    Write-Host ""
    Write-Host "Arma 3 has exited. Stopping log watchers..." -ForegroundColor Yellow
}
finally {
    if ($null -ne $extensionJob) {
        Stop-Job `
            -Job $extensionJob `
            -ErrorAction SilentlyContinue

        Remove-Job `
            -Job $extensionJob `
            -Force `
            -ErrorAction SilentlyContinue
    }

    if ($null -ne $rptJob) {
        Stop-Job `
            -Job $rptJob `
            -ErrorAction SilentlyContinue

        Remove-Job `
            -Job $rptJob `
            -Force `
            -ErrorAction SilentlyContinue
    }

    Write-Host "Log watching stopped." -ForegroundColor Yellow
}