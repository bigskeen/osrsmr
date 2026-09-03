using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace OsrsMr.Core.Input
{
    public static class Win32Input
    {
        public const uint WM_MOUSEMOVE = 0x0200;
        public const uint WM_LBUTTONDOWN = 0x0201;
        public const uint WM_LBUTTONUP = 0x0202;
        public const uint WM_RBUTTONDOWN = 0x0204;
        public const uint WM_RBUTTONUP = 0x0205;
        public const uint WM_KEYDOWN = 0x0100;
        public const uint WM_KEYUP = 0x0101;

        public const int VK_LEFT = 0x25;
        public const int VK_UP = 0x26;
        public const int VK_RIGHT = 0x27;
        public const int VK_DOWN = 0x28;
        public const int VK_SPACE = 0x20;
        public const int VK_ESCAPE = 0x1B;
        public const int VK_RETURN = 0x0D;
        public const int VK_F1 = 0x70;
        public const int VK_F2 = 0x71;
        public const int VK_F3 = 0x72;
        public const int VK_F4 = 0x73;
        public const int VK_F5 = 0x74;
        public const int VK_F6 = 0x75;
        public const int VK_F7 = 0x76;
        public const int VK_F8 = 0x77;

        public const int MK_LBUTTON = 0x0001;
        public const int MK_RBUTTON = 0x0002;

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string? lpszClass, string? lpszWindow);

        public static IntPtr MakeLParam(int x, int y)
        {
            return (IntPtr)((y << 16) | (x & 0xFFFF));
        }

        /// <summary>
        /// Finds the RuneLite client window handle.
        /// </summary>
        public static IntPtr GetClientHandle()
        {
            var processes = Process.GetProcessesByName("RuneLite");
            if (processes.Length > 0 && processes[0].MainWindowHandle != IntPtr.Zero)
            {
                return processes[0].MainWindowHandle;
            }

            processes = Process.GetProcessesByName("javaw");
            if (processes.Length > 0 && processes[0].MainWindowHandle != IntPtr.Zero)
            {
                return processes[0].MainWindowHandle;
            }

            return IntPtr.Zero;
        }

        public static void SendMouseMove(IntPtr hWnd, int x, int y)
        {
            if (hWnd == IntPtr.Zero) return;
            PostMessage(hWnd, WM_MOUSEMOVE, IntPtr.Zero, MakeLParam(x, y));
        }

        public static void SendLeftClick(IntPtr hWnd, int x, int y)
        {
            if (hWnd == IntPtr.Zero) return;
            PostMessage(hWnd, WM_LBUTTONDOWN, (IntPtr)MK_LBUTTON, MakeLParam(x, y));
            PostMessage(hWnd, WM_LBUTTONUP, IntPtr.Zero, MakeLParam(x, y));
        }

        public static void SendRightClick(IntPtr hWnd, int x, int y)
        {
            if (hWnd == IntPtr.Zero) return;
            PostMessage(hWnd, WM_RBUTTONDOWN, (IntPtr)MK_RBUTTON, MakeLParam(x, y));
            PostMessage(hWnd, WM_RBUTTONUP, IntPtr.Zero, MakeLParam(x, y));
        }

        public static void SendKeyDown(IntPtr hWnd, int virtualKeyCode)
        {
            if (hWnd == IntPtr.Zero) return;
            PostMessage(hWnd, WM_KEYDOWN, (IntPtr)virtualKeyCode, IntPtr.Zero);
        }

        public static void SendKeyUp(IntPtr hWnd, int virtualKeyCode)
        {
            if (hWnd == IntPtr.Zero) return;
            PostMessage(hWnd, WM_KEYUP, (IntPtr)virtualKeyCode, IntPtr.Zero);
        }

        public static async System.Threading.Tasks.Task SendKeyAsync(int virtualKeyCode, int durationMs = 50)
        {
            var hWnd = GetClientHandle();
            if (hWnd == IntPtr.Zero) return;
            SendKeyDown(hWnd, virtualKeyCode);
            await System.Threading.Tasks.Task.Delay(durationMs);
            SendKeyUp(hWnd, virtualKeyCode);
        }
    }
}
