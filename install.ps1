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

# Clone https://github.com/Katana/ProjectSystem to current folder
git clone https://github.com/Katana/ProjectSystem.git

cd .\ProjectSystem

. .\setupenv.ps1

pwd