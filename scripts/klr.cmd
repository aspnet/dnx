@ECHO OFF
SETLOCAL
SET ERRORLEVEL=

REM <dev>
@Echo ON
REM </dev>

IF "%K_APPBASE%"=="" (
  SET "K_APPBASE=%CD%"
)

SET "KLR_EXE_PATH=%~dp0klr.exe"
SET "KLR_LIB_PATH=%~dp0..\tools"

REM <dev>
SET FRAMEWORK=net45
SET "KLR_EXE_PATH=%~dp0..\bin\Win32\Debug\klr.exe"
SET "KLR_LIB_PATH=%~dp0..\src\klr.host\bin\%FRAMEWORK%"

:START_Newtonsoft_Json
IF EXIST "%~dp0..\packages\Newtonsoft.Json*" FOR /F %%I IN ('DIR %~dp0..\packages\Newtonsoft.Json* /B /O:-D') DO (SET Newtonsoft_Json=%%I& GOTO :END_Newtonsoft_Json)
:END_Newtonsoft_Json

SET "KLR_LIB_PATH=%KLR_LIB_PATH%;%~dp0..\packages\%Newtonsoft_Json%\lib\%FRAMEWORK%"

REM This is insane (I'm sure there's a better way to build up the KLR_LIB_PATH)

:START_Microsoft_Bcl_Immutable
IF EXIST "%~dp0..\packages\Microsoft.Bcl.Immutable*" FOR /F %%I IN ('DIR %~dp0..\packages\Microsoft.Bcl.Immutable* /B /O:-D') DO (SET Microsoft_Bcl_Immutable=%%I& GOTO :END_Microsoft_Bcl_Immutable)
:END_Microsoft_Bcl_Immutable

SET "KLR_LIB_PATH=%KLR_LIB_PATH%;%~dp0..\packages\%Microsoft_Bcl_Immutable%\lib\portable-net45+win8"

:START_Microsoft_CodeAnalysis_CSharp
IF EXIST "%~dp0..\packages\Microsoft.CodeAnalysis.CSharp*" FOR /F %%I IN ('DIR %~dp0..\packages\Microsoft.CodeAnalysis.CSharp* /B /O:-D') DO (SET Microsoft_CodeAnalysis_CSharp=%%I& GOTO :END_Microsoft_CodeAnalysis_CSharp)
:END_Microsoft_CodeAnalysis_CSharp

SET "KLR_LIB_PATH=%KLR_LIB_PATH%;%~dp0..\packages\%Microsoft_CodeAnalysis_CSharp%\lib\%FRAMEWORK%"

:START_Microsoft_CodeAnalysis_Common
IF EXIST "%~dp0..\packages\Microsoft.CodeAnalysis.Common*" FOR /F %%I IN ('DIR %~dp0..\packages\Microsoft.CodeAnalysis.Common* /B /O:-D') DO (SET Microsoft_CodeAnalysis_Common=%%I& GOTO :END_Microsoft_CodeAnalysis_Common)
:END_Microsoft_CodeAnalysis_Common

SET "KLR_LIB_PATH=%KLR_LIB_PATH%;%~dp0..\packages\%Microsoft_CodeAnalysis_Common%\lib\%FRAMEWORK%"

:START_Microsoft_Bcl_Metadata
IF EXIST "%~dp0..\packages\Microsoft.Bcl.Metadata*" FOR /F %%I IN ('DIR %~dp0..\packages\Microsoft.Bcl.Metadata* /B /O:-D') DO (SET Microsoft_Bcl_Metadata=%%I& GOTO :END_Microsoft_Bcl_Metadata)
:END_Microsoft_Bcl_Metadata

SET "KLR_LIB_PATH=%KLR_LIB_PATH%;%~dp0..\packages\%Microsoft_Bcl_Metadata%\lib\portable-net45+win8"

echo "%KLR_LIB_PATH%"

IF "%~1" == "Microsoft.Framework.ApplicationHost" (
    SET "KLR_LIB_PATH=%KLR_LIB_PATH%;%~dp0..\src\Microsoft.Framework.Runtime\bin\%FRAMEWORK%;%~dp0..\src\Microsoft.Framework.ApplicationHost\bin\%FRAMEWORK%;%~dp0..\src\Microsoft.Framework.Runtime.Roslyn\bin\%FRAMEWORK%"
) ELSE IF "%~3" == "Microsoft.Framework.Project" (
    SET "KLR_LIB_PATH=%KLR_LIB_PATH%;%~dp0..\src\Microsoft.Framework.Runtime\bin\%FRAMEWORK%;%~dp0..\src\Microsoft.Framework.Runtime.Roslyn\bin\%FRAMEWORK%;%~dp0..\src\Microsoft.Framework.Project\bin\%FRAMEWORK%"
) ELSE IF "%~3" == "Microsoft.Framework.PackageManager" (
    SET "KLR_LIB_PATH=%KLR_LIB_PATH%;%~dp0..\src\Microsoft.Framework.Runtime\bin\%FRAMEWORK%;%~dp0..\src\Microsoft.Framework.Runtime.Roslyn\bin\%FRAMEWORK%;%~dp0..\src\Microsoft.Framework.PackageManager\bin\%FRAMEWORK%"
)
REM </dev>

"%KLR_EXE_PATH%" --appbase "%K_APPBASE%" %K_OPTIONS% --lib "%KLR_LIB_PATH%" %*

exit /b %ERRORLEVEL%
ENDLOCAL
