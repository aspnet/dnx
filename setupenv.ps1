# Build the project
.\build.ps1

# Configure this session for K1
$env:K1Dir = $pwd
if (-not(Get-Module -Name "K")) {
    Import-Module (Join-Path (Join-Path $env:K1Dir Scripts) K.psm1)
}

# Modify the profile file to configure K1 for each new session
$K1StartComment = "# Load the K1 environment"
$updateProfile = $true

if (Test-Path -Path $profile) {
    if (Get-Content -Path $profile | Select-String $K1StartComment -Quiet) {
        # The profile is already set up
        $updateProfile = $false
    }
}

if ($updateProfile) {
    Add-Content -Path $profile -Value `n
    Add-Content -Path $profile -Value $K1StartComment
    Add-Content -Path $profile -Value "`$env:K1Dir = `"$pwd`""
    Add-Content -Path $profile -Value "Import-Module (Join-Path (Join-Path `$env:K1Dir Scripts) K.psm1)"
}

Write-Host "Environment successfully configured for K1!" -ForegroundColor Green
Write-Host "Type 'K' in your project folder to get started." -ForegroundColor Cyan