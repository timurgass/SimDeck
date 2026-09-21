@echo off
setlocal
if not exist "%~dp0Install-SimDeckPreset.ps1" goto :missing
if not exist "%~dp0preset-actions.json" goto :missing
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Install-SimDeckPreset.ps1" -Game "F1 25"
set "exitCode=%errorlevel%"
echo.
pause
exit /b %exitCode%

:missing
echo ERROR: installer components are missing from this folder.
echo.
echo Do not run this CMD inside the ZIP or copy it separately.
echo Extract the entire SimDeck-F1-Preset-0.7.1.zip archive,
echo then run this CMD from the extracted folder.
echo.
pause
exit /b 2
