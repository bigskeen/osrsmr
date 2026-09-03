using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace OsrsMr.Core;

public static class ItemDatabase
{
    private static readonly ConcurrentDictionary<int, string> _items = new();

    static ItemDatabase()
    {
        // Common OSRS item database catalog for instant zero-latency resolution
        var common = new Dictionary<int, string>
        {
            // Currency & Core
            { 995, "Coins" },
            { 13204, "Platinum token" },
            { 11849, "Mark of grace" },

            // Runes
            { 554, "Fire rune" },
            { 555, "Water rune" },
            { 556, "Air rune" },
            { 557, "Earth rune" },
            { 558, "Mind rune" },
            { 559, "Body rune" },
            { 560, "Death rune" },
            { 561, "Nature rune" },
            { 562, "Chaos rune" },
            { 563, "Law rune" },
            { 564, "Cosmic rune" },
            { 565, "Blood rune" },
            { 566, "Soul rune" },
            { 9075, "Astral rune" },
            { 21880, "Wrath rune" },
            { 28849, "Sunfire rune" },

            // Combat & Weapons
            { 4151, "Abyssal whip" },
            { 12006, "Abyssal tentacle" },
            { 12926, "Toxic blowpipe" },
            { 25862, "Bow of faerdhinen" },
            { 20997, "Twisted bow" },
            { 22323, "Sanguinesti staff" },
            { 27275, "Tumeken's shadow" },
            { 13576, "Dragon warhammer" },
            { 11802, "Armadyl godsword" },
            { 11804, "Bandos godsword" },
            { 11806, "Saradomin godsword" },
            { 11808, "Zamorak godsword" },
            { 11832, "Bandos chestplate" },
            { 11834, "Bandos tassets" },
            { 11836, "Bandos boots" },
            { 11826, "Armadyl helmet" },
            { 11828, "Armadyl chestplate" },
            { 11830, "Armadyl chainskirt" },
            { 21000, "Ancestral hat" },
            { 21003, "Ancestral robe top" },
            { 21006, "Ancestral robe bottom" },
            { 12954, "Dragon defender" },
            { 22322, "Avernic defender" },
            { 6570, "Fire cape" },
            { 21295, "Infernal cape" },
            { 6585, "Amulet of fury" },
            { 19553, "Amulet of torture" },
            { 19547, "Anguish necklace" },
            { 19544, "Tormented bracelet" },
            { 19550, "Ring of suffering" },
            { 11770, "Seers ring (i)" },
            { 11771, "Archers ring (i)" },
            { 11772, "Warrior ring (i)" },
            { 11773, "Berserker ring (i)" },
            { 25985, "Lightbearer" },

            // Tools & Utilities
            { 1265, "Bronze pickaxe" },
            { 1267, "Iron pickaxe" },
            { 1269, "Steel pickaxe" },
            { 1273, "Mithril pickaxe" },
            { 1271, "Adamant pickaxe" },
            { 1275, "Rune pickaxe" },
            { 11920, "Dragon pickaxe" },
            { 20014, "3rd age pickaxe" },
            { 23677, "Crystal pickaxe" },
            { 1351, "Bronze axe" },
            { 1349, "Iron axe" },
            { 1353, "Steel axe" },
            { 1355, "Mithril axe" },
            { 1357, "Adamant axe" },
            { 1359, "Rune axe" },
            { 6739, "Dragon axe" },
            { 23673, "Crystal axe" },
            { 590, "Tinderbox" },
            { 952, "Spade" },
            { 2347, "Hammer" },
            { 1755, "Chisel" },
            { 1733, "Needle" },
            { 1734, "Thread" },
            { 303, "Small fishing net" },
            { 307, "Fishing rod" },
            { 309, "Fly fishing rod" },
            { 311, "Harpoon" },
            { 301, "Lobster pot" },
            { 313, "Fishing bait" },
            { 314, "Feather" },

            // Food & Potions
            { 315, "Shrimps" },
            { 329, "Salmon" },
            { 333, "Trout" },
            { 379, "Lobster" },
            { 373, "Swordfish" },
            { 7946, "Monkfish" },
            { 385, "Shark" },
            { 397, "Sea turtle" },
            { 391, "Manta ray" },
            { 3144, "Cooked karambwan" },
            { 11936, "Dark crab" },
            { 2297, "Anchovy pizza" },
            { 2434, "Prayer potion(4)" },
            { 139, "Prayer potion(3)" },
            { 141, "Prayer potion(2)" },
            { 143, "Prayer potion(1)" },
            { 3024, "Super restore(4)" },
            { 3026, "Super restore(3)" },
            { 3028, "Super restore(2)" },
            { 3030, "Super restore(1)" },
            { 6685, "Saradomin brew(4)" },
            { 6687, "Saradomin brew(3)" },
            { 6689, "Saradomin brew(2)" },
            { 6691, "Saradomin brew(1)" },
            { 12625, "Stamina potion(4)" },
            { 12627, "Stamina potion(3)" },
            { 12629, "Stamina potion(2)" },
            { 12631, "Stamina potion(1)" },
            { 2436, "Super attack(4)" },
            { 2440, "Super strength(4)" },
            { 2442, "Super defence(4)" },
            { 2444, "Ranging potion(4)" },
            { 3040, "Magic potion(4)" },
            { 11730, "Overload (4)" },

            // Ores & Bars
            { 436, "Copper ore" },
            { 438, "Tin ore" },
            { 440, "Iron ore" },
            { 442, "Silver ore" },
            { 453, "Coal" },
            { 444, "Gold ore" },
            { 447, "Mithril ore" },
            { 449, "Adamantite ore" },
            { 451, "Runite ore" },
            { 2349, "Bronze bar" },
            { 2351, "Iron bar" },
            { 2353, "Steel bar" },
            { 2355, "Silver bar" },
            { 2357, "Gold bar" },
            { 2359, "Mithril bar" },
            { 2361, "Adamantite bar" },
            { 2363, "Runite bar" },

            // Logs
            { 1511, "Logs" },
            { 1521, "Oak logs" },
            { 1519, "Willow logs" },
            { 6333, "Teak logs" },
            { 1517, "Maple logs" },
            { 6332, "Mahogany logs" },
            { 1515, "Yew logs" },
            { 1513, "Magic logs" },
            { 19669, "Redwood logs" },

            // Bones & Ashes
            { 526, "Bones" },
            { 532, "Big bones" },
            { 534, "Babydragon bones" },
            { 536, "Dragon bones" },
            { 22124, "Superior dragon bones" },
            { 11943, "Lava dragon bones" },
            { 6729, "Dagannoth bones" },
            { 22780, "Hydra bones" }
        };

        foreach (var kvp in common)
        {
            _items[kvp.Key] = kvp.Value;
        }
    }

    public static void RegisterItem(int id, string name)
    {
        if (id > 0 && !string.IsNullOrWhiteSpace(name) && 
            !name.StartsWith("Item #", StringComparison.OrdinalIgnoreCase) &&
            !name.Equals("Empty", StringComparison.OrdinalIgnoreCase) &&
            !name.Equals("EMPTY", StringComparison.OrdinalIgnoreCase))
        {
            _items[id] = name;
        }
    }

    public static string ResolveItemName(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return "";
        if (int.TryParse(input, out int id))
        {
            return GetItemName(id);
        }
        if (input.StartsWith("Item #", StringComparison.OrdinalIgnoreCase) && int.TryParse(input.AsSpan(6), out int parsedId))
        {
            var res = GetItemName(parsedId);
            if (!string.IsNullOrEmpty(res) && res != parsedId.ToString())
            {
                return res;
            }
        }
        return input;
    }

    public static string GetItemName(int id)
    {
        if (id <= 0 || id == 65535) return "";
        if (_items.TryGetValue(id, out var name))
        {
            return name;
        }
        return $"Item #{id}";
    }
}
