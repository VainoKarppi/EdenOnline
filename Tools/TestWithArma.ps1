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

# Terminate-ExistingProcess

if (Build-Project -projectPath $projectPath -destinationPath $modFolder) {
    if (Pack-Addons -modFolder $modFolder) {
        if (Start-Arma) {
            # Combine Watch-ExtensionLog and Watch-RPTLog in the same console
            # E:\SteamLibrary\steamapps\common\Arma 3\EdenOnline_Logs

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

                # Read extension log output
                Receive-Job $extensionJob -ErrorAction SilentlyContinue |
                    ForEach-Object {
                        Write-Host "[EXT] $_"
                    }

                Start-Sleep -Milliseconds 50

                # Read RPT log output
                Receive-Job $rptJob -ErrorAction SilentlyContinue |
                    ForEach-Object {
                        Write-Host "[RPT] $_"
                    }

                Start-Sleep -Milliseconds 50
            }

            # Arma 3 has exited
            Write-Host ""
            Write-Host "Arma 3 has exited. Stopping log watchers..."

            Stop-Job $extensionJob -ErrorAction SilentlyContinue
            Stop-Job $rptJob -ErrorAction SilentlyContinue

            Remove-Job $extensionJob -Force -ErrorAction SilentlyContinue
            Remove-Job $rptJob -Force -ErrorAction SilentlyContinue

            Write-Host "Log watching stopped."
        }
    }
}