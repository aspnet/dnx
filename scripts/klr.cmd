@ECHO OFF
SETLOCAL

IF "%TARGET_FRAMEWORK%" == "k10" (
  SET K_OPTIONS=%K_OPTIONS% --core45
)

IF "%K_APPBASE%" NEQ "" (
  SET K_OPTIONS=%K_OPTIONS% --appbase "%K_APPBASE%"
)

::echo %~dp0..\bin\Debug\klr.exe %K_OPTIONS% %*

"%~dp0..\bin\Debug\klr.exe" %K_OPTIONS% %*
ENDLOCAL
