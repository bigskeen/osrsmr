using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OsrsMr.Core.Interaction;
using OsrsMr.Core.Queries;
using OsrsMr.Core.Scripting;

namespace OsrsMr.Core.Bossing
{
    public enum DkBoss
    {
        None,
        Rex,      // Melee (weak to Magic)
        Prime,    // Magic (weak to Ranged)
        Supreme   // Ranged (weak to Melee)
    }

    /// <summary>
    /// Autonomous Dagannoth Kings (DKS) Encounter Manager.
    /// Manages Tribrid switching, overhead protections (Protect from Magic, Missiles, Melee),
    /// target prioritization, and safe-spotting.
    /// </summary>
    public static class DagannothKingsController
    {
        private static GameState State => BrainEngine.Instance.State;

        public const int DagannothSupremeNpcId = 2265;
        public const int DagannothPrimeNpcId = 2266;
        public const int DagannothRexNpcId = 2267;

        /// <summary>
        /// Gets the nearest active Dagannoth King NPC.
        /// </summary>
        public static NpcSnapshot? GetActiveKing()
        {
            return Queries.Queries.Npcs
                .Named("Dagannoth Supreme", "Dagannoth Prime", "Dagannoth Rex")
                .WithinDistance(25)
                .Nearest();
        }

        /// <summary>
        /// Determines the target Dagannoth King based on aggression or proximity.
        /// </summary>
        public static DkBoss GetCurrentTargetBoss()
        {
            var prime = Queries.Queries.Npcs.Named("Dagannoth Prime").WithinDistance(25).Nearest();
            if (prime != null && (prime.IsInteractingWithMe || prime.Distance < 12)) return DkBoss.Prime;

            var supreme = Queries.Queries.Npcs.Named("Dagannoth Supreme").WithinDistance(25).Nearest();
            if (supreme != null && (supreme.IsInteractingWithMe || supreme.Distance < 12)) return DkBoss.Supreme;

            var rex = Queries.Queries.Npcs.Named("Dagannoth Rex").WithinDistance(25).Nearest();
            if (rex != null) return DkBoss.Rex;

            return DkBoss.None;
        }

        /// <summary>
        /// Switches overhead protection prayer and offensive prayer for the active King.
        /// </summary>
        public static async Task<bool> HandlePrayersAsync(DkBoss boss)
        {
            switch (boss)
            {
                case DkBoss.Prime:
                    await Prayers.SetActiveAsync(Prayer.ProtectFromMagic, true);
                    await CombatPvM.SetOffensivePrayerAsync("ranged");
                    break;

                case DkBoss.Supreme:
                    await Prayers.SetActiveAsync(Prayer.ProtectFromMissiles, true);
                    await CombatPvM.SetOffensivePrayerAsync("melee");
                    break;

                case DkBoss.Rex:
                    await Prayers.SetActiveAsync(Prayer.ProtectFromMelee, true);
                    await CombatPvM.SetOffensivePrayerAsync("magic");
                    break;

                default:
                    // If spinolyps are attacking, default to Protect from Missiles or Magic
                    await Prayers.SetActiveAsync(Prayer.ProtectFromMissiles, true);
                    break;
            }

            return true;
        }

        /// <summary>
        /// Equips the appropriate gear set for the current boss.
        /// </summary>
        public static async Task<bool> HandleGearSwapAsync(DkBoss boss, string[] magicGear, string[] rangeGear, string[] meleeGear)
        {
            return boss switch
            {
                DkBoss.Rex => await CombatPvM.EquipGearSetAsync(magicGear),
                DkBoss.Prime => await CombatPvM.EquipGearSetAsync(rangeGear),
                DkBoss.Supreme => await CombatPvM.EquipGearSetAsync(meleeGear),
                _ => true
            };
        }
    }
}
