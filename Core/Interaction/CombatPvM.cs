using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OsrsMr.Core.Input;
using OsrsMr.Core.Queries;
using OsrsMr.Core.Scripting;

namespace OsrsMr.Core.Interaction
{
    /// <summary>
    /// Advanced PvM & Bossing Interaction Controller providing rapid gear-swapping,
    /// dynamic offensive/defensive prayer handling, and projectile-reactive defense.
    /// </summary>
    public static class CombatPvM
    {
        private static GameState State => BrainEngine.Instance.State;

        /// <summary>
        /// Equips a set of items from the inventory sequentially with humanized rapid-clicking.
        /// </summary>
        public static async Task<bool> EquipGearSetAsync(params string[] itemNames)
        {
            if (itemNames == null || itemNames.Length == 0) return true;

            bool allEquipped = true;
            foreach (var name in itemNames)
            {
                if (string.IsNullOrWhiteSpace(name)) continue;
                if (Equipment.IsEquipped(name)) continue;

                var item = Queries.Queries.Inventory.Named(name).First();
                if (item != null)
                {
                    await item.InteractAsync("Wield");
                    await Condition.SleepAsync(40, 90);
                }
                else
                {
                    allEquipped = false;
                }
            }

            return allEquipped;
        }

        /// <summary>
        /// Toggles the optimal offensive skilling or combat prayer for a given combat style.
        /// </summary>
        public static async Task<bool> SetOffensivePrayerAsync(string combatStyle)
        {
            Prayer targetPrayer = combatStyle.ToLowerInvariant() switch
            {
                "magic" or "mage" => Prayers.IsActive(Prayer.Augury) ? Prayer.Augury : Prayer.MysticMight,
                "ranged" or "range" => Prayers.IsActive(Prayer.Rigour) ? Prayer.Rigour : Prayer.EagleEye,
                "melee" => Prayers.IsActive(Prayer.Piety) ? Prayer.Piety : Prayer.UltimateStrength,
                _ => Prayer.Piety
            };

            return await Prayers.SetActiveAsync(targetPrayer, true);
        }

        /// <summary>
        /// Scans active projectiles targeting the player and automatically triggers the correct overhead protection prayer.
        /// Returns true if an overhead prayer was updated.
        /// </summary>
        public static async Task<bool> HandleIncomingProjectilesAsync()
        {
            var projectiles = Queries.Queries.Projectiles.TargetingPlayer().ToList();
            if (!projectiles.Any()) return false;

            // Sort by remaining cycles (closest projectile first)
            var imminent = projectiles.OrderBy(p => p.RemainingCycles).FirstOrDefault();
            if (imminent == null) return false;

            // Common Boss Projectile IDs
            // Zulrah: 1044 (Magic), 1046 (Ranged)
            // Vorkath: 393 (Fireball), 395 (Magic), 396 (Ranged)
            // Jad / Fight Caves: 441 (Ranged), 442 (Magic)
            Prayer? overhead = imminent.Id switch
            {
                1044 or 395 or 442 or 160 => Prayer.ProtectFromMagic,
                1046 or 396 or 441 or 1120 => Prayer.ProtectFromMissiles,
                _ => null
            };

            if (overhead.HasValue && !Prayers.IsActive(overhead.Value))
            {
                return await Prayers.SetActiveAsync(overhead.Value, true);
            }

            return false;
        }

        /// <summary>
        /// Performs an automated special attack with an optional weapon swap and swap-back.
        /// </summary>
        public static async Task<bool> ExecuteSpecialAttackAsync(string specWeapon, string primaryWeapon, int minEnergy = 50)
        {
            if (State.Player == null || State.Player.SpecPercent < minEnergy) return false;

            // 1. Equip spec weapon if needed
            if (!string.IsNullOrWhiteSpace(specWeapon) && !Equipment.IsEquipped(specWeapon))
            {
                await Equipment.EquipAsync(specWeapon);
                await Condition.SleepAsync(80, 150);
            }

            // 2. Toggle special attack
            await Combat.ToggleSpecialAttackAsync();
            await Condition.SleepAsync(120, 200);

            // 3. Return to primary weapon if needed
            if (!string.IsNullOrWhiteSpace(primaryWeapon) && !Equipment.IsEquipped(primaryWeapon))
            {
                await Equipment.EquipAsync(primaryWeapon);
            }

            return true;
        }
    }
}
