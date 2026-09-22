@echo off
setlocal
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0Install-SimDeckAccPreset.ps1"
if errorlevel 1 (
  echo.
  echo Installation failed. Read the message above.
)
echo.
pause
