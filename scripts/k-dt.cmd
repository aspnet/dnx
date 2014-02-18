@Echo OFF
SETLOCAL

IF "%K_APPBASE%"=="" (
  SET K_APPBASE=%CD%
)

SET ERRORLEVEL=

if "%TARGET_PLATFORM%" == "" (
    SET PLATFORM=x86
)

if "%TARGET_PLATFORM%" == "amd64" (
    SET PLATFORM=amd64
)

if "%TARGET_FRAMEWORK%" == "" (
    SET FRAMEWORK=net45
)

if "%TARGET_FRAMEWORK%" == "k10" (
    SET FRAMEWORK=K
)

SET LIB_PATH=%~dp0..\src\Microsoft.Net.DesignTimeHost\bin\Debug\%FRAMEWORK%;%~dp0..\src\Microsoft.Net.Runtime.Roslyn\bin\Debug\%FRAMEWORK%

CALL "%~dp0KLR" --lib "%LIB_PATH%" "Microsoft.Net.DesignTimeHost" %*

exit /b %ERRORLEVEL%

ENDLOCAL
