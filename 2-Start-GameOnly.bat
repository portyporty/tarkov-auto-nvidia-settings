@echo off
cd /d "%~dp0"
echo TarkovNvColor - game-only (does NOT open launcher)
echo Open Tarkov yourself, then colors apply while the game runs.
TarkovNvColor.exe --session --game-only
pause
