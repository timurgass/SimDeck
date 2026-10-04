@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -STA -File "%~dp0Install-ETS2-Telemetry.ps1" -Game ats %*
if errorlevel 1 (
    echo Installation failed. Check the message above.
    pause
    exit /b 1
)
echo Installation complete.
pause
