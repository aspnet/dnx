@Echo OFF
SETLOCAL
SET ERRORLEVEL=

REM <dev>
@Echo ON
REM </dev>

SET KLR_RUNTIME_PATH=%~dp0
REM <dev>
SET KLR_RUNTIME_PATH=%~dp0..\artifacts\build\ProjectK\Runtime\x86
REM </dev>
SET CROSSGEN_PATH=%KLR_RUNTIME_PATH%\crossgen.exe

CALL "%~dp0KLR" "Microsoft.Net.Project" build --crossgenPath "%CROSSGEN_PATH%" --runtimePath "%KLR_RUNTIME_PATH%" %*

exit /b %ERRORLEVEL%
ENDLOCAL
