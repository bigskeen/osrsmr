$WshShell = New-Object -ComObject WScript.Shell
$DesktopPath = [System.Environment]::GetFolderPath("Desktop")

# 1. OSRS Bridge (Script Launcher)
$Shortcut = $WshShell.CreateShortcut("$DesktopPath\OSRS Bridge.lnk")
$Shortcut.TargetPath = "powershell.exe"
$Shortcut.Arguments = "-ExecutionPolicy Bypass -File `"$PSScriptRoot\launch.ps1`""
$Shortcut.WorkingDirectory = "$PSScriptRoot"
$Shortcut.WindowStyle = 1
$Shortcut.Description = "Launch OSRS Bridge with latest updates"
$Shortcut.Save()

# 2. OSRS Memory Reader (Direct Executable)
$ExeShortcut = $WshShell.CreateShortcut("$DesktopPath\OSRS Memory Reader (osrsmr).lnk")
$ExeShortcut.TargetPath = "$PSScriptRoot\bin\Release\net9.0-windows\osrsmr.exe"
$ExeShortcut.WorkingDirectory = "$PSScriptRoot\bin\Release\net9.0-windows"
$ExeShortcut.Description = "OSRS Memory Reader (osrsmr)"
$ExeShortcut.Save()

# 3. RuneLite Desktop Shortcut with Bridge Hook
$RuneLiteExe = "$env:LOCALAPPDATA\RuneLite\RuneLite.exe"
if (Test-Path $RuneLiteExe) {
    $AgentJar = "$PSScriptRoot\agent.jar"
    if (-not (Test-Path $AgentJar)) {
        $AgentJar = "$PSScriptRoot\bin\Release\net9.0-windows\agent.jar"
    }
    $RlShortcut = $WshShell.CreateShortcut("$DesktopPath\RuneLite.lnk")
    $RlShortcut.TargetPath = $RuneLiteExe
    $RlShortcut.Arguments = "-J-XX:-DisableAttachMechanism -J-javaagent:`"$AgentJar`""
    $RlShortcut.WorkingDirectory = "$env:LOCALAPPDATA\RuneLite"
    $RlShortcut.Description = "RuneLite (Bridge Hooked)"
    $RlShortcut.Save()
    Write-Host "Updated Desktop shortcut 'RuneLite' with hook parameters!" -ForegroundColor Green
}

Write-Host "Desktop shortcuts created and synchronized!" -ForegroundColor Green
