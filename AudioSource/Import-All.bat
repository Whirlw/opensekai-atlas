@echo off
setlocal
cd /d "%~dp0.."
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "Tools\import-audio-pack.ps1" -Apply
if errorlevel 1 echo Import failed. Review the errors above.
echo.
pause
