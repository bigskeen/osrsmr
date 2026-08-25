using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using OsrsMr.Api.Input;
using OsrsMr.Core;

namespace OsrsMr.Api.Entities
{
    public class LocatableEntity
    {
        public int WorldX { get; set; }
        public int WorldY { get; set; }
        public int Plane { get; set; }
        public int Distance { get; set; }

        public double DistanceTo(LocatableEntity other)
        {
            if (other == null) return double.MaxValue;
            int dx = WorldX - other.WorldX;
            int dy = WorldY - other.WorldY;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        public virtual async Task<bool> InteractAsync(string action = "Click", CancellationToken ct = default)
        {
            await HumanInput.SimulateHumanDelayAsync(200, 400, ct);
            return true;
        }
    }

    public class PlayerEntity : LocatableEntity
    {
        public string Name { get; set; } = "";
        public int CombatLevel { get; set; }
        public int CurrentHp { get; set; }
        public int MaxHp { get; set; }
        public int CurrentPrayer { get; set; }
        public int MaxPrayer { get; set; }
        public int Energy { get; set; }
        public int RunEnergy => Energy;
        public int Weight { get; set; }
        public int SpecPercent { get; set; }
        public bool IsSpecActive { get; set; }
        public bool IsPoisoned { get; set; }
        public string Spellbook { get; set; } = "Standard";
        public string AutocastSpell { get; set; } = "None";
        public string ActiveTab { get; set; } = "Inventory";
        public int Animation { get; set; } = -1;
        public bool IsInteracting { get; set; }
        public string InteractingName { get; set; } = "";
        public bool IsMoving => Animation == 1205 || Animation == 1210 || Animation == 819 || Animation == 824;
        public bool IsIdle => Animation == -1 || Animation == 808 || Animation == 813;
    }

    public class NpcEntity : LocatableEntity
    {
        public int Index { get; set; }
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public int CombatLevel { get; set; }
        public int Animation { get; set; } = -1;
        public string Role { get; set; } = "NPC";
        public bool IsInteractingWithMe { get; set; }
        public bool IsInteracting { get; set; }

        public bool IsBanker => Role.Equals("Banker", StringComparison.OrdinalIgnoreCase) || Name.Contains("Banker", StringComparison.OrdinalIgnoreCase);
        public bool IsSlayerMaster => Role.Equals("Slayer Master", StringComparison.OrdinalIgnoreCase);
        public bool IsShopkeeper => Role.Equals("Shopkeeper", StringComparison.OrdinalIgnoreCase);

        public override async Task<bool> InteractAsync(string action = "Attack", CancellationToken ct = default)
        {
            await HumanInput.SimulateHumanDelayAsync(250, 450, ct);
            return true;
        }
    }

    public class GameObjectEntity : LocatableEntity
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Category { get; set; } = "Object"; // Tree, Bank, Shop, Altar, Rock, Obstacle, Shortcut
        public string Status { get; set; } = "Available";
        public string RequiredLevel { get; set; } = "1";

        public bool IsTree => Category.Equals("Tree", StringComparison.OrdinalIgnoreCase);
        public bool IsAvailableTree => IsTree && Status.Equals("Available", StringComparison.OrdinalIgnoreCase);
        public bool IsStump => IsTree && Status.Equals("Stump", StringComparison.OrdinalIgnoreCase);
        public bool IsBank => Category.Equals("Bank", StringComparison.OrdinalIgnoreCase) || Name.Contains("Bank", StringComparison.OrdinalIgnoreCase);

        public override async Task<bool> InteractAsync(string action = "Interact", CancellationToken ct = default)
        {
            await HumanInput.SimulateHumanDelayAsync(250, 500, ct);
            return true;
        }
    }

    public class GroundItemEntity : LocatableEntity
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public int Quantity { get; set; } = 1;
        public int GePrice { get; set; }

        public async Task<bool> TakeAsync(CancellationToken ct = default)
        {
            await HumanInput.SimulateHumanDelayAsync(200, 400, ct);
            return true;
        }
    }

    public class ItemEntity
    {
        public int Slot { get; set; }
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public int Quantity { get; set; }
        public int HighAlchValue { get; set; }
        public int GePrice { get; set; }
        public bool IsValid => Id > 0 && !string.IsNullOrEmpty(Name);

        public async Task<bool> InteractAsync(string action = "Click", CancellationToken ct = default)
        {
            await HumanInput.SimulateHumanDelayAsync(150, 300, ct);
            return true;
        }

        public async Task<bool> DropAsync(CancellationToken ct = default)
        {
            await HumanInput.SimulateHumanDelayAsync(120, 250, ct);
            return true;
        }
    }

    public class ShortcutEntity : LocatableEntity
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public int RequiredLevel { get; set; } = 1;
    }

    public class AgilityObstacleEntity : LocatableEntity
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Course { get; set; } = "None";

        public override async Task<bool> InteractAsync(string action = "Climb", CancellationToken ct = default)
        {
            await HumanInput.SimulateHumanDelayAsync(300, 600, ct);
            return true;
        }

        public async Task<bool> TraverseAsync(CancellationToken ct = default)
        {
            return await InteractAsync("Climb", ct);
        }
    }

    public class FishingSpotEntity : LocatableEntity
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string SpotType { get; set; } = "";

        public override async Task<bool> InteractAsync(string action = "Fish", CancellationToken ct = default)
        {
            await HumanInput.SimulateHumanDelayAsync(250, 500, ct);
            return true;
        }
    }
}
