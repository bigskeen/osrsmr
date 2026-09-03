using System;
using System.Linq;
using System.Threading.Tasks;
using OsrsMr.Core.Interaction;
using OsrsMr.Core.Queries;
using OsrsMr.Core.Scripting;

namespace OsrsMr.Core.Minigames
{
    /// <summary>
    /// Automation controller for Fishing Trawler minigame.
    /// Handles bailing water with buckets, repairing leak holes with swamp paste, fixing torn nets, and inspecting catch.
    /// </summary>
    public static class FishingTrawler
    {
        private static GameState State => BrainEngine.Instance.State;

        public static bool IsInTrawler =>
            State.Minigame.Name.Contains("Fishing Trawler", StringComparison.OrdinalIgnoreCase) ||
            Queries.Queries.Objects.Named("Bailing bucket", "Leak", "Torn net", "Trawler catch").WithinDistance(25).Any();

        /// <summary>
        /// Bails water from the boat hull using a bucket.
        /// </summary>
        public static async Task<bool> BailWaterAsync()
        {
            var bucket = State.Inventory.Values.FirstOrDefault(i => i.Name.Contains("Bailing bucket", StringComparison.OrdinalIgnoreCase) || i.Name.Contains("Bucket", StringComparison.OrdinalIgnoreCase));
            if (bucket == null) return false;

            await bucket.InteractAsync("Bail");
            return true;
        }

        /// <summary>
        /// Repairs a leak in the hull using swamp paste.
        /// </summary>
        public static async Task<bool> RepairLeakAsync()
        {
            var leak = Queries.Queries.Objects.Named("Leak").WithinDistance(8).Nearest();
            if (leak == null) return false;

            await leak.InteractAsync("Plug");
            return true;
        }

        /// <summary>
        /// Fixes the torn fishing net on deck using rope.
        /// </summary>
        public static async Task<bool> FixTornNetAsync()
        {
            var tornNet = Queries.Queries.Objects.Named("Torn net", "Net").WithinDistance(10).Nearest();
            if (tornNet == null) return false;

            await tornNet.InteractAsync("Fix");
            return true;
        }

        /// <summary>
        /// Inspects and loots the trawler catch net after the game concludes.
        /// </summary>
        public static async Task<bool> LootCatchAsync()
        {
            var catchNet = Queries.Queries.Objects.Named("Trawler catch", "Catch net").WithinDistance(15).Nearest();
            if (catchNet == null) return false;

            await catchNet.InteractAsync("Inspect");
            return true;
        }
    }
}
