@echo off
chcp 65001 >nul
if not exist "%~dp0Install-SimDeckPreset.ps1" goto :missing
if not exist "%~dp0preset-actions.json" goto :missing
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Install-SimDeckPreset.ps1" -Game "F1 24"
echo.
pause
exit /b %errorlevel%

:missing
echo ОШИБКА: рядом с этим файлом нет компонентов установщика.
echo.
echo Не запускайте CMD прямо из ZIP и не скачивайте его отдельно.
echo Полностью распакуйте SimDeck-F1-Preset-0.7.1.zip в одну папку,
echo затем запустите Install-F1-24.cmd из распакованной папки.
echo.
pause
exit /b 2
