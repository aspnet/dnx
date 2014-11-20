@Echo OFF
SETLOCAL
SET ERRORLEVEL=

klr --appbase %CD% %K_OPTIONS% --lib "%~dp0lib\Microsoft.Framework.PackageManager" Microsoft.Framework.PackageManager --tools-path "%~dp0lib\Microsoft.Framework.PackageManager" %*

exit /b %ERRORLEVEL%
ENDLOCAL
