.\build.ps1

mkdir artifacts -force | Out-Null

mkdir artifacts\tools -force | Out-Null

cp scripts\* artifacts\tools

# Fixup scripts
$scripts = ls artifacts\tools -filter *.cmd

foreach($file in $scripts)
{
   $content = cat $file.FullName
   $content = $content | %{ $_.Replace("..\bin\Debug", "bin").Replace("..\src\", "") }
   Set-Content $file.FullName $content
}

# Manually fixup k.cmd to use compiled version of MS.Net.Project
$content = cat artifacts\tools\k.cmd | %{ $_.Replace("%~dp0k-run %~dp0Microsoft.Net.Project %*", "CALL %~dp0KLR %~dp0Microsoft.Net.Project\bin\Debug\Microsoft.Net.Project.dll %*") }
Set-Content artifacts\tools\k.cmd $content

# Move the bin folder in here
mkdir artifacts\tools\bin -force | Out-Null
cp bin\Debug\* artifacts\tools\bin\

# Now copy source
cp -r src\Microsoft.Net.ApplicationHost artifacts\tools -Force
cp -r src\Microsoft.Net.Project artifacts\tools -Force
cp -r src\Microsoft.Net.OwinHost artifacts\tools -Force
cp -r src\Microsoft.Net.Launch artifacts\tools -Force
cp -r src\Microsoft.Net.Runtime artifacts\tools -Force

# Remove the stuff we don't need
ls -r artifacts\tools\*obj | rm -r
ls -r artifacts\tools\*\bin\*\*.xml | rm