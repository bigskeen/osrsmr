using System;
using System.Linq;
using System.Threading.Tasks;
using OsrsMr.Core.Interaction;
using OsrsMr.Core.Queries;
using OsrsMr.Core.Scripting;

namespace OsrsMr.Core.Minigames
{
    public enum PestControlLander
    {
        Novice,
        Intermediate,
        Veteran
    }

    /// <summary>
    /// Automation controller for the Pest Control minigame.
    /// Handles boat boarding, portal prioritization (Shields down), spinner clearing, Void Knight defense, and gate repair.
    /// </summary>
    public static class PestControl
    {
        private static GameState State => BrainEngine.Instance.State;

        public static bool IsInGame =>
            State.Minigame.Name.Contains("Pest Control", StringComparison.OrdinalIgnoreCase) ||
            Queries.Queries.Npcs.Named("Void Knight").WithinDistance(35).Any();

        /// <summary>
        /// Boards the specified Pest Control lander boat gangplank.
        /// </summary>
        public static async Task<bool> BoardBoatAsync(PestControlLander lander = PestControlLander.Novice)
        {
            string plankName = lander switch
            {
                PestControlLander.Intermediate => "Gangplank (Intermediate)",
                PestControlLander.Veteran => "Gangplank (Veteran)",
                _ => "Gangplank (Novice)"
            };

            var plank = Queries.Queries.Objects.Named(plankName, "Gangplank").WithinDistance(15).Nearest();
            if (plank == null) return false;

            await plank.InteractAsync("Cross");
            return true;
        }

        /// <summary>
        /// Targets any vulnerable portal whose shield is down (Purple, Blue, Yellow, Red).
        /// </summary>
        public static async Task<bool> AttackActivePortalAsync()
        {
            // Attack Spinners first if repairing portal
            var spinner = Queries.Queries.Npcs.Named("Spinner").WithinDistance(15).Nearest();
            if (spinner != null)
            {
                await spinner.InteractAsync("Attack");
                return true;
            }

            // Attack vulnerable portal
            var portal = Queries.Queries.Npcs
                .Filter(n => n.Name.Contains("portal", StringComparison.OrdinalIgnoreCase) && !n.Name.Contains("shield", StringComparison.OrdinalIgnoreCase))
                .WithinDistance(20)
                .Nearest();

            if (portal != null)
            {
                await portal.InteractAsync("Attack");
                return true;
            }

            return false;
        }

        /// <summary>
        /// Attacks monsters attacking the Void Knight in the center platform.
        /// </summary>
        public static async Task<bool> DefendVoidKnightAsync()
        {
            var threateningMonster = Queries.Queries.Npcs
                .Named("Brawler", "Defiler", "Torcher", "Ravager", "Shifter", "Splatter")
                .WithinDistance(15)
                .Nearest();

            if (threateningMonster != null)
            {
                await threateningMonster.InteractAsync("Attack");
                return true;
            }

            return false;
        }

        /// <summary>
        /// Repairs and closes broken wooden barricades or gates.
        /// </summary>
        public static async Task<bool> RepairGatesAsync()
        {
            var brokenGate = Queries.Queries.Objects.Named("Broken gate", "Damaged barricade").WithinDistance(10).Nearest();
            if (brokenGate != null)
            {
                await brokenGate.InteractAsync("Repair");
                return true;
            }

            var openGate = Queries.Queries.Objects.Named("Gate").WithinDistance(8).Nearest();
            if (openGate != null)
            {
                await openGate.InteractAsync("Close");
                return true;
            }

            return false;
        }
    }
}
