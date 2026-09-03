using System;
using System.Linq;
using System.Threading.Tasks;
using OsrsMr.Core.Input;
using OsrsMr.Core.Profiles;
using OsrsMr.Core.Queries;
using OsrsMr.Core.Scripting;

namespace OsrsMr.Core.Interaction
{
    /// <summary>
    /// Automated Bank PIN interface handler.
    /// Detects the OSRS Bank PIN screen (Group 213) and inputs digits securely.
    /// </summary>
    public static class BankPin
    {
        private static GameState State => BrainEngine.Instance.State;

        public const int BankPinGroupId = 213;

        /// <summary>
        /// Indicates whether the Bank PIN interface is currently open and visible.
        /// </summary>
        public static bool IsOpen
        {
            get
            {
                return State.Widgets.Values.Any(w => w.GroupId == BankPinGroupId && !w.IsHidden)
                    || State.Widgets.Values.Any(w => !w.IsHidden && (
                        w.Text.Contains("FIRST DIGIT", StringComparison.OrdinalIgnoreCase) ||
                        w.Text.Contains("SECOND DIGIT", StringComparison.OrdinalIgnoreCase) ||
                        w.Text.Contains("THIRD DIGIT", StringComparison.OrdinalIgnoreCase) ||
                        w.Text.Contains("FOURTH DIGIT", StringComparison.OrdinalIgnoreCase) ||
                        w.Text.Contains("Bank of Gielinor", StringComparison.OrdinalIgnoreCase)));
            }
        }

        /// <summary>
        /// Automatically enters the PIN configured in the active account profile.
        /// </summary>
        public static async Task<bool> EnterActiveProfilePinAsync()
        {
            var profile = ProfileManager.Instance.ActiveProfile;
            if (string.IsNullOrWhiteSpace(profile?.BankPin))
            {
                return false;
            }

            return await EnterPinAsync(profile.BankPin);
        }

        /// <summary>
        /// Enters a 4-digit bank PIN by identifying and clicking the shuffled on-screen buttons.
        /// </summary>
        public static async Task<bool> EnterPinAsync(string pin)
        {
            if (string.IsNullOrWhiteSpace(pin) || pin.Length != 4)
            {
                return false;
            }

            if (!IsOpen)
            {
                return false;
            }

            for (int i = 0; i < 4; i++)
            {
                if (!IsOpen)
                {
                    // Bank pin screen may have closed early or bank opened
                    if (Bank.IsOpen) return true;
                    return false;
                }

                char digitChar = pin[i];
                string digitStr = digitChar.ToString();

                bool clicked = await ClickDigitButtonAsync(digitStr);
                if (!clicked)
                {
                    // Retry once after slight delay
                    await Condition.SleepAsync(400, 600);
                    clicked = await ClickDigitButtonAsync(digitStr);
                }

                if (!clicked)
                {
                    return false;
                }

                // Randomized human reaction delay between button presses
                await Condition.SleepAsync(650, 1100);
            }

            // Wait for bank to open or PIN screen to dismiss
            return await Condition.WaitAsync(() => Bank.IsOpen || !IsOpen, timeoutMs: 3000);
        }

        /// <summary>
        /// Finds the widget corresponding to the requested digit and clicks it.
        /// </summary>
        private static async Task<bool> ClickDigitButtonAsync(string digit)
        {
            // 1. Check for visible widget matching exact digit text
            var digitWidget = Queries.Queries.Widgets
                .InGroup(BankPinGroupId)
                .VisibleOnly()
                .Filter(w => w.Text.Trim() == digit || w.Actions.Any(a => a.Contains(digit)))
                .First();

            if (digitWidget != null && digitWidget.BoundsWidth > 0 && digitWidget.BoundsHeight > 0)
            {
                await digitWidget.ClickAsync();
                return true;
            }

            // 2. Check for child text widgets matching the single digit
            var fallbackWidget = Queries.Queries.Widgets
                .VisibleOnly()
                .Filter(w => w.GroupId == BankPinGroupId || (w.Id >> 16) == BankPinGroupId)
                .Filter(w => w.Text.Trim() == digit)
                .First();

            if (fallbackWidget != null)
            {
                await fallbackWidget.ClickAsync();
                return true;
            }

            return false;
        }
    }
}
