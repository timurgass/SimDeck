@echo off
setlocal
if not exist "%~dp0Install-ETS2-Preset.ps1" (
  echo Install-ETS2-Preset.ps1 is missing. Extract the whole archive first.
  pause
  exit /b 1
)
if not exist "%~dp0preset-actions.json" (
  echo preset-actions.json is missing. Extract the whole archive first.
  pause
  exit /b 1
)
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Install-ETS2-Preset.ps1" %*
set "result=%errorlevel%"
if not "%result%"=="0" echo Installation failed with code %result%.
pause
exit /b %result%
