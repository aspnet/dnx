msbuild .\nativelib.vcxproj /p:Configuration=Release /p:Platform=Win32
msbuild .\nativelib.vcxproj /p:Configuration=Release /p:Platform=x64
copy  .\x64\Release\nativelib.dll ..\src\NativeLib\runtimes\win7-x64\native\
copy  .\Release\nativelib.dll ..\src\NativeLib\runtimes\win7-x86\native\