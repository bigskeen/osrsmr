using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using OsrsMr.Core.Data;
using OsrsMr.Core.Input;
using OsrsMr.Core.Interaction;
using OsrsMr.Core.Queries;
using OsrsMr.Core.Spatial;

namespace OsrsMr.Core.Scripting
{
    // =========================================================================
    // 1. Auto Fletcher Script
    // =========================================================================
    public enum FletchingActivity
    {
        CutLogs,
        StringBows,
        TipDarts
    }

    [ScriptManifest(
        name: "Auto Fletcher AIO",
        author: "osrsmr",
        version: "2.0.0",
        description: "Automates log cutting, bow stringing, and dart/arrow tipping with smart banking and HUD tracker.",
        category: ScriptCategory.Fletching)]
    public class AutoFletcherScript : LoopScript
    {
        private int _itemsFletched = 0;
        private int _previousProductCount = 0;

        [ScriptSetting("Fletching Mode", "Activity mode to execute", Order = 1)]
        public FletchingActivity Mode { get; set; } = FletchingActivity.CutLogs;

        [ScriptSetting("Log Type", "Logs to cut (CutLogs mode)", Order = 2, Options = new[] { "Logs", "Oak logs", "Willow logs", "Maple logs", "Yew logs", "Magic logs", "Redwood logs" })]
        public string LogType { get; set; } = "Yew logs";

        [ScriptSetting("Unstrung Bow", "Unstrung bow to string (StringBows mode)", Order = 3, Options = new[] { "Shortbow (u)", "Longbow (u)", "Oak shortbow (u)", "Oak longbow (u)", "Willow shortbow (u)", "Willow longbow (u)", "Maple shortbow (u)", "Maple longbow (u)", "Yew shortbow (u)", "Yew longbow (u)", "Magic shortbow (u)", "Magic longbow (u)" })]
        public string UnstrungBow { get; set; } = "Yew longbow (u)";

        [ScriptSetting("Dart Tip", "Dart tip to feather (TipDarts mode)", Order = 4, Options = new[] { "Bronze dart tip", "Iron dart tip", "Steel dart tip", "Mithril dart tip", "Adamant dart tip", "Rune dart tip", "Dragon dart tip", "Amethyst dart tip" })]
        public string DartTip { get; set; } = "Mithril dart tip";

        [ScriptSetting("Bank Destination", "Bank location to withdraw materials", Order = 5)]
        public BankLocation BankDestination { get; set; } = BankLocation.GrandExchange;

        [ScriptSetting("Random Events", "Policy for handling random event NPCs", Order = 6)]
        public RandomEventHandling RandomEventsPolicy { get; set; } = RandomEventHandling.Dismiss;

        public override void OnStart()
        {
            Log($"Auto Fletcher AIO v2.0 started. Mode: {Mode}, Bank: {BankDestination}");
            _previousProductCount = CountProducts();
        }

        private int CountProducts()
        {
            return Mode switch
            {
                FletchingActivity.CutLogs => Queries.Queries.Inventory.Filter(i => i.Name.Contains("(u)") || i.Name.Contains("shaft")).Count(),
                FletchingActivity.StringBows => Queries.Queries.Inventory.Filter(i => (i.Name.EndsWith("bow") || i.Name.EndsWith("shortbow") || i.Name.EndsWith("longbow")) && !i.Name.Contains("(u)")).Count(),
                FletchingActivity.TipDarts => Queries.Queries.Inventory.Filter(i => i.Name.EndsWith("dart")).Count(),
                _ => 0
            };
        }

        public override void OnPaint(DrawingContext dc)
        {
            double elapsedHours = RunningTime.TotalHours;
            int ratePerHour = elapsedHours > 0.001 ? (int)(_itemsFletched / elapsedHours) : 0;

            var bgBrush = new SolidColorBrush(Color.FromArgb(215, 20, 24, 20));
            var borderPen = new Pen(new SolidColorBrush(Color.FromArgb(255, 132, 204, 22)), 1.5);
            dc.DrawRoundedRectangle(bgBrush, borderPen, new Rect(15, 200, 230, 80), 6, 6);

            var typeFace = new Typeface("Segoe UI");
            var titleText = new FormattedText("🏹 Auto Fletcher AIO", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeFace, 12, Brushes.LimeGreen, 1.0);
            var ft1 = new FormattedText($"Fletched: {_itemsFletched} | Rate: {ratePerHour}/hr", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeFace, 11, Brushes.White, 1.0);
            var ft2 = new FormattedText($"Mode: {Mode} | Task: {CurrentTaskName}", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeFace, 10, Brushes.LightGreen, 1.0);

            dc.DrawText(titleText, new Point(25, 208));
            dc.DrawText(ft1, new Point(25, 228));
            dc.DrawText(ft2, new Point(25, 248));
        }

        public override async Task<int> OnLoopAsync()
        {
            if (State.Player == null) return 600;

            int currentProducts = CountProducts();
            if (currentProducts > _previousProductCount)
            {
                _itemsFletched += (currentProducts - _previousProductCount);
                _previousProductCount = currentProducts;
            }
            else
            {
                _previousProductCount = currentProducts;
            }

            if (RandomEventsPolicy != RandomEventHandling.Ignore && RandomEvents.IsRandomEventPresent())
            {
                SetTask("Handling random event NPC...");
                await RandomEvents.HandleRandomEventAsync(RandomEventsPolicy);
                return Antiban.HumanDelay(1500, 2000);
            }

            switch (Mode)
            {
                case FletchingActivity.CutLogs:
                    return await ExecuteCutLogsAsync();
                case FletchingActivity.StringBows:
                    return await ExecuteStringBowsAsync();
                case FletchingActivity.TipDarts:
                    return await ExecuteTipDartsAsync();
                default:
                    return 1000;
            }
        }

        private async Task<int> ExecuteCutLogsAsync()
        {
            if (!InventoryActions.Contains(LogType))
            {
                SetTask("Banking for logs...");
                SetAction("Walking to Bank", BankDestination.ToString());
                ClearIssue();
                bool atBank = await WebWalker.WalkToBankAsync(BankDestination);
                if (!atBank) return Antiban.HumanDelay(600, 1000);

                if (!Bank.IsOpen)
                {
                    await Bank.OpenAsync();
                    await Condition.WaitAsync(() => Bank.IsOpen, 3000);
                }

                if (Bank.IsOpen)
                {
                    await Bank.DepositAllExceptAsync("Knife");
                    if (!InventoryActions.Contains("Knife"))
                    {
                        await Bank.WithdrawAsync("Knife", 1);
                    }
                    await Bank.WithdrawAsync(LogType, 27);
                    await Bank.CloseAsync();
                }
                return Antiban.HumanDelay(800, 1400);
            }

            if (State.Player.Animation == -1)
            {
                SetTask($"Cutting {LogType}...");
                SetAction("Using Knife on Logs", LogType);
                ClearIssue();
                await InventoryActions.UseItemOnItemAsync("Knife", LogType);
                await Condition.WaitAsync(() => Dialogs.IsMakeInterfaceOpen() || State.Player.Animation != -1, 2000);

                if (Dialogs.IsMakeInterfaceOpen())
                {
                    await Dialogs.ConfirmMakeAllAsync();
                    await Condition.WaitAsync(() => !InventoryActions.Contains(LogType) || State.Player.Animation == -1, 15000);
                }
            }
            return Antiban.HumanDelay(1200, 1800);
        }

        private async Task<int> ExecuteStringBowsAsync()
        {
            if (!InventoryActions.Contains(UnstrungBow) || !InventoryActions.Contains("Bow string"))
            {
                SetTask("Banking for strings & unstrung bows...");
                SetAction("Walking to Bank", BankDestination.ToString());
                ClearIssue();
                bool atBank = await WebWalker.WalkToBankAsync(BankDestination);
                if (!atBank) return Antiban.HumanDelay(600, 1000);

                if (!Bank.IsOpen)
                {
                    await Bank.OpenAsync();
                    await Condition.WaitAsync(() => Bank.IsOpen, 3000);
                }

                if (Bank.IsOpen)
                {
                    await Bank.DepositAllAsync();
                    await Bank.WithdrawAsync(UnstrungBow, 14);
                    await Bank.WithdrawAsync("Bow string", 14);
                    await Bank.CloseAsync();
                }
                return Antiban.HumanDelay(800, 1400);
            }

            if (State.Player.Animation == -1)
            {
                SetTask($"Stringing {UnstrungBow}...");
                SetAction("Using Bow string on Unstrung Bow", UnstrungBow);
                ClearIssue();
                await InventoryActions.UseItemOnItemAsync("Bow string", UnstrungBow);
                await Condition.WaitAsync(() => Dialogs.IsMakeInterfaceOpen() || State.Player.Animation != -1, 2000);

                if (Dialogs.IsMakeInterfaceOpen())
                {
                    await Dialogs.ConfirmMakeAllAsync();
                    await Condition.WaitAsync(() => !InventoryActions.Contains(UnstrungBow) || !InventoryActions.Contains("Bow string") || State.Player.Animation == -1, 15000);
                }
            }
            return Antiban.HumanDelay(1200, 1800);
        }

        private async Task<int> ExecuteTipDartsAsync()
        {
            if (!InventoryActions.Contains(DartTip) || !InventoryActions.Contains("Feather"))
            {
                SetTask($"Stopped: Missing {DartTip} or Feathers!");
                ReportIssue($"Missing required fletching supplies: {DartTip} or Feathers.");
                return -1;
            }

            SetTask($"Tipping {DartTip}...");
            SetAction("Fletching Feathers with Dart Tip", DartTip);
            ClearIssue();
            await InventoryActions.UseItemOnItemAsync("Feather", DartTip);
            await Condition.SleepAsync(150, 300);
            return Antiban.HumanDelay(200, 450);
        }

        public override void OnStop()
        {
            Log($"Auto Fletcher AIO stopped. Total items fletched: {_itemsFletched}");
        }
    }

    // =========================================================================
    // 2. Auto Cooker Script
    // =========================================================================
    public enum CookingHeatSource
    {
        Range,
        Fire,
        RoguesDenFire
    }

    [ScriptManifest(
        name: "Auto Cooker AIO",
        author: "osrsmr",
        version: "2.0.0",
        description: "Cooks raw fish and meats on Ranges or Fires with burnt item tracking, banking, and HUD metrics.",
        category: ScriptCategory.Cooking)]
    public class AutoCookerScript : LoopScript
    {
        private int _cookedCount = 0;
        private int _burntCount = 0;
        private int _previousBurnt = 0;

        [ScriptSetting("Raw Food", "Raw food item to cook", Order = 1, Options = new[] { "Raw shrimps", "Raw trout", "Raw salmon", "Raw tuna", "Raw lobster", "Raw swordfish", "Raw monkfish", "Raw shark", "Raw karambwan", "Raw beef", "Raw chicken" })]
        public string RawFood { get; set; } = "Raw lobster";

        [ScriptSetting("Heat Source", "Type of heating source to cook on", Order = 2)]
        public CookingHeatSource HeatSource { get; set; } = CookingHeatSource.Range;

        [ScriptSetting("Bank Destination", "Bank location to withdraw raw food", Order = 3)]
        public BankLocation BankDestination { get; set; } = BankLocation.GrandExchange;

        [ScriptSetting("Random Events", "Policy for handling random event NPCs", Order = 4)]
        public RandomEventHandling RandomEventsPolicy { get; set; } = RandomEventHandling.Dismiss;

        public override void OnStart()
        {
            Log($"Auto Cooker AIO v2.0 started. Raw food: {RawFood}, Heat: {HeatSource}, Bank: {BankDestination}");
            _previousBurnt = CountBurntFood();
        }

        private int CountBurntFood()
        {
            return Queries.Queries.Inventory.Filter(i => i.Name.StartsWith("Burnt", StringComparison.OrdinalIgnoreCase)).Count();
        }

        public override void OnPaint(DrawingContext dc)
        {
            double elapsedHours = RunningTime.TotalHours;
            int ratePerHour = elapsedHours > 0.001 ? (int)(_cookedCount / elapsedHours) : 0;
            int totalCookedOrBurnt = _cookedCount + _burntCount;
            double successRate = totalCookedOrBurnt > 0 ? (_cookedCount * 100.0 / totalCookedOrBurnt) : 100.0;

            var bgBrush = new SolidColorBrush(Color.FromArgb(215, 28, 16, 12));
            var borderPen = new Pen(new SolidColorBrush(Color.FromArgb(255, 251, 146, 60)), 1.5);
            dc.DrawRoundedRectangle(bgBrush, borderPen, new Rect(15, 200, 230, 85), 6, 6);

            var typeFace = new Typeface("Segoe UI");
            var titleText = new FormattedText("🍳 Auto Cooker AIO", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeFace, 12, Brushes.Orange, 1.0);
            var ft1 = new FormattedText($"Cooked: {_cookedCount} | Burnt: {_burntCount} ({successRate:F1}%)", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeFace, 11, Brushes.White, 1.0);
            var ft2 = new FormattedText($"Rate: {ratePerHour}/hr | Task: {CurrentTaskName}", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeFace, 10, Brushes.SandyBrown, 1.0);

            dc.DrawText(titleText, new Point(25, 208));
            dc.DrawText(ft1, new Point(25, 228));
            dc.DrawText(ft2, new Point(25, 250));
        }

        public override async Task<int> OnLoopAsync()
        {
            if (State.Player == null) return 600;

            int currentBurnt = CountBurntFood();
            if (currentBurnt > _previousBurnt)
            {
                _burntCount += (currentBurnt - _previousBurnt);
                _previousBurnt = currentBurnt;
            }
            else
            {
                _previousBurnt = currentBurnt;
            }

            if (RandomEventsPolicy != RandomEventHandling.Ignore && RandomEvents.IsRandomEventPresent())
            {
                SetTask("Handling random event NPC...");
                await RandomEvents.HandleRandomEventAsync(RandomEventsPolicy);
                return Antiban.HumanDelay(1500, 2000);
            }

            if (!InventoryActions.Contains(RawFood))
            {
                SetTask("Banking for raw food...");
                SetAction("Walking to Bank", BankDestination.ToString());
                ClearIssue();
                bool atBank = await WebWalker.WalkToBankAsync(BankDestination);
                if (!atBank) return Antiban.HumanDelay(600, 1000);

                if (!Bank.IsOpen)
                {
                    await Bank.OpenAsync();
                    await Condition.WaitAsync(() => Bank.IsOpen, 3000);
                }

                if (Bank.IsOpen)
                {
                    await Bank.DepositAllAsync();
                    bool withdrawn = await Bank.WithdrawAsync(RawFood, 28);
                    if (!withdrawn)
                    {
                        SetTask($"Stopped: No {RawFood} in bank!");
                        ReportIssue($"Bank contains no {RawFood}. Stopping cooker.");
                        return -1;
                    }
                    await Bank.CloseAsync();
                }
                return Antiban.HumanDelay(800, 1400);
            }

            if (State.Player.Animation != -1)
            {
                SetTask($"Cooking {RawFood}...");
                return Antiban.HumanDelay(1200, 1800);
            }

            string targetObjectName = HeatSource switch
            {
                CookingHeatSource.Range => "Range",
                CookingHeatSource.RoguesDenFire => "Fire",
                _ => "Fire"
            };

            var cookingObject = Queries.Queries.Objects.Named(targetObjectName, "Clay oven", "Cooking range").WithinDistance(10).Nearest();
            if (cookingObject != null)
            {
                string objName = cookingObject.Name;
                SetTask($"Using {RawFood} on {objName}...");
                SetAction($"Interacting with {objName}", $"Cooking {RawFood}");
                ClearIssue();
                int preCookCount = InventoryActions.Count(RawFood);
                await InventoryActions.UseItemOnGameObjectAsync(RawFood, cookingObject);
                await Condition.WaitAsync(() => Dialogs.IsMakeInterfaceOpen() || State.Player.Animation != -1, 3000);

                if (Dialogs.IsMakeInterfaceOpen())
                {
                    await Dialogs.ConfirmMakeAllAsync();
                    await Condition.WaitAsync(() => !InventoryActions.Contains(RawFood) || State.Player.Animation == -1, 20000);
                    int postCookCount = InventoryActions.Count(RawFood);
                    _cookedCount += Math.Max(0, preCookCount - postCookCount - _burntCount);
                }
                return Antiban.HumanDelay(1000, 1600);
            }

            SetTask($"Searching for {targetObjectName}...");
            ReportWarning($"No {targetObjectName} located nearby.");
            return Antiban.HumanDelay(1000, 1500);
        }

        public override void OnStop()
        {
            Log($"Auto Cooker AIO stopped. Total cooked: {_cookedCount}, Burnt: {_burntCount}");
        }
    }

    // =========================================================================
    // 3. Auto Smelter & Cannonballer Script
    // =========================================================================
    public enum SmeltingMode
    {
        SmeltBars,
        MakeCannonballs
    }

    public enum SmeltBarType
    {
        Bronze,
        Iron,
        Silver,
        Gold,
        Steel,
        Mithril,
        Adamantite,
        Runite
    }

    [ScriptManifest(
        name: "Auto Smelter & Cannonballer",
        author: "osrsmr",
        version: "2.0.0",
        description: "Smelts metal bars and casts cannonballs at furnaces (Edgeville, Al Kharid, Falador) with automated banking.",
        category: ScriptCategory.Smithing)]
    public class AutoSmelterScript : LoopScript
    {
        private int _barsSmelted = 0;

        [ScriptSetting("Smelting Mode", "Smelt ores into bars or cast cannonballs", Order = 1)]
        public SmeltingMode Mode { get; set; } = SmeltingMode.MakeCannonballs;

        [ScriptSetting("Bar Type", "Bar type to smelt (SmeltBars mode)", Order = 2)]
        public SmeltBarType BarType { get; set; } = SmeltBarType.Steel;

        [ScriptSetting("Bank Location", "Bank to withdraw ores or steel bars", Order = 3)]
        public BankLocation BankDestination { get; set; } = BankLocation.Edgeville;

        [ScriptSetting("Random Events", "Policy for handling random event NPCs", Order = 4)]
        public RandomEventHandling RandomEventsPolicy { get; set; } = RandomEventHandling.Dismiss;

        public override void OnStart()
        {
            Log($"Auto Smelter & Cannonballer v2.0 started. Mode: {Mode}, Bar: {BarType}, Bank: {BankDestination}");
        }

        public override void OnPaint(DrawingContext dc)
        {
            double elapsedHours = RunningTime.TotalHours;
            int ratePerHour = elapsedHours > 0.001 ? (int)(_barsSmelted / elapsedHours) : 0;

            var bgBrush = new SolidColorBrush(Color.FromArgb(215, 24, 20, 18));
            var borderPen = new Pen(new SolidColorBrush(Color.FromArgb(255, 168, 162, 158)), 1.5);
            dc.DrawRoundedRectangle(bgBrush, borderPen, new Rect(15, 200, 230, 80), 6, 6);

            var typeFace = new Typeface("Segoe UI");
            var titleText = new FormattedText("🔨 Auto Smelter Pro", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeFace, 12, Brushes.Silver, 1.0);
            string metricLabel = Mode == SmeltingMode.MakeCannonballs ? "Balls Made" : "Bars Smelted";
            var ft1 = new FormattedText($"{metricLabel}: {_barsSmelted} | Rate: {ratePerHour}/hr", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeFace, 11, Brushes.White, 1.0);
            var ft2 = new FormattedText($"Task: {CurrentTaskName}", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeFace, 10, Brushes.LightGray, 1.0);

            dc.DrawText(titleText, new Point(25, 208));
            dc.DrawText(ft1, new Point(25, 228));
            dc.DrawText(ft2, new Point(25, 248));
        }

        public override async Task<int> OnLoopAsync()
        {
            if (State.Player == null) return 600;

            if (RandomEventsPolicy != RandomEventHandling.Ignore && RandomEvents.IsRandomEventPresent())
            {
                SetTask("Handling random event NPC...");
                await RandomEvents.HandleRandomEventAsync(RandomEventsPolicy);
                return Antiban.HumanDelay(1500, 2000);
            }

            if (Mode == SmeltingMode.MakeCannonballs)
            {
                return await ExecuteCannonballsAsync();
            }
            else
            {
                return await ExecuteSmeltBarsAsync();
            }
        }

        private async Task<int> ExecuteCannonballsAsync()
        {
            if (!InventoryActions.Contains("Ammo mould"))
            {
                SetTask("Banking for Ammo mould...");
                await BankAtDestinationAsync();
                if (Bank.IsOpen)
                {
                    await Bank.DepositAllExceptAsync("Ammo mould");
                    await Bank.WithdrawAsync("Ammo mould", 1);
                    await Bank.WithdrawAsync("Steel bar", 27);
                    await Bank.CloseAsync();
                }
                return Antiban.HumanDelay(800, 1400);
            }

            if (!InventoryActions.Contains("Steel bar"))
            {
                SetTask("Banking for Steel bars...");
                await BankAtDestinationAsync();
                if (Bank.IsOpen)
                {
                    await Bank.DepositAllExceptAsync("Ammo mould");
                    bool withdrawn = await Bank.WithdrawAsync("Steel bar", 27);
                    if (!withdrawn)
                    {
                        SetTask("Stopped: Out of Steel bars!");
                        ReportIssue("No Steel bars found in bank.");
                        return -1;
                    }
                    await Bank.CloseAsync();
                }
                return Antiban.HumanDelay(800, 1400);
            }

            if (State.Player.Animation != -1)
            {
                SetTask("Smelting cannonballs...");
                return Antiban.HumanDelay(1500, 2500);
            }

            var furnace = Queries.Queries.Objects.Named("Furnace", "Clay furnace").WithinDistance(12).Nearest();
            if (furnace != null)
            {
                SetTask("Interacting with Furnace...");
                SetAction("Smelting Steel bars at Furnace", "Making Cannonballs");
                ClearIssue();
                int preBars = InventoryActions.Count("Steel bar");
                await furnace.InteractAsync("Smelt");
                await Condition.WaitAsync(() => Dialogs.IsMakeInterfaceOpen() || State.Player.Animation != -1, 3000);

                if (Dialogs.IsMakeInterfaceOpen())
                {
                    await Dialogs.ConfirmMakeAllAsync();
                    await Condition.WaitAsync(() => !InventoryActions.Contains("Steel bar") || State.Player.Animation == -1, 35000);
                    int postBars = InventoryActions.Count("Steel bar");
                    _barsSmelted += (preBars - postBars) * 4;
                }
                return Antiban.HumanDelay(1000, 1600);
            }

            SetTask("Navigating to Furnace...");
            return Antiban.HumanDelay(1000, 1500);
        }

        private async Task<int> ExecuteSmeltBarsAsync()
        {
            string primaryOre = BarType switch
            {
                SmeltBarType.Bronze => "Copper ore",
                SmeltBarType.Iron => "Iron ore",
                SmeltBarType.Silver => "Silver ore",
                SmeltBarType.Gold => "Gold ore",
                SmeltBarType.Steel => "Iron ore",
                SmeltBarType.Mithril => "Mithril ore",
                SmeltBarType.Adamantite => "Adamantite ore",
                SmeltBarType.Runite => "Runite ore",
                _ => "Iron ore"
            };

            if (!InventoryActions.Contains(primaryOre))
            {
                SetTask($"Banking for {primaryOre}...");
                await BankAtDestinationAsync();
                if (Bank.IsOpen)
                {
                    await Bank.DepositAllAsync();
                    if (BarType == SmeltBarType.Bronze)
                    {
                        await Bank.WithdrawAsync("Copper ore", 14);
                        await Bank.WithdrawAsync("Tin ore", 14);
                    }
                    else if (BarType == SmeltBarType.Steel)
                    {
                        await Bank.WithdrawAsync("Iron ore", 9);
                        await Bank.WithdrawAsync("Coal", 18);
                    }
                    else if (BarType == SmeltBarType.Mithril)
                    {
                        await Bank.WithdrawAsync("Mithril ore", 5);
                        await Bank.WithdrawAsync("Coal", 20);
                    }
                    else
                    {
                        await Bank.WithdrawAsync(primaryOre, 28);
                    }
                    await Bank.CloseAsync();
                }
                return Antiban.HumanDelay(800, 1400);
            }

            if (State.Player.Animation != -1)
            {
                SetTask($"Smelting {BarType} bars...");
                return Antiban.HumanDelay(1500, 2500);
            }

            var furnace = Queries.Queries.Objects.Named("Furnace").WithinDistance(12).Nearest();
            if (furnace != null)
            {
                SetTask($"Smelting {BarType} bars at Furnace...");
                SetAction("Smelting Ores", BarType.ToString());
                ClearIssue();
                int preOres = InventoryActions.Count(primaryOre);
                await furnace.InteractAsync("Smelt");
                await Condition.WaitAsync(() => Dialogs.IsMakeInterfaceOpen() || State.Player.Animation != -1, 3000);

                if (Dialogs.IsMakeInterfaceOpen())
                {
                    await Dialogs.ConfirmMakeAllAsync();
                    await Condition.WaitAsync(() => !InventoryActions.Contains(primaryOre) || State.Player.Animation == -1, 25000);
                    int postOres = InventoryActions.Count(primaryOre);
                    _barsSmelted += Math.Max(0, preOres - postOres);
                }
                return Antiban.HumanDelay(1000, 1600);
            }

            return Antiban.HumanDelay(800, 1200);
        }

        private async Task BankAtDestinationAsync()
        {
            SetAction("Walking to Bank", BankDestination.ToString());
            ClearIssue();
            bool atBank = await WebWalker.WalkToBankAsync(BankDestination);
            if (atBank && !Bank.IsOpen)
            {
                await Bank.OpenAsync();
                await Condition.WaitAsync(() => Bank.IsOpen, 3000);
            }
        }

        public override void OnStop()
        {
            Log($"Auto Smelter & Cannonballer stopped. Total smelted: {_barsSmelted}");
        }
    }

    // =========================================================================
    // 4. Auto Herblore Script
    // =========================================================================
    public enum HerbloreMode
    {
        CleanHerbs,
        MakeUnfinishedPotions,
        MakeFinishedPotions
    }

    [ScriptManifest(
        name: "Auto Herblore & Cleaner",
        author: "osrsmr",
        version: "2.0.0",
        description: "Cleans grimy herbs, mixes unfinished potions, and finishes potions with dynamic banking and rate tracking.",
        category: ScriptCategory.Herblore)]
    public class AutoHerbloreScript : LoopScript
    {
        private int _herbsProcessed = 0;

        [ScriptSetting("Herblore Mode", "Herblore task to execute", Order = 1)]
        public HerbloreMode Mode { get; set; } = HerbloreMode.CleanHerbs;

        [ScriptSetting("Herb Name", "Grimy herb to clean (CleanHerbs mode)", Order = 2, Options = new[] { "Grimy guam leaf", "Grimy marrentill", "Grimy tarromin", "Grimy harralander", "Grimy ranarr weed", "Grimy toadflax", "Grimy irit leaf", "Grimy avantoe", "Grimy kwuarm", "Grimy snapdragon", "Grimy cadantine", "Grimy lantadyme", "Grimy dwarf weed", "Grimy torstol" })]
        public string GrimyHerb { get; set; } = "Grimy ranarr weed";

        [ScriptSetting("Clean Herb", "Clean herb for unfinished potions", Order = 3, Options = new[] { "Guam leaf", "Marrentill", "Tarromin", "Harralander", "Ranarr weed", "Toadflax", "Irit leaf", "Avantoe", "Kwuarm", "Snapdragon", "Cadantine", "Lantadyme", "Dwarf weed", "Torstol" })]
        public string CleanHerb { get; set; } = "Ranarr weed";

        [ScriptSetting("Secondary Item", "Secondary ingredient for finished potions", Order = 4, Options = new[] { "Snape grass", "Eye of newt", "Red spiders' eggs", "Limpwurt root", "White berries", "Dragon scale dust", "Toad's legs", "Mort myre fungus", "Crushed nest", "Wine of zamorak" })]
        public string SecondaryItem { get; set; } = "Snape grass";

        [ScriptSetting("Unfinished Potion", "Unfinished potion name for finished potions", Order = 5, Options = new[] { "Ranarr potion (unf)", "Prayer potion (unf)", "Toadflax potion (unf)", "Irit potion (unf)", "Avantoe potion (unf)", "Kwuarm potion (unf)", "Snapdragon potion (unf)", "Cadantine potion (unf)", "Torstol potion (unf)" })]
        public string UnfinishedPotion { get; set; } = "Ranarr potion (unf)";

        [ScriptSetting("Bank Destination", "Bank location to withdraw ingredients", Order = 6)]
        public BankLocation BankDestination { get; set; } = BankLocation.GrandExchange;

        [ScriptSetting("Random Events", "Policy for handling random event NPCs", Order = 7)]
        public RandomEventHandling RandomEventsPolicy { get; set; } = RandomEventHandling.Dismiss;

        public override void OnStart()
        {
            Log($"Auto Herblore & Cleaner v2.0 started. Mode: {Mode}, Bank: {BankDestination}");
        }

        public override void OnPaint(DrawingContext dc)
        {
            double elapsedHours = RunningTime.TotalHours;
            int ratePerHour = elapsedHours > 0.001 ? (int)(_herbsProcessed / elapsedHours) : 0;

            var bgBrush = new SolidColorBrush(Color.FromArgb(215, 14, 28, 16));
            var borderPen = new Pen(new SolidColorBrush(Color.FromArgb(255, 74, 222, 128)), 1.5);
            dc.DrawRoundedRectangle(bgBrush, borderPen, new Rect(15, 200, 230, 80), 6, 6);

            var typeFace = new Typeface("Segoe UI");
            var titleText = new FormattedText("🌿 Auto Herblore Pro", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeFace, 12, Brushes.LightGreen, 1.0);
            var ft1 = new FormattedText($"Processed: {_herbsProcessed} | Rate: {ratePerHour}/hr", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeFace, 11, Brushes.White, 1.0);
            var ft2 = new FormattedText($"Mode: {Mode} | Task: {CurrentTaskName}", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeFace, 10, Brushes.Aquamarine, 1.0);

            dc.DrawText(titleText, new Point(25, 208));
            dc.DrawText(ft1, new Point(25, 228));
            dc.DrawText(ft2, new Point(25, 248));
        }

        public override async Task<int> OnLoopAsync()
        {
            if (State.Player == null) return 600;

            if (RandomEventsPolicy != RandomEventHandling.Ignore && RandomEvents.IsRandomEventPresent())
            {
                SetTask("Handling random event NPC...");
                await RandomEvents.HandleRandomEventAsync(RandomEventsPolicy);
                return Antiban.HumanDelay(1500, 2000);
            }

            switch (Mode)
            {
                case HerbloreMode.CleanHerbs:
                    return await ExecuteCleanHerbsAsync();
                case HerbloreMode.MakeUnfinishedPotions:
                    return await ExecuteMakeUnfAsync();
                case HerbloreMode.MakeFinishedPotions:
                    return await ExecuteMakeFinishedAsync();
                default:
                    return 1000;
            }
        }

        private async Task<int> ExecuteCleanHerbsAsync()
        {
            var grimy = Queries.Queries.Inventory.Named(GrimyHerb).First();
            if (grimy == null)
            {
                SetTask("Banking for grimy herbs...");
                SetAction("Walking to Bank", BankDestination.ToString());
                ClearIssue();
                bool atBank = await WebWalker.WalkToBankAsync(BankDestination);
                if (atBank && !Bank.IsOpen)
                {
                    await Bank.OpenAsync();
                    await Condition.WaitAsync(() => Bank.IsOpen, 3000);
                }

                if (Bank.IsOpen)
                {
                    await Bank.DepositAllAsync();
                    bool withdrawn = await Bank.WithdrawAsync(GrimyHerb, 28);
                    if (!withdrawn)
                    {
                        SetTask($"Stopped: No {GrimyHerb} in bank!");
                        ReportIssue($"Bank contains no {GrimyHerb}. Stopping herblore.");
                        return -1;
                    }
                    await Bank.CloseAsync();
                }
                return Antiban.HumanDelay(800, 1400);
            }

            SetTask($"Cleaning {GrimyHerb}...");
            SetAction($"Cleaning {GrimyHerb}", "Inventory Click");
            ClearIssue();
            await grimy.InteractAsync("Clean");
            _herbsProcessed++;
            return Antiban.HumanDelay(120, 240);
        }

        private async Task<int> ExecuteMakeUnfAsync()
        {
            if (!InventoryActions.Contains("Vial of water") || !InventoryActions.Contains(CleanHerb))
            {
                SetTask("Banking for Vials & Herbs...");
                SetAction("Walking to Bank", BankDestination.ToString());
                ClearIssue();
                bool atBank = await WebWalker.WalkToBankAsync(BankDestination);
                if (atBank && !Bank.IsOpen)
                {
                    await Bank.OpenAsync();
                    await Condition.WaitAsync(() => Bank.IsOpen, 3000);
                }

                if (Bank.IsOpen)
                {
                    await Bank.DepositAllAsync();
                    await Bank.WithdrawAsync("Vial of water", 14);
                    await Bank.WithdrawAsync(CleanHerb, 14);
                    await Bank.CloseAsync();
                }
                return Antiban.HumanDelay(800, 1400);
            }

            if (State.Player.Animation == -1)
            {
                SetTask($"Mixing {CleanHerb} with Vial of water...");
                SetAction("Using Herb on Vial of water", CleanHerb);
                ClearIssue();
                int preCount = InventoryActions.Count(CleanHerb);
                await InventoryActions.UseItemOnItemAsync(CleanHerb, "Vial of water");
                await Condition.WaitAsync(() => Dialogs.IsMakeInterfaceOpen() || State.Player.Animation != -1, 2000);

                if (Dialogs.IsMakeInterfaceOpen())
                {
                    await Dialogs.ConfirmMakeAllAsync();
                    await Condition.WaitAsync(() => !InventoryActions.Contains(CleanHerb) || !InventoryActions.Contains("Vial of water") || State.Player.Animation == -1, 15000);
                    int postCount = InventoryActions.Count(CleanHerb);
                    _herbsProcessed += Math.Max(0, preCount - postCount);
                }
            }
            return Antiban.HumanDelay(1000, 1500);
        }

        private async Task<int> ExecuteMakeFinishedAsync()
        {
            if (!InventoryActions.Contains(UnfinishedPotion) || !InventoryActions.Contains(SecondaryItem))
            {
                SetTask("Banking for unf potions & secondaries...");
                SetAction("Walking to Bank", BankDestination.ToString());
                ClearIssue();
                bool atBank = await WebWalker.WalkToBankAsync(BankDestination);
                if (atBank && !Bank.IsOpen)
                {
                    await Bank.OpenAsync();
                    await Condition.WaitAsync(() => Bank.IsOpen, 3000);
                }

                if (Bank.IsOpen)
                {
                    await Bank.DepositAllAsync();
                    await Bank.WithdrawAsync(UnfinishedPotion, 14);
                    await Bank.WithdrawAsync(SecondaryItem, 14);
                    await Bank.CloseAsync();
                }
                return Antiban.HumanDelay(800, 1400);
            }

            if (State.Player.Animation == -1)
            {
                SetTask($"Adding {SecondaryItem} to {UnfinishedPotion}...");
                SetAction("Using Secondary on Potion", SecondaryItem);
                ClearIssue();
                int preCount = InventoryActions.Count(SecondaryItem);
                await InventoryActions.UseItemOnItemAsync(SecondaryItem, UnfinishedPotion);
                await Condition.WaitAsync(() => Dialogs.IsMakeInterfaceOpen() || State.Player.Animation != -1, 2000);

                if (Dialogs.IsMakeInterfaceOpen())
                {
                    await Dialogs.ConfirmMakeAllAsync();
                    await Condition.WaitAsync(() => !InventoryActions.Contains(SecondaryItem) || !InventoryActions.Contains(UnfinishedPotion) || State.Player.Animation == -1, 15000);
                    int postCount = InventoryActions.Count(SecondaryItem);
                    _herbsProcessed += Math.Max(0, preCount - postCount);
                }
            }
            return Antiban.HumanDelay(1000, 1500);
        }

        public override void OnStop()
        {
            Log($"Auto Herblore & Cleaner stopped. Total processed: {_herbsProcessed}");
        }
    }

    // =========================================================================
    // 5. Auto Rooftop Agility Script
    // =========================================================================
    public enum AgilityCourse
    {
        GnomeStronghold,
        DraynorVillage,
        VarrockRooftop,
        CanifisRooftop,
        FaladorRooftop,
        SeersVillageRooftop,
        PollnivneachRooftop,
        RellekkaRooftop,
        ArdougneRooftop
    }

    [ScriptManifest(
        name: "Auto Rooftop Agility AIO",
        author: "osrsmr",
        version: "2.0.0",
        description: "Executes rooftop agility courses with automatic Mark of Grace looting, food consumption, and lap tracking.",
        category: ScriptCategory.Agility)]
    public class AutoAgilityScript : LoopScript
    {
        private int _lapsCompleted = 0;
        private int _marksLooted = 0;
        private int _previousMarks = 0;

        [ScriptSetting("Agility Course", "Target rooftop agility course", Order = 1)]
        public AgilityCourse Course { get; set; } = AgilityCourse.VarrockRooftop;

        [ScriptSetting("Loot Marks of Grace", "Automatically pickup Marks of Grace", Order = 2)]
        public bool LootMarksOfGrace { get; set; } = true;

        [ScriptSetting("Eat Food at HP %", "Health percentage to eat food", Order = 3)]
        public int EatAtHpPercent { get; set; } = 50;

        [ScriptSetting("Random Events", "Policy for handling random event NPCs", Order = 4)]
        public RandomEventHandling RandomEventsPolicy { get; set; } = RandomEventHandling.Dismiss;

        public override void OnStart()
        {
            Log($"Auto Rooftop Agility AIO v2.0 started. Course: {Course}, LootMarks: {LootMarksOfGrace}");
            _previousMarks = CountMarksOfGrace();
        }

        private int CountMarksOfGrace()
        {
            return Queries.Queries.Inventory.Named("Mark of grace").Count();
        }

        public override void OnPaint(DrawingContext dc)
        {
            double elapsedHours = RunningTime.TotalHours;
            int lapsPerHour = elapsedHours > 0.001 ? (int)(_lapsCompleted / elapsedHours) : 0;
            int marksPerHour = elapsedHours > 0.001 ? (int)(_marksLooted / elapsedHours) : 0;

            var bgBrush = new SolidColorBrush(Color.FromArgb(215, 17, 40, 36));
            var borderPen = new Pen(new SolidColorBrush(Color.FromArgb(255, 45, 212, 191)), 1.5);
            dc.DrawRoundedRectangle(bgBrush, borderPen, new Rect(15, 200, 235, 85), 6, 6);

            var typeFace = new Typeface("Segoe UI");
            var titleText = new FormattedText("🏃 Auto Agility Rooftops", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeFace, 12, Brushes.Aquamarine, 1.0);
            var ft1 = new FormattedText($"Laps: {_lapsCompleted} ({lapsPerHour}/hr) | Marks: {_marksLooted} ({marksPerHour}/hr)", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeFace, 10.5, Brushes.White, 1.0);
            var ft2 = new FormattedText($"Course: {Course} | Task: {CurrentTaskName}", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeFace, 10, Brushes.Turquoise, 1.0);

            dc.DrawText(titleText, new Point(25, 208));
            dc.DrawText(ft1, new Point(25, 228));
            dc.DrawText(ft2, new Point(25, 250));
        }

        public override async Task<int> OnLoopAsync()
        {
            if (State.Player == null) return 600;

            int currentMarks = CountMarksOfGrace();
            if (currentMarks > _previousMarks)
            {
                _marksLooted += (currentMarks - _previousMarks);
                _previousMarks = currentMarks;
            }
            else
            {
                _previousMarks = currentMarks;
            }

            if (RandomEventsPolicy != RandomEventHandling.Ignore && RandomEvents.IsRandomEventPresent())
            {
                SetTask("Handling random event NPC...");
                await RandomEvents.HandleRandomEventAsync(RandomEventsPolicy);
                return Antiban.HumanDelay(1500, 2000);
            }

            if (Combat.GetHealthPercent() <= EatAtHpPercent)
            {
                SetTask("Eating food for low HP...");
                await Combat.EatFoodAsync();
                await Condition.SleepAsync(600, 900);
            }

            if (LootMarksOfGrace)
            {
                var mark = Queries.Queries.GroundItems.Named("Mark of grace").WithinDistance(15).Nearest();
                if (mark != null)
                {
                    SetTask("Looting Mark of Grace...");
                    SetAction("Taking Ground Item", "Mark of grace");
                    ClearIssue();
                    await mark.TakeAsync();
                    await Condition.WaitAsync(() => !Queries.Queries.GroundItems.Named("Mark of grace").WithinDistance(2).Any(), 4000);
                    return Antiban.HumanDelay(600, 1000);
                }
            }

            if (State.Player.Animation != -1 && State.Player.Animation != State.Player.PoseAnimation)
            {
                SetTask("Navigating obstacle...");
                return Antiban.HumanDelay(1200, 1800);
            }

            var nextObstacle = FindNextObstacle();
            if (nextObstacle != null)
            {
                string obsName = nextObstacle.Name;
                SetTask($"Interacting with {obsName}...");
                SetAction($"Agility Obstacle: {obsName}", $"Distance: {nextObstacle.Distance:F1}");
                ClearIssue();
                bool clicked = await nextObstacle.InteractAsync("Climb");
                if (!clicked)
                {
                    clicked = await nextObstacle.InteractAsync("Jump");
                }
                if (clicked)
                {
                    await Condition.WaitAsync(() => (State.Player != null && (State.Player.Animation != -1 || State.Player.IsMoving)), 2500);
                    if (IsCourseCompletionObstacle(obsName))
                    {
                        _lapsCompleted++;
                    }
                }
                return Antiban.HumanDelay(800, 1400);
            }

            SetTask("Searching for next obstacle...");
            return Antiban.HumanDelay(800, 1200);
        }

        private SceneObjectSnapshot? FindNextObstacle()
        {
            return Course switch
            {
                AgilityCourse.GnomeStronghold => Queries.Queries.Objects.Named("Log balance", "Obstacle net", "Tree branch", "Balancing rope", "Obstacle pipe").WithinDistance(15).Nearest(),
                AgilityCourse.DraynorVillage => Queries.Queries.Objects.Named("Rough wall", "Tightrope", "Narrow wall", "Wall", "Gap", "Crate").WithinDistance(15).Nearest(),
                AgilityCourse.VarrockRooftop => Queries.Queries.Objects.Named("Rough wall", "Clothes line", "Gap", "Wall", "Balancing ledge", "Edge").WithinDistance(15).Nearest(),
                AgilityCourse.CanifisRooftop => Queries.Queries.Objects.Named("Tall tree", "Gap", "Pole-vault").WithinDistance(15).Nearest(),
                AgilityCourse.FaladorRooftop => Queries.Queries.Objects.Named("Rough wall", "Tightrope", "Handholds", "Gap", "Ledge", "Edge").WithinDistance(15).Nearest(),
                AgilityCourse.SeersVillageRooftop => Queries.Queries.Objects.Named("Wall", "Gap", "Tightrope", "Edge").WithinDistance(15).Nearest(),
                _ => Queries.Queries.Objects.Named("Rough wall", "Wall", "Gap", "Tightrope", "Obstacle net", "Log balance").WithinDistance(15).Nearest()
            };
        }

        private bool IsCourseCompletionObstacle(string name)
        {
            return name.Equals("Edge", StringComparison.OrdinalIgnoreCase) ||
                   name.Equals("Crate", StringComparison.OrdinalIgnoreCase) ||
                   name.Equals("Obstacle pipe", StringComparison.OrdinalIgnoreCase);
        }

        public override void OnStop()
        {
            Log($"Auto Rooftop Agility AIO stopped. Total laps: {_lapsCompleted}, Marks: {_marksLooted}");
        }
    }

    // =========================================================================
    // 6. Auto Master Farmer & Pickpocket Script
    // =========================================================================
    [ScriptManifest(
        name: "Auto Pickpocket & Master Farmer",
        author: "osrsmr",
        version: "2.0.0",
        description: "Automated pickpocketing for Master Farmers and Ardougne Knights with stun recovery, coin pouch opening, and auto-eating.",
        category: ScriptCategory.Thieving)]
    public class AutoThieverScript : LoopScript
    {
        private int _pickpocketCount = 0;
        private int _stunCount = 0;

        [ScriptSetting("Target NPC", "NPC to pickpocket", Order = 1, Options = new[] { "Master Farmer", "Knight of Ardougne", "Guard", "Man", "Woman", "Warrior woman", "Paladin", "Hero", "Elf" })]
        public string TargetNpc { get; set; } = "Master Farmer";

        [ScriptSetting("Eat at HP %", "Health threshold to consume food", Order = 2)]
        public int EatAtHpPercent { get; set; } = 60;

        [ScriptSetting("Open Coin Pouches At", "Inventory coin pouch count before opening", Order = 3)]
        public int CoinPouchThreshold { get; set; } = 28;

        [ScriptSetting("Equip Dodgy Necklaces", "Auto-equip Dodgy necklace when broken", Order = 4)]
        public bool EquipDodgyNecklace { get; set; } = true;

        [ScriptSetting("Random Events", "Policy for handling random event NPCs", Order = 5)]
        public RandomEventHandling RandomEventsPolicy { get; set; } = RandomEventHandling.Dismiss;

        public override void OnStart()
        {
            Log($"Auto Pickpocket v2.0 started. Target: {TargetNpc}, EatAtHp: {EatAtHpPercent}%, PouchThreshold: {CoinPouchThreshold}");
        }

        public override void OnPaint(DrawingContext dc)
        {
            double elapsedHours = RunningTime.TotalHours;
            int ratePerHour = elapsedHours > 0.001 ? (int)(_pickpocketCount / elapsedHours) : 0;

            var bgBrush = new SolidColorBrush(Color.FromArgb(215, 20, 26, 32));
            var borderPen = new Pen(new SolidColorBrush(Color.FromArgb(255, 148, 163, 184)), 1.5);
            dc.DrawRoundedRectangle(bgBrush, borderPen, new Rect(15, 200, 230, 85), 6, 6);

            var typeFace = new Typeface("Segoe UI");
            var titleText = new FormattedText("🗝️ Auto Pickpocket Pro", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeFace, 12, Brushes.LightSlateGray, 1.0);
            var ft1 = new FormattedText($"Pockets: {_pickpocketCount} | Stuns: {_stunCount}", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeFace, 11, Brushes.White, 1.0);
            var ft2 = new FormattedText($"Rate: {ratePerHour}/hr | Task: {CurrentTaskName}", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeFace, 10, Brushes.LightSkyBlue, 1.0);

            dc.DrawText(titleText, new Point(25, 208));
            dc.DrawText(ft1, new Point(25, 228));
            dc.DrawText(ft2, new Point(25, 250));
        }

        public override async Task<int> OnLoopAsync()
        {
            if (State.Player == null) return 600;

            if (RandomEventsPolicy != RandomEventHandling.Ignore && RandomEvents.IsRandomEventPresent())
            {
                SetTask("Handling random event NPC...");
                await RandomEvents.HandleRandomEventAsync(RandomEventsPolicy);
                return Antiban.HumanDelay(1500, 2000);
            }

            if (Combat.GetHealthPercent() <= EatAtHpPercent)
            {
                SetTask("Eating food...");
                bool ate = await Combat.EatFoodAsync();
                if (!ate)
                {
                    SetTask("Stopped: Out of Food & Low HP!");
                    ReportIssue("Health dropped below threshold and no food was found.");
                    return -1;
                }
                await Condition.SleepAsync(600, 900);
            }

            if (EquipDodgyNecklace && !Equipment.IsEquipped("Dodgy necklace") && InventoryActions.Contains("Dodgy necklace"))
            {
                SetTask("Equipping Dodgy necklace...");
                await Equipment.EquipAsync("Dodgy necklace");
                await Condition.SleepAsync(400, 700);
            }

            int pouches = InventoryActions.Count("Coin pouch");
            if (pouches >= CoinPouchThreshold || InventoryActions.IsFull)
            {
                SetTask($"Opening {pouches} Coin pouches...");
                SetAction("Opening Coin Pouches", $"Count: {pouches}");
                ClearIssue();
                await InventoryActions.ClickItemAsync("Coin pouch");
                await Condition.SleepAsync(300, 600);
            }

            if (State.Player.Graphic == 80 || State.Player.Animation == 424)
            {
                _stunCount++;
                SetTask("Player is stunned! Waiting...");
                await Condition.WaitAsync(() => State.Player == null || (State.Player.Graphic != 80 && State.Player.Animation != 424), 4500);
                return Antiban.HumanDelay(300, 600);
            }

            var query = Queries.Queries.Npcs.WithinDistance(10);
            if (!string.IsNullOrWhiteSpace(TargetNpc) && !TargetNpc.Equals("All", StringComparison.OrdinalIgnoreCase))
            {
                var names = TargetNpc.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                if (names.Length > 0)
                {
                    query = query.Filter(n => names.Any(name => n.Name.Contains(name, StringComparison.OrdinalIgnoreCase)));
                }
            }

            var target = query.Nearest();
            if (target != null)
            {
                string targetName = target.Name;
                string hpStr = Combat.GetHealthPercent().ToString();
                SetTask($"Pickpocketing {targetName}...");
                SetAction($"Pickpocketing {targetName}", "Health: " + hpStr + "%");
                ClearIssue();
                bool clicked = await target.InteractAsync("Pickpocket");
                if (clicked)
                {
                    _pickpocketCount++;
                }
                return Antiban.HumanDelay(400, 700);
            }

            SetTask($"Searching for {TargetNpc}...");
            ReportWarning($"No {TargetNpc} within 10 tiles.");
            return Antiban.HumanDelay(800, 1200);
        }

        public override void OnStop()
        {
            Log($"Auto Pickpocket stopped. Total pickpockets: {_pickpocketCount}, Stuns: {_stunCount}");
        }
    }
}
