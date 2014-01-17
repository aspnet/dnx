@Echo OFF

SET ERRORLEVEL=

"%~dp0\NuGet.exe" restore %*

exit /b %ERRORLEVEL%