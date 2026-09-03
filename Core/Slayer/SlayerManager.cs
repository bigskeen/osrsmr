using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OsrsMr.Core.Interaction;
using OsrsMr.Core.Queries;
using OsrsMr.Core.Scripting;

namespace OsrsMr.Core.Slayer
{
    public class SlayerTaskInfo
    {
        public string MonsterName { get; set; } = "";
        public int RemainingCount { get; set; }
        public string SlayerMaster { get; set; } = "";
        public string Location { get; set; } = "";
        public string? FinishingItem { get; set; }
        public List<string> RequiredGear { get; set; } = new();
    }

    /// <summary>
    /// Autonomous Slayer Task & Equipment Manager.
    /// Manages task metadata, required slayer protective gear, and automated finishing blow execution.
    /// </summary>
    public static class SlayerManager
    {
        private static GameState State => BrainEngine.Instance.State;

        /// <summary>
        /// Returns the finishing blow item required to kill the given monster, if applicable.
        /// </summary>
        public static string? GetFinishingItemForMonster(string monsterName)
        {
            if (string.IsNullOrWhiteSpace(monsterName)) return null;

            if (monsterName.Contains("Gargoyle", StringComparison.OrdinalIgnoreCase))
                return "Rock hammer";
            if (monsterName.Contains("Rockslug", StringComparison.OrdinalIgnoreCase))
                return "Bag of salt";
            if (monsterName.Contains("Zygomite", StringComparison.OrdinalIgnoreCase))
                return "Fungicide spray";
            if (monsterName.Contains("Lizard", StringComparison.OrdinalIgnoreCase))
                return "Ice cooler";

            return null;
        }

        /// <summary>
        /// Returns the required protective gear for the specified monster.
        /// </summary>
        public static List<string> GetRequiredGearForMonster(string monsterName)
        {
            var list = new List<string>();
            if (string.IsNullOrWhiteSpace(monsterName)) return list;

            if (monsterName.Contains("Banshee", StringComparison.OrdinalIgnoreCase))
            {
                list.Add("Earmuffs");
                list.Add("Slayer helmet");
            }
            else if (monsterName.Contains("Spectre", StringComparison.OrdinalIgnoreCase))
            {
                list.Add("Nosepeg");
                list.Add("Slayer helmet");
            }
            else if (monsterName.Contains("Cockatrice", StringComparison.OrdinalIgnoreCase) ||
                     monsterName.Contains("Basilisk", StringComparison.OrdinalIgnoreCase))
            {
                list.Add("Mirror shield");
                list.Add("V-shield");
            }
            else if (monsterName.Contains("Killerwatt", StringComparison.OrdinalIgnoreCase))
            {
                list.Add("Insulated boots");
            }
            else if (monsterName.Contains("Wall beast", StringComparison.OrdinalIgnoreCase))
            {
                list.Add("Spiny helmet");
                list.Add("Slayer helmet");
            }
            else if (monsterName.Contains("Fever spider", StringComparison.OrdinalIgnoreCase))
            {
                list.Add("Slayer gloves");
            }

            return list;
        }

        /// <summary>
        /// Checks whether the player is currently wearing the necessary protective gear for the given monster.
        /// </summary>
        public static bool HasRequiredGearEquipped(string monsterName)
        {
            var reqs = GetRequiredGearForMonster(monsterName);
            if (!reqs.Any()) return true;

            // Slayer helmet substitutes all headgear requirements
            if (Equipment.IsEquipped("Slayer helmet") || Equipment.IsEquipped("Black mask"))
            {
                bool onlyNeedsHeadgear = reqs.All(r => r.Contains("helmet", StringComparison.OrdinalIgnoreCase) ||
                                                       r.Contains("earmuffs", StringComparison.OrdinalIgnoreCase) ||
                                                       r.Contains("nosepeg", StringComparison.OrdinalIgnoreCase) ||
                                                       r.Contains("mask", StringComparison.OrdinalIgnoreCase));
                if (onlyNeedsHeadgear) return true;
            }

            // Check if any valid option from the requirement list is equipped
            return reqs.Any(r => Equipment.IsEquipped(r));
        }

        /// <summary>
        /// Automatically performs the finishing blow item on an NPC if low health.
        /// </summary>
        public static async Task<bool> FinishMonsterAsync(NpcSnapshot target)
        {
            if (target == null) return false;

            string? finishingItemName = GetFinishingItemForMonster(target.Name);
            if (string.IsNullOrWhiteSpace(finishingItemName)) return false;

            // Check if monster is at low health (< 15%)
            if (target.CurrentHp > 10 && target.Health != "10%" && target.Health != "0%" && !target.Health.StartsWith("1"))
            {
                return false;
            }

            var item = Queries.Queries.Inventory.Named(finishingItemName).First();
            if (item != null)
            {
                await item.InteractAsync("Use");
                await Condition.SleepAsync(80, 150);
                return await target.InteractAsync("Use");
            }

            return false;
        }
    }
}
