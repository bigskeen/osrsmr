using System;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using OsrsMr.Core.Data;
using OsrsMr.Core.Input;
using OsrsMr.Core.Interaction;
using OsrsMr.Core.Minigames;
using OsrsMr.Core.Queries;

namespace OsrsMr.Core.Scripting
{
    // =========================================================================
    // 1. Wintertodt Minigame Bot Script
    // =========================================================================
    [ScriptManifest(
        name: "Wintertodt AIO",
        author: "osrsmr",
        version: "2.0.0",
        description: "Automates Wintertodt: chops roots, fletches kindling, feeds braziers, dodges snowfall, and heals pyromancers.",
        category: ScriptCategory.Minigames)]
    public class WintertodtScript : LoopScript
    {
        private int _gamesCompleted = 0;

        [ScriptSetting("Fletch Kindling", "Fletch Bruma roots into kindling for extra points", Order = 1)]
        public bool FletchKindling { get; set; } = true;

        [ScriptSetting("Heal Pyromancers", "Use rejuvenation potions to revive incapacitated pyromancers", Order = 2)]
        public bool HealPyromancers { get; set; } = true;

        [ScriptSetting("Eat at HP %", "Health threshold to automatically eat food", Order = 3)]
        public int EatThresholdPercent { get; set; } = 40;

        [ScriptSetting("Random Events", "Policy for handling random event NPCs", Order = 4)]
        public RandomEventHandling RandomEventsPolicy { get; set; } = RandomEventHandling.Dismiss;

        public override void OnStart()
        {
            Log("Wintertodt AIO v2.0 started. Tracking braziers, pyromancers, and snowfall...");
        }

        public override void OnPaint(DrawingContext dc)
        {
            var bgBrush = new SolidColorBrush(Color.FromArgb(210, 20, 20, 20));
            var borderPen = new Pen(new SolidColorBrush(Color.FromArgb(255, 0, 229, 255)), 1.5);
            dc.DrawRoundedRectangle(bgBrush, borderPen, new Rect(15, 200, 220, 75), 6, 6);

            var typeFace = new Typeface("Segoe UI");
            var titleText = new FormattedText("❄ Wintertodt AIO", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeFace, 12, Brushes.Cyan, 1.0);
            var ft1 = new FormattedText($"Task: {CurrentTaskName}", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeFace, 11, Brushes.White, 1.0);
            var ft2 = new FormattedText($"Runtime: {RunningTime:hh\\:mm\\:ss} | Games: {_gamesCompleted}", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeFace, 10, Brushes.LightSkyBlue, 1.0);

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
                SetTask("Handling random event...");
                await RandomEvents.HandleRandomEventAsync(RandomEventsPolicy);
                return Antiban.HumanDelay(1500, 2000);
            }

            // 2. Health check / Auto-eating
            double hpPercent = State.Player.MaxHp > 0 ? (double)State.Player.CurrentHp / State.Player.MaxHp * 100 : 100;
            if (hpPercent <= EatThresholdPercent)
            {
                SetTask("Eating food to restore health...");
                await FoodCatalog.EatFoodAsync();
                return Antiban.HumanDelay(800, 1200);
            }

            // 3. Dodge dangerous snowfall / falling ice
            if (Wintertodt.IsUnderSnowfall())
            {
                SetTask("Dodging snowfall AOE!");
                await Wintertodt.DodgeSnowfallAsync();
                return Antiban.HumanDelay(600, 900);
            }

            // 4. Heal incapacitated pyromancer
            if (HealPyromancers)
            {
                var injuredPyromancer = Queries.Queries.Npcs.Named("Incapacitated Pyromancer").WithinDistance(8).Nearest();
                if (injuredPyromancer != null && InventoryActions.Contains("Rejuvenation potion"))
                {
                    SetTask("Healing incapacitated Pyromancer...");
                    await Wintertodt.HealPyromancerAsync();
                    return Antiban.HumanDelay(1500, 2000);
                }
            }

            // 5. Feed Brazier if holding kindling or roots
            bool hasKindling = InventoryActions.Contains("Bruma kindling");
            bool hasRoots = InventoryActions.Contains("Bruma root");

            if (hasKindling || (!FletchKindling && hasRoots) || (InventoryActions.IsFull && !FletchKindling))
            {
                SetTask("Feeding brazier...");
                await Wintertodt.FeedBrazierAsync();
                return Antiban.HumanDelay(2000, 3000);
            }

            // 6. Fletch kindling if enabled
            if (FletchKindling && hasRoots)
            {
                SetTask("Fletching Bruma roots into kindling...");
                await Wintertodt.FletchKindlingAsync();
                return Antiban.HumanDelay(1500, 2500);
            }

            // 7. Light/Fix broken brazier
            var brokenBrazier = Queries.Queries.Objects.Named("Broken brazier", "Unlit brazier").WithinDistance(10).Nearest();
            if (brokenBrazier != null)
            {
                SetTask("Fixing or lighting brazier...");
                await Wintertodt.FixOrLightBrazierAsync();
                return Antiban.HumanDelay(1200, 1800);
            }

            // 8. Chop roots
            SetTask("Chopping Bruma roots...");
            await Wintertodt.ChopRootsAsync();
            return Antiban.HumanDelay(2000, 3000);
        }
    }

    // =========================================================================
    // 2. Tempoross Minigame Bot Script
    // =========================================================================
    [ScriptManifest(
        name: "Tempoross AIO",
        author: "osrsmr",
        version: "2.0.0",
        description: "Automates Tempoross: harpoon fishing, cooking fish, loading cannons, wave tethering, and pool attacks.",
        category: ScriptCategory.Minigames)]
    public class TemporossScript : LoopScript
    {
        [ScriptSetting("Cook Fish", "Cook harpoonfish before loading into cannons for bonus points", Order = 1)]
        public bool CookFish { get; set; } = true;

        public override void OnStart()
        {
            Log("Tempoross AIO v2.0 started. Watching storm, waves, and spirit pool...");
        }

        public override void OnPaint(DrawingContext dc)
        {
            var bgBrush = new SolidColorBrush(Color.FromArgb(210, 20, 20, 20));
            var borderPen = new Pen(new SolidColorBrush(Color.FromArgb(255, 33, 150, 243)), 1.5);
            dc.DrawRoundedRectangle(bgBrush, borderPen, new Rect(15, 200, 220, 75), 6, 6);

            var typeFace = new Typeface("Segoe UI");
            var titleText = new FormattedText("🌊 Tempoross AIO", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeFace, 12, Brushes.DodgerBlue, 1.0);
            var ft1 = new FormattedText($"Task: {CurrentTaskName}", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeFace, 11, Brushes.White, 1.0);
            var ft2 = new FormattedText($"Runtime: {RunningTime:hh\\:mm\\:ss}", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeFace, 10, Brushes.LightCyan, 1.0);

            dc.DrawText(titleText, new Point(25, 208));
            dc.DrawText(ft1, new Point(25, 228));
            dc.DrawText(ft2, new Point(25, 248));
        }

        public override async Task<int> OnLoopAsync()
        {
            if (State.Player == null) return 600;

            // 1. Attack Spirit Pool if vulnerable
            var pool = Queries.Queries.Npcs.Named("Spirit pool").WithinDistance(15).Nearest();
            if (pool != null)
            {
                SetTask("Attacking Tempoross Spirit Pool!");
                await Tempoross.AttackSpiritPoolAsync();
                return Antiban.HumanDelay(2000, 3000);
            }

            // 2. Full inventory handling -> Cook or Load
            if (InventoryActions.IsFull)
            {
                if (CookFish && InventoryActions.Contains("Raw harpoonfish"))
                {
                    SetTask("Cooking raw harpoonfish...");
                    await Tempoross.CookFishAsync();
                    return Antiban.HumanDelay(2500, 3500);
                }
                else
                {
                    SetTask("Loading harpoonfish into cannons...");
                    await Tempoross.LoadCannonsAsync();
                    return Antiban.HumanDelay(2000, 3000);
                }
            }

            // 3. Fish harpoonfish
            SetTask("Fishing harpoonfish...");
            await Tempoross.FishHarpoonfishAsync();
            return Antiban.HumanDelay(2500, 3500);
        }
    }

    // =========================================================================
    // 3. Guardians of the Rift Bot Script
    // =========================================================================
    [ScriptManifest(
        name: "Guardians of the Rift AIO",
        author: "osrsmr",
        version: "2.0.0",
        description: "Automates GotR: mines remains, crafts essence, enters elemental/catalytic rifts, binds runes, and charges barriers.",
        category: ScriptCategory.Minigames)]
    public class GotRScript : LoopScript
    {
        [ScriptSetting("Craft Runes", "Prioritize entering active portals and crafting runes", Order = 1)]
        public bool CraftRunes { get; set; } = true;

        [ScriptSetting("Repair Pouches", "Automatically repair degraded essence pouches with Dark Mage", Order = 2)]
        public bool RepairPouches { get; set; } = true;

        [ScriptSetting("Eat at HP %", "Health threshold to automatically eat food", Order = 3)]
        public int EatThresholdPercent { get; set; } = 40;

        [ScriptSetting("Random Events", "Policy for handling random event NPCs", Order = 4)]
        public RandomEventHandling RandomEventsPolicy { get; set; } = RandomEventHandling.Dismiss;

        public override void OnStart()
        {
            Log("Guardians of the Rift AIO v2.0 started. Monitoring portals and Great Guardian...");
        }

        public override void OnPaint(DrawingContext dc)
        {
            var bgBrush = new SolidColorBrush(Color.FromArgb(210, 20, 20, 20));
            var borderPen = new Pen(new SolidColorBrush(Color.FromArgb(255, 156, 39, 176)), 1.5);
            dc.DrawRoundedRectangle(bgBrush, borderPen, new Rect(15, 200, 220, 75), 6, 6);

            var typeFace = new Typeface("Segoe UI");
            var titleText = new FormattedText("🌀 Guardians of the Rift", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeFace, 12, Brushes.MediumPurple, 1.0);
            var ft1 = new FormattedText($"Task: {CurrentTaskName}", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeFace, 11, Brushes.White, 1.0);
            var ft2 = new FormattedText($"Runtime: {RunningTime:hh\\:mm\\:ss}", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeFace, 10, Brushes.Violet, 1.0);

            dc.DrawText(titleText, new Point(25, 208));
            dc.DrawText(ft1, new Point(25, 228));
            dc.DrawText(ft2, new Point(25, 248));
        }

        public override async Task<int> OnLoopAsync()
        {
            if (State.Player == null) return 600;

            // 1. Power Guardian or repair barrier if holding runes/cells
            if (InventoryActions.Contains("cell") || InventoryActions.Contains("rune"))
            {
                SetTask("Powering Great Guardian / Charging barriers...");
                await GuardiansOfTheRift.PowerGuardianOrBarrierAsync();
                return Antiban.HumanDelay(1800, 2500);
            }

            // 2. If holding essence, enter active portal and craft runes
            if (InventoryActions.Contains("Guardian essence") || InventoryActions.Contains("essence"))
            {
                var altar = Queries.Queries.Objects.Filter(o => o.Name.Contains("Altar", StringComparison.OrdinalIgnoreCase)).WithinDistance(12).Nearest();
                if (altar != null)
                {
                    SetTask("Crafting runes at altar...");
                    await GuardiansOfTheRift.CraftAtAltarAsync();
                    return Antiban.HumanDelay(1500, 2200);
                }
                else
                {
                    SetTask("Entering active Runecraft portal...");
                    await GuardiansOfTheRift.EnterActivePortalAsync();
                    return Antiban.HumanDelay(2000, 3000);
                }
            }

            // 3. Craft essence at workbench if holding fragments
            if (InventoryActions.Contains("Guardian fragments") && InventoryActions.FreeSlots > 0)
            {
                SetTask("Crafting essence at workbench...");
                await GuardiansOfTheRift.CraftEssenceAsync();
                return Antiban.HumanDelay(2000, 3000);
            }

            // 4. Mine guardian remains
            SetTask("Mining huge guardian remains...");
            await GuardiansOfTheRift.MineRemainsAsync();
            return Antiban.HumanDelay(2500, 3500);
        }
    }

    // =========================================================================
    // 4. Pest Control Bot Script
    // =========================================================================
    [ScriptManifest(
        name: "Pest Control AIO",
        author: "osrsmr",
        version: "2.0.0",
        description: "Automates Pest Control: boards landers, attacks unshielded portals, clears spinners, and defends Void Knight.",
        category: ScriptCategory.Minigames)]
    public class PestControlScript : LoopScript
    {
        [ScriptSetting("Lander Difficulty", "Boat lander tier to board", Order = 1)]
        public PestControlLander Lander { get; set; } = PestControlLander.Novice;

        [ScriptSetting("Attack Portals", "Prioritize attacking active unshielded portals", Order = 2)]
        public bool AttackPortals { get; set; } = true;

        [ScriptSetting("Kill Spinners", "Prioritize killing healing spinners near portals", Order = 3)]
        public bool KillSpinners { get; set; } = true;

        [ScriptSetting("Eat at HP %", "Health threshold to consume food", Order = 4)]
        public int EatThresholdPercent { get; set; } = 40;

        [ScriptSetting("Random Events", "Policy for handling random event NPCs", Order = 5)]
        public RandomEventHandling RandomEventsPolicy { get; set; } = RandomEventHandling.Dismiss;

        public override void OnStart()
        {
            Log($"Pest Control AIO v2.0 started. Target Lander: {Lander}");
        }

        public override void OnPaint(DrawingContext dc)
        {
            var bgBrush = new SolidColorBrush(Color.FromArgb(210, 20, 20, 20));
            var borderPen = new Pen(new SolidColorBrush(Color.FromArgb(255, 76, 175, 80)), 1.5);
            dc.DrawRoundedRectangle(bgBrush, borderPen, new Rect(15, 200, 220, 75), 6, 6);

            var typeFace = new Typeface("Segoe UI");
            var titleText = new FormattedText("🛡 Pest Control AIO", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeFace, 12, Brushes.LimeGreen, 1.0);
            var ft1 = new FormattedText($"Task: {CurrentTaskName}", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeFace, 11, Brushes.White, 1.0);
            var ft2 = new FormattedText($"Lander: {Lander} | Runtime: {RunningTime:hh\\:mm\\:ss}", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeFace, 10, Brushes.Yellow, 1.0);

            dc.DrawText(titleText, new Point(25, 208));
            dc.DrawText(ft1, new Point(25, 228));
            dc.DrawText(ft2, new Point(25, 248));
        }

        public override async Task<int> OnLoopAsync()
        {
            if (State.Player == null) return 600;

            // 1. Board boat if on lander pier
            var plank = Queries.Queries.Objects.Named("Gangplank", "Gangplank (Novice)", "Gangplank (Intermediate)", "Gangplank (Veteran)").WithinDistance(10).Nearest();
            if (plank != null && !PestControl.IsInGame)
            {
                SetTask($"Boarding {Lander} lander boat...");
                await PestControl.BoardBoatAsync(Lander);
                return Antiban.HumanDelay(2000, 3000);
            }

            // 2. Attack vulnerable portals & spinners
            var portalOrSpinner = Queries.Queries.Npcs.Named("Spinner", "Purple portal", "Blue portal", "Yellow portal", "Red portal").WithinDistance(25).Nearest();
            if (portalOrSpinner != null)
            {
                SetTask($"Attacking {portalOrSpinner.Name}...");
                await PestControl.AttackActivePortalAsync();
                return Antiban.HumanDelay(1500, 2500);
            }

            // 3. Defend Void Knight
            SetTask("Defending Void Knight in center platform...");
            await PestControl.DefendVoidKnightAsync();
            return Antiban.HumanDelay(1800, 2600);
        }
    }

    // =========================================================================
    // 5. Nightmare Zone (NMZ) Bot Script
    // =========================================================================
    [ScriptManifest(
        name: "Nightmare Zone AIO",
        author: "osrsmr",
        version: "2.0.0",
        description: "Automates NMZ: drinks Overload & Absorption potions, maintains 1 HP, and collects power-up runes.",
        category: ScriptCategory.Minigames)]
    public class NightmareZoneScript : LoopScript
    {
        [ScriptSetting("Drink Overloads", "Automatically drink overload doses", Order = 1)]
        public bool DrinkOverloads { get; set; } = true;

        [ScriptSetting("Drink Absorptions", "Maintain absorption shield > 200", Order = 2)]
        public bool DrinkAbsorptions { get; set; } = true;

        [ScriptSetting("Flick Rapid Heal", "Flick Rapid Heal prayer to keep HP at 1", Order = 3)]
        public bool FlickRapidHeal { get; set; } = true;

        [ScriptSetting("Eat at HP %", "Threshold to eat rock cake / locator orb", Order = 4)]
        public int EatThresholdPercent { get; set; } = 50;

        [ScriptSetting("Random Events", "Policy for handling random event NPCs", Order = 5)]
        public RandomEventHandling RandomEventsPolicy { get; set; } = RandomEventHandling.Dismiss;

        public override void OnStart()
        {
            Log("Nightmare Zone AIO v2.0 started. Maintaining 1 HP and max absorption...");
        }

        public override void OnPaint(DrawingContext dc)
        {
            var bgBrush = new SolidColorBrush(Color.FromArgb(210, 20, 20, 20));
            var borderPen = new Pen(new SolidColorBrush(Color.FromArgb(255, 255, 193, 7)), 1.5);
            dc.DrawRoundedRectangle(bgBrush, borderPen, new Rect(15, 200, 220, 75), 6, 6);

            var typeFace = new Typeface("Segoe UI");
            var titleText = new FormattedText("💤 Nightmare Zone AIO", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeFace, 12, Brushes.Gold, 1.0);
            var ft1 = new FormattedText($"Task: {CurrentTaskName}", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeFace, 11, Brushes.White, 1.0);
            string hpDisplay = State.Player != null ? $"{State.Player.CurrentHp}/{State.Player.MaxHp}" : "N/A";
            var ft2 = new FormattedText($"HP: {hpDisplay} | Runtime: {RunningTime:hh\\:mm\\:ss}", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeFace, 10, Brushes.LightGoldenrodYellow, 1.0);

            dc.DrawText(titleText, new Point(25, 208));
            dc.DrawText(ft1, new Point(25, 228));
            dc.DrawText(ft2, new Point(25, 248));
        }

        public override async Task<int> OnLoopAsync()
        {
            if (State.Player == null) return 600;

            // 1. Collect Power-ups
            var powerUp = Queries.Queries.Objects.Named("Power surge", "Zapper", "Ultimate force", "Recurrent damage").WithinDistance(8).Nearest();
            if (powerUp != null)
            {
                SetTask($"Collecting {powerUp.Name}...");
                await NightmareZone.CollectPowerUpAsync();
                return Antiban.HumanDelay(1000, 1500);
            }

            // 2. Maintain Overload
            if (State.Player.CurrentHp > 50 && InventoryActions.Contains("Overload"))
            {
                SetTask("Drinking Overload potion...");
                await NightmareZone.DrinkOverloadAsync();
                return Antiban.HumanDelay(1200, 1800);
            }

            // 3. Maintain 1 HP
            if (State.Player.CurrentHp > 1)
            {
                SetTask("Lowering health to 1 HP...");
                await NightmareZone.GuzzleDownTo1HpAsync();
                return Antiban.HumanDelay(800, 1400);
            }

            // 4. Drink absorption
            if (InventoryActions.Contains("Absorption"))
            {
                SetTask("Sipping Absorption potion...");
                await NightmareZone.DrinkAbsorptionAsync();
                return Antiban.HumanDelay(600, 1000);
            }

            SetTask("In combat: Monitoring HP and absorption...");
            return Antiban.HumanDelay(2000, 4000);
        }
    }

    // =========================================================================
    // 6. Barrows Minigame Bot Script
    // =========================================================================
    [ScriptManifest(
        name: "Barrows AIO",
        author: "osrsmr",
        version: "2.0.0",
        description: "Automates Barrows: digs into mounds, switches protection prayers, solves puzzle doors, and loots chest.",
        category: ScriptCategory.Minigames)]
    public class BarrowsScript : LoopScript
    {
        [ScriptSetting("Eat at HP %", "Health threshold to automatically eat food", Order = 1)]
        public int EatThresholdPercent { get; set; } = 40;

        [ScriptSetting("Drink Prayer at Points", "Prayer point threshold to drink prayer potion", Order = 2)]
        public int DrinkPrayerThreshold { get; set; } = 15;

        [ScriptSetting("Loot Chest", "Automatically search and loot chest in crypt", Order = 3)]
        public bool LootChest { get; set; } = true;

        [ScriptSetting("Random Events", "Policy for handling random event NPCs", Order = 4)]
        public RandomEventHandling RandomEventsPolicy { get; set; } = RandomEventHandling.Dismiss;

        public override void OnStart()
        {
            Log("Barrows AIO v2.0 started. Defeating 6 brothers and looting crypt chest...");
        }

        public override void OnPaint(DrawingContext dc)
        {
            var bgBrush = new SolidColorBrush(Color.FromArgb(210, 20, 20, 20));
            var borderPen = new Pen(new SolidColorBrush(Color.FromArgb(255, 121, 85, 72)), 1.5);
            dc.DrawRoundedRectangle(bgBrush, borderPen, new Rect(15, 200, 220, 75), 6, 6);

            var typeFace = new Typeface("Segoe UI");
            var titleText = new FormattedText("⚰ Barrows Brothers AIO", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeFace, 12, Brushes.SandyBrown, 1.0);
            var ft1 = new FormattedText($"Task: {CurrentTaskName}", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeFace, 11, Brushes.White, 1.0);
            var ft2 = new FormattedText($"Runtime: {RunningTime:hh\\:mm\\:ss}", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeFace, 10, Brushes.BurlyWood, 1.0);

            dc.DrawText(titleText, new Point(25, 208));
            dc.DrawText(ft1, new Point(25, 228));
            dc.DrawText(ft2, new Point(25, 248));
        }

        public override async Task<int> OnLoopAsync()
        {
            if (State.Player == null) return 600;

            // 1. Health & Prayer Checks
            double hpPercent = State.Player.MaxHp > 0 ? (double)State.Player.CurrentHp / State.Player.MaxHp * 100 : 100;
            if (hpPercent <= EatThresholdPercent)
            {
                SetTask("Eating food to restore health...");
                await FoodCatalog.EatFoodAsync();
                return Antiban.HumanDelay(800, 1200);
            }

            if (State.Player.CurrentPrayer <= DrinkPrayerThreshold && InventoryActions.Contains("Prayer potion", "Super restore"))
            {
                SetTask("Drinking prayer potion...");
                await Combat.DrinkPrayerPotionAsync();
                return Antiban.HumanDelay(800, 1200);
            }

            // 2. Loot crypt chest if present
            var chest = Queries.Queries.Objects.Named("Chest", "Barrows chest").WithinDistance(8).Nearest();
            if (chest != null)
            {
                SetTask("Looting Barrows reward chest...");
                await Barrows.LootChestAsync();
                return Antiban.HumanDelay(2000, 3000);
            }

            // 3. Solve crypt puzzle door if visible
            var puzzleDoor = Queries.Queries.Objects.Named("Door").WithinDistance(5).Nearest();
            if (puzzleDoor != null)
            {
                SetTask("Solving Crypt puzzle door...");
                await Barrows.SolvePuzzleDoorAsync();
                return Antiban.HumanDelay(1500, 2500);
            }

            // 4. Fight active Barrows brother
            var activeBrother = Queries.Queries.Npcs.Filter(n =>
                n.Name.Equals("Ahrim the Blighted", StringComparison.OrdinalIgnoreCase) ||
                n.Name.Equals("Dharok the Wretched", StringComparison.OrdinalIgnoreCase) ||
                n.Name.Equals("Guthan the Infested", StringComparison.OrdinalIgnoreCase) ||
                n.Name.Equals("Karil the Tainted", StringComparison.OrdinalIgnoreCase) ||
                n.Name.Equals("Torag the Corrupted", StringComparison.OrdinalIgnoreCase) ||
                n.Name.Equals("Verac the Defiled", StringComparison.OrdinalIgnoreCase)).WithinDistance(15).Nearest();

            if (activeBrother != null)
            {
                SetTask($"Fighting {activeBrother.Name}...");
                await Barrows.SetBrotherPrayerAsync(activeBrother.Name);
                await activeBrother.InteractAsync("Attack");
                return Antiban.HumanDelay(1000, 1800);
            }

            // 5. Dig or search sarcophagus
            SetTask("Digging or searching Sarcophagus...");
            await Barrows.DigOrSearchSarcophagusAsync();
            return Antiban.HumanDelay(2000, 3000);
        }
    }

    // =========================================================================
    // 7. Blast Furnace Bot Script
    // =========================================================================
    [ScriptManifest(
        name: "Blast Furnace AIO",
        author: "osrsmr",
        version: "2.0.0",
        description: "Automates Blast Furnace: deposits ore onto conveyor belt, cools and collects smelted bars from dispenser, and banks.",
        category: ScriptCategory.Minigames)]
    public class BlastFurnaceScript : LoopScript
    {
        [ScriptSetting("Bar Type", "Type of metal bar to smelt", Order = 1, Options = new[] { "Steel bar", "Mithril bar", "Adamantite bar", "Runite bar", "Gold bar" })]
        public string BarType { get; set; } = "Steel bar";

        [ScriptSetting("Use Coal Bag", "Use coal bag to carry extra coal", Order = 2)]
        public bool UseCoalBag { get; set; } = true;

        [ScriptSetting("Drink Stamina Potions", "Drink stamina potion when run energy < 30%", Order = 3)]
        public bool DrinkStamina { get; set; } = true;

        [ScriptSetting("Random Events", "Policy for handling random event NPCs", Order = 4)]
        public RandomEventHandling RandomEventsPolicy { get; set; } = RandomEventHandling.Dismiss;

        public override void OnStart()
        {
            Log("Blast Furnace AIO v2.0 started. Tracking conveyor belt and bar dispenser...");
        }

        public override void OnPaint(DrawingContext dc)
        {
            var bgBrush = new SolidColorBrush(Color.FromArgb(210, 20, 20, 20));
            var borderPen = new Pen(new SolidColorBrush(Color.FromArgb(255, 255, 87, 34)), 1.5);
            dc.DrawRoundedRectangle(bgBrush, borderPen, new Rect(15, 200, 220, 75), 6, 6);

            var typeFace = new Typeface("Segoe UI");
            var titleText = new FormattedText("🔥 Blast Furnace AIO", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeFace, 12, Brushes.OrangeRed, 1.0);
            var ft1 = new FormattedText($"Task: {CurrentTaskName}", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeFace, 11, Brushes.White, 1.0);
            var ft2 = new FormattedText($"Runtime: {RunningTime:hh\\:mm\\:ss}", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeFace, 10, Brushes.Coral, 1.0);

            dc.DrawText(titleText, new Point(25, 208));
            dc.DrawText(ft1, new Point(25, 228));
            dc.DrawText(ft2, new Point(25, 248));
        }

        public override async Task<int> OnLoopAsync()
        {
            if (State.Player == null) return 600;

            // 1. Cool & retrieve smelted bars
            var dispenser = Queries.Queries.Objects.Named("Bar dispenser").WithinDistance(10).Nearest();
            if (dispenser != null && InventoryActions.FreeSlots > 10)
            {
                SetTask("Equipping Ice gloves and collecting smelted bars...");
                await BlastFurnace.CollectBarsAsync();
                return Antiban.HumanDelay(2000, 3000);
            }

            // 2. Put ore on conveyor belt
            if (InventoryActions.Contains("ore") || InventoryActions.Contains("Coal"))
            {
                SetTask("Placing ores on conveyor belt...");
                await BlastFurnace.LoadConveyorBeltAsync();
                return Antiban.HumanDelay(2000, 3000);
            }

            // 3. Bank for more ore / deposit bars
            if (Bank.IsOpen)
            {
                SetTask("Depositing bars and withdrawing ore...");
                await Bank.DepositAllExceptAsync("Bucket of water", "Coal bag", "Ice gloves", "Goldsmith gauntlets");
                return Antiban.HumanDelay(1000, 1500);
            }

            var bankChest = Queries.Queries.Objects.Named("Bank chest").WithinDistance(10).Nearest();
            if (bankChest != null)
            {
                SetTask("Opening Blast Furnace bank chest...");
                await bankChest.InteractAsync("Use");
                return Antiban.HumanDelay(1200, 1800);
            }

            return Antiban.HumanDelay(1500, 2500);
        }
    }

    // =========================================================================
    // 8. Tithe Farm Bot Script
    // =========================================================================
    [ScriptManifest(
        name: "Tithe Farm AIO",
        author: "osrsmr",
        version: "2.0.0",
        description: "Automates Tithe Farm: plants seeds, waters 4x4 cycles, refills watering cans, harvests fruit, and deposits sacks.",
        category: ScriptCategory.Minigames)]
    public class TitheFarmScript : LoopScript
    {
        [ScriptSetting("Seed Type", "Tithe Farm seed to plant", Order = 1, Options = new[] { "Golovanova seed", "Bologano seed", "Logavano seed" })]
        public string SeedType { get; set; } = "Golovanova seed";

        [ScriptSetting("Watering Rounds", "Number of watering rounds before harvesting", Order = 2)]
        public int WateringRounds { get; set; } = 3;

        [ScriptSetting("Random Events", "Policy for handling random event NPCs", Order = 3)]
        public RandomEventHandling RandomEventsPolicy { get; set; } = RandomEventHandling.Dismiss;

        public override void OnStart()
        {
            Log($"Tithe Farm AIO v2.0 started with Seed: {SeedType}...");
        }

        public override void OnPaint(DrawingContext dc)
        {
            var bgBrush = new SolidColorBrush(Color.FromArgb(210, 20, 20, 20));
            var borderPen = new Pen(new SolidColorBrush(Color.FromArgb(255, 139, 195, 74)), 1.5);
            dc.DrawRoundedRectangle(bgBrush, borderPen, new Rect(15, 200, 220, 75), 6, 6);

            var typeFace = new Typeface("Segoe UI");
            var titleText = new FormattedText("🌱 Tithe Farm AIO", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeFace, 12, Brushes.LightGreen, 1.0);
            var ft1 = new FormattedText($"Seed: {SeedType} | Task: {CurrentTaskName}", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeFace, 11, Brushes.White, 1.0);
            var ft2 = new FormattedText($"Runtime: {RunningTime:hh\\:mm\\:ss}", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeFace, 10, Brushes.PaleGreen, 1.0);

            dc.DrawText(titleText, new Point(25, 208));
            dc.DrawText(ft1, new Point(25, 228));
            dc.DrawText(ft2, new Point(25, 248));
        }

        public override async Task<int> OnLoopAsync()
        {
            if (State.Player == null) return 600;

            // 1. Refill watering can if empty
            if (!InventoryActions.Contains("Watering can(8)") && !InventoryActions.Contains("Gricoller's can") && InventoryActions.Contains("Watering can"))
            {
                SetTask("Refilling watering cans at water barrel...");
                await TitheFarm.RefillWateringCansAsync();
                return Antiban.HumanDelay(2000, 3000);
            }

            // 2. Harvest ripe fruit
            var ripeFruit = Queries.Queries.Objects.Filter(o => o.Name.Contains("fruit", StringComparison.OrdinalIgnoreCase) || o.Name.Contains("ripe", StringComparison.OrdinalIgnoreCase)).WithinDistance(8).Nearest();
            if (ripeFruit != null)
            {
                SetTask("Harvesting ripe tithe fruit...");
                await TitheFarm.HarvestFruitAsync();
                return Antiban.HumanDelay(1500, 2200);
            }

            // 3. Deposit sack if holding 100 fruit or full inventory
            if (InventoryActions.Contains("fruit") && InventoryActions.IsFull)
            {
                SetTask("Depositing harvested fruit into sack...");
                await TitheFarm.DepositSackAsync();
                return Antiban.HumanDelay(2000, 3000);
            }

            // 4. Water growing plants
            var unwateredPlant = Queries.Queries.Objects.Filter(o => o.Name.Contains("plant", StringComparison.OrdinalIgnoreCase) && !o.Name.Contains("dead", StringComparison.OrdinalIgnoreCase)).WithinDistance(8).Nearest();
            if (unwateredPlant != null)
            {
                SetTask("Watering growing Tithe plant...");
                await TitheFarm.WaterPlantAsync();
                return Antiban.HumanDelay(1500, 2200);
            }

            // 5. Plant seeds in empty patches
            SetTask($"Planting {SeedType}...");
            await TitheFarm.PlantSeedsAsync(SeedType);
            return Antiban.HumanDelay(1500, 2500);
        }
    }

    // =========================================================================
    // 9. Fishing Trawler Bot Script
    // =========================================================================
    [ScriptManifest(
        name: "Fishing Trawler AIO",
        author: "osrsmr",
        version: "2.0.0",
        description: "Automates Fishing Trawler: bails water, repairs leaks with swamp paste, fixes torn nets, and loots catch.",
        category: ScriptCategory.Minigames)]
    public class FishingTrawlerScript : LoopScript
    {
        [ScriptSetting("Bail Water", "Bail water out of sinking boat", Order = 1)]
        public bool BailWater { get; set; } = true;

        [ScriptSetting("Fix Leaks", "Use swamp paste to repair holes in boat hull", Order = 2)]
        public bool FixLeaks { get; set; } = true;

        [ScriptSetting("Eat at HP %", "Health threshold to automatically eat food", Order = 3)]
        public int EatThresholdPercent { get; set; } = 40;

        [ScriptSetting("Random Events", "Policy for handling random event NPCs", Order = 4)]
        public RandomEventHandling RandomEventsPolicy { get; set; } = RandomEventHandling.Dismiss;

        public override void OnStart()
        {
            Log("Fishing Trawler AIO v2.0 started. Monitoring leaks, water level, and net health...");
        }

        public override void OnPaint(DrawingContext dc)
        {
            var bgBrush = new SolidColorBrush(Color.FromArgb(210, 20, 20, 20));
            var borderPen = new Pen(new SolidColorBrush(Color.FromArgb(255, 0, 188, 212)), 1.5);
            dc.DrawRoundedRectangle(bgBrush, borderPen, new Rect(15, 200, 220, 75), 6, 6);

            var typeFace = new Typeface("Segoe UI");
            var titleText = new FormattedText("⛵ Fishing Trawler AIO", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeFace, 12, Brushes.Cyan, 1.0);
            var ft1 = new FormattedText($"Task: {CurrentTaskName}", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeFace, 11, Brushes.White, 1.0);
            var ft2 = new FormattedText($"Runtime: {RunningTime:hh\\:mm\\:ss}", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeFace, 10, Brushes.PaleTurquoise, 1.0);

            dc.DrawText(titleText, new Point(25, 208));
            dc.DrawText(ft1, new Point(25, 228));
            dc.DrawText(ft2, new Point(25, 248));
        }

        public override async Task<int> OnLoopAsync()
        {
            if (State.Player == null) return 600;

            // 1. Loot catch net if on dock
            var catchNet = Queries.Queries.Objects.Named("Trawler catch", "Catch net").WithinDistance(15).Nearest();
            if (catchNet != null && !FishingTrawler.IsInTrawler)
            {
                SetTask("Inspecting and looting trawler catch...");
                await FishingTrawler.LootCatchAsync();
                return Antiban.HumanDelay(2000, 3000);
            }

            // 2. Repair leaks
            var leak = Queries.Queries.Objects.Named("Leak").WithinDistance(8).Nearest();
            if (leak != null && InventoryActions.Contains("Swamp paste"))
            {
                SetTask("Plugging boat leak with swamp paste...");
                await FishingTrawler.RepairLeakAsync();
                return Antiban.HumanDelay(1200, 1800);
            }

            // 3. Fix torn nets
            var tornNet = Queries.Queries.Objects.Named("Torn net", "Net").WithinDistance(10).Nearest();
            if (tornNet != null && InventoryActions.Contains("Rope"))
            {
                SetTask("Repairing torn fishing net on deck...");
                await FishingTrawler.FixTornNetAsync();
                return Antiban.HumanDelay(1500, 2200);
            }

            // 4. Bail water
            SetTask("Bailing water out of the hull...");
            await FishingTrawler.BailWaterAsync();
            return Antiban.HumanDelay(1200, 1800);
        }
    }
}
