@Echo OFF

SET ERRORLEVEL=

"%~dp0\NuGet.exe" install %*

exit /b %ERRORLEVEL%