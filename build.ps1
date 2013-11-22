# Check for msbuild
try {
    msbuild > $null
}
catch {
    Write-Host "msbuild not found. Please ensure you're running in a Visual Studio developer command prompt." -ForegroundColor Yellow
    Exit 1
}

# Restores nuget packages based on project.json file

if(!(Test-Path .nuget\nuget.exe))
{
    Write-Host "Downloading NuGet.exe"
    curl http://nuget.org/nuget.exe -outfile .nuget\nuget.exe
}

# Normal nuget restore
& .nuget\nuget.exe restore

# Restore nuget from project.json with special version of nuget
scripts/NuGet.exe restore

# Build the solution
msbuild KRuntime.sln /m /v:quiet /nologo
