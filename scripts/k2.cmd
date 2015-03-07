@Echo OFF
SETLOCAL
SET ERRORLEVEL=
:: K [command] [args]
::   command :  Required - Name of the command to execute
::   args : Optional - Any further args will be passed directly to the command.

:: e.g. To compile the app in the current folder:
::      C:\src\MyApp\>K build

IF "%DNX_APPBASE%"=="" (
  SET "DNX_APPBASE=%CD%"
)

"%~dp0dnx" --appbase "%DNX_APPBASE%" %DNX_OPTIONS% "Microsoft.Framework.ApplicationHost2" %*

exit /b %ERRORLEVEL%
ENDLOCAL
