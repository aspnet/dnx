param($configuration = "Debug")

$ErrorActionPreference = "Stop"

trap
{
    Write-Error $_
    exit 1
}

$path = Split-Path $MyInvocation.MyCommand.Path

# Restores nuget packages based on project.json file

if(!(Test-Path $path\.nuget\nuget.exe))
{
    if(!(Test-Path $path\.nuget))
    {
        mkdir $path\.nuget -Force | Out-Null
    }
    
    Write-Host "Downloading NuGet.exe" 
    (new-object net.webclient).DownloadFile("https://nuget.org/nuget.exe", "$path\.nuget\NuGet.exe")
}

# Package and install roslyn locally
ls $path\lib\roslyn -filter *.nuspec | %{ & $path\.nuget\nuget.exe pack $_.FullName -NoPackageAnalysis -o $_.Directory.FullName }
ls $path\lib\roslyn -filter *.nuspec | %{ & $path\.nuget\nuget.exe install $_.BaseName -o $path\packages -source $_.Directory.FullName }

# Normal nuget restore
& $path\.nuget\nuget.exe restore

# Restore nuget from project.json with special version of nuget
& $path\scripts\NuGet.exe restore

# Add the k10 profile for JSON.NET (copy of the portable profile)
ls $path\packages\Newtonsoft.Json.*\lib\netcore45\Newtonsoft.Json.dll | %{ $dir = (Join-Path $_.Directory.Parent.FullName "k10"); mkdir $dir -force > $null; cp $_.FullName $dir }

# Requires dev 12
$msb = Join-Path ${env:ProgramFiles(x86)} "MSBuild\12.0\Bin\MSBuild.exe"

if(!(Test-Path $msb))
{
    Write-Host "msbuild not found. Please ensure youhave VS2013 installed." -ForegroundColor Yellow
}

# Build the solution
& $msb $path\KRuntime.sln /m /p:Configuration=$configuration /v:quiet /nologo

