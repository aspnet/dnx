@Echo OFF
SETLOCAL
SET ERRORLEVEL=

SET "KRE_RUNTIME_PATH=%~dp0."
SET "CROSSGEN_PATH=%~dp0crossgen.exe"

"%~dp0klr" --appbase "%CD%" --lib "%~dp0lib\Microsoft.Framework.Project" "Microsoft.Framework.Project" crossgen --exePath "%CROSSGEN_PATH%" --runtimePath "%KRE_RUNTIME_PATH%" %*

exit /b %ERRORLEVEL%
ENDLOCAL
