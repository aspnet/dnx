@Echo OFF
:: K [command] [args]
::   command :  Required - Name of the command to execute
::   args : Optional - Any further args will be passed directly to the command.

:: e.g. To compile the app in the current folder:
::      C:\src\MyApp\>K build

SETLOCAL

SET ERRORLEVEL=

if "%TARGET_FRAMEWORK%" == "" (
    SET FRAMEWORK=net45
)

if "%TARGET_FRAMEWORK%" == "k10" (
    SET FRAMEWORK=K
)

SET LIB_PATH=..\src\klr.host\bin\Debug\%FRAMEWORK%;%~dp0..\src\Microsoft.Net.Project\bin\Debug\%FRAMEWORK%
SET HOST_BIN=..\bin\Win32\Debug

IF EXIST "%~dp0k-%1.cmd" (
  "%~dp0k-%1.cmd" %2 %3 %4 %5 %6 %7 %8 %9 
) ELSE (
  CALL "%~dp0KLR" --lib "%~dp0%LIB_PATH%" --lib "%~dp0%HOST_BIN%" "Microsoft.Net.Project" %*
)

exit /b %ERRORLEVEL%

ENDLOCAL