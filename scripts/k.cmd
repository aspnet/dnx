@Echo OFF
SETLOCAL
SET ERRORLEVEL=
:: K [command] [args]
::   command :  Required - Name of the command to execute
::   args : Optional - Any further args will be passed directly to the command.

:: e.g. To compile the app in the current folder:
::      C:\src\MyApp\>K build

IF "%KRE_APPBASE%"=="" (
  SET "KRE_APPBASE=%CD%"
)

"%~dp0klr" --appbase "%KRE_APPBASE%" %KRE_OPTIONS% "Microsoft.Framework.ApplicationHost" %*

exit /b %ERRORLEVEL%
ENDLOCAL
