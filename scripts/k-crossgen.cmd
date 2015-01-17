@Echo OFF
SETLOCAL
SET ERRORLEVEL=

SET "DOTNET_RUNTIME_PATH=%~dp0."
SET "CROSSGEN_PATH=%~dp0crossgen.exe"

"%~dp0dotnet" --appbase "%CD%" --lib "%~dp0lib\Microsoft.Framework.Project" "Microsoft.Framework.Project" crossgen --exePath "%CROSSGEN_PATH%" --runtimePath "%DOTNET_RUNTIME_PATH%" %*

exit /b %ERRORLEVEL%
ENDLOCAL
