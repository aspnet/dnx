@Echo OFF
SETLOCAL
SET ERRORLEVEL=

REM <dev>
@Echo ON
REM </dev>

CALL "%~dp0KLR" "Microsoft.Net.Project" crossgen %*

exit /b %ERRORLEVEL%
ENDLOCAL
