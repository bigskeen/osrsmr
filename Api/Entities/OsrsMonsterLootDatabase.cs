using System;
using System.Collections.Generic;
using System.Linq;

namespace OsrsMr.Api.Entities
{
    public class MonsterLootItem
    {
        public string ItemName { get; set; } = "";
        public string Category { get; set; } = "General"; // Bones, Herbs, Runes, Weapons/Armor, Valuables, Resources
        public string Rarity { get; set; } = "Common"; // Always, Common, Uncommon, Rare, Very Rare
        public string Quantity { get; set; } = "1";

        public override string ToString() => $"{ItemName} ({Category}) - {Rarity}";
    }

    public static class OsrsMonsterLootDatabase
    {
        private static readonly Dictionary<string, List<MonsterLootItem>> DropTables = new(StringComparer.OrdinalIgnoreCase);

        static OsrsMonsterLootDatabase()
        {
            RegisterMonster("Goblin", new()
            {
                new() { ItemName = "Bones", Category = "Bones", Rarity = "Always" },
                new() { ItemName = "Coins", Category = "Currency", Rarity = "Common", Quantity = "1-25" },
                new() { ItemName = "Bronze spear", Category = "Weapons/Armor", Rarity = "Common" },
                new() { ItemName = "Bronze sword", Category = "Weapons/Armor", Rarity = "Common" },
                new() { ItemName = "Goblin mail", Category = "Weapons/Armor", Rarity = "Common" },
                new() { ItemName = "Water rune", Category = "Runes", Rarity = "Common", Quantity = "6" },
                new() { ItemName = "Body rune", Category = "Runes", Rarity = "Common", Quantity = "2-7" },
                new() { ItemName = "Earth rune", Category = "Runes", Rarity = "Common", Quantity = "4" },
                new() { ItemName = "Nature rune", Category = "Runes", Rarity = "Uncommon", Quantity = "1" },
                new() { ItemName = "Grimy guam leaf", Category = "Herbs", Rarity = "Uncommon" },
                new() { ItemName = "Grimy marrentill", Category = "Herbs", Rarity = "Uncommon" },
                new() { ItemName = "Grimy tarromin", Category = "Herbs", Rarity = "Uncommon" },
                new() { ItemName = "Grimy harralander", Category = "Herbs", Rarity = "Uncommon" },
                new() { ItemName = "Grimy ranarr weed", Category = "Herbs", Rarity = "Rare" },
                new() { ItemName = "Beer", Category = "Food/Drink", Rarity = "Common" },
                new() { ItemName = "Clue scroll (beginner)", Category = "Valuables", Rarity = "Rare" },
                new() { ItemName = "Clue scroll (easy)", Category = "Valuables", Rarity = "Rare" },
                new() { ItemName = "Chef's hat", Category = "Other", Rarity = "Rare" }
            });

            RegisterMonster("Cow", new()
            {
                new() { ItemName = "Bones", Category = "Bones", Rarity = "Always" },
                new() { ItemName = "Cowhide", Category = "Resources", Rarity = "Always" },
                new() { ItemName = "Raw beef", Category = "Food/Drink", Rarity = "Always" }
            });

            RegisterMonster("Chicken", new()
            {
                new() { ItemName = "Bones", Category = "Bones", Rarity = "Always" },
                new() { ItemName = "Raw chicken", Category = "Food/Drink", Rarity = "Always" },
                new() { ItemName = "Feather", Category = "Resources", Rarity = "Always", Quantity = "5-15" },
                new() { ItemName = "Egg", Category = "Resources", Rarity = "Rare" }
            });

            RegisterMonster("Hill Giant", new()
            {
                new() { ItemName = "Big bones", Category = "Bones", Rarity = "Always" },
                new() { ItemName = "Coins", Category = "Currency", Rarity = "Common", Quantity = "10-200" },
                new() { ItemName = "Limpwurt root", Category = "Herbs/Resources", Rarity = "Common" },
                new() { ItemName = "Iron arrow", Category = "Ammunition", Rarity = "Common", Quantity = "15" },
                new() { ItemName = "Steel arrow", Category = "Ammunition", Rarity = "Uncommon", Quantity = "10" },
                new() { ItemName = "Fire rune", Category = "Runes", Rarity = "Common", Quantity = "15" },
                new() { ItemName = "Water rune", Category = "Runes", Rarity = "Common", Quantity = "6" },
                new() { ItemName = "Law rune", Category = "Runes", Rarity = "Uncommon", Quantity = "2" },
                new() { ItemName = "Nature rune", Category = "Runes", Rarity = "Uncommon", Quantity = "6" },
                new() { ItemName = "Cosmic rune", Category = "Runes", Rarity = "Uncommon", Quantity = "2" },
                new() { ItemName = "Death rune", Category = "Runes", Rarity = "Rare", Quantity = "2" },
                new() { ItemName = "Iron full helm", Category = "Weapons/Armor", Rarity = "Common" },
                new() { ItemName = "Steel longsword", Category = "Weapons/Armor", Rarity = "Uncommon" },
                new() { ItemName = "Mithril arrow", Category = "Ammunition", Rarity = "Uncommon", Quantity = "5" },
                new() { ItemName = "Grimy ranarr weed", Category = "Herbs", Rarity = "Rare" },
                new() { ItemName = "Grimy irit leaf", Category = "Herbs", Rarity = "Rare" },
                new() { ItemName = "Grimy avantoe", Category = "Herbs", Rarity = "Rare" },
                new() { ItemName = "Grimy kwuarm", Category = "Herbs", Rarity = "Rare" },
                new() { ItemName = "Giant key", Category = "Valuables", Rarity = "Rare" },
                new() { ItemName = "Clue scroll (beginner)", Category = "Valuables", Rarity = "Rare" },
                new() { ItemName = "Uncut ruby", Category = "Gems", Rarity = "Rare" },
                new() { ItemName = "Uncut diamond", Category = "Gems", Rarity = "Very Rare" }
            });

            RegisterMonster("Moss Giant", new()
            {
                new() { ItemName = "Big bones", Category = "Bones", Rarity = "Always" },
                new() { ItemName = "Coins", Category = "Currency", Rarity = "Common", Quantity = "20-300" },
                new() { ItemName = "Earth rune", Category = "Runes", Rarity = "Common", Quantity = "27" },
                new() { ItemName = "Air rune", Category = "Runes", Rarity = "Common", Quantity = "18" },
                new() { ItemName = "Law rune", Category = "Runes", Rarity = "Uncommon", Quantity = "3-6" },
                new() { ItemName = "Nature rune", Category = "Runes", Rarity = "Uncommon", Quantity = "6" },
                new() { ItemName = "Cosmic rune", Category = "Runes", Rarity = "Uncommon", Quantity = "3" },
                new() { ItemName = "Death rune", Category = "Runes", Rarity = "Uncommon", Quantity = "3-6" },
                new() { ItemName = "Blood rune", Category = "Runes", Rarity = "Rare", Quantity = "1" },
                new() { ItemName = "Black sq shield", Category = "Weapons/Armor", Rarity = "Common" },
                new() { ItemName = "Steel med helm", Category = "Weapons/Armor", Rarity = "Common" },
                new() { ItemName = "Mithril sword", Category = "Weapons/Armor", Rarity = "Uncommon" },
                new() { ItemName = "Mithril spear", Category = "Weapons/Armor", Rarity = "Uncommon" },
                new() { ItemName = "Adamant sword", Category = "Weapons/Armor", Rarity = "Rare" },
                new() { ItemName = "Grimy ranarr weed", Category = "Herbs", Rarity = "Uncommon" },
                new() { ItemName = "Grimy snapdragon", Category = "Herbs", Rarity = "Rare" },
                new() { ItemName = "Grimy torstol", Category = "Herbs", Rarity = "Rare" },
                new() { ItemName = "Mossy key", Category = "Valuables", Rarity = "Rare" },
                new() { ItemName = "Clue scroll (medium)", Category = "Valuables", Rarity = "Rare" }
            });

            RegisterMonster("Chaos Druid", new()
            {
                new() { ItemName = "Bones", Category = "Bones", Rarity = "Always" },
                new() { ItemName = "Coins", Category = "Currency", Rarity = "Common", Quantity = "10-250" },
                new() { ItemName = "Grimy guam leaf", Category = "Herbs", Rarity = "Common" },
                new() { ItemName = "Grimy marrentill", Category = "Herbs", Rarity = "Common" },
                new() { ItemName = "Grimy tarromin", Category = "Herbs", Rarity = "Common" },
                new() { ItemName = "Grimy harralander", Category = "Herbs", Rarity = "Common" },
                new() { ItemName = "Grimy ranarr weed", Category = "Herbs", Rarity = "Common" },
                new() { ItemName = "Grimy irit leaf", Category = "Herbs", Rarity = "Uncommon" },
                new() { ItemName = "Grimy avantoe", Category = "Herbs", Rarity = "Uncommon" },
                new() { ItemName = "Grimy kwuarm", Category = "Herbs", Rarity = "Uncommon" },
                new() { ItemName = "Grimy cadantine", Category = "Herbs", Rarity = "Uncommon" },
                new() { ItemName = "Grimy lantadyme", Category = "Herbs", Rarity = "Rare" },
                new() { ItemName = "Grimy dwarf weed", Category = "Herbs", Rarity = "Rare" },
                new() { ItemName = "Law rune", Category = "Runes", Rarity = "Common", Quantity = "2" },
                new() { ItemName = "Nature rune", Category = "Runes", Rarity = "Common", Quantity = "3" },
                new() { ItemName = "Air rune", Category = "Runes", Rarity = "Common", Quantity = "9-36" },
                new() { ItemName = "Earth rune", Category = "Runes", Rarity = "Common", Quantity = "9" },
                new() { ItemName = "Mithril bolts", Category = "Ammunition", Rarity = "Uncommon", Quantity = "2-12" },
                new() { ItemName = "Uncut ruby", Category = "Gems", Rarity = "Rare" }
            });

            RegisterMonster("Guard", new()
            {
                new() { ItemName = "Bones", Category = "Bones", Rarity = "Always" },
                new() { ItemName = "Coins", Category = "Currency", Rarity = "Common", Quantity = "1-30" },
                new() { ItemName = "Iron arrow", Category = "Ammunition", Rarity = "Common", Quantity = "1-10" },
                new() { ItemName = "Bronze arrow", Category = "Ammunition", Rarity = "Common", Quantity = "1-12" },
                new() { ItemName = "Air rune", Category = "Runes", Rarity = "Common", Quantity = "6" },
                new() { ItemName = "Earth rune", Category = "Runes", Rarity = "Common", Quantity = "3" },
                new() { ItemName = "Fire rune", Category = "Runes", Rarity = "Common", Quantity = "2" },
                new() { ItemName = "Body rune", Category = "Runes", Rarity = "Common", Quantity = "2-7" },
                new() { ItemName = "Iron bolts", Category = "Ammunition", Rarity = "Common", Quantity = "2-12" },
                new() { ItemName = "Iron dagger", Category = "Weapons/Armor", Rarity = "Common" },
                new() { ItemName = "Grimy guam leaf", Category = "Herbs", Rarity = "Uncommon" },
                new() { ItemName = "Grimy marrentill", Category = "Herbs", Rarity = "Uncommon" },
                new() { ItemName = "Grimy ranarr weed", Category = "Herbs", Rarity = "Rare" },
                new() { ItemName = "Clue scroll (medium)", Category = "Valuables", Rarity = "Rare" }
            });

            RegisterMonster("Sand Crab", new()
            {
                new() { ItemName = "Coins", Category = "Currency", Rarity = "Common", Quantity = "5-100" },
                new() { ItemName = "Seaweed", Category = "Resources", Rarity = "Common", Quantity = "1-2" },
                new() { ItemName = "Edible seaweed", Category = "Food/Drink", Rarity = "Common" },
                new() { ItemName = "Oyster", Category = "Resources", Rarity = "Uncommon" },
                new() { ItemName = "Oyster pearls", Category = "Valuables", Rarity = "Rare" },
                new() { ItemName = "Clue scroll (easy)", Category = "Valuables", Rarity = "Rare" }
            });

            RegisterMonster("Rock Crab", new()
            {
                new() { ItemName = "Coins", Category = "Currency", Rarity = "Common", Quantity = "8-120" },
                new() { ItemName = "Seaweed", Category = "Resources", Rarity = "Common", Quantity = "1-3" },
                new() { ItemName = "Edible seaweed", Category = "Food/Drink", Rarity = "Common" },
                new() { ItemName = "Oyster", Category = "Resources", Rarity = "Uncommon" },
                new() { ItemName = "Iron ore", Category = "Resources", Rarity = "Uncommon" },
                new() { ItemName = "Copper ore", Category = "Resources", Rarity = "Uncommon" },
                new() { ItemName = "Tin ore", Category = "Resources", Rarity = "Uncommon" },
                new() { ItemName = "Hobgoblin metal prop", Category = "Valuables", Rarity = "Rare" },
                new() { ItemName = "Clue scroll (easy)", Category = "Valuables", Rarity = "Rare" }
            });

            RegisterMonster("Blue Dragon", new()
            {
                new() { ItemName = "Dragon bones", Category = "Bones", Rarity = "Always" },
                new() { ItemName = "Blue dragonhide", Category = "Resources", Rarity = "Always" },
                new() { ItemName = "Coins", Category = "Currency", Rarity = "Common", Quantity = "50-1000" },
                new() { ItemName = "Water rune", Category = "Runes", Rarity = "Common", Quantity = "75" },
                new() { ItemName = "Fire rune", Category = "Runes", Rarity = "Common", Quantity = "37" },
                new() { ItemName = "Nature rune", Category = "Runes", Rarity = "Uncommon", Quantity = "15" },
                new() { ItemName = "Law rune", Category = "Runes", Rarity = "Uncommon", Quantity = "3" },
                new() { ItemName = "Adamant dart", Category = "Ammunition", Rarity = "Uncommon", Quantity = "10" },
                new() { ItemName = "Rune dagger", Category = "Weapons/Armor", Rarity = "Rare" },
                new() { ItemName = "Dragon med helm", Category = "Weapons/Armor", Rarity = "Very Rare" },
                new() { ItemName = "Dragon spear", Category = "Weapons/Armor", Rarity = "Very Rare" },
                new() { ItemName = "Shield left half", Category = "Valuables", Rarity = "Very Rare" },
                new() { ItemName = "Grimy ranarr weed", Category = "Herbs", Rarity = "Uncommon" },
                new() { ItemName = "Grimy snapdragon", Category = "Herbs", Rarity = "Rare" },
                new() { ItemName = "Grimy torstol", Category = "Herbs", Rarity = "Rare" },
                new() { ItemName = "Clue scroll (hard)", Category = "Valuables", Rarity = "Rare" }
            });

            RegisterMonster("Green Dragon", new()
            {
                new() { ItemName = "Dragon bones", Category = "Bones", Rarity = "Always" },
                new() { ItemName = "Green dragonhide", Category = "Resources", Rarity = "Always" },
                new() { ItemName = "Coins", Category = "Currency", Rarity = "Common", Quantity = "44-440" },
                new() { ItemName = "Nature rune", Category = "Runes", Rarity = "Common", Quantity = "15" },
                new() { ItemName = "Law rune", Category = "Runes", Rarity = "Common", Quantity = "3" },
                new() { ItemName = "Fire rune", Category = "Runes", Rarity = "Common", Quantity = "37" },
                new() { ItemName = "Water rune", Category = "Runes", Rarity = "Common", Quantity = "75" },
                new() { ItemName = "Mithril ore", Category = "Resources", Rarity = "Uncommon" },
                new() { ItemName = "Adamant full helm", Category = "Weapons/Armor", Rarity = "Uncommon" },
                new() { ItemName = "Rune dagger", Category = "Weapons/Armor", Rarity = "Rare" },
                new() { ItemName = "Dragon med helm", Category = "Weapons/Armor", Rarity = "Very Rare" },
                new() { ItemName = "Clue scroll (hard)", Category = "Valuables", Rarity = "Rare" }
            });

            RegisterMonster("Gargoyle", new()
            {
                new() { ItemName = "Coins", Category = "Currency", Rarity = "Common", Quantity = "400-10000" },
                new() { ItemName = "Granite maul", Category = "Weapons/Armor", Rarity = "Rare" },
                new() { ItemName = "Mystic robe top (dark)", Category = "Weapons/Armor", Rarity = "Rare" },
                new() { ItemName = "Rune 2h sword", Category = "Weapons/Armor", Rarity = "Common" },
                new() { ItemName = "Rune battleaxe", Category = "Weapons/Armor", Rarity = "Common" },
                new() { ItemName = "Rune platelegs", Category = "Weapons/Armor", Rarity = "Uncommon" },
                new() { ItemName = "Rune full helm", Category = "Weapons/Armor", Rarity = "Uncommon" },
                new() { ItemName = "Adamant boots", Category = "Weapons/Armor", Rarity = "Uncommon" },
                new() { ItemName = "Fire rune", Category = "Runes", Rarity = "Common", Quantity = "75-150" },
                new() { ItemName = "Chaos rune", Category = "Runes", Rarity = "Common", Quantity = "30-50" },
                new() { ItemName = "Death rune", Category = "Runes", Rarity = "Common", Quantity = "30-50" },
                new() { ItemName = "Blood rune", Category = "Runes", Rarity = "Uncommon", Quantity = "15-20" },
                new() { ItemName = "Pure essence", Category = "Resources", Rarity = "Common", Quantity = "150" },
                new() { ItemName = "Steel bar", Category = "Resources", Rarity = "Common", Quantity = "15-30" },
                new() { ItemName = "Mithril bar", Category = "Resources", Rarity = "Uncommon", Quantity = "15" },
                new() { ItemName = "Gold ore", Category = "Resources", Rarity = "Uncommon", Quantity = "10-20" },
                new() { ItemName = "Grimy ranarr weed", Category = "Herbs", Rarity = "Uncommon" },
                new() { ItemName = "Grimy snapdragon", Category = "Herbs", Rarity = "Rare" },
                new() { ItemName = "Clue scroll (hard)", Category = "Valuables", Rarity = "Rare" }
            });

            RegisterMonster("Bloodveld", new()
            {
                new() { ItemName = "Bones", Category = "Bones", Rarity = "Always" },
                new() { ItemName = "Coins", Category = "Currency", Rarity = "Common", Quantity = "40-800" },
                new() { ItemName = "Blood rune", Category = "Runes", Rarity = "Common", Quantity = "10-30" },
                new() { ItemName = "Fire rune", Category = "Runes", Rarity = "Common", Quantity = "60" },
                new() { ItemName = "Meat pizza", Category = "Food/Drink", Rarity = "Common", Quantity = "2" },
                new() { ItemName = "Mithril sq shield", Category = "Weapons/Armor", Rarity = "Common" },
                new() { ItemName = "Rune med helm", Category = "Weapons/Armor", Rarity = "Uncommon" },
                new() { ItemName = "Rune full helm", Category = "Weapons/Armor", Rarity = "Rare" },
                new() { ItemName = "Rune battleaxe", Category = "Weapons/Armor", Rarity = "Rare" },
                new() { ItemName = "Rune dagger", Category = "Weapons/Armor", Rarity = "Rare" },
                new() { ItemName = "Grimy ranarr weed", Category = "Herbs", Rarity = "Uncommon" },
                new() { ItemName = "Grimy avantoe", Category = "Herbs", Rarity = "Uncommon" },
                new() { ItemName = "Grimy kwuarm", Category = "Herbs", Rarity = "Uncommon" },
                new() { ItemName = "Grimy cadantine", Category = "Herbs", Rarity = "Uncommon" },
                new() { ItemName = "Clue scroll (hard)", Category = "Valuables", Rarity = "Rare" }
            });

            RegisterMonster("Abyssal Demon", new()
            {
                new() { ItemName = "Abyssal ashes", Category = "Bones", Rarity = "Always" },
                new() { ItemName = "Abyssal whip", Category = "Weapons/Armor", Rarity = "Rare" },
                new() { ItemName = "Abyssal dagger", Category = "Weapons/Armor", Rarity = "Very Rare" },
                new() { ItemName = "Abyssal head", Category = "Valuables", Rarity = "Very Rare" },
                new() { ItemName = "Coins", Category = "Currency", Rarity = "Common", Quantity = "200-3000" },
                new() { ItemName = "Chaos rune", Category = "Runes", Rarity = "Common", Quantity = "25-50" },
                new() { ItemName = "Death rune", Category = "Runes", Rarity = "Common", Quantity = "30" },
                new() { ItemName = "Blood rune", Category = "Runes", Rarity = "Common", Quantity = "15-20" },
                new() { ItemName = "Law rune", Category = "Runes", Rarity = "Common", Quantity = "2-4" },
                new() { ItemName = "Rune chainbody", Category = "Weapons/Armor", Rarity = "Uncommon" },
                new() { ItemName = "Rune med helm", Category = "Weapons/Armor", Rarity = "Uncommon" },
                new() { ItemName = "Pure essence", Category = "Resources", Rarity = "Common", Quantity = "60" },
                new() { ItemName = "Grimy ranarr weed", Category = "Herbs", Rarity = "Uncommon" },
                new() { ItemName = "Grimy snapdragon", Category = "Herbs", Rarity = "Rare" },
                new() { ItemName = "Grimy torstol", Category = "Herbs", Rarity = "Rare" },
                new() { ItemName = "Clue scroll (hard)", Category = "Valuables", Rarity = "Rare" },
                new() { ItemName = "Clue scroll (elite)", Category = "Valuables", Rarity = "Very Rare" }
            });

            RegisterMonster("Vorkath", new()
            {
                new() { ItemName = "Superior dragon bones", Category = "Bones", Rarity = "Always", Quantity = "2" },
                new() { ItemName = "Blue dragonhide", Category = "Resources", Rarity = "Always", Quantity = "2" },
                new() { ItemName = "Vorkath's head", Category = "Valuables", Rarity = "Uncommon" },
                new() { ItemName = "Draconic visage", Category = "Valuables", Rarity = "Very Rare" },
                new() { ItemName = "Skeletal visage", Category = "Valuables", Rarity = "Very Rare" },
                new() { ItemName = "Dragonbone necklace", Category = "Valuables", Rarity = "Rare" },
                new() { ItemName = "Jar of decay", Category = "Valuables", Rarity = "Very Rare" },
                new() { ItemName = "Coins", Category = "Currency", Rarity = "Common", Quantity = "20000-80000" },
                new() { ItemName = "Dragon bolts (unf)", Category = "Ammunition", Rarity = "Common", Quantity = "50-100" },
                new() { ItemName = "Dragon dart tip", Category = "Ammunition", Rarity = "Common", Quantity = "50-100" },
                new() { ItemName = "Rune longsword", Category = "Weapons/Armor", Rarity = "Common", Quantity = "2-3" },
                new() { ItemName = "Rune kiteshield", Category = "Weapons/Armor", Rarity = "Common", Quantity = "2-3" },
                new() { ItemName = "Dragon battleaxe", Category = "Weapons/Armor", Rarity = "Uncommon" },
                new() { ItemName = "Dragon platelegs", Category = "Weapons/Armor", Rarity = "Uncommon" },
                new() { ItemName = "Dragon plateskirt", Category = "Weapons/Armor", Rarity = "Uncommon" },
                new() { ItemName = "Chaos rune", Category = "Runes", Rarity = "Common", Quantity = "650-1000" },
                new() { ItemName = "Death rune", Category = "Runes", Rarity = "Common", Quantity = "300-500" },
                new() { ItemName = "Wrath rune", Category = "Runes", Rarity = "Common", Quantity = "30-60" },
                new() { ItemName = "Diamond", Category = "Gems", Rarity = "Common", Quantity = "10-20" },
                new() { ItemName = "Dragonstone", Category = "Gems", Rarity = "Uncommon", Quantity = "2-3" },
                new() { ItemName = "Manta ray", Category = "Food/Drink", Rarity = "Common", Quantity = "35-50" },
                new() { ItemName = "Grimy ranarr weed", Category = "Herbs", Rarity = "Uncommon", Quantity = "10-25" },
                new() { ItemName = "Grimy snapdragon", Category = "Herbs", Rarity = "Uncommon", Quantity = "10-25" },
                new() { ItemName = "Grimy torstol", Category = "Herbs", Rarity = "Uncommon", Quantity = "10-25" },
                new() { ItemName = "Clue scroll (elite)", Category = "Valuables", Rarity = "Rare" }
            });
        }

        private static void RegisterMonster(string name, List<MonsterLootItem> items)
        {
            DropTables[name] = items;
        }

        public static List<MonsterLootItem> GetLootTable(string monsterName)
        {
            if (string.IsNullOrWhiteSpace(monsterName))
                return GetDefaultLootTable();

            string clean = monsterName.Trim();

            // Try exact or partial match
            foreach (var kvp in DropTables)
            {
                if (kvp.Key.Equals(clean, StringComparison.OrdinalIgnoreCase) ||
                    clean.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase) ||
                    kvp.Key.Contains(clean, StringComparison.OrdinalIgnoreCase))
                {
                    return kvp.Value;
                }
            }

            // Fallback generated table for unknown monsters
            return GetDefaultLootTable(clean);
        }

        private static List<MonsterLootItem> GetDefaultLootTable(string name = "Monster")
        {
            return new List<MonsterLootItem>
            {
                new() { ItemName = "Bones", Category = "Bones", Rarity = "Always" },
                new() { ItemName = "Coins", Category = "Currency", Rarity = "Common", Quantity = "10-100" },
                new() { ItemName = "Grimy guam leaf", Category = "Herbs", Rarity = "Uncommon" },
                new() { ItemName = "Grimy marrentill", Category = "Herbs", Rarity = "Uncommon" },
                new() { ItemName = "Grimy ranarr weed", Category = "Herbs", Rarity = "Rare" },
                new() { ItemName = "Nature rune", Category = "Runes", Rarity = "Uncommon", Quantity = "2-5" },
                new() { ItemName = "Law rune", Category = "Runes", Rarity = "Uncommon", Quantity = "2" },
                new() { ItemName = "Death rune", Category = "Runes", Rarity = "Rare", Quantity = "2-4" },
                new() { ItemName = "Uncut sapphire", Category = "Gems", Rarity = "Rare" },
                new() { ItemName = "Uncut emerald", Category = "Gems", Rarity = "Rare" },
                new() { ItemName = "Uncut ruby", Category = "Gems", Rarity = "Rare" },
                new() { ItemName = "Uncut diamond", Category = "Gems", Rarity = "Very Rare" },
                new() { ItemName = "Clue scroll (easy)", Category = "Valuables", Rarity = "Rare" }
            };
        }
    }
}
