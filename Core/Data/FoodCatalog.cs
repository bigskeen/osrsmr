using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OsrsMr.Core.Interaction;
using OsrsMr.Core.Queries;
using OsrsMr.Core.Scripting;

namespace OsrsMr.Core.Data
{
    /// <summary>
    /// Metadata descriptor for a food item in OSRS.
    /// </summary>
    public class FoodItem
    {
        public string Name { get; set; } = "";
        public int HealAmount { get; set; }
        public bool IsComboFood { get; set; }
        public bool IsMultiBite { get; set; }

        public FoodItem(string name, int healAmount, bool isCombo = false, bool isMultiBite = false)
        {
            Name = name;
            HealAmount = healAmount;
            IsComboFood = isCombo;
            IsMultiBite = isMultiBite;
        }
    }

    /// <summary>
    /// Comprehensive OSRS Food catalog containing heal values and eating routines.
    /// </summary>
    public static class FoodCatalog
    {
        private static GameState State => BrainEngine.Instance.State;

        private static readonly Dictionary<string, FoodItem> Foods = new(StringComparer.OrdinalIgnoreCase)
        {
            // --- Cooked Fish ---
            { "Shrimps", new FoodItem("Shrimps", 3) },
            { "Cooked chicken", new FoodItem("Cooked chicken", 3) },
            { "Cooked meat", new FoodItem("Cooked meat", 3) },
            { "Sardine", new FoodItem("Sardine", 4) },
            { "Bread", new FoodItem("Bread", 5) },
            { "Herring", new FoodItem("Herring", 5) },
            { "Trout", new FoodItem("Trout", 7) },
            { "Pike", new FoodItem("Pike", 8) },
            { "Roast beast meat", new FoodItem("Roast beast meat", 8) },
            { "Salmon", new FoodItem("Salmon", 9) },
            { "Tuna", new FoodItem("Tuna", 10) },
            { "Crab meat", new FoodItem("Crab meat", 10) },
            { "Stew", new FoodItem("Stew", 11) },
            { "Lobster", new FoodItem("Lobster", 12) },
            { "Cake", new FoodItem("Cake", 12, isMultiBite: true) },
            { "2/3 cake", new FoodItem("2/3 cake", 8, isMultiBite: true) },
            { "Slice of cake", new FoodItem("Slice of cake", 4) },
            { "Meat pie", new FoodItem("Meat pie", 12, isMultiBite: true) },
            { "Redberry pie", new FoodItem("Redberry pie", 10, isMultiBite: true) },
            { "Bass", new FoodItem("Bass", 13) },
            { "Swordfish", new FoodItem("Swordfish", 14) },
            { "Apple pie", new FoodItem("Apple pie", 14, isMultiBite: true) },
            { "Plain pizza", new FoodItem("Plain pizza", 14, isMultiBite: true) },
            { "Chocolate cake", new FoodItem("Chocolate cake", 15, isMultiBite: true) },
            { "2/3 chocolate cake", new FoodItem("2/3 chocolate cake", 10, isMultiBite: true) },
            { "Chocolate slice", new FoodItem("Chocolate slice", 5) },
            { "Potato with butter", new FoodItem("Potato with butter", 14) },
            { "Potato with cheese", new FoodItem("Potato with cheese", 16) },
            { "Egg potato", new FoodItem("Egg potato", 16) },
            { "Meat pizza", new FoodItem("Meat pizza", 16, isMultiBite: true) },
            { "Monkfish", new FoodItem("Monkfish", 16) },
            { "Anchovy pizza", new FoodItem("Anchovy pizza", 18, isMultiBite: true) },
            { "Cooked karambwan", new FoodItem("Cooked karambwan", 18, isCombo: true) },
            { "Curry", new FoodItem("Curry", 19) },
            { "Shark", new FoodItem("Shark", 20) },
            { "Mushroom potato", new FoodItem("Mushroom potato", 20) },
            { "Sea turtle", new FoodItem("Sea turtle", 21) },
            { "Manta ray", new FoodItem("Manta ray", 22) },
            { "Dark crab", new FoodItem("Dark crab", 22) },
            { "Tuna potato", new FoodItem("Tuna potato", 22) },
            { "Pineapple pizza", new FoodItem("Pineapple pizza", 22, isMultiBite: true) },
            { "Summer pie", new FoodItem("Summer pie", 22, isMultiBite: true) },
            { "Wild pie", new FoodItem("Wild pie", 22, isMultiBite: true) },
            { "Anglerfish", new FoodItem("Anglerfish", 22) },
            { "Saradomin brew(4)", new FoodItem("Saradomin brew(4)", 16, isMultiBite: true) },
            { "Saradomin brew(3)", new FoodItem("Saradomin brew(3)", 16, isMultiBite: true) },
            { "Saradomin brew(2)", new FoodItem("Saradomin brew(2)", 16, isMultiBite: true) },
            { "Saradomin brew(1)", new FoodItem("Saradomin brew(1)", 16, isMultiBite: true) },
            { "Purple sweets", new FoodItem("Purple sweets", 2) },
            { "Warm jug", new FoodItem("Warm jug", 10) },
            { "Bruma root", new FoodItem("Bruma root", 0) }
        };

        /// <summary>
        /// Checks whether the given item name is a recognized food.
        /// </summary>
        public static bool IsFood(string itemName)
        {
            if (string.IsNullOrWhiteSpace(itemName)) return false;
            return Foods.ContainsKey(itemName) || Foods.Keys.Any(k => itemName.Contains(k, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Gets the heal amount for a given food name.
        /// </summary>
        public static int GetHealAmount(string itemName)
        {
            if (string.IsNullOrWhiteSpace(itemName)) return 0;
            if (Foods.TryGetValue(itemName, out var food)) return food.HealAmount;

            var match = Foods.FirstOrDefault(kvp => itemName.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase));
            return match.Value?.HealAmount ?? 0;
        }

        /// <summary>
        /// Returns all food items currently present in the player's inventory.
        /// </summary>
        public static List<ItemSnapshot> GetInventoryFoods()
        {
            return State.Inventory.Values
                .Where(i => i.Id > 0 && IsFood(i.Name))
                .OrderByDescending(i => GetHealAmount(i.Name))
                .ToList();
        }

        /// <summary>
        /// Gets the best (highest healing) food in the inventory.
        /// </summary>
        public static ItemSnapshot? GetBestInventoryFood()
        {
            return GetInventoryFoods().FirstOrDefault();
        }

        /// <summary>
        /// Eats either the specified food or the highest healing food available in inventory.
        /// </summary>
        public static async Task<bool> EatFoodAsync(string? specificFoodName = null)
        {
            ItemSnapshot? target = null;
            if (!string.IsNullOrWhiteSpace(specificFoodName))
            {
                target = State.Inventory.Values.FirstOrDefault(i => i.Name.Contains(specificFoodName, StringComparison.OrdinalIgnoreCase));
            }

            target ??= GetBestInventoryFood();

            if (target == null) return false;

            string action = target.Name.Contains("brew", StringComparison.OrdinalIgnoreCase) ? "Drink" : "Eat";
            await target.InteractAsync(action);
            return true;
        }
    }
}
