@ECHO OFF
SETLOCAL

set ERRORLEVEL=

IF "%TARGET_FRAMEWORK%" == "k10" (
  SET K_OPTIONS=%K_OPTIONS% --core45
)

IF "%K_APPBASE%" NEQ "" (
  SET K_OPTIONS=%K_OPTIONS% --appbase "%K_APPBASE%"
)

"%~dp0..\bin\Win32\Debug\klr.exe" %K_OPTIONS% %*

exit /b %ERRORLEVEL%

ENDLOCAL
