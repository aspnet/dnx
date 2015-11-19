if(Test-Path $PSScriptRoot\bin) {
    del -rec -for $PSScriptRoot\bin
}
if(Test-Path $PSScriptRoot\feed\RuntimeRestoreTest.1.0.0.nupkg) {
    del -for $PSScriptRoot\feed\RuntimeRestoreTest.1.0.0.nupkg
}

mkdir $PSScriptRoot\bin
mkdir $PSScriptRoot\bin\runtimes

dir $PSScriptRoot\Sample.*.cs | foreach {
    $rid = [IO.Path]::GetFileNameWithoutExtension($_.Name).Substring("Sample.".Length);
    Write-Host "Compiling $($_.Name) for $rid"
    mkdir $PSScriptRoot\bin\runtimes\$rid\lib\dnx451
    csc "$($_.FullName)" /out:"$PSScriptRoot\bin\runtimes\$rid\lib\dnx451\RuntimeRestoreTest.dll" /target:library
}

mkdir "$PSScriptRoot\bin\lib\dnx451"
csc "$PSScriptRoot\Default.cs" /out:"$PSScriptRoot\bin\lib\dnx451\RuntimeRestoreTest.dll" /target:library

& "$PSScriptRoot\..\..\..\.nuget\nuget.exe" pack "$PSScriptRoot\RuntimeRestoreTest.nuspec" -out "$PSScriptRoot\bin"

cp "$PSScriptRoot\bin\RuntimeRestoreTest.1.0.0.nupkg" "$PSScriptRoot\feed"
