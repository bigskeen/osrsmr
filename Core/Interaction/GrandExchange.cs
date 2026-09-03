using System;
using System.Linq;
using System.Threading.Tasks;
using OsrsMr.Core.Input;
using OsrsMr.Core.Queries;
using OsrsMr.Core.Scripting;

namespace OsrsMr.Core.Interaction
{
    /// <summary>
    /// Interaction controller for the Grand Exchange (trading, buying, selling, collecting).
    /// </summary>
    public static class GrandExchange
    {
        private static GameState State => BrainEngine.Instance.State;

        /// <summary>
        /// Checks whether the Grand Exchange interface is currently open.
        /// </summary>
        public static bool IsOpen => State.Widgets.Values.Any(w => w.GroupId == 465 && !w.IsHidden);

        /// <summary>
        /// Opens the Grand Exchange via the nearest GE clerk or booth.
        /// </summary>
        public static async Task<bool> OpenAsync()
        {
            if (IsOpen) return true;

            var clerk = Queries.Queries.Npcs
                .Filter(n => n.Name.Contains("Grand Exchange Clerk", StringComparison.OrdinalIgnoreCase))
                .Nearest();

            if (clerk != null)
            {
                await clerk.InteractAsync("Exchange");
                return await Condition.WaitAsync(() => IsOpen, timeoutMs: 4000);
            }

            var booth = Queries.Queries.Objects
                .Filter(o => o.Name.Contains("Grand Exchange booth", StringComparison.OrdinalIgnoreCase))
                .Nearest();

            if (booth != null)
            {
                await booth.InteractAsync("Exchange");
                return await Condition.WaitAsync(() => IsOpen, timeoutMs: 4000);
            }

            return false;
        }

        /// <summary>
        /// Clicks the "Collect to inventory" button in the Grand Exchange interface.
        /// </summary>
        public static async Task<bool> CollectAllAsync()
        {
            if (!IsOpen) return false;

            // Widget 465, child 6 is typically Collect to Inventory
            var collectBtn = Queries.Queries.Widgets
                .InGroup(465)
                .WithChildId(6)
                .VisibleOnly()
                .First();

            if (collectBtn != null)
            {
                await collectBtn.ClickAsync();
                await Condition.SleepAsync(250, 500);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Closes the Grand Exchange interface.
        /// </summary>
        public static async Task<bool> CloseAsync()
        {
            if (!IsOpen) return true;

            await Win32Input.SendKeyAsync(Win32Input.VK_ESCAPE);
            return await Condition.WaitAsync(() => !IsOpen, timeoutMs: 2000);
        }
    }
}
