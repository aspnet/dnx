@Echo OFF
SETLOCAL
SET ERRORLEVEL=

SET ARGS=%*
SET ARGS_NO_QUOTE=%ARGS:"=%
IF NOT "%ARGS_NO_QUOTE%"=="" SET ARGS=%ARGS:/?="/?"%

"%~dp0klr" --appbase "%CD%" %K_OPTIONS% --lib "%~dp0lib\Microsoft.Framework.PackageManager" Microsoft.Framework.PackageManager --tools-path "%~dp0lib\Microsoft.Framework.PackageManager" %ARGS%

exit /b %ERRORLEVEL%
ENDLOCAL
