@Echo OFF
SETLOCAL
SET ERRORLEVEL=

"%~dp0klr" --appbase "%CD%" %K_OPTIONS% --lib "%~dp0lib\Microsoft.Framework.PackageManager" Microsoft.Framework.PackageManager %*

exit /b %ERRORLEVEL%
ENDLOCAL
