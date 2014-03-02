@ECHO OFF
SETLOCAL
SET ERRORLEVEL=

REM <dev>
@Echo ON
REM </dev>

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
    REM <dev>
    SET FRAMEWORK=K
    REM </dev>
    SET K_OPTIONS=--core45 %K_OPTIONS%
)

IF "%K_APPBASE%"=="" (
  SET K_APPBASE=%CD%
)

SET KLR_EXE_PATH=%~dp0bin\%PLATFORM%\klr.exe
SET KLR_LIB_PATH=%~dp0%FRAMEWORK%

REM <dev>
SET KLR_EXE_PATH=%~dp0..\bin\Win32\Debug\klr.exe
SET KLR_LIB_PATH=%~dp0..\src\klr.host\bin\Debug\%FRAMEWORK%

IF "%~1" == "Microsoft.Net.ApplicationHost" (
    SET KLR_LIB_PATH=%KLR_LIB_PATH%;%~dp0..\src\Microsoft.Net.ApplicationHost\bin\Debug\%FRAMEWORK%;%~dp0..\src\Microsoft.Net.Runtime.Roslyn\bin\Debug\%FRAMEWORK%
) ELSE IF "%~1" == "Microsoft.Net.Project" (
    SET KLR_LIB_PATH=%KLR_LIB_PATH%;%~dp0..\src\Microsoft.Net.Project\bin\Debug\%FRAMEWORK%
) ELSE IF "%~1" == "Microsoft.Net.DesignTimeHost" (
    SET KLR_LIB_PATH=%KLR_LIB_PATH%;%~dp0..\src\Microsoft.Net.DesignTimeHost\bin\Debug\%FRAMEWORK%;%~dp0..\src\Microsoft.Net.Runtime.Roslyn\bin\Debug\%FRAMEWORK%
)
REM </dev>

"%KLR_EXE_PATH%" --appbase "%K_APPBASE%" %K_OPTIONS% --lib "%KLR_LIB_PATH%" %*

exit /b %ERRORLEVEL%
ENDLOCAL
