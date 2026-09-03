using System;
using System.Linq;
using System.Threading.Tasks;
using OsrsMr.Core.Interaction;
using OsrsMr.Core.Queries;
using OsrsMr.Core.Scripting;
using OsrsMr.Core.Spatial;

namespace OsrsMr.Core.Minigames
{
    /// <summary>
    /// Automation controller for the Guardians of the Rift (GotR) minigame.
    /// Handles mining huge remains, crafting essence, portal entry, altar binding, and barrier charging.
    /// </summary>
    public static class GuardiansOfTheRift
    {
        private static GameState State => BrainEngine.Instance.State;

        public static bool IsInGotR =>
            State.Minigame.Name.Contains("Guardians of the Rift", StringComparison.OrdinalIgnoreCase) ||
            Queries.Queries.Objects.Named("The Great Guardian", "Huge guardian remains", "Essence workbench").WithinDistance(30).Any();

        /// <summary>
        /// Mines huge guardian remains in the main chamber.
        /// </summary>
        public static async Task<bool> MineRemainsAsync()
        {
            var remains = Queries.Queries.Objects.Named("Huge guardian remains", "Guardian parts").WithinDistance(20).Nearest();
            if (remains == null) return false;

            await remains.InteractAsync("Mine");
            return true;
        }

        /// <summary>
        /// Crafts guardian fragments into uncharged essence at the workbench.
        /// </summary>
        public static async Task<bool> CraftEssenceAsync()
        {
            var workbench = Queries.Queries.Objects.Named("Workbench", "Essence workbench").WithinDistance(15).Nearest();
            if (workbench == null) return false;

            await workbench.InteractAsync("Work-at");
            return true;
        }

        /// <summary>
        /// Enters the best active runecrafting portal (Blood, Death, Law, Cosmic, Nature, Fire, Water, Earth, Air).
        /// </summary>
        public static async Task<bool> EnterActivePortalAsync()
        {
            string[] portalPriority = new[]
            {
                "Blood portal", "Death portal", "Law portal", "Nature portal",
                "Cosmic portal", "Chaos portal", "Fire portal", "Earth portal",
                "Water portal", "Air portal", "Mind portal", "Body portal"
            };

            foreach (var portalName in portalPriority)
            {
                var portal = Queries.Queries.Objects.Named(portalName).WithinDistance(20).Nearest();
                if (portal != null)
                {
                    await portal.InteractAsync("Enter");
                    return true;
                }
            }

            // Fallback: Check for mysterious portal / rift portal
            var anyPortal = Queries.Queries.Objects.Filter(o => o.Name.Contains("portal", StringComparison.OrdinalIgnoreCase)).WithinDistance(20).Nearest();
            if (anyPortal != null)
            {
                await anyPortal.InteractAsync("Enter");
                return true;
            }

            return false;
        }

        /// <summary>
        /// Binds essence at the active Runecraft altar.
        /// </summary>
        public static async Task<bool> CraftAtAltarAsync()
        {
            var altar = Queries.Queries.Objects.Filter(o => o.Name.Contains("Altar", StringComparison.OrdinalIgnoreCase)).WithinDistance(15).Nearest();
            if (altar == null) return false;

            await altar.InteractAsync("Craft-rune");
            return true;
        }

        /// <summary>
        /// Powers up The Great Guardian or places charged cells onto damaged cellular barriers.
        /// </summary>
        public static async Task<bool> PowerGuardianOrBarrierAsync()
        {
            // Place cell if held
            var cell = State.Inventory.Values.FirstOrDefault(i => i.Name.Contains("cell", StringComparison.OrdinalIgnoreCase));
            if (cell != null)
            {
                var barrier = Queries.Queries.Objects.Named("Cell tile", "Barrier", "Cell power table").WithinDistance(15).Nearest();
                if (barrier != null)
                {
                    await barrier.InteractAsync("Power");
                    return true;
                }
            }

            // Power Great Guardian
            var guardian = Queries.Queries.Npcs.Named("The Great Guardian").WithinDistance(20).Nearest();
            if (guardian != null)
            {
                await guardian.InteractAsync("Power-up");
                return true;
            }

            return false;
        }
    }
}
