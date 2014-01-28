@Echo OFF
SETLOCAL
IF "%1"=="" (
  SET K_APPBASE=%CD%
) ELSE (
  SET K_APPBASE=%1
)

SET ERRORLEVEL=

SET TARGET=%TARGET_FRAMEWORK%
if "%TARGET%" == "" (
   SET TARGET=bin\Debug\net45
)

CALL "%~dp0KLR" "%~dp0..\src\Microsoft.Net.ApplicationHost\%TARGET%\Microsoft.Net.ApplicationHost.dll" %*

exit /b %ERRORLEVEL%

ENDLOCAL
