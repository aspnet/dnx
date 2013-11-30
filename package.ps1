param($configuration = "Debug", $includeSymbols = $false)

.\build.ps1 $configuration

mkdir artifacts -force | Out-Null

mkdir artifacts\tools -force | Out-Null

cp scripts\* artifacts\tools

# Fixup scripts
$scripts = ls artifacts\tools -filter *.cmd

foreach($file in $scripts)
{
   $content = cat $file.FullName
   $content = $content | %{ $_.Replace("..\bin\Debug", "bin").Replace("..\src\", "").Replace("bin\Debug", "bin\$configuration") }
   Set-Content $file.FullName $content
}

# Manually fixup k.cmd to use compiled version of MS.Net.Project
$content = cat artifacts\tools\k.cmd | %{ $_.Replace("%~dp0k-run %~dp0Microsoft.Net.Project %*", "CALL %~dp0KLR %~dp0Microsoft.Net.Project\bin\$configuration\Microsoft.Net.Project.dll %*") }
Set-Content artifacts\tools\k.cmd $content

# Move the bin folder in here
mkdir artifacts\tools\bin -force | Out-Null
cp bin\$configuration\* artifacts\tools\bin\

if(!$includeSymbols)
{
    # Remove the stuff we don't need
    ls artifacts\tools\bin\*.pdb | rm
    ls artifacts\tools\bin\*.ilk | rm
    ls artifacts\tools\bin\*.lib | rm
    ls artifacts\tools\bin\*.exp | rm
}

# Now copy source
cp -r src\Microsoft.Net.ApplicationHost artifacts\tools -Force
cp -r src\Microsoft.Net.Project artifacts\tools -Force
cp -r src\Microsoft.Net.OwinHost artifacts\tools -Force
cp -r src\Microsoft.Net.Launch artifacts\tools -Force
cp -r src\Microsoft.Net.Runtime artifacts\tools -Force

if(!$includeSymbols)
{
    # Remove the stuff we don't need
    ls -r artifacts\tools\*obj | rm -r
    ls -r artifacts\tools\*\bin\*\*.xml | rm
}