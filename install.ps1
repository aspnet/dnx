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

if (-not (Test-Path "$pwd\.git" -pathType container)) {
    # Clone https://github.com/Katana/ProjectSystem to current folder
    Write-Host "Cloning the repository for you..."
    git clone https://github.com/Katana/ProjectSystem.git
    cd .\ProjectSystem
}

. .\build.ps1

if (-not $env:PATH -like "*$pwd\scripts*") {
    # Update the PATH
    [Environment]::SetEnvironmentVariable("PATH", "$env:PATH;$pwd\scripts", "User")
}