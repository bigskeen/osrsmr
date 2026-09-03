using System;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using OsrsMr.Core.Clues;
using OsrsMr.Core.Data;
using OsrsMr.Core.Input;
using OsrsMr.Core.Interaction;
using OsrsMr.Core.Queries;
using OsrsMr.Core.Questing;

namespace OsrsMr.Core.Scripting
{
    // =========================================================================
    // 1. Auto Clue Scroll Solver Bot Script
    // =========================================================================
    [ScriptManifest(
        name: "Auto Clue Solver AIO",
        author: "osrsmr",
        version: "1.0.0",
        description: "Autonomous clue solver: reads active clue text, solves coordinate/cryptic/emote steps, digs at locations, and loots reward casket.",
        category: ScriptCategory.Clues)]
    public class AutoClueSolverScript : LoopScript
    {
        private int _casketsOpened = 0;

        [ScriptSetting("Auto Open Caskets", "Automatically open reward caskets when completed", Order = 1)]
        public bool AutoOpenCaskets { get; set; } = true;

        public override void OnStart()
        {
            Log("Auto Clue Solver AIO started. Inspecting active clue scrolls...");
        }

        public override void OnPaint(DrawingContext dc)
        {
            var bgBrush = new SolidColorBrush(Color.FromArgb(220, 20, 20, 20));
            var borderPen = new Pen(new SolidColorBrush(Color.FromArgb(255, 230, 200, 50)), 1.5);
            dc.DrawRoundedRectangle(bgBrush, borderPen, new Rect(15, 200, 230, 80), 6, 6);

            var typeFace = new Typeface("Segoe UI");
            var titleText = new FormattedText("📜 Auto Clue Solver", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeFace, 12, Brushes.Khaki, 1.0);
            var ft1 = new FormattedText($"Task: {CurrentTaskName}", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeFace, 11, Brushes.White, 1.0);
            var ft2 = new FormattedText($"Runtime: {RunningTime:hh\\:mm\\:ss} | Caskets: {_casketsOpened}", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeFace, 10, Brushes.Yellow, 1.0);

            dc.DrawText(titleText, new Point(25, 208));
            dc.DrawText(ft1, new Point(25, 228));
            dc.DrawText(ft2, new Point(25, 248));
        }

        public override async Task<int> OnLoopAsync()
        {
            if (State.Player == null) return 600;

            // 1. Check for reward casket
            var casket = Queries.Queries.Inventory
                .Filter(i => i.Name.Contains("Reward casket", StringComparison.OrdinalIgnoreCase))
                .First();

            if (casket != null)
            {
                if (AutoOpenCaskets)
                {
                    SetTask("Opening Reward Casket!");
                    await casket.InteractAsync("Open");
                    _casketsOpened++;
                    return Antiban.HumanDelay(1000, 1600);
                }
                else
                {
                    SetTask("Reward Casket acquired!");
                    return Antiban.HumanDelay(2000, 3000);
                }
            }

            // 2. Locate active clue scroll
            var clue = ClueScrollSolver.GetActiveClueInInventory();
            if (clue == null)
            {
                SetTask("No active clue scroll in inventory.");
                return Antiban.HumanDelay(1000, 2000);
            }

            // 3. Read clue scroll if interface is not open
            string clueText = ClueScrollSolver.ReadClueScrollWidgetText();
            if (string.IsNullOrWhiteSpace(clueText))
            {
                SetTask("Reading clue scroll...");
                await clue.InteractAsync("Read");
                return Antiban.HumanDelay(800, 1200);
            }

            // 4. Check for spade / digging step
            var spade = Queries.Queries.Inventory.Named("Spade").First();
            if (spade != null)
            {
                SetTask("Digging at target location...");
                await ClueScrollSolver.DigAtCoordinateAsync();
                return Antiban.HumanDelay(1500, 2200);
            }

            SetTask("Clue step active: " + clueText.Substring(0, Math.Min(25, clueText.Length)) + "...");
            return Antiban.HumanDelay(1000, 1500);
        }
    }

    // =========================================================================
    // 2. Auto Cook's Assistant Quest Bot Script
    // =========================================================================
    [ScriptManifest(
        name: "Auto Cook's Assistant",
        author: "osrsmr",
        version: "1.0.0",
        description: "Autonomous quest bot for Cook's Assistant: verifies ingredients, navigates Lumbridge kitchen, and completes dialogue chain.",
        category: ScriptCategory.Quests)]
    public class AutoCooksAssistantScript : LoopScript
    {
        private bool _completed = false;

        public override void OnStart()
        {
            Log("Auto Cook's Assistant initialized. Verifying required ingredients...");
        }

        public override void OnPaint(DrawingContext dc)
        {
            var bgBrush = new SolidColorBrush(Color.FromArgb(220, 20, 20, 20));
            var borderPen = new Pen(new SolidColorBrush(Color.FromArgb(255, 100, 180, 255)), 1.5);
            dc.DrawRoundedRectangle(bgBrush, borderPen, new Rect(15, 200, 230, 80), 6, 6);

            var typeFace = new Typeface("Segoe UI");
            var titleText = new FormattedText("🍳 Cook's Assistant AIO", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeFace, 12, Brushes.LightCyan, 1.0);
            var ft1 = new FormattedText($"Task: {CurrentTaskName}", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeFace, 11, Brushes.White, 1.0);
            var ft2 = new FormattedText($"Status: {(_completed ? "Complete" : "In Progress")} | Time: {RunningTime:hh\\:mm\\:ss}", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeFace, 10, Brushes.SkyBlue, 1.0);

            dc.DrawText(titleText, new Point(25, 208));
            dc.DrawText(ft1, new Point(25, 228));
            dc.DrawText(ft2, new Point(25, 248));
        }

        public override async Task<int> OnLoopAsync()
        {
            if (State.Player == null) return 600;

            var quest = QuestHelperEngine.GetQuest("Cook's Assistant");
            if (quest == null) return 1000;

            // 1. Dialogue handling if open
            if (Dialogs.IsOpen())
            {
                SetTask("Progressing conversation with Cook...");
                await QuestHelperEngine.ProgressDialogAsync("What's the problem?", "I'm always happy to help a cook.", "Yes.");
                return Antiban.HumanDelay(400, 700);
            }

            // 2. Check ingredients
            if (!QuestHelperEngine.HasAllRequiredItems(quest))
            {
                SetTask("Missing ingredients (Flour, Milk, Egg)!");
                Log("Warning: Ensure Pot of flour, Bucket of milk, and Egg are in inventory.");
                return Antiban.HumanDelay(2000, 3000);
            }

            // 3. Find Cook in Lumbridge Kitchen
            var cook = Queries.Queries.Npcs.Named("Cook").WithinDistance(15).Nearest();
            if (cook != null)
            {
                SetTask("Talking to Cook...");
                await cook.InteractAsync("Talk-to");
                return Antiban.HumanDelay(1000, 1600);
            }
            else
            {
                SetTask("Walking to Lumbridge Castle Kitchen...");
                await Movement.WalkToAsync(quest.StartLocation.X, quest.StartLocation.Y);
                return Antiban.HumanDelay(1500, 2500);
            }
        }
    }
}
