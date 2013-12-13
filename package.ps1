param($configuration = "Debug", $includeSymbols = $false, $runtimePath, $nightly = $false)

$sdkRoot = "artifacts\sdk"

.\build.ps1 $configuration

mkdir $sdkRoot -force | Out-Null

mkdir $sdkRoot\tools -force | Out-Null

cp scripts\* $sdkRoot\tools

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
cp bin\$configuration\* $sdkRoot\tools\bin\

# if the framework is local then build it against the frameworks
if((Test-Path $sdkRoot\Framework) -and (Test-Path $sdkRoot\tools\Microsoft.Net.Project))
{
    & $sdkRoot\tools\k.cmd build src\Microsoft.Net.Runtime.Interfaces
    & $sdkRoot\tools\k.cmd build src\klr.host
    & $sdkRoot\tools\k.cmd build src\Stubs
    
    cp -r src\Microsoft.Net.Runtime.Interfaces -filter *.dll $sdkRoot\tools\bin\ -force
    cp -r src\klr.host -filter *.dll $sdkRoot\tools\bin\ -force
    cp -r src\Stubs -filter *.dll $sdkRoot\tools\bin\ -force
    
    rm $sdkRoot\tools\bin\klr.host.dll
    rm $sdkRoot\tools\bin\Microsoft.Net.Runtime.Interfaces.dll
    rm -r -force $sdkRoot\tools\bin\*\bin\Debug
    rm -r -force $sdkRoot\tools\bin\*\Properties
    rm -r -force $sdkRoot\tools\bin\*\obj
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
cp -r src\Microsoft.Net.ApplicationHost -filter *.dll $sdkRoot\tools -Force
cp -r src\Microsoft.Net.Project -filter *.dll $sdkRoot\tools -Force
cp -r src\Microsoft.Net.OwinHost -filter *.dll $sdkRoot\tools -Force
cp -r src\Microsoft.Net.Launch $sdkRoot\tools -Force
cp src\Microsoft.Net.Runtime\Executable.cs $sdkRoot\tools\Microsoft.Net.Launch -Force
rm $sdkRoot\tools\Microsoft.Net.Launch\.include


# Remove the stuff we don't need
rm -r $sdkRoot\tools\*\obj 
rm -r -force $sdkRoot\tools\*\properties


# Copy the runtime
if($runtimePath) 
{
    cp -r $runtimePath\* $sdkRoot -force
}

# NuGet pack
cp ProjectK.nuspec $sdkRoot

$spec = [xml](cat $sdkRoot\ProjectK.nuspec)
$version = $spec.package.metadata.version

if($nightly)
{
    $now = [DateTime]::Now;
    $version += "-" + ($now.Year - 2011) + [DateTime]::Now.ToString("MMdd");
}

.nuget\NuGet.exe pack $sdkRoot\ProjectK.nuspec -o $sdkRoot -NoPackageAnalysis -version $version