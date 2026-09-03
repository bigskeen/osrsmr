using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OsrsMr.Core.Data;
using OsrsMr.Core.Queries;
using OsrsMr.Core.Scripting;

namespace OsrsMr.Core.Interaction
{
    /// <summary>
    /// Autonomous Ground Item Valuation & Looting Controller.
    /// Manages ground loot detection, priority sorting, inventory space management, and item pickups.
    /// </summary>
    public static class Looting
    {
        private static GameState State => BrainEngine.Instance.State;

        /// <summary>
        /// Scans and automatically loots valuable ground items in the surrounding area.
        /// </summary>
        public static async Task<bool> LootGroundItemsAsync(int minValueThreshold = 1000, bool lootClues = true, bool lootUntradeables = true, int maxDistance = 15)
        {
            var groundItems = Queries.Queries.GroundItems
                .WithinDistance(maxDistance)
                .Filter(item =>
                {
                    if (lootClues && item.Name.Contains("Clue scroll", StringComparison.OrdinalIgnoreCase)) return true;
                    if (lootUntradeables && LootManager.IsRareOrUntradeable(item.Name)) return true;

                    int estimatedVal = LootManager.GetEstimatedValue(item.Name, item.Quantity);
                    return estimatedVal >= minValueThreshold;
                })
                .ToList();

            if (!groundItems.Any()) return false;

            // Sort by estimated value descending
            var target = groundItems
                .OrderByDescending(i => LootManager.IsRareOrUntradeable(i.Name))
                .ThenByDescending(i => LootManager.GetEstimatedValue(i.Name, i.Quantity))
                .FirstOrDefault();

            if (target == null) return false;

            // Check inventory capacity
            if (InventoryActions.IsFull)
            {
                // If the item is stackable and already in inventory, we can pick it up directly
                bool alreadyHasStack = Queries.Queries.Inventory.Named(target.Name).Any();
                if (!alreadyHasStack)
                {
                    // Eat food to make space if needed
                    var food = Queries.Queries.Inventory.Filter(i => FoodCatalog.IsFood(i.Name)).First();
                    if (food != null)
                    {
                        await food.InteractAsync("Eat");
                        await Condition.SleepAsync(250, 450);
                    }
                    else
                    {
                        // Drop low value junk item (vials, bones, etc.)
                        var junk = Queries.Queries.Inventory
                            .Filter(i => i.Name.Equals("Vial", StringComparison.OrdinalIgnoreCase) ||
                                         i.Name.Equals("Jug", StringComparison.OrdinalIgnoreCase) ||
                                         i.Name.Equals("Bones", StringComparison.OrdinalIgnoreCase) ||
                                         i.Name.Equals("Beer glass", StringComparison.OrdinalIgnoreCase))
                            .First();

                        if (junk != null)
                        {
                            await junk.InteractAsync("Drop");
                            await Condition.SleepAsync(200, 350);
                        }
                        else
                        {
                            return false; // Inventory full of important items
                        }
                    }
                }
            }

            // Pick up the target ground item
            return await target.TakeAsync();
        }
    }
}
