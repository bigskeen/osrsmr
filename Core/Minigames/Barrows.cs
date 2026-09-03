using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OsrsMr.Core.Interaction;
using OsrsMr.Core.Queries;
using OsrsMr.Core.Scripting;

namespace OsrsMr.Core.Minigames
{
    public enum BarrowsBrother
    {
        Dharok,
        Ahrim,
        Karil,
        Guthan,
        Torag,
        Verac
    }

    /// <summary>
    /// Automation controller for the Barrows minigame.
    /// Handles mound digging, prayer selection per brother, crypt search, puzzle doors, and chest looting.
    /// </summary>
    public static class Barrows
    {
        private static GameState State => BrainEngine.Instance.State;

        public static readonly HashSet<string> BrotherNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "Dharok the Wretched", "Ahrim the Blighted", "Karil the Tainted",
            "Guthan the Infested", "Torag the Corrupted", "Verac the Defiled"
        };

        /// <summary>
        /// Digs with spade into the mound or searches the sarcophagus.
        /// </summary>
        public static async Task<bool> DigOrSearchSarcophagusAsync()
        {
            var sarcophagus = Queries.Queries.Objects.Named("Sarcophagus").WithinDistance(10).Nearest();
            if (sarcophagus != null)
            {
                await sarcophagus.InteractAsync("Search");
                return true;
            }

            var spade = State.Inventory.Values.FirstOrDefault(i => i.Name.Equals("Spade", StringComparison.OrdinalIgnoreCase));
            if (spade != null)
            {
                await spade.InteractAsync("Dig");
                return true;
            }

            return false;
        }

        /// <summary>
        /// Configures optimal protection prayer for the active Barrows brother.
        /// </summary>
        public static async Task<bool> SetBrotherPrayerAsync(string brotherName)
        {
            if (brotherName.Contains("Ahrim", StringComparison.OrdinalIgnoreCase))
            {
                return await Prayers.SetActiveAsync(Prayer.ProtectFromMagic, true);
            }
            else if (brotherName.Contains("Karil", StringComparison.OrdinalIgnoreCase))
            {
                return await Prayers.SetActiveAsync(Prayer.ProtectFromMissiles, true);
            }
            else // Dharok, Guthan, Torag, Verac
            {
                return await Prayers.SetActiveAsync(Prayer.ProtectFromMelee, true);
            }
        }

        /// <summary>
        /// Solves the puzzle door in the Barrows tunnels.
        /// </summary>
        public static async Task<bool> SolvePuzzleDoorAsync()
        {
            // Barrows puzzle widget group 25
            var puzzleWidget = Queries.Queries.Widgets
                .InGroup(25)
                .VisibleOnly()
                .First();

            if (puzzleWidget == null) return false;

            // Click the first available puzzle answer widget
            var answer = Queries.Queries.Widgets.InGroup(25).WithChildId(2).VisibleOnly().First()
                         ?? Queries.Queries.Widgets.InGroup(25).WithChildId(3).VisibleOnly().First();

            if (answer != null)
            {
                await answer.ClickAsync();
                await Condition.SleepAsync(400, 700);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Opens and loots the central Barrows chest.
        /// </summary>
        public static async Task<bool> LootChestAsync()
        {
            var chest = Queries.Queries.Objects.Named("Chest", "Barrows chest").WithinDistance(10).Nearest();
            if (chest == null) return false;

            await chest.InteractAsync("Open");
            await Condition.SleepAsync(600, 1000);
            await chest.InteractAsync("Search");
            return true;
        }
    }
}
