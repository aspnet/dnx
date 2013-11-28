@Echo OFF

SETLOCAL
SET _appHost=%~dp0..\src\Microsoft.Net.ApplicationHost\bin\Debug\Microsoft.Net.ApplicationHost.dll
SET _owinHost=%~dp0..\src\Microsoft.Net.OwinHost\bin\Debug\Microsoft.Net.OwinHost.dll
SET _watchDog=%~dp0..\src\Microsoft.Net.Launch
SET _klr=%~dp0..\bin\Debug\klr.exe

CALL %_klr% %_appHost% %_watchDog% %_klr% --appBase %CD% %_owinHost% /dev 1 %*
ENDLOCAL
