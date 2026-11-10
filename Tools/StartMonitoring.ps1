# Import the functions used by the log watchers.
$functionsScript = Join-Path $PSScriptRoot "Functions.ps1"
. $functionsScript

function Write-LogLine {
    param (
        [string]$Type,
        [string]$Message
    )

    $prefix = "[$Type] "

    if ($Message -match "ERROR:\s*") {
        $color = "Red"
    } elseif ($Message -match "DEBUG:\s*") {
        $color = "Gray"
    } elseif ($Message -match "INFO:\s*") {
        $color = "Cyan"
    } elseif ($Type -eq "EXT") {
        $color = "DarkGray"
    } elseif ($Type -eq "RPT") {
        $color = "Green"
    } else {
        $color = "White"
    }

    Write-Host $prefix -NoNewline -ForegroundColor $color
    Write-Host $Message -ForegroundColor $color
}

$extensionJob = Start-Job -ScriptBlock {
    . "$using:functionsScript"
    Watch-ExtensionLog
}

$rptJob = Start-Job -ScriptBlock {
    . "$using:functionsScript"
    Watch-RPTLog
}

$responseDeadline = (Get-Date).AddSeconds(60)
$armaResponded = $false
$armaStarted = $false

try {
    while ($true) {
        $armaProcesses = Get-Process -Name "arma3_x64" -ErrorAction SilentlyContinue

        if ($armaProcesses) {
            $armaStarted = $true

            if (-not $armaResponded -and (Get-Date) -lt $responseDeadline) {
                $armaProcess = $armaProcesses | Select-Object -First 1
                if ($armaProcess.Responding) {
                    $armaResponded = $true
                    Write-Host "Arma 3 is responding." -ForegroundColor Green
                }
            }

            if (-not $armaResponded -and (Get-Date) -ge $responseDeadline) {
                Write-Host "Arma 3 did not respond within 60 seconds; continuing to monitor until it exits." -ForegroundColor Yellow
                $armaResponded = $true
            }
        } elseif ($armaStarted -or (Get-Date) -ge $responseDeadline) {
            break
        }

        Receive-Job $extensionJob -ErrorAction SilentlyContinue |
            ForEach-Object {
                Write-LogLine -Type "EXT" -Message $_
            }

        Receive-Job $rptJob -ErrorAction SilentlyContinue |
            ForEach-Object {
                Write-LogLine -Type "RPT" -Message $_
            }

        Start-Sleep -Milliseconds 100
    }

    Write-Host ""
    if ($armaStarted) {
        Write-Host "Arma 3 has exited. Stopping log watchers..." -ForegroundColor Yellow
    } else {
        Write-Host "Arma 3 did not start within 10 seconds. Stopping log watchers..." -ForegroundColor Red
    }
} finally {
    Stop-Job $extensionJob, $rptJob -ErrorAction SilentlyContinue
    Remove-Job $extensionJob, $rptJob -Force -ErrorAction SilentlyContinue
}

Write-Host "Log watching stopped." -ForegroundColor Yellow
