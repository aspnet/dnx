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
    Write-Host "Cloning the repository for you..." -ForegroundColor Cyan
    git clone https://github.com/Katana/ProjectSystem.git
    $scripts = "$pwd\ProjectSystem\scripts"
    cd .\ProjectSystem
}

. .\build.ps1

# Prompt to GAC & ngen Roslyn
$gacngen = ""
while ($gacngen -notmatch "[y|n]"){
    Write-Host ""
    Write-Host "GAC & NGen Roslyn? (makes app startup much faster, requires admin)" -ForegroundColor Cyan
    $gacngen = Read-Host "[Y/N]"
    Write-Host ""
}

if ($gacngen -eq "y"){
    cd .\roslyn
    Get-ChildItem | % {
        Write-Host "Installing $_.Name in the GAC"
        gacutil /i $_.Name /f /silent

        Write-Host "Generating native images for $_.Name"
        ngen install $_.Name /silent > $null
    }
    Write-Host "Roslyn files installed into GAC & native images generated" -ForegroundColor Cyan
    Write-Host ""
    cd ..
}

$envPath = [Environment]::GetEnvironmentVariable("PATH", "User")

if (-not ($envPath -like "*$scripts*")) {
    # Update the PATH
    [Environment]::SetEnvironmentVariable("PATH", "$envPath;$scripts", "User")

    Write-Host "Environment configured for K1, remember to restart your prompt!" -ForegroundColor Green
}
else {
    Write-Host "Done!" -ForegroundColor Green
}