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
$modFolder = "$projectPath\Examples\@$((Get-BuildInfo).AssemblyName -replace '_x64$', '')"

if (-not (Test-Path -Path $modFolder)) {
    New-Item -Path $modFolder -ItemType Directory | Out-Null
    Write-Host "Created new mod folder: $modFolder" -ForegroundColor Green
    Write-Host "Please ensure the mod folder is set up correctly before proceeding!" -ForegroundColor Yellow
    Read-Host "Press Enter to continue..."
}

Write-Host "Project Path: $projectPath" -ForegroundColor Blue
Write-Host "Mod Path: $modFolder" -ForegroundColor Blue
Write-Host ""

Terminate-ExistingProcess

if (Build-Project -projectPath $projectPath -destinationPath $modFolder) {
    if (Pack-Addons -modFolder $modFolder) {
        if (Start-Arma) {
            # Combine Watch-ExtensionLog and Watch-RPTLog in the same console
            Start-Job -ScriptBlock {
                . "$using:functionsScript"
                Watch-ExtensionLog | ForEach-Object { Write-Host "[EXT] $_" }
            }

            Watch-RPTLog | ForEach-Object { Write-Host "[RPT] $_" }

            # Ask if the user wants to terminate Arma 3 before exit
            $userInput = Read-Host "Do you want to terminate Arma 3 before exiting? (Y/N)"

            if ($userInput -eq 'Y' -or $userInput -eq 'y') {
                # Terminate Arma 3 process
                $armaProcess = Get-Process -Name "arma3_x64" -ErrorAction SilentlyContinue
                if ($armaProcess) {
                    Write-Host "Terminating Arma 3..."
                    Stop-Process -Name "arma3_x64" -Force
                }
            } else {
                Write-Host "Arma 3 will remain running."
            }

            # Stop the background job
            Get-Job | Stop-Job | Remove-Job
        }
    }
}