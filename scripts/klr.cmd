@ECHO OFF
SETLOCAL

set ERRORLEVEL=

IF "%K_APPBASE%" NEQ "" (
  SET K_OPTIONS=%K_OPTIONS% --appbase "%K_APPBASE%"
)

if "%TARGET_FRAMEWORK%" == "" (
    SET FRAMEWORK=net45
)

if "%TARGET_FRAMEWORK%" == "k10" (
    SET FRAMEWORK=K
    SET K_OPTIONS=%K_OPTIONS% --core45
)

SET LIB_PATH=%~dp0..\src\klr.host\bin\Debug\%FRAMEWORK%

"%~dp0..\bin\Win32\Debug\klr.exe" %K_OPTIONS% --lib "%LIB_PATH%" %*

exit /b %ERRORLEVEL%

ENDLOCAL
