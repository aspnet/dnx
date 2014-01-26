param($configuration = "Debug", $runtimePath = $env:RuntimePath, $nightly = $false)

$ErrorActionPreference = "Stop"

trap
{
    Write-Error $_
    exit 1
}

function Verify-ExitCode
{
    if ($LASTEXITCODE -ne 0) 
    {
        exit 1
    }
}


# Get the executing script path
$path = Split-Path $MyInvocation.MyCommand.Path

# Root of the sdk
$sdkRoot = "$path\artifacts\sdk"

# The list of projects we're interested in harvesting
$hostProjects = @("Microsoft.Net.Runtime.Interfaces", "klr.host", "Stubs")
$runtimeProjects = @("Microsoft.Net.OwinHost", "Microsoft.Net.Runtime", "Microsoft.Net.Project", "Microsoft.Net.ApplicationHost")

# Make the sdk and tools folders
mkdir $sdkRoot -force | Out-Null
mkdir $sdkRoot\tools -force | Out-Null

# Copy the cmd files
cp $path\scripts\* $sdkRoot\tools

# Fixup scripts based on new relative paths in the resulting package
$scripts = ls $sdkRoot\tools -filter *.cmd

foreach($file in $scripts)
{
   $content = cat $file.FullName
   $content = $content | %{ 
        $s = $_.Replace("..\bin\Debug", "bin")
        $s = $s.Replace("..\src\", "")
        $s = $s.Replace("bin\Debug", "bin\$configuration")
        $s = $s.Replace("=Debug", "=$configuration")
        $s
   }
   Set-Content $file.FullName $content
}

# Move the bin folder in here
mkdir $sdkRoot\tools\bin -force | Out-Null
cp $path\bin\$configuration\* $sdkRoot\tools\bin\ -force

# If we're building ourselves this will be true
$bootstrapping = $false

# if the framework is local then build ourselves
if((Test-Path $sdkRoot\Framework) -and (Test-Path $sdkRoot\tools\Microsoft.Net.Project))
{
    # tools\bin\*
    $hostProjects | %{
        & $sdkRoot\tools\k.cmd build $path\src\$_
        Verify-ExitCode
        cp -r $path\src\$_ -filter *.dll $sdkRoot\tools\bin\ -force

        # Remove this file from the root if it exists there
        if(Test-Path $sdkRoot\tools\bin\$_.dll) {
            rm $sdkRoot\tools\bin\$_.dll
        }
    }
    
    # tools\* Skip OwinHost since it isn't buildable yet
    $runtimeProjects | Select -Skip 1 | %{
        & $sdkRoot\tools\k.cmd build $path\src\$_
        Verify-ExitCode
    }

    $bootstrapping = $true
}

# Copy the binaries to tools\*
$runtimeProjects | %{
    cp -r $path\src\$_ -filter *.dll $sdkRoot\tools -Force    
}

# Temporary special case for Microsoft.Net.Launch
cp -r $path\src\Microsoft.Net.Launch $sdkRoot\tools -Force
cp $path\src\Microsoft.Net.Runtime\Executable.cs $sdkRoot\tools\Microsoft.Net.Launch -Force
rm $sdkRoot\tools\Microsoft.Net.Launch\.include

# If we're bootstrapping then do some extra steps
if($bootstrapping)
{
    $runners = @("Microsoft.Net.Project", "Microsoft.Net.ApplicationHost")
    $targetFrameworks = @("k10", "net45")
    $packages = @("Newtonsoft.Json", "Microsoft.CodeAnalysis", "Microsoft.CodeAnalysis.CSharp", "System.Collections.Immutable", "System.Reflection.Metadata.Ecma335")

    # Copy the dependency graph to each runner folder
    $runners | %{
        $project = $_
        $targetFrameworks | %{
            $framework = $_
            cp $sdkRoot\tools\Microsoft.Net.Runtime\bin\$framework\Microsoft.Net.Runtime.dll $sdkRoot\tools\$project\bin\$framework
            $packages | %{
                $package = $_
                if ($package -eq "Newtonsoft.Json")
                {
                    ls $path\packages\$package.*\lib\netcore45\$package.dll | %{ cp $_.FullName $sdkRoot\tools\$project\bin\$framework } 
                }
                else
                {                
                    ls $path\packages\$package.*\lib\$framework\$package.dll | %{ cp $_.FullName $sdkRoot\tools\$project\bin\$framework }
                }
            }
        }
    }
}

# Nuke the Microsoft.Net.Runtime folder
rm -r -Force $sdkRoot\tools\Microsoft.Net.Runtime -ErrorAction SilentlyContinue

# Remove files we don't care about
$foldersToRemove = @("bin\$configuration", "obj", "properties")
$foldersToRemove | %{
    rm -r -force $sdkRoot\tools\bin\*\$_
}

$foldersToRemove | Select -Skip 1 | %{
    rm -r -force $sdkRoot\tools\*\$_
}

@("pdb", "ilk", "lib", "exp") | %{
    rm $sdkRoot\tools\bin\*.$_ | rm
}

# Copy the runtime
if($runtimePath) 
{
    cp -r $runtimePath\* $sdkRoot -force
}

# NuGet pack
cp $path\ProjectK.nuspec $sdkRoot

$spec = [xml](cat $sdkRoot\ProjectK.nuspec)
$version = $spec.package.metadata.version

if($nightly -or $env:Nightly -eq "1")
{
    $buildNumber = "000" + $env:BUILD_NUMBER;
    $buildNumber = $buildNumber.Substring($buildNumber.Length - 3);
    
    $now = [DateTime]::Now;
    $version += "-" + ($now.Year - 2011) + [DateTime]::Now.ToString("MMdd");
    $version += "-" + $buildNumber;
}

& $path\.nuget\NuGet.exe pack $sdkRoot\ProjectK.nuspec -o $sdkRoot -NoPackageAnalysis -version $version
Verify-ExitCode