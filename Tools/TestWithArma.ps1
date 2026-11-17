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
        if (Start-Arma) {
            & (Join-Path $PSScriptRoot "StartMonitoring.ps1")
        }
    }
}