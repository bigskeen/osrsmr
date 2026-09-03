using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media;
using OsrsMr.Core.Data;
using OsrsMr.Core.Input;
using OsrsMr.Core.Interaction;
using OsrsMr.Core.Queries;
using OsrsMr.Core.Slayer;
using OsrsMr.Core.Spatial;
using OsrsMr.Core.Wilderness;

namespace OsrsMr.Core.Scripting
{
    [ScriptManifest(
        name: "Auto Slayer AIO",
        author: "Community",
        version: "1.0.0",
        description: "Universal Slayer combat engine with automatic protective gear checks, finishing item blows, and smart ground looting.",
        category: ScriptCategory.Slayer
    )]
    public class AutoSlayerScript : LoopScript
    {
        [ScriptSetting("Monster Name", "Target Slayer monster to attack (e.g. Bloodveld, Gargoyle, Aberrant spectre)", Order = 1, DefaultValue = "Bloodveld")]
        public string MonsterName { get; set; } = "Bloodveld";

        [ScriptSetting("Food Name", "Food item name to consume for healing", Order = 2, DefaultValue = "Shark")]
        public string FoodName { get; set; } = "Shark";

        [ScriptSetting("Eat HP %", "Health percentage threshold to eat food", Order = 3, DefaultValue = 55)]
        public int EatHpPercent { get; set; } = 55;

        [ScriptSetting("Use Special Attack", "Automatically triggers weapon special attack when energy is available", Order = 4, DefaultValue = true)]
        public bool UseSpecialAttack { get; set; } = true;

        [ScriptSetting("Loot Minimum Value (GP)", "Minimum ground item value to pick up", Order = 5, DefaultValue = 1000)]
        public int LootThresholdGp { get; set; } = 1000;

        private int _killsCount = 0;
        private string _status = "Initializing";

        public override async Task<int> OnLoopAsync()
        {
            if (State.Player == null) return 600;

            // 1. Health & Food check
            int hpPercent = Combat.GetHealthPercent();
            if (hpPercent <= EatHpPercent)
            {
                _status = "Eating Food";
                await Combat.EatFoodAsync(FoodName);
                return Antiban.RandomDelay(300, 600);
            }

            // 2. Prayer check
            if (State.Player != null && State.Player.CurrentPrayer < 15)
            {
                var pot = Queries.Queries.Inventory
                    .Filter(i => i.Name.Contains("Prayer potion", StringComparison.OrdinalIgnoreCase) ||
                                 i.Name.Contains("Super restore", StringComparison.OrdinalIgnoreCase))
                    .First();

                if (pot != null)
                {
                    _status = "Drinking Prayer Pot";
                    await pot.InteractAsync("Drink");
                    return Antiban.RandomDelay(300, 600);
                }
            }

            // 3. Check for finishing blows on nearby low health monsters
            var finishTarget = Queries.Queries.Npcs
                .Named(MonsterName)
                .WithinDistance(8)
                .Filter(n => n.Health == "10%" || n.Health == "0%" || (n.CurrentHp > 0 && n.CurrentHp <= 10))
                .First();

            if (finishTarget != null && SlayerManager.GetFinishingItemForMonster(MonsterName) != null)
            {
                _status = $"Finishing {MonsterName}";
                if (await SlayerManager.FinishMonsterAsync(finishTarget))
                {
                    _killsCount++;
                    return Antiban.RandomDelay(600, 1000);
                }
            }

            // 4. Ground Loot Check
            if (await Looting.LootGroundItemsAsync(LootThresholdGp, lootClues: true, lootUntradeables: true))
            {
                _status = "Looting Ground Items";
                return Antiban.RandomDelay(600, 1200);
            }

            // 5. Special Attack
            if (UseSpecialAttack && State.Player != null && State.Player.SpecPercent >= 50)
            {
                await Combat.ToggleSpecialAttackAsync();
            }

            // 6. Check if already in combat
            var target = Queries.Queries.Npcs.Named(MonsterName).InteractingWithMe().First();
            if (target != null && target.CurrentHp > 0)
            {
                _status = $"Fighting {MonsterName}";
                return Antiban.RandomDelay(600, 1200);
            }

            // 7. Find next available monster
            var nextMonster = Queries.Queries.Npcs
                .Named(MonsterName)
                .WithinDistance(15)
                .Alive()
                .Nearest();

            if (nextMonster != null)
            {
                _status = $"Attacking {MonsterName}";
                await nextMonster.InteractAsync("Attack");
                await Condition.SleepAsync(800, 1400);
                return Antiban.RandomDelay(600, 1200);
            }

            _status = "Searching for Monster";
            return Antiban.RandomDelay(800, 1500);
        }

        public override void OnPaint(DrawingContext dc)
        {
            var font = new Typeface("Segoe UI");
            var titleText = new FormattedText("⚔️ Auto Slayer AIO",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Windows.FlowDirection.LeftToRight,
                font, 14, new SolidColorBrush(Color.FromRgb(244, 63, 94)), 1.0);

            var statusText = new FormattedText($"Status: {_status} | Monster: {MonsterName} | Kills: {_killsCount}",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Windows.FlowDirection.LeftToRight,
                font, 12, Brushes.White, 1.0);

            dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(200, 20, 20, 25)), new Pen(new SolidColorBrush(Color.FromRgb(244, 63, 94)), 1), new System.Windows.Rect(10, 35, 340, 50));
            dc.DrawText(titleText, new System.Windows.Point(20, 40));
            dc.DrawText(statusText, new System.Windows.Point(20, 60));
        }
    }

    [ScriptManifest(
        name: "Auto Wilderness Green Dragons",
        author: "Community",
        version: "1.0.0",
        description: "Autonomous Wilderness Green Dragon killer with active antifire protection, PK threat detection, and emergency teleport evasion.",
        category: ScriptCategory.Wilderness
    )]
    public class AutoWildernessGreenDragonsScript : LoopScript
    {
        [ScriptSetting("Food Name", "Food name for healing", Order = 1, DefaultValue = "Shark")]
        public string FoodName { get; set; } = "Shark";

        [ScriptSetting("Eat HP %", "Health percentage to eat food", Order = 2, DefaultValue = 60)]
        public int EatHpPercent { get; set; } = 60;

        [ScriptSetting("Emergency Escape on PK", "Instantly escape and teleport if a hostile player enters combat range", Order = 3, DefaultValue = true)]
        public bool AvoidPkers { get; set; } = true;

        [ScriptSetting("Bank Destination", "Bank to deposit dragon bones and hides", Order = 4, DefaultValue = BankLocation.Edgeville)]
        public BankLocation BankDest { get; set; } = BankLocation.Edgeville;

        private int _dragonsKilled = 0;
        private string _status = "Initializing";

        public override async Task<int> OnLoopAsync()
        {
            if (State.Player == null) return 600;

            // 1. PK Threat Detection & Evasion
            if (AvoidPkers && WildernessManager.IsInWilderness)
            {
                var threats = WildernessManager.GetThreatPlayers();
                if (threats.Any())
                {
                    _status = "⚠️ PK THREAT DETECTED! ESCAPING!";
                    await WildernessManager.HandleDefensiveOverheadsAsync();
                    await WildernessManager.EmergencyEscapeAsync();
                    return Antiban.RandomDelay(500, 1000);
                }
            }

            // 2. Health check
            if (Combat.GetHealthPercent() <= EatHpPercent)
            {
                _status = "Eating Food";
                await Combat.EatFoodAsync(FoodName);
                return Antiban.RandomDelay(300, 600);
            }

            // 3. Antifire potion upkeep
            if (!State.StatusEffects.HasAntifire)
            {
                var pot = Queries.Queries.Inventory
                    .Filter(i => i.Name.Contains("Antifire potion", StringComparison.OrdinalIgnoreCase) ||
                                 i.Name.Contains("Extended antifire", StringComparison.OrdinalIgnoreCase))
                    .First();

                if (pot != null)
                {
                    _status = "Drinking Antifire";
                    await pot.InteractAsync("Drink");
                    return Antiban.RandomDelay(300, 600);
                }
            }

            // 4. Inventory Full -> Bank at Edgeville
            if (InventoryActions.IsFull)
            {
                _status = "Banking Loot";
                if (Bank.IsOpen)
                {
                    await Bank.DepositAllExceptAsync(FoodName, "Extended antifire", "Antifire potion", "Royal seed pod", "Amulet of glory");
                    await Condition.SleepAsync(400, 800);
                    await Bank.CloseAsync();
                    return Antiban.RandomDelay(600, 1200);
                }

                await WebWalker.WalkToBankAsync(BankDest);
                return Antiban.RandomDelay(1000, 2000);
            }

            // 5. Ground Looting (Dragon bones, Green dragonhide, Clue scrolls)
            if (await Looting.LootGroundItemsAsync(minValueThreshold: 1500, lootClues: true, lootUntradeables: true))
            {
                _status = "Looting Dragon Drops";
                return Antiban.RandomDelay(600, 1200);
            }

            // 6. Check active combat
            var activeDragon = Queries.Queries.Npcs.Named("Green dragon").InteractingWithMe().First();
            if (activeDragon != null && activeDragon.CurrentHp > 0)
            {
                _status = "Fighting Green Dragon";
                return Antiban.RandomDelay(600, 1200);
            }

            // 7. Attack next dragon
            var nextDragon = Queries.Queries.Npcs.Named("Green dragon").WithinDistance(15).Alive().Nearest();
            if (nextDragon != null)
            {
                _status = "Attacking Green Dragon";
                if (await nextDragon.InteractAsync("Attack"))
                {
                    _dragonsKilled++;
                    await Condition.SleepAsync(800, 1500);
                }
                return Antiban.RandomDelay(600, 1200);
            }

            _status = "Searching for Green Dragon";
            return Antiban.RandomDelay(800, 1600);
        }

        public override void OnPaint(DrawingContext dc)
        {
            var font = new Typeface("Segoe UI");
            var titleText = new FormattedText("☠️ Auto Wilderness Green Dragons",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Windows.FlowDirection.LeftToRight,
                font, 14, new SolidColorBrush(Color.FromRgb(239, 68, 68)), 1.0);

            var statusText = new FormattedText($"Status: {_status} | Wild Lvl: {WildernessManager.CurrentWildernessLevel} | Dragons: {_dragonsKilled}",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Windows.FlowDirection.LeftToRight,
                font, 12, Brushes.White, 1.0);

            dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(200, 25, 15, 15)), new Pen(new SolidColorBrush(Color.FromRgb(239, 68, 68)), 1), new System.Windows.Rect(10, 35, 360, 50));
            dc.DrawText(titleText, new System.Windows.Point(20, 40));
            dc.DrawText(statusText, new System.Windows.Point(20, 60));
        }
    }

    [ScriptManifest(
        name: "Auto Chaos Druids AIO",
        author: "Community",
        version: "1.0.0",
        description: "Attacks Chaos Druids for valuable grimy herbs, seeds, and runes with automated food eating and banking.",
        category: ScriptCategory.Combat
    )]
    public class AutoChaosDruidsScript : LoopScript
    {
        [ScriptSetting("Food Name", "Food item for healing", Order = 1, DefaultValue = "Lobster")]
        public string FoodName { get; set; } = "Lobster";

        [ScriptSetting("Eat HP %", "Health percentage to eat", Order = 2, DefaultValue = 50)]
        public int EatHpPercent { get; set; } = 50;

        [ScriptSetting("Bank Destination", "Bank location when inventory is full of herbs", Order = 3, DefaultValue = BankLocation.Edgeville)]
        public BankLocation BankDest { get; set; } = BankLocation.Edgeville;

        private int _herbsLooted = 0;
        private string _status = "Initializing";

        public override async Task<int> OnLoopAsync()
        {
            if (State.Player == null) return 600;

            // 1. Health check
            if (Combat.GetHealthPercent() <= EatHpPercent)
            {
                _status = "Eating Food";
                await Combat.EatFoodAsync(FoodName);
                return Antiban.RandomDelay(300, 600);
            }

            // 2. Inventory Full -> Bank
            if (InventoryActions.IsFull)
            {
                _status = "Banking Herbs";
                if (Bank.IsOpen)
                {
                    await Bank.DepositAllExceptAsync(FoodName);
                    await Condition.SleepAsync(300, 600);
                    await Bank.CloseAsync();
                    return Antiban.RandomDelay(600, 1000);
                }

                await WebWalker.WalkToBankAsync(BankDest);
                return Antiban.RandomDelay(1000, 2000);
            }

            // 3. Ground Loot (Herbs, Runes, Seeds)
            if (await Looting.LootGroundItemsAsync(minValueThreshold: 500, lootClues: true, lootUntradeables: true))
            {
                _herbsLooted++;
                _status = "Looting Herbs & Runes";
                return Antiban.RandomDelay(600, 1200);
            }

            // 4. In Combat check
            var druid = Queries.Queries.Npcs.Named("Chaos druid").InteractingWithMe().First();
            if (druid != null && druid.CurrentHp > 0)
            {
                _status = "Fighting Chaos Druid";
                return Antiban.RandomDelay(600, 1200);
            }

            // 5. Attack next druid
            var nextDruid = Queries.Queries.Npcs.Named("Chaos druid").WithinDistance(12).Alive().Nearest();
            if (nextDruid != null)
            {
                _status = "Attacking Chaos Druid";
                await nextDruid.InteractAsync("Attack");
                await Condition.SleepAsync(700, 1300);
                return Antiban.RandomDelay(600, 1200);
            }

            _status = "Searching for Druid";
            return Antiban.RandomDelay(800, 1500);
        }

        public override void OnPaint(DrawingContext dc)
        {
            var font = new Typeface("Segoe UI");
            var titleText = new FormattedText("⚔️ Auto Chaos Druids AIO",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Windows.FlowDirection.LeftToRight,
                font, 14, new SolidColorBrush(Color.FromRgb(248, 113, 113)), 1.0);

            var statusText = new FormattedText($"Status: {_status} | Items Looted: {_herbsLooted}",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Windows.FlowDirection.LeftToRight,
                font, 12, Brushes.White, 1.0);

            dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(200, 25, 20, 20)), new Pen(new SolidColorBrush(Color.FromRgb(248, 113, 113)), 1), new System.Windows.Rect(10, 35, 340, 50));
            dc.DrawText(titleText, new System.Windows.Point(20, 40));
            dc.DrawText(statusText, new System.Windows.Point(20, 60));
        }
    }
}
