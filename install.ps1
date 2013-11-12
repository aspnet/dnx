# Check for git
try {
    git > $null
}
catch {
    Write-Host "git not found. Please install git from http://gitscm.org/ then run again." -ForegroundColor Yellow
    Exit 1
}

# Check for msbuild
try {
    msbuild > $null
}
catch {
    Write-Host "msbuild not found. Please ensure you're running in a Visual Studio developer command prompt." -ForegroundColor Yellow
    Exit 1
}

$scripts = "$pwd\scripts"

if (-not (Test-Path "$pwd\.git" -pathType container)) {
    # Clone https://github.com/Katana/ProjectSystem to current folder
    Write-Host "Cloning the repository for you..." -ForegroundColor Yellow
    git clone https://github.com/Katana/ProjectSystem.git
    $scripts = "$pwd\ProjectSystem\scripts"
    pushd .\ProjectSystem
}

. .\build.ps1

Write-Host "scripts folder at $scripts"

if (-not ($env:PATH -like "*$scripts*")) {
    # Update the PATH
    [Environment]::SetEnvironmentVariable("PATH", "$env:PATH;$scripts", "User")

    Write-Host "Environment configured for K1, remember to restart your prompt!" -ForegroundColor Yellow
}