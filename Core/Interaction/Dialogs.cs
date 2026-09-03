using System;
using System.Linq;
using System.Threading.Tasks;
using OsrsMr.Core.Input;
using OsrsMr.Core.Queries;
using OsrsMr.Core.Scripting;

namespace OsrsMr.Core.Interaction
{
    /// <summary>
    /// Interaction controller for NPC dialogs, conversation options, make-X menus, and quest cutscenes.
    /// </summary>
    public static class Dialogs
    {
        private static GameState State => BrainEngine.Instance.State;

        /// <summary>
        /// Checks whether a dialog prompt (continue / click to continue) is active.
        /// </summary>
        public static bool CanContinue()
        {
            return Queries.Queries.Widgets
                .VisibleOnly()
                .Filter(w => w.Text.Contains("Click here to continue", StringComparison.OrdinalIgnoreCase) ||
                             w.Text.Contains("click to continue", StringComparison.OrdinalIgnoreCase) ||
                             w.Text.Contains("Press space to continue", StringComparison.OrdinalIgnoreCase))
                .Any();
        }

        /// <summary>
        /// Checks whether multi-choice dialog options are currently displayed.
        /// </summary>
        public static bool IsChoiceOpen()
        {
            // Standard multi-choice dialog widget group 219 or options containing Select an Option
            return Queries.Queries.Widgets
                .InGroup(219)
                .VisibleOnly()
                .Any() ||
                Queries.Queries.Widgets
                .VisibleOnly()
                .Filter(w => w.Text.Contains("Select an Option", StringComparison.OrdinalIgnoreCase))
                .Any();
        }

        /// <summary>
        /// Checks whether any dialog is currently open.
        /// </summary>
        public static bool IsOpen() => CanContinue() || IsChoiceOpen() || IsMakeInterfaceOpen();

        /// <summary>
        /// Checks whether the modern Make-X / Make-All skilling interface (Group 270) is open.
        /// </summary>
        public static bool IsMakeInterfaceOpen()
        {
            return Queries.Queries.Widgets
                .InGroup(270)
                .VisibleOnly()
                .Any() ||
                Queries.Queries.Widgets
                .VisibleOnly()
                .Filter(w => w.Text.Contains("What would you like to make?", StringComparison.OrdinalIgnoreCase) ||
                             w.Text.Contains("How many would you like to", StringComparison.OrdinalIgnoreCase))
                .Any();
        }

        /// <summary>
        /// Confirms Make-All in the active Make-X interface by sending Spacebar or clicking the prompt.
        /// </summary>
        public static async Task<bool> ConfirmMakeAllAsync()
        {
            if (!IsMakeInterfaceOpen()) return false;
            await Win32Input.SendKeyAsync(Win32Input.VK_SPACE);
            await Condition.SleepAsync(300, 500);
            return true;
        }

        /// <summary>
        /// Continues through the current conversation dialog by sending Space or clicking the continue widget.
        /// </summary>
        public static async Task<bool> ContinueAsync()
        {
            if (!CanContinue()) return false;

            // Spacebar advances 99% of modern OSRS dialogs
            await Win32Input.SendKeyAsync(Win32Input.VK_SPACE);
            await Condition.SleepAsync(250, 400);
            return true;
        }

        /// <summary>
        /// Selects a conversation option matching the specified text substring.
        /// </summary>
        public static async Task<bool> SelectOptionAsync(string optionText)
        {
            if (string.IsNullOrEmpty(optionText)) return false;

            var optionWidget = Queries.Queries.Widgets
                .VisibleOnly()
                .Filter(w => w.Text.Contains(optionText, StringComparison.OrdinalIgnoreCase))
                .First();

            if (optionWidget == null) return false;

            await optionWidget.ClickAsync();
            await Condition.SleepAsync(300, 500);
            return true;
        }

        /// <summary>
        /// Selects a conversation option by its 1-based index (e.g. Option 1, Option 2).
        /// </summary>
        public static async Task<bool> SelectOptionIndexAsync(int index)
        {
            if (index < 1 || index > 9) return false;

            // Virtual key codes for digits '1'-'9' are 0x31 to 0x39
            int vkCode = 0x30 + index;
            await Win32Input.SendKeyAsync(vkCode);
            await Condition.SleepAsync(300, 500);
            return true;
        }

        /// <summary>
        /// Continuously clicks continue through linear NPC conversations until a choice is reached or the dialog finishes.
        /// </summary>
        public static async Task<bool> CompleteDialogueChainAsync(int maxSteps = 15)
        {
            for (int i = 0; i < maxSteps; i++)
            {
                if (!IsOpen()) break;
                if (IsChoiceOpen()) return true;

                if (CanContinue())
                {
                    await ContinueAsync();
                    await Condition.SleepAsync(400, 600);
                }
                else
                {
                    break;
                }
            }

            return !IsOpen() || IsChoiceOpen();
        }
    }
}
