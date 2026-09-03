using System;
using System.Drawing;
using System.Threading.Tasks;
using OsrsMr.Core.Input;
using OsrsMr.Core.Spatial;

namespace OsrsMr.Core.Interaction
{
    /// <summary>
    /// Extension methods providing RuneMate-like .Interact() / .Click() on game entities.
    /// </summary>
    public static class EntityInteractionExtensions
    {
        private static readonly Random Rnd = new();
        private static GameState State => BrainEngine.Instance.State;

        /// <summary>
        /// Attempts to click or interact with an NPC using 3D-to-2D viewport projection.
        /// </summary>
        public static async Task<bool> InteractAsync(this NpcSnapshot npc, string action = "Attack")
        {
            if (npc == null || State?.Player == null) return false;

            var screenPoint = Viewport.WorldToCanvas(
                npc.WorldX, npc.WorldY, 0,
                State.Player.WorldX, State.Player.WorldY,
                State.Camera);

            if (!screenPoint.HasValue) return false;

            // Add slight natural randomization to click point
            int clickX = screenPoint.Value.X + Rnd.Next(-6, 7);
            int clickY = screenPoint.Value.Y + Rnd.Next(-10, 5);

            await Mouse.ClickAsync(clickX, clickY, rightClick: false);
            return true;
        }

        /// <summary>
        /// Attempts to click or interact with a Scene/Game Object (rock, tree, bank booth, door).
        /// </summary>
        public static async Task<bool> InteractAsync(this SceneObjectSnapshot obj, string action = "Use")
        {
            if (obj == null || State?.Player == null) return false;

            var screenPoint = Viewport.WorldToCanvas(
                obj.WorldX, obj.WorldY, obj.Plane,
                State.Player.WorldX, State.Player.WorldY,
                State.Camera);

            if (!screenPoint.HasValue) return false;

            int clickX = screenPoint.Value.X + Rnd.Next(-8, 9);
            int clickY = screenPoint.Value.Y + Rnd.Next(-8, 9);

            await Mouse.ClickAsync(clickX, clickY, rightClick: false);
            return true;
        }

        /// <summary>
        /// Attempts to click or pick up a ground item.
        /// </summary>
        public static async Task<bool> TakeAsync(this GroundItemSnapshot item)
        {
            if (item == null || State?.Player == null) return false;

            var screenPoint = Viewport.WorldToCanvas(
                item.WorldX, item.WorldY, item.Plane,
                State.Player.WorldX, State.Player.WorldY,
                State.Camera);

            if (!screenPoint.HasValue) return false;

            await Mouse.ClickAsync(screenPoint.Value.X, screenPoint.Value.Y, rightClick: false);
            return true;
        }

        /// <summary>
        /// Clicks a widget or interface component on screen.
        /// </summary>
        public static async Task<bool> ClickAsync(this WidgetSnapshot widget)
        {
            if (widget == null || widget.IsHidden || widget.BoundsWidth <= 0 || widget.BoundsHeight <= 0)
                return false;

            int clickX = widget.BoundsX + (widget.BoundsWidth / 2) + Rnd.Next(-Math.Max(1, widget.BoundsWidth / 4), Math.Max(2, widget.BoundsWidth / 4));
            int clickY = widget.BoundsY + (widget.BoundsHeight / 2) + Rnd.Next(-Math.Max(1, widget.BoundsHeight / 4), Math.Max(2, widget.BoundsHeight / 4));

            await Mouse.ClickAsync(clickX, clickY, rightClick: false);
            return true;
        }

        /// <summary>
        /// Interacts with an inventory slot.
        /// </summary>
        public static async Task<bool> InteractAsync(this ItemSnapshot item, string action = "Use")
        {
            if (item == null) return false;

            // Standard OSRS Inventory Grid: 4 columns x 7 rows
            // Inventory tab starting offset in fixed viewport: approx (563, 213), cell 42x36
            int col = item.Slot % 4;
            int row = item.Slot / 4;
            int startX = 563;
            int startY = 213;
            int slotWidth = 42;
            int slotHeight = 36;

            int targetX = startX + (col * slotWidth) + (slotWidth / 2) + Rnd.Next(-6, 7);
            int targetY = startY + (row * slotHeight) + (slotHeight / 2) + Rnd.Next(-6, 7);

            bool isRightClick = action.Equals("Drop", StringComparison.OrdinalIgnoreCase) ||
                               action.Equals("Examine", StringComparison.OrdinalIgnoreCase);

            await Mouse.ClickAsync(targetX, targetY, isRightClick);
            return true;
        }
    }
}
