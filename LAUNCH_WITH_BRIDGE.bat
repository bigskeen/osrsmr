@echo off
setlocal

:: Path to the bridge's agent
set AGENT_PATH=%~dp0agent.jar
:: Path to RuneLite
set RUNELITE_EXE=%LOCALAPPDATA%\RuneLite\RuneLite.exe

echo ========================================
echo OSRS Bridge: One-Click Launcher
echo ========================================

:: 1. Clean the config to ensure no blocking flags
echo [1/3] Preparing configuration...
powershell -Command "$p = join-path $env:LOCALAPPDATA 'RuneLite\config.json'; if (test-path $p) { $c = get-content $p | convertfrom-json; $v = $c.vmArgs | where { $_ -notmatch 'javaagent|DisableAttach|nojvm|UsePerfData' }; $c.vmArgs = $v; $c | convertto-json | set-content $p }"

:: 2. Launch RuneLite with the agent injected directly
echo [2/3] Launching RuneLite with Hook...
start "" "%RUNELITE_EXE%" -J-XX:-DisableAttachMechanism -J-javaagent:"%AGENT_PATH%"

:: 3. Launch the Bridge
echo [3/3] Opening Bridge UI...
start "" "%~dp0bin\Release\net9.0-windows\osrsmr.exe"

echo.
echo SUCCESS! RuneLite is opening with the bridge connected.
echo You can close this window now.
echo.
pause