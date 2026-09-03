using System;
using System.Collections.Generic;
using System.Linq;
using OsrsMr.Core.Queries;

namespace OsrsMr.Core.Data
{
    /// <summary>
    /// Metadata descriptor for a tool or weapon in OSRS.
    /// </summary>
    public class ToolItem
    {
        public string Name { get; set; } = "";
        public int RequiredSkillLevel { get; set; }
        public int RequiredAttackLevel { get; set; }
        public int TierScore { get; set; } // Higher = better tier (Bronze=1, Dragon=60, Crystal=70)

        public ToolItem(string name, int skillReq, int attackReq, int tier)
        {
            Name = name;
            RequiredSkillLevel = skillReq;
            RequiredAttackLevel = attackReq;
            TierScore = tier;
        }
    }

    /// <summary>
    /// Comprehensive OSRS Tool catalog for pickaxes, woodcutting axes, and skilling equipment.
    /// </summary>
    public static class ToolCatalog
    {
        private static GameState State => BrainEngine.Instance.State;

        // --- All Pickaxes in OSRS ---
        private static readonly Dictionary<string, ToolItem> Pickaxes = new(StringComparer.OrdinalIgnoreCase)
        {
            { "Bronze pickaxe", new ToolItem("Bronze pickaxe", 1, 1, 1) },
            { "Iron pickaxe", new ToolItem("Iron pickaxe", 1, 1, 5) },
            { "Steel pickaxe", new ToolItem("Steel pickaxe", 6, 5, 10) },
            { "Black pickaxe", new ToolItem("Black pickaxe", 11, 10, 15) },
            { "Mithril pickaxe", new ToolItem("Mithril pickaxe", 21, 20, 20) },
            { "Adamant pickaxe", new ToolItem("Adamant pickaxe", 31, 30, 30) },
            { "Rune pickaxe", new ToolItem("Rune pickaxe", 41, 40, 40) },
            { "Gilded pickaxe", new ToolItem("Gilded pickaxe", 41, 40, 41) },
            { "Dragon pickaxe", new ToolItem("Dragon pickaxe", 61, 60, 60) },
            { "Dragon pickaxe (or)", new ToolItem("Dragon pickaxe (or)", 61, 60, 60) },
            { "Dragon pickaxe (upgraded)", new ToolItem("Dragon pickaxe (upgraded)", 61, 60, 60) },
            { "3rd age pickaxe", new ToolItem("3rd age pickaxe", 61, 65, 65) },
            { "Infernal pickaxe", new ToolItem("Infernal pickaxe", 61, 60, 62) },
            { "Crystal pickaxe", new ToolItem("Crystal pickaxe", 71, 70, 70) },
            { "Corrupted pickaxe", new ToolItem("Corrupted pickaxe", 71, 70, 70) },
            { "Trailblazer pickaxe", new ToolItem("Trailblazer pickaxe", 61, 60, 60) }
        };

        // --- All Woodcutting Axes in OSRS ---
        private static readonly Dictionary<string, ToolItem> Axes = new(StringComparer.OrdinalIgnoreCase)
        {
            { "Bronze axe", new ToolItem("Bronze axe", 1, 1, 1) },
            { "Iron axe", new ToolItem("Iron axe", 1, 1, 5) },
            { "Steel axe", new ToolItem("Steel axe", 6, 5, 10) },
            { "Black axe", new ToolItem("Black axe", 11, 10, 15) },
            { "Mithril axe", new ToolItem("Mithril axe", 21, 20, 20) },
            { "Adamant axe", new ToolItem("Adamant axe", 31, 30, 30) },
            { "Rune axe", new ToolItem("Rune axe", 41, 40, 40) },
            { "Gilded axe", new ToolItem("Gilded axe", 41, 40, 41) },
            { "Dragon axe", new ToolItem("Dragon axe", 61, 60, 60) },
            { "Dragon axe (or)", new ToolItem("Dragon axe (or)", 61, 60, 60) },
            { "3rd age axe", new ToolItem("3rd age axe", 61, 65, 65) },
            { "Infernal axe", new ToolItem("Infernal axe", 61, 60, 62) },
            { "Crystal axe", new ToolItem("Crystal axe", 71, 70, 70) },
            { "Corrupted axe", new ToolItem("Corrupted axe", 71, 70, 70) },
            { "Trailblazer axe", new ToolItem("Trailblazer axe", 61, 60, 60) },
            { "Blessed axe", new ToolItem("Blessed axe", 1, 1, 5) }
        };

        // --- All Skilling & Minigame Tools ---
        private static readonly HashSet<string> SkillingTools = new(StringComparer.OrdinalIgnoreCase)
        {
            "Small fishing net", "Big fishing net", "Fishing rod", "Fly fishing rod", "Harpoon",
            "Barbarian rod", "Lobster pot", "Oily fishing rod", "Pearl fishing rod", "Pearl fly fishing rod",
            "Pearl barbarian rod", "Dragon harpoon", "Infernal harpoon", "Crystal harpoon",
            "Tinderbox", "Knife", "Chisel", "Hammer", "Spade", "Rake", "Seed dibber", "Watering can",
            "Secateurs", "Magic secateurs", "Saw", "Crystal saw", "Amy's saw", "Glassblowing pipe",
            "Needle", "Pestle and mortar", "Rope", "Bucket", "Empty bucket", "Bucket of water",
            "Ice gloves", "Goldsmith gauntlets", "Bruma torch", "Warm clothing", "Lockpick"
        };

        /// <summary>
        /// Checks whether the item name matches any known pickaxe.
        /// </summary>
        public static bool IsPickaxe(string itemName)
        {
            if (string.IsNullOrWhiteSpace(itemName)) return false;
            return Pickaxes.ContainsKey(itemName) || (itemName.Contains("pickaxe", StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Checks whether the item name matches any known woodcutting axe.
        /// </summary>
        public static bool IsAxe(string itemName)
        {
            if (string.IsNullOrWhiteSpace(itemName)) return false;
            // Differentiate battleaxes from woodcutting axes
            if (itemName.Contains("battleaxe", StringComparison.OrdinalIgnoreCase) || itemName.Contains("greataxe", StringComparison.OrdinalIgnoreCase))
                return false;

            return Axes.ContainsKey(itemName) || (itemName.EndsWith("axe", StringComparison.OrdinalIgnoreCase) && !itemName.Contains("pickaxe", StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Checks whether the item name matches general skilling equipment or minigame tools.
        /// </summary>
        public static bool IsSkillingTool(string itemName)
        {
            if (string.IsNullOrWhiteSpace(itemName)) return false;
            return IsPickaxe(itemName) || IsAxe(itemName) || SkillingTools.Any(t => itemName.Contains(t, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Returns whether the player has a pickaxe equipped or in their inventory.
        /// </summary>
        public static bool HasPickaxe()
        {
            return State.Equipment.Values.Any(e => IsPickaxe(e.Name)) || State.Inventory.Values.Any(i => IsPickaxe(i.Name));
        }

        /// <summary>
        /// Returns whether the player has a woodcutting axe equipped or in their inventory.
        /// </summary>
        public static bool HasAxe()
        {
            return State.Equipment.Values.Any(e => IsAxe(e.Name)) || State.Inventory.Values.Any(i => IsAxe(i.Name));
        }

        /// <summary>
        /// Gets the best equipped or inventory pickaxe.
        /// </summary>
        public static ItemSnapshot? GetBestPickaxe()
        {
            var equipped = State.Equipment.Values.FirstOrDefault(e => IsPickaxe(e.Name));
            if (equipped != null) return equipped;

            return State.Inventory.Values
                .Where(i => IsPickaxe(i.Name))
                .OrderByDescending(i => Pickaxes.TryGetValue(i.Name, out var p) ? p.TierScore : 0)
                .FirstOrDefault();
        }

        /// <summary>
        /// Gets the best equipped or inventory axe.
        /// </summary>
        public static ItemSnapshot? GetBestAxe()
        {
            var equipped = State.Equipment.Values.FirstOrDefault(e => IsAxe(e.Name));
            if (equipped != null) return equipped;

            return State.Inventory.Values
                .Where(i => IsAxe(i.Name))
                .OrderByDescending(i => Axes.TryGetValue(i.Name, out var a) ? a.TierScore : 0)
                .FirstOrDefault();
        }
    }
}
