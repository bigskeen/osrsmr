using System;
using System.Linq;
using System.Threading.Tasks;
using OsrsMr.Core.Queries;
using OsrsMr.Core.Scripting;

namespace OsrsMr.Core.Interaction
{
    /// <summary>
    /// Interaction controller for checking and managing equipped items.
    /// </summary>
    public static class Equipment
    {
        private static GameState State => BrainEngine.Instance.State;

        /// <summary>
        /// Checks whether an item is currently equipped by name.
        /// </summary>
        public static bool IsEquipped(string itemName)
        {
            return State.Equipment.Values.Any(i => string.Equals(i.Name, itemName, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Checks whether an item with the given ID is equipped.
        /// </summary>
        public static bool IsEquipped(int itemId)
        {
            return State.Equipment.Values.Any(i => i.Id == itemId);
        }

        /// <summary>
        /// Gets the equipped item in the specified equipment slot.
        /// </summary>
        public static ItemSnapshot? GetItemInSlot(EquipmentSlot slot)
        {
            int slotIdx = (int)slot;
            return State.Equipment.Values.FirstOrDefault(i => i.Slot == slotIdx);
        }

        /// <summary>
        /// Equips an item from the player's inventory by name.
        /// </summary>
        public static async Task<bool> EquipAsync(string itemName)
        {
            if (IsEquipped(itemName)) return true;

            var item = Queries.Queries.Inventory.Named(itemName).First();
            if (item == null) return false;

            await item.InteractAsync("Wield");
            return await Condition.WaitAsync(() => IsEquipped(itemName), timeoutMs: 2500);
        }

        /// <summary>
        /// Equips an item from the player's inventory by ID.
        /// </summary>
        public static async Task<bool> EquipAsync(int itemId)
        {
            if (IsEquipped(itemId)) return true;

            var item = Queries.Queries.Inventory.WithIds(itemId).First();
            if (item == null) return false;

            await item.InteractAsync("Wield");
            return await Condition.WaitAsync(() => IsEquipped(itemId), timeoutMs: 2500);
        }

        /// <summary>
        /// Unequips an item from the specified slot.
        /// </summary>
        public static async Task<bool> UnequipAsync(EquipmentSlot slot)
        {
            var item = GetItemInSlot(slot);
            if (item == null) return true;

            await item.InteractAsync("Remove");
            return await Condition.WaitAsync(() => GetItemInSlot(slot) == null, timeoutMs: 2500);
        }
    }
}
