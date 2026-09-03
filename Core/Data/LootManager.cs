using System;
using System.Collections.Generic;

namespace OsrsMr.Core.Data
{
    /// <summary>
    /// Item valuation and rarity heuristics database for ground looting and alchemy decisions.
    /// </summary>
    public static class LootManager
    {
        private static readonly Dictionary<string, int> ItemPriceCatalog = new(StringComparer.OrdinalIgnoreCase)
        {
            // Bones
            { "Dragon bones", 2500 },
            { "Superior dragon bones", 11000 },
            { "Lava dragon bones", 3500 },
            { "Wyvern bones", 2200 },
            { "Dagannoth bones", 8500 },
            { "Hydra bones", 6000 },
            { "Babydragon bones", 500 },
            { "Big bones", 300 },

            // Hides
            { "Green dragonhide", 1800 },
            { "Blue dragonhide", 1900 },
            { "Red dragonhide", 2400 },
            { "Black dragonhide", 3200 },

            // High Value Runes
            { "Death rune", 180 },
            { "Blood rune", 210 },
            { "Soul rune", 160 },
            { "Wrath rune", 280 },
            { "Nature rune", 95 },
            { "Law rune", 120 },
            { "Chaos rune", 60 },
            { "Astral rune", 140 },

            // Valuable Herbs
            { "Grimy ranarr weed", 7000 },
            { "Grimy snapdragon", 8000 },
            { "Grimy torstol", 5500 },
            { "Grimy toadflax", 2400 },
            { "Grimy avantoe", 2000 },
            { "Grimy kwuarm", 2200 },
            { "Grimy cadantine", 2300 },
            { "Grimy lantadyme", 2100 },

            // Valuable Seeds
            { "Ranarr seed", 32000 },
            { "Snapdragon seed", 45000 },
            { "Torstol seed", 30000 },
            { "Magic seed", 95000 },
            { "Yew seed", 40000 },
            { "Palm tree seed", 38000 },
            { "Dragonfruit tree seed", 120000 },
            { "Celastrus seed", 85000 },
            { "Redwood tree seed", 42000 },

            // Keys & Special Items
            { "Brimstone key", 80000 },
            { "Larran's key", 160000 },
            { "Crystal key", 18000 },
            { "Tooth half of key", 8000 },
            { "Loop half of key", 10000 },
            { "Uncut dragonstone", 12000 },
            { "Uncut diamond", 2100 },
            { "Uncut ruby", 1100 },

            // High Alch Rune & Dragon Gear
            { "Rune full helm", 21120 },
            { "Rune platebody", 39000 },
            { "Rune platelegs", 38400 },
            { "Rune plateskirt", 38400 },
            { "Rune chainbody", 30000 },
            { "Rune 2h sword", 38400 },
            { "Rune scimitar", 15360 },
            { "Rune battleaxe", 24960 },
            { "Rune warhammer", 24960 },
            { "Rune kiteshield", 32640 },
            { "Rune sq shield", 23040 },
            { "Rune pickaxe", 18800 },
            { "Rune axe", 12800 },
            { "Rune dagger", 4800 },
            { "Dragon dagger", 17520 },
            { "Dragon mace", 30000 },
            { "Dragon battleaxe", 120000 },
            { "Dragon longsword", 60000 },
            { "Dragon boots", 160000 },
            { "Granite maul", 500000 },
            { "Abyssal whip", 1800000 },
            { "Dark bow", 450000 }
        };

        /// <summary>
        /// Gets the estimated unit price for an item.
        /// </summary>
        public static int GetEstimatedUnitPrice(string itemName)
        {
            if (string.IsNullOrWhiteSpace(itemName)) return 0;

            if (ItemPriceCatalog.TryGetValue(itemName.Trim(), out int price))
            {
                return price;
            }

            // Heuristic fallbacks based on item name keywords
            if (itemName.Contains("Rune ", StringComparison.OrdinalIgnoreCase)) return 15000;
            if (itemName.Contains("Dragon ", StringComparison.OrdinalIgnoreCase)) return 30000;
            if (itemName.Contains("seed", StringComparison.OrdinalIgnoreCase)) return 2000;
            if (itemName.Contains("Grimy ", StringComparison.OrdinalIgnoreCase)) return 1500;
            if (itemName.Contains("Uncut ", StringComparison.OrdinalIgnoreCase)) return 1000;
            if (itemName.Contains("key", StringComparison.OrdinalIgnoreCase)) return 10000;
            if (itemName.Contains("rune", StringComparison.OrdinalIgnoreCase)) return 100;
            if (itemName.Contains("Coins", StringComparison.OrdinalIgnoreCase)) return 1;

            return 100;
        }

        /// <summary>
        /// Calculates the total estimated value for an item stack.
        /// </summary>
        public static int GetEstimatedValue(string itemName, int quantity = 1)
        {
            return GetEstimatedUnitPrice(itemName) * Math.Max(1, quantity);
        }

        /// <summary>
        /// Checks whether the item is a rare or untradeable drop that should always be prioritized.
        /// </summary>
        public static bool IsRareOrUntradeable(string itemName)
        {
            if (string.IsNullOrWhiteSpace(itemName)) return false;

            return itemName.Contains("Clue scroll", StringComparison.OrdinalIgnoreCase) ||
                   itemName.Contains("Champion scroll", StringComparison.OrdinalIgnoreCase) ||
                   itemName.Contains("Brimstone key", StringComparison.OrdinalIgnoreCase) ||
                   itemName.Contains("Larran's key", StringComparison.OrdinalIgnoreCase) ||
                   itemName.Contains("Ecumenical key", StringComparison.OrdinalIgnoreCase) ||
                   itemName.Contains("Ancient shard", StringComparison.OrdinalIgnoreCase) ||
                   itemName.Contains("Dark totem", StringComparison.OrdinalIgnoreCase) ||
                   itemName.Contains("Curved bone", StringComparison.OrdinalIgnoreCase) ||
                   itemName.Contains("Long bone", StringComparison.OrdinalIgnoreCase) ||
                   itemName.Contains("Pet ", StringComparison.OrdinalIgnoreCase);
        }
    }
}
