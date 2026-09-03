using System;
using System.Threading.Tasks;
using OsrsMr.Core.Input;
using OsrsMr.Core.Scripting;

namespace OsrsMr.Core.Interaction
{
    /// <summary>
    /// High-level camera rotation, pitch, and orientation controller.
    /// </summary>
    public static class Camera
    {
        private static GameState State => BrainEngine.Instance.State;

        public static int Pitch => State.Camera.Pitch;
        public static int Yaw => State.Camera.Yaw;

        /// <summary>
        /// Rotates the camera to face a target world tile.
        /// </summary>
        public static async Task TurnToAsync(int worldX, int worldY)
        {
            if (State?.Player == null) return;

            int dx = worldX - State.Player.WorldX;
            int dy = worldY - State.Player.WorldY;

            // Compute angle in degrees (0 = North, 90 = East, 180 = South, 270 = West)
            double angleRad = Math.Atan2(dx, dy);
            double angleDeg = (angleRad * (180.0 / Math.PI) + 360.0) % 360.0;

            // Convert to OSRS yaw units (0 - 2047)
            int targetYaw = (int)(angleDeg / 360.0 * 2048.0);
            await SetYawAsync(targetYaw);
        }

        /// <summary>
        /// Turns camera towards an NPC.
        /// </summary>
        public static async Task TurnToAsync(NpcSnapshot npc)
        {
            if (npc != null)
            {
                await TurnToAsync(npc.WorldX, npc.WorldY);
            }
        }

        /// <summary>
        /// Turns camera towards a scene object.
        /// </summary>
        public static async Task TurnToAsync(SceneObjectSnapshot obj)
        {
            if (obj != null)
            {
                await TurnToAsync(obj.WorldX, obj.WorldY);
            }
        }

        /// <summary>
        /// Rotates the camera towards the target yaw angle using keyboard arrow keys.
        /// </summary>
        public static async Task SetYawAsync(int targetYaw)
        {
            targetYaw &= 2047;
            int currentYaw = Yaw & 2047;
            int diff = targetYaw - currentYaw;

            // Shortest rotational distance
            if (diff > 1024) diff -= 2048;
            if (diff < -1024) diff += 2048;

            if (Math.Abs(diff) < 40) return; // already aligned

            IntPtr hWnd = Win32Input.GetClientHandle();
            int key = diff > 0 ? Win32Input.VK_RIGHT : Win32Input.VK_LEFT;
            int pressDuration = Math.Min(1200, Math.Max(100, Math.Abs(diff) * 2));

            Win32Input.SendKeyDown(hWnd, key);
            await Task.Delay(pressDuration);
            Win32Input.SendKeyUp(hWnd, key);
        }

        /// <summary>
        /// Adjusts camera pitch up or down.
        /// </summary>
        public static async Task SetPitchAsync(bool pitchUp, int durationMs = 300)
        {
            IntPtr hWnd = Win32Input.GetClientHandle();
            int key = pitchUp ? Win32Input.VK_UP : Win32Input.VK_DOWN;

            Win32Input.SendKeyDown(hWnd, key);
            await Task.Delay(durationMs);
            Win32Input.SendKeyUp(hWnd, key);
        }
    }
}
