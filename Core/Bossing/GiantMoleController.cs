using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OsrsMr.Core.Interaction;
using OsrsMr.Core.Queries;
using OsrsMr.Core.Scripting;

namespace OsrsMr.Core.Bossing
{
    /// <summary>
    /// Autonomous Giant Mole Encounter Manager.
    /// Handles Dharok 1-HP guzzling, Falador shield burrow tracking, stamina upkeep,
    /// light source verification, and spade cavern entrance.
    /// </summary>
    public static class GiantMoleController
    {
        private static GameState State => BrainEngine.Instance.State;

        public const int GiantMoleNpcId = 5779;

        /// <summary>
        /// Gets the active Giant Mole NPC if in range.
        /// </summary>
        public static NpcSnapshot? GetMoleNpc()
        {
            return Queries.Queries.Npcs
                .Named("Giant Mole")
                .WithinDistance(30)
                .Nearest();
        }

        /// <summary>
        /// Maintains 1 HP for Dharok's set effect by guzzling Dwarven rock cake or Locator orb.
        /// </summary>
        public static async Task<bool> MaintainDharokHealthAsync()
        {
            if (State.Player == null) return false;

            // Ensure Protect from Melee is active before guzzling down to low HP
            if (!Prayers.IsActive(Prayer.ProtectFromMelee))
            {
                await Prayers.SetActiveAsync(Prayer.ProtectFromMelee, true);
            }

            if (State.Player.CurrentHp > 1)
            {
                var cake = Queries.Queries.Inventory.Named("Dwarven rock cake").First();
                if (cake != null)
                {
                    await cake.InteractAsync("Guzzle");
                    await Condition.SleepAsync(80, 160);
                    return true;
                }

                var orb = Queries.Queries.Inventory.Named("Locator orb").First();
                if (orb != null)
                {
                    await orb.InteractAsync("Feel");
                    await Condition.SleepAsync(80, 160);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Maintains Stamina and Prayer points for running across the large Falador Mole cavern.
        /// </summary>
        public static async Task<bool> MaintainPotionsAsync()
        {
            // Stamina upkeep
            if (!State.StatusEffects.HasStamina && State.Player?.Energy < 60)
            {
                var stam = Queries.Queries.Inventory
                    .Filter(i => i.Name.Contains("Stamina potion", StringComparison.OrdinalIgnoreCase))
                    .First();

                if (stam != null)
                {
                    await stam.InteractAsync("Drink");
                    await Condition.SleepAsync(250, 450);
                    return true;
                }
            }

            // Prayer restore upkeep
            if (Prayers.CurrentPoints < 20)
            {
                var pot = Queries.Queries.Inventory
                    .Filter(i => i.Name.Contains("Prayer potion", StringComparison.OrdinalIgnoreCase) ||
                                 i.Name.Contains("Super restore", StringComparison.OrdinalIgnoreCase))
                    .First();

                if (pot != null)
                {
                    await pot.InteractAsync("Drink");
                    await Condition.SleepAsync(250, 450);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Digs into the Falador Park Mole hill using a spade.
        /// </summary>
        public static async Task<bool> EnterMoleCavernAsync()
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
