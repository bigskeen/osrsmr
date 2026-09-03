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
        public int XpToNextLevel => Data.ExperienceTable.GetXpToNextLevel(Experience, Level);
        public double ProgressPercentage => Data.ExperienceTable.GetProgressPercentage(Experience, Level);
        public int NextLevel => Math.Min(Data.ExperienceTable.MaxVirtualLevel, Math.Max(Level, Data.ExperienceTable.GetLevelForExperience(Experience)) + 1);
        public int XpForNextLevel => Data.ExperienceTable.GetExperienceForLevel(NextLevel);
    }

    public class PlayerSnapshot
    {
        public string Name { get; set; } = "Unknown";
        public string Town { get; set; } = "Gielinor";
        public string Location { get; set; } = "";
        public int RegionId { get; set; }
        public int WorldX { get; set; }
        public int WorldY { get; set; }
        public int Plane { get; set; }
        public int CombatLevel { get; set; } = 3;
        public int TotalLevel { get; set; } = 32;
        public long TotalExperience { get; set; } = 1154;
        public int Animation { get; set; } = -1;
        public int PoseAnimation { get; set; } = -1;
        public int Graphic { get; set; } = -1;
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
        public string InteractingType { get; set; } = "None";
        public int InteractingId { get; set; } = -1;
        public bool IsInCombat { get; set; }
        public bool IsFighting { get; set; }
        public bool IsAttacking { get; set; }
        public string CombatTarget { get; set; } = "None";
        public int TargetCombatLevel { get; set; }
        public string TargetHealth { get; set; } = "None";
        public int TargetDistance { get; set; }
        public string EnemyPrayer { get; set; } = "None";
        public string EnemyAttackStyle { get; set; } = "None";
        public bool IsUnderAttack { get; set; }
        public string UnderAttackBy { get; set; } = "None";
        public bool IsMoving { get; set; }
        public bool IsIdle { get; set; } = true;
        public bool IsInstanced { get; set; }
        public bool IsVengeanceActive { get; set; }
        public int WildernessLevel { get; set; }
        public bool IsInWilderness { get; set; }
        public string EnemyWeapon { get; set; } = "None";
        public string EnemyGear { get; set; } = "None";
        public int EnemyAnimation { get; set; } = -1;
        public int EnemyPoseAnimation { get; set; } = -1;
    }

    public class AttackingEnemySnapshot
    {
        public int Index { get; set; }
        public string Name { get; set; } = "Unknown";
        public int CombatLevel { get; set; }
        public string Health { get; set; } = "100%";
        public int Distance { get; set; }
        public string Prayer { get; set; } = "None";
        public string AttackStyle { get; set; } = "Melee";
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
        public string Health { get; set; } = "100%";
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
        public int Plane { get; set; }
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

    public class NearbyPlayerSnapshot
    {
        public int Index { get; set; }
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public int CombatLevel { get; set; }
        public int Distance { get; set; }
    }

    public class CameraSnapshot
    {
        public int Pitch { get; set; }
        public int Yaw { get; set; }
        public int Zoom { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int Z { get; set; }
        public int CanvasWidth { get; set; }
        public int CanvasHeight { get; set; }
        public int ViewportWidth { get; set; }
        public int ViewportHeight { get; set; }
        public int ViewportOffsetX { get; set; }
        public int ViewportOffsetY { get; set; }
        public int Scale { get; set; }
    }

    public class StatusEffectsSnapshot
    {
        public bool IsPoisoned { get; set; }
        public bool IsEnvenomed { get; set; }
        public int PoisonDamage { get; set; }
        public int VenomDamage { get; set; }
        public int VenomImmunityTicks { get; set; }
        public int PoisonImmunityTicks { get; set; }
        public int AntifireTicks { get; set; }
        public int SuperAntifireTicks { get; set; }
        public bool IsSuperAntifire { get; set; }
        public int StaminaTicks { get; set; }
        public int OverloadTicks { get; set; }
        public int DivineTicks { get; set; }
        public int ImbuedHeartCooldownTicks { get; set; }
        public int PrayerEnhanceTicks { get; set; }
        public int ChargeTicks { get; set; }
        public int FreezeTicks { get; set; }
        public bool AutoRetaliate { get; set; } = true;
        public bool RunEnabled { get; set; } = true;

        public bool HasStamina => StaminaTicks > 0;
        public bool HasAntifire => AntifireTicks > 0 || SuperAntifireTicks > 0;
        public bool HasOverload => OverloadTicks > 0;
        public bool HasDivine => DivineTicks > 0;
        public bool IsImbuedHeartReady => ImbuedHeartCooldownTicks <= 0;
        public bool HasPrayerEnhance => PrayerEnhanceTicks > 0;
        public bool HasCharge => ChargeTicks > 0;
        public bool HasImmunity => PoisonImmunityTicks > 0 || VenomImmunityTicks > 0;

        public string StaminaDurationFormatted => FormatTicks(StaminaTicks);
        public string AntifireDurationFormatted => FormatTicks(Math.Max(AntifireTicks, SuperAntifireTicks));
        public string OverloadDurationFormatted => FormatTicks(OverloadTicks);
        public string DivineDurationFormatted => FormatTicks(DivineTicks);
        public string ImbuedHeartCooldownFormatted => FormatTicks(ImbuedHeartCooldownTicks);
        public string PrayerEnhanceDurationFormatted => FormatTicks(PrayerEnhanceTicks);
        public string ImmunityDurationFormatted => FormatTicks(Math.Max(PoisonImmunityTicks, VenomImmunityTicks));

        private static string FormatTicks(int ticks)
        {
            if (ticks <= 0) return "0:00";
            int totalSeconds = (int)(ticks * 0.6);
            int m = totalSeconds / 60;
            int s = totalSeconds % 60;
            return $"{m}:{s:D2}";
        }
    }

    public class ActivePrayersSnapshot
    {
        public HashSet<string> Active { get; } = new(StringComparer.OrdinalIgnoreCase);

        public bool Has(string prayerName) => Active.Contains(prayerName);
    }

    public class GrandExchangeOfferSnapshot
    {
        public int Slot { get; set; }
        public string State { get; set; } = "Empty"; // Empty, Buying, Bought, Selling, Sold, Cancelled
        public int ItemId { get; set; }
        public string ItemName { get; set; } = "";
        public int Price { get; set; }
        public int TotalQuantity { get; set; }
        public int QuantityTransferred { get; set; }
        public int Spent { get; set; }
    }

    public class ProjectileSnapshot
    {
        public int Id { get; set; }
        public int StartX { get; set; }
        public int StartY { get; set; }
        public int TargetX { get; set; }
        public int TargetY { get; set; }
        public int TargetIndex { get; set; }
        public int Plane { get; set; }
        public int RemainingCycles { get; set; }
        public int EndCycle { get; set; }
    }

    public class GraphicsObjectSnapshot
    {
        public int Id { get; set; }
        public int WorldX { get; set; }
        public int WorldY { get; set; }
        public int Plane { get; set; }
        public int StartCycle { get; set; }
        public int Level { get; set; }
    }

    public class RunePouchSlotSnapshot
    {
        public int Slot { get; set; }
        public int RuneId { get; set; }
        public string RuneName { get; set; } = "None";
        public int Quantity { get; set; }
    }

    public class GemBagSnapshot
    {
        public int Sapphires { get; set; }
        public int Emeralds { get; set; }
        public int Rubies { get; set; }
        public int Diamonds { get; set; }
        public int Dragonstones { get; set; }
        public int TotalGems => Sapphires + Emeralds + Rubies + Diamonds + Dragonstones;
    }

    public class EssencePouchesSnapshot
    {
        public int Small { get; set; }
        public int Medium { get; set; }
        public int Large { get; set; }
        public int Giant { get; set; }
        public int Colossal { get; set; }
        public int TotalEssence => Small + Medium + Large + Giant + Colossal;
    }

    public class EquipmentBonusesSnapshot
    {
        public int AttackStab { get; set; }
        public int AttackSlash { get; set; }
        public int AttackCrush { get; set; }
        public int AttackMagic { get; set; }
        public int AttackRange { get; set; }
        public int DefenceStab { get; set; }
        public int DefenceSlash { get; set; }
        public int DefenceCrush { get; set; }
        public int DefenceMagic { get; set; }
        public int DefenceRange { get; set; }
        public int MeleeStrength { get; set; }
        public int RangedStrength { get; set; }
        public int MagicDamage { get; set; }
        public int PrayerBonus { get; set; }
    }

    public class MenuEntrySnapshot
    {
        public int Index { get; set; }
        public string Option { get; set; } = "";
        public string Target { get; set; } = "";
        public int Identifier { get; set; }
        public int Opcode { get; set; }
        public int Param0 { get; set; }
        public int Param1 { get; set; }
    }

    public class WidgetSnapshot
    {
        public int Id { get; set; }
        public int GroupId { get; set; }
        public int ChildId { get; set; }
        public string Text { get; set; } = "";
        public string Name { get; set; } = "";
        public bool IsHidden { get; set; }
        public int BoundsX { get; set; }
        public int BoundsY { get; set; }
        public int BoundsWidth { get; set; }
        public int BoundsHeight { get; set; }
        public int ItemId { get; set; } = -1;
        public int ItemQuantity { get; set; }
        public List<string> Actions { get; set; } = new();
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
        public CameraSnapshot Camera { get; } = new();
        public StatusEffectsSnapshot StatusEffects { get; } = new();
        public ActivePrayersSnapshot ActivePrayers { get; } = new();
        public EquipmentBonusesSnapshot EquipmentBonuses { get; } = new();

        public ConcurrentDictionary<string, SkillSnapshot> Skills { get; } = new(StringComparer.OrdinalIgnoreCase);
        public ConcurrentDictionary<int, ItemSnapshot> Inventory { get; } = new();
        public ConcurrentDictionary<int, ItemSnapshot> Equipment { get; } = new();
        public ConcurrentDictionary<int, ItemSnapshot> EnemyEquipment { get; } = new();
        public ConcurrentDictionary<int, ItemSnapshot> Bank { get; } = new();
        public ConcurrentDictionary<int, ItemSnapshot> Shop { get; } = new();
        public ConcurrentDictionary<int, NpcSnapshot> Npcs { get; } = new();
        public ConcurrentDictionary<int, SceneObjectSnapshot> Objects { get; } = new();
        public ConcurrentDictionary<string, GroundItemSnapshot> GroundItems { get; } = new();
        public ConcurrentDictionary<int, FishingSpotSnapshot> FishingSpots { get; } = new();
        public ConcurrentDictionary<int, NearbyPlayerSnapshot> NearbyPlayers { get; } = new();
        public ConcurrentDictionary<int, SceneObjectSnapshot> AgilityObstacles { get; } = new();
        public ConcurrentDictionary<int, SceneObjectSnapshot> Shortcuts { get; } = new();
        public ConcurrentDictionary<int, AttackingEnemySnapshot> AttackingEnemies { get; } = new();
        public ConcurrentDictionary<int, GrandExchangeOfferSnapshot> GrandExchangeOffers { get; } = new();
        public ConcurrentDictionary<int, ProjectileSnapshot> Projectiles { get; } = new();
        public ConcurrentDictionary<string, GraphicsObjectSnapshot> GraphicsObjects { get; } = new();
        public ConcurrentDictionary<int, RunePouchSlotSnapshot> RunePouch { get; } = new();
        public ConcurrentDictionary<int, ItemSnapshot> LootingBag { get; } = new();
        public GemBagSnapshot GemBag { get; } = new();
        public EssencePouchesSnapshot EssencePouches { get; } = new();
        public ConcurrentDictionary<int, WidgetSnapshot> Widgets { get; } = new();
        public List<MenuEntrySnapshot> MenuEntries { get; set; } = new();
        public ConcurrentDictionary<int, int> Varbits { get; } = new();
        public ConcurrentDictionary<int, int> Varps { get; } = new();

        public bool IsBankOpen { get; set; }
        public bool IsShopOpen { get; set; }
        public bool IsGrandExchangeOpen { get; set; }
        public bool IsDepositBoxOpen { get; set; }
        public string CurrentBank { get; set; } = "None";
        public string NearestBank { get; set; } = "None";
        public int NearestBankDistance { get; set; } = -1;
        public bool InBank { get; set; }
        public string CurrentShop { get; set; } = "None";
        public string NearestShop { get; set; } = "None";
        public int NearestShopDistance { get; set; } = -1;
        public bool InShop { get; set; }
        public string ShopLocation { get; set; } = "General Store";
        public string ShopName { get; set; } = "General Store";
        public bool IsLoggedIn { get; set; } = true;
        public bool IsInstanced { get; set; }
        public int GameTick { get; set; }
        public int WorldNumber { get; set; }
        public string EngineState { get; set; } = "Logged In";
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    }
}
