using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OsrsMr.Core.Interaction;
using OsrsMr.Core.Queries;
using OsrsMr.Core.Scripting;

namespace OsrsMr.Core.Bossing
{
    public enum ZulrahPhase
    {
        Unknown,
        RangeGreen,   // Attacks with Ranged (weak to Magic)
        MagicBlue,    // Attacks with Magic & Ranged (weak to Ranged)
        MeleeRed,     // Attacks with Melee (weak to Magic)
        JadPhase      // Alternates Ranged & Magic
    }

    /// <summary>
    /// Autonomous Zulrah Encounter Manager.
    /// Manages prayer switching, gear swapping, toxic cloud avoidance, recoil maintenance, and venom curing.
    /// </summary>
    public static class ZulrahController
    {
        private static GameState State => BrainEngine.Instance.State;

        public const int ZulrahGreenId = 2042;
        public const int ZulrahBlueId = 2043;
        public const int ZulrahRedId = 2044;

        public const int VenomCloudGraphicId = 310;
        public const int VenomCloudGraphicAltId = 311;

        /// <summary>
        /// Gets the current Zulrah NPC instance if active in the scene.
        /// </summary>
        public static NpcSnapshot? GetZulrahNpc()
        {
            return Queries.Queries.Npcs
                .Named("Zulrah")
                .WithinDistance(30)
                .Nearest();
        }

        /// <summary>
        /// Detects the active Zulrah combat form based on NPC ID or animation.
        /// </summary>
        public static ZulrahPhase GetCurrentPhase()
        {
            var zulrah = GetZulrahNpc();
            if (zulrah == null) return ZulrahPhase.Unknown;

            return zulrah.Id switch
            {
                ZulrahGreenId => ZulrahPhase.RangeGreen,
                ZulrahBlueId => ZulrahPhase.MagicBlue,
                ZulrahRedId => ZulrahPhase.MeleeRed,
                _ => ZulrahPhase.Unknown
            };
        }

        /// <summary>
        /// Checks whether the player is currently standing on or adjacent to a toxic venom cloud.
        /// </summary>
        public static bool IsPlayerInDangerCloud()
        {
            if (State.Player == null) return false;
            int px = State.Player.WorldX;
            int py = State.Player.WorldY;

            return State.GraphicsObjects.Values.Any(g =>
                (g.Id == VenomCloudGraphicId || g.Id == VenomCloudGraphicAltId) &&
                Math.Abs(g.WorldX - px) <= 1 && Math.Abs(g.WorldY - py) <= 1);
        }

        /// <summary>
        /// Switches overhead and offensive prayers to match Zulrah's current form.
        /// </summary>
        public static async Task<bool> HandlePrayersAsync(ZulrahPhase phase)
        {
            switch (phase)
            {
                case ZulrahPhase.RangeGreen:
                    await Prayers.SetActiveAsync(Prayer.ProtectFromMissiles, true);
                    await CombatPvM.SetOffensivePrayerAsync("magic");
                    break;

                case ZulrahPhase.MagicBlue:
                    await Prayers.SetActiveAsync(Prayer.ProtectFromMagic, true);
                    await CombatPvM.SetOffensivePrayerAsync("ranged");
                    break;

                case ZulrahPhase.MeleeRed:
                    await Prayers.SetActiveAsync(Prayer.ProtectFromMelee, true);
                    await CombatPvM.SetOffensivePrayerAsync("magic");
                    break;

                default:
                    // Fallback to projectile reactive defense
                    await CombatPvM.HandleIncomingProjectilesAsync();
                    break;
            }

            return true;
        }

        /// <summary>
        /// Swaps gear to match Zulrah's current vulnerability.
        /// </summary>
        public static async Task<bool> HandleGearSwapAsync(ZulrahPhase phase, string[] magicGear, string[] rangeGear)
        {
            if (phase == ZulrahPhase.RangeGreen || phase == ZulrahPhase.MeleeRed)
            {
                return await CombatPvM.EquipGearSetAsync(magicGear);
            }
            else if (phase == ZulrahPhase.MagicBlue)
            {
                return await CombatPvM.EquipGearSetAsync(rangeGear);
            }

            return true;
        }

        /// <summary>
        /// Automatically drinks Antivenom / Antipoison potion if venomed or poisoned.
        /// </summary>
        public static async Task<bool> HandleVenomCureAsync()
        {
            if (State.StatusEffects.IsEnvenomed || State.StatusEffects.IsPoisoned)
            {
                var cureItem = Queries.Queries.Inventory
                    .Filter(i => i.Name.Contains("Anti-venom", StringComparison.OrdinalIgnoreCase) ||
                                 i.Name.Contains("Antidote++", StringComparison.OrdinalIgnoreCase) ||
                                 i.Name.Contains("Superantipoison", StringComparison.OrdinalIgnoreCase) ||
                                 i.Name.Contains("Antipoison", StringComparison.OrdinalIgnoreCase))
                    .First();

                if (cureItem != null)
                {
                    await cureItem.InteractAsync("Drink");
                    await Condition.SleepAsync(250, 450);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Automatically equips a new Ring of Recoil if the active one shattered.
        /// </summary>
        public static async Task<bool> HandleRecoilRingAsync()
        {
            if (!Equipment.IsEquipped("Ring of suffering") && !Equipment.IsEquipped("Ring of recoil"))
            {
                var recoil = Queries.Queries.Inventory.Named("Ring of recoil").First();
                if (recoil != null)
                {
                    await recoil.InteractAsync("Wear");
                    await Condition.SleepAsync(100, 250);
                    return true;
                }
            }

            return false;
        }
    }
}
