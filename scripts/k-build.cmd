@Echo OFF
SETLOCAL
SET ERRORLEVEL=

CALL "%~dp0KLR" "Microsoft.Net.Project" build %*

exit /b %ERRORLEVEL%
ENDLOCAL
