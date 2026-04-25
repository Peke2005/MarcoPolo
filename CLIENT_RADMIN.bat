@echo off
setlocal
set /p HOST_IP=IP Radmin del host: 
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Tools\RunLanClient.ps1" -Address "%HOST_IP%" -Port 7777
pause
