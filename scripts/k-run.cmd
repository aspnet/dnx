@Echo OFF
SETLOCAL

IF "%K_APPBASE%"=="" (
  SET K_APPBASE=%CD%
)

SET ERRORLEVEL=

SET TARGET=%TARGET_FRAMEWORK%
SET HOST_BIN=..\bin\Win32\Debug

if "%TARGET%" == "" (
   SET TARGET=..\src\Microsoft.Net.ApplicationHost\bin\Debug\net45;%~dp0%..\src\Microsoft.Net.Runtime.Roslyn\bin\Debug\net45
)

CALL "%~dp0KLR" --lib "%~dp0%TARGET%" --lib "%~dp0%HOST_BIN%" "Microsoft.Net.ApplicationHost" %*

exit /b %ERRORLEVEL%

ENDLOCAL
