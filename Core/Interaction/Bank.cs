using System;
using System.Linq;
using System.Threading.Tasks;
using OsrsMr.Core.Input;
using OsrsMr.Core.Queries;
using OsrsMr.Core.Scripting;

namespace OsrsMr.Core.Interaction
{
    /// <summary>
    /// High-level bank interface controller (open, deposit, withdraw, close).
    /// </summary>
    public static class Bank
    {
        private static GameState State => BrainEngine.Instance.State;

        /// <summary>
        /// Returns whether the bank interface is currently open.
        /// </summary>
        public static bool IsOpen => State.IsBankOpen || State.InBank || State.Widgets.Values.Any(w => w.GroupId == 12 && !w.IsHidden);

        /// <summary>
        /// Attempts to open the nearest bank booth or banker NPC.
        /// </summary>
        public static async Task<bool> OpenAsync()
        {
            if (IsOpen) return true;

            // 1. Look for nearest Bank object
            var bankObj = Queries.Queries.Objects
                .Filter(o => (!string.IsNullOrEmpty(o.Category) && o.Category.Equals("Bank", StringComparison.OrdinalIgnoreCase)) ||
                             o.Name.Contains("Bank booth", StringComparison.OrdinalIgnoreCase) ||
                             o.Name.Contains("Bank chest", StringComparison.OrdinalIgnoreCase) ||
                             o.Name.Contains("Grand Exchange booth", StringComparison.OrdinalIgnoreCase) ||
                             o.Name.Contains("Bank counter", StringComparison.OrdinalIgnoreCase) ||
                             o.Name.Contains("Bank table", StringComparison.OrdinalIgnoreCase) ||
                             o.Name.Equals("Bank", StringComparison.OrdinalIgnoreCase) ||
                             o.Name.Equals("Chest", StringComparison.OrdinalIgnoreCase) && o.Distance <= 6)
                .WithinDistance(12)
                .Nearest();

            if (bankObj != null)
            {
                // Try "Bank" action, fallback to "Open" or first available interaction
                bool interacted = await bankObj.InteractAsync("Bank");
                if (!interacted)
                {
                    interacted = await bankObj.InteractAsync("Open");
                }
                if (!interacted)
                {
                    interacted = await bankObj.InteractAsync("Use");
                }

                bool opened = await Condition.WaitAsync(() => IsOpen || BankPin.IsOpen, timeoutMs: 4000);
                if (BankPin.IsOpen)
                {
                    await BankPin.EnterActiveProfilePinAsync();
                }
                return IsOpen;
            }

            // 2. Fallback to Banker NPC
            var banker = Queries.Queries.Npcs
                .Filter(n => n.Name.Contains("Banker", StringComparison.OrdinalIgnoreCase) ||
                             n.Name.Contains("Ghost banker", StringComparison.OrdinalIgnoreCase) ||
                             n.Name.Contains("Emerald Benedict", StringComparison.OrdinalIgnoreCase) ||
                             n.Name.Equals("Bank", StringComparison.OrdinalIgnoreCase))
                .WithinDistance(12)
                .Nearest();

            if (banker != null)
            {
                bool interacted = await banker.InteractAsync("Bank");
                if (!interacted)
                {
                    interacted = await banker.InteractAsync("Talk-to");
                }

                bool opened = await Condition.WaitAsync(() => IsOpen || BankPin.IsOpen, timeoutMs: 4000);
                if (BankPin.IsOpen)
                {
                    await BankPin.EnterActiveProfilePinAsync();
                }
                return IsOpen;
            }

            return false;
        }

        /// <summary>
        /// Clicks the "Deposit inventory" widget button.
        /// </summary>
        public static async Task<bool> DepositAllAsync()
        {
            if (!IsOpen) return false;

            // Widget 12, child 42 is typically Deposit All Inventory in OSRS
            var depositAllWidget = Queries.Queries.Widgets
                .InGroup(12)
                .WithChildId(42)
                .VisibleOnly()
                .First();

            if (depositAllWidget != null)
            {
                await depositAllWidget.ClickAsync();
                await Condition.SleepAsync(300, 600);
                return true;
            }

            // Fallback: Click the standard deposit inventory screen button area (approx 446, 336)
            await Mouse.ClickAsync(446, 336);
            await Condition.SleepAsync(300, 600);
            return true;
        }

        /// <summary>
        /// Deposits all items except the specified whitelist IDs.
        /// </summary>
        public static async Task<bool> DepositAllExceptAsync(params int[] keepItemIds)
        {
            if (!IsOpen) return false;

            var itemsToDeposit = State.Inventory.Values
                .Where(i => i.Id > 0 && !keepItemIds.Contains(i.Id))
                .ToList();

            foreach (var item in itemsToDeposit)
            {
                await item.InteractAsync("Deposit-All");
                await Condition.SleepAsync(150, 300);
            }

            return true;
        }

        /// <summary>
        /// Deposits all items except items whose names contain any of the specified whitelisted substrings.
        /// </summary>
        public static async Task<bool> DepositAllExceptAsync(params string[] keepItemNames)
        {
            if (!IsOpen) return false;

            var itemsToDeposit = State.Inventory.Values
                .Where(i => i.Id > 0 && !keepItemNames.Any(k => i.Name.Contains(k, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            foreach (var item in itemsToDeposit)
            {
                await item.InteractAsync("Deposit-All");
                await Condition.SleepAsync(150, 300);
            }

            return true;
        }

        /// <summary>
        /// Withdraws an item by name with smart quantity action selection.
        /// </summary>
        public static async Task<bool> WithdrawAsync(string itemName, int quantity = 1)
        {
            if (!IsOpen) return false;

            var bankItem = State.Bank.Values
                .FirstOrDefault(i => i.Name.Equals(itemName, StringComparison.OrdinalIgnoreCase))
                ?? State.Bank.Values.FirstOrDefault(i => i.Name.Contains(itemName, StringComparison.OrdinalIgnoreCase));

            if (bankItem == null) return false;

            string action = quantity switch
            {
                <= 0 => "Withdraw-All",
                1 => "Withdraw-1",
                5 => "Withdraw-5",
                10 => "Withdraw-10",
                >= 28 => "Withdraw-All",
                _ => $"Withdraw-{quantity}"
            };

            bool interacted = await bankItem.InteractAsync(action);
            if (!interacted)
            {
                // Fallback to standard withdraw
                interacted = await bankItem.InteractAsync("Withdraw-1");
            }

            await Condition.SleepAsync(200, 450);
            return interacted;
        }

        /// <summary>
        /// Closes the bank interface.
        /// </summary>
        public static async Task<bool> CloseAsync()
        {
            if (!IsOpen) return true;

            // Widget 12, child 3 is typically Close Bank button (ESC or (482, 40))
            var closeWidget = Queries.Queries.Widgets
                .InGroup(12)
                .WithChildId(3)
                .VisibleOnly()
                .First();

            if (closeWidget != null)
            {
                await closeWidget.ClickAsync();
            }
            else
            {
                // Fallback top right close button (approx 482, 40)
                await Mouse.ClickAsync(482, 40);
            }

            return await Condition.WaitAsync(() => !IsOpen, timeoutMs: 2500);
        }
    }
}
