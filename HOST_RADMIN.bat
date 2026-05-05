@echo off
setlocal
echo Iniciando backend auth y build normal con login...
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Tools\StartAuthBackendRadmin.ps1" -Port 3001
start "" "%~dp0Builds\Release\FrentePartido\FrentePartido.exe"
pause
