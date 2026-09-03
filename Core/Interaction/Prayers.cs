using System;
using System.Threading.Tasks;
using OsrsMr.Core.Input;
using OsrsMr.Core.Queries;
using OsrsMr.Core.Scripting;

namespace OsrsMr.Core.Interaction
{
    public enum Prayer
    {
        ThickSkin,
        BurstOfStrength,
        ClarityOfThought,
        SharpEye,
        MysticWill,
        RockSkin,
        SuperhumanStrength,
        ImprovedReflexes,
        RapidRestore,
        RapidHeal,
        ProtectItem,
        HawkEye,
        MysticLore,
        SteelSkin,
        UltimateStrength,
        IncredibleReflexes,
        ProtectFromMagic,
        ProtectFromMissiles,
        ProtectFromMelee,
        EagleEye,
        MysticMight,
        Retribution,
        Redemption,
        Smite,
        Preserve,
        Chivalry,
        Piety,
        Rigour,
        Augury
    }

    /// <summary>
    /// Interaction controller for prayers, overhead protections, offensive prayers, and quick prayers.
    /// </summary>
    public static class Prayers
    {
        private static GameState State => BrainEngine.Instance.State;

        /// <summary>
        /// Checks whether a specific prayer is currently active.
        /// </summary>
        public static bool IsActive(Prayer prayer)
        {
            return State.ActivePrayers.Has(prayer.ToString());
        }

        /// <summary>
        /// Checks whether quick prayers are currently toggled on.
        /// </summary>
        public static bool IsQuickPrayerActive => State.ActivePrayers.Active.Contains("QuickPrayer");

        /// <summary>
        /// Returns current prayer points.
        /// </summary>
        public static int CurrentPoints => State.Player?.CurrentPrayer ?? 0;

        /// <summary>
        /// Toggles or activates a prayer.
        /// </summary>
        public static async Task<bool> SetActiveAsync(Prayer prayer, bool activate)
        {
            if (IsActive(prayer) == activate) return true;
            if (activate && CurrentPoints <= 0) return false;

            // Prayer tab / widget click:
            // Standard Prayer Tab Group 541 in OSRS
            var prayerWidget = Queries.Queries.Widgets
                .InGroup(541)
                .Filter(w => w.Name.Contains(prayer.ToString(), StringComparison.OrdinalIgnoreCase) ||
                             w.Text.Contains(prayer.ToString(), StringComparison.OrdinalIgnoreCase))
                .VisibleOnly()
                .First();

            if (prayerWidget != null)
            {
                await prayerWidget.ClickAsync();
            }
            else
            {
                // Fallback: Open prayer tab (F5 / standard UI slot) and click
                await Win32Input.SendKeyAsync(Win32Input.VK_F5);
                await Condition.SleepAsync(100, 200);
            }

            return await Condition.WaitAsync(() => IsActive(prayer) == activate, timeoutMs: 1500);
        }

        /// <summary>
        /// Toggles Quick Prayers on or off (Minimap orb click approx 567, 118).
        /// </summary>
        public static async Task<bool> ToggleQuickPrayersAsync(bool enable)
        {
            if (IsQuickPrayerActive == enable) return true;
            if (enable && CurrentPoints <= 0) return false;

            // Click Quick Prayer Orb on minimap border (approx 567, 118)
            await Mouse.ClickAsync(567, 118);
            return await Condition.WaitAsync(() => IsQuickPrayerActive == enable, timeoutMs: 1500);
        }
    }
}
