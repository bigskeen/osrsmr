using System;
using System.Drawing;
using System.Threading.Tasks;
using OsrsMr.Core.Spatial;

namespace OsrsMr.Core.Input
{
    /// <summary>
    /// High-level humanized mouse interaction controller.
    /// Moves and clicks targets using WindMouse trajectory curves.
    /// </summary>
    public static class Mouse
    {
        private static readonly Random Rnd = new();
        public static Point CurrentPosition { get; private set; } = new(0, 0);

        public static async Task MoveAsync(int targetX, int targetY)
        {
            IntPtr hWnd = Win32Input.GetClientHandle();
            var trajectory = WindMouse.GenerateTrajectory(CurrentPosition, new Point(targetX, targetY));

            foreach (var pt in trajectory)
            {
                Win32Input.SendMouseMove(hWnd, pt.X, pt.Y);
                CurrentPosition = pt;
                await Task.Delay(Rnd.Next(3, 9));
            }
        }

        public static async Task ClickAsync(int x, int y, bool rightClick = false)
        {
            IntPtr hWnd = Win32Input.GetClientHandle();
            await MoveAsync(x, y);
            await Task.Delay(Rnd.Next(40, 100)); // reaction delay

            if (rightClick)
            {
                Win32Input.SendRightClick(hWnd, x, y);
            }
            else
            {
                Win32Input.SendLeftClick(hWnd, x, y);
            }
        }

        public static async Task ClickAsync(Polygon2D polygon, bool rightClick = false)
        {
            if (polygon == null || polygon.Points.Count == 0) return;
            var center = polygon.GetCenter();
            int offsetX = Rnd.Next(-3, 4);
            int offsetY = Rnd.Next(-3, 4);
            await ClickAsync(center.X + offsetX, center.Y + offsetY, rightClick);
        }
    }
}
