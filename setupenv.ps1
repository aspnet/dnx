# Build the project
.\build.ps1

# Configure this session for K1
$env:K1Dir = $pwd
. (Join-Path (Join-Path $env:K1Dir Scripts) K.ps1)

# Modify the profile file to configure K1 for each new session
Add-Content -Path $profile -Value `n
Add-Content -Path $profile -Value "# Load the K1 environment"
Add-Content -Path $profile -Value "`$env:K1Dir = `"$pwd`""
Add-Content -Path $profile -Value ". (Join-Path (Join-Path `$env:K1Dir Scripts) K.ps1)"

Write-Host "Environment successfully configured for K1!" -ForegroundColor Green
Write-Host "Type 'K' in your project folder to get started." -ForegroundColor Cyan