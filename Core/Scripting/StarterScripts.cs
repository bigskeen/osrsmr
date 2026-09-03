using System;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using OsrsMr.Core.Input;
using OsrsMr.Core.Interaction;
using OsrsMr.Core.Queries;
using OsrsMr.Core.Spatial;

namespace OsrsMr.Core.Scripting
{
    public enum MiningMethod
    {
        DropWhenFull,
        BankOres
    }

    public enum WoodcuttingMethod
    {
        DropWhenFull,
        BankLogs
    }

    public enum FishingMethod
    {
        DropWhenFull,
        BankFish
    }

    // =========================================================================
    // 1. Auto Miner Script
    // =========================================================================
    [ScriptManifest(
        name: "Auto Miner",
        author: "osrsmr",
        version: "2.0.0",
        description: "Intelligent auto miner with tool validation, power-dropping, bank navigation, and live HUD metrics.",
        category: ScriptCategory.Mining)]
    public class SampleMinerScript : LoopScript
    {
        private int _minedCount = 0;
        private int _previousOreCount = 0;

        [ScriptSetting("Rock Type", "Type of rock to mine", Order = 1, Options = new[] { "All", "Iron rocks", "Copper rocks", "Tin rocks", "Clay rocks", "Coal rocks", "Mithril rocks", "Adamantite rocks", "Runite rocks", "Rocks" })]
        public string RockType { get; set; } = "All";

        [ScriptSetting("Mining Method", "Choose whether to drop ores or bank them", Order = 2)]
        public MiningMethod Method { get; set; } = MiningMethod.DropWhenFull;

        [ScriptSetting("Bank Destination", "Target bank if banking method is selected", Order = 3)]
        public BankLocation BankDestination { get; set; } = BankLocation.Nearest;

        [ScriptSetting("Random Events", "How to handle random event NPCs that spawn", Order = 4)]
        public RandomEventHandling RandomEventsPolicy { get; set; } = RandomEventHandling.Dismiss;

        public override void OnStart()
        {
            _previousOreCount = CountTotalOres();
            Log($"Auto Miner v2.0 started. Rock: {RockType}, Method: {Method}, Bank: {BankDestination}, RandomEvents: {RandomEventsPolicy}");
        }

        private int CountTotalOres()
        {
            return Queries.Queries.Inventory.Filter(i => i.Name.EndsWith("ore", StringComparison.OrdinalIgnoreCase) || i.Name.Equals("Clay", StringComparison.OrdinalIgnoreCase) || i.Name.Equals("Coal", StringComparison.OrdinalIgnoreCase)).Count();
        }

        public override void OnPaint(DrawingContext dc)
        {
            double elapsedHours = RunningTime.TotalHours;
            int ratePerHour = elapsedHours > 0.001 ? (int)(_minedCount / elapsedHours) : 0;

            var bgBrush = new SolidColorBrush(Color.FromArgb(210, 20, 20, 20));
            var borderPen = new Pen(new SolidColorBrush(Color.FromArgb(255, 0, 229, 255)), 1.5);
            dc.DrawRoundedRectangle(bgBrush, borderPen, new Rect(15, 200, 220, 75), 6, 6);

            var typeFace = new Typeface("Segoe UI");
            var titleText = new FormattedText("⛏ Auto Miner Pro", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeFace, 12, Brushes.Cyan, 1.0);
            var ft1 = new FormattedText($"Ores Mined: {_minedCount}", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeFace, 11, Brushes.White, 1.0);
            var ft2 = new FormattedText($"Rate: {ratePerHour}/hr | Task: {CurrentTaskName}", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeFace, 10, Brushes.LimeGreen, 1.0);

            dc.DrawText(titleText, new Point(25, 208));
            dc.DrawText(ft1, new Point(25, 228));
            dc.DrawText(ft2, new Point(25, 248));
        }

        public override async Task<int> OnLoopAsync()
        {
            if (State.Player == null) return 600;

            // Track ore count increases accurately
            int currentOreCount = CountTotalOres();
            if (currentOreCount > _previousOreCount)
            {
                _minedCount += (currentOreCount - _previousOreCount);
                _previousOreCount = currentOreCount;
            }
            else if (currentOreCount < _previousOreCount)
            {
                _previousOreCount = currentOreCount;
            }

            // 1. Random Events Handling
            if (RandomEventsPolicy != RandomEventHandling.Ignore && RandomEvents.IsRandomEventPresent())
            {
                SetTask("Handling random event NPC...");
                await RandomEvents.HandleRandomEventAsync(RandomEventsPolicy);
                return Antiban.HumanDelay(1500, 2000);
            }

            // 2. Pickaxe Verification
            bool hasPickaxe = Queries.Queries.Equipment.Filter(e => e.Name.Contains("pickaxe", StringComparison.OrdinalIgnoreCase)).Any() ||
                              Queries.Queries.Inventory.Filter(i => i.Name.Contains("pickaxe", StringComparison.OrdinalIgnoreCase)).Any();
            if (!hasPickaxe)
            {
                SetTask("Warning: No pickaxe detected!");
                ReportIssue("No pickaxe found equipped or in inventory. Please equip or obtain a pickaxe.");
                return 3000;
            }

            // 3. Full Inventory Handling
            if (InventoryActions.IsFull)
            {
                if (Method == MiningMethod.DropWhenFull)
                {
                    SetTask("Dropping ores...");
                    SetAction("Power-dropping inventory ores");
                    ClearIssue();
                    await InventoryActions.DropAllExceptAsync("pickaxe", "gem bag", "hammer", "waterskin");
                    _previousOreCount = CountTotalOres();
                    return Antiban.HumanDelay(800, 1200);
                }
                else
                {
                    if (Bank.IsOpen)
                    {
                        SetTask("Depositing ores in bank...");
                        SetAction("Depositing ores in bank");
                        ClearIssue();
                        await Bank.DepositAllExceptAsync("pickaxe", "gem bag", "hammer", "waterskin");
                        _previousOreCount = CountTotalOres();
                        await Condition.SleepAsync(600, 1000);
                        await Bank.CloseAsync();
                        return Antiban.HumanDelay(800, 1200);
                    }

                    var nearbyBank = Queries.Queries.Objects
                        .Filter(o => o.Name.Contains("Bank", StringComparison.OrdinalIgnoreCase) || o.Name.Contains("Grand Exchange booth", StringComparison.OrdinalIgnoreCase))
                        .WithinDistance(8)
                        .Nearest();

                    if (nearbyBank != null)
                    {
                        SetTask("Opening bank...");
                        SetAction("Opening nearby bank booth/chest");
                        ClearIssue();
                        await Bank.OpenAsync();
                        return Antiban.HumanDelay(1000, 1500);
                    }
                    else
                    {
                        SetTask($"Walking to {BankDestination}...");
                        SetAction($"Navigating to bank ({BankDestination})");
                        ClearIssue();
                        await WebWalker.WalkToBankAsync(BankDestination);
                        return Antiban.HumanDelay(1500, 2500);
                    }
                }
            }

            // 4. Mining Animation & Idle Check
            if (State.Player.Animation != -1 && State.Player.Animation != State.Player.PoseAnimation)
            {
                SetTask("Mining rock in progress...");
                SetAction("Mining rock in progress", $"Animation: {State.Player.Animation}");
                ClearIssue();
                Antiban.MaybeMicroBreak(0.05, 300, 1000);
                return Antiban.HumanDelay(600, 900);
            }

            // 5. Find and Mine Rock
            var query = Queries.Queries.Objects.WithinDistance(14);
            if (RockType == "All" || string.IsNullOrWhiteSpace(RockType))
            {
                query = query.Filter(o => o.Name.Contains("rocks", StringComparison.OrdinalIgnoreCase) || o.Name.Contains("rock", StringComparison.OrdinalIgnoreCase) || o.Name.Equals("Rocks", StringComparison.OrdinalIgnoreCase));
            }
            else
            {
                query = query.Named(RockType);
            }

            var rock = query.Nearest();
            if (rock != null)
            {
                SetTask($"Mining {rock.Name} ({rock.Distance:F1} tiles away)");
                SetAction($"Interacting with {rock.Name}", $"Distance: {rock.Distance:F1} tiles");
                ClearIssue();
                int preCount = CountTotalOres();
                bool clicked = await rock.InteractAsync("Mine");
                if (clicked)
                {
                    // Wait until player begins mining or gets ore or rock depletes
                    await Condition.WaitAsync(() =>
                        (State.Player != null && State.Player.Animation != -1) || CountTotalOres() > preCount || rock.Distance > 15,
                        2500);
                }
                return Antiban.HumanDelay(1000, 1600);
            }

            SetTask("Searching for available rocks...");
            ReportWarning($"No available '{RockType}' rocks found within 14 tiles.");
            return Antiban.HumanDelay(800, 1200);
        }

        public override void OnStop()
        {
            Log($"Auto Miner stopped. Total ores mined: {_minedCount}");
        }
    }

    // =========================================================================
    // 2. Auto Woodcutter Script
    // =========================================================================
    [ScriptManifest(
        name: "Auto Woodcutter",
        author: "osrsmr",
        version: "2.0.0",
        description: "Chops trees with axe verification, power-dropping, bank web-walking, and live performance paint.",
        category: ScriptCategory.Woodcutting)]
    public class SampleWoodcutterScript : LoopScript
    {
        private int _choppedCount = 0;
        private int _previousLogCount = 0;

        [ScriptSetting("Tree Type", "Type of tree to chop", Order = 1, Options = new[] { "Tree", "Oak tree", "Willow tree", "Yew tree", "Magic tree", "Teak tree", "Maple tree" })]
        public string TreeType { get; set; } = "Tree";

        [ScriptSetting("Chopping Method", "Choose whether to drop logs or bank them", Order = 2)]
        public WoodcuttingMethod Method { get; set; } = WoodcuttingMethod.DropWhenFull;

        [ScriptSetting("Bank Destination", "Target bank if banking method is selected", Order = 3)]
        public BankLocation BankDestination { get; set; } = BankLocation.Nearest;

        [ScriptSetting("Random Events", "How to handle random event NPCs that spawn", Order = 4)]
        public RandomEventHandling RandomEventsPolicy { get; set; } = RandomEventHandling.Dismiss;

        public override void OnStart()
        {
            _previousLogCount = CountTotalLogs();
            Log($"Auto Woodcutter v2.0 started. Tree: {TreeType}, Method: {Method}, Bank: {BankDestination}, RandomEvents: {RandomEventsPolicy}");
        }

        private int CountTotalLogs()
        {
            return Queries.Queries.Inventory.Filter(i => i.Name.EndsWith("logs", StringComparison.OrdinalIgnoreCase) || i.Name.Equals("Logs", StringComparison.OrdinalIgnoreCase)).Count();
        }

        public override void OnPaint(DrawingContext dc)
        {
            double elapsedHours = RunningTime.TotalHours;
            int ratePerHour = elapsedHours > 0.001 ? (int)(_choppedCount / elapsedHours) : 0;

            var bgBrush = new SolidColorBrush(Color.FromArgb(210, 20, 20, 20));
            var borderPen = new Pen(new SolidColorBrush(Color.FromArgb(255, 76, 175, 80)), 1.5);
            dc.DrawRoundedRectangle(bgBrush, borderPen, new Rect(15, 200, 220, 75), 6, 6);

            var typeFace = new Typeface("Segoe UI");
            var titleText = new FormattedText("🪓 Auto Woodcutter Pro", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeFace, 12, Brushes.LimeGreen, 1.0);
            var ft1 = new FormattedText($"Logs Chopped: {_choppedCount}", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeFace, 11, Brushes.White, 1.0);
            var ft2 = new FormattedText($"Rate: {ratePerHour}/hr | Task: {CurrentTaskName}", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeFace, 10, Brushes.LightGreen, 1.0);

            dc.DrawText(titleText, new Point(25, 208));
            dc.DrawText(ft1, new Point(25, 228));
            dc.DrawText(ft2, new Point(25, 248));
        }

        public override async Task<int> OnLoopAsync()
        {
            if (State.Player == null) return 600;

            int currentLogCount = CountTotalLogs();
            if (currentLogCount > _previousLogCount)
            {
                _choppedCount += (currentLogCount - _previousLogCount);
                _previousLogCount = currentLogCount;
            }
            else if (currentLogCount < _previousLogCount)
            {
                _previousLogCount = currentLogCount;
            }

            // 1. Random Events
            if (RandomEventsPolicy != RandomEventHandling.Ignore && RandomEvents.IsRandomEventPresent())
            {
                SetTask("Handling random event NPC...");
                await RandomEvents.HandleRandomEventAsync(RandomEventsPolicy);
                return Antiban.HumanDelay(1500, 2000);
            }

            // 2. Axe Verification
            bool hasAxe = Queries.Queries.Equipment.Filter(e => e.Name.Contains("axe", StringComparison.OrdinalIgnoreCase) || e.Name.Contains("hatchet", StringComparison.OrdinalIgnoreCase)).Any() ||
                          Queries.Queries.Inventory.Filter(i => i.Name.Contains("axe", StringComparison.OrdinalIgnoreCase) || i.Name.Contains("hatchet", StringComparison.OrdinalIgnoreCase)).Any();
            if (!hasAxe)
            {
                SetTask("Warning: No axe detected!");
                ReportIssue("No woodcutting axe found equipped or in inventory. Please equip or obtain an axe.");
                return 3000;
            }

            // 3. Full Inventory Handling
            if (InventoryActions.IsFull)
            {
                if (Method == WoodcuttingMethod.DropWhenFull)
                {
                    SetTask("Dropping logs...");
                    SetAction("Power-dropping inventory logs");
                    ClearIssue();
                    await InventoryActions.DropAllExceptAsync("axe", "hatchet", "tinderbox", "bird nest", "clue scroll");
                    _previousLogCount = CountTotalLogs();
                    return Antiban.HumanDelay(800, 1200);
                }
                else
                {
                    if (Bank.IsOpen)
                    {
                        SetTask("Depositing logs in bank...");
                        SetAction("Depositing logs in bank");
                        ClearIssue();
                        await Bank.DepositAllExceptAsync("axe", "hatchet", "tinderbox");
                        _previousLogCount = CountTotalLogs();
                        await Condition.SleepAsync(600, 1000);
                        await Bank.CloseAsync();
                        return Antiban.HumanDelay(800, 1200);
                    }

                    var nearbyBank = Queries.Queries.Objects
                        .Filter(o => o.Name.Contains("Bank", StringComparison.OrdinalIgnoreCase) || o.Name.Contains("Grand Exchange booth", StringComparison.OrdinalIgnoreCase))
                        .WithinDistance(8)
                        .Nearest();

                    if (nearbyBank != null)
                    {
                        SetTask("Opening bank...");
                        SetAction("Opening nearby bank booth/chest");
                        ClearIssue();
                        await Bank.OpenAsync();
                        return Antiban.HumanDelay(1000, 1500);
                    }
                    else
                    {
                        SetTask($"Walking to {BankDestination}...");
                        SetAction($"Navigating to bank ({BankDestination})");
                        ClearIssue();
                        await WebWalker.WalkToBankAsync(BankDestination);
                        return Antiban.HumanDelay(1500, 2500);
                    }
                }
            }

            // 4. Chopping Animation Active
            if (State.Player.Animation != -1 && State.Player.Animation != State.Player.PoseAnimation)
            {
                SetTask("Chopping tree in progress...");
                SetAction($"Chopping {TreeType} tree", $"Animation: {State.Player.Animation}");
                ClearIssue();
                Antiban.MaybeMicroBreak(0.04, 300, 1200);
                return Antiban.HumanDelay(600, 900);
            }

            // 5. Find and Chop Tree
            var query = Queries.Queries.Objects.WithinDistance(15);
            if (string.IsNullOrWhiteSpace(TreeType) || TreeType.Equals("All", StringComparison.OrdinalIgnoreCase) || TreeType.Equals("Tree", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Filter(o => o.Name.Contains("tree", StringComparison.OrdinalIgnoreCase) || o.Name.Equals("Tree", StringComparison.OrdinalIgnoreCase));
            }
            else
            {
                query = query.Filter(o => o.Name.Contains(TreeType, StringComparison.OrdinalIgnoreCase));
            }

            var tree = query.Nearest();
            if (tree != null)
            {
                SetTask($"Chopping {tree.Name}");
                SetAction($"Interacting with {tree.Name}", $"Distance: {tree.Distance:F1} tiles");
                ClearIssue();
                int preCount = CountTotalLogs();
                bool clicked = await tree.InteractAsync("Chop down");
                if (clicked)
                {
                    await Condition.WaitAsync(() =>
                        (State.Player != null && State.Player.Animation != -1) || CountTotalLogs() > preCount || tree.Distance > 16,
                        2500);
                }
                return Antiban.HumanDelay(1000, 1600);
            }

            SetTask("Searching for trees...");
            ReportWarning($"No available '{TreeType}' trees found within 15 tiles.");
            return Antiban.HumanDelay(800, 1200);
        }

        public override void OnStop()
        {
            Log($"Auto Woodcutter stopped. Total logs chopped: {_choppedCount}");
        }
    }

    // =========================================================================
    // 3. Auto Combat Fighter Script
    // =========================================================================
    [ScriptManifest(
        name: "Auto Combat Fighter",
        author: "osrsmr",
        version: "2.0.0",
        description: "Engages target monsters, eats food dynamically, triggers special attacks, and uses prayer potions.",
        category: ScriptCategory.Combat)]
    public class SampleFighterScript : LoopScript
    {
        private int _killsCount = 0;

        [ScriptSetting("Target NPC Name", "Name of the target monster to attack", Order = 1)]
        public string TargetNpc { get; set; } = "Goblin";

        [ScriptSetting("Eat At HP %", "Health percentage threshold to consume food", Order = 2)]
        public int EatThresholdHp { get; set; } = 50;

        [ScriptSetting("Food Name", "Name of food item in inventory (e.g. Lobster, Shark, Salmon, Food)", Order = 3)]
        public string FoodName { get; set; } = "Lobster";

        [ScriptSetting("Use Special Attack", "Automatically trigger special attack when energy >= 50%", Order = 4)]
        public bool UseSpec { get; set; } = true;

        [ScriptSetting("Random Events", "How to handle random event NPCs that spawn", Order = 5)]
        public RandomEventHandling RandomEventsPolicy { get; set; } = RandomEventHandling.Dismiss;

        public override void OnStart()
        {
            Log($"Auto Combat Fighter v2.0 started. Target: {TargetNpc}, EatAt: {EatThresholdHp}%, Spec: {UseSpec}, RandomEvents: {RandomEventsPolicy}");
        }

        public override void OnPaint(DrawingContext dc)
        {
            double elapsedHours = RunningTime.TotalHours;
            int ratePerHour = elapsedHours > 0.001 ? (int)(_killsCount / elapsedHours) : 0;

            var bgBrush = new SolidColorBrush(Color.FromArgb(210, 20, 20, 20));
            var borderPen = new Pen(new SolidColorBrush(Color.FromArgb(255, 244, 67, 54)), 1.5);
            dc.DrawRoundedRectangle(bgBrush, borderPen, new Rect(15, 200, 220, 75), 6, 6);

            var typeFace = new Typeface("Segoe UI");
            var titleText = new FormattedText("⚔ Auto Fighter Pro", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeFace, 12, Brushes.OrangeRed, 1.0);
            var ft1 = new FormattedText($"Targets Engaged: {_killsCount}", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeFace, 11, Brushes.White, 1.0);
            var ft2 = new FormattedText($"Rate: {ratePerHour}/hr | Task: {CurrentTaskName}", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeFace, 10, Brushes.Yellow, 1.0);

            dc.DrawText(titleText, new Point(25, 208));
            dc.DrawText(ft1, new Point(25, 228));
            dc.DrawText(ft2, new Point(25, 248));
        }

        public override async Task<int> OnLoopAsync()
        {
            if (State.Player == null) return 600;

            // 1. Random Events
            if (RandomEventsPolicy != RandomEventHandling.Ignore && RandomEvents.IsRandomEventPresent())
            {
                SetTask("Handling random event NPC...");
                await RandomEvents.HandleRandomEventAsync(RandomEventsPolicy);
                return Antiban.HumanDelay(1500, 2000);
            }

            // 2. Health Monitoring & Food Consumption
            if (State.Player.MaxHp > 0)
            {
                double hpPercent = (double)State.Player.CurrentHp / State.Player.MaxHp * 100.0;
                if (hpPercent < EatThresholdHp)
                {
                    SetTask($"Low HP ({hpPercent:F0}%): Eating food...");
                    bool ate = await Combat.EatFoodAsync(FoodName);
                    if (ate)
                    {
                        Log($"Ate food to restore HP. Current HP: {State.Player.CurrentHp}/{State.Player.MaxHp}");
                        return Antiban.HumanDelay(600, 900);
                    }
                }
            }

            // 3. Prayer Restoration
            if (State.Player.CurrentPrayer < 15 && State.Player.MaxPrayer > 0)
            {
                if (InventoryActions.Contains("Prayer potion", "Super restore", "Moonlight potion"))
                {
                    SetTask("Drinking prayer potion...");
                    await Combat.DrinkPrayerPotionAsync();
                    return Antiban.HumanDelay(600, 900);
                }
            }

            // 4. Special Attack Trigger
            if (UseSpec && Combat.SpecialAttackPercent >= 50 && !Combat.IsSpecialAttackActive && (State.Player.IsInCombat || State.Player.IsFighting))
            {
                SetTask("Activating Special Attack!");
                await Combat.ToggleSpecialAttackAsync(true);
            }

            // 5. In Combat State
            if (State.Player.IsInCombat || State.Player.IsFighting || State.Player.IsInteracting)
            {
                SetTask($"Fighting {TargetNpc}...");
                SetAction($"Fighting {TargetNpc}", $"Enemy HP: {State.Player.TargetHealth}");
                ClearIssue();
                Antiban.MaybeMicroBreak(0.02, 200, 600);
                return Antiban.HumanDelay(600, 1000);
            }

            // 6. Target Acquisition & Engagement
            var query = Queries.Queries.Npcs.Alive().WithinDistance(14);
            if (!string.IsNullOrWhiteSpace(TargetNpc) && !TargetNpc.Equals("All", StringComparison.OrdinalIgnoreCase))
            {
                var names = TargetNpc.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                if (names.Length > 0)
                {
                    query = query.Filter(n => names.Any(name => n.Name.Contains(name, StringComparison.OrdinalIgnoreCase)));
                }
            }

            var enemy = query.Nearest();
            if (enemy != null)
            {
                SetTask($"Attacking {enemy.Name} (Lvl {enemy.CombatLevel})");
                SetAction($"Engaging {enemy.Name} (Lvl {enemy.CombatLevel})", $"Distance: {enemy.Distance:F1} tiles");
                ClearIssue();
                bool clicked = await enemy.InteractAsync("Attack");
                if (clicked)
                {
                    _killsCount++;
                    await Condition.WaitAsync(() => State.Player != null && (State.Player.IsInCombat || State.Player.IsInteracting), 3000);
                }
                return Antiban.HumanDelay(1000, 1600);
            }

            SetTask($"Searching for {TargetNpc}...");
            ReportWarning($"No {TargetNpc} found within 14 tiles.");
            return Antiban.HumanDelay(800, 1200);
        }

        public override void OnStop()
        {
            Log($"Auto Combat Fighter stopped. Total engagements: {_killsCount}");
        }
    }

    // =========================================================================
    // 4. Auto Fisher Script
    // =========================================================================
    [ScriptManifest(
        name: "Auto Fisher",
        author: "osrsmr",
        version: "2.0.0",
        description: "Catches fish at nearby fishing spots with configurable verbs, banking, and HUD tracking.",
        category: ScriptCategory.Fishing)]
    public class SampleFisherScript : LoopScript
    {
        private int _caughtCount = 0;
        private int _previousFishCount = 0;

        [ScriptSetting("Fishing Action", "Action verb for the spot (e.g. Net, Bait, Lure, Harpoon, Cage, Small Net)", Order = 1, Options = new[] { "Net", "Bait", "Lure", "Harpoon", "Cage", "Small Net" })]
        public string ActionVerb { get; set; } = "Net";

        [ScriptSetting("Fishing Method", "Choose whether to drop caught fish or bank them", Order = 2)]
        public FishingMethod Method { get; set; } = FishingMethod.DropWhenFull;

        [ScriptSetting("Bank Destination", "Target bank if banking method is selected", Order = 3)]
        public BankLocation BankDestination { get; set; } = BankLocation.Nearest;

        [ScriptSetting("Random Events", "How to handle random event NPCs that spawn", Order = 4)]
        public RandomEventHandling RandomEventsPolicy { get; set; } = RandomEventHandling.Dismiss;

        public override void OnStart()
        {
            _previousFishCount = CountTotalFish();
            Log($"Auto Fisher v2.0 started. Action: {ActionVerb}, Method: {Method}, Bank: {BankDestination}, RandomEvents: {RandomEventsPolicy}");
        }

        private int CountTotalFish()
        {
            return Queries.Queries.Inventory.Filter(i =>
                i.Name.Contains("Shrimp", StringComparison.OrdinalIgnoreCase) ||
                i.Name.Contains("Trout", StringComparison.OrdinalIgnoreCase) ||
                i.Name.Contains("Salmon", StringComparison.OrdinalIgnoreCase) ||
                i.Name.Contains("Lobster", StringComparison.OrdinalIgnoreCase) ||
                i.Name.Contains("Swordfish", StringComparison.OrdinalIgnoreCase) ||
                i.Name.Contains("Tuna", StringComparison.OrdinalIgnoreCase) ||
                i.Name.Contains("Shark", StringComparison.OrdinalIgnoreCase) ||
                i.Name.Contains("Herring", StringComparison.OrdinalIgnoreCase) ||
                i.Name.Contains("Anchovies", StringComparison.OrdinalIgnoreCase)).Count();
        }

        public override void OnPaint(DrawingContext dc)
        {
            double elapsedHours = RunningTime.TotalHours;
            int ratePerHour = elapsedHours > 0.001 ? (int)(_caughtCount / elapsedHours) : 0;

            var bgBrush = new SolidColorBrush(Color.FromArgb(210, 20, 20, 20));
            var borderPen = new Pen(new SolidColorBrush(Color.FromArgb(255, 33, 150, 243)), 1.5);
            dc.DrawRoundedRectangle(bgBrush, borderPen, new Rect(15, 200, 220, 75), 6, 6);

            var typeFace = new Typeface("Segoe UI");
            var titleText = new FormattedText("🐟 Auto Fisher Pro", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeFace, 12, Brushes.DodgerBlue, 1.0);
            var ft1 = new FormattedText($"Fish Caught: {_caughtCount}", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeFace, 11, Brushes.White, 1.0);
            var ft2 = new FormattedText($"Rate: {ratePerHour}/hr | Task: {CurrentTaskName}", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeFace, 10, Brushes.Cyan, 1.0);

            dc.DrawText(titleText, new Point(25, 208));
            dc.DrawText(ft1, new Point(25, 228));
            dc.DrawText(ft2, new Point(25, 248));
        }

        public override async Task<int> OnLoopAsync()
        {
            if (State.Player == null) return 600;

            int currentFish = CountTotalFish();
            if (currentFish > _previousFishCount)
            {
                _caughtCount += (currentFish - _previousFishCount);
                _previousFishCount = currentFish;
            }
            else if (currentFish < _previousFishCount)
            {
                _previousFishCount = currentFish;
            }

            // 1. Random Events
            if (RandomEventsPolicy != RandomEventHandling.Ignore && RandomEvents.IsRandomEventPresent())
            {
                SetTask("Handling random event NPC...");
                await RandomEvents.HandleRandomEventAsync(RandomEventsPolicy);
                return Antiban.HumanDelay(1500, 2000);
            }

            // 2. Full Inventory Handling
            if (InventoryActions.IsFull)
            {
                if (Method == FishingMethod.DropWhenFull)
                {
                    SetTask("Dropping caught fish...");
                    SetAction("Power-dropping caught fish");
                    ClearIssue();
                    await InventoryActions.DropAllExceptAsync("net", "rod", "harpoon", "pot", "bait", "feather", "clue scroll", "casket");
                    _previousFishCount = CountTotalFish();
                    return Antiban.HumanDelay(800, 1200);
                }
                else
                {
                    if (Bank.IsOpen)
                    {
                        SetTask("Depositing fish in bank...");
                        SetAction("Depositing fish in bank");
                        ClearIssue();
                        await Bank.DepositAllExceptAsync("net", "rod", "harpoon", "pot", "bait", "feather");
                        _previousFishCount = CountTotalFish();
                        await Condition.SleepAsync(600, 1000);
                        await Bank.CloseAsync();
                        return Antiban.HumanDelay(800, 1200);
                    }

                    var nearbyBank = Queries.Queries.Objects
                        .Filter(o => o.Name.Contains("Bank", StringComparison.OrdinalIgnoreCase) || o.Name.Contains("Grand Exchange booth", StringComparison.OrdinalIgnoreCase))
                        .WithinDistance(8)
                        .Nearest();

                    if (nearbyBank != null)
                    {
                        SetTask("Opening bank...");
                        SetAction("Opening nearby bank");
                        ClearIssue();
                        await Bank.OpenAsync();
                        return Antiban.HumanDelay(1000, 1500);
                    }
                    else
                    {
                        SetTask($"Walking to {BankDestination}...");
                        SetAction($"Navigating to bank ({BankDestination})");
                        ClearIssue();
                        await WebWalker.WalkToBankAsync(BankDestination);
                        return Antiban.HumanDelay(1500, 2500);
                    }
                }
            }

            // 3. Fishing Animation Active
            if (State.Player.Animation != -1 && State.Player.Animation != State.Player.PoseAnimation)
            {
                SetTask("Fishing in progress...");
                SetAction("Fishing in progress", $"Animation: {State.Player.Animation}");
                ClearIssue();
                Antiban.MaybeMicroBreak(0.04, 300, 1200);
                return Antiban.HumanDelay(600, 900);
            }

            // 4. Find Fishing Spot
            var spot = Queries.Queries.Npcs
                .Filter(n => n.Name.Contains("Fishing spot", StringComparison.OrdinalIgnoreCase) || n.Name.Contains("Rod Fishing spot", StringComparison.OrdinalIgnoreCase) || n.Name.Contains("Fish", StringComparison.OrdinalIgnoreCase))
                .WithinDistance(16)
                .Nearest();

            if (spot != null)
            {
                SetTask($"Fishing at {spot.Name} with '{ActionVerb}'");
                SetAction($"Interacting with {spot.Name}", $"Action: {ActionVerb}");
                ClearIssue();
                int preFish = CountTotalFish();
                bool clicked = await spot.InteractAsync(ActionVerb);
                if (clicked)
                {
                    await Condition.WaitAsync(() =>
                        (State.Player != null && State.Player.Animation != -1) || CountTotalFish() > preFish || spot.Distance > 16,
                        3000);
                }
                return Antiban.HumanDelay(1000, 1600);
            }

            SetTask("Searching for fishing spots...");
            ReportWarning($"No fishing spots found within 16 tiles.");
            return Antiban.HumanDelay(800, 1200);
        }

        public override void OnStop()
        {
            Log($"Auto Fisher stopped. Total fish caught: {_caughtCount}");
        }
    }

    // =========================================================================
    // 5. Auto High Alcher Script
    // =========================================================================
    [ScriptManifest(
        name: "Auto High Alcher",
        author: "osrsmr",
        version: "2.0.0",
        description: "Automates High Alchemy on inventory items with rune detection, Gaussian variance, and HUD tracker.",
        category: ScriptCategory.Magic)]
    public class SampleAlcherScript : LoopScript
    {
        private int _alchedCount = 0;

        [ScriptSetting("Target Item", "Item name in inventory to high alch", Order = 1)]
        public string TargetItem { get; set; } = "Yew longbow";

        [ScriptSetting("Random Events", "How to handle random event NPCs that spawn", Order = 2)]
        public RandomEventHandling RandomEventsPolicy { get; set; } = RandomEventHandling.Dismiss;

        public override void OnStart()
        {
            Log($"Auto High Alcher v2.0 started. Target item: {TargetItem}, RandomEvents: {RandomEventsPolicy}");
        }

        public override void OnPaint(DrawingContext dc)
        {
            double elapsedHours = RunningTime.TotalHours;
            int ratePerHour = elapsedHours > 0.001 ? (int)(_alchedCount / elapsedHours) : 0;

            var bgBrush = new SolidColorBrush(Color.FromArgb(210, 20, 20, 20));
            var borderPen = new Pen(new SolidColorBrush(Color.FromArgb(255, 156, 39, 176)), 1.5);
            dc.DrawRoundedRectangle(bgBrush, borderPen, new Rect(15, 200, 220, 75), 6, 6);

            var typeFace = new Typeface("Segoe UI");
            var titleText = new FormattedText("✨ Auto High Alcher Pro", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeFace, 12, Brushes.MediumPurple, 1.0);
            var ft1 = new FormattedText($"Items Alched: {_alchedCount}", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeFace, 11, Brushes.White, 1.0);
            var ft2 = new FormattedText($"Rate: {ratePerHour}/hr | Task: {CurrentTaskName}", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeFace, 10, Brushes.Violet, 1.0);

            dc.DrawText(titleText, new Point(25, 208));
            dc.DrawText(ft1, new Point(25, 228));
            dc.DrawText(ft2, new Point(25, 248));
        }

        public override async Task<int> OnLoopAsync()
        {
            if (State.Player == null) return 600;

            // 1. Random Events
            if (RandomEventsPolicy != RandomEventHandling.Ignore && RandomEvents.IsRandomEventPresent())
            {
                SetTask("Handling random event NPC...");
                await RandomEvents.HandleRandomEventAsync(RandomEventsPolicy);
                return Antiban.HumanDelay(1500, 2000);
            }

            // 2. Nature Rune Verification
            if (!InventoryActions.Contains("Nature rune"))
            {
                SetTask("Stopped: No Nature runes!");
                ReportIssue("No Nature runes found in inventory. Stopping High Alcher.");
                return -1;
            }

            // 3. Target Item Verification
            var targetItem = Queries.Queries.Inventory.Named(TargetItem).First()
                ?? Queries.Queries.Inventory.Filter(i => i.Name.Contains(TargetItem, StringComparison.OrdinalIgnoreCase)).First();

            if (targetItem == null)
            {
                SetTask($"Stopped: No {TargetItem} left!");
                ReportIssue($"No more '{TargetItem}' found in inventory. Stopping High Alcher.");
                return -1;
            }

            // 4. Cast High Alchemy
            SetTask($"Alching {targetItem.Name}...");
            SetAction($"Casting High Alchemy on {targetItem.Name}");
            ClearIssue();
            bool cast = await Magic.CastHighAlchAsync(targetItem.Name);
            if (cast)
            {
                _alchedCount++;
            }

            return Antiban.HumanDelay(1800, 2400);
        }

        public override void OnStop()
        {
            Log($"Auto High Alcher stopped. Total items alched: {_alchedCount}");
        }
    }
}
