# 0. Close Old Running Instances
Write-Host "Closing any existing osrsmr, RuneLite, and Java instances..."
Get-Process | Where-Object { $_.ProcessName -match 'osrsmr|runelite|RuneLiteWrapper|javaw|java' } | ForEach-Object {
    try {
        if ($_.MainWindowTitle -match 'Rider' -or $_.Path -match 'JetBrains') { return }
        Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue
    } catch { }
}
Start-Sleep -Milliseconds 300

# 1. Build Agent
$localAppData = $env:LOCALAPPDATA
$javac = "C:\Program Files\JetBrains\JetBrains Rider 2026.1.2\jbr\bin\javac.exe"
$jar = "C:\Program Files\JetBrains\JetBrains Rider 2026.1.2\jbr\bin\jar.exe"

# Search for any jar.exe on the system since JBR and RuneLite don't have it
if (-not (Test-Path $jar)) {
    $foundJar = Get-ChildItem -Path "C:\Program Files\Java", "C:\Program Files (x86)\Java", "$localAppData\RuneLite" -Recurse -Filter jar.exe -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($foundJar) {
        $jar = $foundJar.FullName
    }
}

if (-not (Test-Path $jar)) {
    $jar = "jar"
}

Write-Host "Building Java Agent..."
$rootDir = Get-Location

# Build Agent
Write-Host "Compiling Java sources for Java 11..."
if (Test-Path "agent/out") { Remove-Item "agent/out" -Recurse -Force }
New-Item -ItemType Directory -Path "agent/out" -Force

& $javac --release 11 -d "$rootDir/agent/out" "$rootDir/agent/src/main/java/com/osrsmr/agent/BytecodeAgent.java"
& $javac --release 11 -d "$rootDir/agent/out" "$rootDir/agent/src/main/java/com/osrsmr/attach/AttachHelper.java"
Copy-Item "$rootDir/agent/src/main/resources/META-INF" "$rootDir/agent/out/META-INF" -Recurse -Force

# 2. Package JAR
Write-Host "Packaging agent.jar..."
$outDir = "$rootDir/agent/out"
$targetJar = "$rootDir/agent/agent.jar"
if (Test-Path $targetJar) { Remove-Item $targetJar -Force }

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

$zipArchiveMode = [System.IO.Compression.ZipArchiveMode]::Create
$zip = [System.IO.Compression.ZipFile]::Open($targetJar, $zipArchiveMode)
Get-ChildItem -Path $outDir -Recurse | Where-Object { -not $_.PSIsContainer } | ForEach-Object {
    $relPath = $_.FullName.Substring($outDir.Length + 1).Replace('\', '/')
    $entry = $zip.CreateEntry($relPath)
    $stream = $entry.Open()
    $bytes = [System.IO.File]::ReadAllBytes($_.FullName)
    $stream.Write($bytes, 0, $bytes.Length)
    $stream.Close()
}
$zip.Dispose()

cd $rootDir
try { Copy-Item "agent/agent.jar" "agent.jar" -Force -ErrorAction Stop } catch { Write-Warning "Could not overwrite root agent.jar: $($_.Exception.Message)" }
try { Copy-Item "agent/agent.jar" "bin/Release/net9.0-windows/agent.jar" -Force -ErrorAction Stop } catch { Write-Warning "Could not overwrite bin/Release agent.jar: $($_.Exception.Message)" }
try { Copy-Item "agent/agent.jar" "bin/Debug/net9.0-windows/agent.jar" -Force -ErrorAction Stop } catch { Write-Warning "Could not overwrite bin/Debug agent.jar: $($_.Exception.Message)" }
try { if (Test-Path "$localAppData\RuneLite") { Copy-Item "agent/agent.jar" "$localAppData\RuneLite\agent.jar" -Force -ErrorAction SilentlyContinue } } catch { }
try { if (Test-Path "$env:USERPROFILE\.runelite") { Copy-Item "agent/agent.jar" "$env:USERPROFILE\.runelite\agent.jar" -Force -ErrorAction SilentlyContinue } } catch { }

# 3. Build Wrapper
Write-Host "Building RuneLite Jagex Proxy Wrapper..."
$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (Test-Path "$PSScriptRoot\RuneLiteWrapper.cs") {
    & $csc /target:winexe /out:"$PSScriptRoot\RuneLiteWrapper.exe" /optimize+ "$PSScriptRoot\RuneLiteWrapper.cs"
    Copy-Item "$PSScriptRoot\RuneLiteWrapper.exe" "$PSScriptRoot\bin\Release\net9.0-windows\RuneLiteWrapper.exe" -Force
    Copy-Item "$PSScriptRoot\RuneLiteWrapper.exe" "$PSScriptRoot\bin\Debug\net9.0-windows\RuneLiteWrapper.exe" -Force
}

# 4. Build Bridge
Write-Host "Building C# Bridge..."
dotnet build osrsmr.csproj -c Release

# 5. Update Shortcut
./create_shortcut.ps1
