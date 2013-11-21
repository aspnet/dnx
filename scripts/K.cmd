@Echo OFF
:: K [command] [path]
::   command :  Required - Either 'run' or 'compile'.
::   path : Optional - The path to the app folder. Defaults to PWD.
::   args : Optional - Any further args will be passed directly to the app.

:: e.g. To run the app in current folder:
::      C:\src\MyApp\>K run

:: e.g. To run the app in a specified folder:
::      C:\>K run C:\src\MyApp

:: e.g. To compile the app in the current folder:
::      C:\src\MyApp\>K compile

SET _cmd=%~1
IF "%_cmd%"=="" SET _cmd=run

SET _path=%~f2
IF "%_path%"=="" SET _path=%CD%

%~dp0..\bin\Debug\klr.exe %~dp0..\K\bin\Debug\K.dll %_cmd% %_path% %3 %4 %5 %6 %7 %8 %9
