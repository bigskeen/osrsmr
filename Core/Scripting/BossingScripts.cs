using System;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using OsrsMr.Core.Bossing;
using OsrsMr.Core.Data;
using OsrsMr.Core.Input;
using OsrsMr.Core.Interaction;
using OsrsMr.Core.Queries;

namespace OsrsMr.Core.Scripting
{
    // =========================================================================
    // 1. Auto Zulrah Boss Bot Script
    // =========================================================================
    [ScriptManifest(
        name: "Auto Zulrah AIO",
        author: "osrsmr",
        version: "1.0.0",
        description: "Autonomous Zulrah killer: phase detection, gear swapping, overhead prayers, toxic cloud avoidance, recoil maintenance, and venom curing.",
        category: ScriptCategory.Bossing)]
    public class AutoZulrahScript : LoopScript
    {
        private int _killsCompleted = 0;

        [ScriptSetting("Magic Gear Set", "Comma-separated list of magic items to wear", Order = 1)]
        public string MagicGear { get; set; } = "Trident of the swamp, Ahrim's robetop, Ahrim's robeskirt, Occult necklace";

        [ScriptSetting("Range Gear Set", "Comma-separated list of ranged items to wear", Order = 2)]
        public string RangeGear { get; set; } = "Toxic blowpipe, Blessed d'hide body, Blessed d'hide chaps, Necklace of anguish";

        [ScriptSetting("Eat at HP %", "Health threshold to automatically eat food", Order = 3)]
        public int EatThresholdPercent { get; set; } = 55;

        [ScriptSetting("Special Attack Weapon", "Weapon to use for special attack (e.g., Dragon warhammer, Bandos godsword)", Order = 4)]
        public string SpecWeapon { get; set; } = "";

        public override void OnStart()
        {
            Log("Auto Zulrah AIO initialized. Monitoring phase transitions and venom clouds...");
        }

        public override void OnPaint(DrawingContext dc)
        {
            var bgBrush = new SolidColorBrush(Color.FromArgb(220, 20, 20, 20));
            var borderPen = new Pen(new SolidColorBrush(Color.FromArgb(255, 0, 200, 115)), 1.5);
            dc.DrawRoundedRectangle(bgBrush, borderPen, new Rect(15, 200, 230, 80), 6, 6);

            var typeFace = new Typeface("Segoe UI");
            var titleText = new FormattedText("🐍 Auto Zulrah AIO", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeFace, 12, Brushes.SpringGreen, 1.0);
            var ft1 = new FormattedText($"Task: {CurrentTaskName}", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeFace, 11, Brushes.White, 1.0);
            var ft2 = new FormattedText($"Runtime: {RunningTime:hh\\:mm\\:ss} | Kills: {_killsCompleted}", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeFace, 10, Brushes.LightGreen, 1.0);

            dc.DrawText(titleText, new Point(25, 208));
            dc.DrawText(ft1, new Point(25, 228));
            dc.DrawText(ft2, new Point(25, 248));
        }

        public override async Task<int> OnLoopAsync()
        {
            if (State.Player == null) return 600;

            // 1. Health check / Auto-eat
            double hpPercent = State.Player.MaxHp > 0 ? (double)State.Player.CurrentHp / State.Player.MaxHp * 100 : 100;
            if (hpPercent <= EatThresholdPercent)
            {
                SetTask("Eating food to restore health...");
                await FoodCatalog.EatFoodAsync();
                return Antiban.HumanDelay(600, 900);
            }

            // 2. Prayer restore
            if (Prayers.CurrentPoints < 25)
            {
                SetTask("Restoring prayer points...");
                var pot = Queries.Queries.Inventory
                    .Filter(i => i.Name.Contains("Prayer potion", StringComparison.OrdinalIgnoreCase) ||
                                 i.Name.Contains("Super restore", StringComparison.OrdinalIgnoreCase))
                    .First();
                if (pot != null)
                {
                    await pot.InteractAsync("Drink");
                    return Antiban.HumanDelay(400, 700);
                }
            }

            // 3. Venom / Poison cure
            if (await ZulrahController.HandleVenomCureAsync())
            {
                SetTask("Drinking venom cure...");
                return Antiban.HumanDelay(300, 500);
            }

            // 4. Recoil ring upkeep
            if (await ZulrahController.HandleRecoilRingAsync())
            {
                SetTask("Equipping replacement recoil ring...");
                return Antiban.HumanDelay(300, 500);
            }

            // 5. Detect Zulrah
            var zulrah = ZulrahController.GetZulrahNpc();
            if (zulrah == null)
            {
                // Check if ground items need looting
                var loot = Queries.Queries.GroundItems.WithinDistance(15).First();
                if (loot != null && !InventoryActions.IsFull)
                {
                    SetTask($"Looting {loot.Name}...");
                    await loot.TakeAsync();
                    return Antiban.HumanDelay(1000, 1500);
                }

                SetTask("Waiting for Zulrah to emerge...");
                return Antiban.HumanDelay(600, 1000);
            }

            var phase = ZulrahController.GetCurrentPhase();

            // 6. Handle Prayers for current phase
            SetTask($"Praying for phase: {phase}");
            await ZulrahController.HandlePrayersAsync(phase);

            // 7. Handle Gear Swap
            string[] mageItems = MagicGear.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
            string[] rangeItems = RangeGear.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
            await ZulrahController.HandleGearSwapAsync(phase, mageItems, rangeItems);

            // 8. Special attack if configured
            if (!string.IsNullOrWhiteSpace(SpecWeapon) && State.Player.SpecPercent >= 50)
            {
                SetTask("Executing special attack...");
                await CombatPvM.ExecuteSpecialAttackAsync(SpecWeapon, mageItems.FirstOrDefault() ?? "");
                return Antiban.HumanDelay(800, 1200);
            }

            // 9. Attack Zulrah if not already attacking
            if (!State.Player.IsAttacking)
            {
                SetTask("Attacking Zulrah...");
                await zulrah.InteractAsync("Attack");
                return Antiban.HumanDelay(1200, 1800);
            }

            return Antiban.HumanDelay(400, 800);
        }
    }

    // =========================================================================
    // 2. Auto Vorkath Boss Bot Script
    // =========================================================================
    [ScriptManifest(
        name: "Auto Vorkath AIO",
        author: "osrsmr",
        version: "1.0.0",
        description: "Autonomous Vorkath killer: fireball dodge, acid pool evasion, Zombified Spawn one-tick Crumble Undead cast, potion upkeep, and loot collection.",
        category: ScriptCategory.Bossing)]
    public class AutoVorkathScript : LoopScript
    {
        private int _killsCompleted = 0;

        [ScriptSetting("Protect from Magic", "Pray Protect from Magic (otherwise Protect from Missiles)", Order = 1)]
        public bool PrayMagic { get; set; } = true;

        [ScriptSetting("Slayer Staff", "Staff to equip for auto-casting Crumble Undead", Order = 2)]
        public string SlayerStaff { get; set; } = "Slayer's staff";

        [ScriptSetting("Eat at HP %", "Health threshold to automatically eat food", Order = 3)]
        public int EatThresholdPercent { get; set; } = 60;

        public override void OnStart()
        {
            Log("Auto Vorkath AIO started. Monitoring fireball projectiles, acid pools, and zombified spawns...");
        }

        public override void OnPaint(DrawingContext dc)
        {
            var bgBrush = new SolidColorBrush(Color.FromArgb(220, 20, 20, 20));
            var borderPen = new Pen(new SolidColorBrush(Color.FromArgb(255, 70, 130, 240)), 1.5);
            dc.DrawRoundedRectangle(bgBrush, borderPen, new Rect(15, 200, 230, 80), 6, 6);

            var typeFace = new Typeface("Segoe UI");
            var titleText = new FormattedText("🐉 Auto Vorkath AIO", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeFace, 12, Brushes.LightSkyBlue, 1.0);
            var ft1 = new FormattedText($"Task: {CurrentTaskName}", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeFace, 11, Brushes.White, 1.0);
            var ft2 = new FormattedText($"Runtime: {RunningTime:hh\\:mm\\:ss} | Kills: {_killsCompleted}", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeFace, 10, Brushes.DeepSkyBlue, 1.0);

            dc.DrawText(titleText, new Point(25, 208));
            dc.DrawText(ft1, new Point(25, 228));
            dc.DrawText(ft2, new Point(25, 248));
        }

        public override async Task<int> OnLoopAsync()
        {
            if (State.Player == null) return 600;

            // 1. Fireball projectile avoidance (Immediate priority)
            if (VorkathController.IsLethalFireballIncoming())
            {
                SetTask("DODGING FIREBALL!");
                var step = VorkathController.FindSafeStepTile();
                if (step.HasValue)
                {
                    await Movement.WalkToAsync(step.Value.x, step.Value.y);
                }
                return Antiban.HumanDelay(500, 800);
            }

            // 2. Handle Zombified Spawn (Immediate priority)
            var spawn = Queries.Queries.Npcs.Named("Zombified Spawn").WithinDistance(15).Nearest();
            if (spawn != null)
            {
                SetTask("Casting Crumble Undead on Zombified Spawn...");
                await VorkathController.HandleZombifiedSpawnAsync(SlayerStaff);
                return Antiban.HumanDelay(800, 1200);
            }

            // 3. Acid pool evasion
            if (VorkathController.IsAcidPhaseActive() || VorkathController.IsPlayerInAcidPool())
            {
                SetTask("Navigating safe tiles through acid pool...");
                var safeTile = VorkathController.FindSafeStepTile();
                if (safeTile.HasValue)
                {
                    await Movement.WalkToAsync(safeTile.Value.x, safeTile.Value.y);
                }
                return Antiban.HumanDelay(400, 600);
            }

            // 4. Health & Potion maintenance
            double hpPercent = State.Player.MaxHp > 0 ? (double)State.Player.CurrentHp / State.Player.MaxHp * 100 : 100;
            if (hpPercent <= EatThresholdPercent)
            {
                SetTask("Eating food...");
                await FoodCatalog.EatFoodAsync();
                return Antiban.HumanDelay(600, 900);
            }

            await VorkathController.MaintainBuffsAsync();

            // 5. Detect Vorkath
            var vorkath = VorkathController.GetVorkathNpc();
            if (vorkath == null)
            {
                // Check if Vorkath is sleeping (Poke to wake)
                var sleepingVorkath = Queries.Queries.Npcs.Named("Vorkath").Filter(n => n.Id == VorkathController.VorkathSleepingNpcId).First();
                if (sleepingVorkath != null)
                {
                    SetTask("Poking sleeping Vorkath...");
                    await sleepingVorkath.InteractAsync("Poke");
                    return Antiban.HumanDelay(2000, 3000);
                }

                // Check loot
                var loot = Queries.Queries.GroundItems.WithinDistance(15).First();
                if (loot != null && !InventoryActions.IsFull)
                {
                    SetTask($"Looting {loot.Name}...");
                    await loot.TakeAsync();
                    return Antiban.HumanDelay(1000, 1500);
                }

                SetTask("Waiting for Vorkath...");
                return Antiban.HumanDelay(800, 1200);
            }

            // 6. Prayers
            await VorkathController.HandlePrayersAsync(PrayMagic);

            // 7. Attack Vorkath
            if (!State.Player.IsAttacking)
            {
                SetTask("Attacking Vorkath...");
                await vorkath.InteractAsync("Attack");
                return Antiban.HumanDelay(1200, 1800);
            }

            return Antiban.HumanDelay(400, 700);
        }
    }

    // =========================================================================
    // 3. Auto Giant Mole Bot Script
    // =========================================================================
    [ScriptManifest(
        name: "Auto Giant Mole AIO",
        author: "osrsmr",
        version: "1.0.0",
        description: "Autonomous Giant Mole killer: Dharok 1-HP guzzling, Falador park spade entrance, prayer protection, stamina upkeep, and burrow tracking.",
        category: ScriptCategory.Bossing)]
    public class AutoGiantMoleScript : LoopScript
    {
        private int _killsCompleted = 0;

        [ScriptSetting("Use Dharok 1-HP Method", "Maintain 1 HP with rock cake / locator orb for max Dharok DPS", Order = 1)]
        public bool UseDharokMethod { get; set; } = true;

        [ScriptSetting("Eat at HP %", "Health threshold to eat if not using Dharok method", Order = 2)]
        public int EatThresholdPercent { get; set; } = 40;

        public override void OnStart()
        {
            Log("Auto Giant Mole AIO started. Verifying spade, light source, and Falador shield...");
        }

        public override void OnPaint(DrawingContext dc)
        {
            var bgBrush = new SolidColorBrush(Color.FromArgb(220, 20, 20, 20));
            var borderPen = new Pen(new SolidColorBrush(Color.FromArgb(255, 200, 150, 60)), 1.5);
            dc.DrawRoundedRectangle(bgBrush, borderPen, new Rect(15, 200, 230, 80), 6, 6);

            var typeFace = new Typeface("Segoe UI");
            var titleText = new FormattedText("🐾 Auto Giant Mole AIO", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeFace, 12, Brushes.BurlyWood, 1.0);
            var ft1 = new FormattedText($"Task: {CurrentTaskName}", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeFace, 11, Brushes.White, 1.0);
            var ft2 = new FormattedText($"Runtime: {RunningTime:hh\\:mm\\:ss} | Kills: {_killsCompleted}", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeFace, 10, Brushes.Gold, 1.0);

            dc.DrawText(titleText, new Point(25, 208));
            dc.DrawText(ft1, new Point(25, 228));
            dc.DrawText(ft2, new Point(25, 248));
        }

        public override async Task<int> OnLoopAsync()
        {
            if (State.Player == null) return 600;

            // 1. Maintain Protect from Melee
            if (!Prayers.IsActive(Prayer.ProtectFromMelee))
            {
                SetTask("Enabling Protect from Melee...");
                await Prayers.SetActiveAsync(Prayer.ProtectFromMelee, true);
                await CombatPvM.SetOffensivePrayerAsync("melee");
            }

            // 2. Health & Potion maintenance
            if (UseDharokMethod)
            {
                await GiantMoleController.MaintainDharokHealthAsync();
            }
            else
            {
                double hpPercent = State.Player.MaxHp > 0 ? (double)State.Player.CurrentHp / State.Player.MaxHp * 100 : 100;
                if (hpPercent <= EatThresholdPercent)
                {
                    SetTask("Eating food...");
                    await FoodCatalog.EatFoodAsync();
                }
            }

            await GiantMoleController.MaintainPotionsAsync();

            // 3. Locate Mole
            var mole = GiantMoleController.GetMoleNpc();
            if (mole == null)
            {
                // Check if in surface (need to dig)
                var moleHill = Queries.Queries.Objects.Named("Mole hill").WithinDistance(10).First();
                if (moleHill != null)
                {
                    SetTask("Digging into Mole cavern...");
                    await GiantMoleController.EnterMoleCavernAsync();
                    return Antiban.HumanDelay(1500, 2500);
                }

                // Check loot
                var loot = Queries.Queries.GroundItems.WithinDistance(15).Filter(g => g.Name.Contains("Mole claw") || g.Name.Contains("Mole skin")).First();
                if (loot != null && !InventoryActions.IsFull)
                {
                    SetTask($"Looting {loot.Name}...");
                    await loot.TakeAsync();
                    return Antiban.HumanDelay(800, 1200);
                }

                SetTask("Searching cavern for burrowed Mole...");
                return Antiban.HumanDelay(800, 1400);
            }

            // 4. Attack Mole
            if (!State.Player.IsAttacking)
            {
                SetTask("Attacking Giant Mole...");
                await mole.InteractAsync("Attack");
                return Antiban.HumanDelay(1500, 2200);
            }

            return Antiban.HumanDelay(400, 700);
        }
    }

    // =========================================================================
    // 4. Auto Dagannoth Kings Bot Script
    // =========================================================================
    [ScriptManifest(
        name: "Auto Dagannoth Kings AIO",
        author: "osrsmr",
        version: "1.0.0",
        description: "Autonomous Dagannoth Kings killer: Tribrid gear swapping, overhead prayer switching (Prime/Supreme/Rex), and target prioritization.",
        category: ScriptCategory.Bossing)]
    public class AutoDagannothKingsScript : LoopScript
    {
        private int _killsCompleted = 0;

        [ScriptSetting("Magic Gear (for Rex)", "Magic equipment set", Order = 1)]
        public string MagicGear { get; set; } = "Trident of the seas, Occult necklace, Mystic robe top";

        [ScriptSetting("Ranged Gear (for Prime)", "Ranged equipment set", Order = 2)]
        public string RangeGear { get; set; } = "Toxic blowpipe, Blessed d'hide body, Necklace of anguish";

        [ScriptSetting("Melee Gear (for Supreme)", "Melee equipment set", Order = 3)]
        public string MeleeGear { get; set; } = "Abyssal whip, Dragon defender, Amulet of fury";

        [ScriptSetting("Eat at HP %", "Health threshold to automatically eat food", Order = 4)]
        public int EatThresholdPercent { get; set; } = 50;

        public override void OnStart()
        {
            Log("Auto Dagannoth Kings AIO started. Tracking Prime, Supreme, and Rex spawns...");
        }

        public override void OnPaint(DrawingContext dc)
        {
            var bgBrush = new SolidColorBrush(Color.FromArgb(220, 20, 20, 20));
            var borderPen = new Pen(new SolidColorBrush(Color.FromArgb(255, 180, 50, 180)), 1.5);
            dc.DrawRoundedRectangle(bgBrush, borderPen, new Rect(15, 200, 230, 80), 6, 6);

            var typeFace = new Typeface("Segoe UI");
            var titleText = new FormattedText("👑 Auto Dagannoth Kings", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeFace, 12, Brushes.Violet, 1.0);
            var ft1 = new FormattedText($"Task: {CurrentTaskName}", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeFace, 11, Brushes.White, 1.0);
            var ft2 = new FormattedText($"Runtime: {RunningTime:hh\\:mm\\:ss} | Kills: {_killsCompleted}", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeFace, 10, Brushes.Plum, 1.0);

            dc.DrawText(titleText, new Point(25, 208));
            dc.DrawText(ft1, new Point(25, 228));
            dc.DrawText(ft2, new Point(25, 248));
        }

        public override async Task<int> OnLoopAsync()
        {
            if (State.Player == null) return 600;

            // 1. Health check & Prayer restore
            double hpPercent = State.Player.MaxHp > 0 ? (double)State.Player.CurrentHp / State.Player.MaxHp * 100 : 100;
            if (hpPercent <= EatThresholdPercent)
            {
                SetTask("Eating food...");
                await FoodCatalog.EatFoodAsync();
                return Antiban.HumanDelay(600, 900);
            }

            if (Prayers.CurrentPoints < 25)
            {
                SetTask("Drinking prayer potion...");
                var pot = Queries.Queries.Inventory
                    .Filter(i => i.Name.Contains("Prayer potion", StringComparison.OrdinalIgnoreCase) ||
                                 i.Name.Contains("Super restore", StringComparison.OrdinalIgnoreCase))
                    .First();
                if (pot != null)
                {
                    await pot.InteractAsync("Drink");
                    return Antiban.HumanDelay(350, 600);
                }
            }

            // 2. Identify target king
            var targetBoss = DagannothKingsController.GetCurrentTargetBoss();
            var activeKing = DagannothKingsController.GetActiveKing();

            if (activeKing == null || targetBoss == DkBoss.None)
            {
                // Check loot
                var loot = Queries.Queries.GroundItems.WithinDistance(15).Filter(g => g.Name.Contains("Berserker ring") || g.Name.Contains("Archers ring") || g.Name.Contains("Seers ring") || g.Name.Contains("Dragon axe")).First();
                if (loot != null && !InventoryActions.IsFull)
                {
                    SetTask($"Looting {loot.Name}...");
                    await loot.TakeAsync();
                    return Antiban.HumanDelay(800, 1200);
                }

                SetTask("Waiting for next King spawn...");
                return Antiban.HumanDelay(800, 1400);
            }

            // 3. Set prayers for target king
            SetTask($"Engaging {targetBoss} - setting prayers...");
            await DagannothKingsController.HandlePrayersAsync(targetBoss);

            // 4. Swap gear
            string[] mage = MagicGear.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
            string[] range = RangeGear.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
            string[] melee = MeleeGear.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
            await DagannothKingsController.HandleGearSwapAsync(targetBoss, mage, range, melee);

            // 5. Attack target King
            if (!State.Player.IsAttacking)
            {
                SetTask($"Attacking {activeKing.Name}...");
                await activeKing.InteractAsync("Attack");
                return Antiban.HumanDelay(1200, 1800);
            }

            return Antiban.HumanDelay(400, 700);
        }
    }
}
