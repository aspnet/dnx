@Echo OFF
SETLOCAL
SET ERRORLEVEL=

SET ARGS=%*
IF NOT "%ARGS%"=="" SET ARGS=%ARGS:/?="/?"%

"%~dp0klr" --appbase "%CD%" %K_OPTIONS% --lib "%~dp0lib\Microsoft.Framework.PackageManager" Microsoft.Framework.PackageManager --tools-path "%~dp0lib\Microsoft.Framework.PackageManager" %ARGS%

exit /b %ERRORLEVEL%
ENDLOCAL
