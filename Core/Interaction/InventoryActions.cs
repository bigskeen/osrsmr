using System;
using System.Linq;
using System.Threading.Tasks;
using OsrsMr.Core.Data;
using OsrsMr.Core.Scripting;

namespace OsrsMr.Core.Interaction
{
    /// <summary>
    /// High-level inventory manager (drop, use, check counts, eat/drink).
    /// </summary>
    public static class InventoryActions
    {
        private static GameState State => BrainEngine.Instance.State;

        public static int CountItems => State.Inventory.Values.Count(i => i.Id > 0);
        public static int FreeSlots => Math.Max(0, 28 - CountItems);
        public static bool IsFull => CountItems >= 28;
        public static bool IsEmpty => CountItems == 0;

        public static int Count(string itemName)
        {
            return State.Inventory.Values
                .Count(i => i.Id > 0 && i.Name.Contains(itemName, StringComparison.OrdinalIgnoreCase));
        }

        public static bool Contains(string itemName)
        {
            return State.Inventory.Values.Any(i => i.Id > 0 && i.Name.Contains(itemName, StringComparison.OrdinalIgnoreCase));
        }

        public static bool Contains(params string[] itemNames)
        {
            return State.Inventory.Values.Any(i => i.Id > 0 && itemNames.Any(name => i.Name.Contains(name, StringComparison.OrdinalIgnoreCase)));
        }

        public static bool Contains(int itemId)
        {
            return State.Inventory.Values.Any(i => i.Id == itemId);
        }

        public static int GetQuantity(string itemName)
        {
            return State.Inventory.Values
                .Where(i => i.Id > 0 && i.Name.Contains(itemName, StringComparison.OrdinalIgnoreCase))
                .Sum(i => i.Quantity > 0 ? i.Quantity : 1);
        }

        /// <summary>
        /// Clicks / interacts with an item in the inventory by name.
        /// </summary>
        public static async Task<bool> ClickItemAsync(string itemName, string action = "Use")
        {
            var item = State.Inventory.Values
                .FirstOrDefault(i => i.Id > 0 && i.Name.Contains(itemName, StringComparison.OrdinalIgnoreCase));

            if (item == null) return false;
            return await item.InteractAsync(action);
        }

        /// <summary>
        /// Uses one inventory item on another inventory item (e.g. Knife on Log, Feather on Dart tip).
        /// </summary>
        public static async Task<bool> UseItemOnItemAsync(string sourceItemName, string targetItemName)
        {
            var sourceItem = State.Inventory.Values
                .FirstOrDefault(i => i.Id > 0 && i.Name.Contains(sourceItemName, StringComparison.OrdinalIgnoreCase));
            var targetItem = State.Inventory.Values
                .FirstOrDefault(i => i.Id > 0 && i.Name.Contains(targetItemName, StringComparison.OrdinalIgnoreCase));

            if (sourceItem == null || targetItem == null) return false;

            await sourceItem.InteractAsync("Use");
            await Condition.SleepAsync(150, 300);
            return await targetItem.InteractAsync("Use");
        }

        /// <summary>
        /// Uses an inventory item on a game/scene object (e.g. Raw food on Fire/Range).
        /// </summary>
        public static async Task<bool> UseItemOnGameObjectAsync(string itemName, SceneObjectSnapshot targetObject)
        {
            var item = State.Inventory.Values
                .FirstOrDefault(i => i.Id > 0 && i.Name.Contains(itemName, StringComparison.OrdinalIgnoreCase));

            if (item == null || targetObject == null) return false;

            await item.InteractAsync("Use");
            await Condition.SleepAsync(150, 300);
            return await targetObject.InteractAsync("Use");
        }

        /// <summary>
        /// Drops all inventory items that match the given name filter.
        /// </summary>
        public static async Task DropAllAsync(string itemName)
        {
            var matching = State.Inventory.Values
                .Where(i => i.Id > 0 && i.Name.Contains(itemName, StringComparison.OrdinalIgnoreCase))
                .OrderBy(i => i.Slot)
                .ToList();

            foreach (var item in matching)
            {
                await item.InteractAsync("Drop");
                await Condition.SleepAsync(80, 160);
            }
        }

        /// <summary>
        /// Drops all inventory items except those in the whitelist.
        /// </summary>
        public static async Task DropAllExceptAsync(params string[] keepItemNames)
        {
            var itemsToDrop = State.Inventory.Values
                .Where(i => i.Id > 0 && !keepItemNames.Any(k => i.Name.Contains(k, StringComparison.OrdinalIgnoreCase)))
                .OrderBy(i => i.Slot)
                .ToList();

            foreach (var item in itemsToDrop)
            {
                await item.InteractAsync("Drop");
                await Condition.SleepAsync(80, 160);
            }
        }

        /// <summary>
        /// Drops all items except recognized tools (pickaxes, axes, etc.) and food.
        /// </summary>
        public static async Task DropAllExceptToolsAndFoodAsync()
        {
            var itemsToDrop = State.Inventory.Values
                .Where(i => i.Id > 0 && !ToolCatalog.IsSkillingTool(i.Name) && !FoodCatalog.IsFood(i.Name))
                .OrderBy(i => i.Slot)
                .ToList();

            foreach (var item in itemsToDrop)
            {
                await item.InteractAsync("Drop");
                await Condition.SleepAsync(80, 160);
            }
        }

        /// <summary>
        /// Uses an item in the inventory (clicks it, e.g. food, potion, teleport tab).
        /// </summary>
        public static async Task<bool> UseItemAsync(string itemName)
        {
            var item = State.Inventory.Values
                .FirstOrDefault(i => i.Id > 0 && i.Name.Contains(itemName, StringComparison.OrdinalIgnoreCase));

            if (item == null) return false;

            await item.InteractAsync("Use");
            return true;
        }
    }
}
