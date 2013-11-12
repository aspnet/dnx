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

SET WatchDog=%~dp0..\WatchDog
SET WebHost=%~dp0..\WebHost\bin\Debug\WebHost.exe

SET WebUrl=%~1
IF "%WebUrl%"=="" SET WebUrl=http://localhost:8080

SET WebPath=%~f2
IF "%WebPath%"=="" SET WebPath=%CD%

::Echo K run %WatchDog% %WebHost% %WebPath% %WebUrl%

Call %~dp0\K run %WatchDog% %WebHost% %WebPath% %WebUrl% < Nul