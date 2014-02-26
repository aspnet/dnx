@ECHO OFF
SETLOCAL
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
    SET FRAMEWORK=k10
    SET K_OPTIONS=--core45 %K_OPTIONS%
)

IF "%K_APPBASE%"=="" (
  SET K_APPBASE=%CD%
)

"%~dp0bin\%PLATFORM%\klr.exe" --appbase "%K_APPBASE%" %K_OPTIONS% --lib "%~dp0%FRAMEWORK%" %*

exit /b %ERRORLEVEL%
ENDLOCAL
