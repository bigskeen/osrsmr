using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace OsrsMr.Core.Scripting
{
    /// <summary>
    /// Timing and synchronization primitives for bot scripts (polling conditions, waiting for animations, etc.).
    /// </summary>
    public static class Condition
    {
        private static readonly Random Rnd = new();

        /// <summary>
        /// Asynchronously blocks until the given predicate returns true or timeout expires.
        /// </summary>
        public static async Task<bool> WaitAsync(Func<bool> condition, int timeoutMs = 5000, int pollIntervalMs = 50)
        {
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                if (condition()) return true;
                await Task.Delay(pollIntervalMs);
            }
            return condition();
        }

        /// <summary>
        /// Synchronously blocks until the given predicate returns true or timeout expires.
        /// </summary>
        public static bool Wait(Func<bool> condition, int timeoutMs = 5000, int pollIntervalMs = 50)
        {
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                if (condition()) return true;
                Thread.Sleep(pollIntervalMs);
            }
            return condition();
        }

        /// <summary>
        /// Asynchronously sleeps for a fixed or randomized duration.
        /// </summary>
        public static async Task SleepAsync(int ms)
        {
            if (ms <= 0) return;
            await Task.Delay(ms);
        }

        /// <summary>
        /// Asynchronously sleeps for a randomized duration between minMs and maxMs.
        /// </summary>
        public static async Task SleepAsync(int minMs, int maxMs)
        {
            if (minMs > maxMs) (minMs, maxMs) = (maxMs, minMs);
            int duration = Rnd.Next(minMs, maxMs + 1);
            await Task.Delay(duration);
        }

        /// <summary>
        /// Synchronously sleeps for a randomized duration between minMs and maxMs.
        /// </summary>
        public static void Sleep(int minMs, int maxMs)
        {
            if (minMs > maxMs) (minMs, maxMs) = (maxMs, minMs);
            int duration = Rnd.Next(minMs, maxMs + 1);
            Thread.Sleep(duration);
        }

        /// <summary>
        /// Waits until the player becomes idle (no movement and no action animation).
        /// </summary>
        public static async Task<bool> WaitForPlayerIdleAsync(GameState state, int timeoutMs = 6000)
        {
            return await WaitAsync(() =>
            {
                if (state?.Player == null) return true;
                return !state.Player.IsMoving && (state.Player.Animation == -1 || state.Player.Animation == state.Player.PoseAnimation);
            }, timeoutMs, 100);
        }
    }
}
