@echo off
setlocal
cd /d "%~dp0.."
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "Tools\import-all-art.ps1"
if errorlevel 1 echo Import failed. Review the error above.
echo.
pause
