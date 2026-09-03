using System;
using System.Linq;
using System.Threading.Tasks;
using OsrsMr.Core.Interaction;
using OsrsMr.Core.Queries;
using OsrsMr.Core.Scripting;

namespace OsrsMr.Core.Minigames
{
    /// <summary>
    /// Automation controller for Nightmare Zone (NMZ).
    /// Handles drinking Overload, Absorption potions, maintaining 1 HP, and gathering power-up orbs.
    /// </summary>
    public static class NightmareZone
    {
        private static GameState State => BrainEngine.Instance.State;

        public static bool IsInNmz =>
            State.Minigame.Name.Contains("Nightmare Zone", StringComparison.OrdinalIgnoreCase) ||
            Queries.Queries.Objects.Named("Potion decanter", "Co-op chest", "Absorption (4)").WithinDistance(20).Any();

        /// <summary>
        /// Drinks an Overload potion if HP is above 50 and overload effect has expired.
        /// </summary>
        public static async Task<bool> DrinkOverloadAsync()
        {
            if (State.Player.CurrentHp <= 50) return false;

            var overload = State.Inventory.Values
                .FirstOrDefault(i => i.Id > 0 && i.Name.Contains("Overload", StringComparison.OrdinalIgnoreCase));

            if (overload == null) return false;

            await overload.InteractAsync("Drink");
            return true;
        }

        /// <summary>
        /// Drinks an Absorption potion to keep absorption pool healthy.
        /// </summary>
        public static async Task<bool> DrinkAbsorptionAsync()
        {
            var absorption = State.Inventory.Values
                .FirstOrDefault(i => i.Id > 0 && i.Name.Contains("Absorption", StringComparison.OrdinalIgnoreCase));

            if (absorption == null) return false;

            await absorption.InteractAsync("Drink");
            return true;
        }

        /// <summary>
        /// Uses a Rock cake or Locator orb to guzzle down to 1 HP.
        /// </summary>
        public static async Task<bool> GuzzleDownTo1HpAsync()
        {
            if (State.Player.CurrentHp <= 1) return true;

            var rockCake = State.Inventory.Values
                .FirstOrDefault(i => i.Name.Contains("Dwarven rock cake", StringComparison.OrdinalIgnoreCase) ||
                                     i.Name.Contains("Locator orb", StringComparison.OrdinalIgnoreCase));

            if (rockCake != null)
            {
                await rockCake.InteractAsync("Guzzle");
                return true;
            }

            // Rapid Heal prayer flick to prevent HP regeneration past 1
            await Prayers.SetActiveAsync(Prayer.RapidHeal, true);
            await Condition.SleepAsync(80, 150);
            await Prayers.SetActiveAsync(Prayer.RapidHeal, false);
            return true;
        }

        /// <summary>
        /// Collects NMZ power-up orbs (Power Surge, Zapper, Ultimate Force, Recurrent Damage).
        /// </summary>
        public static async Task<bool> CollectPowerUpAsync()
        {
            var powerUp = Queries.Queries.Objects
                .Named("Power surge", "Zapper", "Ultimate force", "Recurrent damage")
                .WithinDistance(10)
                .Nearest();

            if (powerUp != null)
            {
                await powerUp.InteractAsync("Activate");
                return true;
            }

            return false;
        }
    }
}
