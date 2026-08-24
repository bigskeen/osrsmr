# This script builds the Bridge and Hook, then launches them.

# 1. Build
./rebuild.ps1

# 2. Add Firewall Rule (Optional, only if needed for local port 43594)
# New-NetFirewallRule -DisplayName "OSRS Bridge" -Direction Inbound -Action Allow -Protocol TCP -LocalPort 43594

# 3. Launch Bridge (Background)
Start-Process "bin/Release/net9.0-windows/osrsmr.exe"

# 4. Instructions
Write-Host ""
Write-Host "--- RESTART COMPLETE ---" -ForegroundColor Green
Write-Host "1. The Bridge is now running."
Write-Host "2. To hook RuneLite, add this to your Java arguments:" -ForegroundColor Cyan
Write-Host "   -javaagent:`"C:\Users\bigsk\RiderProjects\osrsmr\agent.jar`"" -ForegroundColor Yellow
Write-Host "3. Once OSRS loads, the Bridge will show 'Agent Connected!' and begin discovery."
Write-Host "------------------------"
