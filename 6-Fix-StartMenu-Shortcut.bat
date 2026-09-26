@echo off
cd /d "%~dp0"
echo Fixes Start Menu shortcut after a Tarkov/BSG update.

net session >nul 2>&1
if %errorlevel% NEQ 0 (
  echo Requesting Administrator to remove the update-created ProgramData shortcut...
  powershell -NoProfile -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
  exit /b
)

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Fix-TarkovShortcut.ps1"
echo.
pause
