@Echo OFF
SETLOCAL
SET ERRORLEVEL=

"%~dp0dnx" %DNX_OPTIONS% "%~dp0lib\Microsoft.Framework.PackageManager\Microsoft.Framework.PackageManager.dll" %*

exit /b %ERRORLEVEL%
ENDLOCAL
