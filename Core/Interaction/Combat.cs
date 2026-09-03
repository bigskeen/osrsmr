using System;
using System.Linq;
using System.Threading.Tasks;
using OsrsMr.Core.Data;
using OsrsMr.Core.Input;
using OsrsMr.Core.Scripting;

namespace OsrsMr.Core.Interaction
{
    /// <summary>
    /// High-level combat controller (special attack, auto-retaliate, eating, quick prayers).
    /// </summary>
    public static class Combat
    {
        private static GameState State => BrainEngine.Instance.State;

        public static bool IsInCombat => State.Player.IsInCombat || State.Player.IsFighting;
        public static bool IsUnderAttack => State.Player.IsUnderAttack;
        public static int SpecialAttackPercent => State.Player.SpecPercent;
        public static bool IsSpecialAttackActive => State.Player.IsSpecActive;
        public static bool IsAutoRetaliateActive => State.StatusEffects.AutoRetaliate;

        /// <summary>
        /// Returns player current health percentage (0 - 100).
        /// </summary>
        public static int GetHealthPercent()
        {
            if (State?.Player == null || State.Player.MaxHp <= 0) return 100;
            return (int)((State.Player.CurrentHp * 100.0) / Math.Max(1, State.Player.MaxHp));
        }

        /// <summary>
        /// Toggles Special Attack on or off.
        /// Fixed special attack orb position: approx (595, 442) or combat tab spec bar (595, 440).
        /// </summary>
        public static async Task<bool> ToggleSpecialAttackAsync(bool enable = true)
        {
            if (IsSpecialAttackActive == enable) return true;

            // Click special attack orb (approx 595, 442)
            await Mouse.ClickAsync(595, 442);
            await Condition.SleepAsync(150, 300);
            return IsSpecialAttackActive == enable;
        }

        /// <summary>
        /// Toggles Quick Prayers on or off.
        /// Quick prayer orb position: approx (566, 95).
        /// </summary>
        public static async Task<bool> ToggleQuickPrayersAsync(bool enable = true)
        {
            bool currentlyActive = State.ActivePrayers.Active.Count > 0;
            if (currentlyActive == enable) return true;

            await Mouse.ClickAsync(566, 95);
            await Condition.SleepAsync(150, 300);
            return true;
        }

        /// <summary>
        /// Toggles Auto Retaliate on or off.
        /// </summary>
        public static async Task<bool> ToggleAutoRetaliateAsync(bool enable = true)
        {
            if (IsAutoRetaliateActive == enable) return true;

            // Combat tab button (approx 542, 186) -> auto retaliate button (approx 642, 385)
            await Mouse.ClickAsync(542, 186); // Combat options tab
            await Condition.SleepAsync(100, 200);
            await Mouse.ClickAsync(642, 385); // Auto retaliate box
            await Condition.SleepAsync(150, 300);
            return true;
        }

        /// <summary>
        /// Eats any available food in the inventory to restore health, or food matching the optional specified food name.
        /// </summary>
        public static async Task<bool> EatFoodAsync(string? foodName = null)
        {
            return await FoodCatalog.EatFoodAsync(foodName);
        }

        /// <summary>
        /// Drinks a prayer restore potion (Prayer potion, Super restore).
        /// </summary>
        public static async Task<bool> DrinkPrayerPotionAsync()
        {
            var pot = State.Inventory.Values
                .FirstOrDefault(i => i.Id > 0 && (i.Name.Contains("Prayer potion", StringComparison.OrdinalIgnoreCase) ||
                                                 i.Name.Contains("Super restore", StringComparison.OrdinalIgnoreCase)));

            if (pot == null) return false;

            await pot.InteractAsync("Drink");
            return true;
        }
    }
}
