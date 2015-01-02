@Echo OFF
SETLOCAL
SET ERRORLEVEL=

"%~dp0dotnet" %DOTNET_OPTIONS% --lib "%~dp0lib\Microsoft.Framework.Project" "%~dp0lib\Microsoft.Framework.PackageManager\Microsoft.Framework.PackageManager.dll" %*

exit /b %ERRORLEVEL%
ENDLOCAL
