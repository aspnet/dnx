@Echo OFF
:: Web [url] [path]
::   url :  Optional - The URL to listen on. Defaults to http://localhost:8080
::   path : Optional - The path to the web app folder. Defaults to PWD.

:: e.g. To run the current folder on the default URL:
::      C:\src\MyWebApp\>Web

:: e.g. To run the current folder on a specific URL:
::      C:\src\MyWebApp\>Web "http://localhost:9001"

:: e.g. To run a specific folder on the default URL:
::      C:\>Web "http://localhost:9001" C:\src\MyWebApp

SETLOCAL
SET K_APPBASE=%CD%
CALL %~dp0KLR %~dp0..\src\Microsoft.Net.ApplicationHost\bin\Debug\Microsoft.Net.ApplicationHost.dll %~dp0..\src\Microsoft.Net.OwinHost %*
ENDLOCAL

:: todo - the rest

::SET _klr=%~dp0..\bin\Debug\klr.exe
::SET _k=%~dp0..\src\K\bin\Debug\K.dll
::SET _watchDog=%~dp0..\src\WatchDog
::SET _webHost=%~dp0..\src\WebHost\bin\Debug\WebHost.dll

::SET _url=%~1
::IF "%_url%"=="" SET _url=http://localhost:8080

;;SET _path=%~f2
::IF "%_path%"=="" SET _path=%CD%

::Echo K run %_watchDog% %_webHost% %_path% %_url%
::Call %_klr% %_k% run %_watchDog% %_klr% %_webHost% %_path% %_url% < Nul
