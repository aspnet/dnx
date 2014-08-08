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
SET "KLR_EXE_PATH=%~dp0..\bin\Win32\Debug\klr.exe"
SET "KLR_LIB_PATH=%~dp0..\src\klr.host\bin\debug\net45"
SET "PACKAGE_ROOT=%KRE_PACKAGES%"

IF "%PACKAGE_ROOT%"=="" (
  SET "PACKAGE_ROOT=%USERPROFILE%\.kpm\packages"
)

SET "PACKAGE_LIBS=%PACKAGE_ROOT%\Newtonsoft.Json\5.0.8\lib\net45"
SET "PACKAGE_LIBS=%PACKAGE_LIBS%;%PACKAGE_ROOT%\Microsoft.Bcl.Immutable\1.1.20-beta\lib\portable-net45+win8"
SET "PACKAGE_LIBS=%PACKAGE_LIBS%;%PACKAGE_ROOT%\Microsoft.Bcl.Metadata\1.0.12-alpha\lib\portable-net45+win8"

:START_Microsoft_CodeAnalysis_CSharp
FOR /F %%I IN ('DIR %PACKAGE_ROOT%\Microsoft.CodeAnalysis.CSharp\* /B /O:-D') DO (SET Microsoft_CodeAnalysis_CSharp=%%I& GOTO :END_Microsoft_CodeAnalysis_CSharp)
:END_Microsoft_CodeAnalysis_CSharp

SET "PACKAGE_LIBS=%PACKAGE_LIBS%;%PACKAGE_ROOT%\Microsoft.CodeAnalysis.CSharp\%Microsoft_CodeAnalysis_CSharp%\lib\net45"

:START_Microsoft_CodeAnalysis_Common
FOR /F %%I IN ('DIR %PACKAGE_ROOT%\Microsoft.CodeAnalysis.Common\* /B /O:-D') DO (SET Microsoft_CodeAnalysis_Common=%%I& GOTO :END_Microsoft_CodeAnalysis_Common)
:END_Microsoft_CodeAnalysis_Common

SET "PACKAGE_LIBS=%PACKAGE_LIBS%;%PACKAGE_ROOT%\Microsoft.CodeAnalysis.Common\%Microsoft_CodeAnalysis_Common%\lib\net45"

SET "KLR_LIB_PATH=%KLR_LIB_PATH%;%PACKAGE_LIBS%"

IF "%~1" == "Microsoft.Framework.ApplicationHost" (
    SET "KLR_LIB_PATH=%KLR_LIB_PATH%;%~dp0..\src\Microsoft.Framework.Runtime\bin\debug\net45;%~dp0..\src\Microsoft.Framework.ApplicationHost\bin\debug\net45;%~dp0..\src\Microsoft.Framework.Runtime.Roslyn\bin\debug\net45"
) ELSE IF "%~3" == "Microsoft.Framework.Project" (
    SET "KLR_LIB_PATH=%KLR_LIB_PATH%;%~dp0..\src\Microsoft.Framework.Runtime\bin\debug\net45;%~dp0..\src\Microsoft.Framework.Runtime.Roslyn\bin\debug\net45;%~dp0..\src\Microsoft.Framework.Project\bin\debug\net45"
) ELSE IF "%~3" == "Microsoft.Framework.PackageManager" (
    SET "KLR_LIB_PATH=%KLR_LIB_PATH%;%~dp0..\src\Microsoft.Framework.Runtime\bin\debug\net45;%~dp0..\src\Microsoft.Framework.Runtime.Roslyn\bin\debug\net45;%~dp0..\src\Microsoft.Framework.PackageManager\bin\debug\net45"
)

echo %KLR_LIB_PATH%

REM </dev>

"%KLR_EXE_PATH%" --appbase "%K_APPBASE%" %K_OPTIONS% --lib "%KLR_LIB_PATH%" %*

exit /b %ERRORLEVEL%
ENDLOCAL