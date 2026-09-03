using System;
using System.Linq;
using System.Threading.Tasks;
using OsrsMr.Core.Interaction;
using OsrsMr.Core.Queries;
using OsrsMr.Core.Scripting;

namespace OsrsMr.Core.Minigames
{
    /// <summary>
    /// Automation controller for Tithe Farm minigame.
    /// Handles planting, watering 4x4 cycles, refill watering cans, and harvesting.
    /// </summary>
    public static class TitheFarm
    {
        private static GameState State => BrainEngine.Instance.State;

        public static bool IsInTitheFarm =>
            State.Minigame.Name.Contains("Tithe", StringComparison.OrdinalIgnoreCase) ||
            Queries.Queries.Objects.Named("Tithe patch", "Water barrel", "Sack (Tithe Farm)").WithinDistance(20).Any();

        /// <summary>
        /// Plants seeds in empty Tithe patches.
        /// </summary>
        public static async Task<bool> PlantSeedsAsync(string seedName = "Golovanova seed")
        {
            var patch = Queries.Queries.Objects.Named("Tithe patch").Filter(p => p.Status == "Available" || p.Status == "Empty").WithinDistance(6).Nearest();
            if (patch == null) return false;

            var seed = State.Inventory.Values.FirstOrDefault(i => i.Name.Contains(seedName, StringComparison.OrdinalIgnoreCase) || i.Name.Contains("seed", StringComparison.OrdinalIgnoreCase));
            if (seed == null) return false;

            await seed.InteractAsync("Use");
            await Condition.SleepAsync(150, 300);
            await patch.InteractAsync("Use");
            return true;
        }

        /// <summary>
        /// Waters growing tithe plants.
        /// </summary>
        public static async Task<bool> WaterPlantAsync()
        {
            var plant = Queries.Queries.Objects.Filter(o => o.Name.Contains("plant", StringComparison.OrdinalIgnoreCase) && !o.Name.Contains("dead", StringComparison.OrdinalIgnoreCase)).WithinDistance(6).Nearest();
            if (plant == null) return false;

            await plant.InteractAsync("Water");
            return true;
        }

        /// <summary>
        /// Harvests fully grown tithe fruit.
        /// </summary>
        public static async Task<bool> HarvestFruitAsync()
        {
            var fruitPlant = Queries.Queries.Objects.Filter(o => o.Name.Contains("fruit", StringComparison.OrdinalIgnoreCase) || o.Name.Contains("ripe", StringComparison.OrdinalIgnoreCase)).WithinDistance(6).Nearest();
            if (fruitPlant == null) return false;

            await fruitPlant.InteractAsync("Harvest");
            return true;
        }

        /// <summary>
        /// Refills watering cans at the water barrel.
        /// </summary>
        public static async Task<bool> RefillWateringCansAsync()
        {
            var barrel = Queries.Queries.Objects.Named("Water barrel", "Barrel").WithinDistance(15).Nearest();
            if (barrel == null) return false;

            var can = State.Inventory.Values.FirstOrDefault(i => i.Name.Contains("Watering can", StringComparison.OrdinalIgnoreCase));
            if (can == null) return false;

            await can.InteractAsync("Use");
            await Condition.SleepAsync(150, 300);
            await barrel.InteractAsync("Use");
            return true;
        }

        /// <summary>
        /// Deposits harvested fruit into the sack.
        /// </summary>
        public static async Task<bool> DepositSackAsync()
        {
            var sack = Queries.Queries.Objects.Named("Sack (Tithe Farm)", "Sack").WithinDistance(15).Nearest();
            if (sack == null) return false;

            await sack.InteractAsync("Deposit");
            return true;
        }
    }
}
