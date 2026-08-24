@echo off
echo ========================================
echo OSRS Bridge Repair ^& Launch Tool
echo ========================================

echo 1. Closing any stuck processes...
taskkill /F /IM osrsmr.exe /T 2>nul
taskkill /F /IM java.exe /T 2>nul
taskkill /F /IM javaw.exe /T 2>nul

echo 2. Cleaning temporary files...
set BRIDGE_DIR=%~dp0bin\Release\net9.0-windows
if not exist "%BRIDGE_DIR%" set BRIDGE_DIR=%~dp0

echo 3. Checking for required files...
if not exist "%BRIDGE_DIR%\osrsmr.exe" (
    echo ERROR: osrsmr.exe not found in %BRIDGE_DIR%
    pause
    exit /b
)

echo 4. Launching Bridge as Administrator...
powershell -Command "Start-Process '%BRIDGE_DIR%\osrsmr.exe' -Verb RunAs"

echo Done! The bridge should open in a few seconds.
pause