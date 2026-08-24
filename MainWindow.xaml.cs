using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text.Json;
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
    private readonly ObservableCollection<PlayerItem> _players = new();
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
            PlayerList.ItemsSource = _players;
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
                else if (key.StartsWith("NEARBY_PLAYER[") || key.StartsWith("PLAYER_NEARBY[") || (key.StartsWith("PLAYER[") && !key.StartsWith("PLAYER_NAME")))
                {
                    UpdatePlayerList(key, value);
                }
                else if (key == "TOTAL_PLAYERS" || key == "TOTAL_NEARBY_PLAYERS")
                {
                    if (int.TryParse(value, out int totalPlayers))
                    {
                        while (_players.Count > totalPlayers)
                        {
                            _players.RemoveAt(_players.Count - 1);
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

    private void UpdatePlayerList(string key, string value)
    {
        // Format: NEARBY_PLAYER[0]: ID, Name, Distance, CombatLevel
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
                    var player = new PlayerItem
                    {
                        Id = parts[0].Trim(),
                        Name = parts[1].Trim(),
                        Distance = parts[2].Trim(),
                        CombatLevel = parts[3].Trim()
                    };

                    if (index < _players.Count)
                        _players[index] = player;
                    else
                        _players.Add(player);
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
                    if (string.IsNullOrWhiteSpace(value) || value == "0" || value == "-1" || value == "65535" || value == "0,0" || value.StartsWith("0,") || value.StartsWith("-1,") || value.StartsWith("65535,"))
                    {
                        border.Background = new SolidColorBrush(Color.FromRgb(42, 42, 42));
                        border.ToolTip = null;
                        border.Child = null;
                    }
                    else
                    {
                        int lastComma = value.LastIndexOf(',');
                        string displayText = lastComma >= 0 ? value.Substring(0, lastComma).Trim() : value.Trim();
                        int qty = 1;
                        if (lastComma >= 0 && int.TryParse(value.Substring(lastComma + 1).Trim(), out int parsedQty))
                        {
                            qty = parsedQty;
                        }

                        displayText = ItemDatabase.ResolveItemName(displayText);

                        string toolTipText = qty > 1 ? $"{displayText} (x{qty})" : displayText;
                        string labelText = qty > 1 ? $"{displayText}\nx{qty}" : displayText;

                        border.Background = new SolidColorBrush(Color.FromRgb(0, 100, 150));
                        border.ToolTip = toolTipText;
                        border.Child = new TextBlock
                        {
                            Text = labelText,
                            FontSize = 7.5,
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
                    if (string.IsNullOrWhiteSpace(value) || value == "0" || value == "-1" || value == "65535" || value == "0,0" || value.StartsWith("0,") || value.StartsWith("-1,") || value.StartsWith("65535,"))
                    {
                        _inventorySlots[index].Background = new SolidColorBrush(Color.FromArgb(40, 128, 128, 128));
                        _inventorySlots[index].ToolTip = $"Slot {index + 1}: Empty";
                        _inventorySlots[index].Child = null;
                    }
                    else
                    {
                        int lastComma = value.LastIndexOf(',');
                        string displayText = lastComma >= 0 ? value.Substring(0, lastComma).Trim() : value.Trim();
                        int qty = 1;
                        if (lastComma >= 0 && int.TryParse(value.Substring(lastComma + 1).Trim(), out int parsedQty))
                        {
                            qty = parsedQty;
                        }

                        displayText = ItemDatabase.ResolveItemName(displayText);

                        string toolTipText = qty > 1 ? $"{displayText} (x{qty})" : displayText;
                        string labelText = qty > 1 ? $"{displayText}\nx{qty}" : displayText;

                        _inventorySlots[index].Background = new SolidColorBrush(Color.FromArgb(140, 0, 110, 180));
                        _inventorySlots[index].ToolTip = toolTipText;
                        _inventorySlots[index].Child = new TextBlock 
                        { 
                            Text = labelText, 
                            FontSize = 7.5, 
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

                                    // Wait up to 3.5s for agent socket
                                    for (int i = 0; i < 7 && !_isAgentConnected; i++)
                                    {
                                        await Task.Delay(500);
                                    }
                                    if (_isAgentConnected) break;

                                    // Give newly attached agent a 12-second grace period before attempting re-attach
                                    _failedPidCooldown[pid] = DateTime.UtcNow;
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

public class PlayerItem
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Distance { get; set; } = "";
    public string CombatLevel { get; set; } = "";
}

public static class ItemDatabase
{
    private static readonly ConcurrentDictionary<int, string> _items = new();
    private static readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(10) };

    static ItemDatabase()
    {
        InitializeStaticItems();
        Task.Run(InitializeOnlineMappingAsync);
    }

    public static string ResolveItemName(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return "";
        if (int.TryParse(input, out int id))
        {
            return GetItemName(id);
        }
        return input;
    }

    public static string GetItemName(int id)
    {
        if (id <= 0) return "";
        if (_items.TryGetValue(id, out var name))
        {
            return name;
        }
        return id.ToString();
    }

    private static void InitializeStaticItems()
    {
        _items[995] = "Coins";
        _items[1351] = "Bronze axe";
        _items[1349] = "Iron axe";
        _items[1353] = "Steel axe";
        _items[1355] = "Mithril axe";
        _items[1357] = "Adamant axe";
        _items[1359] = "Rune axe";
        _items[6739] = "Dragon axe";
        _items[1265] = "Bronze pickaxe";
        _items[1267] = "Iron pickaxe";
        _items[1269] = "Steel pickaxe";
        _items[1273] = "Mithril pickaxe";
        _items[1271] = "Adamant pickaxe";
        _items[1275] = "Rune pickaxe";
        _items[11920] = "Dragon pickaxe";
        _items[303] = "Small fishing net";
        _items[307] = "Fishing rod";
        _items[309] = "Fly fishing rod";
        _items[311] = "Harpoon";
        _items[301] = "Lobster pot";
        _items[313] = "Fishing bait";
        _items[314] = "Feather";
        _items[590] = "Tinderbox";
        _items[1755] = "Chisel";
        _items[2347] = "Hammer";
        _items[1733] = "Needle";
        _items[1734] = "Thread";
        _items[946] = "Knife";
        _items[1925] = "Bucket";
        _items[1929] = "Bucket of water";
        _items[1935] = "Jug";
        _items[1937] = "Jug of water";
        _items[227] = "Vial of water";
        _items[229] = "Vial";
        _items[554] = "Fire rune";
        _items[555] = "Water rune";
        _items[556] = "Air rune";
        _items[557] = "Earth rune";
        _items[558] = "Mind rune";
        _items[559] = "Body rune";
        _items[560] = "Death rune";
        _items[561] = "Nature rune";
        _items[562] = "Chaos rune";
        _items[563] = "Law rune";
        _items[564] = "Cosmic rune";
        _items[565] = "Blood rune";
        _items[566] = "Soul rune";
        _items[21880] = "Wrath rune";
        _items[9075] = "Astral rune";
        _items[315] = "Shrimps";
        _items[325] = "Salmon";
        _items[329] = "Salmon";
        _items[333] = "Trout";
        _items[377] = "Lobster";
        _items[379] = "Lobster";
        _items[383] = "Raw shark";
        _items[385] = "Shark";
        _items[386] = "Shark (noted)";
        _items[395] = "Sea turtle";
        _items[397] = "Sea turtle";
        _items[389] = "Manta ray";
        _items[391] = "Manta ray";
        _items[3144] = "Cooked karambwan";
        _items[13441] = "Anglerfish";
        _items[11936] = "Dark crab";
        _items[7946] = "Monkfish";
        _items[2434] = "Prayer potion(4)";
        _items[139] = "Prayer potion(3)";
        _items[141] = "Prayer potion(2)";
        _items[143] = "Prayer potion(1)";
        _items[6685] = "Saradomin brew(4)";
        _items[6687] = "Saradomin brew(3)";
        _items[6689] = "Saradomin brew(2)";
        _items[6691] = "Saradomin brew(1)";
        _items[3024] = "Super restore(4)";
        _items[3026] = "Super restore(3)";
        _items[3028] = "Super restore(2)";
        _items[3030] = "Super restore(1)";
        _items[12625] = "Stamina potion(4)";
        _items[12627] = "Stamina potion(3)";
        _items[12629] = "Stamina potion(2)";
        _items[12631] = "Stamina potion(1)";
        _items[2440] = "Super strength(4)";
        _items[157] = "Super strength(3)";
        _items[159] = "Super strength(2)";
        _items[161] = "Super strength(1)";
        _items[2436] = "Super attack(4)";
        _items[145] = "Super attack(3)";
        _items[147] = "Super attack(2)";
        _items[149] = "Super attack(1)";
        _items[2442] = "Super defence(4)";
        _items[163] = "Super defence(3)";
        _items[165] = "Super defence(2)";
        _items[167] = "Super defence(1)";
        _items[2444] = "Ranging potion(4)";
        _items[169] = "Ranging potion(3)";
        _items[171] = "Ranging potion(2)";
        _items[173] = "Ranging potion(1)";
        _items[3040] = "Magic potion(4)";
        _items[3042] = "Magic potion(3)";
        _items[3044] = "Magic potion(2)";
        _items[3046] = "Magic potion(1)";
        _items[12695] = "Super combat potion(4)";
        _items[12697] = "Super combat potion(3)";
        _items[12699] = "Super combat potion(2)";
        _items[12701] = "Super combat potion(1)";
        _items[23685] = "Divine super combat potion(4)";
        _items[23688] = "Divine super combat potion(3)";
        _items[23691] = "Divine super combat potion(2)";
        _items[23694] = "Divine super combat potion(1)";
        _items[4151] = "Abyssal whip";
        _items[12006] = "Abyssal tentacle";
        _items[1305] = "Dragon longsword";
        _items[4587] = "Dragon scimitar";
        _items[1377] = "Dragon battleaxe";
        _items[1215] = "Dragon dagger";
        _items[5698] = "Dragon dagger(p++)";
        _items[11802] = "Armadyl godsword";
        _items[11804] = "Bandos godsword";
        _items[11806] = "Saradomin godsword";
        _items[11808] = "Zamorak godsword";
        _items[11832] = "Bandos chestplate";
        _items[11834] = "Bandos tassets";
        _items[11836] = "Bandos boots";
        _items[11826] = "Armadyl helmet";
        _items[11828] = "Armadyl chestplate";
        _items[11830] = "Armadyl chainskirt";
        _items[11840] = "Dragon boots";
        _items[21736] = "Primordial boots";
        _items[21742] = "Pegasian boots";
        _items[21748] = "Eternal boots";
        _items[6585] = "Amulet of fury";
        _items[19553] = "Amulet of torture";
        _items[19547] = "Necklace of anguish";
        _items[19544] = "Tormented bracelet";
        _items[19550] = "Ring of suffering";
        _items[1704] = "Amulet of glory";
        _items[1712] = "Amulet of glory(4)";
        _items[11978] = "Amulet of glory(6)";
        _items[1725] = "Amulet of strength";
        _items[1727] = "Amulet of magic";
        _items[1731] = "Amulet of power";
        _items[6737] = "Berserker ring";
        _items[11773] = "Berserker ring (i)";
        _items[6731] = "Seers ring";
        _items[11770] = "Seers ring (i)";
        _items[6733] = "Archers ring";
        _items[11771] = "Archers ring (i)";
        _items[6735] = "Warrior ring";
        _items[11772] = "Warrior ring (i)";
        _items[22975] = "Brimstone ring";
        _items[7462] = "Barrows gloves";
        _items[7461] = "Dragon gloves";
        _items[7460] = "Rune gloves";
        _items[10551] = "Fighter torso";
        _items[1127] = "Rune platebody";
        _items[1079] = "Rune platelegs";
        _items[1093] = "Rune plateskirt";
        _items[1163] = "Rune full helm";
        _items[1201] = "Rune kiteshield";
        _items[3140] = "Dragon chainbody";
        _items[4087] = "Dragon platelegs";
        _items[4585] = "Dragon plateskirt";
        _items[1149] = "Dragon med helm";
        _items[11838] = "Dragon defender";
        _items[12954] = "Dragon defender (t)";
        _items[8850] = "Rune defender";
        _items[12926] = "Toxic blowpipe";
        _items[12924] = "Toxic blowpipe (empty)";
        _items[12934] = "Zulrah's scales";
        _items[11283] = "Dragonfire shield";
        _items[10499] = "Ava's accumulator";
        _items[22109] = "Ava's assembler";
        _items[25865] = "Bow of faerdhinen (c)";
        _items[25867] = "Bow of faerdhinen";
        _items[20997] = "Twisted bow";
        _items[22325] = "Scythe of vitur";
        _items[27275] = "Tumeken's shadow";
        _items[4716] = "Dharok's helm";
        _items[4718] = "Dharok's greataxe";
        _items[4720] = "Dharok's platebody";
        _items[4722] = "Dharok's platelegs";
        _items[4708] = "Ahrim's hood";
        _items[4710] = "Ahrim's staff";
        _items[4712] = "Ahrim's robetop";
        _items[4714] = "Ahrim's robeskirt";
        _items[4724] = "Guthan's helm";
        _items[4726] = "Guthan's warspear";
        _items[4728] = "Guthan's platebody";
        _items[4730] = "Guthan's chainskirt";
        _items[4732] = "Karil's coif";
        _items[4734] = "Karil's crossbow";
        _items[4736] = "Karil's leathertop";
        _items[4738] = "Karil's leatherskirt";
        _items[4745] = "Torag's helm";
        _items[4747] = "Torag's hammers";
        _items[4749] = "Torag's platebody";
        _items[4751] = "Torag's platelegs";
        _items[4753] = "Verac's helm";
        _items[4755] = "Verac's flail";
        _items[4757] = "Verac's brassard";
        _items[4759] = "Verac's plateskirt";
        _items[11864] = "Slayer helmet";
        _items[11865] = "Slayer helmet (i)";
        _items[6570] = "Fire cape";
        _items[21295] = "Infernal cape";
        _items[13280] = "Max cape";
        _items[436] = "Copper ore";
        _items[438] = "Tin ore";
        _items[440] = "Iron ore";
        _items[442] = "Silver ore";
        _items[444] = "Gold ore";
        _items[447] = "Mithril ore";
        _items[449] = "Adamantite ore";
        _items[451] = "Runite ore";
        _items[453] = "Coal";
        _items[2349] = "Bronze bar";
        _items[2351] = "Iron bar";
        _items[2353] = "Steel bar";
        _items[2355] = "Silver bar";
        _items[2357] = "Gold bar";
        _items[2359] = "Mithril bar";
        _items[2361] = "Adamantite bar";
        _items[2363] = "Runite bar";
        _items[1511] = "Logs";
        _items[1521] = "Oak logs";
        _items[1519] = "Willow logs";
        _items[6333] = "Teak logs";
        _items[1517] = "Maple logs";
        _items[6332] = "Mahogany logs";
        _items[1515] = "Yew logs";
        _items[1513] = "Magic logs";
        _items[19669] = "Redwood logs";
        _items[526] = "Bones";
        _items[532] = "Big bones";
        _items[536] = "Dragon bones";
        _items[22124] = "Superior dragon bones";
        _items[199] = "Grimy guam leaf";
        _items[201] = "Grimy marrentill";
        _items[203] = "Grimy tarromin";
        _items[205] = "Grimy harralander";
        _items[207] = "Grimy ranarr weed";
        _items[209] = "Grimy irit leaf";
        _items[211] = "Grimy avantoe";
        _items[213] = "Grimy kwuarm";
        _items[215] = "Grimy cadantine";
        _items[217] = "Grimy dwarf weed";
        _items[219] = "Grimy torstol";
        _items[3049] = "Grimy toadflax";
        _items[3051] = "Grimy snapdragon";
        _items[8007] = "Varrock teleport";
        _items[8008] = "Lumbridge teleport";
        _items[8009] = "Falador teleport";
        _items[8010] = "Camelot teleport";
        _items[8011] = "Ardougne teleport";
        _items[8013] = "Teleport to house";
        _items[2412] = "Saradomin cape";
        _items[2413] = "Guthix cape";
        _items[2414] = "Zamorak cape";
        _items[21791] = "Imbued saradomin cape";
        _items[21793] = "Imbued guthix cape";
        _items[21795] = "Imbued zamorak cape";
        _items[11850] = "Graceful hood";
        _items[11852] = "Graceful cape";
        _items[11854] = "Graceful top";
        _items[11856] = "Graceful legs";
        _items[11858] = "Graceful gloves";
        _items[11860] = "Graceful boots";
        _items[8839] = "Void knight top";
        _items[8840] = "Void knight robe";
        _items[8842] = "Void knight gloves";
        _items[11663] = "Void mage helm";
        _items[11664] = "Void ranger helm";
        _items[11665] = "Void melee helm";
        _items[13072] = "Elite void top";
        _items[13073] = "Elite void robe";
        _items[12791] = "Rune pouch";
        _items[27281] = "Divine rune pouch";
        _items[12940] = "Toxic staff of the dead";
        _items[12904] = "Toxic staff (uncharged)";
        _items[12926] = "Toxic blowpipe";
        _items[12924] = "Toxic blowpipe (empty)";
        _items[12929] = "Serpentine helm (uncharged)";
        _items[12931] = "Serpentine helm";
        _items[13239] = "Primordial boots";
        _items[13237] = "Pegasian boots";
        _items[13235] = "Eternal boots";
        _items[22978] = "Brimstone ring";
        _items[19553] = "Amulet of torture";
        _items[19547] = "Necklace of anguish";
        _items[19544] = "Tormented bracelet";
        _items[19550] = "Ring of suffering";
        _items[20653] = "Amulet of the damned";
        _items[20655] = "Amulet of the damned (full)";
        _items[11770] = "Seers ring (i)";
        _items[11771] = "Archers ring (i)";
        _items[11772] = "Warrior ring (i)";
        _items[11773] = "Berserker ring (i)";
        _items[12695] = "Super combat potion(4)";
        _items[12697] = "Super combat potion(3)";
        _items[12699] = "Super combat potion(2)";
        _items[12701] = "Super combat potion(1)";
        _items[23685] = "Divine super combat potion(4)";
        _items[23688] = "Divine super combat potion(3)";
        _items[23691] = "Divine super combat potion(2)";
        _items[23694] = "Divine super combat potion(1)";
        _items[2452] = "Antifire potion(4)";
        _items[2454] = "Antifire potion(3)";
        _items[2456] = "Antifire potion(2)";
        _items[2458] = "Antifire potion(1)";
        _items[11951] = "Extended antifire(4)";
        _items[11953] = "Extended antifire(3)";
        _items[11955] = "Extended antifire(2)";
        _items[11957] = "Extended antifire(1)";
        _items[22209] = "Extended super antifire(4)";
        _items[22212] = "Extended super antifire(3)";
        _items[22215] = "Extended super antifire(2)";
        _items[22218] = "Extended super antifire(1)";
        _items[2446] = "Antipoison(4)";
        _items[175] = "Antipoison(3)";
        _items[177] = "Antipoison(2)";
        _items[179] = "Antipoison(1)";
        _items[2448] = "Superantipoison(4)";
        _items[181] = "Superantipoison(3)";
        _items[183] = "Superantipoison(2)";
        _items[185] = "Superantipoison(1)";
        _items[5952] = "Antidote+(4)";
        _items[5954] = "Antidote+(3)";
        _items[5956] = "Antidote+(2)";
        _items[5958] = "Antidote+(1)";
        _items[5943] = "Antidote++(4)";
        _items[5945] = "Antidote++(3)";
        _items[5947] = "Antidote++(2)";
        _items[5949] = "Antidote++(1)";
        _items[12913] = "Anti-venom(4)";
        _items[12915] = "Anti-venom(3)";
        _items[12917] = "Anti-venom(2)";
        _items[12919] = "Anti-venom(1)";
        _items[12905] = "Anti-venom+(4)";
        _items[12907] = "Anti-venom+(3)";
        _items[12909] = "Anti-venom+(2)";
        _items[12911] = "Anti-venom+(1)";
        _items[3024] = "Super restore(4)";
        _items[3026] = "Super restore(3)";
        _items[3028] = "Super restore(2)";
        _items[3030] = "Super restore(1)";
        _items[6685] = "Saradomin brew(4)";
        _items[6687] = "Saradomin brew(3)";
        _items[6689] = "Saradomin brew(2)";
        _items[6691] = "Saradomin brew(1)";
        _items[12625] = "Stamina potion(4)";
        _items[12627] = "Stamina potion(3)";
        _items[12629] = "Stamina potion(2)";
        _items[12631] = "Stamina potion(1)";
        _items[2436] = "Super attack(4)";
        _items[145] = "Super attack(3)";
        _items[147] = "Super attack(2)";
        _items[149] = "Super attack(1)";
        _items[2440] = "Super strength(4)";
        _items[157] = "Super strength(3)";
        _items[159] = "Super strength(2)";
        _items[161] = "Super strength(1)";
        _items[2442] = "Super defence(4)";
        _items[163] = "Super defence(3)";
        _items[165] = "Super defence(2)";
        _items[167] = "Super defence(1)";
        _items[2444] = "Ranging potion(4)";
        _items[169] = "Ranging potion(3)";
        _items[171] = "Ranging potion(2)";
        _items[173] = "Ranging potion(1)";
        _items[3040] = "Magic potion(4)";
        _items[3042] = "Magic potion(3)";
        _items[3044] = "Magic potion(2)";
        _items[3046] = "Magic potion(1)";
    }

    private static async Task InitializeOnlineMappingAsync()
    {
        try
        {
            string cacheDir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "osrsmr");
            Directory.CreateDirectory(cacheDir);
            string cacheFile = System.IO.Path.Combine(cacheDir, "items_mapping.json");

            if (File.Exists(cacheFile))
            {
                try
                {
                    string cachedJson = await File.ReadAllTextAsync(cacheFile);
                    LoadFromJson(cachedJson);
                }
                catch { }
            }

            _httpClient.DefaultRequestHeaders.UserAgent.Clear();
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("osrsmr - osrs item mapping helper");

            // 1. Fetch complete item database (all tradeable & untradeable items)
            try
            {
                string jsonDb = await _httpClient.GetStringAsync("https://raw.githubusercontent.com/osrsbox/osrsbox-db/master/docs/items-summary.json");
                if (!string.IsNullOrWhiteSpace(jsonDb))
                {
                    LoadFromJson(jsonDb);
                    try
                    {
                        await File.WriteAllTextAsync(cacheFile, jsonDb);
                    }
                    catch { }
                }
            }
            catch { }

            // 2. Fetch OSRS wiki mapping
            try
            {
                string jsonWiki = await _httpClient.GetStringAsync("https://prices.runescape.wiki/api/v1/osrs/mapping");
                if (!string.IsNullOrWhiteSpace(jsonWiki))
                {
                    LoadFromJson(jsonWiki);
                }
            }
            catch { }
        }
        catch { }
    }

    private static void LoadFromJson(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in doc.RootElement.EnumerateObject())
                {
                    if (int.TryParse(prop.Name, out int propId) && propId > 0)
                    {
                        if (prop.Value.ValueKind == JsonValueKind.Object)
                        {
                            if (prop.Value.TryGetProperty("name", out var nameProp))
                            {
                                string? name = nameProp.GetString();
                                if (!string.IsNullOrWhiteSpace(name))
                                {
                                    _items[propId] = name;
                                }
                            }
                        }
                        else if (prop.Value.ValueKind == JsonValueKind.String)
                        {
                            string? name = prop.Value.GetString();
                            if (!string.IsNullOrWhiteSpace(name))
                            {
                                _items[propId] = name;
                            }
                        }
                    }
                    else if (prop.Value.ValueKind == JsonValueKind.Object && prop.Value.TryGetProperty("id", out var idProp) && prop.Value.TryGetProperty("name", out var nameProp))
                    {
                        int id = idProp.GetInt32();
                        string? name = nameProp.GetString();
                        if (id > 0 && !string.IsNullOrWhiteSpace(name))
                        {
                            _items[id] = name;
                        }
                    }
                }
            }
            else if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var el in doc.RootElement.EnumerateArray())
                {
                    if (el.TryGetProperty("id", out var idProp) && el.TryGetProperty("name", out var nameProp))
                    {
                        int id = idProp.GetInt32();
                        string? name = nameProp.GetString();
                        if (id > 0 && !string.IsNullOrWhiteSpace(name))
                        {
                            _items[id] = name;
                        }
                    }
                }
            }
        }
        catch { }
    }
}
