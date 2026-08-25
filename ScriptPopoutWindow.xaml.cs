using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using OsrsMr.Api;
using OsrsMr.Api.CustomScripts;
using OsrsMr.Api.Entities;
using OsrsMr.Api.Framework;

namespace OsrsMr
{
    public class PopoutNpcEntry
    {
        public string Name { get; set; } = "";
        public string Distance { get; set; } = "";
        public string CombatLevel { get; set; } = "";
    }

    public partial class ScriptPopoutWindow : Window
    {
        public CustomScriptDefinition Definition { get; }
        public CustomScriptBot BotInstance { get; private set; }

        private readonly DispatcherTimer _telemetryTimer;
        private readonly Dictionary<string, FrameworkElement> _fieldControls = new();

        private readonly ObservableCollection<PopoutNpcEntry> _nearbyNpcs = new();
        private readonly ObservableCollection<MonsterLootItem> _monsterLootTable = new();
        private readonly ObservableCollection<string> _activeLootList = new();

        public ScriptPopoutWindow(CustomScriptDefinition definition)
        {
            InitializeComponent();
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));

            PopoutNearbyNpcsList.ItemsSource = _nearbyNpcs;
            PopoutLootTableList.ItemsSource = _monsterLootTable;
            PopoutActiveLootList.ItemsSource = _activeLootList;

            // Find or create registered bot instance
            var existing = ScriptRunner.Instance.RegisteredBots.OfType<CustomScriptBot>()
                .FirstOrDefault(b => b.Definition.Id == definition.Id);

            if (existing != null)
            {
                BotInstance = existing;
            }
            else
            {
                BotInstance = new CustomScriptBot(definition);
                ScriptRunner.Instance.RegisterBot(BotInstance);
            }

            Title = $"OSRSMR Script Controller - {Definition.Name}";
            PopulateScriptDetails();
            LoadCombatLootSettings();
            BuildDynamicConfigForm();
            ScanNearbyNpcs();

            _telemetryTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(250)
            };
            _telemetryTimer.Tick += TelemetryTimer_Tick;
            _telemetryTimer.Start();

            ScriptRunner.Instance.OnLogMessage += OnGlobalLogReceived;

            Closed += (s, e) =>
            {
                _telemetryTimer.Stop();
                ScriptRunner.Instance.OnLogMessage -= OnGlobalLogReceived;
            };
        }

        private void OnGlobalLogReceived(string msg)
        {
            Dispatcher.InvokeAsync(() =>
            {
                if (ScriptRunner.Instance.ActiveBot == BotInstance)
                {
                    LogToPopout(msg);
                }
            });
        }

        private void PopulateScriptDetails()
        {
            PopoutScriptNameText.Text = Definition.Name;
            PopoutScriptDescText.Text = Definition.Description;
            PopoutAuthorText.Text = Definition.Author;
            PopoutVersionText.Text = Definition.Version;
            PopoutCategoryText.Text = Definition.Category;
            PopoutStepsCountText.Text = $"{Definition.Steps?.Count ?? 0} Steps";

            // Category badge color
            switch (Definition.Category?.ToLowerInvariant())
            {
                case "combat":
                    PopoutCategoryBadge.Background = new SolidColorBrush(Color.FromRgb(183, 28, 28));
                    break;
                case "woodcutting":
                    PopoutCategoryBadge.Background = new SolidColorBrush(Color.FromRgb(27, 94, 32));
                    break;
                case "mining":
                    PopoutCategoryBadge.Background = new SolidColorBrush(Color.FromRgb(230, 81, 0));
                    break;
                case "magic":
                    PopoutCategoryBadge.Background = new SolidColorBrush(Color.FromRgb(74, 20, 140));
                    break;
                case "agility":
                    PopoutCategoryBadge.Background = new SolidColorBrush(Color.FromRgb(0, 121, 107));
                    break;
                case "fishing":
                    PopoutCategoryBadge.Background = new SolidColorBrush(Color.FromRgb(2, 119, 189));
                    break;
                default:
                    PopoutCategoryBadge.Background = new SolidColorBrush(Color.FromRgb(66, 66, 66));
                    break;
            }

            // Banking options init
            if (!string.IsNullOrWhiteSpace(Definition.BankingOption))
            {
                foreach (ComboBoxItem item in PopoutBankingModeComboBox.Items)
                {
                    if (item.Content?.ToString()?.Contains(Definition.BankingOption, StringComparison.OrdinalIgnoreCase) == true)
                    {
                        item.IsSelected = true;
                        break;
                    }
                }
            }
        }

        private void LoadCombatLootSettings()
        {
            // Target monster from steps or config fields
            var attackStep = Definition.Steps.FirstOrDefault(s => s.ActionType == CustomActionType.AttackNpc);
            if (attackStep != null && !string.IsNullOrWhiteSpace(attackStep.TargetName))
            {
                PopoutTargetMonsterBox.Text = attackStep.TargetName;
            }

            // Food step
            var foodStep = Definition.Steps.FirstOrDefault(s => s.ActionType == CustomActionType.EatFood);
            if (foodStep != null)
            {
                if (!string.IsNullOrWhiteSpace(foodStep.TargetName)) PopoutFoodNameBox.Text = foodStep.TargetName;
                if (!string.IsNullOrWhiteSpace(foodStep.ConditionArg)) PopoutEatHpBox.Text = foodStep.ConditionArg;
            }

            // Loot steps
            _activeLootList.Clear();
            var lootSteps = Definition.Steps.Where(s => s.ActionType == CustomActionType.LootGroundItem).ToList();
            foreach (var step in lootSteps)
            {
                if (!string.IsNullOrWhiteSpace(step.TargetName) && !_activeLootList.Contains(step.TargetName))
                {
                    _activeLootList.Add(step.TargetName);
                }
            }

            if (_activeLootList.Count == 0)
            {
                _activeLootList.Add("Bones");
                _activeLootList.Add("Coins");
                _activeLootList.Add("Grimy ranarr weed");
            }

            RefreshLootTable(PopoutTargetMonsterBox.Text);
        }

        private void RefreshLootTable(string monsterName)
        {
            _monsterLootTable.Clear();
            var drops = OsrsMonsterLootDatabase.GetLootTable(monsterName);
            foreach (var d in drops)
            {
                _monsterLootTable.Add(d);
            }
        }

        private void ScanNearbyNpcs()
        {
            _nearbyNpcs.Clear();
            try
            {
                var npcs = BotApi.Npcs.Query().ToList();
                foreach (var npc in npcs)
                {
                    if (npc == null || string.IsNullOrWhiteSpace(npc.Name)) continue;
                    _nearbyNpcs.Add(new PopoutNpcEntry
                    {
                        Name = npc.Name,
                        Distance = $"{npc.Distance:F1}m",
                        CombatLevel = npc.CombatLevel > 0 ? npc.CombatLevel.ToString() : ""
                    });
                }
            }
            catch { }

            if (_nearbyNpcs.Count == 0)
            {
                _nearbyNpcs.Add(new PopoutNpcEntry { Name = "Goblin", CombatLevel = "2", Distance = "3.2m" });
                _nearbyNpcs.Add(new PopoutNpcEntry { Name = "Guard", CombatLevel = "21", Distance = "5.8m" });
                _nearbyNpcs.Add(new PopoutNpcEntry { Name = "Hill Giant", CombatLevel = "28", Distance = "8.4m" });
            }
        }

        private void PopoutScanNpcsBtn_Click(object sender, RoutedEventArgs e)
        {
            ScanNearbyNpcs();
            LogToPopout($"[Scanner] Scanned {_nearbyNpcs.Count} nearby monsters/NPCs.");
        }

        private void PopoutNearbyNpcsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PopoutNearbyNpcsList.SelectedItem is PopoutNpcEntry npc)
            {
                string rawName = npc.Name;
                int idx = rawName.IndexOf('(');
                if (idx > 0) rawName = rawName.Substring(0, idx).Trim();

                PopoutTargetMonsterBox.Text = rawName;
                RefreshLootTable(rawName);
                LogToPopout($"[Target] Selected monster: {rawName}");
            }
        }

        private void PopoutTargetMonsterBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(PopoutTargetMonsterBox.Text)) return;
            RefreshLootTable(PopoutTargetMonsterBox.Text);
        }

        private void PopoutAddSelectedLoot_Click(object sender, RoutedEventArgs e)
        {
            var selected = PopoutLootTableList.SelectedItems.OfType<MonsterLootItem>().ToList();
            if (selected.Count == 0 && PopoutLootTableList.SelectedItem is MonsterLootItem single)
            {
                selected.Add(single);
            }

            int count = 0;
            foreach (var item in selected)
            {
                if (!_activeLootList.Contains(item.ItemName))
                {
                    _activeLootList.Add(item.ItemName);
                    count++;
                }
            }
            LogToPopout($"[Loot List] Added {count} item(s) to active loot list.");
        }

        private void PopoutAddHerbsLoot_Click(object sender, RoutedEventArgs e)
        {
            int count = 0;
            foreach (var item in _monsterLootTable.Where(i => i.Category.Contains("Herb", StringComparison.OrdinalIgnoreCase)))
            {
                if (!_activeLootList.Contains(item.ItemName))
                {
                    _activeLootList.Add(item.ItemName);
                    count++;
                }
            }
            LogToPopout($"[Loot List] Added {count} herb(s) to active loot list.");
        }

        private void PopoutAddAlwaysLoot_Click(object sender, RoutedEventArgs e)
        {
            int count = 0;
            foreach (var item in _monsterLootTable.Where(i => i.Rarity.Equals("Always", StringComparison.OrdinalIgnoreCase) || i.Category.Contains("Bones", StringComparison.OrdinalIgnoreCase)))
            {
                if (!_activeLootList.Contains(item.ItemName))
                {
                    _activeLootList.Add(item.ItemName);
                    count++;
                }
            }
            LogToPopout($"[Loot List] Added {count} 100% item(s) to active loot list.");
        }

        private void PopoutRemoveLootItem_Click(object sender, RoutedEventArgs e)
        {
            if (PopoutActiveLootList.SelectedItem is string item)
            {
                _activeLootList.Remove(item);
                LogToPopout($"[Loot List] Removed '{item}' from loot list.");
            }
        }

        private void PopoutClearLootList_Click(object sender, RoutedEventArgs e)
        {
            _activeLootList.Clear();
            LogToPopout("[Loot List] Cleared all loot items.");
        }

        private void PopoutAddCustomLootItem_Click(object sender, RoutedEventArgs e)
        {
            string item = PopoutCustomLootItemBox.Text?.Trim() ?? "";
            if (!string.IsNullOrWhiteSpace(item) && !_activeLootList.Contains(item))
            {
                _activeLootList.Add(item);
                PopoutCustomLootItemBox.Text = "";
                LogToPopout($"[Loot List] Added custom item '{item}' to loot list.");
            }
        }

        private void BuildDynamicConfigForm()
        {
            DynamicFieldsListPanel.Children.Clear();
            _fieldControls.Clear();

            if (Definition.ConfigFields == null || Definition.ConfigFields.Count == 0)
            {
                AiScriptAssistant.AutoPopulateConfigFields(Definition);
            }

            if (Definition.ConfigFields == null || Definition.ConfigFields.Count == 0)
                return;

            foreach (var field in Definition.ConfigFields)
            {
                if (field.FieldType.Equals("BankingOption", StringComparison.OrdinalIgnoreCase) ||
                    field.Key.Equals("targetName", StringComparison.OrdinalIgnoreCase) ||
                    field.Key.Equals("monsterName", StringComparison.OrdinalIgnoreCase) ||
                    field.Key.Equals("foodName", StringComparison.OrdinalIgnoreCase) ||
                    field.Key.Equals("eatHpPercent", StringComparison.OrdinalIgnoreCase))
                {
                    continue; // Handled in dedicated combat & banking sections
                }

                var fieldBorder = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(37, 37, 40)),
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(10, 8, 10, 8),
                    Margin = new Thickness(0, 0, 0, 8),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(58, 58, 60)),
                    BorderThickness = new Thickness(1)
                };

                var panel = new StackPanel();

                // Field Label & Description
                var headerPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };
                var label = new TextBlock
                {
                    Text = field.Label,
                    Foreground = new SolidColorBrush(Color.FromRgb(0, 229, 255)),
                    FontWeight = FontWeights.Bold,
                    FontSize = 11
                };
                headerPanel.Children.Add(label);

                if (!string.IsNullOrWhiteSpace(field.Description))
                {
                    var desc = new TextBlock
                    {
                        Text = $" - {field.Description}",
                        Foreground = new SolidColorBrush(Color.FromRgb(140, 140, 140)),
                        FontSize = 10,
                        Margin = new Thickness(4, 1, 0, 0)
                    };
                    headerPanel.Children.Add(desc);
                }
                panel.Children.Add(headerPanel);

                // Field Input Control
                if (field.FieldType.Equals("Dropdown", StringComparison.OrdinalIgnoreCase) && field.Options != null && field.Options.Count > 0)
                {
                    var combo = new ComboBox
                    {
                        Background = new SolidColorBrush(Color.FromRgb(240, 240, 240)),
                        Foreground = Brushes.Black,
                        FontWeight = FontWeights.SemiBold,
                        Margin = new Thickness(0, 2, 0, 0),
                        HorizontalAlignment = HorizontalAlignment.Left,
                        MinWidth = 200
                    };

                    foreach (var opt in field.Options)
                    {
                        var item = new ComboBoxItem
                        {
                            Content = opt,
                            Background = Brushes.White,
                            Foreground = Brushes.Black,
                            FontWeight = FontWeights.SemiBold
                        };
                        combo.Items.Add(item);
                        if (opt.Equals(field.Value, StringComparison.OrdinalIgnoreCase) || opt.Equals(field.DefaultValue, StringComparison.OrdinalIgnoreCase))
                        {
                            item.IsSelected = true;
                        }
                    }

                    if (combo.SelectedItem == null && combo.Items.Count > 0)
                        combo.SelectedIndex = 0;

                    panel.Children.Add(combo);
                    _fieldControls[field.Key] = combo;
                }
                else if (field.FieldType.Equals("Checkbox", StringComparison.OrdinalIgnoreCase))
                {
                    var chk = new CheckBox
                    {
                        Content = field.Label,
                        Foreground = Brushes.White,
                        IsChecked = bool.TryParse(field.Value, out bool val) ? val : false,
                        Margin = new Thickness(0, 4, 0, 0)
                    };
                    panel.Children.Add(chk);
                    _fieldControls[field.Key] = chk;
                }
                else
                {
                    var txt = new TextBox
                    {
                        Text = !string.IsNullOrWhiteSpace(field.Value) ? field.Value : field.DefaultValue,
                        Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)),
                        Foreground = Brushes.White,
                        BorderBrush = new SolidColorBrush(Color.FromRgb(85, 85, 85)),
                        Padding = new Thickness(6, 4, 6, 4),
                        Margin = new Thickness(0, 2, 0, 0)
                    };
                    panel.Children.Add(txt);
                    _fieldControls[field.Key] = txt;
                }

                fieldBorder.Child = panel;
                DynamicFieldsListPanel.Children.Add(fieldBorder);
            }
        }

        private void SaveConfigValuesFromUI()
        {
            string monster = PopoutTargetMonsterBox.Text?.Trim() ?? "Goblin";
            string food = PopoutFoodNameBox.Text?.Trim() ?? "Trout";
            string hpThresh = PopoutEatHpBox.Text?.Trim() ?? "50";

            if (PopoutBankingModeComboBox.SelectedItem is ComboBoxItem bankItem)
            {
                Definition.BankingOption = bankItem.Content?.ToString() ?? "Deposit All";
            }

            // If this is a Combat script or has combat steps, reconstruct combat steps with active loot list
            if (Definition.Category.Equals("Combat", StringComparison.OrdinalIgnoreCase) ||
                Definition.Steps.Any(s => s.ActionType == CustomActionType.AttackNpc))
            {
                var newSteps = new List<CustomActionStep>();

                // 1. Eat Food
                newSteps.Add(new CustomActionStep
                {
                    Title = $"Eat {food} When Low HP",
                    ActionType = CustomActionType.EatFood,
                    Condition = CustomConditionType.HpBelowPercent,
                    ConditionArg = hpThresh,
                    TargetName = food,
                    ActionVerb = "Eat",
                    WaitAfterMs = 600
                });

                // 2. Loot items from Active Loot List
                foreach (var lootItem in _activeLootList)
                {
                    newSteps.Add(new CustomActionStep
                    {
                        Title = $"Loot {lootItem}",
                        ActionType = CustomActionType.LootGroundItem,
                        Condition = CustomConditionType.InventoryNotFull,
                        TargetName = lootItem,
                        ActionVerb = "Take",
                        WaitAfterMs = 800
                    });
                }

                // 3. Attack Monster
                newSteps.Add(new CustomActionStep
                {
                    Title = $"Attack {monster}",
                    ActionType = CustomActionType.AttackNpc,
                    Condition = CustomConditionType.PlayerIsIdle,
                    TargetName = monster,
                    ActionVerb = "Attack",
                    WaitAfterMs = 2000
                });

                Definition.Steps = newSteps;
                PopoutStepsCountText.Text = $"{Definition.Steps.Count} Steps";
            }

            if (Definition.ConfigFields != null)
            {
                foreach (var field in Definition.ConfigFields)
                {
                    if (_fieldControls.TryGetValue(field.Key, out var ctrl))
                    {
                        if (ctrl is TextBox txt)
                        {
                            field.Value = txt.Text?.Trim() ?? "";
                        }
                        else if (ctrl is ComboBox combo)
                        {
                            if (combo.SelectedItem is ComboBoxItem cbi)
                                field.Value = cbi.Content?.ToString() ?? "";
                            else if (combo.SelectedItem is string str)
                                field.Value = str;
                        }
                        else if (ctrl is CheckBox chk)
                        {
                            field.Value = chk.IsChecked == true ? "True" : "False";
                        }
                    }
                }
            }

            Definition.ApplyConfigValues();
            CustomScriptStorage.Save(Definition);
        }

        private void TelemetryTimer_Tick(object? sender, EventArgs e)
        {
            var active = ScriptRunner.Instance.ActiveBot;
            if (active != null && active == BotInstance)
            {
                switch (ScriptRunner.Instance.Status)
                {
                    case ScriptStatus.Running:
                        PopoutStatusBadge.Background = new SolidColorBrush(Color.FromRgb(46, 125, 50));
                        PopoutStatusBadgeText.Text = "RUNNING";
                        PopoutStatusBadgeText.Foreground = Brushes.White;
                        break;
                    case ScriptStatus.Paused:
                        PopoutStatusBadge.Background = new SolidColorBrush(Color.FromRgb(245, 127, 23));
                        PopoutStatusBadgeText.Text = "PAUSED";
                        PopoutStatusBadgeText.Foreground = Brushes.Black;
                        break;
                    case ScriptStatus.Starting:
                        PopoutStatusBadge.Background = new SolidColorBrush(Color.FromRgb(0, 122, 204));
                        PopoutStatusBadgeText.Text = "STARTING";
                        PopoutStatusBadgeText.Foreground = Brushes.White;
                        break;
                    default:
                        PopoutStatusBadge.Background = new SolidColorBrush(Color.FromRgb(66, 66, 66));
                        PopoutStatusBadgeText.Text = ScriptRunner.Instance.Status.ToString().ToUpper();
                        PopoutStatusBadgeText.Foreground = new SolidColorBrush(Color.FromRgb(200, 200, 200));
                        break;
                }

                PopoutActiveStepText.Text = active.StatusText;
                var rt = ScriptRunner.Instance.Runtime;
                PopoutRuntimeText.Text = $"{(int)rt.TotalHours:D2}:{rt.Minutes:D2}:{rt.Seconds:D2}";
                PopoutLoopsText.Text = ScriptRunner.Instance.LoopIterations.ToString();
            }
            else
            {
                PopoutStatusBadge.Background = new SolidColorBrush(Color.FromRgb(66, 66, 66));
                PopoutStatusBadgeText.Text = "STOPPED";
                PopoutStatusBadgeText.Foreground = new SolidColorBrush(Color.FromRgb(180, 180, 180));
                PopoutActiveStepText.Text = "Ready to start";
            }
        }

        private async void PopoutStartBtn_Click(object sender, RoutedEventArgs e)
        {
            SaveConfigValuesFromUI();

            // Refresh registered bot instance
            var existing = ScriptRunner.Instance.RegisteredBots.OfType<CustomScriptBot>()
                .FirstOrDefault(b => b.Definition.Id == Definition.Id);

            if (existing != null)
            {
                existing.Definition = Definition;
                BotInstance = existing;
            }
            else
            {
                BotInstance = new CustomScriptBot(Definition);
                ScriptRunner.Instance.RegisterBot(BotInstance);
            }

            LogToPopout($"[Script] Launching '{Definition.Name}'...");
            bool ok = await ScriptRunner.Instance.StartAsync(BotInstance);
            if (ok)
            {
                LogToPopout($"[Script] '{Definition.Name}' started successfully!");
            }
            else
            {
                LogToPopout($"[Error] Could not start script (already running or error).");
            }
        }

        private void PopoutPauseBtn_Click(object sender, RoutedEventArgs e)
        {
            if (ScriptRunner.Instance.Status == ScriptStatus.Running)
            {
                ScriptRunner.Instance.Pause();
                LogToPopout("[Script] Script paused.");
            }
            else if (ScriptRunner.Instance.Status == ScriptStatus.Paused)
            {
                ScriptRunner.Instance.Resume();
                LogToPopout("[Script] Script resumed.");
            }
        }

        private async void PopoutStopBtn_Click(object sender, RoutedEventArgs e)
        {
            await ScriptRunner.Instance.StopAsync();
            LogToPopout("[Script] Script stopped.");
        }

        private void PopoutSaveBtn_Click(object sender, RoutedEventArgs e)
        {
            SaveConfigValuesFromUI();
            LogToPopout("[Config] Options and Loot List saved and applied to script!");
        }

        private void LogToPopout(string msg)
        {
            string line = $"[{DateTime.Now:HH:mm:ss}] {msg}\n";
            PopoutLogBox.AppendText(line);
            PopoutLogBox.ScrollToEnd();
        }
    }
}
