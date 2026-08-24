using System.Collections.ObjectModel;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace osrsmr;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<DataItem> _dataItems = new();
    private readonly ObservableCollection<DataItem> _skills = new();
    private readonly ObservableCollection<NpcItem> _npcs = new();
    private readonly Border[] _inventorySlots = new Border[28];
    private readonly Dictionary<string, Border> _equipmentSlots = new();
    private TcpListener? _listener;
    private bool _running = true;
    private volatile bool _isAgentConnected = false;
    private TcpClient? _activeTcpClient = null;
    private int _activeSessionId = 0;
    private string? _lastAttachedPid = null;
    private readonly Dictionary<int, DateTime> _failedPidCooldown = new();
    private static readonly object _logLock = new();
    private string? _cachedJavaPath = null;

    [DllImport("kernel32.dll")]
    public static extern IntPtr OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId);

    [DllImport("kernel32.dll")]
    public static extern bool ReadProcessMemory(int hProcess, long lpBaseAddress, byte[] lpBuffer, int dwSize, ref int lpNumberOfBytesRead);

    [DllImport("kernel32.dll")]
    public static extern bool CloseHandle(IntPtr hObject);

    private const int PROCESS_WM_READ = 0x0010;

    public MainWindow()
    {
        try 
        {
            InitializeComponent();
            DataList.ItemsSource = _dataItems;
            SkillsControl.ItemsSource = _skills;
            NpcList.ItemsSource = _npcs;
            InitializeInventoryGrid();
            InitializeEquipmentMapping();
            StartServer();
            StartAutoAttachLoop();
            Task.Run(() => FixRuneLiteShortcut());
            
            // Log environment info
            _dataItems.Add(new DataItem { Key = "OS", Value = RuntimeInformation.OSDescription });
            _dataItems.Add(new DataItem { Key = "Bridge Version", Value = "1.2.0" });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"FATAL STARTUP ERROR: {ex.Message}\n\n{ex.StackTrace}", "Bridge Crash");
            throw;
        }
    }

    private void InitializeEquipmentMapping()
    {
        _equipmentSlots["0"] = Equip_Head;
        _equipmentSlots["1"] = Equip_Cape;
        _equipmentSlots["2"] = Equip_Neck;
        _equipmentSlots["3"] = Equip_Weapon;
        _equipmentSlots["4"] = Equip_Body;
        _equipmentSlots["5"] = Equip_Shield;
        _equipmentSlots["7"] = Equip_Legs;
        _equipmentSlots["9"] = Equip_Hands;
        _equipmentSlots["10"] = Equip_Feet;
        _equipmentSlots["12"] = Equip_Ring;
        _equipmentSlots["13"] = Equip_Ammo;
    }

    private void InitializeInventoryGrid()
    {
        for (int i = 0; i < 28; i++)
        {
            var border = new Border
            {
                BorderBrush = Brushes.DimGray,
                BorderThickness = new Thickness(1),
                Background = new SolidColorBrush(Color.FromArgb(40, 128, 128, 128)),
                Margin = new Thickness(2),
                Width = 36,
                Height = 36,
                ToolTip = $"Slot {i + 1}"
            };

            int row = i / 4;
            int col = i % 4;

            Grid.SetRow(border, row);
            Grid.SetColumn(border, col);
            InventoryGrid.Children.Add(border);
            _inventorySlots[i] = border;
        }
    }

    private async void StartServer()
    {
        try
        {
            _listener = new TcpListener(IPAddress.Loopback, 43594);
            _listener.Start();
            UpdateStatus("Listening on port 43594...", System.Windows.Media.Brushes.Cyan);

            while (_running)
            {
                var client = await _listener.AcceptTcpClientAsync();
                HandleClient(client);
            }
        }
        catch (Exception ex)
        {
            UpdateStatus($"Server Error: {ex.Message}", System.Windows.Media.Brushes.Red);
        }
    }

    private async void HandleClient(TcpClient client)
    {
        int sessionId = Interlocked.Increment(ref _activeSessionId);
        try
        {
            _activeTcpClient?.Close();
        }
        catch { }
        _activeTcpClient = client;
        _isAgentConnected = true;

        Dispatcher.Invoke(() => UpdateStatus("Agent Connected & Linked!", Brushes.Lime));
        LogMessage($"[BRIDGE] Agent client #{sessionId} connected to TCP port 43594.");
        
        // Update diagnostic info
        Dispatcher.Invoke(() => {
            var existing = _dataItems.FirstOrDefault(i => i.Key == "Agent Link");
            string val = $"Connected (#{sessionId}) at {DateTime.Now.ToLongTimeString()}";
            if (existing != null) existing.Value = val;
            else _dataItems.Add(new DataItem { Key = "Agent Link", Value = val });
        });

        try
        {
            using (client)
            using (var stream = client.GetStream())
            using (var reader = new System.IO.StreamReader(stream, System.Text.Encoding.UTF8))
            {
                while (_running && client.Connected && sessionId == _activeSessionId)
                {
                    string? line = await reader.ReadLineAsync();
                    if (line == null) break;

                    if (sessionId != _activeSessionId) break;

                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        ProcessLine(line);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            if (sessionId == _activeSessionId)
            {
                LogMessage($"[BRIDGE_SOCKET_ERROR] {ex.Message}");
            }
        }
        finally
        {
            if (sessionId == _activeSessionId)
            {
                _isAgentConnected = false;
                LogMessage($"[BRIDGE] Agent client #{sessionId} disconnected.");
                Dispatcher.Invoke(() => UpdateStatus("Agent Disconnected - Scanning for RuneLite...", Brushes.Yellow));
            }
        }
    }

    private void ProcessLine(string line)
    {
        Dispatcher.Invoke((Action)(() =>
        {
            if (line.Contains(":"))
            {
                var parts = line.Split(':', 2);
                var key = parts[0].Trim();
                var value = parts[1].Trim();

                if (key.StartsWith("INV["))
                {
                    UpdateInventorySlot(key, value);
                }
                else if (key.StartsWith("EQUIP["))
                {
                    UpdateEquipmentSlot(key, value);
                }
                else if (key.StartsWith("NPC["))
                {
                    UpdateNpcList(key, value);
                }
                else if (key == "TOTAL_NPCS")
                {
                    if (int.TryParse(value, out int totalNpcs))
                    {
                        while (_npcs.Count > totalNpcs)
                        {
                            _npcs.RemoveAt(_npcs.Count - 1);
                        }
                    }
                }
                else if (key.StartsWith("SKILL["))
                {
                    UpdateSkill(key, value);
                }
                else if (key == "PID")
                {
                    var existing = _dataItems.FirstOrDefault(i => i.Key == "Client PID");
                    if (existing != null)
                        existing.Value = value;
                    else
                        _dataItems.Add(new DataItem { Key = "Client PID", Value = value });
                }
                else if (key == "Status" || key == "Status:")
                {
                    var existing = _dataItems.FirstOrDefault(i => i.Key == "Agent Status");
                    if (existing != null)
                        existing.Value = value;
                    else
                        _dataItems.Add(new DataItem { Key = "Agent Status", Value = value });
                }
                else if (key == "Client Class" || key == "Client Class:")
                {
                    var existing = _dataItems.FirstOrDefault(i => i.Key == key);
                    if (existing != null)
                        existing.Value = value;
                    else
                        _dataItems.Add(new DataItem { Key = key, Value = value });
                }
                else if (key == "Searching for Game Client...")
                {
                    var existing = _dataItems.FirstOrDefault(i => i.Key == "Discovery");
                    if (existing != null)
                        existing.Value = "Scanning...";
                    else
                        _dataItems.Add(new DataItem { Key = "Discovery", Value = "Scanning..." });
                }
                else if (key == "GameState" || key == "ENGINE_STATE" || key == "Game State")
                {
                    if (value == "Logged In" || value == "30")
                    {
                        UpdateStatus("Bridge Linked: Logged In", Brushes.Lime);
                    }
                    else if (value == "Login Screen" || value == "10" || value == "11")
                    {
                        UpdateStatus("At Login Screen", Brushes.Cyan);
                    }
                    else if (value == "Logging In" || value == "20")
                    {
                        UpdateStatus("Logging In...", Brushes.Cyan);
                    }
                    else if (value == "Loading" || value == "25")
                    {
                        UpdateStatus("Game Loading...", Brushes.Yellow);
                    }
                    else if (value == "Hopping" || value == "45")
                    {
                        UpdateStatus("Hopping Worlds...", Brushes.Yellow);
                    }
                    else if (value == "Starting" || value == "1")
                    {
                        UpdateStatus("RuneLite Initializing...", Brushes.Yellow);
                    }
                    else if (value == "Connection Lost" || value == "40")
                    {
                        UpdateStatus("Connection Lost", Brushes.Orange);
                    }
                    else if (value == "Detecting..." || value == "0" || value == "Unknown")
                    {
                        if (StatusLabel.Text != "Bridge Linked: Logged In" && StatusLabel.Text != "At Login Screen" && !StatusLabel.Text.StartsWith("Logging In"))
                        {
                            UpdateStatus("Connected - Scanning Game Data...", Brushes.Yellow);
                        }
                    }
                    
                    var existing = _dataItems.FirstOrDefault(i => i.Key == key);
                    if (existing != null)
                        existing.Value = value;
                    else
                        _dataItems.Add(new DataItem { Key = key, Value = value });
                }
                else if (key == "PLAYER_NAME")
                {
                    PlayerNameText.Text = value;
                }
                else if (key == "LOCATION")
                {
                    PlayerLocationText.Text = value;
                }
                else if (key == "PLAYER_X" || key == "PLAYER_Y")
                {
                    var existing = _dataItems.FirstOrDefault(i => i.Key == key);
                    if (existing != null)
                        existing.Value = value;
                    else
                        _dataItems.Add(new DataItem { Key = key, Value = value });
                        
                    var px = _dataItems.FirstOrDefault(i => i.Key == "PLAYER_X")?.Value;
                    var py = _dataItems.FirstOrDefault(i => i.Key == "PLAYER_Y")?.Value;
                    if (px != null && py != null)
                    {
                        PlayerLocationText.Text = $"({px}, {py})";
                    }
                }
                else if (key == "CURRENT_TAB")
                {
                    UpdateCurrentTab(value);
                }
                else if (key == "LOCATION_STATUS")
                {
                    var existing = _dataItems.FirstOrDefault(i => i.Key == key);
                    if (existing != null)
                        existing.Value = value;
                    else
                        _dataItems.Add(new DataItem { Key = key, Value = value });
                }
                else
                {
                    var existing = _dataItems.FirstOrDefault(i => i.Key == key);
                    if (existing != null)
                        existing.Value = value;
                    else
                        _dataItems.Add(new DataItem { Key = key, Value = value });
                }
            }
        }));
    }

    private void UpdateCurrentTab(string value)
    {
        if (int.TryParse(value, out int tabIndex))
        {
            string[] tabs = { 
                "Combat", "Skills", "Quest", "Inventory", "Equipment", 
                "Prayer", "Magic", "Clan", "Account", "Friends", 
                "Ignore", "Logout", "Settings", "Emotes", "Music" 
            };
            if (tabIndex >= 0 && tabIndex < tabs.Length)
                ActiveTabText.Text = tabs[tabIndex];
            else
                ActiveTabText.Text = "Unknown (" + tabIndex + ")";
        }
        else
        {
            ActiveTabText.Text = value;
        }
    }

    private void UpdateSkill(string key, string value)
    {
        // Format: SKILL[Attack]: 99
        try
        {
            int openBracket = key.IndexOf('[');
            int closeBracket = key.IndexOf(']');
            if (openBracket != -1 && closeBracket != -1)
            {
                string skillName = key.Substring(openBracket + 1, closeBracket - openBracket - 1);
                var existing = _skills.FirstOrDefault(s => s.Key == skillName);
                if (existing != null)
                    existing.Value = value;
                else
                    _skills.Add(new DataItem { Key = skillName, Value = value });
            }
        }
        catch { }
    }

    private void UpdateNpcList(string key, string value)
    {
        // Format: NPC[0]: ID, Name, Distance, Health
        try
        {
            int openBracket = key.IndexOf('[');
            int closeBracket = key.IndexOf(']');
            if (openBracket != -1 && closeBracket != -1)
            {
                int index = int.Parse(key.Substring(openBracket + 1, closeBracket - openBracket - 1));
                var parts = value.Split(',');
                if (parts.Length >= 4)
                {
                    var npc = new NpcItem
                    {
                        Id = parts[0].Trim(),
                        Name = parts[1].Trim(),
                        Distance = parts[2].Trim(),
                        Health = parts[3].Trim()
                    };

                    if (index < _npcs.Count)
                        _npcs[index] = npc;
                    else
                        _npcs.Add(npc);
                }
            }
        }
        catch { }
    }

    private void UpdateEquipmentSlot(string key, string value)
    {
        // Format: EQUIP[slotId]: ID, Quantity or Name, Quantity
        try
        {
            int openBracket = key.IndexOf('[');
            int closeBracket = key.IndexOf(']');
            if (openBracket != -1 && closeBracket != -1)
            {
                string slotId = key.Substring(openBracket + 1, closeBracket - openBracket - 1);
                if (_equipmentSlots.TryGetValue(slotId, out var border))
                {
                    if (string.IsNullOrWhiteSpace(value) || value == "0" || value == "-1" || value == "0,0" || value.StartsWith("0,"))
                    {
                        border.Background = new SolidColorBrush(Color.FromRgb(42, 42, 42));
                        border.Child = null;
                    }
                    else
                    {
                        var parts = value.Split(',');
                        string displayText = parts[0].Trim();
                        if (parts.Length > 1 && int.TryParse(parts[1].Trim(), out int qty) && qty > 1)
                        {
                            displayText = $"{displayText} ({qty})";
                        }

                        border.Background = new SolidColorBrush(Color.FromRgb(0, 100, 150));
                        border.Child = new TextBlock
                        {
                            Text = displayText,
                            FontSize = 8,
                            Foreground = Brushes.White,
                            TextWrapping = TextWrapping.Wrap,
                            TextAlignment = TextAlignment.Center,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            VerticalAlignment = VerticalAlignment.Center
                        };
                    }
                }
            }
        }
        catch { }
    }

    private void UpdateInventorySlot(string key, string value)
    {
        // Format: INV[0]: ID, Quantity or Name, Quantity
        try
        {
            int openBracket = key.IndexOf('[');
            int closeBracket = key.IndexOf(']');
            if (openBracket != -1 && closeBracket != -1)
            {
                int index = int.Parse(key.Substring(openBracket + 1, closeBracket - openBracket - 1));
                if (index >= 0 && index < 28)
                {
                    if (string.IsNullOrWhiteSpace(value) || value == "0" || value == "-1" || value == "0,0" || value.StartsWith("0,"))
                    {
                        _inventorySlots[index].Background = new SolidColorBrush(Color.FromArgb(40, 128, 128, 128));
                        _inventorySlots[index].Child = null;
                    }
                    else
                    {
                        var parts = value.Split(',');
                        string displayText = parts[0].Trim();
                        if (parts.Length > 1 && int.TryParse(parts[1].Trim(), out int qty) && qty > 1)
                        {
                            displayText = $"{displayText} ({qty})";
                        }

                        _inventorySlots[index].Background = new SolidColorBrush(Color.FromArgb(100, 0, 122, 204));
                        _inventorySlots[index].Child = new TextBlock 
                        { 
                            Text = displayText, 
                            FontSize = 8, 
                            Foreground = Brushes.White,
                            TextWrapping = TextWrapping.Wrap,
                            TextAlignment = TextAlignment.Center,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            VerticalAlignment = VerticalAlignment.Center
                        };
                    }
                }
            }
        }
        catch { }
    }

    private void UpdateStatus(string text, System.Windows.Media.Brush color)
    {
        Dispatcher.Invoke(() =>
        {
            StatusLabel.Text = text;
            StatusLabel.Foreground = color;
        });
    }

    private void ClearLogs_Click(object sender, RoutedEventArgs e) => _dataItems.Clear();

    private void RestartHook_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("Please restart your OSRS client to reload the hook.", "Restart Info");
    }

    private void RepairConfig_Click(object sender, RoutedEventArgs e)
    {
        CheckAndFixRuneLiteConfig();
    }

    private void ViewLogs_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (System.IO.File.Exists("attach_log.txt"))
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "notepad.exe",
                    Arguments = "attach_log.txt",
                    UseShellExecute = true
                });
            }
            else
            {
                MessageBox.Show("attach_log.txt not found.", "Logs");
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to open logs: {ex.Message}", "Error");
        }
    }

    private void AttachButton_Click(object sender, RoutedEventArgs e)
    {
        _failedPidCooldown.Clear();
        Task.Run(() => FindAndAttachRuneLite());
    }

    private void FindAndAttachRuneLite()
    {
        try
        {
            Dispatcher.Invoke(() => UpdateStatus("Scanning for RuneLite / Java...", Brushes.Yellow));
            
            var candidates = FindRuneLiteCandidateProcesses();
            if (candidates.Count == 0)
            {
                Dispatcher.Invoke(() => UpdateStatus("RuneLite not found! Waiting for client...", Brushes.Orange));
                return;
            }

            bool attached = false;
            foreach (var (pid, name, title) in candidates)
            {
                Dispatcher.Invoke(() => UpdateStatus($"Attaching to {name} (PID {pid})...", Brushes.Cyan));
                if (TryAttachAgent(pid.ToString()))
                {
                    _lastAttachedPid = pid.ToString();
                    attached = true;
                    Dispatcher.Invoke(() => UpdateStatus($"Attached to PID {pid}. Connecting...", Brushes.Lime));
                    break;
                }
            }

            if (!attached)
            {
                Dispatcher.Invoke(() => UpdateStatus("Attach attempt completed. Waiting for JVM...", Brushes.Red));
            }
        }
        catch (Exception ex)
        {
            LogMessage($"[ATTACH_BUTTON_ERROR] {ex.Message}");
            Dispatcher.Invoke(() => UpdateStatus($"Error: {ex.Message}", Brushes.Red));
        }
    }

    private List<(int Id, string Name, string Title)> FindRuneLiteCandidateProcesses()
    {
        var list = new List<(int Id, string Name, string Title)>();
        try
        {
            var processes = System.Diagnostics.Process.GetProcesses();
            foreach (var p in processes)
            {
                try
                {
                    string name = p.ProcessName.ToLowerInvariant();
                    string title = p.MainWindowTitle;
                    string titleLower = title.ToLowerInvariant();

                    bool match = false;
                    if (name.Contains("runelite") || titleLower.Contains("runelite"))
                    {
                        match = true;
                    }
                    else if ((name.Contains("java") || name.Contains("javaw")) && (titleLower.Contains("runelite") || titleLower.Contains("old school") || titleLower.Contains("osrs")))
                    {
                        match = true;
                    }
                    else if (name.Contains("osclient") || titleLower.Contains("old school runescape"))
                    {
                        match = true;
                    }

                    if (match)
                    {
                        list.Add((p.Id, p.ProcessName, title));
                    }
                }
                catch { }
            }
        }
        catch (Exception ex)
        {
            LogMessage($"[CANDIDATE_SCAN_ERROR] {ex.Message}");
        }

        // If there are processes with active RuneLite / Old School window titles, strictly prioritize those
        var titled = list.Where(c => !string.IsNullOrWhiteSpace(c.Title) && (c.Title.ToLowerInvariant().Contains("runelite") || c.Title.ToLowerInvariant().Contains("old school"))).ToList();
        if (titled.Count > 0)
        {
            return titled;
        }

        return list
            .OrderByDescending(c => c.Title.ToLowerInvariant().Contains("runelite"))
            .ThenByDescending(c => c.Title.ToLowerInvariant().Contains("old school"))
            .ThenByDescending(c => c.Name.Equals("javaw", StringComparison.OrdinalIgnoreCase) || c.Name.Equals("java", StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(c => c.Name.ToLowerInvariant().Contains("runelite"))
            .ToList();
    }

    private void LaunchButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string runeLiteExe = System.IO.Path.Combine(appData, @"RuneLite\RuneLite.exe");
            
            if (!System.IO.File.Exists(runeLiteExe))
            {
                string progFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                runeLiteExe = System.IO.Path.Combine(progFiles, @"RuneLite\RuneLite.exe");
            }
            if (!System.IO.File.Exists(runeLiteExe))
            {
                string progFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
                runeLiteExe = System.IO.Path.Combine(progFilesX86, @"RuneLite\RuneLite.exe");
            }

            if (!System.IO.File.Exists(runeLiteExe))
            {
                MessageBox.Show("Could not find RuneLite.exe at standard installation paths.", "RuneLite Not Found");
                return;
            }

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string agentPath = System.IO.Path.Combine(baseDir, "agent.jar");
            if (!System.IO.File.Exists(agentPath))
                agentPath = System.IO.Path.Combine(Environment.CurrentDirectory, "agent.jar");
            agentPath = System.IO.Path.GetFullPath(agentPath);

            FixRuneLiteShortcut(agentPath, runeLiteExe);

            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = runeLiteExe,
                Arguments = $"-J-XX:-DisableAttachMechanism -J-javaagent:\"{agentPath}\"",
                WorkingDirectory = System.IO.Path.GetDirectoryName(runeLiteExe),
                UseShellExecute = true
            };

            System.Diagnostics.Process.Start(psi);
            UpdateStatus("Launching RuneLite with Hook...", Brushes.Cyan);
            LogMessage($"[LAUNCH] Started RuneLite: {runeLiteExe} {psi.Arguments}");
        }
        catch (Exception ex)
        {
            LogMessage($"[LAUNCH_ERROR] {ex.Message}");
            UpdateStatus($"Launch error: {ex.Message}", Brushes.Red);
        }
    }

    private void FixRuneLiteShortcut(string? agentPath = null, string? runeLiteExe = null)
    {
        try
        {
            if (string.IsNullOrEmpty(agentPath))
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                agentPath = System.IO.Path.Combine(baseDir, "agent.jar");
                if (!System.IO.File.Exists(agentPath))
                    agentPath = System.IO.Path.Combine(Environment.CurrentDirectory, "agent.jar");
                agentPath = System.IO.Path.GetFullPath(agentPath);
            }

            if (string.IsNullOrEmpty(runeLiteExe))
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                runeLiteExe = System.IO.Path.Combine(appData, @"RuneLite\RuneLite.exe");
                if (!System.IO.File.Exists(runeLiteExe))
                {
                    string progFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                    runeLiteExe = System.IO.Path.Combine(progFiles, @"RuneLite\RuneLite.exe");
                }
            }

            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            string shortcutPath = System.IO.Path.Combine(desktop, "RuneLite.lnk");

            if (System.IO.File.Exists(runeLiteExe))
            {
                Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType != null)
                {
                    dynamic shell = Activator.CreateInstance(shellType)!;
                    dynamic shortcut = shell.CreateShortcut(shortcutPath);
                    shortcut.TargetPath = runeLiteExe;
                    shortcut.Arguments = $"-J-XX:-DisableAttachMechanism -J-javaagent:\"{agentPath}\"";
                    shortcut.WorkingDirectory = System.IO.Path.GetDirectoryName(runeLiteExe);
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

    private bool _configChecked = false;

    private void CheckAndFixRuneLiteConfig()
    {
        if (_configChecked) return;
        try
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string configPath = System.IO.Path.Combine(appData, "RuneLite", "config.json");
            
            // Get absolute path to agent.jar
            string agentPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "agent.jar");
            if (!System.IO.File.Exists(agentPath))
                agentPath = System.IO.Path.Combine(Environment.CurrentDirectory, "agent.jar");
            
            agentPath = System.IO.Path.GetFullPath(agentPath).Replace("\\", "/");

            if (System.IO.File.Exists(configPath))
            {
                string content = System.IO.File.ReadAllText(configPath);
                
                // 1. Remove all occurrences of blocking flags from the array
                string[] blockingFlags = {
                    "-XX:\\+DisableAttachMechanism",
                    "-Drunelite\\.launcher\\.nojvm=true",
                    "-XX:-UsePerfData",
                    "-Drunelite\\.launcher\\.nojvm\\\\u003dtrue"
                };

                bool changed = false;
                foreach (var flag in blockingFlags)
                {
                    string pattern = $"(,?\\s*\"{flag}\"\\s*)|(\\s*\"{flag}\"\\s*,?)";
                    if (Regex.IsMatch(content, pattern))
                    {
                        content = Regex.Replace(content, pattern, "");
                        changed = true;
                    }
                }

                // 2. Ensure -javaagent is present and unique
                string escapedPath = agentPath.Replace("/", "\\\\");
                string agentArg = $"-javaagent:{escapedPath}";
                
                if (content.Contains("-javaagent:"))
                {
                    string oldAgentPattern = @"\s*""-javaagent:[^""]+""\s*,?|\s*,?\s*""-javaagent:[^""]+""\s*";
                    if (Regex.IsMatch(content, oldAgentPattern) && !content.Contains(agentArg))
                    {
                        content = Regex.Replace(content, oldAgentPattern, "");
                        changed = true;
                    }
                }

                // 3. Clean up the array format
                content = Regex.Replace(content, @",\s*,", ",");
                content = Regex.Replace(content, @"\[\s*,", "[");
                content = Regex.Replace(content, @",\s*\]", "]");
                content = Regex.Replace(content, @"\[\s*\]", "[]");

                // 4. Inject the correct javaagent into vmArgs if missing
                if (!content.Contains(agentArg))
                {
                    var vmArgsMatch = Regex.Match(content, "\"vmArgs\"\\s*:\\s*\\[");
                    if (vmArgsMatch.Success)
                    {
                        int insertIndex = vmArgsMatch.Index + vmArgsMatch.Length;
                        string injection = $"\n      \"{agentArg}\"";
                        
                        if (!Regex.IsMatch(content.Substring(insertIndex), @"^\s*\]"))
                        {
                            injection += ",";
                        }
                        
                        content = content.Insert(insertIndex, injection);
                        changed = true;
                    }
                }

                if (changed)
                {
                    System.IO.File.WriteAllText(configPath, content);
                    UpdateStatus("Config Repaired & Hooked.", Brushes.Lime);
                    MessageBox.Show("RuneLite configuration has been optimized.\n\nPlease RESTART RuneLite now.", "Bridge Optimized");
                }
                _configChecked = true;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to fix config: {ex.Message}");
        }
    }

    private void RepairRuneLiteConfig()
    {
        try
        {
            FixRuneLiteShortcut();
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string configPath = System.IO.Path.Combine(appData, "RuneLite", "config.json");

            if (System.IO.File.Exists(configPath))
            {
                string content = System.IO.File.ReadAllText(configPath);
                
                // Remove -javaagent
                string oldAgentPattern = @"\s*""-javaagent:[^""]+""\s*,?|\s*,?\s*""-javaagent:[^""]+""\s*";
                content = Regex.Replace(content, oldAgentPattern, "");

                // Clean up format
                content = Regex.Replace(content, @",\s*,", ",");
                content = Regex.Replace(content, @"\[\s*,", "[");
                content = Regex.Replace(content, @",\s*\]", "]");
                content = Regex.Replace(content, @"\[\s*\]", "[]");

                System.IO.File.WriteAllText(configPath, content);
                UpdateStatus("Desktop RuneLite shortcut updated with Bridge hook.", Brushes.Lime);
                MessageBox.Show("Desktop RuneLite shortcut and configuration have been synchronized!\n\nYou can launch RuneLite directly or click 'Launch RuneLite'.", "Shortcuts Repaired");
            }
            else
            {
                UpdateStatus("Desktop RuneLite shortcut updated with Bridge hook.", Brushes.Lime);
                MessageBox.Show("Desktop RuneLite shortcut has been synchronized!", "Shortcuts Repaired");
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to repair shortcuts: {ex.Message}", "Error");
        }
    }

    private void LogMessage(string message)
    {
        try
        {
            lock (_logLock)
            {
                System.IO.File.AppendAllText("attach_log.txt", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}\n");
            }
        }
        catch { }
    }

    private void StartAutoAttachLoop()
    {
        Task.Run(async () =>
        {
            while (_running)
            {
                try
                {
                    if (!_isAgentConnected)
                    {
                        var candidates = FindRuneLiteCandidateProcesses();
                        if (candidates.Count > 0)
                        {
                            foreach (var (pid, name, title) in candidates)
                            {
                                if (_isAgentConnected) break;

                                if (_failedPidCooldown.TryGetValue(pid, out var lastFailTime))
                                {
                                    if ((DateTime.UtcNow - lastFailTime).TotalSeconds < 8)
                                    {
                                        continue;
                                    }
                                }

                                if (_isAgentConnected) break;

                                Dispatcher.Invoke(() =>
                                {
                                    if (!_isAgentConnected)
                                        UpdateStatus($"Detected {name} (PID {pid}). Attaching...", Brushes.Cyan);
                                });

                                bool success = TryAttachAgent(pid.ToString());
                                if (success)
                                {
                                    _lastAttachedPid = pid.ToString();
                                    _failedPidCooldown.Remove(pid);

                                    // Wait up to 3.5s for agent socket
                                    for (int i = 0; i < 7 && !_isAgentConnected; i++)
                                    {
                                        await Task.Delay(500);
                                    }
                                    if (_isAgentConnected) break;
                                }
                                else
                                {
                                    _failedPidCooldown[pid] = DateTime.UtcNow;
                                }
                            }

                            if (!_isAgentConnected)
                            {
                                Dispatcher.Invoke(() =>
                                {
                                    if (!_isAgentConnected)
                                        UpdateStatus("Scanning for active RuneLite JVM...", Brushes.Orange);
                                });
                            }
                        }
                        else
                        {
                            Dispatcher.Invoke(() =>
                            {
                                if (!_isAgentConnected)
                                    UpdateStatus("Waiting for RuneLite to launch...", Brushes.Yellow);
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogMessage($"[AUTO_ATTACH_LOOP_ERROR] {ex.Message}");
                }

                await Task.Delay(1500);
            }
        });
    }

    private string GetCompatibleJavaPath()
    {
        if (!string.IsNullOrEmpty(_cachedJavaPath) && System.IO.File.Exists(_cachedJavaPath))
        {
            return _cachedJavaPath;
        }

        var candidatePaths = new List<string>();

        // 1. JetBrains installations
        try
        {
            if (System.IO.Directory.Exists(@"C:\Program Files\JetBrains"))
            {
                foreach (var dir in System.IO.Directory.GetDirectories(@"C:\Program Files\JetBrains"))
                {
                    string jbrJava = System.IO.Path.Combine(dir, "jbr", "bin", "java.exe");
                    if (System.IO.File.Exists(jbrJava)) candidatePaths.Add(jbrJava);
                }
            }
        }
        catch { }

        // 2. LocalAppData JetBrains
        try
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string riderJbr = System.IO.Path.Combine(localAppData, @"Programs\Rider\jbr\bin\java.exe");
            if (System.IO.File.Exists(riderJbr)) candidatePaths.Add(riderJbr);
        }
        catch { }

        // 3. Environment variables
        string? javaHome = Environment.GetEnvironmentVariable("JAVA_HOME");
        if (!string.IsNullOrEmpty(javaHome))
        {
            string j = System.IO.Path.Combine(javaHome, "bin", "java.exe");
            if (System.IO.File.Exists(j)) candidatePaths.Add(j);
        }
        string? jdkHome = Environment.GetEnvironmentVariable("JDK_HOME");
        if (!string.IsNullOrEmpty(jdkHome))
        {
            string j = System.IO.Path.Combine(jdkHome, "bin", "java.exe");
            if (System.IO.File.Exists(j)) candidatePaths.Add(j);
        }

        // 4. Standard JDK paths
        string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        string[] jdkRoots = {
            System.IO.Path.Combine(programFiles, "Java"),
            System.IO.Path.Combine(programFiles, "Eclipse Adoptium"),
            System.IO.Path.Combine(programFiles, "Microsoft"),
            System.IO.Path.Combine(programFiles, "Amazon Corretto"),
            System.IO.Path.Combine(programFiles, "Zulu")
        };

        foreach (var root in jdkRoots)
        {
            try
            {
                if (System.IO.Directory.Exists(root))
                {
                    foreach (var dir in System.IO.Directory.GetDirectories(root))
                    {
                        string j = System.IO.Path.Combine(dir, "bin", "java.exe");
                        if (System.IO.File.Exists(j)) candidatePaths.Add(j);
                    }
                }
            }
            catch { }
        }

        // Test candidates for jdk.attach
        foreach (var path in candidatePaths.Distinct())
        {
            if (!System.IO.File.Exists(path)) continue;

            LogMessage($"[JAVA_CHECK] Testing JDK: {path}");
            try
            {
                var checkPsi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = path,
                    Arguments = "--list-modules",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                using var proc = System.Diagnostics.Process.Start(checkPsi);
                if (proc != null)
                {
                    string outStr = proc.StandardOutput.ReadToEnd();
                    string errStr = proc.StandardError.ReadToEnd();
                    proc.WaitForExit(3000);
                    if (outStr.Contains("jdk.attach"))
                    {
                        _cachedJavaPath = path;
                        LogMessage($"[JAVA_SELECT] Selected compatible JDK with jdk.attach: {path}");
                        return path;
                    }
                }
            }
            catch (Exception ex)
            {
                LogMessage($"[JAVA_CHECK_ERROR] {path}: {ex.Message}");
            }
        }

        LogMessage("[JAVA_WARN] No compatible JDK with jdk.attach found in candidate list. Falling back to system 'java'.");
        return "java";
    }

    private bool TryAttachAgent(string pid)
    {
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string agentPath = System.IO.Path.Combine(baseDir, "agent.jar");
        
        if (!System.IO.File.Exists(agentPath))
            agentPath = System.IO.Path.Combine(Environment.CurrentDirectory, "agent.jar");
        
        agentPath = System.IO.Path.GetFullPath(agentPath);

        if (!System.IO.File.Exists(agentPath))
        {
            LogMessage($"[ATTACH_ERROR] agent.jar not found at {agentPath}");
            Dispatcher.Invoke(() => UpdateStatus("Error: agent.jar not found!", Brushes.Red));
            return false;
        }

        string javaExe = GetCompatibleJavaPath();

        try
        {
            LogMessage($"[ATTACH_EXEC] Attaching to PID {pid} using {javaExe} and agent {agentPath}");

            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = javaExe,
                Arguments = $"-Djdk.attach.allowAttachSelf=true --add-modules jdk.attach -cp \"{agentPath}\" com.osrsmr.attach.AttachHelper {pid} \"{agentPath}\"",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = System.IO.Path.GetDirectoryName(agentPath)
            };

            using var process = System.Diagnostics.Process.Start(psi);
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
                Dispatcher.Invoke(() => UpdateStatus("RuneLite blocked attach. Click 'Launch RuneLite' to start hooked.", Brushes.Orange));
                LogMessage("[ATTACH_HINT] Target JVM has DisableAttachMechanism active. Use 'Launch RuneLite' button or updated desktop shortcut to start RuneLite with hook.");
            }

            return false;
        }
        catch (Exception ex)
        {
            LogMessage($"[ATTACH_EXCEPTION] PID {pid}: {ex.Message}");
            return false;
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _running = false;
        _listener?.Stop();
        base.OnClosed(e);
    }
}

public class DataItem : System.ComponentModel.INotifyPropertyChanged
{
    private string _key = "";
    private string _value = "";

    public string Key { get => _key; set { _key = value; OnPropertyChanged(nameof(Key)); } }
    public string Value { get => _value; set { _value = value; OnPropertyChanged(nameof(Value)); } }

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));
}

public class NpcItem
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Distance { get; set; } = "";
    public string Health { get; set; } = "";
}
