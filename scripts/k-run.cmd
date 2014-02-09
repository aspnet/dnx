@Echo OFF
SETLOCAL

IF "%K_APPBASE%"=="" (
  SET K_APPBASE=%CD%
)

SET ERRORLEVEL=

if "%TARGET_FRAMEWORK%" == "" (
    SET FRAMEWORK=net45
)

if "%TARGET_FRAMEWORK%" == "k10" (
    SET FRAMEWORK=K
)

SET LIB_PATH=..\src\klr.host\bin\Debug\%FRAMEWORK%;%~dp0..\src\Microsoft.Net.ApplicationHost\bin\Debug\%FRAMEWORK%;%~dp0..\src\Microsoft.Net.Runtime.Roslyn\bin\Debug\%FRAMEWORK%
SET HOST_BIN=..\bin\Win32\Debug

CALL "%~dp0KLR" --lib "%~dp0%LIB_PATH%" --lib "%~dp0%HOST_BIN%" "Microsoft.Net.ApplicationHost" %*

exit /b %ERRORLEVEL%

ENDLOCAL
