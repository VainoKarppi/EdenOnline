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
$destinationPath = "$PSScriptRoot\callExtension"

Write-Host "Project Path: $projectPath" -ForegroundColor Blue
Write-Host "Destination Path: $destinationPath" -ForegroundColor Blue
Write-Host ""

Terminate-ExistingProcess
Download-CallExtension

if (Build-Project -projectPath $projectPath -destinationPath $destinationPath) {
    if (Prepare-TestScript -destinationPath $destinationPath) {
        if (Run-CallExtension -destinationPath $destinationPath -testScriptPath "$destinationPath\current_test.sqf") {
            Write-Host ""
            Write-Host ""
            Write-Host "=====================" -ForegroundColor Green -BackgroundColor DarkGray
            Write-Host "   ALL TESTS DONE!   " -ForegroundColor Green -BackgroundColor DarkGray
            Write-Host "=====================" -ForegroundColor Green -BackgroundColor DarkGray
            Write-Host ""
            Write-Host ""
        }
    }
}




