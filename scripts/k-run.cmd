@Echo OFF
SETLOCAL
IF "%1"=="" (
  SET K_APPBASE=%CD%
) ELSE (
  SET K_APPBASE=%1
)

SET ERRORLEVEL=

SET TARGET=%TARGET_FRAMEWORK%
SET HOST_BIN=..\bin\Debug

if "%TARGET%" == "" (
   SET TARGET=..\src\Microsoft.Net.ApplicationHost\bin\Debug\net45;%~dp0%..\src\Microsoft.Net.Runtime.Roslyn\bin\Debug\net45
)

CALL "%~dp0KLR" "%~dp0%TARGET%;%~dp0%HOST_BIN%" "Microsoft.Net.ApplicationHost" %*

exit /b %ERRORLEVEL%

ENDLOCAL
