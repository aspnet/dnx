param($configuration = "Debug", $includeSymbols = $false)

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

# Manually fixup k.cmd to use compiled version of MS.Net.Project
$content = cat $sdkRoot\tools\k.cmd | %{ $_.Replace("%~dp0k-run %~dp0Microsoft.Net.Project %*", "CALL %~dp0KLR %~dp0Microsoft.Net.Project\bin\$configuration\Microsoft.Net.Project.dll %*") }
Set-Content $sdkRoot\tools\k.cmd $content

# Move the bin folder in here
mkdir $sdkRoot\tools\bin -force | Out-Null
cp bin\$configuration\* $sdkRoot\tools\bin\

if(!$includeSymbols)
{
    # Remove the stuff we don't need
    ls $sdkRoot\tools\bin\*.pdb | rm
    ls $sdkRoot\tools\bin\*.ilk | rm
    ls $sdkRoot\tools\bin\*.lib | rm
    ls $sdkRoot\tools\bin\*.exp | rm
}

# Now copy source
cp -r src\Microsoft.Net.ApplicationHost $sdkRoot\tools -Force
cp -r src\Microsoft.Net.Project $sdkRoot\tools -Force
cp -r src\Microsoft.Net.OwinHost $sdkRoot\tools -Force
cp -r src\Microsoft.Net.Launch $sdkRoot\tools -Force
cp -r src\Microsoft.Net.Runtime $sdkRoot\tools -Force

if(!$includeSymbols)
{
    # Remove the stuff we don't need
    ls -r $sdkRoot\tools\*obj | rm -r
    ls -r $sdkRoot\tools\*\bin\*\*.xml | rm
}

# NuGet pack
cp ProjectK.nuspec $sdkRoot
.nuget\NuGet.exe pack $sdkRoot\ProjectK.nuspec -o $sdkRoot -NoPackageAnalysis