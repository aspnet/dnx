@ECHO OFF
SETLOCAL

SET K_OPTIONS=

IF "%K_APPBASE%" NEQ "" (
  SET K_OPTIONS=%K_OPTIONS% --appbase %K_APPBASE%
)

echo %~dp0..\bin\Debug\klr.exe %K_OPTIONS% %*

%~dp0..\bin\Debug\klr.exe %K_OPTIONS% %*
ENDLOCAL
