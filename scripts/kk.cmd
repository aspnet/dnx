@Echo OFF
:: K [command] [path]
::   command :  Required - Either 'run' or 'build'.
::   path : Optional - The path to the app folder. Defaults to PWD.
::   args : Optional - Any further args will be passed directly to the app.

:: e.g. To run the app in current folder:
::      C:\src\MyApp\>K

:: e.g. To run the app in a specified folder:
::      C:\>K C:\src\MyApp

:: e.g. To compile the app in the current folder:
::      C:\src\MyApp\>KP build

call K %~dp0..\src\Microsoft.Net.Project %*
