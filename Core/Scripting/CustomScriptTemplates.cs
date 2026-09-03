using System;

namespace OsrsMr.Core.Scripting
{
    /// <summary>
    /// Boilerplate templates for custom in-client C# bot script development.
    /// </summary>
    public static class CustomScriptTemplates
    {
        public const string BasicLoopScriptTemplate = @"using System;
using System.Threading.Tasks;
using System.Windows.Media;
using OsrsMr.Core.Input;
using OsrsMr.Core.Interaction;
using OsrsMr.Core.Queries;
using OsrsMr.Core.Scripting;

namespace OsrsMr.Scripts.Custom
{
    [ScriptManifest(
        name: ""My Custom Loop Script"",
        author: ""Developer"",
        version: ""1.0.0"",
        description: ""A custom bot script created in Script Studio."",
        category: ScriptCategory.Other
    )]
    public class MyCustomScript : LoopScript
    {
        [ScriptSetting(""Action Delay (ms)"", ""Base delay between loop iterations"", Order = 1, DefaultValue = 600)]
        public int BaseDelay { get; set; } = 600;

        private int _counter = 0;
        private string _status = ""Running"";

        public override void OnStart()
        {
            _counter = 0;
            _status = ""Started"";
        }

        public override async Task<int> OnLoopAsync()
        {
            _counter++;
            _status = $""Executing step #{_counter}"";

            // Example: Scan nearest NPC
            var nearestNpc = Queries.Npcs.WithinDistance(10).Nearest();
            if (nearestNpc != null)
            {
                // Do something with nearest NPC
            }

            return Antiban.RandomDelay(BaseDelay, BaseDelay + 300);
        }

        public override void OnStop()
        {
            _status = ""Stopped"";
        }

        public override void OnPaint(DrawingContext dc)
        {
            var font = new Typeface(""Segoe UI"");
            var formatted = new FormattedText($""Custom Bot: {_status} | Count: {_counter}"",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Windows.FlowDirection.LeftToRight,
                font, 12, Brushes.Cyan, 1.0);

            dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(180, 0, 0, 0)), null, new System.Windows.Rect(10, 35, 260, 30));
            dc.DrawText(formatted, new System.Windows.Point(15, 42));
        }
    }
}
";

        public const string SkillingScriptTemplate = @"using System;
using System.Threading.Tasks;
using System.Windows.Media;
using OsrsMr.Core.Data;
using OsrsMr.Core.Input;
using OsrsMr.Core.Interaction;
using OsrsMr.Core.Queries;
using OsrsMr.Core.Scripting;
using OsrsMr.Core.Spatial;

namespace OsrsMr.Scripts.Custom
{
    [ScriptManifest(
        name: ""My Custom Skiller"",
        author: ""Developer"",
        version: ""1.0.0"",
        description: ""A custom gathering/skilling bot script."",
        category: ScriptCategory.Mining
    )]
    public class MyCustomSkillerScript : LoopScript
    {
        [ScriptSetting(""Target Object Name"", ""Game object to interact with (e.g. Iron rocks, Willow)"", Order = 1, DefaultValue = ""Iron rocks"")]
        public string ObjectName { get; set; } = ""Iron rocks"";

        [ScriptSetting(""Drop When Full"", ""Drop inventory items when full instead of banking"", Order = 2, DefaultValue = true)]
        public bool DropWhenFull { get; set; } = true;

        private int _itemsGathered = 0;
        private string _status = ""Ready"";

        public override async Task<int> OnLoopAsync()
        {
            // 1. Inventory Full Handling
            if (InventoryActions.IsFull)
            {
                if (DropWhenFull)
                {
                    _status = ""Dropping Items"";
                    await InventoryActions.DropAllExceptAsync(""Pickaxe"", ""Axe"");
                    return Antiban.RandomDelay(400, 800);
                }
                else
                {
                    _status = ""Banking"";
                    if (Bank.IsOpen)
                    {
                        await Bank.DepositAllExceptAsync(""Pickaxe"", ""Axe"");
                        await Bank.CloseAsync();
                        return Antiban.RandomDelay(600, 1000);
                    }
                    await WebWalker.WalkToNearestBankAsync();
                    return Antiban.RandomDelay(1000, 2000);
                }
            }

            // 2. Idle / Gathering Check
            if (State.Player != null && !State.Player.IsIdle)
            {
                _status = ""Gathering..."";
                return Antiban.RandomDelay(600, 1200);
            }

            // 3. Find and Interact with Target Object
            var targetObj = Queries.Objects.Named(ObjectName).WithinDistance(10).Nearest();
            if (targetObj != null)
            {
                _status = $""Interacting with {ObjectName}"";
                if (await targetObj.InteractAsync(""Mine"", ""Chop down"", ""Harvest""))
                {
                    _itemsGathered++;
                    await Condition.WaitForPlayerIdleAsync(3000);
                }
                return Antiban.RandomDelay(600, 1200);
            }

            _status = ""Waiting for spawn"";
            return Antiban.RandomDelay(800, 1500);
        }

        public override void OnPaint(DrawingContext dc)
        {
            var font = new Typeface(""Segoe UI"");
            var formatted = new FormattedText($""Gathered: {_itemsGathered} | Status: {_status}"",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Windows.FlowDirection.LeftToRight,
                font, 12, Brushes.Yellow, 1.0);

            dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(180, 0, 0, 0)), null, new System.Windows.Rect(10, 35, 260, 30));
            dc.DrawText(formatted, new System.Windows.Point(15, 42));
        }
    }
}
";

        public const string CombatScriptTemplate = @"using System;
using System.Threading.Tasks;
using System.Windows.Media;
using OsrsMr.Core.Data;
using OsrsMr.Core.Input;
using OsrsMr.Core.Interaction;
using OsrsMr.Core.Queries;
using OsrsMr.Core.Scripting;

namespace OsrsMr.Scripts.Custom
{
    [ScriptManifest(
        name: ""My Custom Combat Bot"",
        author: ""Developer"",
        version: ""1.0.0"",
        description: ""A custom NPC combat fighter with auto-eating and looting."",
        category: ScriptCategory.Combat
    )]
    public class MyCustomCombatScript : LoopScript
    {
        [ScriptSetting(""Target NPC"", ""NPC name to fight"", Order = 1, DefaultValue = ""Goblin"")]
        public string TargetNpcName { get; set; } = ""Goblin"";

        [ScriptSetting(""Food Name"", ""Food item name"", Order = 2, DefaultValue = ""Trout"")]
        public string FoodName { get; set; } = ""Trout"";

        [ScriptSetting(""Eat at HP %"", ""Health threshold"", Order = 3, DefaultValue = 50)]
        public int EatThreshold { get; set; } = 50;

        private int _kills = 0;
        private string _status = ""Idle"";

        public override async Task<int> OnLoopAsync()
        {
            // 1. Health check
            if (Combat.GetHealthPercent() <= EatThreshold)
            {
                _status = ""Eating"";
                await Combat.EatFoodAsync(FoodName);
                return Antiban.RandomDelay(300, 600);
            }

            // 2. Loot ground items
            if (await Looting.LootGroundItemsAsync(minValueThreshold: 500))
            {
                _status = ""Looting"";
                return Antiban.RandomDelay(600, 1000);
            }

            // 3. Combat check
            var activeEnemy = Queries.Npcs.Named(TargetNpcName).InteractingWithMe().First();
            if (activeEnemy != null && activeEnemy.CurrentHp > 0)
            {
                _status = $""Fighting {TargetNpcName}"";
                return Antiban.RandomDelay(600, 1200);
            }

            // 4. Attack next target
            var nextEnemy = Queries.Npcs.Named(TargetNpcName).WithinDistance(12).Alive().Nearest();
            if (nextEnemy != null)
            {
                _status = $""Attacking {TargetNpcName}"";
                if (await nextEnemy.InteractAsync(""Attack""))
                {
                    _kills++;
                }
                return Antiban.RandomDelay(800, 1400);
            }

            _status = ""Searching"";
            return Antiban.RandomDelay(800, 1500);
        }

        public override void OnPaint(DrawingContext dc)
        {
            var font = new Typeface(""Segoe UI"");
            var formatted = new FormattedText($""Kills: {_kills} | Status: {_status}"",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Windows.FlowDirection.LeftToRight,
                font, 12, Brushes.Crimson, 1.0);

            dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(180, 0, 0, 0)), null, new System.Windows.Rect(10, 35, 260, 30));
            dc.DrawText(formatted, new System.Windows.Point(15, 42));
        }
    }
}
";
    }
}
