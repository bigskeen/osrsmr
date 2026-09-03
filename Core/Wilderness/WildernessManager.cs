using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OsrsMr.Core.Interaction;
using OsrsMr.Core.Queries;
using OsrsMr.Core.Scripting;

namespace OsrsMr.Core.Wilderness
{
    /// <summary>
    /// Wilderness & Player-Killer (PK) Safety and Avoidance Controller.
    /// Provides wilderness level calculation, threat evaluation, instant escape teleporting,
    /// and defensive prayer switching.
    /// </summary>
    public static class WildernessManager
    {
        private static GameState State => BrainEngine.Instance.State;

        /// <summary>
        /// Calculates the Wilderness level for a given world Y coordinate.
        /// Returns 0 if outside the Wilderness.
        /// </summary>
        public static int GetWildernessLevel(int worldY)
        {
            // Wilderness surface spans from Y: 3524 to Y: 4000+
            if (worldY < 3524) return 0;

            // Deep wilderness dungeons can also be mapped
            if (worldY >= 9900 && worldY <= 10400)
            {
                // Wilderness God Wars Dungeon / Rev Caves approx level 25-35
                return 30;
            }

            int level = (worldY - 3520) / 8 + 1;
            return Math.Clamp(level, 1, 60);
        }

        /// <summary>
        /// Gets the current Wilderness level for the local player.
        /// </summary>
        public static int CurrentWildernessLevel
        {
            get
            {
                if (State.Player == null) return 0;
                return GetWildernessLevel(State.Player.WorldY);
            }
        }

        /// <summary>
        /// Checks whether the local player is currently inside the Wilderness.
        /// </summary>
        public static bool IsInWilderness => CurrentWildernessLevel > 0;

        /// <summary>
        /// Checks whether a given player can attack or be attacked by the local player based on combat levels and wilderness depth.
        /// </summary>
        public static bool CanAttackOrBeAttacked(NearbyPlayerSnapshot target)
        {
            if (State.Player == null || target == null) return false;
            int wildLevel = CurrentWildernessLevel;
            if (wildLevel <= 0) return false;

            int minLevel = Math.Max(3, State.Player.CombatLevel - wildLevel);
            int maxLevel = Math.Min(126, State.Player.CombatLevel + wildLevel);

            return target.CombatLevel >= minLevel && target.CombatLevel <= maxLevel;
        }

        /// <summary>
        /// Scans for potentially dangerous hostile players in render range.
        /// </summary>
        public static List<NearbyPlayerSnapshot> GetThreatPlayers(int maxDistance = 18)
        {
            if (State.Player == null || !IsInWilderness) return new List<NearbyPlayerSnapshot>();

            return Queries.Queries.Players
                .WithinDistance(maxDistance)
                .Filter(p =>
                {
                    // Exclude self
                    if (p.Name.Equals(State.Player.Name, StringComparison.OrdinalIgnoreCase))
                        return false;

                    // Must be in attackable combat bracket
                    return CanAttackOrBeAttacked(p);
                })
                .ToList();
        }

        /// <summary>
        /// Executes an instant emergency escape sequence (teleporting, running south, or logging out).
        /// </summary>
        public static async Task<bool> EmergencyEscapeAsync()
        {
            int wildLvl = CurrentWildernessLevel;

            // 1. Activate Protect Item prayer if available
            await Prayers.SetActiveAsync(Prayer.ProtectItem, true);

            // 2. Try Level 30 Wilderness teleports (Royal Seed Pod, Dragonstone Jewelry, Slayer Ring)
            if (wildLvl <= 30)
            {
                // Check Royal Seed Pod
                var seedPod = Queries.Queries.Inventory.Named("Royal seed pod").First();
                if (seedPod != null)
                {
                    await seedPod.InteractAsync("Commune");
                    return true;
                }

                // Check Amulet of Glory
                var glory = Queries.Queries.Inventory
                    .Filter(i => i.Name.StartsWith("Amulet of glory(", StringComparison.OrdinalIgnoreCase))
                    .First();
                if (glory != null)
                {
                    await glory.InteractAsync("Edgeville");
                    return true;
                }

                // Check Ring of Wealth / Slayer Ring / Grand Seed Pod
                var ringOfWealth = Queries.Queries.Inventory
                    .Filter(i => i.Name.StartsWith("Ring of wealth (", StringComparison.OrdinalIgnoreCase))
                    .First();
                if (ringOfWealth != null)
                {
                    await ringOfWealth.InteractAsync("Grand Exchange");
                    return true;
                }
            }

            // 3. Try Level 20 Wilderness teleports (Standard Teleports, Teletabs)
            if (wildLvl <= 20)
            {
                var tab = Queries.Queries.Inventory
                    .Filter(i => i.Name.EndsWith("teleport", StringComparison.OrdinalIgnoreCase) ||
                                 i.Name.EndsWith("tab", StringComparison.OrdinalIgnoreCase))
                    .First();

                if (tab != null)
                {
                    await tab.InteractAsync("Break");
                    return true;
                }

                // Try casting teleport spell
                await Magic.CastAsync(Spell.VarrockTeleport);
                return true;
            }

            // 4. If above 30 Wild or teleblocked, sprint south towards Wilderness Ditch
            if (State.Player != null)
            {
                await Movement.ToggleRunAsync(true);
                int targetY = Math.Max(3520, State.Player.WorldY - 15);
                await Movement.WalkToAsync(State.Player.WorldX, targetY);
            }

            return false;
        }

        /// <summary>
        /// Adapts defensive overhead prayers based on incoming threat projectiles or default magic defense.
        /// </summary>
        public static async Task<bool> HandleDefensiveOverheadsAsync(NearbyPlayerSnapshot? threat = null)
        {
            // First check incoming projectiles
            if (await CombatPvM.HandleIncomingProjectilesAsync())
            {
                return true;
            }

            // Fallback overhead for wilderness survival: Protect from Magic (prevents Teleblock & Freeze duration)
            if (!Prayers.IsActive(Prayer.ProtectFromMagic))
            {
                return await Prayers.SetActiveAsync(Prayer.ProtectFromMagic, true);
            }

            return false;
        }
    }
}
