@Echo OFF
SETLOCAL
SET ERRORLEVEL=

REM <dev>
@Echo ON
REM </dev>

if "%TARGET_PLATFORM%" == "" (
    SET PLATFORM=x86
)

if "%TARGET_PLATFORM%" == "amd64" (
    SET PLATFORM=amd64
)

SET KLR_RUNTIME_PATH=%~dp0..\Runtime\%PLATFORM%
REM <dev>
SET KLR_RUNTIME_PATH=%~dp0..\artifacts\build\ProjectK\Runtime\x86
REM </dev>
SET CROSSGEN_PATH=%KLR_RUNTIME_PATH%\crossgen.exe

CALL "%~dp0KLR" "Microsoft.Net.Project" crossgen --exePath "%CROSSGEN_PATH%" --runtimePath "%KLR_RUNTIME_PATH%" %*

exit /b %ERRORLEVEL%
ENDLOCAL
