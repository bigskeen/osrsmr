using System;
using System.Linq;

namespace OsrsMr.Core.Queries
{
    /// <summary>
    /// Global entry points for querying game entities in a clean, declarative syntax.
    /// </summary>
    public static class Queries
    {
        private static GameState State => BrainEngine.Instance.State;

        public static NpcQuery Npcs => new(State.Npcs.Values);
        public static GameObjectQuery Objects => new(State.Objects.Values);
        public static GroundItemQuery GroundItems => new(State.GroundItems.Values);
        public static InventoryQuery Inventory => new(State.Inventory.Values);
        public static EquipmentQuery Equipment => new(State.Equipment.Values);
        public static InventoryQuery Bank => new(State.Bank.Values);
        public static InventoryQuery Shop => new(State.Shop.Values);
        public static WidgetQuery Widgets => new(State.Widgets.Values);
        public static ProjectileQuery Projectiles => new(State.Projectiles.Values);
        public static PlayerQuery Players => new(State.NearbyPlayers.Values);
    }
}
