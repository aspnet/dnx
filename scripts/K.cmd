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

::Echo %~dp0..\K\bin\Debug\K.exe %*

%~dp0..\K\bin\Debug\K.exe %*