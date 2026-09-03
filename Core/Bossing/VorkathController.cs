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
    /// Autonomous Vorkath Encounter Manager.
    /// Handles fireball dodging, acid pool evasion, Zombified Spawn one-tick Crumble Undead casting,
    /// potion upkeep (Super Antifire, Venom cure), and protection prayer management.
    /// </summary>
    public static class VorkathController
    {
        private static GameState State => BrainEngine.Instance.State;

        public const int VorkathAwakeNpcId = 8061;
        public const int VorkathSleepingNpcId = 8059;
        public const int ZombifiedSpawnNpcId = 8062;

        public const int AcidPoolGraphicId = 1483;
        public const int FireballProjectileId = 391;
        public const int VerticalFireballProjectileId = 393;

        /// <summary>
        /// Gets the active Vorkath boss NPC.
        /// </summary>
        public static NpcSnapshot? GetVorkathNpc()
        {
            return Queries.Queries.Npcs
                .Named("Vorkath")
                .WithinDistance(25)
                .Nearest();
        }

        /// <summary>
        /// Checks if a lethal vertical fireball is incoming directly to the player's current tile.
        /// </summary>
        public static bool IsLethalFireballIncoming()
        {
            return Queries.Queries.Projectiles
                .TargetingPlayer()
                .Filter(p => p.Id == FireballProjectileId || p.Id == VerticalFireballProjectileId)
                .Any();
        }

        /// <summary>
        /// Checks if the player is standing inside or directly next to an acid pool.
        /// </summary>
        public static bool IsPlayerInAcidPool()
        {
            if (State.Player == null) return false;
            int px = State.Player.WorldX;
            int py = State.Player.WorldY;

            return State.GraphicsObjects.Values.Any(g =>
                g.Id == AcidPoolGraphicId &&
                g.WorldX == px && g.WorldY == py);
        }

        /// <summary>
        /// Checks if there is an active acid phase in progress.
        /// </summary>
        public static bool IsAcidPhaseActive()
        {
            return State.GraphicsObjects.Values.Count(g => g.Id == AcidPoolGraphicId) >= 5;
        }

        /// <summary>
        /// Finds a safe adjacent coordinate with no acid pools for walking/dodging.
        /// </summary>
        public static (int x, int y)? FindSafeStepTile()
        {
            if (State.Player == null) return null;
            int px = State.Player.WorldX;
            int py = State.Player.WorldY;

            int[] deltas = { -2, 2, -1, 1, -3, 3 };
            foreach (var dx in deltas)
            {
                int testX = px + dx;
                int testY = py;

                bool hasAcid = State.GraphicsObjects.Values.Any(g => g.Id == AcidPoolGraphicId && g.WorldX == testX && g.WorldY == testY);
                if (!hasAcid)
                {
                    return (testX, testY);
                }
            }

            return (px + 2, py);
        }

        /// <summary>
        /// Automatically targets and casts Crumble Undead on the Zombified Spawn if present.
        /// </summary>
        public static async Task<bool> HandleZombifiedSpawnAsync(string slayerStaffName = "Slayer's staff")
        {
            var spawn = Queries.Queries.Npcs
                .Named("Zombified Spawn")
                .WithinDistance(15)
                .Nearest();

            if (spawn == null) return false;

            // Equip Slayer's staff if available and not equipped
            if (!string.IsNullOrWhiteSpace(slayerStaffName) && !Equipment.IsEquipped(slayerStaffName))
            {
                await Equipment.EquipAsync(slayerStaffName);
                await Condition.SleepAsync(40, 80);
            }

            // Cast Crumble Undead spell on the spawn
            return await Magic.CastOnNpcAsync(Spell.CrumbleUndead, spawn);
        }

        /// <summary>
        /// Maintains required antifire and antivenom protection buffs.
        /// </summary>
        public static async Task<bool> MaintainBuffsAsync()
        {
            if (!State.StatusEffects.HasAntifire)
            {
                var pot = Queries.Queries.Inventory
                    .Filter(i => i.Name.Contains("Super antifire", StringComparison.OrdinalIgnoreCase) ||
                                 i.Name.Contains("Antifire potion", StringComparison.OrdinalIgnoreCase))
                    .First();

                if (pot != null)
                {
                    await pot.InteractAsync("Drink");
                    await Condition.SleepAsync(250, 400);
                    return true;
                }
            }

            if (State.StatusEffects.IsEnvenomed || State.StatusEffects.IsPoisoned || !State.StatusEffects.HasImmunity)
            {
                var pot = Queries.Queries.Inventory
                    .Filter(i => i.Name.Contains("Anti-venom", StringComparison.OrdinalIgnoreCase) ||
                                 i.Name.Contains("Antidote++", StringComparison.OrdinalIgnoreCase))
                    .First();

                if (pot != null)
                {
                    await pot.InteractAsync("Drink");
                    await Condition.SleepAsync(250, 400);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Sets the optimal protection and offensive prayers for Vorkath.
        /// </summary>
        public static async Task<bool> HandlePrayersAsync(bool useProtectFromMagic = true)
        {
            var prot = useProtectFromMagic ? Prayer.ProtectFromMagic : Prayer.ProtectFromMissiles;
            if (!Prayers.IsActive(prot))
            {
                await Prayers.SetActiveAsync(prot, true);
            }

            await CombatPvM.SetOffensivePrayerAsync("ranged");
            return true;
        }
    }
}
