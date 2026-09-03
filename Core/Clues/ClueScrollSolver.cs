using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OsrsMr.Core.Interaction;
using OsrsMr.Core.Queries;
using OsrsMr.Core.Scripting;
using OsrsMr.Core.Spatial;

namespace OsrsMr.Core.Clues
{
    public enum ClueType
    {
        Unknown,
        Coordinate,
        Anagram,
        Cipher,
        Emote,
        Cryptic,
        HotCold
    }

    public class ClueStep
    {
        public string Text { get; set; } = "";
        public ClueType Type { get; set; } = ClueType.Unknown;
        public WorldPoint? TargetLocation { get; set; }
        public string NpcName { get; set; } = "";
        public string SolutionAnswer { get; set; } = "";
        public string[] RequiredItems { get; set; } = Array.Empty<string>();
        public string EmoteName { get; set; } = "";
    }

    /// <summary>
    /// Autonomous Clue Scroll Step Solver and Analyzer.
    /// Supports coordinate conversions, emote clue requirements, and cryptic NPC steps.
    /// </summary>
    public static class ClueScrollSolver
    {
        private static GameState State => BrainEngine.Instance.State;

        // OSRS Observatory reference point for coordinate clues: (2440, 3161)
        public const int ObservatoryX = 2440;
        public const int ObservatoryY = 3161;

        /// <summary>
        /// Gets the active Clue Scroll item from the player's inventory.
        /// </summary>
        public static ItemSnapshot? GetActiveClueInInventory()
        {
            return Queries.Queries.Inventory
                .Filter(i => i.Name.Contains("Clue scroll", StringComparison.OrdinalIgnoreCase))
                .First();
        }

        /// <summary>
        /// Converts OSRS nautical coordinates (degrees and minutes) into exact WorldPoint coordinates.
        /// Formula: 1 degree = 60 minutes = 18.75 tiles (approx 3.2 minutes per tile)
        /// </summary>
        public static WorldPoint ConvertCoordinatesToWorldPoint(int degNorth, int minNorth, int degEast, int minEast)
        {
            double totalNorthMinutes = (degNorth * 60) + minNorth;
            double totalEastMinutes = (degEast * 60) + minEast;

            int targetX = ObservatoryX + (int)Math.Round(totalEastMinutes / 3.2);
            int targetY = ObservatoryY + (int)Math.Round(totalNorthMinutes / 3.2);

            return new WorldPoint(targetX, targetY, 0);
        }

        /// <summary>
        /// Reads the clue scroll text from the active clue widget interface (Group 287 or 203).
        /// </summary>
        public static string ReadClueScrollWidgetText()
        {
            var clueWidget = Queries.Queries.Widgets
                .InGroup(287)
                .VisibleOnly()
                .Filter(w => !string.IsNullOrWhiteSpace(w.Text))
                .First();

            if (clueWidget != null) return clueWidget.Text;

            var scrollWidget = Queries.Queries.Widgets
                .InGroup(203)
                .VisibleOnly()
                .Filter(w => !string.IsNullOrWhiteSpace(w.Text))
                .First();

            return scrollWidget?.Text ?? "";
        }

        /// <summary>
        /// Checks whether all required items for an emote clue are equipped or in inventory.
        /// </summary>
        public static bool HasRequiredItems(ClueStep step)
        {
            if (step.RequiredItems == null || step.RequiredItems.Length == 0) return true;

            foreach (var item in step.RequiredItems)
            {
                bool inInventory = Queries.Queries.Inventory.Named(item).Any();
                bool isEquipped = Equipment.IsEquipped(item);
                if (!inInventory && !isEquipped) return false;
            }

            return true;
        }

        /// <summary>
        /// Digs at the target coordinate location using a Spade.
        /// </summary>
        public static async Task<bool> DigAtCoordinateAsync()
        {
            var spade = Queries.Queries.Inventory.Named("Spade").First();
            if (spade != null)
            {
                await spade.InteractAsync("Dig");
                await Condition.SleepAsync(800, 1200);
                return true;
            }

            return false;
        }
    }
}
