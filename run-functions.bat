@echo off
REM ============================================================
REM  run-functions.bat — quick launcher for local development
REM
REM  Builds the project and starts the Functions host on
REM  http://localhost:7071. Press Ctrl+C in this window to stop.
REM
REM  PREREQUISITE: Azurite (local storage emulator) must already
REM  be running — see README.md, section "Running locally".
REM ============================================================

REM Run from the folder this script lives in (no hardcoded paths)
cd /d "%~dp0"

echo Stopping any stale Functions host...
taskkill /f /im func.exe >nul 2>&1

echo Building project...
dotnet build
if errorlevel 1 (
    echo.
    echo BUILD FAILED - fix the errors above and try again.
    pause
    exit /b 1
)

echo.
echo Starting Azure Functions host on http://localhost:7071 ...
echo (Press Ctrl+C to stop)
echo.
func start

pause