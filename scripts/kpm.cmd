@Echo OFF
SETLOCAL
SET ERRORLEVEL=

REM <dev>
@Echo ON
REM </dev>

CALL "%~dp0KLR.cmd" --lib "%~dp0;%~dp0lib\Microsoft.Framework.PackageManager;%~dp0lib\Microsoft.Framework.Project" "Microsoft.Framework.PackageManager" %*

exit /b %ERRORLEVEL%
ENDLOCAL