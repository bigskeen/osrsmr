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
    /// Automation controller for the Wintertodt minigame.
    /// Handles braziers, chopping, fletching, feeding, snowfall dodging, pyromancer healing, and auto-eating.
    /// </summary>
    public static class Wintertodt
    {
        private static GameState State => BrainEngine.Instance.State;

        public const int SnowfallGraphicsId = 502;

        public static bool IsInWintertodt =>
            State.Minigame.Name.Contains("Wintertodt", StringComparison.OrdinalIgnoreCase) ||
            Queries.Queries.Objects.Named("Bruma roots", "Brazier").WithinDistance(25).Any();

        /// <summary>
        /// Checks if the tile currently under the player has falling snow damage incoming.
        /// </summary>
        public static bool IsUnderSnowfall()
        {
            if (State.Player == null) return false;
            return State.GraphicsObjects.Values.Any(g =>
                (g.Id == SnowfallGraphicsId || g.Id == 501 || g.Id == 503) &&
                Math.Abs(g.WorldX - State.Player.WorldX) <= 1 &&
                Math.Abs(g.WorldY - State.Player.WorldY) <= 1);
        }

        /// <summary>
        /// Steps 1-2 tiles away from dangerous snowfall areas.
        /// </summary>
        public static async Task<bool> DodgeSnowfallAsync()
        {
            if (!IsUnderSnowfall() || State.Player == null) return false;

            int targetX = State.Player.WorldX + 2;
            int targetY = State.Player.WorldY;
            await Movement.WalkToAsync(targetX, targetY);
            await Condition.SleepAsync(400, 600);
            return true;
        }

        /// <summary>
        /// Chops Bruma roots.
        /// </summary>
        public static async Task<bool> ChopRootsAsync()
        {
            var roots = Queries.Queries.Objects.Named("Bruma roots").WithinDistance(15).Nearest();
            if (roots == null) return false;

            await roots.InteractAsync("Chop");
            return true;
        }

        /// <summary>
        /// Fletches Bruma roots into kindling using a knife.
        /// </summary>
        public static async Task<bool> FletchKindlingAsync()
        {
            var knife = State.Inventory.Values.FirstOrDefault(i => i.Name.Equals("Knife", StringComparison.OrdinalIgnoreCase));
            var root = State.Inventory.Values.FirstOrDefault(i => i.Name.Equals("Bruma root", StringComparison.OrdinalIgnoreCase));

            if (knife == null || root == null) return false;

            await knife.InteractAsync("Use");
            await Condition.SleepAsync(150, 300);
            await root.InteractAsync("Use");
            return true;
        }

        /// <summary>
        /// Feeds kindling or Bruma roots into the brazier.
        /// </summary>
        public static async Task<bool> FeedBrazierAsync()
        {
            var brazier = Queries.Queries.Objects.Named("Brazier", "Burning brazier").WithinDistance(15).Nearest();
            if (brazier == null) return false;

            await brazier.InteractAsync("Feed");
            return true;
        }

        /// <summary>
        /// Lights an unlit brazier using a tinderbox/torch or fixes a broken brazier using a hammer.
        /// </summary>
        public static async Task<bool> FixOrLightBrazierAsync()
        {
            var brokenBrazier = Queries.Queries.Objects.Named("Broken brazier").WithinDistance(15).Nearest();
            if (brokenBrazier != null)
            {
                await brokenBrazier.InteractAsync("Fix");
                return true;
            }

            var unlitBrazier = Queries.Queries.Objects.Named("Unlit brazier", "Brazier").WithinDistance(15).Nearest();
            if (unlitBrazier != null)
            {
                await unlitBrazier.InteractAsync("Light");
                return true;
            }

            return false;
        }

        /// <summary>
        /// Heals an incapacitated or injured Pyromancer with a rejuvenation potion.
        /// </summary>
        public static async Task<bool> HealPyromancerAsync()
        {
            var potion = State.Inventory.Values.FirstOrDefault(i => i.Name.Contains("Rejuvenation potion", StringComparison.OrdinalIgnoreCase));
            if (potion == null) return false;

            var pyromancer = Queries.Queries.Npcs.Named("Pyromancer", "Incapacitated Pyromancer").WithinDistance(10).Nearest();
            if (pyromancer == null) return false;

            await potion.InteractAsync("Use");
            await Condition.SleepAsync(150, 300);
            await pyromancer.InteractAsync("Use");
            return true;
        }
    }
}
