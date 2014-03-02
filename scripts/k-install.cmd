@Echo OFF
SET ERRORLEVEL=

REM <dev>
@Echo ON
REM </dev>

"%~dp0\NuGet.exe" install %*

exit /b %ERRORLEVEL%
