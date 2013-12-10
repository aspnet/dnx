@Echo OFF
SETLOCAL
IF "%1"=="" (
  SET K_APPBASE=%CD%
) ELSE (
  SET K_APPBASE=%1
)
CALL "%~dp0KLR" "%~dp0..\src\Microsoft.Net.ApplicationHost\bin\Debug\Microsoft.Net.ApplicationHost.dll" %*
ENDLOCAL
