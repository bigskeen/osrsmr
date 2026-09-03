using System;
using System.Linq;
using System.Threading.Tasks;
using OsrsMr.Core.Data;
using OsrsMr.Core.Input;
using OsrsMr.Core.Interaction;
using OsrsMr.Core.Queries;
using OsrsMr.Core.Scripting;
using OsrsMr.Core.Spatial;

namespace OsrsMr.Core.Minigames
{
    /// <summary>
    /// Automation controller for the Tempoross minigame.
    /// Handles fishing harpoonfish, cooking, cannon loading, wave tethering, and spirit attacking.
    /// </summary>
    public static class Tempoross
    {
        private static GameState State => BrainEngine.Instance.State;

        public static bool IsInTempoross =>
            State.Minigame.Name.Contains("Tempoross", StringComparison.OrdinalIgnoreCase) ||
            Queries.Queries.Objects.Named("Mast", "Totem pole", "Ammunition crate", "Shrine").WithinDistance(20).Any();

        /// <summary>
        /// Fishes raw harpoonfish from the nearest available spot (prioritizing double fish spots).
        /// </summary>
        public static async Task<bool> FishHarpoonfishAsync()
        {
            var spot = Queries.Queries.Npcs.Named("Fishing spot", "Harpoonfish fishing spot").WithinDistance(15).Nearest();
            if (spot == null) return false;

            await spot.InteractAsync("Harpoon");
            return true;
        }

        /// <summary>
        /// Cooks raw harpoonfish on the shrine/stove.
        /// </summary>
        public static async Task<bool> CookFishAsync()
        {
            var shrine = Queries.Queries.Objects.Named("Shrine", "Range").WithinDistance(15).Nearest();
            if (shrine == null) return false;

            await shrine.InteractAsync("Cook-at");
            return true;
        }

        /// <summary>
        /// Loads cooked (or raw) harpoonfish into the mast ammunition crates.
        /// </summary>
        public static async Task<bool> LoadCannonsAsync()
        {
            var crate = Queries.Queries.Objects.Named("Ammunition crate").WithinDistance(15).Nearest();
            if (crate == null) return false;

            await crate.InteractAsync("Fill");
            return true;
        }

        /// <summary>
        /// Tethers to the mast or totem pole to survive colossal waves.
        /// </summary>
        public static async Task<bool> TetherToMastAsync()
        {
            var tetherObj = Queries.Queries.Objects.Named("Mast", "Totem pole").WithinDistance(10).Nearest();
            if (tetherObj == null) return false;

            await tetherObj.InteractAsync("Tether");
            return true;
        }

        /// <summary>
        /// Attacks the Spirit pool during the vulnerable energy-depleted phase.
        /// </summary>
        public static async Task<bool> AttackSpiritPoolAsync()
        {
            var pool = Queries.Queries.Npcs.Named("Spirit pool", "Tempoross").WithinDistance(15).Nearest();
            if (pool == null) return false;

            await pool.InteractAsync("Harpoon");
            return true;
        }
    }
}
