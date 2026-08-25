using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace OsrsMr.Core
{
    public class SkillSnapshot
    {
        public int Level { get; set; } = 1;
        public int BoostedLevel { get; set; } = 1;
        public int Experience { get; set; } = 0;
    }

    public class PlayerSnapshot
    {
        public string Name { get; set; } = "Unknown";
        public int WorldX { get; set; }
        public int WorldY { get; set; }
        public int Plane { get; set; }
        public int CombatLevel { get; set; } = 3;
        public int Animation { get; set; } = -1;
        public int CurrentHp { get; set; } = 10;
        public int MaxHp { get; set; } = 10;
        public int CurrentPrayer { get; set; } = 1;
        public int MaxPrayer { get; set; } = 1;
        public int Energy { get; set; } = 100;
        public int Weight { get; set; } = 0;
        public int SpecPercent { get; set; } = 100;
        public bool IsSpecActive { get; set; }
        public string Spellbook { get; set; } = "Standard";
        public string AutocastSpell { get; set; } = "None";
        public string ActiveTab { get; set; } = "Inventory";
        public bool IsInteracting { get; set; }
        public string InteractingName { get; set; } = "";
    }

    public class ItemSnapshot
    {
        public int Slot { get; set; }
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public int Quantity { get; set; }
        public int HighAlchValue { get; set; }
        public int GePrice { get; set; }
    }

    public class NpcSnapshot
    {
        public int Index { get; set; }
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public int CombatLevel { get; set; }
        public int Distance { get; set; }
        public int WorldX { get; set; }
        public int WorldY { get; set; }
        public int Animation { get; set; } = -1;
        public int CurrentHp { get; set; } = -1;
        public int MaxHp { get; set; } = -1;
        public string Role { get; set; } = "NPC";
        public bool IsInteractingWithMe { get; set; }
    }

    public class SceneObjectSnapshot
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Category { get; set; } = "Object"; // Tree, Bank, Shop, Altar, Rock, Obstacle, Shortcut
        public int Distance { get; set; }
        public int WorldX { get; set; }
        public int WorldY { get; set; }
        public int Plane { get; set; }
        public string Status { get; set; } = "Available";
        public string RequiredLevel { get; set; } = "1";
    }

    public class GroundItemSnapshot
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public int Quantity { get; set; } = 1;
        public int Distance { get; set; }
        public int WorldX { get; set; }
        public int WorldY { get; set; }
        public int GePrice { get; set; }
    }

    public class DialogSnapshot
    {
        public bool IsOpen { get; set; }
        public string Type { get; set; } = "None"; // NPC, Player, Options, Sprite, Message
        public string Speaker { get; set; } = "";
        public string Text { get; set; } = "";
        public List<string> Options { get; set; } = new();
    }

    public class SlayerSnapshot
    {
        public string TaskName { get; set; } = "None";
        public int AmountRemaining { get; set; }
        public int InitialAmount { get; set; }
        public string Master { get; set; } = "None";
        public int Streak { get; set; }
        public int Points { get; set; }
    }

    public class MinigameSnapshot
    {
        public bool IsActive { get; set; }
        public string Name { get; set; } = "None";
        public string Status { get; set; } = "";
        public string Points { get; set; } = "";
        public string Extra { get; set; } = "";
    }

    public class AgilitySnapshot
    {
        public string CurrentCourse { get; set; } = "None";
        public int CourseLevelReq { get; set; }
        public int MarksOfGraceNearby { get; set; }
    }

    public class FishingSpotSnapshot
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string SpotType { get; set; } = "";
        public int Distance { get; set; }
        public int WorldX { get; set; }
        public int WorldY { get; set; }
    }

    /// <summary>
    /// Thread-safe game state container updated continuously by the Brain network parser.
    /// </summary>
    public class GameState
    {
        public PlayerSnapshot Player { get; } = new();
        public DialogSnapshot Dialog { get; } = new();
        public SlayerSnapshot Slayer { get; } = new();
        public MinigameSnapshot Minigame { get; } = new();
        public AgilitySnapshot Agility { get; } = new();

        public ConcurrentDictionary<string, SkillSnapshot> Skills { get; } = new(StringComparer.OrdinalIgnoreCase);
        public ConcurrentDictionary<int, ItemSnapshot> Inventory { get; } = new();
        public ConcurrentDictionary<int, ItemSnapshot> Equipment { get; } = new();
        public ConcurrentDictionary<int, ItemSnapshot> Bank { get; } = new();
        public ConcurrentDictionary<int, ItemSnapshot> Shop { get; } = new();
        public ConcurrentDictionary<int, NpcSnapshot> Npcs { get; } = new();
        public ConcurrentDictionary<int, SceneObjectSnapshot> Objects { get; } = new();
        public ConcurrentDictionary<string, GroundItemSnapshot> GroundItems { get; } = new();
        public ConcurrentDictionary<int, FishingSpotSnapshot> FishingSpots { get; } = new();
        public ConcurrentDictionary<int, SceneObjectSnapshot> AgilityObstacles { get; } = new();
        public ConcurrentDictionary<int, SceneObjectSnapshot> Shortcuts { get; } = new();
        public ConcurrentDictionary<int, int> Varbits { get; } = new();
        public ConcurrentDictionary<int, int> Varps { get; } = new();

        public bool IsBankOpen { get; set; }
        public bool IsShopOpen { get; set; }
        public bool IsLoggedIn { get; set; } = true;
        public int GameTick { get; set; }
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    }
}
