@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Enable-ACC-Broadcast.ps1"
if errorlevel 1 echo ACC setup failed. Read the message above.
pause
