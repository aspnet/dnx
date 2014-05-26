@Echo OFF
SETLOCAL
SET ERRORLEVEL=

REM <dev>
@Echo ON
REM </dev>

CALL "%~dp0KLR.cmd" --lib "%~dp0;%~dp0lib\Microsoft.Framework.PackageManager" "Microsoft.Framework.PackageManager" %*

exit /b %ERRORLEVEL%
ENDLOCAL