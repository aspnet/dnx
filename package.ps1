param($configuration = "Debug", $includeSymbols = $false, $runtimePath = $env:RuntimePath, $nightly = $false)

$ErrorActionPreference = "Stop"

$path = Split-Path $MyInvocation.MyCommand.Path

$sdkRoot = "$path\artifacts\sdk"

mkdir $sdkRoot -force | Out-Null

mkdir $sdkRoot\tools -force | Out-Null

cp $path\scripts\* $sdkRoot\tools

# Fixup scripts
$scripts = ls $sdkRoot\tools -filter *.cmd

foreach($file in $scripts)
{
   $content = cat $file.FullName
   $content = $content | %{ $_.Replace("..\bin\Debug", "bin").Replace("..\src\", "").Replace("bin\Debug", "bin\$configuration") }
   Set-Content $file.FullName $content
}

# Move the bin folder in here
mkdir $sdkRoot\tools\bin -force | Out-Null
cp $path\bin\$configuration\* $sdkRoot\tools\bin\

$bootstrapping = $false

# if the framework is local then build it against the frameworks
if((Test-Path $sdkRoot\Framework) -and (Test-Path $sdkRoot\tools\Microsoft.Net.Project))
{
    & $sdkRoot\tools\k.cmd build $path\src\Microsoft.Net.Runtime.Interfaces
    & $sdkRoot\tools\k.cmd build $path\src\klr.host
    & $sdkRoot\tools\k.cmd build $path\src\Stubs
    
    # Stuff in the bin folder
    cp -r $path\src\Microsoft.Net.Runtime.Interfaces -filter *.dll $sdkRoot\tools\bin\ -force
    cp -r $path\src\klr.host -filter *.dll $sdkRoot\tools\bin\ -force
    cp -r $path\src\Stubs -filter *.dll $sdkRoot\tools\bin\ -force
    
    # Stuff one level up (TODO: OwinHost)
    & $sdkRoot\tools\k.cmd build $path\src\Microsoft.Net.Runtime
    & $sdkRoot\tools\k.cmd build $path\src\Microsoft.Net.Project
    & $sdkRoot\tools\k.cmd build $path\src\Microsoft.Net.ApplicationHost
    
    rm $sdkRoot\tools\bin\klr.host.dll
    rm $sdkRoot\tools\bin\Microsoft.Net.Runtime.Interfaces.dll
    rm -r -force $sdkRoot\tools\bin\*\bin\Debug
    rm -r -force $sdkRoot\tools\bin\*\Properties
    rm -r -force $sdkRoot\tools\bin\*\obj
    
    $bootstrapping = $true
}

if(!$includeSymbols)
{
    # Remove the stuff we don't need
    ls $sdkRoot\tools\bin\*.pdb | rm
    ls $sdkRoot\tools\bin\*.ilk | rm
    ls $sdkRoot\tools\bin\*.lib | rm
    ls $sdkRoot\tools\bin\*.exp | rm
}

# Now copy source
cp -r $path\src\Microsoft.Net.ApplicationHost -filter *.dll $sdkRoot\tools -Force
cp -r $path\src\Microsoft.Net.Project -filter *.dll $sdkRoot\tools -Force
cp -r $path\src\Microsoft.Net.OwinHost -filter *.dll $sdkRoot\tools -Force
cp -r $path\src\Microsoft.Net.Launch $sdkRoot\tools -Force
cp -r $path\src\Microsoft.Net.Runtime -filter *.dll $sdkRoot\tools -Force
cp $path\src\Microsoft.Net.Runtime\Executable.cs $sdkRoot\tools\Microsoft.Net.Launch -Force
rm $sdkRoot\tools\Microsoft.Net.Launch\.include

# If we're bootstrapping then do some extra steps
if($bootstrapping)
{
    # Copy dependencies to app host an family
    @("Microsoft.Net.Project", "Microsoft.Net.ApplicationHost") | %{
        $project = $_
        @("k10", "net45") | %{
            $framework = $_
            cp $sdkRoot\tools\Microsoft.Net.Runtime\bin\$framework\Microsoft.Net.Runtime.dll $sdkRoot\tools\$project\bin\$framework
            @("Newtonsoft.Json", "Microsoft.CodeAnalysis", "Microsoft.CodeAnalysis.CSharp", "System.Collections.Immutable", "System.Reflection.Metadata.Ecma335") | %{
                $package = $_
                ls $path\packages\$package.*\lib\$framework\$package.dll | %{ cp $_.FullName $sdkRoot\tools\$project\bin\$framework }
            }
        }
    }
}

# Remove the stuff we don't need
rm -r $sdkRoot\tools\*\obj 
rm -r -force $sdkRoot\tools\*\properties

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