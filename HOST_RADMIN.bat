@echo off
setlocal
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Tools\RunLanHost.ps1" -Port 7777
pause
