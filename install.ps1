# Check for git
try {
    git > $null
}
catch {
    Write-Host "git not found. Please install git from http://gitscm.org/ then run again." -ForegroundColor Yellow
    Exit
}


# Clone https://github.com/Katana/ProjectSystem to current folder
git clone https://github.com/Katana/ProjectSystem.git

cd .\ProjectSystem

. .\setupenv.ps1