@Echo OFF
:: K [command] [args]
::   command :  Required - Either 'build' or 'clean'.
::   args : Optional - Any further args will be passed directly to the app.

:: e.g. To compile the app in the current folder:
::      C:\src\MyApp\>K build

IF EXIST %~dp0k-%1.cmd (
  %~dp0k-%1.cmd %2 %3 %4 %5 %6 %7 %8 %9 
) ELSE (
  %~dp0k-run %~dp0..\src\Microsoft.Net.Project %*
)
