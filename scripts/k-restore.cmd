@Echo OFF
SET ERRORLEVEL=

REM <dev>
@Echo ON
REM </dev>

"%~dp0\NuGet.exe" restore %*

exit /b %ERRORLEVEL%