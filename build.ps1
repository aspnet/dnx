param($configuration = "Debug", $buildSolution = $true)

# Restores nuget packages based on project.json file

if(!(Test-Path .nuget\nuget.exe))
{
    Write-Host "Downloading NuGet.exe"
    curl http://nuget.org/nuget.exe -outfile .nuget\nuget.exe
}

# Package and install roslyn locally
ls lib\roslyn -filter *.nuspec | %{ & .nuget\nuget.exe pack $_.FullName -NoPackageAnalysis -o $_.Directory.FullName }
ls lib\roslyn -filter *.nuspec | %{ & .nuget\nuget.exe install $_.BaseName -o packages -source $_.Directory.FullName }

# Normal nuget restore
& .nuget\nuget.exe restore

# Restore nuget from project.json with special version of nuget
scripts/NuGet.exe restore

# Add the k10 profile for JSON.NET (copy of the portable profile)
ls .\packages\Newtonsoft.Json.*\lib\netcore45\Newtonsoft.Json.dll | %{ $dir = (Join-Path $_.Directory.Parent.FullName "k10"); mkdir $dir -force > $null; cp $_.FullName $dir }

# Requires dev 12
$msb = Join-Path ${env:ProgramFiles(x86)} "MSBuild\12.0\Bin\MSBuild.exe"

if(!(Test-Path $msb))
{
    Write-Host "msbuild not found. Please ensure youhave VS2013 installed." -ForegroundColor Yellow
}

# Build the solution
& $msb KRuntime.sln /m /p:Configuration=$configuration /v:quiet /nologo

