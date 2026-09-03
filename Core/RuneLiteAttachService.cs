using System.Diagnostics;
using System.IO;
using System.Windows.Media;

namespace OsrsMr.Core;

public class RuneLiteAttachService
{
    private static readonly RuneLiteAttachService _instance = new();
    public static RuneLiteAttachService Instance => _instance;

    private readonly object _logLock = new();
    private readonly object _processTrackLock = new();
    private Process? _trackedRuneLiteProcess;
    private string? _cachedJavaPath;
    private readonly Dictionary<int, DateTime> _failedPidCooldown = new();
    private bool _configChecked = false;

    public void LogMessage(string message)
    {
        try
        {
            lock (_logLock)
            {
                File.AppendAllText("attach_log.txt", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}\n");
            }
        }
        catch { }
    }

    public void SyncAgentJar()
    {
        try
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string sourceAgent = Path.Combine(baseDir, "agent.jar");
            if (!File.Exists(sourceAgent))
                sourceAgent = Path.Combine(Environment.CurrentDirectory, "agent.jar");

            if (!File.Exists(sourceAgent)) return;

            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            string[] targetDirs = {
                Path.Combine(localAppData, "RuneLite"),
                Path.Combine(userProfile, ".runelite"),
                Path.Combine(localAppData, "Jagex Launcher", "games", "RuneLite"),
                Path.Combine(userProfile, ".jagexlauncher", "games", "runelite"),
                Path.Combine(Environment.CurrentDirectory, "bin", "Release", "net9.0-windows"),
                Path.Combine(Environment.CurrentDirectory, "bin", "Debug", "net9.0-windows")
            };

            foreach (var dir in targetDirs)
            {
                try
                {
                    if (Directory.Exists(dir))
                    {
                        string dest = Path.Combine(dir, "agent.jar");
                        if (File.Exists(dest))
                        {
                            var fi = new FileInfo(dest);
                            if (fi.IsReadOnly) fi.IsReadOnly = false;
                        }
                        File.Copy(sourceAgent, dest, overwrite: true);
                    }
                }
                catch { }
            }
        }
        catch (Exception ex)
        {
            LogMessage($"[AGENT_SYNC_ERROR] {ex.Message}");
        }
    }

    public List<(int Id, string Name, string Title)> FindRuneLiteCandidateProcesses()
    {
        var list = new List<(int Id, string Name, string Title, int Priority)>();
        int currentPid = Environment.ProcessId;
        try
        {
            var processes = Process.GetProcesses();
            foreach (var p in processes)
            {
                try
                {
                    if (p.Id == currentPid) continue;

                    string name = p.ProcessName.ToLowerInvariant();
                    if (name.Contains("jagexlauncher") ||
                        name.Contains("osrsmr") ||
                        name.Contains("rider") ||
                        name.Contains("idea") ||
                        name.Contains("devenv") ||
                        name.Contains("code") ||
                        name.Contains("chrome") ||
                        name.Contains("firefox") ||
                        name.Contains("msedge") ||
                        name.Contains("explorer"))
                    {
                        continue;
                    }

                    string title = p.MainWindowTitle ?? "";
                    string titleLower = title.ToLowerInvariant();

                    bool isJvm = name.Equals("java", StringComparison.OrdinalIgnoreCase) ||
                                 name.Equals("javaw", StringComparison.OrdinalIgnoreCase);
                    bool isRuneLiteExe = name.Contains("runelite") || titleLower.Contains("runelite");

                    if (isJvm)
                    {
                        int priority = 50;
                        if (titleLower.Contains("runelite") || titleLower.Contains("old school") || titleLower.Contains("osrs"))
                        {
                            priority = 100;
                        }
                        else
                        {
                            try
                            {
                                string? modulePath = p.MainModule?.FileName?.ToLowerInvariant();
                                if (!string.IsNullOrEmpty(modulePath) && (modulePath.Contains("runelite") || modulePath.Contains(".runelite")))
                                {
                                    priority = 90;
                                }
                            }
                            catch { }
                        }
                        list.Add((p.Id, p.ProcessName, title, priority));
                    }
                    else if (isRuneLiteExe)
                    {
                        int priority = 10;
                        list.Add((p.Id, p.ProcessName, title, priority));
                    }
                }
                catch { }
            }
        }
        catch (Exception ex)
        {
            LogMessage($"[CANDIDATE_SCAN_ERROR] {ex.Message}");
        }

        return list
            .OrderByDescending(c => c.Priority)
            .Select(c => (c.Id, c.Name, c.Title))
            .ToList();
    }

    public void FixRuneLiteShortcut(string? agentPath = null, string? runeLiteExe = null)
    {
        try
        {
            if (string.IsNullOrEmpty(agentPath))
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                agentPath = Path.Combine(baseDir, "agent.jar");
                if (!File.Exists(agentPath))
                    agentPath = Path.Combine(Environment.CurrentDirectory, "agent.jar");
                agentPath = Path.GetFullPath(agentPath);
            }

            if (string.IsNullOrEmpty(runeLiteExe))
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                runeLiteExe = Path.Combine(appData, @"RuneLite\RuneLite.exe");
                if (!File.Exists(runeLiteExe))
                {
                    string progFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                    runeLiteExe = Path.Combine(progFiles, @"RuneLite\RuneLite.exe");
                }
            }

            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            string shortcutPath = Path.Combine(desktop, "RuneLite.lnk");

            if (File.Exists(runeLiteExe))
            {
                Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType != null)
                {
                    dynamic shell = Activator.CreateInstance(shellType)!;
                    dynamic shortcut = shell.CreateShortcut(shortcutPath);
                    shortcut.TargetPath = runeLiteExe;
                    shortcut.Arguments = $"-J-XX:-DisableAttachMechanism -J-javaagent:\"{agentPath}\"";
                    shortcut.WorkingDirectory = Path.GetDirectoryName(runeLiteExe);
                    shortcut.Description = "RuneLite (Bridge Hooked)";
                    shortcut.Save();
                    LogMessage("[SHORTCUT] Updated Desktop RuneLite.lnk with JVM hook parameters.");
                }
            }
        }
        catch (Exception ex)
        {
            LogMessage($"[SHORTCUT_ERROR] {ex.Message}");
        }
    }

    public static void EnsureWrapperCompiled(string wrapperExePath)
    {
        try
        {
            if (File.Exists(wrapperExePath)) return;
            string csc = @"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe";
            if (!File.Exists(csc))
                csc = @"C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe";
            if (!File.Exists(csc)) return;

            string srcPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "RuneLiteWrapper.cs");
            if (!File.Exists(srcPath))
                srcPath = Path.Combine(Environment.CurrentDirectory, "RuneLiteWrapper.cs");

            if (File.Exists(srcPath))
            {
                var psi = new ProcessStartInfo(csc, $"/target:winexe /out:\"{wrapperExePath}\" /optimize+ \"{srcPath}\"")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                using var p = Process.Start(psi);
                p?.WaitForExit();
            }
        }
        catch { }
    }

    public void InstallJagexLauncherHook()
    {
        try
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            string[] runeLiteDirs = {
                Path.Combine(localAppData, "RuneLite"),
                Path.Combine(localAppData, "Jagex Launcher", "games", "RuneLite"),
                Path.Combine(userProfile, ".jagexlauncher", "games", "runelite"),
                Path.Combine(programFiles, "RuneLite")
            };

            string agentSource = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "agent.jar");
            if (!File.Exists(agentSource))
                agentSource = Path.Combine(Environment.CurrentDirectory, "agent.jar");

            string wrapperSource = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "RuneLiteWrapper.exe");
            if (!File.Exists(wrapperSource))
                wrapperSource = Path.Combine(Environment.CurrentDirectory, "RuneLiteWrapper.exe");

            EnsureWrapperCompiled(wrapperSource);

            foreach (var rDir in runeLiteDirs)
            {
                if (!Directory.Exists(rDir)) continue;

                if (File.Exists(agentSource))
                {
                    string targetAgent = Path.Combine(rDir, "agent.jar");
                    try { File.Copy(agentSource, targetAgent, overwrite: true); } catch { }
                }

                string mainExe = Path.Combine(rDir, "RuneLite.exe");
                string origExe = Path.Combine(rDir, "RuneLite_real.exe");

                if (File.Exists(mainExe) && File.Exists(wrapperSource))
                {
                    try
                    {
                        var fiMain = new FileInfo(mainExe);
                        if (!File.Exists(origExe) && fiMain.Length > 200000)
                        {
                            File.Copy(mainExe, origExe, overwrite: true);
                            LogMessage($"[WRAPPER_BACKUP] Backed up original RuneLite.exe ({fiMain.Length} bytes) -> RuneLite_real.exe");
                        }

                        if (File.Exists(origExe))
                        {
                            var fiWrapper = new FileInfo(wrapperSource);
                            if (fiMain.Length != fiWrapper.Length)
                            {
                                File.Copy(wrapperSource, mainExe, overwrite: true);
                                LogMessage($"[WRAPPER_INSTALL] Replaced RuneLite.exe in {rDir} with RuneLiteWrapper.exe");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LogMessage($"[WRAPPER_INSTALL_WARN] Could not replace RuneLite.exe in {rDir}: {ex.Message}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            LogMessage($"[WRAPPER_SETUP_ERROR] {ex.Message}");
        }
    }

    public void CheckAndFixRuneLiteConfig(bool silent = false)
    {
        if (_configChecked && silent) return;
        _configChecked = true;

        try
        {
            SyncAgentJar();
            InstallJagexLauncherHook();

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string agentJarPath = Path.Combine(baseDir, "agent.jar");
            if (!File.Exists(agentJarPath))
                agentJarPath = Path.Combine(Environment.CurrentDirectory, "agent.jar");
            agentJarPath = Path.GetFullPath(agentJarPath);

            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);

            string[] configPaths = {
                Path.Combine(localAppData, @"RuneLite\RuneLite.cfg"),
                Path.Combine(userProfile, @".runelite\RuneLite.cfg"),
                Path.Combine(localAppData, @"Jagex Launcher\games\RuneLite\RuneLite.cfg"),
                Path.Combine(userProfile, @".jagexlauncher\games\runelite\RuneLite.cfg"),
                Path.Combine(programFiles, @"RuneLite\RuneLite.cfg")
            };

            foreach (var cfg in configPaths)
            {
                if (!File.Exists(cfg)) continue;

                try
                {
                    string content = File.ReadAllText(cfg);
                    bool modified = false;

                    string agentArg = $"-javaagent:{agentJarPath}";
                    string attachArg = "-XX:-DisableAttachMechanism";

                    if (!content.Contains(attachArg))
                    {
                        if (content.Contains("[JVM]"))
                            content = content.Replace("[JVM]", $"[JVM]\n{attachArg}");
                        else
                            content += $"\n[JVM]\n{attachArg}\n";
                        modified = true;
                    }

                    if (!content.Contains("agent.jar"))
                    {
                        if (content.Contains("[JVM]"))
                            content = content.Replace("[JVM]", $"[JVM]\n{agentArg}");
                        else
                            content += $"\n{agentArg}\n";
                        modified = true;
                    }

                    if (modified)
                    {
                        var fi = new FileInfo(cfg);
                        if (fi.IsReadOnly) fi.IsReadOnly = false;
                        File.WriteAllText(cfg, content);
                        LogMessage($"[CONFIG_FIX] Updated RuneLite.cfg at: {cfg}");
                    }
                }
                catch (Exception ex)
                {
                    LogMessage($"[CONFIG_FIX_WARN] Could not update {cfg}: {ex.Message}");
                }
            }

            FixRuneLiteShortcut(agentJarPath);
        }
        catch (Exception ex)
        {
            LogMessage($"[CONFIG_CHECK_ERROR] {ex.Message}");
        }
    }

    public string GetCompatibleJavaPath()
    {
        if (!string.IsNullOrEmpty(_cachedJavaPath) && File.Exists(_cachedJavaPath))
        {
            return _cachedJavaPath;
        }

        var candidatePaths = new List<string>();
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        string programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        try
        {
            if (Directory.Exists(@"C:\Program Files\JetBrains"))
            {
                foreach (var dir in Directory.GetDirectories(@"C:\Program Files\JetBrains"))
                {
                    string jbrJava = Path.Combine(dir, "jbr", "bin", "java.exe");
                    if (File.Exists(jbrJava)) candidatePaths.Add(jbrJava);
                }
            }
        }
        catch { }

        try
        {
            string riderJbr = Path.Combine(localAppData, @"Programs\Rider\jbr\bin\java.exe");
            if (File.Exists(riderJbr)) candidatePaths.Add(riderJbr);
        }
        catch { }

        string? javaHome = Environment.GetEnvironmentVariable("JAVA_HOME");
        if (!string.IsNullOrEmpty(javaHome))
        {
            string j = Path.Combine(javaHome, "bin", "java.exe");
            if (File.Exists(j)) candidatePaths.Add(j);
        }
        string? jdkHome = Environment.GetEnvironmentVariable("JDK_HOME");
        if (!string.IsNullOrEmpty(jdkHome))
        {
            string j = Path.Combine(jdkHome, "bin", "java.exe");
            if (File.Exists(j)) candidatePaths.Add(j);
        }

        string[] runeLiteJres = {
            Path.Combine(localAppData, @"RuneLite\jre\bin\java.exe"),
            Path.Combine(localAppData, @"Jagex Launcher\games\RuneLite\jre\bin\java.exe"),
            Path.Combine(programFiles, @"RuneLite\jre\bin\java.exe"),
            Path.Combine(programFilesX86, @"RuneLite\jre\bin\java.exe"),
            Path.Combine(userProfile, @".jagexlauncher\games\runelite\jre\bin\java.exe"),
            Path.Combine(userProfile, @".runelite\jre\bin\java.exe")
        };
        foreach (var rj in runeLiteJres)
        {
            if (File.Exists(rj)) candidatePaths.Add(rj);
        }

        string[] jdkRoots = {
            Path.Combine(programFiles, "Java"),
            Path.Combine(programFiles, "Eclipse Adoptium"),
            Path.Combine(programFiles, "Microsoft"),
            Path.Combine(programFiles, "Amazon Corretto"),
            Path.Combine(programFiles, "Zulu")
        };

        foreach (var root in jdkRoots)
        {
            try
            {
                if (Directory.Exists(root))
                {
                    foreach (var dir in Directory.GetDirectories(root))
                    {
                        string j = Path.Combine(dir, "bin", "java.exe");
                        if (File.Exists(j)) candidatePaths.Add(j);
                    }
                }
            }
            catch { }
        }

        foreach (var path in candidatePaths.Distinct())
        {
            if (!File.Exists(path)) continue;

            try
            {
                var checkPsi = new ProcessStartInfo
                {
                    FileName = path,
                    Arguments = "--list-modules",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                using var proc = Process.Start(checkPsi);
                if (proc != null)
                {
                    string outStr = proc.StandardOutput.ReadToEnd();
                    proc.WaitForExit(3000);
                    if (outStr.Contains("jdk.attach"))
                    {
                        _cachedJavaPath = path;
                        LogMessage($"[JAVA_SELECT] Selected compatible JDK with jdk.attach: {path}");
                        return path;
                    }
                }
            }
            catch { }
        }

        return "java";
    }

    public bool TryAttachAgent(string pid, Action<string, Brush>? updateStatus = null)
    {
        SyncAgentJar();
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string agentPath = Path.Combine(baseDir, "agent.jar");

        if (!File.Exists(agentPath))
            agentPath = Path.Combine(Environment.CurrentDirectory, "agent.jar");

        agentPath = Path.GetFullPath(agentPath);

        if (!File.Exists(agentPath))
        {
            LogMessage($"[ATTACH_ERROR] agent.jar not found at {agentPath}");
            updateStatus?.Invoke("Error: agent.jar not found!", Brushes.Red);
            return false;
        }

        string javaExe = GetCompatibleJavaPath();

        try
        {
            LogMessage($"[ATTACH_EXEC] Attaching to PID {pid} using {javaExe} and agent {agentPath}");

            var psi = new ProcessStartInfo
            {
                FileName = javaExe,
                Arguments = $"-Djdk.attach.allowAttachSelf=true --add-modules jdk.attach -cp \"{agentPath}\" com.osrsmr.attach.AttachHelper {pid} \"{agentPath}\"",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = Path.GetDirectoryName(agentPath)
            };
            psi.EnvironmentVariables["JAVA_TOOL_OPTIONS"] = "";
            psi.EnvironmentVariables["_JAVA_OPTIONS"] = "";

            using var process = Process.Start(psi);
            if (process == null)
            {
                LogMessage($"[ATTACH_ERROR] Failed to start Java process for PID {pid}");
                return false;
            }

            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();
            process.WaitForExit(6000);

            LogMessage($"[ATTACH_RESULT] PID {pid} (Exit {process.ExitCode})\nOut: {output.Trim()}\nErr: {error.Trim()}");

            if (process.ExitCode == 0 || output.Contains("[ATTACH_SUCCESS]") || output.Contains("[ATTACH_DONE]"))
            {
                return true;
            }

            if (error.Contains("The VM does not support the attach mechanism") || error.Contains("DisableAttachMechanism"))
            {
                updateStatus?.Invoke("RuneLite blocked attach. Click 'Launch RuneLite' to start hooked.", Brushes.Orange);
                LogMessage("[ATTACH_HINT] Target JVM has DisableAttachMechanism active. Use 'Launch RuneLite' button or updated desktop shortcut.");
            }

            return false;
        }
        catch (Exception ex)
        {
            LogMessage($"[ATTACH_EXCEPTION] PID {pid}: {ex.Message}");
            return false;
        }
    }

    public void TrackRuneLiteProcess(int pid, Action<string, Brush>? onStatus = null)
    {
        try
        {
            lock (_processTrackLock)
            {
                if (_trackedRuneLiteProcess != null && !_trackedRuneLiteProcess.HasExited && _trackedRuneLiteProcess.Id == pid)
                    return;

                try
                {
                    var proc = Process.GetProcessById(pid);
                    _trackedRuneLiteProcess = proc;
                    proc.EnableRaisingEvents = true;
                    proc.Exited += (s, e) =>
                    {
                        LogMessage($"[LIFECYCLE] Monitored game client (PID {pid}) closed.");
                        _trackedRuneLiteProcess = null;
                        onStatus?.Invoke("RuneLite client process closed. Waiting for client...", Brushes.Yellow);
                    };
                    LogMessage($"[LIFECYCLE] Tracking RuneLite client lifecycle on PID {pid}.");
                }
                catch (Exception ex)
                {
                    LogMessage($"[LIFECYCLE_TRACK_ERROR] {ex.Message}");
                }
            }
        }
        catch { }
    }

    public static int KillAllRuneLiteInstances()
    {
        int count = 0;
        int currentPid = Environment.ProcessId;
        try
        {
            var processes = Process.GetProcesses();
            foreach (var p in processes)
            {
                try
                {
                    if (p.Id == currentPid) continue;
                    string name = p.ProcessName.ToLowerInvariant();
                    string title = (p.MainWindowTitle ?? "").ToLowerInvariant();
                    bool match = false;

                    if (name.Contains("runelite") || title.Contains("runelite") || name.Contains("runelitewrapper"))
                    {
                        match = true;
                    }
                    else if (name.Contains("java") || name.Contains("javaw"))
                    {
                        if (title.Contains("runelite") || title.Contains("old school") || title.Contains("osrs"))
                        {
                            match = true;
                        }
                        else
                        {
                            try
                            {
                                string? modulePath = p.MainModule?.FileName?.ToLowerInvariant();
                                if (!string.IsNullOrEmpty(modulePath) && (modulePath.Contains("runelite") || modulePath.Contains(".runelite") || modulePath.Contains("osrsmr")))
                                {
                                    match = true;
                                }
                            }
                            catch { }
                        }
                    }

                    if (match)
                    {
                        try { p.Kill(true); } catch { try { p.Kill(); } catch { } }
                        count++;
                    }
                }
                catch { }
            }
        }
        catch { }

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "taskkill.exe",
                Arguments = "/F /T /IM RuneLite.exe /IM RuneLite_real.exe /IM RuneLite_orig.exe /IM RuneLiteWrapper.exe",
                CreateNoWindow = true,
                UseShellExecute = false
            };
            using var proc = Process.Start(psi);
            proc?.WaitForExit(1500);
        }
        catch { }

        return count;
    }

    public void ClearCooldowns() => _failedPidCooldown.Clear();

    public void FindAndAttachAnyClient(Func<bool> isConnected, Action<string, Brush> updateStatus)
    {
        if (isConnected())
        {
            updateStatus("Already connected to active client.", Brushes.Lime);
            return;
        }

        try
        {
            updateStatus("Scanning for RuneLite JVM...", Brushes.Yellow);
            var candidates = FindRuneLiteCandidateProcesses();
            if (candidates.Count == 0)
            {
                updateStatus("No active game client found. Waiting...", Brushes.Orange);
                return;
            }

            bool attached = false;
            foreach (var (pid, name, title) in candidates)
            {
                updateStatus($"Attaching to {name} (PID {pid})...", Brushes.Cyan);
                if (TryAttachAgent(pid.ToString(), updateStatus))
                {
                    TrackRuneLiteProcess(pid, updateStatus);
                    attached = true;
                    updateStatus($"Attached to PID {pid}. Connecting...", Brushes.Lime);
                    break;
                }
            }

            if (!attached)
            {
                updateStatus("Attach attempt completed. Waiting for client...", Brushes.Red);
            }
        }
        catch (Exception ex)
        {
            LogMessage($"[ATTACH_BUTTON_ERROR] {ex.Message}");
            updateStatus($"Error: {ex.Message}", Brushes.Red);
        }
    }

    public void StartAutoAttachLoop(Func<bool> isConnected, Action<string, Brush> updateStatus, CancellationToken token)
    {
        Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    if (isConnected())
                    {
                        await Task.Delay(2000, token);
                        continue;
                    }

                    var candidates = FindRuneLiteCandidateProcesses();
                    if (candidates.Count > 0)
                    {
                        foreach (var (pid, name, title) in candidates)
                        {
                            if (isConnected()) break;

                            if (_failedPidCooldown.TryGetValue(pid, out var lastFailTime))
                            {
                                if ((DateTime.UtcNow - lastFailTime).TotalSeconds < 10)
                                {
                                    continue;
                                }
                            }

                            if (isConnected()) break;

                            updateStatus($"Detected JVM {name} (PID {pid}). Attaching...", Brushes.Cyan);

                            bool success = TryAttachAgent(pid.ToString(), updateStatus);
                            if (success)
                            {
                                TrackRuneLiteProcess(pid, updateStatus);

                                for (int i = 0; i < 7 && !isConnected(); i++)
                                {
                                    await Task.Delay(500, token);
                                }
                                if (isConnected()) break;

                                _failedPidCooldown[pid] = DateTime.UtcNow;
                            }
                            else
                            {
                                _failedPidCooldown[pid] = DateTime.UtcNow;
                            }
                        }

                        if (!isConnected())
                        {
                            if (candidates.Count > 0)
                            {
                                var first = candidates[0];
                                updateStatus($"RuneLite running (PID {first.Id}) - Restart RuneLite to connect with Bridge", Brushes.Orange);
                            }
                            else
                            {
                                updateStatus("Scanning for active OSRS clients...", Brushes.Orange);
                            }
                        }
                    }
                    else
                    {
                        if (!isConnected())
                            updateStatus("Waiting for OSRS / RuneLite to launch...", Brushes.Yellow);
                    }
                }
                catch (Exception ex)
                {
                    LogMessage($"[AUTO_ATTACH_LOOP_ERROR] {ex.Message}");
                }

                try
                {
                    await Task.Delay(2000, token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }, token);
    }
}
