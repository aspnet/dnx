@Echo OFF
SETLOCAL
SET ERRORLEVEL=
:: K [command] [args]
::   command :  Required - Name of the command to execute
::   args : Optional - Any further args will be passed directly to the command.

:: e.g. To compile the app in the current folder:
::      C:\src\MyApp\>K build

IF "%K_APPBASE%"=="" (
  SET "K_APPBASE=%CD%"
)

"%~dp0klr" --appbase "%K_APPBASE%" %K_OPTIONS% "Microsoft.Framework.ApplicationHost" %*

exit /b %ERRORLEVEL%
ENDLOCAL
