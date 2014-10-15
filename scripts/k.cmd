@Echo OFF
SETLOCAL
SET ERRORLEVEL=
:: K [command] [args]
::   command :  Required - Name of the command to execute
::   args : Optional - Any further args will be passed directly to the command.

:: e.g. To compile the app in the current folder:
::      C:\src\MyApp\>K build

REM <dev>
@Echo ON
REM </dev>

IF EXIST "%~dp0k-%1.cmd" (
  "%~dp0k-%1.cmd" %2 %3 %4 %5 %6 %7 %8 %9 
) ELSE (
  SET ARGS=%*
  IF NOT "%ARGS%"=="" SET ARGS=%ARGS:/?="/?"%
  CALL "%~dp0KLR.cmd" "Microsoft.Framework.ApplicationHost" %ARGS%
)

exit /b %ERRORLEVEL%
ENDLOCAL