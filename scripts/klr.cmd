REM This is used for dev only

if "%TARGET_FRAMEWORK%" == "" (
    CALL "%~dp0klr-svr" %*
)

if "%TARGET_FRAMEWORK%" == "net45" (
    CALL "%~dp0klr-svr" %*
)

if "%TARGET_FRAMEWORK%" == "k10" (
    CALL "%~dp0klr-svrc" %*
)