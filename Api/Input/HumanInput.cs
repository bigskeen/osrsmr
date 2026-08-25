using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace OsrsMr.Api.Input
{
    public static class HumanInput
    {
        private static readonly Random _rand = new();

        [DllImport("user32.dll")]
        private static extern bool SetCursorPos(int X, int Y);

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, int dwExtraInfo);

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, int dwExtraInfo);

        private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        private const uint MOUSEEVENTF_LEFTUP = 0x0004;
        private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
        private const uint MOUSEEVENTF_RIGHTUP = 0x0010;
        private const uint KEYEVENTF_KEYUP = 0x0002;

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }

        public static Point CurrentPosition
        {
            get
            {
                GetCursorPos(out POINT p);
                return new Point(p.X, p.Y);
            }
        }

        public static int NextGaussian(int mean, int stdDev, int min = 10, int max = 10000)
        {
            double u1 = 1.0 - _rand.NextDouble();
            double u2 = 1.0 - _rand.NextDouble();
            double randStdNormal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
            double randNormal = mean + stdDev * randStdNormal;
            return Math.Clamp((int)randNormal, min, max);
        }

        public static async Task DelayAsync(int meanMs, int stdDevMs, CancellationToken ct = default)
        {
            int delay = NextGaussian(meanMs, stdDevMs);
            await Task.Delay(delay, ct);
        }

        public static async Task MoveMouseAsync(Point destination, int speedMultiplier = 1, CancellationToken ct = default)
        {
            Point start = CurrentPosition;
            var path = GenerateBezierPath(start, destination);

            foreach (var pt in path)
            {
                if (ct.IsCancellationRequested) break;
                SetCursorPos(pt.X, pt.Y);
                int sleep = NextGaussian(3 * speedMultiplier, 1, 1, 15);
                await Task.Delay(sleep, ct);
            }

            SetCursorPos(destination.X, destination.Y);
        }

        public static async Task ClickAsync(bool rightClick = false, CancellationToken ct = default)
        {
            uint down = rightClick ? MOUSEEVENTF_RIGHTDOWN : MOUSEEVENTF_LEFTDOWN;
            uint up = rightClick ? MOUSEEVENTF_RIGHTUP : MOUSEEVENTF_LEFTUP;

            mouse_event(down, 0, 0, 0, 0);
            await Task.Delay(NextGaussian(65, 15, 30, 150), ct);
            mouse_event(up, 0, 0, 0, 0);
        }

        public static async Task MoveAndClickAsync(Point target, int variance = 3, bool rightClick = false, CancellationToken ct = default)
        {
            int targetX = target.X + _rand.Next(-variance, variance + 1);
            int targetY = target.Y + _rand.Next(-variance, variance + 1);

            await MoveMouseAsync(new Point(targetX, targetY), 1, ct);
            await DelayAsync(80, 25, ct);
            await ClickAsync(rightClick, ct);
        }

        public static async Task SendKeyAsync(byte vkCode, CancellationToken ct = default)
        {
            keybd_event(vkCode, 0, 0, 0);
            await Task.Delay(NextGaussian(55, 12, 25, 120), ct);
            keybd_event(vkCode, 0, KEYEVENTF_KEYUP, 0);
        }

        public static async Task PressKeyAsync(char key, CancellationToken ct = default)
        {
            byte vk = (byte)char.ToUpperInvariant(key);
            if (key == ' ') vk = 0x20; // VK_SPACE
            else if (key == '\n' || key == '\r') vk = 0x0D; // VK_RETURN
            else if (key == '\t') vk = 0x09; // VK_TAB
            else if (key == 27) vk = 0x1B; // VK_ESCAPE

            await SendKeyAsync(vk, ct);
        }

        public static async Task SimulateHumanDelayAsync(int minMs, int maxMs, CancellationToken ct = default)
        {
            int mean = (minMs + maxMs) / 2;
            int stdDev = Math.Max(20, (maxMs - minMs) / 6);
            await DelayAsync(mean, stdDev, ct);
        }

        private static List<Point> GenerateBezierPath(Point start, Point end)
        {
            var points = new List<Point>();
            double distance = Math.Sqrt(Math.Pow(end.X - start.X, 2) + Math.Pow(end.Y - start.Y, 2));
            int steps = Math.Max((int)(distance / 12), 15);

            // Control points for cubic Bézier curve
            int ctrlX1 = start.X + (end.X - start.X) / 3 + _rand.Next(-30, 30);
            int ctrlY1 = start.Y + (end.Y - start.Y) / 3 + _rand.Next(-30, 30);
            int ctrlX2 = start.X + 2 * (end.X - start.X) / 3 + _rand.Next(-20, 20);
            int ctrlY2 = start.Y + 2 * (end.Y - start.Y) / 3 + _rand.Next(-20, 20);

            for (int i = 0; i <= steps; i++)
            {
                double t = (double)i / steps;
                // Ease-in-out curve parameter
                double easedT = t * t * (3 - 2 * t);

                double x = Math.Pow(1 - easedT, 3) * start.X +
                           3 * Math.Pow(1 - easedT, 2) * easedT * ctrlX1 +
                           3 * (1 - easedT) * Math.Pow(easedT, 2) * ctrlX2 +
                           Math.Pow(easedT, 3) * end.X;

                double y = Math.Pow(1 - easedT, 3) * start.Y +
                           3 * Math.Pow(1 - easedT, 2) * easedT * ctrlY1 +
                           3 * (1 - easedT) * Math.Pow(easedT, 2) * ctrlY2 +
                           Math.Pow(easedT, 3) * end.Y;

                points.Add(new Point((int)x, (int)y));
            }

            return points;
        }
    }
}
