using System;
using System.Threading;
using OsrsMr.Core.Input;

namespace OsrsMr.Core.Input
{
    /// <summary>
    /// Anti-ban humanization engine simulating natural human variances, fatigue, micro-breaks, and camera adjustments.
    /// </summary>
    public static class Antiban
    {
        private static readonly Random Rng = new();

        /// <summary>
        /// Generates human-like delay with Gaussian normal distribution around the target mean.
        /// </summary>
        public static int HumanDelay(int meanMs, double stdDev = 0.25)
        {
            double u1 = 1.0 - Rng.NextDouble();
            double u2 = 1.0 - Rng.NextDouble();
            double randStdNormal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
            double delay = meanMs + (stdDev * meanMs * randStdNormal);
            return (int)Math.Max(50, Math.Round(delay));
        }

        /// <summary>
        /// Returns a randomized integer delay between min and max milliseconds.
        /// </summary>
        public static int RandomDelay(int minMs, int maxMs)
        {
            return Rng.Next(minMs, maxMs + 1);
        }

        /// <summary>
        /// Randomly performs small human idling / micro-breaks based on probability.
        /// </summary>
        public static bool MaybeMicroBreak(double probability = 0.05, int minMs = 500, int maxMs = 2500)
        {
            if (Rng.NextDouble() < probability)
            {
                int sleepTime = Rng.Next(minMs, maxMs);
                Thread.Sleep(sleepTime);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Simulates camera pitch or yaw adjustments via arrow key simulation.
        /// </summary>
        public static void RotateCameraRandomly(IntPtr targetHwnd)
        {
            if (targetHwnd == IntPtr.Zero) return;

            // Arrow keys: VK_LEFT = 0x25, VK_RIGHT = 0x27, VK_UP = 0x26, VK_DOWN = 0x28
            int key = Rng.Next(4) switch
            {
                0 => 0x25,
                1 => 0x27,
                2 => 0x26,
                _ => 0x28
            };

            int holdDuration = Rng.Next(80, 260);
            Win32Input.SendKeyDown(targetHwnd, key);
            Thread.Sleep(holdDuration);
            Win32Input.SendKeyUp(targetHwnd, key);
        }
    }
}
