using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.ComponentModel;
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
using System.Windows.Threading;
using OsrsMr.Core;
using OsrsMr.Core.Profiles;
using OsrsMr.Core.Data;
using OsrsMr.Api;
using OsrsMr.Api.Entities;
using OsrsMr.Api.Framework;
using OsrsMr.Api.CustomScripts;
using OsrsMr.Scripts;
using OsrsMr;
using ScriptStatus = OsrsMr.Api.Framework.ScriptStatus;

namespace osrsmr;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<DataItem> _dataItems = new();
    private readonly Dictionary<string, DataItem> _dataItemsIndex = new(StringComparer.OrdinalIgnoreCase);
    private readonly ObservableCollection<DataItem> _skills = new();
    private readonly Dictionary<string, DataItem> _skillsIndex = new(StringComparer.OrdinalIgnoreCase);
    private readonly ObservableCollection<SkillProgressItem> _displayedSkills = new();
    private string _currentSkillFilter = "All";
    private readonly ObservableCollection<NpcItem> _npcs = new();
    private readonly ObservableCollection<PlayerItem> _players = new();
    private readonly ObservableCollection<PrayerViewModel> _prayers = new();
    private readonly ObservableCollection<TreeItem> _trees = new();
    private readonly ObservableCollection<SceneObjectItem> _banks = new();
    private readonly ObservableCollection<SceneObjectItem> _shops = new();
    private readonly ObservableCollection<SceneObjectItem> _altars = new();
    private readonly ObservableCollection<SceneObjectItem> _rocks = new();
    private readonly ObservableCollection<GroundItem> _groundItems = new();
    private readonly ObservableCollection<ContainerItem> _bankItems = new();
    private readonly ObservableCollection<ContainerItem> _shopItems = new();
    private readonly ObservableCollection<GrandExchangeOfferUiItem> _geOffers = new();
    private readonly ObservableCollection<RunePouchSlotUiItem> _runePouchSlots = new();
    private readonly ObservableCollection<ContainerItem> _lootingBagItems = new();
    private readonly ObservableCollection<ShortcutItem> _shortcuts = new();
    private readonly ObservableCollection<AgilityObstacleItem> _agilityObstacles = new();
    private readonly ObservableCollection<FishingSpotItem> _fishingSpots = new();
    private readonly ObservableCollection<CustomActionStep> _creatorSteps = new();
    private readonly ObservableCollection<NpcItem> _creatorNearbyNpcs = new();
    private readonly ObservableCollection<MonsterLootItem> _creatorLootTable = new();
    private readonly ObservableCollection<string> _creatorActiveLootList = new();
    private readonly List<CustomScriptDefinition> _savedCustomScripts = new();
    private readonly Dictionary<string, PrayerViewModel> _prayerMap = new(StringComparer.OrdinalIgnoreCase);
    private readonly Border[] _inventorySlots = new Border[28];
    private readonly string[] _lastInventoryRaw = new string[28];
    private class InventorySlotHolder
    {
        public Border Border { get; set; } = null!;
        public TextBlock NameText { get; set; } = null!;
        public TextBlock QtyText { get; set; } = null!;
        public TextBlock SlotNumText { get; set; } = null!;
        public string LastRaw { get; set; } = "";
    }
    private readonly InventorySlotHolder[] _inventorySlotHolders = new InventorySlotHolder[28];
    private readonly Dictionary<string, string> _lastEquipmentRaw = new(StringComparer.OrdinalIgnoreCase);
    private static readonly SolidColorBrush SlotEmptyBg = new SolidColorBrush(Color.FromArgb(50, 30, 34, 42));
    private static readonly SolidColorBrush SlotEmptyBorder = new SolidColorBrush(Color.FromRgb(50, 55, 65));
    private static readonly SolidColorBrush SlotOccupiedBg = new SolidColorBrush(Color.FromRgb(24, 48, 76));
    private static readonly SolidColorBrush SlotOccupiedBorder = new SolidColorBrush(Color.FromRgb(0, 180, 216));
    private readonly Dictionary<string, Border> _equipmentSlots = new();

    private void SetDataItem(string key, string value)
    {
        if (_dataItemsIndex.TryGetValue(key, out var existing))
        {
            if (existing.Value != value)
                existing.Value = value;
        }
        else
        {
            var item = new DataItem { Key = key, Value = value };
            _dataItemsIndex[key] = item;
            _dataItems.Add(item);
        }
    }
    private readonly DispatcherTimer _botTimer = new();
    private TcpListener? _listener;
    private bool _running = true;
    private volatile bool _isAgentConnected = false;
    private volatile bool _isTcpConnected = false;
    private TcpClient? _activeTcpClient = null;
    private int _activeSessionId = 0;
    private string? _lastAttachedPid = null;
    private readonly Dictionary<int, DateTime> _failedPidCooldown = new();
    private static readonly object _logLock = new();
    private string? _cachedJavaPath = null;
    private System.Diagnostics.Process? _trackedRuneLiteProcess = null;
    private readonly object _processTrackLock = new();

    private string _currentLocationName = "Unknown";
    private int _currentX = 0;
    private int _currentY = 0;
    private int _currentPlane = 0;
    private int _currentRegionId = 0;

    private void UpdatePlayerLocationDisplay()
    {
        if (_currentX > 0 && _currentY > 0)
        {
            if (string.IsNullOrEmpty(_currentLocationName) || _currentLocationName == "Unknown" || _currentLocationName.StartsWith("Region #") || _currentLocationName == "Gielinor")
            {
                _currentLocationName = OsrsMr.Core.Spatial.WorldLocations.ResolveAreaName(_currentX, _currentY, _currentPlane, _currentRegionId);
            }
            PlayerLocationText.Text = $"{_currentLocationName} ({_currentX}, {_currentY})";
        }
        else if (!string.IsNullOrEmpty(_currentLocationName) && _currentLocationName != "Unknown")
        {
            PlayerLocationText.Text = _currentLocationName;
        }
    }

    [DllImport("kernel32.dll")]
    public static extern IntPtr OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId);

    [DllImport("kernel32.dll")]
    public static extern bool ReadProcessMemory(int hProcess, long lpBaseAddress, byte[] lpBuffer, int dwSize, ref int lpNumberOfBytesRead);

    [DllImport("kernel32.dll")]
    public static extern bool CloseHandle(IntPtr hObject);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint Msg, UIntPtr wParam, string lParam, uint fuFlags, uint uTimeout, out UIntPtr lpdwResult);

    private static readonly IntPtr HWND_BROADCAST = new IntPtr(0xffff);
    private const uint WM_SETTINGCHANGE = 0x001A;
    private const uint SMTO_ABORTIFHUNG = 0x0002;

    private const int PROCESS_WM_READ = 0x0010;

    public MainWindow()
    {
        try 
        {
            InitializeComponent();
            DataList.ItemsSource = _dataItems;
            SkillsControl.ItemsSource = _displayedSkills;
            RefreshSkillsDisplay();
            NpcList.ItemsSource = _npcs;
            PlayerList.ItemsSource = _players;
            TreesList.ItemsSource = _trees;
            BanksList.ItemsSource = _banks;
            ShopsList.ItemsSource = _shops;
            AltarsList.ItemsSource = _altars;
            RocksList.ItemsSource = _rocks;
            GroundItemsList.ItemsSource = _groundItems;
            BankContainerList.ItemsSource = _bankItems;
            ShopContainerList.ItemsSource = _shopItems;
            GeOffersControl.ItemsSource = _geOffers;
            for (int i = 0; i < 8; i++)
            {
                _geOffers.Add(new GrandExchangeOfferUiItem { Slot = i, State = "Empty" });
            }

            RunePouchControl.ItemsSource = _runePouchSlots;
            for (int i = 0; i < 4; i++)
            {
                _runePouchSlots.Add(new RunePouchSlotUiItem { Slot = i, RuneName = "None", Quantity = 0 });
            }

            LootingBagContainerList.ItemsSource = _lootingBagItems;
            ShortcutsList.ItemsSource = _shortcuts;
            WorldShortcutsList.ItemsSource = _shortcuts;
            AgilityObstaclesList.ItemsSource = _agilityObstacles;
            WorldAgilityObstaclesList.ItemsSource = _agilityObstacles;
            FishingSpotsList.ItemsSource = _fishingSpots;
            InitializePrayers();
            InitializeInventoryGrid();
            InitializeEquipmentMapping();
            InitializeBotController();
            InitializeScriptCreator();

            SyncAgentJar();
            StartServer();
            StartAutoAttachLoop();
            Task.Run(() => FixRuneLiteShortcut());
            Task.Run(() => CheckAndFixRuneLiteConfig(silent: true));
            
            // Log environment info
            _dataItems.Add(new DataItem { Key = "OS", Value = RuntimeInformation.OSDescription });
            _dataItems.Add(new DataItem { Key = "Bridge Version", Value = "2.0.0 (RuneLite JVM Bridge)" });
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

        foreach (var kvp in _equipmentSlots)
        {
            ResetEquipmentSlotUi(kvp.Key, kvp.Value);
        }
    }

    private void InitializePrayers()
    {
        string[] standardPrayers = {
            "Thick Skin", "Burst of Strength", "Clarity of Thought", "Sharp Eye", "Mystic Will",
            "Rock Skin", "Superhuman Strength", "Improved Reflexes", "Rapid Restore", "Rapid Heal",
            "Protect Item", "Hawk Eye", "Mystic Lore", "Steel Skin", "Ultimate Strength",
            "Incredible Reflexes", "Protect from Magic", "Protect from Missiles", "Protect from Melee",
            "Eagle Eye", "Mystic Might", "Retribution", "Redemption", "Smite",
            "Preserve", "Chivalry", "Piety", "Rigour", "Augury"
        };

        foreach (var p in standardPrayers)
        {
            var vm = new PrayerViewModel { Name = p, IsActive = false };
            _prayers.Add(vm);
            _prayerMap[p] = vm;
        }

        PrayersControl.ItemsSource = _prayers;
    }

    private void InitializeInventoryGrid()
    {
        InventoryGrid.Children.Clear();
        for (int i = 0; i < 28; i++)
        {
            var border = new Border
            {
                BorderBrush = new SolidColorBrush(Color.FromRgb(50, 55, 65)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Background = new SolidColorBrush(Color.FromArgb(50, 30, 34, 42)),
                Margin = new Thickness(2),
                Width = 44,
                Height = 44,
                ToolTip = $"Slot {i + 1}: Empty"
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
                client.NoDelay = true;
                client.ReceiveBufferSize = 65536;
                _ = Task.Run(() => HandleClient(client));
            }
        }
        catch (Exception ex)
        {
            UpdateStatus($"Server Error: {ex.Message}", System.Windows.Media.Brushes.Red);
        }
    }

    private async Task HandleClient(TcpClient client)
    {
        int sessionId = Interlocked.Increment(ref _activeSessionId);
        try
        {
            _activeTcpClient?.Close();
        }
        catch { }
        _activeTcpClient = client;
        _isTcpConnected = true;
        _isAgentConnected = true;

        _ = Dispatcher.BeginInvoke(() => UpdateStatus("Agent Connected & Linked!", Brushes.Lime));
        LogMessage($"[BRIDGE] Agent client #{sessionId} connected to TCP port 43594.");

        // Track candidate RuneLite process if not already tracked
        try
        {
            if (_trackedRuneLiteProcess == null || _trackedRuneLiteProcess.HasExited)
            {
                var candidates = FindRuneLiteCandidateProcesses();
                if (candidates.Count > 0)
                {
                    TrackRuneLiteProcess(candidates[0].Id);
                }
            }
        }
        catch { }
        
        // Update diagnostic info
        _ = Dispatcher.BeginInvoke(() => {
            SetDataItem("Agent Link", $"Connected (#{sessionId}) at {DateTime.Now.ToLongTimeString()}");
            SetDataItem("Client Mode", "RuneLite Java Agent");
        });

        try
        {
            using (client)
            using (var stream = client.GetStream())
            using (var reader = new System.IO.StreamReader(stream, System.Text.Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 65536))
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
                _isTcpConnected = false;
                _isAgentConnected = false;
                LogMessage($"[BRIDGE] Agent client #{sessionId} disconnected.");
                _ = Dispatcher.BeginInvoke(() => UpdateStatus("Agent Disconnected - Waiting for Client...", Brushes.Yellow));
            }
        }
    }

    private readonly System.Collections.Concurrent.ConcurrentQueue<string> _incomingLines = new();
    private int _isDispatcherScheduled = 0;

    private void ProcessLine(string line)
    {
        try { BrainEngine.Instance.ProcessLine(line); } catch { }

        _incomingLines.Enqueue(line);
        if (_incomingLines.Count > 3000)
        {
            while (_incomingLines.Count > 1500 && _incomingLines.TryDequeue(out _)) { }
        }

        if (System.Threading.Interlocked.CompareExchange(ref _isDispatcherScheduled, 1, 0) == 0)
        {
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, (Action)DrainIncomingLines);
        }
    }

    private void DrainIncomingLines()
    {
        System.Threading.Interlocked.Exchange(ref _isDispatcherScheduled, 0);
        int count = 0;
        while (count < 250 && _incomingLines.TryDequeue(out var line))
        {
            ProcessLineOnUi(line);
            count++;
        }
        if (!_incomingLines.IsEmpty && System.Threading.Interlocked.CompareExchange(ref _isDispatcherScheduled, 1, 0) == 0)
        {
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, (Action)DrainIncomingLines);
        }
    }

    private void ProcessLineOnUi(string line)
    {
        try
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
                else if (key.StartsWith("SKILL_XP["))
                {
                    UpdateSkillXp(key, value);
                }
                else if (key == "TOTAL_LEVEL")
                {
                    if (int.TryParse(value, out int totLvl))
                    {
                        SkillTrackerEngine.Instance.TotalLevel = totLvl;
                    }
                }
                else if (key == "TOTAL_XP")
                {
                    if (long.TryParse(value, out long totXp))
                    {
                        SkillTrackerEngine.Instance.TotalXp = totXp;
                    }
                }
                else if (key == "PID")
                {
                    SetDataItem("Client PID", value);
                }
                else if (key == "Status" || key == "Status:")
                {
                    SetDataItem("Agent Status", value);
                }
                else if (key == "Client Class" || key == "Client Class:")
                {
                    SetDataItem(key, value);
                }
                else if (key == "Searching for Game Client...")
                {
                    SetDataItem("Discovery", "Scanning...");
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
                    
                    SetDataItem(key, value);
                }
                else if (key == "PLAYER_NAME")
                {
                    PlayerNameText.Text = value;
                }
                else if (key == "HITPOINTS" || key == "HP" || key == "PLAYER_HP" || key == "HEALTH")
                {
                    UpdatePlayerHitpoints(value);
                }
                else if (key == "PRAYER" || key == "PLAYER_PRAYER")
                {
                    UpdatePlayerPrayer(value);
                }
                else if (key == "RUN_ENERGY" || key == "ENERGY" || key == "PLAYER_ENERGY")
                {
                    UpdatePlayerRunEnergy(value);
                }
                else if (key == "WEIGHT" || key == "PLAYER_WEIGHT")
                {
                    UpdatePlayerWeight(value);
                }
                else if (key == "IN_COMBAT" || key == "IS_IN_COMBAT" || key == "PLAYER_IN_COMBAT")
                {
                    UpdateCombatStatus(value);
                }
                else if (key == "COMBAT_TARGET" || key == "TARGET_NAME" || key == "TARGET")
                {
                    UpdateCombatTarget(value);
                }
                else if (key == "COMBAT_TARGET_HP" || key == "TARGET_HP")
                {
                    UpdateCombatTargetHealth(value);
                }
                else if (key == "ANIMATION" || key == "PLAYER_ANIMATION")
                {
                    UpdatePlayerAnimation(value);
                }
                else if (key == "SLAYER_MASTER")
                {
                    if (SlayerMasterText != null) SlayerMasterText.Text = value;
                }
                else if (key == "SLAYER_REMAINING")
                {
                    if (SlayerRemainingText != null) SlayerRemainingText.Text = value;
                }
                else if (key == "LOCATION" || key == "LOCATION_NAME" || key == "TOWN")
                {
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        _currentLocationName = value;
                        UpdatePlayerLocationDisplay();
                    }
                }
                else if (key == "REGION_ID")
                {
                    if (int.TryParse(value, out int rId))
                    {
                        _currentRegionId = rId;
                        UpdatePlayerLocationDisplay();
                    }
                }
                else if (key == "PLAYER_PLANE" || key == "PLANE")
                {
                    if (int.TryParse(value, out int plane))
                    {
                        _currentPlane = plane;
                        UpdatePlayerLocationDisplay();
                    }
                }
                else if (key == "PLAYER_X" || key == "PLAYER_Y")
                {
                    SetDataItem(key, value);
                        
                    if (key == "PLAYER_X" && int.TryParse(value, out int px)) _currentX = px;
                    if (key == "PLAYER_Y" && int.TryParse(value, out int py)) _currentY = py;

                    UpdatePlayerLocationDisplay();
                }
                else if (key == "WORLD_LOCATION")
                {
                    var locParts = value.Split(',');
                    if (locParts.Length >= 2 && int.TryParse(locParts[0].Trim(), out int wx) && int.TryParse(locParts[1].Trim(), out int wy))
                    {
                        _currentX = wx;
                        _currentY = wy;
                        if (locParts.Length >= 3 && int.TryParse(locParts[2].Trim(), out int wp))
                        {
                            _currentPlane = wp;
                        }
                        UpdatePlayerLocationDisplay();
                    }
                }
                else if (key == "CURRENT_TAB")
                {
                    UpdateCurrentTab(value);
                }
                else if (key.StartsWith("PRAYER["))
                {
                    UpdatePrayerStatus(key, value);
                }
                else if (key == "SPELLBOOK")
                {
                    UpdateSpellbook(value);
                }
                else if (key == "SPELLBOOK_ID")
                {
                    if (string.IsNullOrEmpty(SpellbookText.Text) || SpellbookText.Text == "Standard")
                    {
                        UpdateSpellbook(value);
                    }
                }
                else if (key == "AUTOCAST_SPELL")
                {
                    AutocastSpellText.Text = value;
                    if (value != "None" && !string.IsNullOrWhiteSpace(value))
                    {
                        AutocastBadge.Background = new SolidColorBrush(Color.FromRgb(30, 45, 65));
                        AutocastSpellText.Foreground = new SolidColorBrush(Color.FromRgb(130, 170, 255));
                    }
                    else
                    {
                        AutocastBadge.Background = new SolidColorBrush(Color.FromRgb(42, 42, 58));
                        AutocastSpellText.Foreground = Brushes.Gray;
                    }
                }
                else if (key == "SELECTED_SPELL")
                {
                    SelectedSpellText.Text = value;
                }
                else if (key == "QUICK_PRAYER")
                {
                    UpdateQuickPrayer(value);
                }
                else if (key == "ACTIVE_PRAYERS")
                {
                    ActivePrayersListText.Text = value;
                    if (value.Equals("None", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(value))
                    {
                        foreach (var p in _prayers) p.IsActive = false;
                    }
                    else
                    {
                        var activeSet = new HashSet<string>(value.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim().Replace(" ", "").Replace("_", "").ToLowerInvariant()), StringComparer.OrdinalIgnoreCase);
                        foreach (var p in _prayers)
                        {
                            string norm = p.Name.Replace(" ", "").Replace("_", "").ToLowerInvariant();
                            p.IsActive = activeSet.Contains(norm);
                        }
                    }
                }
                else if (key == "ACTIVE_PRAYER_COUNT")
                {
                    ActivePrayerCountText.Text = $"{value} Active";
                }
                else if (key == "LOCATION_STATUS")
                {
                    SetDataItem(key, value);
                }
                else if (key.StartsWith("TREE["))
                {
                    UpdateTreeList(key, value);
                }
                else if (key == "TOTAL_TREES")
                {
                    if (int.TryParse(value, out int totalTrees))
                    {
                        while (_trees.Count > totalTrees)
                            _trees.RemoveAt(_trees.Count - 1);
                    }
                }
                else if (key.StartsWith("BANK_OBJ["))
                {
                    UpdateBankObjList(key, value);
                }
                else if (key == "TOTAL_BANKS")
                {
                    if (int.TryParse(value, out int totalBanks))
                    {
                        while (_banks.Count > totalBanks)
                            _banks.RemoveAt(_banks.Count - 1);
                    }
                }
                else if (key.StartsWith("SHOP_OBJ["))
                {
                    UpdateShopObjList(key, value);
                }
                else if (key == "TOTAL_SHOPS")
                {
                    if (int.TryParse(value, out int totalShops))
                    {
                        while (_shops.Count > totalShops)
                            _shops.RemoveAt(_shops.Count - 1);
                    }
                }
                else if (key.StartsWith("ALTAR_OBJ["))
                {
                    UpdateAltarObjList(key, value);
                }
                else if (key == "TOTAL_ALTARS")
                {
                    if (int.TryParse(value, out int totalAltars))
                    {
                        while (_altars.Count > totalAltars)
                            _altars.RemoveAt(_altars.Count - 1);
                    }
                }
                else if (key.StartsWith("ROCK_OBJ["))
                {
                    UpdateRockObjList(key, value);
                }
                else if (key == "TOTAL_ROCKS")
                {
                    if (int.TryParse(value, out int totalRocks))
                    {
                        while (_rocks.Count > totalRocks)
                            _rocks.RemoveAt(_rocks.Count - 1);
                    }
                }
                else if (key.StartsWith("GROUND_ITEM["))
                {
                    UpdateGroundItemList(key, value);
                }
                else if (key == "TOTAL_GROUND_ITEMS")
                {
                    if (int.TryParse(value, out int totalGroundItems))
                    {
                        while (_groundItems.Count > totalGroundItems)
                            _groundItems.RemoveAt(_groundItems.Count - 1);
                    }
                }
                else if (key.StartsWith("BANK_ITEM["))
                {
                    UpdateBankContainerItem(key, value);
                }
                else if (key == "BANK_OPEN")
                {
                    UpdateBankStatus(value);
                }
                else if (key == "BANK_TOTAL_ITEMS")
                {
                    if (BankItemCountText != null) BankItemCountText.Text = $"{value} Items Loaded";
                    if (int.TryParse(value, out int totalBankItems))
                    {
                        while (_bankItems.Count > totalBankItems)
                            _bankItems.RemoveAt(_bankItems.Count - 1);
                    }
                }
                else if (key.StartsWith("SHOP_ITEM["))
                {
                    UpdateShopContainerItem(key, value);
                }
                else if (key == "SHOP_OPEN")
                {
                    UpdateShopStatus(value);
                }
                else if (key == "SHOP_NAME")
                {
                    if (ShopTitleText != null) ShopTitleText.Text = value;
                }
                else if (key.StartsWith("SHOP_TOTAL_ITEMS"))
                {
                    if (ShopItemCountText != null) ShopItemCountText.Text = $"{value} Items In Stock";
                    if (int.TryParse(value, out int totalShopItems))
                    {
                        while (_shopItems.Count > totalShopItems)
                            _shopItems.RemoveAt(_shopItems.Count - 1);
                    }
                }
                else if (key.StartsWith("GE_SLOT["))
                {
                    UpdateGrandExchangeOffer(key, value);
                }
                else if (key.StartsWith("RUNE_POUCH["))
                {
                    UpdateRunePouchSlot(key, value);
                }
                else if (key.StartsWith("LOOTING_BAG["))
                {
                    UpdateLootingBagItem(key, value);
                }
                else if (key == "GEM_BAG")
                {
                    UpdateGemBagUi(value);
                }
                else if (key == "ESSENCE_POUCHES")
                {
                    UpdateEssencePouchesUi(value);
                }
                else if (key == "SPECIAL_ATTACK_PERCENT" || key == "SPECIAL_ATTACK_ENERGY")
                {
                    UpdateSpecialAttack(value);
                }
                else if (key == "SPECIAL_ATTACK_ACTIVE")
                {
                    UpdateSpecialAttackActive(value);
                }
                else if (key.StartsWith("BUFF_") || key.StartsWith("POISON_") || key.StartsWith("STATUS_") || key == "AUTO_RETALIATE" || key == "RUN_MODE")
                {
                    UpdateBuffsAndStatusUi();
                }
                else if (key == "SLAYER_TASK")
                {
                    UpdateSlayerTask(value);
                }
                else if (key == "SLAYER_COUNT")
                {
                    UpdateSlayerCount(value);
                }
                else if (key == "SLAYER_POINTS")
                {
                    if (SlayerPointsText != null) SlayerPointsText.Text = value;
                }
                else if (key == "SLAYER_STREAK")
                {
                    if (SlayerStreakText != null) SlayerStreakText.Text = $"{value} Tasks Completed";
                }
                else if (key == "SLAYER_MASTER_NEARBY")
                {
                    if (SlayerMasterNearbyText != null) SlayerMasterNearbyText.Text = value;
                }
                else if (key == "DIALOG_ACTIVE")
                {
                    UpdateDialogActive(value);
                }
                else if (key == "DIALOG_TYPE")
                {
                    UpdateDialogType(value);
                }
                else if (key == "DIALOG_TITLE")
                {
                    UpdateDialogTitle(value);
                }
                else if (key == "DIALOG_TEXT")
                {
                    UpdateDialogText(value);
                }
                else if (key == "DIALOG_OPTIONS")
                {
                    UpdateDialogOptions(value);
                }
                else if (key.StartsWith("SHORTCUT["))
                {
                    UpdateShortcutList(key, value);
                }
                else if (key == "TOTAL_SHORTCUTS")
                {
                    if (int.TryParse(value, out int totalShortcuts))
                    {
                        while (_shortcuts.Count > totalShortcuts)
                            _shortcuts.RemoveAt(_shortcuts.Count - 1);
                    }
                }
                else if (key.StartsWith("AGILITY_OBSTACLE["))
                {
                    UpdateAgilityObstacleList(key, value);
                }
                else if (key == "TOTAL_AGILITY_OBSTACLES")
                {
                    if (int.TryParse(value, out int totalObstacles))
                    {
                        while (_agilityObstacles.Count > totalObstacles)
                            _agilityObstacles.RemoveAt(_agilityObstacles.Count - 1);
                    }
                }
                else if (key.StartsWith("FISHING_SPOT["))
                {
                    UpdateFishingSpotList(key, value);
                }
                else if (key == "TOTAL_FISHING_SPOTS")
                {
                    if (int.TryParse(value, out int totalFish))
                    {
                        while (_fishingSpots.Count > totalFish)
                            _fishingSpots.RemoveAt(_fishingSpots.Count - 1);
                        if (FishingSpotsSummaryText != null)
                            FishingSpotsSummaryText.Text = $"{totalFish} Fishing Spot{(totalFish == 1 ? "" : "s")} Detected";
                        if (PlayerFishingText != null)
                            PlayerFishingText.Text = totalFish > 0 ? $"{totalFish} Nearby" : "0 Nearby";
                    }
                }
                else if (key == "AGILITY_COURSE")
                {
                    UpdateAgilityCourse(value);
                }
                else if (key == "AGILITY_COURSE_LEVEL")
                {
                    UpdateAgilityCourseLevel(value);
                }
                else if (key == "MARKS_OF_GRACE_COUNT")
                {
                    UpdateMarksOfGrace(value);
                }
                else if (key == "MINIGAME_ACTIVE")
                {
                    UpdateMinigameActive(value);
                }
                else if (key == "MINIGAME_NAME")
                {
                    UpdateMinigameName(value);
                }
                else if (key == "MINIGAME_STATUS")
                {
                    if (MinigameStatusText != null) MinigameStatusText.Text = value;
                }
                else if (key == "MINIGAME_POINTS")
                {
                    if (MinigamePointsText != null) MinigamePointsText.Text = value;
                }
                else if (key == "MINIGAME_EXTRA")
                {
                    if (MinigameExtraText != null) MinigameExtraText.Text = value;
                }
                else
                {
                    SetDataItem(key, value);
                }
            }
        }
        catch { }
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

    private void UpdateSpellbook(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;

        string normalized = value.Trim();
        string displayName = normalized;
        SolidColorBrush bgBrush;
        SolidColorBrush fgBrush;

        switch (normalized.ToLowerInvariant())
        {
            case "standard":
            case "modern":
            case "0":
                displayName = "Standard";
                bgBrush = new SolidColorBrush(Color.FromRgb(30, 57, 42));
                fgBrush = new SolidColorBrush(Color.FromRgb(76, 175, 80));
                break;
            case "ancient magicks":
            case "ancient":
            case "ancients":
            case "1":
                displayName = "Ancient Magicks";
                bgBrush = new SolidColorBrush(Color.FromRgb(50, 30, 65));
                fgBrush = new SolidColorBrush(Color.FromRgb(186, 104, 200));
                break;
            case "lunar":
            case "lunar spellbook":
            case "2":
                displayName = "Lunar";
                bgBrush = new SolidColorBrush(Color.FromRgb(25, 45, 70));
                fgBrush = new SolidColorBrush(Color.FromRgb(100, 181, 246));
                break;
            case "arceuus":
            case "necromancy":
            case "3":
                displayName = "Arceuus";
                bgBrush = new SolidColorBrush(Color.FromRgb(65, 45, 25));
                fgBrush = new SolidColorBrush(Color.FromRgb(255, 183, 77));
                break;
            case "ancient (swap)":
            case "4":
                displayName = "Ancient (Swap)";
                bgBrush = new SolidColorBrush(Color.FromRgb(65, 25, 45));
                fgBrush = new SolidColorBrush(Color.FromRgb(255, 105, 180));
                break;
            case "lunar (swap)":
            case "5":
                displayName = "Lunar (Swap)";
                bgBrush = new SolidColorBrush(Color.FromRgb(25, 60, 65));
                fgBrush = new SolidColorBrush(Color.FromRgb(80, 227, 194));
                break;
            case "arceuus (swap)":
            case "6":
                displayName = "Arceuus (Swap)";
                bgBrush = new SolidColorBrush(Color.FromRgb(65, 50, 20));
                fgBrush = new SolidColorBrush(Color.FromRgb(255, 215, 0));
                break;
            default:
                bgBrush = new SolidColorBrush(Color.FromRgb(40, 40, 40));
                fgBrush = Brushes.White;
                break;
        }

        if (SpellbookText != null) SpellbookText.Text = displayName;
        if (SpellbookBadge != null)
        {
            SpellbookBadge.Background = bgBrush;
            if (SpellbookText != null) SpellbookText.Foreground = fgBrush;
        }

        if (PlayerSpellbookText != null) PlayerSpellbookText.Text = displayName;
        if (PlayerSpellbookBadge != null)
        {
            PlayerSpellbookBadge.Background = bgBrush;
            if (PlayerSpellbookText != null) PlayerSpellbookText.Foreground = fgBrush;
        }
    }

    private void UpdateQuickPrayer(string value)
    {
        bool isActive = (value == "1" || value.Equals("Active", StringComparison.OrdinalIgnoreCase));
        QuickPrayerText.Text = isActive ? "Active" : "Inactive";
        if (isActive)
        {
            QuickPrayerBadge.Background = new SolidColorBrush(Color.FromRgb(20, 60, 40));
            QuickPrayerText.Foreground = new SolidColorBrush(Color.FromRgb(0, 230, 118));
        }
        else
        {
            QuickPrayerBadge.Background = new SolidColorBrush(Color.FromRgb(42, 42, 42));
            QuickPrayerText.Foreground = Brushes.Gray;
        }
    }

    private void UpdatePrayerStatus(string key, string value)
    {
        try
        {
            int openBracket = key.IndexOf('[');
            int closeBracket = key.IndexOf(']');
            if (openBracket != -1 && closeBracket != -1)
            {
                string prayerName = key.Substring(openBracket + 1, closeBracket - openBracket - 1).Trim();
                bool isActive = value.Equals("1") || 
                               value.Equals("true", StringComparison.OrdinalIgnoreCase) || 
                               value.Equals("active", StringComparison.OrdinalIgnoreCase);

                if (_prayerMap.TryGetValue(prayerName, out var pvm))
                {
                    pvm.IsActive = isActive;
                }
                else
                {
                    string norm = prayerName.Replace(" ", "").Replace("_", "").ToLowerInvariant();
                    var match = _prayers.FirstOrDefault(p => p.Name.Replace(" ", "").Replace("_", "").Equals(norm, StringComparison.OrdinalIgnoreCase));
                    if (match != null)
                    {
                        match.IsActive = isActive;
                        _prayerMap[prayerName] = match;
                    }
                }
            }
        }
        catch { }
    }

    private void RefreshSkillsDisplay()
    {
        if (_displayedSkills == null) return;
        _displayedSkills.Clear();
        var tracker = SkillTrackerEngine.Instance;
        if (tracker?.Skills != null)
        {
            foreach (var skill in tracker.Skills)
            {
                if (skill == null) continue;
                if (_currentSkillFilter == "Active" && !skill.IsActive) continue;
                if (_currentSkillFilter != "All" && _currentSkillFilter != "Active" && !skill.Category.Equals(_currentSkillFilter, StringComparison.OrdinalIgnoreCase)) continue;
                _displayedSkills.Add(skill);
            }
        }

        if (tracker != null)
        {
            if (SkillsTotalLevelText != null) SkillsTotalLevelText.Text = tracker.TotalLevel.ToString();
            if (SkillsTotalXpText != null) SkillsTotalXpText.Text = $"{tracker.TotalXp:N0} XP";
            if (SkillsGainedText != null) SkillsGainedText.Text = tracker.TotalXpGainedFormatted;
            if (SkillsRateText != null) SkillsRateText.Text = tracker.TotalXpPerHourFormatted;
        }
    }

    private void SkillFilterBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SkillFilterBox?.SelectedItem is ComboBoxItem cbi && cbi.Tag is string tag)
        {
            _currentSkillFilter = tag;
            RefreshSkillsDisplay();
        }
    }

    private void ResetSkillTracker_Click(object sender, RoutedEventArgs e)
    {
        SkillTrackerEngine.Instance.ResetSession();
        RefreshSkillsDisplay();
    }

    private void UpdatePlayerHitpoints(string value)
    {
        try
        {
            int slash = value.IndexOf('/');
            int cur = 0, max = 0;
            if (slash != -1)
            {
                int.TryParse(value.Substring(0, slash).Trim(), out cur);
                int.TryParse(value.Substring(slash + 1).Trim(), out max);
            }
            else
            {
                int.TryParse(value.Trim(), out cur);
                max = cur;
            }

            if (max <= 0) max = Math.Max(cur, 99);
            if (PlayerHealthBar != null)
            {
                PlayerHealthBar.Maximum = max;
                PlayerHealthBar.Value = Math.Clamp(cur, 0, max);
            }
            if (PlayerHealthText != null)
            {
                int pct = max > 0 ? (cur * 100 / max) : 0;
                PlayerHealthText.Text = $"{cur} / {max} ({pct}%)";
            }
        }
        catch { }
    }

    private void UpdatePlayerPrayer(string value)
    {
        try
        {
            int slash = value.IndexOf('/');
            int cur = 0, max = 0;
            if (slash != -1)
            {
                int.TryParse(value.Substring(0, slash).Trim(), out cur);
                int.TryParse(value.Substring(slash + 1).Trim(), out max);
            }
            else
            {
                int.TryParse(value.Trim(), out cur);
                max = cur;
            }

            if (max <= 0) max = Math.Max(cur, 99);
            if (PlayerPrayerBar != null)
            {
                PlayerPrayerBar.Maximum = max;
                PlayerPrayerBar.Value = Math.Clamp(cur, 0, max);
            }
            if (PlayerPrayerText != null)
            {
                int pct = max > 0 ? (cur * 100 / max) : 0;
                PlayerPrayerText.Text = $"{cur} / {max} ({pct}%)";
            }
        }
        catch { }
    }

    private void UpdatePlayerRunEnergy(string value)
    {
        try
        {
            string clean = value.Replace("%", "").Trim();
            if (double.TryParse(clean, out double energy))
            {
                if (PlayerEnergyBar != null) PlayerEnergyBar.Value = Math.Clamp(energy, 0, 100);
                if (PlayerEnergyText != null) PlayerEnergyText.Text = $"{energy:0}%";
            }
        }
        catch { }
    }

    private void UpdatePlayerWeight(string value)
    {
        try
        {
            if (PlayerWeightText != null)
            {
                string clean = value.Trim();
                if (!clean.EndsWith("kg", StringComparison.OrdinalIgnoreCase)) clean += " kg";
                PlayerWeightText.Text = clean;
            }
        }
        catch { }
    }

    private void UpdateCombatStatus(string value)
    {
        try
        {
            bool inCombat = value.Equals("True", StringComparison.OrdinalIgnoreCase) || 
                            value.Equals("1") || 
                            value.Equals("Yes", StringComparison.OrdinalIgnoreCase);

            if (PlayerCombatBadge != null)
            {
                PlayerCombatBadge.Background = inCombat 
                    ? new SolidColorBrush(Color.FromRgb(80, 20, 20)) 
                    : new SolidColorBrush(Color.FromRgb(37, 37, 53));
            }
            if (PlayerCombatText != null)
            {
                PlayerCombatText.Text = inCombat ? "IN COMBAT" : "Out of Combat";
                PlayerCombatText.Foreground = inCombat 
                    ? new SolidColorBrush(Color.FromRgb(255, 100, 100)) 
                    : new SolidColorBrush(Color.FromRgb(160, 160, 176));
            }
        }
        catch { }
    }

    private void UpdateCombatTarget(string value)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(value) || value.Equals("None", StringComparison.OrdinalIgnoreCase) || value.Equals("null", StringComparison.OrdinalIgnoreCase))
            {
                if (PlayerTargetText != null)
                {
                    PlayerTargetText.Text = "None";
                    PlayerTargetText.Foreground = new SolidColorBrush(Color.FromRgb(0, 229, 255));
                }
                if (CombatTargetHealthBar != null) CombatTargetHealthBar.Value = 0;
                if (CombatTargetHealthText != null) CombatTargetHealthText.Text = "No Target";
            }
            else
            {
                if (PlayerTargetText != null)
                {
                    PlayerTargetText.Text = value.Trim();
                    PlayerTargetText.Foreground = new SolidColorBrush(Color.FromRgb(255, 80, 80));
                }
            }
        }
        catch { }
    }

    private void UpdateCombatTargetHealth(string value)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(value) || value.Equals("None", StringComparison.OrdinalIgnoreCase) || value == "-1")
            {
                if (CombatTargetHealthBar != null) CombatTargetHealthBar.Value = 0;
                if (CombatTargetHealthText != null) CombatTargetHealthText.Text = "No Target";
                return;
            }

            string clean = value.Replace("%", "").Trim();
            if (double.TryParse(clean, out double pct))
            {
                if (CombatTargetHealthBar != null) CombatTargetHealthBar.Value = Math.Clamp(pct, 0, 100);
                if (CombatTargetHealthText != null) CombatTargetHealthText.Text = $"{pct:0}%";
            }
            else
            {
                if (CombatTargetHealthText != null) CombatTargetHealthText.Text = value;
            }
        }
        catch { }
    }

    private void UpdatePlayerAnimation(string value)
    {
        try
        {
            if (PlayerAnimationText != null)
            {
                if (value == "-1" || value == "Idle (-1)" || string.IsNullOrWhiteSpace(value))
                {
                    PlayerAnimationText.Text = "Idle (-1)";
                    PlayerAnimationText.Foreground = new SolidColorBrush(Color.FromRgb(160, 160, 176));
                }
                else
                {
                    PlayerAnimationText.Text = value.StartsWith("Anim") ? value : $"Anim #{value}";
                    PlayerAnimationText.Foreground = new SolidColorBrush(Color.FromRgb(255, 215, 0));
                }
            }
        }
        catch { }
    }

    private void UpdateSkill(string key, string value)
    {
        // Format: SKILL[Attack]: 99 or 99/99
        try
        {
            int openBracket = key.IndexOf('[');
            int closeBracket = key.IndexOf(']');
            if (openBracket != -1 && closeBracket != -1)
            {
                string skillName = key.Substring(openBracket + 1, closeBracket - openBracket - 1).Trim();
                if (_skillsIndex.TryGetValue(skillName, out var existing))
                {
                    if (existing.Value != value) existing.Value = value;
                }
                else
                {
                    var item = new DataItem { Key = skillName, Value = value };
                    _skillsIndex[skillName] = item;
                    _skills.Add(item);
                }

                int slash = value.IndexOf('/');
                if (slash != -1)
                {
                    if (int.TryParse(value.Substring(0, slash).Trim(), out int cur) && int.TryParse(value.Substring(slash + 1).Trim(), out int max))
                    {
                        SkillTrackerEngine.Instance.UpdateSkillLevels(skillName, cur, max);
                    }
                }
                else if (int.TryParse(value.Trim(), out int lvl))
                {
                    SkillTrackerEngine.Instance.UpdateSkillLevels(skillName, lvl, lvl);
                }

                if (skillName.Equals("Hitpoints", StringComparison.OrdinalIgnoreCase))
                {
                    UpdatePlayerHitpoints(value);
                }
                else if (skillName.Equals("Prayer", StringComparison.OrdinalIgnoreCase))
                {
                    UpdatePlayerPrayer(value);
                }
            }
        }
        catch { }
    }

    private void UpdateSkillXp(string key, string value)
    {
        // Format: SKILL_XP[Attack]: 13034431
        try
        {
            int openBracket = key.IndexOf('[');
            int closeBracket = key.IndexOf(']');
            if (openBracket != -1 && closeBracket != -1)
            {
                string skillName = key.Substring(openBracket + 1, closeBracket - openBracket - 1).Trim();
                if (int.TryParse(value.Trim(), out int xp))
                {
                    SkillTrackerEngine.Instance.UpdateSkillXp(skillName, xp);
                }
            }
        }
        catch { }
    }

    private void UpdateNpcList(string key, string value)
    {
        // Format: NPC[0]: ID, Name, Health%, WorldX, WorldY, Plane, Distance, InCombat, Anim, TargetingMe
        // Fallback Format: NPC[0]: ID, Name, Distance, Health[, Category]
        try
        {
            int openBracket = key.IndexOf('[');
            int closeBracket = key.IndexOf(']');
            if (openBracket != -1 && closeBracket != -1)
            {
                int index = int.Parse(key.Substring(openBracket + 1, closeBracket - openBracket - 1));
                var parts = value.Split(',');
                if (parts.Length >= 7)
                {
                    string id = parts[0].Trim();
                    string name = parts[1].Trim();
                    string rawHp = parts[2].Trim();
                    string hp = (rawHp == "-1" || string.IsNullOrEmpty(rawHp)) ? "100%" : (rawHp.EndsWith("%") ? rawHp : rawHp + "%");
                    string dist = parts[6].Trim().EndsWith("m") ? parts[6].Trim() : parts[6].Trim() + "m";
                    bool inCombat = parts.Length > 7 && parts[7].Trim() == "1";
                    bool targetingMe = parts.Length > 9 && parts[9].Trim() == "1";
                    string category = targetingMe ? "Aggressive" : (inCombat ? "In Combat" : "NPC");

                    var npc = new NpcItem
                    {
                        Id = id,
                        Name = name,
                        Distance = dist,
                        Health = hp,
                        Category = category
                    };

                    if (category == "Slayer Master" && SlayerMasterNearbyText != null)
                    {
                        SlayerMasterNearbyText.Text = $"{npc.Name} ({npc.Distance})";
                    }

                    if (index < _npcs.Count)
                    {
                        var curr = _npcs[index];
                        if (curr.Id == npc.Id && curr.Name == npc.Name && curr.Distance == npc.Distance && curr.Health == npc.Health && curr.Category == npc.Category)
                            return;
                        _npcs[index] = npc;
                    }
                    else
                        _npcs.Add(npc);
                }
                else if (parts.Length >= 4)
                {
                    string category = parts.Length >= 5 ? parts[4].Trim() : "NPC";
                    var npc = new NpcItem
                    {
                        Id = parts[0].Trim(),
                        Name = parts[1].Trim(),
                        Distance = parts[2].Trim().EndsWith("m") ? parts[2].Trim() : parts[2].Trim() + "m",
                        Health = parts[3].Trim(),
                        Category = category
                    };

                    if (category == "Slayer Master" && SlayerMasterNearbyText != null)
                    {
                        SlayerMasterNearbyText.Text = $"{npc.Name} ({npc.Distance})";
                    }

                    if (index < _npcs.Count)
                    {
                        var curr = _npcs[index];
                        if (curr.Id == npc.Id && curr.Name == npc.Name && curr.Distance == npc.Distance && curr.Health == npc.Health && curr.Category == npc.Category)
                            return;
                        _npcs[index] = npc;
                    }
                    else
                        _npcs.Add(npc);
                }
            }
        }
        catch { }
    }

    private void UpdateTreeList(string key, string value)
    {
        // Format: TREE[0]: id,name,dist,worldX,worldY,status
        try
        {
            int openBracket = key.IndexOf('[');
            int closeBracket = key.IndexOf(']');
            if (openBracket != -1 && closeBracket != -1)
            {
                int index = int.Parse(key.Substring(openBracket + 1, closeBracket - openBracket - 1));
                var parts = value.Split(',');
                if (parts.Length >= 6)
                {
                    var tree = new TreeItem
                    {
                        Id = parts[0].Trim(),
                        Name = parts[1].Trim(),
                        Distance = parts[2].Trim() + "m",
                        Location = $"({parts[3].Trim()}, {parts[4].Trim()})",
                        Status = parts[5].Trim()
                    };
                    if (index < _trees.Count)
                    {
                        var curr = _trees[index];
                        if (curr.Id == tree.Id && curr.Name == tree.Name && curr.Distance == tree.Distance && curr.Location == tree.Location && curr.Status == tree.Status)
                            return;
                        _trees[index] = tree;
                    }
                    else
                        _trees.Add(tree);
                }
            }
        }
        catch { }
    }

    private void UpdateBankObjList(string key, string value)
    {
        // Format: BANK_OBJ[0]: id,name,dist,worldX,worldY
        try
        {
            int openBracket = key.IndexOf('[');
            int closeBracket = key.IndexOf(']');
            if (openBracket != -1 && closeBracket != -1)
            {
                int index = int.Parse(key.Substring(openBracket + 1, closeBracket - openBracket - 1));
                var parts = value.Split(',');
                if (parts.Length >= 5)
                {
                    var item = new SceneObjectItem
                    {
                        Id = parts[0].Trim(),
                        Name = parts[1].Trim(),
                        Distance = parts[2].Trim() + "m",
                        Location = $"({parts[3].Trim()}, {parts[4].Trim()})"
                    };
                    if (index < _banks.Count)
                    {
                        var curr = _banks[index];
                        if (curr.Id == item.Id && curr.Name == item.Name && curr.Distance == item.Distance && curr.Location == item.Location)
                            return;
                        _banks[index] = item;
                    }
                    else
                        _banks.Add(item);
                }
            }
        }
        catch { }
    }

    private void UpdateShopObjList(string key, string value)
    {
        // Format: SHOP_OBJ[0]: id,name,dist,worldX,worldY
        try
        {
            int openBracket = key.IndexOf('[');
            int closeBracket = key.IndexOf(']');
            if (openBracket != -1 && closeBracket != -1)
            {
                int index = int.Parse(key.Substring(openBracket + 1, closeBracket - openBracket - 1));
                var parts = value.Split(',');
                if (parts.Length >= 5)
                {
                    var item = new SceneObjectItem
                    {
                        Id = parts[0].Trim(),
                        Name = parts[1].Trim(),
                        Distance = parts[2].Trim() + "m",
                        Location = $"({parts[3].Trim()}, {parts[4].Trim()})"
                    };
                    if (index < _shops.Count)
                    {
                        var curr = _shops[index];
                        if (curr.Id == item.Id && curr.Name == item.Name && curr.Distance == item.Distance && curr.Location == item.Location)
                            return;
                        _shops[index] = item;
                    }
                    else
                        _shops.Add(item);
                }
            }
        }
        catch { }
    }

    private void UpdateAltarObjList(string key, string value)
    {
        // Format: ALTAR_OBJ[0]: id,name,dist,worldX,worldY
        try
        {
            int openBracket = key.IndexOf('[');
            int closeBracket = key.IndexOf(']');
            if (openBracket != -1 && closeBracket != -1)
            {
                int index = int.Parse(key.Substring(openBracket + 1, closeBracket - openBracket - 1));
                var parts = value.Split(',');
                if (parts.Length >= 5)
                {
                    var item = new SceneObjectItem
                    {
                        Id = parts[0].Trim(),
                        Name = parts[1].Trim(),
                        Distance = parts[2].Trim() + "m",
                        Location = $"({parts[3].Trim()}, {parts[4].Trim()})"
                    };
                    if (index < _altars.Count)
                    {
                        var curr = _altars[index];
                        if (curr.Id == item.Id && curr.Name == item.Name && curr.Distance == item.Distance && curr.Location == item.Location)
                            return;
                        _altars[index] = item;
                    }
                    else
                        _altars.Add(item);
                }
            }
        }
        catch { }
    }

    private void UpdateRockObjList(string key, string value)
    {
        // Format: ROCK_OBJ[0]: id,name,dist,worldX,worldY
        try
        {
            int openBracket = key.IndexOf('[');
            int closeBracket = key.IndexOf(']');
            if (openBracket != -1 && closeBracket != -1)
            {
                int index = int.Parse(key.Substring(openBracket + 1, closeBracket - openBracket - 1));
                var parts = value.Split(',');
                if (parts.Length >= 5)
                {
                    var item = new SceneObjectItem
                    {
                        Id = parts[0].Trim(),
                        Name = parts[1].Trim(),
                        Distance = parts[2].Trim() + "m",
                        Location = $"({parts[3].Trim()}, {parts[4].Trim()})"
                    };
                    if (index < _rocks.Count)
                    {
                        var curr = _rocks[index];
                        if (curr.Id == item.Id && curr.Name == item.Name && curr.Distance == item.Distance && curr.Location == item.Location)
                            return;
                        _rocks[index] = item;
                    }
                    else
                        _rocks.Add(item);
                }
            }
        }
        catch { }
    }

    private void UpdateGroundItemList(string key, string value)
    {
        // Format: GROUND_ITEM[0]: id,name,qty,dist,worldX,worldY
        try
        {
            int openBracket = key.IndexOf('[');
            int closeBracket = key.IndexOf(']');
            if (openBracket != -1 && closeBracket != -1)
            {
                int index = int.Parse(key.Substring(openBracket + 1, closeBracket - openBracket - 1));
                var parts = value.Split(',');
                if (parts.Length >= 6)
                {
                    var item = new GroundItem
                    {
                        Id = parts[0].Trim(),
                        Name = parts[1].Trim(),
                        Quantity = parts[2].Trim(),
                        Distance = parts[3].Trim() + "m",
                        Location = $"({parts[4].Trim()}, {parts[5].Trim()})"
                    };
                    if (index < _groundItems.Count)
                    {
                        var curr = _groundItems[index];
                        if (curr.Id == item.Id && curr.Name == item.Name && curr.Quantity == item.Quantity && curr.Distance == item.Distance && curr.Location == item.Location)
                            return;
                        _groundItems[index] = item;
                    }
                    else
                        _groundItems.Add(item);
                }
            }
        }
        catch { }
    }

    private void UpdateBankContainerItem(string key, string value)
    {
        // Format: BANK_ITEM[0]: id,name,qty
        try
        {
            int openBracket = key.IndexOf('[');
            int closeBracket = key.IndexOf(']');
            if (openBracket != -1 && closeBracket != -1)
            {
                int index = int.Parse(key.Substring(openBracket + 1, closeBracket - openBracket - 1));
                var parts = value.Split(',');
                if (parts.Length >= 3)
                {
                    var item = new ContainerItem
                    {
                        Id = parts[0].Trim(),
                        Name = parts[1].Trim(),
                        Quantity = parts[2].Trim()
                    };
                    if (index < _bankItems.Count)
                    {
                        var curr = _bankItems[index];
                        if (curr.Id == item.Id && curr.Name == item.Name && curr.Quantity == item.Quantity)
                            return;
                        _bankItems[index] = item;
                    }
                    else
                        _bankItems.Add(item);
                }
            }
        }
        catch { }
    }

    private void UpdateShopContainerItem(string key, string value)
    {
        // Format: SHOP_ITEM[0]: id,name,qty
        try
        {
            int openBracket = key.IndexOf('[');
            int closeBracket = key.IndexOf(']');
            if (openBracket != -1 && closeBracket != -1)
            {
                int index = int.Parse(key.Substring(openBracket + 1, closeBracket - openBracket - 1));
                var parts = value.Split(',');
                if (parts.Length >= 3)
                {
                    var item = new ContainerItem
                    {
                        Id = parts[0].Trim(),
                        Name = parts[1].Trim(),
                        Quantity = parts[2].Trim()
                    };
                    if (index < _shopItems.Count)
                    {
                        var curr = _shopItems[index];
                        if (curr.Id == item.Id && curr.Name == item.Name && curr.Quantity == item.Quantity)
                            return;
                        _shopItems[index] = item;
                    }
                    else
                        _shopItems.Add(item);
                }
            }
        }
        catch { }
    }

    private void UpdateGrandExchangeOffer(string key, string value)
    {
        // Format: GE_SLOT[0]: State,ItemId,ItemName,Price,TotalQty,QtySold,Spent
        try
        {
            int openBracket = key.IndexOf('[');
            int closeBracket = key.IndexOf(']');
            if (openBracket != -1 && closeBracket != -1)
            {
                int slot = int.Parse(key.Substring(openBracket + 1, closeBracket - openBracket - 1));
                if (slot < 0 || slot >= 8) return;

                var parts = value.Split(',');
                if (parts.Length >= 7)
                {
                    string state = parts[0].Trim();
                    int itemId = int.TryParse(parts[1].Trim(), out int id) ? id : 0;
                    string name = parts[2].Trim();
                    int price = int.TryParse(parts[3].Trim(), out int p) ? p : 0;
                    int tot = int.TryParse(parts[4].Trim(), out int tq) ? tq : 0;
                    int trans = int.TryParse(parts[5].Trim(), out int qt) ? qt : 0;
                    int spent = int.TryParse(parts[6].Trim(), out int sp) ? sp : 0;

                    if (slot < _geOffers.Count)
                    {
                        var offer = _geOffers[slot];
                        offer.State = state;
                        offer.ItemId = itemId;
                        offer.ItemName = (itemId > 0 && !string.IsNullOrWhiteSpace(name) && name != "None") ? name : (itemId > 0 ? $"Item #{itemId}" : "Empty Slot");
                        offer.Price = price;
                        offer.TotalQuantity = tot;
                        offer.QuantityTransferred = trans;
                        offer.Spent = spent;
                    }
                }
            }
        }
        catch { }
    }

    private void UpdateRunePouchSlot(string key, string value)
    {
        // Format: RUNE_POUCH[0]: typeIdx,runeName,qty
        try
        {
            int openBracket = key.IndexOf('[');
            int closeBracket = key.IndexOf(']');
            if (openBracket != -1 && closeBracket != -1)
            {
                int slot = int.Parse(key.Substring(openBracket + 1, closeBracket - openBracket - 1));
                if (slot < 0 || slot >= 4) return;

                var parts = value.Split(',');
                if (parts.Length >= 3)
                {
                    int runeId = int.TryParse(parts[0].Trim(), out int id) ? id : 0;
                    string name = parts[1].Trim();
                    int qty = int.TryParse(parts[2].Trim(), out int q) ? q : 0;

                    if (slot < _runePouchSlots.Count)
                    {
                        var runeSlot = _runePouchSlots[slot];
                        runeSlot.RuneId = runeId;
                        runeSlot.RuneName = (runeId > 0 && !string.IsNullOrWhiteSpace(name) && name != "None") ? name : "Empty";
                        runeSlot.Quantity = qty;
                    }
                }
            }
        }
        catch { }
    }

    private void UpdateLootingBagItem(string key, string value)
    {
        // Format: LOOTING_BAG[0]: id,name,qty
        try
        {
            int openBracket = key.IndexOf('[');
            int closeBracket = key.IndexOf(']');
            if (openBracket != -1 && closeBracket != -1)
            {
                int index = int.Parse(key.Substring(openBracket + 1, closeBracket - openBracket - 1));
                var parts = value.Split(',');
                if (parts.Length >= 3)
                {
                    int id = int.TryParse(parts[0].Trim(), out int pid) ? pid : 0;
                    if (id <= 0 || parts[0].Trim() == "0" || parts[0].Trim() == "-1" || parts[0].Trim().Equals("EMPTY", StringComparison.OrdinalIgnoreCase))
                    {
                        if (index < _lootingBagItems.Count)
                            _lootingBagItems.RemoveAt(index);
                        return;
                    }

                    var item = new ContainerItem
                    {
                        Id = parts[0].Trim(),
                        Name = parts[1].Trim(),
                        Quantity = parts[2].Trim()
                    };
                    if (index < _lootingBagItems.Count)
                        _lootingBagItems[index] = item;
                    else
                        _lootingBagItems.Add(item);
                }
            }
        }
        catch { }
    }

    private void UpdateGemBagUi(string value)
    {
        // Format: GEM_BAG: s,e,r,d,ds
        try
        {
            var parts = value.Split(',');
            if (parts.Length >= 5)
            {
                int s = int.TryParse(parts[0].Trim(), out int ps) ? ps : 0;
                int e = int.TryParse(parts[1].Trim(), out int pe) ? pe : 0;
                int r = int.TryParse(parts[2].Trim(), out int pr) ? pr : 0;
                int d = int.TryParse(parts[3].Trim(), out int pd) ? pd : 0;
                int ds = int.TryParse(parts[4].Trim(), out int pds) ? pds : 0;

                if (GemBagSapphireText != null) GemBagSapphireText.Text = s.ToString();
                if (GemBagEmeraldText != null) GemBagEmeraldText.Text = e.ToString();
                if (GemBagRubyText != null) GemBagRubyText.Text = r.ToString();
                if (GemBagDiamondText != null) GemBagDiamondText.Text = d.ToString();
                if (GemBagDragonstoneText != null) GemBagDragonstoneText.Text = ds.ToString();
                if (GemBagTotalText != null) GemBagTotalText.Text = (s + e + r + d + ds).ToString();
            }
        }
        catch { }
    }

    private void UpdateEssencePouchesUi(string value)
    {
        // Format: ESSENCE_POUCHES: small,med,large,giant,colossal
        try
        {
            var parts = value.Split(',');
            if (parts.Length >= 5)
            {
                int sm = int.TryParse(parts[0].Trim(), out int psm) ? psm : 0;
                int md = int.TryParse(parts[1].Trim(), out int pmd) ? pmd : 0;
                int lg = int.TryParse(parts[2].Trim(), out int plg) ? plg : 0;
                int gt = int.TryParse(parts[3].Trim(), out int pgt) ? pgt : 0;
                int col = int.TryParse(parts[4].Trim(), out int pcol) ? pcol : 0;

                if (EssPouchSmallText != null) EssPouchSmallText.Text = sm.ToString();
                if (EssPouchMedText != null) EssPouchMedText.Text = md.ToString();
                if (EssPouchLargeText != null) EssPouchLargeText.Text = lg.ToString();
                if (EssPouchGiantText != null) EssPouchGiantText.Text = gt.ToString();
                if (EssPouchColossalText != null) EssPouchColossalText.Text = col.ToString();
                if (EssPouchTotalText != null) EssPouchTotalText.Text = (sm + md + lg + gt + col).ToString();
            }
        }
        catch { }
    }

    private void UpdateSpecialAttack(string value)
    {
        string clean = value.Replace("%", "").Trim();
        if (double.TryParse(clean, out double pct))
        {
            if (SpecProgressBar != null) SpecProgressBar.Value = Math.Clamp(pct, 0, 100);
            if (SpecPercentText != null) SpecPercentText.Text = $"{pct:0}% Special Attack Energy";
            if (PlayerSpecText != null) PlayerSpecText.Text = $"{pct:0}%";
        }
    }

    private void UpdateSpecialAttackActive(string value)
    {
        bool isActive = value.Equals("Active", StringComparison.OrdinalIgnoreCase) || value.Equals("True", StringComparison.OrdinalIgnoreCase);
        if (SpecActiveBadge != null && SpecActiveText != null)
        {
            SpecActiveBadge.Background = isActive ? new SolidColorBrush(Color.FromRgb(30, 60, 45)) : new SolidColorBrush(Color.FromRgb(42, 42, 42));
            SpecActiveText.Text = isActive ? "Active" : "Inactive";
            SpecActiveText.Foreground = isActive ? new SolidColorBrush(Color.FromRgb(76, 175, 80)) : Brushes.Gray;
        }
    }

    private void UpdateBuffsAndStatusUi()
    {
        var effects = BrainEngine.Instance.State.StatusEffects;
        if (effects == null) return;

        // Stamina
        if (BuffStaminaText != null)
        {
            if (effects.HasStamina)
            {
                BuffStaminaText.Text = effects.StaminaDurationFormatted;
                BuffStaminaText.Foreground = new SolidColorBrush(Color.FromRgb(251, 191, 36));
            }
            else
            {
                BuffStaminaText.Text = "Inactive";
                BuffStaminaText.Foreground = Brushes.Gray;
            }
        }

        // Antifire
        if (BuffAntifireText != null)
        {
            if (effects.HasAntifire)
            {
                string type = effects.IsSuperAntifire ? "Super (" : "Active (";
                BuffAntifireText.Text = type + effects.AntifireDurationFormatted + ")";
                BuffAntifireText.Foreground = new SolidColorBrush(Color.FromRgb(129, 199, 132));
            }
            else
            {
                BuffAntifireText.Text = "Inactive";
                BuffAntifireText.Foreground = Brushes.Gray;
            }
        }

        // Poison / Venom
        if (BuffPoisonText != null)
        {
            if (effects.IsEnvenomed)
            {
                BuffPoisonText.Text = $"Venomed ({effects.VenomDamage} dmg)";
                BuffPoisonText.Foreground = new SolidColorBrush(Color.FromRgb(52, 211, 153));
            }
            else if (effects.IsPoisoned)
            {
                BuffPoisonText.Text = $"Poisoned ({effects.PoisonDamage} dmg)";
                BuffPoisonText.Foreground = new SolidColorBrush(Color.FromRgb(74, 222, 128));
            }
            else if (effects.HasImmunity)
            {
                BuffPoisonText.Text = $"Immune ({effects.ImmunityDurationFormatted})";
                BuffPoisonText.Foreground = new SolidColorBrush(Color.FromRgb(56, 189, 248));
            }
            else
            {
                BuffPoisonText.Text = "Healthy";
                BuffPoisonText.Foreground = new SolidColorBrush(Color.FromRgb(52, 211, 153));
            }
        }

        // Overload
        if (BuffOverloadText != null)
        {
            if (effects.HasOverload)
            {
                BuffOverloadText.Text = effects.OverloadDurationFormatted;
                BuffOverloadText.Foreground = new SolidColorBrush(Color.FromRgb(167, 139, 250));
            }
            else
            {
                BuffOverloadText.Text = "Inactive";
                BuffOverloadText.Foreground = Brushes.Gray;
            }
        }

        // Divine
        if (BuffDivineText != null)
        {
            if (effects.HasDivine)
            {
                BuffDivineText.Text = effects.DivineDurationFormatted;
                BuffDivineText.Foreground = new SolidColorBrush(Color.FromRgb(96, 165, 250));
            }
            else
            {
                BuffDivineText.Text = "Inactive";
                BuffDivineText.Foreground = Brushes.Gray;
            }
        }

        // Imbued Heart
        if (BuffHeartText != null)
        {
            if (effects.IsImbuedHeartReady)
            {
                BuffHeartText.Text = "Ready";
                BuffHeartText.Foreground = new SolidColorBrush(Color.FromRgb(56, 189, 248));
            }
            else
            {
                BuffHeartText.Text = $"Cooldown ({effects.ImbuedHeartCooldownFormatted})";
                BuffHeartText.Foreground = new SolidColorBrush(Color.FromRgb(248, 113, 113));
            }
        }

        // Prayer Enhance
        if (BuffPrayerEnhanceText != null)
        {
            if (effects.HasPrayerEnhance)
            {
                BuffPrayerEnhanceText.Text = effects.PrayerEnhanceDurationFormatted;
                BuffPrayerEnhanceText.Foreground = new SolidColorBrush(Color.FromRgb(0, 229, 255));
            }
            else
            {
                BuffPrayerEnhanceText.Text = "Inactive";
                BuffPrayerEnhanceText.Foreground = Brushes.Gray;
            }
        }

        // Charge
        if (BuffChargeText != null)
        {
            if (effects.HasCharge)
            {
                BuffChargeText.Text = "Active (" + effects.ChargeTicks + "t)";
                BuffChargeText.Foreground = new SolidColorBrush(Color.FromRgb(251, 146, 60));
            }
            else
            {
                BuffChargeText.Text = "Inactive";
                BuffChargeText.Foreground = Brushes.Gray;
            }
        }

        // Toggles
        if (BuffTogglesText != null)
        {
            string ret = effects.AutoRetaliate ? "ON" : "OFF";
            string run = effects.RunEnabled ? "ON" : "OFF";
            BuffTogglesText.Text = $"Retaliate: {ret} | Run: {run}";
            BuffTogglesText.Foreground = new SolidColorBrush(Color.FromRgb(203, 213, 225));
        }

        // Poison Status Badge
        if (PoisonStatusText != null)
        {
            if (effects.IsPoisoned)
            {
                PoisonStatusText.Text = $"Poison: {effects.PoisonDamage} dmg";
                PoisonStatusText.Foreground = new SolidColorBrush(Color.FromRgb(74, 222, 128));
            }
            else if (effects.HasImmunity)
            {
                PoisonStatusText.Text = "Poison: Immune";
                PoisonStatusText.Foreground = new SolidColorBrush(Color.FromRgb(56, 189, 248));
            }
            else
            {
                PoisonStatusText.Text = "Poison: Normal";
                PoisonStatusText.Foreground = new SolidColorBrush(Color.FromRgb(160, 160, 176));
            }
        }

        // Venom Status Badge
        if (VenomStatusText != null)
        {
            if (effects.IsEnvenomed)
            {
                VenomStatusText.Text = $"Venom: {effects.VenomDamage} dmg";
                VenomStatusText.Foreground = new SolidColorBrush(Color.FromRgb(52, 211, 153));
            }
            else
            {
                VenomStatusText.Text = "Venom: None";
                VenomStatusText.Foreground = new SolidColorBrush(Color.FromRgb(160, 160, 176));
            }
        }

        // Freeze Status Badge
        if (FreezeStatusText != null)
        {
            if (effects.FreezeTicks > 0)
            {
                FreezeStatusText.Text = $"Frozen ({effects.FreezeTicks}t)";
                FreezeStatusText.Foreground = new SolidColorBrush(Color.FromRgb(96, 165, 250));
            }
            else
            {
                FreezeStatusText.Text = "Freeze: None";
                FreezeStatusText.Foreground = new SolidColorBrush(Color.FromRgb(160, 160, 176));
            }
        }

        // Vengeance Status Badge
        if (VengeanceStatusText != null)
        {
            bool veng = BrainEngine.Instance.State.Player?.IsVengeanceActive ?? false;
            if (veng)
            {
                VengeanceStatusText.Text = "Veng: ACTIVE";
                VengeanceStatusText.Foreground = new SolidColorBrush(Color.FromRgb(248, 113, 113));
            }
            else
            {
                VengeanceStatusText.Text = "Veng: Inactive";
                VengeanceStatusText.Foreground = new SolidColorBrush(Color.FromRgb(160, 160, 176));
            }
        }
    }

    private void UpdateSlayerTask(string value)
    {
        if (SlayerTaskText != null) SlayerTaskText.Text = value;
        if (PlayerSlayerText != null)
        {
            string count = SlayerCountText?.Text ?? "0";
            PlayerSlayerText.Text = value != "None" ? $"{value} ({count})" : "None";
        }
    }

    private void UpdateSlayerCount(string value)
    {
        if (SlayerCountText != null) SlayerCountText.Text = value;
        if (PlayerSlayerText != null)
        {
            string task = SlayerTaskText?.Text ?? "None";
            PlayerSlayerText.Text = task != "None" ? $"{task} ({value})" : "None";
        }
    }

    private void UpdateBankStatus(string value)
    {
        bool isOpen = value.Equals("True", StringComparison.OrdinalIgnoreCase);
        if (BankOpenBadge != null && BankOpenText != null)
        {
            BankOpenBadge.Background = isOpen ? new SolidColorBrush(Color.FromRgb(30, 60, 45)) : new SolidColorBrush(Color.FromRgb(42, 42, 42));
            BankOpenText.Text = isOpen ? "Bank Open" : "Bank Closed";
            BankOpenText.Foreground = isOpen ? new SolidColorBrush(Color.FromRgb(76, 175, 80)) : Brushes.Gray;
        }
    }

    private void UpdateShopStatus(string value)
    {
        bool isOpen = value.Equals("True", StringComparison.OrdinalIgnoreCase);
        if (ShopOpenBadge != null && ShopOpenText != null)
        {
            ShopOpenBadge.Background = isOpen ? new SolidColorBrush(Color.FromRgb(30, 60, 45)) : new SolidColorBrush(Color.FromRgb(42, 42, 42));
            ShopOpenText.Text = isOpen ? "Store Open" : "Store Closed";
            ShopOpenText.Foreground = isOpen ? new SolidColorBrush(Color.FromRgb(76, 175, 80)) : Brushes.Gray;
        }
    }

    private void UpdateDialogActive(string value)
    {
        bool isActive = value.Equals("True", StringComparison.OrdinalIgnoreCase);
        if (PlayerDialogBadge != null && PlayerDialogBadgeText != null)
        {
            PlayerDialogBadge.Background = isActive ? new SolidColorBrush(Color.FromRgb(30, 50, 75)) : new SolidColorBrush(Color.FromRgb(42, 42, 42));
            PlayerDialogBadgeText.Text = isActive ? "Active" : "Inactive";
            PlayerDialogBadgeText.Foreground = isActive ? new SolidColorBrush(Color.FromRgb(0, 229, 255)) : Brushes.Gray;
        }
        if (!isActive)
        {
            if (DialogTypeText != null) DialogTypeText.Text = "No Active Dialogue";
            if (DialogTypeBadge != null) DialogTypeBadge.Background = new SolidColorBrush(Color.FromRgb(42, 42, 42));
            if (DialogTitleText != null) DialogTitleText.Text = "";
            if (DialogContentText != null) DialogContentText.Text = "No dialog currently open in the game client.";
            if (DialogOptionsPanel != null) DialogOptionsPanel.Visibility = Visibility.Collapsed;
        }
    }

    private void UpdateDialogType(string value)
    {
        if (DialogTypeText != null) DialogTypeText.Text = $"{value} Dialogue";
        if (DialogTypeBadge != null)
        {
            DialogTypeBadge.Background = new SolidColorBrush(Color.FromRgb(30, 50, 75));
            if (DialogTypeText != null) DialogTypeText.Foreground = new SolidColorBrush(Color.FromRgb(0, 229, 255));
        }
    }

    private void UpdateDialogTitle(string value)
    {
        if (DialogTitleText != null) DialogTitleText.Text = value;
    }

    private void UpdateDialogText(string value)
    {
        if (DialogContentText != null && !string.IsNullOrWhiteSpace(value))
        {
            DialogContentText.Text = value;
        }
    }

    private void UpdateDialogOptions(string value)
    {
        if (DialogOptionsPanel != null && DialogOptionsText != null)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                var options = value.Split('|');
                var sb = new System.Text.StringBuilder();
                for (int i = 0; i < options.Length; i++)
                {
                    if (i > 0) sb.AppendLine();
                    sb.Append($"{i + 1}. {options[i].Trim()}");
                }
                DialogOptionsText.Text = sb.ToString();
                DialogOptionsPanel.Visibility = Visibility.Visible;
            }
            else
            {
                DialogOptionsPanel.Visibility = Visibility.Collapsed;
            }
        }
    }

    private void UpdateShortcutList(string key, string value)
    {
        // Format: SHORTCUT[0]: id,name,reqLevel,dist,worldX,worldY
        try
        {
            int openBracket = key.IndexOf('[');
            int closeBracket = key.IndexOf(']');
            if (openBracket != -1 && closeBracket != -1)
            {
                int index = int.Parse(key.Substring(openBracket + 1, closeBracket - openBracket - 1));
                var parts = value.Split(',');
                if (parts.Length >= 6)
                {
                    var item = new ShortcutItem
                    {
                        Id = parts[0].Trim(),
                        Name = parts[1].Trim(),
                        ReqLevel = "Lvl " + parts[2].Trim(),
                        Distance = parts[3].Trim() + "m",
                        Location = $"({parts[4].Trim()}, {parts[5].Trim()})"
                    };
                    if (index < _shortcuts.Count)
                        _shortcuts[index] = item;
                    else
                        _shortcuts.Add(item);
                }
            }
        }
        catch { }
    }

    private void UpdateAgilityObstacleList(string key, string value)
    {
        // Format: AGILITY_OBSTACLE[0]: id,name,course,dist,worldX,worldY
        try
        {
            int openBracket = key.IndexOf('[');
            int closeBracket = key.IndexOf(']');
            if (openBracket != -1 && closeBracket != -1)
            {
                int index = int.Parse(key.Substring(openBracket + 1, closeBracket - openBracket - 1));
                var parts = value.Split(',');
                if (parts.Length >= 6)
                {
                    var item = new AgilityObstacleItem
                    {
                        Id = parts[0].Trim(),
                        Name = parts[1].Trim(),
                        Course = parts[2].Trim(),
                        Distance = parts[3].Trim() + "m",
                        Location = $"({parts[4].Trim()}, {parts[5].Trim()})"
                    };
                    if (index < _agilityObstacles.Count)
                        _agilityObstacles[index] = item;
                    else
                        _agilityObstacles.Add(item);
                }
            }
        }
        catch { }
    }

    private void UpdateFishingSpotList(string key, string value)
    {
        // Format: FISHING_SPOT[0]: id,name,spotType,dist,worldX,worldY
        try
        {
            int openBracket = key.IndexOf('[');
            int closeBracket = key.IndexOf(']');
            if (openBracket != -1 && closeBracket != -1)
            {
                int index = int.Parse(key.Substring(openBracket + 1, closeBracket - openBracket - 1));
                var parts = value.Split(',');
                if (parts.Length >= 6)
                {
                    var item = new FishingSpotItem
                    {
                        Id = parts[0].Trim(),
                        Name = parts[1].Trim(),
                        SpotType = parts[2].Trim(),
                        Distance = parts[3].Trim() + "m",
                        Location = $"({parts[4].Trim()}, {parts[5].Trim()})"
                    };
                    if (index < _fishingSpots.Count)
                        _fishingSpots[index] = item;
                    else
                        _fishingSpots.Add(item);
                }
            }
        }
        catch { }
    }

    private void UpdateAgilityCourse(string value)
    {
        if (AgilityCourseText != null) AgilityCourseText.Text = value;
        if (PlayerAgilityText != null)
        {
            if (value != "None")
            {
                string req = AgilityCourseLevelText?.Text ?? "";
                PlayerAgilityText.Text = !string.IsNullOrEmpty(req) && req != "-" ? $"{value} ({req})" : value;
            }
            else
            {
                PlayerAgilityText.Text = "None";
            }
        }
    }

    private void UpdateAgilityCourseLevel(string value)
    {
        if (AgilityCourseLevelText != null) AgilityCourseLevelText.Text = value != "-" ? $"Level {value}" : "-";
        if (PlayerAgilityText != null)
        {
            string course = AgilityCourseText?.Text ?? "None";
            if (course != "None")
            {
                PlayerAgilityText.Text = value != "-" ? $"{course} (Lvl {value})" : course;
            }
        }
    }

    private void UpdateMarksOfGrace(string value)
    {
        if (MarksOfGraceText != null)
        {
            int count = int.TryParse(value, out int c) ? c : 0;
            MarksOfGraceText.Text = count > 0 ? $"{count} Nearby" : "0 Nearby";
            MarksOfGraceText.Foreground = count > 0 ? new SolidColorBrush(Color.FromRgb(255, 215, 0)) : Brushes.Gray;
        }
    }

    private void UpdateMinigameActive(string value)
    {
        bool isActive = value.Equals("True", StringComparison.OrdinalIgnoreCase);
        if (MinigameActiveBadge != null && MinigameActiveText != null)
        {
            MinigameActiveBadge.Background = isActive ? new SolidColorBrush(Color.FromRgb(40, 25, 55)) : new SolidColorBrush(Color.FromRgb(42, 42, 42));
            MinigameActiveText.Text = isActive ? "Active" : "Inactive";
            MinigameActiveText.Foreground = isActive ? new SolidColorBrush(Color.FromRgb(186, 104, 200)) : Brushes.Gray;
        }
    }

    private void UpdateMinigameName(string value)
    {
        if (MinigameNameText != null) MinigameNameText.Text = value;
        if (PlayerMinigameText != null)
        {
            PlayerMinigameText.Text = value;
            PlayerMinigameText.Foreground = value != "None" ? new SolidColorBrush(Color.FromRgb(186, 104, 200)) : Brushes.Gray;
        }
    }

    private void UpdatePlayerList(string key, string value)
    {
        // Format: PLAYER[0]: Name, CombatLevel, WorldX, WorldY, Plane, Distance, InCombat, Anim, Interacting
        // Fallback Format: NEARBY_PLAYER[0]: ID, Name, Distance, CombatLevel
        try
        {
            int openBracket = key.IndexOf('[');
            int closeBracket = key.IndexOf(']');
            if (openBracket != -1 && closeBracket != -1)
            {
                int index = int.Parse(key.Substring(openBracket + 1, closeBracket - openBracket - 1));
                var parts = value.Split(',');
                if (parts.Length >= 6)
                {
                    string name = parts[0].Trim();
                    string cbLvl = "Lvl " + parts[1].Trim();
                    string dist = parts[5].Trim().EndsWith("m") ? parts[5].Trim() : parts[5].Trim() + "m";
                    string id = (index + 1).ToString();

                    var player = new PlayerItem
                    {
                        Id = id,
                        Name = name,
                        Distance = dist,
                        CombatLevel = cbLvl
                    };

                    if (index < _players.Count)
                    {
                        var curr = _players[index];
                        if (curr.Id == player.Id && curr.Name == player.Name && curr.Distance == player.Distance && curr.CombatLevel == player.CombatLevel)
                            return;
                        _players[index] = player;
                    }
                    else
                        _players.Add(player);
                }
                else if (parts.Length >= 4)
                {
                    var player = new PlayerItem
                    {
                        Id = parts[0].Trim(),
                        Name = parts[1].Trim(),
                        Distance = parts[2].Trim().EndsWith("m") ? parts[2].Trim() : parts[2].Trim() + "m",
                        CombatLevel = parts[3].Trim()
                    };

                    if (index < _players.Count)
                    {
                        var curr = _players[index];
                        if (curr.Id == player.Id && curr.Name == player.Name && curr.Distance == player.Distance && curr.CombatLevel == player.CombatLevel)
                            return;
                        _players[index] = player;
                    }
                    else
                        _players.Add(player);
                }
            }
        }
        catch { }
    }

    private static bool TryParseItemPayload(string value, out int id, out string name, out int qty)
    {
        id = -1;
        name = "";
        qty = 0;

        if (string.IsNullOrWhiteSpace(value)) return false;
        value = value.Trim();

        if (value.Equals("EMPTY", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("Empty", StringComparison.OrdinalIgnoreCase) ||
            value == "0" || value == "-1" || value == "65535" || value == "0,0" ||
            value.StartsWith("0,0") || value.StartsWith("0,None") || value.StartsWith("-1,Empty"))
        {
            return false;
        }

        var parts = value.Split(',');
        if (parts.Length == 3)
        {
            // Format: ID, Name, Quantity
            if (int.TryParse(parts[0].Trim(), out int parsedId)) id = parsedId;
            name = parts[1].Trim();
            if (int.TryParse(parts[2].Trim(), out int parsedQty)) qty = parsedQty;
            else qty = 1;

            if (id > 0 && id != 65535 && !name.Equals("EMPTY", StringComparison.OrdinalIgnoreCase))
            {
                ItemDatabase.RegisterItem(id, name);
                if (name.StartsWith("Item #", StringComparison.OrdinalIgnoreCase))
                {
                    name = ItemDatabase.ResolveItemName(name);
                }
                return true;
            }
            return false;
        }
        else if (parts.Length == 2)
        {
            // Could be "ID, Quantity" or "Name, Quantity" or "ID, Name"
            if (int.TryParse(parts[0].Trim(), out int parsedId))
            {
                id = parsedId;
                if (int.TryParse(parts[1].Trim(), out int parsedQty))
                {
                    qty = parsedQty;
                    name = ItemDatabase.GetItemName(id);
                }
                else
                {
                    name = parts[1].Trim();
                    qty = 1;
                    ItemDatabase.RegisterItem(id, name);
                }

                if (id > 0 && id != 65535 && !name.Equals("EMPTY", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
                return false;
            }
            else
            {
                name = parts[0].Trim();
                if (int.TryParse(parts[1].Trim(), out int parsedQty)) qty = parsedQty;
                else qty = 1;

                return !string.IsNullOrEmpty(name) && !name.Equals("EMPTY", StringComparison.OrdinalIgnoreCase);
            }
        }
        else if (parts.Length == 1)
        {
            if (int.TryParse(value, out int parsedId))
            {
                id = parsedId;
                if (id > 0 && id != 65535)
                {
                    name = ItemDatabase.GetItemName(id);
                    qty = 1;
                    return true;
                }
                return false;
            }
            else
            {
                name = value;
                qty = 1;
                return !string.IsNullOrEmpty(name) && !name.Equals("EMPTY", StringComparison.OrdinalIgnoreCase);
            }
        }

        return false;
    }

    private static string FormatItemQuantity(int qty)
    {
        if (qty >= 10_000_000) return $"{qty / 1_000_000}M";
        if (qty >= 100_000) return $"{qty / 1_000}K";
        return qty.ToString();
    }

    private static System.Windows.Media.Brush GetQuantityBrush(int qty)
    {
        if (qty >= 10_000_000) return new SolidColorBrush(Color.FromRgb(0, 255, 128));
        if (qty >= 100_000) return Brushes.White;
        return new SolidColorBrush(Color.FromRgb(255, 255, 0));
    }

    private static string GetEquipmentSlotDefaultName(string slotId) => slotId switch
    {
        "0" => "Head",
        "1" => "Cape",
        "2" => "Neck",
        "3" => "Weapon",
        "4" => "Body",
        "5" => "Shield",
        "7" => "Legs",
        "9" => "Hands",
        "10" => "Feet",
        "12" => "Ring",
        "13" => "Ammo",
        _ => $"Slot {slotId}"
    };

    private void ResetEquipmentSlotUi(string slotId, Border border)
    {
        border.Background = new SolidColorBrush(Color.FromRgb(28, 30, 36));
        border.BorderBrush = new SolidColorBrush(Color.FromRgb(55, 60, 70));
        border.CornerRadius = new CornerRadius(4);
        string slotName = GetEquipmentSlotDefaultName(slotId);
        border.ToolTip = $"{slotName} (Empty)";
        border.Child = new TextBlock
        {
            Text = slotName,
            FontSize = 9,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(95, 100, 115)),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            TextAlignment = TextAlignment.Center
        };
    }

    private void UpdateEquipmentSlot(string key, string value)
    {
        // Format: EQUIP[slotId]: ID, Name, Quantity
        try
        {
            int openBracket = key.IndexOf('[');
            int closeBracket = key.IndexOf(']');
            if (openBracket != -1 && closeBracket != -1)
            {
                string slotId = key.Substring(openBracket + 1, closeBracket - openBracket - 1);
                if (_lastEquipmentRaw.TryGetValue(slotId, out var lastVal) && lastVal == value)
                    return;
                _lastEquipmentRaw[slotId] = value;

                if (_equipmentSlots.TryGetValue(slotId, out var border))
                {
                    if (TryParseItemPayload(value, out int id, out string name, out int qty))
                    {
                        border.Background = new SolidColorBrush(Color.FromRgb(24, 48, 76));
                        border.BorderBrush = new SolidColorBrush(Color.FromRgb(0, 180, 216));
                        string slotName = GetEquipmentSlotDefaultName(slotId);
                        string qtyDisplay = qty > 1 ? $" (x{qty:N0})" : "";
                        string idDisplay = id > 0 ? $" [ID: {id}]" : "";
                        border.ToolTip = $"{slotName}: {name}{idDisplay}{qtyDisplay}";

                        var cellGrid = new Grid();

                        var nameText = new TextBlock
                        {
                            Text = name,
                            FontSize = 8,
                            FontWeight = FontWeights.SemiBold,
                            Foreground = Brushes.White,
                            TextWrapping = TextWrapping.Wrap,
                            TextAlignment = TextAlignment.Center,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            VerticalAlignment = VerticalAlignment.Center,
                            Margin = new Thickness(2)
                        };
                        cellGrid.Children.Add(nameText);

                        if (qty > 1)
                        {
                            var qtyText = new TextBlock
                            {
                                Text = FormatItemQuantity(qty),
                                FontSize = 7.5,
                                FontWeight = FontWeights.Bold,
                                Foreground = GetQuantityBrush(qty),
                                HorizontalAlignment = HorizontalAlignment.Right,
                                VerticalAlignment = VerticalAlignment.Top,
                                Margin = new Thickness(0, 1, 2, 0)
                            };
                            cellGrid.Children.Add(qtyText);
                        }

                        border.Child = cellGrid;
                    }
                    else
                    {
                        ResetEquipmentSlotUi(slotId, border);
                    }
                }
            }
        }
        catch { }
    }

    private void UpdateInventorySlot(string key, string value)
    {
        // Format: INV[0]: ID, Name, Quantity
        try
        {
            int openBracket = key.IndexOf('[');
            int closeBracket = key.IndexOf(']');
            if (openBracket != -1 && closeBracket != -1)
            {
                int index = int.Parse(key.Substring(openBracket + 1, closeBracket - openBracket - 1));
                if (index >= 0 && index < 28)
                {
                    if (_lastInventoryRaw[index] == value)
                        return;
                    _lastInventoryRaw[index] = value;

                    if (_inventorySlots[index] != null)
                    {
                        if (TryParseItemPayload(value, out int id, out string name, out int qty))
                        {
                            var slotBorder = _inventorySlots[index];
                            slotBorder.Background = new SolidColorBrush(Color.FromRgb(24, 48, 76));
                            slotBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(0, 180, 216));
                            string qtyDisplay = qty > 1 ? $" (x{qty:N0})" : "";
                            string idDisplay = id > 0 ? $" [ID: {id}]" : "";
                            slotBorder.ToolTip = $"Slot {index + 1}: {name}{idDisplay}{qtyDisplay}";

                            var cellGrid = new Grid();

                            var nameText = new TextBlock
                            {
                                Text = name,
                                FontSize = 8,
                                FontWeight = FontWeights.SemiBold,
                                Foreground = Brushes.White,
                                TextWrapping = TextWrapping.Wrap,
                                TextAlignment = TextAlignment.Center,
                                HorizontalAlignment = HorizontalAlignment.Center,
                                VerticalAlignment = VerticalAlignment.Center,
                                Margin = new Thickness(2)
                            };
                            cellGrid.Children.Add(nameText);

                            if (qty > 1)
                            {
                                var qtyText = new TextBlock
                                {
                                    Text = FormatItemQuantity(qty),
                                    FontSize = 7.5,
                                    FontWeight = FontWeights.Bold,
                                    Foreground = GetQuantityBrush(qty),
                                    HorizontalAlignment = HorizontalAlignment.Right,
                                    VerticalAlignment = VerticalAlignment.Top,
                                    Margin = new Thickness(0, 1, 2, 0)
                                };
                                cellGrid.Children.Add(qtyText);
                            }

                            slotBorder.Child = cellGrid;
                        }
                        else
                        {
                            var slotBorder = _inventorySlots[index];
                            slotBorder.Background = new SolidColorBrush(Color.FromArgb(50, 30, 34, 42));
                            slotBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(50, 55, 65));
                            slotBorder.ToolTip = $"Slot {index + 1}: Empty";
                            slotBorder.Child = null;
                        }
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
            if ((_isTcpConnected || _isAgentConnected) && 
                (text.StartsWith("Scanning") || text.StartsWith("Waiting") || text.StartsWith("Detected") || text.StartsWith("Attaching") || text.Contains("blocked attach") || text.Contains("Restart RuneLite")))
            {
                return;
            }
            StatusLabel.Text = text;
            StatusLabel.Foreground = color;
        });
    }

    private void ClearLogs_Click(object sender, RoutedEventArgs e)
    {
        _dataItemsIndex.Clear();
        _dataItems.Clear();
    }

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
        Task.Run(() => FindAndAttachAnyClient());
    }

    private void FindAndAttachAnyClient()
    {
        if (_isTcpConnected || _isAgentConnected)
        {
            Dispatcher.Invoke(() => UpdateStatus("Already connected to active client.", Brushes.Lime));
            return;
        }

        try
        {
            Dispatcher.Invoke(() => UpdateStatus("Scanning for RuneLite JVM...", Brushes.Yellow));

            var candidates = FindRuneLiteCandidateProcesses();
            if (candidates.Count == 0)
            {
                Dispatcher.Invoke(() => UpdateStatus("No active game client found. Waiting...", Brushes.Orange));
                return;
            }

            bool attached = false;
            foreach (var (pid, name, title) in candidates)
            {
                Dispatcher.Invoke(() => UpdateStatus($"Attaching to {name} (PID {pid})...", Brushes.Cyan));
                if (TryAttachAgent(pid.ToString()))
                {
                    _lastAttachedPid = pid.ToString();
                    TrackRuneLiteProcess(pid);
                    attached = true;
                    Dispatcher.Invoke(() => UpdateStatus($"Attached to PID {pid}. Connecting...", Brushes.Lime));
                    break;
                }
            }

            if (!attached)
            {
                Dispatcher.Invoke(() => UpdateStatus("Attach attempt completed. Waiting for client...", Brushes.Red));
            }
        }
        catch (Exception ex)
        {
            LogMessage($"[ATTACH_BUTTON_ERROR] {ex.Message}");
            Dispatcher.Invoke(() => UpdateStatus($"Error: {ex.Message}", Brushes.Red));
        }
    }

    private void SyncAgentJar()
    {
        try
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string sourceAgent = System.IO.Path.Combine(baseDir, "agent.jar");
            if (!System.IO.File.Exists(sourceAgent))
                sourceAgent = System.IO.Path.Combine(Environment.CurrentDirectory, "agent.jar");
            
            if (!System.IO.File.Exists(sourceAgent)) return;

            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            string[] targetDirs = {
                System.IO.Path.Combine(localAppData, "RuneLite"),
                System.IO.Path.Combine(userProfile, ".runelite"),
                System.IO.Path.Combine(localAppData, "Jagex Launcher", "games", "RuneLite"),
                System.IO.Path.Combine(userProfile, ".jagexlauncher", "games", "runelite"),
                System.IO.Path.Combine(Environment.CurrentDirectory, "bin", "Release", "net9.0-windows"),
                System.IO.Path.Combine(Environment.CurrentDirectory, "bin", "Debug", "net9.0-windows")
            };

            foreach (var dir in targetDirs)
            {
                try
                {
                    if (System.IO.Directory.Exists(dir))
                    {
                        string dest = System.IO.Path.Combine(dir, "agent.jar");
                        if (System.IO.File.Exists(dest))
                        {
                            var fi = new System.IO.FileInfo(dest);
                            if (fi.IsReadOnly) fi.IsReadOnly = false;
                        }
                        System.IO.File.Copy(sourceAgent, dest, overwrite: true);
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

    private List<(int Id, string Name, string Title)> FindRuneLiteCandidateProcesses()
    {
        var list = new List<(int Id, string Name, string Title, int Priority)>();
        int currentPid = Environment.ProcessId;
        try
        {
            var processes = System.Diagnostics.Process.GetProcesses();
            foreach (var p in processes)
            {
                try
                {
                    if (p.Id == currentPid) continue;

                    string name = p.ProcessName.ToLowerInvariant();
                    // Ignore non-RuneLite tools/IDEs/browsers/launchers
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

    private void LaunchButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            SyncAgentJar();
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

            var proc = System.Diagnostics.Process.Start(psi);
            if (proc != null)
            {
                TrackRuneLiteProcess(proc.Id);
            }
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

    private static void EnsureWrapperCompiled(string wrapperExePath)
    {
        try
        {
            if (System.IO.File.Exists(wrapperExePath)) return;
            string csc = @"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe";
            if (!System.IO.File.Exists(csc))
                csc = @"C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe";
            if (!System.IO.File.Exists(csc)) return;

            string srcPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "RuneLiteWrapper.cs");
            if (!System.IO.File.Exists(srcPath))
                srcPath = System.IO.Path.Combine(Environment.CurrentDirectory, "RuneLiteWrapper.cs");

            if (System.IO.File.Exists(srcPath))
            {
                var psi = new System.Diagnostics.ProcessStartInfo(csc, $"/target:winexe /out:\"{wrapperExePath}\" /optimize+ \"{srcPath}\"")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                using (var p = System.Diagnostics.Process.Start(psi))
                {
                    p?.WaitForExit();
                }
            }
        }
        catch { }
    }

    private void InstallJagexLauncherHook()
    {
        try
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            string[] runeLiteDirs = {
                System.IO.Path.Combine(localAppData, "RuneLite"),
                System.IO.Path.Combine(localAppData, "Jagex Launcher", "games", "RuneLite"),
                System.IO.Path.Combine(userProfile, ".jagexlauncher", "games", "runelite"),
                System.IO.Path.Combine(programFiles, "RuneLite")
            };

            string agentSource = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "agent.jar");
            if (!System.IO.File.Exists(agentSource))
                agentSource = System.IO.Path.Combine(Environment.CurrentDirectory, "agent.jar");

            string wrapperExe = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "RuneLiteWrapper.exe");
            if (!System.IO.File.Exists(wrapperExe))
                wrapperExe = System.IO.Path.Combine(Environment.CurrentDirectory, "RuneLiteWrapper.exe");

            if (!System.IO.File.Exists(wrapperExe))
            {
                EnsureWrapperCompiled(wrapperExe);
            }

            foreach (var dir in runeLiteDirs)
            {
                if (!System.IO.Directory.Exists(dir)) continue;

                string targetExe = System.IO.Path.Combine(dir, "RuneLite.exe");
                string realExe = System.IO.Path.Combine(dir, "RuneLite_real.exe");
                string targetAgent = System.IO.Path.Combine(dir, "agent.jar");

                // 1. Sync agent.jar into the RuneLite directory
                if (System.IO.File.Exists(agentSource))
                {
                    try
                    {
                        if (!System.IO.File.Exists(targetAgent) || 
                            new System.IO.FileInfo(agentSource).Length != new System.IO.FileInfo(targetAgent).Length)
                        {
                            System.IO.File.Copy(agentSource, targetAgent, true);
                            LogMessage($"[HOOK] Synced agent.jar into {dir}");
                        }
                    }
                    catch (Exception ex)
                    {
                        LogMessage($"[HOOK_WARN] Could not copy agent.jar to {dir}: {ex.Message}");
                    }
                }

                // 2. Install wrapper executable
                if (System.IO.File.Exists(targetExe) && System.IO.File.Exists(wrapperExe))
                {
                    try
                    {
                        long wrapperLength = new System.IO.FileInfo(wrapperExe).Length;
                        long targetLength = new System.IO.FileInfo(targetExe).Length;

                        if (!System.IO.File.Exists(realExe))
                        {
                            if (targetLength != wrapperLength)
                            {
                                System.IO.File.Move(targetExe, realExe);
                                System.IO.File.Copy(wrapperExe, targetExe, true);
                                LogMessage($"[HOOK] Installed Jagex Launcher RuneLite proxy wrapper in {dir}");
                            }
                        }
                        else
                        {
                            if (targetLength != wrapperLength)
                            {
                                System.IO.File.Copy(wrapperExe, targetExe, true);
                                LogMessage($"[HOOK] Updated Jagex Launcher RuneLite proxy wrapper in {dir}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LogMessage($"[HOOK_WARN] Could not install wrapper in {dir}: {ex.Message}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            LogMessage($"[HOOK_ERROR] Failed to install Jagex Launcher hook: {ex.Message}");
        }
    }

    private void CheckAndFixRuneLiteConfig(bool silent = false)
    {
        if (_configChecked && silent) return;
        try
        {
            SyncAgentJar();
            InstallJagexLauncherHook();
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            string[] configPaths = {
                System.IO.Path.Combine(localAppData, "RuneLite", "config.json"),
                System.IO.Path.Combine(localAppData, "Jagex Launcher", "games", "RuneLite", "config.json"),
                System.IO.Path.Combine(userProfile, ".jagexlauncher", "games", "runelite", "config.json"),
                System.IO.Path.Combine(programFiles, "RuneLite", "config.json"),
                System.IO.Path.Combine(userProfile, ".runelite", "config.json")
            };
            
            // Get absolute path to agent.jar
            string agentPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "agent.jar");
            if (!System.IO.File.Exists(agentPath))
                agentPath = System.IO.Path.Combine(Environment.CurrentDirectory, "agent.jar");
            
            agentPath = System.IO.Path.GetFullPath(agentPath).Replace("\\", "/");
            string escapedPath = agentPath.Replace("/", "\\\\");
            string agentArg = $"-javaagent:{escapedPath}";
            const string disableAttachArg = "-XX:-DisableAttachMechanism";

            bool anyModified = false;

            foreach (var configPath in configPaths)
            {
                if (!System.IO.File.Exists(configPath)) continue;

                // Ensure file is writable before modifying
                var fileInfo = new System.IO.FileInfo(configPath);
                if (fileInfo.IsReadOnly)
                {
                    fileInfo.IsReadOnly = false;
                }

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
                        string injection = $"\n      \"{agentArg}\",\n      \"{disableAttachArg}\"";
                        
                        if (!Regex.IsMatch(content.Substring(insertIndex), @"^\s*\]"))
                        {
                            injection += ",";
                        }
                        
                        content = content.Insert(insertIndex, injection);
                        changed = true;
                    }
                }
                else if (!content.Contains(disableAttachArg))
                {
                    var vmArgsMatch = Regex.Match(content, "\"vmArgs\"\\s*:\\s*\\[");
                    if (vmArgsMatch.Success)
                    {
                        int insertIndex = vmArgsMatch.Index + vmArgsMatch.Length;
                        string injection = $"\n      \"{disableAttachArg}\"";
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
                    anyModified = true;
                    LogMessage($"[CONFIG] Injected agent hook and attach support into {configPath}");
                }

                // Lock config file so RuneLite / Jagex Launcher cannot overwrite our injected vmArgs
                try
                {
                    fileInfo.Refresh();
                    fileInfo.IsReadOnly = true;
                }
                catch { }
            }

            try
            {
                string toolOptions = $"-XX:-DisableAttachMechanism -javaagent:\"{agentPath.Replace('/', '\\')}\"";
                Environment.SetEnvironmentVariable("JAVA_TOOL_OPTIONS", toolOptions, EnvironmentVariableTarget.User);
                Environment.SetEnvironmentVariable("JAVA_TOOL_OPTIONS", toolOptions, EnvironmentVariableTarget.Process);
                Environment.SetEnvironmentVariable("_JAVA_OPTIONS", toolOptions, EnvironmentVariableTarget.User);
                Environment.SetEnvironmentVariable("_JAVA_OPTIONS", toolOptions, EnvironmentVariableTarget.Process);
                
                // Broadcast environment change to open windows and shells
                try
                {
                    SendMessageTimeout(HWND_BROADCAST, WM_SETTINGCHANGE, UIntPtr.Zero, "Environment", SMTO_ABORTIFHUNG, 2000, out _);
                }
                catch { }

                LogMessage($"[ENV] Configured _JAVA_OPTIONS and JAVA_TOOL_OPTIONS: {toolOptions}");
            }
            catch (Exception envEx)
            {
                LogMessage($"[ENV_WARN] Could not set environment variables: {envEx.Message}");
            }

            if (anyModified)
            {
                UpdateStatus("Config Repaired & Hooked.", Brushes.Lime);
                if (!silent)
                {
                    MessageBox.Show("RuneLite configuration has been optimized.\n\nPlease RESTART RuneLite now.", "Bridge Optimized");
                }
            }
            _configChecked = true;
        }
        catch (Exception ex)
        {
            LogMessage($"[CONFIG_ERROR] Failed to fix config: {ex.Message}");
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
                    if (_isAgentConnected || _isTcpConnected)
                    {
                        await Task.Delay(2000);
                        continue;
                    }

                    // Scan for RuneLite JVM Candidates
                    var candidates = FindRuneLiteCandidateProcesses();
                    if (candidates.Count > 0)
                    {
                        foreach (var (pid, name, title) in candidates)
                        {
                            if (_isAgentConnected || _isTcpConnected) break;

                            if (_failedPidCooldown.TryGetValue(pid, out var lastFailTime))
                            {
                                if ((DateTime.UtcNow - lastFailTime).TotalSeconds < 10)
                                {
                                    continue;
                                }
                            }

                            if (_isAgentConnected || _isTcpConnected) break;

                            Dispatcher.Invoke(() =>
                            {
                                if (!_isAgentConnected && !_isTcpConnected)
                                    UpdateStatus($"Detected JVM {name} (PID {pid}). Attaching...", Brushes.Cyan);
                            });

                            bool success = TryAttachAgent(pid.ToString());
                            if (success)
                            {
                                _lastAttachedPid = pid.ToString();
                                TrackRuneLiteProcess(pid);

                                // Wait up to 3.5s for agent socket
                                for (int i = 0; i < 7 && !_isAgentConnected && !_isTcpConnected; i++)
                                {
                                    await Task.Delay(500);
                                }
                                if (_isAgentConnected || _isTcpConnected) break;

                                // Give newly attached agent cooldown period before attempting re-attach
                                _failedPidCooldown[pid] = DateTime.UtcNow;
                            }
                            else
                            {
                                _failedPidCooldown[pid] = DateTime.UtcNow;
                            }
                        }

                        if (!_isAgentConnected && !_isTcpConnected)
                        {
                            Dispatcher.Invoke((Action)(() =>
                            {
                                if (!_isAgentConnected && !_isTcpConnected)
                                {
                                    if (candidates.Count > 0)
                                    {
                                        var first = candidates[0];
                                        UpdateStatus($"RuneLite running (PID {first.Id}) - Restart RuneLite to connect with Bridge", Brushes.Orange);
                                    }
                                    else
                                    {
                                        UpdateStatus("Scanning for active OSRS clients...", Brushes.Orange);
                                    }
                                }
                            }));
                        }
                    }
                    else
                    {
                        Dispatcher.Invoke(() =>
                        {
                            if (!_isAgentConnected && !_isTcpConnected)
                                UpdateStatus("Waiting for OSRS / RuneLite to launch...", Brushes.Yellow);
                        });
                    }
                }
                catch (Exception ex)
                {
                    LogMessage($"[AUTO_ATTACH_LOOP_ERROR] {ex.Message}");
                }

                await Task.Delay(2000);
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
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        string programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

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

        // 4. RuneLite and Jagex Launcher bundled JREs
        string[] runeLiteJres = {
            System.IO.Path.Combine(localAppData, @"RuneLite\jre\bin\java.exe"),
            System.IO.Path.Combine(localAppData, @"Jagex Launcher\games\RuneLite\jre\bin\java.exe"),
            System.IO.Path.Combine(programFiles, @"RuneLite\jre\bin\java.exe"),
            System.IO.Path.Combine(programFilesX86, @"RuneLite\jre\bin\java.exe"),
            System.IO.Path.Combine(userProfile, @".jagexlauncher\games\runelite\jre\bin\java.exe"),
            System.IO.Path.Combine(userProfile, @".runelite\jre\bin\java.exe")
        };
        foreach (var rj in runeLiteJres)
        {
            if (System.IO.File.Exists(rj)) candidatePaths.Add(rj);
        }

        // 5. Standard JDK paths
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
        SyncAgentJar();
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
            psi.EnvironmentVariables["JAVA_TOOL_OPTIONS"] = "";
            psi.EnvironmentVariables["_JAVA_OPTIONS"] = "";

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

    private void InitializeBotController()
    {
        ScriptRunner.Instance.RegisterBot(new AutoWoodcutterBot());
        ScriptRunner.Instance.RegisterBot(new AutoFisherBot());
        ScriptRunner.Instance.RegisterBot(new RooftopAgilityBot());
        ScriptRunner.Instance.RegisterBot(new AutoAlcherBot());

        BotSelectorComboBox.ItemsSource = ScriptRunner.Instance.RegisteredBots.Select(b => $"{b.Name} ({b.Category})").ToList();
        if (ScriptRunner.Instance.RegisteredBots.Count > 0)
        {
            BotSelectorComboBox.SelectedIndex = 0;
        }

        ScriptRunner.Instance.OnStatusChanged += (status) => Dispatcher.Invoke(() => UpdateBotUiStatus(status));
        ScriptRunner.Instance.OnLogMessage += (msg) => Dispatcher.Invoke(() => AppendBotConsole(msg));
        ScriptRunner.Instance.OnTick += () => Dispatcher.Invoke(() =>
        {
            BotCyclesText.Text = $"{ScriptRunner.Instance.LoopIterations} cycles";
            if (ScriptRunner.Instance.ActiveBot != null)
            {
                BotTaskText.Text = ScriptRunner.Instance.ActiveBot.StatusText;
            }
        });

        _botTimer.Interval = TimeSpan.FromSeconds(1);
        _botTimer.Tick += (s, e) =>
        {
            SkillTrackerEngine.Instance.UpdateTimerTick();
            RefreshSkillsDisplay();

            if (ScriptRunner.Instance.Status == ScriptStatus.Running)
            {
                BotRuntimeText.Text = ScriptRunner.Instance.Runtime.ToString(@"hh\:mm\:ss");
            }
        };
        _botTimer.Start();
    }

    private void BotSelectorComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        int idx = BotSelectorComboBox.SelectedIndex;
        if (idx >= 0 && idx < ScriptRunner.Instance.RegisteredBots.Count)
        {
            var bot = ScriptRunner.Instance.RegisteredBots[idx];
            if (BotTaskText != null) BotTaskText.Text = $"Selected: {bot.Name}";
            AppendBotConsole($"[Manager] Selected '{bot.Name}' v{bot.Version} ({bot.Category}) - {bot.Description}");
        }
    }

    private async void StartBotBtn_Click(object sender, RoutedEventArgs e)
    {
        int idx = BotSelectorComboBox.SelectedIndex;
        if (idx >= 0 && idx < ScriptRunner.Instance.RegisteredBots.Count)
        {
            var bot = ScriptRunner.Instance.RegisteredBots[idx];
            await ScriptRunner.Instance.StartAsync(bot);
        }
    }

    private void PauseBotBtn_Click(object sender, RoutedEventArgs e)
    {
        if (ScriptRunner.Instance.Status == ScriptStatus.Running)
        {
            ScriptRunner.Instance.Pause();
        }
        else if (ScriptRunner.Instance.Status == ScriptStatus.Paused)
        {
            ScriptRunner.Instance.Resume();
        }
    }

    private async void StopBotBtn_Click(object sender, RoutedEventArgs e)
    {
        await ScriptRunner.Instance.StopAsync();
    }

    private void ClearBotConsole_Click(object sender, RoutedEventArgs e)
    {
        BotConsoleTextBox.Text = "";
    }

    private void UpdateBotUiStatus(ScriptStatus status)
    {
        switch (status)
        {
            case ScriptStatus.Running:
                BotStatusBadge.Background = new SolidColorBrush(Color.FromRgb(20, 50, 25));
                BotStatusText.Text = "RUNNING";
                BotStatusText.Foreground = new SolidColorBrush(Color.FromRgb(102, 187, 106));
                StartBotBtn.IsEnabled = false;
                PauseBotBtn.IsEnabled = true;
                PauseBotBtn.Content = "Pause";
                StopBotBtn.IsEnabled = true;
                break;
            case ScriptStatus.Paused:
                BotStatusBadge.Background = new SolidColorBrush(Color.FromRgb(50, 45, 15));
                BotStatusText.Text = "PAUSED";
                BotStatusText.Foreground = new SolidColorBrush(Color.FromRgb(255, 202, 40));
                StartBotBtn.IsEnabled = false;
                PauseBotBtn.IsEnabled = true;
                PauseBotBtn.Content = "Resume";
                StopBotBtn.IsEnabled = true;
                break;
            case ScriptStatus.Stopped:
            default:
                BotStatusBadge.Background = new SolidColorBrush(Color.FromRgb(58, 42, 42));
                BotStatusText.Text = "STOPPED";
                BotStatusText.Foreground = new SolidColorBrush(Color.FromRgb(229, 115, 115));
                StartBotBtn.IsEnabled = true;
                PauseBotBtn.IsEnabled = false;
                PauseBotBtn.Content = "Pause";
                StopBotBtn.IsEnabled = false;
                break;
        }
    }

    private void AppendBotConsole(string msg)
    {
        if (BotConsoleTextBox == null) return;
        string time = DateTime.Now.ToString("HH:mm:ss");
        BotConsoleTextBox.AppendText($"[{time}] {msg}\n");
        BotConsoleTextBox.ScrollToEnd();
    }

    private void InitializeScriptCreator()
    {
        CreatorStepsListBox.ItemsSource = _creatorSteps;
        CreatorNearbyNpcsList.ItemsSource = _creatorNearbyNpcs;
        CreatorLootTableList.ItemsSource = _creatorLootTable;
        CreatorActiveLootList.ItemsSource = _creatorActiveLootList;
        ScanCreatorNearbyNpcs();
        RefreshSavedScriptsList();

        if (_savedCustomScripts.Count > 0)
        {
            LoadScriptIntoEditor(_savedCustomScripts[0]);
        }
        else
        {
            var templates = ScriptTemplates.GetDefaultTemplates();
            if (templates.Count > 0)
            {
                LoadScriptIntoEditor(templates[0]);
            }
        }
    }

    private void RefreshSavedScriptsList(string? selectName = null)
    {
        _savedCustomScripts.Clear();
        var loaded = CustomScriptStorage.LoadAll();
        _savedCustomScripts.AddRange(loaded);

        SavedScriptsComboBox.ItemsSource = _savedCustomScripts.Select(s => s.Name).ToList();

        if (!string.IsNullOrEmpty(selectName))
        {
            int idx = _savedCustomScripts.FindIndex(s => s.Name.Equals(selectName, StringComparison.OrdinalIgnoreCase));
            if (idx >= 0) SavedScriptsComboBox.SelectedIndex = idx;
        }
        else if (_savedCustomScripts.Count > 0)
        {
            SavedScriptsComboBox.SelectedIndex = 0;
        }

        RefreshBotSelectorComboBox();
    }

    private void RefreshBotSelectorComboBox(string? selectBotName = null)
    {
        foreach (var def in _savedCustomScripts)
        {
            var existing = ScriptRunner.Instance.RegisteredBots.OfType<CustomScriptBot>().FirstOrDefault(b => b.Definition.Id == def.Id);
            if (existing != null)
            {
                ScriptRunner.Instance.UnregisterBot(existing);
            }
            ScriptRunner.Instance.RegisterBot(new CustomScriptBot(def));
        }

        int prevIdx = BotSelectorComboBox.SelectedIndex;
        BotSelectorComboBox.ItemsSource = ScriptRunner.Instance.RegisteredBots.Select(b => $"{b.Name} ({b.Category})").ToList();

        if (!string.IsNullOrEmpty(selectBotName))
        {
            int foundIdx = ScriptRunner.Instance.RegisteredBots.ToList().FindIndex(b => b.Name.Equals(selectBotName, StringComparison.OrdinalIgnoreCase));
            if (foundIdx >= 0)
            {
                BotSelectorComboBox.SelectedIndex = foundIdx;
                return;
            }
        }

        if (prevIdx >= 0 && prevIdx < ScriptRunner.Instance.RegisteredBots.Count)
        {
            BotSelectorComboBox.SelectedIndex = prevIdx;
        }
        else if (ScriptRunner.Instance.RegisteredBots.Count > 0)
        {
            BotSelectorComboBox.SelectedIndex = 0;
        }
    }

    private void LoadScriptIntoEditor(CustomScriptDefinition def)
    {
        if (CreatorScriptNameBox != null) CreatorScriptNameBox.Text = def.Name;
        if (CreatorDescBox != null) CreatorDescBox.Text = def.Description;
        if (CreatorMinDelayBox != null) CreatorMinDelayBox.Text = def.MinLoopDelayMs.ToString();
        if (CreatorMaxDelayBox != null) CreatorMaxDelayBox.Text = def.MaxLoopDelayMs.ToString();

        if (CreatorCategoryComboBox != null)
        {
            foreach (var rawItem in CreatorCategoryComboBox.Items)
            {
                string? text = rawItem is ComboBoxItem cbi ? cbi.Content?.ToString() : rawItem?.ToString();
                if (text?.Equals(def.Category, StringComparison.OrdinalIgnoreCase) == true)
                {
                    CreatorCategoryComboBox.SelectedItem = rawItem;
                    break;
                }
            }
        }

        _creatorSteps.Clear();
        foreach (var step in def.Steps)
        {
            _creatorSteps.Add(step);
        }

        if (_creatorSteps.Count > 0 && CreatorStepsListBox != null)
        {
            CreatorStepsListBox.SelectedIndex = 0;
        }
    }

    private void LoadTemplateBtn_Click(object sender, RoutedEventArgs e)
    {
        string? tplName = (CreatorTemplatesComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString();
        var templates = ScriptTemplates.GetDefaultTemplates();
        var matched = templates.FirstOrDefault(t => t.Name.Equals(tplName, StringComparison.OrdinalIgnoreCase)) ?? templates.FirstOrDefault();

        if (matched != null)
        {
            LoadScriptIntoEditor(matched);
            AppendBotConsole($"[Script Creator] Loaded template '{matched.Name}' ({matched.Steps.Count} steps)");
        }
    }

    private void CreatorStepsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CreatorStepsListBox?.SelectedItem is CustomActionStep step)
        {
            if (StepTitleBox != null) StepTitleBox.Text = step.Title;
            if (StepConditionArgBox != null) StepConditionArgBox.Text = step.ConditionArg;
            if (StepTargetNameBox != null) StepTargetNameBox.Text = step.TargetName;
            if (StepActionVerbBox != null) StepActionVerbBox.Text = step.ActionVerb;
            if (StepParam1Box != null) StepParam1Box.Text = step.Param1;
            if (StepWaitBox != null) StepWaitBox.Text = step.WaitAfterMs.ToString();

            int condIdx = (int)step.Condition;
            if (StepConditionComboBox != null && condIdx >= 0 && condIdx < StepConditionComboBox.Items.Count)
            {
                StepConditionComboBox.SelectedIndex = condIdx;
            }

            int actIdx = (int)step.ActionType;
            if (StepActionTypeComboBox != null && actIdx >= 0 && actIdx < StepActionTypeComboBox.Items.Count)
            {
                StepActionTypeComboBox.SelectedIndex = actIdx;
            }
        }
    }

    private void StepActionTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (StepTargetLabel == null || StepVerbLabel == null || StepParam1Label == null || StepTargetNameBox == null || StepActionVerbBox == null || StepParam1Box == null) return;

        int idx = StepActionTypeComboBox.SelectedIndex;
        if (idx < 0) return;

        var actionType = (CustomActionType)idx;
        switch (actionType)
        {
            case CustomActionType.ChopObject:
                StepTargetLabel.Text = "Tree Name (e.g. Tree, Oak tree, Willow):";
                StepVerbLabel.Text = "Action Verb:";
                if (string.IsNullOrWhiteSpace(StepTargetNameBox.Text) || StepTargetNameBox.Text == "Tree") StepTargetNameBox.Text = "Tree";
                StepActionVerbBox.Text = "Chop down";
                StepParam1Label.Text = "(Unused):";
                break;

            case CustomActionType.MineObject:
                StepTargetLabel.Text = "Rock Name (e.g. Iron rocks, Copper rocks):";
                StepVerbLabel.Text = "Action Verb:";
                StepActionVerbBox.Text = "Mine";
                StepParam1Label.Text = "(Unused):";
                break;

            case CustomActionType.ClickObject:
                StepTargetLabel.Text = "Object Name (e.g. Door, Ladder, Altar):";
                StepVerbLabel.Text = "Action Verb (e.g. Open, Climb-up, Pray):";
                StepParam1Label.Text = "(Unused):";
                break;

            case CustomActionType.AttackNpc:
                StepTargetLabel.Text = "Target NPC Name (e.g. Goblin, Guard):";
                StepVerbLabel.Text = "Action Verb:";
                StepActionVerbBox.Text = "Attack";
                StepParam1Label.Text = "(Unused):";
                break;

            case CustomActionType.TalkNpc:
                StepTargetLabel.Text = "NPC Name (e.g. Banker, Master Farmer):";
                StepVerbLabel.Text = "Action Verb (Talk-to, Pickpocket, Bank):";
                StepActionVerbBox.Text = "Talk-to";
                StepParam1Label.Text = "(Unused):";
                break;

            case CustomActionType.DropItem:
                StepTargetLabel.Text = "Item Name to Drop (e.g. Logs, Iron ore):";
                StepVerbLabel.Text = "Action Verb:";
                StepActionVerbBox.Text = "Drop";
                StepParam1Label.Text = "(Unused):";
                break;

            case CustomActionType.DropAllExcept:
                StepTargetLabel.Text = "Keep Items (comma separated, e.g. axe,pickaxe):";
                StepVerbLabel.Text = "Action Verb:";
                StepActionVerbBox.Text = "Drop";
                StepParam1Label.Text = "(Unused):";
                break;

            case CustomActionType.EatFood:
                StepTargetLabel.Text = "Food Item Name (e.g. Trout, Lobster, Shark):";
                StepVerbLabel.Text = "Action Verb:";
                StepActionVerbBox.Text = "Eat";
                StepParam1Label.Text = "(Unused):";
                break;

            case CustomActionType.CleanHerb:
                StepTargetLabel.Text = "Grimy Herb Name (e.g. Grimy ranarr weed):";
                StepVerbLabel.Text = "Action Verb:";
                StepActionVerbBox.Text = "Clean";
                StepParam1Label.Text = "(Unused):";
                break;

            case CustomActionType.UseItemOnItem:
                StepTargetLabel.Text = "Primary Item Name (e.g. Knife, Tinderbox):";
                StepVerbLabel.Text = "Action Verb:";
                StepActionVerbBox.Text = "Use";
                StepParam1Label.Text = "Target Item Name (e.g. Logs, Oak logs):";
                break;

            case CustomActionType.LootGroundItem:
                StepTargetLabel.Text = "Ground Item Name (e.g. Mark of grace, Coins):";
                StepVerbLabel.Text = "Action Verb:";
                StepActionVerbBox.Text = "Take";
                StepParam1Label.Text = "(Unused):";
                break;

            case CustomActionType.BankDepositAll:
                StepTargetLabel.Text = "Target: (Deposit All Inventory)";
                StepVerbLabel.Text = "Action Verb:";
                StepActionVerbBox.Text = "Deposit All";
                StepParam1Label.Text = "(Unused):";
                break;

            case CustomActionType.BankDepositAllExcept:
                StepTargetLabel.Text = "Keep Items (comma separated, e.g. axe,pot):";
                StepVerbLabel.Text = "Action Verb:";
                StepActionVerbBox.Text = "Deposit";
                StepParam1Label.Text = "(Unused):";
                break;

            case CustomActionType.BankWithdrawItem:
                StepTargetLabel.Text = "Item Name to Withdraw:";
                StepVerbLabel.Text = "Action Verb:";
                StepActionVerbBox.Text = "Withdraw";
                StepParam1Label.Text = "Withdraw Quantity (e.g. 1, 5, All):";
                break;

            case CustomActionType.CloseBank:
                StepTargetLabel.Text = "Target: (Close Bank Interface)";
                StepVerbLabel.Text = "Action Verb:";
                StepActionVerbBox.Text = "Close";
                StepParam1Label.Text = "(Unused):";
                break;

            case CustomActionType.CastSpellOnItem:
                StepTargetLabel.Text = "Spell Name (e.g. High Level Alchemy):";
                StepVerbLabel.Text = "Action Verb:";
                StepActionVerbBox.Text = "Cast";
                StepParam1Label.Text = "Target Inventory Item (e.g. Yew longbow):";
                break;

            case CustomActionType.CastTeleport:
                StepTargetLabel.Text = "Teleport Name (e.g. Varrock Teleport):";
                StepVerbLabel.Text = "Action Verb:";
                StepActionVerbBox.Text = "Cast";
                StepParam1Label.Text = "(Unused):";
                break;

            case CustomActionType.RunAgilityObstacle:
                StepTargetLabel.Text = "Obstacle Name (e.g. Wall, Gap, Tightrope):";
                StepVerbLabel.Text = "Action Verb (Climb, Jump, Cross):";
                StepActionVerbBox.Text = "Climb";
                StepParam1Label.Text = "(Unused):";
                break;

            case CustomActionType.ContinueDialog:
                StepTargetLabel.Text = "Target: (Press Space / Continue Dialog)";
                StepVerbLabel.Text = "Action Verb:";
                StepActionVerbBox.Text = "Continue";
                StepParam1Label.Text = "(Unused):";
                break;

            case CustomActionType.SelectDialogOption:
                StepTargetLabel.Text = "Target: (Dialog Options)";
                StepVerbLabel.Text = "Action Verb:";
                StepActionVerbBox.Text = "Select Option";
                StepParam1Label.Text = "Option Number (1, 2, 3, 4, 5):";
                break;

            case CustomActionType.WaitSeconds:
                StepTargetLabel.Text = "Target: (Pause / Sleep Delay)";
                StepVerbLabel.Text = "Action Verb:";
                StepActionVerbBox.Text = "Wait";
                StepParam1Label.Text = "(Unused):";
                break;

            case CustomActionType.WaitForIdle:
                StepTargetLabel.Text = "Target: (Wait Until Player is Idle)";
                StepVerbLabel.Text = "Action Verb:";
                StepActionVerbBox.Text = "Wait";
                StepParam1Label.Text = "(Unused):";
                break;
        }
    }

    private void AddStepBtn_Click(object sender, RoutedEventArgs e)
    {
        var step = BuildStepFromForm();
        _creatorSteps.Add(step);
        CreatorStepsListBox.SelectedIndex = _creatorSteps.Count - 1;
        AppendBotConsole($"[Script Creator] Added step: {step.Summary}");
    }

    private void UpdateStepBtn_Click(object sender, RoutedEventArgs e)
    {
        int idx = CreatorStepsListBox.SelectedIndex;
        if (idx >= 0 && idx < _creatorSteps.Count)
        {
            var step = BuildStepFromForm();
            _creatorSteps[idx] = step;
            CreatorStepsListBox.SelectedIndex = idx;
            AppendBotConsole($"[Script Creator] Updated step {idx + 1}: {step.Summary}");
        }
        else
        {
            MessageBox.Show("Please select a step from the list to update.", "No Step Selected", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private CustomActionStep BuildStepFromForm()
    {
        int condIdx = Math.Max(0, StepConditionComboBox.SelectedIndex);
        int actIdx = Math.Max(0, StepActionTypeComboBox.SelectedIndex);
        int.TryParse(StepWaitBox.Text, out int waitMs);
        if (waitMs <= 0) waitMs = 1000;

        return new CustomActionStep
        {
            Title = string.IsNullOrWhiteSpace(StepTitleBox.Text) ? "Action Step" : StepTitleBox.Text.Trim(),
            Condition = (CustomConditionType)condIdx,
            ConditionArg = StepConditionArgBox.Text?.Trim() ?? "",
            ActionType = (CustomActionType)actIdx,
            TargetName = StepTargetNameBox.Text?.Trim() ?? "",
            ActionVerb = StepActionVerbBox.Text?.Trim() ?? "Click",
            Param1 = StepParam1Box.Text?.Trim() ?? "",
            WaitAfterMs = waitMs
        };
    }

    private void DeleteStepBtn_Click(object sender, RoutedEventArgs e)
    {
        int idx = CreatorStepsListBox.SelectedIndex;
        if (idx >= 0 && idx < _creatorSteps.Count)
        {
            _creatorSteps.RemoveAt(idx);
            if (_creatorSteps.Count > 0)
            {
                CreatorStepsListBox.SelectedIndex = Math.Min(idx, _creatorSteps.Count - 1);
            }
        }
    }

    private void MoveUpStepBtn_Click(object sender, RoutedEventArgs e)
    {
        int idx = CreatorStepsListBox.SelectedIndex;
        if (idx > 0 && idx < _creatorSteps.Count)
        {
            var item = _creatorSteps[idx];
            _creatorSteps.RemoveAt(idx);
            _creatorSteps.Insert(idx - 1, item);
            CreatorStepsListBox.SelectedIndex = idx - 1;
        }
    }

    private void MoveDownStepBtn_Click(object sender, RoutedEventArgs e)
    {
        int idx = CreatorStepsListBox.SelectedIndex;
        if (idx >= 0 && idx < _creatorSteps.Count - 1)
        {
            var item = _creatorSteps[idx];
            _creatorSteps.RemoveAt(idx);
            _creatorSteps.Insert(idx + 1, item);
            CreatorStepsListBox.SelectedIndex = idx + 1;
        }
    }

    private void ClearStepsBtn_Click(object sender, RoutedEventArgs e)
    {
        _creatorSteps.Clear();
    }

    private void SaveScriptBtn_Click(object sender, RoutedEventArgs e)
    {
        string name = CreatorScriptNameBox.Text?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(name))
        {
            MessageBox.Show("Please enter a name for your custom script.", "Script Name Required", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (_creatorSteps.Count == 0)
        {
            MessageBox.Show("Please add at least one action step to the script.", "No Steps Added", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        string category = (CreatorCategoryComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Custom";
        int.TryParse(CreatorMinDelayBox.Text, out int minDelay);
        int.TryParse(CreatorMaxDelayBox.Text, out int maxDelay);
        if (minDelay <= 0) minDelay = 600;
        if (maxDelay <= minDelay) maxDelay = minDelay + 600;

        var def = new CustomScriptDefinition
        {
            Name = name,
            Category = category,
            Description = CreatorDescBox.Text?.Trim() ?? "",
            MinLoopDelayMs = minDelay,
            MaxLoopDelayMs = maxDelay,
            Steps = _creatorSteps.ToList()
        };

        bool saved = CustomScriptStorage.Save(def);
        if (saved)
        {
            RefreshSavedScriptsList(def.Name);
            RefreshBotSelectorComboBox(def.Name);

            AppendBotConsole($"[Script Creator] Saved custom script '{def.Name}' with {def.Steps.Count} steps! Ready to run.");
            MessageBox.Show($"Script '{def.Name}' was successfully saved and registered in the Script Manager!\n\nYou can now select it in the Bot Controller tab and click 'Start Script'.", "Script Saved Successfully", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        else
        {
            MessageBox.Show("Failed to save custom script file.", "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void TestStepBtn_Click(object sender, RoutedEventArgs e)
    {
        if (CreatorStepsListBox.SelectedItem is CustomActionStep step)
        {
            AppendBotConsole($"[Script Creator] Testing step: {step.Summary}...");
            var testBot = new CustomScriptBot(new CustomScriptDefinition
            {
                Name = "Test Runner",
                Steps = new List<CustomActionStep> { step }
            });
            testBot.OnLog += (msg) => AppendBotConsole(msg);

            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await testBot.OnLoopAsync(cts.Token);
                AppendBotConsole($"[Script Creator] Finished test for '{step.Title}'.");
            }
            catch (Exception ex)
            {
                AppendBotConsole($"[Script Creator] Test error: {ex.Message}");
            }
        }
        else
        {
            MessageBox.Show("Please select a step from the list to test.", "No Step Selected", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void ExportCSharpBtn_Click(object sender, RoutedEventArgs e)
    {
        string name = CreatorScriptNameBox.Text?.Trim() ?? "CustomScript";
        string category = (CreatorCategoryComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Custom";
        int.TryParse(CreatorMinDelayBox.Text, out int minDelay);
        int.TryParse(CreatorMaxDelayBox.Text, out int maxDelay);
        if (minDelay <= 0) minDelay = 600;
        if (maxDelay <= minDelay) maxDelay = minDelay + 600;

        var def = new CustomScriptDefinition
        {
            Name = name,
            Category = category,
            Description = CreatorDescBox.Text?.Trim() ?? "",
            MinLoopDelayMs = minDelay,
            MaxLoopDelayMs = maxDelay,
            Steps = _creatorSteps.ToList()
        };

        string code = CustomScriptStorage.ExportToCSharp(def);

        var viewerWindow = new Window
        {
            Title = $"Generated C# Code - {def.Name}",
            Width = 650,
            Height = 500,
            Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)),
            WindowStartupLocation = WindowStartupLocation.CenterScreen
        };
        if (this.IsLoaded && this.IsVisible)
        {
            viewerWindow.Owner = this;
            viewerWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        }

        var grid = new Grid { Margin = new Thickness(10) };
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var textBox = new TextBox
        {
            Text = code,
            IsReadOnly = true,
            Background = new SolidColorBrush(Color.FromRgb(24, 24, 24)),
            Foreground = new SolidColorBrush(Color.FromRgb(169, 183, 198)),
            FontFamily = new FontFamily("Consolas"),
            FontSize = 11,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        Grid.SetRow(textBox, 0);
        grid.Children.Add(textBox);

        var copyBtn = new Button
        {
            Content = "Copy C# Code to Clipboard",
            Padding = new Thickness(15, 6, 15, 6),
            Margin = new Thickness(0, 8, 0, 0),
            Background = new SolidColorBrush(Color.FromRgb(0, 122, 204)),
            Foreground = Brushes.White,
            FontWeight = FontWeights.Bold,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        copyBtn.Click += (s, ev) =>
        {
            try
            {
                Clipboard.SetText(code);
                MessageBox.Show("C# source code copied to clipboard!", "Copied", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not copy to clipboard: {ex.Message}", "Clipboard Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        };
        Grid.SetRow(copyBtn, 1);
        grid.Children.Add(copyBtn);

        viewerWindow.Content = grid;
        viewerWindow.ShowDialog();
    }

    private void SavedScriptsComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SavedScriptsComboBox == null) return;
        int idx = SavedScriptsComboBox.SelectedIndex;
        if (idx >= 0 && _savedCustomScripts != null && idx < _savedCustomScripts.Count)
        {
            LoadScriptIntoEditor(_savedCustomScripts[idx]);
        }
    }

    private void DeleteSavedScriptBtn_Click(object sender, RoutedEventArgs e)
    {
        int idx = SavedScriptsComboBox.SelectedIndex;
        if (idx >= 0 && idx < _savedCustomScripts.Count)
        {
            var script = _savedCustomScripts[idx];
            var result = MessageBox.Show($"Are you sure you want to delete custom script '{script.Name}'?", "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                CustomScriptStorage.Delete(script.Id);
                var bot = ScriptRunner.Instance.RegisteredBots.OfType<CustomScriptBot>().FirstOrDefault(b => b.Definition.Id == script.Id);
                if (bot != null) ScriptRunner.Instance.UnregisterBot(bot);

                RefreshSavedScriptsList();
                AppendBotConsole($"[Script Creator] Deleted custom script '{script.Name}'");
            }
        }
    }

    private void PopoutCurrentScriptBtn_Click(object sender, RoutedEventArgs e)
    {
        int idx = BotSelectorComboBox.SelectedIndex;
        var bots = ScriptRunner.Instance.RegisteredBots.ToList();
        if (idx >= 0 && idx < bots.Count)
        {
            var bot = bots[idx];
            CustomScriptDefinition def;
            if (bot is CustomScriptBot csb)
            {
                def = csb.Definition;
            }
            else
            {
                def = new CustomScriptDefinition
                {
                    Name = bot.Name,
                    Category = bot.Category,
                    Description = bot.Description,
                    Author = bot.Author,
                    Version = bot.Version
                };
            }
            var popout = new ScriptPopoutWindow(def);
            if (this.IsLoaded && this.IsVisible) popout.Owner = this;
            popout.Show();
        }
        else
        {
            MessageBox.Show("Please select a script from the dropdown first.", "No Script Selected", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void OpenScriptStudioBtn_Click(object sender, RoutedEventArgs e)
    {
        var studio = new ScriptStudioWindow();
        if (this.IsLoaded && this.IsVisible) studio.Owner = this;
        studio.Show();
    }

    private void PopoutCreatorScriptBtn_Click(object sender, RoutedEventArgs e)
    {
        string name = CreatorScriptNameBox.Text?.Trim() ?? "Custom Script";
        string category = (CreatorCategoryComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Custom";
        int.TryParse(CreatorMinDelayBox.Text, out int minDelay);
        int.TryParse(CreatorMaxDelayBox.Text, out int maxDelay);
        if (minDelay <= 0) minDelay = 600;
        if (maxDelay <= minDelay) maxDelay = minDelay + 600;

        var def = new CustomScriptDefinition
        {
            Name = name,
            Category = category,
            Description = CreatorDescBox.Text?.Trim() ?? "",
            MinLoopDelayMs = minDelay,
            MaxLoopDelayMs = maxDelay,
            Steps = _creatorSteps.ToList()
        };

        var popout = new ScriptPopoutWindow(def);
        if (this.IsLoaded && this.IsVisible) popout.Owner = this;
        popout.Show();
    }

    private void OpenAiAssistantBtn_Click(object sender, RoutedEventArgs e)
    {
        AiAssistantPanel.Visibility = Visibility.Visible;
    }

    private void CloseAiAssistantBtn_Click(object sender, RoutedEventArgs e)
    {
        AiAssistantPanel.Visibility = Visibility.Collapsed;
    }

    private void CopyAiPromptBtn_Click(object sender, RoutedEventArgs e)
    {
        string prompt = AiScriptAssistant.GetAiPromptTemplate();
        try
        {
            Clipboard.SetText(prompt);
            MessageBox.Show("AI Prompt Template copied to clipboard!\n\nPaste it into ChatGPT, Claude, or any AI model to generate custom scripts.", "AI Prompt Copied", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not copy: {ex.Message}", "Clipboard Error", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void PasteAiScriptBtn_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (Clipboard.ContainsText())
            {
                AiScriptInputBox.Text = Clipboard.GetText();
            }
        }
        catch { }
    }

    private void UploadAiScriptBtn_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Script Files (*.json;*.txt;*.cs)|*.json;*.txt;*.cs|All Files (*.*)|*.*",
            Title = "Upload AI Script File"
        };
        if (dlg.ShowDialog() == true)
        {
            try
            {
                string text = File.ReadAllText(dlg.FileName);
                AiScriptInputBox.Text = text;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not read file: {ex.Message}", "File Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void ImportAiScriptAndPopoutBtn_Click(object sender, RoutedEventArgs e)
    {
        string raw = AiScriptInputBox.Text?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(raw))
        {
            MessageBox.Show("Please paste or upload AI script content into the box first.", "No Script Content", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            var def = AiScriptAssistant.ParseAiResponse(raw);
            if (def == null)
            {
                MessageBox.Show("Could not parse AI script. Please make sure the JSON format is valid.", "Import Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            CustomScriptStorage.Save(def);
            RefreshSavedScriptsList(def.Name);
            LoadScriptIntoEditor(def);
            AiAssistantPanel.Visibility = Visibility.Collapsed;

            AppendBotConsole($"[AI Assistant] Imported '{def.Name}' ({def.Steps.Count} steps)");

            var popout = new ScriptPopoutWindow(def);
            if (this.IsLoaded && this.IsVisible) popout.Owner = this;
            popout.Show();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not parse AI script:\n{ex.Message}\n\nEnsure the text contains valid JSON from the AI Prompt Template.", "Import Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void CreatorScanNearbyNpcs_Click(object sender, RoutedEventArgs e)
    {
        ScanCreatorNearbyNpcs();
        AppendBotConsole($"[Script Creator] Scanned {_creatorNearbyNpcs.Count} nearby monsters.");
    }

    private void ScanCreatorNearbyNpcs()
    {
        _creatorNearbyNpcs.Clear();
        foreach (var npc in _npcs)
        {
            if (npc != null && !string.IsNullOrWhiteSpace(npc.Name))
            {
                _creatorNearbyNpcs.Add(npc);
            }
        }

        if (_creatorNearbyNpcs.Count == 0)
        {
            _creatorNearbyNpcs.Add(new NpcItem { Name = "Goblin", Distance = "3.5m", Health = "100%", Category = "Combat" });
            _creatorNearbyNpcs.Add(new NpcItem { Name = "Guard", Distance = "6.1m", Health = "100%", Category = "Combat" });
            _creatorNearbyNpcs.Add(new NpcItem { Name = "Hill Giant", Distance = "8.9m", Health = "100%", Category = "Combat" });
        }
    }

    private void CreatorNearbyNpcsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CreatorNearbyNpcsList?.SelectedItem is NpcItem npc)
        {
            string rawName = npc.Name;
            if (!string.IsNullOrEmpty(rawName))
            {
                int idx = rawName.IndexOf('(');
                if (idx > 0) rawName = rawName.Substring(0, idx).Trim();

                RefreshCreatorLootTable(rawName);
                AppendBotConsole($"[Script Creator] Selected target monster: '{rawName}' - loaded loot table.");
            }
        }
    }

    private void RefreshCreatorLootTable(string monsterName)
    {
        _creatorLootTable.Clear();
        var drops = OsrsMonsterLootDatabase.GetLootTable(monsterName);
        foreach (var d in drops)
        {
            _creatorLootTable.Add(d);
        }
    }

    private void CreatorAddSelectedLoot_Click(object sender, RoutedEventArgs e)
    {
        var selected = CreatorLootTableList.SelectedItems.OfType<MonsterLootItem>().ToList();
        if (selected.Count == 0 && CreatorLootTableList.SelectedItem is MonsterLootItem single)
            selected.Add(single);

        int added = 0;
        foreach (var item in selected)
        {
            if (!_creatorActiveLootList.Contains(item.ItemName))
            {
                _creatorActiveLootList.Add(item.ItemName);
                added++;
            }
        }
        AppendBotConsole($"[Script Creator] Added {added} item(s) to loot list.");
    }

    private void CreatorAddHerbsLoot_Click(object sender, RoutedEventArgs e)
    {
        int added = 0;
        foreach (var item in _creatorLootTable.Where(i => i.Category.Contains("Herb", StringComparison.OrdinalIgnoreCase)))
        {
            if (!_creatorActiveLootList.Contains(item.ItemName))
            {
                _creatorActiveLootList.Add(item.ItemName);
                added++;
            }
        }
        AppendBotConsole($"[Script Creator] Added {added} herbs to loot list.");
    }

    private void CreatorAddAlwaysLoot_Click(object sender, RoutedEventArgs e)
    {
        int added = 0;
        foreach (var item in _creatorLootTable.Where(i => i.Rarity.Equals("Always", StringComparison.OrdinalIgnoreCase) || i.Category.Contains("Bones", StringComparison.OrdinalIgnoreCase)))
        {
            if (!_creatorActiveLootList.Contains(item.ItemName))
            {
                _creatorActiveLootList.Add(item.ItemName);
                added++;
            }
        }
        AppendBotConsole($"[Script Creator] Added {added} 100% item(s) to loot list.");
    }

    private void CreatorAddCustomLootItem_Click(object sender, RoutedEventArgs e)
    {
        string name = CreatorCustomLootBox.Text?.Trim() ?? "";
        if (!string.IsNullOrWhiteSpace(name) && !_creatorActiveLootList.Contains(name))
        {
            _creatorActiveLootList.Add(name);
            CreatorCustomLootBox.Text = "";
            AppendBotConsole($"[Script Creator] Added '{name}' to active loot list.");
        }
    }

    private void CreatorRemoveLootItem_Click(object sender, RoutedEventArgs e)
    {
        if (CreatorActiveLootList.SelectedItem is string name)
        {
            _creatorActiveLootList.Remove(name);
            AppendBotConsole($"[Script Creator] Removed '{name}' from active loot list.");
        }
    }

    private void CreatorApplyCombatLootToSteps_Click(object sender, RoutedEventArgs e)
    {
        string monsterName = "Goblin";
        if (CreatorNearbyNpcsList.SelectedItem is NpcItem selectedNpc && !string.IsNullOrWhiteSpace(selectedNpc.Name))
        {
            monsterName = selectedNpc.Name;
            int parenIdx = monsterName.IndexOf('(');
            if (parenIdx > 0) monsterName = monsterName.Substring(0, parenIdx).Trim();
        }

        foreach (var rawItem in CreatorCategoryComboBox.Items)
        {
            string? text = rawItem is ComboBoxItem cbi ? cbi.Content?.ToString() : rawItem?.ToString();
            if (text?.Equals("Combat", StringComparison.OrdinalIgnoreCase) == true)
            {
                CreatorCategoryComboBox.SelectedItem = rawItem;
                break;
            }
        }

        CreatorScriptNameBox.Text = $"Auto Fighter - {monsterName}";
        CreatorDescBox.Text = $"Fights {monsterName}, eats food, and loots selected items from drop table.";

        _creatorSteps.Clear();

        // 1. Eat food step
        _creatorSteps.Add(new CustomActionStep
        {
            Title = "Eat Food When Low HP",
            ActionType = CustomActionType.EatFood,
            Condition = CustomConditionType.HpBelowPercent,
            ConditionArg = "50",
            TargetName = "Trout",
            ActionVerb = "Eat",
            WaitAfterMs = 600
        });

        // 2. Loot items from active loot list
        if (_creatorActiveLootList.Count == 0)
        {
            _creatorActiveLootList.Add("Bones");
            _creatorActiveLootList.Add("Coins");
        }

        foreach (var loot in _creatorActiveLootList)
        {
            _creatorSteps.Add(new CustomActionStep
            {
                Title = $"Loot {loot}",
                ActionType = CustomActionType.LootGroundItem,
                Condition = CustomConditionType.InventoryNotFull,
                TargetName = loot,
                ActionVerb = "Take",
                WaitAfterMs = 800
            });
        }

        // 3. Attack Monster step
        _creatorSteps.Add(new CustomActionStep
        {
            Title = $"Attack {monsterName}",
            ActionType = CustomActionType.AttackNpc,
            Condition = CustomConditionType.PlayerIsIdle,
            TargetName = monsterName,
            ActionVerb = "Attack",
            WaitAfterMs = 2000
        });

        CreatorStepsListBox.SelectedIndex = 0;
        AppendBotConsole($"[Script Creator] Generated complete Auto Fighter sequence for '{monsterName}' with {_creatorActiveLootList.Count} loot items!");
    }

    private void AddQuickBankingStep_Click(object sender, RoutedEventArgs e)
    {
        string act = (QuickBankingOptionsComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Open Nearest Bank";
        CustomActionStep step;

        if (act.Contains("Open Nearest Bank", StringComparison.OrdinalIgnoreCase))
        {
            step = new CustomActionStep
            {
                Title = "Open Nearest Bank",
                ActionType = CustomActionType.OpenNearestBank,
                Condition = CustomConditionType.InventoryFull,
                TargetName = "Bank booth",
                ActionVerb = "Bank",
                WaitAfterMs = 1500
            };
        }
        else if (act.Contains("Deposit All Except", StringComparison.OrdinalIgnoreCase))
        {
            step = new CustomActionStep
            {
                Title = "Deposit All Except Tools",
                ActionType = CustomActionType.BankDepositAllExcept,
                Condition = CustomConditionType.BankIsOpen,
                TargetName = "axe,pickaxe,Trout",
                ActionVerb = "Deposit",
                WaitAfterMs = 800
            };
        }
        else if (act.Contains("Deposit Equipment", StringComparison.OrdinalIgnoreCase))
        {
            step = new CustomActionStep
            {
                Title = "Deposit Equipment",
                ActionType = CustomActionType.BankDepositEquipment,
                Condition = CustomConditionType.BankIsOpen,
                WaitAfterMs = 600
            };
        }
        else if (act.Contains("Deposit All", StringComparison.OrdinalIgnoreCase))
        {
            step = new CustomActionStep
            {
                Title = "Deposit All Items",
                ActionType = CustomActionType.BankDepositAll,
                Condition = CustomConditionType.BankIsOpen,
                WaitAfterMs = 800
            };
        }
        else if (act.Contains("Withdraw ALL", StringComparison.OrdinalIgnoreCase))
        {
            step = new CustomActionStep
            {
                Title = "Withdraw ALL Items",
                ActionType = CustomActionType.BankWithdrawAll,
                Condition = CustomConditionType.BankIsOpen,
                TargetName = "Trout",
                WaitAfterMs = 800
            };
        }
        else if (act.Contains("Withdraw All-But-1", StringComparison.OrdinalIgnoreCase))
        {
            step = new CustomActionStep
            {
                Title = "Withdraw All-But-1",
                ActionType = CustomActionType.BankWithdrawAllButOne,
                Condition = CustomConditionType.BankIsOpen,
                TargetName = "Trout",
                WaitAfterMs = 800
            };
        }
        else if (act.Contains("Withdraw Item", StringComparison.OrdinalIgnoreCase))
        {
            step = new CustomActionStep
            {
                Title = "Withdraw Item",
                ActionType = CustomActionType.BankWithdrawItem,
                Condition = CustomConditionType.BankIsOpen,
                TargetName = "Trout",
                Param1 = "10",
                WaitAfterMs = 800
            };
        }
        else
        {
            step = new CustomActionStep
            {
                Title = "Close Bank Interface",
                ActionType = CustomActionType.CloseBank,
                Condition = CustomConditionType.BankIsOpen,
                WaitAfterMs = 500
            };
        }

        _creatorSteps.Add(step);
        CreatorStepsListBox.SelectedIndex = _creatorSteps.Count - 1;
        AppendBotConsole($"[Script Creator] Added banking step: {step.Summary}");
    }

    private void InsertBankingRoutineBtn_Click(object sender, RoutedEventArgs e)
    {
        string preset = (QuickBankingPresetsComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";

        if (preset.Contains("Full Bank", StringComparison.OrdinalIgnoreCase))
        {
            _creatorSteps.Add(new CustomActionStep
            {
                Title = "Open Bank When Full",
                ActionType = CustomActionType.OpenNearestBank,
                Condition = CustomConditionType.InventoryFull,
                TargetName = "Bank booth",
                WaitAfterMs = 1500
            });
            _creatorSteps.Add(new CustomActionStep
            {
                Title = "Deposit All Items",
                ActionType = CustomActionType.BankDepositAll,
                Condition = CustomConditionType.BankIsOpen,
                WaitAfterMs = 800
            });
            _creatorSteps.Add(new CustomActionStep
            {
                Title = "Close Bank Interface",
                ActionType = CustomActionType.CloseBank,
                Condition = CustomConditionType.BankIsOpen,
                WaitAfterMs = 500
            });
        }
        else if (preset.Contains("Keep Tools", StringComparison.OrdinalIgnoreCase))
        {
            _creatorSteps.Add(new CustomActionStep
            {
                Title = "Open Bank When Full",
                ActionType = CustomActionType.OpenNearestBank,
                Condition = CustomConditionType.InventoryFull,
                TargetName = "Bank booth",
                WaitAfterMs = 1500
            });
            _creatorSteps.Add(new CustomActionStep
            {
                Title = "Deposit All Except Tools",
                ActionType = CustomActionType.BankDepositAllExcept,
                Condition = CustomConditionType.BankIsOpen,
                TargetName = "axe,pickaxe",
                WaitAfterMs = 800
            });
            _creatorSteps.Add(new CustomActionStep
            {
                Title = "Close Bank Interface",
                ActionType = CustomActionType.CloseBank,
                Condition = CustomConditionType.BankIsOpen,
                WaitAfterMs = 500
            });
        }
        else if (preset.Contains("Combat Restock", StringComparison.OrdinalIgnoreCase))
        {
            _creatorSteps.Add(new CustomActionStep
            {
                Title = "Open Bank When Full",
                ActionType = CustomActionType.OpenNearestBank,
                Condition = CustomConditionType.InventoryFull,
                TargetName = "Bank booth",
                WaitAfterMs = 1500
            });
            _creatorSteps.Add(new CustomActionStep
            {
                Title = "Deposit Loot (Except Food/Pots)",
                ActionType = CustomActionType.BankDepositAllExcept,
                Condition = CustomConditionType.BankIsOpen,
                TargetName = "Trout,Lobster,Shark,Prayer potion(4)",
                WaitAfterMs = 800
            });
            _creatorSteps.Add(new CustomActionStep
            {
                Title = "Withdraw Food (10x)",
                ActionType = CustomActionType.BankWithdrawItem,
                Condition = CustomConditionType.BankIsOpen,
                TargetName = "Trout",
                Param1 = "10",
                WaitAfterMs = 800
            });
            _creatorSteps.Add(new CustomActionStep
            {
                Title = "Close Bank Interface",
                ActionType = CustomActionType.CloseBank,
                Condition = CustomConditionType.BankIsOpen,
                WaitAfterMs = 500
            });
        }

        CreatorStepsListBox.SelectedIndex = _creatorSteps.Count - 1;
        AppendBotConsole($"[Script Creator] Inserted '{preset}' routine into steps.");
    }

    public void TrackRuneLiteProcess(int pid)
    {
        try
        {
            lock (_processTrackLock)
            {
                if (_trackedRuneLiteProcess != null && !_trackedRuneLiteProcess.HasExited && _trackedRuneLiteProcess.Id == pid)
                    return;

                try
                {
                    var proc = System.Diagnostics.Process.GetProcessById(pid);
                    _trackedRuneLiteProcess = proc;
                    proc.EnableRaisingEvents = true;
                    proc.Exited += (s, e) =>
                    {
                        LogMessage($"[LIFECYCLE] Monitored process PID {pid} exited.");
                        _trackedRuneLiteProcess = null;
                        Dispatcher.Invoke(() =>
                        {
                            if (!_isAgentConnected && !_isTcpConnected)
                            {
                                UpdateStatus("RuneLite process exited. Scanning for active client...", Brushes.Yellow);
                            }
                        });
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

    private void CloseRuneLite_Click(object sender, RoutedEventArgs e)
    {
        int killed = KillAllRuneLiteInstances();
        LogMessage($"[CLIENT_CONTROL] Terminated {killed} RuneLite / game instance(s).");
        UpdateStatus(killed > 0 ? $"Closed {killed} RuneLite instance(s)." : "No running RuneLite instances found.", Brushes.Yellow);
    }

    public static int KillAllRuneLiteInstances()
    {
        int count = 0;
        int currentPid = Environment.ProcessId;
        try
        {
            var processes = System.Diagnostics.Process.GetProcesses();
            foreach (var p in processes)
            {
                try
                {
                    if (p.Id == currentPid) continue;
                    string name = p.ProcessName.ToLowerInvariant();
                    string title = p.MainWindowTitle.ToLowerInvariant();
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
                                if (!string.IsNullOrEmpty(modulePath) && (modulePath.Contains("runelite") || modulePath.Contains(".runelite")))
                                {
                                    match = true;
                                }
                            }
                            catch { }
                        }
                    }

                    if (match)
                    {
                        p.Kill(true);
                        count++;
                    }
                }
                catch { }
            }
        }
        catch { }
        return count;
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _running = false;
        try { _botTimer.Stop(); } catch { }
        try { _ = ScriptRunner.Instance.StopAsync(); } catch { }
        try { _activeTcpClient?.Close(); } catch { }
        try { _listener?.Stop(); } catch { }
    }

    protected override void OnClosed(EventArgs e)
    {
        _running = false;
        try { _botTimer.Stop(); } catch { }
        try { _ = ScriptRunner.Instance.StopAsync(); } catch { }
        try { _activeTcpClient?.Close(); } catch { }
        try { _listener?.Stop(); } catch { }
        try { Application.Current.Shutdown(); } catch { }
        base.OnClosed(e);
        try { Environment.Exit(0); } catch { }
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
    public string Category { get; set; } = "NPC";
}

public class TreeItem
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Distance { get; set; } = "";
    public string Location { get; set; } = "";
    public string Status { get; set; } = "Available";
}

public class SceneObjectItem
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Distance { get; set; } = "";
    public string Location { get; set; } = "";
}

public class GroundItem
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Quantity { get; set; } = "";
    public string Distance { get; set; } = "";
    public string Location { get; set; } = "";
}

public class ContainerItem
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Quantity { get; set; } = "";
}

public class ShortcutItem
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string ReqLevel { get; set; } = "1";
    public string Distance { get; set; } = "";
    public string Location { get; set; } = "";
}

public class AgilityObstacleItem
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Course { get; set; } = "None";
    public string Distance { get; set; } = "";
    public string Location { get; set; } = "";
}

public class FishingSpotItem
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string SpotType { get; set; } = "";
    public string Distance { get; set; } = "";
    public string Location { get; set; } = "";
}

public class PlayerItem
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Distance { get; set; } = "";
    public string CombatLevel { get; set; } = "";
}

public class PrayerViewModel : System.ComponentModel.INotifyPropertyChanged
{
    private string _name = "";
    private bool _isActive;

    public string Name
    {
        get => _name;
        set { _name = value; OnPropertyChanged(nameof(Name)); }
    }

    public bool IsActive
    {
        get => _isActive;
        set
        {
            if (_isActive != value)
            {
                _isActive = value;
                OnPropertyChanged(nameof(IsActive));
                OnPropertyChanged(nameof(BackgroundBrush));
                OnPropertyChanged(nameof(BorderBrush));
                OnPropertyChanged(nameof(StatusBrush));
                OnPropertyChanged(nameof(TextBrush));
            }
        }
    }

    public Brush BackgroundBrush => _isActive 
        ? new SolidColorBrush(Color.FromArgb(80, 0, 180, 216)) 
        : new SolidColorBrush(Color.FromRgb(37, 37, 38));

    public Brush BorderBrush => _isActive 
        ? new SolidColorBrush(Color.FromRgb(0, 229, 255)) 
        : new SolidColorBrush(Color.FromRgb(63, 63, 70));

    public Brush StatusBrush => _isActive 
        ? new SolidColorBrush(Color.FromRgb(0, 255, 128)) 
        : new SolidColorBrush(Color.FromRgb(90, 90, 90));

    public Brush TextBrush => _isActive 
        ? Brushes.White 
        : new SolidColorBrush(Color.FromRgb(160, 160, 160));

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));
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

    public static void RegisterItem(int id, string name)
    {
        if (id > 0 && !string.IsNullOrWhiteSpace(name) && 
            !name.StartsWith("Item #", StringComparison.OrdinalIgnoreCase) &&
            !name.Equals("Empty", StringComparison.OrdinalIgnoreCase) &&
            !name.Equals("EMPTY", StringComparison.OrdinalIgnoreCase))
        {
            _items[id] = name;
            OsrsMr.Core.ItemDatabase.RegisterItem(id, name);
        }
    }

    public static string ResolveItemName(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return "";
        if (int.TryParse(input, out int id))
        {
            return GetItemName(id);
        }
        if (input.StartsWith("Item #", StringComparison.OrdinalIgnoreCase) && int.TryParse(input.AsSpan(6), out int parsedId))
        {
            var res = GetItemName(parsedId);
            if (!string.IsNullOrEmpty(res) && res != parsedId.ToString())
            {
                return res;
            }
        }
        return OsrsMr.Core.ItemDatabase.ResolveItemName(input);
    }

    public static string GetItemName(int id)
    {
        if (id <= 0 || id == 65535) return "";
        if (_items.TryGetValue(id, out var name))
        {
            return name;
        }
        var coreName = OsrsMr.Core.ItemDatabase.GetItemName(id);
        if (!string.IsNullOrEmpty(coreName)) return coreName;
        return $"Item #{id}";
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

public class GrandExchangeOfferUiItem : INotifyPropertyChanged
{
    private int _slot;
    private string _state = "Empty";
    private int _itemId;
    private string _itemName = "Empty";
    private int _price;
    private int _totalQuantity;
    private int _quantityTransferred;
    private int _spent;

    public int Slot
    {
        get => _slot;
        set { _slot = value; OnPropertyChanged(nameof(Slot)); OnPropertyChanged(nameof(SlotHeader)); }
    }

    public string SlotHeader => $"Slot {Slot + 1}";

    public string State
    {
        get => _state;
        set
        {
            _state = value;
            OnPropertyChanged(nameof(State));
            OnPropertyChanged(nameof(StatusBgBrush));
            OnPropertyChanged(nameof(StatusFgBrush));
        }
    }

    public int ItemId
    {
        get => _itemId;
        set { _itemId = value; OnPropertyChanged(nameof(ItemId)); }
    }

    public string ItemName
    {
        get => _itemName;
        set { _itemName = value; OnPropertyChanged(nameof(ItemName)); }
    }

    public int Price
    {
        get => _price;
        set { _price = value; OnPropertyChanged(nameof(Price)); OnPropertyChanged(nameof(PriceFormatted)); }
    }

    public string PriceFormatted => $"{Price:N0} gp";

    public int TotalQuantity
    {
        get => _totalQuantity;
        set
        {
            _totalQuantity = value;
            OnPropertyChanged(nameof(TotalQuantity));
            OnPropertyChanged(nameof(ProgressFormatted));
            OnPropertyChanged(nameof(ProgressPercentage));
        }
    }

    public int QuantityTransferred
    {
        get => _quantityTransferred;
        set
        {
            _quantityTransferred = value;
            OnPropertyChanged(nameof(QuantityTransferred));
            OnPropertyChanged(nameof(ProgressFormatted));
            OnPropertyChanged(nameof(ProgressPercentage));
        }
    }

    public int Spent
    {
        get => _spent;
        set { _spent = value; OnPropertyChanged(nameof(Spent)); OnPropertyChanged(nameof(SpentFormatted)); }
    }

    public string SpentFormatted => $"{Spent:N0} gp";

    public string ProgressFormatted => TotalQuantity > 0 ? $"{QuantityTransferred:N0} / {TotalQuantity:N0} ({(double)QuantityTransferred / TotalQuantity * 100:F0}%)" : "0 / 0";

    public double ProgressPercentage => TotalQuantity > 0 ? Math.Min(100.0, (double)QuantityTransferred / TotalQuantity * 100.0) : 0;

    public Brush StatusBgBrush
    {
        get
        {
            string s = State.ToUpperInvariant();
            if (s.Contains("BUYING")) return new SolidColorBrush(Color.FromRgb(15, 35, 50));
            if (s.Contains("BOUGHT") || s.Contains("SOLD")) return new SolidColorBrush(Color.FromRgb(15, 45, 25));
            if (s.Contains("SELLING")) return new SolidColorBrush(Color.FromRgb(45, 35, 10));
            if (s.Contains("CANCEL")) return new SolidColorBrush(Color.FromRgb(45, 15, 15));
            return new SolidColorBrush(Color.FromRgb(30, 30, 35));
        }
    }

    public Brush StatusFgBrush
    {
        get
        {
            string s = State.ToUpperInvariant();
            if (s.Contains("BUYING")) return new SolidColorBrush(Color.FromRgb(56, 189, 248));
            if (s.Contains("BOUGHT") || s.Contains("SOLD")) return new SolidColorBrush(Color.FromRgb(52, 211, 153));
            if (s.Contains("SELLING")) return new SolidColorBrush(Color.FromRgb(251, 191, 36));
            if (s.Contains("CANCEL")) return new SolidColorBrush(Color.FromRgb(248, 113, 113));
            return Brushes.Gray;
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public class RunePouchSlotUiItem : INotifyPropertyChanged
{
    private int _slot;
    private int _runeId;
    private string _runeName = "None";
    private int _quantity;

    public int Slot
    {
        get => _slot;
        set { _slot = value; OnPropertyChanged(nameof(Slot)); OnPropertyChanged(nameof(SlotHeader)); }
    }

    public string SlotHeader => $"Slot {Slot + 1}";

    public int RuneId
    {
        get => _runeId;
        set { _runeId = value; OnPropertyChanged(nameof(RuneId)); }
    }

    public string RuneName
    {
        get => _runeName;
        set { _runeName = value; OnPropertyChanged(nameof(RuneName)); }
    }

    public int Quantity
    {
        get => _quantity;
        set { _quantity = value; OnPropertyChanged(nameof(Quantity)); OnPropertyChanged(nameof(QuantityFormatted)); }
    }

    public string QuantityFormatted => $"{Quantity:N0}";

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
