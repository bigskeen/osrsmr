using System;
using System.Linq;
using System.Threading.Tasks;
using OsrsMr.Core.Interaction;
using OsrsMr.Core.Queries;
using OsrsMr.Core.Scripting;

namespace OsrsMr.Core.Minigames
{
    /// <summary>
    /// Automation controller for the Blast Furnace minigame.
    /// Handles coal bag filling, conveyor belt loading, bar dispenser collection with ice gloves, and stamina maintenance.
    /// </summary>
    public static class BlastFurnace
    {
        private static GameState State => BrainEngine.Instance.State;

        public static bool IsInBlastFurnace =>
            State.Minigame.Name.Contains("Blast Furnace", StringComparison.OrdinalIgnoreCase) ||
            Queries.Queries.Objects.Named("Conveyor belt", "Bar dispenser", "Blast furnace coffer").WithinDistance(20).Any();

        /// <summary>
        /// Loads ores into the conveyor belt.
        /// </summary>
        public static async Task<bool> LoadConveyorBeltAsync()
        {
            var conveyor = Queries.Queries.Objects.Named("Conveyor belt").WithinDistance(15).Nearest();
            if (conveyor == null) return false;

            await conveyor.InteractAsync("Put-ore-on");
            return true;
        }

        /// <summary>
        /// Empties or fills the Coal bag.
        /// </summary>
        public static async Task<bool> EmptyCoalBagAsync()
        {
            var coalBag = State.Inventory.Values
                .FirstOrDefault(i => i.Name.Contains("Coal bag", StringComparison.OrdinalIgnoreCase));

            if (coalBag == null) return false;

            await coalBag.InteractAsync("Empty");
            return true;
        }

        /// <summary>
        /// Collects molten bars from the bar dispenser using Ice gloves.
        /// </summary>
        public static async Task<bool> CollectBarsAsync()
        {
            // Equip Ice gloves if in inventory
            var iceGloves = State.Inventory.Values
                .FirstOrDefault(i => i.Name.Contains("Ice gloves", StringComparison.OrdinalIgnoreCase));

            if (iceGloves != null)
            {
                await iceGloves.InteractAsync("Wear");
                await Condition.SleepAsync(200, 400);
            }

            var dispenser = Queries.Queries.Objects.Named("Bar dispenser").WithinDistance(15).Nearest();
            if (dispenser == null) return false;

            await dispenser.InteractAsync("Take");
            return true;
        }

        /// <summary>
        /// Pays coins into the Blast furnace coffer.
        /// </summary>
        public static async Task<bool> PayCofferAsync()
        {
            var coffer = Queries.Queries.Objects.Named("Coffer", "Blast furnace coffer").WithinDistance(15).Nearest();
            if (coffer == null) return false;

            await coffer.InteractAsync("Deposit");
            return true;
        }
    }
}
