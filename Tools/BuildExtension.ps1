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

Write-Host "Project Path: $projectPath" -ForegroundColor Blue
Write-Host ""

Build-Project -projectPath $projectPath