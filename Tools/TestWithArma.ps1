# Import the function script
$functionsScript = "$PSScriptRoot\Functions.ps1"
try {
    . $functionsScript
    Write-Host "Functions loaded successfully." -ForegroundColor Green
} catch {
    Write-Host "Error loading functions: $_" -ForegroundColor Red
    exit 1
}

$projectPath = Get-ProjectPath
$modFolder = "$projectPath\@$((Get-BuildInfo).AssemblyName -replace '_x64$', '')"

if (-not (Test-Path -Path $modFolder)) {
    New-Item -Path $modFolder -ItemType Directory | Out-Null
    Write-Host "Created new mod folder: $modFolder" -ForegroundColor Green
    Write-Host "Please ensure the mod folder is set up correctly before proceeding!" -ForegroundColor Yellow
    Read-Host "Press Enter to continue..."
}

Write-Host "Project Path: $projectPath" -ForegroundColor Blue
Write-Host "Mod Path: $modFolder" -ForegroundColor Blue
Write-Host ""

# Add function name on top of each file, if it doesent exists. (helps when using search)
Add-SQFFunctionTags -ModFolder $modFolder

# Terminate-ExistingProcess

if (Build-Project -projectPath $projectPath -destinationPath $modFolder) {
    if (Pack-Addons -modFolder $modFolder) {
        function Write-LogLine {
            param (
                [string]$Type,
                [string]$Message
            )

            $prefix = "[$Type] "

            # Log-level colors take priority
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


        if (Start-Arma) {
            $armaPath = Get-ArmaPath

            # Start extension log watcher
            $extensionJob = Start-Job -ScriptBlock {
                . "$using:functionsScript"
                Watch-ExtensionLog
            }

            # Start RPT log watcher
            $rptJob = Start-Job -ScriptBlock {
                . "$using:functionsScript"
                Watch-RPTLog
            }

            # Monitor both watchers while Arma is running
            while (Get-Process -Name "arma3_x64" -ErrorAction SilentlyContinue) {

                # Extension log
                Receive-Job $extensionJob -ErrorAction SilentlyContinue |
                    ForEach-Object {
                        Write-LogLine -Type "EXT" -Message $_
                    }
                
                Start-Sleep -Milliseconds 50

                # RPT log
                Receive-Job $rptJob -ErrorAction SilentlyContinue |
                    ForEach-Object {
                        Write-LogLine -Type "RPT" -Message $_
                    }

                Start-Sleep -Milliseconds 50
            }

            # Arma 3 has exited
            Write-Host ""
            Write-Host "Arma 3 has exited. Stopping log watchers..." -ForegroundColor Yellow

            Stop-Job $extensionJob -ErrorAction SilentlyContinue
            Stop-Job $rptJob -ErrorAction SilentlyContinue

            Remove-Job $extensionJob -Force -ErrorAction SilentlyContinue
            Remove-Job $rptJob -Force -ErrorAction SilentlyContinue

            Write-Host "Log watching stopped." -ForegroundColor Yellow
        }
    }
}